using System.Collections;
using System.Reflection;
using NUnit.Framework;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.SceneManagement;
using Wassup.Core;
using Wassup.Bridge;
using Wassup.Battle.Units;

namespace Wassup.Tests.PlayMode
{
    // battle-structures 스펙 종료 Play 검증 — 거점이 라이브 판에 실제로 선다.
    //
    // 저작물: MapDocument_Test(dev 슬롯, 30×30 전면 Walk) 에 적 본능
    // (Structure_TestInstinct — cannon_base_red 프랍, 포탑) 1기를 (15,15) 에 저작해 뒀다.
    // 이 테스트가 재는 것: 부팅 → 스폰(SO HP·3×3 차단) → 뷰 프랍 → 적이 블로커를
    // 우회해 여전히 골에 도달(연결성 생존). 본능의 발사 자체는 EditMode 가 실
    // AttackSystem 으로 이미 고정했다(ArmedInstinct_FiresProjectileRequest...).
    public class StructureLivePlayTest
    {
        private const float TimeoutSec = 90f;
        private const int DevInstinctMapIndex = 6;   // 메인 6장 뒤 dev[0] = MapDocument_Test(적 본능)
        private const int DevSiegeMapIndex = 8;      // dev[2] = MapDocument_SiegeTest(적 마음, spawns 미저작)
        private int _savedIndex = -1;

        [SetUp]
        public void SetUp()
        {
            _savedIndex = DevMapOverride.Index;
            DevMapOverride.Index = DevInstinctMapIndex;
        }

        [TearDown]
        public void TearDown()
        {
            DevMapOverride.Index = _savedIndex;   // PlayerPrefs 는 머신 상태 — 반드시 원복
            LogAssert.ignoreFailingMessages = false;
        }

        private static object GetField(object target, string name)
        {
            var fi = target.GetType().GetField(name, BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNotNull(fi, $"Field '{name}' not found");
            return fi.GetValue(target);
        }

        [UnityTest]
        public IEnumerator Structures_BootOnDevMap_SpawnBlockAndSurviveConnectivity()
        {
            LogAssert.ignoreFailingMessages = true;
            yield return SceneManager.LoadSceneAsync(SceneNames.Battle, LoadSceneMode.Single);
            for (int i = 0; i < 6; i++) yield return null;

            var bridge = Object.FindObjectOfType<BattleBridge>();
            Assert.IsNotNull(bridge, "BattleBridge present");
            bridge.BeginPlacement();
            yield return null;

            // ── 후속 2(리뷰 M-5) — 프랍은 **배치 페이즈부터** 보인다 ──
            // 9×9 배치 배제는 맵 빌드 시 파생되므로, 프랍이 StartBattle 까지 없으면 플레이어는
            // «막힌 칸» 만 보고 이유를 알 수 없다. 뷰를 맵 수명으로 옮긴 것의 실증.
            int propsDuringPlacement = 0;
            foreach (Transform child in bridge.transform)
                if (child.name.StartsWith("Structure_")) propsDuringPlacement++;
            Assert.Greater(propsDuringPlacement, 0,
                "배치 페이즈에 거점 프랍이 보여야 한다 — 뷰가 엔티티(StartBattle)에 묶여 있으면 0 이다");

            bridge.StartBattle();
            yield return null;

            // StartBattle 이 프랍을 재생성하거나 날리지 않는다(수명 분리 — 엔티티만 이 시점).
            int propsAfterStart = 0;
            foreach (Transform child in bridge.transform)
                if (child.name.StartsWith("Structure_")) propsAfterStart++;
            Assert.AreEqual(propsDuringPlacement, propsAfterStart,
                "StartBattle 은 프랍을 건드리지 않는다(중복 생성·소실 둘 다 금지)");

            var em = (EntityManager)GetField(bridge, "_em");

            // ── 스폰: 적 본능이 (15,15) 에 SO HP·3×3 차단으로 선다 ──
            using (var q = em.CreateEntityQuery(ComponentType.ReadOnly<StructureTag>()))
            {
                var entities = q.ToEntityArray(Allocator.Temp);
                Entity instinct = Entity.Null;
                foreach (var e in entities)
                {
                    var st = em.GetComponentData<StructureTag>(e);
                    if (st.faction == Faction.EnemyInstinct) { instinct = e; break; }
                }
                entities.Dispose();

                Assert.AreNotEqual(Entity.Null, instinct, "저작된 적 본능이 라이브 판에 스폰된다");
                Assert.AreEqual(new int2(15, 15), em.GetComponentData<StructureTag>(instinct).cell);
                Assert.AreEqual(500f, em.GetComponentData<Health>(instinct).value, 1e-3f, "HP 는 SO(500)에서");
                Assert.AreEqual(9,
                    em.GetBuffer<Wassup.Battle.Effects.BlockingHazardCellsBuffer>(instinct).Length,
                    "3×3 본체 통행 차단");
                Assert.IsTrue(em.HasComponent<Wassup.Battle.Combat.AttackState>(instinct),
                    "공격 저작(damage 10 + fireball) → AttackState 베이크");
            }

            // ── 뷰: 프랍 인스턴스(cannon_base_red)가 브리지 아래 선다 ──
            bool viewFound = false;
            foreach (Transform child in bridge.transform)
                if (child.name.StartsWith("Structure_")) { viewFound = true; break; }
            Assert.IsTrue(viewFound, "SO.viewPrefab 프랍이 셀 중심에 인스턴스된다");

            // ── 연결성 생존: 3×3 블로커를 우회해 적이 여전히 골에 도달한다 ──
            // 디펜더 0 → 적이 골 타워를 공성해 안정도가 준다(= 도달의 관측치).
            for (int i = 0; i < 20 && bridge.NextWaveHasNext; i++) bridge.ForceNextWave();
            int startStability = bridge.GoalStabilityCurrent;
            float start = Time.unscaledTime;
            while (bridge.GoalStabilityCurrent >= startStability)
            {
                Assert.Less(Time.unscaledTime - start, TimeoutSec,
                    "적이 골에 도달하지 못한다 — 본능 3×3 블로커가 경로를 끊었을 수 있다(연결성 회귀)");
                yield return null;
            }
        }

