using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Wassup.Battle.Combat;
using Wassup.Battle.Effects;
using Wassup.Battle.Movement;
using Wassup.Battle.Units;
using Wassup.Bridge;
using Wassup.Core;
using Wassup.Data;

namespace Wassup.Tests.PlayMode
{
    // waypoint-routing unit 3 — 실제 BattleScene + MovementLab + 검증 덱을 태우는 증상 계측.
    // 순수 Step이 아니라 스폰 부착 → 필드 슬롯 → Movement 소비 → 골 도달의 전 구간을 센다.
    public class WaypointRoutingLiveTest
    {
        private const int WaypointLabMapIndex = 7; // main 6장 + dev[1] MovementLab
        private int _savedIndex;
        private bool _savedEndless;

        [SetUp]
        public void SetUp()
        {
            _savedIndex = DevMapOverride.Index;
            _savedEndless = DevMapOverride.Endless;
            DevMapOverride.Endless = false;
            DevMapOverride.Index = WaypointLabMapIndex;
        }

        [TearDown]
        public void TearDown()
        {
            DevMapOverride.Index = _savedIndex;
            DevMapOverride.Endless = _savedEndless;
        }

        [UnityTearDown]
        public IEnumerator UnityTearDown()
        {
            // 이 클래스가 씬을 다시 여는 테스트를 둘 가지므로, 첫 씬의 Draft tween이
            // 다음 씬에서 파괴된 콜백을 호출하지 않게 테스트 사이에 정리한다.
            PrimeTween.Tween.StopAll();
            yield return null;
        }

