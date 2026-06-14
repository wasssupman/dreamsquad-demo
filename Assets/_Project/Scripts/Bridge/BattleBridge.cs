using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Rendering;
using Unity.Transforms;
using UnityEngine;
using UnityEngine.UI;
using Wassup.Battle.Combat;
using Wassup.Battle.Combat.Projectile;
using Wassup.Battle.Effects;
using Wassup.Battle.Movement;
using Wassup.Battle.Units;
using Wassup.Battle.Units.HealthBar;
using Wassup.Core;
using Wassup.Data;
using Wassup.Data.MapGrid;
using Wassup.Data.Season;
using Wassup.Presentation.Backdrop;
using Wassup.Rendering;
using Wassup.UI;
using TMPro;
// DraftController lives in Wassup.Core above.

namespace Wassup.Bridge
{
    // The ONLY allowed bridge between MonoBehaviour world and ECS world.
    // External MonoBehaviour code must go through this class — no direct EntityManager / World / SystemAPI access.
    public class BattleBridge : MonoBehaviour
    {
        [SerializeField] private AttackDeck deck;
        [SerializeField] private MapData map;
        [SerializeField] private MapGenerationSettings mapSettings;
        [Header("Phase 10B - Procedural (legacy)")]
        [SerializeField] private bool useProcedural = true;
        [Header("Map Grid Generation (new)")]
        [SerializeField] private MapSource mapSource = MapSource.Legacy;
        [SerializeField] private MapGridGenerationSettings mapGridSettings;
        [SerializeField] private MapDocument mapDocument;
        [Header("Season")]
        [SerializeField] private SeasonRegistry seasonRegistry;
        [SerializeField] private bool enableSeasonBackdrop = true;
        [SerializeField] private MapPathShape mapPathShape = MapPathShape.Free;
        [SerializeField] private MapGenerationOptions mapGenerationOptions = MapGenerationOptions.Default;
        [SerializeField] private float tileSize = 1f;
        [SerializeField] private float spawnHeight = 0.5f;
        [SerializeField] private ResultScreen resultScreen;
        [SerializeField] private DefenderUnitData[] defenderPool;
        [SerializeField] private DraftController draftController;
        [SerializeField] private SkillRuntime skillRuntime;
        [SerializeField] private PlacementPhaseView _placementPhaseView;
        [SerializeField] private Wassup.Presentation.SpineUnitPool spineUnitPool;
        [SerializeField] private Wassup.Presentation.QuadUnitViewPool enemyViewPool;
        [SerializeField] private Wassup.Presentation.QuadUnitViewPool defenderFallbackViewPool;
        [SerializeField] private float spineDefenderYOffset = 0f;
        [Header("Character Billboard")]
        // Spine units have no shader billboard (unlike the Quad fallback that uses
        // Billboard_Unlit), so they stand fully upright in the XY plane and read as
        // stiff standees under the angled camera. Tilt them back around X toward the
        // camera so they look planted. 0 = upright, larger = leans toward the camera.
        // Pivots at the unit origin (feet) → sorting/position unaffected.
        [SerializeField] private float characterBillboardTilt = 35f;
        [SerializeField] private Wassup.Presentation.VfxSpawner vfxSpawner;
        [SerializeField] private Wassup.Presentation.DamageNumberSpawner damageNumberSpawner;
        [SerializeField] private Wassup.UI.ScoreHudView scoreHud;
        [SerializeField] private Wassup.Presentation.ProjectileViewPool _projectileViewPool;
        // Phase 9 P9-07 — tileSize 단일 소스화. Awake 에서 MapView/PlacementInput 으로 주입.
        [SerializeField] private Wassup.Core.MapView mapView;
        [SerializeField] private Wassup.Core.PlacementInput placementInput;
        [Header("Tilemap View Backend (tilemap-view-backend)")]
        [SerializeField] private Wassup.Core.BoardViewMode boardViewMode = Wassup.Core.BoardViewMode.Legacy3D;
        [SerializeField] private Wassup.Core.TilemapMapView tilemapMapView;
        [SerializeField] private Wassup.Data.TileSetData tileSet;
        [SerializeField] private Wassup.Data.BoardCameraPreset tilemapCameraPresetRect;
        [SerializeField] private Wassup.Data.BoardCameraPreset tilemapCameraPresetIso;
        [Header("Tilemap mode tuning (tilemap-mode-adoption)")]
        [SerializeField] private float legacyCharacterScale = 0.7f;
        [SerializeField] private float tilemapCharacterScale = 0.42f;
        [SerializeField] private float tilemapBillboardTilt = 0f;
        [Header("Stack Modifier Registry")]
        [SerializeField] private Wassup.Data.StackModifierSO[] stackModifierAuthoring;

        private ManualMapInput? _manualMapInput;
        private GeneratedMap _generatedMap;
        private GameObject _backdropRoot;
        private int2? _mapGridGridSizeOverride;

        private World _world;
        private EntityManager _em;
        private EntityQuery _aliveAttackersQuery;
        private bool _aliveAttackersQueryCreated;
        private readonly List<PendingSpawnEntry> _pending = new();
        private readonly List<Material> _ownedRuntimeMaterials = new();
        private readonly HashSet<Vector2Int> _occupiedTiles = new();
        private readonly Dictionary<Vector2Int, (Entity entity, DefenderUnitData data)> _defenderByTile = new();
        private readonly HashSet<Entity> _onPlaceTriggeredEntities = new();
        private readonly List<ProjectileData> _projectileDataByIndex = new();
        private readonly Dictionary<ProjectileData, int> _projectileDataIndex = new();
        private readonly List<HazardSO> _zoneHazardRegistry = new();
        private readonly Dictionary<HazardSO, int> _zoneHazardIndex = new();
        private readonly List<BlockingHazardSO> _blockingHazardSoRegistry = new();
        private readonly Dictionary<BlockingHazardSO, int> _blockingHazardSoIndex = new();
        private static readonly Dictionary<Wassup.Battle.Effects.StackKind, Wassup.Data.ThresholdRule[]> _stackThresholds = new();
        private readonly Dictionary<Entity, GameObject> _blockingHazardVisualMap = new();
        private Transform _blockingHazardVisualRoot;
        private EntityQuery _projectileSpawnRequestQuery;
        private bool _projectileSpawnRequestQueryCreated;
        private EntityQuery _projectileQuery;
        private bool _projectileQueryCreated;
        private RenderMeshArray _healthBarRenderArray;
        private Material _healthBarMaterial;
        // tilemap-mode-adoption unit 0 — 모드별 유닛 스케일. const 제거. 맵 빌드 시 모드 기준으로 설정.
        // 기본값 = Legacy 값(0.7) 이라 빌드 전/Legacy3D 에서 현행 동일.
        public static float CharacterVisualScale { get; private set; } = 0.7f;
        // Live-readable mirror of characterBillboardTilt, read by SpineUnitView each
        // LateUpdate. Synced from the serialized field in Awake/OnValidate; can be
        // poked at runtime (e.g. via tooling) to tune the lean without recompiling.
        // tilemap-mode-adoption unit 0 — 맵 빌드 시 Tilemap 모드면 tilemapBillboardTilt(기본 0) 로 덮어쓴다.
        public static float CharacterBillboardTilt = 35f;
        private const float SynergyPerNeighbor = 0.1f;
        private readonly HashSet<Entity> _synergyActivatedEntities = new();
        private int _synergyActivations;
        private int _synergyPeakCount;
        private float _startTime;
        private float _timerDuration;
        private bool _running;
        private bool _placementAllowed;
        private bool _resultShown;
        // draft-stage-map-prebuild Unit 0 — ECS infrastructure idempotent guard.
        private bool _ecsInfrastructureReady;
        private bool _usingGeneratedWaves;
        private GeneratedWavePlan _wavePlan;
        private int _nextWaveIndex;
        private Button _nextWaveButton;
        private TextMeshProUGUI _nextWaveLabel;
        private int _goalReachedCount;
        private NativeQueue<GoalReachedEvent> _goalEventQueue;
        private NativeQueue<DefenderDeathEvent> _defenderDeathQueue;
        private NativeQueue<Wassup.Battle.Combat.MeteorBurstEvent> _meteorBurstQueue;
        private NativeQueue<Wassup.Battle.Combat.UnitAttackVisualEvent> _unitAttackVisualQueue;
        private NativeQueue<Wassup.Battle.Combat.Projectile.ProjectileHitEvent> _projectileHitEventQueue;
        private NativeQueue<Wassup.Battle.Units.HealAppliedEvent> _healAppliedEventQueue;
        private NativeQueue<Wassup.Battle.Units.DamageNumberEvent> _damageNumberEventQueue;
        private NativeQueue<Wassup.Battle.Units.EnemyKilledEvent> _enemyKilledEventQueue;
        private NativeQueue<Wassup.Battle.Effects.EnemyCcEvent> _enemyCcQueue;
        private NativeQueue<Wassup.Battle.Effects.StatModifierApplyEvent> _statModifierQueue;
        private NativeQueue<Wassup.Battle.Effects.StackModifierApplyEvent> _stackModifierQueue;
        private NativeQueue<Wassup.Battle.Effects.HazardRuntimeEvent> _hazardRuntimeEventQueue;
        private NativeQueue<Wassup.Battle.Effects.HazardDestroyedEvent> _hazardDestroyedQueue;
        private NativeQueue<Wassup.Battle.Effects.HazardSpawnRequest> _hazardSpawnRequestQueue;
        private NativeQueue<Wassup.Battle.Combat.AttackOutputLogEvent> _attackOutputLogQueue;
        private NativeQueue<Wassup.Battle.Movement.MovementPauseRequest> _movementPauseRequestQueue;
        private Unity.Collections.NativeHashSet<Unity.Mathematics.int2> _blockedCells;
        private Unity.Collections.NativeParallelMultiHashMap<Unity.Mathematics.int2, Wassup.Battle.Effects.HazardEffect> _hazardCellToEffects;

        // Phase 9 flow field 싱글톤 entity reference
        private Entity _flowFieldSingleton = Entity.Null;

        // map-origin-placement: board 월드 원점. 모든 grid↔world 변환의 단일 소스.
        // BuildFlowField 에서 mapView.transform.position 으로 캡처. mapView 없으면 zero.
        private float3 _boardOrigin = float3.zero;
        // tilemap-view-backend — Tilemap 뷰 경로 여부 (Legacy3D = false). 맵 빌드/헬스바/backdrop 분기 기준.
        private bool UseTilemapView => boardViewMode != Wassup.Core.BoardViewMode.Legacy3D;
        private bool _healthBarRenderGateLogged;

        // match-seed-unification — GameManager 가 주입하는 단일 매치 시드.
        // 맵/웨이브/비주얼 시드가 여기서 파생된다(작업 2/3). 0 = 미주입(즉석 폴백).
        private int _matchSeed;
        public void SetMatchSeed(int seed) => _matchSeed = seed;

        private struct PendingSpawnEntry
        {
            public SpawnEntry entry;
            public int deckIndex;
        }

        private void Awake()
        {
            if (mapView == null)
                Debug.LogError("[BattleBridge] mapView reference missing — assign in Inspector.", this);

            if (placementInput == null)
                Debug.LogError("[BattleBridge] placementInput reference missing — assign in Inspector.", this);

            SeasonRuntime.Bind(seasonRegistry);
            if (seasonRegistry == null || seasonRegistry.activeSeason == null
                || seasonRegistry.activeSeason.mapTheme == null)
            {
                Debug.LogError("[BattleBridge] SeasonRegistry / activeSeason / mapTheme 가 wiring 되지 않았다. BattleScene 에 SeasonRegistry.asset 을 연결하라.", this);
            }

            CharacterBillboardTilt = characterBillboardTilt;

            EnsureMonoViewPools();
        }

        private void OnValidate()
        {
            // Keep the static mirror in sync while tuning in the inspector (edit/play).
            CharacterBillboardTilt = characterBillboardTilt;
        }

        private void Start()
        {
            if (resultScreen != null)
            {
                resultScreen.RestartRequested += OnRestartRequested;
                resultScreen.RedraftRequested += OnRedraftRequested;
            }
        }

        private void OnRestartRequested()
        {
            if (_world == null)
            {
                _placementPhaseView?.BeginPlacementPhase();
                return;
            }

            var logger = GameManager.Instance?.Logger;
            if (logger != null)
            {
                logger.EndSession();
                logger.StartSession();
                // Phase 7 (Q6=a): Restart keeps the same picked skill loadout,
                // but logger.StartSession() created an empty SkillRecord — so
                // re-populate skill.loadout/pool/seed so the new log file carries
                // the same audit trail the player actually plays with.
                ReLogSkillLoadoutForNewSession(logger);
            }

            TeardownCurrentBattle();
            if (resultScreen != null) resultScreen.Hide();
            _running = false;
            _resultShown = false;
            _placementPhaseView?.BeginPlacementPhase();
        }

        private void ReLogSkillLoadoutForNewSession(Logging.BattleLogger logger)
        {
            if (logger == null) return;
            var ctl = GameManager.Instance?.SkillLoadout;
            if (ctl == null || ctl.Picked.Count == 0) return;
            var pickedIds = new System.Collections.Generic.List<string>();
            foreach (var s in ctl.Picked) if (s != null) pickedIds.Add(s.id);
            logger.SetSkillLoadout(pickedIds);
            var poolIds = new System.Collections.Generic.List<string>();
            foreach (var s in ctl.Pool) if (s != null) poolIds.Add(s.id);
            logger.SetSkillPool(poolIds, ctl.Seed);
        }

        private void OnRedraftRequested()
        {
            if (draftController == null)
            {
                Debug.LogWarning("[BattleBridge] RedraftRequested but draftController unset; falling back to RestartBattle.");
                RestartBattle();
                return;
            }
            // Re-opening the draft invalidates the previous session — roll the log,
            // tear down ECS state, hide the result panel, then let DraftController
            // show the pick UI. StartBattle will fire again inside TryConfirm.
            var logger = GameManager.Instance?.Logger;
            if (logger != null)
            {
                logger.EndSession();
                logger.StartSession();
            }
            if (_world != null) TeardownCurrentBattle();
            if (resultScreen != null) resultScreen.Hide();
            _running = false;
            _resultShown = false;
            // draft-stage-map-prebuild Unit 3 — TeardownCurrentBattle disposed the map;
            // rebuild it before re-entering draft so the playfield is ready.
            PrepareDraftMap();
            draftController.BeginDraft();
        }

        // External MB entry point — tears down current ECS state and starts a fresh session on the same deck/map.
        public void RestartBattle()
        {
            if (_world == null)
            {
                // No battle started yet — delegate to StartBattle.
                StartBattle();
                return;
            }
            // Roll the log over: close current session, start a fresh one so each battle has its own JSON file.
            var logger = GameManager.Instance?.Logger;
            if (logger != null)
            {
                logger.EndSession();
                logger.StartSession();
            }
            TeardownCurrentBattle();
            if (resultScreen != null) resultScreen.Hide();
            StartBattle();
        }

        private void TeardownCurrentBattle()
        {
            BackdropMounter.Unmount(ref _backdropRoot);
            _running = false;
            _placementAllowed = false;
            if (skillRuntime != null) skillRuntime.ResetAll();
            if (GameManager.Instance != null && GameManager.Instance.CostRuntime != null)
                GameManager.Instance.CostRuntime.StopRegen();
            if (spineUnitPool != null) spineUnitPool.DisposeAll();
            if (enemyViewPool != null) enemyViewPool.DisposeAll();
            if (defenderFallbackViewPool != null) defenderFallbackViewPool.DisposeAll();
            ClearBlockingHazardVisuals();
            SetNextWaveButtonVisible(false);

            if (HasLiveEntityManager())
            {
                // FlowFieldSingleton owns Persistent NativeArrays in its component data.
                // Dispose those before any broad singleton entity cleanup.
                TeardownFlowField();
                DestroyEcsInfrastructureEntities();
                DestroyBattleEntities();
            }
            else
            {
                _flowFieldSingleton = Entity.Null;
            }

            DisposeEcsInfrastructureNativeContainers();
            DisposeCachedQueries();
            _zoneHazardRegistry.Clear();
            _zoneHazardIndex.Clear();
            _blockingHazardSoRegistry.Clear();
            _blockingHazardSoIndex.Clear();

            // Phase 10A (P10A-04A): dispose GeneratedMap (idempotent) alongside FlowField.
            TeardownGeneratedMap();
            // draft-stage-map-prebuild Unit 0 — allow EnsureQueriesAndQueues to reinitialise on next entry.
            _ecsInfrastructureReady = false;
        }

        private bool HasLiveEntityManager()
            => _world != null && _world.IsCreated && _em != default;

        private void DestroyBattleEntities()
        {
            DestroyEntitiesByType<AttackUnitTag>();
            DestroyEntitiesByType<DefenderUnitTag>();
            DestroyEntitiesByType<ProjectileTag>();
            DestroyEntitiesByType<HealthBarTag>();
            DestroyEntitiesByType<Wassup.Battle.Effects.Hazard>();
            DestroyEntitiesByType<Wassup.Battle.Effects.BlockingHazard>();
            DestroyEntitiesByType<Wassup.Battle.Effects.Obstacle>();
        }

        private void DestroyEcsInfrastructureEntities()
        {
            DestroyEntitiesByType<GoalReachedEventsSingleton>();
            DestroyEntitiesByType<DefenderDeathEventsSingleton>();
            DestroyEntitiesByType<Wassup.Battle.Combat.MeteorBurstEventsSingleton>();
            DestroyEntitiesByType<Wassup.Battle.Combat.UnitAttackVisualEventsSingleton>();
            DestroyEntitiesByType<Wassup.Battle.Combat.Projectile.ProjectileHitEventsSingleton>();
            DestroyEntitiesByType<Wassup.Battle.Units.HealAppliedEventsSingleton>();
            DestroyEntitiesByType<Wassup.Battle.Units.DamageNumberEventsSingleton>();
            DestroyEntitiesByType<Wassup.Battle.Units.EnemyKilledEventsSingleton>();
            DestroyEntitiesByType<Wassup.Battle.Effects.EnemyCcEventsSingleton>();
            DestroyEntitiesByType<Wassup.Battle.Effects.StatModifierApplyEventsSingleton>();
            DestroyEntitiesByType<Wassup.Battle.Effects.StackModifierApplyEventsSingleton>();
            DestroyEntitiesByType<Wassup.Battle.Effects.HazardRuntimeEventsSingleton>();
            DestroyEntitiesByType<Wassup.Battle.Effects.HazardDestroyedEventsSingleton>();
            DestroyEntitiesByType<Wassup.Battle.Effects.HazardSpawnRequestsSingleton>();
            DestroyEntitiesByType<Wassup.Battle.Combat.AttackOutputLogEventsSingleton>();
            DestroyEntitiesByType<Wassup.Battle.Movement.MovementPauseRequestEventsSingleton>();
            DestroyEntitiesByType<Wassup.Battle.Effects.ObstacleSingleton>();
            DestroyEntitiesByType<Wassup.Battle.Effects.HazardSingleton>();
        }

