using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.SceneManagement;
using Unity.Entities;
using Unity.Transforms;
using Unity.Mathematics;
using Wassup.Core;
using Wassup.Bridge;
using Wassup.Data;
using Wassup.Battle.Units;

namespace Wassup.Tests.PlayMode
{
    // beam-ranger-defender unit 1 — **빔이 실제로 그려지는지**를 보는 테스트.
    //
    // 왜 필요했나: 기존 버스터즈 테스트(HitscanDefenderTest / OnPlaceDotNearbyTest)는 ECS
    // 데미지만 봤다. 그런데 빔은 `BattleBridge.Update()` 의 드레인에서 구동되고, 그 Update 는
    // **`if (!_running) return;`** 로 막혀 있다. 즉 `StartBattle()` 없이는 빔 경로가 통째로
    // 안 도는데도 데미지 테스트는 전부 통과한다 — 실제로 이 구멍으로 "빔이 끊긴다" 결함이
    // green 을 뚫고 나갔다. 그래서 이 테스트는 반드시 StartBattle 후에 검증한다.
    //
    // 확인하는 것: 세션이 열리고, 빔 몸통이 **실제 사거리만큼 늘어나** 배치되는가.
    // (프리팹 원본 스케일 4.17 / 원본 위치 그대로면 배치가 한 번도 안 된 것이다.)
    public class BeamPresentationTest
    {
        [TearDown]
        public void TearDown() => LogAssert.ignoreFailingMessages = false;

        [UnityTest]
        public IEnumerator BeamUnit_Attacking_StretchesBeamBodyBetweenMuzzleAndTarget()
        {
            LogAssert.ignoreFailingMessages = true;
            yield return SceneManager.LoadSceneAsync(SceneNames.Battle, LoadSceneMode.Single);
            for (int i = 0; i < 6; i++) yield return null;

            var em = World.DefaultGameObjectInjectionWorld.EntityManager;
            var bridge = Object.FindObjectOfType<BattleBridge>();
            var gm = Object.FindObjectOfType<GameManager>();
            var busters = FindCatalog().ById("busters");
            Assert.IsNotNull(busters.beamVfxPrefab, "빔 유닛 판별자 = beamVfxPrefab. 비면 빔이 없다");

            bridge.SetDefenderPool(new[] { busters });
            bridge.BeginPlacement();
            gm.CostRuntime.ResetToStart();
            gm.CostRuntime.AddCost(100000);

            Vector2Int cell = FindPlaceableCell(bridge, busters);
            Assert.AreNotEqual(int.MinValue, cell.x, "placeable cell");
            Assert.IsTrue(bridge.PlaceDefenderAs(cell.x, cell.y, busters), "place busters");

            // ★ 이것이 없으면 드레인이 안 돌아 빔이 영원히 안 생긴다.
            bridge.StartBattle();

            var defender = FindDefender(bridge, em);
            var defPos = em.GetComponentData<LocalTransform>(defender).Position;
            var enemy = SpawnDummyEnemy(em, defPos + new float3(1.6f, 0f, 0f));

            // 세션이 열리고 배치될 때까지.
            Transform body = null;
            float t = 0f;
            while (t < 4f && body == null)
            {
                t += Time.deltaTime;
                var presenter = GameObject.Find("BeamPresenter (auto)");
                if (presenter != null && presenter.transform.childCount > 0)
                {
                    var beam = presenter.transform.GetChild(0);
                    if (beam.gameObject.activeSelf) body = beam.Find("BeamBody");
                }
                yield return null;
            }

            Assert.IsNotNull(body, "공격 중이면 빔 세션이 열려 있어야 한다(활성 BeamBody)");

            // 프리팹 원본은 z=4.17 · 위치 (0, 2.41, 0). 그대로면 배치가 한 번도 안 된 것.
            float z = body.localScale.z;
            Assert.That(z, Is.Not.EqualTo(4.17f).Within(0.001f),
                "BeamBody 의 z 가 프리팹 원본 그대로다 = TryPlace 가 한 번도 성공하지 않았다");
            Assert.Greater(z, 0.01f, "빔 길이가 0 이면 몸통이 안 보인다");

            if (em.Exists(enemy)) em.DestroyEntity(enemy);
        }

        // ── helpers ──────────────────────────────────────────────────────────
        private static DefenderCatalog FindCatalog()
        {
            var all = Resources.FindObjectsOfTypeAll<DefenderCatalog>();
            return all.Length > 0 ? all[0] : null;
        }

        private static Entity SpawnDummyEnemy(EntityManager em, float3 pos)
        {
            const float Hp = 1_000_000f;
            var e = em.CreateEntity();
            em.AddComponentData(e, LocalTransform.FromPosition(pos));
            em.AddComponentData(e, new Health { value = Hp, max = Hp });
            em.AddComponentData(e, new FactionTag { value = Faction.Enemy });
            em.AddBuffer<IncomingDamage>(e);
            em.AddComponent<AttackUnitTag>(e);
            return e;
        }

        private static Vector2Int FindPlaceableCell(BattleBridge bridge, DefenderUnitData u)
        {
            for (int x = -24; x < 48; x++)
                for (int y = -24; y < 48; y++)
                    if (bridge.CanPlaceDefenderAt(x, y, u, out _))
                        return new Vector2Int(x, y);
            return new Vector2Int(int.MinValue, int.MinValue);
        }

        private static Entity FindDefender(BattleBridge bridge, EntityManager em)
        {
            var f = typeof(BattleBridge).GetField("_defenderByTile", BindingFlags.NonPublic | BindingFlags.Instance);
            var dict = (System.Collections.IDictionary)f.GetValue(bridge);
            foreach (System.Collections.DictionaryEntry de in dict)
            {
                var v = de.Value;
                var entity = (Entity)v.GetType().GetField("Item1").GetValue(v);
                if (em.Exists(entity)) return entity;
            }
            return Entity.Null;
        }
    }
}
