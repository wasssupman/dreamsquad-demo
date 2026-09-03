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
    // PlacementAuraTest 패턴): Begin(점유·바인딩·DefenderFootprint 스왑 + PendingDeployment 재부착)
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
            // defender-board-limit — 이 테스트는 같은 유닛을 판에 2기 세운다(재배치 후 원 타일
            // 재사용). 라이브 기본값 maxOnBoard=1 이면 2기째가 LimitReached 로 막히므로 상한을
            // 푼다. 카탈로그 에셋이 아니라 **런타임 사본**에만 쓴다 — 에셋 직접 수정은 에디터에서
            // 디스크에 박힌다.
            var unit = Object.Instantiate(cat.ById("ranger"));
            unit.maxOnBoard = 99;
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

            // 목적지: from 에서 이동 가능한 첫 유효 셀.
            // unit 9 — from 자신을 **명시로 제외**한다. 제자리 재정비가 유효해져서 스캔이 그냥
            // 소스 칸을 집어 오고, 그러면 이 테스트가 검증하려는 "이동" 자체가 일어나지 않는다.
            Vector2Int to = default; bool foundTo = false;
            for (int x = -24; x < 48 && !foundTo; x++)
                for (int y = -24; y < 48 && !foundTo; y++)
                {
                    var c = new Vector2Int(x, y);
                    if (c != from && bridge.CanRelocateDefender(from, c, out _)) { to = c; foundTo = true; }
                }
            Assert.IsTrue(foundTo, "valid relocation target exists");

            // Begin — 확정 프레임 원자 스왑
            Assert.IsTrue(bridge.TryBeginDefenderRelocation(from, to, out var moved, out var reason),
                $"begin relocation ({reason})");
            Assert.AreEqual(entity, moved, "same entity relocated");
            Assert.AreEqual(to, SoleCell(bridge), "binding moved to target cell");
            Assert.AreEqual(Entity.Null, EntityAt(bridge, em, from), "source binding removed");
            Assert.IsTrue(em.HasComponent<PendingDeployment>(entity), "PendingDeployment re-attached (비타겟·비무장)");
            var tile = em.GetComponentData<DefenderFootprint>(entity);
            Assert.AreEqual(new int2(to.x, to.y), tile.anchor, "DefenderFootprint updated at confirm frame");

            // busy 중 재이동 거부 (SourceBusy)
            Assert.IsFalse(bridge.CanRelocateDefender(to, from, out var busyReason), "no re-move while pending");
            Assert.AreEqual(PlacementRejectReason.SourceBusy, busyReason, "reject reason = SourceBusy");

            // Finish — 착지 프레임 LocalTransform 이동 (Begin 은 위치를 건드리지 않는다)
            float3 posAfterBegin = em.GetComponentData<LocalTransform>(entity).Position;
            Assert.Less(math.distance(posBefore.xz, posAfterBegin.xz), 0.001f, "Begin leaves LocalTransform untouched");
            bridge.FinishDefenderRelocation(to, entity);
            float3 posAfterFinish = em.GetComponentData<LocalTransform>(entity).Position;
            Assert.Greater(math.distance(posBefore.xz, posAfterFinish.xz), 0.5f, "Finish moves sim position");

            // Activate — 전투 복귀. unit 8 부터 on-place 는 **재발화한다**(확정 프레임에 재무장).
            // 효과 타일만 자기 가드로 1회에 남는다.
            bridge.ActivateRelocatedDefender(to, entity, 0f);
            Assert.IsFalse(em.HasComponent<PendingDeployment>(entity), "PendingDeployment removed on activate");

            // 비워진 원 타일에 재배치 성공
            Assert.IsTrue(bridge.PlaceDefenderAs(from.x, from.y, unit), "source tile is free for a new placement");
        }


        // defender-relocation unit 8 — 대가와 보상. 재배치 1회에 코스트가 유닛 코스트만큼 줄고,
        // 활성화 시점에 최대 체력 비율만큼 회복하며, on-place 가드가 재무장된다.
        [UnityTest]
        public IEnumerator Relocate_SpendsCost_AndHealsOnActivate()
        {
            LogAssert.ignoreFailingMessages = true;
            yield return SceneManager.LoadSceneAsync(SceneNames.Battle, LoadSceneMode.Single);
            for (int i = 0; i < 6; i++) yield return null;

            var bridge = Object.FindObjectOfType<BattleBridge>();
            var cat = FindCatalog();
            var unit = Object.Instantiate(cat.ById("ranger"));
            unit.maxOnBoard = 99;
            bridge.SetDefenderPool(new[] { unit });
            bridge.BeginPlacement();
            var gm = Object.FindObjectOfType<GameManager>();
            gm.CostRuntime.ResetToStart();
            gm.CostRuntime.AddCost(1000);
            yield return null;

            Assert.IsTrue(PlaceFirstValid(bridge, unit), "place unit");
            var em = World.DefaultGameObjectInjectionWorld.EntityManager;
            var from = SoleCell(bridge);
            var entity = EntityAt(bridge, em, from);

            // 목적지 확보
            Vector2Int to = default; bool foundTo = false;
            for (int x = -24; x < 48 && !foundTo; x++)
            for (int y = -24; y < 48 && !foundTo; y++)
            {
                var c = new Vector2Int(x, y);
                if (c != from && bridge.CanRelocateDefender(from, c, out _)) { to = c; foundTo = true; }
            }
            Assert.IsTrue(foundTo, "a valid relocation target exists");

            // 반피로 만든다 — 만피면 회복이 관측되지 않는다(클램프).
            var health = em.GetComponentData<Health>(entity);
            health.value = health.max * 0.25f;
            em.SetComponentData(entity, health);
            float hpBefore = health.value;
            float maxHp = health.max;

            // on-place 재무장 관측: 재배치 전에는 가드에 들어 있어 재발동이 거부된다.
            Assert.IsFalse(bridge.TriggerDeploymentOnPlaceSkill(from, entity),
                "on-place is guarded before relocation (exactly-once)");

            float costBefore = gm.CostRuntime.Current;
            Assert.IsTrue(bridge.TryBeginDefenderRelocation(from, to, out var moved, out _), "begin relocation");
            Assert.AreEqual(entity, moved, "same entity (재배치는 유닛을 새로 만들지 않는다)");
            Assert.AreEqual(costBefore - unit.cost, gm.CostRuntime.Current, 0.001f,
                "확정 프레임에 배치 코스트 전액 차감 (계약 1 rev)");

            bridge.FinishDefenderRelocation(to, moved);
            bridge.ActivateRelocatedDefender(to, moved, 0.5f);
            for (int i = 0; i < 3; i++) yield return null; // IncomingHeal 은 DamageApplicationSystem 이 배수

            float hpAfter = em.GetComponentData<Health>(moved).value;
            Assert.Greater(hpAfter, hpBefore, "활성화 시점에 회복이 들어간다 (계약 12)");
            Assert.AreEqual(math.min(maxHp, hpBefore + maxHp * 0.5f), hpAfter, maxHp * 0.05f,
                "회복량 = 최대 체력 × ratio (상한 클램프)");

            // on-place 가 실제로 다시 돌았다는 증거: 활성화가 가드를 **다시** 채웠다.
            // 재발동이 없었다면 가드가 빈 채라 이 호출이 true 를 냈을 것이다.
            Assert.IsFalse(bridge.TriggerDeploymentOnPlaceSkill(to, moved),
                "on-place re-fired at activation and re-armed the guard (계약 4 rev)");
        }

        // defender-relocation unit 9 — 제자리 재정비. 같은 칸이 취소가 아니라 확정이고,
        // 자기 점유를 목적지 점유로 오판하지 않으며, 유닛은 그 칸에 그대로 남는다.
        [UnityTest]
        public IEnumerator Relocate_SameCell_IsRefit_NotCancel()
        {
            LogAssert.ignoreFailingMessages = true;
            yield return SceneManager.LoadSceneAsync(SceneNames.Battle, LoadSceneMode.Single);
            for (int i = 0; i < 6; i++) yield return null;

            var bridge = Object.FindObjectOfType<BattleBridge>();
            var cat = FindCatalog();
            var unit = Object.Instantiate(cat.ById("ranger"));
            unit.maxOnBoard = 99;
            bridge.SetDefenderPool(new[] { unit });
            bridge.BeginPlacement();
            var gm = Object.FindObjectOfType<GameManager>();
            gm.CostRuntime.ResetToStart();
            gm.CostRuntime.AddCost(1000);
            yield return null;

            Assert.IsTrue(PlaceFirstValid(bridge, unit), "place unit");
            var em = World.DefaultGameObjectInjectionWorld.EntityManager;
            var cell = SoleCell(bridge);
            var entity = EntityAt(bridge, em, cell);

            // 자기 점유가 Occupied 로 튀지 않는다 — from==to 검사가 공간 판정보다 앞이라는 계약.
            Assert.IsTrue(bridge.CanRelocateDefender(cell, cell, out var reason),
                "same cell is a valid refit target");
            Assert.AreEqual(PlacementRejectReason.None, reason, "제자리 = 사유 없음");

            var health = em.GetComponentData<Health>(entity);
            health.value = health.max * 0.25f;
            em.SetComponentData(entity, health);
            float hpBefore = health.value;
            float maxHp = health.max;
            float costBefore = gm.CostRuntime.Current;

            Assert.IsTrue(bridge.TryBeginDefenderRelocation(cell, cell, out var same, out _), "begin refit in place");
            Assert.AreEqual(entity, same, "same entity");
            Assert.AreEqual(costBefore - unit.cost, gm.CostRuntime.Current, 0.001f, "제자리도 코스트를 낸다");
            Assert.AreEqual(cell, CellOfEntity(bridge, entity), "유닛은 그 칸에 그대로 남는다");
            Assert.IsTrue(em.HasComponent<PendingDeployment>(entity), "제자리도 이탈 구간을 거친다");

            bridge.FinishDefenderRelocation(cell, same);
            bridge.ActivateRelocatedDefender(cell, same, 0.5f);
            for (int i = 0; i < 3; i++) yield return null;

            Assert.IsFalse(em.HasComponent<PendingDeployment>(same), "전투 복귀");
            Assert.Greater(em.GetComponentData<Health>(same).value, hpBefore, "제자리 재정비도 회복한다");
            Assert.AreEqual(cell, CellOfEntity(bridge, same), "복귀 후에도 같은 칸");
            Assert.IsTrue(math.abs(maxHp - health.max) < 0.001f, "최대 체력은 건드리지 않는다");
        }

        // ── helpers ──────────────────────────────────────────────────────────

        // 엔티티가 실제로 어느 칸에 묶여 있는지 — _defenderByTile 역참조(브리지 read seam).
        private static Vector2Int CellOfEntity(BattleBridge bridge, Entity e)
        {
            Assert.IsTrue(bridge.TryGetDefenderCell(e, out var c), "entity is bound to a cell");
            return c;
        }

        // 시너지 기여만 격리 판정: StatModifierSlot 버퍼에서 origin=Synergy·DamageMul 슬롯의
        // magnitude 를 직독(read-only). 슬롯 없음 = 중립 1.0.

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