        private void DisposeEcsInfrastructureNativeContainers()
        {
            if (_goalEventQueue.IsCreated) _goalEventQueue.Dispose();
            if (_defenderDeathQueue.IsCreated) _defenderDeathQueue.Dispose();
            if (_meteorBurstQueue.IsCreated) _meteorBurstQueue.Dispose();
            if (_unitAttackVisualQueue.IsCreated) _unitAttackVisualQueue.Dispose();
            if (_projectileHitEventQueue.IsCreated) _projectileHitEventQueue.Dispose();
            if (_healAppliedEventQueue.IsCreated) _healAppliedEventQueue.Dispose();
            if (_damageNumberEventQueue.IsCreated) _damageNumberEventQueue.Dispose();
            if (_enemyKilledEventQueue.IsCreated) _enemyKilledEventQueue.Dispose();
            if (_enemyCcQueue.IsCreated) _enemyCcQueue.Dispose();
            if (_statModifierQueue.IsCreated) _statModifierQueue.Dispose();
            if (_stackModifierQueue.IsCreated) _stackModifierQueue.Dispose();
            if (_attackOutputLogQueue.IsCreated) _attackOutputLogQueue.Dispose();
            if (_movementPauseRequestQueue.IsCreated) _movementPauseRequestQueue.Dispose();
            if (_hazardRuntimeEventQueue.IsCreated) _hazardRuntimeEventQueue.Dispose();
            if (_hazardDestroyedQueue.IsCreated) _hazardDestroyedQueue.Dispose();
            if (_hazardSpawnRequestQueue.IsCreated) _hazardSpawnRequestQueue.Dispose();
            if (_blockedCells.IsCreated) _blockedCells.Dispose();
            if (_hazardCellToEffects.IsCreated) _hazardCellToEffects.Dispose();
        }

        private void DisposeCachedQueries()
        {
            if (!HasLiveEntityManager())
            {
                _aliveAttackersQueryCreated = false;
                _projectileSpawnRequestQueryCreated = false;
                _projectileQueryCreated = false;
                return;
            }

            if (_aliveAttackersQueryCreated)
            {
                _aliveAttackersQuery.Dispose();
                _aliveAttackersQueryCreated = false;
            }
            if (_projectileSpawnRequestQueryCreated)
            {
                _projectileSpawnRequestQuery.Dispose();
                _projectileSpawnRequestQueryCreated = false;
            }
            if (_projectileQueryCreated)
            {
                _projectileQuery.Dispose();
                _projectileQueryCreated = false;
            }
        }

        // Idempotent: 재호출(판 재시작/redraft) 시 기존 Persistent arrays dispose 후 재생성.
        // CRITICAL #1 (Codex 2차 리뷰): AddComponentData 는 component 존재 시 throw,
        // 그리고 기존 arrays 가 dispose 없이 덮어써지면 누수. TeardownFlowField 선행으로 해결.
        private void BuildFlowField()
        {
            if (!_generatedMap.IsCreated || _em == null) return;

            // 기존 싱글톤 있으면 arrays dispose + entity destroy (멱등성 보장)
            TeardownFlowField();

            // map-origin-placement: _boardOrigin 은 BuildMapForBattle 이 mapView.Initialize 직후
            // 캡처한다(backdrop Mount 가 BuildFlowField 보다 먼저 호출되므로 그쪽이 단일 캡처 지점).

            int w = _generatedMap.gridSize.x;
            int h = _generatedMap.gridSize.y;
            int n = w * h;

            var walk = new NativeArray<byte>(n, Allocator.Temp);
            try
            {
                for (int i = 0; i < n; i++)
                    walk[i] = (byte)(_generatedMap.tiles[i] == MapTileType.Walk ? 1 : 0);

                var flow = new NativeArray<float2>(n, Allocator.Persistent);
                var dist = new NativeArray<int>(n, Allocator.Persistent);
                try
                {
                    var gridSize = _generatedMap.gridSize;
                    var goal = _generatedMap.goal;

                    FlowFieldBuilder.Build(walk, gridSize, goal, flow, dist);

                    var data = new FlowFieldSingleton
                    {
                        flow = flow,
                        dist = dist,
                        gridSize = gridSize,
                        goalCell = goal,
                        tileSize = tileSize,
                        origin = _boardOrigin,
                        version = _generatedMap.generatorVersion,
                    };

                    _flowFieldSingleton = _em.CreateEntity();
                    _em.AddComponentData(_flowFieldSingleton, data);
                    Debug.Log($"[BattleBridge] FlowField built — boardOrigin={_boardOrigin} tileSize={tileSize} grid={gridSize}");
                }
                catch
                {
                    if (flow.IsCreated) flow.Dispose();
                    if (dist.IsCreated) dist.Dispose();
                    throw;
                }
            }
            finally
            {
                if (walk.IsCreated) walk.Dispose();
            }
        }

        // Phase 10A (P10A-04A): GeneratedMap dispose 멱등. 재시작/redraft 시 TearDown 후 재생성.
        private void TeardownGeneratedMap()
        {
            // Tilemap 뷰 잔상 제거 (RebuildDraftMap 재진입 / 전투 종료 안전). Clear 는 idempotent.
            if (tilemapMapView != null) tilemapMapView.Clear();
            if (_generatedMap.IsCreated) _generatedMap.Dispose();
            _generatedMap = default;
        }

        private int2 GridSize
        {
            get
            {
                var normalized = mapGenerationOptions.Normalized();
                if (normalized.gridSize.x > 0 && normalized.gridSize.y > 0)
                    return normalized.gridSize;
                return mapSettings != null
                    ? new int2(mapSettings.gridWidth, mapSettings.gridHeight)
                    : new int2(20, 10);
            }
        }

        private int GeneratorVersion => mapSettings != null ? mapSettings.generatorVersion : 1;

        private void BuildMapForBattle()
        {
            TeardownGeneratedMap();
            TeardownFlowField();

            var theme   = SeasonRuntime.Active?.mapTheme;
            var backdrop = SeasonRuntime.Active?.backdrop;

            // match-seed-unification — 맵 시드는 GameManager 주입 matchSeed 에서 파생.
            // 미주입(0, 예: 테스트 직접 호출) 시 즉석 random matchSeed 로 폴백해 항상 유효.
            int matchSeed = _matchSeed != 0 ? _matchSeed : Wassup.Core.MatchSeed.GenerateRandom();
            int seed = Wassup.Core.MatchSeed.DeriveMapSeed(matchSeed);
            int version = GeneratorVersion;
            var options = mapGenerationOptions.Normalized();
            mapPathShape = options.pathShape;
            int2 gridSize = options.gridSize;

            switch (mapSource)
            {
                case MapSource.Manual:
                    if (_manualMapInput.HasValue)
                        _generatedMap = BattleMapBuilder.BuildFromManual(_manualMapInput.Value, seed, version);
                    else
                        goto case MapSource.Fixture;
                    break;

                case MapSource.Procedural_Legacy:
                    _generatedMap = ProceduralMapGenerator.Generate(
                        seed, gridSize, theme, version,
                        options.pathShape, options.spawnLaneCount, options.MinPlaceableRatio);
                    break;

                case MapSource.Fixture:
                    if (map == null)
                    {
                        Debug.LogError("[BattleBridge] map reference missing — cannot build fixture GeneratedMap.", this);
                        _generatedMap = BattleMapBuilder.BuildFallbackLinear(gridSize, seed, version, options.spawnLaneCount);
                    }
                    else
                    {
                        _generatedMap = BattleMapBuilder.BuildFromFixture(map, seed, version);
                    }
                    break;

                case MapSource.MapGrid:
                    try
                    {
                        _generatedMap = MapGridBattleAdapter.Build(seed, mapGridSettings, mapDocument, _mapGridGridSizeOverride);
                    }
                    catch (MapGenerationFailedException ex)
                    {
                        Debug.LogError($"[BattleBridge] {ex.Message}", this);
                        _generatedMap = default;
                        return;
                    }
                    break;

                case MapSource.Legacy:
                default:
                    if (_manualMapInput.HasValue)
                    {
                        _generatedMap = BattleMapBuilder.BuildFromManual(_manualMapInput.Value, seed, version);
                    }
                    else if (useProcedural)
                    {
                        _generatedMap = ProceduralMapGenerator.Generate(
                            seed, gridSize, theme, version,
                            options.pathShape, options.spawnLaneCount, options.MinPlaceableRatio);
                    }
                    else if (map == null)
                    {
                        Debug.LogError("[BattleBridge] map reference missing — cannot build fixture GeneratedMap.", this);
                        _generatedMap = BattleMapBuilder.BuildFallbackLinear(gridSize, seed, version, options.spawnLaneCount);
                    }
                    else
                    {
                        _generatedMap = BattleMapBuilder.BuildFromFixture(map, seed, version);
                    }
                    break;
            }

            // MapGrid 케이스는 Validator 가 이미 connectivity 보장 — fallback skip.
            if (mapSource != MapSource.MapGrid && !MapConnectivity.AllSpawnsReachGoal(_generatedMap))
            {
                Debug.LogWarning("[BattleBridge] GeneratedMap connectivity failed; using fallback linear map.", this);
                TeardownGeneratedMap();
                _generatedMap = BattleMapBuilder.BuildFallbackLinear(gridSize, seed, version, options.spawnLaneCount);
            }

            // tilemap-mode-adoption unit 0 — 모드별 유닛 스케일/틸트를 빌드 시 1회 확정 (유닛 스폰 전).
            CharacterVisualScale = UseTilemapView ? tilemapCharacterScale : legacyCharacterScale;
            CharacterBillboardTilt = UseTilemapView ? tilemapBillboardTilt : characterBillboardTilt;

            if (UseTilemapView)
            {
                if (tilemapMapView != null)
                    tilemapMapView.Initialize(_generatedMap, tileSize, tileSet, boardViewMode);
                else
                    Debug.LogError("[BattleBridge] boardViewMode 이 Tilemap 이지만 tilemapMapView 가 비어있다.", this);
                // Tilemap 모드 sim origin 은 무조건 zero — 비활성 mapView transform 을 읽지 않는다 (README 계약).
                _boardOrigin = float3.zero;
            }
            else
            {
                // map-origin-placement: board 원점을 MapView.transform.position 에서 1회 캡처.
                if (mapView != null) mapView.Initialize(_generatedMap, tileSize, theme);
                _boardOrigin = mapView != null ? (float3)mapView.transform.position : float3.zero;
            }
            if (placementInput != null) placementInput.Initialize(_generatedMap, tileSize);

            // Backdrop mount — Legacy3D 에서만. always Unmount first so RebuildDraftMap is safe.
            BackdropMounter.Unmount(ref _backdropRoot);
            if (!UseTilemapView && enableSeasonBackdrop && backdrop != null && Camera.main != null)
                _backdropRoot = BackdropMounter.Mount(_generatedMap, Camera.main, backdrop, tileSize, BoardOrigin);

            // sim↔view 변환의 단일 지점 — BuildFlowField 직전 1회 설정 (Legacy3D = identity).
            Wassup.Core.BoardSpace.Configure(boardViewMode, BoardOrigin, tileSize,
                tilemapMapView != null ? tilemapMapView.Grid : null);

            if (UseTilemapView) ApplyTilemapCameraPreset(); // 모드별 ortho 카메라 — 매 빌드 idempotent 재적용

            BuildFlowField();

            // props/obstacles — Legacy3D 에서만 (Tilemap 모드 외곽 연출은 후속 theming spec).
            // MapGrid 는 Walk + Place 만 칠하고 Env/Deco 가 없음 — prop placer 가 빈손이 됨.
            if (!UseTilemapView && mapView != null && theme != null && mapSource != MapSource.MapGrid)
            {
                if (theme.tileProps != null && theme.tileProps.Length > 0)
                {
                    var visualPlan = mapView.VisualPlan;
                    var placements = BackgroundPropPlacer.Generate(visualPlan, theme, _generatedMap.seed);
                    mapView.InstantiateBackgroundProps(visualPlan, theme, placements);
                }
                else
                {
                    mapView.InstantiateObstacles(_generatedMap, theme);
                }
            }

            GameManager.Instance?.Logger?.LogMap(
                _generatedMap.seed,
                _generatedMap.generatorVersion,
                _generatedMap.gridSize,
                _generatedMap.spawns.Length,
                options.pathShape.ToString());
            Debug.Log($"[BattleBridge] Map: seed={_generatedMap.seed} ver={_generatedMap.generatorVersion} shape={options.pathShape} density={options.obstacleDensity} size={_generatedMap.gridSize} spawns={_generatedMap.spawns.Length}");
        }

        private void TeardownFlowField()
        {
            if (_world == null || !_world.IsCreated || _em == default)
            {
                _flowFieldSingleton = Entity.Null;
                return;
            }
            if (_flowFieldSingleton != Entity.Null && _em != null && _em.Exists(_flowFieldSingleton))
            {
                if (_em.HasComponent<FlowFieldSingleton>(_flowFieldSingleton))
                {
                    var data = _em.GetComponentData<FlowFieldSingleton>(_flowFieldSingleton);
                    data.Dispose();
                }
                _em.DestroyEntity(_flowFieldSingleton);
            }
            _flowFieldSingleton = Entity.Null;
        }

        // Phase 6: placement phase enters this path — ECS state is initialized so
        // PlaceDefenderAs works immediately, but spawns / timer stay dormant.
        public void BeginPlacement()
        {
            if (deck == null || map == null)
            {
                Debug.LogError("[BattleBridge] deck or map reference missing.", this);
                return;
            }
            _world = World.DefaultGameObjectInjectionWorld;
            if (_world == null)
            {
                Debug.LogWarning("[BattleBridge] Default World not ready at BeginPlacement; will retry.");
                return;
            }
            _em = _world.EntityManager;
            _pending.Clear();
            _occupiedTiles.Clear();
            _defenderByTile.Clear();
            // ingame-dreamcatcher Unit 2/3 — reset card registry + triggers for a new match.
            _activeDcEffects.Clear();
            _dcStackCounter = 100;
            _firstDefenderPlacedFired = false;
            _onPlaceTriggeredEntities.Clear();
            _synergyActivatedEntities.Clear();
            _synergyActivations = 0;
            _synergyPeakCount = 0;
            _goalReachedCount = 0;
            _running = false;
            _placementAllowed = true;
            _resultShown = false;
            if (skillRuntime != null) skillRuntime.ResetAll();
            _usingGeneratedWaves = false;
            _wavePlan = default;
            _nextWaveIndex = 0;
            SetNextWaveButtonVisible(false);

            EnsureQueriesAndQueues();

            // draft-stage-map-prebuild Unit 0 — map normally built by PrepareDraftMap.
            // Fallback for paths that bypass draft (tests, direct StartBattle).
            if (!_generatedMap.IsCreated)
            {
                Debug.LogWarning("[BattleBridge] BeginPlacement: map not prepared, building now.");
                BuildMapForBattle();
            }

            GameManager.Instance?.Logger?.SetAttackDeckId(deck.deckId);
            Debug.Log("[BattleBridge] Placement phase ready.");
        }


        public void StartBattle()
        {
            if (deck == null || map == null)
            {
                Debug.LogError("[BattleBridge] deck or map reference missing.", this);
                return;
            }
            if (!_placementAllowed) BeginPlacement();
            if (_world == null) return;
            _pending.Clear();
            _usingGeneratedWaves = TryInitializeGeneratedWaves();
            if (!_usingGeneratedWaves)
            {
                for (int i = 0; i < deck.spawns.Count; i++)
                    _pending.Add(new PendingSpawnEntry { entry = deck.spawns[i], deckIndex = i });
            }
            _startTime = Time.time;
            _timerDuration = deck.timerDurationSec;
            _running = true;
            if (_usingGeneratedWaves)
                QueueDueWaves(0f);
            RefreshNextWaveButton();
            Debug.Log(_usingGeneratedWaves
                ? $"[BattleBridge] Battle started with generated deck '{deck.deckId}' seed={_wavePlan.seed} waves={_wavePlan.waves.Count}."
                : $"[BattleBridge] Battle started with legacy deck '{deck.deckId}' ({deck.spawns.Count} spawns queued).");
        }