        // 공성 모드 파생(unit 6)의 **라이브** 검증. EditMode 는 ToGeneratedMap 투영만 쟀고,
        // «파생 스폰에서 웨이브가 실제로 나온다» 는 여기서만 확인된다.
        // 저작물: MapDocument_SiegeTest — 적 마음(15,25) 1기 + 적 본능(15,12) 1기 ·
        // 방어 골(15,0) 1개 · spawns 미저작. 본능이 스폰→골 직선상에 있어 «막으면 돌아간다» 도
        // 같은 판에서 실측된다(연결성은 30×30 전면 Walk 라 3×3 로 끊기지 않는다).
        [UnityTest]
        public IEnumerator SiegeMap_DerivesSpawnFromEnemyCore_AndWavesComeFromIt()
        {
            LogAssert.ignoreFailingMessages = true;
            DevMapOverride.Index = DevSiegeMapIndex;
            yield return SceneManager.LoadSceneAsync(SceneNames.Battle, LoadSceneMode.Single);
            for (int i = 0; i < 6; i++) yield return null;

            var bridge = Object.FindObjectOfType<BattleBridge>();
            Assert.IsNotNull(bridge, "BattleBridge present");
            bridge.BeginPlacement();
            yield return null;

            // ── 파생: 저작 spawns 가 0인데 런타임 스폰이 적 마음 셀 1개로 채워졌다 ──
            // (파생이 없으면 spawns 0 → MapConnectivity false → fallback linear 로 교체되어
            //  격자가 20×10 이 된다. 격자 크기가 곧 «문서가 살아남았나» 의 관측치다.)
            var map = (Wassup.Data.GeneratedMap)GetField(bridge, "_generatedMap");
            Assert.AreEqual(new int2(30, 30), map.gridSize,
                "저작 문서가 살아남았다 — fallback linear(20×10)로 교체되지 않았다");
            Assert.AreEqual(1, map.spawns.Length, "적 마음 1기 → 파생 스폰 1개");
            Assert.AreEqual(new int2(15, 25), map.spawns[0], "스폰 = 적 마음 셀");

            // 적 마음 엔티티도 서 있다(스폰 지점이면서 거점).
            var em = (EntityManager)GetField(bridge, "_em");
            bridge.StartBattle();
            yield return null;
            using (var q = em.CreateEntityQuery(ComponentType.ReadOnly<StructureTag>()))
            {
                var entities = q.ToEntityArray(Allocator.Temp);
                bool foundCore = false, foundInstinct = false;
                foreach (var e in entities)
                {
                    var f = em.GetComponentData<StructureTag>(e).faction;
                    if (f == Faction.EnemyCore) foundCore = true;
                    if (f == Faction.EnemyInstinct) foundInstinct = true;
                }
                entities.Dispose();
                Assert.IsTrue(foundCore, "적 마음이 거점 엔티티로 선다(스폰 지점 겸 거점)");
                Assert.IsTrue(foundInstinct, "같은 판에 적 본능도 선다 — 마음·본능 두 종류가 공존한다");
            }

            // ── 웨이브가 그 셀에서 나온다 — 적이 파생 스폰 근처에 실제로 생성되는지 ──
            for (int i = 0; i < 5 && bridge.NextWaveHasNext; i++) bridge.ForceNextWave();
            float3 spawnWorld = bridge.GridToWorldCenterVector(new Vector2Int(15, 25));
            float start = Time.unscaledTime;
            bool sawEnemyNearSpawn = false;
            while (!sawEnemyNearSpawn)
            {
                Assert.Less(Time.unscaledTime - start, TimeoutSec,
                    "파생 스폰에서 적이 나오지 않는다 — 웨이브 생성이 spawns[] 를 안 쓰거나 파생이 끊겼다");
                using (var q = em.CreateEntityQuery(
                    ComponentType.ReadOnly<AttackUnitTag>(),
                    ComponentType.ReadOnly<Unity.Transforms.LocalTransform>()))
                {
                    var xf = q.ToComponentDataArray<Unity.Transforms.LocalTransform>(Allocator.Temp);
                    for (int i = 0; i < xf.Length; i++)
                        if (math.distance(xf[i].Position, spawnWorld) < 6f) { sawEnemyNearSpawn = true; break; }
                    xf.Dispose();
                }
                yield return null;
            }
        }

