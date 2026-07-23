using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.SceneManagement;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using Wassup.Core;
using Wassup.Core.TimeControl;
using Wassup.Bridge;
using Wassup.Data;
using Wassup.Battle.Units;

namespace Wassup.Tests.PlayMode
{
    // defender-relocation unit 0 — 재배치 시뮬 토대 스모크 (BattleBridge 직접 구동,
    // PlacementAuraTest 패턴): Begin(점유·바인딩·DefenderTile 스왑 + PendingDeployment 재부착)
    // → busy 중 재이동 거부 → Finish(LocalTransform 이동) → Activate(전투 복귀)
    // → 비워진 원 타일에 재배치 성공.
    public class RelocationSmokeTest
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
        public IEnumerator Relocate_SwapsSimState_AndFreesSourceTile()
        {
            LogAssert.ignoreFailingMessages = true;
            yield return SceneManager.LoadSceneAsync(SceneNames.Battle, LoadSceneMode.Single);
            for (int i = 0; i < 6; i++) yield return null;

            var bridge = Object.FindObjectOfType<BattleBridge>();
            Assert.IsNotNull(bridge, "BattleBridge present");
            var cat = FindCatalog();
            Assert.IsNotNull(cat, "DefenderCatalog present");
            var unit = cat.ById("ranger");
            Assert.IsNotNull(unit, "ranger in catalog");

            bridge.SetDefenderPool(new[] { unit });
            bridge.BeginPlacement();
            var gm = Object.FindObjectOfType<GameManager>();
            gm.CostRuntime.ResetToStart();
            gm.CostRuntime.AddCost(1000);
            yield return null;

            Assert.IsTrue(PlaceFirstValid(bridge, unit), "place source unit");
            var em = World.DefaultGameObjectInjectionWorld.EntityManager;
            var from = SoleCell(bridge);
            var entity = EntityAt(bridge, em, from);
            Assert.AreNotEqual(Entity.Null, entity, "source entity resolved");
            float3 posBefore = em.GetComponentData<LocalTransform>(entity).Position;

            // 목적지: from 에서 이동 가능한 첫 유효 셀
            Vector2Int to = default; bool foundTo = false;
            for (int x = -24; x < 48 && !foundTo; x++)
                for (int y = -24; y < 48 && !foundTo; y++)
                    if (bridge.CanRelocateDefender(from, new Vector2Int(x, y), out _))
                    { to = new Vector2Int(x, y); foundTo = true; }
            Assert.IsTrue(foundTo, "valid relocation target exists");

            // Begin — 확정 프레임 원자 스왑
            Assert.IsTrue(bridge.TryBeginDefenderRelocation(from, to, out var moved, out var reason),
                $"begin relocation ({reason})");
            Assert.AreEqual(entity, moved, "same entity relocated");
            Assert.AreEqual(to, SoleCell(bridge), "binding moved to target cell");
            Assert.AreEqual(Entity.Null, EntityAt(bridge, em, from), "source binding removed");
            Assert.IsTrue(em.HasComponent<PendingDeployment>(entity), "PendingDeployment re-attached (비타겟·비무장)");
            var tile = em.GetComponentData<DefenderTile>(entity);
            Assert.AreEqual(new int2(to.x, to.y), tile.cell, "DefenderTile updated at confirm frame");

            // busy 중 재이동 거부 (SourceBusy)
            Assert.IsFalse(bridge.CanRelocateDefender(to, from, out var busyReason), "no re-move while pending");
            Assert.AreEqual(PlacementRejectReason.SourceBusy, busyReason, "reject reason = SourceBusy");

            // Finish — 착지 프레임 LocalTransform 이동 (Begin 은 위치를 건드리지 않는다)
            float3 posAfterBegin = em.GetComponentData<LocalTransform>(entity).Position;
            Assert.Less(math.distance(posBefore.xz, posAfterBegin.xz), 0.001f, "Begin leaves LocalTransform untouched");
            bridge.FinishDefenderRelocation(to, entity);
            float3 posAfterFinish = em.GetComponentData<LocalTransform>(entity).Position;
            Assert.Greater(math.distance(posBefore.xz, posAfterFinish.xz), 0.5f, "Finish moves sim position");

            // Activate — 전투 복귀 (on-place 는 _onPlaceTriggeredEntities 가드로 재발화 없음)
            bridge.ActivateDeployedDefender(to, entity);
            Assert.IsFalse(em.HasComponent<PendingDeployment>(entity), "PendingDeployment removed on activate");

            // 비워진 원 타일에 재배치 성공
            Assert.IsTrue(bridge.PlaceDefenderAs(from.x, from.y, unit), "source tile is free for a new placement");
        }