        private void EnsureQueriesAndQueues()
        {
            // draft-stage-map-prebuild Unit 0 — idempotent guard; skip if already initialised.
            if (_ecsInfrastructureReady) return;

            if (!_aliveAttackersQueryCreated)
            {
                _aliveAttackersQuery = _em.CreateEntityQuery(ComponentType.ReadOnly<AttackUnitTag>());
                _aliveAttackersQueryCreated = true;
            }

            if (!_projectileSpawnRequestQueryCreated)
            {
                _projectileSpawnRequestQuery = _em.CreateEntityQuery(ComponentType.ReadOnly<ProjectileSpawnRequest>());
                _projectileSpawnRequestQueryCreated = true;
            }

            if (!_projectileQueryCreated)
            {
                _projectileQuery = _em.CreateEntityQuery(ComponentType.ReadOnly<ProjectileTag>());
                _projectileQueryCreated = true;
            }

            // Create the shared queue and inject the singleton so ECS systems can enqueue events.
            if (_goalEventQueue.IsCreated) _goalEventQueue.Dispose();
            _goalEventQueue = new NativeQueue<GoalReachedEvent>(Allocator.Persistent);
            var singletonEntity = _em.CreateEntity();
            _em.AddComponentData(singletonEntity, new GoalReachedEventsSingleton { queue = _goalEventQueue });

            // Phase 4 defender-death event channel.
            if (_defenderDeathQueue.IsCreated) _defenderDeathQueue.Dispose();
            _defenderDeathQueue = new NativeQueue<DefenderDeathEvent>(Allocator.Persistent);
            var deathSingleton = _em.CreateEntity();
            _em.AddComponentData(deathSingleton, new DefenderDeathEventsSingleton { queue = _defenderDeathQueue });

            // Phase 8 §12 Meteor burst event channel for VFX timing.
            if (_meteorBurstQueue.IsCreated) _meteorBurstQueue.Dispose();
            _meteorBurstQueue = new NativeQueue<Wassup.Battle.Combat.MeteorBurstEvent>(Allocator.Persistent);
            var meteorBurstSingleton = _em.CreateEntity();
            _em.AddComponentData(meteorBurstSingleton, new Wassup.Battle.Combat.MeteorBurstEventsSingleton { queue = _meteorBurstQueue });

            // Unified attack visual trigger channel — every attacker (defender
            // or enemy) enqueues one event per fire so SpineUnitPool can play
            // the attack animation and facing flip uniformly.
            if (_unitAttackVisualQueue.IsCreated) _unitAttackVisualQueue.Dispose();
            _unitAttackVisualQueue = new NativeQueue<Wassup.Battle.Combat.UnitAttackVisualEvent>(Allocator.Persistent);
            var attackSingleton = _em.CreateEntity();
            _em.AddComponentData(attackSingleton, new Wassup.Battle.Combat.UnitAttackVisualEventsSingleton { queue = _unitAttackVisualQueue });

            // Combat→Presentation hit-VFX channel. ProjectileHitSystem enqueues
            // one event per direct-target impact; DrainProjectileHitEvents
            // (currently a stub) will consume them in task 3 to play hit prefabs.
            if (_projectileHitEventQueue.IsCreated) _projectileHitEventQueue.Dispose();
            _projectileHitEventQueue = new NativeQueue<Wassup.Battle.Combat.Projectile.ProjectileHitEvent>(Allocator.Persistent);
            var projectileHitSingleton = _em.CreateEntity();
            _em.AddComponentData(projectileHitSingleton, new Wassup.Battle.Combat.Projectile.ProjectileHitEventsSingleton { queue = _projectileHitEventQueue });

            // Units→Presentation heal pulse channel. DamageApplicationSystem
            // enqueues one event per entity whose IncomingHeal buffer was drained.
            if (_healAppliedEventQueue.IsCreated) _healAppliedEventQueue.Dispose();
            _healAppliedEventQueue = new NativeQueue<Wassup.Battle.Units.HealAppliedEvent>(Allocator.Persistent);
            var healAppliedSingleton = _em.CreateEntity();
            _em.AddComponentData(healAppliedSingleton, new Wassup.Battle.Units.HealAppliedEventsSingleton { queue = _healAppliedEventQueue });

            // Units->Presentation damage-number channel. DamageApplicationSystem enqueues
            // one event per enemy (AttackUnitTag) whose IncomingDamage was applied.
            if (_damageNumberEventQueue.IsCreated) _damageNumberEventQueue.Dispose();
            _damageNumberEventQueue = new NativeQueue<Wassup.Battle.Units.DamageNumberEvent>(Allocator.Persistent);
            var damageNumberSingleton = _em.CreateEntity();
            _em.AddComponentData(damageNumberSingleton, new Wassup.Battle.Units.DamageNumberEventsSingleton { queue = _damageNumberEventQueue });

            // Units->Presentation enemy-kill channel. DamageApplicationSystem enqueues
            // one event per enemy (AttackUnitTag) killed by damage; BattleBridge bumps
            // the live score HUD.
            if (_enemyKilledEventQueue.IsCreated) _enemyKilledEventQueue.Dispose();
            _enemyKilledEventQueue = new NativeQueue<Wassup.Battle.Units.EnemyKilledEvent>(Allocator.Persistent);
            var enemyKilledSingleton = _em.CreateEntity();
            _em.AddComponentData(enemyKilledSingleton, new Wassup.Battle.Units.EnemyKilledEventsSingleton { queue = _enemyKilledEventQueue });

            // CC event channel. CcApplySystem drains this queue each frame to apply
            // impulse / slow buffers to enemy entities.
            if (_enemyCcQueue.IsCreated) _enemyCcQueue.Dispose();
            _enemyCcQueue = new NativeQueue<Wassup.Battle.Effects.EnemyCcEvent>(Allocator.Persistent);
            var enemyCcSingleton = _em.CreateEntity();
            _em.AddComponentData(enemyCcSingleton, new Wassup.Battle.Effects.EnemyCcEventsSingleton { queue = _enemyCcQueue });

            // StatModifier apply channel. ModifierApplySystem drains this each frame to attach stat modifiers.
            if (_statModifierQueue.IsCreated) _statModifierQueue.Dispose();
            _statModifierQueue = new NativeQueue<Wassup.Battle.Effects.StatModifierApplyEvent>(Allocator.Persistent);
            var statModifierSingleton = _em.CreateEntity();
            _em.AddComponentData(statModifierSingleton, new Wassup.Battle.Effects.StatModifierApplyEventsSingleton { queue = _statModifierQueue });

            // StackModifier apply channel. ModifierApplySystem drains this each frame to attach stack modifiers.
            if (_stackModifierQueue.IsCreated) _stackModifierQueue.Dispose();
            _stackModifierQueue = new NativeQueue<Wassup.Battle.Effects.StackModifierApplyEvent>(Allocator.Persistent);
            var stackModifierSingleton = _em.CreateEntity();
            _em.AddComponentData(stackModifierSingleton, new Wassup.Battle.Effects.StackModifierApplyEventsSingleton { queue = _stackModifierQueue });

            // Hazard debug telemetry channel. Zone/DoT systems enqueue here; BattleBridge drains to JSON logs.
            if (_hazardRuntimeEventQueue.IsCreated) _hazardRuntimeEventQueue.Dispose();
            _hazardRuntimeEventQueue = new NativeQueue<Wassup.Battle.Effects.HazardRuntimeEvent>(Allocator.Persistent);
            var hazardRuntimeSingleton = _em.CreateEntity();
            _em.AddComponentData(hazardRuntimeSingleton, new Wassup.Battle.Effects.HazardRuntimeEventsSingleton { queue = _hazardRuntimeEventQueue });

            // Blocking hazard destruction channel. BattleBridge drains this for
            // MonoBehaviour visual cleanup and VFX once blocking visuals exist.
            if (_hazardDestroyedQueue.IsCreated) _hazardDestroyedQueue.Dispose();
            _hazardDestroyedQueue = new NativeQueue<Wassup.Battle.Effects.HazardDestroyedEvent>(Allocator.Persistent);
            var hazardDestroyedSingleton = _em.CreateEntity();
            _em.AddComponentData(hazardDestroyedSingleton, new Wassup.Battle.Effects.HazardDestroyedEventsSingleton { queue = _hazardDestroyedQueue });

            // Hazard caster spawn request channel. Effects systems enqueue
            // unmanaged requests; BattleBridge owns SO lookup and visual spawning.
            if (_hazardSpawnRequestQueue.IsCreated) _hazardSpawnRequestQueue.Dispose();
            _hazardSpawnRequestQueue = new NativeQueue<Wassup.Battle.Effects.HazardSpawnRequest>(Allocator.Persistent);
            var hazardSpawnRequestSingleton = _em.CreateEntity();
            _em.AddComponentData(hazardSpawnRequestSingleton, new Wassup.Battle.Effects.HazardSpawnRequestsSingleton { queue = _hazardSpawnRequestQueue });

            // Attack-output log channel. AttackSystem enqueues one event per output fired;
            // BattleBridge drains each frame and forwards to BattleLogger.RecordAttackOutput.
            if (_attackOutputLogQueue.IsCreated) _attackOutputLogQueue.Dispose();
            _attackOutputLogQueue = new NativeQueue<Wassup.Battle.Combat.AttackOutputLogEvent>(Allocator.Persistent);
            var attackOutputLogSingleton = _em.CreateEntity();
            _em.AddComponentData(attackOutputLogSingleton, new Wassup.Battle.Combat.AttackOutputLogEventsSingleton { queue = _attackOutputLogQueue });

            // Combat→Movement pause request channel. MovementSystem owns the actual
            // EnemyAttackMovePause component add/update.
            if (_movementPauseRequestQueue.IsCreated) _movementPauseRequestQueue.Dispose();
            _movementPauseRequestQueue = new NativeQueue<Wassup.Battle.Movement.MovementPauseRequest>(Allocator.Persistent);
            var movementPauseSingleton = _em.CreateEntity();
            _em.AddComponentData(movementPauseSingleton, new Wassup.Battle.Movement.MovementPauseRequestEventsSingleton { queue = _movementPauseRequestQueue });

            // Obstacle blocked-cells set. ObstacleLifetimeSystem rebuilds it each frame.
            if (_blockedCells.IsCreated) _blockedCells.Dispose();
            _blockedCells = new Unity.Collections.NativeHashSet<Unity.Mathematics.int2>(64, Allocator.Persistent);
            var obstacleSingleton = _em.CreateEntity();
            _em.AddComponentData(obstacleSingleton, new Wassup.Battle.Effects.ObstacleSingleton { blockedCells = _blockedCells });

            // Hazard cell→effects map. HazardLifetimeSystem rebuilds it each frame.
            if (_hazardCellToEffects.IsCreated) _hazardCellToEffects.Dispose();
            _hazardCellToEffects = new Unity.Collections.NativeParallelMultiHashMap<Unity.Mathematics.int2, Wassup.Battle.Effects.HazardEffect>(64, Allocator.Persistent);
            var hazardSingleton = _em.CreateEntity();
            _em.AddComponentData(hazardSingleton, new Wassup.Battle.Effects.HazardSingleton { cellToEffects = _hazardCellToEffects });

            // Fix 3 (task 10): seed visual RNG so jitter is reproducible per session.
            // match-seed-unification — 비주얼 시드도 matchSeed 계열에서 파생(맵과 decorrelated).
            int visualSeed = Wassup.Core.MatchSeed.DeriveVisualSeed(_matchSeed != 0 ? _matchSeed : 42);
            _projectileViewPool?.Initialize(visualSeed);

            // Build StackModifier threshold registry for StackModifierTickSystem lookups.
            BuildStackThresholdRegistry();

            // draft-stage-map-prebuild Unit 0 — BuildMapForBattle removed from here; called explicitly
            // by PrepareDraftMap / RebuildDraftMap / BeginPlacement fallback.
            _ecsInfrastructureReady = true;
        }

        public void StopBattle()
        {
            if (HasLiveEntityManager())
            {
                TeardownCurrentBattle();
                return;
            }

            BackdropMounter.Unmount(ref _backdropRoot);
            _running = false;
            _placementAllowed = false;
            SetNextWaveButtonVisible(false);
            DisposeEcsInfrastructureNativeContainers();
            DisposeCachedQueries();
            TeardownGeneratedMap();
            _ecsInfrastructureReady = false;
        }

        // draft-stage-map-prebuild Unit 0 — called by GameManager.Start before BeginDraft.
        // Initialises ECS infrastructure and builds the map so it is visible during the draft stage.
        public void PrepareDraftMap()
        {
            if (deck == null || map == null)
            {
                Debug.LogError("[BattleBridge] deck or map reference missing.", this);
                return;
            }
            _world = World.DefaultGameObjectInjectionWorld;
            if (_world == null)
            {
                Debug.LogWarning("[BattleBridge] Default World not ready at PrepareDraftMap; deferring 1 frame.");
                StartCoroutine(DeferredPrepareDraftMap());
                return;
            }
            _em = _world.EntityManager;

            EnsureQueriesAndQueues();
            BuildMapForBattle();
        }

        private System.Collections.IEnumerator DeferredPrepareDraftMap()
        {
            yield return null;
            PrepareDraftMap();
        }

        // draft-stage-map-prebuild Unit 1 — entity + visual + SO registry cleanup
        // for option toggle / Redraft. Mirrors the relevant subset of TeardownCurrentBattle.
        private void CleanupDraftMapBeforeRebuild()
        {
            BackdropMounter.Unmount(ref _backdropRoot);
            if (_em != null)
            {
                DestroyEntitiesByType<Wassup.Battle.Effects.Hazard>();
                DestroyEntitiesByType<Wassup.Battle.Effects.BlockingHazard>();
                DestroyEntitiesByType<Wassup.Battle.Effects.Obstacle>();
            }

            if (mapView != null) mapView.ResetVisualRoots();

            ClearBlockingHazardVisuals();
            _zoneHazardRegistry.Clear();
            _zoneHazardIndex.Clear();
            _blockingHazardSoRegistry.Clear();
            _blockingHazardSoIndex.Clear();

            TeardownGeneratedMap();
            TeardownFlowField();
        }

        private void DestroyEntitiesByType<T>() where T : unmanaged, Unity.Entities.IComponentData
        {
            if (!HasLiveEntityManager()) return;
            using var q = _em.CreateEntityQuery(Unity.Entities.ComponentType.ReadOnly<T>());
            if (!q.IsEmpty) _em.DestroyEntity(q);
        }

        // draft-stage-map-prebuild Unit 0 — called by DraftController on option change / Redraft.
        public void RebuildDraftMap()
        {
            if (_world == null) { PrepareDraftMap(); return; }
            CleanupDraftMapBeforeRebuild();
            BuildMapForBattle();
#if UNITY_INCLUDE_TESTS
            RebuildDraftMapCallCount++;
#endif
        }

        // draft-stage-map-prebuild Unit 0 — true once BuildMapForBattle has succeeded at least once.
        public bool HasGeneratedMap => _generatedMap.IsCreated;

#if UNITY_INCLUDE_TESTS
        // Unit 4 EditMode test counter — stripped from non-test builds.
        public int RebuildDraftMapCallCount { get; private set; }
#endif

        private bool TryInitializeGeneratedWaves()
        {
            _wavePlan = default;
            _nextWaveIndex = 0;

            if (deck == null || !deck.useGeneratedWaves)
                return false;

            try
            {
                // match-seed-unification — 웨이브 시드도 matchSeed 에서 파생(맵과 decorrelated).
                int waveSeed = Wassup.Core.MatchSeed.DeriveWaveSeed(_matchSeed != 0 ? _matchSeed : 1);
                _wavePlan = WavePatternGenerator.Generate(deck, waveSeed);
                GameManager.Instance?.Logger?.SetWavePattern(_wavePlan);
                return _wavePlan.waves != null && _wavePlan.waves.Count > 0;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[BattleBridge] Generated wave plan failed; using legacy spawns. {ex.Message}", this);
                return false;
            }
        }

        private void QueueDueWaves(float elapsedSec)
        {
            if (!_usingGeneratedWaves || _wavePlan.waves == null) return;
            while (_nextWaveIndex < _wavePlan.waves.Count &&
                   elapsedSec + 0.0001f >= _wavePlan.waves[_nextWaveIndex].triggerTimeSec)
            {
                QueueWave(_wavePlan.waves[_nextWaveIndex], _wavePlan.waves[_nextWaveIndex].triggerTimeSec, false, elapsedSec);
                _nextWaveIndex++;
            }
            RefreshNextWaveButton();
        }

        public void ForceNextWave()
        {
            if (!_running || !_usingGeneratedWaves || _wavePlan.waves == null) return;
            if (_nextWaveIndex >= _wavePlan.waves.Count)
            {
                RefreshNextWaveButton();
                return;
            }

            float elapsedSec = Time.time - _startTime;
            var wave = _wavePlan.waves[_nextWaveIndex];
            GameManager.Instance?.Logger?.RecordWaveEvent("wave_forced", wave.waveIndex, elapsedSec, true);
            QueueWave(wave, elapsedSec, true, elapsedSec);
            _nextWaveIndex++;
            RefreshNextWaveButton();
        }

        private void QueueWave(GeneratedWave wave, float baseTriggerTimeSec, bool forced, float elapsedSec)
        {
            int laneCount = _generatedMap.IsCreated ? _generatedMap.spawns.Length : 1;
            var entries = WavePatternGenerator.ExpandWave(wave, baseTriggerTimeSec, laneCount, _wavePlan.intraWaveSpacingSec);
            int baseDeckIndex = wave.waveIndex * 1000;
            for (int i = 0; i < entries.Count; i++)
                _pending.Add(new PendingSpawnEntry { entry = entries[i], deckIndex = baseDeckIndex + i });

            GameManager.Instance?.Logger?.RecordWaveEvent("wave_started", wave.waveIndex, elapsedSec, forced);
            // ingame-dreamcatcher Unit 3 — every 5th wave (5/10/15…) offers a card.
            if ((wave.waveIndex + 1) % 5 == 0)
                WaveMilestoneReached?.Invoke(wave.waveIndex + 1);
            Debug.Log($"[BattleBridge] Wave {wave.waveIndex + 1} queued ({entries.Count} spawns, forced={forced}). {WavePatternGenerator.FormatSummary(wave)}");
        }

        // Replaces the defender pool used by random placement selection. Called by
        // DraftController once picks are confirmed. A null or empty array resets
        // the pool to the inspector-assigned fallback (if any) so the caller can
        // "un-confirm" a draft during tests.
        public void SetDefenderPool(DefenderUnitData[] pool)
        {
            defenderPool = pool;
        }

        public void SetMapSource(MapSource src) => mapSource = src;
        public MapSource CurrentMapSource => mapSource;

        public void SetMapGridGridSizeOverride(int2? gridSize) => _mapGridGridSizeOverride = gridSize;
        public int2? CurrentMapGridGridSizeOverride => _mapGridGridSizeOverride;

        // PlayMode-only mutation of the shared SO. Reverts on Stop.
        public void SetGoalEdgeOnly(bool value)
        {
            if (mapGridSettings != null) mapGridSettings.SetGoalEdgeOnly(value);
        }

        public bool CurrentGoalEdgeOnly => mapGridSettings != null && mapGridSettings.GoalEdgeOnly;

        public void SetMapPathShape(MapPathShape shape)
        {
            mapPathShape = shape;
            var options = mapGenerationOptions.Normalized();
            options.pathShape = shape;
            mapGenerationOptions = options;
        }

        public void SetMapGenerationOptions(MapGenerationOptions options)
        {
            mapGenerationOptions = options.Normalized();
            mapPathShape = mapGenerationOptions.pathShape;
        }

        public DefenderUnitData[] DefenderPool => defenderPool;
        public float TileSize => tileSize;
        public MapView MapView => mapView;
        // map-origin-placement: 입력(레이캐스트 평면)이 board 원점을 읽는 단일 창구.
        public Vector3 BoardOrigin => new Vector3(_boardOrigin.x, _boardOrigin.y, _boardOrigin.z);
        public PlacementInput PlacementInput => placementInput;

        private SkillData[] _skillLoadout;
        public SkillData[] SkillLoadout => _skillLoadout;

        public void SetSkillLoadout(SkillData[] loadout)
        {
            _skillLoadout = loadout;
        }

        private void EnsureNextWaveButton()
        {
            if (_nextWaveButton != null) return;

            var canvasGO = new GameObject("NextWaveCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasGO.transform.SetParent(transform, false);
            var canvas = canvasGO.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 7;
            var scaler = canvasGO.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);

            var buttonGO = new GameObject("NextWaveButton", typeof(RectTransform), typeof(Image), typeof(Button));
            buttonGO.transform.SetParent(canvasGO.transform, false);
            var rt = (RectTransform)buttonGO.transform;
            rt.anchorMin = new Vector2(1f, 0f);
            rt.anchorMax = new Vector2(1f, 0f);
            rt.pivot = new Vector2(1f, 0f);
            rt.anchoredPosition = new Vector2(-40f, 40f);
            rt.sizeDelta = new Vector2(250f, 72f);
            buttonGO.GetComponent<Image>().color = new Color(0.12f, 0.42f, 0.82f, 0.95f);

            _nextWaveButton = buttonGO.GetComponent<Button>();
            _nextWaveButton.onClick.AddListener(ForceNextWave);

            var labelGO = new GameObject("Label", typeof(RectTransform));
            labelGO.transform.SetParent(buttonGO.transform, false);
            var labelRT = (RectTransform)labelGO.transform;
            labelRT.anchorMin = Vector2.zero;
            labelRT.anchorMax = Vector2.one;
            labelRT.offsetMin = Vector2.zero;
            labelRT.offsetMax = Vector2.zero;
            _nextWaveLabel = labelGO.AddComponent<TextMeshProUGUI>();
            _nextWaveLabel.text = "NEXT WAVE";
            _nextWaveLabel.fontSize = 28;
            _nextWaveLabel.color = Color.white;
            _nextWaveLabel.alignment = TextAlignmentOptions.Center;

            canvasGO.SetActive(false);
        }

        private void SetNextWaveButtonVisible(bool visible)
        {
            if (_nextWaveButton == null) return;
            _nextWaveButton.transform.parent.gameObject.SetActive(visible);
        }

        private void RefreshNextWaveButton()
        {
            EnsureNextWaveButton();

            bool visible = _running && _usingGeneratedWaves && _wavePlan.waves != null;
            SetNextWaveButtonVisible(visible);
            if (!visible) return;

            bool hasNext = _nextWaveIndex < _wavePlan.waves.Count;
            _nextWaveButton.interactable = hasNext;
            if (_nextWaveLabel != null)
                _nextWaveLabel.text = hasNext ? $"NEXT WAVE {_nextWaveIndex + 1}" : "NO WAVES";
        }

