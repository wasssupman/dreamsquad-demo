using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Rendering;
using Unity.Transforms;
using UnityEngine;
using Wassup.Battle.Combat;
using Wassup.Battle.Combat.Projectile;
using Wassup.Battle.Effects;
using Wassup.Battle.Movement;
using Wassup.Battle.Units;
using Wassup.Core;
using Wassup.Data;
using Wassup.Data.MapGrid;
using Wassup.Data.Season;
using Wassup.Rendering;
using Wassup.UI;
using Wassup.Battle;
using Wassup.Core.TimeControl;
// DraftController lives in Wassup.Core above.

namespace Wassup.Bridge
{
    // The ONLY allowed bridge between MonoBehaviour world and ECS world.
    // External MonoBehaviour code must go through this class — no direct EntityManager / World / SystemAPI access.
    public partial class BattleBridge : MonoBehaviour
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
        [SerializeField] private MapPathShape mapPathShape = MapPathShape.Free;
        [SerializeField] private MapGenerationOptions mapGenerationOptions = MapGenerationOptions.Default;
        [SerializeField] private float tileSize = 1f;
        [SerializeField] private float spawnHeight = 0.5f;
        [SerializeField] private ResultScreen resultScreen;
        [SerializeField] private DefenderUnitData[] defenderPool;
        [SerializeField] private DraftController draftController;
        [SerializeField] private SkillRuntime skillRuntime;
        [SerializeField] private PlacementPhaseView _placementPhaseView;
        // gift-phase unit 3 — 재시작도 선물 페이즈를 거친다. 배선되면 BeginGift() 로,
        // 없으면 기존처럼 곧장 배치로 폴백.
        [SerializeField] private GiftPhaseView _giftPhaseView;
        [SerializeField] private Wassup.Presentation.SpineUnitPool spineUnitPool;
        [SerializeField] private Wassup.Presentation.QuadUnitViewPool enemyViewPool;
        [SerializeField] private Wassup.Presentation.QuadUnitViewPool defenderFallbackViewPool;
        // placement-enemy-see-through unit 3 — 드래그 배치 중 적 반투명(가려진 뒤 타일 가시성).
        [SerializeField] private float enemyDragDimAlpha = 0.3f;
        [SerializeField] private float enemyDragDimFadeSpeed = 8f;
        private bool _enemyDimActive;
        private float _enemyDimAlpha = 1f;
        public void SetEnemiesDimmed(bool active) => _enemyDimActive = active;
        // placement-enemy-see-through unit 6 — 드래그 중 배치 하이라이트를 적 위로(TilemapMapView 포워딩).
        public void SetPlacementHighlightAboveUnits(bool above) => tilemapMapView?.SetPlacementHighlightAboveUnits(above);
        [SerializeField] private float spineDefenderYOffset = 0f;

        [Header("Spawn Spread")]
        [Tooltip("스폰 시 적을 이동타일 폭 안에서 중앙 기준 대칭 이산 N-레인 오프셋으로 분산(겹침 방지). 끄면 셀 중심 한 점.")]
        [SerializeField] private bool spawnSpreadEnabled = true;
        [Tooltip("타일폭 대비 분산 절반폭(바깥 레인 ±범위). 0.49 미만이라 유닛이 스폰 셀을 벗어나지 않음.")]
        [SerializeField, Range(0f, 0.49f)] private float spawnSpreadFraction = 0.2f;
        [Tooltip("상단(뒤쪽) 범위 압축 비율. 1=대칭, <1=상단만 좁혀 낮춤(키 큰 캐릭터 보정).")]
        [SerializeField, Range(0f, 1f)] private float spawnSpreadTopScale = 0.5f;
        [Tooltip("측면 레인 수(폭 중앙 기준 대칭). 1=중앙 한 줄, 3=중/상/하. 스폰 순서대로 round-robin 배정.")]
        [SerializeField, Range(1, 7)] private int spawnSubLaneCount = 3;

        [Header("Character Billboard")]
        // Spine units have no shader billboard (unlike the Quad fallback that uses
        [SerializeField] private Wassup.Presentation.VfxSpawner vfxSpawner;
        [SerializeField] private Wassup.Presentation.DamageNumberSpawner damageNumberSpawner;
        // unit-health-display — 체력 표기 시각 파라미터 단일 소스. unit 1 은 적 저체력 틴트만 사용.
        [SerializeField] private Wassup.Data.HealthDisplayStyle healthDisplayStyle;
        // enemy-walk-anim-speed unit 0 — 걷기 애니 속도 변조 파라미터(SpineUnitView 가 정적 미러로 읽음).
        [SerializeField] private Wassup.Data.WalkAnimSpeedStyle walkAnimSpeedStyle;
        // unit-health-display unit 2 — 적 피격 마이크로바 스포너.
        [SerializeField] private Wassup.Presentation.EnemyHitBarSpawner enemyHitBarSpawner;
        // unit-status-fx Unit 2 — 상태 연출 스포너(상태 구동 reconcile). 어그로가 첫
        // 등록 상태(Aggro kind), 스턴 등은 registry 항목 + 아래 reconcile 훅으로 추가.
        [SerializeField] private Wassup.Presentation.StatusFxSpawner statusFxSpawner;
        // unit-health-display unit 3 — 방어유닛 타일 테두리 게이지 레이어.
        [SerializeField] private Wassup.Presentation.TileHealthGaugeLayer tileHealthGaugeLayer;
        // unit-dreamcatcher-icons — 부착 카드 아이콘 스트립 스포너. teardown 회수 대칭용
        // (spec 계약 6). 스트립은 이벤트 구동이라 Placement 진입 전까지 리빌드가 없다 —
        // teardown 이 앵커를 파괴해도 뷰는 마지막 위치를 유지하므로 여기서 명시 회수한다.
        [SerializeField] private Wassup.Presentation.DcIconStripSpawner dcIconStripSpawner;
        [SerializeField] private Wassup.UI.ScoreHudView scoreHud;
        [SerializeField] private Wassup.Presentation.ProjectileViewPool _projectileViewPool;
        // Phase 9 P9-07 — tileSize 단일 소스화. Awake 에서 PlacementInput 으로 주입.
        [SerializeField] private Wassup.Core.PlacementInput placementInput;
        [Header("Tilemap View Backend (tilemap-view-backend)")]
        [SerializeField] private Wassup.Core.BoardViewMode boardViewMode = Wassup.Core.BoardViewMode.TilemapRect;
        [SerializeField] private Wassup.Core.TilemapMapView tilemapMapView;
        [SerializeField] private Wassup.Data.TileSetData tileSet;
        [SerializeField] private Wassup.Data.BoardCameraPreset tilemapCameraPresetRect;
        [SerializeField] private Wassup.Data.BoardCameraPreset tilemapCameraPresetIso;
        [Header("Tilemap mode tuning (tilemap-mode-adoption)")]
        [SerializeField] private float tilemapCharacterScale = 0.42f;
        // tilted-billboard unit 2 — XZ 바닥 + 퍼스펙티브에서 캐릭터가 카메라를 향해 서도록 월드 X 틸트.
        // Euler(tilt,0,0). 카메라 pitch×0.7~0.8 (pitch55→≈45). 양수. 실측 튜닝.
        [SerializeField] private float tilemapBillboardTilt = 45f;
        [Header("Prop distance tilt (tilted-billboard unit 6) — 배경 프랍 거리 기반 틸트")]
        [Tooltip("0=비활성(고정 틸트). 프랍 위치별 시선 elevation 편차에 곱하는 계수. 권장 ≈0.78(카메라 pitch×0.78≈중앙 틸트).")]
        [SerializeField] private float propDistanceTiltFactor = 0.78f;
        [Tooltip("거리 틸트 하한(도).")]
        [SerializeField] private float propDistanceTiltMin = 28f;
        [Tooltip("거리 틸트 상한(도).")]
        [SerializeField] private float propDistanceTiltMax = 62f;
        [Header("Blob shadow (tilted-billboard unit 3) — Tilemap 모드 접지 그림자")]
        [SerializeField] private Sprite blobShadowSprite;
        [Tooltip("블롭 월드 지름의 1타일 기준 배율(원형). 프랍별 추가 배율은 PropData.visualScale.")]
        [SerializeField] private float blobShadowSize = 1f;
        [SerializeField] private Color blobShadowColor = new Color(0f, 0f, 0f, 0.45f);
        [SerializeField] private float blobShadowGroundY = 0.216f; // blob 을 발 평면에서 ~5px(@1080) 띄워 접지점 가독.
        [Tooltip("tilemap-real-shadows — ON=진짜 캐스트 그림자(바닥 receive + 빌보드 cast, 블롭 OFF). 모바일은 강제 블롭.")]
        [SerializeField] private bool useRealShadows = true;
        [Range(0.1f, 1f)]
        [Tooltip("tilemap-world-surround unit 5 — 모바일에서 배경/원경 프랍 수를 줄이는 예산 배율(1=풀, 0.5=절반). 데스크톱/에디터는 무시.")]
        [SerializeField] private float mobilePropBudgetScale = 0.5f;
        [Tooltip("Tilemap 모드에서 비활성할 Legacy 환경 오브젝트 (씬 정리 후 배선). 빈 배열 = no-op.")]
        [SerializeField] private GameObject[] tilemapHiddenEnvironment;
        [Header("Stack Modifier Registry")]
        [SerializeField] private Wassup.Data.StackModifierSO[] stackModifierAuthoring;
        [Header("Battle Rules")]
        [Tooltip("같은 방어 유닛을 인접 배치했을 때의 공격력 시너지. 기본 비활성.")]
        [SerializeField] private bool enableAdjacencySynergy;

        [Header("Season Gimmick — Pickup View (season-gimmick-overwork unit 6)")]
        [Tooltip("레드불 픽업 뷰 모델/프리팹(FBX 가능). 비우면 절차적 플레이스홀더 큐브.")]
        [SerializeField] private GameObject pickupViewPrefab;
        [Tooltip("픽업 뷰의 셀 중심 위 높이(월드, 지면 hover).")]
        [SerializeField] private float pickupViewHeight = 0.3f;
        [Tooltip("모델 로컬 스케일(FBX 크기 보정).")]
        [SerializeField] private float pickupModelScale = 1f;
        [Tooltip("모델 로컬 기준 Y(피벗 보정용 추가 오프셋).")]
        [SerializeField] private float pickupModelBaseY = 0f;
        [Tooltip("픽업 모델 머티리얼 override(FBX 임베디드 머티리얼 텍스처 미바인딩 우회). 비우면 원본.")]
        [SerializeField] private Material pickupOverrideMaterial;

        private ManualMapInput? _manualMapInput;
        private GeneratedMap _generatedMap;
        private int2? _mapGridGridSizeOverride;

        private World _world;
        private EntityManager _em;
        private EntityQuery _aliveAttackersQuery;
        private bool _aliveAttackersQueryCreated;
        // aggro-targeting Unit 13 — 어그로된 적 아이콘 reconcile 용 쿼리(Aggroed).
        private EntityQuery _aggroedQuery;
        private bool _aggroedQueryCreated;
        // nightmare-whip-aura unit 3 rev 2 — 메커닉 선언(auraPrefab) 부착 오라 풀.
        // 런타임 소유(씬 배선 없음), 베이크 등록 시 lazy 생성, teardown 에서 해제.
        private Wassup.Presentation.DcAuraVisualPool _dcAuraPool;
        // unit-status-fx 5 — Sleep 연출 소스. CcEffect(Effects 소유)는 읽기만 한다.
        private EntityQuery _ccEffectQuery;
        private bool _ccEffectQueryCreated;
        // unit-buff-debuff-aura 1 — 버프/디버프 오라 소스. StatModifierSlot 버퍼(Effects 소유)는 읽기만.
        // 임시(유한 지속) 슬롯만 판정 — 영구 baseline(로드아웃/시너지/드림캐쳐)은 classifier 가 제외.
        private EntityQuery _modifierSlotQuery;
        private bool _modifierSlotQueryCreated;
        // season-gimmick-overwork unit 6 — 레드불 픽업 뷰 조정용 쿼리 (Pickup 은 Effects 소유, 읽기만).
        private EntityQuery _pickupViewQuery;
        private bool _pickupViewQueryCreated;
        private readonly List<PendingSpawnEntry> _pending = new();
        private readonly List<Material> _ownedRuntimeMaterials = new();
        private readonly HashSet<Vector2Int> _occupiedTiles = new();
        private readonly Dictionary<Vector2Int, (Entity entity, DefenderUnitData data)> _defenderByTile = new();
        private readonly HashSet<Entity> _onPlaceTriggeredEntities = new();
        // effect-tiles unit 1 — 셀 → 효과 타일 (bridge-side, sim 무관). 맵 빌드마다 재구축.
        private readonly Dictionary<Vector2Int, EffectTileData> _effectTilesByCell = new();
        private readonly List<ProjectileData> _projectileDataByIndex = new();
        private readonly Dictionary<ProjectileData, int> _projectileDataIndex = new();
        private readonly List<HazardSO> _zoneHazardRegistry = new();
        private readonly Dictionary<HazardSO, int> _zoneHazardIndex = new();
        private readonly List<BlockingHazardSO> _blockingHazardSoRegistry = new();
        private readonly Dictionary<BlockingHazardSO, int> _blockingHazardSoIndex = new();
        private static readonly Dictionary<Wassup.Battle.Effects.StackKind, Wassup.Data.ThresholdRule[]> _stackThresholds = new();
        private readonly Dictionary<Entity, GameObject> _blockingHazardVisualMap = new();
        private Transform _blockingHazardVisualRoot;
        // season-gimmick-overwork unit 6 — 레드불 픽업 엔티티↔뷰 GameObject 매핑.
        private readonly Dictionary<Entity, GameObject> _pickupVisualMap = new();
        // 조정 시 제거 대상 임시 버퍼 (반복 중 수정 회피, 매 프레임 재사용).
        private readonly List<Entity> _pickupReapBuffer = new();
        private EntityQuery _projectileSpawnRequestQuery;
        private bool _projectileSpawnRequestQueryCreated;
        private EntityQuery _projectileQuery;
        private bool _projectileQueryCreated;
        // tilemap-mode-adoption unit 0 — 유닛 스케일. const 제거. 맵 빌드 시 설정.
        public static float CharacterVisualScale { get; private set; } = 0.42f;
        // Live-readable mirror of tilemapBillboardTilt, read by SpineUnitView each
        // LateUpdate. Synced from the serialized field in Awake/OnValidate; can be
        // poked at runtime (e.g. via tooling) to tune the lean without recompiling.
        public static float CharacterBillboardTilt = 45f;
        // tilted-billboard unit 6 — 배경 프랍 거리 기반 틸트 튜닝 미러(PropBillboard 가 읽음). factor=0=비활성.
        public static float PropDistanceTiltFactor { get; private set; }
        public static float PropDistanceTiltMin { get; private set; } = 28f;
        public static float PropDistanceTiltMax { get; private set; } = 62f;
        // tilted-billboard unit 3 — 블롭 그림자 데이터(하드코딩 금지: serialized 필드에서 빌드 시 미러).
        public static Sprite BlobShadowSprite { get; private set; }
        public static float BlobShadowSize { get; private set; } = 1f;
        public static Color BlobShadowColor { get; private set; } = new Color(0f, 0f, 0f, 0.45f);
        public static float BlobShadowGroundY { get; private set; } = 0.02f;
        // tilemap-real-shadows — 진짜 그림자 모드(데스크톱) vs 블롭(모바일/OFF). 빌드 시 모바일 강제 OFF.
        public static bool UseRealShadows { get; private set; }
        // enemy-walk-anim-speed unit 0 — 걷기 애니 속도 변조 미러(SpineUnitView 가 읽음). SO 미할당 시
        // Enabled=false → 뷰는 배율 1.0(현행 동작, 회귀 없음). 빌드 시 serialized SO 에서 1회 복사.
        public static bool WalkAnimSpeedEnabled { get; private set; }
        public static float WalkAnimRefSpeed { get; private set; } = 2.5f;
        public static float WalkAnimMinTimeScale { get; private set; } = 0.15f;
        public static float WalkAnimMaxTimeScale { get; private set; } = 2f;
        public static float WalkAnimSmoothing { get; private set; } = 0.2f;
        public static float WalkAnimTeleportGuard { get; private set; } = 1.5f;
        private const float SynergyPerNeighbor = 0.1f;
        private readonly HashSet<Entity> _synergyActivatedEntities = new();
        private int _synergyActivations;
        private int _synergyPeakCount;
        private const int EnemyKillScoreDelta = 10;
        private float _startTime;
        // time-manager Unit 3 — 전투 도메인 스케일이 반영된 경과 클럭(웨이브/타이머 load-bearing).
        // _startTime(실시간)은 cosmetic 이벤트/로그 타임스탬프 전용으로 남긴다.
        private double _battleClock;
        private Entity _battleTimeScaleEntity = Entity.Null;
        private float _timerDuration;
        private bool _running;
        public float LogElapsedTime => Mathf.Max(0f, (float)_battleClock);
        private bool _placementAllowed;
        private bool _resultShown;
        // draft-stage-map-prebuild Unit 0 — ECS infrastructure idempotent guard.
        private bool _ecsInfrastructureReady;
        private bool _usingGeneratedWaves;
        private GeneratedWavePlan _wavePlan;
        // wave-authoring-test-mode unit 2 — 테스트 모드 작성 플랜. null 이면 seed 경로.
        private WavePlanAsset _authoredPlan;
        private bool _usingAuthoredPlan;
        private int _nextWaveIndex;
        private int _goalReachedCount;
        // subconscious-curse-expansion unit 1 (몽마의 계약) — 유출 허용치 선불 지불의
        // 런타임 오프셋. SO(deck.defeatGoalReachedCount)는 절대 불변 — 직접 감소시키면
        // 에디터 자산 영구 오염 + 기기에서 매치 간 누적된다(spec critic M1). 매치 리셋
        // (BeginPlacement)에서 0 초기화. 환불 경로 없음(§6 세탁 차단).
        private int _leakAllowancePenalty;
        private NativeQueue<GoalReachedEvent> _goalEventQueue;
        private NativeQueue<DefenderDeathEvent> _defenderDeathQueue;
        private NativeQueue<Wassup.Battle.Combat.UnitAttackVisualEvent> _unitAttackVisualQueue;
        private NativeQueue<Wassup.Battle.Combat.Projectile.ProjectileHitEvent> _projectileHitEventQueue;
        // aggro-targeting Unit 11 — Combat(AttackSystem)→Effects(AggroStateSystem) 히트 채널.
        private NativeQueue<Wassup.Battle.Effects.AggroHitEvent> _aggroHitEventQueue;
        // nightmare-catcher unit 1 — Combat→Combat 보스 위협 귀속 채널.
        private NativeQueue<Wassup.Battle.Combat.ThreatHitEvent> _threatHitEventQueue;
        // nightmare-catcher unit 3 — Combat→Movement 텔레포트(SelfBlink) 요청 채널.
        private NativeQueue<Wassup.Battle.Movement.BlinkRequestEvent> _blinkRequestQueue;
        private NativeQueue<Wassup.Battle.Units.HealAppliedEvent> _healAppliedEventQueue;
        private NativeQueue<Wassup.Battle.Units.DamageNumberEvent> _damageNumberEventQueue;
        private NativeQueue<Wassup.Battle.Units.EnemyKilledEvent> _enemyKilledEventQueue;
        private NativeQueue<Wassup.Battle.Effects.EnemyCcEvent> _enemyCcQueue;
        // combat-action-lock unit 3 — wake-on-hit(Sleep 해제) Units→Effects 채널.
        private NativeQueue<Wassup.Battle.Effects.CcClearRequest> _ccClearQueue;
        private NativeQueue<Wassup.Battle.Effects.StatModifierApplyEvent> _statModifierQueue;
        private NativeQueue<Wassup.Battle.Effects.StackModifierApplyEvent> _stackModifierQueue;
        private NativeQueue<Wassup.Battle.Effects.HazardRuntimeEvent> _hazardRuntimeEventQueue;
        private NativeQueue<Wassup.Battle.Effects.HazardDestroyedEvent> _hazardDestroyedQueue;
        private NativeQueue<Wassup.Battle.Effects.HazardSpawnRequest> _hazardSpawnRequestQueue;
        private NativeQueue<Wassup.Battle.Combat.AttackOutputLogEvent> _attackOutputLogQueue;
        private Unity.Collections.NativeHashSet<Unity.Mathematics.int2> _blockedCells;
        private Unity.Collections.NativeParallelMultiHashMap<Unity.Mathematics.int2, Wassup.Battle.Effects.HazardEffect> _hazardCellToEffects;