        [UnityTest]
        public IEnumerator ValidationWave_ShowsGuides_ThenPassesWaypointsInAuthoredOrder()
        {
            // 테스트가 Outgame의 fluid RT를 렌더 타깃으로 둔 프레임에서 씬을 즉시 교체하면
            // Unity가 활성 RT 해제 오류를 낸다. 일반 전환 루프를 우회하는 하네스이므로 명시 해제한다.
            RenderTexture.active = null;
            yield return SceneManager.LoadSceneAsync(SceneNames.Battle, LoadSceneMode.Single);
            for (int i = 0; i < 6; i++) yield return null;

            var bridge = Object.FindObjectOfType<BattleBridge>();
            Assert.IsNotNull(bridge, "BattleBridge present");
            bridge.BeginPlacement();
            yield return null;
            bridge.StartBattle();

            Assert.IsTrue(bridge.TryGetSpawnGuideForecast(out _, out var guides),
                "MovementLab 일반 매치가 생성 웨이브 예보를 큐잉해야 한다");
            Assert.GreaterOrEqual(guides.Length, 2, "웨이포인트형 두 스웜의 가이드 예보");
            var forecastPaths = new HashSet<int>();
            for (int i = 0; i < guides.Length; i++)
            {
                forecastPaths.Add(guides[i].waypointPathIndex);
                var guidePath = new List<Vector3>();
                Assert.IsTrue(bridge.TryGetSpawnPathSim(
                    guides[i].laneIndex,
                    guides[i].waypointPathIndex,
                    guides[i].traversalLayers,
                    guidePath));
            }
            CollectionAssert.AreEquivalent(new[] { 0, 1 }, forecastPaths,
                "경로 0/1 스웜이 모두 예보에 있어야 한다");

            yield return null;
            yield return null;
            var presenter = Object.FindObjectOfType<Wassup.Presentation.SpawnAlertPresenter>();
            Assert.IsNotNull(presenter, "SpawnAlertPresenter present");
            int visibleLines = 0;
            var lines = presenter.GetComponentsInChildren<LineRenderer>(includeInactive: true);
            for (int i = 0; i < lines.Length; i++)
                if (lines[i].enabled) visibleLines++;
            Assert.Greater(visibleLines, 0,
                "사용자 증상: 첫 스폰 전 활성 SpawnAlertGuide LineRenderer가 있어야 한다");

            var em = World.DefaultGameObjectInjectionWorld.EntityManager;
            Entity routed = Entity.Null;
            Entity otherRoute = Entity.Null;
            Entity airborne = Entity.Null;
            Entity groundborne = Entity.Null;
            float spawnDeadline = Time.unscaledTime + 7f;
            while ((routed == Entity.Null || otherRoute == Entity.Null
                    || airborne == Entity.Null || groundborne == Entity.Null)
                   && Time.unscaledTime < spawnDeadline)
            {
                // Temp 배열은 yield 경계를 넘기면 Unity가 프레임 끝에 폐기한다. 반드시 이
                // 블록 안에서 직접 닫은 뒤 다음 프레임으로 넘어간다.
                using (var query = em.CreateEntityQuery(
                           ComponentType.ReadOnly<AttackUnitTag>(),
                           ComponentType.ReadOnly<LocalTransform>()))
                using (var entities = query.ToEntityArray(Allocator.Temp))
                {
                    for (int i = 0; i < entities.Length; i++)
                    {
                        Entity candidate = entities[i];
                        if (!em.HasComponent<WaypointFollow>(candidate)) continue;
                        var progress = em.GetComponentData<WaypointFollow>(candidate);
                        if (progress.pathIndex == 0) routed = candidate;
                        else if (progress.pathIndex == 1) otherRoute = candidate;
                        if (em.HasComponent<PathFollowState>(candidate))
                        {
                            byte layers = em.GetComponentData<PathFollowState>(candidate).traversalLayers;
                            if ((layers & (byte)Wassup.Data.PlacementLayer.Air) != 0) airborne = candidate;
                            else groundborne = candidate;
                        }
                    }
                }
                yield return null;
            }
            Assert.AreNotEqual(Entity.Null, routed, "waypointPathIndex=0 적이 WaypointFollow와 함께 스폰");
            Assert.AreNotEqual(Entity.Null, otherRoute, "waypointPathIndex=1 적도 같은 웨이브에 스폰");
            Assert.AreNotEqual(Entity.Null, airborne, "첫 웨이브에 Air 적(Skimmer)이 있어야 한다");
            Assert.AreNotEqual(Entity.Null, groundborne, "같은 웨이브에 지상 대조군이 있어야 한다");

            yield return null; // 스폰 다음 view sync까지 진행
            Assert.IsTrue(bridge.TryGetUnitViewAnchor(airborne, out var airborneView));
            Assert.IsTrue(bridge.TryGetUnitViewAnchor(groundborne, out var groundView));
            float airborneViewLift = airborneView.position.y
                - ((Vector3)BoardSpace.ToView(em.GetComponentData<LocalTransform>(airborne).Position)).y;
            float groundViewLift = groundView.position.y
                - ((Vector3)BoardSpace.ToView(em.GetComponentData<LocalTransform>(groundborne).Position)).y;
            Assert.Greater(airborneViewLift, groundViewLift + 1.2f,
                "Skimmer의 판독 가능한 flightLift가 기존 view sync를 통해 실제 뷰에 적용돼야 한다");

            // 목표는 이동 seam이다. 골 타워와 교전해 멈추는 콘텐츠 변수를 제거해
            // Marching으로 골까지 흘려보낸다(스폰·SO 부착 검증은 이미 위에서 끝났다).
            if (em.HasComponent<AttackState>(routed)) em.RemoveComponent<AttackState>(routed);
            if (em.HasComponent<AttackState>(otherRoute)) em.RemoveComponent<AttackState>(otherRoute);

            using var fieldQuery = em.CreateEntityQuery(ComponentType.ReadOnly<FlowFieldSingleton>());
            Assert.AreEqual(1, fieldQuery.CalculateEntityCount(), "flow field singleton");
            var field = fieldQuery.GetSingleton<FlowFieldSingleton>();
            var initialProgress = em.GetComponentData<WaypointFollow>(routed);
            int waypointCount = field.WaypointCountAt(initialProgress.pathIndex);
            Assert.Greater(waypointCount, 0, "검증 경로에 waypoint가 있어야 계측이 유효");

            // waypoint-routing unit 4 — 서로 다른 경로의 첫 waypoint를 각각 차단한다.
            // Air 는 자기 차단 셀을 실제 밟아야 하고, 지상 대조군은 자기 차단 셀을 밟으면 안 된다.
            var airborneProgress = em.GetComponentData<WaypointFollow>(airborne);
            var groundProgress = em.GetComponentData<WaypointFollow>(groundborne);
            int airborneWaypointCount = field.WaypointCountAt(airborneProgress.pathIndex);
            int groundWaypointCount = field.WaypointCountAt(groundProgress.pathIndex);
            Assert.Greater(airborneWaypointCount, airborneProgress.index, "Air 적 앞에 차단할 waypoint가 남아 있어야 한다");
            Assert.Greater(groundWaypointCount, groundProgress.index, "지상 적 앞에 차단할 waypoint가 남아 있어야 한다");
            int2 airborneBlockedCell = field.WaypointAt(airborneProgress.pathIndex, airborneProgress.index);
            int2 groundBlockedCell = field.WaypointAt(groundProgress.pathIndex, groundProgress.index);
            bridge.DebugSpawnObstacleAt(airborneBlockedCell, 60f);
            bridge.DebugSpawnObstacleAt(groundBlockedCell, 60f);

            var entryFrames = new List<int>(waypointCount);
            int lastIndex = initialProgress.index;
            bool reachedGoal = false;
            int airborneBlockedCellFrames = 0;
            int groundBlockedCellFrames = 0;

            for (int frame = 0; frame < 2400 && !reachedGoal; frame++)
            {
                if (em.Exists(airborne) && CellOf(em, airborne, in field).Equals(airborneBlockedCell))
                    airborneBlockedCellFrames++;
                if (em.Exists(groundborne) && CellOf(em, groundborne, in field).Equals(groundBlockedCell))
                    groundBlockedCellFrames++;

                if (!em.Exists(routed))
                {
                    reachedGoal = lastIndex == waypointCount;
                    break;
                }

                var progress = em.GetComponentData<WaypointFollow>(routed);
                if (progress.index > lastIndex)
                {
                    Assert.AreEqual(lastIndex + 1, progress.index,
                        "한 프레임에 저작 순서를 건너뛰면 안 된다");
                    int2 entered = CellOf(em, routed, in field);
                    Assert.AreEqual(field.WaypointAt(progress.pathIndex, lastIndex), entered,
                        $"index {lastIndex} waypoint 셀 진입 프레임");
                    entryFrames.Add(frame);
                    lastIndex = progress.index;
                }

                if (em.HasComponent<PastGoalTag>(routed)) reachedGoal = true;
                yield return null;
            }

            Assert.AreEqual(waypointCount, entryFrames.Count,
                "저작된 waypoint를 각각 한 번씩 순서대로 통과");
            for (int i = 1; i < entryFrames.Count; i++)
                Assert.Greater(entryFrames[i], entryFrames[i - 1], "waypoint 통과 프레임 순서");
            Assert.IsTrue(reachedGoal, "마지막 waypoint 이후 골에 도달");
            Assert.Greater(airborneBlockedCellFrames, 0,
                "Air 적은 지상 차단 셀을 실제로 통과해야 한다");
            Assert.AreEqual(0, groundBlockedCellFrames,
                "같은 판 지상 적은 차단 셀 안으로 들어가면 안 된다");
        }