        // ============== Skill casting (Phase 2) ==============

        // Cast a TilePoint-targeted skill (e.g. Slow Field). Returns true on success.
        // `affectedCount` reports how many ECS entities received the effect — used
        // by the logger and the UI for feedback. Returns false if the battle is
        // not running, the skill type does not match, or the cooldown gate rejects.
        public bool CastSkillAtTile(SkillData skill, Vector2Int tile, out int affectedCount)
        {
            affectedCount = 0;
            if (!_running || skill == null) return false;
            if (skill.target != SkillTargetType.TilePoint) return false;
            if (skillRuntime != null && !skillRuntime.IsReady(skill)) return false;

            Vector2Int secondaryTile = new(-1, -1);
            switch (skill.effect)
            {
                case SkillEffectType.SlowField:
                    affectedCount = ApplySlowField(tile, skill);
                    break;
                case SkillEffectType.Tornado:
                    affectedCount = ApplyTornado(tile, skill);
                    break;
                case SkillEffectType.Meteor:
                    affectedCount = ApplyMeteor(tile, skill);
                    break;
                default:
                    Debug.LogWarning($"[BattleBridge] TilePoint skill '{skill.id}' has unsupported effect {skill.effect}.");
                    return false;
            }

            skillRuntime?.Consume(skill);
            GameManager.Instance?.Logger?.RecordSkillUsage(new Logging.SkillUsageLog
            {
                skill_id = skill.id,
                time = Time.time - _startTime,
                target_tile = tile,
                target_tile_b = secondaryTile,
                affected_count = affectedCount,
                cost_spent = skill.cost,
            });
            Debug.Log($"[BattleBridge] CastSkillAtTile {skill.id} @ {tile} → {affectedCount} affected, cd={skill.cooldownSec}s");
            return true;
        }

        // Phase 7 — Portal two-tap cast. Unlike CastSkillAtTile, this takes two
        // tiles (entry/exit) in one call. SkillBar captures both taps before
        // invoking. Returns false if the skill is not a Portal or the cooldown
        // gate rejects.
        public bool CastPortal(SkillData skill, Vector2Int entryTile, Vector2Int exitTile, out int affectedCount)
        {
            affectedCount = 0;
            if (!_running || skill == null) return false;
            if (skill.effect != SkillEffectType.Portal) return false;
            if (skillRuntime != null && !skillRuntime.IsReady(skill)) return false;

            affectedCount = ApplyPortal(entryTile, exitTile, skill);
            skillRuntime?.Consume(skill);
            GameManager.Instance?.Logger?.RecordSkillUsage(new Logging.SkillUsageLog
            {
                skill_id = skill.id,
                time = Time.time - _startTime,
                target_tile = entryTile,
                target_tile_b = exitTile,
                affected_count = affectedCount,
                cost_spent = skill.cost,
            });
            Debug.Log($"[BattleBridge] CastPortal {skill.id} {entryTile}→{exitTile} for {skill.durationSec}s");
            return true;
        }

        // Cast a DefenderUnit-targeted skill (Power Surge, Rapid Fire). `tile` is
        // the defender's cell coordinate as recorded during PlaceDefender.
        public bool CastSkillOnDefender(SkillData skill, Vector2Int tile, out int affectedCount)
        {
            affectedCount = 0;
            if (!_running || skill == null) return false;
            if (skill.target != SkillTargetType.DefenderUnit) return false;
            if (!_defenderByTile.TryGetValue(tile, out var defender) || !_em.Exists(defender.entity)) return false;
            var entity = defender.entity;
            if (_em.HasComponent<PendingDeployment>(entity)) return false;
            if (skillRuntime != null && !skillRuntime.IsReady(skill)) return false;

            switch (skill.effect)
            {
                case SkillEffectType.PowerSurge:
                    EnqueueDamageMul(entity, skill.magnitude, skill.durationSec);
                    affectedCount = 1;
                    break;
                case SkillEffectType.RapidFire:
                    EnqueueAttackSpeedMul(entity, skill.magnitude, skill.durationSec);
                    affectedCount = 1;
                    break;
                default:
                    Debug.LogWarning($"[BattleBridge] DefenderUnit skill '{skill.id}' has unsupported effect {skill.effect}.");
                    return false;
            }

            skillRuntime?.Consume(skill);
            GameManager.Instance?.Logger?.RecordSkillUsage(new Logging.SkillUsageLog
            {
                skill_id = skill.id,
                time = Time.time - _startTime,
                target_tile = tile,
                affected_count = affectedCount,
                cost_spent = skill.cost,
            });
            Debug.Log($"[BattleBridge] CastSkillOnDefender {skill.id} on defender@{tile} (entity {entity.Index}) cd={skill.cooldownSec}s");
            return true;
        }

        // Phase 9 — 모든 스킬 대상 타일 → world center 계산의 단일 소스.
        // Phase 10 에서 tileSize 가 theme 파라미터로 승격될 때 이 helper 만 바꾸면 됨.
        private float3 GridToWorldCenter(Vector2Int cell, float y = 0f)
            => _boardOrigin + new float3(cell.x * tileSize, y, cell.y * tileSize);

        public Vector3 GridToWorldCenterVector(Vector2Int cell, float y = 0f)
        {
            var p = GridToWorldCenter(cell, y);
            return new Vector3(p.x, p.y, p.z);
        }

        private bool InTileRange(float3 worldPos, Vector2Int originTile, int range)
        {
            var cell = GridMath.WorldToCell(worldPos, tileSize,
                           new int2(_generatedMap.gridSize.x, _generatedMap.gridSize.y), origin: _boardOrigin);
            var origin = new int2(originTile.x, originTile.y);
            return GridMath.ChebyshevDistance(cell, origin) <= range;
        }

        public Unity.Mathematics.int2 DebugWorldToCell(Vector3 worldPosition)
        {
            int2 gridSize = _generatedMap.IsCreated ? _generatedMap.gridSize : GridSize;
            return GridMath.WorldToCell(new float3(worldPosition.x, worldPosition.y, worldPosition.z), tileSize, gridSize, origin: _boardOrigin);
        }

        public bool TryGetNearestWalkCell(Unity.Mathematics.int2 requestedCell, out Unity.Mathematics.int2 walkCell)
        {
            walkCell = requestedCell;
            if (!_generatedMap.IsCreated)
                return false;

            if (IsInGeneratedMapBounds(requestedCell) && _generatedMap.TileAt(requestedCell) == MapTileType.Walk)
                return true;

            int bestDistSq = int.MaxValue;
            bool found = false;
            for (int y = 0; y < _generatedMap.gridSize.y; y++)
            for (int x = 0; x < _generatedMap.gridSize.x; x++)
            {
                var candidate = new int2(x, y);
                if (_generatedMap.TileAt(candidate) != MapTileType.Walk) continue;

                int dx = x - requestedCell.x;
                int dy = y - requestedCell.y;
                int distSq = dx * dx + dy * dy;
                if (distSq >= bestDistSq) continue;

                bestDistSq = distSq;
                walkCell = candidate;
                found = true;
            }

            return found;
        }

        public bool TryFindValidBlockingHazardCell(BlockingHazardSO so, Unity.Mathematics.int2 requestedCell, out Unity.Mathematics.int2 spawnCell, out string reason)
        {
            spawnCell = requestedCell;
            reason = string.Empty;
            if (so == null)
            {
                reason = "BlockingHazardSO is null.";
                return false;
            }
            if (_em == null)
            {
                reason = "EntityManager is not ready. Start battle first.";
                return false;
            }
            if (!_generatedMap.IsCreated)
            {
                reason = "GeneratedMap is not ready. Start battle first.";
                return false;
            }

            if (IsInGeneratedMapBounds(requestedCell)
                && _generatedMap.TileAt(requestedCell) == MapTileType.Walk
                && EffectSpawner.CanSpawnBlockingHazard(_em, so, requestedCell, out reason))
            {
                spawnCell = requestedCell;
                return true;
            }

            int bestDistSq = int.MaxValue;
            bool found = false;
            string lastReason = reason;
            for (int y = 0; y < _generatedMap.gridSize.y; y++)
            for (int x = 0; x < _generatedMap.gridSize.x; x++)
            {
                var candidate = new int2(x, y);
                if (_generatedMap.TileAt(candidate) != MapTileType.Walk) continue;
                if (!EffectSpawner.CanSpawnBlockingHazard(_em, so, candidate, out lastReason)) continue;

                int dx = x - requestedCell.x;
                int dy = y - requestedCell.y;
                int distSq = dx * dx + dy * dy;
                if (distSq >= bestDistSq) continue;

                bestDistSq = distSq;
                spawnCell = candidate;
                found = true;
            }

            if (!found)
                reason = $"No valid blocking hazard cell found. Last rejection: {lastReason}";
            return found;
        }

        private bool IsInGeneratedMapBounds(Unity.Mathematics.int2 cell)
        {
            return cell.x >= 0 && cell.x < _generatedMap.gridSize.x
                && cell.y >= 0 && cell.y < _generatedMap.gridSize.y;
        }

        private int ApplySlowField(Vector2Int tile, SkillData skill)
        {
            // Collect all currently-alive attack unit entities; filter by Chebyshev tile
            // distance to the target tile; apply slow CC effect through EffectSpawner so
            // the Effects context remains the sole writer.
            if (!_aliveAttackersQueryCreated) return 0;
            var entities = _aliveAttackersQuery.ToEntityArray(Allocator.Temp);

            int tileRange = GridMath.RangeToTiles(skill.range);
            int affected = 0;

            for (int i = 0; i < entities.Length; i++)
            {
                var e = entities[i];
                if (!_em.HasComponent<LocalTransform>(e)) continue;
                var pos = _em.GetComponentData<LocalTransform>(e).Position;
                if (!InTileRange(pos, tile, tileRange)) continue;
                EnqueueMoveSpeedMul(e, skill.magnitude, skill.durationSec);
                affected++;
            }

            entities.Dispose();
            return affected;
        }

        // Phase 7 — Tornado. Pulls in-range attackers toward the target tile for
        // `durationSec`. `skill.magnitude` is the pull speed (world units/sec).
        private int ApplyTornado(Vector2Int tile, SkillData skill)
        {
            float3 targetWorld = GridToWorldCenter(tile);
            int tileRange = GridMath.RangeToTiles(skill.range);
            float rangeWorld = tileRange * tileSize; // VFX only

            // Phase 8 §17 — continuous field (replaces Phase 7 per-attacker
            // snapshot). MovementSystem queries live TornadoField entities each
            // frame, so enemies that enter the radius mid-duration are also
            // pulled. Re-cast creates an independent field; multiple fields can
            // coexist and the attacker is pulled by the first one that contains
            // it.
            EffectSpawner.SpawnTornadoField(_em, targetWorld, tileRange, skill.magnitude, skill.durationSec);

            // Phase 8 §12: swirling particle ring over the Tornado center.
            if (vfxSpawner != null)
                vfxSpawner.SpawnTornado(new Vector3(targetWorld.x, 0f, targetWorld.z), rangeWorld, skill.durationSec);

            // Affected count is reported async as attackers enter / get pulled;
            // at cast time we conservatively pre-count overlaps so the log has
            // a baseline without waiting for the field to expire.
            if (!_aliveAttackersQueryCreated) return 0;
            var entities = _aliveAttackersQuery.ToEntityArray(Allocator.Temp);
            int preview = 0;
            for (int i = 0; i < entities.Length; i++)
            {
                var e = entities[i];
                if (!_em.HasComponent<LocalTransform>(e)) continue;
                var p = _em.GetComponentData<LocalTransform>(e).Position;
                if (!InTileRange(p, tile, tileRange)) continue;
                preview++;
            }
            entities.Dispose();
            return preview;
        }

        // Phase 7 — Meteor. Spawns a carrier entity + a MonoBehaviour-side warning
        // ring for `skill.warningSec`; MeteorResolutionSystem applies the AoE damage
        // when the warning expires.
        private int ApplyMeteor(Vector2Int tile, SkillData skill)
        {
            float3 centerWorld = GridToWorldCenter(tile);
            int tileRange = GridMath.RangeToTiles(skill.range);
            float radiusWorld = tileRange * tileSize; // VFX only
            float warn = skill.warningSec > 0f ? skill.warningSec : 0f;
            EffectSpawner.SpawnMeteor(_em, centerWorld, tileRange, skill.magnitude, warn);
            SpawnMeteorWarningVisual(centerWorld, radiusWorld, warn);
            // Phase 8 §13: falling streak during the warning window. Silent
            // no-op when meteorFallPrefab slot empty — Meteor still plays
            // without the falling visual.
            if (vfxSpawner != null && warn > 0f)
                vfxSpawner.SpawnMeteorFall(new Vector3(centerWorld.x, 0f, centerWorld.z), warn);
            // Actual damage count is reported async by MeteorResolutionSystem; at
            // cast time we conservatively pre-count current overlaps so the log is
            // informative without waiting for the burst.
            if (!_aliveAttackersQueryCreated) return 0;
            var entities = _aliveAttackersQuery.ToEntityArray(Allocator.Temp);
            int preview = 0;
            for (int i = 0; i < entities.Length; i++)
            {
                if (!_em.HasComponent<LocalTransform>(entities[i])) continue;
                var p = _em.GetComponentData<LocalTransform>(entities[i]).Position;
                if (!InTileRange(p, tile, tileRange)) continue;
                preview++;
            }
            entities.Dispose();
            return preview;
        }

        // Phase 7 — Portal. Spawns a PortalLink carrier with two endpoints. On
        // teleport, MovementSystem advances the attacker's waypoint index to the
        // first waypoint whose cell matches (or follows) the exit tile so they
        // keep heading toward the goal from the exit.
        private int ApplyPortal(Vector2Int entryTile, Vector2Int exitTile, SkillData skill)
        {
            float3 entryWorld = GridToWorldCenter(entryTile);
            float3 exitWorld = GridToWorldCenter(exitTile);
            float entryRadius = tileSize * 0.5f; // half-tile catch radius
            EffectSpawner.SpawnPortal(_em, entryWorld, exitWorld, entryRadius, skill.durationSec);

            // Phase 8 §12: two swirls + connecting beam for the portal's lifetime.
            if (vfxSpawner != null)
            {
                vfxSpawner.SpawnPortal(
                    new Vector3(entryWorld.x, 0f, entryWorld.z),
                    new Vector3(exitWorld.x, 0f, exitWorld.z),
                    skill.durationSec);
            }

            return 1;
        }

        // Meteor telegraph: a flat translucent red quad on the ground that
        // auto-destroys after warningSec. Deliberately MonoBehaviour-only — ECS
        // holds the resolution timer, but the *visual* is a gameplay-layer object.
        private void SpawnMeteorWarningVisual(float3 centerWorld, float radiusWorld, float warningSec)
        {
            if (warningSec <= 0f) return;
            var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
            go.name = "MeteorWarning";
            var col = go.GetComponent<Collider>();
            if (col != null) Destroy(col);
            // tilemap-view-backend unit 3 — 모드별 평면. Legacy3D=XZ 바닥(법선 +Y), Tilemap=XY 보드(법선 −Z, 카메라 향함).
            Vector3 viewCenter = Wassup.Core.BoardSpace.ToView(centerWorld);
            if (Wassup.Core.BoardSpace.Mode == Wassup.Core.BoardViewMode.Legacy3D)
            {
                go.transform.position = new Vector3(viewCenter.x, 0.02f, viewCenter.z);
                go.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
            }
            else
            {
                go.transform.position = new Vector3(viewCenter.x, viewCenter.y, viewCenter.z - 0.05f);
                go.transform.rotation = Quaternion.identity; // Quad 기본 법선 −Z = 카메라 향함
            }
            float d = radiusWorld * 2f;
            go.transform.localScale = new Vector3(d, d, 1f);
            var rend = go.GetComponent<MeshRenderer>();
            if (rend != null)
            {
                var mat = RuntimeMaterialFactory.CreateTransparent(new Color(1f, 0.15f, 0.15f, 0.55f));
                rend.sharedMaterial = mat;
            }
            Destroy(go, warningSec);
        }

        private void Update()
        {
            if (!_running) return;

            float t = Time.time - _startTime;
            QueueDueWaves(t);
            for (int i = _pending.Count - 1; i >= 0; i--)
            {
                if (t >= _pending[i].entry.triggerTimeSec)
                {
                    SpawnUnit(_pending[i]);
                    _pending.RemoveAt(i);
                }
            }

            DrainProjectileSpawnRequests();
            DrainDefenderDeathEvents();
            DrainMeteorBurstEvents();
            DrainUnitAttackVisualEvents();
            DrainProjectileHitEvents();
            DrainHealAppliedEvents();
            DrainDamageNumberEvents();
            DrainEnemyKilledEvents();
            DrainAttackOutputLogEvents();
            DrainHazardSpawnRequests();
            DrainHazardRuntimeEvents();
            DrainHazardDestroyedEvents();
            DrainGoalEvents();
            CheckTimer();
            CheckVictory();
        }

        private void LateUpdate()
        {
            SyncMonoUnitViews();
            if (_em != null) _projectileViewPool?.SyncTransforms(_em);
        }

        private void SyncMonoUnitViews()
        {
            if (_em == null) return;
            bool canSort = _generatedMap.IsCreated;
            int2 gridSize = canSort ? _generatedMap.gridSize : default;
            if (enemyViewPool != null)
            {
                enemyViewPool.DespawnMissing(_em);
                spineUnitPool?.DespawnMissing(_em);
                if (_aliveAttackersQueryCreated)
                {
                    NativeArray<Entity> entities;
                    try
                    {
                        entities = _aliveAttackersQuery.ToEntityArray(Allocator.Temp);
                    }
                    catch (NullReferenceException)
                    {
                        _aliveAttackersQuery = _em.CreateEntityQuery(ComponentType.ReadOnly<AttackUnitTag>());
                        entities = _aliveAttackersQuery.ToEntityArray(Allocator.Temp);
                    }
                    try
                    {
                        for (int i = 0; i < entities.Length; i++)
                        {
                            var entity = entities[i];
                            if (!_em.HasComponent<LocalTransform>(entity)) continue;

                            var p = _em.GetComponentData<LocalTransform>(entity).Position;
                            var world = new Vector3(p.x, p.y, p.z);
                            if (spineUnitPool != null && spineUnitPool.TryGet(entity, out var spineView))
                            {
                                spineView.UpdatePosition(world);
                                if (canSort) spineView.UpdateSortingOrder(gridSize, tileSize);
                            }
                            else if (enemyViewPool.TryGet(entity, out var view))
                            {
                                view.UpdatePosition(world);
                                if (canSort) view.UpdateSortingOrder(gridSize, tileSize);
                            }
                        }
                    }
                    finally
                    {
                        entities.Dispose();
                    }
                }
            }

            defenderFallbackViewPool?.DespawnMissing(_em);
            foreach (var kv in _defenderByTile)
            {
                var entity = kv.Value.entity;
                if (entity == Entity.Null || !_em.Exists(entity) || !_em.HasComponent<LocalTransform>(entity))
                    continue;

                var p = _em.GetComponentData<LocalTransform>(entity).Position;
                var world = new Vector3(p.x, p.y + spineDefenderYOffset, p.z);
                if (spineUnitPool != null && spineUnitPool.TryGet(entity, out var spineView))
                {
                    spineView.UpdatePosition(world);
                    if (canSort) spineView.UpdateSortingOrder(gridSize, tileSize);
                }
                else if (defenderFallbackViewPool != null &&
                         defenderFallbackViewPool.TryGet(entity, out var fallbackView))
                {
                    fallbackView.UpdatePosition(world);
                    if (canSort) fallbackView.UpdateSortingOrder(gridSize, tileSize);
                }
            }
        }