        // ───────────────────── unit 11 — 공성 승패의 라이브 검증 ─────────────────────
        //
        // units 8~10 이 한 판에서 맞물려 도는 것을 잰다:
        //  (1) 적 마음 축이 활성이고 두 마음의 체력이 같게 저작됐다 (unit 10 + Deck_SiegeTest)
        //  (2) 배치한 방어유닛이 적 마음을 **실제로** 깎는다 (unit 8 — 이전엔 영구 무적이었다)
        //  (3) 사거리가 닿으면 적 본능도 깎는다 (조건부 — 아래 구조적 사유 참조)
        //  (4) 적 마음 잔여 0 → 승리 판정 (unit 10 의 축)
        //
        // (4) 를 800 HP 실그라인딩으로 재지 않는 이유: 축이 재는 것은 «잔여 0 → 승리» 이고
        // 피해 출처는 그 판정과 무관하다. 라이브 피해 경로는 (2) 가 이미 증명하므로, 둘을
        // 한 번에 묶으면 검증이 늘지 않고 소요 시간과 흔들림만 늘어난다.
        [UnityTest]
        public IEnumerator SiegeMap_DefendersBreakEnemyCore_AndCoreDeathWins()
        {
            LogAssert.ignoreFailingMessages = true;
            DevMapOverride.Index = DevSiegeMapIndex;
            yield return SceneManager.LoadSceneAsync(SceneNames.Battle, LoadSceneMode.Single);
            for (int i = 0; i < 6; i++) yield return null;

            var bridge = Object.FindObjectOfType<BattleBridge>();
            Assert.IsNotNull(bridge, "BattleBridge present");
            var cat = Resources.FindObjectsOfTypeAll<Wassup.Data.DefenderCatalog>();
            Assert.Greater(cat.Length, 0, "DefenderCatalog 를 못 찾았다");
            var grinder = cat[0].ById("guardian");   // 근접 — 마음 인접에서 깎는다
            var sniper = cat[0].ById("sniper");      // 최장 사거리 — 본능 저격 시도
            Assert.IsNotNull(grinder, "guardian");
            Assert.IsNotNull(sniper, "sniper");

            bridge.SetDefenderPool(new[] { grinder, sniper });
            bridge.BeginPlacement();
            var gm = Object.FindObjectOfType<GameManager>();
            gm.CostRuntime.ResetToStart();
            gm.CostRuntime.AddCost(5000);   // 배치 비용은 이 테스트의 관심사가 아니다
            yield return null;

            var coreCell = new Vector2Int(15, 25);       // 저작물(MapDocument_SiegeTest)
            var instinctCell = new Vector2Int(15, 12);

            // 적 마음 인접 8칸에 최대한 채운다 — 마음은 본체 1칸만 닫히므로 인접 배치가
            // 가능하다는 사실 자체가 «공성이 새 메커닉 0 으로 성립한다» 의 관측치다.
            int placedAroundCore = 0;
            for (int dx = -1; dx <= 1; dx++)
                for (int dy = -1; dy <= 1; dy++)
                {
                    if (dx == 0 && dy == 0) continue;
                    int x = coreCell.x + dx, y = coreCell.y + dy;
                    if (bridge.CanPlaceDefenderAt(x, y, grinder, out _)
                        && bridge.PlaceDefenderAs(x, y, grinder)) placedAroundCore++;
                }
            Assert.Greater(placedAroundCore, 0,
                "적 마음 인접에 배치할 수 없다 — 마음이 본체 1칸만 닫는다는 계약이 깨졌다");

            // 본능에 가장 가까운 합법 칸(9×9 배제 밖)에 저격수.
            bool sniperPlaced = TryPlaceNearest(bridge, sniper, instinctCell, out var sniperCell);

            bridge.StartBattle();
            yield return null;

            // ── (1) 축 활성 + 저작 대칭 ──
            Assert.Greater(bridge.EnemyCoreMax, 0,
                "적 마음 축이 비활성이다 — 저작이 안 읽혔거나 스폰이 max 를 못 채웠다");
            Assert.AreEqual(bridge.GoalStabilityMax, bridge.EnemyCoreMax,
                "두 마음의 체력이 같게 저작돼야 한다(절대값 비교의 공정성) — "
                + "Deck_SiegeTest.goalStabilityMax ↔ Structure_EnemyCore.health");

            var em = (EntityManager)GetField(bridge, "_em");
            Entity core = FindStructure(em, Faction.EnemyCore);
            Entity instinct = FindStructure(em, Faction.EnemyInstinct);
            Assert.AreNotEqual(Entity.Null, core, "적 마음 엔티티");
            Assert.AreNotEqual(Entity.Null, instinct, "적 본능 엔티티");

            // ── (2) 적 마음이 깎인다 ──
            int coreMax = bridge.EnemyCoreMax;
            float start = Time.unscaledTime;
            while (bridge.EnemyCoreCurrent >= coreMax)
            {
                Assert.Less(Time.unscaledTime - start, TimeoutSec,
                    "적 마음이 한 톨도 깎이지 않는다 — unit 8 의 저작 마스크가 라이브에서 안 먹는다");
                yield return null;
            }

            // ── (3) 적 본능 — 사거리가 닿을 때만 단정한다 ──
            // 9×9 배치 배제(체비셰프 ≤ 4)와 본능 사거리(SO 저작)의 관계가 이것을 결정한다.
            // 닿지 않는 저작이면 그것은 미검증이 아니라 **설계 결과**이므로 로그로 남긴다.
            if (sniperPlaced && em.HasComponent<Health>(instinct))
            {
                float instinctMax = em.GetComponentData<Health>(instinct).max;
                float reach = SniperReach(em, bridge, sniperCell, instinct);
                if (reach > 0f)
                {
                    while (em.Exists(instinct)
                           && em.GetComponentData<Health>(instinct).value >= instinctMax)
                    {
                        Assert.Less(Time.unscaledTime - start, TimeoutSec,
                            "사거리 안인데 적 본능이 깎이지 않는다");
                        yield return null;
                    }
                }
                else
                {
                    Debug.Log("[unit 11] 적 본능은 이 저작에서 사거리 교전이 성립하지 않는다 "
                        + "(9×9 배치 배제 체비셰프 ≤ 4 vs 저격 사거리). 구조적 결과이며 "
                        + "본능 발사 자체는 EditMode 가 실 AttackSystem 으로 고정한다.");
                }
            }

            // ── (4) 잔여 0 → 승리 축 ──
            // 피해 출처는 축과 무관하다((2) 가 라이브 경로를 이미 증명했다).
            em.GetBuffer<IncomingDamage>(core).Add(new IncomingDamage { amount = coreMax * 10f });
            start = Time.unscaledTime;
            while (gm.CurrentPhase != GamePhase.Result)
            {
                Assert.Less(Time.unscaledTime - start, TimeoutSec,
                    "적 마음이 무너졌는데 판이 끝나지 않는다 — CheckEnemyCoreDestroyed 축이 안 돌았다");
                yield return null;
            }
            Assert.AreEqual(0, bridge.EnemyCoreCurrent, "잔여 0 이 판정의 근거였다");
        }

