using System.Collections;
using NUnit.Framework;
using Unity.Entities;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Wassup.Battle.Units;
using Wassup.Bridge;
using Wassup.Core;
using Wassup.Core.TimeControl;
using Wassup.Data;

namespace Wassup.Tests.PlayMode
{
    // defender-board-limit — 판 위 동시 배치 상한의 라이브 판정.
    //
    // EditMode 는 값 해석(폴백)만 본다. 여기서 보는 것은 «세는 것이 판 상태에서 파생된다» 는
    // 계약 자체다: 상한에 닿으면 거부되고, 유닛이 죽어 자리가 비면 **아무 리셋 훅 없이** 다시
    // 배치된다. 카운터를 따로 들었다면 바로 이 축에서 어긋난다.
    //
    // 유닛은 카탈로그 에셋의 **런타임 사본**을 쓴다 — 상한을 만지려고 에셋을 직접 수정하면
    // 에디터에서 그 값이 디스크에 박힌다(시트 드리프트와 같은 사고).
    public class BoardLimitPlacementTest
    {
        [TearDown]
        public void TearDown() => LogAssert.ignoreFailingMessages = false;

        [UnityTearDown]
        public IEnumerator UnityTearDown()
        {
            TimeManager.Instance.ResetAll();
            PrimeTween.Tween.StopAll();
            yield return null;
        }

        [UnityTest]
        public IEnumerator Limit1_BlocksSecondPlacement_AndFreesOnDeath()
        {
            LogAssert.ignoreFailingMessages = true;
            yield return SceneManager.LoadSceneAsync(SceneNames.Battle, LoadSceneMode.Single);
            for (int i = 0; i < 6; i++) yield return null;

            var bridge = Object.FindObjectOfType<BattleBridge>();
            Assert.IsNotNull(bridge, "BattleBridge present");
            var unit = MakeUnit("ranger", 1);
            Assert.AreEqual(1, unit.EffectiveMaxOnBoard, "상한 1 (라이브 기본값)");

            bridge.SetDefenderPool(new[] { unit });
            bridge.BeginPlacement();
            var gm = Object.FindObjectOfType<GameManager>();
            gm.CostRuntime.ResetToStart();
            gm.CostRuntime.AddCost(1000);
            yield return null;

            Assert.AreEqual(0, bridge.DeployedCountOf(unit), "시작 시 0기");
            Assert.IsFalse(bridge.TryGetDeployedEntity(unit, out _), "판에 없으면 목적지도 없다");

            Assert.IsTrue(PlaceFirstValid(bridge, unit, out var firstCell), "1기째 배치");
            Assert.AreEqual(1, bridge.DeployedCountOf(unit), "1기 카운트");
            Assert.IsTrue(bridge.TryGetDeployedEntity(unit, out var entity), "소진 셀의 목적지 해석");
            Assert.AreNotEqual(Entity.Null, entity);

            // 2기째 — 공간이 남아 있어도 상한이 막는다. 사유가 LimitReached 여야
            // 트레이가 "소진"과 "코스트 부족"을 구분해 그릴 수 있다.
            Assert.IsTrue(TryFindOtherValidCell(bridge, unit, firstCell, out var secondCell),
                "상한을 빼면 배치 가능한 다른 칸이 실제로 있다(테스트 전제)");
            Assert.IsFalse(bridge.CanPlaceDefenderAt(secondCell.x, secondCell.y, unit, out var reason),
                "2기째 거부");
            Assert.AreEqual(PlacementRejectReason.LimitReached, reason, "사유 = LimitReached");
            Assert.IsFalse(bridge.PlaceDefenderAs(secondCell.x, secondCell.y, unit), "커밋 경로도 막힌다");
            Assert.AreEqual(1, bridge.DeployedCountOf(unit), "거부는 카운트를 늘리지 않는다");

            // 사망 → 자리가 빈다. 리셋 훅은 없다 — 카운트가 _defenderByTile 파생이라
            // 바인딩이 지워지는 것만으로 상한이 풀린다.
            bridge.StartBattle(); // DrainDefenderDeathEvents 는 _running 게이트 아래에 있다
            yield return null;
            var em = World.DefaultGameObjectInjectionWorld.EntityManager;
            var health = em.GetComponentData<Health>(entity);
            em.SetComponentData(entity, new Health { value = 0f, max = health.max });
            for (int i = 0; i < 8; i++) yield return null;

            Assert.AreEqual(0, bridge.DeployedCountOf(unit), "사망으로 카운트 복귀");
            Assert.IsFalse(bridge.TryGetDeployedEntity(unit, out _), "죽은 뒤엔 목적지 없음");
            Assert.IsTrue(PlaceFirstValid(bridge, unit, out _), "빈 자리에 재배치 성공");
        }