        private void DrainDefenderDeathEvents()
        {
            if (!_defenderDeathQueue.IsCreated) return;
            while (_defenderDeathQueue.TryDequeue(out var evt))
            {
                var cell = new Vector2Int(evt.cell.x, evt.cell.y);
                if (spineUnitPool != null && _defenderByTile.TryGetValue(cell, out var binding))
                {
                    spineUnitPool.NotifyDeath(binding.entity);
                    defenderFallbackViewPool?.Despawn(binding.entity);
                }
                _defenderByTile.Remove(cell);
                _occupiedTiles.Remove(cell);
                RecomputeSynergyFor(cell);
                Debug.Log($"[BattleBridge] Defender died @ {cell}; tile freed, synergy recomputed.");
            }
        }

        // Phase 8 §12 — when MeteorResolutionSystem burns its AoE, it enqueues a
        // burst event so the VFX layer can fire a particle burst on the same
        // frame without any ECS references on the MonoBehaviour side.
        private void DrainMeteorBurstEvents()
        {
            if (!_meteorBurstQueue.IsCreated) return;
            while (_meteorBurstQueue.TryDequeue(out var evt))
            {
                if (vfxSpawner == null) continue;
                vfxSpawner.SpawnMeteorBurst(new Vector3(evt.center.x, 0f, evt.center.z), evt.radius);
            }
        }

        // Unified attack visual drain. AttackSystem enqueues one event per
        // fire for both defenders and enemies so the Spine attack animation
        // and facing flip fire uniformly. Defender-specific side effects
        // (attack VFX prefab, cast VFX) are gated by FindDefenderData — when
        // the attacker is an enemy, FindDefenderData returns null and only
        // the Spine notify runs.
        private void DrainUnitAttackVisualEvents()
        {
            if (!_unitAttackVisualQueue.IsCreated) return;
            while (_unitAttackVisualQueue.TryDequeue(out var evt))
            {
                var targetWorld = new Vector3(evt.targetWorld.x, evt.targetWorld.y, evt.targetWorld.z);
                spineUnitPool?.NotifyAttack(evt.attacker, targetWorld);

                var defData = FindDefenderData(evt.attacker);
                if (defData == null) continue;

                if (defData.attackVfxPrefab != null)
                    _projectileViewPool?.PlayHit(defData.attackVfxPrefab, evt.targetWorld);

                TrySpawnCastVfx(evt.attacker, targetWorld);
            }
        }

        private DefenderUnitData FindDefenderData(Entity entity)
        {
            foreach (var kv in _defenderByTile)
                if (kv.Value.entity == entity) return kv.Value.data;
            return null;
        }

        private void DrainAttackOutputLogEvents()
        {
            if (!_attackOutputLogQueue.IsCreated) return;
            var logger = GameManager.Instance?.Logger;
            while (_attackOutputLogQueue.TryDequeue(out var evt))
            {
                if (logger == null) continue;
                var defData = FindDefenderData(evt.attacker);
                var sourceUnit = defData != null ? defData.displayName : "<unknown>";
                int2 grid = _generatedMap.IsCreated ? _generatedMap.gridSize : GridSize;
                var srcCell = GridMath.WorldToCell(evt.sourcePos, tileSize, grid, origin: _boardOrigin);
                var tgtCell = GridMath.WorldToCell(evt.targetPos, tileSize, grid, origin: _boardOrigin);
                string detail = "";
                if (evt.kind == Wassup.Data.AttackOutputKind.ApplyStat) detail = evt.stat.ToString();
                else if (evt.kind == Wassup.Data.AttackOutputKind.ApplyStack) detail = evt.stackKind.ToString();
                logger.RecordAttackOutput(sourceUnit, evt.kind.ToString(), evt.magnitude, detail, evt.duration,
                    new Vector2Int(srcCell.x, srcCell.y), new Vector2Int(tgtCell.x, tgtCell.y));
            }
        }

        private void TrySpawnCastVfx(Entity defender, Vector3 targetWorld)
        {
            if (_projectileViewPool == null) return;
            if (!_em.HasComponent<ProjectileRef>(defender)) return;
            var pRef = _em.GetComponentData<ProjectileRef>(defender);
            if (pRef.dataIndex < 0 || pRef.dataIndex >= _projectileDataByIndex.Count) return;
            var data = _projectileDataByIndex[pRef.dataIndex];
            if (data.castPrefab == null) return;
            // anchor 는 ResolveCastAnchor 결과 = view 공간. targetWorld 는 sim → view 로 맞춰서 빼야 방향이 정확.
            if (spineUnitPool == null || !spineUnitPool.TryResolveAnchor(defender, out var anchor)) return;
            var dir = (Vector3)Wassup.Core.BoardSpace.ToView(targetWorld) - anchor;
            // 캐스트 방향을 화면 평면에 평탄화: Legacy3D=XZ 바닥(Y 제거), Tilemap=XY 보드(깊이 Z 제거).
            if (Wassup.Core.BoardSpace.Mode == Wassup.Core.BoardViewMode.Legacy3D) dir.y = 0f;
            else dir.z = 0f;
            _projectileViewPool.PlayCast(data.castPrefab, anchor, dir, data.castVfxLifetime);
        }

        // Combat→Presentation hit-VFX channel drain. ProjectileHitSystem enqueues
        // one event per direct-target impact. Task 0 keeps this as a no-op
        // dequeue so the queue does not back up; task 3 connects it to the
        private void DrainProjectileHitEvents()
        {
            if (!_projectileHitEventQueue.IsCreated) return;
            while (_projectileHitEventQueue.TryDequeue(out var evt))
            {
                if (evt.dataIndex < 0 || evt.dataIndex >= _projectileDataByIndex.Count) continue;
                var data = _projectileDataByIndex[evt.dataIndex];
                if (data.hitPrefab != null)
                    _projectileViewPool?.PlayHit(data.hitPrefab, evt.position, data.hitVfxLifetime);
            }
        }

        private void DrainHealAppliedEvents()
        {
            if (!_healAppliedEventQueue.IsCreated) return;
            if (vfxSpawner == null) { _healAppliedEventQueue.Clear(); return; }
            while (_healAppliedEventQueue.TryDequeue(out var evt))
            {
                if (evt.amount <= 0f) continue;
                vfxSpawner.SpawnHealApplied(new Vector3(evt.position.x, evt.position.y, evt.position.z), evt.amount);
            }
        }

        // Enemy-only floating damage numbers. DamageApplicationSystem enqueues one
        // event per enemy whose IncomingDamage was applied; spawn a popup at each.
        private void DrainDamageNumberEvents()
        {
            if (!_damageNumberEventQueue.IsCreated) return;
            if (damageNumberSpawner == null) { _damageNumberEventQueue.Clear(); return; }
            while (_damageNumberEventQueue.TryDequeue(out var evt))
            {
                if (evt.amount <= 0f) continue;
                damageNumberSpawner.Spawn(
                    new Vector3(evt.position.x, evt.position.y, evt.position.z), evt.amount);
            }
        }

        // Enemy kills → live score HUD. One score bump per enemy killed by damage.
        private void DrainEnemyKilledEvents()
        {
            if (!_enemyKilledEventQueue.IsCreated) return;
            if (scoreHud == null) { _enemyKilledEventQueue.Clear(); return; }
            while (_enemyKilledEventQueue.TryDequeue(out _))
            {
                scoreHud.OnEnemyKilled();
            }
        }

        // Converts every staged ProjectileSpawnRequest into a live projectile entity
        // and spawns the prefab view via ProjectileViewPool.
        private void DrainProjectileSpawnRequests()
        {
            if (!_projectileSpawnRequestQueryCreated) return;
            if (_projectileSpawnRequestQuery.IsEmpty) return;

            var requestEntities = _projectileSpawnRequestQuery.ToEntityArray(Allocator.Temp);
            var requestData = _projectileSpawnRequestQuery.ToComponentDataArray<ProjectileSpawnRequest>(Allocator.Temp);
            for (int i = 0; i < requestEntities.Length; i++)
            {
                var req = requestData[i];
                // Spine attack trigger moved to DrainUnitAttackVisualEvents
                // so both projectile and melee defenders share the same hook.
                SpawnProjectile(req, requestEntities[i]);
                _em.RemoveComponent<ProjectileSpawnRequest>(requestEntities[i]);
                if (_em.HasBuffer<ProjectileSpawnOutputElement>(requestEntities[i]))
                    _em.RemoveComponent<ProjectileSpawnOutputElement>(requestEntities[i]);
            }
            requestEntities.Dispose();
            requestData.Dispose();
        }

        private void SpawnProjectile(ProjectileSpawnRequest req, Entity shooter)
        {
            if (req.dataIndex < 0 || req.dataIndex >= _projectileDataByIndex.Count)
            {
                Debug.LogWarning($"[BattleBridge] ProjectileSpawnRequest dataIndex {req.dataIndex} out of range; dropping.");
                return;
            }

            // Snapshot shooter's outputs BEFORE any structural change. Subsequent
            // CreateEntity/AddComponent calls invalidate cached BufferTypeHandles,
            // so reading the buffer after them throws ObjectDisposedException when
            // multiple projectiles spawn in the same frame.
            NativeArray<Wassup.Battle.Combat.AttackOutputElement> outputSnapshot = default;
            bool hasSnapshot = false;
            if (_em.HasBuffer<ProjectileSpawnOutputElement>(shooter))
            {
                var sourceOutputs = _em.GetBuffer<ProjectileSpawnOutputElement>(shooter);
                outputSnapshot = new NativeArray<Wassup.Battle.Combat.AttackOutputElement>(sourceOutputs.Length, Allocator.Temp);
                for (int i = 0; i < sourceOutputs.Length; i++)
                    outputSnapshot[i] = new Wassup.Battle.Combat.AttackOutputElement { value = sourceOutputs[i].value };
                hasSnapshot = true;
            }

            var entity = _em.CreateEntity();
#if UNITY_EDITOR
            _em.SetName(entity, $"Projectile_{req.dataIndex}");
#endif
            var spawnPos = new float3(req.origin.x, spawnHeight, req.origin.z);
            _em.AddComponentData(entity, LocalTransform.FromPositionRotationScale(spawnPos, quaternion.identity, req.visualScale));
            _em.AddComponent<ProjectileTag>(entity);
            _em.AddComponentData(entity, new ProjectileState
            {
                target = req.target,
                speed = req.speed,
                damage = req.damage,
                hitThreshold = req.hitThreshold,
                onHitEffect = req.onHitEffect,
                splashRadius = req.splashRadius,
                splashDamageMul = req.splashDamageMul,
                dataIndex = req.dataIndex,
            });

            if (hasSnapshot)
            {
                var projectileOutputs = _em.AddBuffer<Wassup.Battle.Combat.AttackOutputElement>(entity);
                for (int i = 0; i < outputSnapshot.Length; i++)
                    projectileOutputs.Add(outputSnapshot[i]);
                outputSnapshot.Dispose();
            }

            var data = _projectileDataByIndex[req.dataIndex];
            _projectileViewPool?.Spawn(entity, data, spawnPos);
        }

        // Fires the defender's on-place effect on surrounding entities. Returns
        // the count of entities affected so the logger can record magnitude.
        // Writes to Effects components go through EffectSpawner so the Effects-
        // context write gateway (Phase 2 decision) stays the sole path.
        private int ApplyOnPlaceEffect(DefenderUnitData unitData, Vector2Int placedCell, Entity placedEntity)
        {
            if (unitData.onPlaceEffect == OnPlaceEffectType.None) return 0;

            float3 center = GridToWorldCenter(placedCell);
            int affected = 0;

            if (unitData.onPlaceEffect == OnPlaceEffectType.SlowPulse)
            {
                if (unitData.onPlaceRange <= 0f) return 0;
                if (!_aliveAttackersQueryCreated) return 0;
                int tileRange = GridMath.RangeToTiles(unitData.onPlaceRange);
                var entities = _aliveAttackersQuery.ToEntityArray(Allocator.Temp);
                for (int i = 0; i < entities.Length; i++)
                {
                    var e = entities[i];
                    if (!_em.HasComponent<LocalTransform>(e)) continue;
                    var pos = _em.GetComponentData<LocalTransform>(e).Position;
                    if (!InTileRange(pos, placedCell, tileRange)) continue;
                    EnqueueMoveSpeedMul(e, unitData.onPlaceMagnitude, unitData.onPlaceDuration);
                    affected++;
                }
                entities.Dispose();
            }
            else if (unitData.onPlaceEffect == OnPlaceEffectType.BindNearby)
            {
                if (unitData.onPlaceRange <= 0f) return 0;
                if (!_aliveAttackersQueryCreated) return 0;
                int tileRange = GridMath.RangeToTiles(unitData.onPlaceRange);
                var entities = _aliveAttackersQuery.ToEntityArray(Allocator.Temp);
                for (int i = 0; i < entities.Length; i++)
                {
                    var e = entities[i];
                    if (!_em.HasComponent<LocalTransform>(e)) continue;
                    var pos = _em.GetComponentData<LocalTransform>(e).Position;
                    if (!InTileRange(pos, placedCell, tileRange)) continue;
                    EnqueueMoveSpeedMul(e, unitData.onPlaceMagnitude, unitData.onPlaceDuration);
                    affected++;
                }
                entities.Dispose();
            }
            else if (unitData.onPlaceEffect == OnPlaceEffectType.MeleeBurst)
            {
                if (unitData.onPlaceRange <= 0f || unitData.onPlaceMagnitude <= 0f) return 0;
                if (!_aliveAttackersQueryCreated) return 0;
                int tileRange = GridMath.RangeToTiles(unitData.onPlaceRange);
                var entities = _aliveAttackersQuery.ToEntityArray(Allocator.Temp);
                for (int i = 0; i < entities.Length; i++)
                {
                    var e = entities[i];
                    if (!_em.HasComponent<LocalTransform>(e) || !_em.HasBuffer<IncomingDamage>(e)) continue;
                    var pos = _em.GetComponentData<LocalTransform>(e).Position;
                    if (!InTileRange(pos, placedCell, tileRange)) continue;
                    _em.GetBuffer<IncomingDamage>(e).Add(new IncomingDamage { amount = unitData.onPlaceMagnitude });
                    affected++;
                }
                entities.Dispose();
            }
            else if (unitData.onPlaceEffect == OnPlaceEffectType.ForwardProjectile)
            {
                affected = ApplyForwardOnPlaceProjectile(unitData, placedCell, GridToWorldCenter(placedCell));
            }
            else if (unitData.onPlaceEffect == OnPlaceEffectType.GainCost)
            {
                var costRuntime = GameManager.Instance != null ? GameManager.Instance.CostRuntime : null;
                affected = costRuntime != null
                    ? costRuntime.AddCost(Mathf.RoundToInt(unitData.onPlaceMagnitude))
                    : 0;
            }
            else if (unitData.onPlaceEffect == OnPlaceEffectType.ReduceSkillCooldown)
            {
                affected = skillRuntime != null ? skillRuntime.ReduceAllCooldowns(unitData.onPlaceMagnitude) : 0;
            }
            else if (unitData.onPlaceEffect == OnPlaceEffectType.BoostNearbyDefenders)
            {
                if (unitData.onPlaceRange <= 0f) return 0;
                // Reuse the tuple-based tile map — no ECS query needed since placement
                // grid already gives us every defender. Self-inclusion is allowed
                // (PHASE4.md §4 autonomy, chosen for simplicity + stronger feedback).
                int tileRange = GridMath.RangeToTiles(unitData.onPlaceRange);
                foreach (var kv in _defenderByTile)
                {
                    var d = kv.Value;
                    if (!_em.Exists(d.entity)) continue;
                    if (d.entity != placedEntity && _em.HasComponent<PendingDeployment>(d.entity)) continue;
                    var tileCell = kv.Key;
                    var tileInt = new int2(tileCell.x, tileCell.y);
                    var originInt = new int2(placedCell.x, placedCell.y);
                    if (GridMath.ChebyshevDistance(tileInt, originInt) > tileRange) continue;
                    EnqueueDamageMul(d.entity, unitData.onPlaceMagnitude, unitData.onPlaceDuration);
                    affected++;
                }
            }

            return affected;
        }

        private int ApplyForwardOnPlaceProjectile(DefenderUnitData unitData, Vector2Int placedCell, float3 center)
        {
            if (unitData.onPlaceRange <= 0f || unitData.onPlaceMagnitude <= 0f) return 0;
            if (!_aliveAttackersQueryCreated) return 0;

            float2 forward = FindNearestPathDirection(placedCell);
            float length = unitData.onPlaceRange * tileSize;
            float width = tileSize * 0.45f;
            float widthSq = width * width;
            int affected = 0;

            var entities = _aliveAttackersQuery.ToEntityArray(Allocator.Temp);
            for (int i = 0; i < entities.Length; i++)
            {
                var e = entities[i];
                if (!_em.HasComponent<LocalTransform>(e) || !_em.HasBuffer<IncomingDamage>(e)) continue;
                var pos = _em.GetComponentData<LocalTransform>(e).Position;
                float2 toTarget = new float2(pos.x - center.x, pos.z - center.z);
                float along = math.dot(toTarget, forward);
                if (along < 0f || along > length) continue;
                float2 closest = forward * along;
                float2 lateral = toTarget - closest;
                if (math.lengthsq(lateral) > widthSq) continue;
                _em.GetBuffer<IncomingDamage>(e).Add(new IncomingDamage { amount = unitData.onPlaceMagnitude });
                affected++;
            }
            entities.Dispose();
            return affected;
        }

        private float2 FindNearestPathDirection(Vector2Int placedCell)
        {
            if (!_generatedMap.IsCreated) return new float2(1f, 0f);

            int bestDistSq = int.MaxValue;
            Vector2Int best = placedCell + Vector2Int.right;
            for (int y = 0; y < _generatedMap.gridSize.y; y++)
            for (int x = 0; x < _generatedMap.gridSize.x; x++)
            {
                if (_generatedMap.TileAt(new int2(x, y)) != MapTileType.Walk) continue;
                int dx = x - placedCell.x;
                int dy = y - placedCell.y;
                int d2 = dx * dx + dy * dy;
                if (d2 >= bestDistSq) continue;
                bestDistSq = d2;
                best = new Vector2Int(x, y);
            }

            var dir = new float2(best.x - placedCell.x, best.y - placedCell.y);
            if (math.lengthsq(dir) < 0.001f) return new float2(1f, 0f);
            return math.normalize(dir);
        }

