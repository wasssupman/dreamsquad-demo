using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.SceneManagement;
using Unity.Entities;
using Wassup.Core;
using Wassup.Core.TimeControl;
using Wassup.Bridge;
using Wassup.Data;
using Wassup.Battle.Units;

namespace Wassup.Tests.PlayMode
{
    // defender-clock-out unit 1 — 퇴근(자발적 퇴장)이 **사망 경로를 타지 않는다**는 것을 잡는다.
    //
    // 단정을 고른 기준: 사직서 0장 / 작별선물 0 / 각성 0 을 각각 세팅해 확인하면 기믹 매치 부팅과
    // OnDeath 카드 부착이 테스트의 대부분이 되는데, **그 셋은 전부 "DeadTag 가 붙고 DefenderDied 가
    // 쏘였나"에서 파생된다.** 그래서 `DefenderDied 0회`(여러 프레임에 걸쳐) 하나로 그 가족을 덮는다
    // — DeadTag 가 붙었다면 UnitLifecycleSystem 이 DefenderDeathEvent 를 넣고 드레인이 DefenderDied 를
    // 쏘기 때문이다. CLAUDE.md: "커버리지는 목표가 아니다. 회귀 방지 수준이면 충분하다."
    public class DefenderRetireTest
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
        public IEnumerator Retire_FreesTile_FiresRetiredNotDied_AndCellIsReusable()
        {
            LogAssert.ignoreFailingMessages = true;
            yield return SceneManager.LoadSceneAsync(SceneNames.Battle, LoadSceneMode.Single);
            for (int i = 0; i < 6; i++) yield return null;

            var bridge = Object.FindObjectOfType<BattleBridge>();
            Assert.IsNotNull(bridge, "BattleBridge present");
            var unit = FindCatalog().ById("ranger");
            Assert.IsNotNull(unit, "ranger in catalog");

            bridge.SetDefenderPool(new[] { unit });
            bridge.BeginPlacement();
            var gm = Object.FindObjectOfType<GameManager>();
            gm.CostRuntime.ResetToStart();
            gm.CostRuntime.AddCost(1000);
            yield return null;

            int retired = 0, died = 0;
            bridge.DefenderRetired += (_, __, ___) => retired++;
            bridge.DefenderDied += (_, __, ___) => died++;

            Assert.IsTrue(PlaceFirstValid(bridge, unit), "place defender");
            var em = World.DefaultGameObjectInjectionWorld.EntityManager;
            var cell = SoleCell(bridge);
            var entity = EntityAt(bridge, em, cell);
            Assert.AreNotEqual(Entity.Null, entity, "entity resolved");

            Assert.IsTrue(bridge.RetireDefender(cell), "retire succeeds on a live, landed defender");

            // 즉시 성립하는 것들 — 퇴근은 프레임을 기다리지 않는다(sim 왕복이 없다).
            Assert.AreEqual(Entity.Null, EntityAt(bridge, em, cell), "binding removed");
            Assert.IsFalse(em.Exists(entity), "entity destroyed by the bridge");
            Assert.AreEqual(1, retired, "DefenderRetired fired once");

            // 사망 결과 가족의 가드. DeadTag 가 붙었다면 UnitLifecycleSystem → DefenderDeathEvent →
            // 드레인 → DefenderDied 로 이어진다. 여러 프레임 지켜봐야 그 왕복을 덮는다.
            for (int i = 0; i < 8; i++) yield return null;
            Assert.AreEqual(0, died, "사망 경로에 진입하지 않는다 (사직서·작별선물·각성의 가드)");
            Assert.AreEqual(1, retired, "DefenderRetired 는 더 쏘이지 않는다");

            // 점유가 실제로 풀렸는가 — 상한 1 유닛이 그 칸에 다시 선다.
            Assert.IsTrue(bridge.CanPlaceDefenderAt(cell.x, cell.y, unit, out var reason),
                $"퇴근한 칸이 다시 배치 가능해야 한다 (reason={reason})");
        }