        private static Entity FindStructure(EntityManager em, Faction faction)
        {
            using var q = em.CreateEntityQuery(ComponentType.ReadOnly<StructureTag>());
            var entities = q.ToEntityArray(Allocator.Temp);
            Entity found = Entity.Null;
            foreach (var e in entities)
                if (em.GetComponentData<StructureTag>(e).faction == faction) { found = e; break; }
            entities.Dispose();
            return found;
        }

        // 목표 셀에 가장 가까운 «배치 가능» 칸(체비셰프 오름차순, 반경 12 상한).
        private static bool TryPlaceNearest(BattleBridge bridge, Wassup.Data.DefenderUnitData u,
            Vector2Int target, out Vector2Int cell)
        {
            for (int r = 1; r <= 12; r++)
                for (int dx = -r; dx <= r; dx++)
                    for (int dy = -r; dy <= r; dy++)
                    {
                        if (Mathf.Max(Mathf.Abs(dx), Mathf.Abs(dy)) != r) continue;
                        int x = target.x + dx, y = target.y + dy;
                        if (!bridge.CanPlaceDefenderAt(x, y, u, out _)) continue;
                        if (!bridge.PlaceDefenderAs(x, y, u)) continue;
                        cell = new Vector2Int(x, y);
                        return true;
                    }
            cell = default;
            return false;
        }