        // Recomputes adjacency synergy for `cell` and its eight neighbors. Same-type
        // defender adjacency grants a damage multiplier of (1 + 0.1 × neighborCount).
        // Writes to SynergyBuff go through EffectSpawner so the Effects-context
        // write gateway stays a single code path (Phase 2 decision #9).
        private void RecomputeSynergyFor(Vector2Int cell)
        {
            var cells = new Vector2Int[]
            {
                cell,
                cell + new Vector2Int(1, 0),
                cell + new Vector2Int(-1, 0),
                cell + new Vector2Int(0, 1),
                cell + new Vector2Int(0, -1),
                cell + new Vector2Int(1, 1),
                cell + new Vector2Int(-1, 1),
                cell + new Vector2Int(1, -1),
                cell + new Vector2Int(-1, -1),
            };

            for (int i = 0; i < cells.Length; i++)
            {
                var c = cells[i];
                if (!_defenderByTile.TryGetValue(c, out var here)) continue;
                if (!_em.Exists(here.entity) || _em.HasComponent<PendingDeployment>(here.entity)) continue;
                int neighbors = 0;
                for (int dx = -1; dx <= 1; dx++)
                for (int dz = -1; dz <= 1; dz++)
                {
                    if (dx == 0 && dz == 0) continue;
                    if (_defenderByTile.TryGetValue(c + new Vector2Int(dx, dz), out var n)
                        && n.data == here.data
                        && _em.Exists(n.entity)
                        && !_em.HasComponent<PendingDeployment>(n.entity))
                        neighbors++;
                }

                if (neighbors == 0)
                {
                    // magnitude=1.0 (multiplicative identity) — effectively disables synergy contribution
                    EnqueueSynergyMul(here.entity, 1f);
                }
                else
                {
                    bool wasPresent = _synergyActivatedEntities.Contains(here.entity);
                    EnqueueSynergyMul(here.entity, 1f + SynergyPerNeighbor * neighbors);
                    if (!wasPresent && _synergyActivatedEntities.Add(here.entity))
                    {
                        _synergyActivations++;
                    }
                }
            }

            // Peak tracking: count entities that have received a non-trivial synergy enqueue this session
            int currentCount = _synergyActivatedEntities.Count;
            if (currentCount > _synergyPeakCount) _synergyPeakCount = currentCount;
        }

        // Unit 8: channel enqueue helpers — route legacy effect produces through StatModifier channel.
        // source=target ensures the ApplySystem merge-key matches per-entity, preventing slot accumulation.
        public void EnqueueDamageMul(Entity target, float multiplier, float duration)
        {
            if (!_statModifierQueue.IsCreated) return;
            _statModifierQueue.Enqueue(new Wassup.Battle.Effects.StatModifierApplyEvent
            {
                target    = target,
                stat      = Wassup.Battle.Effects.StatKind.DamageMul,
                op        = Wassup.Battle.Effects.CombineOp.Multiplicative,
                magnitude = multiplier,
                duration  = duration,
                source    = target,
                stackId   = 0,
            });
        }

        // RapidFire / CooldownReduction: multiplier here means "attack speed factor" (how much faster to fire).
        // AttackSpeedMul > 1 = faster attacks. Legacy ApplyCooldownReduction stored 1/multiplier as a cooldown divisor;
        // the new channel stores the speed multiplier directly (ModifierStatsAggregateSystem applies it to attackSpeedMul).
        public void EnqueueAttackSpeedMul(Entity target, float multiplier, float duration)
        {
            if (!_statModifierQueue.IsCreated) return;
            _statModifierQueue.Enqueue(new Wassup.Battle.Effects.StatModifierApplyEvent
            {
                target    = target,
                stat      = Wassup.Battle.Effects.StatKind.AttackSpeedMul,
                op        = Wassup.Battle.Effects.CombineOp.Multiplicative,
                magnitude = multiplier,
                duration  = duration,
                source    = target,
                stackId   = 0,
            });
        }

        public void EnqueueMoveSpeedMul(Entity target, float multiplier, float duration)
        {
            if (!_statModifierQueue.IsCreated) return;
            _statModifierQueue.Enqueue(new Wassup.Battle.Effects.StatModifierApplyEvent
            {
                target    = target,
                stat      = Wassup.Battle.Effects.StatKind.MoveSpeedMul,
                op        = Wassup.Battle.Effects.CombineOp.Multiplicative,
                magnitude = multiplier,
                duration  = duration,
                source    = target,
                stackId   = 0,
            });
        }

        // Synergy: infinite duration, magnitude refreshed each recompute.
        // magnitude=1.0 is the multiplicative identity (neighbors==0 case).
        private void EnqueueSynergyMul(Entity target, float multiplier)
        {
            if (!_statModifierQueue.IsCreated) return;
            _statModifierQueue.Enqueue(new Wassup.Battle.Effects.StatModifierApplyEvent
            {
                target    = target,
                stat      = Wassup.Battle.Effects.StatKind.DamageMul,
                op        = Wassup.Battle.Effects.CombineOp.Multiplicative,
                magnitude = multiplier,
                duration  = float.PositiveInfinity,
                source    = target,
                stackId   = 1,   // stackId=1 distinguishes synergy slot from onplace/skill DamageMul (stackId=0)
            });
        }

        // ingame-dreamcatcher Unit 2 — dreamcatcher card effects are match-long and
        // apply to current + future matching defenders. stackId starts at 100 to
        // avoid colliding with onplace/skill (0) and synergy (1) on the same stat.
        private struct ActiveDcEffect
        {
            public Wassup.Data.CardTargetAxis axis;
            public Wassup.Battle.Effects.StatKind stat;
            public float mult;
            public ushort stackId;
        }
        private readonly System.Collections.Generic.List<ActiveDcEffect> _activeDcEffects =
            new System.Collections.Generic.List<ActiveDcEffect>();
        private ushort _dcStackCounter = 100;
        private const float DcDuration = 1e9f;

        // ingame-dreamcatcher Unit 3 — selection triggers. DreamcatcherController
        // subscribes; absent subscribers simply no-op (non-destructive).
        public event System.Action FirstDefenderPlaced;
        public event System.Action<int> WaveMilestoneReached; // 1-indexed wave number
        private bool _firstDefenderPlacedFired;

        private void FireFirstDefenderPlacedOnce()
        {
            if (_firstDefenderPlacedFired) return;
            _firstDefenderPlacedFired = true;
            FirstDefenderPlaced?.Invoke();
        }

        private void EnqueueStatMul(Entity target, Wassup.Battle.Effects.StatKind stat, float multiplier, float duration, ushort stackId)
        {
            if (!_statModifierQueue.IsCreated) return;
            _statModifierQueue.Enqueue(new Wassup.Battle.Effects.StatModifierApplyEvent
            {
                target    = target,
                stat      = stat,
                op        = Wassup.Battle.Effects.CombineOp.Multiplicative,
                magnitude = multiplier,
                duration  = duration,
                source    = target,
                stackId   = stackId,
            });
        }

        // Applies one card to all currently-placed matching defenders and records
        // it so future placements (ApplyActiveDcEffectsTo) inherit it.
        public void ApplyDreamcatcherCard(Wassup.Data.DreamcatcherCard card)
        {
            if (card == null || card.effects == null) return;
            foreach (var eff in card.effects)
            {
                if (!MapDcEffect(eff, out var stat, out var mult)) continue;
                ushort sid = _dcStackCounter++;
                _activeDcEffects.Add(new ActiveDcEffect { axis = card.axis, stat = stat, mult = mult, stackId = sid });
                foreach (var kv in _defenderByTile)
                {
                    var data = kv.Value.data;
                    var entity = kv.Value.entity;
                    if (data != null && _em.Exists(entity) && MatchesDcAxis(data, card.axis))
                        EnqueueStatMul(entity, stat, mult, DcDuration, sid);
                }
            }
        }

        private void ApplyActiveDcEffectsTo(Entity entity, DefenderUnitData data)
        {
            if (data == null || _activeDcEffects.Count == 0 || !_em.Exists(entity)) return;
            for (int i = 0; i < _activeDcEffects.Count; i++)
            {
                var e = _activeDcEffects[i];
                if (MatchesDcAxis(data, e.axis))
                    EnqueueStatMul(entity, e.stat, e.mult, DcDuration, e.stackId);
            }
        }

        private static bool MapDcEffect(Wassup.Data.CardEffect eff, out Wassup.Battle.Effects.StatKind stat, out float mult)
        {
            switch (eff.kind)
            {
                case Wassup.Data.CardBuffKind.AttackDamage:
                    stat = Wassup.Battle.Effects.StatKind.DamageMul; mult = 1f + eff.percent / 100f; return true;
                case Wassup.Data.CardBuffKind.AttackSpeed:
                    stat = Wassup.Battle.Effects.StatKind.AttackSpeedMul; mult = 1f + eff.percent / 100f; return true;
                case Wassup.Data.CardBuffKind.EffectiveHealth:
                    // HP% proxy: less damage taken = higher effective health (max-HP unchanged).
                    stat = Wassup.Battle.Effects.StatKind.DmgTakenMul; mult = 1f / (1f + eff.percent / 100f); return true;
                case Wassup.Data.CardBuffKind.MoveSpeed:
                    stat = Wassup.Battle.Effects.StatKind.MoveSpeedMul; mult = 1f + eff.percent / 100f; return true;
                default:
                    stat = Wassup.Battle.Effects.StatKind.DamageMul; mult = 1f; return false;
            }
        }

        private static bool MatchesDcAxis(DefenderUnitData data, Wassup.Data.CardTargetAxis axis)
        {
            switch (axis)
            {
                case Wassup.Data.CardTargetAxis.ClassRanger: return data.role == Wassup.Data.DefenderClass.Ranger;
                case Wassup.Data.CardTargetAxis.ClassGuardian: return data.role == Wassup.Data.DefenderClass.Guardian;
                case Wassup.Data.CardTargetAxis.Cost1: return data.cost == 1;
                default: return false;
            }
        }

        // Shared mesh/material for every health bar so adding more units does not
        // allocate per-entity assets. Lazily created and disposed on scene teardown.
        private RenderMeshArray GetOrCreateHealthBarRenderArray()
        {
            if (_healthBarRenderArray.MaterialReferences != null &&
                _healthBarRenderArray.MaterialReferences.Length > 0)
                return _healthBarRenderArray;

            _healthBarMaterial = RuntimeMaterialFactory.CreateOpaque(new Color(0.2f, 0.95f, 0.2f, 1f));
            var mesh = Resources.GetBuiltinResource<Mesh>("Quad.fbx");
            _healthBarRenderArray = new RenderMeshArray(new[] { _healthBarMaterial }, new[] { mesh });
            return _healthBarRenderArray;
        }

        private void CreateHealthBar(Entity owner, float yOffset, float baseScale)
        {
            var entity = _em.CreateEntity();
#if UNITY_EDITOR
            _em.SetName(entity, "HealthBar");
#endif
            _em.AddComponent<HealthBarTag>(entity);
            _em.AddComponentData(entity, new HealthBarState
            {
                owner = owner,
                yOffset = yOffset,
                baseScale = baseScale,
            });
            _em.AddComponentData(entity, LocalTransform.FromPositionRotationScale(
                new float3(0f, 0f, 0f), quaternion.identity, baseScale));

            // tilemap-view-backend — Tilemap 모드에서는 Entities Graphics 렌더 컴포넌트 생성을 게이팅.
            // HealthBarSystem 은 불변 (렌더 컴포넌트가 없으면 그릴 게 없을 뿐). Mono 오버레이 헬스바는 후속 후보.
            if (UseTilemapView)
            {
                if (!_healthBarRenderGateLogged)
                {
                    Debug.Log("[BattleBridge] HealthBar render gated: tilemap view mode");
                    _healthBarRenderGateLogged = true;
                }
                return;
            }

            var arr = GetOrCreateHealthBarRenderArray();
            var desc = new RenderMeshDescription(
                shadowCastingMode: UnityEngine.Rendering.ShadowCastingMode.Off,
                receiveShadows: false);
            RenderMeshUtility.AddComponents(entity, _em, desc, arr,
                MaterialMeshInfo.FromRenderMeshArrayIndices(0, 0));
        }

        private int GetOrCreateProjectileDataIndex(ProjectileData projectile)
        {
            if (_projectileDataIndex.TryGetValue(projectile, out var idx)) return idx;
            idx = _projectileDataByIndex.Count;
            _projectileDataByIndex.Add(projectile);
            _projectileDataIndex[projectile] = idx;
            return idx;
        }

        private void DrainGoalEvents()
        {
            if (!_goalEventQueue.IsCreated) return;
            while (_goalEventQueue.TryDequeue(out var evt))
            {
                enemyViewPool?.Despawn(evt.entity);
                spineUnitPool?.Despawn(evt.entity);
                _goalReachedCount++;
                Debug.Log($"[BattleBridge] Goal reached! Count: {_goalReachedCount}/{deck.defeatGoalReachedCount}");
                if (!_resultShown && _goalReachedCount >= deck.defeatGoalReachedCount)
                {
                    _resultShown = true;
                    _running = false;
                    SetNextWaveButtonVisible(false);
                    int playerScore = CalculatePlayerScore();
                    GameManager.Instance?.Logger?.SetResult("defeat", _goalReachedCount);
                    WriteLoggedScore(playerScore);
                    resultScreen?.ShowDefeat(playerScore);
                    Debug.Log("[BattleBridge] DEFEAT triggered.");
                    return;
                }
            }
        }

        public float TimerRemaining => _running ? Mathf.Max(0f, _timerDuration - (Time.time - _startTime)) : 0f;

        private void CheckTimer()
        {
            if (_resultShown) return;
            if (_timerDuration <= 0f) return;
            if (Time.time - _startTime < _timerDuration) return;

            _resultShown = true;
            _running = false;
            SetNextWaveButtonVisible(false);
            int playerScore = CalculatePlayerScore();
            GameManager.Instance?.Logger?.SetResult("victory_timeout", _goalReachedCount);
            WriteLoggedScore(playerScore);
            resultScreen?.ShowVictory(playerScore);
            Debug.Log("[BattleBridge] VICTORY — timer expired, player survived.");
        }

        // Victory = every spawn in the deck has been processed AND no attack unit entities remain alive.
        private void CheckVictory()
        {
            if (_resultShown) return;
            if (_usingGeneratedWaves && _wavePlan.waves != null && _nextWaveIndex < _wavePlan.waves.Count) return;
            if (_pending.Count > 0) return;
            if (!_aliveAttackersQueryCreated) return;
            if (_aliveAttackersQuery.CalculateEntityCount() > 0) return;

            _resultShown = true;
            _running = false;
            SetNextWaveButtonVisible(false);
            int playerScore = CalculatePlayerScore();
            GameManager.Instance?.Logger?.SetResult("victory", _goalReachedCount);
            WriteLoggedScore(playerScore);
            resultScreen?.ShowVictory(playerScore);
            Debug.Log("[BattleBridge] VICTORY — all attack units defeated.");
        }

        private int CalculatePlayerScore()
        {
            float durationSec = Mathf.Max(0f, Time.time - _startTime);
            return Math.Max(0, (int)(durationSec * 10f - _goalReachedCount * 50));
        }

        private void WriteLoggedScore(int playerScore)
        {
            var logger = GameManager.Instance?.Logger;
            if (logger == null) return;

            var currentEntryField = logger.GetType().GetField("currentEntry",
                BindingFlags.Instance | BindingFlags.NonPublic);
            var currentEntry = currentEntryField?.GetValue(logger);
            if (currentEntry == null) return;

            var resultField = currentEntry.GetType().GetField("result",
                BindingFlags.Instance | BindingFlags.Public);
            var battleResult = resultField?.GetValue(currentEntry);
            if (battleResult == null) return;

            var scoreField = battleResult.GetType().GetField("score",
                BindingFlags.Instance | BindingFlags.Public);
            scoreField?.SetValue(battleResult, playerScore);
        }

        // Random-pick legacy entry (Phase 0-3 behavior). Phase 4 prefers
        // PlaceDefenderAs with an explicit type, but this path stays for tests
        // and the no-selector fallback.
        public bool PlaceDefender(int tileX, int tileY)
        {
            if (defenderPool == null || defenderPool.Length == 0)
            {
                Debug.LogWarning("[BattleBridge] defenderPool is empty — cannot place defender.");
                return false;
            }
            var unitData = defenderPool[UnityEngine.Random.Range(0, defenderPool.Length)];
            return PlaceDefenderAs(tileX, tileY, unitData);
        }

        public bool CanPlaceDefenderAt(int tileX, int tileY, DefenderUnitData unitData, out PlacementRejectReason reason)
        {
            if (!_running && !_placementAllowed)
            {
                reason = PlacementRejectReason.NotRunningOrPlacementClosed;
                return false;
            }
            if (!_generatedMap.IsCreated)
            {
                reason = PlacementRejectReason.MissingMap;
                return false;
            }
            if (tileX < 0 || tileX >= _generatedMap.gridSize.x || tileY < 0 || tileY >= _generatedMap.gridSize.y)
            {
                reason = PlacementRejectReason.OutOfBounds;
                return false;
            }
            if (_generatedMap.TileAt(new int2(tileX, tileY)) != MapTileType.Place)
            {
                reason = PlacementRejectReason.NotBuildable;
                return false;
            }

            var cell = new Vector2Int(tileX, tileY);
            if (_occupiedTiles.Contains(cell))
            {
                reason = PlacementRejectReason.Occupied;
                return false;
            }

            if (unitData == null || unitData.visualMaterial == null)
            {
                reason = PlacementRejectReason.InvalidUnit;
                return false;
            }

            if (defenderPool != null && defenderPool.Length > 0 && System.Array.IndexOf(defenderPool, unitData) < 0)
            {
                reason = PlacementRejectReason.NotInPickedPool;
                return false;
            }

            var costRuntime = GameManager.Instance != null ? GameManager.Instance.CostRuntime : null;
            if (costRuntime != null && !costRuntime.CanAfford(unitData.cost))
            {
                reason = PlacementRejectReason.InsufficientCost;
                return false;
            }

            reason = PlacementRejectReason.None;
            return true;
        }

        // Explicit-type placement (Phase 4). Used by DefenderSelector after the
        // player chooses which picked defender they want on the tile.
        public bool PlaceDefenderAs(int tileX, int tileY, DefenderUnitData unitData)
        {
            var cell = new Vector2Int(tileX, tileY);
            if (!CanPlaceDefenderAt(tileX, tileY, unitData, out var reason))
            {
                LogPlacementReject("PlaceDefenderAs", unitData, reason);
                return false;
            }

            _occupiedTiles.Add(cell);
            GameManager.Instance?.Logger?.RecordPlacement(unitData.displayName, cell, Time.time - _startTime, unitData.cost);

            var entity = CreateDefenderEntity(cell, unitData, pendingDeployment: false, spawnPlacementVfx: true);
            TriggerOnPlaceAndSynergy(unitData, cell, entity);
            FireFirstDefenderPlacedOnce();

            Debug.Log($"[BattleBridge] Placed {unitData.displayName} at ({tileX},{tileY}).");
            return true;
        }