        // 시너지 양쪽 재계산 (spec README 계약 6): 동일 유닛 인접 2기 → 양쪽 damageMul 1.1
        // → 1기를 비인접 셀로 이동 → 잔류 유닛은 Begin 의 from 재계산으로, 이동 유닛은
        // Activate 의 to 재계산으로 각각 1.0 복귀.
        [UnityTest]
        public IEnumerator Relocate_RecomputesSynergy_OnBothCells()
        {
            LogAssert.ignoreFailingMessages = true;
            yield return SceneManager.LoadSceneAsync(SceneNames.Battle, LoadSceneMode.Single);
            for (int i = 0; i < 6; i++) yield return null;

            var bridge = Object.FindObjectOfType<BattleBridge>();
            // 라이브 씬은 enableAdjacencySynergy=0 — 계약(재계산이 양쪽 셀에서 발화) 검증을 위해
            // 테스트에서만 켠다. Play 인스턴스 필드라 씬에 남지 않는다.
            typeof(BattleBridge).GetField("enableAdjacencySynergy", BindingFlags.NonPublic | BindingFlags.Instance)
                .SetValue(bridge, true);
            var cat = FindCatalog();
            var unit = cat.ById("ranger");
            bridge.SetDefenderPool(new[] { unit });
            bridge.BeginPlacement();
            var gm = Object.FindObjectOfType<GameManager>();
            gm.CostRuntime.ResetToStart();
            gm.CostRuntime.AddCost(1000);
            yield return null;

            // 인접 배치 가능한 셀 쌍 탐색
            Vector2Int a = default, b = default; bool foundPair = false;
            for (int x = -24; x < 48 && !foundPair; x++)
            for (int y = -24; y < 48 && !foundPair; y++)
            {
                if (!bridge.CanPlaceDefenderAt(x, y, unit, out _)) continue;
                for (int dx = -1; dx <= 1 && !foundPair; dx++)
                for (int dy = -1; dy <= 1 && !foundPair; dy++)
                {
                    if (dx == 0 && dy == 0) continue;
                    if (bridge.CanPlaceDefenderAt(x + dx, y + dy, unit, out _))
                    { a = new Vector2Int(x, y); b = new Vector2Int(x + dx, y + dy); foundPair = true; }
                }
            }
            Assert.IsTrue(foundPair, "adjacent placeable pair exists");
            Assert.IsTrue(bridge.PlaceDefenderAs(a.x, a.y, unit), "place A");
            Assert.IsTrue(bridge.PlaceDefenderAs(b.x, b.y, unit), "place B");
            var em = World.DefaultGameObjectInjectionWorld.EntityManager;
            var entityA = EntityAt(bridge, em, a);
            var entityB = EntityAt(bridge, em, b);
            for (int i = 0; i < 3; i++) yield return null;
            // 총합 damageMul 이 아니라 시너지 슬롯(origin=Synergy)만 직독 — 랜덤 기믹/드림스톤의
            // 데미지 배율이 총합을 오염시켜도(관측: ×1.25 기믹 런) 무관하게 판정.
            Assert.AreEqual(1.1f, SynergyMagnitude(em, entityA), 0.01f, "A synergy 1.1");
            Assert.AreEqual(1.1f, SynergyMagnitude(em, entityB), 0.01f, "B synergy 1.1");

            // A 를 B 와 비인접인 셀로 이동
            Vector2Int to = default; bool foundTo = false;
            for (int x = -24; x < 48 && !foundTo; x++)
            for (int y = -24; y < 48 && !foundTo; y++)
            {
                var c = new Vector2Int(x, y);
                if (Mathf.Max(Mathf.Abs(c.x - b.x), Mathf.Abs(c.y - b.y)) <= 1) continue; // B 인접 제외
                if (bridge.CanRelocateDefender(a, c, out _)) { to = c; foundTo = true; }
            }
            Assert.IsTrue(foundTo, "non-adjacent relocation target exists");

            Assert.IsTrue(bridge.TryBeginDefenderRelocation(a, to, out var moved, out _), "begin relocation");
            for (int i = 0; i < 3; i++) yield return null;
            Assert.AreEqual(1.0f, SynergyMagnitude(em, entityB), 0.01f, "B recomputed at Begin (from-cell)");

            bridge.FinishDefenderRelocation(to, moved);
            bridge.ActivateDeployedDefender(to, moved);
            for (int i = 0; i < 3; i++) yield return null;
            Assert.AreEqual(1.0f, SynergyMagnitude(em, entityA), 0.01f, "A recomputed at Activate (to-cell)");
        }

        // ── helpers ──────────────────────────────────────────────────────────

        // 시너지 기여만 격리 판정: StatModifierSlot 버퍼에서 origin=Synergy·DamageMul 슬롯의
        // magnitude 를 직독(read-only). 슬롯 없음 = 중립 1.0.
        private static float SynergyMagnitude(EntityManager em, Entity e)
        {
            if (e == Entity.Null || !em.HasBuffer<Wassup.Battle.Effects.StatModifierSlot>(e)) return 1f;
            var buf = em.GetBuffer<Wassup.Battle.Effects.StatModifierSlot>(e);
            for (int i = 0; i < buf.Length; i++)
                if (buf[i].header.origin == Wassup.Battle.Effects.ModifierOrigin.Synergy
                    && buf[i].stat == Wassup.Battle.Effects.StatKind.DamageMul)
                    // ModifierAuthoring.FromMultiplier 역변환: ≥1 배율은 Additive(delta), <1 은 Multiplicative.
                    return buf[i].op == Wassup.Battle.Effects.CombineOp.Additive
                        ? 1f + buf[i].magnitude
                        : buf[i].magnitude;
            return 1f;
        }

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

        private static System.Collections.IDictionary ByTile(BattleBridge bridge)
        {
            var f = typeof(BattleBridge).GetField("_defenderByTile", BindingFlags.NonPublic | BindingFlags.Instance);
            return (System.Collections.IDictionary)f.GetValue(bridge);
        }

        // 테스트는 유닛을 1기만 유지하는 구간에서 호출 — 그 유일 바인딩의 셀.
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