        // 큰 수 = "지금과 동일 동작". 분기가 아니라 같은 비교식이 절대 참이 되지 않는 것으로
        // 성립하는지 본다(예외 코드가 생기면 이 테스트가 아니라 설계가 틀린 것).
        [UnityTest]
        public IEnumerator Limit100_BehavesAsUnlimited()
        {
            LogAssert.ignoreFailingMessages = true;
            yield return SceneManager.LoadSceneAsync(SceneNames.Battle, LoadSceneMode.Single);
            for (int i = 0; i < 6; i++) yield return null;

            var bridge = Object.FindObjectOfType<BattleBridge>();
            var unit = MakeUnit("ranger", 100);
            bridge.SetDefenderPool(new[] { unit });
            bridge.BeginPlacement();
            var gm = Object.FindObjectOfType<GameManager>();
            gm.CostRuntime.ResetToStart();
            gm.CostRuntime.AddCost(1000);
            yield return null;

            for (int n = 1; n <= 3; n++)
            {
                Assert.IsTrue(PlaceFirstValid(bridge, unit, out _), $"{n}기째 배치");
                Assert.AreEqual(n, bridge.DeployedCountOf(unit), $"{n}기 카운트");
            }
        }

        // 카탈로그 에셋의 런타임 사본 + 상한 저작. 사본이라 디스크에 안 남는다.
        private static DefenderUnitData MakeUnit(string id, int maxOnBoard)
        {
            var all = Resources.FindObjectsOfTypeAll<DefenderCatalog>();
            Assert.Greater(all.Length, 0, "DefenderCatalog present");
            var src = all[0].ById(id);
            Assert.IsNotNull(src, $"{id} in catalog");
            var copy = Object.Instantiate(src);
            copy.maxOnBoard = maxOnBoard;
            return copy;
        }

        private static bool PlaceFirstValid(BattleBridge bridge, DefenderUnitData u, out Vector2Int cell)
        {
            for (int x = -24; x < 48; x++)
                for (int y = -24; y < 48; y++)
                    if (bridge.CanPlaceDefenderAt(x, y, u, out _))
                    {
                        cell = new Vector2Int(x, y);
                        return bridge.PlaceDefenderAs(x, y, u);
                    }
            cell = default;
            return false;
        }

        // 상한을 뺀 공간 판정만으로 배치 가능한 다른 칸. 상한 100 짜리 프로브 사본으로 찾는다 —
        // "공간이 없어서 막힌 것" 과 "상한이 막은 것" 을 테스트가 혼동하지 않기 위함이다.
        private static bool TryFindOtherValidCell(BattleBridge bridge, DefenderUnitData u,
            Vector2Int exclude, out Vector2Int cell)
        {
            var probe = Object.Instantiate(u);
            probe.maxOnBoard = 100;
            var pool = bridge.DefenderPool;
            var probePool = new DefenderUnitData[pool.Length + 1];
            System.Array.Copy(pool, probePool, pool.Length);
            probePool[pool.Length] = probe;
            bridge.SetDefenderPool(probePool);

            cell = default;
            bool found = false;
            for (int x = -24; x < 48 && !found; x++)
                for (int y = -24; y < 48 && !found; y++)
                {
                    var c = new Vector2Int(x, y);
                    if (c == exclude) continue;
                    if (bridge.CanPlaceDefenderAt(x, y, probe, out _)) { cell = c; found = true; }
                }

            bridge.SetDefenderPool(pool);
            Object.DestroyImmediate(probe);
            return found;
        }
    }
}