        public bool TryBeginDefenderDeployment(int tileX, int tileY, DefenderUnitData unitData, out Entity entity)
        {
            entity = Entity.Null;
            var cell = new Vector2Int(tileX, tileY);
            if (!CanPlaceDefenderAt(tileX, tileY, unitData, out var reason))
            {
                LogPlacementReject("TryBeginDefenderDeployment", unitData, reason);
                return false;
            }

            var costRuntime = GameManager.Instance != null ? GameManager.Instance.CostRuntime : null;
            if (costRuntime != null && !costRuntime.TrySpend(unitData.cost))
            {
                LogPlacementReject("TryBeginDefenderDeployment", unitData, PlacementRejectReason.InsufficientCost);
                return false;
            }

            _occupiedTiles.Add(cell);
            GameManager.Instance?.Logger?.RecordPlacement(unitData.displayName, cell, Time.time - _startTime, unitData.cost);
            entity = CreateDefenderEntity(cell, unitData, pendingDeployment: true, spawnPlacementVfx: false);
            ApplyOnPlacePush(unitData, cell);
            Debug.Log($"[BattleBridge] Began pending deployment for {unitData.displayName} at ({tileX},{tileY}).");
            return true;
        }

        public void ActivateDeployedDefender(Vector2Int cell, Entity entity)
        {
            if (_em == null || entity == Entity.Null || !_em.Exists(entity)) return;
            if (!_defenderByTile.TryGetValue(cell, out var binding) || binding.entity != entity) return;

            if (!_onPlaceTriggeredEntities.Contains(entity))
                TriggerDeploymentOnPlaceSkill(cell, entity);

            if (_em.HasComponent<PendingDeployment>(entity))
                _em.RemoveComponent<PendingDeployment>(entity);
            RecomputeSynergyFor(cell);
            FireFirstDefenderPlacedOnce();
            Debug.Log($"[BattleBridge] Activated deployed defender {binding.data.displayName} at {cell}.");
        }

        public bool TriggerDeploymentOnPlaceSkill(Vector2Int cell, Entity entity)
        {
            // NOTE: on-place push impulse is enqueued earlier — at TryBeginDefenderDeployment
            // (drag-drop path) or PlaceDefenderAs (instant path). Do NOT re-call ApplyOnPlacePush
            // here; that would double-fire the radius push impulse.
            if (_em == null || entity == Entity.Null || !_em.Exists(entity)) return false;
            if (_onPlaceTriggeredEntities.Contains(entity)) return false;
            if (!_defenderByTile.TryGetValue(cell, out var binding) || binding.entity != entity) return false;

            int onPlaceAffected = ApplyOnPlaceEffect(binding.data, cell, entity);
            _onPlaceTriggeredEntities.Add(entity);
            LogOnPlaceAndSynergy(binding.data, cell, onPlaceAffected);
            return true;
        }

        // tilemap-view-backend unit 4 — 모드별 ortho 카메라 프리셋 적용. gridSize+tileSize 로 orthographicSize 계산.
        // 매 맵 빌드마다 호출 — 프리셋+gridSize 에서 결정론적이라 RebuildDraftMap 재진입에도 idempotent.
        private void ApplyTilemapCameraPreset()
        {
            var preset = boardViewMode == Wassup.Core.BoardViewMode.TilemapIso
                ? tilemapCameraPresetIso : tilemapCameraPresetRect;
            var cam = Camera.main;
            if (preset == null || cam == null) return;

            int2 g = _generatedMap.gridSize;
            // Tilemap 모드 sim origin = zero. 보드 중심(sim) → view.
            float3 centerSim = new float3((g.x - 1) * 0.5f * tileSize, 0f, (g.y - 1) * 0.5f * tileSize);
            Vector3 centerView = Wassup.Core.BoardSpace.ToView(centerSim);

            cam.orthographic = preset.orthographic;
            float halfH = g.y * tileSize * 0.5f;
            float halfW = g.x * tileSize * 0.5f;
            float aspect = cam.aspect > 0.01f ? cam.aspect : (16f / 9f);
            cam.orthographicSize = Mathf.Max(halfH, halfW / aspect) + preset.orthoSizePadding;
            cam.transform.position = centerView + preset.positionOffset;
            cam.transform.rotation = Quaternion.Euler(preset.rotationEuler);
            cam.nearClipPlane = preset.nearClip;
            cam.farClipPlane = preset.farClip;
            cam.transparencySortMode = preset.transparencySortMode;
            cam.transparencySortAxis = preset.sortAxis;
        }

        // tilemap-view-backend unit 3 — 배치 hover/reject 피드백을 활성 뷰로 분기 (PlacementInput/DragController 공용 단일 경로).
        public void SetPlacementHover(Vector2Int cell, bool valid)
        {
            if (UseTilemapView) { if (tilemapMapView != null) tilemapMapView.SetPlacementHover(cell, valid); }
            else if (mapView != null) mapView.SetPlacementHover(cell, valid);
        }

        public void ClearPlacementHover(Vector2Int cell)
        {
            if (UseTilemapView) { if (tilemapMapView != null) tilemapMapView.ClearPlacementHover(cell); }
            else if (mapView != null) mapView.ClearPlacementHover(cell);
        }

        public void ClearPlacementHover()
        {
            if (UseTilemapView) { if (tilemapMapView != null) tilemapMapView.ClearPlacementHover(); }
            else if (mapView != null) mapView.ClearPlacementHover();
        }

        public void FlashPlacementReject(Vector2Int cell)
        {
            if (UseTilemapView) { if (tilemapMapView != null) tilemapMapView.FlashTileReject(cell); }
            else if (mapView != null) mapView.FlashTileReject(cell);
        }

        public float PlayDeploymentPresentation(DefenderUnitData unitData, Vector2Int cell, Entity entity)
        {
            float duration = unitData != null ? Mathf.Max(0f, unitData.deploymentDuration) : 0f;
            var world = GridToWorldCenterVector(cell, spawnHeight);                       // sim
            var viewWorld = (Vector3)Wassup.Core.BoardSpace.ToView(world);                 // view (직접 배치용)

            if (unitData != null && unitData.placementVfxPrefab != null)
            {
                var go = Instantiate(unitData.placementVfxPrefab, viewWorld, Quaternion.identity);
                Destroy(go, Mathf.Max(duration, 1f) + 0.25f);
            }
            else if (vfxSpawner != null)
            {
                vfxSpawner.SpawnPlacementRing(world); // VfxSpawner 가 진입부에서 ToView — sim 전달(이중변환 금지)
            }
            StartCoroutine(PlayDeploymentRingPulse(viewWorld, Mathf.Max(duration, 0.35f)));

            bool spineDeployment = false;
            if (spineUnitPool != null && spineUnitPool.TryGet(entity, out var view))
            {
                spineDeployment = view.PlayDeploy();
            }
            if (!spineDeployment && unitData != null && duration > 0f)
            {
                StartCoroutine(PlayFallbackDeploymentPulse(unitData, viewWorld, duration));
            }

            return duration;
        }

        private IEnumerator PlayFallbackDeploymentPulse(DefenderUnitData unitData, Vector3 world, float duration)
        {
            var go = new GameObject($"DeployPulse_{unitData.displayName}");
            go.transform.position = world + Vector3.up * 0.05f;
            var meshFilter = go.AddComponent<MeshFilter>();
            meshFilter.sharedMesh = unitData.visualMesh != null
                ? unitData.visualMesh
                : Resources.GetBuiltinResource<Mesh>("Quad.fbx");
            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = unitData.visualMaterial;

            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = duration > 0f ? Mathf.Clamp01(elapsed / duration) : 1f;
                float scale = Mathf.Lerp(0.45f, 1.15f, Mathf.Sin(t * Mathf.PI * 0.5f)) * CharacterVisualScale;
                go.transform.localScale = Vector3.one * scale;
                yield return null;
            }

            Destroy(go);
        }

