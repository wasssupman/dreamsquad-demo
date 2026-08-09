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
        [Header("Map Grid")]
        // random-map-pool — (맵, 덱) 인코운터 풀. 맵 생산의 유일 경로(map-pipeline-cleanup unit 2
        // 에서 legacy 소스 제거). 엔트리 하나를 골라 맵·덱을 함께 확정한다(맵마다 그 맵의 적 패턴).
        [SerializeField] private MapDocumentPool mapPool;
        // endless-mode unit 2 — 무한 모드 전용 (맵, 덱) 인카운터. 공용 mapPool 에 넣지 않아
        // 랜덤/토너먼트 맵 선택이 절대 안 뽑는다(계약 5). DevMapOverride.Endless 로만 진입.
        [SerializeField] private MapDocumentPool.Entry endlessEncounter;
        // 비0 = 맵 시드 고정(매판 동일 맵/인덱스 핀). 0 = 토너먼트 시드 결정론(부재 시 0번 폴백).
        [SerializeField] private int fixedMapSeed = 20260719;
        [Header("Season")]
        [SerializeField] private SeasonRegistry seasonRegistry;
        [SerializeField] private float tileSize = 1f;
        [SerializeField] private float spawnHeight = 0.5f;
        // continuous-agent-movement unit 3·12 — 적 충돌 반지름(타일 배수).
        // 0 으로 두면 기존 점 충돌(셀 경계 clamp)로 되돌아간다 — 회귀 시 스위치.
        //
        // unit 12 로 0.35 → 0.25. 0.35 는 "지름 0.7 < 1.0 이라 1타일 복도 통과 가능"으로
        // 정했는데 그 검산이 **단독 통과만** 봤다. 폭1 복도에서 전진하려면 몸통이 통로 한
        // 줄에만 걸쳐야 해서 중심선 ±(0.5−r) 안에 있어야 하는데, r=0.35 면 그 여유가 0.15 로
        // 겹침 해소의 측면 밀어냄(최대 r)보다 작다 → 밀어냄이 유닛을 **전진 불가능한 자리로
        // 보낸다**. 앞이 비면 스스로 벽면 슬라이드로 복귀하지만(unit 11), 앞에 마개(교전 정지
        // 유닛·정면 합류)가 있으면 복귀할 틈이 없어 굳는다. 실측(16기·선두 1기 정지):
        // r=0.35 는 로테이션 6맵 중 4맵에서 100초 교착, r=0.25 는 6/6 통과·최장 정체 1~5프레임.
        [SerializeField, Range(0f, 0.49f)] private float agentRadiusTiles = 0.25f;
        [SerializeField] private ResultScreen resultScreen;
        // battle-score-formula unit 3 — 최종 점수 배점. 미배선이면 기본값(100/900)으로
        // 진행하되 LogError 를 남긴다 — 조용히 0점이 되는 게 최악이다.
        [SerializeField] private ScoreRulesData scoreRules;
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
        // unit-overhead-ui — 레거시 체력/드림캐쳐 표현과 신규 공통 오버헤드 경로 전환.
        [SerializeField] private Wassup.Data.UnitHealthPresentationMode unitHealthPresentationMode =
            Wassup.Data.UnitHealthPresentationMode.Legacy;
        [SerializeField] private Wassup.Presentation.UnitOverheadUiLayer unitOverheadUiLayer;
        // beam-ranger-defender unit 1 — 지속 빔(버스터즈). 씬에 배선하면 인스펙터에서 TTL/추종
        // 속도를 튜닝할 수 있고, 비어 있으면 첫 사용 시 기본값으로 자동 생성한다(EnsureBeamPresenter).
        // 자동 생성 폴백을 두는 이유: 이 기능만을 위해 공용 씬을 저장하면 그 시점의 미저장 WIP 가
        // 같이 박힌다. 튜닝이 필요해지면 그때 씬에 배선하면 된다.
        [SerializeField] private Wassup.Presentation.BeamPresenter beamPresenter;

        // 빔 세션 TTL = 실발사 주기 × 이 계수. 사건이 한 틱 늦어도 끊기지 않을 여유이며
        // 무차원이라 어떤 주기의 빔 유닛에도 그대로 맞는다(유닛 스탯이 아니므로 SO 대상 아님).
        private const float BeamSessionTtlMargin = 1.75f;
        [SerializeField] private Wassup.UI.ScoreHudView scoreHud;
        // score-tally-sequence unit 2 — 결과 연출(점수 합산). 미배선이면 연출을 건너뛰고
        // 곧장 결과 화면으로 간다 — 연출은 곁가지, 결과 화면은 필수다.
        // boss-wave-cadence unit 2 — 보스 스폰 순간 "꿈결 위기!!" 경보. BakeNightmareMechanics
        // 의 보스 확정(BossTag 부착) 단일 지점에서 구동. 미배선(null)이면 무동작.
        [SerializeField] private Wassup.UI.BossWarningView _bossWarning;
        [SerializeField] private Wassup.Presentation.ProjectileViewPool _projectileViewPool;
        private readonly System.Collections.Generic.List<Entity> _projectileViewScratch = new(8);
        // Phase 9 P9-07 — tileSize 단일 소스화. Awake 에서 PlacementInput 으로 주입.
        [SerializeField] private Wassup.Core.PlacementInput placementInput;
        [Header("Tilemap View Backend (tilemap-view-backend)")]
        [SerializeField] private Wassup.Core.BoardViewMode boardViewMode = Wassup.Core.BoardViewMode.TilemapRect;
        [SerializeField] private Wassup.Core.TilemapMapView tilemapMapView;
        // three-minute-survival unit 1 — 안정도 바를 골 앵커에서 월드 Y 로 띄우는 양.
        // 구조물 메쉬가 셀 중심보다 높아서 바가 메쉬를 파고드는 것을 막는다. 씬 배선 불요
        // (신규 SerializeField 는 기존 씬에서 이 initializer 를 받는다).
        [SerializeField] private float goalStabilityBarLift = 1.6f;
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
        // flight-lift-feel unit 1 — 뜬 높이(lift)의 시각 반응. **화면 전역 단일 소유**다:
        // 스케일은 연출별 취향이 아니라 원근 보상이라, 같은 높이의 두 유닛이 다른 크기로 보이면
        // 카메라가 깨져 보인다. 연출별 노브(DragSwaySettings ⑩ / BattleBridge.BossLeap)에 복제 금지.
        // 소비처 4개 공통: 드롭 하마 · 보스 도약 · 재배치 던지기 · 넉업 hop.
        [Header("Lift visual response (flight-lift-feel unit 1)")]
        [Tooltip("월드 높이 1당 유닛 확대율. 0 = 반응 없음(현행). 카메라 축이 시선에 수직이라 원근 확대가 0인 것을 보상한다.")]
        [Range(0f, 0.5f)]
        [SerializeField] private float liftScalePerHeight = 0.14f;
        [Tooltip("유닛 확대 상한. 1 미만은 무시된다(축소 방지).")]
        [Range(1f, 2f)]
        [SerializeField] private float liftScaleMax = 1.35f;
        [Tooltip("그림자가 최소 크기/알파에 닿는 높이(월드). 이 위로는 더 안 줄어든다.")]
        [SerializeField] private float liftShadowFullHeight = 3f;
        [Tooltip("최대 높이에서의 그림자 크기 배율.")]
        [Range(0.1f, 1f)]
        [SerializeField] private float liftShadowMinScale = 0.55f;
        [Tooltip("최대 높이에서의 그림자 알파 배율.")]
        [Range(0f, 1f)]
        [SerializeField] private float liftShadowMinAlpha = 0.35f;
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

        [Header("Season Gimmick — Resignation View (season-gimmick-clockout unit 1)")]
        [Tooltip("사직서 뷰 프리팹. 비우면 절차적 플레이스홀더(흰 종이).")]
        [SerializeField] private GameObject resignationViewPrefab;
        [Tooltip("사직서 뷰의 셀 중심 위 높이(월드).")]
        [SerializeField] private float resignationViewHeight = 0.2f;

        private GeneratedMap _generatedMap;

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
        // dot-effect-extraction unit 1 — 지속 피해 오라 소스. 적·아군 공통이라 태그 게이트 없음.
        // 도트가 origin·element 를 들고 다니므로 종류 래치가 필요 없다.
        private EntityQuery _dotEffectQuery;
        private bool _dotEffectQueryCreated;
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
        // summon-patrol-defender unit 3 — 순찰병 SO 레지스트리. DefenderUnitData 는 managed 라
        // SummonerState 에 담을 수 없어 인덱스로 실어 나른다(_zoneHazardRegistry 와 동형).
        private readonly List<DefenderUnitData> _patrolUnitRegistry = new();
        private readonly Dictionary<DefenderUnitData, int> _patrolUnitIndex = new();
        private readonly List<BlockingHazardSO> _blockingHazardSoRegistry = new();
        private readonly Dictionary<BlockingHazardSO, int> _blockingHazardSoIndex = new();
        private static readonly Dictionary<Wassup.Battle.Effects.StackKind, Wassup.Data.ThresholdRule[]> _stackThresholds = new();
        private readonly Dictionary<Entity, GameObject> _blockingHazardVisualMap = new();
        private Transform _blockingHazardVisualRoot;
        // season-gimmick-overwork unit 6 — 레드불 픽업 엔티티↔뷰 GameObject 매핑.
        private readonly Dictionary<Entity, GameObject> _pickupVisualMap = new();
        // 조정 시 제거 대상 임시 버퍼 (반복 중 수정 회피, 매 프레임 재사용).
        private readonly List<Entity> _pickupReapBuffer = new();
        // season-gimmick-clockout unit 1 — 사직서 엔티티↔뷰 매핑 (Pickup 뷰 동형).
        private EntityQuery _resignationViewQuery;
        private bool _resignationViewQueryCreated;
        private readonly Dictionary<Entity, GameObject> _resignationVisualMap = new();
        private readonly List<Entity> _resignationReapBuffer = new();
        // dreamcatcher-orb-dock unit 6 — 킬 각성 피규어를 "죽은 적 스킨"으로 렌더하기 위한
        // Entity→적 데이터 등록부(스폰 시 기록, 킬 드레인 시 조회+제거). 파괴된 Entity 값도
        // 키 비교는 유효(역참조 안 함 — SO 참조만 보관). teardown 에서 Clear.
        private readonly Dictionary<Entity, AttackUnitData> _enemyTypeByEntity = new();
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
        // flight-lift-feel unit 1 — 코드 기본값이 곧 초기값이라 미배선 씬에서도 동작한다.
        public static float LiftScalePerHeight { get; private set; } = 0.14f;
        public static float LiftScaleMax { get; private set; } = 1.35f;
        public static float LiftShadowFullHeight { get; private set; } = 3f;
        public static float LiftShadowMinScale { get; private set; } = 0.55f;
        public static float LiftShadowMinAlpha { get; private set; } = 0.35f;

        private void MirrorLiftKnobs()
        {
            LiftScalePerHeight = liftScalePerHeight;
            LiftScaleMax = liftScaleMax;
            LiftShadowFullHeight = liftShadowFullHeight;
            LiftShadowMinScale = liftShadowMinScale;
            LiftShadowMinAlpha = liftShadowMinAlpha;
        }
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
        // nextwave-clear-attention unit 0 — 이미 호출된 모든 웨이브의 pending/live 합집합이
        // 비었는지 BattleBridge 가 판정한다. UI 는 아래 read-only getter 만 폴링한다.
        // wave-pattern unit 9 — Next Wave 강제 호출로 앞당긴 누적 시간(앞당김이므로 음수).
        // 플랜의 triggerTimeSec 자체는 불변(브리핑 스트립·로그의 source of truth)이고,
        // 런타임 스케줄만 이 오프셋으로 민다. 남은 웨이브 전체가 같은 값만큼 이동하므로
        // 웨이브 간 간격이 보존되고, 강제 호출 뒤 다음 웨이브는 "호출 시점 + 원래 간격"에 나온다.
        private float _waveTimeShift;
        // three-minute-survival unit 2 — 현재 웨이브가 트리거된 Battle 클럭 시각. 상한 간격의
        // 기준이다(스폰 완료 시각이 아니라 **트리거** 시각). 시계와 함께 리셋된다.
        private float _waveStartSec;
        private int _goalReachedCount;
        // subconscious-curse-expansion unit 1 (몽마의 계약) — 유출 허용치 선불 지불의
        // 런타임 오프셋. SO(deck.defeatGoalReachedCount)는 절대 불변 — 직접 감소시키면
        // 에디터 자산 영구 오염 + 기기에서 매치 간 누적된다(spec critic M1). 매치 리셋
        // (BeginPlacement)에서 0 초기화. 환불 경로 없음(§6 세탁 차단).
        private int _leakAllowancePenalty;
        // battle-score-formula unit 2 — 실제 처치분 누적(유출된 적은 포함되지 않는다).
        // **계약 9: _battleClock 이 0 이 되는 모든 지점에서 함께 0 이 되어야 한다.**
        // _goalReachedCount 처럼 BeginPlacement 에만 두면, teardown 없는 StartBattle
        // 재호출에서 시계만 리셋되고 이 값은 이월돼 이전 판 점수가 얹힌다.
        private int _killScoreTotal;
        // three-minute-survival unit 3 — 처치 **마리 수**(점수와 별개 축, 결과 화면 표기용).
        // 계약 9: _killScoreTotal 과 같은 지점에서 함께 0 이 된다.
        private int _killCount;
        // three-minute-survival unit 0 — 골 안정도. **브리지가 소유하는 값**이다: 유출 1회당
        // 즉발 차감이라 시뮬 상태가 필요 없어 ECS 컴포넌트/시스템을 만들지 않는다(적이 골에
        // 살아남아 때리는 지속 피해 모델은 goal-tower-siege spec 의 몫).
        // 유출한 적의 AttackUnitData.stabilityDamage 만큼 깎이고 0 이면 패배다.
        // **계약 9(_killScoreTotal 과 같은 규칙)**: 시계가 0 이 되는 지점마다 만피로 돌아간다.
        private int _goalStability;
        private int _goalStabilityMax;
        // 유출 적의 등록부 조회 실패 경고를 판당 1회로 제한(로그 폭주 방지).
        private bool _leakTypeMissLogged;
        // goal-tower-siege unit 1 — 타워 부재 경고도 판당 1회.
        private bool _towerMissLogged;
        // goal-tower-siege(rev 2) — 이번 판에 세운 타워 수. 살아있는 수가 이보다 적으면
        // 하나가 부서진 것 = 패배. 표준 사망 경로가 엔티티를 지우므로 이 비교가 곧 판정이다.
        private int _goalTowerCount;
        // battle-structures unit 4(ⓐ, 사용자 확정 2026-08-09) — 붕괴는 **셀 단위**다.
        // 구 `_goalBreached`(bool)는 골 2개 맵에서 하나만 부서져도 전역 전환이라 계약 7
        // («무너진 마음의 셀만 열리고 나머지는 선다»)을 표현할 수 없었다. 매치 경계에서 Clear.
        private readonly HashSet<Vector2Int> _breachedCells = new();
        private NativeQueue<GoalReachedEvent> _goalEventQueue;
        private NativeQueue<DefenderDeathEvent> _defenderDeathQueue;
        // dreamcatcher-shield-break unit 0 — 실드 피격 파열 이벤트 채널(Units→Bridge).
        private NativeQueue<ShieldBreakEvent> _shieldBreakQueue;
        private NativeQueue<Wassup.Battle.Combat.UnitAttackVisualEvent> _unitAttackVisualQueue;
        private NativeQueue<Wassup.Battle.Combat.Projectile.ProjectileHitEvent> _projectileHitEventQueue;
        // aggro-targeting Unit 11 — Combat(AttackSystem)→Effects(AggroStateSystem) 히트 채널.
        private NativeQueue<Wassup.Battle.Effects.AggroHitEvent> _aggroHitEventQueue;
        // attack-decoupling unit 4 — Effects(HazardCastSystem)→Combat(AttackSystem) 캐스트 사건.
        private NativeQueue<Wassup.Battle.Combat.CastEvent> _castEventQueue;
        // use-flow unit 3 — Combat→Bridge 부착 카드 발동 신호(머리 위 아이콘 행 펄스).
        private NativeQueue<Wassup.Battle.Combat.DcTriggerFiredEvent> _dcTriggerFiredQueue;
        // knockup-fighter unit 3 — Combat→Bridge 넉업 띄우기 연출(대상 view 수직 호핑).
        private NativeQueue<Wassup.Battle.Combat.KnockupVisualEvent> _knockupVisualQueue;
        // nightmare-catcher unit 1 — Combat→Combat 보스 위협 귀속 채널.
        private NativeQueue<Wassup.Battle.Combat.ThreatHitEvent> _threatHitEventQueue;
        // nightmare-catcher unit 3 — Combat→Movement 텔레포트(SelfBlink) 요청 채널.
        private NativeQueue<Wassup.Battle.Movement.BlinkRequestEvent> _blinkRequestQueue;
        private NativeQueue<Wassup.Battle.Units.HealAppliedEvent> _healAppliedEventQueue;
        // shield-guardian-defender unit 4 — Effects→Presentation 실드 부여 원샷 VFX 채널.
        private NativeQueue<Wassup.Battle.Effects.ShieldGrantedEvent> _shieldGrantedEventQueue;
        private NativeQueue<Wassup.Battle.Units.DamageNumberEvent> _damageNumberEventQueue;
        private NativeQueue<Wassup.Battle.Units.EnemyKilledEvent> _enemyKilledEventQueue;
        private NativeQueue<Wassup.Battle.Effects.EnemyCcEvent> _enemyCcQueue;
        // dot-effect-extraction unit 0 — 지속 피해 부여 채널(25번째). CC 와 페이로드를 섞지 않는다.
        private NativeQueue<Wassup.Battle.Effects.DotApplyEvent> _dotApplyQueue;
        // combat-action-lock unit 3 — wake-on-hit(Sleep 해제) Units→Effects 채널.
        private NativeQueue<Wassup.Battle.Effects.CcClearRequest> _ccClearQueue;
        private NativeQueue<Wassup.Battle.Effects.StatModifierApplyEvent> _statModifierQueue;
        private NativeQueue<Wassup.Battle.Effects.StackModifierApplyEvent> _stackModifierQueue;
        private NativeQueue<Wassup.Battle.Effects.HazardRuntimeEvent> _hazardRuntimeEventQueue;
        private NativeQueue<Wassup.Battle.Effects.HazardDestroyedEvent> _hazardDestroyedQueue;
        // goal-stability unit 4 — 골 붕괴 채널(Units→Bridge). 연출/로그 전용.
        private NativeQueue<GoalCollapsedEvent> _goalCollapsedQueue;
        private NativeQueue<Wassup.Battle.Effects.HazardSpawnRequest> _hazardSpawnRequestQueue;
        private NativeQueue<Wassup.Battle.Combat.AttackOutputLogEvent> _attackOutputLogQueue;
        // season-gimmick-clockout unit 3 — 메테오 barrage 요청 채널(Effects→Bridge).
        private NativeQueue<Wassup.Battle.Effects.MeteorBarrageRequest> _meteorBarrageRequestQueue;
        // season-gimmick-clockout unit 4 — 메테오 착탄 Walk 셀 선택 결정론 rng(matchSeed 파생, 매치당 seed).
        private Unity.Mathematics.Random _meteorRng;
        private Unity.Collections.NativeHashSet<Unity.Mathematics.int2> _blockedCells;
        private Unity.Collections.NativeParallelMultiHashMap<Unity.Mathematics.int2, Wassup.Battle.Effects.HazardEffect> _hazardCellToEffects;

        // continuous-agent-movement unit 0 — 판 단위 sim 필드 3종의 핸들.
        // goal flow field(Phase 9) / 방어유닛-지향 필드(boss-defender-field unit 1) /
        // 레드불 픽업 스폰 상태(season-gimmick-overwork unit 4)는 라이프사이클을 공유한다:
        // BuildFlowField·BuildPickupSpawnState 에서 서고 TeardownFlowField 에서 같이 죽는다.
        // 할당/해제 구현은 SimFieldInstaller. 내용 갱신은 DefenderFieldSystem(defender field).
        private SimFieldHandles _simFields;

        // enemy-tile-movement-integrity unit 0 — 스폰 측면 분산 순번(맵 빌드마다 0 리셋). 결정론 수열 인덱스.
        private int _spawnSpreadCounter;

        // map-origin-placement: board 월드 원점. 모든 grid↔world 변환의 단일 소스.
        // Tilemap 모드는 무조건 zero (BuildMapForBattle 에서 고정).
        private float3 _boardOrigin = float3.zero;
        // battle-structures unit 4 — 거점 등록부(Bridge 가 스폰 주체라 직접 안다).
        // 구 _goalGaugeList(goal-stability unit 5)의 부활·일반화 — 리뷰 M-e 의 «writer 0» 처분.
        // 게이지 폴링 + 붕괴 감지(ⓐ: 사라진 엔티티의 셀 특정)가 소비한다. 쿼리 없이 맵당 소수 순회.
        private readonly List<(Entity entity, Vector2Int cell, Faction faction)> _structureRegistry = new();
        // unit 4 — 저작 거점의 뷰 인스턴스(SO.viewPrefab). Pickup 프레젠터 선례: 브리지가
        // 만들고 teardown 이 지운다. 골 타워 프랍은 기존 경로(MapThemeData.goalStructureProp) 유지.
        private readonly List<GameObject> _structureViews = new();
        [Tooltip("골 오버헤드 게이지가 뜨는 구조물 높이(월드 유닛) — 유닛 체력바와 같은 창에 투영")]
        [SerializeField] private float goalOverheadHeight = 1.1f;

        // match-seed-unification — GameManager 가 주입하는 단일 매치 시드.
        // 맵/웨이브/비주얼 시드가 여기서 파생된다(작업 2/3). 0 = 미주입(즉석 폴백).
        private int _matchSeed;
        public void SetMatchSeed(int seed) => _matchSeed = seed;

        // random-map-pool unit 1 — BuildMapForBattle 이 풀에서 고른 덱. 미해결(빌드 전)이면 serialized deck 폴백.
        // 모든 덱 소비는 ActiveDeck 경유. public = 브리핑 스트립이 선택된 덱을 읽어 브리핑=실전 일치(unit 4).
        private AttackDeck _resolvedDeck;
        public AttackDeck ActiveDeck => _resolvedDeck != null ? _resolvedDeck : deck;

        // battle-structures unit 3 — 풀에서 고른 맵 문서를 들고 있는다(_resolvedDeck 과 대칭).
        // 거점 스탯(체력·프랍·공격)은 GeneratedMap 이 실을 수 없는 SO 참조라, 스폰(unit 4)이
        // 저작 엔트리를 다시 읽을 창구가 필요하다. 빌드가 끝나면 사라지던 지역 변수였다.
        private Wassup.Data.MapGrid.MapDocument _resolvedMapDoc;

        // endless-mode unit 2 — 현재 배틀이 무한 모드인가. BattleBridge 만 이 값으로 분기한다
        // (진입/간격은 데이터 구동, 누수/시간축/토너먼트 리포트는 아래 각 지점에서 이 플래그로).
        private bool IsEndless => ActiveDeck != null && ActiveDeck.battleMode == BattleMode.Endless;

        // random-map-pool unit 6 — draft 브리핑 스트립이 실전과 동일한 플랜을 프리뷰하도록.
        // TryInitializeGeneratedWaves 의 생성 경로와 같은 ActiveDeck·seed 로직 미러(authored-plan 제외).
        // draft 시점엔 _matchSeed·ActiveDeck 확정(PrepareDraftMap 선행) → 브리핑=실전 결정론적 동일.
        // ActiveDeck null/비생성이면 default(waves==null) 반환 → 스트립이 정적 deck 폴백.
        public GeneratedWavePlan BuildBriefingWavePlan()
        {
            var d = ActiveDeck;
            if (d == null || !d.useGeneratedWaves) return default;
            int waveSeed = d.waveSeed != 0
                ? d.waveSeed
                : Wassup.Core.MatchSeed.DeriveWaveSeed(_matchSeed != 0 ? _matchSeed : 1);
            return WavePatternGenerator.Generate(d, waveSeed);
        }

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
            ApplyUnitHealthPresentationMode();
        }

        private void OnValidate()
        {
            // Keep the static mirror in sync while tuning in the inspector (edit/play).
            CharacterBillboardTilt = tilemapBillboardTilt;
            if (Application.isPlaying) ApplyUnitHealthPresentationMode();
        }

        private bool UnifiedOverheadActive =>
            unitHealthPresentationMode == Wassup.Data.UnitHealthPresentationMode.UnifiedOverhead
            && unitOverheadUiLayer != null;

        private void ApplyUnitHealthPresentationMode()
        {
            bool unified = UnifiedOverheadActive;
            if (unitHealthPresentationMode == Wassup.Data.UnitHealthPresentationMode.UnifiedOverhead
                && unitOverheadUiLayer == null)
                Debug.LogError("[BattleBridge] UnifiedOverhead mode인데 UnitOverheadUiLayer가 미할당 — Legacy로 폴백.", this);
            dcIconStripSpawner?.SetPresentationEnabled(!unified);
            if (!unified) unitOverheadUiLayer?.Clear();
            if (unified)
            {
                enemyHitBarSpawner?.Clear();
                tileHealthGaugeLayer?.Clear();
                unitOverheadUiLayer.RefreshAttachments();
            }
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
            // defender-placement-cooldown 0 — 매치 경계 방어적 리셋(정상 경로는 배치 페이즈 진입에서 처리).
            if (GameManager.Instance != null && GameManager.Instance.CooldownRuntime != null)
                GameManager.Instance.CooldownRuntime.ResetAll();
            if (spineUnitPool != null) spineUnitPool.DisposeAll();
            if (enemyViewPool != null) enemyViewPool.DisposeAll();
            if (defenderFallbackViewPool != null) defenderFallbackViewPool.DisposeAll();
            if (tileHealthGaugeLayer != null) tileHealthGaugeLayer.Clear(); // unit 3 — 게이지 전체 정리
            _structureRegistry.Clear(); // battle-structures unit 4 — 거점 등록부도 같은 지점에서
            ClearStructureViews();      // 리뷰 H-4 — restart 경로는 DestroyStructureEntities 를 안 거쳐
                                        // 지난 판 프랍이 배치 페이즈 내내 남는다(픽업/사직서 뷰와 같은 사고 유형)
            _resolvedMapDoc = null;     // 리뷰 H-3 — 지난 판 문서의 거점이 다음 판에 스폰되는 것 + SO 앱수명 참조 방지
            if (enemyHitBarSpawner != null) enemyHitBarSpawner.Clear(); // unit 2 — 잔여 마이크로바 정리(생명주기 대칭)
            if (statusFxSpawner != null) statusFxSpawner.Clear(); // unit-status-fx unit 2 — 잔여 상태 연출 정리
            if (dcIconStripSpawner != null) dcIconStripSpawner.Clear(); // unit-dreamcatcher-icons — 잔여 아이콘 스트립 정리(생명주기 대칭)
            unitOverheadUiLayer?.Clear(); // unit-overhead-ui — 공통 health/card view 정리
            ClearPickupVisuals(); // season-gimmick-overwork unit 6 — 잔여 레드불 뷰 정리
            ClearResignationVisuals(); // season-gimmick-clockout unit 1 — 잔여 사직서 뷰 정리
            ClearAllyBuffZonePaint(); // active-ally-zone unit 2 — 잔여 장판 점등 정리(생명주기 대칭)
            _enemyTypeByEntity.Clear(); // dreamcatcher-orb-dock unit 6 — 적 데이터 등록부 정리
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
                // unit 0 의 의도적 델타: 이전에는 3개 중 2개만 되돌려 pickup 핸들이 stale 하게
                // 남았다(가드에 막혀 무해했으나 비대칭). Reset 은 3개를 모두 되돌린다.
                _simFields.Reset();
            }

            DisposeEcsInfrastructureNativeContainers();
            DisposeCachedQueries();
            _zoneHazardRegistry.Clear();
            _zoneHazardIndex.Clear();
            _blockingHazardSoRegistry.Clear();
            _blockingHazardSoIndex.Clear();
            // summon-patrol-defender — 순찰병 SO 레지스트리도 형제와 같은 두 지점에서 비운다.
            // 인덱스는 판마다 bake 되므로 안전하고, 안 비우면 managed SO 참조를 앱 수명으로
            // 붙들어 에셋 언로드를 막는다.
            _patrolUnitRegistry.Clear();
            _patrolUnitIndex.Clear();

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
            // season-gimmick-clockout unit 8 — 사직서 엔티티 정리. 누락 시 월드(앱 수명 default
            // world)에 잔존해, 로비 경유 재진입 시 ReconcileResignationViews 가 옛 엔티티의 뷰를
            // 다시 만들어 사직서가 남아 보인다(+ threshold 카운트도 오염).
            DestroyEntitiesByType<Wassup.Battle.Effects.Resignation>();
            // active-ally-zone unit 0 — 아군 버프 장판 캐리어 정리. 누락 시 위 사직서와 같은 사고가
            // 난다: 매치 종료 직전에 깐 장판(6~8초)이 앱 수명 default world 에 남아, 다음 매치에서
            // AllyBuffFieldSystem 이 **옛 centerCell** 로 다시 버프를 걸어 보이지 않는 강화 구역이 된다
            // (뷰 등록부는 매치 경계에서 비워지므로 점등도 없다).
            DestroyEntitiesByType<Wassup.Battle.Effects.AllyBuffField>();
            // goal-tower-siege — 골 타워 정리. 누락하면 앱 수명 default world 에 남아
            // 다음 매치의 EnsureGoalTowers 가 지우기 전까지 로비에서도 살아 있고,
            // 살아있는 수 비교(_goalTowerCount)로 패배를 판정하는 규칙이 오염된다.
            DestroyEntitiesByType<Wassup.Battle.Units.GoalTowerTag>();
            // battle-structures unit 4 — 저작 거점(본능/적 마음)도 같은 이유로 등재.
            DestroyEntitiesByType<Wassup.Battle.Units.StructureTag>();
            _goalTowerCount = 0;
            _structureRegistry.Clear();
            // summon-patrol-defender unit 2 — 거점 순찰 아군 정리. DefenderUnitTag 로 이미
            // 걸리지만 중복으로 등재해 둔다: 위 사직서/AllyBuffField 사고가 정확히 "정리 목록에
            // 안 넣어서" 났고, 이 아키타입은 태그 구성이 일반 방어유닛과 달라 나중에 누가
            // DefenderUnitTag 를 떼면 조용히 새기 때문이다.
            DestroyEntitiesByType<Wassup.Battle.Movement.PatrolAnchor>();
            // summon-patrol-defender unit 3 — 소환 요청 캐리어. 보통 같은 프레임 드레인에서
            // 죽지만, stage 와 drain 사이에 전투가 멈추면 낙오분이 남는다(투사체 캐리어 선례).
            DestroyEntitiesByType<Wassup.Battle.Combat.PatrolRequestCarrier>();
        }

        private void DestroyEcsInfrastructureEntities()
        {
            DestroyEntitiesByType<GoalReachedEventsSingleton>();
            DestroyEntitiesByType<DefenderDeathEventsSingleton>();
            DestroyEntitiesByType<ShieldBreakEventsSingleton>();
            DestroyEntitiesByType<Wassup.Battle.Combat.UnitAttackVisualEventsSingleton>();
            DestroyEntitiesByType<Wassup.Battle.Combat.Projectile.ProjectileHitEventsSingleton>();
            DestroyEntitiesByType<Wassup.Battle.Units.HealAppliedEventsSingleton>();
            DestroyEntitiesByType<Wassup.Battle.Effects.ShieldGrantedEventsSingleton>();
            DestroyEntitiesByType<Wassup.Battle.Units.DamageNumberEventsSingleton>();
            DestroyEntitiesByType<Wassup.Battle.Units.EnemyKilledEventsSingleton>();
            DestroyEntitiesByType<Wassup.Battle.Effects.EnemyCcEventsSingleton>();
            DestroyEntitiesByType<Wassup.Battle.Effects.DotApplyEventsSingleton>();
            DestroyEntitiesByType<Wassup.Battle.Effects.CcClearRequestsSingleton>();
            DestroyEntitiesByType<Wassup.Battle.Effects.StatModifierApplyEventsSingleton>();
            DestroyEntitiesByType<Wassup.Battle.Effects.StackModifierApplyEventsSingleton>();
            DestroyEntitiesByType<Wassup.Battle.Effects.HazardRuntimeEventsSingleton>();
            DestroyEntitiesByType<Wassup.Battle.Effects.HazardDestroyedEventsSingleton>();
            DestroyEntitiesByType<GoalCollapsedEventsSingleton>();
            DestroyEntitiesByType<Wassup.Battle.Effects.HazardSpawnRequestsSingleton>();
            DestroyEntitiesByType<Wassup.Battle.Effects.MeteorBarrageRequestsSingleton>();
            DestroyEntitiesByType<Wassup.Battle.Combat.AttackOutputLogEventsSingleton>();
            DestroyEntitiesByType<Wassup.Battle.Effects.AggroHitEventsSingleton>();
            DestroyEntitiesByType<Wassup.Battle.Combat.CastEventsSingleton>();
            DestroyEntitiesByType<Wassup.Battle.Combat.DcTriggerFiredEventsSingleton>();
            DestroyEntitiesByType<Wassup.Battle.Combat.KnockupVisualEventsSingleton>();
            DestroyEntitiesByType<Wassup.Battle.Combat.BossLeapVisualEventsSingleton>();
            // ultimate-leap — 누락 시 BattleTimeScale H1 과 **같은 실패**: orphan 싱글턴이 남고
            // 다음 매치에 새 엔티티가 생겨 2개 → TryGetSingletonRW 실패 → 2판째부터 궁극기가
            // 예고·이탈·강하 없이 순간이동만 한다(부재-가드가 조용히 삼켜 콘솔도 깨끗하다).
            DestroyEntitiesByType<Wassup.Battle.Combat.UltimateLeapVisualEventsSingleton>();
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
            DestroyEntitiesByType<Wassup.Battle.Effects.ClockOutGimmickConfig>();
            DestroyEntitiesByType<Wassup.Battle.Effects.OnsenGimmickConfig>();
        }

        private void DisposeEcsInfrastructureNativeContainers()
        {
            if (_goalEventQueue.IsCreated) _goalEventQueue.Dispose();
            if (_defenderDeathQueue.IsCreated) _defenderDeathQueue.Dispose();
            if (_shieldBreakQueue.IsCreated) _shieldBreakQueue.Dispose();
            if (_unitAttackVisualQueue.IsCreated) _unitAttackVisualQueue.Dispose();
            if (_projectileHitEventQueue.IsCreated) _projectileHitEventQueue.Dispose();
            if (_aggroHitEventQueue.IsCreated) _aggroHitEventQueue.Dispose();
            if (_castEventQueue.IsCreated) _castEventQueue.Dispose();
            if (_dcTriggerFiredQueue.IsCreated) _dcTriggerFiredQueue.Dispose();
            if (_knockupVisualQueue.IsCreated) _knockupVisualQueue.Dispose();
            DisposeBossLeapChannel(); // 오버라이드 clear 포함 — 진행 중 비행이 자진 종료한다
            DisposeUltimateLeapChannel(); // 동일 — 공중 집합 + 오버라이드 clear
            if (_threatHitEventQueue.IsCreated) _threatHitEventQueue.Dispose();
            if (_blinkRequestQueue.IsCreated) _blinkRequestQueue.Dispose();
            if (_healAppliedEventQueue.IsCreated) _healAppliedEventQueue.Dispose();
            if (_shieldGrantedEventQueue.IsCreated) _shieldGrantedEventQueue.Dispose();
            if (_damageNumberEventQueue.IsCreated) _damageNumberEventQueue.Dispose();
            if (_enemyKilledEventQueue.IsCreated) _enemyKilledEventQueue.Dispose();
            if (_enemyCcQueue.IsCreated) _enemyCcQueue.Dispose();
            if (_dotApplyQueue.IsCreated) _dotApplyQueue.Dispose();
            if (_ccClearQueue.IsCreated) _ccClearQueue.Dispose();
            if (_statModifierQueue.IsCreated) _statModifierQueue.Dispose();
            if (_stackModifierQueue.IsCreated) _stackModifierQueue.Dispose();
            if (_attackOutputLogQueue.IsCreated) _attackOutputLogQueue.Dispose();
            if (_hazardRuntimeEventQueue.IsCreated) _hazardRuntimeEventQueue.Dispose();
            if (_hazardDestroyedQueue.IsCreated) _hazardDestroyedQueue.Dispose();
            if (_goalCollapsedQueue.IsCreated) _goalCollapsedQueue.Dispose();
            if (_hazardSpawnRequestQueue.IsCreated) _hazardSpawnRequestQueue.Dispose();
            if (_meteorBarrageRequestQueue.IsCreated) _meteorBarrageRequestQueue.Dispose();
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
            if (_dotEffectQueryCreated)
            {
                _dotEffectQuery.Dispose();
                _dotEffectQueryCreated = false;
            }
            if (_pickupViewQueryCreated)
            {
                _pickupViewQuery.Dispose();
                _pickupViewQueryCreated = false;
            }
            if (_resignationViewQueryCreated)
            {
                _resignationViewQuery.Dispose();
                _resignationViewQueryCreated = false;
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
        // 이 순서가 계약이므로 설치자 안으로 감추지 않는다 (continuous-agent-movement unit 0).
        private void BuildFlowField()
        {
            if (!_generatedMap.IsCreated || _em == null) return;

            // 기존 싱글톤 있으면 arrays dispose + entity destroy (멱등성 보장)
            TeardownFlowField();

            // map-origin-placement: _boardOrigin 은 BuildMapForBattle 이 설정한다 (Tilemap = zero 고정).
            SimFieldInstaller.InstallNavFields(_em, in _generatedMap, tileSize, _boardOrigin, ref _simFields);
        }

        // battle-structures unit 0 — goal-stability 의 SpawnGoalEntities 를 제거했다.
        // 저작 축(MapDocument.goalMaxStability)이 전 맵 미저작(0)이라 이 경로는 한 번도
        // 엔티티를 만들지 않았고, 라이브 골은 EnsureGoalTowers 하나가 세운다. 골이 두 벌인
        // 채로 거점 태그를 붙이면 "어느 골에 붙였나" 가 실제 버그가 된다.
        // 거점 스폰의 일반화(마음/본능)는 unit 4 SpawnStructureEntities 소관.

        // season-gimmick-overwork unit 4 — 픽업 스폰 후보 셀(Walk∪Place) 싱글턴 구축.
        // FlowFieldSingleton 동형: Persistent NativeArray 소유, TeardownFlowField 가 dispose.
        // gimmick 비활성이면 no-op. 멱등 (재빌드/redraft 시 기존 dispose 후 재생성).
        private void BuildPickupSpawnState()
        {
            TeardownPickupSpawnState();

            if (!_generatedMap.IsCreated || _em == null) return;
            // gimmick-match-integration — 레드불 기믹 배정 시에만 픽업 스폰 후보 구축.
            if (!(_assignedGimmick is Wassup.Data.RedBullGimmickData)) return;

            uint pickupSeed = (uint)Wassup.Core.MatchSeed.DerivePickupSeed(_matchSeed);
            SimFieldInstaller.InstallPickupSpawnState(_em, in _generatedMap, pickupSeed, ref _simFields);
        }

        private void TeardownPickupSpawnState()
            => SimFieldInstaller.TeardownPickupSpawnState(_em, ref _simFields);

        // enemy-spawn-positioning / tile-movement-integrity u0(rev) — 스폰 셀 flow 수직으로 중앙 기준 이산 N-레인 오프셋 계산.
        private float3 ComputeSpawnLateralOffset(int2 spawnCell)
        {
            if (!spawnSpreadEnabled || spawnSpreadFraction <= 0f) return float3.zero;

            float2 flowDir = float2.zero; // flow 0 → SpawnSpread.Perpendicular 가 (1,0) 기준 폴백.
            if (_simFields.flowField != Entity.Null && _em.Exists(_simFields.flowField) &&
                _em.HasComponent<Wassup.Battle.Effects.FlowFieldSingleton>(_simFields.flowField))
            {
                var field = _em.GetComponentData<Wassup.Battle.Effects.FlowFieldSingleton>(_simFields.flowField);
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

        // map-pipeline-cleanup unit 2 — legacy 옵션/설정 에셋 제거 후 FallbackLinear 전용 상수.
        // 값은 제거 시점 라이브와 동일(MapGenerationOptions.Default 20×10 / MapGenerationSettings 1).
        private static readonly int2 FallbackGridSize = new int2(20, 10);
        private const int FallbackGeneratorVersion = 1;
        private const int FallbackSpawnLaneCount = 2;

        private void BuildMapForBattle()
        {
            TeardownGeneratedMap();
            TeardownFlowField();

            var theme   = SeasonRuntime.Active?.mapTheme;

            // match-seed-unification — 맵 시드는 GameManager 주입 matchSeed 에서 파생.
            // 미주입(0, 예: 테스트 직접 호출) 시 즉석 random matchSeed 로 폴백해 항상 유효.
            int matchSeed = _matchSeed != 0 ? _matchSeed : Wassup.Core.MatchSeed.GenerateRandom();
            int seed = fixedMapSeed != 0 ? fixedMapSeed : Wassup.Core.MatchSeed.DeriveMapSeed(matchSeed);

            // random-map-pool unit 1 — 풀에서 (맵, 덱) 인코운터를 한 번 resolve.
            // 맵·덱은 같은 인덱스로 잠긴다(맵마다 그 맵의 적 패턴).
            // tournament-seed-map-select unit 2 — 인덱스 소스: fixedMapSeed(디버그) >
            // 서버 토너먼트 시드(같은 토너먼트 = 같은 맵) > 시드 부재 폴백 0번.
            // map-pipeline-cleanup unit 2 — 단일 mapDocument 폴백 제거: 풀이 유일 소스.
            MapDocument activeDoc = null;
            _resolvedDeck = deck;
            // endless-mode unit 2 — 무한 모드 진입: 공용 풀 이전에 전용 인카운터를 우선한다.
            // 풀 count 를 안 건드려 랜덤/토너먼트 맵 선택은 byte-identical(계약 5). DevMapOverride.Endless 로만.
            if (Wassup.Core.DevMapOverride.Endless && endlessEncounter.deck != null
                && MapGridBattleAdapter.IsUsableDocument(endlessEncounter.document))
            {
                activeDoc = endlessEncounter.document;
                _resolvedDeck = endlessEncounter.deck;
                Debug.Log("[BattleBridge] map source = ENDLESS encounter (DevMapOverride.Endless).");
            }
            else if (mapPool != null && mapPool.Count > 0)
            {
                int poolIndex;
                string poolSource;
                // map-play-feel unit 2 — 개발 확인용 인덱스 강제(모바일 개발빌드 런타임).
                // 서버 API 는 그대로 받되 override 가 설정돼 있으면 최우선. 없으면 아래 기존 3분기.
                // map-painter-tool unit 5 — 풀 뒤 이어붙은 dev 슬롯([Count..Count+DevCount-1])도 해석.
                // 시드 3분기는 mapPool.Count 만 보므로 devEntries 는 결정론에 불가시.
                if (Wassup.Core.DevMapOverride.HasIndex)
                {
                    poolIndex = Mathf.Clamp(Wassup.Core.DevMapOverride.Index, 0,
                        mapPool.Count + mapPool.DevCount - 1);
                    poolSource = poolIndex >= mapPool.Count ? "dev(devEntry)" : "dev";
                }
                else if (fixedMapSeed != 0)
                {
                    poolIndex = MapPoolSelect.SelectIndex(seed, mapPool.Count);
                    poolSource = "debug";
                }
                else if (Wassup.Core.Api.TournamentMatchReporter.HasTournamentSeed)
                {
                    poolIndex = MapPoolSelect.SelectIndexFromTournamentSeed(
                        Wassup.Core.Api.TournamentMatchReporter.TournamentSeed, mapPool.Count);
                    poolSource = "tournament";
                }
                else
                {
                    poolIndex = 0; // 게스트/응답 미도착/직접 Play — 시드 부재는 전부 0번
                    poolSource = "fallback0";
                }
                Debug.Log($"[BattleBridge] map pool index={poolIndex}/{mapPool.Count}(+dev {mapPool.DevCount}) (source={poolSource})");
                var encounter = poolIndex >= mapPool.Count
                    ? mapPool.GetDev(poolIndex - mapPool.Count)
                    : mapPool.Get(poolIndex);
                if (MapGridBattleAdapter.IsUsableDocument(encounter.document))
                {
                    activeDoc = encounter.document;
                    if (encounter.deck != null) _resolvedDeck = encounter.deck;
                }
            }

            // map-pipeline-cleanup unit 2/4 — legacy 맵 소스 스위치·절차 폴백 제거:
            // battle-structures unit 3 — 고른 문서를 보관한다. 거점 스탯(SO 참조)은
            // GeneratedMap 이 실을 수 없어 스폰(unit 4)이 저작 엔트리를 다시 읽어야 한다.
            _resolvedMapDoc = activeDoc;

            // authored 풀 문서 → ToGeneratedMap 이 유일 경로. unusable 문서는 hard-fail.
            try
            {
                _generatedMap = MapGridBattleAdapter.Build(activeDoc);
            }
            catch (MapGenerationFailedException ex)
            {
                Debug.LogError($"[BattleBridge] {ex.Message}", this);
                _generatedMap = default;
                _resolvedMapDoc = null;   // 리뷰 H-3 — 실패한 문서의 거점이 다음 판에 스폰되면 안 된다
                return;
            }

            // authored 문서는 Validator 없이 그대로 반환되므로 connectivity 를 여기서 검사한다.
            if (!MapConnectivity.AllSpawnsReachGoal(_generatedMap))
            {
                Debug.LogWarning("[BattleBridge] GeneratedMap connectivity failed; using fallback linear map.", this);
                TeardownGeneratedMap();
                _generatedMap = BattleMapBuilder.BuildFallbackLinear(
                    FallbackGridSize, seed, FallbackGeneratorVersion, FallbackSpawnLaneCount);
                // 리뷰 H-3 — 폐기된 문서를 계속 들고 있으면 그 좌표(다른 격자계)에 거점이
                // 서고, 공성 파생 스폰은 사라져 공성 맵이 조용히 침략 맵이 된다.
                _resolvedMapDoc = null;
            }

            // tilemap-world-surround unit 1 — MapGrid 내부에 장식 Deco 셀을 데이터로 designate (배경 프랍 호스트).
            // theme.keepRatio<1 일 때만. Walk 경로 불변·시드 결정적. 페인트/VisualPlan(아래) 전에 실행해야 반영된다.
            // random-map-pool — 커빙 시드는 맵 정체성(_generatedMap.seed)에서 뽑는다: 문서맵은 authoringSeed
            // (맵당 고정)라 배치칸이 매판 고정, 절차맵은 gen seed(matchSeed 파생)라 매판 변동 유지.
            // (matchSeed 파생 local seed 를 쓰면 fixedMapSeed=0 일 때 같은 문서맵도 배치칸이 매판 섞였다.)
            // map-painter unit 3 — 맵에 authored Deco(페인터로 명시 지정)가 있으면 시드 커빙 완전 스킵:
            // 지정한 Place/Deco 배치판을 그대로 존중한다. all-Place 문서/절차맵만 시드 커빙.
            bool hasAuthoredDeco = false;
            if (_generatedMap.IsCreated)
                for (int i = 0; i < _generatedMap.tiles.Length; i++)
                    if (_generatedMap.tiles[i] == MapTileType.Deco) { hasAuthoredDeco = true; break; }
            // placement-mask unit 1 — 마스크가 파생값(tiles==Place)과 상이 = 마스크 브러시로 저작된
            // 수동 배치판 ⇒ 커빙 skip (authored-Deco 규칙과 동형). 파생 마스크는 상이 셀 0 → 기존대로 커빙.
            bool hasAuthoredMaskIntent = _generatedMap.IsCreated
                && ObstaclePlacer.HasAuthoredMaskIntent(_generatedMap.tiles, _generatedMap.placeMask);
            if (theme != null
                && theme.mapGridBuildableKeepRatio < 1f && _generatedMap.IsCreated && !hasAuthoredDeco
                && !hasAuthoredMaskIntent)
            {
                var decoRng = Unity.Mathematics.Random.CreateFromIndex((uint)(_generatedMap.seed ^ 0x5A5A5A) | 1u);
                ObstaclePlacer.DesignateDeco(ref decoRng, _generatedMap.tiles,
                    _generatedMap.gridSize, theme.mapGridBuildableKeepRatio);
                // 커빙은 파생-마스크 맵에서만 도니 재파생이 정확히 동기다 (placement-mask unit 1).
                ObstaclePlacer.RederivePlaceMask(_generatedMap.tiles, _generatedMap.placeMask);
            }

            // placement-mask unit 4 리뷰 M-1 — 스폰·골 칸은 배치 불가(런타임 불변식).
            // Walk→Path 파생 때문에 Path 층 유닛에게 스폰/골 칸까지 열려 버리는데(스폰·골은 정의상
            // Walk 셀이다), 적이 튀어나오는 칸·유출 지점 위에 세우는 건 어느 층 저작에도 없던 의미다.
            // **문서/커빙 의미는 건드리지 않는다** — intent 비교와 재파생은 순수 파생 기준이라
            // 그대로 두고, 라이브 맵에만 마지막에 덮는다(문서를 이 규칙으로 오염시키지 않는다).
            if (_generatedMap.IsCreated && _generatedMap.placeMask.IsCreated)
            {
                for (int i = 0; i < _generatedMap.spawns.Length; i++)
                    CloseCellLayers(_generatedMap.spawns[i]);
                if (_generatedMap.goals.IsCreated)
                    for (int i = 0; i < _generatedMap.goals.Length; i++)
                        CloseCellLayers(_generatedMap.goals[i]);
                else
                    CloseCellLayers(_generatedMap.goal);

                // battle-structures unit 4 — 거점 배치 배제(README 요청 7-2). 적 본능은
                // 3×3 본체 + 주변 3타일 = 9×9 를 닫는다(포탑 사거리 안에 세우는 것 방지).
                // 그 외 거점(방어 본능·적 마음)은 본체 footprint 만. 빌드 시 파생이며
                // 저작본을 덮지 않는다 — 위 스폰·골 폐쇄와 같은 자리·같은 성격.
                if (_generatedMap.structures.IsCreated)
                {
                    for (int i = 0; i < _generatedMap.structures.Length; i++)
                    {
                        var st = _generatedMap.structures[i];
                        int half = Wassup.Data.StructurePlacements.FootprintOf(st.faction) / 2;
                        if (st.faction == Faction.EnemyInstinct)
                            half += Wassup.Data.StructurePlacements.EnemyInstinctPlacementPadding;   // 리뷰 A-L1
                        for (int dy = -half; dy <= half; dy++)
                            for (int dx = -half; dx <= half; dx++)
                                CloseCellLayers(new int2(st.cell.x + dx, st.cell.y + dy));
                    }
                }
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
            MirrorLiftKnobs(); // 스폰 전 1회 — 이후는 LateUpdate 가 매 프레임 갱신(라이브 튜닝)
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

            // camera-direction unit 8 — 맵마다 크기가 달라(12×10 ~ 20×12) 고정 포즈로는 여백이
            // 남거나 가장자리가 잘린다. 그리드가 확정된 지금 홈 거리를 다시 잡는다.
            // 카메라를 직접 쓰지 않고 소유자(CameraDirector)의 홈만 갱신 — 페이즈/포커스/킥 델타는
            // 홈 기준이라 그대로 따라온다. view·director 부재(headless)면 조용히 skip.
            // bounds 는 ground 렌더러 실측(TryGetBoardWorldBounds)이 아니라 플레이 그리드다 —
            // 전자는 주변 데코 지대까지 포함해(20×12 → 35×32) 카메라가 과하게 물러난다.
            if (tilemapMapView != null && tilemapMapView.TryGetPlayfieldWorldBounds(
                    new Vector2Int(_generatedMap.gridSize.x, _generatedMap.gridSize.y), out var boardBounds))
                EnsureCameraDirector()?.FrameBoard(boardBounds);

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
                "authored");
            Debug.Log($"[BattleBridge] Map: seed={_generatedMap.seed} ver={_generatedMap.generatorVersion} size={_generatedMap.gridSize} spawns={_generatedMap.spawns.Length}");
            // 로그의 mapSeed 를 실제 빌드에 쓰인 시드로 갱신 (fixedMapSeed 오버라이드/수동 document(-1) 반영).
            GameManager.Instance?.Logger?.SetActualMapSeed(_generatedMap.seed);
        }

        private void TeardownFlowField()
            => SimFieldInstaller.Teardown(_world, _em, ref _simFields);

        // Phase 6: placement phase enters this path — ECS state is initialized so
        // PlaceDefenderAs works immediately, but spawns / timer stay dormant.
        public void BeginPlacement()
        {
            if (ActiveDeck == null || mapPool == null || mapPool.Count == 0)
            {
                Debug.LogError("[BattleBridge] deck or map pool reference missing.", this);
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
            RefreshPlacementHighlightIfShown(); // placement-eligible-tile-highlight unit 2
            _defenderByTile.Clear();
            _defenderViewOverride.Clear(); // defender-relocation review L1 — 뷰 오버라이드도 _defenderByTile 리셋과 co-locate(불변식)
            tileHealthGaugeLayer?.Clear(); // unit 3 — 게이지 정리를 _defenderByTile 리셋과 co-locate(불변식)
            unitOverheadUiLayer?.Clear();
            // ingame-dreamcatcher Unit 2/3 — reset card registry + triggers for a new match.
            _activeDcEffects.Clear();
            _activePlacementSleeps.Clear(); // combat-action-lock — 매치별 placement-aura Sleep 등록 초기화
            _bountyMarked.Clear(); // 살찌운 제물 — 표식 등록부도 매치 경계에서 초기화
            ClearAllyBuffZonePaint(); // active-ally-zone unit 2 — 등록부와 refcount 를 함께 반납
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
            _killScoreTotal = 0;       // battle-score-formula unit 2 — 계약 9
            _killCount = 0;
            ResetGoalStability();      // three-minute-survival unit 0 — 계약 9
            DestroyStructureEntities();  // goal-tower-siege unit 0 — 이전 판의 타워/거점 정리
            RefreshLeakHud();
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

            GameManager.Instance?.Logger?.SetAttackDeckId(ActiveDeck.deckId);
            Debug.Log("[BattleBridge] Placement phase ready.");
        }


        public void StartBattle()
        {
            if (ActiveDeck == null || mapPool == null || mapPool.Count == 0)
            {
                Debug.LogError("[BattleBridge] deck or map pool reference missing.", this);
                return;
            }
            if (!_placementAllowed) BeginPlacement();
            if (_world == null) return;
            _pending.Clear();
            _usingGeneratedWaves = TryInitializeGeneratedWaves();
            if (!_usingGeneratedWaves)
            {
                for (int i = 0; i < ActiveDeck.spawns.Count; i++)
                    _pending.Add(new PendingSpawnEntry { entry = ActiveDeck.spawns[i], deckIndex = i });
            }
            _startTime = Time.time;
            _battleClock = 0.0;
            _killScoreTotal = 0; // battle-score-formula unit 2 — 계약 9 (시계와 짝)
            _killCount = 0;
            ResetGoalStability(); // three-minute-survival unit 0 — 계약 9 (시계와 짝)
            // goal-tower-siege unit 0 — 맵·월드가 준비된 뒤 골 셀마다 타워를 세운다.
            // ResetGoalStability 다음이어야 풀이 이번 판의 최대치를 받는다.
            SpawnStructureEntities();
            // wave-authoring-test-mode unit 2 — 작성 모드는 plan.timerDurationSec(0=endless).
            // seed/legacy 경로는 deck.timerDurationSec 그대로(무변경).
            _timerDuration = _usingAuthoredPlan ? _wavePlan.timerDurationSec : ActiveDeck.timerDurationSec;
            _running = true;
            if (_usingGeneratedWaves)
                QueueDueWaves(0f);
            if (_usingAuthoredPlan)
                Debug.Log($"[BattleBridge] Battle started with AUTHORED plan '{_authoredPlan.displayName}' waves={_wavePlan.waves.Count} endless={(_timerDuration <= 0f)}.");
            else
                Debug.Log(_usingGeneratedWaves
                    ? $"[BattleBridge] Battle started with generated deck '{ActiveDeck.deckId}' seed={_wavePlan.seed} (source={(ActiveDeck.waveSeed != 0 ? "deck-fixed" : "derived")}) waves={_wavePlan.waves.Count}."
                    : $"[BattleBridge] Battle started with legacy deck '{ActiveDeck.deckId}' ({ActiveDeck.spawns.Count} spawns queued).");
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
            if (!_dotEffectQueryCreated)
            {
                _dotEffectQuery = _em.CreateEntityQuery(
                    ComponentType.ReadOnly<Wassup.Battle.Effects.DotEffect>());
                _dotEffectQueryCreated = true;
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
            // season-gimmick-clockout unit 1 — 사직서 뷰 조정용.
            if (!_resignationViewQueryCreated)
            {
                _resignationViewQuery = _em.CreateEntityQuery(ComponentType.ReadOnly<Wassup.Battle.Effects.Resignation>());
                _resignationViewQueryCreated = true;
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

            // dreamcatcher-shield-break unit 0 — 실드 피격 파열 이벤트 채널.
            if (_shieldBreakQueue.IsCreated) _shieldBreakQueue.Dispose();
            _shieldBreakQueue = new NativeQueue<ShieldBreakEvent>(Allocator.Persistent);
            var shieldBreakSingleton = _em.CreateEntity();
            _em.AddComponentData(shieldBreakSingleton, new ShieldBreakEventsSingleton { queue = _shieldBreakQueue });

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

            // attack-decoupling unit 4 — 캐스트 사건 채널(Effects→Combat). 해저드
            // 캐스터는 attackRange 0 이라 RESOLVE 에 못 가므로 캐스트 성사가 곧 그
            // host 의 공격 사건이다. HazardCastSystem 이 enqueue, AttackSystem 이
            // 드레인. 브리지는 lifecycle 만(AggroHit 선례와 동일 3점 세트).
            if (_castEventQueue.IsCreated) _castEventQueue.Dispose();
            _castEventQueue = new NativeQueue<Wassup.Battle.Combat.CastEvent>(Allocator.Persistent);
            var castEventSingleton = _em.CreateEntity();
            _em.AddComponentData(castEventSingleton, new Wassup.Battle.Combat.CastEventsSingleton { queue = _castEventQueue });

            // use-flow unit 3 — Combat→Bridge 부착 카드 발동 신호 채널. AttackSystem 의
            // AttackN 발동 3지점이 host 를 enqueue, 브리지 드레인이 머리 위 아이콘 행 펄스.
            if (_dcTriggerFiredQueue.IsCreated) _dcTriggerFiredQueue.Dispose();
            _dcTriggerFiredQueue = new NativeQueue<Wassup.Battle.Combat.DcTriggerFiredEvent>(Allocator.Persistent);
            var dcFiredSingleton = _em.CreateEntity();
            _em.AddComponentData(dcFiredSingleton, new Wassup.Battle.Combat.DcTriggerFiredEventsSingleton { queue = _dcTriggerFiredQueue });
            _dcProcLastImpact.Clear(); // 매치 경계 — 엔티티는 매치마다 새로우니 스로틀 기록 리셋

            // knockup-fighter-defender unit 3 — 넉업 띄우기 연출 채널. 넉업을 건 쪽(AttackSystem
            // RESOLVE / on-place StunNearby)이 대상을 enqueue, 브리지가 드레인해 view 를 띄운다.
            if (_knockupVisualQueue.IsCreated) _knockupVisualQueue.Dispose();
            _knockupVisualQueue = new NativeQueue<Wassup.Battle.Combat.KnockupVisualEvent>(Allocator.Persistent);
            var knockupVisualSingleton = _em.CreateEntity();
            _em.AddComponentData(knockupVisualSingleton, new Wassup.Battle.Combat.KnockupVisualEventsSingleton { queue = _knockupVisualQueue });
            CreateBossLeapChannel(); // boss-jjangssen unit 6 (상태·코루틴은 BattleBridge.BossLeap.cs)
            CreateUltimateLeapChannel(); // ultimate-leap unit 3 (상태·코루틴은 BattleBridge.UltimateLeap.cs)

            // beam unit 1 — 매치 경계에서 빔 세션을 전부 끊는다. 브리지는 매치 간 살아남으므로
            // (이 함수가 큐를 재생성하는 것이 그 증거) 안 끊으면 이전 매치 엔티티를 키로 든
            // 세션이 남아 TTL 이 만료될 때까지 허공에 빔이 뜬다.
            beamPresenter?.CloseAll();

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

            // shield-guardian-defender unit 4 — Effects→Presentation 실드 부여 원샷 VFX 채널.
            // ShieldCastSystem 이 부여 대상 위치마다 1건 enqueue.
            if (_shieldGrantedEventQueue.IsCreated) _shieldGrantedEventQueue.Dispose();
            _shieldGrantedEventQueue = new NativeQueue<Wassup.Battle.Effects.ShieldGrantedEvent>(Allocator.Persistent);
            var shieldGrantedSingleton = _em.CreateEntity();
            _em.AddComponentData(shieldGrantedSingleton, new Wassup.Battle.Effects.ShieldGrantedEventsSingleton { queue = _shieldGrantedEventQueue });

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

            // dot-effect-extraction unit 0 — 지속 피해 채널(25번째). DotApplySystem 이 드레인해
            // DotEffect 버퍼에 (origin, element) 별로 병합한다.
            if (_dotApplyQueue.IsCreated) _dotApplyQueue.Dispose();
            _dotApplyQueue = new NativeQueue<Wassup.Battle.Effects.DotApplyEvent>(Allocator.Persistent);
            var dotApplySingleton = _em.CreateEntity();
            _em.AddComponentData(dotApplySingleton, new Wassup.Battle.Effects.DotApplyEventsSingleton { queue = _dotApplyQueue });

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

            // goal-stability unit 4 — 골 붕괴 채널(Units→Bridge). 연출/로그 전용 — 유출
            // 전환은 골 엔티티 부재로 이미 성립(공성 게이트)해 상태 갱신이 없다.
            if (_goalCollapsedQueue.IsCreated) _goalCollapsedQueue.Dispose();
            _goalCollapsedQueue = new NativeQueue<GoalCollapsedEvent>(Allocator.Persistent);
            var goalCollapsedSingleton = _em.CreateEntity();
            _em.AddComponentData(goalCollapsedSingleton, new GoalCollapsedEventsSingleton { queue = _goalCollapsedQueue });

            // Hazard caster spawn request channel. Effects systems enqueue
            // unmanaged requests; BattleBridge owns SO lookup and visual spawning.
            if (_hazardSpawnRequestQueue.IsCreated) _hazardSpawnRequestQueue.Dispose();
            _hazardSpawnRequestQueue = new NativeQueue<Wassup.Battle.Effects.HazardSpawnRequest>(Allocator.Persistent);
            var hazardSpawnRequestSingleton = _em.CreateEntity();
            _em.AddComponentData(hazardSpawnRequestSingleton, new Wassup.Battle.Effects.HazardSpawnRequestsSingleton { queue = _hazardSpawnRequestQueue });

            // season-gimmick-clockout unit 3 — 메테오 barrage 요청 채널(Effects→Bridge).
            // ResignationThresholdSystem enqueue → DrainMeteorBarrageRequests(unit 4)이 cast.
            if (_meteorBarrageRequestQueue.IsCreated) _meteorBarrageRequestQueue.Dispose();
            _meteorBarrageRequestQueue = new NativeQueue<Wassup.Battle.Effects.MeteorBarrageRequest>(Allocator.Persistent);
            var meteorBarrageSingleton = _em.CreateEntity();
            _em.AddComponentData(meteorBarrageSingleton, new Wassup.Battle.Effects.MeteorBarrageRequestsSingleton { queue = _meteorBarrageRequestQueue });

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
            _killScoreTotal = 0; // battle-score-formula unit 2 — 계약 9 (시계와 짝)
            _killCount = 0;
            ResetGoalStability(); // three-minute-survival unit 0 — 계약 9 (시계와 짝)
            DestroyStructureEntities();  // goal-tower-siege unit 0 — 타워/거점도 매치와 함께 정리
            _waveTimeShift = 0f; // wave-pattern unit 9 — 계약 9 (시계와 짝)
            _waveStartSec = 0f;  // three-minute-survival unit 2 — 계약 9 (시계와 짝)
            _spawnAlertForecast = null; // spawn-point-alert unit 3 — 계약 9 (시계와 짝)
            _battleTimeScaleEntity = Entity.Null;
            // range-preview unit 3 — 매치 종료 시 격자 표시 무조건 해제(비행 중
            // 종료로 impact drain 이 못 지운 텔레그래프 잔상 방지).
            // placement-thumb-occlusion — ClearRange 를 우회하는 경로라(소유자 무관 강제 해제) 전환은
            // SetRangeOwner 로 태운다. 안 태우면 _rangeInvalid 가 매치 경계를 넘어 살아남고
            // TilemapMapView.Clear() 의 방어 리셋에만 의존하게 된다(우연한 이중 방어).
            SetRangeOwner(RangeDisplayOwner.None);
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
            if (ActiveDeck == null || mapPool == null || mapPool.Count == 0)
            {
                Debug.LogError("[BattleBridge] deck or map pool reference missing.", this);
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
            // summon-patrol-defender — 순찰병 SO 레지스트리도 형제와 같은 두 지점에서 비운다.
            // 인덱스는 판마다 bake 되므로 안전하고, 안 비우면 managed SO 참조를 앱 수명으로
            // 붙들어 에셋 언로드를 막는다.
            _patrolUnitRegistry.Clear();
            _patrolUnitIndex.Clear();

            TeardownGeneratedMap();
            TeardownFlowField();
        }

        private void DestroyEntitiesByType<T>() where T : unmanaged, Unity.Entities.IComponentData
        {
            if (!HasLiveEntityManager()) return;
            using var q = _em.CreateEntityQuery(Unity.Entities.ComponentType.ReadOnly<T>());
            if (!q.IsEmpty) _em.DestroyEntity(q);
        }

        // draft-stage-map-prebuild Unit 0 — 맵 재빌드 진입점. map-pipeline-cleanup unit 2 로
        // 런타임 호출처(옵션 변경 패널)는 사라졌고, 라이프사이클 계약(BattleBridgeDraftMapTests)
        // 이 유지한다. 옵션 카운터는 소비 테스트 삭제로 함께 제거.
        public void RebuildDraftMap()
        {
            if (_world == null) { PrepareDraftMap(); return; }
            CleanupDraftMapBeforeRebuild();
            BuildMapForBattle();
        }

        // draft-stage-map-prebuild Unit 0 — true once BuildMapForBattle has succeeded at least once.
        public bool HasGeneratedMap => _generatedMap.IsCreated;

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
            _waveTimeShift = 0f; // wave-pattern unit 9 — 강제 호출 오프셋은 매치 경계에서 초기화
            _waveStartSec = 0f;  // three-minute-survival unit 2 — 상한 간격 기준 시각도 함께
            _usingAuthoredPlan = false;
            _spawnAlertForecast = null;   // spawn-point-alert unit 3 — 이전 판 예고 이월 방지

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

            if (ActiveDeck == null || !ActiveDeck.useGeneratedWaves)
                return false;

            try
            {
                // wave-pattern unit 6 — 덱 waveSeed 비0 = 고정(테스트 버전, 브리핑 스트립과 동일 플랜).
                // 0 = matchSeed 파생(매판 랜덤, match-seed-unification — 맵과 decorrelated).
                int waveSeed = ActiveDeck.waveSeed != 0
                    ? ActiveDeck.waveSeed
                    : Wassup.Core.MatchSeed.DeriveWaveSeed(_matchSeed != 0 ? _matchSeed : 1);
                _wavePlan = WavePatternGenerator.Generate(ActiveDeck, waveSeed);
                GameManager.Instance?.Logger?.SetWavePattern(_wavePlan);
                return _wavePlan.waves != null && _wavePlan.waves.Count > 0;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[BattleBridge] Generated wave plan failed; using legacy spawns. {ex.Message}", this);
                return false;
            }
        }

        // wave-pattern unit 9 — 런타임 예정 시각 = 플랜 시각 + 강제 호출 누적 오프셋.
        // 스케줄을 읽는 모든 지점(자동 큐잉·강제 호출·스폰 예고)이 이 창구를 쓴다.
        private float ScheduledWaveTime(int waveIndex) =>
            _wavePlan.waves[waveIndex].triggerTimeSec + _waveTimeShift;

        // wave-pattern unit 11 — 웨이브 트리거와 첫 적 등장 사이의 리드인. 스폰 base 에만
        // 더한다. ScheduledWaveTime(트리거 그리드)·_waveTimeShift 산식에는 절대 넣지 않는다 —
        // 섞으면 강제 호출 연타마다 리드인이 누적 왜곡된다.
        private float SpawnLeadInSec => _wavePlan.spawnLeadInSec;

        // three-minute-survival unit 2 — 웨이브 진행은 **이벤트 구동**이다:
        //   다음 웨이브 = 필드에 적 0기(전멸)  OR  현재 웨이브 트리거 후 maxWaveIntervalSec 경과
        // 시각 그리드(triggerTimeSec = i × interval)는 명목값으로만 남는다.
        //
        // 작성 플랜(_usingAuthoredPlan)은 **예외**다: 저작된 durationSec 타임라인이 그 모드의
        // 정본이므로(wave-authoring-test-mode 계약) 기존 시각 스케줄을 그대로 쓴다.
        private void QueueDueWaves(float elapsedSec)
        {
            if (!_usingGeneratedWaves || _wavePlan.waves == null) return;
            if (_usingAuthoredPlan)
            {
                while (_nextWaveIndex < _wavePlan.waves.Count &&
                       elapsedSec + 0.0001f >= ScheduledWaveTime(_nextWaveIndex))
                {
                    QueueWave(_wavePlan.waves[_nextWaveIndex],
                        ScheduledWaveTime(_nextWaveIndex) + SpawnLeadInSec, false, elapsedSec);
                    _nextWaveIndex++;
                }
                return;
            }

            if (_nextWaveIndex >= _wavePlan.waves.Count) return;

            // 웨이브 1 은 판 시작에 무조건 나간다. _nextWaveIndex == 0 에서 전멸 분기를 허용하면
            // "아직 아무것도 안 나왔다" 가 "전멸했다" 로 읽혀 첫 두 웨이브가 같은 프레임에 터진다.
            bool first = _nextWaveIndex == 0;
            bool cleared = !first && NoQueuedAttackersRemain();
            bool capReached = !first && elapsedSec - _waveStartSec >= MaxWaveIntervalSec;
            if (!first && !cleared && !capReached) return;

            QueueWave(_wavePlan.waves[_nextWaveIndex], elapsedSec + SpawnLeadInSec, false, elapsedSec);
            _nextWaveIndex++;
            _waveStartSec = elapsedSec;
        }

        // 상한 간격. 덱이 0(레거시 저작)이면 플랜의 명목 interval 로 폴백해 "상한 없음" 상태를
        // 만들지 않는다 — 0 이면 매 프레임 capReached 가 참이 되어 전 웨이브가 한 번에 쏟아진다.
        private float MaxWaveIntervalSec
        {
            get
            {
                float deckValue = ActiveDeck != null ? ActiveDeck.maxWaveIntervalSec : 0f;
                if (deckValue > 0f) return deckValue;
                return _wavePlan.waveIntervalSec > 0f ? _wavePlan.waveIntervalSec : 20f;
            }
        }

        // 도크 표시용(읽기 전용): 다음 웨이브 자동 진행까지 남은 초.
        public float NextWaveSecondsRemaining => !NextWaveHasNext || _nextWaveIndex == 0
            ? 0f
            : Mathf.Max(0f, MaxWaveIntervalSec - ((float)_battleClock - _waveStartSec));

        public int WaveCountTotal =>
            _wavePlan.waves != null ? _wavePlan.waves.Count : 0;

        // Read-only wave-progress state for the UI (NextWaveDock polls these). The dock
        // owns the button/label chrome; BattleBridge (ECS gateway) no longer builds UI.
        public bool NextWaveAvailable => _running && _usingGeneratedWaves && _wavePlan.waves != null;
        public bool NextWaveHasNext => NextWaveAvailable && _nextWaveIndex < _wavePlan.waves.Count;
        public int NextWaveNumber => _nextWaveIndex + 1;
        // three-minute-survival unit 2 — `NextWaveClearReady`(클리어 강조)는 은퇴했다. 전멸이
        // 곧 자동 진행이라 "눌러라" 라고 알릴 대상이 없다. `_nextWaveClearReady` 내부 상태와
        // `nextwave-clear-attention` 의 도크 어필도 함께 제거.

        // spawn-point-alert unit 3 — **마지막으로 큐잉된 웨이브**의 lane 별 첫 스폰 절대 시각
        // (read-only). SpawnAlertPresenter 폴링 전용. 미래 웨이브 예측이 아니라 QueueWave 가
        // 큐잉 시점에 실제 스폰 base 로 1회 계산해 넣는다 — 실스폰과 어긋날 여지가 없고,
        // 자동/강제/Wave 1 이 모두 같은 경로라 리드인(wave-pattern unit 11) 만큼의 창을 똑같이
        // 얻는다. 반환 배열은 캐시 참조라 수정 금지.
        private float[] _spawnAlertForecast;

        public bool TryGetSpawnAlertForecast(out float battleClockSec, out float[] laneFirstSpawnSec)
        {
            battleClockSec = (float)_battleClock;
            laneFirstSpawnSec = null;
            if (!_running || _spawnAlertForecast == null) return false;
            // 미래 스폰이 남아 있는 동안만 서빙한다. 웨이브의 뒷 lane 들은 레인 간
            // intraWaveSpacing 간격으로 늦게 나오므로, 마지막 lane 스폰까지 유지해야 뒷 lane
            // 예고가 자기 유닛보다 먼저 사라지지 않는다.
            if (LastSpawnSec(_spawnAlertForecast) <= battleClockSec) return false;
            laneFirstSpawnSec = _spawnAlertForecast;
            return true;
        }

        private static float LastSpawnSec(float[] laneFirstSpawnSec)
        {
            float last = -1f;
            for (int i = 0; i < laneFirstSpawnSec.Length; i++)
                if (laneFirstSpawnSec[i] > last) last = laneFirstSpawnSec[i];
            return last;
        }

        // spawn-point-alert unit 1(rev) — 스폰→골 대표 경로(sim, 셀 중심 나열. [0]=스폰).
        // 유닛 이동과 같은 goal flow field 의 flow 를 셀 단위로 따라간다(타이브레이크 동일).
        // 트레일 표시 시작 시에만 호출되므로(웨이브당 lane 수 회) 캐시 불요. 뷰 변환은 호출측.
        public bool TryGetSpawnPathSim(int laneIndex, List<Vector3> outPath)
        {
            if (outPath == null) return false;
            outPath.Clear();
            if (!_generatedMap.IsCreated || laneIndex < 0 || laneIndex >= _generatedMap.spawns.Length)
                return false;
            if (_simFields.flowField == Entity.Null || !_em.Exists(_simFields.flowField) ||
                !_em.HasComponent<Wassup.Battle.Effects.FlowFieldSingleton>(_simFields.flowField))
                return false;

            var field = _em.GetComponentData<Wassup.Battle.Effects.FlowFieldSingleton>(_simFields.flowField);

            // continuous-agent-movement — 예고 라인은 **실제 이동선과 같아야 한다**.
            // MovementSystem 이 매 프레임 하는 것과 같은 절차를 그대로 밟는다: 전방 가시점으로
            // 직행(평활화), 없으면 필드 한 스텝. 라인만 필드 계단을 그리면 유닛이 라인을
            // 벗어나 걷는 것처럼 보인다(사용자 지적 2026-08-08).
            var obstacles = default(Wassup.Battle.Effects.ObstacleSingleton);
            bool hasObstacles = _blockedCells.IsCreated;
            if (hasObstacles) obstacles = new Wassup.Battle.Effects.ObstacleSingleton { blockedCells = _blockedCells };
            var nav = Wassup.Battle.Movement.MovementCellTrim.BuildNavGrid(in field, hasObstacles, in obstacles);

            float radius = agentRadiusTiles * tileSize;
            int2 cell = _generatedMap.spawns[laneIndex];
            float3 pos = Wassup.Battle.Movement.GridMath.CellToWorldCenter(
                cell, field.tileSize, spawnHeight, origin: field.origin);
            outPath.Add(new Vector3(pos.x, pos.y, pos.z));

            int guard = field.gridSize.x * field.gridSize.y + 1; // 순환 방어
            for (int i = 0; i < guard; i++)
            {
                cell = Wassup.Battle.Movement.GridMath.WorldToCell(
                    pos, field.tileSize, field.gridSize, origin: field.origin);
                int idx = Wassup.Battle.Movement.GridMath.CellIndex(cell, field.gridSize);
                if (idx < 0 || idx >= field.flow.Length) break;
                if (field.dist[idx] == 0) break; // 골 도달

                // unit 10 — 목표점 선택 규칙은 MovementSystem 과 같은 순수 헬퍼 하나다.
                // (평활화/코너 꼭짓점 → 폴백 필드 스텝 → 골·고립 종료.) 여기 인라인하지
                // 말 것 — 갈라지면 "라인 ≠ 이동선" 부류가 재발한다.
                if (!Wassup.Battle.Movement.PathSmoothing.TryStepTarget(
                        pos, in nav, in field.flow, radius,
                        Wassup.Battle.Movement.PathSmoothing.DefaultLookahead, out float3 next))
                    break;
                pos = next;
                outPath.Add(new Vector3(pos.x, pos.y, pos.z));
            }
            return outPath.Count >= 2;
        }

        // three-minute-survival unit 2 — **플레이어 경로는 없어졌다**(NextWaveDock 은 정보 표시
        // 전용이고 이 메서드를 부르지 않는다). 메서드 자체는 남는다: PlayMode 스모크
        // (TallyFlowTest·EndlessModeSmokeTest·MovementIntegritySmokeTest)가 이것을 **판 진행
        // 동력**으로 쓰기 때문이다 — no-op 으로 만들면 그 테스트들이 타임아웃으로 죽는다.
        public void ForceNextWave()
        {
            if (!_running || !_usingGeneratedWaves || _wavePlan.waves == null) return;
            if (_nextWaveIndex >= _wavePlan.waves.Count)
                return;
            // 이벤트 구동 경로에서는 그리드 리스케줄(_waveTimeShift)이 의미가 없다 —
            // 지금 큐잉하고 상한 타이머만 재기준한다.
            if (!_usingAuthoredPlan)
            {
                float now = (float)_battleClock;
                var forced = _wavePlan.waves[_nextWaveIndex];
                GameManager.Instance?.Logger?.RecordWaveEvent("wave_forced", forced.waveIndex, now, true);
                QueueWave(forced, now + SpawnLeadInSec, true, now);
                _nextWaveIndex++;
                _waveStartSec = now;
                return;
            }

            // time-manager Unit 3 — 강제 웨이브의 triggerTimeSec 기준도 Battle 클럭이어야 한다.
            // Update 의 스폰 게이트가 _battleClock 을 쓰므로 실시간을 쓰면 정지/슬로우모 시 갈라진다.
            float elapsedSec = (float)_battleClock;
            var wave = _wavePlan.waves[_nextWaveIndex];

            // wave-pattern unit 9 — 앞당긴 만큼 남은 웨이브 전체를 같이 민다(README "연타는 남은
            // wave 들을 순서대로 앞당긴다" 계약). 오프셋이 균일해 웨이브 간 간격이 보존되므로
            // 다음 웨이브는 "지금 + 그 웨이브의 원래 간격"에 나온다 — 연타해도 매번 재기준된다.
            // 인덱스 증가 전에 계산해야 한다(밀 대상 = 지금 강제 호출하는 웨이브).
            _waveTimeShift -= ScheduledWaveTime(_nextWaveIndex) - elapsedSec;

            GameManager.Instance?.Logger?.RecordWaveEvent("wave_forced", wave.waveIndex, elapsedSec, true);
            // unit 11 — 강제 호출도 리드인을 따른다(당긴 웨이브의 첫 적도 리드인 뒤에 나온다).
            QueueWave(wave, elapsedSec + SpawnLeadInSec, true, elapsedSec);
            _nextWaveIndex++;
            // spawn-point-alert unit 3 — 예고는 QueueWave 가 이 웨이브 기준으로 채운다.
            // (unit 1 의 "강제 호출은 예고 없이 즉시 스폰" 계약은 리드인 도입으로 폐기 —
            //  당긴 웨이브도 리드인만큼의 예고 창을 갖는다.)
        }

        private void QueueWave(GeneratedWave wave, float baseTriggerTimeSec, bool forced, float elapsedSec)
        {
            // 자동/강제 호출 모두 같은 진입점(전멸 진행·상한 진행·강제 호출·웨이브 1).
            int laneCount = _generatedMap.IsCreated ? _generatedMap.spawns.Length : 1;
            var entries = WavePatternGenerator.ExpandWave(wave, baseTriggerTimeSec, laneCount, _wavePlan.intraWaveSpacingSec);
            int baseDeckIndex = wave.waveIndex * WavePatternGenerator.DeckIndexStride;
            for (int i = 0; i < entries.Count; i++)
                _pending.Add(new PendingSpawnEntry { entry = entries[i], deckIndex = baseDeckIndex + i });

            // spawn-point-alert unit 3 — 예고는 **이 웨이브의 실제 스폰 base** 로 계산한다(예측 아님).
            // 자동·강제·Wave 1 이 모두 이 경로를 지나므로 예고 창이 균일하게 생긴다.
            _spawnAlertForecast = WavePatternGenerator.FirstSpawnTimesPerLane(
                wave, baseTriggerTimeSec, laneCount, _wavePlan.intraWaveSpacingSec);

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
                // active-ally-zone unit 1 — 아군 버프는 **시간제 장판**이다(즉시 버프 폐기).
                // 빈 칸에도 놓을 수 있어 적 장판과 규칙이 같아졌다 — 0기 거절은 폐기.
                case SkillEffectType.PowerSurge:
                    affectedCount = SpawnAllyBuffZone(tile, skill, Wassup.Battle.Effects.StatKind.DamageMul);
                    break;
                case SkillEffectType.RapidFire:
                    affectedCount = SpawnAllyBuffZone(tile, skill, Wassup.Battle.Effects.StatKind.AttackSpeedMul);
                    break;
                default:
                    Debug.LogWarning($"[BattleBridge] Tile skill '{skill.id}' has unsupported effect {skill.effect}.");
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
            // active-dreamcatcher-tile-aim rev — 입구 == 출구 거절. MovementSystem 의 포탈 스냅은
            // flow step **앞**에 돌아서(MovementSystem 1번 블록) 같은 타일로 잇는 링크는 반경 안
            // 적을 매 프레임 타일 중심으로 되돌린다 = 지속시간만큼의 정지 필드. 카드 한 장 값의
            // 군중제어가 되어버리므로 창구에서 막는다(UI 도 조준 단계에서 같은 판정으로 거절).
            if (entryTile == exitTile)
            {
                Debug.LogWarning($"[BattleBridge] CastPortal '{skill.id}' rejected — entry == exit {entryTile}.");
                return false;
            }
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

        // active-dreamcatcher-tile-aim unit 0 — 지정 타일 반경 내 **아군 전부**에 스킬
        // 모디파이어(공격폭증=DamageMul, 속사=AttackSpeedMul). 구 CastSkillOnDefender(타일의
        // 유닛 1기)를 대체한다. 반경은 skill.range → 체비셰프 타일.
        // active-ally-zone unit 1 — 아군 버프 = 장판 스폰. 시뮬 적용(누가 안에 있나 → 누가
        // 강화되나)은 Effects 의 AllyBuffFieldSystem 소관이고, bridge 는 스폰 호출과 **로그용
        // 스냅샷 카운트**만 한다(TRD: MonoBehaviour 에 전투 로직 금지).
        // 반환값은 로그의 affected_count 전용 — 성공/실패 판정에 쓰지 않는다(0기도 성공).
        private int SpawnAllyBuffZone(Vector2Int tile, SkillData skill, Wassup.Battle.Effects.StatKind stat)
        {
            int tileRange = GridMath.RangeToTiles(skill.range);
            var carrier = Wassup.Battle.Effects.EffectSpawner.SpawnAllyBuffField(
                _em, new int2(tile.x, tile.y), tileRange, stat, skill.magnitude, skill.durationSec);
            PaintAllyBuffZone(carrier, tile, tileRange); // unit 2 — 원인이 화면에 남는다
            CollectAlliesInRange(tile, tileRange, _allyLogScratch);
            return _allyLogScratch.Count;
        }

        // active-ally-zone unit 2 — 장판 점등 등록부(캐리어 엔티티 → 칠한 셀). 만료는 ECS 가
        // 엔티티를 파괴해서 알리므로, 뷰 회수는 프레임 재조정으로 한다(bridge 책임 = 시각 드레인).
        // 셀 목록을 들고 있지 않고 (중심, 반경)만 기억한다 — 캐스트마다 List 를 새로 만들지 않고,
        // 회수 시 같은 규칙으로 다시 만든다(칠한 것과 반납하는 것이 같은 함수에서 나오게).
        private readonly Dictionary<Entity, (Vector2Int center, int tileRange)> _allyZonePaint = new();
        private readonly List<Vector2Int> _zoneCellScratch = new List<Vector2Int>();
        private readonly List<Entity> _zoneGoneScratch = new List<Entity>();

        private void PaintAllyBuffZone(Entity carrier, Vector2Int center, int tileRange)
        {
            if (tilemapMapView == null || carrier == Entity.Null) return;
            if (_allyZonePaint.ContainsKey(carrier)) return; // 같은 캐리어 이중 등록 = refcount 누수
            BuildZoneCells(center, tileRange, _zoneCellScratch);
            tilemapMapView.AddZoneCells(_zoneCellScratch);
            _allyZonePaint[carrier] = (center, tileRange);
        }

        // 보드 안 셀만 담는다 — 점등하는 것과 등록부가 서술하는 것이 같아야 refcount 를 읽을 수 있다.
        private void BuildZoneCells(Vector2Int center, int tileRange, List<Vector2Int> results)
        {
            results.Clear();
            int2 size = _generatedMap.IsCreated ? _generatedMap.gridSize : FallbackGridSize;
            for (int dx = -tileRange; dx <= tileRange; dx++)
            for (int dz = -tileRange; dz <= tileRange; dz++)
            {
                var cell = new Vector2Int(center.x + dx, center.y + dz);
                if (cell.x < 0 || cell.x >= size.x || cell.y < 0 || cell.y >= size.y) continue;
                results.Add(cell);
            }
        }

        // 등록부를 비우는 유일한 지점 — refcount 반납을 **같은 함수 안에서** 한다.
        // 둘을 떼어 놓으면(등록부만 비우고 타일 정리는 TilemapMapView.Clear 에 맡기면) 순서가
        // 바뀌는 순간 stale 엔트리가 새 매치의 refcount 를 깎아 살아 있는 장판의 발자국이 꺼진다 —
        // refcount 를 도입한 이유가 그것이었다. 뷰 쪽 ClearZoneCells 는 멱등이라 이중 호출도 안전.
        private void ClearAllyBuffZonePaint()
        {
            _allyZonePaint.Clear();
            if (tilemapMapView != null) tilemapMapView.ClearZoneCells();
        }

        // 살아 있는 캐리어가 아닌 항목의 점등을 반납한다. 칸별 refcount 라 겹친 장판이 서로의
        // 발자국을 지우지 않는다(TilemapMapView.RemoveZoneCells).
        private void DrainAllyBuffZoneVisuals()
        {
            if (_allyZonePaint.Count == 0) return;
            _zoneGoneScratch.Clear();
            foreach (var kv in _allyZonePaint)
                if (kv.Key == Entity.Null || !_em.Exists(kv.Key)
                    || !_em.HasComponent<Wassup.Battle.Effects.AllyBuffField>(kv.Key))
                    _zoneGoneScratch.Add(kv.Key);

            for (int i = 0; i < _zoneGoneScratch.Count; i++)
            {
                var gone = _zoneGoneScratch[i];
                if (tilemapMapView != null && _allyZonePaint.TryGetValue(gone, out var painted))
                {
                    BuildZoneCells(painted.center, painted.tileRange, _zoneCellScratch);
                    tilemapMapView.RemoveZoneCells(_zoneCellScratch);
                }
                _allyZonePaint.Remove(gone);
            }
        }

        // 배치 대기(PendingDeployment) 유닛 제외 — 아직 판에 서지 않았다(on-place 오라와 같은 규칙).
        // 월드 생존 가드는 레포 관용구(HasLiveEntityManager)를 쓴다 — `_em == default` 단독은
        // 어디서도 리셋되지 않아 티어다운 후에도 통과한다.
        // 남은 용도는 **로그 스냅샷 하나**다(시뮬 멤버십 권위는 ECS 의 DefenderTile).
        private void CollectAlliesInRange(Vector2Int center, int tileRange, List<Entity> results)
        {
            results.Clear();
            if (!HasLiveEntityManager()) return;
            var originInt = new int2(center.x, center.y);
            foreach (var kv in _defenderByTile)
            {
                var e = kv.Value.entity;
                if (e == Entity.Null || !_em.Exists(e)) continue;
                if (_em.HasComponent<PendingDeployment>(e)) continue;
                var cellInt = new int2(kv.Key.x, kv.Key.y);
                if (GridMath.ChebyshevDistance(cellInt, originInt) > tileRange) continue;
                results.Add(e);
            }
        }

        // 로그 스냅샷 전용 버퍼 하나(적용은 ECS 가 한다 — active-ally-zone unit 1).
        private readonly List<Entity> _allyLogScratch = new List<Entity>();

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
            int2 gridSize = _generatedMap.IsCreated ? _generatedMap.gridSize : FallbackGridSize;
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
            get { int2 g = _generatedMap.IsCreated ? _generatedMap.gridSize : FallbackGridSize; return new Vector2Int(g.x, g.y); }
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

            // active-ally-zone unit 2 — 장판 점등 회수는 **페이즈 무관**이다(위 적 dim 페이드와 같은 이유).
            // 승패는 `_running=false` 후 집계/결과 화면을 띄우는데, 그 사이에도 BattleSimGroup 은 돌아
            // 만료된 캐리어가 파괴된다. `_running` 아래에 두면 결과를 읽는 동안 아무것도 없는 자리에
            // 민트 타일이 켜진 채 남아 보드가 거짓을 보여준다.
            if (HasLiveEntityManager()) DrainAllyBuffZoneVisuals();

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
            DrainShieldBreakEvents();
            DrainDcTriggerFiredEvents(); // use-flow unit 3 — 발동 신호 → 아이콘 행 펄스
            DrainKnockupVisualEvents();  // knockup unit 3 — 띄우기 신호 → view 수직 호핑
            DrainUnitAttackVisualEvents();
            // beam unit 1 — 세션 TTL 은 **배틀 도메인 시간**으로 깎는다(공격 사건이 sim 시간).
            // 히트 VFX 를 RESOLVE 시점으로 미뤄 재생(위 PendingHitVfx 주석).
            TickPendingHitVfx(
                Wassup.Core.TimeControl.TimeManager.Instance.DeltaTime(Wassup.Core.TimeControl.TimeDomain.Battle));

            if (beamPresenter != null)
            {
                _beamViewResolver ??= ResolveBeamViewPos;
                beamPresenter.Tick(
                    Wassup.Core.TimeControl.TimeManager.Instance.DeltaTime(Wassup.Core.TimeControl.TimeDomain.Battle),
                    _beamViewResolver);
            }
            DrainProjectileHitEvents();
            DrainHealAppliedEvents();
            DrainShieldGrantedEvents();
            DrainDamageNumberEvents();
            DrainEnemyKilledEvents();
            DrainAttackOutputLogEvents();
            DrainHazardSpawnRequests();
            DrainPatrolSpawnRequests(); // summon-patrol-defender unit 3 — 소환 요청 캐리어
            DrainMeteorBarrageRequests(); // season-gimmick-clockout unit 4 — 사직서 임계 메테오 barrage
            DrainHazardRuntimeEvents();
            DrainHazardDestroyedEvents();
            DrainGoalCollapsedEvents();
            DrainGoalEvents();
            SyncGoalStability(); // goal-tower-siege — 타워 Health → 미러 + 패배 판정
            CheckTimer();
            CheckVictory();
        }

        private void LateUpdate()
        {
            // boss-jjangssen unit 6 rev 3 — **SyncMonoUnitViews 바로 앞이어야 한다.**
            // ECS 시뮬은 MonoBehaviour.Update 뒤에 돌므로, 도약이 발동한 프레임에는
            // Update 시점에 큐가 비어 있다. Update 에서 드레인하면 그 프레임 LateUpdate 가
            // 오버라이드 없이 sim 좌표(=이미 착지점)를 그려 **1프레임 팝**이 보인다.
            // drop-dismount 가 "시작 오버라이드는 동기 등록" 으로 같은 함정을 막은 것과 같은 이유.
            DrainBossLeapVisualEvents();
            // ultimate-leap unit 3 — 같은 이유로 SyncMonoUnitViews 앞이다: 이탈이 발동한 프레임에
            // 오버라이드를 못 걸면 그 프레임 피드가 sim 좌표(=출발지)를 그린 뒤 다음 프레임에
            // 튀어오른다. 착지도 마찬가지로 sim 이 이미 텔레포트한 좌표를 한 프레임 노출하게 된다.
            DrainUltimateLeapVisualEvents();
            // flight-lift-feel unit 3 — lift 노브는 뷰가 **매 프레임** 읽으므로 미러도 매 프레임이다.
            // 맵 빌드 1회 스냅샷(BlobShadow* 와 같은 자리)으로 두면 Play 중 인스펙터 튜닝이 안 먹어,
            // 같이 도입된 리듬·눌림 노브 8개(SO/코루틴이 매 프레임 읽음)와 비대칭이 된다.
            // 감각 튜닝 spec 이라 이 비대칭이 곧 작업 비용이다.
            MirrorLiftKnobs();
            SyncMonoUnitViews();
            ReconcileStatusFx();
            ReconcilePickupViews();
            ReconcileResignationViews();
            if (_em != null) _dcAuraPool?.Sync(_em); // 드림캐쳐 부착 오라 — 뷰 좌표 갱신 뒤 추종
            SyncProjectileViews();
        }

        // projectile-shot-sequence unit 3 — BattleBridge가 유일한 Mono↔ECS 경계다.
        // Pool은 활성 entity key만 제공하고, Bridge가 component를 plain view snapshot으로
        // 번역한다. Presentation은 EntityManager/LocalTransform/ProjectileState를 직접 읽지 않는다.
        private void SyncProjectileViews()
        {
            if (_em == null || _projectileViewPool == null) return;

            _projectileViewPool.CopyActiveEntities(_projectileViewScratch);
            for (int i = 0; i < _projectileViewScratch.Count; i++)
            {
                Entity entity = _projectileViewScratch[i];
                if (!_em.Exists(entity))
                {
                    _projectileViewPool.Despawn(entity);
                    continue;
                }

                var frame = new Wassup.Presentation.ProjectileViewFrame
                {
                    simPosition = _em.GetComponentData<LocalTransform>(entity).Position,
                };
                if (_em.HasComponent<ProjectileState>(entity))
                {
                    var state = _em.GetComponentData<ProjectileState>(entity);
                    frame.hasState = true;
                    frame.movement = state.movement;
                    frame.flightTime = state.flightTime;
                    frame.elapsed = state.elapsed;
                    frame.arcHeight = state.arcHeight;
                }
                _projectileViewPool.SyncTransform(entity, frame);
            }
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
                        // unit-status-fx 6 — Sleep(Zz)·Stun(별) 둘 다 CcEffect 소스. 한 버퍼
                        // 패스로 각 kind 활성 판정(둘 다 action-lock, 공존 가능 — 키 (e,kind)).
                        bool asleep = false, stunned = false;
                        for (int j = 0; j < buf.Length; j++)
                        {
                            if (buf[j].remainingTime <= 0f) continue;
                            if (buf[j].kind == CcKind.Sleep) asleep = true;
                            else if (buf[j].kind == CcKind.Stun) stunned = true;
                        }
                        if (!asleep && !stunned) continue;
                        var anchor = ResolveUnitViewTransform(ccEntities[i]);
                        if (anchor == null) continue;
                        if (asleep) statusFxSpawner.Ensure(ccEntities[i], Wassup.Data.StatusFxKind.Sleep, anchor);
                        if (stunned) statusFxSpawner.Ensure(ccEntities[i], Wassup.Data.StatusFxKind.Stun, anchor);
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

            // dot-effect-extraction unit 1 — 지속 피해 오라. **소스는 도트 자신**이다.
            // 도트가 자기 원소를 들고 다니므로 bridge 가 추측할 것이 없다 —
            // 스택 슬롯을 보던 래치·쿼리·매핑이 전부 사라졌다(옛 방식은 슬롯이 도트보다
            // 먼저 죽어서 종류를 기억해야 했고, 그 기억이 안 꺼져 얼음 오라가 매치 끝까지
            // 남는 결함을 낳았다).
            //
            // 꺼짐 처리를 따로 쓰지 않는다 — StatusFxSpawner 가 BeginFrame/EndFrame 으로
            // 그 프레임에 Ensure 안 된 것을 내린다. 도트가 끝나면 자동으로 사라진다.
            if (_dotEffectQueryCreated)
            {
                var dotEntities = _dotEffectQuery.ToEntityArray(Allocator.Temp);
                try
                {
                    for (int i = 0; i < dotEntities.Length; i++)
                    {
                        var e = dotEntities[i];
                        var dots = _em.GetBuffer<Wassup.Battle.Effects.DotEffect>(e, isReadOnly: true);
                        if (dots.Length == 0) continue;

                        Transform anchor = null;
                        for (int j = 0; j < dots.Length; j++)
                        {
                            if (dots[j].remainingTime <= 0f) continue;
                            var fx = DotAuraKind(dots[j].element);
                            if (!fx.HasValue) continue;
                            // 앵커 해석은 비싸므로 켤 것이 실제로 있을 때만.
                            if (anchor == null)
                            {
                                anchor = ResolveUnitViewTransform(e);
                                if (anchor == null) break;
                            }
                            statusFxSpawner.Ensure(e, fx.Value, anchor);
                        }
                    }
                }
                finally
                {
                    dotEntities.Dispose();
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

        // season-gimmick-clockout unit 1 — 사직서 엔티티↔뷰 poll-reconcile (ReconcilePickupViews 동형).
        // 순수 ECS 스폰이라 이벤트 없이 매 프레임 조정. _running 무관.
        private void ReconcileResignationViews()
        {
            if (_em == null || !_resignationViewQueryCreated) return;

            var entities = _resignationViewQuery.ToEntityArray(Allocator.Temp);
            try
            {
                for (int i = 0; i < entities.Length; i++)
                {
                    var e = entities[i];
                    if (_resignationVisualMap.ContainsKey(e)) continue;
                    var r = _em.GetComponentData<Wassup.Battle.Effects.Resignation>(e);
                    // 셀 중심 → view. Pickup 과 동일하게 BoardSpace.ToView 경유(+0.5 Tilemap 중심 보정).
                    float3 simCenter = GridToWorldCenter(new Vector2Int(r.cell.x, r.cell.y));
                    Vector3 pos = (Vector3)Wassup.Core.BoardSpace.ToView(simCenter) + Vector3.up * resignationViewHeight;
                    var go = new GameObject($"Resignation_{r.cell.x}_{r.cell.y}");
                    go.transform.SetParent(transform, worldPositionStays: false);
                    go.transform.position = pos;
                    go.AddComponent<Wassup.Battle.Effects.ResignationPresenter>().Init(resignationViewPrefab, 0f);
                    _resignationVisualMap[e] = go;
                }

                // 사라진 엔티티(임계 소모) → 뷰 파괴.
                if (_resignationVisualMap.Count > 0)
                {
                    _resignationReapBuffer.Clear();
                    foreach (var kv in _resignationVisualMap)
                        if (!_em.Exists(kv.Key)) _resignationReapBuffer.Add(kv.Key);
                    for (int i = 0; i < _resignationReapBuffer.Count; i++)
                    {
                        var key = _resignationReapBuffer[i];
                        if (_resignationVisualMap.TryGetValue(key, out var go) && go != null)
                            Destroy(go);
                        _resignationVisualMap.Remove(key);
                    }
                }
            }
            finally
            {
                entities.Dispose();
            }
        }

        // season-gimmick-clockout unit 1 — 사직서 뷰 전체 정리 (매치 teardown).
        private void ClearResignationVisuals()
        {
            foreach (var kv in _resignationVisualMap)
                if (kv.Value != null) Destroy(kv.Value);
            _resignationVisualMap.Clear();
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
            bool unifiedOverhead = UnifiedOverheadActive;
            if (unifiedOverhead) unitOverheadUiLayer.BeginFrame();
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
                            // boss-jjangssen unit 6·7 — 도약 비행 중이면 뷰는 아치를 따른다(sim 은 이미
                            // 착지 셀). **수평만 여기서 치환**하고 높이는 아래 뷰의 view 공간 오프셋으로
                            // 넘긴다 — ToView 가 sim-Y 를 버리므로 높이를 여기 섞으면 평면화된다.
                            bool leaping = TryGetEnemyViewOverride(entity, out var leapPos, out float leapHeight);
                            if (leaping) world = new Vector3(leapPos.x, leapPos.y, leapPos.z);
                            // unit-health-display unit 1 — 적 저체력 틴트. HP read-only 평가는
                            // BattleBridge 소관(ECS 창구), 뷰는 Color 만 받아 적용.
                            Color tint = unifiedOverhead ? Color.white : EvaluateEnemyHealthTint(entity);
                            // placement-enemy-see-through unit 3 — 적만 dim(디펜더 루프는 미적용).
                            // SetDimmed 를 SetHealthTint 앞에 — quad 는 SetHealthTint 가 알파를 반영한다.
                            bool dimmed = _enemyDimAlpha < 0.999f;
                            if (spineUnitPool != null && spineUnitPool.TryGet(entity, out var spineView))
                            {
                                // 비행 아니면 0 을 써서 스스로 해제된다(별도 clear 경로 불필요).
                                spineView.SetFlightHeight(leaping ? leapHeight : 0f);
                                spineView.UpdatePosition(world);
                                if (canSort) spineView.UpdateSortingOrder(gridSize, tileSize);
                                spineView.SetDimmed(dimmed, _enemyDimAlpha);
                                spineView.SetHealthTint(tint);
                            }
                            else if (enemyViewPool.TryGet(entity, out var view))
                            {
                                view.SetFlightHeight(leaping ? leapHeight : 0f);
                                view.UpdatePosition(world);
                                if (canSort) view.UpdateSortingOrder(gridSize, tileSize);
                                view.SetDimmed(dimmed, _enemyDimAlpha);
                                view.SetHealthTint(tint);
                            }
                            if (unifiedOverhead && _em.HasComponent<Health>(entity)
                                && TryGetUnitScreenAnchor(entity, out var enemyScreenAnchor, out var enemyAnchor))
                            {
                                var h = _em.GetComponentData<Health>(entity);
                                unitOverheadUiLayer.SetUnit(entity, false, Health.ComputeRatio(h.value, h.max),
                                    enemyScreenAnchor, ProjectTileScreenWidth(enemyAnchor), 0f, GatherOverheadStacks(entity));
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
                // defender-relocation unit 6 — 비행 중엔 컨트롤러가 준 오버라이드가 뷰 위치를 대신한다.
                // 오버라이드는 **VIEW 좌표**(ToView 우회) — 평면 정면뷰(BoardSpace.ToView)가 sim 높이를 버려
                // 아치가 평면이 되던 문제 교정(비행 곡선은 view 공간에서 계산). 비행은 PendingDeployment(비전투)라
                // 게이지/오버헤드 UI 는 생략하고 이 엔티티 처리를 종료한다(착지 후 정상 경로가 복원).
                if (TryGetDefenderViewOverride(entity, out var relocFlightView, out float relocLift,
                        out var relocGround))
                {
                    if (spineUnitPool != null && spineUnitPool.TryGet(entity, out var flightSpine) && flightSpine != null)
                        flightSpine.SetFlightView(relocFlightView, relocLift, relocGround);
                    else if (defenderFallbackViewPool != null &&
                             defenderFallbackViewPool.TryGet(entity, out var flightFallback) && flightFallback != null)
                        flightFallback.transform.position = relocFlightView;
                    continue;
                }
                var world = new Vector3(p.x, p.y + spineDefenderYOffset, p.z);
                // unit-health-display unit 3 — 타일 게이지: defender HP read-only → 타일 중심(바닥)
                // view 좌표로 Set. 만피 숨김은 레이어가 처리.
                if (!unifiedOverhead && tileHealthGaugeLayer != null && _em.HasComponent<Health>(entity))
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
                if (unifiedOverhead && _em.HasComponent<Health>(entity)
                    && TryGetUnitScreenAnchor(entity, out var defenderScreenAnchor, out var defenderAnchor))
                {
                    var h = _em.GetComponentData<Health>(entity);
                    // shield-guardian-defender unit 2 — 실드합 동승(read-only 폴링, 계약 8).
                    // 정규화(HP+실드 > 100% 압축)는 뷰가 수행.
                    float defShieldRatio = 0f;
                    if (h.max > 0f && _em.HasBuffer<Wassup.Battle.Units.ShieldSlot>(entity))
                        defShieldRatio = Wassup.Battle.Units.ShieldMath.Sum(
                            _em.GetBuffer<Wassup.Battle.Units.ShieldSlot>(entity, isReadOnly: true)) / h.max;
                    unitOverheadUiLayer.SetUnit(entity, true, Health.ComputeRatio(h.value, h.max),
                        defenderScreenAnchor, ProjectTileScreenWidth(defenderAnchor), defShieldRatio, GatherOverheadStacks(entity));
                }
            }
            // goal-stability unit 5 — 골 게이지도 유닛과 같은 오버헤드 창(Begin/EndFrame) 안에서 Set.
            SyncGoalOverheadGauges(unifiedOverhead);
            SyncPatrolViews(unifiedOverhead, canSort, gridSize);
            if (unifiedOverhead) unitOverheadUiLayer.EndFrame();
            // three-minute-survival unit 1 — 골 안정도 바. EndFrame 뒤에 둔다(유닛 풀의
            // _seen 소거와 무관한 별도 슬롯이라 순서 의존은 없지만, 유닛 바 위에 그려진다).
            if (unifiedOverhead) SyncGoalStabilityBars();
        }

        // three-minute-survival unit 1 — 골 셀마다 안정도 바 1개. 값은 공유 1개라 두 바가 같은
        // 숫자를 밀살 표시한다. 전투 중에만 보이고 그 외에는 슬롯을 접는다.
        private void SyncGoalStabilityBars()
        {
            if (!_running || _goalStabilityMax <= 0 || !_generatedMap.IsCreated)
            {
                unitOverheadUiLayer.HideStability();
                return;
            }
            var cam = Camera.main;
            if (cam == null) return;

            float ratio = Wassup.Battle.Units.Health.ComputeRatio(_goalStability, _goalStabilityMax);
            string label = _goalStability.ToString();
            bool hasList = _generatedMap.goals.IsCreated && _generatedMap.goals.Length > 0;
            int count = hasList ? _generatedMap.goals.Length : 1;
            for (int i = 0; i < count; i++)
            {
                int2 cell = hasList ? _generatedMap.goals[i] : _generatedMap.goal;
                // primary 골은 구조물 시각 앵커를 쓴다(구조물이 없는 테마는 셀 중심으로 폴백).
                Vector3 world;
                if (i == 0 && tilemapMapView != null && tilemapMapView.TryGetGoalVisualAnchor(out var anchor))
                    world = anchor;
                else
                    // sim → view 변환을 반드시 거친다. GridToWorldCenterVector 만 쓰면
                    // Tilemap 모드에서 반 타일 어긋나 바가 모서리에 놓인다(:2764 선례).
                    world = GridCellToViewCenter(new Vector2Int(cell.x, cell.y));
                world.y += goalStabilityBarLift;

                Vector3 sp = cam.WorldToScreenPoint(world);
                if (sp.z <= 0f) continue; // 카메라 뒤 — 투영이 뒤집힌다
                Vector3 half = Vector3.right * (tileSize * 0.5f);
                Vector3 a = cam.WorldToScreenPoint(world - half);
                Vector3 b = cam.WorldToScreenPoint(world + half);
                float tileScreenWidth = Vector2.Distance(new Vector2(a.x, a.y), new Vector2(b.x, b.y));
                unitOverheadUiLayer.SetStability(i, ratio, label, new Vector2(sp.x, sp.y), tileScreenWidth);
            }
        }

        // summon-patrol-defender unit 5 — 거점 순찰 아군 뷰 동기화.
        //
        // **전용 루프가 필요하다.** 위 두 루프는 각각 `AttackUnitTag` 쿼리(적)와
        // `_defenderByTile` 순회(방어유닛)인데, 순찰병은 둘 다 아니다 — 적 태그를 안 붙이고
        // (계약 1) 타일 딕셔너리에도 안 들어간다(DefenderTile 미부착). 이 루프가 없으면
        // 뷰가 스폰만 되고 **영원히 제자리에 서 있는다**(이 아키타입 고유의 함정).
        //
        // 뷰 회수는 spineUnitPool.DespawnMissing 이 이미 처리한다(엔티티 소멸 기준).
        private void SyncPatrolViews(bool unifiedOverhead, bool canSort, int2 gridSize)
        {
            // 형제 드레인(DrainPatrolSpawnRequests)과 같은 가드로 통일 — `_em == default` 만
            // 보면 월드가 파괴된 뒤 CreateEntityQuery 가 던진다.
            if (!HasLiveEntityManager()) return;
            using var query = _em.CreateEntityQuery(
                ComponentType.ReadOnly<Wassup.Battle.Movement.PatrolAnchor>(),
                ComponentType.ReadOnly<LocalTransform>());
            if (query.IsEmpty) return;

            using var entities = query.ToEntityArray(Allocator.Temp);
            for (int i = 0; i < entities.Length; i++)
            {
                var entity = entities[i];
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

                // 체력 표시 — HP 보유 완전 유닛이고 "죽고 다시 나는" 것이 이 유닛의 핵심
                // 피드백이라 숨기지 않는다(파이프라인 커버리지 '체력 표시' 행).
                if (unifiedOverhead && _em.HasComponent<Health>(entity)
                    && TryGetUnitScreenAnchor(entity, out var garScreenAnchor, out var garAnchor))
                {
                    var h = _em.GetComponentData<Health>(entity);
                    float garShieldRatio = 0f;
                    if (h.max > 0f && _em.HasBuffer<Wassup.Battle.Units.ShieldSlot>(entity))
                        garShieldRatio = Wassup.Battle.Units.ShieldMath.Sum(
                            _em.GetBuffer<Wassup.Battle.Units.ShieldSlot>(entity, isReadOnly: true)) / h.max;
                    unitOverheadUiLayer.SetUnit(entity, true, Health.ComputeRatio(h.value, h.max),
                        garScreenAnchor, ProjectTileScreenWidth(garAnchor), garShieldRatio, GatherOverheadStacks(entity));
                }
            }
        }

        // unit-overhead-ui 확장(unit 8) — 오버헤드 스택행 gather 재사용 버퍼(프레임 GC 회피).
        private readonly System.Collections.Generic.List<Wassup.Data.OverheadStackEntry> _overheadStackScratch = new();

        // 유닛별 활성 스택 수집(듀얼소스, RO): StackModifierSlot(피로도 등) + HeatAccrual(열기).
        // 반환 = 재사용 버퍼 — SetUnit→view.Show 가 동프레임 동기 소비(슬롯 복사)하므로 안전.
        private System.Collections.Generic.List<Wassup.Data.OverheadStackEntry> GatherOverheadStacks(Entity entity)
        {
            _overheadStackScratch.Clear();
            if (_em.HasBuffer<Wassup.Battle.Effects.StackModifierSlot>(entity))
            {
                var buf = _em.GetBuffer<Wassup.Battle.Effects.StackModifierSlot>(entity, isReadOnly: true);
                for (int i = 0; i < buf.Length; i++)
                {
                    var s = buf[i];
                    if (s.stackCount <= 0) continue;
                    if (TryMapOverheadStackKind(s.kind, out var okind))
                        _overheadStackScratch.Add(new Wassup.Data.OverheadStackEntry { kind = okind, count = s.stackCount });
                }
            }
            if (_em.HasComponent<Wassup.Battle.Effects.HeatAccrual>(entity))
            {
                var heat = _em.GetComponentData<Wassup.Battle.Effects.HeatAccrual>(entity);
                if (heat.stacks > 0)
                    _overheadStackScratch.Add(new Wassup.Data.OverheadStackEntry
                    { kind = Wassup.Data.OverheadStackKind.Heat, count = heat.stacks });
            }
            return _overheadStackScratch;
        }

        // Battle.StackKind → OverheadStackKind. 현재 피로도만 아이콘화(나머지 후속). 미매핑 = false.
        private static bool TryMapOverheadStackKind(Wassup.Battle.Effects.StackKind kind,
            out Wassup.Data.OverheadStackKind result)
        {
            switch (kind)
            {
                case Wassup.Battle.Effects.StackKind.Fatigue:
                    result = Wassup.Data.OverheadStackKind.Fatigue; return true;
                default:
                    result = default; return false;
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
                // beam unit 1 — 쏘던 유닛이 죽으면 빔이 허공에 남는다. TTL 만료를 기다리지 않고 즉시 끊는다.
                if (beamPresenter != null && hasBinding) beamPresenter.Close(binding.Item1);
                if (spineUnitPool != null && hasBinding)
                {
                    spineUnitPool.NotifyDeath(binding.entity);
                    defenderFallbackViewPool?.Despawn(binding.entity);
                }
                _defenderByTile.Remove(cell);
                _occupiedTiles.Remove(cell);
                RefreshPlacementHighlightIfShown(); // placement-eligible-tile-highlight unit 2
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
                    DefenderDied?.Invoke(binding.entity, binding.data, GridCellToViewCenter(cell));
            }
        }

        // dreamcatcher-shield-break unit 2 — 실드 피격 파열 이벤트 드레인. payload 분기:
        // SelfTileAoe(A) = SkyFall×TileAoe 폭발(OnDeath/메테오 동형), AreaSleep(B) = 근접 M명 수면.
        // use-flow unit 3 — 부착 카드 발동 신호 → 아이콘 행 펄스 + **부착 임팩트 재사용**
        // (rev 2, 사용자 피드백 "이펙트가 없어 보인다"): 유닛 몸 펀치 + 흰 플래시 + 카드 흡수
        // 링/버스트 VFX. 부착 순간 박히던 그 임팩트가 발동 순간 다시 친다 — 인과 언어 일치.
        // 카메라 킥·흡수 SFX 는 제외(주기 발동 연타에 멀미/소음). 같은 프레임 같은 host 다발
        // 발동은 1회로 코얼레스(월드 임팩트 중첩 방지 — UI 펄스는 뷰가 자체 코얼레스).
        private readonly System.Collections.Generic.HashSet<Entity> _dcFiredScratch = new();
        // rev 2 연발 스로틀 — 주기 발동이 촘촘한 유닛(머신거너 등)에서 월드 임팩트(펀치/플래시/
        // VFX)가 도배되는 것을 host 당 최소 간격으로 막는다. UI 펄스는 스로틀하지 않는다
        // (뷰가 타이머 재시작으로 자체 코얼레스 — 발동 사실 자체는 매번 알린다).
        [SerializeField] private float dcProcImpactMinIntervalSec = 0.25f;
        private readonly System.Collections.Generic.Dictionary<Entity, float> _dcProcLastImpact = new();

        private void DrainDcTriggerFiredEvents()
        {
            if (!_dcTriggerFiredQueue.IsCreated) return;
            _dcFiredScratch.Clear();
            while (_dcTriggerFiredQueue.TryDequeue(out var evt))
            {
                if (!_dcFiredScratch.Add(evt.host)) continue;
                if (unitOverheadUiLayer != null) unitOverheadUiLayer.PulseCards(evt.host);
                bool impactReady = !_dcProcLastImpact.TryGetValue(evt.host, out float last)
                    || Time.unscaledTime - last >= dcProcImpactMinIntervalSec;
                if (impactReady && spineUnitPool != null
                    && spineUnitPool.TryGet(evt.host, out var view) && view != null)
                {
                    view.PlayPunch();
                    view.FlashWhite();
                    SpawnCardAbsorbVfx(view.transform.position);
                    _dcProcLastImpact[evt.host] = Time.unscaledTime;
                }
            }
        }

        // beam unit 1 — 씬에 배선돼 있으면 그것을, 없으면 첫 사용 시 만들어 쓴다.
        // 자동 생성 폴백을 두는 이유: 이 기능만을 위해 공용 씬을 저장하면 그 시점의 미저장 WIP 가
        // 같이 박힌다. 프레젠터는 무상태(TTL 은 호출측이 준다)라 자동 생성이어도 잃는 튜닝이 없다.
        // 빔 양 끝의 view 위치. 매 프레임 delegate 를 새로 만들지 않도록 캐시한다.
        private Wassup.Presentation.BeamPresenter.ViewPosResolver _beamViewResolver;

        // 발사점은 cast anchor 우선(TrySpawnCastVfx 와 같은 경로), 그 다음 유닛 view,
        // **마지막으로 ECS 위치**. 뷰 풀만 보면 풀에 없는 유닛(폴백 뷰·워밍업·비-Spine 적)이
        // 끝점일 때 빔이 통째로 죽는다 — 배치 스킬 빔이 전멸했던 실제 원인이 이것이었다.
        private bool ResolveBeamViewPos(Entity entity, bool useAnchor, out Vector3 pos)
        {
            pos = default;
            if (entity == Entity.Null) return false;
            if (spineUnitPool != null)
            {
                if (useAnchor && spineUnitPool.TryResolveAnchor(entity, out pos)) return true;
                if (spineUnitPool.TryGet(entity, out var view) && view != null)
                {
                    pos = view.transform.position; // 이미 view 공간
                    return true;
                }
            }
            if (!_em.Exists(entity) || !_em.HasComponent<LocalTransform>(entity)) return false;
            pos = (Vector3)Wassup.Core.BoardSpace.ToView(_em.GetComponentData<LocalTransform>(entity).Position);
            return true;
        }

        private Wassup.Presentation.BeamPresenter EnsureBeamPresenter()
        {
            if (beamPresenter != null) return beamPresenter;
            var go = new GameObject("BeamPresenter (auto)");
            go.transform.SetParent(transform, false);
            beamPresenter = go.AddComponent<Wassup.Presentation.BeamPresenter>();
            return beamPresenter;
        }

        // knockup-fighter-defender unit 3 — 띄우기 신호 → 대상 view 수직 호핑.
        // 같은 프레임에 같은 대상이 여러 번 들어오면(다중 히트) 마지막 것으로 갱신 —
        // 재생 중 재신호는 뷰가 타이머를 재시작해 자체 코얼레스한다.
        private void DrainKnockupVisualEvents()
        {
            if (!_knockupVisualQueue.IsCreated) return;
            while (_knockupVisualQueue.TryDequeue(out var evt))
            {
                if (evt.durationSec <= 0f || evt.height <= 0f) continue;
                if (spineUnitPool != null && spineUnitPool.TryGet(evt.target, out var view) && view != null)
                    view.PlayKnockupHop(evt.durationSec, evt.height);
            }
        }

        private void DrainShieldBreakEvents()
        {
            if (!_shieldBreakQueue.IsCreated) return;
            var targets = new System.Collections.Generic.List<(Entity entity, Vector2Int cell)>();
            while (_shieldBreakQueue.TryDequeue(out var evt))
            {
                // use-flow unit 3 — OnShieldBreak/피격트리거 payload 실행 = 부착 카드가 일한
                // 순간. 이 채널은 이미 host 를 실어오므로 신규 채널 없이 펄스가 공짜다.
                if (evt.payload != Wassup.Data.DcPayloadKind.None && unitOverheadUiLayer != null)
                    unitOverheadUiLayer.PulseCards(evt.host);

                var logger = GameManager.Instance?.Logger;
                int2 grid = _generatedMap.IsCreated ? _generatedMap.gridSize : FallbackGridSize;
                var hostCell = GridMath.WorldToCell(evt.position, tileSize, grid, origin: _boardOrigin);
                Logging.ShieldBreakLog log = logger != null
                    ? new Logging.ShieldBreakLog
                    {
                        host_unit = FindDefenderData(evt.host)?.displayName ?? "<unknown>",
                        tile = new Vector2Int(hostCell.x, hostCell.y),
                        // trigger-gates unit 0 — 채널 공유 origin 구분 (실드파열 vs 피격트리거).
                        payload = (evt.fromDamagedTrigger ? "damaged-trigger:" : "") + evt.payload,
                    }
                    : null;

                if (evt.payload == Wassup.Data.DcPayloadKind.SelfTileAoe)
                {
                    // 실드 파열 폭발 — OnDeath 폭발/메테오와 동형. bake 가 AoE view 없으면 슬롯 자체를
                    // 스킵하므로 aoeDataIndex 는 정상 >=0. 실제 데미지는 투사체(ProjectileHitSystem)가
                    // 해결 — 로그의 대상은 cast 시점 범위 내 적 스냅샷(raw magnitude, cap 0 = 투사체 동일).
                    if (evt.aoeDataIndex >= 0)
                    {
                        SpawnProjectile(new ProjectileSpawnRequest
                        {
                            movement = MovementKind.SkyFall,
                            payload = PayloadKind.TileAoe,
                            impact = evt.position,
                            damage = evt.magnitude,
                            impactTileRange = evt.tileRange,
                            flightTime = 0f,
                            dataIndex = evt.aoeDataIndex,
                            visualScale = 1f,
                            // trigger-gates 후속 결정(2026-07-25, 투트랙 리뷰 B-M1) — 폭발 킬
                            // 귀속 통일: owner=host 라 궁지폭발/실드폭발 킬도 주인의 킬로
                            // 인정되어 OnKill 연쇄·위협 귀속이 발동한다 (시체폭발 owner=killer,
                            // 진동갑주 owner=self 와 동일 원칙). host 가 같은 프레임 사망해도
                            // verbatim 복사일 뿐 역참조 없음(corpse killer 선례).
                            owner = evt.host,
                        }, Entity.Null);
                        if (log != null)
                        {
                            CollectShieldBreakTargets(evt.position, evt.tileRange, 0, targets);
                            foreach (var t in targets)
                                log.targets.Add(new Logging.ShieldBreakTargetLog
                                { tile = t.cell, effect = "Damage", magnitude = evt.magnitude });
                        }
                    }
                }
                else if (evt.payload == Wassup.Data.DcPayloadKind.AreaSleep)
                {
                    int cap = (int)evt.magnitude;
                    if (cap >= 1 && evt.tileRange >= 1 && evt.duration > 0f)
                    {
                        CollectShieldBreakTargets(evt.position, evt.tileRange, cap, targets);
                        foreach (var t in targets)
                        {
                            Wassup.Battle.Effects.EffectSpawner.ApplyCc(_em, t.entity,
                                new Wassup.Battle.Effects.CcEffect
                                {
                                    kind = Wassup.Battle.Effects.CcKind.Sleep,
                                    remainingTime = evt.duration,
                                });
                            if (log != null)
                                log.targets.Add(new Logging.ShieldBreakTargetLog
                                { tile = t.cell, effect = "Sleep", magnitude = evt.duration });
                        }
                    }
                }

                if (logger != null) logger.RecordShieldBreak(log);
            }
        }

        // dreamcatcher-shield-break unit 2/5 — 실드 파열 AoE 대상 수집(공유). bomb-thrower AoE
        // (ProjectileHitSystem) 패턴 미러: WorldToCell + TileAoe.IsInTileRange 범위 필터 →
        // AoeTargetCap.SelectNearest(거리² cap, 결정론). cap<=0 = 범위 전체(투사체 폭발과 동일).
        // 호출측: 수면=결과에 ApplyCc(Sleep) + 로그, 데미지=투사체가 별도 해결(여기선 로그용 스냅샷).
        private void CollectShieldBreakTargets(float3 center, int tileRange, int cap,
            System.Collections.Generic.List<(Entity entity, Vector2Int cell)> results)
        {
            results.Clear();
            int2 grid = _generatedMap.IsCreated ? _generatedMap.gridSize : FallbackGridSize;
            var centerCell = GridMath.WorldToCell(center, tileSize, grid, origin: _boardOrigin);
            using var enemyQuery = _em.CreateEntityQuery(
                ComponentType.ReadOnly<AttackUnitTag>(),
                ComponentType.ReadOnly<LocalTransform>());
            var enemies = enemyQuery.ToEntityArray(Allocator.Temp);
            var xforms = enemyQuery.ToComponentDataArray<LocalTransform>(Allocator.Temp);
            var inRange = new NativeList<int>(Allocator.Temp);
            var inRangeDistSq = new NativeList<float>(Allocator.Temp);
            for (int i = 0; i < enemies.Length; i++)
            {
                float3 vpos = xforms[i].Position;
                var cell = GridMath.WorldToCell(vpos, tileSize, grid, origin: _boardOrigin);
                if (!Wassup.Battle.Combat.TileAoe.IsInTileRange(cell, centerCell, tileRange)) continue;
                inRange.Add(i);
                float dx = vpos.x - center.x;
                float dz = vpos.z - center.z;
                inRangeDistSq.Add(dx * dx + dz * dz);
            }
            var selected = new NativeList<int>(Allocator.Temp);
            Wassup.Battle.Combat.AoeTargetCap.SelectNearest(inRangeDistSq.AsArray(), cap, ref selected);
            for (int s = 0; s < selected.Length; s++)
            {
                int idx = inRange[selected[s]];
                var vpos = xforms[idx].Position;
                var cell = GridMath.WorldToCell(vpos, tileSize, grid, origin: _boardOrigin);
                results.Add((enemies[idx], new Vector2Int(cell.x, cell.y)));
            }
            enemies.Dispose();
            xforms.Dispose();
            inRange.Dispose();
            inRangeDistSq.Dispose();
            selected.Dispose();
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

                // battle-audio: per-unit 공격 실행 SFX(근접 클래스 등). 투사체 유닛은 clip 미할당 → 무음.
                Wassup.Core.SoundManager.Instance?.PlayAttack(defData.attackSfxClip);

                if (defData.attackVfxPrefab != null)
                {
                    // 방향성 히트(흙 폭발 등)는 공격자→대상 방향으로 회전시킨다. 방향은 **view 공간**
                    // 에서 구한다 — sim 방향을 그대로 쓰면 평면 보드에서 엉뚱한 축으로 돈다.
                    Vector3 hitFacing = default;
                    if (defData.attackVfxFacesTarget
                        && ResolveBeamViewPos(evt.attacker, true, out var atkView))
                    {
                        hitFacing = (Vector3)Wassup.Core.BoardSpace.ToView(evt.targetWorld) - atkView;
                    }
                    // ⚠ 이 시각 이벤트는 공격 **START** 에 나온다(애니 트리거 겸용). 피해는
                    // hitDelaySec 뒤 RESOLVE 에 들어가므로, 그대로 재생하면 이펙트가 타격보다
                    // 먼저 터진다 — 파이터 4종이 전부 hitDelaySec 0.3 이라 눈에 띄었다.
                    // 배틀 도메인 시간으로 미뤄 RESOLVE 시점에 맞춘다(슬로모에서도 동기 유지).
                    if (defData.hitDelaySec > 0f)
                        _pendingHitVfx.Add(new PendingHitVfx
                        {
                            prefab = defData.attackVfxPrefab,
                            simPos = evt.targetWorld,
                            scale = defData.attackVfxScale,
                            facing = hitFacing,
                            euler = defData.attackVfxEulerOffset,
                            remaining = defData.hitDelaySec,
                        });
                    else
                        _projectileViewPool?.PlayHit(defData.attackVfxPrefab, evt.targetWorld,
                            scale: defData.attackVfxScale, facingViewDir: hitFacing,
                            eulerOffset: defData.attackVfxEulerOffset);
                }

                // beam-ranger-defender unit 1 — 빔 유닛이면 이 공격 사건으로 세션을 열거나 잇는다.
                // "빔 유닛인가"는 SO 의 프리팹 유무가 결정한다(id/kind 분기 없음).
                // 좌표는 view 공간으로 넘긴다 — 평면 보드라 sim 좌표를 그대로 쓰면 빔이 눕는다.
                // 빔 세션 TTL 은 이 공격의 **실발사 주기**에서 온다(attackAnimPeriod 는 attackSpeedMul
                // 까지 반영된 값). 상수로 박으면 공속 버프나 주기가 다른 두 번째 빔 유닛에서 깜빡인다.
                // 남는 건 무차원 여유 계수 하나 — 사건이 조금 늦어도 빔이 끊기지 않을 만큼.
                if (defData.beamVfxPrefab != null && evt.attackAnimPeriod > 0f)
                {
                    EnsureBeamPresenter().Open(
                        evt.attacker,
                        defData.beamVfxPrefab,
                        source: evt.attacker,
                        target: evt.target,
                        ttlSec: evt.attackAnimPeriod * BeamSessionTtlMargin);
                }

                TrySpawnCastVfx(evt.attacker, targetWorld);
            }
        }

        // 히트 VFX 지연 재생 큐(위 주석 — START 이벤트를 RESOLVE 시점으로 미룬다).
        // 코루틴 대신 리스트+틱: 배틀 도메인 시간을 쓰려면 어차피 직접 깎아야 하고,
        // 공격마다 코루틴을 새로 만들면 고속 공격 유닛에서 할당이 쌓인다.
        private struct PendingHitVfx
        {
            public GameObject prefab;
            public float3 simPos;
            public float scale;
            public Vector3 facing;
            public Vector3 euler;
            public float remaining;
        }
        private readonly System.Collections.Generic.List<PendingHitVfx> _pendingHitVfx = new();

        private void TickPendingHitVfx(float battleDeltaTime)
        {
            for (int i = _pendingHitVfx.Count - 1; i >= 0; i--)
            {
                var p = _pendingHitVfx[i];
                p.remaining -= battleDeltaTime;
                if (p.remaining > 0f) { _pendingHitVfx[i] = p; continue; }
                _projectileViewPool?.PlayHit(p.prefab, p.simPos, scale: p.scale,
                    facingViewDir: p.facing, eulerOffset: p.euler);
                _pendingHitVfx.RemoveAt(i);
            }
        }

        // dot-effect-extraction unit 1 — 도트 **원소** → 오라 kind. origin 은 보지 않는다:
        // 장판이 준 화염이든 스택 폭발이 준 화염이든 화면에는 같은 그림이어야 한다.
        // None(원소 없는 도트)은 null = 오라 없음.
        private static Wassup.Data.StatusFxKind? DotAuraKind(Wassup.Battle.Effects.DotElement e) => e switch
        {
            Wassup.Battle.Effects.DotElement.Bleed  => Wassup.Data.StatusFxKind.Bleed,
            Wassup.Battle.Effects.DotElement.Fire   => Wassup.Data.StatusFxKind.Fire,
            Wassup.Battle.Effects.DotElement.Ice    => Wassup.Data.StatusFxKind.Ice,
            Wassup.Battle.Effects.DotElement.Poison => Wassup.Data.StatusFxKind.Poison,
            _ => null,
        };

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
                int2 grid = _generatedMap.IsCreated ? _generatedMap.gridSize : FallbackGridSize;
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

        // shield-guardian-defender unit 4 — 실드 부여 원샷 VFX. ShieldCastSystem 이
        // 부여 대상 위치마다 enqueue → 대상 위치에 단발 이펙트.
        private void DrainShieldGrantedEvents()
        {
            if (!_shieldGrantedEventQueue.IsCreated) return;
            if (vfxSpawner == null) { _shieldGrantedEventQueue.Clear(); return; }
            while (_shieldGrantedEventQueue.TryDequeue(out var evt))
                vfxSpawner.SpawnShieldGranted(new Vector3(evt.position.x, evt.position.y, evt.position.z));
        }

        // Enemy-only floating damage numbers. DamageApplicationSystem enqueues one
        // event per enemy whose IncomingDamage was applied; spawn a popup at each.
        private void DrainDamageNumberEvents()
        {
            if (!_damageNumberEventQueue.IsCreated) return;
            bool hasNumbers = damageNumberSpawner != null;
            bool hasBars = !UnifiedOverheadActive && enemyHitBarSpawner != null;
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

        // unit-overhead-ui — Y는 실제 renderer top, X는 무기 bounds에 끌려가지 않는 visual pivot.
        // 이 조합이 포즈/키별 5px 높이와 머리 중앙 정렬을 동시에 지킨다.
        private bool TryGetUnitScreenAnchor(Entity entity, out Vector2 screenAnchor, out Transform anchor)
        {
            screenAnchor = default;
            anchor = null;
            var cam = Camera.main;
            if (cam == null) return false;
            if (spineUnitPool != null && spineUnitPool.TryGet(entity, out var sv) && sv != null
                && sv.TryGetScreenRect(cam, out var rect))
            {
                anchor = sv.transform;
                screenAnchor = Wassup.Presentation.UnitOverheadLayout.ScreenAnchor(
                    cam.WorldToScreenPoint(anchor.position).x, rect);
                return true;
            }
            if (enemyViewPool != null && enemyViewPool.TryGet(entity, out var qv) && qv != null
                && qv.TryGetScreenRect(cam, out rect))
            {
                anchor = qv.transform;
                screenAnchor = Wassup.Presentation.UnitOverheadLayout.ScreenAnchor(
                    cam.WorldToScreenPoint(anchor.position).x, rect);
                return true;
            }
            if (defenderFallbackViewPool != null && defenderFallbackViewPool.TryGet(entity, out var fv) && fv != null
                && fv.TryGetScreenRect(cam, out rect))
            {
                anchor = fv.transform;
                screenAnchor = Wassup.Presentation.UnitOverheadLayout.ScreenAnchor(
                    cam.WorldToScreenPoint(anchor.position).x, rect);
                return true;
            }
            return false;
        }

        private float ProjectTileScreenWidth(Transform anchor)
        {
            var cam = Camera.main;
            if (cam == null || anchor == null) return 1f;
            Vector3 half = Vector3.right * (tileSize * 0.5f);
            Vector3 a = cam.WorldToScreenPoint(anchor.position - half);
            Vector3 b = cam.WorldToScreenPoint(anchor.position + half);
            return Vector2.Distance(new Vector2(a.x, a.y), new Vector2(b.x, b.y));
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

        // defender-drop-dismount unit 2 — 하마 비행 종점 = 착지 후 정상 피드가 그릴 **뷰** 좌표와 정확히 동일
        // (팝 0 을 구조로 보장). 미러 대상은 피드의 입력이 아니라 **출력**이다: SyncMonoUnitViews 는
        // sim 기반 world 를 UpdatePosition 에 넘기고, SpineUnitView.ApplyRenderPosition 이 내부에서
        // BoardSpace.ToView + SpineVisualOffset(방어 유닛은 zero 계약)을 적용해 그린다. 입력(sim)을
        // 그대로 반환하면 비행이 sim 공간 점으로 날아가다 착지 해제 순간 뷰 좌표로 텔레포트한다
        // (2026-07-28 육안 재현: 화면 오른쪽 이탈 → 스냅). 공식이 바뀌면 여기도 같이 바꾼다.
        public bool TryGetDefenderRestViewPos(Vector2Int cell, out Vector3 world)
        {
            world = default;
            if (_em == null || !_defenderByTile.TryGetValue(cell, out var b)) return false;
            if (b.entity == Entity.Null || !_em.Exists(b.entity) || !_em.HasComponent<LocalTransform>(b.entity))
                return false;
            var p = _em.GetComponentData<LocalTransform>(b.entity).Position;
            world = (Vector3)Wassup.Core.BoardSpace.ToView(
                new Unity.Mathematics.float3(p.x, p.y + spineDefenderYOffset, p.z));
            return true;
        }

        // Enemy kills → live score HUD. One score bump per enemy killed by damage.
        private void DrainEnemyKilledEvents()
        {
            if (!_enemyKilledEventQueue.IsCreated) return;
            while (_enemyKilledEventQueue.TryDequeue(out var evt))
            {
                scoreHud?.OnEnemyKilled(evt.killScore);
                // battle-score-formula unit 2 — 최종 점수용 누적.
                // score-tally-sequence unit 0 이후 바로 윗줄의 HUD 도 **같은 값**을 받는다
                // (예전엔 처치당 고정 +10 이라 15배 어긋나 있었다). 두 경로가 같은
                // evt.killScore 를 쓰므로 전투 중 HUD 숫자 == _killScoreTotal 이다.
                _killScoreTotal += evt.killScore;
                // three-minute-survival unit 3 — 점수는 티어 가중이라 "처치 수" 와 다르다
                // (잡몹 10 + 보스 1 = 20점). 결과 화면이 `처치 N기` 를 따로 보여주므로
                // 마리 수를 별도로 센다.
                _killCount++;
                // dreamcatcher-awakening-hand unit 1 — awakening economy relay.
                // unit 3 — 흡수 비행 시작점으로 사망 view-space 위치 동봉(sim→view).
                // orb-dock unit 6 — 죽은 적 데이터 동봉(피규어 스킨 소스). 등록부 조회+제거.
                Wassup.Data.ISpineUnitVisualData killedVisual = null;
                if (_enemyTypeByEntity.TryGetValue(evt.entity, out var killedType))
                {
                    killedVisual = killedType;
                    _enemyTypeByEntity.Remove(evt.entity);
                }
                EnemyKilledAwakening?.Invoke(evt.awakeningReward,
                    Wassup.Core.BoardSpace.ToView((Vector3)evt.position), killedVisual);
                // 살찌운 제물 — 표식 악몽 처치: 카드 회수 알림(보상은 위 relay 가
                // 표식 시점에 배율된 baked 값으로 이미 지급).
                NotifyEnemyGoneIfMarked(evt.entity);
                int2 grid = _generatedMap.IsCreated ? _generatedMap.gridSize : FallbackGridSize;
                var cell = GridMath.WorldToCell(evt.position, tileSize, grid, origin: _boardOrigin);
                float time = LogElapsedTime;
                var logger = GameManager.Instance?.Logger;
                logger?.RecordKill(string.Empty, new Vector2Int(cell.x, cell.y), time);
                logger?.AddScoreEvent("enemy_killed", evt.killScore, time);

                // 시체폭발 (content-3 unit 3) — 킬 셀 중심 즉발 TileAoe. OnDeath 폭발
                // (DrainDefenderDeathEvents)과 동형. owner=killer 로 폭발발 킬의 OnKill
                // 연쇄 재발동이 사양.
                if (evt.hasKillBurst && evt.burstDataIndex >= 0)
                {
                    var impactWorld = GridToWorldCenter(new Vector2Int(cell.x, cell.y), spawnHeight);
                    SpawnProjectile(new ProjectileSpawnRequest
                    {
                        movement = MovementKind.SkyFall,
                        payload = PayloadKind.TileAoe,
                        impact = impactWorld,
                        damage = evt.burstDamage,
                        impactTileRange = evt.burstTileRange,
                        flightTime = 0f,
                        dataIndex = evt.burstDataIndex,
                        visualScale = 1f,
                        owner = evt.killer,
                    }, Entity.Null);
                }
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

        // season-gimmick-clockout unit 4 — 사직서 임계 barrage 요청을 drain 해 Walk 타일 임의
        // meteorCount 곳에 SkyFall×TileAoe 메테오를 순차 낙하(적 피해). content-1 OnDeath 폭발
        // (SpawnProjectile(...,Entity.Null))과 동형 cast — Combat 투사체 코드 불변, cast 프리미티브만 재사용.
        // season-gimmick-clockout unit 6 — 퇴근 코스트 환급 drain. 기존 지급 패스(CostRuntime.AddCost)로.
        private void DrainMeteorBarrageRequests()
        {
            if (!_meteorBarrageRequestQueue.IsCreated || _meteorBarrageRequestQueue.Count == 0) return;

            // 요청 존재하나 ClockOut 아님/맵 미빌드 → 비우고 드롭(비정상).
            if (!(_assignedGimmick is Wassup.Data.ClockOutGimmickData cd) || !_generatedMap.IsCreated)
            {
                while (_meteorBarrageRequestQueue.TryDequeue(out _)) { }
                return;
            }
            if (cd.meteorProjectile == null)
            {
                Debug.LogWarning("[BattleBridge] ClockOut meteorProjectile 미지정 — 메테오 barrage 드롭.");
                while (_meteorBarrageRequestQueue.TryDequeue(out _)) { }
                return;
            }

            // 이동(Walk) 타일 수집.
            int2 gridSize = _generatedMap.gridSize;
            int n = gridSize.x * gridSize.y;
            var walk = new System.Collections.Generic.List<int2>(n);
            for (int i = 0; i < n; i++)
                if (_generatedMap.tiles[i] == MapTileType.Walk)
                    walk.Add(new int2(i % gridSize.x, i / gridSize.x));

            int dataIndex = GetOrCreateProjectileDataIndex(cd.meteorProjectile);
            var chosen = new System.Collections.Generic.HashSet<int2>();

            while (_meteorBarrageRequestQueue.TryDequeue(out var req))
            {
                if (walk.Count == 0) continue;
                int shots = math.min(req.meteorCount, walk.Count);
                chosen.Clear();
                int landed = 0;
                for (int s = 0; s < shots; s++)
                {
                    // rng 로 미중복 Walk 셀 선택(재시도 상한).
                    int2 cell = default; bool found = false;
                    for (int attempt = 0; attempt < 8; attempt++)
                    {
                        var c = walk[_meteorRng.NextInt(0, walk.Count)];
                        if (chosen.Contains(c)) continue;
                        cell = c; found = true; break;
                    }
                    if (!found) continue;
                    chosen.Add(cell);

                    float3 impactWorld = GridToWorldCenter(new Vector2Int(cell.x, cell.y));
                    SpawnProjectile(new ProjectileSpawnRequest
                    {
                        movement        = MovementKind.SkyFall,
                        payload         = PayloadKind.TileAoe,
                        origin          = impactWorld,
                        impact          = impactWorld,
                        damage          = cd.meteorDamage,
                        visualScale     = cd.meteorProjectile.visualScale,
                        dataIndex       = dataIndex,
                        impactTileRange = cd.meteorTileRange,
                        flightTime      = cd.meteorWarningSec + landed * cd.meteorStaggerSec, // 순차 착탄
                        arcHeight       = cd.meteorProjectile.dropHeight,                     // SkyFall 낙하 시작 높이
                        targetFaction   = ProjectileTargetFaction.Enemy,                     // clockout=적(보스만 Defender)
                    }, Entity.Null);
                    landed++;
                }
            }
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
            // SkyFall 은 sim 이동이 0 이다(Move arm 이 elapsed 만 진행) — 따라서 스폰
            // 위치가 그대로 최종 화면 위치가 된다. 발사 주체가 origin 에 무엇을 넣든
            // 착탄 셀에서 떨어져야 하므로, 궤적의 불변식을 궤적 소유 지점인 여기서
            // 강제한다. (기존 Meteor/구 barrage arm 은 origin==impact 로 보내와서
            // 바이트 동일하고, emitter 처럼 origin=시전자 인 주체만 정정된다.)
            var spawnPos = req.movement == MovementKind.SkyFall
                ? new float3(req.impact.x, spawnHeight, req.impact.z)
                : new float3(req.origin.x, spawnHeight, req.origin.z);
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
                retargetTileRange = req.retargetTileRange,
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
                // bomb-thrower-defender unit 2 — TileAoe cap/CC + 뷰 변종, verbatim(기본 0=레거시).
                aoeTargetCap = req.aoeTargetCap,
                ccKind = req.ccKind,
                ccDuration = req.ccDuration,
                bombType = req.bombType,
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
            else if (req.movement == MovementKind.BezierHomingToEntity)
            {
                // projectile-emission-pattern unit 1 — 제어점은 SO 파라미터
                // (bezierLateral/ForwardBias)가 필요해 **여기서** 산출한다. ISystem 은
                // SO 를 못 읽으므로 번역자인 drain 이 채우는 것이 이 파이프라인의 관례다
                // (SkyFall 의 dropHeight 보충 선례). 덕분에 발사 주체(AttackSystem·
                // emitter·캐스트) 어느 쪽도 SO 를 알 필요가 없다.
                float3 destPos = spawnPos;
                if (req.target != Entity.Null && _em.HasComponent<LocalTransform>(req.target))
                    destPos = _em.GetComponentData<LocalTransform>(req.target).Position;
                // 제어점은 발사 시점 목표 위치 기준으로 한 번만 잡는다 — 이후 종점만
                // 타겟을 따라가므로(Move arm) 곡선이 실시간으로 재조정된다.
                var bezierDest = new float3(destPos.x, spawnHeight, destPos.z);
                state.origin = spawnPos;
                float lateral = projData != null ? projData.bezierLateral : 0f;
                float forwardBias = projData != null ? projData.bezierForwardBias : 0.35f;
                Bezier3.ControlPoints(spawnPos, bezierDest, req.swingIndex, lateral, forwardBias,
                                      out var bezierC1, out var bezierC2);
                state.control1 = bezierC1;
                state.control2 = bezierC2;
                // arcHeight 슬롯 = view 공간 Y 아치 높이(sim 은 XZ 곡선만).
                state.arcHeight = req.arcHeight > 0f ? req.arcHeight
                    : (projData != null ? projData.arcHeight : 0f);
                // 비행 시간은 발사 시 거리/속도로 고정 — 타겟이 움직여도 곡선이
                // 압축되며 파고든다(BallisticArc 와 같은 산출식·같은 하한).
                state.flightTime = BallisticArc.FlightTime(
                    spawnPos, bezierDest, req.speed, projData != null ? projData.minFlightTime : 0.3f);
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
            else if (req.movement == MovementKind.GrenadeToCell)
            {
                // bomb-thrower-defender unit 1 — roll to a fixed cell then fuse.
                // flightTime (travelSec) is request-carried & fixed (SkyFall 관례,
                // 거리 무관) — NOT BallisticArc.FlightTime(속도 유도)이다(계약 2).
                // arcHeight≈0 keeps it on the ground (rolling look). Same spawn-
                // height plane as ballistic so the arc's linear Y stays flat.
                state.origin = spawnPos;
                state.impact = new float3(req.impact.x, spawnHeight, req.impact.z);
                state.impactTileRange = req.impactTileRange;
                state.flightTime = math.max(req.flightTime, 0f);
                state.fuseSec = math.max(req.fuseSec, 0f);
                state.arcHeight = req.arcHeight;
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
                // projectile-shot-sequence unit 5 — emitter carrier는 일회성 request
                // entity라 view가 없다. 실제 공격자(req.owner)를 우선하고 owner 없는
                // legacy 요청만 drain의 shooter를 fallback으로 쓴다. SkyFall은 유닛
                // 발사가 아니라 impact cell에서 내려오므로 anchor를 적용하지 않는다.
                bool hasLaunchAnchor = false;
                Vector3 launchAnchor = default;
                if (req.movement != MovementKind.SkyFall && spineUnitPool != null)
                {
                    Entity visualOwner = req.owner != Entity.Null ? req.owner : shooter;
                    if (visualOwner != Entity.Null)
                        hasLaunchAnchor = spineUnitPool.TryResolveProjectileLaunchAnchor(
                            visualOwner, out launchAnchor);
                }
                _projectileViewPool?.Spawn(
                    entity, projData, spawnPos, initialDrop, hasLaunchAnchor, launchAnchor);
            }
            return entity;
        }

        // 배치 셀 반경 내 살아있는 적을 스크래치에 모은다. on-place 분기 다섯이 같은
        // "쿼리 → 순회 → LocalTransform 확인 → InTileRange" 를 복제하고 있었다(bleed/knockup
        // unit 1 이 두 개를 더하면서 다섯이 됨) — 호출처가 다섯이라 추출이 맞다.
        // 재사용 스크래치라 배치마다 할당이 없다(`_dcFiredScratch` 선례).
        private readonly System.Collections.Generic.List<Entity> _onPlaceInRangeScratch = new();

        private System.Collections.Generic.List<Entity> CollectEnemiesInTileRange(Vector2Int cell, float range)
        {
            _onPlaceInRangeScratch.Clear();
            if (range <= 0f || !_aliveAttackersQueryCreated) return _onPlaceInRangeScratch;
            int tileRange = GridMath.RangeToTiles(range);
            var entities = _aliveAttackersQuery.ToEntityArray(Allocator.Temp);
            for (int i = 0; i < entities.Length; i++)
            {
                var e = entities[i];
                if (!_em.HasComponent<LocalTransform>(e)) continue;
                var pos = _em.GetComponentData<LocalTransform>(e).Position;
                if (!InTileRange(pos, cell, tileRange)) continue;
                _onPlaceInRangeScratch.Add(e);
            }
            entities.Dispose();
            return _onPlaceInRangeScratch;
        }

        // Fires the defender's on-place effect on surrounding entities. Returns
        // the count of entities affected so the logger can record magnitude.
        // Writes to Effects components go through EffectSpawner so the Effects-
        // context write gateway (Phase 2 decision) stays the sole path.
        private int ApplyOnPlaceEffect(DefenderUnitData unitData, Vector2Int placedCell, Entity placedEntity)
        {
            if (unitData.onPlaceEffect == OnPlaceEffectType.None) return 0;

            int affected = 0;

            // SlowPulse 와 BindNearby 는 예전부터 **같은 효과**다(둘 다 이동속도 배율 감쇠).
            // 문구만 다르고 동작이 같으므로 한 분기로 합쳐 둔다 — 갈라 두면 한쪽만 고쳐진다.
            if (unitData.onPlaceEffect == OnPlaceEffectType.SlowPulse
                || unitData.onPlaceEffect == OnPlaceEffectType.BindNearby)
            {
                foreach (var e in CollectEnemiesInTileRange(placedCell, unitData.onPlaceRange))
                {
                    EnqueueMoveSpeedMul(e, unitData.onPlaceMagnitude, unitData.onPlaceDuration, Wassup.Battle.Effects.ModifierOrigin.OnPlace);
                    affected++;
                }
            }
            else if (unitData.onPlaceEffect == OnPlaceEffectType.ApplyStackNearby)
            {
                // bleed-fighter-defender unit 1 — 반경 내 적 전원에 스택 도포(등장 난도질).
                // 스택 종류/수/지속은 SO, 상한은 그 StackKind 를 소유한 StackModifierSO 가
                // 권위다(유닛마다 다른 상한을 적는 것이 아니라 스택의 성질) — 미등록이면
                // AttackSystem outputs 경로와 같은 기본값 5.
                if (unitData.onPlaceMagnitude <= 0f || !_stackModifierQueue.IsCreated) return 0;

                byte maxStack = Wassup.Data.StackModifierSO.DefaultMaxStack;
                if (stackModifierAuthoring != null)
                {
                    foreach (var so in stackModifierAuthoring)
                        if (so != null && so.kind == unitData.onPlaceStackKind) { maxStack = so.maxStack; break; }
                }

                foreach (var e in CollectEnemiesInTileRange(placedCell, unitData.onPlaceRange))
                {
                    _stackModifierQueue.Enqueue(new Wassup.Battle.Effects.StackModifierApplyEvent
                    {
                        target         = e,
                        kind           = unitData.onPlaceStackKind,
                        countDelta     = (byte)math.max(1f, unitData.onPlaceMagnitude),
                        maxStack       = maxStack,
                        perAppDuration = unitData.onPlaceDuration,
                        source         = placedEntity,
                    });
                    affected++;
                }
            }
            else if (unitData.onPlaceEffect == OnPlaceEffectType.StunNearby)
            {
                // knockup-fighter-defender unit 1 — 착지 충격(반경 내 적 전원 넉업).
                // 심은 Stun 그대로 — "공중" 은 뷰가 붙이는 해석이다(unit 3).
                if (unitData.onPlaceDuration <= 0f || !_enemyCcQueue.IsCreated) return 0;

                foreach (var e in CollectEnemiesInTileRange(placedCell, unitData.onPlaceRange))
                {
                    _enemyCcQueue.Enqueue(new Wassup.Battle.Effects.EnemyCcEvent
                    {
                        target = e,
                        effect = new Wassup.Battle.Effects.CcEffect
                        {
                            kind          = Wassup.Battle.Effects.CcKind.Stun,
                            remainingTime = unitData.onPlaceDuration,
                        },
                    });
                    // unit 3 — 공격 넉업과 같은 연출 경로. 여기는 이미 브리지(뷰 접근 가능)라
                    // 큐를 거치지 않고 직접 재생한다.
                    if (unitData.knockupVisualHeight > 0f && spineUnitPool != null
                        && spineUnitPool.TryGet(e, out var hopView) && hopView != null)
                        hopView.PlayKnockupHop(unitData.onPlaceDuration, unitData.knockupVisualHeight);
                    affected++;
                }
            }
            else if (unitData.onPlaceEffect == OnPlaceEffectType.DotNearby)
            {
                // beam-ranger-defender unit 2 — 개점 일제 조사. 심은 기존 이산 tick DoT 그대로
                // (dot-tick-cadence 계약) — 신규 시스템 0. 연출은 대상마다 빔 세션 1개.
                // ⚠ tickInterval>0 일 때 scalar 는 **틱당 피해**다(DPS 아님).
                if (unitData.onPlaceMagnitude <= 0f || unitData.onPlaceDuration <= 0f
                    || !_dotApplyQueue.IsCreated) return 0;

                foreach (var e in CollectEnemiesInTileRange(placedCell, unitData.onPlaceRange))
                {
                    _dotApplyQueue.Enqueue(new Wassup.Battle.Effects.DotApplyEvent
                    {
                        target = e,
                        effect = new Wassup.Battle.Effects.DotEffect
                        {
                            // element 는 None 유지(원소 없음 = 오라 없음). 배치 도트에 원소를
                            // 주고 싶어지면 그때 저작 필드를 신설한다(제약 8).
                            origin        = Wassup.Battle.Effects.DotOrigin.OnPlace,
                            scalar        = unitData.onPlaceMagnitude,
                            tickInterval  = unitData.onPlaceTickInterval,
                            tickTimer     = unitData.onPlaceTickInterval, // 첫 틱 즉발(add-path 규약)
                            remainingTime = unitData.onPlaceDuration,
                        },
                    });
                    // 대상별 빔 — 키가 적 엔티티라 공격 세션(키 = 공격자)과 충돌하지 않는다.
                    // 대상을 엔티티로 넘기므로 2초 동안 적이 걸어가도 빔이 따라간다.
                    if (unitData.beamVfxPrefab != null)
                    {
                        EnsureBeamPresenter().Open(
                            e, unitData.beamVfxPrefab,
                            source: placedEntity, target: e, ttlSec: unitData.onPlaceDuration);
                    }
                    affected++;
                }

                // 조사(照射) 중에는 기본 공격을 하지 않는다. DotNearby 는 다른 on-place 효과와
                // 달리 **지속을 갖는 채널**이라 그동안 유닛이 이 스킬에 묶여 있는 것이 사양이다
                // (순간 효과인 MeleeBurst/StunNearby 등은 해당 없음 — 그래서 이 분기 안에 둔다).
                // 첫 공격 쿨다운을 지속만큼 밀어 둔다. max 를 쓰는 이유는 이미 걸린 쿨다운을
                // 줄이지 않기 위함.
                if (_em.HasComponent<Wassup.Battle.Combat.AttackState>(placedEntity))
                {
                    var atk = _em.GetComponentData<Wassup.Battle.Combat.AttackState>(placedEntity);
                    atk.cooldownRemaining = math.max(atk.cooldownRemaining, unitData.onPlaceDuration);
                    _em.SetComponentData(placedEntity, atk);
                }
            }
            else if (unitData.onPlaceEffect == OnPlaceEffectType.MeleeBurst)
            {
                if (unitData.onPlaceMagnitude <= 0f) return 0;
                foreach (var e in CollectEnemiesInTileRange(placedCell, unitData.onPlaceRange))
                {
                    if (!_em.HasBuffer<IncomingDamage>(e)) continue;
                    _em.GetBuffer<IncomingDamage>(e).Add(new IncomingDamage { amount = unitData.onPlaceMagnitude });
                    affected++;
                }
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
                // placement-mask unit 3 — 배치 셀 자신은 제외. B-1 로 Walk 셀 위 배치가 가능해져
                // 자기 셀이 d2=0 최근접이 되면 zero-길이 가드가 고정 +x 를 쏘던 결함 — 방향의 의미는
                // "가장 가까운 '경로'를 향해" 이므로 최근접 '타' Walk 셀이 맞다.
                if (dx == 0 && dy == 0) continue;
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

        // active-dreamcatcher-tile-aim rev — 보드 **안**만 셀로 인정하는 엄격 변형.
        // `TryScreenToCell` 은 `GridMath.WorldToCell` 의 clamp 때문에 맵 밖(빈 배경)을 찍어도
        // 가장자리 셀에 true 를 준다 — "보드 밖 = 취소(무차감)" 를 계약으로 갖는 조준 경로는
        // 반드시 이걸 써야 한다. 부착/적 표식처럼 관대한 판정이 맞는 곳은 기존 함수를 유지한다.
        public bool TryScreenToCellStrict(Camera cam, Vector2 screenPos, out Vector2Int cell)
        {
            cell = default;
            if (cam == null) return false;
            var ray = cam.ScreenPointToRay(screenPos);
            var plane = Wassup.Core.BoardSpace.RaycastPlane();
            if (!plane.Raycast(ray, out float enter)) return false;
            var world = (float3)(Vector3)Wassup.Core.BoardSpace.ToSim(ray.GetPoint(enter));
            int2 raw = GridMath.WorldToCellUnclamped(world, tileSize, origin: _boardOrigin);
            int2 size = _generatedMap.IsCreated ? _generatedMap.gridSize : FallbackGridSize;
            if (raw.x < 0 || raw.x >= size.x || raw.y < 0 || raw.y >= size.y) return false;
            cell = new Vector2Int(raw.x, raw.y);
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

        // dreamcatcher-attach-lockon unit 5 — base-ring 열거(순수 공간 read). 배치
        // defender 각각의 화면 스프라이트 렉트를 outBuf 에 채운다. component write 0,
        // 신규 EntityQuery/Temp 할당 0. outBuf 는 호출부가 재사용(매프레임 new 금지).
        public void EnumerateDefenderScreenRects(Camera cam,
            System.Collections.Generic.List<(Entity entity, Rect rect)> outBuf)
        {
            outBuf.Clear();
            if (cam == null || spineUnitPool == null) return;
            foreach (var kv in _defenderByTile)
            {
                if (!spineUnitPool.TryGet(kv.Value.entity, out var view) || view == null) continue;
                if (!view.TryGetScreenRect(cam, out var rect)) continue;
                outBuf.Add((kv.Value.entity, rect));
            }
        }

        // dreamcatcher-attach-lockon unit 2/3/4 — 락온 유닛(방어수/적 공용)의 화면 렉트
        // (리티클·콜아웃·화살표 끝점). spineUnitPool 기반이라 적 entity 에도 동작.
        // 스프라이트 렉트 없으면 false(폴백 quad·화면 밖).
        public bool TryGetUnitScreenRect(Entity entity, Camera cam, out Rect rect)
        {
            rect = default;
            if (cam == null || spineUnitPool == null) return false;
            return spineUnitPool.TryGet(entity, out var view) && view != null
                && view.TryGetScreenRect(cam, out rect);
        }

        // dreamcatcher-attach-lockon unit 3 — 콜아웃 정체(아이콘/이름) 소스. 셀
        // 바인딩의 DefenderUnitData 직독(읽기 전용).
        public bool TryGetDefenderData(Vector2Int cell, out DefenderUnitData data)
        {
            if (_defenderByTile.TryGetValue(cell, out var binding))
            {
                data = binding.data;
                return true;
            }
            data = null;
            return false;
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
                // projectile-emission-pattern unit 1 — 곡선 추적 × 단일 착탄.
                ProjectileFlightMode.BezierHoming => (MovementKind.BezierHomingToEntity, PayloadKind.SingleSplash),
                // projectile-emission-pattern unit 4 — 낙하 텔레그래프 × 셀 AoE.
                // 이 축은 여태 ApplyMeteor 하드코딩으로만 존재했다 — 패턴이 데이터로
                // SkyFall 탄을 지정할 수 있어야 하므로 flightMode 어휘에 편입한다.
                ProjectileFlightMode.SkyFall => (MovementKind.SkyFall, PayloadKind.TileAoe),
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

        // battle-leak-limit-hud unit 0 — 패배 비교/HUD/저주 지불이 공유하는 유효 한계.
        private int EffectiveLeakLimit()
            => ActiveDeck != null ? ActiveDeck.defeatGoalReachedCount - _leakAllowancePenalty : 0;

        // battle-score-formula unit 7 — 스트레스점수의 입력. 점수 계산(CalculateBattleScore)과
        // 결과 화면 표기(FinishTally)가 **같은 값**을 써야 화면에서 검산된다. 한계는 덱 원본값
        // 이고 EffectiveLeakLimit()(계약 차감 후)이 아니다 — 차감분은 누적 쪽에 있다(계약 8).
        private int StressAccrued => _goalReachedCount + _leakAllowancePenalty;
        private int StressLimit => ActiveDeck != null ? ActiveDeck.defeatGoalReachedCount : 0;

        // three-minute-survival unit 0 — 분모와 위기색은 한계가 패배를 만들 때만 참이었다.
        // 이제 패배는 안정도가 소유하므로 스트레스는 **개수만** 표시한다(엔드리스가 쓰던
        // 표시 모드를 전 모드로 승격). 안정도 게이지는 unit 1 이 별도로 그린다.
        private void RefreshLeakHud()
            => scoreHud?.SetLeakStatus(_goalReachedCount, EffectiveLeakLimit(), showLimit: StressLimit > 0);

        // three-minute-survival unit 0 — 안정도 만피 복귀. _battleClock 리셋과 짝이다.
        private void ResetGoalStability()
        {
            _goalStabilityMax = ActiveDeck != null ? Mathf.Max(1, ActiveDeck.goalStabilityMax) : 0;
            _goalStability = _goalStabilityMax;
            _breachedCells.Clear();   // stress-after-breach — 붕괴 상태는 매치 경계에서 소멸(이월 금지)
            _leakTypeMissLogged = false;
            _towerMissLogged = false;
        }

        // goal-tower-siege(rev 2) — 골 셀마다 **건물형 유닛**을 세운다.
        //
        // 진영은 `Faction.DefenderCore`(방어 마음) 다. 적의 base targetMask 가 그 비트를 포함
        // 라서 **타겟팅 코드가 한 줄도 필요 없다** — 전용 Faction 비트도, 골 도달 시 마스크를
        // 열어주는 브리지 훅도, 도발 시스템 패치도 전부 사라졌다(rev 1 의 과설계).
        //
        // 단 `DefenderUnitTag` 는 **붙이지 않는다.** 그건 "플레이어가 놓은 유닛" 축이라,
        // 붙이는 순간 배치/코스트/카드 부착/시너지/피로도·열기/픽업/실드가 전부 딸려온다.
        // 진영(Faction)과 유닛 태그를 분리해 쓰는 것은 Blocking 해저드의 선례와 같다.
        //
        // 피해는 표준 경로다: 공격자가 IncomingDamage 에 append → DamageApplicationSystem 이
        // Health 를 깎고 0 이면 DeadTag → UnitLifecycleSystem 이 파괴. 전용 피해 시스템도,
        // 공유 풀 싱글턴도, 미러도 없다. **타워가 사라진 것이 곧 패배 신호**다.
        // 리뷰 A-M3 — ProjectileRef 베이크의 단일 지점(제약 10-b: 호출처 3 — 방어·적·본능).
        // 이전엔 11필드 초기화가 세 곳에 글자 단위로 복제돼, ProjectileData 에 필드가 붙으면
        // 셋 중 둘만 갱신되는 drift 가 가능했다. 반환 = dataIndex(방어 경로가 방향 패턴
        // 베이크에 이어 쓴다).
        private int BakeProjectileRef(Entity entity, Wassup.Data.ProjectileData projectile)
        {
            int dataIndex = GetOrCreateProjectileDataIndex(projectile);
            var axes = ResolveProjectileAxes(projectile.flightMode);
            _em.AddComponentData(entity, new ProjectileRef
            {
                dataIndex = dataIndex,
                speed = projectile.speed,
                hitThreshold = projectile.hitThreshold,
                visualScale = projectile.visualScale,
                onHitEffect = projectile.onHitEffect,
                splashRadius = projectile.splashRadius,
                splashDamageMul = projectile.splashDamageMul,
                movement = axes.movement,
                payload = axes.payload,
                arcHeight = projectile.arcHeight,
                impactTileRange = projectile.impactTileRange,
            });
            return dataIndex;
        }

        // battle-structures unit 4 — EnsureGoalTowers 의 일반화. 두 소스, 한 아키타입:
        //   goals[]                     → 방어 마음(= 현행 골 타워). HP = 덱(현행 유지 —
        //                                 SO 이관은 «HP 소스가 맵마다 갈리는» 상태를 만든다).
        //                                 GoalTowerTag + StructureTag.
        //   _resolvedMapDoc.Structures  → 본능 + 적 마음. HP = SO(StructureData.health).
        //                                 StructureTag 만. 본능은 3×3 통행 차단 버퍼
        //                                 (BlockingHazardCellsBuffer — 기존 소비자가 그대로 처리).
        private void SpawnStructureEntities()
        {
            DestroyStructureEntities();
            if (!HasLiveEntityManager() || !_generatedMap.IsCreated) return;

            // ── 방어 마음(골 타워) — 현행 경로 그대로 ──
            if (_goalStabilityMax > 0)
            {
                bool hasList = _generatedMap.goals.IsCreated && _generatedMap.goals.Length > 0;
                int count = hasList ? _generatedMap.goals.Length : 1;
                for (int i = 0; i < count; i++)
                {
                    int2 cell = hasList ? _generatedMap.goals[i] : _generatedMap.goal;
                    var tower = _em.CreateEntity();
                    _em.AddComponent<Wassup.Battle.Units.GoalTowerTag>(tower);
                    _em.AddComponentData(tower, new Wassup.Battle.Units.StructureTag
                    {
                        cell = cell,
                        faction = Faction.DefenderCore,
                    });
                    _em.AddComponentData(tower, new Health { value = _goalStabilityMax, max = _goalStabilityMax });
                    _em.AddBuffer<IncomingDamage>(tower);
                    _em.AddComponentData(tower, new FactionTag { value = Faction.DefenderCore });
                    _em.AddComponentData(tower, LocalTransform.FromPosition(
                        GridToWorldCenter(new Vector2Int(cell.x, cell.y))));
                    _structureRegistry.Add((tower, new Vector2Int(cell.x, cell.y), Faction.DefenderCore));
                }
                _goalTowerCount = count;
                Debug.Log($"[BattleBridge] Goal towers spawned: {count} @ stability {_goalStabilityMax}");
            }

            // ── 저작 거점(본능 + 적 마음) — unit 3 의 _resolvedMapDoc 에서 SO 스탯을 읽는다 ──
            var docStructures = _resolvedMapDoc != null ? _resolvedMapDoc.Structures : null;
            if (docStructures == null) return;
            int spawned = 0;
            for (int i = 0; i < docStructures.Count; i++)
            {
                var s = docStructures[i];
                if (s.data == null) continue;   // OnValidate 가 이미 에러로 알림 — 방어적 스킵
                var faction = Wassup.Data.StructurePlacements.DeriveFaction(s.side, s.data.kind);
                // (Defender, Core) 는 저작 금지(정본 = goals[]) — 검증을 뚫고 왔어도 안 세운다.
                if (faction == Faction.DefenderCore) continue;

                var cell = new int2(s.cell.x, s.cell.y);
                var entity = _em.CreateEntity();
                _em.AddComponentData(entity, new Wassup.Battle.Units.StructureTag
                {
                    cell = cell,
                    faction = faction,
                });
                _em.AddComponentData(entity, new Health { value = s.data.health, max = s.data.health });
                _em.AddBuffer<IncomingDamage>(entity);
                _em.AddComponentData(entity, new FactionTag { value = faction });
                _em.AddComponentData(entity, LocalTransform.FromPosition(GridToWorldCenter(s.cell)));

                // 본능 3×3 — 통행 차단은 본체만(계약 12: 마음은 비차단). BlockingHazard
                // 다중셀 선례(EffectSpawner)와 같은 버퍼라 통행 코드 신설 0.
                if (Wassup.Data.StructurePlacements.IsInstinct(faction))
                {
                    int half = Wassup.Data.StructurePlacements.InstinctFootprint / 2;
                    var cells = _em.AddBuffer<Wassup.Battle.Effects.BlockingHazardCellsBuffer>(entity);
                    for (int dy = -half; dy <= half; dy++)
                        for (int dx = -half; dx <= half; dx++)
                            cells.Add(new Wassup.Battle.Effects.BlockingHazardCellsBuffer
                            {
                                cell = new int2(cell.x + dx, cell.y + dy),
                            });

                    // battle-structures unit 5 — 본능 공격. 전용 시스템 없음(계약 10):
                    // AttackState + 출력 + ProjectileRef 를 베이크하면 통합 공격자 루프가
                    // 유닛과 똑같이 처리한다(적 원거리의 «호밍 → 방어유닛 직격» 경로).
                    // 마음(Core)은 이 분기 밖 — 공격하지 않는다.
                    if (s.data.attackDamage > 0f)
                    {
                        if (s.data.projectile == null)
                        {
                            // 조용한 미발사 방지 — 적 베이크의 «outputs empty → walk-only» 선례.
                            Debug.LogWarning($"[BattleBridge] {s.data.displayName}: attackDamage={s.data.attackDamage} 인데 projectile 미지정 — 무공격으로 베이크.", s.data);
                        }
                        else if (ResolveProjectileAxes(s.data.projectile.flightMode).payload
                                 == Wassup.Battle.Combat.Projectile.PayloadKind.TileAoe)
                        {
                            // 리뷰 M-10 — 광역 투사체는 본능에 못 물린다. 통합 루프의 ballistic
                            // 요청이 targetFaction 을 싣지 않아 기본 Enemy 풀로 떨어지므로,
                            // 적 본능의 광역이 **적을** 때리는 오귀속이 된다(unit 5 문서 §계약 11).
                            // projectile 미지정과 대칭으로 loud warn + 무공격.
                            Debug.LogWarning($"[BattleBridge] {s.data.displayName}: TileAoe 계열 투사체({s.data.projectile.name})는 본능에 지원되지 않는다(피해풀 오귀속) — 무공격으로 베이크.", s.data);
                        }
                        else
                        {
                            _em.AddComponentData(entity, new AttackState
                            {
                                range = s.data.attackRange,
                                cooldownDuration = s.data.attackCooldown,
                                cooldownRemaining = 0f,
                                attackTargetCount = 1,   // v1 = 투사체 1발 고정
                                // 저작 타겟 마스크 재사용(unit 1 과 같은 축·같은 폴백).
                                targetMask = Wassup.Battle.Combat.EnemyTargetDefaults.Resolve(
                                    (int)s.data.targetFactions),
                                hitDelaySec = 0f,
                            });
                            var outputs = _em.AddBuffer<Wassup.Battle.Combat.AttackOutputElement>(entity);
                            outputs.Add(new Wassup.Battle.Combat.AttackOutputElement
                            {
                                value = new Wassup.Data.AttackOutput
                                {
                                    kind = Wassup.Data.AttackOutputKind.Damage,
                                    magnitude = s.data.attackDamage,
                                },
                            });
                            BakeProjectileRef(entity, s.data.projectile);   // 리뷰 A-M3 — 단일 베이크
                        }
                    }
                }

                // 뷰 — SO 의 viewPrefab 을 셀 중심에 직배치(sim→view 는 BoardSpace.ToView 경유,
                // Pickup 선례와 동일). 프리팹 미지정은 무해(게이지만 뜬다).
                if (s.data.viewPrefab != null)
                {
                    float3 simCenter = GridToWorldCenter(s.cell);
                    var view = Instantiate(s.data.viewPrefab,
                        (Vector3)Wassup.Core.BoardSpace.ToView(simCenter), Quaternion.identity, transform);
                    view.name = $"Structure_{s.data.displayName}_{s.cell.x}_{s.cell.y}";
                    _structureViews.Add(view);
                }

                _structureRegistry.Add((entity, s.cell, faction));
                spawned++;
            }
            if (spawned > 0)
                Debug.Log($"[BattleBridge] Structures spawned: {spawned} (본능/적 마음, SO HP)");
        }

        // 리뷰 H-4 — 뷰 정리는 2곳(여기 + TeardownCurrentBattle의 restart 경로)이 공유한다.
        private void ClearStructureViews()
        {
            for (int i = 0; i < _structureViews.Count; i++)
                if (_structureViews[i] != null) Destroy(_structureViews[i]);
            _structureViews.Clear();
        }

        private void DestroyStructureEntities()
        {
            _goalTowerCount = 0;
            _structureRegistry.Clear();
            ClearStructureViews();
            if (!HasLiveEntityManager()) return;
            using var towerQuery = _em.CreateEntityQuery(
                ComponentType.ReadOnly<Wassup.Battle.Units.GoalTowerTag>());
            _em.DestroyEntity(towerQuery);
            using var structureQuery = _em.CreateEntityQuery(
                ComponentType.ReadOnly<Wassup.Battle.Units.StructureTag>());
            _em.DestroyEntity(structureQuery);
        }

        // 안정도 읽기 창구. unit 1(게이지)·unit 3(동점 판정)이 이것만 쓴다.
        public int GoalStabilityCurrent => _goalStability;
        public int GoalStabilityMax => _goalStabilityMax;

        // subconscious-curse-expansion unit 1 (몽마의 계약) — 잔여 유출 허용치.
        // = SO 기준치 − 선불 차감 − 이미 유출된 수. 컨트롤러 게이트/HUD 조회용.
        public int RemainingLeakAllowance()
            => EffectiveLeakLimit() - _goalReachedCount;

        // 몽마의 계약 선불 지불. 지불 후 잔여가 1 미만이면 거절 — "지불로 즉시 패배"
        // 상태를 구조적으로 금지(spec 게이트 조건: 잔여 − cost ≥ 1). 성공 시 비가역:
        // host 사망 revoke 는 hosted 버프만 회수하고 이 오프셋은 되돌리지 않는다.
        public bool TryPayLeakAllowance(int cost)
        {
            if (cost <= 0) return false;
            if (RemainingLeakAllowance() - cost < 1) return false;
            _leakAllowancePenalty += cost;
            RefreshLeakHud();
            return true;
        }

        private void DrainGoalEvents()
        {
            if (!_goalEventQueue.IsCreated) return;
            while (_goalEventQueue.TryDequeue(out var evt))
            {
                // battle-structures unit 4(ⓐ) — 판정은 셀 단위: **이 적이 도달한 골**(최근접
                // 골 셀)이 부서졌는가. 골 2개 맵에서 한쪽만 부서지면 그쪽 도달만 유출이고
                // 다른 쪽 도달은 여전히 공성이다.
                bool breached = _breachedCells.Contains(NearestGoalCell(evt.position));

                // stress-after-breach(2026-08-08) — 스트레스는 **부서진 골로의 유출만** 센다.
                // 붕괴 전 도달은 공성(안정도 피해)이거나 자폭(안정도 피해)이라 안정도 축이
                // 이미 그것을 세고 있다. 여기서도 세면 한 사건이 두 축을 깎아, 안정도가
                // 멀쩡한데 스트레스 상한으로 먼저 죽는다(1000 남았는데 패배가 실측됐다).
                if (breached)
                {
                    _goalReachedCount++;
                    RefreshLeakHud();
                    CheckStressDefeat();
                }

                // goal-tower-siege(rev 2) — 공성 전환. 적은 **살아 있다**: 뷰·현상금 표식·
                // 데이터 등록부를 건드리지 않는다(지우면 안 보이는 적이 타워를 때리고
                // 데미지 폰트만 허공에 뜬다). 타워가 Faction.DefenderCore 라 적의 base targetMask
                // 가 이미 그것을 포함하므로 **여기서 열어줄 것이 없다.**
                // stress-after-breach — 그 셀의 골이 부서졌으면 때릴 타워가 없다. 공성으로 두면
                // 적이 눌러앉아 웨이브 전멸 판정을 막으므로 유출(뷰 회수 + 파괴)로 내린다.
                if (evt.canSiege && !breached) continue;
                if (evt.canSiege && breached)
                {
                    enemyViewPool?.Despawn(evt.entity);
                    spineUnitPool?.Despawn(evt.entity);
                    NotifyEnemyGoneIfMarked(evt.entity);
                    _enemyTypeByEntity.Remove(evt.entity);
                    if (HasLiveEntityManager() && _em.Exists(evt.entity)) _em.DestroyEntity(evt.entity);
                    continue;
                }

                // 돌격형 자폭(AttackState 없는 Runner·Swift 계열) — 기존 유출 경로 그대로.
                // 골에 붙어도 아무것도 못 하면서 웨이브 전멸 판정만 막으므로 남기지 않는다.
                enemyViewPool?.Despawn(evt.entity);
                spineUnitPool?.Despawn(evt.entity);
                // 살찌운 제물 — 표식 악몽 유출: 무보상 회수.
                NotifyEnemyGoneIfMarked(evt.entity);
                int stabilityDamage = 1;
                if (_enemyTypeByEntity.TryGetValue(evt.entity, out var leakedType) && leakedType != null)
                {
                    stabilityDamage = Mathf.Max(0, leakedType.stabilityDamage);
                    // 킬 경로(DrainEnemyKilledEvents)와 대칭 — 등록부에서 빼야 누적되지 않는다.
                    _enemyTypeByEntity.Remove(evt.entity);
                }
                else if (!_leakTypeMissLogged)
                {
                    // 조용히 0 으로 넘기면 유출이 무해해진다 — 폴백 1 + 경고 1회.
                    _leakTypeMissLogged = true;
                    Debug.LogWarning("[BattleBridge] 유출한 적의 데이터가 등록부에 없다 — 안정도 피해 1 로 폴백.", this);
                }
                // 안정도를 직접 깎지 않는다 — 타워 버퍼로 넣어 공성 피해와 **같은 통로**를
                // 지나게 한다(풀의 writer 는 GoalTowerDamageSystem 하나다).
                // unit 4(ⓐ) — 부서진 셀 도달이면 넣지 않는다(스트레스가 이미 셌다). 전역 bool
                // 시절엔 붕괴 후 타워가 없어 자연 no-op 였지만, per-cell 에선 **다른 살아있는
                // 골**이 존재하므로 가드가 없으면 최근접 검색이 남의 골을 깎는다.
                if (!breached) EnqueueGoalTowerDamage(stabilityDamage, evt.position);
            }
        }

        // unit 4(ⓐ) — 적 위치 → 귀속 골 셀. EnqueueGoalTowerDamage 가 최근접 타워를 고르는
        // 것과 같은 기준을 셀에 적용한다(골 2개 맵에서 이벤트가 어느 골 사건인지 가른다).
        private Vector2Int NearestGoalCell(float3 position)
        {
            // 리뷰 L-13 — 맵 부재 시 (0,0) 반환은 오판 여지가 있으나, 호출자 둘 다
            // (DrainGoalEvents/OpenGoalCellAfterBreach) 라이브 맵이 있어야만 도달한다.
            if (!_generatedMap.IsCreated) return default;
            bool hasList = _generatedMap.goals.IsCreated && _generatedMap.goals.Length > 0;
            int count = hasList ? _generatedMap.goals.Length : 1;
            Vector2Int best = default;
            float bestSq = float.MaxValue;
            for (int i = 0; i < count; i++)
            {
                int2 cell = hasList ? _generatedMap.goals[i] : _generatedMap.goal;
                float3 c = GridToWorldCenter(new Vector2Int(cell.x, cell.y));
                float dx = c.x - position.x, dz = c.z - position.z;
                float d = dx * dx + dz * dz;
                if (d < bestSq) { bestSq = d; best = new Vector2Int(cell.x, cell.y); }
            }
            return best;
        }

        // goal-tower-siege(rev 2) — 돌격형(공격 수단 없는 적)의 자폭 피해. 표준 경로와 같은
        // 통로(IncomingDamage)로 넣어 DamageApplicationSystem 이 처리하게 한다.
        // 적이 도달한 골이 어느 쪽인지는 이벤트에 실린 위치로 가른다(골 2개 맵).
        private void EnqueueGoalTowerDamage(int amount, float3 atPosition)
        {
            if (!HasLiveEntityManager() || amount <= 0) return;
            using var towerQuery = _em.CreateEntityQuery(
                ComponentType.ReadOnly<Wassup.Battle.Units.GoalTowerTag>(),
                ComponentType.ReadOnly<LocalTransform>(),
                ComponentType.ReadWrite<IncomingDamage>());
            if (towerQuery.IsEmpty)
            {
                // 붕괴 후에는 타워가 없는 게 정상이다(피해 대신 스트레스가 오른다) — 경고 금지.
                if (!_towerMissLogged && _breachedCells.Count == 0)
                {
                    _towerMissLogged = true;
                    Debug.LogWarning("[BattleBridge] 골 타워가 없다 — 자폭 피해가 유실된다.", this);
                }
                return;
            }
            var towers = towerQuery.ToEntityArray(Allocator.Temp);
            var nearest = towers[0];
            float bestSq = float.MaxValue;
            for (int i = 0; i < towers.Length; i++)
            {
                float3 p = _em.GetComponentData<LocalTransform>(towers[i]).Position;
                float sq = math.distancesq(p, atPosition);
                if (sq < bestSq) { bestSq = sq; nearest = towers[i]; }
            }
            _em.GetBuffer<IncomingDamage>(nearest).Add(new IncomingDamage { amount = amount });
            towers.Dispose();
        }

        // goal-tower-siege(rev 2) — 안정도 = **타워의 Health** 다. 별도 정본(싱글턴)이 없으므로
        // 브리지는 그것을 읽어 미러(_goalStability)를 갱신하고 패배만 판정한다.
        // 공개 API(GoalStabilityCurrent/Max)는 불변이라 체력바와 점수 tie-break 는 그대로다.
        // 미러는 판정을 소유하지 않는 «가장 위험한 골» 읽기 캐시다(계약 7 — 판정은 per-entity).
        //
        // battle-structures unit 4(ⓐ) — 붕괴는 **셀 단위**다. 등록부를 순회해 사라진(또는
        // Health 0) 엔티티의 셀을 특정한다. 구현이 count 비교였던 시절엔 «하나 부서짐 = 전부
        // 파괴 + 전역 전환» 이라 계약 7 을 표현할 수 없었다.
        // 표준 사망 경로(DeadTag → UnitLifecycleSystem 파괴)는 그대로다 — 브리지는 관측만 한다.
        private void SyncGoalStability()
        {
            // 리뷰 M-7 — 게이트는 등록부 유무다. 구 _goalTowerCount 게이트는 «골 타워 개수»
            // 라는 무관한 개념에 거점(본능·적 마음) 붕괴 관측까지 가둬, 덱 미저작 판에서
            // 거점 붕괴가 영구 미관측이었다.
            if (!HasLiveEntityManager() || _resultShown || _structureRegistry.Count == 0) return;

            float lowest = float.MaxValue;
            float maxHp = 0f;
            bool newCoreBreach = false;
            List<Vector2Int> newBreaches = null;   // 붕괴는 드문 사건 — lazy 할당
            for (int i = _structureRegistry.Count - 1; i >= 0; i--)
            {
                var (entity, cell, faction) = _structureRegistry[i];
                bool alive = _em.Exists(entity) && _em.HasComponent<Health>(entity);
                var health = alive ? _em.GetComponentData<Health>(entity) : default;   // 리뷰 L-11 — 1회 조회
                if (alive && health.value > 0f)
                {
                    if (faction != Faction.DefenderCore) continue;   // 본능·적 마음은 미러에 안 섞는다
                    if (health.value < lowest) lowest = health.value;
                    if (health.max > maxHp) maxHp = health.max;
                    continue;
                }

                // 부서졌다(엔티티 부재 또는 Health 0 — 파괴는 사망 경로가 곧 처리한다).
                _structureRegistry.RemoveAt(i);
                if (faction == Faction.DefenderCore)
                {
                    if (_breachedCells.Add(cell))
                    {
                        newCoreBreach = true;
                        // 리뷰 A-M1 — 여기서 바로 열지 않는다. 열기(유출 전환)는 아래 미러
                        // 갱신 **뒤** — 붕괴가 만든 유출이 이 프레임에 스트레스 상한을 채우면
                        // 그 사슬(LeakSiegingEnemy → CheckStressDefeat → BeginTally →
                        // EncodeSubmission)이 _goalStability 를 제출값으로 싣는데, 루프 안에서
                        // 열면 지난 프레임의 **양수** 미러가 제출된다(구 코드는 0 을 먼저
                        // 놓았다 — 순서 회귀였다).
                        (newBreaches ??= new List<Vector2Int>()).Add(cell);
                    }
                }
                else
                {
                    // 본능·적 마음 붕괴 — v1 은 연출·로그만(README 결정 2). 사격이 멎는 것
                    // 자체가 보상이고, 유출 전환·스트레스는 방어 마음(골) 전용이다.
                    Debug.Log($"[BattleBridge] Structure collapsed — cell=({cell.x},{cell.y}) faction={faction}");
                    tileHealthGaugeLayer?.Hide(cell);
                    vfxSpawner?.SpawnGoalCollapse(GridToWorldCenterVector(cell));
                }
            }

            if (newCoreBreach)
            {
                // 붕괴 프레임의 «가장 위험한 골» 은 방금 0 이 되어 죽은 그 골이다 — 미러도
                // 0 을 보여준다(생존 골 체력으로 덮으면 «부서졌는데 191» 이 HUD 에 뜬다).
                // StressLimit 0 즉시 패배는 이 프레임에 _resultShown 이 서므로 0 으로 얼어붙는다
                // = 구 동작과 동일. 다음 프레임부터는 생존 골 중 최저를 다시 보여준다.
                _goalStability = 0;
            }
            else if (lowest != float.MaxValue)
            {
                _goalStabilityMax = Mathf.Max(0, Mathf.RoundToInt(maxHp));
                // 표시는 올림 — 0.3 남았는데 화면에 0 이 뜨면 "죽었는데 안 죽었다" 가 된다.
                // 골이 여럿이면 **가장 위험한 골**을 보여준다.
                _goalStability = Mathf.Max(0, Mathf.CeilToInt(lowest));
            }
            else
            {
                _goalStability = 0;   // 마음이 하나도 안 남았다
            }

            if (!newCoreBreach) return;

            // 리뷰 A-M1 — 미러가 0 이 된 뒤에 연다(위 주석 참조).
            if (newBreaches != null)
                for (int i = 0; i < newBreaches.Count; i++)
                    OpenGoalCellAfterBreach(newBreaches[i]);

            // stress-after-breach (2026-08-08 사용자 결정) — 골 파괴는 더 이상 그 자체로 패배가
            // 아니다. 상한이 있으면 그 셀이 **유출 지점으로 전환**되고(위에서 이미 열었다),
            // 부서진 셀로의 유출 1회 = 스트레스 1 이 상한에 닿을 때 패배한다.
            if (StressLimit > 0)
            {
                Debug.Log($"[BattleBridge] 골 붕괴 — {_breachedCells.Count}개 셀 유출 전환. 스트레스 {_goalReachedCount}/{StressLimit} 에서 패배.");
                return;
            }

            // 상한 0 = 구 동작 보존: 마음 하나라도 부서지면 즉시 패배.
            _resultShown = true;
            _running = false;
            var score = CalculateBattleScore(defeated: true);
            GameManager.Instance?.Logger?.SetResult("defeat", _goalReachedCount);
            GameManager.Instance?.Logger?.SetScore(score.Total, score.Kill);
            BeginTally(win: false, score, RemainingBattleSeconds());
            Debug.Log("[BattleBridge] DEFEAT — 골이 부서졌다(스트레스 상한 0).");
        }

        // 붕괴 처리(ⓐ: **그 셀만**) — 그 셀에서 공성 중이던 적을 유출로 전환한다. 다른 골의
        // 공성 적은 건드리지 않는다. 전환하지 않으면 때릴 타워가 없는 적이 눌러앉아 웨이브
        // 전멸 판정을 영구히 막는다(구 OpenGoalAfterBreach 와 같은 이유 — 그쪽은 전 타워
        // 파괴 + 전원 전환이었다).
        private void OpenGoalCellAfterBreach(Vector2Int cell)
        {
            if (!HasLiveEntityManager()) return;
            tileHealthGaugeLayer?.Hide(cell);
            vfxSpawner?.SpawnGoalCollapse(GridToWorldCenterVector(cell));
            using var siegeQuery = _em.CreateEntityQuery(
                ComponentType.ReadOnly<Wassup.Battle.Units.GoalReachedMarker>(),
                ComponentType.ReadOnly<AttackUnitTag>(),
                ComponentType.ReadOnly<LocalTransform>());
            var sieging = siegeQuery.ToEntityArray(Allocator.Temp);
            var positions = siegeQuery.ToComponentDataArray<LocalTransform>(Allocator.Temp);
            for (int i = 0; i < sieging.Length; i++)
                if (NearestGoalCell(positions[i].Position) == cell)
                    LeakSiegingEnemy(sieging[i]);
            sieging.Dispose();
            positions.Dispose();
        }

        // 공성 중이던 적 1기를 유출로 처리한다(뷰 회수 + 스트레스 + 엔티티 파괴).
        // 안정도 피해는 넣지 않는다 — 골은 이미 부서졌고 이제 세는 것은 스트레스다.
        private void LeakSiegingEnemy(Entity entity)
        {
            if (!_em.Exists(entity)) return;
            _goalReachedCount++;
            enemyViewPool?.Despawn(entity);
            spineUnitPool?.Despawn(entity);
            NotifyEnemyGoneIfMarked(entity);
            _enemyTypeByEntity.Remove(entity);
            _em.DestroyEntity(entity);
            RefreshLeakHud();
            CheckStressDefeat();
        }

        // 스트레스 상한 패배. 상한 0 = 이 경로 없음(골 파괴가 즉시 패배를 소유).
        private void CheckStressDefeat()
        {
            // 상한의 on/off 는 덱 원본값(StressLimit)이, **문턱값**은 EffectiveLeakLimit()이 정한다 —
            // HUD 분모와 같은 값이어야 화면에서 검산된다(몽마의 계약 선불 차감이 반영된 값).
            if (_resultShown || StressLimit <= 0 || _goalReachedCount < EffectiveLeakLimit()) return;
            _resultShown = true;
            _running = false;
            var score = CalculateBattleScore(defeated: true);
            GameManager.Instance?.Logger?.SetResult("defeat", _goalReachedCount);
            GameManager.Instance?.Logger?.SetScore(score.Total, score.Kill);
            BeginTally(win: false, score, RemainingBattleSeconds());
            Debug.Log($"[BattleBridge] DEFEAT — 스트레스 상한 도달 ({_goalReachedCount}/{StressLimit}).");
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
            // 버팀 승리는 패배가 아니다. defeated:true 를 넘기면 스트레스점수까지 죽는다 —
            // 남은 시간이 0 이라 시간점수는 이미 자동으로 0 이다.
            var score = CalculateBattleScore(defeated: false);
            int playerScore = score.Total;
            GameManager.Instance?.Logger?.SetResult("victory_timeout", _goalReachedCount);
            GameManager.Instance?.Logger?.SetScore(playerScore, score.Kill);
            BeginTally(win: true, score, 0f); // timer expired → 0 left
            Debug.Log("[BattleBridge] VICTORY — timer expired, player survived.");
        }

        // Victory = every spawn in the deck has been processed AND no attack unit entities remain alive.
        private void CheckVictory()
        {
            if (_resultShown) return;
            if (_usingGeneratedWaves && _wavePlan.waves != null && _nextWaveIndex < _wavePlan.waves.Count) return;
            if (!NoQueuedAttackersRemain()) return;

            _resultShown = true;
            _running = false;
            var score = CalculateBattleScore(defeated: false);
            int playerScore = score.Total;
            GameManager.Instance?.Logger?.SetResult("victory", _goalReachedCount);
            GameManager.Instance?.Logger?.SetScore(playerScore, score.Kill);
            BeginTally(win: true, score, RemainingBattleSeconds());
            Debug.Log("[BattleBridge] VICTORY — all attack units defeated.");
        }

        // nextwave-clear-attention unit 0 — 최종 승리와 웨이브 사이 클리어가 공유하는
        // emptiness source of truth. pending 은 호출됐지만 아직 스폰되지 않은 적,
        // AttackUnitTag query 는 이미 필드에 나온 적을 각각 담당한다.
        //
        // three-minute-survival unit 2 — 이제 **웨이브 진행 트리거**이기도 하다(QueueDueWaves).
        // 클리어 강조 UI 는 은퇴했지만 이 판정은 그 자리에 남아 케이던스를 구동한다.
        private bool NoQueuedAttackersRemain()
        {
            if (_pending.Count > 0 || !_aliveAttackersQueryCreated) return false;
            return _aliveAttackersQuery.CalculateEntityCount() == 0;
        }

        // tournament-play-report Units 3/4 — shared result-popup hook: snapshot
        // the deck carried into this match (tournament-deck-info unit 1 — the
        // battle log is no longer sent), send complete, and swap the popup's
        // pending leaderboard for the real ranking
        // when it arrives. Guests and failures fall through silently — the pending
        // list stays. The popup usually isn't open yet when the response lands
        // (ranking beats the ~4s tally) — ResultScreen holds an early response and
        // opens on it, so this callback stays fire-and-forget.
        private void ReportMatchResult(int playerScore)
        {
            // endless-mode unit 2 — 무한 모드는 토너먼트에 리포트하지 않는다(계약 5). 결과 팝업은 정상 표시.
            if (IsEndless)
            {
                Debug.Log("[BattleBridge] ENDLESS — 토너먼트 리포트 스킵.");
                return;
            }
            var logger = GameManager.Instance?.Logger;
            Wassup.Core.Api.TournamentMatchReporter.ReportResult(playerScore, logger?.DeckInfoJson(),
                ranking => resultScreen?.UpdateLeaderboard(ranking, Wassup.Core.Api.UserSession.Current?.userId),
                // tournament-flow-guards unit 2 — 실제 complete 실패만 알림(논블로킹, 재시도 없음).
                onError: _ => Wassup.UI.NoticePopup.ShowAlert("점수 전송 실패",
                    "이번 판 점수가 서버에 전송되지 않았습니다.\n네트워크 상태를 확인해 주세요."));
        }

        // score-tally-sequence unit 1 — 전투 종료 → 결과 연출 → 결과 화면의 단일 관문.
        // 종료 3종(패배/버팀승리/전멸승리)이 전부 여기로 들어온다.
        //
        // **서버 제출은 여기서(연출 시작 시점) 한다** — 연출이 끝나길 기다리면 그 사이
        // 앱이 죽었을 때 기록이 통째로 사라진다. 화면 연출과 기록 전송은 독립이다(계약 3).
        //
        // Tally 동안 전투 HUD 중 ScoreHud 만 살아남는다(연출의 주인공). NextWaveDock·
        // CostDisplay 등은 `== GamePhase.Battle` 을 보므로 자동으로 꺼진다.
        // three-minute-survival unit 3 — **합산 연출(탤리)은 제거됐다.** 시간·스트레스 축이
        // 사라져 더할 것이 없다: 전투 중 HUD 숫자가 이미 최종 점수다. 남기면 내용 없는
        // 4초 정지가 된다.
        //
        // `GamePhase.Tally` 전이는 유지한다 — 전투 HUD 게이팅이 그 페이즈를 읽고, 서버 제출
        // 지점(연출과 독립이라는 계약 3)도 여기 그대로 있다.
        private void BeginTally(bool win, ScoreMath.BattleScore score, float remainingSec)
        {
            GameManager.Instance?.SetPhase(GamePhase.Tally);
            // 제출값에 동점 판정(남은 안정도)을 실어 보낸다 — 서버는 int 하나만 받는다.
            ReportMatchResult(ScoreMath.EncodeSubmission(score.Total, _goalStability, _goalStabilityMax));
            FinishTally(win, score, remainingSec);
        }

        // 연출 종료 → 결과 화면. Result 페이즈로 넘어가며 남은 전투 HUD 가 정리된다.
        // RESTART 는 Result → Placement → Battle 로 되돌아간다(BeginPlacementPhase).
        private void FinishTally(bool win, ScoreMath.BattleScore score, float remainingSec)
        {
            GameManager.Instance?.SetPhase(GamePhase.Result);
            // three-minute-survival unit 3 — 결과 3줄: 처치 수 / 남은 안정도 / 도달 웨이브.
            // 총점(=처치 점수)은 score.Total 이 들고 온다.
            var stats = new Wassup.UI.ResultScreen.MatchStats(
                _killCount, _goalStability, _goalStabilityMax, ReachedWaveNumber, score);
            if (win) resultScreen?.ShowVictory(score, stats);
            else resultScreen?.ShowDefeat(score, stats);
        }

        // 도달 웨이브 = 마지막으로 큐잉된 웨이브 번호. _nextWaveIndex 는 "다음에 나올" 인덱스라
        // 그대로 쓰면 아직 안 나온 웨이브를 도달로 센다.
        private int ReachedWaveNumber => _nextWaveIndex > 0 ? _nextWaveIndex : 0;

        // three-minute-survival unit 3 — 점수는 처치로만 번다. 시간·스트레스 축과 그 배점
        // (ScoreRulesData)은 폐기됐고 패배 분기도 없다 — 져도 잡은 만큼은 남는다.
        // `defeated` 인자는 호출부 3곳(패배·버팀승리·전멸승리)의 의미를 남기기 위해 유지하되
        // 산식에 영향을 주지 않는다.
        private ScoreMath.BattleScore CalculateBattleScore(bool defeated)
            => ScoreMath.Evaluate(_killScoreTotal);

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

        // placement-eligible-tile-highlight unit 2 — 공간 배치 조건만(IsCreated/bounds/마스크/점유).
        // 순수 static(값 in → reason out): 판정(CanPlaceDefenderAt)과 하이라이트 셀 수집이 공유해
        // 어긋나지 않게 한다(PaintLanes 가 시뮬 발사 게이트를 공유하는 것과 동형). EditMode 테스트 대상.
        // 비용/풀/유닛/running 은 CanPlaceDefenderAt 이 별도로 본다.
        // placement-mask unit 1 — 배치 가능성의 정본은 placeMask. 타일 종류와 직교 —
        // Walk 셀도 마스크가 열려 있으면 배치된다(B-1: 통행·행동 규칙은 불변, 위치 제약만 해제).
        // unit 4 — 판정 = (셀 층 & 유닛 층) != 0. 층 교집합을 계산하는 유일한 지점이며,
        // 여기서도 유닛 **클래스**는 보지 않는다 — 호출자가 넘긴 비트만 본다.
        public static PlacementRejectReason SpatialPlacementCheck(
            GeneratedMap map, HashSet<Vector2Int> occupied, int2 cell, PlacementLayer layers)
        {
            if (!map.IsCreated) return PlacementRejectReason.MissingMap;
            if (cell.x < 0 || cell.x >= map.gridSize.x || cell.y < 0 || cell.y >= map.gridSize.y)
                return PlacementRejectReason.OutOfBounds;
            if (!map.PlaceableAt(cell, layers)) return PlacementRejectReason.NotBuildable;
            if (occupied != null && occupied.Contains(new Vector2Int(cell.x, cell.y))) return PlacementRejectReason.Occupied;
            return PlacementRejectReason.None;
        }

        public bool CanPlaceDefenderAt(int tileX, int tileY, DefenderUnitData unitData, out PlacementRejectReason reason)
        {
            if (!_running && !_placementAllowed)
            {
                reason = PlacementRejectReason.NotRunningOrPlacementClosed;
                return false;
            }
            // unit 4 — 층 교집합에 쓸 유닛 레이어. null 은 아래 InvalidUnit 이 잡으므로 여기선 Ground 폴백
            // (사유 우선순위 보존 — 공간 판정이 유닛 검사보다 앞선다는 기존 계약).
            var layers = unitData != null ? unitData.EffectivePlacementLayers : PlacementLayer.Ground;
            var spatial = SpatialPlacementCheck(_generatedMap, _occupiedTiles, new int2(tileX, tileY), layers);
            if (spatial != PlacementRejectReason.None)
            {
                reason = spatial;
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

        // placement-eligible-tile-highlight unit 2 — 배치 가능 셀 하이라이트 게이트웨이(뷰 포워딩, ECS 쓰기 0).
        // 공간 술어로 밝힐 셀을 수집 → TilemapMapView. 비용/풀은 안 본다(계약: 하이라이트=공간, hover=전체 판정).
        private bool _placeableHlShown;
        private readonly List<Vector2Int> _placeableHlScratch = new();
        // unit 4 — 하이라이트는 유닛 종속: 드는 유닛의 층으로 스캔한다(Ground 유닛이면 배치지면이,
        // Path 유닛이면 경로가 빛난다). 유닛 미상이면 Ground 폴백.
        private DefenderUnitData _placeableHlUnit;

        // unit 4 리뷰 M-1 — 라이브 맵에서 한 셀의 모든 배치 층을 닫는다(스폰·골 불변식용).
        private void CloseCellLayers(int2 cell)
        {
            if (cell.x < 0 || cell.x >= _generatedMap.gridSize.x
                || cell.y < 0 || cell.y >= _generatedMap.gridSize.y) return;
            _generatedMap.placeMask[_generatedMap.CellIndex(cell)] = 0;
        }

        // 표시 여부 read seam — 컨트롤러가 자기 래치와 실제 상태를 대조해 자기치유하기 위함(unit 4 리뷰 C-1).
        public bool IsPlacementHighlightShown => _placeableHlShown;

        public void ShowPlacementHighlight(DefenderUnitData unit)
        {
            _placeableHlShown = true;
            _placeableHlUnit = unit;
            RepaintPlacementHighlight();
        }

        public void HidePlacementHighlight()
        {
            _placeableHlShown = false;
            _placeableHlUnit = null;
            if (tilemapMapView != null) tilemapMapView.ClearPlacementHighlight();
        }

        public void RefreshPlacementHighlightIfShown() { if (_placeableHlShown) RepaintPlacementHighlight(); }

        private void RepaintPlacementHighlight()
        {
            if (!_placeableHlShown || tilemapMapView == null || !_generatedMap.IsCreated) return;
            _placeableHlScratch.Clear();
            var layers = _placeableHlUnit != null ? _placeableHlUnit.EffectivePlacementLayers : PlacementLayer.Ground;
            int w = _generatedMap.gridSize.x, h = _generatedMap.gridSize.y;
            for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
                if (SpatialPlacementCheck(_generatedMap, _occupiedTiles, new int2(x, y), layers) == PlacementRejectReason.None)
                    _placeableHlScratch.Add(new Vector2Int(x, y));
            tilemapMapView.SetPlacementHighlight(_placeableHlScratch);
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
            RefreshPlacementHighlightIfShown(); // placement-eligible-tile-highlight unit 2
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
            RefreshPlacementHighlightIfShown(); // placement-eligible-tile-highlight unit 2
            GameManager.Instance?.Logger?.RecordPlacement(unitData.displayName, cell, Time.time - _startTime, unitData.cost);
            entity = CreateDefenderEntity(cell, unitData, pendingDeployment: true, spawnPlacementVfx: false);
            ApplyOnPlacePush(unitData, cell);
            // battle-audio: 유닛별 배치 보이스(deployVoiceClip). 미할당 유닛은 통합 폴백.
            Wassup.Core.SoundManager.Instance?.PlayDeployPlace(unitData.deployVoiceClip);
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

        // placement-thumb-occlusion — 소유권 전환의 유일한 지점. **모든** `_rangeOwner` 대입이 여기를 탄다.
        //
        // 왜 필드 대입을 감싸는가: 배치 유효성 적색(`SetPlacementRangeValidity`)은 **Placement 만 소유**하는
        // 채널인데, 소유권은 획득 시 덮어쓰기로 전환된다(SkillAim 획득이 ClearRange(Placement) 를 부르지
        // 않는다). 전환 지점이 7개라 per-site 로 리셋을 얹으면 반드시 빠지는 곳이 생긴다 — 실제로 초판은
        // 3곳만 얹혀서 StopBattle 과 SetAimGuide 가 새고, TilemapMapView.Clear() 의 방어 리셋이 우연히
        // 덮고 있었다. 여기 한 줄로 만들면 6번째 owner 를 추가하는 사람이 규칙을 외울 필요가 없다.
        //
        // **Placement 는 면제다** — 배치 경로는 포커스 셀이 바뀔 때마다 이 함수를 타므로 여기서 리셋하면
        // false→true 전이가 매번 재발생해 무효 영역을 훑는 동안 플래시가 연발한다(TilemapMapView 의
        // SetPlacementRangeValidity 주석이 경고하는 그 상황). 배치 경로의 리셋은 컨트롤러가 세션 경계에서
        // 명시적으로 한다(ClearHover / ClearBoardScout). 이 비대칭은 load-bearing이니 "통일"하지 말 것.
        private void SetRangeOwner(RangeDisplayOwner next)
        {
            _rangeOwner = next;
            if (next != RangeDisplayOwner.Placement && tilemapMapView != null)
                tilemapMapView.SetPlacementRangeValidity(true);
        }
        private readonly List<Vector2Int> _laneCellScratch = new List<Vector2Int>();
        // unit 5 — 레인 셀과 **그 셀이 속한 방향**을 나란히 모은다. 아이콘이 이 목록에서 나오므로
        // "아이콘 = 칠해진 칸"이 구조적으로 보장된다(셀 선정이 LaneMath 를 공유하는 것과 같은 결).
        private readonly List<Vector2Int> _laneDirScratch = new List<Vector2Int>();
        private readonly List<float> _arrowAngles = new List<float>();
        // 방향 미정(4레인 십자) 세기. 선택된 레인은 1. 0.45 는 시안 하이라이트가
        // 비쳐 다른 색으로 오독됨(2026-07-19 사용자) — 같은 주황으로 읽히는 0.7.
        private const float AimLaneDimAlpha = 0.7f;
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
            // defender-ability-assets unit 2 — 폭탄병은 레인도 거짓말(착지 셀만 때린다) →
            // 조준 페이즈(SetAimGuide)와 같은 착지 후보 4셀. 나머지 facing 유닛은 레인 유지.
            // aimStyle=false — 여기는 아직 배치 단계다. 조준 해치는 드롭 뒤에 나온다(unit 4).
            // summon-patrol-defender unit 5 — 소환사에게 공격범위는 거짓말이다(본인은 안 때린다).
            // 읽어야 할 정보는 **순찰병이 지킬 거점 반경**이고, 이 유닛에서 배치 판단의 전부가
            // 그것이다. 판별은 능력 에셋 보유로 한다(id/kind 분기 금지 — beamVfxPrefab 관례).
            // 중심은 실제 거점과 같게 **walk 셀로 스냅**한다(계약 4). 스냅 실패면 아무것도
            // 그리지 않는다 — 그 자리에 놓으면 소환이 취소된다는 신호다.
            var summonPreview = unit.GetAbility<SummonPatrolAbility>();
            var bombPreview = unit.GetAbility<BombThrowAbility>();
            if (summonPreview != null && summonPreview.patrolUnit != null)
            {
                if (TryGetPatrolAnchorCell(new int2(center.x, center.y), summonPreview.leashTileRadius, out var leashCell))
                    tilemapMapView.SetPlacementRange(
                        new Vector2Int(leashCell.x, leashCell.y), math.max(0, summonPreview.leashTileRadius));
                else
                    tilemapMapView.ClearPlacementRange();
            }
            else if (bombPreview != null) PaintLandingCells(center, bombPreview.landingTiles, null, AimLaneDimAlpha, aimStyle: false);
            else if (unit.RequiresFacing) PaintLanes(center, tileRange, null, AimLaneDimAlpha, aimStyle: false);
            else tilemapMapView.SetPlacementRange(center, tileRange);
            SetRangeOwner(RangeDisplayOwner.Placement); // 유효성 면제 — 컨트롤러가 매 프레임 소유
        }

        public void ClearPlacementRange() => ClearRange(RangeDisplayOwner.Placement);

        // unit 9 — 조준 페이즈 가이드(레인 + 화살표를 한 상태로). 선택 전이면 4레인 십자를
        // 흐리게 = "이 중 하나를 커버한다"(사거리가 즉시 읽힌다), 선택되면 그 레인만 또렷 =
        // "여기를 때린다". 방향을 말해주는 건 레인이 아니라 화살표다 — 레인은 대칭이라
        // "위로 쏜다"와 "위에서 쏜다"가 같아 보인다.
        public void SetAimGuide(Vector2Int center, DefenderUnitData unit, Vector2Int? selected)
        {
            if (tilemapMapView == null || unit == null) return;
            var bombAim = unit.GetAbility<BombThrowAbility>();
            if (bombAim != null)
            {
                // bomb-thrower-defender unit 8 — 착지 타일 조준: 상하좌우 N칸 착지 후보만
                // 하이라이트(레인/화살표 없음 — 머신거너와 다른 모드). 선택되면 그 착지 셀만.
                PaintLandingCells(center, bombAim.landingTiles, selected, 1f, aimStyle: true);
                SetRangeOwner(RangeDisplayOwner.PlacementAim);
                tilemapMapView.ClearAimArrows();
                return;
            }
            int tileRange = GridMath.RangeToTiles(unit.attackRange);
            // unit 4 — 조준 표시는 세기 배율을 쓰지 않는다(전부 불투명). 미선택/선택은 알파가
            // 아니라 **몇 개를 그리느냐**로 갈린다: 미선택=4레인 전부, 선택=그 레인 하나만.
            // 드래그 프리뷰(SetPlacementRange)의 dim 은 outline 이라 그대로 AimLaneDimAlpha.
            PaintLanes(center, tileRange, selected, 1f, aimStyle: true);
            SetRangeOwner(RangeDisplayOwner.PlacementAim);

            // unit 5 — 아이콘은 첫 칸이 아니라 **칠해진 모든 칸**에 얹는다. 채움만으로는
            // "위로 쏜다"와 "위에서 쏜다"가 여전히 같아 보이는데(unit 9 판단 1), 칸마다
            // 화살표가 있으면 레인 전체가 방향을 말한다. 목록은 PaintLanes 가 방금 고른
            // 셀/방향 그대로라 아이콘과 타일이 어긋날 수 없다.
            _arrowAngles.Clear();
            for (int i = 0; i < _laneDirScratch.Count; i++)
            {
                var d = _laneDirScratch[i];
                // 스프라이트는 +Y 를 향한다 → 그 방향으로 눕힌다.
                _arrowAngles.Add(Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg - 90f);
            }
            // 선택되면 그 레인만 칠해지므로, 남은 아이콘은 전부 선택된 레인의 것이다 —
            // 강조는 개별 인덱스가 아니라 상태 하나로 충분하다.
            tilemapMapView.SetAimArrows(_laneCellScratch, _arrowAngles, selected.HasValue);
        }

        public void ClearAimGuide()
        {
            ClearRange(RangeDisplayOwner.PlacementAim);
            if (tilemapMapView != null) tilemapMapView.ClearAimArrows();
        }

        // 칠할 셀을 시뮬의 발사 게이트와 **같은 함수**로 고른다 — 보이는 칸과 실제로 맞는
        // 칸이 구조적으로 일치한다(따로 계산하면 언젠가 어긋난다).
        private void PaintLanes(Vector2Int center, int tileRange, Vector2Int? facing, float alphaMul, bool aimStyle)
        {
            if (tileRange <= 0) return;
            CollectLaneCells(center, tileRange, facing);
            tilemapMapView.SetPlacementCells(_laneCellScratch, alphaMul, aimStyle);
        }

        // 방향별로 나눠 훑어 셀과 방향을 짝지어 모은다. 판정은 여전히 시뮬의 발사 게이트
        // (`LaneMath.IsInLane`)라 보이는 칸 = 실제로 맞는 칸이 유지된다.
        private void CollectLaneCells(Vector2Int center, int tileRange, Vector2Int? facing)
        {
            _laneCellScratch.Clear();
            _laneDirScratch.Clear();
            var c = new int2(center.x, center.y);
            for (int i = 0; i < AimCardinals.Length; i++)
            {
                var dir = AimCardinals[i];
                if (facing.HasValue && facing.Value != dir) continue;
                var d = new int2(dir.x, dir.y);
                for (int dx = -tileRange; dx <= tileRange; dx++)
                for (int dz = -tileRange; dz <= tileRange; dz++)
                {
                    var cell = new int2(center.x + dx, center.y + dz);
                    if (!LaneMath.IsInLane(c, d, tileRange, cell)) continue;
                    _laneCellScratch.Add(new Vector2Int(cell.x, cell.y));
                    _laneDirScratch.Add(dir);
                }
            }
        }

        // bomb-thrower-defender unit 8 — 폭탄 착지 후보 셀. 미선택이면 4 cardinal 착지 셀
        // (center±N) 전부 dim, 선택되면 그 방향 착지 셀 1개만. PaintLanes 의 착지-셀 판.
        private void PaintLandingCells(Vector2Int center, int landingTiles, Vector2Int? facing, float alphaMul, bool aimStyle)
        {
            if (landingTiles <= 0) return;
            _laneCellScratch.Clear();
            if (facing.HasValue)
                _laneCellScratch.Add(center + facing.Value * landingTiles);
            else
                for (int i = 0; i < AimCardinals.Length; i++)
                    _laneCellScratch.Add(center + AimCardinals[i] * landingTiles);
            tilemapMapView.SetPlacementCells(_laneCellScratch, alphaMul, aimStyle);
        }

        // 스킬 조준 범위 — 배치와 달리 중심 셀 포함(AOE 는 중심도 피해 범위).
        public void SetSkillAimRange(Vector2Int center, SkillData skill)
        {
            if (skill == null) return;
            PinCenteredRange(center, GridMath.RangeToTiles(skill.range), RangeDisplayOwner.SkillAim);
        }

        public void ClearSkillAimRange() => ClearRange(RangeDisplayOwner.SkillAim);

        // active-dreamcatcher-tile-aim unit 1 — 타일 중심의 스크린 좌표(조준 화살표 끝점·확정
        // 펄스). sim→view 변환(BoardSpace.ToView)은 보드 공간을 소유한 bridge 안에 남는다 —
        // UI 가 sim 좌표를 카메라에 바로 넣으면 평면 뷰에서 셀이 어긋난다.
        public bool TryGetTileScreenCenter(Vector2Int cell, Camera cam, out Vector2 screen)
        {
            screen = default;
            if (cam == null || !_generatedMap.IsCreated) return false;
            var view = Wassup.Core.BoardSpace.ToView(GridToWorldCenter(cell));
            var p = cam.WorldToScreenPoint(new Vector3(view.x, view.y, view.z));
            screen = new Vector2(p.x, p.y);
            return true;
        }

        // active-dreamcatcher-tile-aim unit 2 — 임의 셀 집합 조준 점등(포탈의 입구+출구후보).
        // 타일맵의 range/cells 는 서로를 지우는 **단일 채널**이라, 두 지점을 동시에 보여주려면
        // 한 번에 칠해야 한다. 해제는 ClearSkillAimRange 가 같은 owner 를 반납한다.
        public void SetSkillAimCells(IReadOnlyList<Vector2Int> cells)
        {
            if (tilemapMapView == null || cells == null || cells.Count == 0) return;
            // aimStyle: false — 나머지 Active 5종의 범위 프리뷰(SetPlacementRange)와 같은 타일·틴트
            // 규칙을 쓴다. true 로 두면 한 번의 캐스트 안에서 점등 아트가 바뀐다(계약 8: 한 채널).
            tilemapMapView.SetPlacementCells(cells, 1f, aimStyle: false);
            SetRangeOwner(RangeDisplayOwner.SkillAim);
        }

        private void PinSkillTelegraph(Vector2Int cell, int tileRange)
            => PinCenteredRange(cell, tileRange, RangeDisplayOwner.SkillTelegraph);

        // 스킬 조준·텔레그래프 공통 — 중심 포함 사각 범위 + 소유권 전환. 둘은 owner 만 다르다.
        // 리셋을 여기 손으로 얹지 않는다: SetRangeOwner 가 Placement 외 전환에서 일괄 처리한다
        // (이 두 경로는 aimStyle=false 로 그려져 _rangeAimStyle 가드 밖이라 반드시 반납이 필요하다).
        private void PinCenteredRange(Vector2Int center, int tileRange, RangeDisplayOwner owner)
        {
            if (tilemapMapView == null) return;
            tilemapMapView.SetPlacementRange(center, tileRange, includeCenter: true);
            SetRangeOwner(owner);
        }

        private void ClearSkillTelegraph() => ClearRange(RangeDisplayOwner.SkillTelegraph);

        private void ClearRange(RangeDisplayOwner caller)
        {
            if (_rangeOwner != caller) return;
            SetRangeOwner(RangeDisplayOwner.None); // 반납도 전환이다 — validity 리셋이 여기 딸려온다
            if (tilemapMapView != null) tilemapMapView.ClearPlacementRange();
        }

        // placement-thumb-occlusion unit 3 — 배치 판정 유효성 → 사거리 틴트(적색 + 전이 플래시).
        // 사거리 페인트는 셀 변경 시만, 유효성은 매 프레임 뒤집힌다(슬로우모 중에도 전투가 돌아 점유가
        // 변하고 코스트가 리젠된다) → 수명이 달라 SetPlacementRange 에 인자를 얹지 않고 분리한다.
        public void SetPlacementRangeValidity(bool valid)
        {
            if (tilemapMapView != null) tilemapMapView.SetPlacementRangeValidity(valid);
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
            _em.AddBuffer<Wassup.Battle.Effects.DotEffect>(entity); // dot-effect-extraction unit 0
            _defenderByTile[cell] = (entity, unitData);
            _em.AddComponentData(entity, new DefenderTile { cell = new int2(cell.x, cell.y) });
#if UNITY_EDITOR
            _em.SetName(entity, $"Defender_{unitData.displayName}_{cell.x}_{cell.y}");
#endif
            var pos = GridToWorldCenter(cell, spawnHeight);
            _em.AddComponentData(entity, LocalTransform.FromPositionRotationScale(pos, quaternion.identity, CharacterVisualScale));
            _em.AddComponent<DefenderUnitTag>(entity);
            _em.AddComponentData(entity, new Health { value = unitData.health, max = unitData.health });
            _em.AddComponentData(entity, new FactionTag { value = Faction.DefenderUnit });
            _em.AddComponentData(entity, new AttackState
            {
                range = unitData.attackRange,
                cooldownDuration = unitData.attackCooldown,
                cooldownRemaining = unitData.deployDelaySec, // attack-hit-delay 2 — 배치 직후 deployDelaySec 동안 idle(공격 X)
                attackTargetCount = unitData.attackTargetCount,
                // battle-structures unit 0 — 아군 타게팅(힐러)은 DefenderUnit 단독이다.
                // AnyDefender 로 넓히면 IncomingHeal 버퍼가 없는 거점이 후보에 들어
                // ECB playback 에서 던진다. 적 타게팅도 EnemyUnit 단독 — 방어유닛이
                // 적 거점을 때리는 것은 이 spec 범위 밖(결정 4).
                targetMask = unitData.targetAllies ? (int)Faction.DefenderUnit : (int)Faction.EnemyUnit,
                hitDelaySec = unitData.hitDelaySec,
            });
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
                sleepOnHitSec       = unitData.sleepOnHitSec,
                knockupOnHitSec     = unitData.knockupOnHitSec,
                knockupVisualHeight = unitData.knockupVisualHeight,
            });
            // defender-ability-assets unit 2 — 게이트 = 능력 에셋 존재(구 hazardCastEnabled).
            var hazardAbility = unitData.GetAbility<HazardCastAbility>();
            if (hazardAbility != null)
            {
                int hazardDataIndex = -1;
                if (hazardAbility.kind == HazardCastKind.Zone)
                    hazardDataIndex = RegisterZoneHazardSO(hazardAbility.zoneHazard);
                else if (hazardAbility.kind == HazardCastKind.Blocking)
                    hazardDataIndex = RegisterBlockingHazardSO(hazardAbility.blockingHazard);

                _em.AddComponentData(entity, new HazardCastState
                {
                    range = hazardAbility.castRange,
                    cooldownDuration = hazardAbility.cooldown,
                    cooldownRemaining = 0f,
                    targetMask = (int)Faction.EnemyUnit,
                    dataIndex = hazardDataIndex,
                    kind = hazardAbility.kind,
                    footprintWidth = math.max(1, hazardAbility.footprintWidth),
                    footprintHeight = math.max(1, hazardAbility.footprintHeight),
                });
            }
            // shield-guardian-defender unit 1 — 실드 캐스트 베이크. 범위 = attackRange
            // 재사용(계약 5). 첫 캐스트는 배치 A초 후(cooldownRemaining = A).
            var shieldAbility = unitData.GetAbility<ShieldCastAbility>();
            if (shieldAbility != null && shieldAbility.cooldown > 0f && shieldAbility.amount > 0f)
            {
                _em.AddComponentData(entity, new Wassup.Battle.Effects.ShieldCastState
                {
                    range = unitData.attackRange,
                    cooldownDuration = shieldAbility.cooldown,
                    cooldownRemaining = shieldAbility.cooldown,
                    amount = shieldAbility.amount,
                    targetCount = shieldAbility.targetCount,
                    filter = shieldAbility.filter,
                });
            }
            // bomb-thrower-defender unit 3 — 폭탄 발사 상태 베이크. RNG 는 캐스터별 독립
            // stream(배치 셀 해시로 decorrelate → order-independent 결정론, 계약 6).
            // defender-ability-assets unit 2 — 게이트 = 능력 에셋 존재 + 유효 수치.
            var bombAbility = unitData.GetAbility<BombThrowAbility>();
            if (bombAbility != null && bombAbility.landingTiles > 0 && bombAbility.travelSec > 0f)
            {
                uint cellHash = (uint)(cell.x * 73856093) ^ (uint)(cell.y * 19349663);
                uint bombSeed = math.max(1u, (uint)Wassup.Core.MatchSeed.DeriveBombSeed(_matchSeed) ^ cellHash);
                _em.AddComponentData(entity, new Wassup.Battle.Combat.BombLauncherState
                {
                    landingTiles = bombAbility.landingTiles,
                    travelSec = bombAbility.travelSec,
                    fuseSec = bombAbility.fuseSec,
                    aoeTileRange = bombAbility.aoeTileRange,
                    aoeTargetCap = bombAbility.aoeTargetCap,
                    arcHeight = bombAbility.arcHeight,
                    dmgBombDamage = bombAbility.damage,
                    sleepSec = bombAbility.sleepSec,
                    stunSec = bombAbility.stunSec,
                    rng = new Unity.Mathematics.Random(bombSeed),
                });
            }
            // summon-patrol-defender unit 3 — 소환 능력 bake. 쿨다운은 AttackState 재사용이라
            // 여기서 따로 두지 않는다(계약 7 — 소환 = 공격).
            var summonAbility = unitData.GetAbility<SummonPatrolAbility>();
            if (summonAbility != null && summonAbility.patrolUnit != null)
            {
                _em.AddComponentData(entity, new Wassup.Battle.Combat.SummonerState
                {
                    patrolDataIndex = RegisterPatrolUnitSO(summonAbility.patrolUnit),
                    leashTileRadius = math.max(0, summonAbility.leashTileRadius),
                    current = Entity.Null,
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
                var dataIndex = BakeProjectileRef(entity, unitData.projectile);   // 리뷰 A-M3 — 단일 베이크
                BakeDefenderDirectionalPattern(entity, unitData, dataIndex);
            }
            else if (unitData.GetAbility<DirectionalVolleyAbility>() != null)
            {
                Debug.LogWarning(
                    $"[BattleBridge] {unitData.displayName}: DirectionalVolleyAbility needs defender projectile — pattern skipped.");
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
            // shield-guardian-defender unit 0 — 실드 슬롯/부여 버퍼 사전 부착
            // (IncomingHeal 선례 — hot path 에서 구조변경 없이 append 하기 위함).
            _em.AddBuffer<Wassup.Battle.Units.ShieldSlot>(entity);
            _em.AddBuffer<Wassup.Battle.Units.IncomingShield>(entity);

            // ingame-dreamcatcher Unit 2 — inherit active match-long card effects.
            ApplyActiveDcEffectsTo(entity, unitData);

            return entity;
        }

        // summon-patrol-defender unit 2 — 거점 순찰 아군(Patrol) 엔티티+뷰 생성.
        //
        // CreateDefenderEntity 를 재사용하지 않는 이유: 그쪽은 _defenderByTile 등록과
        // DefenderTile 부착을 한다 — 배치 점유·재배치·DefenderDeathEvent·사직서 드랍을
        // 통째로 끌고 들어온다. 순찰병은 그 어느 것도 타면 안 된다(README 계약 1).
        //
        // anchorCell 은 **walk 셀**이어야 한다. 호출자가 TryGetNearestWalkCell 로 스냅해
        // 넘긴다 — 방어유닛 셀은 통상 walkable 이 아니라서(placement-mask B-1 의 Walk 셀
        // 배치는 예외 — 그땐 스냅이 자기 셀을 반환) 그대로 쓰면 순찰병이 설 수 없는 칸을
        // 향해 영원히 전진한다(계약 4).
        // owner == Entity.Null 이면 SummonedBy 미부착 = 연쇄 소멸 대상 아님(디버그 스폰).
        private Entity CreatePatrolEntity(
            DefenderUnitData unitData,
            int2 anchorCell,
            int tileRadius,
            Entity owner)
        {
            if (unitData == null || _em == default) return Entity.Null;

            var entity = _em.CreateEntity();
            _em.AddBuffer<IncomingDamage>(entity);
            _em.AddBuffer<Wassup.Battle.Effects.CcEffect>(entity);
            _em.AddBuffer<Wassup.Battle.Effects.DotEffect>(entity);

            var cellV2 = new Vector2Int(anchorCell.x, anchorCell.y);
            var pos = GridToWorldCenter(cellV2, spawnHeight);
            _em.AddComponentData(entity, LocalTransform.FromPositionRotationScale(pos, quaternion.identity, CharacterVisualScale));
#if UNITY_EDITOR
            _em.SetName(entity, $"Patrol_{unitData.displayName}_{anchorCell.x}_{anchorCell.y}");
#endif
            // 계약 1 — DefenderUnitTag 는 선택이 아니다. DestroyBattleEntities 가 타입 기반
            // 파괴라 이 태그가 없으면 매치 경계에서 안 지워지고 앱 수명 world 에 잔존한다.
            _em.AddComponent<DefenderUnitTag>(entity);
            // 계약 1 — DefenderClassTag 도 붙인다. 태그 없음 면제는 EnemyTargetFilter 주석대로
            // 무생물(blocking hazard)용이라, 생물을 태그 없이 태우면 클래스 하드 타게팅 적
            // (킨들러 = 레인저 전용 마스크)이 레인저 대신 순찰병을 쏴서 그 적이 무력화된다.
            _em.AddComponentData(entity, new DefenderClassTag { value = unitData.role });
            _em.AddComponentData(entity, new Health { value = unitData.health, max = unitData.health });
            _em.AddComponentData(entity, new FactionTag { value = Faction.DefenderUnit });
            _em.AddComponentData(entity, new AttackState
            {
                range = unitData.attackRange,
                cooldownDuration = unitData.attackCooldown,
                cooldownRemaining = unitData.deployDelaySec,
                attackTargetCount = unitData.attackTargetCount,
                targetMask = (int)Faction.EnemyUnit,
                hitDelaySec = unitData.hitDelaySec,
            });
            _em.AddComponentData(entity, new Wassup.Battle.Combat.DefenderCcData
            {
                knockbackDistance   = unitData.knockbackDistance,
                knockbackDuration   = unitData.knockbackDuration,
                sleepOnHitSec       = unitData.sleepOnHitSec,
                knockupOnHitSec     = unitData.knockupOnHitSec,
                knockupVisualHeight = unitData.knockupVisualHeight,
            });

            // 적 AI 스택을 그대로 물려받는다 — EnemyAiStateSystem 은 FactionTag 를 안 보고
            // AttackState.targetMask 로만 타겟을 찾는다(faction-agnostic). Halt = 사거리에
            // 적이 들면 정지하고 공격.
            _em.AddComponentData(entity, new Wassup.Battle.Combat.EnemyAiState { value = Wassup.Battle.Combat.AiState.Marching });
            _em.AddComponentData(entity, new Wassup.Battle.Combat.EnemyBehavior
            {
                targetMode = Wassup.Data.EnemyTargetMode.Nearest,
                engageMovement = Wassup.Data.EngageMovement.Halt,
            });
            // continuous-agent-movement unit 3 — 순찰병도 같은 원형 충돌을 쓴다.
            // radius 누락 시 조용히 구 점 충돌로 돌아가 이 아키타입만 코너에서 걸린다(ecs-review HIGH).
            _em.AddComponentData(entity, new Wassup.Battle.Movement.PathFollowState
            {
                speed = unitData.moveSpeed,
                radius = agentRadiusTiles * tileSize,
            });
            _em.AddComponentData(entity, new Wassup.Battle.Movement.PatrolAnchor
            {
                cell = anchorCell,
                tileRadius = math.max(0, tileRadius),
            });
            _em.AddComponentData(entity, new Wassup.Battle.Effects.PatrolStep { dir = float2.zero });
            if (owner != Entity.Null)
                _em.AddComponentData(entity, new SummonedBy { owner = owner });

            if (unitData.outputs != null && unitData.outputs.Length > 0)
            {
                var outputBuf = _em.AddBuffer<Wassup.Battle.Combat.AttackOutputElement>(entity);
                foreach (var output in unitData.outputs)
                    outputBuf.Add(new Wassup.Battle.Combat.AttackOutputElement { value = output });
            }

            _em.AddComponentData(entity, new Wassup.Battle.Effects.ModifierStats
            {
                damageMul      = 1f,
                attackSpeedMul = 1f,
                dmgTakenMul    = 1f,
                regenPerSec    = 0f,
                moveSpeedMul   = 1f,
                damageVsCcMul  = 1f,
                maxHealthMul   = 1f,
            });
            _em.AddComponent<Wassup.Battle.Effects.ModifierStatsDirty>(entity);
            _em.SetComponentEnabled<Wassup.Battle.Effects.ModifierStatsDirty>(entity, false);
            _em.AddBuffer<Wassup.Battle.Units.IncomingHeal>(entity);
            _em.AddBuffer<Wassup.Battle.Units.ShieldSlot>(entity);
            _em.AddBuffer<Wassup.Battle.Units.IncomingShield>(entity);

            // 계약 11 — ApplyActiveDcEffectsTo 를 호출하지 않는다(드림캐쳐/시너지 비적용).

            // unit 5 — 소환 순간 VFX. 전용 아트가 생기기 전까지 배치 링을 재사용한다
            // (순찰병이 "지금 나왔다"를 읽히게 하는 게 목적, 룩은 unit 7 에서 교체).
            if (vfxSpawner != null)
                vfxSpawner.SpawnPlacementRing(new Vector3(pos.x, pos.y, pos.z));

            bool spineSpawned = false;
            if (spineUnitPool != null)
            {
                var spineWorld = new Vector3(pos.x, pos.y + spineDefenderYOffset, pos.z);
                spineSpawned = spineUnitPool.TrySpawn(unitData, unitData, entity, spineWorld, "SpinePat", out var patrolView);
                // unit 6 — 아군 식별 표식. 이 게임에서 움직이는 건 지금까지 전부 적이었다.
                if (spineSpawned && patrolView != null)
                    Wassup.Presentation.AllyMarkerDecal.Attach(patrolView.transform);
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
                    unitData.displayName, entity, fallbackWorld, mesh, material, CharacterVisualScale, out _);
            }

            return entity;
        }

        // summon-patrol-defender unit 2 — 디버그 스폰 공개 API (DebugSpawnHazardAt 동형).
        // 호출자가 준 셀을 walk 셀로 스냅한다. 스냅 실패(맵 미생성/walk 셀 없음) = Entity.Null.
        // summon-patrol-defender — 거점 스냅 + **거리 상한**.
        //
        // TryGetNearestWalkCell 은 전 그리드를 스캔해 **전역 최근접** walk 타일을 고른다 —
        // 반경 제한이 없어서 false 는 "맵에 walk 타일이 0개"일 때만 나온다. 그대로 쓰면
        // "스냅 실패 = 소환 취소"가 사실상 도달 불가한 분기가 되고(완료 기준이 검증 불가해진다),
        // 실제로 벌어지는 일은 그 반대다: 경로에서 먼 Place 타일에 소환사를 놓으면 거점이
        // 화면 저쪽 walk 타일로 날아가 순찰병이 소환사와 무관한 자리에 나온다.
        //
        // 상한 = leash 반경 자체(SO 값, 제약 6) — "순찰병의 집은 소환사가 주장하는 구역
        // 안에 있어야 한다". radius 0 은 Place 타일만 남아 항상 실패하므로 최소 1로 본다.
        private bool TryGetPatrolAnchorCell(int2 ownerCell, int tileRadius, out int2 anchorCell)
        {
            if (!TryGetNearestWalkCell(ownerCell, out anchorCell)) return false;
            return GridMath.ChebyshevDistance(anchorCell, ownerCell) <= math.max(1, tileRadius);
        }

        public Entity DebugSpawnPatrolAt(DefenderUnitData unitData, int2 cell, int tileRadius)
        {
            if (!TryGetPatrolAnchorCell(cell, tileRadius, out var walkCell)) return Entity.Null;
            return CreatePatrolEntity(unitData, walkCell, tileRadius, Entity.Null);
        }

        // summon-patrol-defender unit 2 — 디버그 스폰의 거점 기준 셀.
        //
        // 마우스 커서를 쓰지 않는다: 메뉴 항목을 클릭하는 순간 커서는 메뉴 위에 있어
        // 게임 뷰 좌표가 아니다(기존 HazardDebugMenu 의 커서 레이는 이 이유로 사실상
        // 폴백 셀만 쓴다). 대신 **배치된 방어유닛**을 기준으로 삼는다 — 실제 소환에서
        // 거점이 소환사 셀이므로 테스트가 진짜 경로를 그대로 흉내 낸다.
        //
        // 여러 기가 배치돼 있으면 (y, x) 오름차순 최솟값 하나를 고른다(Dictionary 열거
        // 순서는 보장이 없으므로 결정론을 위해 명시 정렬). 하나도 없으면 보드 중심.
        // fromDefender = 어느 쪽이 쓰였는지(호출자 로그용).
        public bool DebugTryGetPatrolAnchorCell(out int2 cell, out bool fromDefender)
        {
            cell = default;
            fromDefender = false;

            bool found = false;
            foreach (var kv in _defenderByTile)
            {
                var c = new int2(kv.Key.x, kv.Key.y);
                if (!found || c.y < cell.y || (c.y == cell.y && c.x < cell.x))
                {
                    cell = c;
                    found = true;
                }
            }
            if (found)
            {
                fromDefender = true;
                return true;
            }

            if (!_generatedMap.IsCreated) return false;
            cell = new int2(_generatedMap.gridSize.x / 2, _generatedMap.gridSize.y / 2);
            return true;
        }

        // summon-patrol-defender unit 3 — 순찰병 SO 인덱스 등록 (RegisterZoneHazardSO 동형).
        private int RegisterPatrolUnitSO(DefenderUnitData so)
        {
            if (so == null) return -1;
            if (_patrolUnitIndex.TryGetValue(so, out int idx)) return idx;
            idx = _patrolUnitRegistry.Count;
            _patrolUnitRegistry.Add(so);
            _patrolUnitIndex[so] = idx;
            return idx;
        }

        // summon-patrol-defender unit 3 — 소환 요청 캐리어 드레인.
        //
        // walk 셀 스냅을 여기서 한다: TryGetNearestWalkCell 이 GeneratedMap 을 보는 Mono 측
        // API 라 심에서 못 부른다. 스냅 실패 = 소환 취소(요청만 폐기, 에러 아님) — 주변에
        // walk 타일이 없는 자리에 소환사를 놓을 수 있기 때문이다.
        private void DrainPatrolSpawnRequests()
        {
            if (!HasLiveEntityManager()) return;

            using var query = _em.CreateEntityQuery(
                ComponentType.ReadOnly<Wassup.Battle.Combat.PatrolSpawnRequest>(),
                ComponentType.ReadOnly<Wassup.Battle.Combat.PatrolRequestCarrier>());
            if (query.IsEmpty) return;

            using var carriers = query.ToEntityArray(Unity.Collections.Allocator.Temp);
            for (int i = 0; i < carriers.Length; i++)
            {
                var carrier = carriers[i];
                var req = _em.GetComponentData<Wassup.Battle.Combat.PatrolSpawnRequest>(carrier);
                _em.DestroyEntity(carrier);

                if (!_em.Exists(req.owner)) continue;
                if (req.patrolDataIndex < 0 || req.patrolDataIndex >= _patrolUnitRegistry.Count)
                {
                    Debug.LogWarning($"[Summon] Invalid patrol unit index {req.patrolDataIndex}; dropping.");
                    continue;
                }
                var so = _patrolUnitRegistry[req.patrolDataIndex];
                if (so == null) continue;

                if (!TryGetPatrolAnchorCell(req.ownerCell, req.leashTileRadius, out var anchorCell)) continue;

                var patrol = CreatePatrolEntity(so, anchorCell, req.leashTileRadius, req.owner);
                if (patrol == Entity.Null) continue;

                if (_em.HasComponent<Wassup.Battle.Combat.SummonerState>(req.owner))
                {
                    var state = _em.GetComponentData<Wassup.Battle.Combat.SummonerState>(req.owner);
                    state.current = patrol;
                    // 초회 게이트는 **실제로 생성된 시점**에 소비한다. stage 시점에 켜면
                    // 위 스냅 실패(continue)로 소환이 취소된 경우에도 게이트가 닳아,
                    // 이후 적 없이도 소환되는 상태로 넘어간다.
                    state.hasSummonedOnce = true;
                    _em.SetComponentData(req.owner, state);
                }
            }
        }

        // effect-tiles unit 2 — 효과 타일 modifier 슬롯 네임스페이스.
        // 규약: on-place=0 · 시너지=1 · 효과타일=2 · **스킬 아군 버프=3** · 드림캐쳐=100+
        // (EnqueueSynergyMul / AllyBuffField.StackId / _dcStackCounter 참조).
        private const ushort EffectTileStackId = 2;

        // 스킬 아군 버프 슬롯(=3)은 active-ally-zone unit 0 에서 `AllyBuffField.StackId` 로 이전됐다 —
        // 적용 주체가 Effects 시스템이라 상수도 그쪽이 소유한다. 슬롯 규약은 위 주석 참조.

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

        // goal-stability unit 4 — 붕괴 드레인. 게임 상태 갱신 없음: 유출 전환은 골 엔티티
        // 부재(공성 게이트)가 이미 담당한다. unit 5 — 게이지 제거 + 붕괴 원샷 VFX 소비.
        private void DrainGoalCollapsedEvents()
        {
            if (!_goalCollapsedQueue.IsCreated) return;
            while (_goalCollapsedQueue.TryDequeue(out var evt))
            {
                Debug.Log($"[BattleBridge] Goal collapsed — cell=({evt.cell.x},{evt.cell.y}) index={evt.goalIndex} → 유출 지점 전환");
                var cell = new Vector2Int(evt.cell.x, evt.cell.y);
                tileHealthGaugeLayer?.Hide(cell);
                for (int i = _structureRegistry.Count - 1; i >= 0; i--)
                    if (_structureRegistry[i].cell == cell) _structureRegistry.RemoveAt(i);
                vfxSpawner?.SpawnGoalCollapse(new Vector3(evt.worldPosition.x, evt.worldPosition.y, evt.worldPosition.z));
            }
        }

        // goal-stability unit 5 — 골 안정도 게이지를 유닛 체력바와 동일한 오버헤드 UI 로
        // (사용자 결정 2026-08-04 "체력바는 유닛처럼 띄워"). Health read-only 폴링(큐 아님).
        // 골은 뷰 풀에 없어 TryGetUnitScreenAnchor 를 못 쓴다 — 셀 중심 + 구조물 높이를 직접
        // 투영해 같은 (anchor, tileScreenWidth) 계약으로 SetUnit. 붕괴 시 숨김은 EndFrame 의
        // 미표시-자동-Hide 가 처리(별도 코드 0). Legacy 모드는 방어유닛 이원화와 동형으로
        // 타일 게이지 폴백.
        private void SyncGoalOverheadGauges(bool unifiedOverhead)
        {
            if (_structureRegistry.Count == 0 || !HasLiveEntityManager()) return;
            var cam = Camera.main;
            for (int i = 0; i < _structureRegistry.Count; i++)
            {
                var (entity, cell, faction) = _structureRegistry[i];
                if (!_em.Exists(entity) || !_em.HasComponent<Health>(entity)) continue; // 붕괴 정리는 EndFrame/드레인
                var h = _em.GetComponentData<Health>(entity);
                float ratio = Health.ComputeRatio(h.value, h.max);
                var world = GridToWorldCenter(cell);
                var baseView = (Vector3)Wassup.Core.BoardSpace.ToView(new Vector3(world.x, 0f, world.z));
                if (unifiedOverhead && unitOverheadUiLayer != null && cam != null)
                {
                    Vector3 baseScreen = cam.WorldToScreenPoint(baseView);
                    Vector3 topScreen = cam.WorldToScreenPoint(baseView + Vector3.up * goalOverheadHeight);
                    var anchor = new Vector2(baseScreen.x, topScreen.y);
                    Vector3 a = cam.WorldToScreenPoint(baseView - Vector3.right * (tileSize * 0.5f));
                    Vector3 b = cam.WorldToScreenPoint(baseView + Vector3.right * (tileSize * 0.5f));
                    float tileScreenWidth = Vector2.Distance(new Vector2(a.x, a.y), new Vector2(b.x, b.y));
                    // 리뷰 M-6 — 등록부에 이제 적 거점도 들어온다. defender 색 플래그는 진영에서.
                    unitOverheadUiLayer.SetUnit(entity,
                        ((int)faction & Wassup.Battle.Units.Factions.AnyDefender) != 0,
                        ratio, anchor, tileScreenWidth, 0f,
                        GatherOverheadStacks(entity));
                }
                else if (!unifiedOverhead && tileHealthGaugeLayer != null)
                {
                    tileHealthGaugeLayer.Set(cell, baseView, tileSize, ratio);
                }
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
            int2 grid = _generatedMap.IsCreated ? _generatedMap.gridSize : FallbackGridSize;
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
            DestroyEntitiesByType<Wassup.Battle.Effects.ClockOutGimmickConfig>();
            DestroyEntitiesByType<Wassup.Battle.Effects.OnsenGimmickConfig>();

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
            else if (_assignedGimmick is Wassup.Data.ClockOutGimmickData cd)
            {
                Debug.Log($"[GimmickConfig] ClockOutGimmickConfig 주입 (gimmick={cd.gimmickId})");
                var e = _em.CreateEntity();
                _em.AddComponentData(e, new Wassup.Battle.Effects.ClockOutGimmickConfig
                {
                    resignationThreshold = cd.resignationThreshold,
                    meteorCount          = cd.meteorCount,
                    meteorDamage         = cd.meteorDamage,
                    meteorTileRange      = cd.meteorTileRange,
                    meteorWarningSec     = cd.meteorWarningSec,
                    meteorStaggerSec     = cd.meteorStaggerSec,
                });
                // unit 4 — 메테오 셀 선택 rng seed(매치당·matchSeed 파생 → 결정론). 요청은 config
                // 주입 이후에만 발생하므로 seed 선행 보장.
                _meteorRng = new Unity.Mathematics.Random((uint)Wassup.Core.MatchSeed.DeriveMeteorSeed(_matchSeed));
            }
            else if (_assignedGimmick is Wassup.Data.OnsenGimmickData od)
            {
                Debug.Log($"[GimmickConfig] OnsenGimmickConfig 주입 (gimmick={od.gimmickId})");
                var e = _em.CreateEntity();
                _em.AddComponentData(e, new Wassup.Battle.Effects.OnsenGimmickConfig
                {
                    heatInterval  = od.heatInterval,
                    flipThreshold = od.flipThreshold,
                    healPercent   = od.healPercent,
                    lossPercent   = od.lossPercent,
                    heatMaxStack  = od.heatMaxStack,
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
            // boss-wave-cadence unit 2 — 보스 판별의 단일 진실 지점. 여기서만 경보를 구동해
            // SpawnUnit 재판정(로직 이중화·이중 발화)을 피한다. 재진입 코얼레스는 뷰가 담당.
            _bossWarning?.Show();
            // 위협 테이블은 보스와 항상 동행 — 텔레포트 arm 의 타겟 소스.
            // defender 히트가 쌓기 전까지 빈 버퍼(ThreatHitEvent 드레인이 채움).
            _em.AddBuffer<ThreatEntry>(entity);

            // projectile-emission-pattern unit 3 — 패턴 버퍼는 **slots 획득 전에** 붙인다.
            // AddBuffer 는 구조 변경이라 이미 잡아둔 DynamicBuffer 핸들을 무효화한다 —
            // 루프 안에서 붙이면 아래 slots 가 죽는다. 패턴 mechanic 이 없으면 부착하지
            // 않아 기존 유닛(카드만 쓰는 defender 포함)의 chunk 비용은 0 이다.
            bool wantsPattern = false;
            for (int i = 0; i < mechanics.Length; i++)
                if (mechanics[i].payload.kind == Wassup.Data.DcPayloadKind.EmitProjectilePattern)
                { wantsPattern = true; break; }
            if (wantsPattern)
            {
                _em.AddBuffer<Wassup.Battle.Combat.Projectile.Emission.PatternSlot>(entity);
                // 발사 인스턴스 버퍼도 미리(런타임 구조 변경 회피 — IncomingHeal 선례).
                _em.AddBuffer<Wassup.Battle.Combat.Projectile.Emission.EmitterInstance>(entity);
            }
            // ⚠ 여기서 PatternSlot 핸들을 캐시하지 않는다. 위 AddBuffer 2회 + 아래
            // AddBuffer<DcTriggerSlot> 이 전부 구조 변경이라, 먼저 잡은 핸들은 마지막
            // AddBuffer 시점에 죽는다(ObjectDisposedException / 회수된 chunk write).
            // 사용 직전 GetBuffer 로 다시 얻는다 — 그 사이 구조 변경이 없으므로 유효하다.

            // slots 는 **마지막** AddBuffer 라 아래 루프까지 캐시해도 안전하다: 루프 안의
            // 쓰기는 DynamicBuffer.Add(리사이즈, archetype 불변)와 managed 조작뿐이다.
            // 루프에 AddComponent/AddBuffer 를 하나라도 추가하는 순간 이 전제가 깨지므로,
            // 그때는 이 핸들도 사용 직전 재획득으로 바꿔야 한다.
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
                    // struct default 0 은 유효 index 라 미배선 슬롯이 0번 패턴을 쏘게
                    // 된다 — 명시 -1 초기화가 계약이다(unit 3).
                    patternIndex = -1,
                    // boss-jjangssen unit 7 — SelfBlink 착지 슬램(0 = 이동만).
                    slamDamage = math.max(0f, m.payload.slamDamage),
                    slamTileRange = math.max(0, m.payload.slamTileRange),
                };
                if (m.payload.kind == Wassup.Data.DcPayloadKind.AreaBarrage)
                {
                    // projectile-emission-pattern unit 4 — AreaBarrage arm 은 제거됐다
                    // (융단폭격은 EmitProjectilePattern + Pattern_* asset 으로 이관).
                    // enum 값은 append-only 계약상 남아 있으므로, 옛 authoring 이
                    // 조용한 no-op 으로 죽는 대신 여기서 거절 사유를 남긴다.
                    Debug.LogWarning($"[BattleBridge] {unitType.displayName} nightmare mechanic {i}: AreaBarrage 는 EmitProjectilePattern 으로 이관됐다(arm 제거) — skipped. 패턴 asset 을 지정하라.");
                    continue;
                }
                else if (m.payload.kind == Wassup.Data.DcPayloadKind.EmitProjectilePattern)
                {
                    // projectile-emission-pattern unit 3 — 발사 명세 bake. SO 해석은
                    // 브리지가 유일 seam 이므로 spec 변환과 template 조립이 여기서 끝난다.
                    var pattern = m.payload.pattern;
                    if (pattern == null || pattern.barrel == null)
                    {
                        Debug.LogWarning($"[BattleBridge] {unitType.displayName} nightmare mechanic {i}: EmitProjectilePattern needs a pattern with a barrel — skipped.");
                        continue;
                    }
                    if (!_em.HasBuffer<Wassup.Battle.Combat.Projectile.Emission.PatternSlot>(entity))
                    {
                        // 사전 스캔과 어긋난 경우(도달 불가) — 조용한 오발사보다 경고.
                        Debug.LogWarning($"[BattleBridge] {unitType.displayName} nightmare mechanic {i}: pattern buffer missing — skipped.");
                        continue;
                    }
                    int barrelIndex = GetOrCreateProjectileDataIndex(pattern.barrel);
                    // SkyFall 패턴은 낙하 예고가 곧 그 스킬의 정체다 — 0 이면 텔레그래프
                    // 없이 즉착탄하므로 조용히 넘기지 않는다(구 arm 은 authoring 이 duration 을
                    // 요구했다).
                    if (pattern.barrel.flightMode == Wassup.Data.ProjectileFlightMode.SkyFall
                        && pattern.telegraphSec <= 0f)
                    {
                        Debug.LogWarning($"[BattleBridge] {unitType.displayName} nightmare mechanic {i}: SkyFall 패턴의 telegraphSec 가 0 — 예고 없이 즉착탄합니다.");
                    }
                    // (BezierHoming 재조준 봉인은 authoring 표면이 없어 경고가 불필요하다 —
                    //  ProjectileData 에 재조준 필드 자체가 없다. 그 필드를 여는 후속 작업이
                    //  재조준 개통과 한 묶음이라는 점은 README 후속 후보에 적혀 있다.)
                    if (!pattern.TryToSpec(barrelIndex, out var patternSpec))
                    {
                        int shotCount = pattern.shots?.Length ?? 0;
                        Debug.LogWarning(
                            $"[BattleBridge] {unitType.displayName} nightmare mechanic {i}: " +
                            $"invalid projectile shot sequence/binding contract (shots={shotCount}, " +
                            $"capacity={Wassup.Data.ProjectilePatternData.MaxShotCount}, " +
                            $"angles={pattern.minAngleDeg}..{pattern.maxAngleDeg}, " +
                            $"selection={pattern.selection}, flight={pattern.barrel.flightMode}) — skipped.");
                        continue;
                    }
                    // 사용 직전 재획득(위 주석 참조).
                    var patternSlots = _em.GetBuffer<Wassup.Battle.Combat.Projectile.Emission.PatternSlot>(entity);
                    patternSlots.Add(new Wassup.Battle.Combat.Projectile.Emission.PatternSlot
                    {
                        spec = patternSpec,
                        template = BuildPatternTemplate(pattern, barrelIndex, entity, hostIsEnemy: true),
                        fireCountBase = 0,
                    });
                    slot.patternIndex = patternSlots.Length - 1;
                }
                else if ((m.payload.kind == Wassup.Data.DcPayloadKind.SelfBlink ||
                          m.payload.kind == Wassup.Data.DcPayloadKind.AllyMoveSpeedAura ||
                          m.payload.kind == Wassup.Data.DcPayloadKind.SelfTileAoe ||
                          // ultimate-leap unit 0 — 착지 슬램도 ProjectileSpawnRequest 로 나가므로
                          // SelfTileAoe 와 같은 이유로 dataIndex 가 필수다(아래 loud 거절 참조).
                          m.payload.kind == Wassup.Data.DcPayloadKind.UltimateLeap) &&
                         m.payload.projectile != null)
                {
                    // rev 3 (실플레이 피드백) — blink 연출: hitPrefab 만 소비하는
                    // 퍼프 ProjectileData(투사체로는 안 뜀). null 이면 무연출.
                    // nightmare-whip-aura unit 3 — whip 펄스 연출도 같은 경로.
                    // boss-jjangssen unit 2 — SelfTileAoe(진동갑주)도 이 경로가 필요하다.
                    // blink/aura 는 연출만 잃지만 SelfTileAoe 는 **폭발 자체가 사라진다**:
                    // 폭발이 ProjectileSpawnRequest 하나로 표현되고 드레인이 dataIndex<0 이면
                    // 요청을 통째로 버리기 때문에 데미지까지 안 나간다.
                    slot.projectileDataIndex = GetOrCreateProjectileDataIndex(m.payload.projectile);
                }
                else if (m.payload.kind == Wassup.Data.DcPayloadKind.SelfTileAoe)
                {
                    // boss-jjangssen unit 2 — 위 분기에 안 걸렸다 = projectile 미지정.
                    // 조용히 inert 가 되면 "왜 폭발이 없는지" 를 영영 알 수 없으므로 loud 하게
                    // 거절한다(bake 의 기존 loud 거절 선례와 동일 표현). defender 슬롯 경로도
                    // 같은 규칙을 쓴다.
                    Debug.LogWarning($"[BattleBridge] {unitType.displayName} nightmare mechanic {i}: SelfTileAoe 에 ProjectileData(AOE view) 가 없어 폭발 요청이 드롭된다 — skipped. payload.projectile 을 지정하라.");
                    continue;
                }
                else if (m.payload.kind == Wassup.Data.DcPayloadKind.UltimateLeap)
                {
                    // ultimate-leap unit 0 — SelfTileAoe 와 같은 함정: 착지 슬램이
                    // ProjectileSpawnRequest 하나로 표현되고 드레인이 dataIndex<0 이면 요청을
                    // 통째로 버린다 → **연출뿐 아니라 피해까지 사라진다.** 조용히 "이탈만 하고
                    // 아무 일도 안 일어나는" 궁극기가 되므로 loud 하게 거절한다.
                    Debug.LogWarning($"[BattleBridge] {unitType.displayName} nightmare mechanic {i}: UltimateLeap 에 ProjectileData(착지 슬램 view) 가 없어 슬램 요청이 드롭된다 — skipped. payload.projectile 을 지정하라.");
                    continue;
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

        // projectile-shot-sequence unit 2 — 방향 능력의 schedule을 공용 pattern
        // buffer로 굽는다. EntityManager/Unity SO가 실제로 필요한 Bridge seam이므로
        // architecture-neutral 계산과 섞지 않는다.
        private void BakeDefenderDirectionalPattern(Entity entity, DefenderUnitData unitData, int barrelDataIndex)
        {
            var volleyAbility = unitData.GetAbility<DirectionalVolleyAbility>();
            if (volleyAbility == null) return;

            var pattern = volleyAbility.pattern;
            if (pattern == null || pattern.barrel == null)
            {
                Debug.LogWarning(
                    $"[BattleBridge] {unitData.displayName}: DirectionalVolleyAbility needs a pattern with a barrel — pattern skipped.");
                return;
            }
            if (pattern.barrel != unitData.projectile)
            {
                Debug.LogWarning(
                    $"[BattleBridge] {unitData.displayName}: directional pattern barrel must match defender projectile — pattern skipped.");
                return;
            }
            if (!pattern.TryToSpec(barrelDataIndex, out var patternSpec))
            {
                int shotCount = pattern.shots?.Length ?? 0;
                Debug.LogWarning(
                    $"[BattleBridge] {unitData.displayName}: invalid directional projectile shot sequence/binding " +
                    $"(shots={shotCount}, capacity={Wassup.Data.ProjectilePatternData.MaxShotCount}, " +
                    $"angles={pattern.minAngleDeg}..{pattern.maxAngleDeg}, selection={pattern.selection}, " +
                    $"flight={pattern.barrel.flightMode}) — pattern skipped.");
                return;
            }

            // 두 buffer 모두 스폰 때 사전 부착해 AttackSystem은 RESOLVE마다
            // instance를 Add하기만 하고 구조 변경하지 않는다.
            _em.AddBuffer<Wassup.Battle.Combat.Projectile.Emission.PatternSlot>(entity);
            _em.AddBuffer<Wassup.Battle.Combat.Projectile.Emission.EmitterInstance>(entity);
            // 위 AddBuffer 두 번은 구조 변경이다. 마지막 변경 뒤 핸들을 얻는다
            // (boss pattern bake의 dangling-buffer 회귀 가드와 같은 규칙).
            var patternSlots =
                _em.GetBuffer<Wassup.Battle.Combat.Projectile.Emission.PatternSlot>(entity);
            patternSlots.Add(new Wassup.Battle.Combat.Projectile.Emission.PatternSlot
            {
                spec = patternSpec,
                template = BuildPatternTemplate(pattern, barrelDataIndex, entity, hostIsEnemy: false),
                fireCountBase = 0,
            });
        }

        // projectile-emission-pattern unit 3 — 발사 요청 원본 조립. 향후 defender/카드
        // 경로가 같은 함수를 호출하도록 bake 밖으로 분리해 둔다(트리거 소스 확장의 실비용
        // 3요건 중 ②를 선불). 타겟 의존 필드(target/impact/swingIndex)는 비운 채 남기고
        // emitter 가 발마다 채운다. **드레인이 SO 에서 직접 읽는 값은 싣지 않는다** —
        // dropHeight(기존), 베지어 lateral/forwardBias(unit 1).
        private ProjectileSpawnRequest BuildPatternTemplate(
            Wassup.Data.ProjectilePatternData pattern, int barrelDataIndex, Entity owner, bool hostIsEnemy)
        {
            var barrel = pattern.barrel;
            var axes = ResolveProjectileAxes(barrel.flightMode);
            return new ProjectileSpawnRequest
            {
                movement = axes.movement,
                payload = axes.payload,
                // 기존 발사 지점들이 barrel SO 를 읽어 request 를 채우는 목록과 동일하게
                // 유지한다(새 컨벤션을 만들지 않는다).
                speed = barrel.speed,
                hitThreshold = barrel.hitThreshold,
                visualScale = barrel.visualScale,
                // SkyFall 은 arcHeight 슬롯을 "낙하 시작 높이"로 재사용하고, 드레인이
                // req.arcHeight > 0 이면 그 값을, 아니면 dropHeight 를 쓴다. barrel 의
                // arcHeight 기본값은 2 라 그대로 실으면 dropHeight(6~9)를 침묵 오버라이드해
                // 낙하가 뚝 떨어진다 — 구 barrage arm 은 이 필드를 아예 안 실었다.
                arcHeight = axes.movement == MovementKind.SkyFall ? 0f : barrel.arcHeight,
                impactTileRange = barrel.impactTileRange,
                onHitEffect = barrel.onHitEffect,
                splashRadius = barrel.splashRadius,
                splashDamageMul = barrel.splashDamageMul,
                dataIndex = barrelDataIndex,
                owner = owner,
                // 진영은 host 에서 도출한다(계약 7) — 패턴 SO 에 faction 필드 없음.
                targetFaction = hostIsEnemy
                    ? ProjectileTargetFaction.Defender
                    : ProjectileTargetFaction.Enemy,
            };
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

            // spawn-point-alert unit 0 — lane 산식은 WavePatternGenerator 로 이관(예보와 공유).
            int spawnIndex = WavePatternGenerator.EffectiveSpawnIndex(entry.spawnIndex, pending.deckIndex, _generatedMap.spawns.Length);
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
            // dreamcatcher-orb-dock unit 6 — 스폰 시 적 데이터 등록(킬 각성 피규어 스킨 소스).
            _enemyTypeByEntity[entity] = entry.unitType;
            _em.AddComponentData(entity, new Health { value = entry.unitType.health, max = entry.unitType.health });
            _em.AddComponentData(entity, new FactionTag { value = Faction.EnemyUnit });
            // dreamcatcher-awakening-hand unit 1 — bake the death grant so
            // DamageApplicationSystem can stamp it into EnemyKilledEvent.
            // Unconditional attach (0 allowed) keeps the lookup branch-free.
            _em.AddComponentData(entity, new AwakeningReward
            {
                value = Mathf.Max(0, entry.unitType.awakeningReward),
            });
            // battle-score-formula unit 2 — bake the kill score so
            // DamageApplicationSystem can stamp it into EnemyKilledEvent.
            // Unconditional attach (0 allowed) keeps the lookup branch-free.
            _em.AddComponentData(entity, new KillScore
            {
                value = Mathf.Max(0, entry.unitType.killScore),
            });
            // Pre-attach empty buffers so downstream systems never need structural AddBuffer on hot paths.
            _em.AddBuffer<IncomingDamage>(entity);
            _em.AddBuffer<CcEffect>(entity);
            _em.AddBuffer<DotEffect>(entity); // dot-effect-extraction unit 0

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
            // battle-structures unit 1 — 저작 타겟 마스크를 한 번 푼다. 아래 두 곳이 **같은
            // 값**을 써야 한다: AttackState.targetMask(런타임 초기값)와
            // EnemyTargetFilter.factionMask(저작 의도, 불변). 갈리면 도발 게이트(unit 2)가
            // 실제 조준과 다른 의도를 읽는다.
            // 미저작(None=0)은 레거시 마스크로 폴백 — 저작자가 인스펙터에서 마스크를 비웠을
            // 때 그 적이 조용히 무장 해제되는 것을 막는다.
            int authoredTargetMask = Wassup.Battle.Combat.EnemyTargetDefaults.Resolve(
                (int)entry.unitType.targetFactions);

            if (wantsAttack)
            {
                _em.AddComponentData(entity, new AttackState
                {
                    range = entry.unitType.attackRange,
                    cooldownDuration = entry.unitType.attackCooldown,
                    cooldownRemaining = 0f,
                    attackTargetCount = Mathf.Max(1, entry.unitType.attackTargetCount),
                    targetMask = authoredTargetMask,
                    hitDelaySec = entry.unitType.hitDelaySec,
                });
                var outputBuf = _em.AddBuffer<Wassup.Battle.Combat.AttackOutputElement>(entity);
                foreach (var output in entry.unitType.outputs)
                    outputBuf.Add(new Wassup.Battle.Combat.AttackOutputElement { value = output });

                if (attackMethod == Wassup.Data.EnemyAttackMethod.Projectile && entry.unitType.projectile != null)
                    BakeProjectileRef(entity, entry.unitType.projectile);   // 리뷰 A-M3 — 단일 베이크
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

            // battle-structures unit 0 — goal-stability 의 walk-only 골 공격 grant 를 제거했다.
            // 게이트가 _hasStabilityGoals(= SpawnGoalEntities 산물)라 전 맵 M=0 에서 한 번도
            // 발화하지 않았다. 라이브 타워로 재게이팅하면 Runner·Swift 가 AttackState 를 얻어
            // canSiege=true 가 되고 골에서 파괴되지 않아 «필드에 적 0기» 판정을 막는다 —
            // 그건 행동 변화이자 회귀다. «거점 전담 적» 저작은 unit 1 의
            // EnemyTargetFilter.factionMask 가 제자리다(계약 2).

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
            // 이 부착은 wantsAttack 게이트 **밖**이다 — 무기 없는 적(러너·스위프트)도 저작
            // 의도를 갖는다. 계약 2 의 도발 게이트가 이것을 읽는다.
            _em.AddComponentData(entity, new Wassup.Battle.Combat.EnemyTargetFilter
            {
                classMask = (int)entry.unitType.targetClassMask,
                priorityClass = priorityClass,
                factionMask = authoredTargetMask,
            });

            _em.AddComponentData(entity, new PathFollowState
            {
                speed = entry.unitType.moveSpeed,
                // continuous-agent-movement unit 3 — 반지름은 월드 단위로 넘긴다(sim 은 타일을 모른다).
                radius = agentRadiusTiles * tileSize,
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