        // 배치된 저격수의 **런타임** 사거리로 판정한다(SO 값은 스탯 시트가 덮을 수 있다).
        // 반환 > 0 = 사거리 안. 0 = 닿지 않음 또는 저격수를 못 찾음.
        private static float SniperReach(EntityManager em, BattleBridge bridge,
            Vector2Int sniperCell, Entity instinct)
        {
            float3 sniperWorld = bridge.GridToWorldCenterVector(sniperCell);
            using var q = em.CreateEntityQuery(
                ComponentType.ReadOnly<DefenderUnitTag>(),
                ComponentType.ReadOnly<Wassup.Battle.Combat.AttackState>(),
                ComponentType.ReadOnly<Unity.Transforms.LocalTransform>());
            var entities = q.ToEntityArray(Allocator.Temp);
            float best = 0f;
            float3 instinctPos = em.GetComponentData<Unity.Transforms.LocalTransform>(instinct).Position;
            foreach (var e in entities)
            {
                var xf = em.GetComponentData<Unity.Transforms.LocalTransform>(e);
                if (math.distance(xf.Position, sniperWorld) > 0.6f) continue;   // 그 칸의 유닛
                float range = em.GetComponentData<Wassup.Battle.Combat.AttackState>(e).range;
                if (math.distance(xf.Position, instinctPos) <= range) best = range;
                break;
            }
            entities.Dispose();
            return best;
        }
    }
}