        [UnityTest]
        public IEnumerator DefenderCatalog_BakesPathOnlyAndCombinedTargetMasks()
        {
            RenderTexture.active = null;
            yield return SceneManager.LoadSceneAsync(SceneNames.Battle, LoadSceneMode.Single);
            for (int i = 0; i < 6; i++) yield return null;

            var bridge = Object.FindObjectOfType<BattleBridge>();
            Assert.IsNotNull(bridge, "BattleBridge present");
            var catalogs = Resources.FindObjectsOfTypeAll<DefenderCatalog>();
            Assert.Greater(catalogs.Length, 0, "DefenderCatalog present");
            var archer = catalogs[0].ById("archer");
            var antiAir = catalogs[0].ById("anti_air");
            Assert.IsNotNull(archer, "일반 방어유닛 대조군");
            Assert.IsNotNull(antiAir, "신규 대공사수");

            bridge.SetDefenderPool(new[] { archer, antiAir });
            bridge.BeginPlacement();
            var gameManager = GameManager.Instance;
            Assert.IsNotNull(gameManager);
            gameManager.CostRuntime.ResetToStart();
            gameManager.CostRuntime.AddCost(1000);

            Assert.IsTrue(PlaceFirstValid(bridge, archer, out var archerEntity));
            Assert.IsTrue(PlaceFirstValid(bridge, antiAir, out var antiAirEntity));

            var em = World.DefaultGameObjectInjectionWorld.EntityManager;
            Assert.AreEqual((byte)PlacementLayer.Path,
                em.GetComponentData<AttackState>(archerEntity).targetTraversalLayers,
                "일반 방어유닛의 실제 런타임 AttackState는 지상(Path) 전용");
            Assert.AreEqual((byte)(PlacementLayer.Path | PlacementLayer.Air),
                em.GetComponentData<AttackState>(antiAirEntity).targetTraversalLayers,
                "대공사수의 실제 런타임 AttackState는 Path와 Air를 모두 포함");
            Assert.AreEqual(0.2f,
                em.GetComponentData<AttackState>(antiAirEntity).cooldownDuration, 1e-4f,
                "대공사수의 초고속 공격 주기가 실제 런타임에 베이크돼야 한다");
            var antiAirOutputs = em.GetBuffer<AttackOutputElement>(antiAirEntity);
            Assert.AreEqual(1, antiAirOutputs.Length);
            Assert.AreEqual(7f, antiAirOutputs[0].value.magnitude, 1e-4f,
                "낮은 발당 피해가 실제 런타임 출력에 베이크돼야 한다");
        }