        // Phase 9 flow field 싱글톤 entity reference
        private Entity _flowFieldSingleton = Entity.Null;
        // boss-defender-field unit 1 — 방어유닛-지향 필드. goal field 와 라이프사이클 동일
        // (BuildFlowField 생성 / TeardownFlowField 정리). 내용 갱신은 DefenderFieldSystem.
        private Entity _defenderFieldSingleton = Entity.Null;
        // season-gimmick-overwork unit 4 — 레드불 픽업 스폰 상태(후보 셀 배열 소유).
        // goal/defender field 와 동일 lifecycle (BuildPickupSpawnState / TeardownFlowField).
        private Entity _pickupSpawnStateSingleton = Entity.Null;

        // enemy-tile-movement-integrity unit 0 — 스폰 측면 분산 순번(맵 빌드마다 0 리셋). 결정론 수열 인덱스.
        private int _spawnSpreadCounter;

        // map-origin-placement: board 월드 원점. 모든 grid↔world 변환의 단일 소스.
        // Tilemap 모드는 무조건 zero (BuildMapForBattle 에서 고정).
        private float3 _boardOrigin = float3.zero;

        // match-seed-unification — GameManager 가 주입하는 단일 매치 시드.
        // 맵/웨이브/비주얼 시드가 여기서 파생된다(작업 2/3). 0 = 미주입(즉석 폴백).
        private int _matchSeed;
        public void SetMatchSeed(int seed) => _matchSeed = seed;

        // gimmick-match-integration unit 1 — GameManager 가 배정한 매치 기믹(없으면 null).
        // 3개 소비 지점(config 주입·픽업 스폰 게이트·디버그 로그)의 단일 소스. 시즌 결합 대체.
        private Wassup.Data.GimmickData _assignedGimmick;
        public void SetAssignedGimmick(Wassup.Data.GimmickData g) => _assignedGimmick = g;

        private struct PendingSpawnEntry
        {
            public SpawnEntry entry;
            public int deckIndex;
        }

        private void Awake()
        {
            if (tilemapMapView == null)
                Debug.LogError("[BattleBridge] tilemapMapView reference missing — assign in Inspector.", this);

            if (placementInput == null)
                Debug.LogError("[BattleBridge] placementInput reference missing — assign in Inspector.", this);

            SeasonRuntime.Bind(seasonRegistry);
            if (seasonRegistry == null || seasonRegistry.activeSeason == null
                || seasonRegistry.activeSeason.mapTheme == null)
            {
                Debug.LogError("[BattleBridge] SeasonRegistry / activeSeason / mapTheme 가 wiring 되지 않았다. BattleScene 에 SeasonRegistry.asset 을 연결하라.", this);
            }

            CharacterBillboardTilt = tilemapBillboardTilt;

            EnsureMonoViewPools();
        }

        private void OnValidate()
        {
            // Keep the static mirror in sync while tuning in the inspector (edit/play).
            CharacterBillboardTilt = tilemapBillboardTilt;
        }

        // gift-phase unit 3 — 재시작 진입은 선물 페이즈를 거친다(배선 시). 미배선이면
        // 기존처럼 곧장 배치로(HandController 가 Placement 에서 폴백 구성).
        private void EnterPlacementOrGift()
        {
            if (_giftPhaseView != null) _giftPhaseView.BeginGift();
            else _placementPhaseView?.BeginPlacementPhase();
        }

        // result-screen-lobby-exit unit 0 — 결과창 버튼이 "로비로" 가 되면서 호출처가
        // 없다(끊긴 배선이 아니라 의도). 재시작을 되살릴 때 다시 구독하면 되도록
        // 로직은 남겨둔다. EnterPlacementOrGift / ReLogSkillLoadoutForNewSession 도
        // 이 경로 전용이라 함께 대기 상태다.
        private void OnRestartRequested()
        {
            if (_world == null)
            {
                EnterPlacementOrGift();
                return;
            }

            var logger = GameManager.Instance?.Logger;
            if (logger != null)
            {
                logger.StartReplacementSession("restart", incrementRestartIndex: true);
                if (GameManager.Instance != null) logger.SetMatchSeeds(GameManager.Instance.MatchSeed, GameManager.Instance.MatchSeedFixed);
                // Phase 7 (Q6=a): Restart keeps the same picked skill loadout;
                // refresh it after the session rollover so the new log mirrors
                // the loadout the player actually plays with.
                ReLogSkillLoadoutForNewSession(logger);
            }

            // tournament-play-report Unit 3 — a restart is a new tournament
            // attempt. The sole live restart path since the REDRAFT removal.
            Wassup.Core.Api.TournamentMatchReporter.BeginMatch();

            TeardownCurrentBattle();
            if (resultScreen != null) resultScreen.Hide();
            _running = false;
            _resultShown = false;
            EnterPlacementOrGift();
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

        private void TeardownCurrentBattle()
        {
            _running = false;
            _placementAllowed = false;
            if (skillRuntime != null) skillRuntime.ResetAll();
            // time-manager — 시간 스케일 요청도 매치 경계에서 초기화(앱 수명 싱글턴이라 매치 간
            // 잔존 방지; 고아 lease 안전망). skillRuntime.ResetAll 과 동일 지점(시작·종료 양쪽).
            TimeManager.Instance.ResetAll();
            if (GameManager.Instance != null && GameManager.Instance.CostRuntime != null)
                GameManager.Instance.CostRuntime.StopRegen();
            if (spineUnitPool != null) spineUnitPool.DisposeAll();
            if (enemyViewPool != null) enemyViewPool.DisposeAll();
            if (defenderFallbackViewPool != null) defenderFallbackViewPool.DisposeAll();
            if (tileHealthGaugeLayer != null) tileHealthGaugeLayer.Clear(); // unit 3 — 게이지 전체 정리
            if (enemyHitBarSpawner != null) enemyHitBarSpawner.Clear(); // unit 2 — 잔여 마이크로바 정리(생명주기 대칭)
            if (statusFxSpawner != null) statusFxSpawner.Clear(); // unit-status-fx unit 2 — 잔여 상태 연출 정리
            if (dcIconStripSpawner != null) dcIconStripSpawner.Clear(); // unit-dreamcatcher-icons — 잔여 아이콘 스트립 정리(생명주기 대칭)
            ClearPickupVisuals(); // season-gimmick-overwork unit 6 — 잔여 레드불 뷰 정리
            _dcAuraPool?.Clear(); _dcAuraPool = null; // nightmare-whip-aura rev 2 — 드림캐쳐 부착 오라 정리(생명주기 대칭)
            ClearBlockingHazardVisuals();

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
                _defenderFieldSingleton = Entity.Null;
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
            // dreamcatcher-unit-trigger Unit 1 — request carriers normally die in
            // the same-frame drain; this covers stragglers when battle stops
            // between stage and drain.
            DestroyEntitiesByType<ProjectileRequestCarrier>();
            DestroyEntitiesByType<Wassup.Battle.Effects.Hazard>();
            DestroyEntitiesByType<Wassup.Battle.Effects.BlockingHazard>();
            DestroyEntitiesByType<Wassup.Battle.Effects.Obstacle>();
            // season-gimmick-overwork unit 4 — 레드불 픽업 엔티티 정리.
            DestroyEntitiesByType<Wassup.Battle.Effects.Pickup>();
        }

        private void DestroyEcsInfrastructureEntities()
        {
            DestroyEntitiesByType<GoalReachedEventsSingleton>();
            DestroyEntitiesByType<DefenderDeathEventsSingleton>();
            DestroyEntitiesByType<Wassup.Battle.Combat.UnitAttackVisualEventsSingleton>();
            DestroyEntitiesByType<Wassup.Battle.Combat.Projectile.ProjectileHitEventsSingleton>();
            DestroyEntitiesByType<Wassup.Battle.Units.HealAppliedEventsSingleton>();
            DestroyEntitiesByType<Wassup.Battle.Units.DamageNumberEventsSingleton>();
            DestroyEntitiesByType<Wassup.Battle.Units.EnemyKilledEventsSingleton>();
            DestroyEntitiesByType<Wassup.Battle.Effects.EnemyCcEventsSingleton>();
            DestroyEntitiesByType<Wassup.Battle.Effects.CcClearRequestsSingleton>();
            DestroyEntitiesByType<Wassup.Battle.Effects.StatModifierApplyEventsSingleton>();
            DestroyEntitiesByType<Wassup.Battle.Effects.StackModifierApplyEventsSingleton>();
            DestroyEntitiesByType<Wassup.Battle.Effects.HazardRuntimeEventsSingleton>();
            DestroyEntitiesByType<Wassup.Battle.Effects.HazardDestroyedEventsSingleton>();
            DestroyEntitiesByType<Wassup.Battle.Effects.HazardSpawnRequestsSingleton>();
            DestroyEntitiesByType<Wassup.Battle.Combat.AttackOutputLogEventsSingleton>();
            DestroyEntitiesByType<Wassup.Battle.Effects.AggroHitEventsSingleton>();
            DestroyEntitiesByType<Wassup.Battle.Combat.ThreatHitEventsSingleton>();
            DestroyEntitiesByType<Wassup.Battle.Movement.BlinkRequestEventsSingleton>();
            DestroyEntitiesByType<Wassup.Battle.Effects.ObstacleSingleton>();
            DestroyEntitiesByType<Wassup.Battle.Effects.HazardSingleton>();
            // time-manager H1 — BattleTimeScale singleton 도 다른 인프라 싱글턴과 대칭으로 파괴.
            // 누락 시 StopBattle 후 orphan 이 남고 다음 프레임 새 엔티티가 생겨 2개 → TryGetSingleton
            // 실패 → 이후 모든 전투에서 시간 제어(정지/슬로우모)가 영구 무력화된다.
            DestroyEntitiesByType<BattleTimeScale>();
            // season-gimmick-overwork unit 2 — 기믹 config 도 대칭 파괴 (BattleTimeScale 교훈 준수).
            DestroyEntitiesByType<Wassup.Battle.Effects.BurnoutGimmickConfig>();
            DestroyEntitiesByType<Wassup.Battle.Effects.RedBullGimmickConfig>();
        }

        private void DisposeEcsInfrastructureNativeContainers()
        {
            if (_goalEventQueue.IsCreated) _goalEventQueue.Dispose();
            if (_defenderDeathQueue.IsCreated) _defenderDeathQueue.Dispose();
            if (_unitAttackVisualQueue.IsCreated) _unitAttackVisualQueue.Dispose();
            if (_projectileHitEventQueue.IsCreated) _projectileHitEventQueue.Dispose();
            if (_aggroHitEventQueue.IsCreated) _aggroHitEventQueue.Dispose();
            if (_threatHitEventQueue.IsCreated) _threatHitEventQueue.Dispose();
            if (_blinkRequestQueue.IsCreated) _blinkRequestQueue.Dispose();
            if (_healAppliedEventQueue.IsCreated) _healAppliedEventQueue.Dispose();
            if (_damageNumberEventQueue.IsCreated) _damageNumberEventQueue.Dispose();
            if (_enemyKilledEventQueue.IsCreated) _enemyKilledEventQueue.Dispose();
            if (_enemyCcQueue.IsCreated) _enemyCcQueue.Dispose();
            if (_ccClearQueue.IsCreated) _ccClearQueue.Dispose();
            if (_statModifierQueue.IsCreated) _statModifierQueue.Dispose();
            if (_stackModifierQueue.IsCreated) _stackModifierQueue.Dispose();
            if (_attackOutputLogQueue.IsCreated) _attackOutputLogQueue.Dispose();
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
                _aggroedQueryCreated = false;
                _ccEffectQueryCreated = false;
                _projectileSpawnRequestQueryCreated = false;
                _projectileQueryCreated = false;
                return;
            }

            if (_aliveAttackersQueryCreated)
            {
                _aliveAttackersQuery.Dispose();
                _aliveAttackersQueryCreated = false;
            }
            if (_aggroedQueryCreated)
            {
                _aggroedQuery.Dispose();
                _aggroedQueryCreated = false;
            }
            if (_ccEffectQueryCreated)
            {
                _ccEffectQuery.Dispose();
                _ccEffectQueryCreated = false;
            }
            if (_modifierSlotQueryCreated)
            {
                _modifierSlotQuery.Dispose();
                _modifierSlotQueryCreated = false;
            }
            if (_pickupViewQueryCreated)
            {
                _pickupViewQuery.Dispose();
                _pickupViewQueryCreated = false;
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

            // map-origin-placement: _boardOrigin 은 BuildMapForBattle 이 설정한다 (Tilemap = zero 고정).

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

                // boss-defender-field unit 1 — 방어유닛-지향 필드 싱글톤. walkMask 는 위의
                // Temp `walk` 를 Persistent 로 복사(goal field 는 저장 안 하는 값).
                // flow/dist 는 초기 "소스 0" 상태(dist=MaxValue) — 내용은 DefenderFieldSystem 이
                // 매 프레임 재빌드. teardown 은 TeardownFlowField 가 함께 처리(멱등).
                var dWalk = new NativeArray<byte>(n, Allocator.Persistent);
                var dFlow = new NativeArray<float2>(n, Allocator.Persistent);
                var dDist = new NativeArray<int>(n, Allocator.Persistent);
                try
                {
                    dWalk.CopyFrom(walk);
                    for (int i = 0; i < n; i++) dDist[i] = int.MaxValue;

                    _defenderFieldSingleton = _em.CreateEntity();
                    _em.AddComponentData(_defenderFieldSingleton, new Wassup.Battle.Effects.DefenderFieldSingleton
                    {
                        walkMask = dWalk,
                        flow     = dFlow,
                        dist     = dDist,
                        gridSize = _generatedMap.gridSize,
                        tileSize = tileSize,
                        origin   = _boardOrigin,
                    });
                }
                catch
                {
                    if (dWalk.IsCreated) dWalk.Dispose();
                    if (dFlow.IsCreated) dFlow.Dispose();
                    if (dDist.IsCreated) dDist.Dispose();
                    throw;
                }
            }
            finally
            {
                if (walk.IsCreated) walk.Dispose();
            }
        }

        // season-gimmick-overwork unit 4 — 픽업 스폰 후보 셀(Walk∪Place) 싱글턴 구축.
        // FlowFieldSingleton 동형: Persistent NativeArray 소유, TeardownFlowField 가 dispose.
        // gimmick 비활성이면 no-op. 멱등 (재빌드/redraft 시 기존 dispose 후 재생성).
        private void BuildPickupSpawnState()
        {
            TeardownPickupSpawnState();

            if (!_generatedMap.IsCreated || _em == null) return;
            // gimmick-match-integration — 레드불 기믹 배정 시에만 픽업 스폰 후보 구축.
            if (!(_assignedGimmick is Wassup.Data.RedBullGimmickData)) return;

            int2 gridSize = _generatedMap.gridSize;
            int n = gridSize.x * gridSize.y;

            // 이동/배치 타일영역 = Walk∪Place 셀 수집.
            var cells = new System.Collections.Generic.List<int2>(n);
            for (int i = 0; i < n; i++)
            {
                var t = _generatedMap.tiles[i];
                if (t == MapTileType.Walk || t == MapTileType.Place)
                    cells.Add(new int2(i % gridSize.x, i / gridSize.x));
            }
            if (cells.Count == 0) return;

            var candidateCells = new NativeArray<int2>(cells.Count, Allocator.Persistent);
            for (int i = 0; i < cells.Count; i++) candidateCells[i] = cells[i];

            uint pickupSeed = (uint)Wassup.Core.MatchSeed.DerivePickupSeed(_matchSeed);
            _pickupSpawnStateSingleton = _em.CreateEntity();
            _em.AddComponentData(_pickupSpawnStateSingleton, new Wassup.Battle.Effects.PickupSpawnState
            {
                candidateCells = candidateCells,
                elapsed = 0f,
                rng = new Unity.Mathematics.Random(pickupSeed),
            });
            Debug.Log($"[BattleBridge] PickupSpawnState built — 후보 셀 {candidateCells.Length}개 (Walk∪Place), seed={pickupSeed}");
        }

        private void TeardownPickupSpawnState()
        {
            if (_pickupSpawnStateSingleton != Entity.Null && _em != null && _em.Exists(_pickupSpawnStateSingleton))
            {
                if (_em.HasComponent<Wassup.Battle.Effects.PickupSpawnState>(_pickupSpawnStateSingleton))
                {
                    var data = _em.GetComponentData<Wassup.Battle.Effects.PickupSpawnState>(_pickupSpawnStateSingleton);
                    data.Dispose();
                }
                _em.DestroyEntity(_pickupSpawnStateSingleton);
            }
            _pickupSpawnStateSingleton = Entity.Null;
        }