        // 비행 중(배치/재배치 착지 전)에는 내리지 않는다 — 뷰 오버라이드와 활성화 꼬리가 뜬다.
        [UnityTest]
        public IEnumerator Retire_RejectedWhilePendingDeployment()
        {
            LogAssert.ignoreFailingMessages = true;
            yield return SceneManager.LoadSceneAsync(SceneNames.Battle, LoadSceneMode.Single);
            for (int i = 0; i < 6; i++) yield return null;

            var bridge = Object.FindObjectOfType<BattleBridge>();
            var unit = FindCatalog().ById("ranger");
            bridge.SetDefenderPool(new[] { unit });
            bridge.BeginPlacement();
            var gm = Object.FindObjectOfType<GameManager>();
            gm.CostRuntime.ResetToStart();
            gm.CostRuntime.AddCost(1000);
            yield return null;

            // PlaceDefenderAs 는 pendingDeployment:false 라 즉시 활성이다. 비행 상태를 만들려면
            // 드래그 배치가 쓰는 TryBeginDefenderDeployment 로 들어가야 한다.
            Assert.IsTrue(BeginFirstValidDeployment(bridge, unit, out var cell, out var entity),
                "begin pending deployment");
            var em = World.DefaultGameObjectInjectionWorld.EntityManager;
            Assert.IsTrue(em.HasComponent<PendingDeployment>(entity), "still in flight");

            Assert.IsFalse(bridge.RetireDefender(cell), "비행 중에는 퇴근이 거부된다");
            Assert.AreEqual(entity, EntityAt(bridge, em, cell), "거부됐으므로 판에 그대로 남는다");

            // 착지시키면 열린다 — 거부가 영구가 아니라 상태 의존임을 같이 잡는다.
            bridge.ActivateDeployedDefender(cell, entity);
            Assert.IsTrue(bridge.RetireDefender(cell), "착지 후에는 퇴근된다");
        }

        // ── helpers (재배치 스위트와 동형) ────────────────────────────────────

        private static DefenderCatalog FindCatalog()
        {
            var all = Resources.FindObjectsOfTypeAll<DefenderCatalog>();
            return all.Length > 0 ? all[0] : null;
        }

        private static bool PlaceFirstValid(BattleBridge bridge, DefenderUnitData u)
        {
            for (int x = -24; x < 48; x++)
                for (int y = -24; y < 48; y++)
                    if (bridge.CanPlaceDefenderAt(x, y, u, out _))
                        return bridge.PlaceDefenderAs(x, y, u);
            return false;
        }

        private static bool BeginFirstValidDeployment(BattleBridge bridge, DefenderUnitData u,
            out Vector2Int cell, out Entity entity)
        {
            for (int x = -24; x < 48; x++)
                for (int y = -24; y < 48; y++)
                    if (bridge.CanPlaceDefenderAt(x, y, u, out _))
                    {
                        cell = new Vector2Int(x, y);
                        return bridge.TryBeginDefenderDeployment(x, y, u, out entity);
                    }
            cell = default; entity = Entity.Null;
            return false;
        }

        private static System.Collections.IDictionary ByTile(BattleBridge bridge)
        {
            var f = typeof(BattleBridge).GetField("_defenderByTile", BindingFlags.NonPublic | BindingFlags.Instance);
            return (System.Collections.IDictionary)f.GetValue(bridge);
        }

        private static Vector2Int SoleCell(BattleBridge bridge)
        {
            foreach (System.Collections.DictionaryEntry de in ByTile(bridge))
                return (Vector2Int)de.Key;
            return new Vector2Int(int.MinValue, int.MinValue);
        }

        private static Entity EntityAt(BattleBridge bridge, EntityManager em, Vector2Int cell)
        {
            var dict = ByTile(bridge);
            if (!dict.Contains(cell)) return Entity.Null;
            var val = dict[cell];
            var entity = (Entity)val.GetType().GetField("Item1").GetValue(val);
            return em.Exists(entity) ? entity : Entity.Null;
        }
    }
}