        // waypoint-routing unit 9/10 — 사용자 증상 계측: 「Serpent 을 웨이브 7까지 해도
        // 웨이포인트 방식이 한 번도 안 나온다」.
        //
        // 이 테스트는 **증상 그대로**를 단언한다 — 「내 순수 함수가 옳은 값을 낸다」가 아니라
        // 「라이브 Serpent 판에서 지상 적이 레인 기본 경로를 달고 스폰되는가」. 저작·투영은
        // EditMode 가 이미 지켰으므로, 여기서 빨간불이 뜨면 범인은 스폰 경계다.
        // 풀 인덱스 1=Coil, 4=Zig. 둘 다 레인 하나만 경로 1 로 우회시킨 저작이다
        // (unit 10 rev 3 — 새 경로 50%/62% · 유턴 0).
        [UnityTest]
        public IEnumerator RoutedMap_GroundEnemies_SpawnWithLaneDefaultRoute(
            [Values(1, 4)] int poolIndex)
        {
            DevMapOverride.Index = poolIndex;
            RenderTexture.active = null;
            yield return SceneManager.LoadSceneAsync(SceneNames.Battle, LoadSceneMode.Single);
            for (int i = 0; i < 6; i++) yield return null;

            var bridge = Object.FindObjectOfType<BattleBridge>();
            Assert.IsNotNull(bridge, "BattleBridge present");
            bridge.BeginPlacement();
            yield return null;
            bridge.StartBattle();

            var em = World.DefaultGameObjectInjectionWorld.EntityManager;
            int seenGround = 0, groundWithRoute = 0, groundWithoutRoute = 0, air = 0;
            float deadline = Time.unscaledTime + 12f;
            var counted = new HashSet<Entity>();
            while (Time.unscaledTime < deadline && seenGround < 6)
            {
                using (var query = em.CreateEntityQuery(
                           ComponentType.ReadOnly<AttackUnitTag>(),
                           ComponentType.ReadOnly<PathFollowState>()))
                using (var entities = query.ToEntityArray(Allocator.Temp))
                {
                    for (int i = 0; i < entities.Length; i++)
                    {
                        Entity e = entities[i];
                        if (!counted.Add(e)) continue;
                        byte layers = em.GetComponentData<PathFollowState>(e).traversalLayers;
                        if ((layers & (byte)Wassup.Data.PlacementLayer.Air) != 0) { air++; continue; }
                        seenGround++;
                        if (em.HasComponent<WaypointFollow>(e)
                            && em.GetComponentData<WaypointFollow>(e).pathIndex == 1) groundWithRoute++;
                        else groundWithoutRoute++;
                    }
                }
                yield return null;
            }

            Debug.Log($"[레인경로 계측] map={poolIndex} 지상 {seenGround}기 = 경로1 {groundWithRoute} + 무경로 "
                + $"{groundWithoutRoute} · 공중 {air}기");

            Assert.Greater(seenGround, 0, "지상 적이 한 기도 안 스폰됐다 — 계측 자체가 무효");
            // 레인은 localIndex % 2 로 갈리므로 지상 적 여럿을 보면 레인 0 이 반드시 섞인다.
            Assert.Greater(groundWithRoute, 0,
                "레인 0 에서 스폰된 지상 적이 하나도 경로 1 을 달지 않았다 — "
                + "MapDocument.spawnRoutes → GeneratedMap.RouteForSpawn → SpawnUnit 구간이 끊겼다");
            Assert.Greater(groundWithoutRoute, 0,
                "전원이 경로를 달았다 — 레인 1 대조군이 사라져 저작 의도(한 레인만 우회)가 깨졌다");
        }