        // enemy-spawn-positioning / tile-movement-integrity u0(rev) — 스폰 셀 flow 수직으로 중앙 기준 이산 N-레인 오프셋 계산.
        private float3 ComputeSpawnLateralOffset(int2 spawnCell)
        {
            if (!spawnSpreadEnabled || spawnSpreadFraction <= 0f) return float3.zero;

            float2 flowDir = float2.zero; // flow 0 → SpawnSpread.Perpendicular 가 (1,0) 기준 폴백.
            if (_flowFieldSingleton != Entity.Null && _em.Exists(_flowFieldSingleton) &&
                _em.HasComponent<Wassup.Battle.Effects.FlowFieldSingleton>(_flowFieldSingleton))
            {
                var field = _em.GetComponentData<Wassup.Battle.Effects.FlowFieldSingleton>(_flowFieldSingleton);
                int idx = Wassup.Battle.Movement.GridMath.CellIndex(spawnCell, field.gridSize);
                if (idx >= 0 && idx < field.flow.Length) flowDir = field.flow[idx];
            }

            // 폭 중앙 기준 대칭 이산 N-레인 분율 (상단은 topScale 로 좁힘). 스폰 순서 round-robin.
            float frac = Wassup.Battle.Movement.SpawnSpread.LaneFraction(
                _spawnSpreadCounter++, spawnSubLaneCount, spawnSpreadFraction, spawnSpreadTopScale);
            return Wassup.Battle.Movement.SpawnSpread.LateralOffset(frac, tileSize, flowDir);
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

            // tilemap-world-surround unit 1 — MapGrid 내부에 장식 Deco 셀을 데이터로 designate (배경 프랍 호스트).
            // theme.keepRatio<1 일 때만. Walk 경로 불변·시드 결정적. 페인트/VisualPlan(아래) 전에 실행해야 반영된다.
            if (mapSource == MapSource.MapGrid && theme != null
                && theme.mapGridBuildableKeepRatio < 1f && _generatedMap.IsCreated)
            {
                var decoRng = Unity.Mathematics.Random.CreateFromIndex((uint)(seed ^ 0x5A5A5A) | 1u);
                ObstaclePlacer.DesignateDeco(ref decoRng, _generatedMap.tiles,
                    _generatedMap.gridSize, theme.mapGridBuildableKeepRatio);
            }

            // tilemap-mode-adoption unit 0 — 유닛 스케일/틸트를 빌드 시 1회 확정 (유닛 스폰 전).
            CharacterVisualScale = tilemapCharacterScale;
            CharacterBillboardTilt = tilemapBillboardTilt;
            // tilted-billboard unit 6 — 배경 프랍 거리 틸트 미러.
            // refElev(기준 elevation)은 PropBillboard 가 라이브 카메라 pitch 에서 도출 — 페이즈별 카메라 변화 자기보정.
            PropDistanceTiltFactor = propDistanceTiltFactor;
            PropDistanceTiltMin = propDistanceTiltMin;
            PropDistanceTiltMax = propDistanceTiltMax;
            // tilted-billboard unit 3 — 블롭 그림자 데이터 미러(스폰 시 view 가 읽는다).
            BlobShadowSprite = blobShadowSprite;
            BlobShadowSize = blobShadowSize;
            BlobShadowColor = blobShadowColor;
            BlobShadowGroundY = blobShadowGroundY;
            // 모바일은 shadowmap 비용 회피 위해 강제 블롭. 데스크톱/에디터는 serialized 값.
            UseRealShadows = useRealShadows && !Application.isMobilePlatform;
            // enemy-walk-anim-speed unit 0 — 걷기 애니 속도 변조 미러. SO 미할당 시 비활성(배율 1.0).
            WalkAnimSpeedEnabled = walkAnimSpeedStyle != null;
            if (WalkAnimSpeedEnabled)
            {
                WalkAnimRefSpeed = walkAnimSpeedStyle.referenceSpeed;
                WalkAnimMinTimeScale = walkAnimSpeedStyle.minTimeScale;
                WalkAnimMaxTimeScale = walkAnimSpeedStyle.maxTimeScale;
                WalkAnimSmoothing = walkAnimSpeedStyle.smoothing;
                WalkAnimTeleportGuard = walkAnimSpeedStyle.teleportGuard;
            }
            ApplyEnvironmentGating(); // 비-타일맵 환경 오브젝트 숨김 (빈 목록이면 no-op)

            // view-init 는 view 부재 시 조용히 skip — headless(EditMode 테스트) sim 빌드 계약.
            // 실제 씬의 오배선 감지는 Awake 의 null 체크가 담당한다 (Awake 는 EditMode 테스트에서 안 불림).
            if (tilemapMapView != null)
                // 테마-구동 tileSet: theme 이 지정하면 그걸, 아니면 scene 의 tileSet 폴백 (desert-theme).
                tilemapMapView.Initialize(_generatedMap, tileSize,
                    theme != null && theme.tileSet != null ? theme.tileSet : tileSet,
                    boardViewMode, UseRealShadows);
            // sim origin 은 무조건 zero (README 계약).
            _boardOrigin = float3.zero;
            if (placementInput != null) placementInput.Initialize(_generatedMap, tileSize);

            // sim↔view 변환의 단일 지점 — BuildFlowField 직전 1회 설정. grid 없으면(headless) skip —
            // BoardSpace 는 view 계층 전용이라 sim 빌드에 불필요하고, null 전달은 Configure 가 에러로 거부한다.
            if (tilemapMapView != null && tilemapMapView.Grid != null)
                Wassup.Core.BoardSpace.Configure(boardViewMode, BoardOrigin, tileSize, tilemapMapView.Grid);

            // tilted-billboard — 런타임 카메라 자동 조정 비활성. 씬에 수동 배치한 카메라를 그대로 사용한다.
            // (퍼스펙티브 전환 튜닝 중: 카메라 pos/rot/fov 를 씬에서 직접 잡고 덮어쓰지 않도록 주석 처리)
            // camera-direction unit 0 이후 재활성 금지: 카메라 포즈는 CameraDirector 가 매 프레임
            // 절대값으로 소유한다 — 이 호출을 되살려도 다음 LateUpdate 에 홈 포즈로 되돌려져 무효.
            // 페이즈별 카메라가 필요하면 CameraDirector 의 페이즈 포즈 델타(spec unit 1)로 구현한다.
            // ApplyTilemapCameraPreset(); // 매 빌드 idempotent 재적용

            BuildFlowField();
            // season-gimmick-overwork unit 4 — 픽업 스폰 후보 셀(Walk∪Place)은 goal field 와
            // 같은 맵-빌드 시점에 구축. gimmick 비활성이면 no-op.
            BuildPickupSpawnState();
            // gimmick-match-integration — 기믹 config 주입도 맵-빌드 시점(배정된 _assignedGimmick
            // 확정 이후)에 함께. guarded EnsureQueriesAndQueues 로는 배정 전에 1회 돌아 누락됐었다.
            CreateGimmickConfigIfActive();

            // enemy-tile-movement-integrity unit 0 — 스폰 분산 순번 리셋(결정론 수열은 시드 불필요).
            _spawnSpreadCounter = 0;

            // props — grid 권위 배경 프랍(Deco 셀; unit 1 designate 후 존재).
            // tilemap-world-surround unit 2: MapGrid 라도 내부 Deco 가 생기면 prop placer 가 채운다.
            if (theme != null && theme.playAreaProps != null && theme.playAreaProps.Length > 0)
            {
                if (tilemapMapView != null)
                {
                    // unit 5 — 모바일 프랍 예산: 배경 프랍을 배율만큼 솎는다(앞쪽=중앙/가장자리 우선 보존, 필러 컷).
                    float propScale = Application.isMobilePlatform ? Mathf.Clamp01(mobilePropBudgetScale) : 1f;
                    var plan = tilemapMapView.VisualPlan;
                    // unit 10 — 빌보드 틸트가 있어 occlusion 인지 배치(큰 프랍이 플레이 +y 가림 방지).
                    var placements = BackgroundPropPlacer.Generate(plan, theme, _generatedMap.seed, occlusionAware: true);
                    if (propScale < 1f && placements.Count > 0)
                        placements = placements.GetRange(0, Mathf.Max(0, (int)(placements.Count * propScale)));
                    tilemapMapView.InstantiateBackgroundProps(plan, theme, placements);
                }
            }

            // 원경 — 외곽 터레인 링 위 저밀도 프랍. 그림자 OFF(원경). 모바일은 밀도 배율로 솎음.
            if (tilemapMapView != null && theme != null)
            {
                float ringScale = Application.isMobilePlatform ? Mathf.Clamp01(mobilePropBudgetScale) : 1f;
                tilemapMapView.InstantiateRingProps(theme, _generatedMap.gridSize, _generatedMap.seed, ringScale);
            }

            // prop-placement-layer unit 1 — goal/spawn 3D 구조물 프랍. playAreaProps 와 독립 가드.
            if (tilemapMapView != null && theme != null)
            {
                tilemapMapView.InstantiateStructureProps(_generatedMap, theme, tilemapMapView.VisualPlan);
            }

            // effect-tiles unit 1 — Place 셀 seed 결정론 효과 타일. 페인트는 Initialize(Clear) 이후 계약.
            // dict clear 는 가드 밖 — 이전 빌드 잔존 제거(테마가 효과 타일 없어도).
            _effectTilesByCell.Clear();
            if (tilemapMapView != null && theme != null &&
                theme.effectTiles != null && theme.effectTiles.Length > 0 && theme.effectTileCount > 0)
            {
                tilemapMapView.SetEffectTileMaterial(theme.effectTileMaterial); // 펄스 발광 머티리얼(있으면)
                var effectCells = Wassup.Data.EffectTilePlacer.SelectCells(
                    _generatedMap, _generatedMap.seed, theme.effectTileCount);
                // unit 4 — 종류 배정을 seed rng per-cell 로 (round-robin 은 종류 수 > count 면 뒤 종류가 영영 안 나옴).
                var kindRng = Unity.Mathematics.Random.CreateFromIndex(
                    (uint)(_generatedMap.seed ^ 0x7EFFEC7) | 1u);
                for (int i = 0; i < effectCells.Count; i++)
                {
                    var data = theme.effectTiles[kindRng.NextInt(0, theme.effectTiles.Length)];
                    AddEffectTile(new Vector2Int(effectCells[i].x, effectCells[i].y), data);
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
                _defenderFieldSingleton = Entity.Null;
                _pickupSpawnStateSingleton = Entity.Null;
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

            // boss-defender-field unit 1 — defender field 는 goal field 와 라이프사이클 공유.
            if (_defenderFieldSingleton != Entity.Null && _em != null && _em.Exists(_defenderFieldSingleton))
            {
                if (_em.HasComponent<Wassup.Battle.Effects.DefenderFieldSingleton>(_defenderFieldSingleton))
                {
                    var data = _em.GetComponentData<Wassup.Battle.Effects.DefenderFieldSingleton>(_defenderFieldSingleton);
                    data.Dispose();
                }
                _em.DestroyEntity(_defenderFieldSingleton);
            }
            _defenderFieldSingleton = Entity.Null;

            // season-gimmick-overwork unit 4 — 픽업 스폰 상태도 맵 field 와 동일 lifecycle.
            TeardownPickupSpawnState();
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
            tileHealthGaugeLayer?.Clear(); // unit 3 — 게이지 정리를 _defenderByTile 리셋과 co-locate(불변식)
            // ingame-dreamcatcher Unit 2/3 — reset card registry + triggers for a new match.
            _activeDcEffects.Clear();
            _activePlacementSleeps.Clear(); // combat-action-lock — 매치별 placement-aura Sleep 등록 초기화
            _bountyMarked.Clear(); // 살찌운 제물 — 표식 등록부도 매치 경계에서 초기화
            _dcStackCounter = 100;
            _dcInstanceCounter = 0; // dreamcatcher-unit-trigger Unit 1 — per-match instance ids
            // dreamstone-loadout Unit 3 — set-then-apply: reapply the pending stone
            // loadout right after the clear above (single point, see SetDreamstones).
            ApplyPendingDreamstones();
            _onPlaceTriggeredEntities.Clear();
            _synergyActivatedEntities.Clear();
            _synergyActivations = 0;
            _synergyPeakCount = 0;
            _goalReachedCount = 0;
            _leakAllowancePenalty = 0; // 몽마의 계약 선불 — 매치 경계에서 소멸(이월 금지)
            _running = false;
            _placementAllowed = true;
            _resultShown = false;
            if (skillRuntime != null) skillRuntime.ResetAll();
            // time-manager — 시간 스케일 요청도 매치 경계에서 초기화(앱 수명 싱글턴이라 매치 간
            // 잔존 방지; 고아 lease 안전망). skillRuntime.ResetAll 과 동일 지점(시작·종료 양쪽).
            TimeManager.Instance.ResetAll();
            _usingGeneratedWaves = false;
            _usingAuthoredPlan = false;
            _wavePlan = default;
            _nextWaveIndex = 0;

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
            _battleClock = 0.0;
            // wave-authoring-test-mode unit 2 — 작성 모드는 plan.timerDurationSec(0=endless).
            // seed/legacy 경로는 deck.timerDurationSec 그대로(무변경).
            _timerDuration = _usingAuthoredPlan ? _wavePlan.timerDurationSec : deck.timerDurationSec;
            _running = true;
            if (_usingGeneratedWaves)
                QueueDueWaves(0f);
            if (_usingAuthoredPlan)
                Debug.Log($"[BattleBridge] Battle started with AUTHORED plan '{_authoredPlan.displayName}' waves={_wavePlan.waves.Count} endless={(_timerDuration <= 0f)}.");
            else
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
            if (!_aggroedQueryCreated)
            {
                _aggroedQuery = _em.CreateEntityQuery(ComponentType.ReadOnly<Wassup.Battle.Effects.Aggroed>());
                _aggroedQueryCreated = true;
            }
            if (!_ccEffectQueryCreated)
            {
                _ccEffectQuery = _em.CreateEntityQuery(ComponentType.ReadOnly<CcEffect>());
                _ccEffectQueryCreated = true;
            }
            if (!_modifierSlotQueryCreated)
            {
                // 드림캐쳐 강화는 방어유닛에만 부여되므로 defender 로 한정 — 적/기타 아키타입 순회 낭비 방지
                // (ecs-review H2). ⚠ subconscious-curse-expansion unit 2 부터 적도 드림캐쳐
                // origin 슬롯을 가질 수 있다(살찌운 제물 DmgTakenMul) — 이 DefenderUnitTag
                // 게이트가 적 오라 점등을 막는 유일한 장벽이므로 제거 금지.
                _modifierSlotQuery = _em.CreateEntityQuery(
                    ComponentType.ReadOnly<Wassup.Battle.Effects.StatModifierSlot>(),
                    ComponentType.ReadOnly<Wassup.Battle.Units.DefenderUnitTag>());
                _modifierSlotQueryCreated = true;
            }

            if (!_projectileSpawnRequestQueryCreated)
            {
                _projectileSpawnRequestQuery = _em.CreateEntityQuery(ComponentType.ReadOnly<ProjectileSpawnRequest>());
                _projectileSpawnRequestQueryCreated = true;
            }
            // season-gimmick-overwork unit 6 — 픽업 뷰 조정용.
            if (!_pickupViewQueryCreated)
            {
                _pickupViewQuery = _em.CreateEntityQuery(ComponentType.ReadOnly<Wassup.Battle.Effects.Pickup>());
                _pickupViewQueryCreated = true;
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

            // aggro-targeting Unit 11 — Combat→Effects 히트 채널. AttackSystem(Combat)이
            // 가디언 명중을 enqueue, AggroStateSystem(Effects)이 드레인해 Aggroed 부착.
            // 브리지는 lifecycle 만 관리(드레인 안 함 — 순수 ECS 내부 통신).
            if (_aggroHitEventQueue.IsCreated) _aggroHitEventQueue.Dispose();
            _aggroHitEventQueue = new NativeQueue<Wassup.Battle.Effects.AggroHitEvent>(Allocator.Persistent);
            var aggroHitSingleton = _em.CreateEntity();
            _em.AddComponentData(aggroHitSingleton, new Wassup.Battle.Effects.AggroHitEventsSingleton { queue = _aggroHitEventQueue });

            // nightmare-catcher unit 1 — Combat→Combat 보스 위협 귀속 채널. 데미지
            // 생산자(AttackSystem 근접 / ProjectileHitSystem 착탄)가 보스(ThreatEntry
            // 버퍼 보유) 피격을 enqueue, Combat 드레인이 ThreatTable 에 누적(unit 3).
            // 브리지는 lifecycle 만 관리(드레인 안 함 — 순수 ECS 내부 통신).
            if (_threatHitEventQueue.IsCreated) _threatHitEventQueue.Dispose();
            _threatHitEventQueue = new NativeQueue<Wassup.Battle.Combat.ThreatHitEvent>(Allocator.Persistent);
            var threatHitSingleton = _em.CreateEntity();
            _em.AddComponentData(threatHitSingleton, new Wassup.Battle.Combat.ThreatHitEventsSingleton { queue = _threatHitEventQueue });

            // nightmare-catcher unit 3 — Combat→Movement 텔레포트 seam.
            // HealthThresholdSystem(Combat) enqueue → BlinkApplySystem(Movement)
            // 소비·위치 대입. 브리지는 lifecycle 만 관리.
            if (_blinkRequestQueue.IsCreated) _blinkRequestQueue.Dispose();
            _blinkRequestQueue = new NativeQueue<Wassup.Battle.Movement.BlinkRequestEvent>(Allocator.Persistent);
            var blinkRequestSingleton = _em.CreateEntity();
            _em.AddComponentData(blinkRequestSingleton, new Wassup.Battle.Movement.BlinkRequestEventsSingleton { queue = _blinkRequestQueue });

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

            // combat-action-lock unit 3 — wake-on-hit clear channel(16th). CcClearSystem 이
            // drain 해 피격 유닛의 Sleep 을 제거(Units→Effects 단방향).
            if (_ccClearQueue.IsCreated) _ccClearQueue.Dispose();
            _ccClearQueue = new NativeQueue<Wassup.Battle.Effects.CcClearRequest>(Allocator.Persistent);
            var ccClearSingleton = _em.CreateEntity();
            _em.AddComponentData(ccClearSingleton, new Wassup.Battle.Effects.CcClearRequestsSingleton { queue = _ccClearQueue });

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

            // gimmick-match-integration — 기믹 config 주입은 여기(guarded EnsureQueriesAndQueues)가
            // 아니라 BuildMapForBattle 로 이동했다. 이 메서드는 _ecsInfrastructureReady 가드로 매치당
            // 1회만 도는데, 그 1회가 GameManager 의 _assignedGimmick 세팅보다 먼저라 config 가 조용히
            // 누락됐다(픽업 0개 버그). BuildMapForBattle 은 매 맵빌드마다(배정 이후) 돌아 안전.

            // draft-stage-map-prebuild Unit 0 — BuildMapForBattle removed from here; called explicitly
            // by PrepareDraftMap / RebuildDraftMap / BeginPlacement fallback.
            _ecsInfrastructureReady = true;
        }

        public void StopBattle()
        {
            // dreamstone-loadout Unit 3 — reset symmetry: pending loadout must not outlive the match (review M2).
            _pendingDreamstones = null;
            // time-manager Unit 3 — 시간 상태도 매치와 함께 리셋.
            _battleClock = 0.0;
            _battleTimeScaleEntity = Entity.Null;
            // range-preview unit 3 — 매치 종료 시 격자 표시 무조건 해제(비행 중
            // 종료로 impact drain 이 못 지운 텔레그래프 잔상 방지).
            _rangeOwner = RangeDisplayOwner.None;
            _skillTelegraphProjectile = Entity.Null;
            if (tilemapMapView != null) tilemapMapView.ClearPlacementRange();

            if (HasLiveEntityManager())
            {
                TeardownCurrentBattle();
                return;
            }

            _running = false;
            _placementAllowed = false;
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
            if (_em != null)
            {
                DestroyEntitiesByType<Wassup.Battle.Effects.Hazard>();
                DestroyEntitiesByType<Wassup.Battle.Effects.BlockingHazard>();
                DestroyEntitiesByType<Wassup.Battle.Effects.Obstacle>();
                // season-gimmick-overwork unit 4 — redraft 시 잔존 픽업 정리.
                DestroyEntitiesByType<Wassup.Battle.Effects.Pickup>();
            }

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

        // wave-authoring-test-mode unit 2 — 테스트 모드: seed 생성 대신 작성 플랜 사용.
        // null 을 주면 seed 경로로 복귀. StartBattle 의 TryInitializeGeneratedWaves 가 소비.
        public void SetAuthoredWavePlan(WavePlanAsset plan)
        {
            _authoredPlan = plan;
        }

        private bool TryInitializeGeneratedWaves()
        {
            _wavePlan = default;
            _nextWaveIndex = 0;
            _usingAuthoredPlan = false;

            // 작성 플랜 우선. 변환 실패 시 아래 seed 경로로 fall-through.
            if (_authoredPlan != null)
            {
                try
                {
                    _wavePlan = WavePatternGenerator.FromPlanAsset(_authoredPlan);
                    GameManager.Instance?.Logger?.SetWavePattern(_wavePlan);
                    if (_wavePlan.waves != null && _wavePlan.waves.Count > 0)
                    {
                        _usingAuthoredPlan = true;
                        return true;
                    }
                    Debug.LogWarning($"[BattleBridge] Authored plan '{_authoredPlan.name}' has no waves; falling back to seed/legacy.", this);
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[BattleBridge] Authored plan '{_authoredPlan.name}' failed; falling back. {ex.Message}", this);
                }
                _wavePlan = default;
                _nextWaveIndex = 0;
            }

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
        }

        // Read-only wave-progress state for the UI (NextWaveDock polls these). The dock
        // owns the button/label chrome; BattleBridge (ECS gateway) no longer builds UI.
        public bool NextWaveAvailable => _running && _usingGeneratedWaves && _wavePlan.waves != null;
        public bool NextWaveHasNext => NextWaveAvailable && _nextWaveIndex < _wavePlan.waves.Count;
        public int NextWaveNumber => _nextWaveIndex + 1;

        public void ForceNextWave()
        {
            if (!_running || !_usingGeneratedWaves || _wavePlan.waves == null) return;
            if (_nextWaveIndex >= _wavePlan.waves.Count)
                return;

            // time-manager Unit 3 — 강제 웨이브의 triggerTimeSec 기준도 Battle 클럭이어야 한다.
            // Update 의 스폰 게이트가 _battleClock 을 쓰므로 실시간을 쓰면 정지/슬로우모 시 갈라진다.
            float elapsedSec = (float)_battleClock;
            var wave = _wavePlan.waves[_nextWaveIndex];
            GameManager.Instance?.Logger?.RecordWaveEvent("wave_forced", wave.waveIndex, elapsedSec, true);
            QueueWave(wave, elapsedSec, true, elapsedSec);
            _nextWaveIndex++;
        }

        private void QueueWave(GeneratedWave wave, float baseTriggerTimeSec, bool forced, float elapsedSec)
        {
            int laneCount = _generatedMap.IsCreated ? _generatedMap.spawns.Length : 1;
            var entries = WavePatternGenerator.ExpandWave(wave, baseTriggerTimeSec, laneCount, _wavePlan.intraWaveSpacingSec);
            int baseDeckIndex = wave.waveIndex * 1000;
            for (int i = 0; i < entries.Count; i++)
                _pending.Add(new PendingSpawnEntry { entry = entries[i], deckIndex = baseDeckIndex + i });

            GameManager.Instance?.Logger?.RecordWaveEvent("wave_started", wave.waveIndex, elapsedSec, forced);
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
        // map-origin-placement: 입력(레이캐스트 평면)이 board 원점을 읽는 단일 창구.
        public Vector3 BoardOrigin => new Vector3(_boardOrigin.x, _boardOrigin.y, _boardOrigin.z);
        public PlacementInput PlacementInput => placementInput;

        private SkillData[] _skillLoadout;
        public SkillData[] SkillLoadout => _skillLoadout;

        public void SetSkillLoadout(SkillData[] loadout)
        {
            _skillLoadout = loadout;
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
        // tiles (entry/exit) in one call; the caller (DreamcatcherCardDragSlot's
        // Portal two-tap) captures both taps before invoking. Returns false if
        // the skill is not a Portal or the cooldown gate rejects.
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
                    EnqueueDamageMul(entity, skill.magnitude, skill.durationSec, Wassup.Battle.Effects.ModifierOrigin.Skill);
                    affectedCount = 1;
                    break;
                case SkillEffectType.RapidFire:
                    EnqueueAttackSpeedMul(entity, skill.magnitude, skill.durationSec, Wassup.Battle.Effects.ModifierOrigin.Skill);
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

        // placement-cell-snap unit 1 — 히스테리시스 정책(PlacementCellSnap)이 소비할 소수 셀 좌표(unclamped).
        // GridMath.WorldToCell = floor(이 값 + 0.5) 와 같은 공간(셀 중심=정수, 경계=±0.5) → 커밋 셀과 드리프트 없음.
        public Vector2 DebugWorldToCellFractional(Vector3 worldPosition)
        {
            float ts = tileSize > 0f ? tileSize : 1f;
            return new Vector2(
                (worldPosition.x - _boardOrigin.x) / ts,
                (worldPosition.z - _boardOrigin.z) / ts);
        }

        // placement-cell-snap unit 1 — DebugWorldToCell 이 clamp 에 쓰는 grid 크기(정책의 결과 clamp 용).
        public Vector2Int DebugGridSize
        {
            get { int2 g = _generatedMap.IsCreated ? _generatedMap.gridSize : GridSize; return new Vector2Int(g.x, g.y); }
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
                EnqueueMoveSpeedMul(e, skill.magnitude, skill.durationSec, Wassup.Battle.Effects.ModifierOrigin.Skill);
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

        // projectile-trajectory-payload unit 7 — Meteor rides the unified projectile
        // lifecycle (SkyFall × TileAoe, flightTime = warningSec). The request is
        // built HERE (not EffectSpawner): the ProjectileData registry is bridge-
        // private and ProjectileSpawnRequest is Combat-owned, so the bridge is the
        // only seam that can emit it without a context-boundary violation. No ECS
        // carrier entity — SpawnProjectile is called directly (legacy meteor-carrier
        // path removed in unit 8).
        private int ApplyMeteor(Vector2Int tile, SkillData skill)
        {
            float3 centerWorld = GridToWorldCenter(tile);
            int tileRange = GridMath.RangeToTiles(skill.range);
            float warn = skill.warningSec > 0f ? skill.warningSec : 0f;
            if (skill.projectile == null)
            {
                // Config error, visibly dropped — GetOrCreateProjectileDataIndex
                // would NRE on null and a silent fallback would hide the miswiring.
                Debug.LogWarning($"[BattleBridge] Skill '{skill.id}' has no ProjectileData assigned; meteor cast dropped.");
                return 0;
            }
            var req = new ProjectileSpawnRequest
            {
                movement = MovementKind.SkyFall,
                payload = PayloadKind.TileAoe,
                origin = centerWorld,
                impact = centerWorld,
                damage = skill.magnitude,
                visualScale = skill.projectile.visualScale,
                dataIndex = GetOrCreateProjectileDataIndex(skill.projectile),
                impactTileRange = tileRange,
                flightTime = warn,
                // unit 9 — SkyFall 은 arcHeight 슬롯을 낙하 시작 높이로 재사용
                // (신규 state/request 필드 0). 뷰가 (1-t)·dropHeight 를 view-Y 에 더한다.
                arcHeight = skill.projectile.dropHeight,
            };
            // range-preview unit 3 / unit 9 — 착탄 예고는 격자 고정 표시. 해제는
            // "이 투사체의 착탄 이벤트"를 source 엔티티로 정확 판별(hit VFX 유무 무관).
            _skillTelegraphProjectile = SpawnProjectile(req, Entity.Null);
            PinSkillTelegraph(tile, tileRange);
            // Actual damage resolves async (ProjectileHitSystem TileAoe arm at
            // flightTime); at cast time we conservatively pre-count current
            // overlaps so the log is informative without waiting for the burst.
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

        private void Update()
        {
            // time-manager Unit 3 — 매 프레임 Battle 스케일을 ECS 로 흘린다(placement 슬로우모 포함).
            PushBattleTimeScaleToEcs();

            // placement-enemy-see-through unit 3 — 적 dim 알파 페이드. unscaled 라 드래그 슬로우모와 무관.
            // _running 이전에 둬서 페이즈 무관하게 항상 원복/페이드가 진행되게 한다.
            float dimTarget = _enemyDimActive ? Mathf.Clamp01(enemyDragDimAlpha) : 1f;
            _enemyDimAlpha = Mathf.MoveTowards(_enemyDimAlpha, dimTarget,
                enemyDragDimFadeSpeed * UnityEngine.Time.unscaledDeltaTime);

            if (!_running) return;

            // 웨이브/스폰/타이머는 실시간이 아니라 Battle-스케일 클럭을 따른다(정지·슬로우모 반영).
            _battleClock += TimeManager.Instance.DeltaTime(TimeDomain.Battle);
            float t = (float)_battleClock;
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
            ReconcileStatusFx();
            ReconcilePickupViews();
            if (_em != null) _dcAuraPool?.Sync(_em); // 드림캐쳐 부착 오라 — 뷰 좌표 갱신 뒤 추종
            if (_em != null) _projectileViewPool?.SyncTransforms(_em);
        }

        // unit-status-fx Unit 2 — 상태 연출 상태 구동 reconcile. 상태별 ECS 소스로 활성
        // 유닛을 찾아 (유닛, kind) 연출 Ensure, 프레임 끝에 해제된 것 회수. 뷰 좌표는
        // SyncMonoUnitViews 가 이미 갱신했으므로 그 뒤에 호출. 새 상태 추가 = 아래에
        // 쿼리 + Ensure 몇 줄(예: Stun 컴포넌트 쿼리 → Ensure(e, StatusFxKind.Stun, anchor)).
        private void ReconcileStatusFx()
        {
            if (statusFxSpawner == null || _em == null) return;
            statusFxSpawner.BeginFrame();

            // Aggro: Aggroed 보유 적.
            if (_aggroedQueryCreated)
            {
                var aggroed = _aggroedQuery.ToEntityArray(Allocator.Temp);
                try
                {
                    for (int i = 0; i < aggroed.Length; i++)
                    {
                        var anchor = ResolveUnitViewTransform(aggroed[i]);
                        if (anchor != null)
                            statusFxSpawner.Ensure(aggroed[i], Wassup.Data.StatusFxKind.Aggro, anchor);
                    }
                }
                finally
                {
                    aggroed.Dispose();
                }
            }

            // Sleep: CcEffect 버퍼에 Sleep(remainingTime>0) 보유 유닛 — 적·아군 공통
            // (combat-action-lock; defender 는 spine 또는 폴백 뷰 브랜치로 앵커 해석됨).
            if (_ccEffectQueryCreated)
            {
                var ccEntities = _ccEffectQuery.ToEntityArray(Allocator.Temp);
                try
                {
                    for (int i = 0; i < ccEntities.Length; i++)
                    {
                        var buf = _em.GetBuffer<CcEffect>(ccEntities[i], isReadOnly: true);
                        bool asleep = false;
                        for (int j = 0; j < buf.Length; j++)
                            if (buf[j].kind == CcKind.Sleep && buf[j].remainingTime > 0f) { asleep = true; break; }
                        if (!asleep) continue;
                        var anchor = ResolveUnitViewTransform(ccEntities[i]);
                        if (anchor != null)
                            statusFxSpawner.Ensure(ccEntities[i], Wassup.Data.StatusFxKind.Sleep, anchor);
                    }
                }
                finally
                {
                    ccEntities.Dispose();
                }
            }

            // 스탯 모디파이어 슬롯 기반 오라 두 종을 같은 버퍼 스캔에서 판정(중복 순회 회피):
            //   Empowered = 드림캐쳐 출처(ModifierOrigin.Dreamcatcher) 활성 — dreamcatcher-empower-aura.
            //     revoke(mult=1.0 중립화)면 net=identity 라 자동 해제(net-편차 판정). Dreamstone/시너지 등 제외.
            //   Burnout   = 번아웃 출처(ModifierOrigin.Burnout) 활성 — Fatigue 임계 파생 디버프.
            //     StackModifierTickSystem 이 kind==Fatigue 파생에만 Burnout origin 을 심는다 →
            //     다른 Stack 파생(Fire/Ice/…)이 생겨도 번아웃 아이콘과 안 섞임(review #3).
            //   LastRun   = LastRun 컴포넌트 보유 = 라스트런 창(레드불 소비~crash). 컴포넌트가 창을
            //     권위적으로 정의하므로 origin 추론 대신 직접 조회(review #3).
            //   Burnout 모디파이어는 duration 만료로 제거(in-place revoke 없음)라 존재 판정(remaining>0)
            //     으로 충분. 버퍼는 읽기만.
            if (_modifierSlotQueryCreated)
            {
                var slotEntities = _modifierSlotQuery.ToEntityArray(Allocator.Temp);
                try
                {
                    for (int i = 0; i < slotEntities.Length; i++)
                    {
                        var e = slotEntities[i];
                        var slots = _em.GetBuffer<Wassup.Battle.Effects.StatModifierSlot>(e, isReadOnly: true);
                        bool empowered = Wassup.Battle.Effects.ModifierAuraClassifier.HasActiveDreamcatcherModifier(slots.AsNativeArray());
                        bool burnout = false;
                        for (int j = 0; j < slots.Length; j++)
                        {
                            if (slots[j].header.remaining <= 0f) continue;
                            if (slots[j].header.origin == Wassup.Battle.Effects.ModifierOrigin.Burnout) { burnout = true; break; }
                        }
                        bool lastRun = _em.HasComponent<Wassup.Battle.Effects.LastRun>(e);
                        if (!empowered && !burnout && !lastRun) continue;
                        var anchor = ResolveUnitViewTransform(e);
                        if (anchor == null) continue;
                        if (empowered) statusFxSpawner.Ensure(e, Wassup.Data.StatusFxKind.Empowered, anchor);
                        if (burnout) statusFxSpawner.Ensure(e, Wassup.Data.StatusFxKind.Burnout, anchor);
                        if (lastRun) statusFxSpawner.Ensure(e, Wassup.Data.StatusFxKind.LastRun, anchor);
                    }
                }
                finally
                {
                    slotEntities.Dispose();
                }
            }

            // subconscious-curse-expansion unit 3 — 살찌운 제물 표식. 소스 = bridge 표식
            // 등록부(_bountyMarked): 처치/유출 드레인이 제거하므로 잔존 키 = 활성 표식
            // (ECS 쿼리 불요 — 등록부가 이미 권위. Exists 가드는 파괴~드레인 사이 1프레임 창).
            foreach (var marked in _bountyMarked)
            {
                if (!_em.Exists(marked)) continue;
                var anchor = ResolveUnitViewTransform(marked);
                if (anchor != null)
                    statusFxSpawner.Ensure(marked, Wassup.Data.StatusFxKind.Marked, anchor);
            }

            statusFxSpawner.EndFrame();
        }

        // season-gimmick-overwork unit 6 — 픽업 엔티티↔뷰 poll-reconcile. Pickup 은 순수 ECS
        // 스폰이라 이벤트가 없어 매 프레임 조정: 새 엔티티엔 뷰 생성, 사라진 엔티티(소비/만료) 뷰 파괴.
        // _running 무관(placement 중에도 스폰됨). 셀 월드중심에 배치, idle 연출은 PickupPresenter.
        private void ReconcilePickupViews()
        {
            if (_em == null || !_pickupViewQueryCreated) return;

            // 살아있는 픽업 수집.
            var entities = _pickupViewQuery.ToEntityArray(Allocator.Temp);
            try
            {
                // 신규 엔티티 → 뷰 생성.
                for (int i = 0; i < entities.Length; i++)
                {
                    var e = entities[i];
                    if (_pickupVisualMap.ContainsKey(e)) continue;
                    var pickup = _em.GetComponentData<Wassup.Battle.Effects.Pickup>(e);
                    // sim(셀 중심) → view 변환. 다른 모든 뷰와 동일하게 BoardSpace.ToView 경유 —
                    // ToView 가 +0.5 로 Tilemap 셀 중심을 맞춘다(생략 시 반 타일 어긋나 모서리에 놓임).
                    // 시각 hover 는 view 세로(world-up)로만 — ToView 는 sim 높이를 무시한다.
                    float3 simCenter = GridToWorldCenter(new Vector2Int(pickup.cell.x, pickup.cell.y));
                    Vector3 pos = (Vector3)Wassup.Core.BoardSpace.ToView(simCenter) + Vector3.up * pickupViewHeight;
                    var go = new GameObject($"Pickup_{pickup.kind}_{pickup.cell.x}_{pickup.cell.y}");
                    go.transform.SetParent(transform, worldPositionStays: false);
                    go.transform.position = pos;
                    go.AddComponent<Wassup.Battle.Effects.PickupPresenter>()
                        .Init(pickupViewPrefab, pickupModelScale, pickupModelBaseY, pickupOverrideMaterial);
                    _pickupVisualMap[e] = go;
                }

                // 사라진 엔티티 → 뷰 파괴. (_em.Exists 로 판정 — 소비/만료로 DestroyEntity 됨)
                if (_pickupVisualMap.Count > 0)
                {
                    _pickupReapBuffer.Clear();
                    foreach (var kv in _pickupVisualMap)
                        if (!_em.Exists(kv.Key)) _pickupReapBuffer.Add(kv.Key);
                    for (int i = 0; i < _pickupReapBuffer.Count; i++)
                    {
                        var key = _pickupReapBuffer[i];
                        if (_pickupVisualMap.TryGetValue(key, out var go) && go != null)
                            Destroy(go);
                        _pickupVisualMap.Remove(key);
                    }
                }
            }
            finally
            {
                entities.Dispose();
            }
        }

        // season-gimmick-overwork unit 6 — 픽업 뷰 전체 정리 (매치 teardown).
        private void ClearPickupVisuals()
        {
            foreach (var kv in _pickupVisualMap)
                if (kv.Value != null) Destroy(kv.Value);
            _pickupVisualMap.Clear();
        }

        // time-manager Unit 3 — TimeManager.ScaleOf(Battle) 을 ECS singleton 으로 write 해
        // BattleScaledRateManager 가 읽게 한다. _running 무관하게 매 프레임 호출(placement 중
        // 드래그 슬로우모도 반영). ECS 경계: BattleBridge 만 EntityManager 에 접근한다.
        private void PushBattleTimeScaleToEcs()
        {
            if (_world == null || !_world.IsCreated || _em == default) return;
            if (_battleTimeScaleEntity == Entity.Null || !_em.Exists(_battleTimeScaleEntity))
                _battleTimeScaleEntity = _em.CreateEntity(typeof(BattleTimeScale));
            _em.SetComponentData(_battleTimeScaleEntity, new BattleTimeScale
            {
                Value = TimeManager.Instance.ScaleOf(TimeDomain.Battle)
            });
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
                            // unit-health-display unit 1 — 적 저체력 틴트. HP read-only 평가는
                            // BattleBridge 소관(ECS 창구), 뷰는 Color 만 받아 적용.
                            Color tint = EvaluateEnemyHealthTint(entity);
                            // placement-enemy-see-through unit 3 — 적만 dim(디펜더 루프는 미적용).
                            // SetDimmed 를 SetHealthTint 앞에 — quad 는 SetHealthTint 가 알파를 반영한다.
                            bool dimmed = _enemyDimAlpha < 0.999f;
                            if (spineUnitPool != null && spineUnitPool.TryGet(entity, out var spineView))
                            {
                                spineView.UpdatePosition(world);
                                if (canSort) spineView.UpdateSortingOrder(gridSize, tileSize);
                                spineView.SetDimmed(dimmed, _enemyDimAlpha);
                                spineView.SetHealthTint(tint);
                            }
                            else if (enemyViewPool.TryGet(entity, out var view))
                            {
                                view.UpdatePosition(world);
                                if (canSort) view.UpdateSortingOrder(gridSize, tileSize);
                                view.SetDimmed(dimmed, _enemyDimAlpha);
                                view.SetHealthTint(tint);
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
                // unit-health-display unit 3 — 타일 게이지: defender HP read-only → 타일 중심(바닥)
                // view 좌표로 Set. 만피 숨김은 레이어가 처리.
                if (tileHealthGaugeLayer != null && _em.HasComponent<Health>(entity))
                {
                    var dh = _em.GetComponentData<Health>(entity);
                    var tileCenterView = (Vector3)Wassup.Core.BoardSpace.ToView(new Vector3(p.x, 0f, p.z));
                    tileHealthGaugeLayer.Set(kv.Key, tileCenterView, tileSize, Health.ComputeRatio(dh.value, dh.max));
                }
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

        // unit-health-display unit 1 — 적 HP read-only 조회 → HealthDisplayStyle 로 ratio→Color.
        // ECS 경계: HP 는 Units 소유, BattleBridge 는 창구로서 읽기만 한다. SO 미할당 시 무틴트(白).
        private bool _healthTintWarned;
        private Color EvaluateEnemyHealthTint(Entity entity)
        {
            if (healthDisplayStyle == null)
            {
                if (!_healthTintWarned)
                {
                    Debug.LogWarning("[BattleBridge] healthDisplayStyle 미할당 — 적 체력 틴트 스킵.");
                    _healthTintWarned = true;
                }
                return Color.white;
            }
            if (!_em.HasComponent<Health>(entity)) return Color.white;
            var h = _em.GetComponentData<Health>(entity);
            return healthDisplayStyle.EvaluateTint(Health.ComputeRatio(h.value, h.max));
        }

        private void DrainDefenderDeathEvents()
        {
            if (!_defenderDeathQueue.IsCreated) return;
            while (_defenderDeathQueue.TryDequeue(out var evt))
            {
                var cell = new Vector2Int(evt.cell.x, evt.cell.y);
                // dreamcatcher-awakening-hand unit 1 — capture the binding BEFORE
                // removal so DefenderDied can carry the entity (card-recovery key)
                // and the SO (awakeningReward) regardless of the spine pool.
                bool hasBinding = _defenderByTile.TryGetValue(cell, out var binding);
                if (spineUnitPool != null && hasBinding)
                {
                    spineUnitPool.NotifyDeath(binding.entity);
                    defenderFallbackViewPool?.Despawn(binding.entity);
                }
                _defenderByTile.Remove(cell);
                _occupiedTiles.Remove(cell);
                tileHealthGaugeLayer?.Hide(cell); // unit 3 — 사망 시 게이지 제거
                RecomputeSynergyFor(cell);
                Debug.Log($"[BattleBridge] Defender died @ {cell}; tile freed, synergy recomputed.");

                // content-1 ② (작별 선물) — OnDeath×SelfTileAoe explosion at the dead
                // cell. Payload was baked into the event before the entity died, so
                // this touches no destroyed entity. Immediate (flightTime 0) TileAoe.
                if (evt.hasOnDeathAoe && evt.aoeDataIndex >= 0)
                {
                    var impactWorld = GridToWorldCenter(cell, spawnHeight);
                    SpawnProjectile(new ProjectileSpawnRequest
                    {
                        movement = MovementKind.SkyFall,
                        payload = PayloadKind.TileAoe,
                        impact = impactWorld,
                        damage = evt.aoeDamage,
                        impactTileRange = evt.aoeTileRange,
                        flightTime = 0f,
                        dataIndex = evt.aoeDataIndex,
                        visualScale = 1f,
                    }, Entity.Null);
                }

                // dreamcatcher-awakening-hand unit 1 — relay after cleanup so the
                // tile/synergy state is consistent when subscribers run. Entity is
                // already destroyed in ECS; it is passed as a registry KEY only.
                if (hasBinding)
                    DefenderDied?.Invoke(binding.entity, binding.data);
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
                spineUnitPool?.NotifyAttack(evt.attacker, targetWorld, evt.attackAnimPeriod);

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
            // 캐스트 방향을 화면 평면(XY 보드)에 평탄화 — 깊이 Z 제거.
            dir.z = 0f;
            _projectileViewPool.PlayCast(data.castPrefab, anchor, dir, data.castVfxLifetime);
        }

        // camera-direction unit 2 — 구두점 호출용 Director 캐시 (DreamcatcherHandView 패턴).
        // 미배선이면 1회 경고 + 이후 no-op (miss 캐시). 씬 참조 추가 없이 런타임 조회.
        private Wassup.Presentation.CameraDirector _cameraDirector;
        private bool _cameraDirectorMissWarned;

        private Wassup.Presentation.CameraDirector EnsureCameraDirector()
        {
            if (_cameraDirector != null) return _cameraDirector;
            if (_cameraDirectorMissWarned) return null;
            var cam = Camera.main;
            if (cam == null) return null;
            _cameraDirector = cam.GetComponent<Wassup.Presentation.CameraDirector>();
            if (_cameraDirector == null)
            {
                Debug.LogWarning("[BattleBridge] CameraDirector 미배선 — 구두점 연출 생략.", this);
                _cameraDirectorMissWarned = true;
            }
            return _cameraDirector;
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
                // Visual routing: authored hitPrefab wins (GA impact); a prefab-less
                // TileAoe falls back to the legacy procedural burst. radiusWorld
                // travels on the event because the AOE radius is per-cast.
                if (data.hitPrefab != null)
                    _projectileViewPool?.PlayHit(data.hitPrefab, evt.position, data.hitVfxLifetime,
                        data.visualHeightOffset, data.hitVfxScale);
                else if (evt.payload == PayloadKind.TileAoe && evt.radiusWorld > 0f && vfxSpawner != null)
                    vfxSpawner.SpawnMeteorBurst(new Vector3(evt.position.x, 0f, evt.position.z), evt.radiusWorld);

                // camera-direction unit 2 — 헤비(광역) 착탄 구두점: 줌 펄스. 시각 라우팅
                // (hitPrefab 유무)과 무관하게 TileAoe 착탄이면 발동. additive 전용 — 카메라 탈취 없음.
                if (evt.payload == PayloadKind.TileAoe && evt.radiusWorld > 0f)
                    EnsureCameraDirector()?.ZoomPulse();

                // 텔레그래프 해제는 visual 라우팅과 분리 — source 엔티티 정확 판별
                // (unit 9: meteor 에 hitPrefab 이 생겨도, artillery 착탄이 남의
                // 텔레그래프를 지워도 안 되므로 hitPrefab-null 추론을 쓰지 않는다).
                if (evt.source != Entity.Null && evt.source == _skillTelegraphProjectile)
                {
                    ClearSkillTelegraph();
                    _skillTelegraphProjectile = Entity.Null;
                }
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
            bool hasNumbers = damageNumberSpawner != null;
            bool hasBars = enemyHitBarSpawner != null;
            if (!hasNumbers && !hasBars) { _damageNumberEventQueue.Clear(); return; }
            while (_damageNumberEventQueue.TryDequeue(out var evt))
            {
                if (evt.amount <= 0f) continue;
                var simPos = new Vector3(evt.position.x, evt.position.y, evt.position.z);
                if (hasNumbers)
                    damageNumberSpawner.Spawn(simPos, evt.amount);
                if (hasBars)
                {
                    // unit 2 — anchor = 적 뷰 transform(view 좌표). 막타로 뷰가 이미 사라졌으면
                    // ToView(evt.position) 고정 위치로 fallback(M4 계약).
                    Transform anchor = ResolveUnitViewTransform(evt.entity);
                    Vector3 fallbackView = (Vector3)Wassup.Core.BoardSpace.ToView(simPos);
                    enemyHitBarSpawner.Show(evt.entity, anchor, fallbackView, evt.hpRatio);
                }
            }
        }

        // unit-health-display unit 2 — 마이크로바 anchor 해석. Spine → Quad 뷰 순, 없으면 null.
        // unit-status-fx 5 (ecs-review M1/L1) — defender Sleep 앵커로도 쓰이게 되어 이름을
        // Unit 으로 정정하고, spine 미보유 defender 의 폴백 뷰 분기 추가(SyncMonoUnitViews 관례).
        private Transform ResolveUnitViewTransform(Entity entity)
        {
            if (spineUnitPool != null && spineUnitPool.TryGet(entity, out var sv) && sv != null) return sv.transform;
            if (enemyViewPool != null && enemyViewPool.TryGet(entity, out var qv) && qv != null) return qv.transform;
            if (defenderFallbackViewPool != null && defenderFallbackViewPool.TryGet(entity, out var fv) && fv != null) return fv.transform;
            return null;
        }

        // unit-dreamcatcher-icons unit 1 — 부착 아이콘 스트립 앵커 조회. 게이트웨이 경유
        // 읽기 전용 위임(뷰가 EntityManager/뷰 풀을 모르게 유지).
        public bool TryGetUnitViewAnchor(Entity entity, out Transform anchor)
        {
            anchor = ResolveUnitViewTransform(entity);
            return anchor != null;
        }

        // card-fly-to-target-absorb unit 1 — 카드 흡수 묵직 임팩트 게이트웨이(뷰가 뷰풀/EntityManager
        // 를 모르게 유지). SpineUnitView 반응(펀치/플래시)은 spine 유닛일 때만; SpawnCardAbsorbVfx 는
        // view 좌표를 그대로 VfxSpawner 에 위임(ToView 하지 않는 전용 경로).
        public bool TryGetUnitView(Entity entity, out Wassup.Presentation.SpineUnitView view)
        {
            view = null;
            return spineUnitPool != null && spineUnitPool.TryGet(entity, out view) && view != null;
        }

        public void SpawnCardAbsorbVfx(Vector3 viewPos)
        {
            if (vfxSpawner != null) vfxSpawner.SpawnCardAbsorb(viewPos);
        }

        // card-fly-to-target-absorb unit 2 — 타일/포탈 타겟의 셀 → **view** 월드 중심.
        // GridToWorldCenter 는 sim 공간이라 ToView 1회(sim/view 경계). 유닛 케이스의
        // transform.position(view)와 좌표계 일치 → 비행 투영/임팩트 VFX 가 어긋나지 않음.
        public Vector3 GridCellToViewCenter(Vector2Int cell)
            => Wassup.Core.BoardSpace.ToView(GridToWorldCenterVector(cell));

        // Enemy kills → live score HUD. One score bump per enemy killed by damage.
        private void DrainEnemyKilledEvents()
        {
            if (!_enemyKilledEventQueue.IsCreated) return;
            while (_enemyKilledEventQueue.TryDequeue(out var evt))
            {
                scoreHud?.OnEnemyKilled(EnemyKillScoreDelta);
                // dreamcatcher-awakening-hand unit 1 — awakening economy relay.
                EnemyKilledAwakening?.Invoke(evt.awakeningReward);
                // 살찌운 제물 — 표식 악몽 처치: 카드 회수 알림(보상은 위 relay 가
                // 표식 시점에 배율된 baked 값으로 이미 지급).
                NotifyEnemyGoneIfMarked(evt.entity);
                int2 grid = _generatedMap.IsCreated ? _generatedMap.gridSize : GridSize;
                var cell = GridMath.WorldToCell(evt.position, tileSize, grid, origin: _boardOrigin);
                float time = LogElapsedTime;
                var logger = GameManager.Instance?.Logger;
                logger?.RecordKill(string.Empty, new Vector2Int(cell.x, cell.y), time);
                logger?.AddScoreEvent("enemy_killed", EnemyKillScoreDelta, time);
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
                // battle-audio: fire SFX only for DEFENDER-shot projectiles (enemy ranged
                // attacks share this drain, so filter on the shooter's tag before spawn).
                bool shooterIsDefender = _em.HasComponent<DefenderUnitTag>(requestEntities[i]);
                var spawnedProjectile = SpawnProjectile(req, requestEntities[i]);
                if (spawnedProjectile != Entity.Null && shooterIsDefender)
                    Wassup.Core.SoundManager.Instance?.PlayProjectileFire();
                // dreamcatcher-unit-trigger Unit 1 — dedicated carrier entities are
                // destroyed outright: no vestigial empty entity, and no redundant
                // RemoveComponent structural change on an entity about to die.
                if (_em.HasComponent<ProjectileRequestCarrier>(requestEntities[i]))
                {
                    _em.DestroyEntity(requestEntities[i]);
                    continue;
                }
                _em.RemoveComponent<ProjectileSpawnRequest>(requestEntities[i]);
                if (_em.HasBuffer<ProjectileSpawnOutputElement>(requestEntities[i]))
                    _em.RemoveComponent<ProjectileSpawnOutputElement>(requestEntities[i]);
            }
            requestEntities.Dispose();
            requestData.Dispose();
        }

        // Returns the spawned projectile entity (Entity.Null when dropped) so
        // skill casts can track their telegraph's projectile (unit 9).
        private Entity SpawnProjectile(ProjectileSpawnRequest req, Entity shooter)
        {
            if (req.dataIndex < 0 || req.dataIndex >= _projectileDataByIndex.Count)
            {
                Debug.LogWarning($"[BattleBridge] ProjectileSpawnRequest dataIndex {req.dataIndex} out of range; dropping.");
                return Entity.Null;
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

            var projData = _projectileDataByIndex[req.dataIndex];
            var state = new ProjectileState
            {
                movement = req.movement,
                payload = req.payload,
                target = req.target,
                speed = req.speed,
                damage = req.damage,
                hitThreshold = req.hitThreshold,
                onHitEffect = req.onHitEffect,
                splashRadius = req.splashRadius,
                splashDamageMul = req.splashDamageMul,
                dataIndex = req.dataIndex,
                // dreamcatcher-attack-mod-bounce unit 2 — copy bounce params
                // verbatim; defaults 0 = every existing spawn keeps legacy destroy.
                bounceRemaining = req.bounceRemaining,
                bounceTileRange = req.bounceTileRange,
                bounceDamageMul = req.bounceDamageMul,
                // nightmare-catcher unit 1 — shooter attribution, verbatim copy.
                // Null (bridge-cast skills) = no threat credit.
                owner = req.owner,
                // nightmare-catcher unit 4 — TileAoe victim faction, verbatim copy.
                targetFaction = req.targetFaction,
                // dreamcatcher-content-2 unit 3 — frontmost priority victim + mul, verbatim
                // copy. Defaults Null/0 = inert, so every existing spawn keeps base damage.
                priorityTarget = req.priorityTarget,
                priorityDamageMul = req.priorityDamageMul,
                // dreamcatcher-heavy-strike unit 0 — 강공 전-victim 배율 verbatim 복사(기본 0=inert).
                heavyDamageMul = req.heavyDamageMul,
            };
            if (req.movement == MovementKind.BallisticArcToPoint)
            {
                // Keep origin and impact on the same spawn-height plane so the arc's
                // linear Y stays flat and only the sine bump lifts the shell.
                var ballisticImpact = new float3(req.impact.x, spawnHeight, req.impact.z);
                state.origin = spawnPos;
                state.impact = ballisticImpact;
                state.arcHeight = req.arcHeight;
                state.impactTileRange = req.impactTileRange;
                state.flightTime = BallisticArc.FlightTime(spawnPos, ballisticImpact, req.speed, projData.minFlightTime);
            }
            else if (req.movement == MovementKind.DirectionalLinear)
            {
                // defender-directional-volley unit 2 — 방향 직선 비행. 타겟/착탄 셀이
                // 없어 origin + direction + maxDistance 가 궤적 전부. 방향은 여기서
                // 한 번 정규화해 sim 이 매 프레임 normalize 하지 않게 한다(퇴화 벡터는
                // 스폰을 버림 — 정지한 투사체가 사거리 끝까지 안 죽고 남는 것 방지).
                // 정지 조건 둘 다 여기서 막는다: 방향이 0 이거나 속도가 0 이면 traveled 가
                // 영원히 0 → impactReached 가 서지 않아 PathHit 이 소멸 조건을 못 만난다
                // (사거리 소진도 예산 소진도 없는 불멸 투사체 — ecs-review M1).
                float2 dir = req.direction;
                if (math.lengthsq(dir) < 1e-6f || req.speed <= 0f)
                {
                    Debug.LogWarning($"[BattleBridge] Directional projectile cannot travel (dir={dir}, speed={req.speed}); dropping.");
                    _em.DestroyEntity(entity);
                    if (hasSnapshot) outputSnapshot.Dispose();
                    return Entity.Null;
                }
                state.origin = spawnPos;
                state.prevPos = spawnPos;
                state.direction = math.normalize(dir);
                state.maxDistance = req.maxDistance;
                // pierceCount 는 SO 소유 — SkyFall 의 dropHeight 보충과 같은 번역자 역할.
                state.pierceRemaining = projData != null ? math.max(1, projData.pierceCount) : 1;
            }
            else if (req.movement == MovementKind.SkyFall)
            {
                // Sky-fall (unit 7): sim holds at the cell-locked impact; flightTime
                // is request-carried (Meteor's warningSec), not speed-derived — the
                // travel distance is zero so BallisticArc.FlightTime would clamp to
                // minFlightTime and distort the telegraph timing.
                state.origin = spawnPos;
                state.impact = new float3(req.impact.x, spawnHeight, req.impact.z);
                state.impactTileRange = req.impactTileRange;
                state.flightTime = math.max(req.flightTime, 0f);
                // unit 9 — arcHeight 슬롯 = 낙하 시작 높이(view 렌더 전용).
                // nightmare-catcher unit 2 — ECS arm 발 캐리어(보스 융단폭격)는
                // SO 를 못 읽어 arcHeight=0 으로 온다 → 번역자인 drain 이
                // ProjectileData.dropHeight 로 보충(Meteor 는 캐스트 시 직접 기입).
                state.arcHeight = req.arcHeight > 0f ? req.arcHeight
                    : (projData != null ? projData.dropHeight : 0f);
            }
            _em.AddComponentData(entity, state);

            // defender-directional-volley unit 2 — 경로 스윕은 이미 맞힌 대상을
            // 기억해야 프레임마다 같은 적을 재타격하지 않는다(IncomingHeal 사전
            // 부착 선례 — 시스템이 구조 변경 없이 append 만 하게).
            if (req.payload == PayloadKind.PathHit)
                _em.AddBuffer<PathHitRecord>(entity);

            if (hasSnapshot)
            {
                var projectileOutputs = _em.AddBuffer<Wassup.Battle.Combat.AttackOutputElement>(entity);
                for (int i = 0; i < outputSnapshot.Length; i++)
                    projectileOutputs.Add(outputSnapshot[i]);
                outputSnapshot.Dispose();
            }

            // Viewless projectiles (unit 7 — prefab-less ProjectileData): a
            // registered ProjectileData with no prefab means "sim only"; the pool
            // has no null-prefab guard, so the skip lives here.
            // unit 9 — SkyFall 은 첫 프레임부터 하늘(dropHeight)에서 시작해야
            // 풀링 TrailRenderer 가 지면→하늘 스트릭을 긋지 않는다.
            if (projData != null && projData.projectilePrefab != null)
            {
                float initialDrop = req.movement == MovementKind.SkyFall ? projData.dropHeight : 0f;
                _projectileViewPool?.Spawn(entity, projData, spawnPos, initialDrop);
            }
            return entity;
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
                    EnqueueMoveSpeedMul(e, unitData.onPlaceMagnitude, unitData.onPlaceDuration, Wassup.Battle.Effects.ModifierOrigin.OnPlace);
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
                    EnqueueMoveSpeedMul(e, unitData.onPlaceMagnitude, unitData.onPlaceDuration, Wassup.Battle.Effects.ModifierOrigin.OnPlace);
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
                    EnqueueDamageMul(d.entity, unitData.onPlaceMagnitude, unitData.onPlaceDuration, Wassup.Battle.Effects.ModifierOrigin.OnPlace);
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
            if (!enableAdjacencySynergy)
            {
                NeutralizeActiveSynergy();
                return;
            }

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

        // 시너지 토글을 끈 뒤에도 이전 stackId=1 슬롯이 남지 않도록 중립값(+0)으로 refresh 한다.
        // Effects 소유 ModifierStats 는 직접 쓰지 않고 기존 StatModifierApplyEvents 채널만 사용한다.
        private void NeutralizeActiveSynergy()
        {
            if (_synergyActivatedEntities.Count == 0) return;
            foreach (var binding in _defenderByTile.Values)
            {
                if (_em.Exists(binding.entity) && !_em.HasComponent<PendingDeployment>(binding.entity))
                    EnqueueSynergyMul(binding.entity, 1f);
            }
            _synergyActivatedEntities.Clear();
        }

        // Unit 8: channel enqueue helpers — route legacy effect produces through StatModifier channel.
        // source=target ensures the ApplySystem merge-key matches per-entity, preventing slot accumulation.
        // modifier-additive-authoring — the central helper applies the increase/reduction
        // policy (ModifierAuthoring): buffs (multiplier>=1) become additive deltas that sum,
        // reductions stay multiplicative. Callers keep passing multipliers unchanged.
        // INVARIANT: op is now part of the merge key (source,stat,op,stackId). A single
        // (stat, stackId, source=target) channel must stay one-directional — never mix
        // values across the 1.0 boundary. Straddling would leave an Additive and a
        // Multiplicative slot coexisting instead of refreshing (slot accumulation). All
        // current channels are one-directional; keep new ones so (e.g. don't route both
        // haste and slow through EnqueueMoveSpeedMul's stackId=0).
        private void EnqueueStatModifier(Entity target, Wassup.Battle.Effects.StatKind stat, float multiplier, float duration, ushort stackId, Wassup.Battle.Effects.ModifierOrigin origin)
        {
            if (!_statModifierQueue.IsCreated) return;
            Wassup.Battle.Effects.ModifierAuthoring.FromMultiplier(multiplier, out var op, out var magnitude);
            _statModifierQueue.Enqueue(new Wassup.Battle.Effects.StatModifierApplyEvent
            {
                target    = target,
                stat      = stat,
                op        = op,
                magnitude = magnitude,
                duration  = duration,
                source    = target,
                stackId   = stackId,
                origin    = origin,
            });
        }

        // 명시적 op+magnitude enqueue. FromMultiplier 로는 표현 못 하는 값(예: Multiplicative 항등 1.0)이
        // 필요한 revoke 중립화 전용. (일반 경로는 EnqueueStatModifier — 배율→op 자동 분류)
        private void EnqueueStatModifierRaw(Entity target, Wassup.Battle.Effects.StatKind stat, Wassup.Battle.Effects.CombineOp op, float magnitude, float duration, ushort stackId, Wassup.Battle.Effects.ModifierOrigin origin)
        {
            if (!_statModifierQueue.IsCreated) return;
            _statModifierQueue.Enqueue(new Wassup.Battle.Effects.StatModifierApplyEvent
            {
                target    = target,
                stat      = stat,
                op        = op,
                magnitude = magnitude,
                duration  = duration,
                source    = target,
                stackId   = stackId,
                origin    = origin,
            });
        }

        public void EnqueueDamageMul(Entity target, float multiplier, float duration, Wassup.Battle.Effects.ModifierOrigin origin)
            => EnqueueStatModifier(target, Wassup.Battle.Effects.StatKind.DamageMul, multiplier, duration, 0, origin);

        // RapidFire / CooldownReduction: multiplier here means "attack speed factor" (how much faster to fire).
        // AttackSpeedMul > 1 = faster attacks. Legacy ApplyCooldownReduction stored 1/multiplier as a cooldown divisor;
        // the new channel stores the speed multiplier directly (ModifierStatsAggregateSystem applies it to attackSpeedMul).
        public void EnqueueAttackSpeedMul(Entity target, float multiplier, float duration, Wassup.Battle.Effects.ModifierOrigin origin)
            => EnqueueStatModifier(target, Wassup.Battle.Effects.StatKind.AttackSpeedMul, multiplier, duration, 0, origin);

        public void EnqueueMoveSpeedMul(Entity target, float multiplier, float duration, Wassup.Battle.Effects.ModifierOrigin origin)
            => EnqueueStatModifier(target, Wassup.Battle.Effects.StatKind.MoveSpeedMul, multiplier, duration, 0, origin);

        // Synergy: infinite duration, magnitude refreshed each recompute.
        // multiplier=1.0 (neighbors==0) authors as the additive identity (+0.0).
        // stackId=1 distinguishes synergy slot from onplace/skill DamageMul (stackId=0).
        private void EnqueueSynergyMul(Entity target, float multiplier)
            => EnqueueStatModifier(target, Wassup.Battle.Effects.StatKind.DamageMul, multiplier, float.PositiveInfinity, 1, Wassup.Battle.Effects.ModifierOrigin.Synergy);

        // dreamcatcher-bridge-partial-cleanup unit 0 — 드림캐쳐 카드 번역자
        // (레지스트리·apply/revoke·부착 베이크·axis/effect 매핑)는
        // BattleBridge.Dreamcatcher.cs (partial) 로 이동.

        // awakening-hand simplify F1 — pointer→board-cell as a single shared
        // helper. The exact ray→RaycastPlane→ToSim→cell block is hand-rolled in
        // PlacementInput too (pre-existing copy; consolidation is a follow-up)
        // — new call sites MUST use this instead of another copy.
        // BoardSpace.RaycastPlane (not Plane(up)) is load-bearing: the tilemap
        // front-view board plane is near-parallel to an up-plane ray.
        public bool TryScreenToCell(Camera cam, Vector2 screenPos, out Vector2Int cell)
        {
            cell = default;
            if (cam == null) return false;
            var ray = cam.ScreenPointToRay(screenPos);
            var plane = Wassup.Core.BoardSpace.RaycastPlane();
            if (!plane.Raycast(ray, out float enter)) return false;
            var world = (Vector3)Wassup.Core.BoardSpace.ToSim(ray.GetPoint(enter));
            var hit = DebugWorldToCell(world);
            cell = new Vector2Int(hit.x, hit.y);
            return true;
        }

        // subconscious-curse-expansion unit 3 (살찌운 제물) — 드롭 지점 최근접 적 픽.
        // 반경 = radiusTiles × tileSize(유클리드 xz, 셀 양자화 없이 평면 히트 그대로).
        // 픽은 커밋 순간의 스냅샷 — 이후 이동은 무관. 동거리 동점은 entity index
        // 오름차순(결정론, HealthThreshold 폴백 선례). 반경 내 없음 = false(무차감).
        public bool TryPickNearestEnemy(Camera cam, Vector2 screenPos, float radiusTiles, out Entity enemy)
        {
            enemy = Entity.Null;
            if (cam == null || !HasLiveEntityManager()) return false;
            var ray = cam.ScreenPointToRay(screenPos);
            var plane = Wassup.Core.BoardSpace.RaycastPlane();
            if (!plane.Raycast(ray, out float enter)) return false;
            var world = (Vector3)Wassup.Core.BoardSpace.ToSim(ray.GetPoint(enter));

            float maxSq = radiusTiles * tileSize;
            maxSq *= maxSq;
            using var query = _em.CreateEntityQuery(
                ComponentType.ReadOnly<Wassup.Battle.Units.AttackUnitTag>(),
                ComponentType.ReadOnly<Unity.Transforms.LocalTransform>());
            var entities = query.ToEntityArray(Allocator.Temp);
            var transforms = query.ToComponentDataArray<Unity.Transforms.LocalTransform>(Allocator.Temp);
            try
            {
                float bestSq = maxSq;
                for (int i = 0; i < entities.Length; i++)
                {
                    Vector3 d = (Vector3)transforms[i].Position - world;
                    d.y = 0f;
                    float sq = d.sqrMagnitude;
                    if (sq < bestSq ||
                        (sq == bestSq && enemy != Entity.Null && entities[i].Index < enemy.Index))
                    {
                        bestSq = sq;
                        enemy = entities[i];
                    }
                }
            }
            finally
            {
                entities.Dispose();
                transforms.Dispose();
            }
            return enemy != Entity.Null;
        }

        // dreamcatcher-awakening-hand unit 7 — defender lookup for card-drag
        // targeting (hover highlight + Unit-card attach target). Read-only view
        // over the tile→binding registry; Entity is a key for the attach APIs.
        public bool TryGetDefenderAt(Vector2Int cell, out Entity defender)
        {
            if (_defenderByTile.TryGetValue(cell, out var binding))
            {
                defender = binding.entity;
                return true;
            }
            defender = Entity.Null;
            return false;
        }

        // dreamcatcher-awakening-hand rev 4 — card-drag hover focus: tint the
        // hovered defender's spine view so the attach target reads on the UNIT
        // itself (a tile highlight alone hides under the sprite). View-only;
        // fallback quad views simply get no tint (tile hover remains).
        public void SetDefenderHoverHighlight(Entity defender, bool on, Color tint)
        {
            if (spineUnitPool != null && spineUnitPool.TryGet(defender, out var view) && view != null)
                view.SetHoverHighlight(on, tint);
        }

        // dreamcatcher-awakening-hand rev 4 — SCREEN-SPACE defender picking.
        // Root cause fix: the board-plane raycast resolves the cell under the
        // pointer's GROUND point, but tilted-billboard sprites rise above their
        // cell on screen — pointing at a unit's body lands rows behind its feet
        // and cell lookup misses. Here we test the pointer against each spine
        // view's projected sprite rect instead (overlaps → nearest rect center).
        // Callers should fall back to TryGetDefenderAt(cell) for feet-point taps
        // and fallback quad views.
        public bool TryPickDefenderAtScreen(Camera cam, Vector2 screenPos, out Entity defender, out Vector2Int cell)
        {
            defender = Entity.Null;
            cell = default;
            if (cam == null || spineUnitPool == null) return false;
            float best = float.MaxValue;
            foreach (var kv in _defenderByTile)
            {
                if (!spineUnitPool.TryGet(kv.Value.entity, out var view) || view == null) continue;
                if (!view.TryGetScreenRect(cam, out var rect) || !rect.Contains(screenPos)) continue;
                float d = (rect.center - screenPos).sqrMagnitude;
                if (d < best)
                {
                    best = d;
                    defender = kv.Value.entity;
                    cell = kv.Key;
                }
            }
            return defender != Entity.Null;
        }


        // dreamstone-loadout Unit 3 — squad-equipped stones, set-then-apply (mirrors
        // SetDefenderPool). GameManager calls this BEFORE BeginPlacement; storing here
        // only stages the pending list. BeginPlacement applies it right after clearing
        // _activeDcEffects (below), so clear+reapply happen at a single point and a
        // match restart can neither leak (re-added on top of stale entries) nor drop
        // (cleared with nothing to restore) the loadout.
        private System.Collections.Generic.IReadOnlyList<Wassup.Data.DreamstoneData> _pendingDreamstones;

        public void SetDreamstones(System.Collections.Generic.IReadOnlyList<Wassup.Data.DreamstoneData> stones)
        {
            _pendingDreamstones = stones;
        }

        // Applies the pending stone loadout into the same _activeDcEffects registry
        // ApplyDreamcatcherCardInternal uses, targeting axis=All (every allied defender).
        // Called only from BeginPlacement, immediately after its clear — see the
        // set-then-apply note on SetDreamstones above.
        private void ApplyPendingDreamstones()
        {
            if (_pendingDreamstones == null) return;
            for (int i = 0; i < _pendingDreamstones.Count; i++)
            {
                var stone = _pendingDreamstones[i];
                if (stone == null) continue;
                if (!MapDcEffect(stone.effect, out var stat, out var mult)) continue;
                ushort sid = _dcStackCounter++;
                _activeDcEffects.Add(new ActiveDcEffect { axis = Wassup.Data.CardTargetAxis.All, stat = stat, mult = mult, stackId = sid, origin = Wassup.Battle.Effects.ModifierOrigin.Dreamstone });
                // No defenders are placed yet at this point in BeginPlacement (_defenderByTile
                // was just cleared above) — this loop is a no-op today, but sharing it with
                // ApplyDreamcatcherCardInternal's identical loop is harmless and keeps both call sites
                // symmetric if that ever changes. Ordering dependency (review L1): this method
                // runs before EnsureQueriesAndQueues() further down in BeginPlacement, so
                // _statModifierQueue is not yet IsCreated here — moving the stone apply to
                // after placement begins would make EnqueueStatModifier's IsCreated guard
                // silently swallow it instead of a clean no-op.
                foreach (var kv in _defenderByTile)
                {
                    var data = kv.Value.data;
                    var entity = kv.Value.entity;
                    if (data != null && _em.Exists(entity) && MatchesDcAxis(data, Wassup.Data.CardTargetAxis.All))
                        EnqueueStatModifier(entity, stat, mult, DcDuration, sid, Wassup.Battle.Effects.ModifierOrigin.Dreamstone);
                }
            }
        }

        // Maps the authoring-side ProjectileFlightMode onto the ECS trajectory/payload
        // axes. Coherent pairs only; other combinations are follow-ups.
        private static (MovementKind movement, PayloadKind payload) ResolveProjectileAxes(ProjectileFlightMode mode)
            => mode switch
            {
                ProjectileFlightMode.BallisticToCell => (MovementKind.BallisticArcToPoint, PayloadKind.TileAoe),
                // defender-directional-volley unit 1 — 방향 직선 비행 × 경로 스윕 페어.
                ProjectileFlightMode.Directional => (MovementKind.DirectionalLinear, PayloadKind.PathHit),
                _ => (MovementKind.HomingToEntity, PayloadKind.SingleSplash),
            };

        private int GetOrCreateProjectileDataIndex(ProjectileData projectile)
        {
            if (_projectileDataIndex.TryGetValue(projectile, out var idx)) return idx;
            idx = _projectileDataByIndex.Count;
            _projectileDataByIndex.Add(projectile);
            _projectileDataIndex[projectile] = idx;
            return idx;
        }

        // subconscious-curse-expansion unit 1 (몽마의 계약) — 잔여 유출 허용치.
        // = SO 기준치 − 선불 차감 − 이미 유출된 수. 컨트롤러 게이트/HUD 조회용.
        public int RemainingLeakAllowance()
            => deck != null ? deck.defeatGoalReachedCount - _leakAllowancePenalty - _goalReachedCount : 0;

        // 몽마의 계약 선불 지불. 지불 후 잔여가 1 미만이면 거절 — "지불로 즉시 패배"
        // 상태를 구조적으로 금지(spec 게이트 조건: 잔여 − cost ≥ 1). 성공 시 비가역:
        // host 사망 revoke 는 hosted 버프만 회수하고 이 오프셋은 되돌리지 않는다.
        public bool TryPayLeakAllowance(int cost)
        {
            if (cost <= 0) return false;
            if (RemainingLeakAllowance() - cost < 1) return false;
            _leakAllowancePenalty += cost;
            return true;
        }

        private void DrainGoalEvents()
        {
            if (!_goalEventQueue.IsCreated) return;
            while (_goalEventQueue.TryDequeue(out var evt))
            {
                enemyViewPool?.Despawn(evt.entity);
                spineUnitPool?.Despawn(evt.entity);
                // 살찌운 제물 — 표식 악몽 유출: 무보상 회수. 패배 트리거의 조기 return 시
                // 같은 프레임 잔여 이벤트의 EnemyGone 은 미발화 — 매치 종료 직후라 무해
                // (BeginPlacement clear 가 등록부/컨트롤러 양쪽을 정리).
                NotifyEnemyGoneIfMarked(evt.entity);
                _goalReachedCount++;
                // 몽마의 계약 — 패배 판정은 선불 차감을 반영한 유효 허용치 기준.
                Debug.Log($"[BattleBridge] Goal reached! Count: {_goalReachedCount}/{deck.defeatGoalReachedCount - _leakAllowancePenalty}");
                if (!_resultShown && _goalReachedCount >= deck.defeatGoalReachedCount - _leakAllowancePenalty)
                {
                    _resultShown = true;
                    _running = false;
                    int playerScore = CalculatePlayerScore();
                    GameManager.Instance?.Logger?.SetResult("defeat", _goalReachedCount);
                    GameManager.Instance?.Logger?.SetScore(playerScore);
                    resultScreen?.ShowDefeat(playerScore, RemainingBattleSeconds(), _goalReachedCount);
                    ReportMatchResult(playerScore);
                    Debug.Log("[BattleBridge] DEFEAT triggered.");
                    return;
                }
            }
        }

        public float TimerRemaining => _running ? Mathf.Max(0f, _timerDuration - (float)_battleClock) : 0f;

        // Seconds left on the match clock at query time — unlike TimerRemaining this
        // stays valid after _running is cleared (used to stamp the result popup).
        private float RemainingBattleSeconds() => Mathf.Max(0f, _timerDuration - (float)_battleClock);

        private void CheckTimer()
        {
            if (_resultShown) return;
            if (_timerDuration <= 0f) return;
            if ((float)_battleClock < _timerDuration) return;

            _resultShown = true;
            _running = false;
            int playerScore = CalculatePlayerScore();
            GameManager.Instance?.Logger?.SetResult("victory_timeout", _goalReachedCount);
            GameManager.Instance?.Logger?.SetScore(playerScore);
            resultScreen?.ShowVictory(playerScore, 0f, _goalReachedCount); // timer expired → 0 left
            ReportMatchResult(playerScore);
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
            int playerScore = CalculatePlayerScore();
            GameManager.Instance?.Logger?.SetResult("victory", _goalReachedCount);
            GameManager.Instance?.Logger?.SetScore(playerScore);
            resultScreen?.ShowVictory(playerScore, RemainingBattleSeconds(), _goalReachedCount);
            ReportMatchResult(playerScore);
            Debug.Log("[BattleBridge] VICTORY — all attack units defeated.");
        }

        // tournament-play-report Units 3/4 — shared result-popup hook: snapshot
        // the battle log (SetResult/SetScore must already be applied), send
        // complete, and swap the popup's bot leaderboard for the real ranking
        // when it arrives. Guests and failures fall through silently — the bot
        // list stays.
        private void ReportMatchResult(int playerScore)
        {
            // Match is over — enter the Result phase so battle-only HUD (NextWaveDock,
            // ScoreHud, CostDisplay) deactivates. RESTART goes Result →
            // Placement → Battle (BeginPlacementPhase), which re-shows them.
            GameManager.Instance?.SetPhase(GamePhase.Result);
            var logger = GameManager.Instance?.Logger;
            Wassup.Core.Api.TournamentMatchReporter.ReportResult(playerScore, logger?.SnapshotJson(),
                ranking => resultScreen?.UpdateLeaderboard(ranking, Wassup.Core.Api.UserSession.Current?.userId));
        }

        private int CalculatePlayerScore()
        {
            float durationSec = Mathf.Max(0f, (float)_battleClock);
            return Math.Max(0, (int)(durationSec * 10f - _goalReachedCount * 50));
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
            // battle-audio: per-character casual deploy interjection (class-fitting voice).
            Wassup.Core.SoundManager.Instance?.PlayDeployVoice(unitData.deployVoiceClip);
            Debug.Log($"[BattleBridge] Began pending deployment for {unitData.displayName} at ({tileX},{tileY}).");
            return true;
        }

        // defender-directional-volley unit 1 — aim-phase 확정 방향을 기록하고 활성화.
        // DeployedFacing 은 Units 소유, 배치 확정 시 이 1회 기록 후 불변(공통 계약 2).
        // 컴포넌트를 먼저 붙여 on-place 스킬이 활성화 시점에 방향을 읽을 수 있게 한다.
        public void ActivateDeployedDefender(Vector2Int cell, Entity entity, Vector2Int facing)
        {
            if (_em != null && entity != Entity.Null && _em.Exists(entity) && facing != Vector2Int.zero)
                _em.AddComponentData(entity, new DeployedFacing { value = new int2(facing.x, facing.y) });
            ActivateDeployedDefender(cell, entity);
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
            ApplyEffectTileIfAny(cell, entity); // effect-tiles unit 2 — 가드 뒤 exactly-once (드래그 경로)
            LogOnPlaceAndSynergy(binding.data, cell, onPlaceAffected);
            return true;
        }

        // tilemap-view-backend unit 4 — 모드별 ortho 카메라 프리셋 적용. gridSize+tileSize 로 orthographicSize 계산.
        // 매 맵 빌드마다 호출 — 프리셋+gridSize 에서 결정론적이라 RebuildDraftMap 재진입에도 idempotent.
        // [은퇴 — camera-direction unit 0] 호출부 없음. 카메라 포즈는 CameraDirector 가 유일 소유 —
        // 재호출해도 다음 LateUpdate 에 덮여 무효. framing 산식(보드 fit) 참고용으로만 보존.
        private void ApplyTilemapCameraPreset()
        {
            var preset = boardViewMode == Wassup.Core.BoardViewMode.TilemapIso
                ? tilemapCameraPresetIso : tilemapCameraPresetRect;
            var cam = Camera.main;
            if (preset == null || cam == null) return;

            cam.orthographic = preset.orthographic;
            float aspect = cam.aspect > 0.01f ? cam.aspect : (16f / 9f);

            // tilted-billboard unit 1 — 보드 월드 bounds 산출 (페인트 실측 우선, 없으면 gridSize 추정).
            Bounds board;
            if (tilemapMapView != null && tilemapMapView.TryGetBoardWorldBounds(out var b))
            {
                board = b;
            }
            else
            {
                int2 g = _generatedMap.gridSize;
                float3 centerSim = new float3((g.x - 1) * 0.5f * tileSize, 0f, (g.y - 1) * 0.5f * tileSize);
                Vector3 centerView = Wassup.Core.BoardSpace.ToView(centerSim);
                board = new Bounds(centerView, new Vector3(g.x * tileSize, g.y * tileSize, 0f));
            }

            // 회전 먼저 — framing 은 회전된 카메라 기준으로 계산해야 틸트해도 화면에 꽉/중앙.
            cam.transform.rotation = Quaternion.Euler(preset.rotationEuler);

            if (preset.orthographic)
            {
                // ortho: positionOffset 은 "view 축 거리"로 해석. 보드 중심 정면 배치.
                float dist = preset.positionOffset.magnitude;
                if (dist < 0.01f) dist = 20f;
                cam.transform.position = board.center - cam.transform.forward * dist;

                // orthographicSize: 보드 8코너를 카메라 view 공간 투영해 실제 화면 extent 산출(틸트/iso 자동 보정).
                Vector3 ext = board.extents;
                float maxX = 0f, maxY = 0f;
                for (int sx = -1; sx <= 1; sx += 2)
                    for (int sy = -1; sy <= 1; sy += 2)
                        for (int sz = -1; sz <= 1; sz += 2)
                        {
                            Vector3 corner = board.center + new Vector3(sx * ext.x, sy * ext.y, sz * ext.z);
                            Vector3 v = cam.transform.InverseTransformPoint(corner);
                            maxX = Mathf.Max(maxX, Mathf.Abs(v.x));
                            maxY = Mathf.Max(maxY, Mathf.Abs(v.y));
                        }
                cam.orthographicSize = Mathf.Max(maxY, maxX / aspect) + preset.orthoSizePadding;
            }
            else
            {
                // 퍼스펙티브: FOV 로 보드 바운딩 구를 화면에 맞춘다. distance = R / sin(fov/2).
                // 가로 FOV ≥ 세로 FOV(aspect>1)라 세로 기준 구-fit 이면 항상 들어온다. pitch 와 무관하게 추종.
                cam.fieldOfView = preset.fieldOfView;
                float radius = board.extents.magnitude;
                if (radius < 0.01f) radius = 1f;
                float half = Mathf.Deg2Rad * preset.fieldOfView * 0.5f;
                float dist = radius / Mathf.Max(0.01f, Mathf.Sin(half)) * preset.perspectiveFitMargin;
                cam.transform.position = board.center - cam.transform.forward * dist;
            }

            cam.nearClipPlane = preset.nearClip;
            cam.farClipPlane = preset.farClip;
            cam.transparencySortMode = preset.transparencySortMode;
            cam.transparencySortAxis = preset.sortAxis;
            if (preset.solidColorBackground)
            {
                cam.clearFlags = CameraClearFlags.SolidColor; // skybox 제거
                cam.backgroundColor = preset.backgroundColor;
            }
        }

        // unit 1 — Tilemap 뷰에서 항상 숨길 환경 오브젝트 (skybox 는 카메라 clearFlags 가 처리).
        // 빈 배열 = no-op. 실제 대상 배선은 dirty BattleScene 정리(unit 2) 후.
        private void ApplyEnvironmentGating()
        {
            if (tilemapHiddenEnvironment == null) return;
            for (int i = 0; i < tilemapHiddenEnvironment.Length; i++)
            {
                var go = tilemapHiddenEnvironment[i];
                if (go != null) go.SetActive(false);
            }
        }

        // tilemap-view-backend unit 3 — 배치 hover/reject 피드백 (PlacementInput/DragController 공용 단일 경로).
        public void SetPlacementHover(Vector2Int cell, bool valid)
        {
            if (tilemapMapView != null) tilemapMapView.SetPlacementHover(cell, valid);
        }

        // placement-cell-snap unit 4 — 포커스 타일 확정(변경) 시 1회 팝.
        public void PulsePlacementHover(Vector2Int cell, bool valid)
        {
            if (tilemapMapView != null) tilemapMapView.PulsePlacementHover(cell, valid);
        }

        // placement-cell-snap unit 7 rev — 끈적 액체 하이라이트(hover 대체). dir=당김 방향, t=0(중심)~1(파열),
        // valid=배치 가능 팔레트. cell+값만 받는다(오브젝트 불가지).
        public void SetPlacementStretch(Vector2Int cell, Vector2 dir, float t, bool valid)
        {
            if (tilemapMapView != null) tilemapMapView.SetPlacementStretch(cell, dir, t, valid);
        }

        public void ClearPlacementStretch()
        {
            if (tilemapMapView != null) tilemapMapView.ClearPlacementStretch();
        }

        public void ClearPlacementHover(Vector2Int cell)
        {
            if (tilemapMapView != null) tilemapMapView.ClearPlacementHover(cell);
        }

        public void ClearPlacementHover()
        {
            if (tilemapMapView != null) tilemapMapView.ClearPlacementHover();
        }

        // range-preview unit 3 — 격자 범위 표시(_rangeTilemap)의 현재 소유자.
        // 배치 드래그(Placement)·스킬 조준 추종(SkillAim)·캐스트 후 착탄 예고
        // (SkillTelegraph)가 같은 tilemap 을 시분할 사용한다. clear 는 소유자가
        // 일치할 때만 동작 — aim 종료(캐스트 직후)가 방금 고정된 텔레그래프를
        // 지우거나, 텔레그래프 해제가 진행 중인 드래그 표시를 지우는 간섭 방지.
        // (aim 진입이 배치를 취소하는 기존 규칙 덕에 동시 set 경쟁은 없다.)
        // PlacementAim = defender-directional-volley unit 9 (방향 지정 페이즈 레인 프리뷰).
        // 드롭 직후 드래그 세션의 CleanupSession 이 ClearPlacementRange 를 부르는데, 소유가
        // 이미 PlacementAim 이면 그 호출이 내 레인을 지우지 않는다 — 별도 소유자인 이유.
        private enum RangeDisplayOwner { None, Placement, SkillAim, SkillTelegraph, PlacementAim }
        private RangeDisplayOwner _rangeOwner = RangeDisplayOwner.None;
        private readonly List<Vector2Int> _laneCellScratch = new List<Vector2Int>();
        private readonly List<Vector2Int> _arrowCells = new List<Vector2Int>();
        private readonly List<float> _arrowAngles = new List<float>();
        // 방향 미정(4레인 십자) 세기. 선택된 레인은 1.
        private const float AimLaneDimAlpha = 0.45f;
        // 조준 화살표/레인이 쓰는 4방향. 순서 = 화살표 인덱스.
        private static readonly Vector2Int[] AimCardinals =
        {
            Vector2Int.right, Vector2Int.left, Vector2Int.up, Vector2Int.down,
        };
        // unit 9 — 현재 텔레그래프가 추적 중인 스킬 투사체. 그 착탄 이벤트에서만 해제.
        private Entity _skillTelegraphProjectile = Entity.Null;

        public void SetPlacementRange(Vector2Int center, DefenderUnitData unit)
        {
            if (tilemapMapView == null || unit == null) return;
            int tileRange = GridMath.RangeToTiles(unit.attackRange);
            // unit 9 — 방향 유닛에게 네모 사거리는 거짓말이다(레인만 때린다). 방향은 아직
            // 안 정해졌으므로 고를 수 있는 4레인을 십자로 흐리게 — 조준 페이즈와 같은 언어.
            if (unit.directionalAttack) PaintLanes(center, tileRange, null, AimLaneDimAlpha);
            else tilemapMapView.SetPlacementRange(center, tileRange);
            _rangeOwner = RangeDisplayOwner.Placement;
        }

        public void ClearPlacementRange() => ClearRange(RangeDisplayOwner.Placement);

        // unit 9 — 조준 페이즈 가이드(레인 + 화살표를 한 상태로). 선택 전이면 4레인 십자를
        // 흐리게 = "이 중 하나를 커버한다"(사거리가 즉시 읽힌다), 선택되면 그 레인만 또렷 =
        // "여기를 때린다". 방향을 말해주는 건 레인이 아니라 화살표다 — 레인은 대칭이라
        // "위로 쏜다"와 "위에서 쏜다"가 같아 보인다.
        public void SetAimGuide(Vector2Int center, DefenderUnitData unit, Vector2Int? selected)
        {
            if (tilemapMapView == null || unit == null) return;
            int tileRange = GridMath.RangeToTiles(unit.attackRange);
            PaintLanes(center, tileRange, selected, selected.HasValue ? 1f : AimLaneDimAlpha);
            _rangeOwner = RangeDisplayOwner.PlacementAim;

            // 화살표는 각 레인의 첫 칸에 — 유닛을 둘러싼 D-pad 라 엄지로 닿고, 레인이
            // 어디서 출발하는지도 같은 자리에서 말한다.
            _arrowCells.Clear();
            _arrowAngles.Clear();
            int selectedIndex = -1;
            for (int i = 0; i < AimCardinals.Length; i++)
            {
                var c = AimCardinals[i];
                if (selected.HasValue && selected.Value == c) selectedIndex = i;
                _arrowCells.Add(center + c);
                // 스프라이트는 +Y 를 향한다 → 그 방향으로 눕힌다.
                _arrowAngles.Add(Mathf.Atan2(c.y, c.x) * Mathf.Rad2Deg - 90f);
            }
            tilemapMapView.SetAimArrows(_arrowCells, _arrowAngles, selectedIndex);
        }

        public void ClearAimGuide()
        {
            ClearRange(RangeDisplayOwner.PlacementAim);
            if (tilemapMapView != null) tilemapMapView.ClearAimArrows();
        }

        // 칠할 셀을 시뮬의 발사 게이트와 **같은 함수**로 고른다 — 보이는 칸과 실제로 맞는
        // 칸이 구조적으로 일치한다(따로 계산하면 언젠가 어긋난다).
        private void PaintLanes(Vector2Int center, int tileRange, Vector2Int? facing, float alphaMul)
        {
            if (tileRange <= 0) return;
            _laneCellScratch.Clear();
            var c = new int2(center.x, center.y);
            for (int dx = -tileRange; dx <= tileRange; dx++)
            for (int dz = -tileRange; dz <= tileRange; dz++)
            {
                var cell = new int2(center.x + dx, center.y + dz);
                bool lit = facing.HasValue
                    ? LaneMath.IsInLane(c, new int2(facing.Value.x, facing.Value.y), tileRange, cell)
                    : LaneMath.IsInLane(c, new int2(1, 0), tileRange, cell)
                      || LaneMath.IsInLane(c, new int2(-1, 0), tileRange, cell)
                      || LaneMath.IsInLane(c, new int2(0, 1), tileRange, cell)
                      || LaneMath.IsInLane(c, new int2(0, -1), tileRange, cell);
                if (lit) _laneCellScratch.Add(new Vector2Int(cell.x, cell.y));
            }
            tilemapMapView.SetPlacementCells(_laneCellScratch, alphaMul);
        }

        // 스킬 조준 범위 — 배치와 달리 중심 셀 포함(AOE 는 중심도 피해 범위).
        public void SetSkillAimRange(Vector2Int center, SkillData skill)
        {
            if (tilemapMapView == null || skill == null) return;
            tilemapMapView.SetPlacementRange(center, GridMath.RangeToTiles(skill.range), includeCenter: true);
            _rangeOwner = RangeDisplayOwner.SkillAim;
        }

        public void ClearSkillAimRange() => ClearRange(RangeDisplayOwner.SkillAim);

        private void PinSkillTelegraph(Vector2Int cell, int tileRange)
        {
            if (tilemapMapView == null) return;
            tilemapMapView.SetPlacementRange(cell, tileRange, includeCenter: true);
            _rangeOwner = RangeDisplayOwner.SkillTelegraph;
        }

        private void ClearSkillTelegraph() => ClearRange(RangeDisplayOwner.SkillTelegraph);

        private void ClearRange(RangeDisplayOwner caller)
        {
            if (_rangeOwner != caller) return;
            _rangeOwner = RangeDisplayOwner.None;
            if (tilemapMapView != null) tilemapMapView.ClearPlacementRange();
        }

        public void FlashPlacementReject(Vector2Int cell)
        {
            if (tilemapMapView != null) tilemapMapView.FlashTileReject(cell);
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
            // combat-action-lock unit 2 — defender 도 CC(Sleep/Stun) 수신하도록 CcEffect 버퍼
            // 사전 부착. ApplyActiveDcEffectsTo(3641, placement Sleep 적용) 이전이어야 함(MED4).
            _em.AddBuffer<Wassup.Battle.Effects.CcEffect>(entity);
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
                range = unitData.attackRange,
                cooldownDuration = unitData.attackCooldown,
                cooldownRemaining = unitData.deployDelaySec, // attack-hit-delay 2 — 배치 직후 deployDelaySec 동안 idle(공격 X)
                attackTargetCount = unitData.attackTargetCount,
                targetMask = unitData.targetAllies ? (int)Faction.Defender : (int)Faction.Enemy,
                hitDelaySec = unitData.hitDelaySec,
            });
            // defender-directional-volley unit 4 — 다연발 유닛만 볼리 상태를 진다.
            // 스폰 시 사전 부착 = 발사 때마다 구조 변경이 없다(IncomingHeal 선례).
            // shotCount <= 1 이면 미부착 → AttackSystem 이 현행 단발 경로 그대로.
            if (unitData.shotCount > 1)
            {
                _em.AddComponentData(entity, new Wassup.Battle.Combat.VolleyFireState
                {
                    shotCount = unitData.shotCount,
                    shotIntervalSec = unitData.shotIntervalSec,
                    spreadAngleDeg = unitData.spreadAngleDeg,
                });
            }
            // aggro-targeting Unit 4 — expose defender class so enemies can filter/prioritize.
            _em.AddComponentData(entity, new Wassup.Battle.Units.DefenderClassTag { value = unitData.role });
            // aggro-targeting Unit 10 — guardians (aggroCapacity > 0) carry AggroCapacity
            // (존재=가디언 표식). Fighter/Ranger (aggroCapacity == 0) get none. 획득은
            // 히트 구동(AttackSystem RESOLVE→AggroHitEvent) — 별도 range 없음.
            if (unitData.aggroCapacity > 0)
            {
                _em.AddComponentData(entity, new Wassup.Battle.Effects.AggroCapacity
                {
                    max = unitData.aggroCapacity,
                    held = 0,
                });
            }
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
                var axes = ResolveProjectileAxes(unitData.projectile.flightMode);
                _em.AddComponentData(entity, new ProjectileRef
                {
                    dataIndex = dataIndex,
                    speed = unitData.projectile.speed,
                    hitThreshold = unitData.projectile.hitThreshold,
                    visualScale = unitData.projectile.visualScale,
                    onHitEffect = unitData.projectile.onHitEffect,
                    splashRadius = unitData.projectile.splashRadius,
                    splashDamageMul = unitData.projectile.splashDamageMul,
                    movement = axes.movement,
                    payload = axes.payload,
                    arcHeight = unitData.projectile.arcHeight,
                    impactTileRange = unitData.projectile.impactTileRange,
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
                damageVsCcMul  = 1f, // dreamcatcher-new-abilities unit 2 — base 1 (dirty 는 disabled 로 추가돼 무-모디파이어 유닛은 집계가 안 돌므로 여기서 필수)
                maxHealthMul   = 1f, // season-gimmick-overwork unit 1 — base 1 (동일 사유)
            });
            _em.AddComponent<Wassup.Battle.Effects.ModifierStatsDirty>(entity);
            _em.SetComponentEnabled<Wassup.Battle.Effects.ModifierStatsDirty>(entity, false);
            _em.AddBuffer<Wassup.Battle.Units.IncomingHeal>(entity);

            // ingame-dreamcatcher Unit 2 — inherit active match-long card effects.
            ApplyActiveDcEffectsTo(entity, unitData);

            return entity;
        }

        // effect-tiles unit 2 — 효과 타일 modifier 슬롯 네임스페이스.
        // 규약: on-place/skill=0 · 시너지=1 · 드림캐쳐=100+ (EnqueueSynergyMul/_dcStackCounter 참조).
        private const ushort EffectTileStackId = 2;

        // effect-tiles unit 1 — 효과 타일 단일 진입점: dict 등록 + View 페인트. 셀당 1개(덮어쓰기).
        // 맵 빌드 seed 선정이 첫 client — 후속 런타임 생성 루트(드림캐쳐/유닛 능력)도 이 진입점 사용.
        public void AddEffectTile(Vector2Int cell, EffectTileData data)
        {
            if (data == null) return;
            _effectTilesByCell[cell] = data;
            if (tilemapMapView != null)
                tilemapMapView.SetEffectTile(cell, data.overlayTile);
            // unit 2 — 점유 셀 즉시 적용(순서 무관 불변식). 맵 빌드 시점엔 유닛이 없어
            // 현재는 후속 런타임 생성 루트(드림캐쳐/유닛 능력)에서만 도달. 재적용은 merge-key refresh 라 멱등.
            if (_defenderByTile.TryGetValue(cell, out var occupant))
                ApplyEffectTileIfAny(cell, occupant.entity);
        }

        // effect-tiles unit 2 — 셀에 효과 타일이 있으면 배치 유닛에 효과 부여 (기존 modifier 파이프라인).
        // source=배치유닛 + 전용 stackId=2 → merge-key 가 on-place(0)/시너지(1)와 분리 스택, 재호출은 refresh(멱등).
        // duration=∞ (시너지 관용) — 유닛 제거/재배치 기능이 없어 revocation 불요(spec 후속).
        // EffectTileData.op 를 존중해야 해서 중앙 EnqueueStatModifier(값 기준 op 결정, additive-authoring
        // Policy B) 를 우회하고 직접 enqueue — 타일이 저작한 op 를 그대로 쓴다(정책 non-goal, spec 참조).
        // unit 4 — 다중 stat: entries 루프. stat 이 다르면 같은 stackId 여도 슬롯 분리.
        private void ApplyEffectTileIfAny(Vector2Int cell, Entity entity)
        {
            if (!_effectTilesByCell.TryGetValue(cell, out var data) || data == null || data.effects == null) return;
            if (!_statModifierQueue.IsCreated || entity == Entity.Null) return;
            for (int i = 0; i < data.effects.Length; i++)
            {
                var e = data.effects[i];
                _statModifierQueue.Enqueue(new Wassup.Battle.Effects.StatModifierApplyEvent
                {
                    target    = entity,
                    stat      = e.stat,
                    op        = e.op,
                    magnitude = e.magnitude,
                    duration  = float.PositiveInfinity,
                    source    = entity,
                    stackId   = EffectTileStackId,
                    origin    = Wassup.Battle.Effects.ModifierOrigin.Tile,
                });
            }
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
            ApplyEffectTileIfAny(cell, entity); // effect-tiles unit 2 — 가드 뒤 exactly-once
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
            cube.transform.position = (Vector3)Wassup.Core.BoardSpace.ToView(new float3(worldPos.x, worldPos.y + tileSize * 0.5f, worldPos.z));
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
            // tilemap-view-backend 후속 — hazard 비주얼도 sim→view 변환 경계를 거친다(BoardSpace 경유).
            Vector3 hazardPos = (Vector3)Wassup.Core.BoardSpace.ToView(worldOrigin);
            var visual = Instantiate(so.visualPrefab, hazardPos, Quaternion.identity);
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
            {
                RecordBlockingHazard(so, cell, "spawn_rejected", "EffectSpawner rejected spawn");
                return entity;
            }

#if UNITY_EDITOR
            _em.SetName(entity, $"BlockingHazard_{so.name}_{cell.x}_{cell.y}");
#endif
            RecordBlockingHazard(so, cell, "spawn", string.Empty);

            if (so.visualPrefab == null)
            {
                Debug.LogWarning($"[BattleBridge] BlockingHazardSO '{so.name}' has no visualPrefab. Spawned hazard will be invisible.");
                return entity;
            }

            EnsureBlockingHazardVisualRoot();
            var p = _em.GetComponentData<LocalTransform>(entity).Position;
            // tilemap-view-backend 후속 — blocking hazard 비주얼도 sim→view 경계를 거친다(BoardSpace 경유).
            var visual = Instantiate(so.visualPrefab, (Vector3)Wassup.Core.BoardSpace.ToView(p), Quaternion.identity, _blockingHazardVisualRoot);
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

        // season-gimmick-overwork unit 3 — 피로도/번아웃 검증용 디버그 로그.
        // FatigueDebugMenu(에디터 메뉴)의 유일 창구 (절대 제약 1: ECS 접근은 BattleBridge 경유).
        public void DebugLogFatigueStacks()
        {
            if (!Application.isPlaying || !HasLiveEntityManager())
            {
                Debug.LogWarning("[FatigueDebug] Enter Play Mode with a live battle first.");
                return;
            }

            // 기믹 config 주입 여부 — 미주입이면 FatigueAccrualSystem 이 self-gate 로 안 돈다.
            var configQuery = _em.CreateEntityQuery(
                Unity.Entities.ComponentType.ReadOnly<Wassup.Battle.Effects.BurnoutGimmickConfig>());
            Debug.Log($"[FatigueDebug] BurnoutGimmickConfig 주입={!configQuery.IsEmpty} (season={SeasonRuntime.Active?.seasonId ?? "null"}, gimmick={_assignedGimmick?.gimmickId ?? "null"})");
            configQuery.Dispose();

            var query = _em.CreateEntityQuery(
                Unity.Entities.ComponentType.ReadOnly<Wassup.Battle.Units.DefenderUnitTag>());
            using var entities = query.ToEntityArray(Unity.Collections.Allocator.Temp);
            int logged = 0;
            foreach (var entity in entities)
            {
                byte fatigue = 0;
                if (_em.HasBuffer<Wassup.Battle.Effects.StackModifierSlot>(entity))
                {
                    var slots = _em.GetBuffer<Wassup.Battle.Effects.StackModifierSlot>(entity);
                    for (int i = 0; i < slots.Length; i++)
                        if (slots[i].kind == Wassup.Battle.Effects.StackKind.Fatigue)
                            fatigue = slots[i].stackCount;
                }
                var stats = _em.HasComponent<Wassup.Battle.Effects.ModifierStats>(entity)
                    ? _em.GetComponentData<Wassup.Battle.Effects.ModifierStats>(entity)
                    : default;
                var hp = _em.HasComponent<Wassup.Battle.Units.Health>(entity)
                    ? _em.GetComponentData<Wassup.Battle.Units.Health>(entity)
                    : default;
                Debug.Log($"[FatigueDebug] {entity} 피로도={fatigue} 공속x{stats.attackSpeedMul:F2} 공격x{stats.damageMul:F2} 최대체력x{stats.maxHealthMul:F2} HP={hp.value:F0}/{hp.max:F0}");
                logged++;
            }
            if (logged == 0) Debug.Log("[FatigueDebug] 배치된 defender 없음.");
            query.Dispose();
        }

        // season-gimmick-overwork unit 4 — 레드불 픽업 검증용 디버그 로그.
        public void DebugLogPickups()
        {
            if (!Application.isPlaying || !HasLiveEntityManager())
            {
                Debug.LogWarning("[PickupDebug] Enter Play Mode with a live battle first.");
                return;
            }

            var stateQuery = _em.CreateEntityQuery(
                Unity.Entities.ComponentType.ReadOnly<Wassup.Battle.Effects.PickupSpawnState>());
            Debug.Log($"[PickupDebug] PickupSpawnState 주입={!stateQuery.IsEmpty}");
            stateQuery.Dispose();

            var query = _em.CreateEntityQuery(
                Unity.Entities.ComponentType.ReadOnly<Wassup.Battle.Effects.Pickup>());
            using var pickups = query.ToComponentDataArray<Wassup.Battle.Effects.Pickup>(Unity.Collections.Allocator.Temp);
            Debug.Log($"[PickupDebug] 활성 픽업 {pickups.Length}개");
            int nonPlayable = 0;
            for (int i = 0; i < pickups.Length; i++)
            {
                var cell = pickups[i].cell;
                var tile = _generatedMap.IsCreated && IsInGeneratedMapBounds(cell)
                    ? _generatedMap.TileAt(cell).ToString() : "OOB";
                bool ok = tile == "Walk" || tile == "Place";
                if (!ok) nonPlayable++;
                Debug.Log($"[PickupDebug]   {pickups[i].kind} cell=({cell.x},{cell.y}) tile={tile}{(ok ? "" : " ⚠비이동/배치")} 남은수명={pickups[i].remainingLife:F1}s");
            }
            Debug.Log($"[PickupDebug] 이동/배치 외 타일 스폰 = {nonPlayable}개 (0 이어야 정상)");
            query.Dispose();
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
                    Instantiate(so.destructionVfxPrefab, (Vector3)Wassup.Core.BoardSpace.ToView(evt.worldPosition), Quaternion.identity);
                }

                _blockingHazardVisualMap.Remove(evt.hazardEntity);
                RecordBlockingHazardDestroyed(so, evt.worldPosition);
            }
        }

        private void RecordBlockingHazard(BlockingHazardSO so, Unity.Mathematics.int2 cell, string eventType, string reason)
        {
            if (so == null) return;
            int side = BlockingHazardLogSide(so);
            GameManager.Instance?.Logger?.RecordBlockingHazard(new Logging.BlockingHazardLog
            {
                event_type = eventType,
                hazard_id = so.name,
                tile = new Vector2Int(cell.x, cell.y),
                time = LogElapsedTime,
                width = side,
                height = side,
                hp = Mathf.RoundToInt(so.maxHp),
                reason = reason ?? string.Empty,
            });
        }

        private void RecordBlockingHazardDestroyed(BlockingHazardSO so, float3 worldPosition)
        {
            var cell = WorldToLogCell(worldPosition);
            int side = BlockingHazardLogSide(so);
            GameManager.Instance?.Logger?.RecordBlockingHazard(new Logging.BlockingHazardLog
            {
                event_type = "destroyed",
                hazard_id = so != null ? so.name : string.Empty,
                tile = cell,
                time = LogElapsedTime,
                width = side,
                height = side,
                hp = 0,
                reason = string.Empty,
            });
        }

        private Vector2Int WorldToLogCell(float3 worldPosition)
        {
            int2 grid = _generatedMap.IsCreated ? _generatedMap.gridSize : GridSize;
            var cell = GridMath.WorldToCell(worldPosition, tileSize, grid, origin: _boardOrigin);
            return new Vector2Int(cell.x, cell.y);
        }

        private static int BlockingHazardLogSide(BlockingHazardSO so)
        {
            if (so == null) return 0;
            return so.shape == HazardShape.SingleCell ? 1 : 3;
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

        // season-gimmick-overwork unit 2 — 활성 시즌의 기믹을 ECS config 싱글턴으로 주입.
        // SO 수치를 blittable 로 복사 (Burst 시스템이 SO 를 직접 만지지 않는다).
        // gimmick == null 이면 아무것도 만들지 않는다 = 기믹 시스템 전체 비활성.
        private void CreateGimmickConfigIfActive()
        {
            // gimmick-match-integration — 배정된 기믹 타입에 맞는 config 만 주입(둘은 상호배타).
            // 소스 = GameManager 배정 _assignedGimmick(BattleConfig.gimmickPool).
            // BuildMapForBattle 에서 매 맵빌드마다 호출되므로 재빌드/재진입 대비 기존 config 선제거
            // (idempotent — BuildPickupSpawnState 의 Teardown-first 와 동일 패턴).
            DestroyEntitiesByType<Wassup.Battle.Effects.BurnoutGimmickConfig>();
            DestroyEntitiesByType<Wassup.Battle.Effects.RedBullGimmickConfig>();

            if (_assignedGimmick is Wassup.Data.BurnoutGimmickData bd)
            {
                Debug.Log($"[GimmickConfig] BurnoutGimmickConfig 주입 (gimmick={bd.gimmickId})");
                var e = _em.CreateEntity();
                _em.AddComponentData(e, new Wassup.Battle.Effects.BurnoutGimmickConfig
                {
                    fatigueInterval       = bd.fatigueInterval,
                    fatigueAmount         = bd.fatigueAmount,
                    fatigueMaxStack       = bd.fatigueStack != null ? bd.fatigueStack.maxStack : (byte)5,
                    fatiguePerAppDuration = bd.fatigueStack != null ? bd.fatigueStack.perAppDuration : 25f,
                });
            }
            else if (_assignedGimmick is Wassup.Data.RedBullGimmickData rd)
            {
                Debug.Log($"[GimmickConfig] RedBullGimmickConfig 주입 (gimmick={rd.gimmickId})");
                var e = _em.CreateEntity();
                _em.AddComponentData(e, new Wassup.Battle.Effects.RedBullGimmickConfig
                {
                    redbullSpawnInterval  = rd.redbullSpawnInterval,
                    redbullLifetime       = rd.redbullLifetime,
                    redbullMaxActive      = rd.maxActivePickups,
                    lastRunAttackSpeedMul = rd.lastRunAttackSpeedMul,
                    lastRunDuration       = rd.lastRunDuration,
                    lastRunDamageFraction = rd.lastRunDamageFraction,
                });
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
            TeardownCurrentBattle();

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

        // nightmare-catcher unit 5 — boss spawn bake (병렬 경로): nightmareMechanics
        // 를 선언한 적이 곧 보스. BossTag + ThreatEntry(위협 테이블, unit 1) +
        // DcTriggerSlot 을 부착한다. defender 부착 API(ApplyDreamcatcherCardToUnit —
        // defender 가드 + 손패 회수 레지스트리)는 의도적으로 미사용: 보스 슬롯은
        // 손패 순환과 무관하고, teardown 은 AttackUnitTag 적 경로 상속(신규 0).
        private void BakeNightmareMechanics(Entity entity, AttackUnitData unitType)
        {
            var mechanics = unitType.nightmareMechanics;
            if (mechanics == null || mechanics.Length == 0) return;

            _em.AddComponent<BossTag>(entity);
            // 위협 테이블은 보스와 항상 동행 — 텔레포트 arm 의 타겟 소스.
            // defender 히트가 쌓기 전까지 빈 버퍼(ThreatHitEvent 드레인이 채움).
            _em.AddBuffer<ThreatEntry>(entity);
            var slots = _em.AddBuffer<DcTriggerSlot>(entity);

            for (int i = 0; i < mechanics.Length; i++) // bake-time only read (managed array)
            {
                var m = mechanics[i];
                if (m.trigger.kind == Wassup.Data.DcTriggerKind.None ||
                    m.payload.kind == Wassup.Data.DcPayloadKind.None)
                {
                    Debug.LogWarning($"[BattleBridge] {unitType.displayName} nightmare mechanic {i}: None kind — skipped.");
                    continue;
                }
                // 기존 트리거(AttackN/OnDamagedN/OnDeath)의 arm 은 defender 게이트
                // 미개방(spec unit 4) — 보스에 베이크하면 침묵 no-op 이 되므로
                // 사고 방지를 위해 명시 경고 후 스킵. 개방 시 이 가드를 함께 푼다.
                if (m.trigger.kind != Wassup.Data.DcTriggerKind.PeriodicTimer &&
                    m.trigger.kind != Wassup.Data.DcTriggerKind.HealthThreshold)
                {
                    Debug.LogWarning($"[BattleBridge] {unitType.displayName} nightmare mechanic {i}: trigger '{m.trigger.kind}' arm is defender-gated (미개방) — skipped.");
                    continue;
                }

                var slot = new DcTriggerSlot
                {
                    instanceId = _dcInstanceCounter++,
                    trigger = m.trigger.kind,
                    period = (ushort)math.clamp(m.trigger.period, 0, ushort.MaxValue),
                    counter = 0,
                    payload = m.payload.kind,
                    magnitude = m.payload.magnitude,
                    projectileDataIndex = -1,
                    tileRange = math.max(0, m.payload.tileRange),
                    // 트리거 상태(unit 5 append) — degenerate(<=0)는 트리거
                    // 순수함수의 내부 가드가 no-fire 처리(계약 9).
                    periodSeconds = m.trigger.periodSeconds,
                    fraction = m.trigger.fraction,
                    nextBoundaryIndex = 1,
                    maxHpRef = unitType.health,
                    duration = math.max(0f, m.payload.duration),
                };
                if (m.payload.kind == Wassup.Data.DcPayloadKind.AreaBarrage)
                {
                    // SkyFall 낙하 비주얼 필수 — Meteor 파이프라인 재사용(unit 2).
                    if (m.payload.projectile == null || m.payload.magnitude <= 0f)
                    {
                        Debug.LogWarning($"[BattleBridge] {unitType.displayName} nightmare mechanic {i}: AreaBarrage needs ProjectileData + positive magnitude — skipped.");
                        continue;
                    }
                    slot.projectileDataIndex = GetOrCreateProjectileDataIndex(m.payload.projectile);
                    slot.visualScale = m.payload.projectile.visualScale;
                }
                else if ((m.payload.kind == Wassup.Data.DcPayloadKind.SelfBlink ||
                          m.payload.kind == Wassup.Data.DcPayloadKind.AllyMoveSpeedAura) &&
                         m.payload.projectile != null)
                {
                    // rev 3 (실플레이 피드백) — blink 연출: hitPrefab 만 소비하는
                    // 퍼프 ProjectileData(투사체로는 안 뜀). null 이면 무연출.
                    // nightmare-whip-aura unit 3 — whip 펄스 연출도 같은 경로.
                    slot.projectileDataIndex = GetOrCreateProjectileDataIndex(m.payload.projectile);
                }
                // nightmare-whip-aura unit 3 rev 2 — 메커닉 선언 부착 오라(kind 무관):
                // 메커닉 데이터가 auraPrefab 을 선언하면 드림캐쳐 프레젠테이션 풀에
                // 등록, host 생존 동안 뷰를 따라다닌다. bridge 는 전달만(kind 분기 없음).
                if (m.payload.auraPrefab != null)
                {
                    _dcAuraPool ??= new Wassup.Presentation.DcAuraVisualPool(ResolveUnitViewTransform);
                    _dcAuraPool.Register(entity, m.payload.auraPrefab, m.payload.auraScale);
                }
                if (m.payload.kind == Wassup.Data.DcPayloadKind.AllyMoveSpeedAura &&
                    m.payload.duration <= m.trigger.periodSeconds)
                {
                    // nightmare-whip-aura unit 1 — authoring 계약: duration >
                    // periodSeconds (merge-refresh 유지). 위반은 펄스 사이 버프
                    // 만료(점멸) — 경고만, skip 하지 않는다(테스트 자유 유지).
                    Debug.LogWarning($"[BattleBridge] {unitType.displayName} nightmare mechanic {i}: AllyMoveSpeedAura duration({m.payload.duration}) <= periodSeconds({m.trigger.periodSeconds}) — 버프가 펄스 사이에 만료(점멸)합니다.");
                }
                slots.Add(slot);
            }
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
            // enemy-spawn-positioning 1 — 셀 중심에 sub-cell 측면 오프셋(진행방향 수직)을 더해 스폰 겹침 해소.
            // |오프셋|<0.5·tileSize 라 유닛은 같은 셀에 머문다 → flow/goal/cell-trim 등 셀 단위 시스템 불변.
            spawnWorldPos += ComputeSpawnLateralOffset(spawn);
            _em.AddComponentData(entity, LocalTransform.FromPositionRotationScale(spawnWorldPos, quaternion.identity, CharacterVisualScale));

            _em.AddComponent<AttackUnitTag>(entity);
            _em.AddComponentData(entity, new Health { value = entry.unitType.health, max = entry.unitType.health });
            _em.AddComponentData(entity, new FactionTag { value = Faction.Enemy });
            // dreamcatcher-awakening-hand unit 1 — bake the death grant so
            // DamageApplicationSystem can stamp it into EnemyKilledEvent.
            // Unconditional attach (0 allowed) keeps the lookup branch-free.
            _em.AddComponentData(entity, new AwakeningReward
            {
                value = Mathf.Max(0, entry.unitType.awakeningReward),
            });
            // Pre-attach empty buffers so downstream systems never need structural AddBuffer on hot paths.
            _em.AddBuffer<IncomingDamage>(entity);
            _em.AddBuffer<CcEffect>(entity);

            // nightmare-catcher unit 5 — 보스 분기 베이크. nightmareMechanics 없는
            // 일반 적은 이 호출이 즉시 return(무변경).
            BakeNightmareMechanics(entity, entry.unitType);

            // enemy-behavior-components Unit 2 — attackMethod decides attack components.
            // Defensive (Critic C1): Melee/Projectile with empty outputs → walk-only
            // (no AttackState), never a damage-0 attacker. All hit effects come
            // through outputs[] (AttackOutputElement).
            var attackMethod = entry.unitType.attackMethod;
            bool hasAttackOutputs = entry.unitType.outputs != null && entry.unitType.outputs.Length > 0;
            bool wantsAttack = attackMethod != Wassup.Data.EnemyAttackMethod.None;
            if (wantsAttack && !hasAttackOutputs)
            {
                Debug.LogWarning($"[BattleBridge] {entry.unitType.displayName}: attackMethod={attackMethod} but outputs empty — baked as walk-only.");
                wantsAttack = false;
            }
            if (wantsAttack)
            {
                _em.AddComponentData(entity, new AttackState
                {
                    range = entry.unitType.attackRange,
                    cooldownDuration = entry.unitType.attackCooldown,
                    cooldownRemaining = 0f,
                    attackTargetCount = Mathf.Max(1, entry.unitType.attackTargetCount),
                    targetMask = (int)(Faction.Defender | Faction.BlockingHazard),
                    hitDelaySec = entry.unitType.hitDelaySec,
                });
                var outputBuf = _em.AddBuffer<Wassup.Battle.Combat.AttackOutputElement>(entity);
                foreach (var output in entry.unitType.outputs)
                    outputBuf.Add(new Wassup.Battle.Combat.AttackOutputElement { value = output });

                if (attackMethod == Wassup.Data.EnemyAttackMethod.Projectile && entry.unitType.projectile != null)
                {
                    var dataIndex = GetOrCreateProjectileDataIndex(entry.unitType.projectile);
                    var axes = ResolveProjectileAxes(entry.unitType.projectile.flightMode);
                    _em.AddComponentData(entity, new ProjectileRef
                    {
                        dataIndex = dataIndex,
                        speed = entry.unitType.projectile.speed,
                        hitThreshold = entry.unitType.projectile.hitThreshold,
                        visualScale = entry.unitType.projectile.visualScale,
                        onHitEffect = entry.unitType.projectile.onHitEffect,
                        splashRadius = entry.unitType.projectile.splashRadius,
                        splashDamageMul = entry.unitType.projectile.splashDamageMul,
                        movement = axes.movement,
                        payload = axes.payload,
                        arcHeight = entry.unitType.projectile.arcHeight,
                        impactTileRange = entry.unitType.projectile.impactTileRange,
                    });
                }
            }

            // aggro-targeting Unit 1 — taunt-attack profile for enemies with no
            // normal outputs (Runner/Swift) so they can hit the guardian while
            // aggroed. AggroAssignmentSystem activates it on aggro, strips on release.
            if (entry.unitType.aggroAttackDamage > 0f)
                _em.AddComponentData(entity, new Wassup.Battle.Combat.AggroAttackProfile
                {
                    damage = entry.unitType.aggroAttackDamage,
                    cooldown = entry.unitType.aggroAttackCooldown,
                    range = entry.unitType.aggroAttackRange,
                });

            // enemy-behavior-components Unit 2 — behavior + filter from SO (enemyClass
            // hardcode removed). EnemyBehavior drives targeting/aim; FocusTarget is
            // pre-attached for FocusUntilDead (AttackSystem only writes its value).
            _em.AddComponentData(entity, new Wassup.Battle.Combat.EnemyBehavior
            {
                targetMode = entry.unitType.targetMode,
                // enemy-ai-fsm — SO 의 engageMovement 직접 bake(값 세팅은 unit 4 SO 마이그레이션).
                engageMovement = entry.unitType.engageMovement,
            });
            // enemy-ai-fsm Unit 0 — FSM 상태 초기값. EnemyAiStateSystem(unit 1)이 매 틱 갱신.
            _em.AddComponentData(entity, new Wassup.Battle.Combat.EnemyAiState
            {
                value = Wassup.Battle.Combat.AiState.Marching,
            });
            if (entry.unitType.targetMode == Wassup.Data.EnemyTargetMode.FocusUntilDead)
                _em.AddComponentData(entity, new Wassup.Battle.Combat.FocusTarget { current = Entity.Null });

            int priorityClass = entry.unitType.targetPriorityClass == Wassup.Data.DefenderClass.None
                ? -1
                : (int)entry.unitType.targetPriorityClass;
            _em.AddComponentData(entity, new Wassup.Battle.Combat.EnemyTargetFilter
            {
                classMask = (int)entry.unitType.targetClassMask,
                priorityClass = priorityClass,
            });

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
                damageVsCcMul  = 1f, // dreamcatcher-new-abilities unit 2 — base 1 (dirty 는 disabled 로 추가돼 무-모디파이어 유닛은 집계가 안 돌므로 여기서 필수)
                maxHealthMul   = 1f, // season-gimmick-overwork unit 1 — base 1 (동일 사유)
            });
            _em.AddComponent<Wassup.Battle.Effects.ModifierStatsDirty>(entity);
            _em.SetComponentEnabled<Wassup.Battle.Effects.ModifierStatsDirty>(entity, false);
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