        private IEnumerator PlayDeploymentRingPulse(Vector3 world, float duration)
        {
            var ring = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            ring.name = "DeploymentRingPulse";
            ring.transform.position = world + Vector3.up * 0.08f;
            var collider = ring.GetComponent<Collider>();
            if (collider != null) Destroy(collider);

            var renderer = ring.GetComponent<Renderer>();
            var material = RuntimeMaterialFactory.CreateTransparent(new Color(0.2f, 0.95f, 1f, 0.7f));
            if (renderer != null) renderer.sharedMaterial = material;

            float elapsed = 0f;
            float d = Mathf.Max(0.1f, duration);
            while (elapsed < d)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / d);
                float scale = Mathf.Lerp(0.2f, 1.35f, t);
                ring.transform.localScale = new Vector3(scale, 0.025f, scale);
                if (renderer != null && material != null)
                {
                    var color = material.color;
                    color.a = Mathf.Lerp(0.7f, 0f, t);
                    RuntimeMaterialFactory.ApplyColor(material, color);
                }
                yield return null;
            }

            Destroy(ring);
            Destroy(material);
        }

        private Entity CreateDefenderEntity(
            Vector2Int cell,
            DefenderUnitData unitData,
            bool pendingDeployment,
            bool spawnPlacementVfx)
        {
            var entity = _em.CreateEntity();
            // Phase 4: defenders can now take damage from enemy attackers, so
            // they need an IncomingDamage buffer just like attack units have.
            _em.AddBuffer<IncomingDamage>(entity);
            _defenderByTile[cell] = (entity, unitData);
            _em.AddComponentData(entity, new DefenderTile { cell = new int2(cell.x, cell.y) });
#if UNITY_EDITOR
            _em.SetName(entity, $"Defender_{unitData.displayName}_{cell.x}_{cell.y}");
#endif
            var pos = GridToWorldCenter(cell, spawnHeight);
            _em.AddComponentData(entity, LocalTransform.FromPositionRotationScale(pos, quaternion.identity, CharacterVisualScale));
            _em.AddComponent<DefenderUnitTag>(entity);
            _em.AddComponentData(entity, new Health { value = unitData.health, max = unitData.health });
            _em.AddComponentData(entity, new FactionTag { value = Faction.Defender });
            _em.AddComponentData(entity, new AttackState
            {
                damage = unitData.attackDamage,
                range = unitData.attackRange,
                cooldownDuration = unitData.attackCooldown,
                cooldownRemaining = 0f,
                attackTargetCount = unitData.attackTargetCount,
                targetMask = unitData.targetAllies ? (int)Faction.Defender : (int)Faction.Enemy,
            });
            _em.AddComponentData(entity, new Wassup.Battle.Combat.DefenderCcData
            {
                knockbackDistance   = unitData.knockbackDistance,
                knockbackDuration   = unitData.knockbackDuration,
                onPlacePushDistance = unitData.onPlacePushDistance,
                onPlacePushDuration = unitData.onPlacePushDuration,
                onPlacePushRadius   = unitData.onPlacePushRadius,
            });
            if (unitData.hazardCastEnabled)
            {
                int hazardDataIndex = -1;
                if (unitData.hazardCastKind == HazardCastKind.Zone)
                    hazardDataIndex = RegisterZoneHazardSO(unitData.zoneHazard);
                else if (unitData.hazardCastKind == HazardCastKind.Blocking)
                    hazardDataIndex = RegisterBlockingHazardSO(unitData.blockingHazard);

                _em.AddComponentData(entity, new HazardCastState
                {
                    range = unitData.hazardCastRange,
                    cooldownDuration = unitData.hazardCastCooldown,
                    cooldownRemaining = 0f,
                    targetMask = (int)Faction.Enemy,
                    dataIndex = hazardDataIndex,
                    kind = unitData.hazardCastKind,
                    footprintWidth = math.max(1, unitData.hazardFootprintWidth),
                    footprintHeight = math.max(1, unitData.hazardFootprintHeight),
                });
            }
            if (pendingDeployment)
                _em.AddComponent<PendingDeployment>(entity);

            // Phase 8 §12: placement pulse VFX (procedural particle ring).
            if (spawnPlacementVfx && vfxSpawner != null)
                vfxSpawner.SpawnPlacementRing(new Vector3(pos.x, pos.y, pos.z));

            // Phase 8: if the unit has a Spine skeleton configured, spawn a
            // SkeletonAnimation GameObject instead of the billboard RenderMesh.
            // When no skin/skel is set we fall through to the Phase 5 billboard,
            // so Spine rollout is per-unit and doesn't block unconfigured types.
            bool spineSpawned = false;
            if (spineUnitPool != null)
            {
                var spineWorld = new Vector3(pos.x, pos.y + spineDefenderYOffset, pos.z);
                spineSpawned = spineUnitPool.TrySpawn(unitData, unitData, entity, spineWorld, "SpineDef", out _);
            }
            if (!spineSpawned)
            {
                EnsureMonoViewPools();
                var fallbackWorld = new Vector3(pos.x, pos.y + spineDefenderYOffset, pos.z);
                var mesh = unitData.visualMesh != null
                    ? unitData.visualMesh
                    : Resources.GetBuiltinResource<Mesh>("Quad.fbx");
                var material = ResolveUnitMaterial(unitData.visualMaterial, Color.white);
                defenderFallbackViewPool.TrySpawn(
                    unitData.displayName,
                    entity,
                    fallbackWorld,
                    mesh,
                    material,
                    CharacterVisualScale,
                    out _);
            }

            if (unitData.projectile != null)
            {
                var dataIndex = GetOrCreateProjectileDataIndex(unitData.projectile);
                _em.AddComponentData(entity, new ProjectileRef
                {
                    dataIndex = dataIndex,
                    speed = unitData.projectile.speed,
                    hitThreshold = unitData.projectile.hitThreshold,
                    visualScale = unitData.projectile.visualScale,
                    onHitEffect = unitData.projectile.onHitEffect,
                    splashRadius = unitData.projectile.splashRadius,
                    splashDamageMul = unitData.projectile.splashDamageMul,
                });
            }

            // modifier-framework unit 5: attach AttackOutputElement buffer when SO defines outputs.
            // AttackSystem branches on HasBuffer to decide legacy vs outputs path.
            if (unitData.outputs != null && unitData.outputs.Length > 0)
            {
                var outputBuf = _em.AddBuffer<Wassup.Battle.Combat.AttackOutputElement>(entity);
                foreach (var output in unitData.outputs)
                    outputBuf.Add(new Wassup.Battle.Combat.AttackOutputElement { value = output });
            }

            // modifier-framework unit 6: ModifierStats cache + dirty flag + IncomingHeal buffer.
            // ModifierStats defaults: dmgTakenMul=1 (no reduction), regenPerSec=0 (no regen).
            // ModifierStatsDirty is IEnableableComponent — added disabled; ApplySystem enables when modifiers arrive.
            // IncomingHeal buffer is pre-attached so AttackSystem ECB can AppendToBuffer without structural change.
            _em.AddComponentData(entity, new Wassup.Battle.Effects.ModifierStats
            {
                damageMul      = 1f,
                attackSpeedMul = 1f,
                dmgTakenMul    = 1f,
                regenPerSec    = 0f,
                moveSpeedMul   = 1f,
            });
            _em.AddComponent<Wassup.Battle.Effects.ModifierStatsDirty>(entity);
            _em.SetComponentEnabled<Wassup.Battle.Effects.ModifierStatsDirty>(entity, false);
            _em.AddBuffer<Wassup.Battle.Units.IncomingHeal>(entity);

            // ingame-dreamcatcher Unit 2 — inherit active match-long card effects.
            ApplyActiveDcEffectsTo(entity, unitData);

            CreateHealthBar(entity, yOffset: 0.9f * CharacterVisualScale, baseScale: 0.35f * CharacterVisualScale);
            return entity;
        }

        private void TriggerOnPlaceAndSynergy(DefenderUnitData unitData, Vector2Int cell, Entity entity)
        {
            // Fixed order: onPlace → synergy recompute → log (PHASE4 §2.5 P4-05).
            // onPlace is a standalone snapshot effect and must fire before the
            // new defender's SynergyBuff is computed. Individual on-place effect
            // rules decide whether the placed defender is included.
            int onPlaceAffected = ApplyOnPlaceEffect(unitData, cell, entity);
            ApplyOnPlacePush(unitData, cell);
            _onPlaceTriggeredEntities.Add(entity);
            RecomputeSynergyFor(cell);
            LogOnPlaceAndSynergy(unitData, cell, onPlaceAffected);
        }

        public Unity.Entities.Entity DebugSpawnObstacleAt(Unity.Mathematics.int2 cell, float lifetime = 5f)
        {
            if (_em == null)
            {
                Debug.LogWarning("[ObstacleDebug] _em is null — call StartBattle first.");
                return Unity.Entities.Entity.Null;
            }
            float3 worldPos = GridToWorldCenter(new Vector2Int(cell.x, cell.y));
            var e = Wassup.Battle.Effects.EffectSpawner.SpawnObstacle(_em, cell, worldPos, lifetime);
            Debug.Log($"[ObstacleDebug] Spawned entity {e} at cell {cell} world {worldPos} lifetime={lifetime}s");

            // Debug visual: semi-transparent cube that despawns with the obstacle.
            var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.name = $"DebugObstacle_{cell.x}_{cell.y}";
            cube.transform.position = new Vector3(worldPos.x, worldPos.y + tileSize * 0.5f, worldPos.z);
            cube.transform.localScale = Vector3.one * tileSize;
            var mat = cube.GetComponent<Renderer>().material;
            mat.color = new Color(1f, 0.4f, 0.1f, 0.7f);
            Destroy(cube, lifetime);

            return e;
        }

        public Unity.Entities.Entity SpawnHazardWithVisual(HazardSO so, Unity.Mathematics.int2 cell)
        {
            if (so == null || _em == null)
                return Unity.Entities.Entity.Null;
            if (!TryGetNearestWalkCell(cell, out cell))
            {
                Debug.LogWarning("[BattleBridge] Cannot spawn hazard: generated map has no walk cells.");
                return Unity.Entities.Entity.Null;
            }

            var e = Wassup.Battle.Effects.EffectSpawner.SpawnHazard(_em, so, cell);
            if (e == Unity.Entities.Entity.Null)
                return e;

            RecordHazardSpawn(so, cell);

            if (so.visualPrefab == null)
            {
                Debug.LogWarning($"[BattleBridge] HazardSO '{so.name}' has no visualPrefab. Spawned hazard will be invisible.");
                return e;
            }

            float3 worldOrigin = GridToWorldCenter(new Vector2Int(cell.x, cell.y), 0.05f);
            var visual = Instantiate(so.visualPrefab, new Vector3(worldOrigin.x, worldOrigin.y, worldOrigin.z), Quaternion.identity);
            var scale = ShapeToHazardVisualScale(so.shape, so.radius, visual.transform.localScale.y);
            visual.transform.localScale = scale * tileSize;

            var lifetime = visual.GetComponent<Wassup.Presentation.HazardVisualLifetime>();
            if (lifetime == null)
                lifetime = visual.AddComponent<Wassup.Presentation.HazardVisualLifetime>();
            lifetime.Init(so.lifetime);

            return e;
        }

        public Unity.Entities.Entity DebugSpawnHazardAt(HazardSO so, Unity.Mathematics.int2 cell)
        {
            return SpawnHazardWithVisual(so, cell);
        }

        public Unity.Entities.Entity SpawnBlockingHazardWithVisual(BlockingHazardSO so, Unity.Mathematics.int2 cell)
        {
            if (so == null || _em == null)
                return Unity.Entities.Entity.Null;

            int dataIndex = RegisterBlockingHazardSO(so);
            var entity = Wassup.Battle.Effects.EffectSpawner.SpawnBlockingHazard(_em, so, cell, dataIndex);
            if (entity == Unity.Entities.Entity.Null)
                return entity;

#if UNITY_EDITOR
            _em.SetName(entity, $"BlockingHazard_{so.name}_{cell.x}_{cell.y}");
#endif

            CreateHealthBar(entity, yOffset: 1.35f * CharacterVisualScale, baseScale: 0.55f * CharacterVisualScale);

            if (so.visualPrefab == null)
            {
                Debug.LogWarning($"[BattleBridge] BlockingHazardSO '{so.name}' has no visualPrefab. Spawned hazard will be invisible.");
                return entity;
            }

            EnsureBlockingHazardVisualRoot();
            var p = _em.GetComponentData<LocalTransform>(entity).Position;
            var visual = Instantiate(so.visualPrefab, new Vector3(p.x, p.y, p.z), Quaternion.identity, _blockingHazardVisualRoot);
            var presenter = visual.GetComponent<BlockingHazardPresenter>();
            if (presenter == null)
                presenter = visual.AddComponent<BlockingHazardPresenter>();
            presenter.Bind(entity);
            _blockingHazardVisualMap[entity] = visual;
            return entity;
        }

        public Unity.Entities.Entity DebugSpawnBlockingHazardAt(BlockingHazardSO so, Unity.Mathematics.int2 cell)
        {
            if (!Application.isPlaying)
            {
                Debug.LogWarning("[BlockingHazardDebug] Enter Play Mode first.");
                return Unity.Entities.Entity.Null;
            }
            if (_em == null)
            {
                Debug.LogWarning("[BlockingHazardDebug] EntityManager is not ready. Start battle first.");
                return Unity.Entities.Entity.Null;
            }
            return SpawnBlockingHazardWithVisual(so, cell);
        }

        private int RegisterBlockingHazardSO(BlockingHazardSO so)
        {
            if (so == null) return -1;
            if (_blockingHazardSoIndex.TryGetValue(so, out int idx)) return idx;
            idx = _blockingHazardSoRegistry.Count;
            _blockingHazardSoRegistry.Add(so);
            _blockingHazardSoIndex[so] = idx;
            return idx;
        }

        private int RegisterZoneHazardSO(HazardSO so)
        {
            if (so == null) return -1;
            if (_zoneHazardIndex.TryGetValue(so, out int idx)) return idx;
            idx = _zoneHazardRegistry.Count;
            _zoneHazardRegistry.Add(so);
            _zoneHazardIndex[so] = idx;
            return idx;
        }

        private void EnsureBlockingHazardVisualRoot()
        {
            if (_blockingHazardVisualRoot != null) return;
            var root = new GameObject("BlockingHazardVisuals");
            root.transform.SetParent(transform, worldPositionStays: false);
            _blockingHazardVisualRoot = root.transform;
        }

        private void ClearBlockingHazardVisuals()
        {
            foreach (var kv in _blockingHazardVisualMap)
            {
                if (kv.Value != null)
                    Destroy(kv.Value);
            }
            _blockingHazardVisualMap.Clear();
            if (_blockingHazardVisualRoot != null)
                Destroy(_blockingHazardVisualRoot.gameObject);
            _blockingHazardVisualRoot = null;
        }

        private void RecordHazardSpawn(HazardSO so, Unity.Mathematics.int2 cell)
        {
            if (so == null) return;
            if (so.effects == null || so.effects.Length == 0)
            {
                GameManager.Instance?.Logger?.RecordHazard(new Logging.HazardLog
                {
                    event_type = "spawn",
                    hazard_id = so.name,
                    kind = string.Empty,
                    tile = new Vector2Int(cell.x, cell.y),
                    time = Time.time - _startTime,
                    target_index = -1,
                });
                return;
            }

            for (int i = 0; i < so.effects.Length; i++)
            {
                var effect = so.effects[i];
                GameManager.Instance?.Logger?.RecordHazard(new Logging.HazardLog
                {
                    event_type = "spawn",
                    hazard_id = so.name,
                    kind = effect.kind.ToString(),
                    tile = new Vector2Int(cell.x, cell.y),
                    time = Time.time - _startTime,
                    scalar = effect.param1,
                    target_index = -1,
                });
            }
        }

        private void DrainHazardRuntimeEvents()
        {
            if (!_hazardRuntimeEventQueue.IsCreated) return;
            while (_hazardRuntimeEventQueue.TryDequeue(out var evt))
            {
                string eventType = evt.eventType == HazardRuntimeEventType.ZoneApply ? "zone_apply" : "dot_damage";
                GameManager.Instance?.Logger?.RecordHazard(new Logging.HazardLog
                {
                    event_type = eventType,
                    hazard_id = string.Empty,
                    kind = evt.kind.ToString(),
                    tile = new Vector2Int(evt.cell.x, evt.cell.y),
                    time = Time.time - _startTime,
                    scalar = evt.scalar,
                    amount = evt.amount,
                    target_index = evt.target.Index,
                });
            }
        }

        private void DrainHazardSpawnRequests()
        {
            if (!_hazardSpawnRequestQueue.IsCreated) return;
            while (_hazardSpawnRequestQueue.TryDequeue(out var req))
            {
                if (!_em.Exists(req.caster)) continue;

                if (req.kind == HazardCastKind.Zone)
                {
                    if (req.dataIndex < 0 || req.dataIndex >= _zoneHazardRegistry.Count)
                    {
                        Debug.LogWarning($"[HazardCast] Invalid zone hazard index {req.dataIndex}; dropping.");
                        continue;
                    }

                    var so = _zoneHazardRegistry[req.dataIndex];
                    if (so == null) continue;
                    SpawnHazardWithVisual(so, req.centerCell);
                }
                else if (req.kind == HazardCastKind.Blocking)
                {
                    if (req.dataIndex < 0 || req.dataIndex >= _blockingHazardSoRegistry.Count)
                    {
                        Debug.LogWarning($"[HazardCast] Invalid blocking hazard index {req.dataIndex}; dropping.");
                        continue;
                    }

                    var so = _blockingHazardSoRegistry[req.dataIndex];
                    if (so == null) continue;
                    SpawnBlockingHazardWithVisual(so, req.centerCell);
                }
            }
        }

        private void DrainHazardDestroyedEvents()
        {
            if (!_hazardDestroyedQueue.IsCreated) return;
            while (_hazardDestroyedQueue.TryDequeue(out var evt))
            {
                BlockingHazardSO so = null;
                if (evt.hazardSoIndex >= 0 && evt.hazardSoIndex < _blockingHazardSoRegistry.Count)
                    so = _blockingHazardSoRegistry[evt.hazardSoIndex];

                if (_blockingHazardVisualMap.TryGetValue(evt.hazardEntity, out var visual) && visual != null)
                {
                    var presenter = visual.GetComponent<BlockingHazardPresenter>();
                    if (presenter != null)
                        presenter.OnDestroyed(so != null ? so.destructionVfxPrefab : null);
                    else
                        Destroy(visual);
                }
                else if (so != null && so.destructionVfxPrefab != null)
                {
                    Instantiate(so.destructionVfxPrefab, new Vector3(evt.worldPosition.x, evt.worldPosition.y, evt.worldPosition.z), Quaternion.identity);
                }

                _blockingHazardVisualMap.Remove(evt.hazardEntity);
            }
        }

        // --- Stack Modifier SO Registry ---

        private void BuildStackThresholdRegistry()
        {
            _stackThresholds.Clear();
            if (stackModifierAuthoring == null) return;
            foreach (var so in stackModifierAuthoring)
            {
                if (so == null) continue;
                _stackThresholds[so.kind] = so.thresholds ?? System.Array.Empty<Wassup.Data.ThresholdRule>();
            }
        }

        /// <summary>
        /// Called by StackModifierTickSystem (managed context — not Burst).
        /// Returns the ThresholdRule array for the given StackKind, or empty if none registered.
        /// </summary>
        public static Wassup.Data.ThresholdRule[] GetStackThresholds(Wassup.Battle.Effects.StackKind kind)
        {
            if (_stackThresholds.TryGetValue(kind, out var rules))
                return rules;
            return System.Array.Empty<Wassup.Data.ThresholdRule>();
        }

        private static Vector3 ShapeToHazardVisualScale(HazardShape shape, int radius, float yScale)
        {
            float side = shape switch
            {
                HazardShape.SingleCell => 1f,
                HazardShape.Square3x3 => 3f,
                HazardShape.RadiusSquare => 2f * Mathf.Max(1, radius) + 1f,
                _ => 1f,
            };
            return new Vector3(side, yScale, side);
        }

        [UnityEngine.ContextMenu("Debug Spawn Obstacle At (3,1)")]
        private void DebugSpawnObstacleContext() => DebugSpawnObstacleAt(new Unity.Mathematics.int2(3, 1), 5f);

        private void ApplyOnPlacePush(DefenderUnitData unitData, Vector2Int cell)
        {
            if (unitData.onPlacePushDistance <= 0f || unitData.onPlacePushDuration <= 0f
                || unitData.onPlacePushRadius <= 0f) return;
            if (!_aliveAttackersQueryCreated) return;

            float3 defCenter = GridToWorldCenter(cell);
            int tileRange = GridMath.RangeToTiles(unitData.onPlacePushRadius);
            float speed = unitData.onPlacePushDistance / unitData.onPlacePushDuration;

            var entities = _aliveAttackersQuery.ToEntityArray(Allocator.Temp);
            for (int i = 0; i < entities.Length; i++)
            {
                var e = entities[i];
                if (!_em.HasComponent<LocalTransform>(e)) continue;
                var pos = _em.GetComponentData<LocalTransform>(e).Position;
                if (!InTileRange(pos, cell, tileRange)) continue;
                float3 toEnemy = pos - defCenter;
                toEnemy.y = 0f;
                float3 dir = math.normalizesafe(toEnemy);
                EffectSpawner.ApplyCc(_em, e, new CcEffect
                {
                    kind = CcKind.Impulse,
                    vector = dir * speed,
                    remainingTime = unitData.onPlacePushDuration,
                });
            }
            entities.Dispose();
        }

        private void LogOnPlaceAndSynergy(DefenderUnitData unitData, Vector2Int cell, int onPlaceAffected)
        {
            var logger = GameManager.Instance?.Logger;
            if (unitData.onPlaceEffect != OnPlaceEffectType.None)
            {
                Debug.Log($"[BattleBridge] On-place {unitData.displayName}: {unitData.onPlaceEffect} affected={onPlaceAffected} at {cell}.");
                if (logger != null)
                {
                    logger.RecordOnPlace(new Logging.OnPlaceUsageLog
                    {
                        unit_type = unitData.displayName,
                        effect = unitData.onPlaceEffect.ToString(),
                        tile = cell,
                        time = Time.time - _startTime,
                        affected_count = onPlaceAffected,
                    });
                }
            }
            if (logger != null)
                logger.SetSynergyStats(_synergyActivations, _synergyPeakCount);
        }

        private static void LogPlacementReject(string source, DefenderUnitData unitData, PlacementRejectReason reason)
        {
            if (reason == PlacementRejectReason.None) return;
            string name = unitData != null ? unitData.displayName : "<null>";
            Debug.LogWarning($"[BattleBridge] {source} rejected {name}: {reason}.");
        }

        private void OnDestroy()
        {
            if (resultScreen != null)
            {
                resultScreen.RestartRequested -= OnRestartRequested;
                resultScreen.RedraftRequested -= OnRedraftRequested;
            }

            BackdropMounter.Unmount(ref _backdropRoot);
            TeardownCurrentBattle();

            if (_healthBarMaterial != null) Destroy(_healthBarMaterial);
            for (int i = 0; i < _ownedRuntimeMaterials.Count; i++)
            {
                if (_ownedRuntimeMaterials[i] != null)
                    Destroy(_ownedRuntimeMaterials[i]);
            }
            _ownedRuntimeMaterials.Clear();
        }

        private void EnsureMonoViewPools()
        {
            if (spineUnitPool == null)
            {
                var go = new GameObject("SpineUnitViewPool");
                go.transform.SetParent(transform, worldPositionStays: false);
                spineUnitPool = go.AddComponent<Wassup.Presentation.SpineUnitPool>();
            }
            if (enemyViewPool == null)
                enemyViewPool = CreateViewPool("EnemyViewPool");
            if (defenderFallbackViewPool == null)
                defenderFallbackViewPool = CreateViewPool("DefenderFallbackViewPool");
        }

        private Wassup.Presentation.QuadUnitViewPool CreateViewPool(string poolName)
        {
            var go = new GameObject(poolName);
            go.transform.SetParent(transform, worldPositionStays: false);
            return go.AddComponent<Wassup.Presentation.QuadUnitViewPool>();
        }

        private Material ResolveUnitMaterial(Material source, Color fallbackColor)
        {
            if (source != null) return source;
            var material = RuntimeMaterialFactory.CreateOpaque(fallbackColor);
            if (material != null) _ownedRuntimeMaterials.Add(material);
            return material;
        }

        private static int EffectiveSpawnIndex(int authoredIndex, int deckIndex, int laneCount)
        {
            if (laneCount <= 0) return 0;
            if (laneCount <= 2)
                return math.clamp(authoredIndex, 0, laneCount - 1);
            return math.abs(deckIndex) % laneCount;
        }

        private void SpawnUnit(PendingSpawnEntry pending)
        {
            var entry = pending.entry;
            if (entry.unitType == null)
            {
                Debug.LogWarning("[BattleBridge] SpawnEntry missing unitType, skipping.");
                return;
            }

            if (!_generatedMap.IsCreated || _generatedMap.spawns.Length == 0)
            {
                Debug.LogWarning("[BattleBridge] GeneratedMap.spawns empty — cannot spawn attacker");
                return;
            }

            if (entry.unitType.visualMaterial == null)
            {
                Debug.LogWarning("[BattleBridge] visualMaterial null — entity will not render.");
                return;
            }

            var entity = _em.CreateEntity();
#if UNITY_EDITOR
            _em.SetName(entity, $"Enemy_{entry.unitType.displayName}");
#endif

            int spawnIndex = EffectiveSpawnIndex(entry.spawnIndex, pending.deckIndex, _generatedMap.spawns.Length);
            if (spawnIndex < 0 || spawnIndex >= _generatedMap.spawns.Length)
            {
                Debug.LogWarning($"[BattleBridge] SpawnEntry.spawnIndex={spawnIndex} out of range (spawns={_generatedMap.spawns.Length}). Fallback to 0.");
                spawnIndex = 0;
            }

            var spawn = _generatedMap.spawns[spawnIndex];
            var spawnWorldPos = GridToWorldCenter(new Vector2Int(spawn.x, spawn.y), spawnHeight);
            _em.AddComponentData(entity, LocalTransform.FromPositionRotationScale(spawnWorldPos, quaternion.identity, CharacterVisualScale));

            _em.AddComponent<AttackUnitTag>(entity);
            _em.AddComponentData(entity, new Health { value = entry.unitType.health, max = entry.unitType.health });
            _em.AddComponentData(entity, new FactionTag { value = Faction.Enemy });
            // Pre-attach empty buffers so downstream systems never need structural AddBuffer on hot paths.
            _em.AddBuffer<IncomingDamage>(entity);
            _em.AddBuffer<CcEffect>(entity);

            // modifier-legacy-migration unit 1: enemies attack only through
            // outputs[]. attackDamage remains serialized compatibility data.
            bool hasAttackOutputs = entry.unitType.outputs != null && entry.unitType.outputs.Length > 0;
            if (hasAttackOutputs)
            {
                _em.AddComponentData(entity, new AttackState
                {
                    damage = entry.unitType.attackDamage,
                    range = entry.unitType.attackRange,
                    cooldownDuration = entry.unitType.attackCooldown,
                    cooldownRemaining = 0f,
                    attackTargetCount = 1,
                    targetMask = (int)(Faction.Defender | Faction.BlockingHazard),
                    movePauseOnAttackSec = entry.unitType.movePauseOnAttackSec,
                });
                var outputBuf = _em.AddBuffer<Wassup.Battle.Combat.AttackOutputElement>(entity);
                foreach (var output in entry.unitType.outputs)
                    outputBuf.Add(new Wassup.Battle.Combat.AttackOutputElement { value = output });

                if (entry.unitType.projectile != null)
                {
                    var dataIndex = GetOrCreateProjectileDataIndex(entry.unitType.projectile);
                    _em.AddComponentData(entity, new ProjectileRef
                    {
                        dataIndex = dataIndex,
                        speed = entry.unitType.projectile.speed,
                        hitThreshold = entry.unitType.projectile.hitThreshold,
                        visualScale = entry.unitType.projectile.visualScale,
                        onHitEffect = entry.unitType.projectile.onHitEffect,
                        splashRadius = entry.unitType.projectile.splashRadius,
                        splashDamageMul = entry.unitType.projectile.splashDamageMul,
                    });
                }
            }

            _em.AddComponentData(entity, new PathFollowState
            {
                speed = entry.unitType.moveSpeed,
            });

            EnsureMonoViewPools();
            bool spineSpawned = spineUnitPool != null &&
                                spineUnitPool.TrySpawn(entry.unitType, null, entity, spawnWorldPos, "SpineEnemy", out _);
            if (!spineSpawned)
            {
                var mesh = entry.unitType.visualMesh != null
                    ? entry.unitType.visualMesh
                    : Resources.GetBuiltinResource<Mesh>("Quad.fbx");
                enemyViewPool.TrySpawn(
                    entry.unitType.displayName,
                    entity,
                    spawnWorldPos,
                    mesh,
                    CreateAttackUnitRuntimeMaterial(entry.unitType.visualMaterial),
                    CharacterVisualScale,
                    out _);
            }

            // modifier-framework: ModifierStats cache + dirty flag for enemy entities.
            // Enemies can receive ApplyStat effects (e.g. Stack-threshold debuffs) so they
            // need the same ModifierStats/ModifierStatsDirty as defenders. IncomingHeal is NOT added
            // — enemies do not receive heals.
            _em.AddComponentData(entity, new Wassup.Battle.Effects.ModifierStats
            {
                damageMul      = 1f,
                attackSpeedMul = 1f,
                dmgTakenMul    = 1f,
                regenPerSec    = 0f,
                moveSpeedMul   = 1f,
            });
            _em.AddComponent<Wassup.Battle.Effects.ModifierStatsDirty>(entity);
            _em.SetComponentEnabled<Wassup.Battle.Effects.ModifierStatsDirty>(entity, false);

            CreateHealthBar(entity, yOffset: 0.9f * CharacterVisualScale, baseScale: 0.35f * CharacterVisualScale);
        }

        private Material CreateAttackUnitRuntimeMaterial(Material source)
        {
            if (source == null) return null;
            var material = new Material(source);
            _ownedRuntimeMaterials.Add(material);

            if (material.HasProperty("_AlphaClip"))
                material.SetFloat("_AlphaClip", 1f);
            if (material.HasProperty("_Cutoff"))
                material.SetFloat("_Cutoff", Mathf.Max(material.GetFloat("_Cutoff"), 0.5f));

            material.EnableKeyword("_ALPHATEST_ON");
            material.SetOverrideTag("RenderType", "TransparentCutout");
            material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.AlphaTest;
            return material;
        }
    }
}