        // siege-duel-map 후속 — 「Duel 에 웨이브 개선이 적용되어 있는지 모르겠다」의 라이브 답.
        //
        // Duel 은 풀 본편이 아니라 dev 슬롯(6+3=9)이고, dev 엔트리의 deck 이 null 이면 브리지의
        // 직렬화 deck(구 Deck_WaveA — 컨셉 0·보스 0·적 9종)으로 폴백한다. 실제로 그 상태였다.
        // 여기서 재는 것은 «맵이 뜬다» 가 아니라 **그 판의 웨이브가 현행 세대인가** 다.
        // 풀 6 + dev[3]=Duel(9) · dev[4]=Ford(10) · dev[5]=Isle(11) — 공성 3종.
        [UnityTest]
        public IEnumerator SiegeDevSlot_RunsCurrentGenerationWaves(
            [Values(9, 10, 11)] int slot)
        {
            DevMapOverride.Index = slot;
            RenderTexture.active = null;
            yield return SceneManager.LoadSceneAsync(SceneNames.Battle, LoadSceneMode.Single);
            for (int i = 0; i < 6; i++) yield return null;

            var bridge = Object.FindObjectOfType<BattleBridge>();
            Assert.IsNotNull(bridge, "BattleBridge present");
            bridge.BeginPlacement();
            yield return null;

            // 공성 파생 — 적 마음 셀이 유일한 스폰이다(레인 1개라 레인 경로 축은 N/A).
            Assert.IsTrue(bridge.HasGeneratedMap, $"공성 문서가 dev 슬롯 {slot} 로 해석돼야 한다");

            bridge.StartBattle();
            yield return null;

            // 컨셉 라벨이 붙어 있으면 waveConceptPool 이 실제로 돌았다는 뜻이다.
            // 빈 문자열이면 컨셉 없는 덱(WaveA 폴백)이다 — 이 판별이 이 테스트의 핵심이다.
            Assert.IsFalse(string.IsNullOrEmpty(bridge.NextWaveConceptLabel),
                $"슬롯 {slot}: 웨이브에 컨셉 라벨이 없다 — 컨셉 풀 없는 덱(Deck_WaveA 폴백)으로 돌고 있다");

            var em = World.DefaultGameObjectInjectionWorld.EntityManager;
            int spawned = 0;
            float deadline = Time.unscaledTime + 10f;
            while (Time.unscaledTime < deadline && spawned == 0)
            {
                using (var query = em.CreateEntityQuery(ComponentType.ReadOnly<AttackUnitTag>()))
                    spawned = query.CalculateEntityCount();
                yield return null;
            }
            Assert.Greater(spawned, 0, "공성 스폰(적 마음 셀)에서 적이 실제로 나와야 한다");
        }

        // tutorial-map — 엔트리가 실은 저작 플랜이 덱의 생성 웨이브를 실제로 이기는가.
        //
        // 판별기는 **컨셉 라벨의 부재**다. 저작 플랜(FromPlanAsset)은 컨셉을 만들지 않으므로
        // 라벨이 빈다. 반대로 폴백이 일어나면 Deck_Tutorial(Serpent 복제)의 컨셉 풀이 돌아
        // 라벨이 채워진다 — 즉 라벨이 비어 있음 = 저작 플랜이 쓰이고 있음.
        [UnityTest]
        public IEnumerator TutorialDevSlot_UsesAuthoredPlan_NotGeneratedWaves()
        {
            DevMapOverride.Index = 12;   // 풀 6 + dev[6] = Tutorial
            RenderTexture.active = null;
            yield return SceneManager.LoadSceneAsync(SceneNames.Battle, LoadSceneMode.Single);
            for (int i = 0; i < 6; i++) yield return null;

            var bridge = Object.FindObjectOfType<BattleBridge>();
            Assert.IsNotNull(bridge, "BattleBridge present");
            Assert.IsTrue(bridge.HasGeneratedMap, "튜토리얼 문서가 dev 슬롯 12 로 해석돼야 한다");

            bridge.BeginPlacement();
            yield return null;
            bridge.StartBattle();
            yield return null;

            Assert.IsTrue(string.IsNullOrEmpty(bridge.NextWaveConceptLabel),
                "컨셉 라벨이 붙어 있다 — 저작 플랜이 아니라 덱의 생성 웨이브로 돌고 있다. "
                + "MapDocumentPool.Entry.plan 배선 또는 TryInitializeGeneratedWaves 우선순위를 확인하라");

            var world = World.DefaultGameObjectInjectionWorld;
            Assert.IsNotNull(world, "기본 ECS 월드가 없다");
            var em = world.EntityManager;

            // 튜토리얼은 **전 레인이 웨이포인트로 움직인다**(목표 최단거리가 아니라 저작 경로).
            // 레인 0 → 경로 1, 레인 1 → 경로 2 이므로 스폰된 지상 적은 전부 그 둘 중 하나를 단다.
            int spawned = 0, routed = 0;
            var seenPaths = new HashSet<int>();
            var counted = new HashSet<Entity>();
            float deadline = Time.unscaledTime + 12f;
            while (Time.unscaledTime < deadline && spawned < 4)
            {
                using (var query = em.CreateEntityQuery(
                           ComponentType.ReadOnly<AttackUnitTag>(),
                           ComponentType.ReadOnly<PathFollowState>()))
                using (var entities = query.ToEntityArray(Allocator.Temp))
                {
                    for (int i = 0; i < entities.Length; i++)
                    {
                        Entity e = entities[i];
                        if (!counted.Add(e)) continue;
                        spawned++;
                        if (!em.HasComponent<WaypointFollow>(e)) continue;
                        routed++;
                        seenPaths.Add(em.GetComponentData<WaypointFollow>(e).pathIndex);
                    }
                }
                yield return null;
            }
            Assert.Greater(spawned, 0, "저작 웨이브 1 이 실제로 적을 내보내야 한다");
            Assert.AreEqual(spawned, routed,
                $"{spawned}기 중 {routed}기만 웨이포인트를 달았다 — 최단거리로 걷는 적이 남아 있다");
            foreach (int p in seenPaths)
                Assert.Greater(p, 0, "지상 레인이 Air 경로(0)를 타고 있다");
        }

        private static bool PlaceFirstValid(
            BattleBridge bridge, DefenderUnitData unit, out Entity entity)
        {
            for (int x = -24; x < 48; x++)
            for (int y = -24; y < 48; y++)
            {
                if (!bridge.CanPlaceDefenderAt(x, y, unit, out _)) continue;
                if (!bridge.PlaceDefenderAs(x, y, unit)) continue;
                if (bridge.TryGetDefenderAt(new Vector2Int(x, y), out entity)) return true;
            }

            entity = Entity.Null;
            return false;
        }

        private static int2 CellOf(EntityManager em, Entity entity, in FlowFieldSingleton field)
            => GridMath.WorldToCell(
                em.GetComponentData<LocalTransform>(entity).Position,
                field.tileSize, field.gridSize, field.origin);
    }
}
