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
        // bonus-wave-pull unit 4 — 보너스 당기기의 모든 수치(적·마리수·타임라인·트리거 임계).
        // 미할당 = 이 판에 보너스 당기기가 없다(버튼이 안 뜬다) — 에러가 아니다.
        [SerializeField] private Wassup.Data.BonusWaveData bonusWaveData;
        [Header("Map Grid")]
        // map-diorama-stage unit 2 — (스테이지 프리팹, 덱, 플랜) 인코운터 풀. 맵 생산의 유일 경로.
        // 인덱스 선정 의미는 MapDocumentPool 시절과 동일(맵마다 그 맵의 적 패턴, dev 슬롯 불가시).
        [SerializeField] private MapStagePool mapPool;
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
        // gift-phase-removal unit 1 — 재시작도 매치 인트로(기믹 리빌)를 거친다. 배선되면
        // BeginIntro() 로, 없으면 곧장 배치로 폴백.
        [SerializeField] private Wassup.UI.GimmickPhaseView _gimmickPhaseView;
        [SerializeField] private Wassup.Presentation.SpineUnitPool spineUnitPool;
        // defender-clock-out unit 3 — 퇴근 이탈 연출. 미배선이면 즉시 반납으로 폴백한다
        // (연출은 게임 규칙을 하나도 소유하지 않는다 — 없어도 퇴근은 그대로 성립).
        [SerializeField] private Wassup.UI.DefenderRetireFlight retireFlight;
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
        [SerializeField] private Wassup.Core.TilemapMapView tilemapMapView;
        // three-minute-survival unit 1 — 안정도 바를 골 앵커에서 월드 Y 로 띄우는 양.
        // 구조물 메쉬가 셀 중심보다 높아서 바가 메쉬를 파고드는 것을 막는다. 씬 배선 불요
        // (신규 SerializeField 는 기존 씬에서 이 initializer 를 받는다).
        [SerializeField] private Wassup.Data.TileSetData tileSet;
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
        // tilted-billboard unit 7 — **평면 상대** 리프트다(구 blobShadowGroundY = 절대 월드 Y).
        // 절대 Y 는 스테이지마다 다른 발바닥 평면(MapStage.gridOriginLocal.y: 0 / 0.19 / 0.87)을
        // 몰라서 StreetDay 에서 0.65 만큼 바닥 아래로 파묻혔다. 평면은 BoardSpace 가 소유한다.
        [Tooltip("블롭을 보드 평면에서 띄우는 양(월드). 발 평면에서 ~5px(@1080) = 접지점 가독 + z-fight 회피.")]
        [SerializeField] private float blobShadowLift = 0.026f;
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
        // bonus-wave-pull unit 4 — 전멸 판정 전용(보너스 적 제외). 위 쿼리와 수명을 공유한다
        // (_aliveAttackersQueryCreated 하나가 둘 다를 게이팅).
        private EntityQuery _aliveNormalAttackersQuery;
        private bool _aliveAttackersQueryCreated;

        // ★**둘은 항상 함께 만든다.** 플래그 하나가 둘을 게이팅하므로 한쪽만 되살리면
        // 나머지가 stale 인 채로 «생성됨» 으로 읽힌다. 실제로 stale 복구 경로(SyncMonoUnitViews 의
        // NullReferenceException catch)가 예전엔 한 개만 다시 만들었고, 그러면 그 뒤의
        // NoQueuedAttackersRemain() 이 try 없이 죽은 쿼리를 쳐서 **그 판의 웨이브 진행이 멎는다**.
        private void CreateAliveAttackerQueries()
        {
            _aliveAttackersQuery = _em.CreateEntityQuery(ComponentType.ReadOnly<AttackUnitTag>());
            // 위 쿼리의 11개 소비처(광역기 사전집계·배치 스킬 대상 수집 등)는 보너스 적을 계속
            // 봐야 하므로 필터는 **별도 쿼리**로만 존재한다.
            _aliveNormalAttackersQuery = _em.CreateEntityQuery(new EntityQueryDesc
            {
                All = new[] { ComponentType.ReadOnly<AttackUnitTag>() },
                None = new[] { ComponentType.ReadOnly<Wassup.Battle.Units.BonusWaveTag>() },
            });
        }
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
        // bonus-wave-pull unit 4 — 보너스 웨이브 전용 큐. **_pending 과 섞지 않는다**(계약 1) —
        // 그쪽은 웨이브 플랜·레인·컨셉을 나르고 이쪽은 포탈 셀과 링 배치만 나른다.
        private readonly List<PendingBonusSpawn> _bonusPending = new();
        private bool _bonusWaveActive;          // 계약 13 — 동시 1벌
        private float _bonusPortalCloseAtSec;   // 마지막 스폰 + linger. 이 시각에 포탈이 닫힌다
        private int _normalKillCount;             // 계약 12 — 트리거 전용(보너스 적 제외)
        private int _bonusConsumedKillMark;     // 마지막 보너스 당김 시점의 _normalKillCount
        private readonly List<Material> _ownedRuntimeMaterials = new();
        private readonly HashSet<Vector2Int> _occupiedTiles = new();
        private readonly Dictionary<Vector2Int, (Entity entity, DefenderUnitData data)> _defenderByTile = new();
        // defender-footprint unit 1 — footprint 점유 셀 → 그 유닛의 **대표 셀**(= _defenderByTile 키).
        // _defenderByTile 은 유닛당 1엔트리(대표 셀 키)를 유지해 «엔트리 수 = 기수» 소비자
        // (DeployedCountOf·뷰 동기·순회)를 지킨다. 셀→유닛 해석은 이 맵을 거친다(1×1 은 항등).
        // 등록/해제는 OccupyDefenderFootprint / ReleaseDefenderFootprint 두 함수만 지난다.
        private readonly Dictionary<Vector2Int, Vector2Int> _defenderCellOwner = new();
        private readonly List<Vector2Int> _footprintReleaseScratch = new();
        // defender-footprint unit 5 리뷰 H-1 — «취소 유예 대상» 자격 셋. PendingDeployment 는
        // 재배치 비행에도 붙어 그 존재만으론 «방금 놓은 유닛»을 식별하지 못한다 — 신규 배치가
        // 등록하고 활성화·퇴장(ReleaseDefenderTile)·리셋이 지운다. 취소 API 의 진짜 술어다.
        private readonly HashSet<Entity> _cancellableDeployments = new();
        private readonly HashSet<Entity> _onPlaceTriggeredEntities = new();
        // defender-relocation unit 8 — 효과 타일은 on-place 와 **다른** 가드를 쓴다.
        // 재배치가 on-place 를 재무장하기 때문(README 계약 4). 왜 함께 풀면 안 되는지는
        // ApplyEffectTileOnce 주석 참조.
        private readonly HashSet<Entity> _effectTileAppliedEntities = new();
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
        public static float BlobShadowLift { get; private set; } = 0.026f;
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
        // tutorial-map — 맵 인카운터가 실어 온 저작 플랜. 테스트 모드 플랜(_authoredPlan)이
        // 우선하고 이것은 그 다음이다 — 테스트 모드로 특정 플랜을 보려던 사람이 튜토리얼 맵을
        // 골랐을 때 조용히 덮이면 안 된다.
        private WavePlanAsset _encounterPlan;
        // 이번 판이 실제로 쓴 플랜(_authoredPlan 또는 _encounterPlan). 소스가 둘이 된 뒤로
        // 「_authoredPlan 이 곧 활성 플랜」이 거짓이 됐다 — 로그가 그걸 직접 읽어 NRE 가 났다.
        private WavePlanAsset _activePlan;
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
        // wave-pull-revival unit 0 — 필드를 마지막으로 비운 뒤 당긴 횟수. 겹침 상한의 기준.
        // **전멸 진행에서만 0 이 된다** — 타임아웃 진행(capReached)에서 리셋하면 «가만히 있기»가
        // 당김 예산을 벌어준다. 계약 9: 시계가 0 이 되는 지점마다 함께 0.
        private int _pullsSinceClear;
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
        // three-minute-survival unit 3 — 처치 **마리 수**(점수와 별개 축, 결과 화면 표기용).
        // 계약 9: 시계(_battleClock)가 0 이 되는 지점마다 함께 0 이 된다.
        private int _killCount;
        // three-minute-survival unit 0 — 골 안정도. **브리지가 소유하는 값**이다: 유출 1회당
        // 즉발 차감이라 시뮬 상태가 필요 없어 ECS 컴포넌트/시스템을 만들지 않는다(적이 골에
        // 살아남아 때리는 지속 피해 모델은 goal-tower-siege spec 의 몫).
        // 유출한 적의 AttackUnitData.stabilityDamage 만큼 깎이고 0 이면 패배다.
        // **계약 9(_killCount 와 같은 규칙)**: 시계가 0 이 되는 지점마다 만피로 돌아간다.
        private int _goalStability;
        private int _goalStabilityMax;
        // battle-structures unit 10 — **적 마음 축**. 위 방어 마음 축의 거울이고 활성 조건도
        // 같은 모양이다: `_enemyCoreMax > 0` 이면 이 축이 산다(타이머 축의 `_timerDuration > 0`,
        // 유출 축의 `defeatGoalReachedCount > 0` 과 같은 형태 — 계약 15).
        //
        // 모드 판정을 두지 않는 이유가 여기 있다: 침략 맵은 적 마음이 저작되지 않아 max 가
        // 0 이므로 이 축이 저절로 죽고, 타이머 만료 비교의 «적 잔여» 도 0 이 되어
        // `_goalStability >= 0` 이 항상 참 = 기존 victory_timeout 동치가 된다. 침략/공성이
        // 같은 코드를 탄다. ⚠ 「적 마음 엔티티가 없다」를 축 조건으로 쓰면 침략 맵이 첫
        // 프레임에 승리한다 — 조건은 「상한이 있었는데 지금 잔여가 0」이다.
        private int _enemyCoreCurrent;
        private int _enemyCoreMax;
        // 유출 적의 등록부 조회 실패 경고를 판당 1회로 제한(로그 폭주 방지).
        // goal-tower-siege unit 1 — 타워 부재 경고도 판당 1회.
        // heart-stress-axis unit 0 rev 2 — 돌격형 직격의 등록부 miss 경고 1회.
        private bool _leakTypeMissLogged;
        private bool _towerMissLogged;
        // heart-stress-axis unit 2 — 처치 회복의 등록부 miss 경고 1회.
        private bool _killHealTypeMissLogged;
        // heart-stress-axis unit 1 rev 2 — **심박 저작.** 마음 프랍(보드)과 화면 림이 같은
        // 배율을 써야 «마음과 화면이 같이 뛴다» 가 성립하므로, 계산 주체인 브리지가 값을 갖는다.
        // (TileSetData 는 보드 전용이고 ScoreHudView 는 화면 전용이라 어느 쪽도 단일 소스가 못 된다.)
        [Header("Heart stress — 심박 (heart-stress-axis)")]
        [Tooltip("스트레스 0 일 때 분당 심박. 평온.")]
        [SerializeField, Min(20f)] private float heartRestBpm = 52f;
        [Tooltip("스트레스 100 일 때 분당 심박. 이 값이 «위급» 의 체감을 정한다.")]
        [SerializeField, Min(20f)] private float heartMaxBpm = 168f;
        [Tooltip("심박이 밝기를 얼마나 깊게 흔드는가. 0 = 안 뛴다.")]
        [SerializeField, Range(0f, 0.9f)] private float heartBeatDepth = 0.5f;

        // heart-stress-axis unit 9 rev 2 — **머리 위 바의 «방금 올랐다» 펀치.**
        // 차오르는 바만으로는 변화가 안 읽힌다(1% 오르면 폭이 1% 늘 뿐이다). 오른 그 순간에
        // 크기로 사건을 만든다. 심박과 역할이 다르다 — 심박은 상태, 펀치는 사건.
        [Tooltip("스트레스가 오를 때 바가 얼마나 커지는가. 0 = 안 튄다.")]
        [SerializeField, Range(0f, 1f)] private float heartBarPunchDepth = 0.35f;
        [Tooltip("«최대 펀치» 로 치는 1프레임 상승분(0~100 축). 작을수록 잔타에도 크게 튄다.")]
        [SerializeField, Min(0.1f)] private float heartBarPunchFullRise = 4f;
        [Tooltip("펀치가 초당 얼마나 가라앉는가. 클수록 짧고 날카롭다.")]
        [SerializeField, Min(0.1f)] private float heartBarPunchDecayPerSec = 3.2f;
        private float _heartBarPunch;

        // heart-stress-axis unit 10 — **마음이 터지는 한 박자.** 붕괴 프레임에 결과 화면이
        // 덮어써서 «터졌다» 를 한 번도 못 보던 것을 고친다. 지연은 화면에만 걸고 집계·서버
        // 제출은 즉시다(「제출이 표시보다 앞」 계약).
        [Header("Heart burst — 파괴 박자 (heart-stress-axis unit 10)")]
        [Tooltip("마음이 무너진 뒤 결과 화면까지 버는 시간(초). 0 = 즉시(연출 없음).")]
        [SerializeField, Min(0f)] private float coreBurstHoldSec = 1.25f;
        [Tooltip("그 박자 동안의 전투 시간 배율. 1 = 안 늦춘다.")]
        [SerializeField, Range(0.05f, 1f)] private float coreBurstTimeScale = 0.3f;
        private Coroutine _coreBurstRoutine;
        // ⚠ 리스를 **필드로** 들고 있는 이유: 박자 중에 사용자가 로비로 나가면 코루틴이
        // 죽으면서 `Dispose` 가 실행되지 않아 **다음 판이 느린 채로 시작한다.** 판 경계
        // (`ResetGoalStability`)에서 반납할 수 있어야 한다.
        private TimeLease _coreBurstLease;
        private bool _coreBurstLeased;

        // heart-stress-axis unit 3 — 직전 프레임 스트레스(0~100). 넷 상승분 산출용.
        private float _lastHeartStress;
        // unit 1 rev 2 — 심박 누적 위상(0~1). 시각에서 파생하지 않는다(위 배선 주석 참조).
        private float _heartBeatPhase;
        // unit 6 — 마음 방패(살아있는 방어 본능 ≥ 1). 태그와 짝이며 writer 는 SyncGoalStability.
        private bool _coreShielded;
        // unit 8 — 스트레스 단계(0 평온 ~ 3 임계). 히스테리시스라 상태를 들고 있어야 한다.
        private int _heartStage;
        // unit 0 rev 4 — 돌격형이 마음을 치고 산화한 수 = 이 판의 「놓쳤다」.
        // ⚠ 옛 유출 카운터(`_goalReachedCount`)를 재사용하지 **않는다** — 그쪽은 몽마의 계약
        // 부착 게이트(`RemainingLeakAllowance`)를 먹이는 **라이브** 값이라, 여기서 올리면
        // 그 카드가 판 중반부터 조용히 봉인된다.
        private int _rusherArrivalCount;
        private readonly List<Entity> _liveCoresScratch = new();
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
        private NativeQueue<Wassup.Battle.Effects.AggroAcquireEvent> _aggroAcquireEventQueue;
        // attack-decoupling unit 4 — Effects(HazardCastSystem)→Combat(AttackSystem) 캐스트 사건.
        private NativeQueue<Wassup.Battle.Combat.CastEvent> _castEventQueue;
        // use-flow unit 3 — Combat→Bridge 부착 카드 발동 신호(머리 위 아이콘 행 펄스).
        private NativeQueue<Wassup.Battle.Combat.DcTriggerFiredEvent> _dcTriggerFiredQueue;

        // skill-layer-foundation unit 4/5 — 감지(Burst) → 디스패처(managed) 채널과
        // 그 뒤의 레지스트리·어댑터. 브리지가 소유하는 이유는 저작 계층(SO)에 닿을 수
        // 있는 유일한 자리이고, 채널 수명주기가 이미 여기 모여 있기 때문이다.
        private NativeQueue<Wassup.Battle.Skills.SkillFiredEvent> _skillFiredQueue;
        private readonly Wassup.Skills.SkillRegistry _skillRegistry = new Wassup.Skills.SkillRegistry();
        private readonly Wassup.Battle.Skills.EcsSkillContext _skillContext = new Wassup.Battle.Skills.EcsSkillContext();
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

        // battle-sim-extraction M0 unit 1 — 매치 내 stable ID 발급기(`SimEntityId`).
        // 스폰 순서대로 0,1,2… 를 나눠 주고 **재사용하지 않는다**. 리셋은 매치 경계
        // 한 곳(`EnsureQueriesAndQueues`)이며, 거기서 리셋하는 이유는 그 지점이
        // 맵 빌드(거점 스폰)보다 **앞**이라 «리셋 전에 이미 발급된 엔티티»가 생길 수
        // 없기 때문이다. 카운터가 Bridge 에 있는 것은 지금 발급자가 전부 managed 스폰
        // 경로이기 때문 — ECS 내부 스폰이 ID 를 필요로 하는 날(M1 이벤트·스냅샷 키)
        // 싱글턴으로 승격한다.
        private int _nextSimEntityId;

        // map-origin-placement: board 월드 원점. 모든 grid↔world 변환의 단일 소스.
        // Tilemap 모드는 무조건 zero (BuildMapForBattle 에서 고정).
        private float3 _boardOrigin = float3.zero;
        // battle-structures unit 4 — 거점 등록부(Bridge 가 스폰 주체라 직접 안다).
        // 구 _goalGaugeList(goal-stability unit 5)의 부활·일반화 — 리뷰 M-e 의 «writer 0» 처분.
        // 게이지 폴링 + 붕괴 감지(ⓐ: 사라진 엔티티의 셀 특정)가 소비한다. 쿼리 없이 맵당 소수 순회.
        private readonly List<(Entity entity, Vector2Int cell, Faction faction)> _structureRegistry = new();
        // map-diorama-stage unit 10 — 스테이지 거점 저작(StructureMarker → StructureEntry, 관리 참조). 맵 수명.
        // 구 _resolvedMapDoc.Structures 의 자리 — SpawnStructureEntities/Views 가 SO 스탯·프랍을 여기서 읽는다.
        private readonly List<Wassup.Data.StructureEntry> _stageStructures = new();
        // unit 4 — 저작 거점의 뷰 인스턴스(SO.viewPrefab). Pickup 프레젠터 선례: 브리지가
        // 만들고 teardown 이 지운다. 골 타워 프랍은 기존 경로(MapThemeData.goalStructureProp) 유지.
        private readonly List<GameObject> _structureViews = new();
        // instinct-turret-readout unit 1 — 본능 프랍의 포신 조준 프리젠터. **셀로 잇는다**:
        // 뷰는 맵 수명(배치 페이즈부터 보인다)이고 엔티티는 판 수명이라 엔티티 참조로 묶으면
        // 매 판 재배선이 필요하다. 셀은 두 수명 모두에서 불변이다.
        private readonly Dictionary<Vector2Int, Wassup.Presentation.StructureTurretView> _structureTurretsByCell = new();
        // instinct-wreck unit 0 — 붕괴한 거점의 잔해 프리젠터. 포신 사전과 **같은 자리·같은
        // 규칙**(셀로 잇는다 — 뷰는 맵 수명, 엔티티는 판 수명이라 셀만이 두 수명에서 불변).
        private readonly Dictionary<Vector2Int, Wassup.Presentation.StructureWreckView> _structureWrecksByCell = new();
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
        // map-diorama-stage unit 2 — 현재 판의 스테이지 인스턴스(= 맵 비주얼). 맵과 같은 수명 —
        // 파괴는 TeardownGeneratedMap 이 소유해 teardown 5경로(매치 종료·재빌드 선행·빌드 실패·
        // StopBattle·draft 정리) 전부를 덮는다.
        private Wassup.Core.MapStage _stageInstance;
        // map-diorama-stage unit 4 — 골/스폰 마커 등록부. 구 TilemapMapView._goalPropsByCell 의
        // 후계(브리지 소유). 골 균열/붕괴 연출과 튜토리얼 앵커가 여기서 마커를 찾는다.
        private readonly Dictionary<Vector2Int, Wassup.Core.GoalMarker> _goalMarkersByCell = new();
        private readonly List<Wassup.Core.SpawnMarker> _spawnMarkersByLane = new();

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
            // wave-concept-blocks unit 2 계약 6 — 브리핑과 런타임은 **같은 laneCount** 를 넘긴다.
            // 다르면 컨셉 후보 집합이 갈려 예고와 실스폰이 다른 편성을 보여준다.
            return WavePatternGenerator.Generate(d, waveSeed, GeneratorLaneCount);
        }

        // 맵의 스폰 지점 수. 컨셉의 lane 위상 해석과 후보 게이트의 입력이다(결정론 키의 일부).
        // 맵 미확정이면 2 로 폴백한다 — 생성기도 같은 값을 기본으로 쓴다.
        private int GeneratorLaneCount =>
            _generatedMap.IsCreated && _generatedMap.spawns.Length > 0
                ? _generatedMap.spawns.Length
                : 2;

        // gimmick-match-integration unit 1 — GameManager 가 배정한 매치 기믹(없으면 null).
        // 3개 소비 지점(config 주입·픽업 스폰 게이트·디버그 로그)의 단일 소스. 시즌 결합 대체.
        private Wassup.Data.GimmickData _assignedGimmick;
        public void SetAssignedGimmick(Wassup.Data.GimmickData g) => _assignedGimmick = g;

        // bonus-wave-pull unit 4 — 보너스 스폰 1건. 포탈 셀을 좌표로 들고 다니는 이유는
        // 스폰 시점에 맵을 다시 조회하지 않기 위해서다(당김과 스폰 사이에 맵은 안 바뀌지만,
        // 인덱스로 들고 있으면 저작이 바뀐 문서를 다시 로드했을 때 조용히 다른 칸이 된다).
        private struct PendingBonusSpawn
        {
            public float spawnAtSec;   // 배틀 도메인 시계 기준 절대 시각
            public int2 cell;
            public int ringIndex;
            public int ringCount;
        }

        private struct PendingSpawnEntry
        {
            public SpawnEntry entry;
            public int laneIndex;
            // duel-route-tours unit 1 — 이 스폰을 만든 컨셉 슬롯의 경로 지정. -1 = 무지정.
            // 레거시 덱 스폰(생성 웨이브 미사용 경로)은 컨셉이 없어 -1 로 남는다.
            public int pathIndex;
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

        // gift-phase-removal unit 1 — 재시작 진입은 매치 인트로(기믹 리빌)를 거친다(배선 시).
        // 미배선이면 곧장 배치로(HandController 가 Placement 진입에서 덱을 구성한다).
        private void EnterPlacementOrIntro()
        {
            if (_gimmickPhaseView != null) _gimmickPhaseView.BeginIntro();
            else _placementPhaseView?.BeginPlacementPhase();
        }

        // result-screen-lobby-exit unit 0 — 결과창 버튼이 "로비로" 가 되면서 호출처가
        // 없다(끊긴 배선이 아니라 의도). 재시작을 되살릴 때 다시 구독하면 되도록
        // 로직은 남겨둔다. EnterPlacementOrIntro / ReLogSkillLoadoutForNewSession 도
        // 이 경로 전용이라 함께 대기 상태다.
        private void OnRestartRequested()
        {
            if (_world == null)
            {
                EnterPlacementOrIntro();
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
            EnterPlacementOrIntro();
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
            if (enemyHitBarSpawner != null) enemyHitBarSpawner.Clear(); // unit 2 — 잔여 마이크로바 정리(생명주기 대칭)
            if (statusFxSpawner != null) statusFxSpawner.Clear(); // unit-status-fx unit 2 — 잔여 상태 연출 정리
            if (dcIconStripSpawner != null) dcIconStripSpawner.Clear(); // unit-dreamcatcher-icons — 잔여 아이콘 스트립 정리(생명주기 대칭)
            unitOverheadUiLayer?.Clear(); // unit-overhead-ui — 공통 health/card view 정리
            ClearPickupVisuals(); // season-gimmick-overwork unit 6 — 잔여 레드불 뷰 정리
            ClearResignationVisuals(); // season-gimmick-clockout unit 1 — 잔여 사직서 뷰 정리
            ClearAllyBuffZonePaint(); // active-ally-zone unit 2 — 잔여 장판 점등 정리(생명주기 대칭)
            ClearBonusPortalViews(); // bonus-wave-pull unit 6 — 잔여 포탈 뷰 정리(생명주기 대칭).
                                     // ResetBonusWaveState 에도 있지만 그건 **다음 판 진입** 시점이라,
                                     // 여기가 없으면 판을 끝내고 로비로 나가는 경로에서 포탈이 남는다.
            // defender-clock-out unit 3 — 진행 중 퇴근 연출 정리. 떼어낸(Detach) 뷰는 풀의
            // _byEntity 에 없어 바로 위 spineUnitPool.DisposeAll() 이 **안 치운다**. 그리고 이
            // 컴포넌트가 붙은 GO 는 씬 루트라 아무도 비활성화하지 않아 OnDisable 도 안 불린다
            // — 즉 이 한 줄이 없으면 재시작 시 지난 판 유닛과 키링이 새 판 보드 위에서 논다.
            //
            // ⚠⚠ **`?.` 를 쓰지 말 것.** C# 의 null 조건 연산자는 Unity 의 fake-null 을 모른다.
            // `TeardownCurrentBattle` 은 `OnDestroy` 에서도 불리는데 그 시점엔 이 컴포넌트가 이미
            // 파괴돼 있어 `retireFlight?.CancelAll()` 이 **MissingReferenceException 을 던지고**,
            // 그러면 이 메서드가 중단돼 아래 `DestroyEntitiesByType<BattleTimeScale>()` 이 실행되지
            // 않는다 → 싱글턴이 누수돼 다음 씬에서 "found 2 instances" 로 터진다(실측 2026-08-15).
            // 형제 줄들이 전부 `if (x != null)` 인 이유가 이것이다(Unity 오버로드 == 가 fake-null 처리).
            if (retireFlight != null) retireFlight.CancelAll();
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
            _battleSimGroupCache = null; // M0 unit 2 — 월드가 갈리면 그룹 핸들도 무효
        }

        private bool HasLiveEntityManager()
            => _world != null && _world.IsCreated && _em != default;

        // battle-sim-extraction M0 unit 1 — 스폰 지점에서 stable ID 를 붙이는 유일한 통로.
        // 여기 말고 어디서도 `SimEntityId` 를 쓰거나 고치지 않는다(사후 부여 = 순서 왜곡).
        // 대상은 «타겟 후보가 될 수 있는 것»(FactionTag+Health+LocalTransform) 전부 + 투사체.
        private void AttachSimEntityId(Entity entity)
        {
            _em.AddComponentData(entity, new Wassup.Battle.Units.SimEntityId { value = _nextSimEntityId++ });
        }

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
            DestroyEntitiesByType<Wassup.Battle.Effects.AggroAcquireEventsSingleton>();
            DestroyEntitiesByType<Wassup.Battle.Combat.CastEventsSingleton>();
            DestroyEntitiesByType<Wassup.Battle.Combat.DcTriggerFiredEventsSingleton>();
            DestroyEntitiesByType<Wassup.Battle.Skills.SkillFiredEventsSingleton>();
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
            if (_aggroAcquireEventQueue.IsCreated) _aggroAcquireEventQueue.Dispose();
            if (_castEventQueue.IsCreated) _castEventQueue.Dispose();
            if (_dcTriggerFiredQueue.IsCreated) _dcTriggerFiredQueue.Dispose();
            if (_skillFiredQueue.IsCreated) _skillFiredQueue.Dispose();
            Wassup.Battle.Skills.SkillDispatchSystemBase.Uninstall();
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
                _aliveNormalAttackersQuery.Dispose();
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

            // waypoint-routing unit 1 — 이번 판 공격 로스터의 통행층 합집합. Default(Path)는
            // 설치자가 슬롯 0에 고정하며, 여기서는 실제 SO 저작값을 중복 없이 전달한다.
            var traversalMasks = new NativeList<byte>(4, Allocator.Temp);
            try
            {
                var attackPool = ActiveDeck != null ? ActiveDeck.ResolveAttackUnitPool() : null;
                if (attackPool != null)
                    for (int i = 0; i < attackPool.Length; i++)
                        AddTraversalMask(attackPool[i], ref traversalMasks);

                if (ActiveDeck != null)
                {
                    AddTraversalMask(ActiveDeck.bossUnit, ref traversalMasks);
                    if (ActiveDeck.bossPool != null)
                        for (int i = 0; i < ActiveDeck.bossPool.Length; i++)
                            AddTraversalMask(ActiveDeck.bossPool[i], ref traversalMasks);
                }

                // map-origin-placement: _boardOrigin 은 BuildMapForBattle 이 설정한다 (Tilemap = zero 고정).
                SimFieldInstaller.InstallNavFields(
                    _em, in _generatedMap, tileSize, _boardOrigin, ref _simFields,
                    traversalMasks.AsArray());
            }
            finally
            {
                traversalMasks.Dispose();
            }
        }

        private static void AddTraversalMask(AttackUnitData unit, ref NativeList<byte> masks)
        {
            if (unit == null) return;
            byte candidate = (byte)unit.EffectiveTraversalLayers;
            for (int i = 0; i < masks.Length; i++)
                if (masks[i] == candidate) return;
            masks.Add(candidate);
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
                // traversal-layers unit 1a — 라우팅은 슬롯별 stride 다. 슬롯 뷰로 읽는다.
                var spawnFlow = field.FlowSlot(Wassup.Battle.Effects.FlowFieldSingleton.PrimarySlot);
                if (idx >= 0 && idx < spawnFlow.Length) flowDir = spawnFlow[idx];
            }

            // 폭 중앙 기준 대칭 이산 N-레인 분율 (상단은 topScale 로 좁힘). 스폰 순서 round-robin.
            float frac = Wassup.Battle.Movement.SpawnSpread.LaneFraction(
                _spawnSpreadCounter++, spawnSubLaneCount, spawnSpreadFraction, spawnSpreadTopScale);
            return Wassup.Battle.Movement.SpawnSpread.LateralOffset(frac, tileSize, flowDir);
        }

        // Phase 10A (P10A-04A): GeneratedMap dispose 멱등. 재시작/redraft 시 TearDown 후 재생성.
        // map-diorama-stage unit 4 — 스테이지 인스턴스에서 마커 등록부 구축. 셀 양자화는
        // 스캐너와 같은 단일 산식(MapStageMath) — 기즈모·빌더·등록부가 같은 셀을 본다.
        private void BuildStageMarkerRegistry()
        {
            _goalMarkersByCell.Clear();
            _spawnMarkersByLane.Clear();
            if (_stageInstance == null) return;
            foreach (var g in _stageInstance.GetComponentsInChildren<Wassup.Core.GoalMarker>(false))
            {
                Vector3 local = _stageInstance.transform.InverseTransformPoint(g.transform.position);
                Vector2Int cell = Wassup.Data.MapStageMath.LocalToCell(
                    local, _stageInstance.gridOriginLocal, tileSize);
                _goalMarkersByCell[cell] = g;
            }
            _spawnMarkersByLane.AddRange(
                _stageInstance.GetComponentsInChildren<Wassup.Core.SpawnMarker>(false));
            _spawnMarkersByLane.Sort((a, b) => a.laneIndex.CompareTo(b.laneIndex));
        }

        // map-diorama-stage unit 4 — 튜토리얼 포커스 앵커 (구 TilemapMapView.TryGet*VisualAnchor 승계).
        // 마커가 없으면 셀 중심의 뷰 좌표로 폴백 — 구 의미와 동일.
        public bool TryGetGoalVisualAnchor(out Vector3 world)
        {
            world = default;
            if (!_generatedMap.IsCreated) return false;
            var primary = new Vector2Int(_generatedMap.goal.x, _generatedMap.goal.y);
            if (_goalMarkersByCell.TryGetValue(primary, out var marker) && marker != null)
            {
                world = marker.VisualAnchor();
                return true;
            }
            world = CellCenterView(_generatedMap.goal);
            return true;
        }

        public int SpawnLaneCount => _generatedMap.IsCreated ? _generatedMap.spawns.Length : 0;

        public bool TryGetSpawnVisualAnchor(int laneIndex, out Vector3 world)
        {
            world = default;
            if (!_generatedMap.IsCreated || laneIndex < 0 || laneIndex >= _generatedMap.spawns.Length)
                return false;
            if (laneIndex < _spawnMarkersByLane.Count && _spawnMarkersByLane[laneIndex] != null)
            {
                world = _spawnMarkersByLane[laneIndex].VisualAnchor();
                return true;
            }
            world = CellCenterView(_generatedMap.spawns[laneIndex]);
            return true;
        }

        private Vector3 CellCenterView(int2 cell)
            => Wassup.Core.BoardSpace.ToView(
                Wassup.Battle.Movement.GridMath.CellToWorldCenter(cell, tileSize, 0f, float3.zero));

        private void TeardownGeneratedMap()
        {
            // Tilemap 뷰 잔상 제거 (RebuildDraftMap 재진입 / 전투 종료 안전). Clear 는 idempotent.
            if (tilemapMapView != null) tilemapMapView.Clear();
            // battle-structures 후속 2 — 거점 프랍도 맵과 같은 수명. 맵 teardown 경로가 5곳
            // (매치 종료·재빌드 선행·빌드 실패·StopBattle·draft 정리)이라 여기 두면 전부 덮인다.
            ClearStructureViews();
            _goalMarkersByCell.Clear();   // unit 4 — 마커 등록부는 스테이지와 같은 수명
            _spawnMarkersByLane.Clear();
            _stageStructures.Clear();     // unit 10 — 거점 저작도 스테이지와 같은 수명
            // map-diorama-stage unit 2 — 스테이지 인스턴스(맵 비주얼)도 맵과 같은 수명.
            // EditMode(라이브 경로 테스트)에서는 Destroy 가 불법이라 즉시 파괴로 분기.
            if (_stageInstance != null)
            {
                if (Application.isPlaying) Destroy(_stageInstance.gameObject);
                else DestroyImmediate(_stageInstance.gameObject);
                _stageInstance = null;
                // camera-direction unit 18 — 볼륨은 스테이지와 같은 수명이다. 놓아주지 않으면
                // 카메라 소유자가 파괴된 프로파일에 계속 쓴다.
                PushStagePostVolume();
            }
            if (_generatedMap.IsCreated) _generatedMap.Dispose();
            _generatedMap = default;
        }

        // map-pipeline-cleanup unit 2 — legacy 옵션/설정 에셋 제거 후 FallbackLinear 전용 상수.
        // 값은 제거 시점 라이브와 동일(MapGenerationOptions.Default 20×10 / MapGenerationSettings 1).
        private static readonly int2 FallbackGridSize = new int2(20, 10);

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
            Wassup.Core.MapStage stagePrefab = null;
            _resolvedDeck = deck;
            _encounterPlan = null;   // tutorial-map — 이월 금지(이전 판의 플랜이 다음 맵에 붙으면 안 된다)
            // endless-mode-removal unit 0 — 엔드리스 전용 인카운터 분기는 제거했다. 그 분기는
            // mapPool 을 건드리지 않는 **선행** 분기였으므로, 빼도 아래 인덱스 계산은 한 줄도
            // 안 바뀐다 — 랜덤/토너먼트 맵 배정이 byte-identical 로 남는다.
            if (mapPool != null && mapPool.Count > 0)
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
                if (encounter.stage != null)
                {
                    stagePrefab = encounter.stage;
                    if (encounter.deck != null) _resolvedDeck = encounter.deck;
                    _encounterPlan = encounter.plan;   // tutorial-map — 저작 플랜은 맵과 한 몸
                }
            }

            // map-diorama-stage unit 2 — 스테이지 프리팹이 유일 맵 소스. 없으면 hard-fail.
            if (stagePrefab == null)
            {
                Debug.LogError("[BattleBridge] 맵 스테이지 프리팹이 없다 — MapStagePool 엔트리를 확인할 것.", this);
                _generatedMap = default;
                return;
            }

            // 인스턴스가 곧 비주얼이다. 루트는 원점·무회전으로 고정 — gridOriginLocal(스테이지
            // 로컬)이 그대로 월드 좌표가 되어 격자 정렬(아래 AlignGridTo)이 단순해진다.
            _stageInstance = Instantiate(stagePrefab);
            _stageInstance.name = stagePrefab.name;
            _stageInstance.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            // 리뷰 M-2 — 스케일도 고정. 스캐너는 로컬(스케일 나눔)로 양자화하고 격자는 월드
            // (스케일 곱)로 정렬하므로, 루트 스케일≠1 이면 프랍과 셀이 조용히 어긋난다(C-1 재발 경로).
            _stageInstance.transform.localScale = Vector3.one;

            // 프랍 선언 스캔 → 조립. 형식 오류·연결성 실패 = hard-fail — 조용한 폴백 맵 교체
            // (BuildFallbackLinear)는 은퇴했다(README 계약 9 개정: 디오라마에서 연결성 실패는
            // 저작 오류이고, 폴백 맵은 unit 3 이후 렌더러가 없다). 실패 시 스테이지 인스턴스는
            // TeardownGeneratedMap 이 함께 정리한다.
            try
            {
                var scan = Wassup.Core.MapStageScanner.Scan(_stageInstance, tileSize);
                _generatedMap = DioramaMapBuilder.Assemble(scan, Unity.Collections.Allocator.Persistent);
                // unit 10 — 거점 관리 목록은 빌더와 같은 (y, x) 사전순(Assemble 이 이미 형식 검증을 통과시켰다).
                _stageStructures.Clear();
                _stageStructures.AddRange(scan.structures);
                _stageStructures.Sort(DioramaMapBuilder.CompareStructureRowMajor);
            }
            catch (MapGenerationFailedException ex)
            {
                Debug.LogError($"[BattleBridge] {ex.Message}", this);
                TeardownGeneratedMap();
                return;
            }

            if (!MapConnectivity.AllSpawnsReachGoal(_generatedMap))
            {
                Debug.LogError("[BattleBridge] 스테이지 연결성 실패(스폰→골 도달 불가) — 차단 프랍 배치를 확인할 것.", this);
                TeardownGeneratedMap();
                return;
            }

            // map-diorama-stage unit 2 — 시드 커빙(ObstaclePlacer.DesignateDeco) 블록은 제거했다.
            // 발동 시 RederivePlaceMask 가 마스크를 Derive(Walk→Path)로 되써 Ground 유닛 전원이
            // 배치 불가가 된다(critic M-3). 차단/배치판은 이제 프랍 선언이 전부다.

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

                // 거점이 선 자리엔 못 놓는다 — 건물이 서 있으니까. 그게 전부다.
                //
                // instinct-content unit 1 — 적 본능의 「주변 배치 배제」(구 9×9)는 폐지됐다.
                // 값(3→0)뿐 아니라 술어·분기까지 지웠다: 사용자 지시는 «배치 불가» 였고 그건
                // 건물 자리를 뜻했지, 본능만 특별히 넓게 막으라는 뜻이 아니었다. 남은 규칙은
                // 스폰·골 폐쇄와 완전히 같은 성격 — footprint 만, 빌드 시 파생, 저작본 불변.
                if (_generatedMap.structures.IsCreated)
                {
                    for (int i = 0; i < _generatedMap.structures.Length; i++)
                    {
                        var st = _generatedMap.structures[i];
                        int half = Wassup.Data.StructurePlacements.FootprintOf(st.faction) / 2;
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
            BlobShadowLift = blobShadowLift;
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
            // map-diorama-stage unit 2 (critic C-1) — grid.transform 의 **유일한** writer.
            // 셀 (0,0) 최소 모서리를 스테이지의 gridOriginLocal 에 맞춘다. CenterBoardAtWorldOrigin
            // (구 writer)은 제거됨 — writer 가 둘이면 프랍과 논리 셀이 조용히 어긋난다.
            // 리뷰 M-1 — Initialize **앞**에서 정렬한다: Initialize 내부의 구조물 뷰 앵커 리셋이
            // grid.transform 을 라이브로 읽으므로, 뒤에 정렬하면 앵커가 옛 포즈로 굳는다
            // (페인트류는 전부 셀 좌표라 순서 무관 — 확인됨).
            if (tilemapMapView != null && tilemapMapView.Grid != null && _stageInstance != null)
                tilemapMapView.AlignGridTo(
                    _stageInstance.transform.TransformPoint(_stageInstance.gridOriginLocal));
            if (tilemapMapView != null)
                // 테마-구동 tileSet: theme 이 지정하면 그걸, 아니면 scene 의 tileSet 폴백 (desert-theme).
                tilemapMapView.Initialize(_generatedMap, tileSize,
                    theme != null && theme.tileSet != null ? theme.tileSet : tileSet,
                    UseRealShadows);
            // sim origin 은 무조건 zero (README 계약).
            _boardOrigin = float3.zero;
            if (placementInput != null) placementInput.Initialize(_generatedMap, tileSize);

            // sim↔view 변환의 단일 지점 — BuildFlowField 직전 1회 설정. grid 없으면(headless) skip —
            // BoardSpace 는 view 계층 전용이라 sim 빌드에 불필요하고, null 전달은 Configure 가 에러로 거부한다.
            if (tilemapMapView != null && tilemapMapView.Grid != null)
                Wassup.Core.BoardSpace.Configure(BoardOrigin, tileSize, tilemapMapView.Grid);

            // 카메라 포즈는 CameraDirector 가 매 프레임 절대값으로 소유한다 — 여기서 카메라를 직접
            // 쓰면 다음 LateUpdate 에 덮여 무효다. 맵 빌드 시점에 카메라를 만지는 경로를 다시
            // 만들지 말 것(옛 ApplyTilemapCameraPreset 이 그래서 은퇴·제거됐다).
            // 페이즈별 카메라가 필요하면 CameraDirectionConfig 의 상태 레시피(camera-direction unit 10)로.

            // camera-direction unit 11 — 맵마다 크기가 달라(12×10 ~ 20×12) 카메라가 판에 맞춰
            // 물러나야 한다. 그리드가 확정된 지금 보드 bounds 를 카메라 소유자에게 **밀어준다**.
            // 브리지가 이 push 의 정당한 소유자인 이유: 격자 크기가 _generatedMap.gridSize,
            // 즉 sim 쪽 데이터다(뷰끼리 직접 주고받게 되돌리지 말 것).
            // Director 는 이 값을 저장만 하고 포즈는 상태 레시피에서 매 프레임 계산한다.
            // view·director 부재(headless)면 조용히 skip.
            // bounds 는 ground 렌더러 실측이 아니라 플레이 그리드다 —
            // 전자는 주변 데코 지대까지 포함해(20×12 → 35×32) 카메라가 과하게 물러난다.
            if (tilemapMapView != null && tilemapMapView.TryGetPlayfieldWorldBounds(
                    new Vector2Int(_generatedMap.gridSize.x, _generatedMap.gridSize.y), out var boardBounds))
                EnsureCameraDirector()?.SetBoardBounds(boardBounds);

            // camera-direction unit 18 — 포스트 볼륨도 스테이지 소유다(map-diorama-stage 가 씬의
            // 전역 Post 를 프리팹 안으로 옮겼다). 보드 bounds 와 같은 이유로 브리지가 밀어준다:
            // 볼륨은 스테이지 인스턴스에 붙어 있어 씬에서 미리 배선할 수 없다.
            PushStagePostVolume();

            BuildFlowField();
            // season-gimmick-overwork unit 4 — 픽업 스폰 후보 셀(Walk∪Place)은 goal field 와
            // 같은 맵-빌드 시점에 구축. gimmick 비활성이면 no-op.
            BuildPickupSpawnState();
            // gimmick-match-integration — 기믹 config 주입도 맵-빌드 시점(배정된 _assignedGimmick
            // 확정 이후)에 함께. guarded EnsureQueriesAndQueues 로는 배정 전에 1회 돌아 누락됐었다.
            CreateGimmickConfigIfActive();

            // enemy-tile-movement-integrity unit 0 — 스폰 분산 순번 리셋(결정론 수열은 시드 불필요).
            _spawnSpreadCounter = 0;

            // map-diorama-stage unit 2 (critic M-8) — 절차 배경/링 프랍 인스턴스화는 차단했다.
            // 합성 tiles 에서 Deco→Env zone 이라 BackgroundPropPlacer 가 아티스트가 이미 프랍을
            // 놓은 셀 위에 절차 프랍을 범람시킨다. 프랍은 이제 스테이지 프리팹의 저작물이 전부다.
            // (BackgroundPropPlacer/InstantiateRingProps 코드 은퇴는 unit 3 소관.)

            // map-diorama-stage unit 4 — 골/스폰 구조물 프랍 인스턴스화 은퇴. 골/스폰의 «몸»은
            // 스테이지 프리팹에 저작된 프랍이고, 연출 훅(앵커·균열·붕괴)은 마커가 소유한다.
            BuildStageMarkerRegistry();

            // battle-structures 후속 2(리뷰 M-5) — 거점 프랍은 **맵 수명**이다.
            // 엔티티는 StartBattle 이 세우지만(판 수명), footprint 배치 배제는 이 빌드 시점에 이미
            // 파생됐다. 뷰를 엔티티에 묶어두면 **배치 페이즈에 «막힌 칸만 있고 왜 막혔는지
            // 보여주는 것이 없는»** 구간이 생긴다 — 플레이어가 알 방법이 없다.
            // 정리는 TeardownGeneratedMap 이 소유한다(맵과 같은 수명 = 재빌드마다 정확히 1벌).
            SpawnStructureViews();

            // effect-tiles unit 1 — Place 셀 seed 결정론 효과 타일. 페인트는 Initialize(Clear) 이후 계약.
            // dict clear 는 가드 밖 — 이전 빌드 잔존 제거(테마가 효과 타일 없어도).
            _effectTilesByCell.Clear();
            // US-004b — 스테이지가 효과 타일을 억제할 수 있다. 열린 마당에서는 전 셀이 후보라
            // 고정 셀 계측(e2e)이 오염된다 — 픽스처 스테이지 저작 스위치.
            bool stageSuppressesEffectTiles = _stageInstance != null && _stageInstance.suppressEffectTiles;
            if (!stageSuppressesEffectTiles && tilemapMapView != null && theme != null &&
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
            ResetBonusWaveState();   // bonus-wave-pull unit 4 — _pending 리셋과 co-locate
            _occupiedTiles.Clear();
            _defenderCellOwner.Clear(); // defender-footprint unit 1 — 점유 집합과 co-locate(불변식)
            _cancellableDeployments.Clear(); // unit 5 리뷰 H-1 — 매치 경계에서 자격 셋도 함께
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
            _effectTileAppliedEntities.Clear(); // unit 8 — 두 가드는 항상 같은 지점에서 함께 비운다
            _synergyActivatedEntities.Clear();
            _synergyActivations = 0;
            _synergyPeakCount = 0;
            _goalReachedCount = 0;
            _leakAllowancePenalty = 0; // 몽마의 계약 선불 — 매치 경계에서 소멸(이월 금지)
            _killCount = 0;
            ResetGoalStability();      // three-minute-survival unit 0 — 계약 9
            DestroyStructureEntities();  // goal-tower-siege unit 0 — 이전 판의 타워/거점 정리
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

            // on-place-skill-rework 리뷰 반영 — **배치 페이즈에 쌓인 발사 요청을 버린다.**
            // `DrainProjectileSpawnRequests` 는 `Update` 의 `if (!_running) return;` 아래라
            // 전투 시작 전에는 돌지 않는데, sim(emitter)은 `_running` 을 모르고 캐리어를 만든다.
            // 그래서 배치 페이즈에 캐논을 놓으면 캐리어가 파괴되지 않고 남아, **여기서
            // `_running` 이 켜지는 순간 낡은 좌표로 일제히 터진다**(실측 캐리어 3개).
            // 정리 시점은 판 종료(`DestroyBattleEntities`)뿐이라 그 사이 창이 비어 있었다.
            //
            // ⚠ 「배치 페이즈에 배치한 스킬은 낭비된다」는 기존 사양 그대로다(README 후속 후보
            // 「배치 페이즈 발동 정책」). 이 줄이 고치는 것은 **낭비가 뒤늦게 터지는 것**뿐이다.
            DestroyEntitiesByType<ProjectileRequestCarrier>();

            // on-place-shuttle-shotgun unit 2 — **배치 페이즈에 쌓인 실드 부여 연출도 버린다.**
            // 위 캐리어와 같은 창(窓)의 다른 얼굴이다: 실드셔틀을 배치 페이즈에 놓으면 sim 이
            // 실드를 즉시 붙이지만(그건 정상 — 상태라 드레인이 필요 없다) 부여 VFX 이벤트는
            // `_running` 아래 드레인이라 큐에 남아, 여기서 **전투 시작 순간 일제히 터진다**.
            // 실드는 이미 붙어 있으므로 연출만 뒤늦게 오는 것이고, 그건 사건이 아니라 잔상이다.
            if (_shieldGrantedEventQueue.IsCreated) _shieldGrantedEventQueue.Clear();

            _pending.Clear();
            ResetBonusWaveState();   // bonus-wave-pull unit 4 — 두 리셋 지점 양쪽에 있어야 한다
            _usingGeneratedWaves = TryInitializeGeneratedWaves();
            if (!_usingGeneratedWaves)
            {
                int laneCount = math.max(1, _generatedMap.spawns.Length);
                for (int i = 0; i < ActiveDeck.spawns.Count; i++)
                    _pending.Add(new PendingSpawnEntry
                    {
                        entry = ActiveDeck.spawns[i],
                        laneIndex = WavePatternGenerator.EffectiveSpawnIndex(
                            ActiveDeck.spawns[i].spawnIndex, i, laneCount),
                        pathIndex = -1,   // 레거시 덱 스폰은 컨셉이 없다
                    });
            }
            _startTime = Time.time;
            _battleClock = 0.0;
            _killCount = 0;
            ResetGoalStability(); // three-minute-survival unit 0 — 계약 9 (시계와 짝)
            // goal-tower-siege unit 0 — 맵·월드가 준비된 뒤 골 셀마다 타워를 세운다.
            // ResetGoalStability 다음이어야 풀이 이번 판의 최대치를 받는다.
            SpawnStructureEntities();
            // wave-authoring-test-mode unit 2 — 작성 모드는 plan.timerDurationSec(0=endless).
            // seed/legacy 경로는 deck.timerDurationSec 그대로(무변경).
            _timerDuration = _usingAuthoredPlan ? _wavePlan.timerDurationSec : ActiveDeck.timerDurationSec;
            // battle-sim-extraction M0 unit 3 — 조건 물질화. 여기가 유일한 수집 지점이다:
            // 맵·웨이브플랜·거점이 확정됐고 아직 sim 이 한 틱도 돌지 않은 유일한 순간.
            //
            // ⚠ **진단이 판을 막을 수 없어야 한다.** 이 수집은 임의의 SO 그래프를 리플렉션으로
            // 훑는다 — 예상 못한 필드 타입 하나, throw 하는 getter 하나면 예외가 난다. 그런데
            // 이 자리는 `_running = true` **앞**이라, 막지 않으면 그 예외가 곧 **판이 시작되지
            // 않는 것**이 된다. 스냅샷은 골든 판독용 부가 정보이고 게임 규칙이 아니므로,
            // 실패하면 해시를 비우고 그대로 판을 시작한다(빈 해시는 골든 쪽에서 드러난다).
            try { _matchConfig = CollectMatchConfig(); }
            catch (System.Exception e)
            {
                _matchConfig = default;
                Debug.LogWarning($"[BattleBridge] MatchConfig 수집 실패 — 판은 그대로 시작한다: {e.Message}");
            }
            _running = true;
            if (_usingGeneratedWaves)
                QueueDueWaves(0f);
            if (_usingAuthoredPlan)
                Debug.Log($"[BattleBridge] Battle started with AUTHORED plan '{_activePlan?.displayName}' "
                    + $"(source={(_authoredPlan != null ? "test-mode" : "map-encounter")}) "
                    + $"waves={_wavePlan.waves.Count} timer={_timerDuration:F0}s.");
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
                CreateAliveAttackerQueries();
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
            if (_aggroAcquireEventQueue.IsCreated) _aggroAcquireEventQueue.Dispose();
            _aggroAcquireEventQueue = new NativeQueue<Wassup.Battle.Effects.AggroAcquireEvent>(Allocator.Persistent);
            var aggroAcquireSingleton = _em.CreateEntity();
            _em.AddComponentData(aggroAcquireSingleton, new Wassup.Battle.Effects.AggroAcquireEventsSingleton { queue = _aggroAcquireEventQueue });

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
            // skill-layer-foundation unit 4/5 — 스킬 발동 채널. 감지자가 skillId != 0 인
            // 슬롯을 여기 싣고, seam 3곳의 디스패처가 concrete 를 부른다.
            if (_skillFiredQueue.IsCreated) _skillFiredQueue.Dispose();
            _skillFiredQueue = new NativeQueue<Wassup.Battle.Skills.SkillFiredEvent>(Allocator.Persistent);
            var skillFiredSingleton = _em.CreateEntity();
            _em.AddComponentData(skillFiredSingleton,
                new Wassup.Battle.Skills.SkillFiredEventsSingleton { queue = _skillFiredQueue });
            InstallSkillLayer();

            _dcProcLastImpact.Clear(); // 매치 경계 — 엔티티는 매치마다 새로우니 스로틀 기록 리셋
            _nextSimEntityId = 0;      // battle-sim-extraction M0 unit 1 — stable ID 는 매치마다 0 부터

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
            _killCount = 0;
            ResetGoalStability(); // three-minute-survival unit 0 — 계약 9 (시계와 짝)
            DestroyStructureEntities();  // goal-tower-siege unit 0 — 타워/거점도 매치와 함께 정리
            _waveTimeShift = 0f; // wave-pattern unit 9 — 계약 9 (시계와 짝)
            _waveStartSec = 0f;  // three-minute-survival unit 2 — 계약 9 (시계와 짝)
            _pullsSinceClear = 0; // wave-pull-revival unit 0 — 계약 9 (시계와 짝)
            _spawnGuideForecast = null; // waypoint-routing unit 7 — 계약 9 (시계와 짝)
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
            _pullsSinceClear = 0; // wave-pull-revival unit 0 — 겹침 예산도 매치 경계에서 초기화
            _usingAuthoredPlan = false;
            _activePlan = null;
            _spawnGuideForecast = null;   // waypoint-routing unit 7 — 이전 판 예고 이월 방지

            // 작성 플랜 우선. 변환 실패 시 아래 seed 경로로 fall-through.
            // tutorial-map — 소스 둘: 테스트 모드(_authoredPlan) > 맵 인카운터(_encounterPlan).
            // 테스트 모드가 이기는 이유는 그쪽이 «지금 이 플랜을 보겠다» 는 명시 지시라서다.
            WavePlanAsset plan = _authoredPlan != null ? _authoredPlan : _encounterPlan;
            if (plan != null)
            {
                try
                {
                    _wavePlan = WavePatternGenerator.FromPlanAsset(plan);
                    GameManager.Instance?.Logger?.SetWavePattern(_wavePlan);
                    if (_wavePlan.waves != null && _wavePlan.waves.Count > 0)
                    {
                        _usingAuthoredPlan = true;
                        _activePlan = plan;   // 활성 플랜의 단일 출처 — 소스가 둘이라 필수
                        return true;
                    }
                    Debug.LogWarning($"[BattleBridge] Authored plan '{plan.name}' has no waves; falling back to seed/legacy.", this);
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[BattleBridge] Authored plan '{plan.name}' failed; falling back. {ex.Message}", this);
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
                // unit 2 계약 6 — 브리핑(BuildBriefingWavePlan)과 같은 laneCount 창구를 쓴다.
                _wavePlan = WavePatternGenerator.Generate(ActiveDeck, waveSeed, GeneratorLaneCount);
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
            // wave-pull-revival unit 0 — 겹침 예산은 **필드를 비웠을 때만** 회복한다.
            // capReached 로 넘어온 것은 «정리했다»가 아니라 «못 정리한 채 하나 더 받았다»다.
            if (cleared) _pullsSinceClear = 0;
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

        // wave-concept-blocks unit 5 — 블록 전환 예고용 읽기 전용 창구 2개.
        //
        // 3분에 10~16웨이브면 웨이브당 12~18초이고 리드인은 2초다. 게다가 전멸시 즉시 다음이라
        // 잘 막으면 더 짧아진다 — 컨셉을 읽고 대응할 창이 없다. 블록이 3웨이브라 전환이 판당
        // 4번뿐이므로 그 4번만 미리 알려주면 루프가 성립한다.
        public string NextWaveConceptLabel =>
            NextWaveHasNext ? _wavePlan.waves[_nextWaveIndex].conceptLabel : "";

        // 매 프레임 HUD 로 밀어준다. HUD 가 브리지를 참조하지 않게 하는 기존 방향
        // (scoreHud.SetTopBar / OnEnemyKilled)을 그대로 따른다 — 씬 wiring 이 늘지 않는다.
        private void RefreshTimerHud()
        {
            if (scoreHud == null) return;

            // battle-hud-legibility — 남은시간·웨이브 진행이 좌하단 도크에서 **좌상단 배지**로
            // 옮겨왔다(중앙은 전부 보드라 큰 표기는 코너에만 놓인다).
            // 도크가 직접 읽던 것을 여기서 밀어준다(HUD 는 브리지를 모른다).
            //
            // **진행 중인** 웨이브 번호다. NextWaveNumber(= _nextWaveIndex + 1)는 «다음에 나올»
            // 번호라 그대로 쓰면 3번 웨이브와 싸우는 동안 「웨이브 4」가 뜬다.
            // 총 개수는 클램프 상한으로만 쓰고 화면에 내보내지 않는다(HUD 는 현재 번호만 받는다).
            int current = Mathf.Clamp(NextWaveNumber - 1, 1, Mathf.Max(1, WaveCountTotal));
            scoreHud.SetTopBar(TimerRemaining, TimerDuration, current);
        }


        // 블록 경계 계산을 **브리지가 소유한다.** 도크가 conceptHoldWaves 로 다시 계산하면
        // 두 곳이 갈린다(생성기의 블록 구획과 표시가 어긋나면 예고가 거짓말이 된다).
        public bool NextWaveStartsBlock
        {
            get
            {
                if (!NextWaveHasNext) return false;
                int hold = ActiveDeck != null ? Mathf.Max(1, ActiveDeck.conceptHoldWaves) : 1;
                return _nextWaveIndex % hold == 0;
            }
        }
        // three-minute-survival unit 2 — `NextWaveClearReady`(클리어 강조)는 은퇴했다. 전멸이
        // 곧 자동 진행이라 "눌러라" 라고 알릴 대상이 없다. `_nextWaveClearReady` 내부 상태와
        // `nextwave-clear-attention` 의 도크 어필도 함께 제거.

        // waypoint-routing unit 7 — **마지막으로 큐잉된 웨이브**의 (스웜 × 실제 lane)별
        // 첫 스폰과 경로 입력. QueueWave 가 실제 pending 과 같은 상세 펼침 결과에서 1회 만든다.
        // 반환 배열은 캐시 참조라 수정 금지.
        private SpawnGuideForecast[] _spawnGuideForecast;

        public bool TryGetSpawnGuideForecast(
            out float battleClockSec, out SpawnGuideForecast[] forecasts)
        {
            battleClockSec = (float)_battleClock;
            forecasts = null;
            if (!_running || _spawnGuideForecast == null) return false;
            if (LastSpawnSec(_spawnGuideForecast) <= battleClockSec) return false;
            forecasts = _spawnGuideForecast;
            return true;
        }

        private static float LastSpawnSec(SpawnGuideForecast[] forecasts)
        {
            float last = -1f;
            for (int i = 0; i < forecasts.Length; i++)
                if (forecasts[i].firstSpawnSec > last) last = forecasts[i].firstSpawnSec;
            return last;
        }

        // spawn-point-alert unit 1(rev) — 스폰→골 대표 경로(sim, 셀 중심 나열. [0]=스폰).
        // 유닛 이동과 같은 goal flow field 의 flow 를 셀 단위로 따라간다(타이브레이크 동일).
        // 트레일 표시 시작 시에만 호출되므로(웨이브당 lane 수 회) 캐시 불요. 뷰 변환은 호출측.
        // waypoint-routing unit 7 — 스폰→웨이포인트들→골 대표 경로. 각 구간은 실제
        // MovementSystem 과 같은 (목적지, 통행층) 슬롯·NavGrid·PathSmoothing 을 사용한다.
        public bool TryGetSpawnPathSim(
            int laneIndex,
            int waypointPathIndex,
            byte traversalLayers,
            List<Vector3> outPath)
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
            if (traversalLayers == 0) traversalLayers = TraversalSlots.DefaultMask;
            var navScratch = new NativeArray<byte>(math.max(1, field.CellCount), Allocator.Temp);
            try
            {
                var nav = MovementCellTrim.BuildNavGrid(
                    in field, traversalLayers, hasObstacles, in obstacles, navScratch);

                float radius = agentRadiusTiles * tileSize;
                int2 cell = _generatedMap.spawns[laneIndex];
                float3 pos = GridMath.CellToWorldCenter(
                    cell, field.tileSize, spawnHeight, origin: field.origin);
                outPath.Add(new Vector3(pos.x, pos.y, pos.z));

                // instinct-content unit 3 — 적은 스폰 시 **거점 하나를 목적지로 고른다**.
                // 그 선택을 여기서 다시 하지 않으면 예고선만 마음으로 곧장 뻗고 유닛은 거점으로
                // 꺾어 「가이드 ≠ 실제 이동선」이 된다(사용자 지적 2026-08-12).
                // 선택은 **스폰 위치에서** 일어나므로 웨이포인트를 밟기 전 좌표로 묻는다.
                bool hasStructureLeg = TryResolveFirstStructureDestination(
                    pos, in field, out int2 structureDest);

                int waypointCount = field.WaypointCountAt(waypointPathIndex);
                for (int i = 0; i < waypointCount; i++)
                {
                    int2 waypoint = field.WaypointAt(waypointPathIndex, i);
                    AppendSpawnPathSegment(
                        in field, in nav, waypoint, traversalLayers, radius,
                        ref cell, ref pos, outPath);
                }

                // 거점을 부순 뒤에는 재선정으로 다음 목표(결국 마음)로 이어지므로, 예고선도
                // 「스폰 → 첫 거점 → 마음」 두 구간으로 그린다.
                if (hasStructureLeg)
                    AppendSpawnPathSegment(
                        in field, in nav, structureDest, traversalLayers, radius,
                        ref cell, ref pos, outPath);

                AppendSpawnPathSegment(
                    in field, in nav, FlowFieldSingleton.GoalSentinel, traversalLayers, radius,
                    ref cell, ref pos, outPath);
            }
            finally
            {
                navScratch.Dispose();
            }
            return outPath.Count >= 2;
        }

        // 예고선의 첫 목적지 = **적이 실제로 고르는 그 목적지**.
        //
        // 규칙(`StructureChoice.NearestIndex`)과 정렬 기준(`IsBefore`)을 `StructureDestinationSystem`
        // 과 **함께 쓴다**. 후보를 모으는 코드만 다르다 — 여긴 `EntityManager`, 저긴 `SystemAPI` 라
        // 아키텍처가 강제하는 차이다. 규칙까지 두 벌이 되면 이 버그가 그대로 돌아온다.
        //
        // 마스크는 기본값을 쓴다. 거점 후보의 진영은 마음/본능뿐이고 **기본 마스크와 마음사냥꾼
        // 마스크가 그 둘에 대해 같은 답**을 내므로 오늘 저작된 전 적종이 같은 목적지를 고른다.
        // 거점 종류를 좁게 저작한 적이 생기면 그 적만 예고선과 갈릴 수 있다(그때 웨이브 구성별
        // 예고가 필요해진다).
        private bool TryResolveFirstStructureDestination(
            float3 fromWorld, in Wassup.Battle.Effects.FlowFieldSingleton field, out int2 destCell)
        {
            destCell = default;
            // heart-stress-axis unit 6 — 예고선도 방패 걸린 마음을 제외한다. 안 그러면
            // **예고선은 마음으로 가는 길을 그리는데 적은 본능으로 간다** — 예고가 거짓이 된다.
            // 이 선택은 StructureDestinationSystem 의 사본이고(같은 StructureChoice 를 쓴다),
            // 그래서 배제 규칙도 같이 따라가야 한다.
            using var q = _em.CreateEntityQuery(new EntityQueryDesc
            {
                All = new[] { ComponentType.ReadOnly<Wassup.Battle.Units.StructureTag>() },
                None = new[] { ComponentType.ReadOnly<Wassup.Battle.Units.CoreShielded>() },
            });
            var entities = q.ToEntityArray(Allocator.Temp);
            var cells = new NativeList<int2>(8, Allocator.Temp);
            var world = new NativeList<float2>(8, Allocator.Temp);
            var factions = new NativeList<int>(8, Allocator.Temp);
            var isGoal = new NativeList<bool>(8, Allocator.Temp);
            try
            {
                for (int i = 0; i < entities.Length; i++)
                {
                    var e = entities[i];
                    if (_em.HasComponent<Wassup.Battle.Units.DeadTag>(e)) continue;
                    var tag = _em.GetComponentData<Wassup.Battle.Units.StructureTag>(e);
                    var w = GridMath.CellToWorldCenter(
                        tag.cell, field.tileSize, 0f, origin: field.origin);
                    cells.Add(tag.cell);
                    world.Add(new float2(w.x, w.z));
                    factions.Add((int)tag.faction);
                    isGoal.Add(_em.HasComponent<Wassup.Battle.Units.GoalTowerTag>(e));
                }

                for (int i = 1; i < cells.Length; i++)
                    for (int j = i; j > 0 && StructureChoice.IsBefore(cells[j], cells[j - 1]); j--)
                    {
                        (cells[j], cells[j - 1]) = (cells[j - 1], cells[j]);
                        (world[j], world[j - 1]) = (world[j - 1], world[j]);
                        (factions[j], factions[j - 1]) = (factions[j - 1], factions[j]);
                        (isGoal[j], isGoal[j - 1]) = (isGoal[j - 1], isGoal[j]);
                    }

                int pick = StructureChoice.NearestIndex(
                    new float2(fromWorld.x, fromWorld.z), world.AsArray(), factions.AsArray(),
                    Wassup.Battle.Combat.EnemyTargetDefaults.DefaultEnemyMask);

                // 마음이 뽑히면 구간을 넣지 않는다 — 뒤이어 붙는 골 구간이 그 답이다.
                if (pick < 0 || isGoal[pick]) return false;
                destCell = cells[pick];
                return true;
            }
            finally
            {
                isGoal.Dispose(); factions.Dispose(); world.Dispose();
                cells.Dispose(); entities.Dispose();
            }
        }

        private static bool AppendSpawnPathSegment(
            in FlowFieldSingleton field,
            in NavGrid nav,
            int2 destination,
            byte traversalLayers,
            float radius,
            ref int2 cell,
            ref float3 pos,
            List<Vector3> outPath)
        {
            int slot = field.SlotFor(destination, traversalLayers);
            var lineFlow = field.FlowSlot(slot);
            var lineDist = field.DistSlot(slot);
            int guard = field.CellCount + 1;
            for (int i = 0; i < guard; i++)
            {
                cell = GridMath.WorldToCell(pos, field.tileSize, field.gridSize, origin: field.origin);
                int idx = GridMath.CellIndex(cell, field.gridSize);
                if (idx < 0 || idx >= lineFlow.Length || lineDist[idx] == int.MaxValue) return false;
                if (lineDist[idx] == 0) return true;

                // unit 10 — 목표점 선택 규칙은 MovementSystem 과 공유한다. 여기서 별도
                // 선형 보간/경로 탐색을 만들면 "가이드 ≠ 실제 이동선"이 다시 생긴다.
                if (!PathSmoothing.TryStepTarget(
                        pos, in nav, in lineFlow, radius,
                        PathSmoothing.DefaultLookahead, out float3 next))
                    return false;
                if (math.distancesq(pos, next) <= 1e-8f) return false;
                pos = next;
                outPath.Add(new Vector3(pos.x, pos.y, pos.z));
            }
            return false;
        }

        // wave-pull-revival unit 0 — 당김 상한. 저작이 비면(0 이하) 폴백 3.
        // 0 을 «당김 금지»로 읽지 않는 것이 계약이다 — 저작 누락이 조용히 버튼을 죽이면
        // «왜 안 눌리지»의 원인이 데이터에 있다는 것을 알 방법이 없다.
        private const int PullCapFallback = 3;
        private bool _warnedMissingPullCap;

        private int MaxPullsPerClear
        {
            get
            {
                int authored = ActiveDeck != null ? ActiveDeck.maxPullsPerClear : 0;
                if (authored > 0) return authored;
                if (!_warnedMissingPullCap)
                {
                    _warnedMissingPullCap = true;
                    Debug.LogWarning(
                        $"[BattleBridge] 덱의 maxPullsPerClear 가 저작되지 않았다 — 폴백 {PullCapFallback} 을 쓴다.",
                        this);
                }
                return PullCapFallback;
            }
        }

        // ── 당김: 기제(ForceNextWave)와 규칙(TryPullNextWave)을 나눈다 ──────────────
        //
        // 상한을 ForceNextWave 안에 넣으면 **기존 PlayMode 스모크 3종이 죽는다** — 그들은
        // 이 메서드를 판 진행 동력으로 연타한다(TallyFlowTest 20회 등).
        // 상한은 «플레이어 입력에 대한 게임 규칙»이지 스케줄러의 물리 법칙이 아니므로,
        // 규칙을 한 층 위에 두는 것이 의미와도 맞는다.
        //
        // **플레이어 경로는 TryPullNextWave 하나뿐이다.** UI 에서 ForceNextWave 를 직접
        // 부르지 말 것 — 부르면 상한이 우회된다.
        public bool PullAvailable => NextWaveHasNext;

        // 상한이 회복되는 사건은 **«필드를 비웠다»** 하나다(QueueDueWaves 의 cleared 분기).
        // 작성 플랜(_usingAuthoredPlan)은 저작된 시각 타임라인이 정본이라 그 분기를
        // **구조적으로 지나지 않는다** — 상한을 그대로 걸면 예산이 영영 회복되지 않아
        // 저작 모드에서 3회 뒤 버튼이 영구 잠긴다. 회복 사건이 없는 모드에는 상한도 없다.
        private bool PullCapApplies => !_usingAuthoredPlan;

        public bool PullAllowed =>
            PullAvailable && (!PullCapApplies || _pullsSinceClear < MaxPullsPerClear);
        public int PullsRemaining =>
            !PullAvailable ? 0
            : !PullCapApplies ? MaxPullsPerClear
            : Mathf.Max(0, MaxPullsPerClear - _pullsSinceClear);

        public bool TryPullNextWave()
        {
            if (!PullAllowed) return false;
            ForceNextWave();
            return true;
        }

        // three-minute-survival unit 2 → wave-pull-revival unit 0 — **기제**다. 상한을 보지
        // 않는다(위 TryPullNextWave 가 규칙 층). PlayMode 스모크
        // (TallyFlowTest·MovementIntegritySmokeTest)가 이것을 **판 진행
        // 동력**으로 쓰기 때문에 no-op 으로 만들면 그 테스트들이 타임아웃으로 죽는다.
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
                _pullsSinceClear++; // wave-pull-revival unit 0 — 얹은 것을 센다
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
            // 작성 플랜은 PullCapApplies 로 상한이 면제되므로 이 증가는 표시에도 판정에도
            // 쓰이지 않는다. 그래도 올려둔다 — «얹은 횟수»라는 값의 의미가 경로마다 달라지면
            // 나중에 이 카운터를 다른 곳에서 읽을 때 조용히 틀린다.
            _pullsSinceClear++;
            // spawn-point-alert unit 3 — 예고는 QueueWave 가 이 웨이브 기준으로 채운다.
            // (unit 1 의 "강제 호출은 예고 없이 즉시 스폰" 계약은 리드인 도입으로 폐기 —
            //  당긴 웨이브도 리드인만큼의 예고 창을 갖는다.)
        }

        private void QueueWave(GeneratedWave wave, float baseTriggerTimeSec, bool forced, float elapsedSec)
        {
            // 자동/강제 호출 모두 같은 진입점(전멸 진행·상한 진행·강제 호출·웨이브 1).
            int laneCount = _generatedMap.IsCreated ? _generatedMap.spawns.Length : 1;
            var entries = WavePatternGenerator.ExpandWave(
                wave, baseTriggerTimeSec, laneCount, _wavePlan.intraWaveSpacingSec);
            for (int i = 0; i < entries.Count; i++)
                _pending.Add(new PendingSpawnEntry
                {
                    entry = entries[i].entry,
                    laneIndex = entries[i].laneIndex,
                    pathIndex = entries[i].pathIndex,
                });

            // waypoint-routing unit 7 — 실제 pending 과 **같은 상세 펼침 결과**에서
            // (스웜 × 실제 lane) 예고를 만든다. 시간·lane 규칙을 별도로 재연산하지 않는다.
            // waypoint-flight-enemy unit 11 — 예보도 스폰(SpawnUnit → RouteForSpawn)과 같은
            // 레인 기본 경로를 해석한다. 이걸 빼먹으면 레인 경로 맵(Coil·Zig)에서 가이드만
            // 최단거리를 그린다(사용자 지적 2026-08-15).
            int[] laneRoutes = null;
            if (_generatedMap.IsCreated)
            {
                laneRoutes = new int[laneCount];
                for (int i = 0; i < laneCount; i++) laneRoutes[i] = _generatedMap.RouteForSpawn(i);
            }
            _spawnGuideForecast = WavePatternGenerator.BuildSpawnGuideForecasts(entries, laneRoutes);

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
                    // unit 7a — 실행은 concrete 가 한다. 여기가 남기는 것은 **셈**뿐이다:
                    // `affectedCount` 는 로그 전용이고(호출처는 `out _` 로 버린다) 「몇 기가
                    // 걸렸나」는 실행 전에도 셀 수 있는 **읽기**다. 그래서 fire-and-forget
                    // 포트를 깨지 않고 로그를 지킨다.
                    affectedCount = CountEnemiesInTileRange(tile, GridMath.RangeToTiles(skill.range));
                    CastActiveSkillAtTile(Wassup.Skills.Concrete.TileStatBurstSkill.Id, skill, tile,
                        statSelector: (int)Wassup.Battle.Effects.StatKind.MoveSpeedMul);
                    break;
                case SkillEffectType.Tornado:
                {
                    int tornadoTiles = GridMath.RangeToTiles(skill.range);
                    affectedCount = CountEnemiesInTileRange(tile, tornadoTiles);
                    CastActiveSkillAtTile(Wassup.Skills.Concrete.PullFieldSkill.Id, skill, tile);
                    // 연출은 호출자 몫이다(계약 6 — 판 밖 요소). 소용돌이 링은 시전
                    // 지점이 이미 아는 값(중심·반경·지속)으로 그린다.
                    if (vfxSpawner != null)
                    {
                        var tornadoWorld = GridToWorldCenter(tile);
                        vfxSpawner.SpawnTornado(
                            new Vector3(tornadoWorld.x, 0f, tornadoWorld.z),
                            tornadoTiles * tileSize, skill.durationSec);
                    }
                    break;
                }
                case SkillEffectType.Meteor:
                {
                    // ⚠ **저작 오류라도 시전은 성공이다**(레거시 동작 — 그물이 박아 뒀다).
                    // 떨어질 것이 없을 뿐 쿨다운은 소모되고 로그도 남는다. 조용히 넘기지
                    // 않는 것은 여기서 짖기 때문이고, 스킬은 발화조차 안 한다.
                    if (skill.projectile == null)
                    {
                        Debug.LogWarning($"[BattleBridge] Skill '{skill.id}' has no ProjectileData assigned; meteor cast dropped.");
                        break;
                    }
                    int meteorTiles = GridMath.RangeToTiles(skill.range);
                    affectedCount = CountEnemiesInTileRange(tile, meteorTiles);
                    CastActiveSkillAtTile(Wassup.Skills.Concrete.TileMeteorSkill.Id, skill, tile,
                        dataIndex: GetOrCreateProjectileDataIndex(skill.projectile),
                        visualScale: skill.projectile.visualScale,
                        duration: skill.warningSec > 0f ? skill.warningSec : 0f);
                    break;
                }
                // active-ally-zone unit 1 — 아군 버프는 **시간제 장판**이다(즉시 버프 폐기).
                // 빈 칸에도 놓을 수 있어 적 장판과 규칙이 같아졌다 — 0기 거절은 폐기.
                case SkillEffectType.PowerSurge:
                    affectedCount = CountAlliesInTileRange(tile, GridMath.RangeToTiles(skill.range));
                    CastActiveSkillAtTile(Wassup.Skills.Concrete.AllyBuffFieldSkill.Id, skill, tile,
                        statSelector: (int)Wassup.Battle.Effects.StatKind.DamageMul);
                    break;
                case SkillEffectType.RapidFire:
                    affectedCount = CountAlliesInTileRange(tile, GridMath.RangeToTiles(skill.range));
                    CastActiveSkillAtTile(Wassup.Skills.Concrete.AllyBuffFieldSkill.Id, skill, tile,
                        statSelector: (int)Wassup.Battle.Effects.StatKind.AttackSpeedMul);
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
            // unit 7e — **이름표가 아니라 명세를 본다**(사용자 결정 2026-08-26).
            // 「두 칸을 받는다」는 그 스킬의 조준 사양이고, 새 두 칸 스킬이 생겨도
            // 이 줄은 그대로다.
            if (!skill.NeedsTwoTiles) return false;
            // 두 칸 조준의 규칙 — **같은 칸이면 거절**. 이것도 이름표가 아니라 조준 사양에
            // 딸린 규칙이라, 두 칸을 받는 스킬이 늘어도 여기가 그대로 답한다.
            //
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

            // unit 7c — 실행은 concrete 가 한다. 입구==출구 거절은 **여기 남는다** —
            // 그건 두 번 탭하는 입력의 규칙이고 UI 도 같은 판정으로 조준을 거절한다.
            affectedCount = 1;   // 로그 전용. 포탈은 대상 수가 아니라 링크 1개다.
            CastActiveSkillAtTile(Wassup.Skills.Concrete.PortalSkill.Id, skill, entryTile,
                tileB: exitTile, hasCellB: true);
            // 연출은 호출자 몫(계약 6) — 두 소용돌이와 잇는 빔.
            if (vfxSpawner != null)
            {
                var pEntry = GridToWorldCenter(entryTile);
                var pExit = GridToWorldCenter(exitTile);
                vfxSpawner.SpawnPortal(
                    new Vector3(pEntry.x, 0f, pEntry.z),
                    new Vector3(pExit.x, 0f, pExit.z),
                    skill.durationSec);
            }
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
        // skill-layer-migration unit 7b — `SpawnAllyBuffZone` 과 `PaintAllyBuffZone` 은
        // 은퇴했다. 스폰은 concrete 가 하고 점등은 `DrainAllyBuffZoneVisuals` 의 양방향
        // 재조정이 한다 — **시전 시점의 캐리어 핸들이 더는 필요 없다.**

        // active-ally-zone unit 2 — 장판 점등 등록부(캐리어 엔티티 → 칠한 셀). 만료는 ECS 가
        // 엔티티를 파괴해서 알리므로, 뷰 회수는 프레임 재조정으로 한다(bridge 책임 = 시각 드레인).
        // 셀 목록을 들고 있지 않고 (중심, 반경)만 기억한다 — 캐스트마다 List 를 새로 만들지 않고,
        // 회수 시 같은 규칙으로 다시 만든다(칠한 것과 반납하는 것이 같은 함수에서 나오게).
        private readonly Dictionary<Entity, (Vector2Int center, int tileRange)> _allyZonePaint = new();
        private readonly List<Vector2Int> _zoneCellScratch = new List<Vector2Int>();
        private readonly List<Entity> _zoneGoneScratch = new List<Entity>();

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
            // skill-layer-migration unit 7b — **점등도 여기서 맞춘다.**
            // 예전엔 시전 지점이 캐리어 엔티티를 **반환받아** 등록했는데, 실행이 스킬
            // 레이어로 가면서 그 반환값이 사라졌다. 그런데 이 함수는 이미 「살아 있는
            // 캐리어와 내 목록을 맞춘다」를 하고 있었다 — 반납만 하던 것을 **양방향**으로
            // 만들면 반환값이 필요 없다(셈을 preview 로 푼 것과 같은 해법).
            //
            // ⚠ 캐리어가 `centerCell`·`tileRange` 를 들고 있어서 가능하다. 시전 시점의
            // 지식이 아니라 **캐리어 자신의 상태**로 칠한다.
            if (_allyZonePaint.Count == 0 && !HasLiveEntityManager()) return;
            if (HasLiveEntityManager() && tilemapMapView != null)
            {
                using var newQ = _em.CreateEntityQuery(
                    ComponentType.ReadOnly<Wassup.Battle.Effects.AllyBuffField>());
                var live = newQ.ToEntityArray(Allocator.Temp);
                var liveData = newQ.ToComponentDataArray<Wassup.Battle.Effects.AllyBuffField>(Allocator.Temp);
                for (int i = 0; i < live.Length; i++)
                {
                    if (_allyZonePaint.ContainsKey(live[i])) continue;
                    var c = new Vector2Int(liveData[i].centerCell.x, liveData[i].centerCell.y);
                    BuildZoneCells(c, liveData[i].tileRange, _zoneCellScratch);
                    tilemapMapView.AddZoneCells(_zoneCellScratch);
                    _allyZonePaint[live[i]] = (c, liveData[i].tileRange);
                }
                live.Dispose(); liveData.Dispose();
            }
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

        // battle-sim-extraction — 하네스 배치가 「닿는 자리」를 고르려면 골이 어디인지 알아야 한다.
        // 브리지가 이미 소유한 값을 얇게 노출할 뿐이다(제약 12 판단 순서의 (b)).
        // `goals` 미생성/빈이면 단일 `goal` 폴백 — `AnyEnemyWithinTilesOfGoal` 과 같은 규약.
        public Vector2Int[] DebugGoalCells
        {
            get
            {
                if (!_generatedMap.IsCreated) return System.Array.Empty<Vector2Int>();
                if (_generatedMap.goals.IsCreated && _generatedMap.goals.Length > 0)
                {
                    var outCells = new Vector2Int[_generatedMap.goals.Length];
                    for (int i = 0; i < outCells.Length; i++)
                        outCells[i] = new Vector2Int(_generatedMap.goals[i].x, _generatedMap.goals[i].y);
                    return outCells;
                }
                return new[] { new Vector2Int(_generatedMap.goal.x, _generatedMap.goal.y) };
            }
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

        // unit 7a — 액티브 발화. 시전 주체가 없으므로 `Caster` 는 비운다 —
        // 진영은 디스패처가 `CasterRef.Player` 로 접어 준다(플레이어 = 방어유닛 편).
        // ⚠ **부착 seam 을 쓴다.** 액티브도 동기 트랜잭션이다(쿨다운 게이트 → 실행 →
        // `Consume` + 로그). 프레임을 기다리면 소모 뒤에 실행이 도착한다.
        private void CastActiveSkillAtTile(int skillId, SkillData skill, Vector2Int tile,
            int statSelector = 0, Vector2Int tileB = default, bool hasCellB = false,
            int dataIndex = -1, float visualScale = 0f, float? duration = null)
        {
            if (!_skillFiredQueue.IsCreated) return;
            _skillFiredQueue.Enqueue(new Wassup.Battle.Skills.SkillFiredEvent
            {
                Seam = Wassup.Battle.Skills.SkillSeam.Immediate,
                Caster = Entity.Null,
                SkillId = skillId,
                TargetCellA = new int2(tile.x, tile.y),
                TargetCellB = new int2(tileB.x, tileB.y),
                HasCellB = hasCellB,
                Magnitude = skill.magnitude,
                // 메테오만 지속이 **낙하 예고**다 — 저작 필드가 다르다.
                Duration = duration ?? skill.durationSec,
                TileRange = GridMath.RangeToTiles(skill.range),
                StatSelector = statSelector,
                DataIndex = dataIndex,
                VisualScale = visualScale,
            });
            RunImmediateSkills();
        }

        // 로그 preview — 「이 칸 반경 안 아군 수」. 판정에 쓰지 않는다(빈 칸 시전도 성공).
        private int CountAlliesInTileRange(Vector2Int tile, int tileRange)
        {
            CollectAlliesInRange(tile, tileRange, _allyLogScratch);
            return _allyLogScratch.Count;
        }

        // 로그 preview — 「이 칸 반경 안 적 수」. 판정에 쓰지 않는다.
        private int CountEnemiesInTileRange(Vector2Int tile, int tileRange)
        {
            if (!_aliveAttackersQueryCreated) return 0;
            var entities = _aliveAttackersQuery.ToEntityArray(Allocator.Temp);
            int n = 0;
            for (int i = 0; i < entities.Length; i++)
            {
                var e = entities[i];
                if (!_em.HasComponent<LocalTransform>(e)) continue;
                if (InTileRange(_em.GetComponentData<LocalTransform>(e).Position, tile, tileRange)) n++;
            }
            entities.Dispose();
            return n;
        }

        // skill-layer-migration unit 7a~7c — `ApplySlowField` · `ApplyTornado` ·
        // `ApplyPortal` 은 은퇴했다. 실행은 concrete 가 하고, 이 파일에 남은 것은
        // 로그 preview(셈)와 연출뿐이다.


        // projectile-trajectory-payload unit 7 — Meteor rides the unified projectile
        // lifecycle (SkyFall × TileAoe, flightTime = warningSec). The request is
        // built HERE (not EffectSpawner): the ProjectileData registry is bridge-
        // private and ProjectileSpawnRequest is Combat-owned, so the bridge is the
        // only seam that can emit it without a context-boundary violation. No ECS
        // carrier entity — SpawnProjectile is called directly (legacy meteor-carrier
        // path removed in unit 8).
        // skill-layer-migration unit 7d — `ApplyMeteor` 는 은퇴했다. 예고는 이제
        // **앞으로 흐르는 값**이다(`ProjectileSpawnRequest.telegraphTileRange`) —
        // 스폰된 엔티티를 돌려받을 필요가 없어졌다.


        private void Update()
        {
            // battle-sim-extraction M0 unit 2 — 하네스 구동 중에는 스텝이 몬다(상호 배타).
            // 여기서 막지 않으면 렌더 프레임과 스텝이 **둘 다** 시계를 밀어 `_battleClock`
            // 이 두 번 전진한다 — 「거의 결정론」이 되고 그건 결정론이 아니다.
            if (Wassup.Core.TimeControl.SimHarnessClock.Active) return;
            TickBattleFrame();
        }

        // 한 프레임(또는 한 스텝)의 배틀 진행. 라이브는 `Update` 가, 하네스는
        // `StepOneTick` 이 부른다. 시간 원천은 **양쪽 다 `TimeManager` 도메인 델타**라
        // 이 본문은 자기가 어느 쪽에 실려 도는지 모른다(그래서 두 경로가 갈리지 않는다).
        private void TickBattleFrame()
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
            // elite-enemy-tier unit 5 — ★**QueueDueWaves 보다 먼저** 드레인한다. 분열 자식이
            // 여기서 태어나기 때문이다. 뒤에 두면(원래 위치) 부모 슬라임이 마지막 생존 적일 때
            // 자식이 생기기 전에 NoQueuedAttackersRemain() 이 참이 되어 다음 웨이브가 큐잉된다
            // — 「엘리트를 죽이면 판이 빨라지는」 뒤집힌 인센티브가 된다. (구 CheckVictory 도
            // 같은 술어라 승리까지 선언됐었다 — 그 판정은 kill-race unit 0 에서 은퇴.) 이 드레인은 다른 드레인·스폰 루프에 의존하지
            // 않는다(킬 버스트는 SpawnProjectile 직접 호출, 점수·각성 중계는 순수 가산,
            // 등록부 정리와 표식 회수는 순서 무관). 자식은 ECB 가 아니라 직접 AddComponent 라
            // 같은 프레임에 즉시 _aliveAttackersQuery 에 들어온다.
            DrainEnemyKilledEvents();
            QueueDueWaves(t);
            RefreshTimerHud();
            for (int i = _pending.Count - 1; i >= 0; i--)
            {
                if (t >= _pending[i].entry.triggerTimeSec)
                {
                    SpawnUnit(_pending[i]);
                    _pending.RemoveAt(i);
                }
            }

            // bonus-wave-pull unit 4 — 보너스 스폰 펌프. 여기(TickBattleFrame) 안이어야
            // sim 하네스(StepOneTick)와 라이브가 같은 경로를 탄다. 시각은 배틀 도메인 시계 t.
            TickBonusWave(t);

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
            // DrainEnemyKilledEvents 는 이 자리에서 Update 최상단(QueueDueWaves 앞)으로 옮겼다
            // — elite-enemy-tier unit 5. 되돌리면 분열이 웨이브 회전을 앞당긴다.
            DrainAttackOutputLogEvents();
            DrainHazardSpawnRequests();
            DrainPatrolSpawnRequests(); // summon-patrol-defender unit 3 — 소환 요청 캐리어
            DrainMeteorBarrageRequests(); // season-gimmick-clockout unit 4 — 사직서 임계 메테오 barrage
            DrainHazardRuntimeEvents();
            DrainHazardDestroyedEvents();
            DrainGoalCollapsedEvents();
            DrainGoalEvents();
            SyncGoalStability(); // goal-tower-siege — 타워 Health → 미러(연출·로그 전용)
            // bonus-wave-pull unit 9 — 등장 래치. ★**SyncGoalStability 바로 뒤**여야 한다 —
            // 스트레스 게이트가 이 프레임의 마음 체력을 보고 판정한다. 앞에 두면 한 프레임 묵은
            // 값으로 열리고 닫히는데, 문턱 근처에서는 그 한 프레임이 곧 떨림이다.
            TickBonusPullOffer();
            // three-minute-kill-race unit 0 — 판을 끝내는 것은 시계 하나다.
            // (적 마음 붕괴·웨이브 전멸 판정은 은퇴했다.)
            CheckTimer();
        }

        // battle-sim-extraction M0 unit 4 — 골든이 **exact** 로 보는 최종 정수.
        // 점수는 「1킬 = 1점」이라 kills 와 같은 값이다(three-minute-kill-race unit 1) —
        // 두 필드로 두는 이유는 그 등식이 룰 변경으로 깨지는 날 골든이 먼저 알려야 하기 때문.
        public void ReadFinalTally(out int kills, out int score, out int leaks)
        {
            kills = _killCount;
            score = _killCount;
            leaks = _rusherArrivalCount;
        }

        // battle-sim-extraction M0 unit 4 — 관측 탭이 쓰는 축 변환. 기록은 `Entity` 를
        // 절대 싣지 않는다(프로세스 밖에서 의미가 없다) — `SimEntityId` 하나가 축이다.
        private int SimIdOf(Entity e)
            => e != Entity.Null && _em != default && _em.Exists(e)
               && _em.HasComponent<Wassup.Battle.Units.SimEntityId>(e)
                ? _em.GetComponentData<Wassup.Battle.Units.SimEntityId>(e).value
                : -1;

        // battle-sim-extraction M0 unit 3 — 이번 판의 조건 스냅샷(+ 해시).
        // 골든 헤더와 하네스 보고서가 이 해시를 싣는다. 다르면 코드 회귀가 아니라
        // **조건 드리프트**(대개 시트 임포트가 SO 를 덮은 것)라는 뜻이다.
        private Wassup.Core.MatchConfigSnapshot _matchConfig;

        public string MatchConfigHash => _matchConfig.hash;
        public string MatchConfigText => _matchConfig.text;

        // 수집 범위의 기준은 「게임 결과에 영향을 주는가」 하나다. 뷰 전용 knob(블롭 그림자·
        // lift·프랍 예산·틸트 등 60여 개)은 담지 않는다 — 담으면 연출 튜닝이 «조건이 바뀌었다»로
        // 읽혀 판독 장치가 거짓말을 한다. 분류표는 spec 3번 문서에 있다.
        private Wassup.Core.MatchConfigSnapshot CollectMatchConfig()
        {
            var w = new Wassup.Core.MatchConfigWriter();

            w.Section("header");
            w.Put("schema", 1);
            w.Put("matchSeed", _matchSeed);

            w.Section("map");
            w.Put("seed", _generatedMap.IsCreated ? _generatedMap.seed : 0);
            w.Put("generatorVersion", _generatedMap.IsCreated ? _generatedMap.generatorVersion : 0);
            if (_generatedMap.IsCreated)
            {
                w.Put("gridX", _generatedMap.gridSize.x);
                w.Put("gridY", _generatedMap.gridSize.y);
                // 타일·배치층은 결과를 바꾸는 셀 데이터다 — 원소를 다 접는 대신 배열 자체를
                // 해시에 태운다(길이 + 바이트 합산이 아니라 원소 순서를 그대로 문자열로).
                var tiles = new System.Text.StringBuilder(_generatedMap.tiles.Length);
                for (int i = 0; i < _generatedMap.tiles.Length; i++) tiles.Append((int)_generatedMap.tiles[i]);
                w.Put("tiles", tiles.ToString());
                if (_generatedMap.placeMask.IsCreated)
                {
                    var pm = new System.Text.StringBuilder(_generatedMap.placeMask.Length * 2);
                    for (int i = 0; i < _generatedMap.placeMask.Length; i++) pm.Append(_generatedMap.placeMask[i]).Append(',');
                    w.Put("placeMask", pm.ToString());
                }
                for (int i = 0; i < _generatedMap.spawns.Length; i++)
                    w.Put($"spawn[{i}]", $"{_generatedMap.spawns[i].x},{_generatedMap.spawns[i].y}");
                if (_generatedMap.goals.IsCreated)
                    for (int i = 0; i < _generatedMap.goals.Length; i++)
                        w.Put($"goal[{i}]", $"{_generatedMap.goals[i].x},{_generatedMap.goals[i].y}");
                else w.Put("goal[0]", $"{_generatedMap.goal.x},{_generatedMap.goal.y}");
                if (_generatedMap.waypointCells.IsCreated)
                {
                    var wp = new System.Text.StringBuilder();
                    for (int i = 0; i < _generatedMap.waypointCells.Length; i++)
                        wp.Append(_generatedMap.waypointCells[i].x).Append(':').Append(_generatedMap.waypointCells[i].y).Append(',');
                    w.Put("waypoints", wp.ToString());
                }
                if (_generatedMap.structures.IsCreated)
                    w.Put("structureCount", _generatedMap.structures.Length);
                // bonus-wave-pull unit 4(계약 2) — 포탈 칸은 결과를 바꾼다(보너스 적이 어디서
                // 나오는지가 곧 사냥 경로다). 안 담으면 포탈을 옮겨도 configHash 가 안 움직여
                // 드리프트 판독기가 「조건 무변화」라고 거짓말한다.
                if (_generatedMap.bonusSpawns.IsCreated)
                    for (int i = 0; i < _generatedMap.bonusSpawns.Length; i++)
                        w.Put($"bonusSpawn[{i}]",
                            $"{_generatedMap.bonusSpawns[i].x},{_generatedMap.bonusSpawns[i].y}");
            }

            w.Section("deck");
            w.PutAsset("deck", ActiveDeck);
            // 같은 이유로 보너스 웨이브 저작도 담는다 — 마리수·임계·간격 전부 결과를 바꾼다.
            w.PutAsset("bonusWave", bonusWaveData);
            // ★적 SO 는 **따로** 담아야 한다. PutAsset 의 Describe 는 참조 필드를 «이름까지만»
            // 접으므로 위 한 줄엔 `enemyUnit = "Enemy_DreamShard"` 문자열만 들어간다. 그리고
            // 이 적은 계약 4 때문에 웨이브 플랜에 절대 안 실려 아래 [enemies] 섹션에도 없다 —
            // 즉 담지 않으면 시트가 이 적의 체력을 10배로 올려도 configHash 가 안 움직인다.
            w.PutAsset("bonusEnemy", bonusWaveData != null ? bonusWaveData.enemyUnit : null);

            w.Section("waves");
            w.Put("usingGenerated", _usingGeneratedWaves);
            w.Put("usingAuthored", _usingAuthoredPlan);
            w.Put("timerDuration", _timerDuration);
            if (_wavePlan.waves != null)
            {
                w.Put("seed", _wavePlan.seed);
                w.Put("generatorVersion", _wavePlan.generatorVersion);
                w.Put("waveInterval", _wavePlan.waveIntervalSec);
                w.Put("intraWaveSpacing", _wavePlan.intraWaveSpacingSec);
                w.Put("spawnLeadIn", _wavePlan.spawnLeadInSec);
                w.Put("waveCount", _wavePlan.waves.Count);
                // **생성 결과**를 담는다(생성기 입력이 아니라). 웨이브 생성은 셋업 난수를
                // 쓰므로(UnityEngine.Random) 결과를 물질화해야 sim 상류가 격리된다 —
                // 이게 이 unit 이 「생성기 seed 만 담으면 안 된다」고 말하는 이유다.
                for (int i = 0; i < _wavePlan.waves.Count; i++)
                {
                    var wave = _wavePlan.waves[i];
                    w.Put($"w{i}.t", wave.triggerTimeSec);
                    w.Put($"w{i}.mode", (int)wave.expandMode);
                    w.Put($"w{i}.interval", wave.spawnIntervalSec);
                    w.Put($"w{i}.concept", wave.conceptLabel);
                    int gn = wave.groups != null ? wave.groups.Count : 0;
                    w.Put($"w{i}.g", gn);
                    for (int j = 0; j < gn; j++)
                    {
                        var g = wave.groups[j];
                        w.Put($"w{i}.g{j}",
                            $"{(g.unit != null ? g.unit.name : "~")}x{g.count}@{g.triggerOffsetSec.ToString("R", System.Globalization.CultureInfo.InvariantCulture)}" +
                            $"/lane{g.laneIndex}/path{g.pathIndex}");
                    }
                }
            }

            // 적 로스터는 **이번 판의 웨이브 플랜에 실제로 등장하는 것만** 접는다. 덱 전체를
            // 담으면 이 판이 쓰지도 않는 적의 스탯 변경이 해시를 흔들어, 판독 장치의 신호가
            // 그만큼 무뎌진다.
            w.Section("enemies");
            var seenEnemies = new System.Collections.Generic.List<Wassup.Data.AttackUnitData>();
            if (_wavePlan.waves != null)
                foreach (var wave in _wavePlan.waves)
                    if (wave.groups != null)
                        foreach (var g in wave.groups)
                            if (g.unit != null && !seenEnemies.Contains(g.unit)) seenEnemies.Add(g.unit);
            seenEnemies.Sort((a, b) => string.CompareOrdinal(a.name, b.name)); // 등장 순서에 기대지 않는다
            w.Put("n", seenEnemies.Count);
            for (int i = 0; i < seenEnemies.Count; i++) w.PutAsset($"e{i}", seenEnemies[i]);

            w.Section("defenders");
            int dn = defenderPool != null ? defenderPool.Length : 0;
            w.Put("n", dn);
            for (int i = 0; i < dn; i++) w.PutAsset($"d{i}", defenderPool[i]);

            w.Section("gimmick");
            w.PutAsset("assigned", _assignedGimmick);

            w.Section("stackModifiers");
            int sn = stackModifierAuthoring != null ? stackModifierAuthoring.Length : 0;
            w.Put("n", sn);
            for (int i = 0; i < sn; i++) w.PutAsset($"s{i}", stackModifierAuthoring[i]);

            // 씬 상주 gameplay knob. 이 목록이 이 unit 의 실질이다 — 스탯 SO 만 스냅샷하면
            // 「같은 SO 인데 결과가 다르다」가 남는다(스폰 spread·인접 시너지가 그 예).
            w.Section("sceneKnobs");
            w.Put("tileSize", tileSize);
            w.Put("spawnHeight", spawnHeight);
            w.Put("agentRadiusTiles", agentRadiusTiles);
            w.Put("spawnSpreadEnabled", spawnSpreadEnabled);
            w.Put("spawnSpreadFraction", spawnSpreadFraction);
            w.Put("spawnSpreadTopScale", spawnSpreadTopScale);
            w.Put("spawnSubLaneCount", spawnSubLaneCount);
            w.Put("enableAdjacencySynergy", enableAdjacencySynergy);
            w.Put("dcProcImpactMinIntervalSec", dcProcImpactMinIntervalSec);
            w.Put("fixedMapSeed", fixedMapSeed);

            return w.Build();
        }

        // battle-sim-extraction M0 unit 2 — 고정 스텝 1회. 하네스(에디터 메뉴·검증 러너)가
        // `SimHarnessClock.Begin(dt)` 뒤에 이 메서드를 N 번 부른다.
        //
        // **순서가 이 메서드의 전부다.** 라이브 플레이어 루프는
        // `MonoBehaviour.Update` → `SimulationSystemGroup`(= BattleSimGroup) → `LateUpdate`
        // 순으로 돈다(그 사실은 아래 `LateUpdate` 주석이 이미 근거로 쓰고 있다: 「ECS 시뮬은
        // MonoBehaviour.Update 뒤에 돈다」). 그래서 스텝도 **Bridge 먼저, ECS 나중**이다.
        // 뒤집으면 ECS 가 만든 캐리어를 같은 스텝에서 드레인하게 되어 한 틱 빠른 세상이
        // 되고, 그 위에서 뜬 골든은 라이브가 한 번도 낸 적 없는 궤적을 정본이라 우긴다.
        //
        // 뷰(`LateUpdate`)는 부르지 않는다 — 프레젠테이션이고 풀·코루틴을 스텝 안으로
        // 끌고 들어온다. 하네스가 재는 것은 sim 이다.
        public void StepOneTick()
        {
            if (!Wassup.Core.TimeControl.SimHarnessClock.Active)
            {
                Debug.LogWarning("[BattleBridge] StepOneTick 은 SimHarnessClock.Begin 이후에만 유효하다.");
                return;
            }

            float dt = Wassup.Core.TimeControl.SimHarnessClock.StepDt;

            // ① 자기 Update 로 돌던 배틀 런타임들. 스펙 스케치는 `SkillRuntime` 만 꼽았지만
            // **코스트와 배치 쿨타임도 같은 부류**다 — 셋 다 `TimeManager` 배틀 델타로
            // self-tick 하고, 셋 다 «입력이 통과하느냐» 를 게이트한다. 하나라도 스텝 밖에
            // 남으면 같은 틱의 같은 입력이 두 판에서 다른 판정을 받는다(코스트 부족 →
            // 배치 거부). 실제로 이 셋을 넣기 전에는 하네스 배치 입력이 매번 거부됐다.
            var gm = GameManager.Instance;
            if (gm != null)
            {
                if (gm.CostRuntime != null) gm.CostRuntime.Tick(dt);
                if (gm.CooldownRuntime != null) gm.CooldownRuntime.Tick(dt);
            }
            if (skillRuntime != null) skillRuntime.Tick(dt);

            // ② Bridge 프레임(시계·웨이브·스폰·drain) → ③ ECS 1스텝. 이 순서가 라이브다.
            TickBattleFrame();

            Wassup.Battle.BattleSimGroup group = ResolveBattleSimGroup();
            if (group == null) return;
            Wassup.Core.TimeControl.SimHarnessClock.RequestStep();
            group.Update(); // rate manager 가 요청을 소비해 정확히 1회 전진한다.
        }

        private Wassup.Battle.BattleSimGroup _battleSimGroupCache;

        private Wassup.Battle.BattleSimGroup ResolveBattleSimGroup()
        {
            if (_battleSimGroupCache != null) return _battleSimGroupCache;
            if (_world == null || !_world.IsCreated) return null;
            _battleSimGroupCache = _world.GetExistingSystemManaged<Wassup.Battle.BattleSimGroup>();
            return _battleSimGroupCache;
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
                    // dreamcatcher-content-4 — 궤도구만 **보드 깊이 소팅**을 받는다.
                    // 나머지 탄은 «날아가 사라지는 것» 이라 항상 위(플랫 +1000)가 맞지만,
                    // 유닛을 도는 구슬은 뒤로 갔을 때 몸에 가려야 «돈다» 로 읽힌다.
                    //
                    // 깊이는 셀 Y 다 — 유닛과 **같은 식**(BoardSortOrder.Compute)을 써야
                    // 서로 끼어들 수 있다. offset 3 = 같은 셀 tie 를 피하면서, 한 칸 뒤면
                    // −10 대역(확실히 뒤), 한 칸 앞이면 +10 대역(확실히 앞)이 되게 하는 값.
                    // 좌우 극점은 살짝 앞으로 읽힌다(구슬이 옆에 있을 땐 어느 쪽이든 무방).
                    // ⚠ **sim 좌표를 넘긴다 — view 좌표가 아니다.** `BoardSpace.ToView` 는
                    // 평면 보드라 sim-Y 를 drop 하고 z 를 화면 높이로 접어서, view 로 셀을
                    // 역산하면 행 정렬이 통째로 무너진다(`SpineUnitView.UpdateSortingOrder`
                    // 가 같은 이유로 `_simWorld` 를 쓴다 — 그 주석이 이 함정을 경고한다).
                    if (state.movement == MovementKind.OrbitAroundPoint && _generatedMap.IsCreated)
                    {
                        var simPos = frame.simPosition;
                        frame.boardSortOrder = Wassup.Presentation.BoardSortOrder.ComputeFromWorld(
                            _generatedMap.gridSize,
                            new Vector3(simPos.x, simPos.y, simPos.z),
                            tileSize,
                            offset: Wassup.Presentation.BoardSortOrder.CharacterOffset + 2);
                    }
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
                        // ★**두 쿼리를 함께** 되살린다 — 한쪽만 만들면 전멸 판정 쿼리가 stale 인 채
                        // 남아 NoQueuedAttackersRemain() 에서 그 판의 웨이브 진행이 멎는다.
                        CreateAliveAttackerQueries();
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
                            // waypoint-routing unit 4 — 상시 비행 lift 는 SO→기존 적 등록부를
                            // 따라 view 로만 흐른다. sim 위치/타게팅에는 손대지 않는다.
                            float flightLift = _enemyTypeByEntity.TryGetValue(entity, out var enemyType)
                                && enemyType != null
                                ? Mathf.Max(0f, enemyType.flightLift)
                                : 0f;
                            float viewFlightHeight = flightLift + (leaping ? leapHeight : 0f);
                            // unit-health-display unit 1 — 적 저체력 틴트. HP read-only 평가는
                            // BattleBridge 소관(ECS 창구), 뷰는 Color 만 받아 적용.
                            Color tint = unifiedOverhead ? Color.white : EvaluateEnemyHealthTint(entity);
                            // placement-enemy-see-through unit 3 — 적만 dim(디펜더 루프는 미적용).
                            // SetDimmed 를 SetHealthTint 앞에 — quad 는 SetHealthTint 가 알파를 반영한다.
                            bool dimmed = _enemyDimAlpha < 0.999f;
                            if (spineUnitPool != null && spineUnitPool.TryGet(entity, out var spineView))
                            {
                                // 지상·비도약이면 0 을 써서 스스로 해제된다(별도 clear 경로 불필요).
                                spineView.SetFlightHeight(viewFlightHeight);
                                spineView.UpdatePosition(world);
                                if (canSort) spineView.UpdateSortingOrder(gridSize, tileSize);
                                spineView.SetDimmed(dimmed, _enemyDimAlpha);
                                spineView.SetHealthTint(tint);
                            }
                            else if (enemyViewPool.TryGet(entity, out var view))
                            {
                                view.SetFlightHeight(viewFlightHeight);
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
                                    enemyScreenAnchor, ProjectTileScreenWidth(enemyAnchor),
                                    ShieldRatioOf(entity, h), GatherOverheadStacks(entity));
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
                // defender-footprint unit 2 — 짝수 변 footprint 는 **뷰만** 기하 중심(+0.5칸)에 선다.
                // sim 위치(대표 셀 중심)는 불변 — README 계약 2. 홀수/1×1 은 오프셋 0.
                var fpOff = FootprintViewOffset(kv.Value.data);
                p.x += fpOff.x;
                p.z += fpOff.y;
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
                    SyncSummonerAnimationState(entity, kv.Value.data, spineView);
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
                    unitOverheadUiLayer.SetUnit(entity, true, Health.ComputeRatio(h.value, h.max),
                        defenderScreenAnchor, ProjectTileScreenWidth(defenderAnchor),
                        ShieldRatioOf(entity, h), GatherOverheadStacks(entity));
                }
            }
            // goal-stability unit 5 — 골 게이지도 유닛과 같은 오버헤드 창(Begin/EndFrame) 안에서 Set.
            SyncGoalOverheadGauges(unifiedOverhead);
            SyncBlockingHazardOverheadGauges(unifiedOverhead); // bomb-barrel-on-place unit 8 — 설치물 체력 바
            SyncPatrolViews(unifiedOverhead, canSort, gridSize);
            if (unifiedOverhead) unitOverheadUiLayer.EndFrame();
            // three-minute-survival unit 1 — 골 안정도 바. EndFrame 뒤에 둔다(유닛 풀의
            // _seen 소거와 무관한 별도 슬롯이라 순서 의존은 없지만, 유닛 바 위에 그려진다).
        }

        // three-minute-kill-race unit 2 — `SyncGoalStabilityBars()` 는 제거했다.
        // ⚠ 그 시절 계약(「바·숫자로 그리지 않는다」)은 heart-stress-axis 가 뒤집었다.
        // 지금 마음의 상태를 그리는 것은 넷이다 — 셋은 **연출**, 하나는 **정보**다:
        //   연출 — 프랍 붉은 틴트(`SetGoalStressTint`) · 심박 · 화면 포스트 비네트
        //   정보 — **머리 위 스트레스 바**(unit 9 rev 2). 차오르는 0~100 이고 fill 이
        //          **파랑 → 빨강**으로 램프하며(스킨 저작), 오를 때마다 크기로 튄다.
        //          전용 함수가 아니라 `SyncGoalOverheadGauges` 공용 경로를 그대로 탄다.
        // 머리 위 숫자 `87 / 100`(unit 8)는 unit 9 에서 **껐다**(ScoreHudView 토글, 기본 false).
        // `SetGoalCrack`(균열 단계)은 호출처 0 인 휴면이다 — 여기서 찾지 말 것.

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
                    unitOverheadUiLayer.SetUnit(entity, true, Health.ComputeRatio(h.value, h.max),
                        garScreenAnchor, ProjectTileScreenWidth(garAnchor),
                        ShieldRatioOf(entity, h), GatherOverheadStacks(entity));
                }
            }
        }

        // shield-guardian-defender unit 2 — 실드합 동승(read-only 폴링, 계약 8).
        // 정규화(HP+실드 > 100% 압축)는 뷰가 수행.
        // boss-mamemo unit 2 — 방어유닛·순찰병 두 곳에 복붙돼 있던 3줄을 여기로 모으고
        // **적 분기를 편입**했다. 적 분기는 이 인자가 리터럴 0f 라 실드를 줘도 게이지가
        // 안 그려졌다 — 하위 레이어(UnitOverheadUiLayer·UnitOverheadView·enemy skin 의
        // shield 색)는 이미 진영 무관이었으므로 막힌 곳은 이 호출 하나였다.
        private float ShieldRatioOf(Entity entity, in Health h)
        {
            if (h.max <= 0f || !_em.HasBuffer<Wassup.Battle.Units.ShieldSlot>(entity)) return 0f;
            return Wassup.Battle.Units.ShieldMath.Sum(
                _em.GetBuffer<Wassup.Battle.Units.ShieldSlot>(entity, isReadOnly: true)) / h.max;
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

        // summon-patrol-defender unit 10 — 소환물 생존 여부를 소환사 뷰의 애니 상태로 옮긴다.
        //
        // **뷰는 SummonerState 를 모른다**(절대 제약 1). 여기서 sim 사실을 읽어 «이름 2개»만
        // 밀어 넣고, 엣지 판정(오버라이드가 걸려 있었나)은 뷰가 한다 — 그래서 브리지에
        // "직전 프레임" 딕셔너리를 만들지 않는다.
        //
        // 게이트 순서가 중요하다: SummonerState 보유 확인이 **먼저**다. 통과한 소수만 능력
        // 목록을 훑으므로 디펜더 전원에 대해 매 프레임 abilities 를 도는 낭비가 없다.
        private void SyncSummonerAnimationState(Entity summoner, DefenderUnitData data,
            Wassup.Presentation.SpineUnitView view)
        {
            if (view == null || data == null) return;
            if (!_em.HasComponent<Wassup.Battle.Combat.SummonerState>(summoner)) return;

            var ability = FindSummonPatrolAbility(data);
            if (ability == null) return;
            if (string.IsNullOrEmpty(ability.activeAnimation)) return;

            Entity patrol = _em.GetComponentData<Wassup.Battle.Combat.SummonerState>(summoner).current;
            if (IsPatrolAlive(patrol)) view.SetLoopOverride(ability.activeAnimation, ability.lostAnimation);
            else view.ClearLoopOverride();
        }

        // README 계약 9 의 생존 술어. **3중이어야 한다** — Exists 만 보면 DeadTag 가 붙고
        // 실제 파괴되기까지의 프레임 동안 순찰병이 살아 보여서 상실 모션이 늦게 나간다.
        // (BattleBridge.Relocation 의 검사는 2중인데, 거기선 그 지연이 무해했다.)
        private bool IsPatrolAlive(Entity patrol)
        {
            if (patrol == Entity.Null || !_em.Exists(patrol)) return false;
            if (_em.HasComponent<DeadTag>(patrol)) return false;
            if (!_em.HasComponent<Health>(patrol)) return false;
            return _em.GetComponentData<Health>(patrol).value > 0f;
        }

        private static SummonPatrolAbility FindSummonPatrolAbility(DefenderUnitData data)
        {
            var abilities = data.abilities;
            if (abilities == null) return null;
            for (int i = 0; i < abilities.Count; i++)
                if (abilities[i] is SummonPatrolAbility summon) return summon;
            return null;
        }

        private void DrainDefenderDeathEvents()
        {
            if (!_defenderDeathQueue.IsCreated) return;
            while (_defenderDeathQueue.TryDequeue(out var evt))
            {
                // ⚠ f 축이 **0 으로 고정됐다**(skill-layer-migration unit 3g). 예전엔 작별
                // 선물의 피해량을 실었는데 그 payload 가 concrete 로 갔다. 그 실행은 투사체
                // 채널에 그대로 남으므로 트레이스가 보는 사건이 사라지지는 않는다.
                // 골든 코퍼스 덱에 작별 선물 카드가 있으면 이 축이 달라진다 — 그 경우
                // 코퍼스를 다시 뜨는 것이 맞고, 「사망 사건」이라는 이 채널의 뜻은 그대로다.
                Wassup.Core.Trace.LegacyTraceRecorder.Ev(Wassup.Core.Trace.TraceChannel.DefenderDeath,
                    i: evt.cell.x * 1000 + evt.cell.y, f: 0f);
                var cell = new Vector2Int(evt.cell.x, evt.cell.y);
                // defender-clock-out unit 1 — 판 정리는 퇴근과 공유한다(ReleaseDefenderTile).
                // 바인딩을 제거 **전에** 받아 오는 계약도 그 안에 있다 — DefenderDied 가 엔티티를
                // 카드 회수 키로, SO 를 awakeningReward 로 실어야 하기 때문이다.
                bool hasBinding = ReleaseDefenderTile(cell, out var binding);
                // ⚠ 뷰 반납은 공유 함수에 넣지 않는다. 사망은 NotifyDeath(=Kill()=deathAnimation,
                // 기본값 "die"), 퇴근은 Despawn(즉시). 넣었다면 bool playDeathAnim 플래그 파라미터를
                // 부르게 된다 — 뷰 반납의 주인은 처음부터 호출처다.
                if (spineUnitPool != null && hasBinding)
                {
                    spineUnitPool.NotifyDeath(binding.entity);
                    defenderFallbackViewPool?.Despawn(binding.entity);
                }
                Debug.Log($"[BattleBridge] Defender died @ {cell}; tile freed, synergy recomputed.");

                // skill-layer-migration unit 3g — **작별 선물 실행기가 여기서 사라졌다.**
                // concrete 로 갔고 자기 죽음 seam 이 실행한다.

                // dreamcatcher-awakening-hand unit 1 — relay after cleanup so the
                // tile/synergy state is consistent when subscribers run. Entity is
                // already destroyed in ECS; it is passed as a registry KEY only.
                if (hasBinding)
                    DefenderDied?.Invoke(binding.entity, binding.data, GridCellToViewCenter(cell));
            }
        }

        // defender-clock-out unit 1 — **방어 유닛이 판에서 내려왔다.** 원인(사망/퇴근)과 무관한
        // 결과만 담는다. 호출처 2개(사망 드레인 · RetireDefender)가 공유한다 — 갈라 두면 한쪽만
        // 고치는 버그가 난다(유령 게이지 · 안 풀리는 점유).
        //
        // ⚠ 두 가지가 여기 **없다**:
        //  ① 뷰 반납 — 사망은 NotifyDeath(사망 애니), 퇴근은 Despawn(즉시)이라 갈린다.
        //     넣으면 bool playDeathAnim 플래그 파라미터를 부른다.
        //  ② 엔티티 파괴 — 사망은 UnitLifecycleSystem 이 이미 파괴한 뒤고, 퇴근은 호출자가 한다.
        //     **파괴 주체는 호출처가 갖는다**는 것이 이 함수의 계약이다.
        //
        // ⚠ 바인딩을 제거 **전에** out 으로 넘긴다(엔티티가 카드 회수 키다). 순서를 뒤집으면
        // 두 호출처가 동시에 깨진다.
        // defender-footprint unit 1 — 점유 등록. 배치 2경로·재배치 스왑만 부른다.
        // owner 맵과 _occupiedTiles 는 항상 함께 움직인다(갈라지면 유령 점유·해석 불능).
        private void OccupyDefenderFootprint(Vector2Int anchor, Vector2Int size)
        {
            var rect = FootprintMath.Cells(anchor, size);
            var primary = FootprintMath.PrimaryCell(anchor, size);
            for (int y = rect.yMin; y < rect.yMax; y++)
            {
                for (int x = rect.xMin; x < rect.xMax; x++)
                {
                    var c = new Vector2Int(x, y);
                    _occupiedTiles.Add(c);
                    _defenderCellOwner[c] = primary;
                }
            }
        }

        // defender-footprint unit 1 — 점유 해제. SO 를 다시 읽지 않고 owner 맵을 스캔하는 이유:
        // 배치 후 시트 임포트가 footprint 값을 바꿔도 **등록 시점의 칸들**이 정확히 반납되게 —
        // SO 재계산으로 반납하면 유령 점유가 남는다. 보드는 수십 칸이라 스캔 비용 무시.
        private void ReleaseDefenderFootprint(Vector2Int primary)
        {
            _footprintReleaseScratch.Clear();
            foreach (var kv in _defenderCellOwner)
                if (kv.Value == primary) _footprintReleaseScratch.Add(kv.Key);
            for (int i = 0; i < _footprintReleaseScratch.Count; i++)
            {
                _occupiedTiles.Remove(_footprintReleaseScratch[i]);
                _defenderCellOwner.Remove(_footprintReleaseScratch[i]);
            }
        }

        // defender-footprint unit 1 — footprint 안 어느 칸이 와도 그 유닛의 대표 셀(바인딩 키)로
        // 해석한다. 1×1 은 항등. 셀-키 공개 API(조회·활성화·퇴근·재배치)가 footprint 에
        // 투명해지는 지점이다. owner 맵 미등록 셀은 바인딩 직검으로 폴백.
        private bool TryResolveDefenderKey(Vector2Int cell, out Vector2Int key)
        {
            if (_defenderCellOwner.TryGetValue(cell, out key)) return true;
            key = cell;
            return _defenderByTile.ContainsKey(cell);
        }

        private bool ReleaseDefenderTile(Vector2Int cell, out (Entity entity, DefenderUnitData data) binding)
        {
            // defender-footprint unit 1 — 사망 이벤트의 cell = DefenderTile.cell = 대표 셀이지만,
            // 퇴근 등 다른 호출처가 footprint 임의 칸을 넘겨도 같은 유닛으로 접히게 해석한다.
            if (!TryResolveDefenderKey(cell, out var key)) key = cell;
            bool hasBinding = _defenderByTile.TryGetValue(key, out binding);
            // beam unit 1 — 쏘던 유닛이 내려가면 빔이 허공에 남는다. TTL 만료를 기다리지 않고 끊는다.
            if (beamPresenter != null && hasBinding) beamPresenter.Close(binding.entity);
            if (hasBinding) _cancellableDeployments.Remove(binding.entity); // unit 5 리뷰 H-1 — 퇴장 = 자격 소멸
            _defenderByTile.Remove(key);
            ReleaseDefenderFootprint(key);
            _occupiedTiles.Remove(key); // owner 맵을 안 지난 레거시 점유 방어(정상 경로에선 이미 비었다)
            RefreshPlacementHighlightIfShown(); // placement-eligible-tile-highlight unit 2
            tileHealthGaugeLayer?.Hide(key);   // unit 3 — 퇴장 시 게이지 제거
            RecomputeSynergyFor(key);
            return hasBinding;
        }

        // defender-clock-out unit 1 — **퇴근**: 판 위 유닛을 플레이어 의사로 내린다.
        // 사망이 아니므로 사직서·작별 선물·각성이 일어나지 않는다 — 배제 코드가 있어서가 아니라
        // **DeadTag 를 안 달고 DefenderDied 를 안 쏘기 때문**이다(사망 경로에 진입 자체를 안 한다).
        //
        // ⚠ **defender 엔티티를 브리지가 파괴하는 것은 이 리포의 첫 사례다.** 기존 DestroyEntity
        // 9건 중 유닛은 적 2건뿐이고(공성 유출 · 골 붕괴) 나머지는 캐리어·필드·구조물이다.
        // 그래도 성립하는 근거: 퇴근은 UI 기원 행위이고, 브리지가 유일한 Mono↔ECS 게이트웨이이며,
        // **브리지가 배치한 것을 브리지가 수거하는 대칭**이다. 참조 보유자(FocusTarget ·
        // SummonerState · Aggroed · 투사체 target)가 전부 매 프레임 Exists/HasComponent 를 첫
        // 관문으로 쓰므로 dangling 이 없다(사망이 매번 파괴하기에 그 내성은 이미 검증돼 있다).
        //
        // 순찰병은 별도 배선이 없다 — PatrolLifecycleSystem 의 소환사 생존 판정 첫 줄이
        // Exists(owner) 라 **파괴 자체가 신호**다(다음 sim 틱에 회수, 그 1틱은 무해).
        // defender-footprint unit 5 — 배치 취소 유예: PendingDeployment(착지 연출~활성화 전)
        // 동안 배치를 되돌린다. 이 구간은 전투 미참여·on-place 미발화가 구조로 보장되므로
        // (README 계약 9 «유예 중 효과 미시작») 코스트 전액 환불·쿨다운 되감기가 정당하다.
        // 활성화 이후엔 false = 유예 종료. 파괴·뷰 반납은 퇴근(RetireDefender)의
        // «브리지가 배치한 것을 브리지가 수거» 형태를 미러한다(즉시 Despawn — 사망 애니 없음).
        public bool TryCancelPendingDeployment(Entity entity)
        {
            if (_em == null || entity == Entity.Null || !_em.Exists(entity)) return false;
            // 리뷰 H-1 — PendingDeployment 는 재배치 비행에도 붙는다. 자격 셋이 진짜 술어 —
            // 없으면 이 API 가 이동 중인 활성 유닛을 파괴하고 코스트를 이중 환불한다.
            if (!_cancellableDeployments.Contains(entity)) return false;
            if (!_em.HasComponent<PendingDeployment>(entity)) return false;
            if (_em.HasComponent<Wassup.Battle.Units.DeadTag>(entity)) return false;
            if (!TryGetDefenderCell(entity, out var cell)) return false;

            // 되돌릴 수 없는 sim 변경 먼저(퇴근 리뷰 2026-08-15 와 같은 순서 원칙).
            if (!ReleaseDefenderTile(cell, out var binding))
            {
                // 바인딩 부재 = 반쯤 무너진 상태 — 환불 근거가 없으므로 여기서 물러난다(조용한 손실 방지).
                Debug.LogWarning($"[BattleBridge] Cancel pending: no binding @ {cell} — aborted.");
                return false;
            }
            _em.DestroyEntity(entity);

            spineUnitPool?.Despawn(entity);
            defenderFallbackViewPool?.Despawn(entity);
            ClearDefenderViewOverride(entity);
            _onPlaceTriggeredEntities.Remove(entity);
            _effectTileAppliedEntities.Remove(entity);

            var costRuntime = GameManager.Instance != null ? GameManager.Instance.CostRuntime : null;
            if (binding.data != null && costRuntime != null) costRuntime.RefundSpend(binding.data.cost);
            var cooldownRt = GameManager.Instance != null ? GameManager.Instance.CooldownRuntime : null;
            // 리뷰 H-1 — 사망·퇴근 쿨이 같은 키를 덮었을 수 있어 배치 쿨 길이 이하일 때만 지운다.
            if (binding.data != null && cooldownRt != null)
                cooldownRt.ClearCooldownUpTo(binding.data, binding.data.placementCooldown);

            DefenderDeploymentCancelled?.Invoke(entity, binding.data);
            Debug.Log($"[BattleBridge] Pending deployment cancelled @ {cell} — refunded.");
            return true;
        }

        // defender-footprint unit 5 — 유예 창 관측 read seam(되돌리기 버튼 표시 게이트).
        public bool IsDefenderPendingDeployment(Entity entity)
            => _em != null && entity != Entity.Null && _em.Exists(entity)
               && _em.HasComponent<PendingDeployment>(entity);

        public bool RetireDefender(Vector2Int cell)
        {
            if (_em == null) return false;
            // defender-footprint unit 1 — footprint 임의 칸 → 대표 셀 해석.
            if (TryResolveDefenderKey(cell, out var retireKey)) cell = retireKey;
            if (!_defenderByTile.TryGetValue(cell, out var pre)) return false;
            if (pre.entity == Entity.Null || !_em.Exists(pre.entity)) return false;
            // 비행 중(배치/재배치 착지 전)에는 내리지 않는다 — 뷰 오버라이드와 활성화 꼬리가 뜬다.
            if (_em.HasComponent<PendingDeployment>(pre.entity)) return false;
            // 이미 죽는 중이면 사망 경로에 양보한다(보상은 죽음의 것이다).
            if (_em.HasComponent<Wassup.Battle.Units.DeadTag>(pre.entity)) return false;

            ReleaseDefenderTile(cell, out var binding);

            // dreamcatcher-content-4 unit 5 (퇴직 위로금) — 퇴근 사건의 payload 를 **파괴 직전에
            // 슬롯에서 직독**한다. 사망 경로는 payload 를 DefenderDeathEvent 에 미리 구워 나르는데,
            // 그건 드레인이 도는 시점에 엔티티가 이미 없기 때문이다. 퇴근은 **파괴 주체가 바로
            // 여기**라서 그 우회가 필요 없다 — 아직 살아 있는 엔티티의 버퍼를 그냥 읽으면 된다.
            //
            // ⚠ **브리지가 defender 엔티티의 트리거 슬롯을 읽는 첫 사례다.** 여태 슬롯 소비는
            // 전부 Combat 시스템 몫이었고 브리지는 bake(쓰기)만 했다. 그래도 성립하는 근거:
            // "퇴근" 이라는 사건은 sim 에 존재하지 않는다 — DeadTag 를 안 다는 것이 곧 이 카드의
            // 계약이라(defender-clock-out 계약 1) 사건 지점이 브리지 말고는 없다.
            //
            // 스냅샷으로 뜨는 이유 둘: ① 바로 아래에서 엔티티가 파괴된다 ② cast(SpawnProjectile)
            // 가 구조 변경이라 DynamicBuffer 핸들이 그 자리에서 무효화된다.
            // isReadOnly: 슬롯 쓰기는 Combat 소유다(맥락 경계) — 브리지는 bake 때 말고는 읽기만 한다.
            var retireSlots = default(NativeArray<DcTriggerSlot>);
            if (_em.HasBuffer<DcTriggerSlot>(binding.entity))
                retireSlots = _em.GetBuffer<DcTriggerSlot>(binding.entity, isReadOnly: true)
                                 .ToNativeArray(Allocator.Temp);

            // ⚠ **되돌릴 수 없는 sim 변경을 먼저 끝낸다**(코드리뷰 2026-08-15 반영).
            // 원래는 뷰 처리 뒤에 있었는데, 그 사이의 프레젠테이션 코드(키링 생성 = Shader.Find /
            // new GameObject, 코루틴 시작)가 던지면 **엔티티는 살아 있는데 바인딩만 사라진**
            // 반쯤 무너진 상태가 된다. 그 상태의 유닛은 다시 선택·퇴근이 안 되고, 나중에 죽어도
            // 드레인이 hasBinding=false 라 DefenderDied 가 안 나가 **부착 카드가 영구 소실**된다.
            // 아래 뷰 경로는 엔티티를 Dictionary 키로만 쓰고 EntityManager 를 만지지 않으므로
            // 순서를 앞당겨도 무해하다.
            _em.DestroyEntity(binding.entity);

            // dreamcatcher-content-4 unit 5 — **비워진 그 칸에 운석이 떨어진다.** 실행 형태는
            // 기존 SelfTileAoe 그대로다(SkyFall × TileAoe 투사체 하나 — 작별 선물·실드 파열
            // 폭발과 같은 경로). 파괴 뒤에 쏘지만 스냅샷만 참조하므로 소멸한 엔티티를 만지지 않는다.
            //
            // **전 매칭 슬롯이 발동한다** — 카드를 2장 붙였으면 운석 2발. OnDeath 의 "첫 매칭
            // 슬롯만" 은 사망 이벤트 struct 가 payload 필드를 한 벌만 실어서 생긴 제약이었고,
            // 여기는 버퍼를 직독하므로 해당 없다(HealthThreshold 가 슬롯당 발동인 것과 같은 자리).
            //
            // 값은 전부 **이 카드가 소유한다**(계약 7-1) — 액티브 카드 운석(SkillData)·시즌 기믹
            // 폭격(ClockOutGimmickData)과 겉모습(탄 SO)만 공유하고 피해·반경·예고는 독립이다.
            if (retireSlots.IsCreated)
            {
                var impactWorld = GridToWorldCenter(cell, spawnHeight); // 비워진 칸 중심 — 슬롯 불변
                for (int i = 0; i < retireSlots.Length; i++)
                {
                    var slot = retireSlots[i];
                    // 이 루프는 host 의 **전체** 슬롯 버퍼를 훑는다 — 다른 트리거(공격 N회·
                    // 실드 파열 …)의 슬롯이 같은 버퍼에 섞여 있으므로 두 축을 다 본다.
                    if (slot.trigger != Wassup.Data.DcTriggerKind.OnRetire) continue;

                    // ⚠ **시뮬 밖 생산자다**(skill-layer-migration unit 3e). 퇴근은 사용자
                    // 입력이 부르는 브리지 경로라 감지자가 시스템이 아니다. 그래서 이벤트가
                    // **자기 seam 을 말해야** 한다 — 안 그러면 프레임 첫 seam 이 집어가고,
                    // 그 seam 의 「시전자 생존」 가드에 걸려 조용히 버려진다(퇴근한 유닛은
                    // 바로 위에서 이미 파괴됐다). 자기 죽음 seam 이 그 가드가 꺼진 유일한 곳이다.
                    if (slot.skillId != Wassup.Skills.SkillRegistry.NotRouted
                        && _skillFiredQueue.IsCreated)
                    {
                        _skillFiredQueue.Enqueue(new Wassup.Battle.Skills.SkillFiredEvent
                        {
                            Seam = Wassup.Battle.Skills.SkillSeam.Lifecycle,
                            Caster = Entity.Null,   // 퇴근한 유닛은 이미 없다(레거시 owner 도 비었다)
                            // ⚠ 핸들이 비었으므로 진영은 값으로 실어야 한다(unit 8 선행).
                            // 퇴근은 방어유닛 전용 사건이라 리터럴이 참이다.
                            CasterFaction = Wassup.Battle.Units.Faction.DefenderUnit,
                            SkillId = slot.skillId,
                            SlotIndex = i,
                            FiredPosition = new float3(impactWorld.x, impactWorld.y, impactWorld.z),
                            Target = Entity.Null,
                            // 비워진 칸 중심 — 슬롯 불변. 이 자리를 지금 안 실으면 못 읽는다.
                            TargetPosition = new float3(impactWorld.x, impactWorld.y, impactWorld.z),
                            Magnitude = slot.magnitude,
                            Duration = slot.duration,     // 낙하 예고 초(계약 8)
                            TileRange = slot.tileRange,
                            DataIndex = slot.projectileDataIndex,
                            VisualScale = slot.visualScale,   // 퇴근 운석만 저작 배율을 읽는다
                            // 레거시는 층을 안 실었다(= 무제한).
                            TargetTraversalLayers = 0,
                        });
                        continue;   // 실행은 스킬 레이어가 한다
                    }

                    if (slot.payload != Wassup.Data.DcPayloadKind.SelfTileAoe) continue;
                    // bake 가 탄 SO·양수 magnitude 를 이미 강제한다(그래서 여기 걸리는 슬롯은
                    // 없어야 한다). 그럼에도 확인하는 이유는 값이 빈 슬롯을 그대로 쏘면
                    // SpawnProjectile 이 dataIndex 범위 경고를 뱉고 조용히 드롭하기 때문 —
                    // "안 터지는 카드" 의 원인이 로그 한 줄로 흐려진다.
                    if (slot.projectileDataIndex < 0 || slot.magnitude <= 0f) continue;

                    SpawnProjectile(new ProjectileSpawnRequest
                    {
                        movement        = MovementKind.SkyFall,
                        payload         = PayloadKind.TileAoe,
                        origin          = impactWorld,
                        impact          = impactWorld,
                        damage          = slot.magnitude,
                        impactTileRange = slot.tileRange,
                        // 낙하 예고 초. bake 가 payload.duration 을 이 슬롯에 실었다(계약 8 —
                        // AreaBarrage 의 duration=텔레그래프 선례). 0 이면 즉시 착탄.
                        flightTime      = slot.duration,
                        dataIndex       = slot.projectileDataIndex,
                        // arcHeight 는 **비워 둔다** — 드레인이 탄 SO 의 dropHeight 로 보충한다
                        // (보스 융단폭격과 같은 처리). 여기서 SO 를 다시 읽으면 낙하 높이의
                        // 출처가 둘이 된다.
                        visualScale     = slot.visualScale > 0f ? slot.visualScale : 1f,
                        targetFaction   = ProjectileTargetFaction.Enemy,
                        // owner 는 비운다 — 퇴근한 유닛은 **이미 없다**. 실드 파열 폭발이
                        // owner=host 로 킬을 귀속시키는 것과 갈리는 지점이다(그쪽 host 는
                        // 같은 프레임에 죽어도 파괴는 다음 틱이라 키로서 유효하다).
                        owner           = Entity.Null,
                    }, Entity.Null);
                }
                retireSlots.Dispose();
            }

            // 사망 애니를 타지 않는다(계약 11) — NotifyDeath(=Kill()=deathAnimation) 대신 여기로.
            //
            // unit 3 — 연출이 배선돼 있으면 뷰를 **떼어내 넘긴다**(파괴하지 않는다). 엔티티는
            // 이미 사라졌고 뷰만 위로 뽑혀 나간다(보스 도약과 같은 형태 — sim 은 즉시 끝나고
            // 뷰만 남는다). 넘긴 뒤 뷰의 수명은 연출 소유다 — SpineUnitPool.Detach 의 계약.
            // 미배선이면 종전대로 즉시 반납(개발 씬·테스트에서 조용히 동작).
            //
            // rev 2 — 링을 칠 좌표를 함께 넘긴다. VfxSpawner 가 진입부에서 ToView 하므로
            // **sim 좌표**여야 한다(이중 변환 금지 — 배치 링 호출부와 같은 규약).
            if (retireFlight != null && spineUnitPool != null
                && spineUnitPool.Detach(binding.entity, out var retiringView))
                retireFlight.Fly(retiringView, GridToWorldCenterVector(cell, spawnHeight), binding.data);
            else
                spineUnitPool?.Despawn(binding.entity);
            defenderFallbackViewPool?.Despawn(binding.entity);
            Debug.Log($"[BattleBridge] Defender retired @ {cell}; tile freed, synergy recomputed.");
            DefenderRetired?.Invoke(binding.entity, binding.data, GridCellToViewCenter(cell));
            return true;
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
                Wassup.Core.Trace.LegacyTraceRecorder.Ev(Wassup.Core.Trace.TraceChannel.DcTriggerFired, SimIdOf(evt.host));
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
                Wassup.Core.Trace.LegacyTraceRecorder.Ev(Wassup.Core.Trace.TraceChannel.Knockup, SimIdOf(evt.target), f: evt.durationSec);
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
                Wassup.Core.Trace.LegacyTraceRecorder.Ev(Wassup.Core.Trace.TraceChannel.ShieldBreak, SimIdOf(evt.host),
                    i: evt.tileRange, f: evt.magnitude);
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

                // ⚠ **이전된 슬롯은 실행하지 않는다**(skill-layer-migration unit 3d‴).
                // 위 펄스와 아래 로그는 이전 여부와 무관하게 돈다 — 이 채널이 나르는 것은
                // 「카드가 일했다」는 사실이고, 스킬 레이어로 간 것은 **실행뿐**이다.
                bool routedToSkillLayer = evt.skillId != Wassup.Skills.SkillRegistry.NotRouted;

                if (evt.payload == Wassup.Data.DcPayloadKind.SelfTileAoe)
                {
                    // 실드 파열 폭발 — OnDeath 폭발/메테오와 동형. bake 가 AoE view 없으면 슬롯 자체를
                    // 스킵하므로 aoeDataIndex 는 정상 >=0. 실제 데미지는 투사체(ProjectileHitSystem)가
                    // 해결 — 로그의 대상은 cast 시점 범위 내 적 스냅샷(raw magnitude, cap 0 = 투사체 동일).
                    if (evt.aoeDataIndex >= 0)
                    {
                        // 실행만 건너뛴다 — 아래 로그 스냅샷은 이전 여부와 무관하다.
                        if (!routedToSkillLayer)
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
                        }
                        if (log != null)
                        {
                            CollectShieldBreakTargets(evt.host, evt.position, evt.tileRange, 0, targets);
                            foreach (var t in targets)
                                log.targets.Add(new Logging.ShieldBreakTargetLog
                                { tile = t.cell, effect = "Damage", magnitude = evt.magnitude });
                        }
                    }
                }
                else if (evt.payload == Wassup.Data.DcPayloadKind.AreaSleep)
                {
                    int cap = (int)evt.magnitude;
                    // ⚠ 이전된 슬롯은 재우지 않는다 — 스킬 레이어가 이미 재웠다.
                    // 로그는 그대로 남긴다(대상 스냅샷은 이전 여부와 무관한 사실이다).
                    if (cap >= 1 && evt.tileRange >= 1 && evt.duration > 0f)
                    {
                        CollectShieldBreakTargets(evt.host, evt.position, evt.tileRange, cap, targets);
                        foreach (var t in targets)
                        {
                            if (!routedToSkillLayer)
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
        // skill-layer-foundation unit 2b — 브리지(managed) 쪽 진영 조회.
        // ECS 쪽 `FactionQuery` 와 같은 답을 내야 한다: `FactionTag` 이 정본, 부재 시 유닛 태그,
        // 둘 다 없으면 None. 여기만 시그니처가 다른 이유는 브리지엔 `ComponentLookup` 이
        // 없고 `_em` 직접 조회를 쓰기 때문이다.
        private Wassup.Battle.Units.Faction FactionOfEntity(Entity e)
        {
            if (!_em.Exists(e)) return Wassup.Battle.Units.Faction.None;
            bool hasTag = _em.HasComponent<Wassup.Battle.Units.FactionTag>(e);
            // 결정은 `FactionRelation.Resolve` 가 소유한다 — 여기서 4단 체인을 복제하면
            // ECS 쪽(`FactionQuery`)과 조용히 갈린다(투트랙 리뷰 M3).
            return Wassup.Battle.Units.FactionRelation.Resolve(
                hasTag,
                hasTag ? _em.GetComponentData<Wassup.Battle.Units.FactionTag>(e).value
                       : Wassup.Battle.Units.Faction.None,
                _em.HasComponent<AttackUnitTag>(e),
                _em.HasComponent<DefenderUnitTag>(e));
        }

        private void CollectShieldBreakTargets(Entity caster, float3 center, int tileRange, int cap,
            System.Collections.Generic.List<(Entity entity, Vector2Int cell)> results)
        {
            results.Clear();
            int2 grid = _generatedMap.IsCreated ? _generatedMap.gridSize : FallbackGridSize;
            var centerCell = GridMath.WorldToCell(center, tileSize, grid, origin: _boardOrigin);

            // unit 2b — 대상 풀은 **caster 의 상대 진영**이다.
            //
            // 여기가 `DcTrigger.cs` 가 이름을 대며 경고한 그 드레인이다 — 대상 풀이
            // `AttackUnitTag` 하드코딩이라, `OnShieldBreak` 를 적에게 열면 **보스의 파열
            // 폭발이 자기 진영을 때린다**. 그래서 화이트리스트가 그 문을 잠가 두고 있었다.
            // 이 줄이 상대적이 되면 그 잠금의 이유가 사라진다(철거는 migration unit 8).
            //
            // ⚠ 파열은 host 가 **같은 프레임에 파괴될 수 있다**(관통 킬 프레임에도 발동한다).
            // 그때는 진영을 못 읽으므로 기존 동작(적 풀)을 유지한다 — byte-identical.
            var opponents = Wassup.Battle.Units.FactionRelation.OpponentUnitsOf(FactionOfEntity(caster));
            using var enemyQuery = opponents == Wassup.Battle.Units.Faction.DefenderUnit
                ? _em.CreateEntityQuery(
                    ComponentType.ReadOnly<DefenderUnitTag>(),
                    ComponentType.ReadOnly<LocalTransform>())
                : _em.CreateEntityQuery(
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
                Wassup.Core.Trace.LegacyTraceRecorder.Ev(Wassup.Core.Trace.TraceChannel.UnitAttack,
                    SimIdOf(evt.attacker), SimIdOf(evt.target), f: evt.attackAnimPeriod);
                var targetWorld = new Vector3(evt.targetWorld.x, evt.targetWorld.y, evt.targetWorld.z);

                // elite-enemy-tier unit 4 — 이 플래그가 켜진 이벤트는 «공격 사건» 이 아니라
                // **VFX 캐리어**다(피해는 Burst ISystem 이 이미 적용했고 그쪽은 VfxSpawner 를 못
                // 부른다). 공격 시작 이벤트는 RESOLVE 보다 앞서 별도로 나갔으므로 여기서
                // NotifyAttack 을 다시 부르면 한 프레임에 공격 애니가 두 번 트리거된다.
                if (evt.hasAreaBreath)
                {
                    // 연출 소유권은 VfxSpawner 다(원샷 VFX 의 프리팹 슬롯·스폰·수명).
                    // 브리지는 뷰 앵커만 풀어서 넘긴다 — spineUnitPool 접근이 여기 있으므로.
                    if (vfxSpawner != null && ResolveBeamViewPos(evt.attacker, true, out var breathOrigin))
                        vfxSpawner.SpawnAreaBreath(breathOrigin,
                            new Vector2(evt.breathDir.x, evt.breathDir.y),
                            evt.breathRangeWorld, evt.breathHalfAngleDeg);
                    continue;
                }

                spineUnitPool?.NotifyAttack(evt.attacker, targetWorld, evt.attackAnimPeriod);

                // elite-whirlpot unit 1 — 적의 «유닛별 공격 광역» VFX(팽이 회오리). 이 분기는
                // 반드시 아래 `defData == null → continue` **앞**에 있어야 한다 — 그 아래는 전부
                // 방어유닛 전용이라 적은 여기서 빠져나간다.
                //
                // SO 직독은 분열이 킬 드레인에서 쓴 것과 같은 수법이다: 브리지가 이미
                // `_enemyTypeByEntity` 로 적의 AttackUnitData 를 손에 들고 있으므로 **sim 이벤트에
                // 필드를 늘리지 않는다**(신규 채널 0 · 신규 필드 0).
                //
                // 「회오리를 갖는가」는 **프리팹 유무**가 결정한다 — id 분기도, attackTargetCount
                // 판정도 아니다(AttackUnitData 주석의 계약).
                if (vfxSpawner != null
                    && _enemyTypeByEntity.TryGetValue(evt.attacker, out var atkType)
                    && atkType != null && atkType.attackVfxPrefab != null
                    && ResolveBeamViewPos(evt.attacker, useAnchor: false, out var aoeOriginView))
                {
                    // 넘기는 것은 **이번 공격의 실발사 주기**뿐이다. `attackAnimPeriod` 는
                    // attackSpeedMul 까지 반영된 sim 값이라 공속이 바뀌어도 연출이 따라간다
                    // (빔 세션 TTL 과 같은 근거). ★그 주기를 수명으로 바꾸는 정책과 튜닝 knob 은
                    // `VfxSpawner` 소유다 — 브레스에서 이관받은 소유권을 다시 흘리지 않는다.
                    float period = evt.attackAnimPeriod > 0f
                        ? evt.attackAnimPeriod
                        : Mathf.Max(0.1f, atkType.attackCooldown);
                    vfxSpawner.SpawnUnitAttackAoe(
                        atkType.attackVfxPrefab, aoeOriginView,
                        radiusTiles: atkType.attackRange,
                        scalePerTile: atkType.attackVfxScalePerTile,
                        attackPeriodSeconds: period);
                }

                // instinct-turret-readout unit 1 — 본능의 포신 조준. 이 분기는 반드시 아래
                // `defData == null → continue` **앞**에 있어야 한다(회오리 VFX 와 같은 이유):
                // 그 아래는 전부 방어유닛 전용이라 거점은 거기까지 못 간다.
                //
                // 본능은 스파인 풀에도 `_enemyTypeByEntity` 에도 없어 위 소비자 전부를 그냥
                // 통과했다 — 사건은 오는데 받는 사람이 없던 자리다. 신규 큐 0.
                if (_structureTurretsByCell.Count > 0 && HasLiveEntityManager()
                    && _em.Exists(evt.attacker)
                    && _em.HasComponent<Wassup.Battle.Units.StructureTag>(evt.attacker))
                {
                    var atkCell = _em.GetComponentData<Wassup.Battle.Units.StructureTag>(evt.attacker).cell;
                    // 셀 중복 저작(같은 칸 거점 2개)은 여기서 막지 않는다 — 규칙 주인은
                    // StructureAuthoringRules.ValidateStructures(footprint 겹침 = 에러)다.
                    if (_structureTurretsByCell.TryGetValue(new Vector2Int(atkCell.x, atkCell.y), out var turret)
                        && turret != null
                        // 공격자 위치는 기존 헬퍼가 푼다 — 거점은 스파인 풀에 없어
                        // LocalTransform → 뷰 공간 폴백으로 떨어진다(셀 중심 재계산 불필요).
                        && ResolveBeamViewPos(evt.attacker, useAnchor: false, out var turretView))
                    {
                        // 방향은 **뷰 공간**에서 구한다 — 보드는 grid 가 월드 90°X 로 누운
                        // 평면이라 sim 벡터를 그대로 쓰면 엉뚱한 축으로 돈다(방어유닛의
                        // attackVfxFacesTarget 이 같은 이유로 그렇게 한다).
                        turret.AimAt((Vector3)Wassup.Core.BoardSpace.ToView(evt.targetWorld) - turretView);
                    }
                }

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
                Wassup.Core.Trace.LegacyTraceRecorder.Ev(Wassup.Core.Trace.TraceChannel.AttackOutputLog,
                    SimIdOf(evt.attacker), i: (int)evt.kind, f: evt.magnitude);
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

        // camera-direction unit 18 — 스테이지의 전역 포스트 볼륨을 카메라 소유자에게 밀어준다.
        // 마커(`GoalMarker`/`SpawnMarker`) 스캔과 같은 방식이라 스테이지 프리팹에 새 필드를
        // 요구하지 않는다. 스테이지가 없으면 null 을 밀어 죽은 참조를 끊는다.
        private bool _stagePostVolumeMissWarned;

        private void PushStagePostVolume()
        {
            var director = EnsureCameraDirector();
            if (director == null) return;
            var volume = _stageInstance != null
                ? _stageInstance.GetComponentInChildren<UnityEngine.Rendering.Volume>(true)
                : null;
            // 조용히 죽는 것이 이 결함의 본질이었다(씬 Post 를 스테이지로 옮기며 참조가 끊겼고
            // 비네트가 로그 한 줄 없이 사라졌다). 스테이지가 볼륨을 안 들고 있으면 경고한다.
            if (volume == null && _stageInstance != null && !_stagePostVolumeMissWarned)
            {
                _stagePostVolumeMissWarned = true;
                Debug.LogWarning($"[BattleBridge] 스테이지 '{_stageInstance.name}' 에 Volume 이 없다 — "
                    + "스트레스 비네트가 그려지지 않는다. 스테이지 프리팹의 Post 오브젝트를 확인할 것.", this);
            }
            director.SetPostVolume(volume);
        }

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
                Wassup.Core.Trace.LegacyTraceRecorder.Ev(Wassup.Core.Trace.TraceChannel.ProjectileHit,
                    SimIdOf(evt.source), i: evt.dataIndex, f: evt.radiusWorld);
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
                // 위치만 실린 채널 — 실을 축이 없어 셀 좌표(×100 반올림)를 a/b 에 넣는다.
                Wassup.Core.Trace.LegacyTraceRecorder.Ev(Wassup.Core.Trace.TraceChannel.HealApplied,
                    Mathf.RoundToInt(evt.position.x * 100f), Mathf.RoundToInt(evt.position.z * 100f),
                    f: evt.amount);
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
            {
                Wassup.Core.Trace.LegacyTraceRecorder.Ev(Wassup.Core.Trace.TraceChannel.ShieldGranted,
                    Mathf.RoundToInt(evt.position.x * 100f), Mathf.RoundToInt(evt.position.z * 100f));
                vfxSpawner.SpawnShieldGranted(new Vector3(evt.position.x, evt.position.y, evt.position.z));
            }
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
                Wassup.Core.Trace.LegacyTraceRecorder.Ev(Wassup.Core.Trace.TraceChannel.DamageNumber, SimIdOf(evt.entity), f: evt.amount);
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
        // first-run-tutorial — 목표 지점의 **실제 구조물 중심**(셀 중심이 아니다). 뷰 포워딩이라
        // ECS 를 타지 않지만, 컨트롤러가 TilemapMapView 를 직접 들지 않게 여기로 통일한다
        // (TryGetUnitViewAnchor 와 같은 창구).
        public bool TryGetGoalViewAnchor(out Vector3 worldPosition)
        {
            // map-diorama-stage 병합 수선 — 골 앵커 소스는 뷰가 아니라 스테이지 마커 등록부다
            // (구 tilemapMapView.TryGetGoalVisualAnchor 는 문서 파이프라인과 함께 은퇴).
            return TryGetGoalVisualAnchor(out worldPosition);
        }

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
            if (_em == null) return false;
            // defender-footprint unit 2 — 호출자가 앵커를 들고 와도 대표 셀로 해석. 피드 공식 미러
            // 계약에 footprint 뷰 오프셋도 포함된다(피드가 더하면 여기도 더한다 — 팝 0).
            if (TryResolveDefenderKey(cell, out var key)) cell = key;
            if (!_defenderByTile.TryGetValue(cell, out var b)) return false;
            if (b.entity == Entity.Null || !_em.Exists(b.entity) || !_em.HasComponent<LocalTransform>(b.entity))
                return false;
            var p = _em.GetComponentData<LocalTransform>(b.entity).Position;
            var fpOff = FootprintViewOffset(b.data);
            world = (Vector3)Wassup.Core.BoardSpace.ToView(
                new Unity.Mathematics.float3(p.x + fpOff.x, p.y + spineDefenderYOffset, p.z + fpOff.y));
            return true;
        }

        // defender-footprint unit 2 — 짝수 변 footprint 의 뷰 오프셋(월드 단위). 홀수/1×1 = zero.
        // 소비처는 뷰 피드 계열만(sync·RestViewPos·비행 앵커·타일 게이지) — sim 소비 금지.
        private Vector2 FootprintViewOffset(DefenderUnitData data)
        {
            if (data == null) return Vector2.zero;
            var o = FootprintMath.CenterOffsetFromPrimary(data.Footprint);
            if (o == Vector2.zero) return Vector2.zero;
            return new Vector2(o.x * tileSize, o.y * tileSize);
        }

        // defender-footprint unit 2 — 앵커 + 유닛 → footprint **기하 중심**의 view 좌표.
        // 배치 비행(탭 시뮬)의 종점 등 스폰 전 시점에 쓴다(스폰 후엔 TryGetDefenderRestViewPos).
        public Vector3 GridAnchorToViewCenter(Vector2Int anchor, DefenderUnitData unit)
        {
            var size = unit != null ? unit.Footprint : Vector2Int.one;
            var primary = FootprintMath.PrimaryCell(anchor, size);
            var fpOff = FootprintViewOffset(unit);
            var w = GridToWorldCenterVector(primary);
            return Wassup.Core.BoardSpace.ToView(new Vector3(w.x + fpOff.x, w.y, w.z + fpOff.y));
        }

        // Enemy kills → live score HUD. One score bump per enemy killed by damage.
        private void DrainEnemyKilledEvents()
        {
            if (!_enemyKilledEventQueue.IsCreated) return;
            while (_enemyKilledEventQueue.TryDequeue(out var evt))
            {
                Wassup.Core.Trace.LegacyTraceRecorder.Ev(Wassup.Core.Trace.TraceChannel.EnemyKilled,
                    SimIdOf(evt.entity), SimIdOf(evt.killer), i: evt.awakeningReward);
                scoreHud?.OnEnemyKilled();
                // battle-score-formula unit 2 — 최종 점수용 누적.
                // score-tally-sequence unit 0 이후 바로 윗줄의 HUD 도 **같은 값**을 받는다
                // (예전엔 처치당 고정 +10 이라 15배 어긋나 있었다). 두 경로가 같은
                // three-minute-kill-race unit 1 — **1킬 = 1점**이라 누적이 하나다.
                // 전투 중 HUD 숫자 == _killCount == 최종 점수.
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

                // bonus-wave-pull unit 5(계약 12) — **트리거 카운터는 일반 적만 센다.**
                // 점수(_killCount)는 바로 위에서 이미 전부 셌다 — 1킬 1점 불변이고 여기만 갈린다.
                // 안 가르면 보너스 킬이 다시 임계를 채워 실효 임계가 (N − enemyCount) 로 내려가고,
                // N ≤ enemyCount 면 보너스 웨이브가 자기 자신을 무한 재발화한다.
                // ★판별에 BonusWaveTag 를 쓸 수 없다 — 이 드레인 시점엔 엔티티가 이미
                // 파괴돼 있다. 등록부가 준 SO 로 가르며, 그 동치는 계약 4(보너스 적은 덱 풀에
                // 절대 안 들어간다)가 보장한다. 풀에 넣는 날 이 줄이 함께 깨진다.
                bool wasBonusKill = bonusWaveData != null && killedType != null
                                      && killedType == bonusWaveData.enemyUnit;
                if (!wasBonusKill) _normalKillCount++;

                // heart-stress-axis unit 2 — **잡을수록 마음이 숨을 돌린다.**
                // ⚠ `evt.awakeningReward` 가 아니라 **SO 원값**을 쓴다. 이벤트에 실린 값은
                // 「살찌운 제물」 카드의 배율이 이미 곱해진 baked 값이라, 그걸 쓰면 카드 하나가
                // 각성 충전과 스트레스 회복 **두 축**을 겸하게 된다(카드의 성격이 조용히 바뀐다).
                // 등록부 miss 는 회복 0 + 경고 1회 — 조용히 폴백값을 주면 회복이 공짜가 된다.
                if (killedType != null) EnqueueGoalHeal(killedType.awakeningReward);
                else if (!_killHealTypeMissLogged)
                {
                    _killHealTypeMissLogged = true;
                    Debug.LogWarning("[BattleBridge] 처치된 적의 데이터가 등록부에 없다 — 마음 회복 0 으로 넘긴다.", this);
                }

                // elite-enemy-tier unit 5 — 분열(슬라임). **여기서 SO 를 직독한다** — 위 등록부가
                // 죽은 적의 AttackUnitData 를 이미 들고 있어(파괴된 Entity 값도 키 비교는 유효)
                // 슬롯·이벤트 필드·sim 스탬프가 하나도 필요 없다. 유출(골 도달)은
                // EnemyKilledEvent 를 발화시키지 않으므로 «체력 소진 시에만 분열» 이 자동 성립한다.
                SpawnSplitChildren(killedType, (Vector3)evt.position);
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
                logger?.AddScoreEvent("enemy_killed", 1, time);

                // skill-layer-migration unit 3g — **시체폭발·잿불 실행기가 여기서 사라졌다.**
                // 둘 다 concrete 로 갔고 죽음 seam 이 실행한다. 이 드레인에 남은 일은
                // 킬 기록과 각성/표식 회수뿐이다.
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
                Wassup.Core.Trace.LegacyTraceRecorder.Ev(Wassup.Core.Trace.TraceChannel.ProjectileSpawn,
                    SimIdOf(requestEntities[i]), i: req.dataIndex, f: req.damage);
                // Spine attack trigger moved to DrainUnitAttackVisualEvents
                // so both projectile and melee defenders share the same hook.
                // battle-audio: fire SFX only for DEFENDER-shot projectiles (enemy ranged
                // attacks share this drain, so filter on the shooter's tag before spawn).
                bool shooterIsDefender = _em.HasComponent<DefenderUnitTag>(requestEntities[i]);
                var spawnedProjectile = SpawnProjectile(req, requestEntities[i]);
                if (spawnedProjectile != Entity.Null && shooterIsDefender)
                    Wassup.Core.SoundManager.Instance?.PlayProjectileFire();
                // unit 7d — 착탄 예고. 해제는 종전대로 **이 엔티티의 착탄 이벤트**로
                // 판별하므로(hit VFX 유무 무관), 그 엔티티를 만든 자리에서 잡는다.
                if (spawnedProjectile != Entity.Null && req.telegraphTileRange > 0)
                {
                    var tCell = GridMath.WorldToCell(req.impact, tileSize,
                        _generatedMap.IsCreated ? _generatedMap.gridSize : FallbackGridSize,
                        origin: _boardOrigin);
                    _skillTelegraphProjectile = spawnedProjectile;
                    PinSkillTelegraph(new Vector2Int(tCell.x, tCell.y), req.telegraphTileRange);
                }
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
                Wassup.Core.Trace.LegacyTraceRecorder.Ev(Wassup.Core.Trace.TraceChannel.MeteorBarrage, i: req.meteorCount);
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
            // dreamcatcher-content-4 리뷰 M3 — 궤도는 **궤도 위 시작점(위상 0)** 에서 태어난다.
            // 중심에서 태우면 첫 프레임 스윕 선분이 «중심 → 반경 끝» 인 **방사선**이 되어,
            // 호스트 발밑(고리 안쪽)의 적까지 딱 한 번 맞는다 — 어느 계약에도 없는 피해 사건이다.
            // (뷰에는 궤도 arm 이 없어 그 선분을 그리지도 않으므로 «화면과 일치하는 정직한 선분»
            //  이라는 변명도 성립하지 않는다. 1프레임 뷰 점프도 같이 사라진다.)
            // 위상 규약은 Orbit.Position 한 곳에만 산다 — 여기서 다시 유도하지 않는다.
            // on-place-skill-rework unit 10 — 적 조준 낙하탄의 착탄점은 **임자의 현재 위치**다.
            // 발사 주체(emitter Entity fan-out)는 `impact` 를 싣지 않는다 — Entity 바인딩이라
            // 조준이 엔티티 하나뿐이기 때문이다. 여기서 한 번 해석해 스폰 위치를 잡고,
            // 이후 갱신은 Move arm 이 프레임마다 한다(조준의 소유자는 궤적이다).
            if (req.movement == MovementKind.SkyFallOnEntity
                && req.target != Entity.Null && _em.HasComponent<LocalTransform>(req.target))
            {
                var aimPos = _em.GetComponentData<LocalTransform>(req.target).Position;
                req.impact = new float3(aimPos.x, 0f, aimPos.z);
            }
            var spawnPos = req.movement == MovementKind.SkyFall
                            || req.movement == MovementKind.SkyFallOnEntity
                ? new float3(req.impact.x, spawnHeight, req.impact.z)
                : req.movement == MovementKind.OrbitAroundPoint
                    ? Wassup.Battle.Combat.Projectile.Orbit.Position(
                        new float3(req.origin.x, spawnHeight, req.origin.z), req.maxDistance,
                        req.speed, 0f, req.orbitPhase)
                    : new float3(req.origin.x, spawnHeight, req.origin.z);
            _em.AddComponentData(entity, LocalTransform.FromPositionRotationScale(spawnPos, quaternion.identity, req.visualScale));
            _em.AddComponent<ProjectileTag>(entity);
            AttachSimEntityId(entity);

            var projData = _projectileDataByIndex[req.dataIndex];
            var state = new ProjectileState
            {
                movement = req.movement,
                payload = req.payload,
                target = req.target,
                speed = req.speed,
                damage = req.damage,
                targetTraversalLayers = req.targetTraversalLayers,
                hitThreshold = req.hitThreshold,
                onHitEffect = req.onHitEffect,
                splashRadius = req.splashRadius,
                splashDamageMul = req.splashDamageMul,
                dataIndex = req.dataIndex,
                // dreamcatcher-content-4 unit 0 — PathHit 재타격 간격은 **탄 SO 소유**다
                // (바로 아래 Directional 분기의 pierceCount 와 같은 번역자 역할). 요청
                // struct 에 필드를 만들지 않는 이유가 그것이다 — 발사 주체(카드 arm·
                // emitter·AttackSystem)는 이 값을 몰라도 된다. 기본 0 = 기존 전 발사 지점 무변화.
                rehitCooldownSec = projData != null ? math.max(0f, projData.rehitCooldownSec) : 0f,
                // content-5 unit 2 — 넉백도 탄 SO 소유(위와 같은 자리·같은 번역자 역할).
                // 저작은 「거리 ÷ 시간」이고 sim 이 쓰는 것은 속도라 **여기서 환산**한다 —
                // 근접 넉백(knockbackDistance / knockbackDuration)의 기존 관례와 같은 식.
                // 시간이 0 이면 나눗셈이 성립하지 않으므로 둘 다 0(=꺼짐)으로 흘린다.
                // 둘 다 양수일 때만 켠다 — 한쪽만 저작된 값은 «넉백 없음» 이다(거리 없이
                // 시간만, 시간 없이 거리만은 의미가 없고 후자는 0 나눗셈이다).
                knockbackSpeed = KnockbackOn(projData) ? projData.knockbackDistance / projData.knockbackDuration : 0f,
                knockbackDuration = KnockbackOn(projData) ? projData.knockbackDuration : 0f,
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
                // bomb-thrower-defender unit 2 — TileAoe cap/CC verbatim(기본 0=레거시).
                aoeTargetCap = req.aoeTargetCap,
                ccKind = req.ccKind,
                ccDuration = req.ccDuration,
                // bomb-barrel-on-place unit 2 — SpawnBlocker 가 세울 설치물 index, verbatim.
                blockerDataIndex = req.blockerDataIndex,
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
            else if (req.movement == MovementKind.BoomerangReturn)
            {
                // dreamcatcher-content-5 unit 1 — 왕복(부메랑). 발사점이 곧 귀환점이라
                // 타겟 엔티티를 잡지 않는다(궤도와 같은 계열). 축은 여기서 한 번 정규화해
                // sim 이 매 프레임 normalize 하지 않게 한다 — Directional 과 같은 규약.
                //
                // 퇴화 저작은 바로 위 Directional 과 **같은 이유로 같은 형태로 막는다**:
                // 축이 0 이거나 속도가 0 이면 왕복 완료 조건(speed*elapsed >= 2*maxDistance)이
                // 영원히 거짓이고, 재타격이 켜진 탄은 관통 예산도 안 깎아 **불멸 투사체**가
                // 된다. maxDistance 0 도 같은 부류다(태어난 자리에서 영원히 스윕).
                float2 axis = req.direction;
                if (math.lengthsq(axis) < 1e-6f || req.speed <= 0f || req.maxDistance <= 0f)
                {
                    Debug.LogWarning($"[BattleBridge] Boomerang cannot travel (axis={axis}, speed={req.speed}, dist={req.maxDistance}); dropping.");
                    _em.DestroyEntity(entity);
                    if (hasSnapshot) outputSnapshot.Dispose();
                    return Entity.Null;
                }
                state.origin = spawnPos;
                // ⚠ prevPos 를 0 으로 두면 첫 스윕 선분이 **맵 원점 → 발사점**이 되어
                // 그 선 위 적 전원을 때린다(궤도가 content-4 리뷰 M3 에서 겪은 결함).
                state.prevPos = spawnPos;
                state.direction = math.normalize(axis);   // 발사 축 — sim 이 되먹이지 않는다
                state.maxDistance = req.maxDistance;      // 편도 거리
                // 관통 예산은 탄 SO 소유(Directional·Orbit 과 같은 자리). 재타격이 켜진
                // 정상 부메랑은 이 값을 소모하지 않으므로(계약 3) 비정상 경로의 안전망이다.
                state.pierceRemaining = projData != null ? math.max(1, projData.pierceCount) : 1;
            }
            else if (req.movement == MovementKind.OrbitAroundPoint)
            {
                // dreamcatcher-content-4 unit 0 — 궤도(화염구). 중심은 발사 시점 고정점이라
                // 타겟 엔티티를 잡지 않는다. req.speed 는 **각속도(rad/s)** 로 온다 —
                // 발사 arm(BossPeriodicTriggerSystem)이 슬롯의 선속도 ÷ 반경으로 이미 변환해
                // 보낸다(그 arm 은 ISystem 이라 SO 를 못 읽으므로 bake 가 구운 값을 쓴다).
                state.origin = new float3(req.origin.x, spawnHeight, req.origin.z);
                state.prevPos = spawnPos;
                state.maxDistance = req.maxDistance;   // 궤도 반경
                state.speed = req.speed;               // 각속도
                state.orbitPhase = req.orbitPhase;      // 균등 배치용 각도 오프셋
                state.flightTime = math.max(req.flightTime, 0f); // 지속 초
                // 관통 예산은 **탄 SO 소유**다(Directional 과 같은 자리·같은 규약).
                // 재타격이 켜진 정상 궤도는 이 값을 읽지도 쓰지도 않으므로(계약 3 — 유일한
                // 종료 조건은 수명) 무슨 값이든 무해하다. 이 값이 일하는 것은 **비정상 경로**
                // 하나뿐이다: 기록 버퍼가 없거나 쿨타임이 0 인 궤도. 그때 예산이 «적당 1회»
                // 상한이 되어 탄이 곧 사라진다.
                // ⚠ 예전엔 여기 int.MaxValue 를 박았는데, 그러면 그 비정상 경로가
                // **매 프레임 전원 타격 + 영원히 안 죽는 탄** 이 된다(리뷰 M1). 눈에 띄게
                // 일찍 죽는 편이 조용히 30배 때리는 것보다 낫다.
                state.pierceRemaining = projData != null ? math.max(1, projData.pierceCount) : 1;
            }
            // on-place-skill-rework unit 10 — 적 조준 낙하탄도 같은 필드 규약을 쓴다
            // (arcHeight = 낙하 시작 높이 · flightTime = 예고). 다른 것은 조준뿐이고
            // 그 갱신은 Move arm 이 소유한다. `impactTileRange` 는 SingleSplash 짝이라
            // 읽히지 않는다 — 값을 그대로 흘려도 무해하다.
            else if (req.movement == MovementKind.SkyFall
                     || req.movement == MovementKind.SkyFallOnEntity)
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
                bool fallsFromSky = req.movement == MovementKind.SkyFall
                                    || req.movement == MovementKind.SkyFallOnEntity;
                float initialDrop = fallsFromSky ? projData.dropHeight : 0f;
                // projectile-shot-sequence unit 5 — emitter carrier는 일회성 request
                // entity라 view가 없다. 실제 공격자(req.owner)를 우선하고 owner 없는
                // legacy 요청만 drain의 shooter를 fallback으로 쓴다. SkyFall은 유닛
                // 발사가 아니라 impact cell에서 내려오므로 anchor를 적용하지 않는다.
                bool hasLaunchAnchor = false;
                Vector3 launchAnchor = default;
                if (!fallsFromSky && spineUnitPool != null)
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


        private bool CanDefenderTargetMover(byte attackTargetLayers, Entity target)
        {
            byte targetLayers = _em.HasComponent<PathFollowState>(target)
                ? _em.GetComponentData<PathFollowState>(target).traversalLayers
                : (byte)0;
            return PlacementLayers.CanTarget(attackTargetLayers, targetLayers);
        }

        // Fires the defender's on-place effect on surrounding entities. Returns
        // the count of entities affected so the logger can record magnitude.
        // Writes to Effects components go through EffectSpawner so the Effects-
        // context write gateway (Phase 2 decision) stays the sole path.

        // defender-on-place-skills unit 4 — 통로 반폭(타일). 0.45 였을 때 **레인 오프셋만큼 옆으로
        // 선 적**(±0.5)이 바로 옆에서도 탈락했다 — 실측 lat=1.0 으로 두 마리가 빠졌다.

        // unit 4 rev — 후보를 **한 번만** 모은다. 방향 결정과 명중 판정이 같은 집합을 봐야 한다:
        // 갈라 두면 한쪽만 고쳐진다(초판이 정확히 그랬다 — 방향 선정이 이탈 중인 보스를 후보로
        // 잡아 총구를 줬고, 그 보스는 피해를 받을 수 없어 일격이 통째로 낭비됐다).

        // 「이번 프레임 합법 후보」 — `AttackSystem` 의 targetCandidatesQuery 와 **같은 집합**이다.
        // `DeadTag`(파괴 대기)와 `UltimateLeapState`(판 밖 — 들어온 피해를 버린다)를 뺀다.
        // 빼지 않으면 때릴 수 없는 대상에 총구를 주고, `IncomingDamage` 에 넣기만 하면 올라가는
        // affected 로그가 거짓 양성이 된다. `UltimateLeapState` 주석의 소비처 목록도 함께 갱신할 것.


        // defender-on-place-skills unit 4 — 전방 관통 일격의 총구 방향.
        //
        // 옛 규칙은 "가장 가까운 길 칸 쪽"(`FindNearestPathDirection`)이었고 **삭제했다.** 그 탐색은
        // 맵을 y·x 오름차순으로 훑으며 동점에서 먼저 찾은 칸을 지켰는데, 배치 셀 이웃이 전부 Walk 이면
        // (Walk 위 배치 허용 이후로는 보통) 거리 1 동점자 중 남쪽이 항상 이겼다 — 실측 252칸 중 173칸이
        // (0,-1). 총구가 사실상 남쪽에 고정돼 사용자 플레이 4회 배치가 전부 affected=0 이었다.
        //
        // 새 규칙(사용자 결정 2026-08-15):
        //   1) 조준이 있으면 그 방향. 방향 지정 유닛의 조준을 스킬이 물려받는다 — 활성화가
        //      `DeployedFacing` 을 on-place **앞에** 붙여 두는 것이 이걸 위해서였다.
        //   2) 없으면 사거리 안 가장 가까운 적 방향. 조준 UX 가 없는 유닛(마크스맨 등)의 규칙.
        //
        // **조준이 최근접보다 세다.** 조준은 방향만 정하고, 사건 성립(후보 존재)은 호출처가
        // 이미 판정했다. 그래서 조준 방향에 아무도 없어도 발사는 일어나고 명중이 0일 수 있다 —
        // 어디를 쏠지는 플레이어 몫이라는 뜻이다. 후보가 비어 있지 않은 것은 호출처 계약이다.
        //
        // on-place-shuttle-shotgun unit 1 — 판정 자체는 **순수 함수 `SkillAim` 가 소유**한다
        // (skill-layer-migration unit 1 에서 도메인 어셈블리로 이사했다 — 규칙은 무변경).
        // 규칙 경로(배치 스킬의 방향 발사)가 두 번째 소비자가 되면서 뽑았다 — 두 벌로 두면 한쪽만
        // 고쳐지는 날이 온다. 여기 남는 것은 «엔티티에서 값을 꺼내는 일» 과 **레거시 폴백** 뿐이다.

        // Recomputes adjacency synergy for `cell` and its eight neighbors. Same-type
        // defender adjacency grants a damage multiplier of (1 + 0.1 × neighborCount).
        // Writes to SynergyBuff go through EffectSpawner so the Effects-context
        // write gateway stays a single code path (Phase 2 decision #9).
        // defender-footprint unit 3 — 시너지 스캔 스크래치(할당 재사용).
        private readonly List<(Entity entity, DefenderUnitData data, RectInt rect)> _synergyScratch = new();

        // defender-footprint 리뷰 M-6 — «등록 스냅샷» 방어선을 검사 쪽에도. SO footprint 가
        // 배치 후(시트 임포트) 바뀌어도 시너지·재배치 자기-겹침은 **실제 점유 칸** 기준이어야
        // ReleaseDefenderFootprint 와 같은 rect 를 본다. 미등록(이론상 없음)이면 SO 폴백.
        private RectInt RegisteredFootprintRect(Vector2Int primary, Vector2Int fallbackSize)
        {
            bool any = false;
            int minX = 0, minY = 0, maxX = 0, maxY = 0;
            foreach (var kv in _defenderCellOwner)
            {
                if (kv.Value != primary) continue;
                if (!any) { minX = maxX = kv.Key.x; minY = maxY = kv.Key.y; any = true; }
                else
                {
                    if (kv.Key.x < minX) minX = kv.Key.x;
                    if (kv.Key.x > maxX) maxX = kv.Key.x;
                    if (kv.Key.y < minY) minY = kv.Key.y;
                    if (kv.Key.y > maxY) maxY = kv.Key.y;
                }
            }
            if (!any)
                return FootprintMath.Cells(FootprintMath.AnchorFromPrimary(primary, fallbackSize), fallbackSize);
            return new RectInt(minX, minY, maxX - minX + 1, maxY - minY + 1);
        }

        private void RecomputeSynergyFor(Vector2Int cell)
        {
            if (!enableAdjacencySynergy)
            {
                NeutralizeActiveSynergy();
                return;
            }
            if (!HasLiveEntityManager()) return; // 리뷰 L-6 — teardown 경계 방어(관용구 공유)

            // defender-footprint unit 3 — 인접 = 두 footprint rect 의 **체비셰프 거리 1(둘레 접촉)**.
            // 대표 셀 8이웃으로 재면 3×3 유닛의 8이웃이 전부 자기 몸 안이라 시너지가 죽는다.
            // 1×1 끼리 거리 1 = 기존 8이웃과 동치(무회귀).
            //
            // 국소 재계산(변경 셀 3×3) → **전수 재계산**: footprint 제거 직후엔 반납된 rect 를
            // 여기서 알 수 없어 국소화가 성립하지 않는다. 판 위 유닛은 수십 기라 O(n²) rect
            // 비교는 무시 가능, EnqueueSynergyMul refresh 는 멱등이라 과잉 enqueue 무해.
            // cell 파라미터는 호출 계약(변경 지점 통보)으로 유지하되 계산엔 쓰지 않는다.
            _ = cell;
            _synergyScratch.Clear();
            foreach (var kv in _defenderByTile)
            {
                if (!_em.Exists(kv.Value.entity) || _em.HasComponent<PendingDeployment>(kv.Value.entity)) continue;
                var size = kv.Value.data != null ? kv.Value.data.Footprint : Vector2Int.one;
                var rect = RegisteredFootprintRect(kv.Key, size); // 리뷰 M-6 — 등록 스냅샷 기준
                _synergyScratch.Add((kv.Value.entity, kv.Value.data, rect));
            }

            for (int i = 0; i < _synergyScratch.Count; i++)
            {
                var here = _synergyScratch[i];
                int neighbors = 0;
                for (int j = 0; j < _synergyScratch.Count; j++)
                {
                    if (i == j) continue;
                    var other = _synergyScratch[j];
                    if (other.data != here.data) continue;
                    if (FootprintMath.RectChebyshevDistance(here.rect, other.rect) == 1)
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
        // skill-layer-migration unit 4d — `EnqueueAttackSpeedMul` 은 은퇴했다. 유일한
        // 호출처가 마지막 불꽃 bake 였고 그게 concrete 로 갔다(공속 버프는 이제 스킬이
        // `ApplyStatModifier` 로 낸다). 형제 `EnqueueMoveSpeedMul` 은 살아 있다.

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

        // placement-armed-board-drag unit 4 — 화면 → 보드 **소수** 셀좌표. clamp 도 bounds 판정도 하지
        // 않고 좌표만 넘긴다: 격자 밖 관용을 얼마나 줄지는 `PlacementCellSnap.Resolve` 가 단독으로
        // 소유하는 정책이고(트레이 D&D 가 이미 그걸 쓴다), 여기서 한 번 더 판정하면 규칙이 두 곳으로
        // 갈라져 한쪽만 튜닝되는 드리프트가 생긴다. 반환 frac 은 `DebugWorldToCellFractional` 과
        // 같은 공간(셀 중심=정수) — 즉 `GridMath.WorldToCell` 과 드리프트 없음.
        public bool TryScreenToBoardFrac(Camera cam, Vector2 screenPos, out Vector2 frac)
        {
            frac = default;
            if (cam == null) return false;
            var ray = cam.ScreenPointToRay(screenPos);
            var plane = Wassup.Core.BoardSpace.RaycastPlane();
            if (!plane.Raycast(ray, out float enter)) return false;
            var world = (Vector3)Wassup.Core.BoardSpace.ToSim(ray.GetPoint(enter));
            frac = DebugWorldToCellFractional(world);
            return true;
        }

        // subconscious-curse-expansion unit 3 (살찌운 제물) — 드롭 지점 최근접 적 픽.
        // 반경 = radiusTiles × tileSize(유클리드 xz, 셀 양자화 없이 평면 히트 그대로).
        // 픽은 커밋 순간의 스냅샷 — 이후 이동은 무관. 동거리 동점은 `SimEntityId`
        // 오름차순(= 먼저 스폰된 쪽; battle-sim-extraction M0 unit 1 에서 `Entity.Index`
        // 에서 갈아탔다 — 할당기 번호는 신 sim 에서 재현이 불가능하다).
        // 반경 내 없음 = false(무차감).
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
                int bestSimId = Wassup.Battle.Units.SimEntityId.Unassigned;
                for (int i = 0; i < entities.Length; i++)
                {
                    Vector3 d = (Vector3)transforms[i].Position - world;
                    d.y = 0f;
                    float sq = d.sqrMagnitude;
                    int simId = _em.HasComponent<Wassup.Battle.Units.SimEntityId>(entities[i])
                        ? _em.GetComponentData<Wassup.Battle.Units.SimEntityId>(entities[i]).value
                        : Wassup.Battle.Units.SimEntityId.Unassigned;
                    if (sq < bestSq ||
                        (sq == bestSq && enemy != Entity.Null && simId < bestSimId))
                    {
                        bestSq = sq;
                        bestSimId = simId;
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
            // defender-footprint unit 1 — footprint 임의 칸(발밑 탭)도 그 유닛으로 해석.
            if (TryResolveDefenderKey(cell, out var key) && _defenderByTile.TryGetValue(key, out var binding))
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
        // defender-footprint unit 4 — 픽 재설계. ① 포함 후보 중 **앞면(렌더 순서) 우선** —
        // 예전 «중심 최근접»은 겹침 영역에서 손가락이 앞 유닛 몸 위인데 뒤 유닛 중심이 더
        // 가까우면 뒤가 뽑히는 구조적 오선택이 있었다. 판정이 렌더 순서를 **읽기만** 하는
        // 것이라 렌더-입력 분리 계약(README 계약 8)은 유지된다(가려진 뒤 유닛은 노출 부위·
        // 발밑 셀 폴백·자석으로 여전히 도달). 동률은 중심 최근접.
        // ② paddingPx: 스프라이트보다 넓은 픽 영역(1폭 유닛 가로 확대 — 요구 문서 10절).
        // ③ magnetPx: 포함 후보가 없을 때 확장 렉트까지의 거리 ≤ 이 값인 최근접 흡착.
        // ④ magnetFilter(rev 2026-08-28): 자석은 «가까운 **유효** 유닛»만 잡는다(요구 문서 8절) —
        //    호출부의 유효 집합(부착 스냅샷 등)을 받아 무효 유닛으로의 흡착을 막는다. **포함
        //    판정에는 적용하지 않는다** — 손가락이 직접 올라간 무효 유닛은 잡아서 invalid 폼으로
        //    «왜 안 되는지»를 보여주는 것이 lock-on 계약이다(dreamcatcher-attach-lockon C).
        // 기본 파라미터(0,0,null)의 포함 집합은 기존과 동일하다.
        public bool TryPickDefenderAtScreen(Camera cam, Vector2 screenPos, out Entity defender, out Vector2Int cell,
            float paddingPx = 0f, float magnetPx = 0f, HashSet<Entity> magnetFilter = null)
        {
            defender = Entity.Null;
            cell = default;
            if (cam == null || spineUnitPool == null) return false;
            int bestOrder = int.MinValue;
            float bestCenter = float.MaxValue;
            float bestMagnet = float.MaxValue;
            int bestMagnetOrder = int.MinValue;
            var gridSize = _generatedMap.IsCreated ? _generatedMap.gridSize : new int2(1, 1);
            foreach (var kv in _defenderByTile)
            {
                if (!spineUnitPool.TryGet(kv.Value.entity, out var view) || view == null) continue;
                if (!view.TryGetScreenRect(cam, out var rect)) continue;
                if (paddingPx > 0f)
                {
                    rect.xMin -= paddingPx; rect.xMax += paddingPx;
                    rect.yMin -= paddingPx; rect.yMax += paddingPx;
                }
                // 리뷰 M-2/L-3 — 렌더 정렬은 뷰 월드(짝수 변 +0.5칸 오프셋 포함)의 RoundToInt 로
                // 돈다(SpineUnitView.UpdateSortingOrder → ComputeFromWorld). 픽도 같은 오프셋을
                // 같은 반올림으로 미러해야 짝수 footprint 에서 «앞면 우선»이 실제 앞면과 일치한다.
                var pickOff = FootprintMath.CenterOffsetFromPrimary(
                    kv.Value.data != null ? kv.Value.data.Footprint : Vector2Int.one);
                int order = Wassup.Presentation.BoardSortOrder.Compute(gridSize,
                    Mathf.RoundToInt(kv.Key.x + pickOff.x), Mathf.RoundToInt(kv.Key.y + pickOff.y));
                if (rect.Contains(screenPos))
                {
                    float dc = (rect.center - screenPos).sqrMagnitude;
                    if (order > bestOrder || (order == bestOrder && dc < bestCenter))
                    {
                        bestOrder = order;
                        bestCenter = dc;
                        defender = kv.Value.entity;
                        cell = kv.Key;
                    }
                }
                else if (bestOrder == int.MinValue && magnetPx > 0f)
                {
                    // rev — 자석은 유효 유닛만(필터가 오면). 포함 분기는 위에서 이미 지나갔다.
                    if (magnetFilter != null && !magnetFilter.Contains(kv.Value.entity)) continue;
                    // 리뷰 M-5 — 자석 동률도 앞면 우선(포함 분기와 같은 규칙). 부동소수 동률은
                    // 1px 미만 오차 밴드로 판정해 열거 순서 의존을 제거한다.
                    float d = ScreenDistanceToRect(rect, screenPos);
                    if (d > magnetPx) continue;
                    bool closer = d < bestMagnet - 0.5f;
                    bool tieFront = Mathf.Abs(d - bestMagnet) <= 0.5f && order > bestMagnetOrder;
                    if (closer || tieFront)
                    {
                        bestMagnet = d;
                        bestMagnetOrder = order;
                        defender = kv.Value.entity;
                        cell = kv.Key;
                    }
                }
            }
            return defender != Entity.Null;
        }

        // defender-footprint unit 4 — 스크린 점→렉트 거리(내부 0). 픽 자석·락온 히스테리시스가 공유.
        public static float ScreenDistanceToRect(Rect r, Vector2 p)
        {
            float dx = Mathf.Max(Mathf.Max(r.xMin - p.x, 0f), p.x - r.xMax);
            float dy = Mathf.Max(Mathf.Max(r.yMin - p.y, 0f), p.y - r.yMax);
            return Mathf.Sqrt(dx * dx + dy * dy);
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
                // on-place-skill-rework unit 10 — 낙하 텔레그래프 × **적 하나**.
                // 위 SkyFall 과 그림은 같고 조준이 다르다. 이 짝이 없어서 unit 1·8 이 셀 조준
                // 궤적으로 적 조준을 흉내냈고, 한 탄에 조준이 둘이 되어 예고 시간만큼 어긋났다.
                ProjectileFlightMode.SkyFallOnTarget => (MovementKind.SkyFallOnEntity, PayloadKind.SingleSplash),
                // dreamcatcher-content-5 unit 0 — 왕복(부메랑) × 경로 스윕.
                // Directional 과 같은 페이로드 짝이다 — 둘 다 «비행 중 스치는 것을 때리고
                // 비행이 끝나면 소멸» 이라 착탄 지점 개념이 없다.
                ProjectileFlightMode.Boomerang => (MovementKind.BoomerangReturn, PayloadKind.PathHit),
                // bomb-barrel-on-place unit 3 — 곡사 × **설치물 세우기**. 궤적은 위
                // BallisticToCell 과 같고 착탄에서 하는 일만 다르다(피해 ↔ 물건).
                ProjectileFlightMode.BallisticBlocker => (MovementKind.BallisticArcToPoint, PayloadKind.SpawnBlocker),
                _ => (MovementKind.HomingToEntity, PayloadKind.SingleSplash),
            };

        // content-5 unit 2 — 넉백은 「거리 ÷ 시간」 저작이라 **둘 다 양수일 때만** 성립한다.
        // 두 필드가 서로를 게이트하던 형태(각자 상대를 검사)를 하나로 접었다.
        private static bool KnockbackOn(ProjectileData p)
            => p != null && p.knockbackDistance > 0f && p.knockbackDuration > 0f;

        // skill-layer-migration unit 2e — 스킬이 트는 **뷰 프리팹** 표. 투사체 SO 표와
        // 같은 규약(bake 가 index 를 굽고 런타임은 index 로만 부른다) — 어댑터가
        // `GameObject` 를 들면 도메인 쪽 어셈블리 경계가 흐려진다.
        private readonly System.Collections.Generic.List<GameObject> _skillVfxPrefabs = new();
        private readonly System.Collections.Generic.Dictionary<GameObject, int> _skillVfxIndex = new();

        private int GetOrCreateSkillVfxIndex(GameObject prefab)
        {
            if (prefab == null) return -1;
            if (_skillVfxIndex.TryGetValue(prefab, out var idx)) return idx;
            idx = _skillVfxPrefabs.Count;
            _skillVfxPrefabs.Add(prefab);
            _skillVfxIndex[prefab] = idx;
            return idx;
        }

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

        // 유출 누적(_goalReachedCount)은 HUD 에 그리지 않는다 — 아무것도 판정하지 않는
        // 수치를 점수 옆에 세워두면 «관리해야 할 자원»으로 잘못 읽힌다. 카운터 자체는
        // 살아 있고 완주 로그와 MatchTally 집계로만 나간다.

        // three-minute-survival unit 0 — 안정도 만피 복귀. _battleClock 리셋과 짝이다.
        private void ResetGoalStability()
        {
            // heart-stress-axis — 연출 상태는 판 경계에서 명시적으로 지운다
            // (`_breachedCells`·`_goalCrackStage` 와 같은 규칙). rev 2 의 「보드 잠식」은
            // 은퇴했고 지금 남는 것은 심박 위상·단계·방패 플래그·돌격 피격 수다.
            _lastHeartStress = 0f;
            _heartBeatPhase = 0f;
            _heartBarPunch = 0f;   // unit 9 rev 2 — 안 지우면 새 판 첫 프레임에 바가 부푼 채 뜬다
            ReleaseCoreBurstHold(stopRoutine: true);   // unit 10 — 슬로우 리스가 판을 넘어가지 않게
            _heartStage = 0;
            _coreShielded = false;
            _rusherArrivalCount = 0;
            scoreHud?.SetHeartStress(0f, 1f, 0f);
            _goalStabilityMax = ActiveDeck != null ? Mathf.Max(1, ActiveDeck.goalStabilityMax) : 0;
            _goalStability = _goalStabilityMax;
            // unit 10 — 적 마음 축도 매치 경계에서 소멸한다. 실제 max 는 스폰이 확정한다
            // (저작에서 오므로 덱만 보고는 알 수 없다) — 여기서는 «축 비활성» 으로 초기화.
            _enemyCoreMax = 0;
            _enemyCoreCurrent = 0;
            _breachedCells.Clear();   // stress-after-breach — 붕괴 상태는 매치 경계에서 소멸(이월 금지)
            _goalCrackStage.Clear();  // unit 2 — 균열 단계도 같은 규칙(프랍 루트 재빌드가 색을 원복한다)
            _leakTypeMissLogged = false;
            _towerMissLogged = false;
            _killHealTypeMissLogged = false;
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
        //                                 (OccupiedCellsBuffer — 기존 소비자가 그대로 처리).
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
                    AttachSimEntityId(tower);
                    _em.AddComponentData(tower, new Health { value = _goalStabilityMax, max = _goalStabilityMax });
                    _em.AddBuffer<IncomingDamage>(tower);
                    // heart-stress-axis unit 2 — 악몽 처치가 마음을 회복시킨다. 힐은 이미
                    // DamageApplicationSystem 의 **같은 줄**이 처리한다(min(max, value − dmg + heal))
                    // — 붙일 것은 이 버퍼 하나뿐이고 새 시스템도 새 채널도 없다.
                    // ⚠ GoalTowerTag 의 계약을 뒤집는 지점이다(그 파일 주석 참조).
                    _em.AddBuffer<Wassup.Battle.Units.IncomingHeal>(tower);
                    _em.AddComponentData(tower, new FactionTag { value = Faction.DefenderCore });
                    _em.AddComponentData(tower, LocalTransform.FromPosition(
                        GridToWorldCenter(new Vector2Int(cell.x, cell.y))));
                    _structureRegistry.Add((tower, new Vector2Int(cell.x, cell.y), Faction.DefenderCore));
                }
                _goalTowerCount = count;
                Debug.Log($"[BattleBridge] Goal towers spawned: {count} @ stability {_goalStabilityMax}");
                // heart-stress-axis unit 0 — 마음 1개 전제(명제 10)의 표면화. **하드 에러는 두지
                // 않는다** — `goals[]` 기계를 막는 것은 map-rework 계약 3("멀티골 기계는 건드리지
                // 않는다") 소관이라 이 spec 이 하면 스코프 위반이다. 여기서는 저작 사고를 보이게만 한다.
                if (count > 1)
                    Debug.LogWarning($"[BattleBridge] 마음이 {count}개 스폰됐다 — heart-stress-axis 는 "
                        + "1개 전제다. 종료는 «첫» 마음 파괴에서 일어난다(계약).", this);
            }

            // ── 저작 거점(본능 + 적 마음) — unit 3 의 _resolvedMapDoc 에서 SO 스탯을 읽는다 ──
            // map-diorama-stage unit 10 — 입력 = 스테이지 StructureMarker(본능만, 계약 11). 문서 시절과 같은
            // StructureEntry 형태라 아래 로직은 그대로다.
            var docStructures = _stageStructures;
            if (docStructures.Count == 0) return;
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
                AttachSimEntityId(entity);
                _em.AddComponentData(entity, new Health { value = s.data.health, max = s.data.health });
                _em.AddBuffer<IncomingDamage>(entity);
                _em.AddComponentData(entity, new FactionTag { value = faction });
                _em.AddComponentData(entity, LocalTransform.FromPosition(GridToWorldCenter(s.cell)));

                // unit 10 — 적 마음 축의 활성 조건을 여기서 확정한다(저작에서 오므로 덱만
                // 보고는 알 수 없다). 합으로 두는 이유: 계약 6 은 공성 맵에 적 마음 1개를
                // 강제하지만, 그 규칙이 완화되어도 판정이 조용히 «첫 마음만» 이 되지 않는다.
                if (faction == Faction.EnemyCore)
                    _enemyCoreMax += Mathf.Max(0, Mathf.RoundToInt(s.data.health));

                // 본능 3×3 **점유** 선언 — 차단이 아니다. 사거리를 「가장 가까운 벽면까지」로
                // 재는 데 쓰이고(AttackSystem), 흐름장 목적지의 BFS 소스가 된다.
                // 통행을 막는 것은 `BlockingHazard` 컴포넌트를 **함께** 든 방벽뿐이다
                // (instinct-content unit 1 — 옛 계약 12「본능 footprint 는 벽」은 폐기).
                if (Wassup.Data.StructurePlacements.IsInstinct(faction))
                {
                    int half = Wassup.Data.StructurePlacements.FootprintOf(faction) / 2;
                    var cells = _em.AddBuffer<Wassup.Battle.Effects.OccupiedCellsBuffer>(entity);
                    for (int dy = -half; dy <= half; dy++)
                        for (int dx = -half; dx <= half; dx++)
                            cells.Add(new Wassup.Battle.Effects.OccupiedCellsBuffer
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

                // 뷰는 여기서 만들지 않는다 — 맵 수명이라 BuildMapForBattle 이 소유한다(후속 2).
                _structureRegistry.Add((entity, s.cell, faction));
                spawned++;
            }
            // unit 10 — 첫 Sync 전에도 타이머 비교가 옳은 값을 보게 한다. Sync 가 매 프레임
            // 갱신하지만 초기값을 여기서 못 박아 «호출 순서에 의존하지 않는» 상태로 둔다.
            _enemyCoreCurrent = _enemyCoreMax;
            if (spawned > 0)
                Debug.Log($"[BattleBridge] Structures spawned: {spawned} (본능/적 마음, SO HP)"
                    + (_enemyCoreMax > 0 ? $" · 적 마음 축 활성 (max {_enemyCoreMax})" : string.Empty));
        }

        // battle-structures 후속 2(리뷰 M-5) — 거점 프랍 생성. **맵 빌드 시점**이라 배치
        // 페이즈부터 보인다(footprint 배치 배제가 이미 파생된 그 시점). 엔티티(판 수명)와 분리돼
        // 있으므로 여기서 등록부를 건드리지 않는다 — 게이지는 등록부 기반이라 전투 시작
        // 전까지 안 뜨는 게 맞다(체력은 아직 없다).
        // sim→view 는 BoardSpace.ToView 경유(Pickup 프레젠터 선례). 프리팹 미지정은 무해.
        private void SpawnStructureViews()
        {
            ClearStructureViews();   // 멱등 — 재빌드마다 정확히 1벌
            // map-diorama-stage unit 10 — 입력 = 스테이지 StructureMarker(본능만, 계약 11). 문서 시절과 같은
            // StructureEntry 형태라 아래 로직은 그대로다.
            var docStructures = _stageStructures;
            if (docStructures.Count == 0) return;
            for (int i = 0; i < docStructures.Count; i++)
            {
                var s = docStructures[i];
                if (s.data == null || s.data.viewPrefab == null) continue;
                // 스폰과 같은 필터 — 방어 마음은 goals[] 정본이라 거점 프랍을 세우지 않는다
                // (골 구조물 프랍은 theme.goalStructureProp 이 이미 담당).
                if (Wassup.Data.StructurePlacements.DeriveFaction(s.side, s.data.kind)
                    == Faction.DefenderCore) continue;

                float3 simCenter = GridToWorldCenter(s.cell);
                var view = Instantiate(s.data.viewPrefab,
                    (Vector3)Wassup.Core.BoardSpace.ToView(simCenter), Quaternion.identity, transform);
                // instinct-content unit 0 rev — SO 스케일 knob (프리팹 원본 스케일에 곱).
                view.transform.localScale *= s.data.viewScale;
                view.name = $"Structure_{s.data.displayName}_{s.cell.x}_{s.cell.y}";
                _structureViews.Add(view);
                // instinct-turret-readout unit 1 — 포신을 가진 프랍이면 셀로 등록해 둔다.
                // 「포신을 갖는가」는 **컴포넌트 유무**가 결정한다(kind 분기도 id 분기도 없다).
                // 자식까지 훑는다 — 지금 두 변형은 루트에 달고 있지만, 리그가 깊어진 프랍이
                // 프리젠터를 자식에 달면 루트 전용 탐색은 **경고도 없이** 조준을 끈다(리뷰 low).
                // 거점이 포신을 안 갖는 것 자체는 정상이라(마음) 미발견은 경고 대상이 아니다.
                var turret = view.GetComponentInChildren<Wassup.Presentation.StructureTurretView>();
                if (turret != null) _structureTurretsByCell[s.cell] = turret;
                // instinct-wreck unit 0 — 잔해 프리젠터도 같은 판단으로 등록한다. 「잔해를
                // 갖는가」는 **컴포넌트 유무**가 정한다(kind 분기도 id 분기도 없다). 미발견은
                // 경고 대상이 아니다 — 마음은 원래 이 프리젠터가 없고 그게 정상이다.
                var wreck = view.GetComponentInChildren<Wassup.Presentation.StructureWreckView>();
                if (wreck != null) _structureWrecksByCell[s.cell] = wreck;
            }
        }

        // 리뷰 H-4 — 뷰 정리는 TeardownGeneratedMap(맵 수명)과 TeardownCurrentBattle 이 공유한다.
        private void ClearStructureViews()
        {
            for (int i = 0; i < _structureViews.Count; i++)
                if (_structureViews[i] != null) Destroy(_structureViews[i]);
            _structureViews.Clear();
            _structureTurretsByCell.Clear();   // 프리젠터는 뷰와 같은 수명 — stale 참조 방지
            _structureWrecksByCell.Clear();    // instinct-wreck unit 0 — 형제와 같은 지점
        }

        private void DestroyStructureEntities()
        {
            // 뷰는 지우지 않는다 — 맵 수명이라 TeardownGeneratedMap 이 소유한다(후속 2).
            // 여기서 지우면 StartBattle(SpawnStructureEntities → 이 메서드)이 배치 페이즈에
            // 세워둔 프랍을 매번 날린다.
            _goalTowerCount = 0;
            _structureRegistry.Clear();
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

        // battle-structures unit 10 — 적 마음 축의 읽기 창구(위와 대칭). Max 0 = 이 축 비활성
        // (= 침략 맵). 3분 만료 판정이 `GoalStabilityCurrent >= EnemyCoreCurrent` 이므로 이 둘을
        // 읽으면 화면에서 판정을 검산할 수 있다 — 그게 이 API 의 존재 이유다(unit 11 이 소비).
        public int EnemyCoreCurrent => _enemyCoreCurrent;
        public int EnemyCoreMax => _enemyCoreMax;

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
            return true;
        }

        private void DrainGoalEvents()
        {
            if (!_goalEventQueue.IsCreated) return;
            while (_goalEventQueue.TryDequeue(out var evt))
            {
                Wassup.Core.Trace.LegacyTraceRecorder.Ev(Wassup.Core.Trace.TraceChannel.GoalReached,
                    SimIdOf(evt.entity), i: evt.canSiege ? 1 : 0);
                // battle-structures unit 4(ⓐ) — 판정은 셀 단위: **이 적이 도달한 골**(최근접
                // 골 셀)이 부서졌는가. 골 2개 맵에서 한쪽만 부서지면 그쪽 도달만 유출이고
                // 다른 쪽 도달은 여전히 공성이다.
                bool breached = _breachedCells.Contains(NearestGoalCell(evt.position));

                // heart-stress-axis unit 0 rev 3 — **이 분기는 도달 불가다.** 첫 마음 파괴에
                // 판이 끝나므로 `_breachedCells` 를 소비할 프레임이 없다(계약). 남겨두는 것은
                // 계약이 뒤집힐 때의 안전망이자 되돌리기 비용 때문이다.
                // 세는 일 자체는 아래 돌격형 분기로 옮겼다 — 이 판에서 「놓쳤다」는 사건은
                // «부서진 마음으로 흘러듦» 이 아니라 «돌격형이 마음을 치고 산화함» 이다.
                if (breached)
                {
                    _goalReachedCount++;
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
                // heart-stress-axis unit 0 rev 2 — **돌격형은 몸통박치기로 마음을 직격한다.**
                //
                // 여기로 오는 것은 공격 수단이 없는 적(`attackMethod: None` = Runner·Swift)뿐이다.
                // 그들의 정체성은 **속도**(moveSpeed 2.5 = Basic 1.3 의 약 2배)이고, 도달하면
                // 소멸하면서 `stabilityDamage` 를 마음에 한 번에 꽂는다 — 공성형이 서서히
                // 갉는 것과 대비되는 «한 방» 어휘다.
                //
                // rev 1 은 이 피해를 아예 끊었었다(「공격력 없는 적은 피해 0」). 뒤집은 이유:
                // 처치가 곧 회복인 구조(unit 2)에서 **안 잡히고 통과한 적은 점수·각성치·회복
                // 셋을 동시에 못 벌게 한다.** 거기에 피해까지 0 이면 돌격형은 판단할 거리가
                // 없는 교통량이 된다. 「빠르게 이동하되 마음에 직접 타격」이 확정 컨셉이다.
                //
                // 값 대역은 `AttackUnitData.stabilityDamage` 가 소유한다(하드코딩 금지).
                // 등록부 제거는 킬 경로(DrainEnemyKilledEvents)와 대칭 — 빼지 않으면 누적된다.
                // rev 4 — **놓친 수는 자기 카운터를 갖는다.** rev 3 은 여기서 `_goalReachedCount`
                // 를 올렸는데, 그 카운터는 **휴면이 아니라 라이브 게이트를 먹인다**:
                //   `RemainingLeakAllowance()` = 덱 `defeatGoalReachedCount`(10) − `_goalReachedCount`
                //   → 「몽마의 계약」(`leakAllowanceCost` 1)이 «잔여 − cost < 1» 이면 **부착 거절**
                // 즉 **돌격형 9기가 통과하면 그 카드가 판 내내 영영 안 붙었다**(코드 리뷰 발견).
                // Runner·Swift 는 13개 덱 전부에 `maxPerWave: 0`(무제한)이라 3분 판에서 9기는 흔하다.
                // README 계약이 「유출 축은 판정만 끊고 **휴면**」이라 선언했는데 코드가 그걸 어겼다.
                //
                // 공성형은 마음 앞에서 살아 있어 아직 잡을 수 있지만(= 안 놓쳤다), 돌격형은
                // 도달하는 순간 사라져 회복 통로가 닫힌다 — 이 판에서 「놓쳤다」로 셀 수 있는
                // 유일한 사건이고 `MatchTally.Leaks` 가 이 값을 나른다.
                _rusherArrivalCount++;

                int rushDamage = 0;
                if (_enemyTypeByEntity.TryGetValue(evt.entity, out var leakedType) && leakedType != null)
                {
                    rushDamage = Mathf.Max(0, leakedType.stabilityDamage);
                    _enemyTypeByEntity.Remove(evt.entity);
                }
                else if (!_leakTypeMissLogged)
                {
                    // 조용히 0 으로 넘기면 돌격형이 무해해진다 — 경고 1회.
                    _leakTypeMissLogged = true;
                    Debug.LogWarning("[BattleBridge] 도달한 돌격형의 데이터가 등록부에 없다 — 마음 직격 0 으로 넘긴다.", this);
                }
                // heart-stress-axis unit 6 — 방패가 서 있으면 돌격형도 마음을 못 친다.
                // 공성형은 «후보 제외» 로 조준 자체가 안 가지만, 돌격형은 조준이 아니라
                // «도달» 로 오므로 여기서 따로 막는다(같은 규칙의 두 입구).
                // `breached` 가 참인 프레임은 존재하지 않는다(첫 붕괴에 판이 끝난다) — 가드는
                // 계약이 뒤집힐 때를 위한 안전망으로 남긴다.
                if (!breached && !_coreShielded) EnqueueGoalTowerDamage(rushDamage, evt.position);
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

        // heart-stress-axis unit 2 — 악몽 처치 → 마음 회복. `EnqueueGoalTowerDamage` 의 형제이나
        // **대상 선택이 다르다**: 피해는 「위치 최근접 1기」인데 회복은 **살아있는 마음 전체**에
        // 넣는다(feature 계약). 피해는 «어느 마음이 맞았나» 가 사건의 일부지만 회복은 그렇지
        // 않고, 최근접으로 두면 마음이 둘인 저작 사고에서 만피 쪽이 흡수해 clamp 로 소멸시킨다.
        // (마음 1개 전제에서는 두 규칙이 동치라 **테스트로 구분되지 않는다** — 그래서 계약이다.)
        private void EnqueueGoalHeal(int awakeningReward)
        {
            if (!HasLiveEntityManager() || awakeningReward <= 0) return;
            float mul = ActiveDeck != null ? ActiveDeck.killHealPerAwakening : 0f;
            float amount = awakeningReward * mul;
            if (amount <= 0f) return;   // 배율 0 = 회복이 꺼진 판
            using var towerQuery = _em.CreateEntityQuery(
                ComponentType.ReadOnly<Wassup.Battle.Units.GoalTowerTag>(),
                ComponentType.ReadWrite<Wassup.Battle.Units.IncomingHeal>());
            if (towerQuery.IsEmpty) return;   // 마음이 없는 판(미저작·붕괴 후) — 정상, 경고 없음
            using var towers = towerQuery.ToEntityArray(Allocator.Temp);
            for (int i = 0; i < towers.Length; i++)
                _em.GetBuffer<Wassup.Battle.Units.IncomingHeal>(towers[i])
                   .Add(new Wassup.Battle.Units.IncomingHeal { amount = amount });
        }

        // goal-tower-siege(rev 2) — 돌격형(공격 수단 없는 적)의 자폭 피해. 표준 경로와 같은
        // 통로(IncomingDamage)로 넣어 DamageApplicationSystem 이 처리하게 한다.
        // 적이 도달한 골이 어느 쪽인지는 이벤트에 실린 위치로 가른다(골 2개 맵).
        //
        // heart-stress-axis unit 0 rev 2 — 살아 있다. 호출처는 `DrainGoalEvents` 하나이고
        // 돌격형(Runner·Swift)의 **마음 직격**을 나른다. 값은 SO 의 `stabilityDamage`.
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
        // unit 2 — 골 셀별 마지막 균열 단계. 매치 경계에서 비운다(_breachedCells 와 같은 규칙).
        private readonly Dictionary<Vector2Int, int> _goalCrackStage = new();

        // 남은 비율 → 4단계(온전/1/2/3). 경계를 넉넉히 잡아 «맞자마자 새까매지는» 인상을 피한다.
        private void PushGoalCrack(Vector2Int cell, in Health health)
        {
            float ratio = Health.ComputeRatio(health.value, health.max);
            int stage = ratio > 0.75f ? 0 : ratio > 0.5f ? 1 : ratio > 0.25f ? 2 : 3;
            if (_goalCrackStage.TryGetValue(cell, out int prev) && prev == stage) return;
            _goalCrackStage[cell] = stage;
            if (_goalMarkersByCell.TryGetValue(cell, out var crackMarker) && crackMarker != null)
                crackMarker.SetCrackStage(stage);   // unit 4 — 마커 뷰가 균열 연출 소유
        }

        private void SyncGoalStability()
        {
            // 리뷰 M-7 — 게이트는 등록부 유무다. 구 _goalTowerCount 게이트는 «골 타워 개수»
            // 라는 무관한 개념에 거점(본능·적 마음) 붕괴 관측까지 가둬, 덱 미저작 판에서
            // 거점 붕괴가 영구 미관측이었다.
            if (!HasLiveEntityManager() || _resultShown || _structureRegistry.Count == 0) return;

            float lowest = float.MaxValue;
            float maxHp = 0f;
            float enemyCoreRemaining = 0f;   // unit 10 — 적 마음 축의 잔여(같은 순회에 얹는다)
            bool newCoreBreach = false;
            List<Vector2Int> newBreaches = null;   // 붕괴는 드문 사건 — lazy 할당
            // heart-stress-axis unit 6 — **본능이 마음의 방패다.** 이 순회가 이미 진영과 Health 를
            // 들고 있어 새 쿼리가 필요 없다(적 마음 잔여를 같은 순회에 얹은 선례).
            int liveDefenderInstincts = 0;
            // 필드 재사용 — 매 프레임 도는 순회라 lazy new 는 GC 압력이 된다(_breachedCells 규칙).
            _liveCoresScratch.Clear();
            for (int i = _structureRegistry.Count - 1; i >= 0; i--)
            {
                var (entity, cell, faction) = _structureRegistry[i];
                bool alive = _em.Exists(entity) && _em.HasComponent<Health>(entity);
                var health = alive ? _em.GetComponentData<Health>(entity) : default;   // 리뷰 L-11 — 1회 조회
                if (alive && health.value > 0f)
                {
                    // unit 10 — 적 마음은 자기 축의 잔여로 모은다. 방어 미러와 **섞지 않는다**
                    // (미러는 «가장 위험한 골» 캐시고 이쪽은 «적 본진이 얼마나 남았나» 다).
                    if (faction == Faction.EnemyCore) enemyCoreRemaining += health.value;
                    if (faction == Faction.DefenderInstinct) liveDefenderInstincts++;
                    if (faction != Faction.DefenderCore) continue;   // 본능·적 마음은 미러에 안 섞는다
                    _liveCoresScratch.Add(entity);
                    if (health.value < lowest) lowest = health.value;
                    if (health.max > maxHp) maxHp = health.max;
                    // heart-stress-axis unit 1 rev 2 — **균열 push 를 끊었다.** 마음 프랍의
                    // 틴트 writer 는 이제 `SetGoalStressTint`(스트레스 붉음 + 심박) 하나다.
                    // 같은 렌더러 색을 두 곳이 쓰면 마지막에 쓴 쪽이 이겨 심박이 매 프레임
                    // 그을림으로 덮인다. `PushGoalCrack`/`SetGoalCrack` 은 휴면(삭제 안 함).
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
                        // 그 사슬(LeakSiegingEnemy → CheckStressDefeat → EndMatch)이
                        // 결과 화면에 _goalStability 를 싣는데, 루프 안에서 열면 지난 프레임의
                        // **양수** 미러가 «부서졌는데 남아 있다» 로 찍힌다.
                        // (unit 6 이전엔 이 값이 서버 제출값에도 실려 더 무거운 순서였다.)
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
                    // instinct-wreck unit 0 — 프랍에게도 알린다. 지금까지 붕괴는 게이지·VFX·로그만
                    // 알고 **프랍은 몰라서** 부서진 포탑이 멀쩡히 서 있었다.
                    if (_structureWrecksByCell.TryGetValue(cell, out var wreckView) && wreckView != null)
                    {
                        // unit 1 — 떼어낸 부품 컨테이너는 **기존 뷰 스윕에 넘긴다**. 새 정리
                        // 경로(OnDestroy 훅)를 만들면 씬 언로드 시점의 fake-null 레이스를 타고,
                        // 그 사고는 이 파일에 이미 실측 주석으로 박혀 있다(retireFlight, 2026-08-15).
                        var debrisRoot = wreckView.Collapse();
                        if (debrisRoot != null) _structureViews.Add(debrisRoot);
                    }
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

            // heart-stress-axis unit 6 — 방패 태그 토글. **writer 는 여기 하나다.**
            // 태그가 하는 일은 «피해 차단» 이 아니라 «타겟 후보에서 제외» 다(CoreShielded 주석).
            // 구조 변경이라 매 프레임이 아니라 **상태가 바뀔 때만** 쓴다 — 판당 최대 두 번
            // (판 시작에 부착, 마지막 본능이 무너질 때 해제).
            // ⚠ **프레임 순서에 의존한다.** 이 구조 변경이 안전한 이유는
            // `MonoBehaviour.Update`(여기) → `BattleSimGroup`(AttackSystem 등) 순서라
            // 시스템들이 스냅샷을 만들 때 아키타입 변경이 **이미 커밋돼 있기** 때문이다.
            // `LateUpdate` 로 옮기거나 sim 이 도는 중에 두 번째 호출처를 만들면
            // `ObjectDisposedException: EntityTypeHandle invalidated by a structural change`
            // 가 돌아온다 — AttackSystem 이 그 사고의 실측 주석을 갖고 있다(FactionTag lookup).
            bool shieldUp = liveDefenderInstincts > 0;
            if (shieldUp != _coreShielded && _liveCoresScratch.Count > 0)
            {
                for (int i = 0; i < _liveCoresScratch.Count; i++)
                {
                    if (!_em.Exists(_liveCoresScratch[i])) continue;
                    if (shieldUp) _em.AddComponent<Wassup.Battle.Units.CoreShielded>(_liveCoresScratch[i]);
                    else _em.RemoveComponent<Wassup.Battle.Units.CoreShielded>(_liveCoresScratch[i]);
                }
                Debug.Log(shieldUp
                    ? $"[BattleBridge] 마음 방패 ON — 살아있는 방어 본능 {liveDefenderInstincts}기"
                    : "[BattleBridge] 마음 방패 OFF — 본능이 모두 무너졌다. 이제 마음이 깎인다.");
                _coreShielded = shieldUp;
            }

            // unit 10 — 적 마음 잔여 갱신. **판정은 아무데서도 하지 않는다**(kill-race unit 0):
            // 적 마음이 무너져도 판은 계속되고, 이 미러는 연출·로그의 입력일 뿐이다.
            // 표시는 올림(방어 미러와 같은 규칙) — 0.3 남았는데 0 으로 보이면 «죽었는데 안 죽었다».
            _enemyCoreCurrent = Mathf.Max(0, Mathf.CeilToInt(enemyCoreRemaining));

            if (!newCoreBreach) return;

            // heart-stress-axis unit 0 — **첫 마음 파괴가 곧 판의 끝이다**(feature 계약).
            // 스트레스 100 == 마음 HP 0 == 이 프레임. `goals` 개수와 **무관하게** «첫» 붕괴에서
            // 끝난다 — 마음이 1개인 동안은 「첫」과 「마지막」이 관측 불가능하게 같으므로
            // 이 선택을 코드와 테스트로 고정해 둔다(StructureSpawnAndBreachTests 2타워 단언).
            //
            // ⚠ **아래 `OpenBreachedCellsForLeak` 을 부르지 않는 것이 「누수가 없다」의 실체다.**
            // EndMatch 가 `_running=false` 를 세워 다음 TickBattleFrame 이 통째로 멈추므로,
            // `_goalReachedCount` 증가 경로 2곳(DrainGoalEvents 의 breached 분기 · LeakSiegingEnemy)에
            // **도달할 프레임이 없다.** (`_breachedCells` 는 이 프레임에 1개가 되지만 아무도 그걸
            // 소비하지 않는다 — 같은 프레임의 DrainGoalEvents 는 이 함수 **앞**에서 이미 돌았다.)
            // 「마지막 마음이 무너져야 끝」으로 바꾸면 첫 붕괴가 배수구를 열어 누수가 되살아난다.
            //
            // 되돌리려면: 아래 EndMatch 를 지우고 `OpenBreachedCellsForLeak(newBreaches);` 를 되살린다.
            Debug.Log($"[BattleBridge] STRESS FULL — 마음이 무너졌다. (처치 {_killCount}기 · 경과 {(float)_battleClock:F1}s)");
            // unit 10 — **연출을 규칙에서 떼어낸다.** 붕괴 VFX·프랍 그을림은 원래
            // `OpenGoalCellAfterBreach`(유출 배수구) 안에만 있어서, 위에서 배수구를 안 부르기로
            // 하자 **연출까지 같이 죽었다**(본능은 나가는데 마음만 안 나가던 이유). 배수구는
            // 계속 안 부르고 — 그게 「누수가 없다」의 실체다 — 연출만 여기서 직접 쏜다.
            PlayCoreBurst(newBreaches);
            EndMatch("stress_full");
        }

        // heart-stress-axis unit 0 — **휴면**(호출처 0). 마음이 무너지는 프레임에 판이 끝나므로
        // 이 코드에 도달할 길이 없다. 지우지 않는 이유는 되돌리기 비용 때문이다 — A/B 판단이
        // 끝나 「누수 없음」이 확정되면 그때 후속 후보(휴면 코드 정리)로 걷어낸다.
        //
        // 원 역할(리뷰 A-M1): 미러가 0 이 된 **뒤에** 붕괴 셀을 유출 지점으로 연다.
        private void OpenBreachedCellsForLeak(List<Vector2Int> breaches)
        {
            if (breaches == null) return;
            for (int i = 0; i < breaches.Count; i++)
                OpenGoalCellAfterBreach(breaches[i]);
            Debug.Log($"[BattleBridge] 골 붕괴 — {_breachedCells.Count}개 셀 유출 전환. 판은 계속된다.");
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
            // waypoint-routing 후속(사용자 결정 B, 2026-08-12) — 붕괴한 골의 프랍을 그을린
            // 붕괴 상태로 전환. 원샷 VFX 만으로는 «이미 뚫린 곳» 이 잔존 표시되지 않아,
            // 이후 여기 도달한 적의 소멸(유출 전환)이 «살아있는 마음을 안 때리는 버그» 로
            // 읽혔다(비행 적이 무저항 완주로 이 장면을 100% 노출하며 표면화).
            if (_goalMarkersByCell.TryGetValue(cell, out var collapseMarker) && collapseMarker != null)
                collapseMarker.MarkCollapsed();   // unit 4 — 마커 뷰가 붕괴 연출 소유
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
        }

        // three-minute-kill-race unit 0 — `CheckStressDefeat()` 는 제거했다. 유출은 이제
        // 아무것도 깎지 않고 «못 잡은 적 = 못 번 점수 + 못 번 각성치» 라는 기회비용만 남는다.
        // 카운터(`_goalReachedCount`)는 HUD·로그 집계로 계속 쓴다.

        public float TimerRemaining => _running ? Mathf.Max(0f, _timerDuration - (float)_battleClock) : 0f;

        // battle-hud-legibility — 이 판이 원래 몇 초짜리였나. HUD 는 이걸 **숫자로 그리지
        // 않는다**(그리는 것은 TimerRemaining 이다) — 「기준이 있는 판인가」를 묻는 용도다.
        // 0 이하 = 무한(엔드리스)이고, 그때 호출측은 타이머 배지를 통째로 숨긴다.
        public float TimerDuration => _timerDuration;

        // unit 7 — `RemainingBattleSeconds()` 는 제거했다. 종료 4경로가 계산해서 넘겼지만
        // 소비처(구 결과 화면의 「남은 시간」 줄)가 이미 죽어 값이 버려지고 있었다.

        // ── 유저 제출 (three-minute-kill-race unit 3) ────────────────────────────
        //
        // **판을 끝낼 수 있는 사람은 유저뿐이다** — 3분 만료 말고는 이 경로 하나다.
        // 시스템이 판을 끝내는 판정은 unit 0 에서 전부 은퇴했다.

        /// <summary>제출 개방까지 필요한 경과(초). P1 — 튜닝 대상.</summary>
        public const float SubmitUnlockSec = 60f;

        /// <summary>「제출」이 열렸는가. 시계는 **Battle 도메인**(`_battleClock`)이다 —
        /// `Time.time` 을 쓰면 메뉴를 열어 둔 시간까지 세어 «안 싸웠는데 열린다».</summary>
        public bool CanSubmit => _running && !_resultShown && (float)_battleClock >= SubmitUnlockSec;

        /// <summary>현재 킬로 성적을 확정하고 판을 끝낸다. 페널티 없음.
        /// 마감 파이프라인(취합 → 기록 → 통보 → 표시)을 그대로 탄다 — 제출 전용 경로를
        /// 따로 만들지 않는 것이 unit 0 단일 관문의 값어치다.</summary>
        public void SubmitMatch()
        {
            if (!CanSubmit) return;   // 재진입·조기 호출 방어(_resultShown 도 여기 포함)
            EndMatch("submitted");
            Debug.Log($"[BattleBridge] SUBMITTED — 유저 제출. (처치 {_killCount}기 · 경과 {(float)_battleClock:F1}s)");
        }

        // three-minute-kill-race unit 0 — 판정 2개를 제거했다:
        //
        // - `CheckEnemyCoreDestroyed()` (적 마음 붕괴 = 즉시 승리) — 이제 부숴도 판은 계속된다.
        //   `_enemyCoreCurrent` 미러는 연출·로그용으로 남는다.
        // - `CheckVictory()` (웨이브 전멸 = 즉시 승리) — 3분에 소진 불가라 이미 사문화였다.
        //   짝인 `NoQueuedAttackersRemain()` 은 **유지**한다: 웨이브 케이던스의 동력이다.
        //
        // 판을 끝내는 것은 시계 하나뿐이고, unit 3 의 유저 제출이 두 번째로 붙는다.
        private void CheckTimer()
        {
            if (_resultShown) return;
            if (_timerDuration <= 0f) return;
            if ((float)_battleClock < _timerDuration) return;

            // **만료 = 완주다.** 예전엔 여기서 두 마음의 남은 체력을 견줘 승/패를 갈랐는데
            // (battle-structures 계약 15 «버틴다»), 패배가 사라지면서 견줄 것이 없어졌다.
            EndMatch("complete");
            Debug.Log($"[BattleBridge] COMPLETE — 3분 완주. (처치 {_killCount}기 · 돌격 피격 {_rusherArrivalCount})");
        }

        // nextwave-clear-attention unit 0 — 최종 승리와 웨이브 사이 클리어가 공유하는
        // emptiness source of truth. pending 은 호출됐지만 아직 스폰되지 않은 적,
        // AttackUnitTag query 는 이미 필드에 나온 적을 각각 담당한다.
        //
        // three-minute-survival unit 2 — 이제 **웨이브 진행 트리거**이기도 하다(QueueDueWaves).
        // 클리어 강조 UI 는 은퇴했지만 이 판정은 그 자리에 남아 케이던스를 구동한다.
        //
        // bonus-wave-pull unit 4(계약 10) — **보너스 적과 보너스 큐는 여기 안 든다.** 보너스 당기기는
        // 서브 컨텐츠라 본류 페이스를 건드리면 안 되는데, 아무것도 안 하면 보너스 적이
        // AttackUnitTag 를 갖는다는 사실만으로 ⓐ 웨이브 진행이 전멸 구동 → 20초 상한 구동으로
        // 강등되고 ⓑ _pullsSinceClear 가 회복되지 않아 일반 당김 알약이 잠긴 채 남는다.
        // 보너스 적은 골에 도달해도 공성으로 살아남으므로(계약 7ⓒ) 그 상태가 그 판 내내 굳는다.
        //
        // ⚠ **_aliveAttackersQuery 자체에 필터를 걸지 말 것.** 그 쿼리는 11곳이 공유하고 거기엔
        // 슬로우·토네이도·메테오 사전집계, CollectEnemiesInTileRange(배치 스킬 대상), 전방
        // 투사체, 밀쳐냄, 골 근접 경보가 들어 있다 — 필터를 걸면 보너스 적이 광역기와 배치
        // 스킬에서 통째로 사라진다. 그래서 전멸 판정 **전용 쿼리**를 따로 세운다.
        private bool NoQueuedAttackersRemain()
        {
            if (_pending.Count > 0 || !_aliveAttackersQueryCreated) return false;
            return _aliveNormalAttackersQuery.CalculateEntityCount() == 0;
        }

        // tournament-play-report Units 3/4 — shared result-popup hook: snapshot
        // the deck carried into this match (tournament-deck-info unit 1 — the
        // battle log is no longer sent), send complete, and swap the popup's
        // pending leaderboard for the real ranking
        // when it arrives. Guests and failures fall through silently — the pending
        // list stays. The popup usually isn't open yet when the response lands
        // (ranking beats the ~4s tally) — ResultScreen holds an early response and
        // opens on it, so this callback stays fire-and-forget.
        private void ReportMatchResult(MatchTally tally)
        {
            // endless-mode-removal unit 0 — 「무한 모드는 리포트하지 않는다」 가드는 제거했다.
            // 이제 **모든 판이 토너먼트에 올라간다** — 엔드리스가 하던 일이 정확히 이것 하나였다.
            var logger = GameManager.Instance?.Logger;
            // unit 7 — 서버로 가는 값은 여기 하나다. tally 가 「무엇을 제출하나」를 소유하고
            // 브리지는 그것을 꺼내 실어 보내기만 한다(가공 지점이 남아 있으면 안 된다).
            Wassup.Core.Api.TournamentMatchReporter.ReportResult(tally.SubmissionScore, logger?.DeckInfoJson(),
                ranking => resultScreen?.UpdateLeaderboard(ranking, Wassup.Core.Api.UserSession.Current?.userId),
                // tournament-flow-guards unit 2 — 실제 complete 실패만 알림(논블로킹, 재시도 없음).
                onError: _ => Wassup.UI.NoticePopup.ShowAlert("점수 전송 실패",
                    "이번 판 점수가 서버에 전송되지 않았습니다.\n네트워크 상태를 확인해 주세요."));
        }

        // three-minute-survival unit 7 — **판 마감의 단일 관문.**
        //
        //   취합(BuildTally) → 기록(로거) → 통보(서버) → 표시(결과 화면)
        //
        // three-minute-kill-race unit 0 — 여기로 들어오는 경로가 **하나**로 줄었다(3분 만료).
        // 판정 4개(골붕괴 즉사·스트레스 상한·적 마음 붕괴·웨이브 전멸)가 은퇴했기 때문이다.
        // unit 3 의 유저 제출이 두 번째 경로로 붙는다. **그 둘 말고 이 메서드를 부르는 코드를
        // 새로 만들지 말 것** — 그게 곧 패배 조건의 부활이다(feature 계약).
        //
        // **제출이 표시보다 앞이라는 순서는 계약이다**(score-tally-sequence 계약 3) —
        // 화면을 기다리다 앱이 죽으면 기록이 통째로 사라진다. 둘은 독립이다.
        //
        // `GamePhase.Tally` 전이는 유지한다: 합산 연출 자체는 은퇴했지만 전투 HUD 게이팅이 그
        // 페이즈를 읽는다. Tally 동안 ScoreHud 만 남고 NextWaveDock·CostDisplay 등은
        // `== GamePhase.Battle` 로 자동 정리된다. ⚠ 이 enum 은 `CameraDirectionConfig.asset` 에
        // 정수로 직렬화되므로 값을 빼거나 끼우지 말 것.
        private void EndMatch(string outcome)
        {
            _resultShown = true;
            _running = false;

            var tally = BuildTally(outcome);
            var logger = GameManager.Instance?.Logger;
            logger?.SetResult(tally.Outcome, tally.Leaks);
            // 로그 스키마의 score/kill_score 두 필드는 유지한다 — 이제 같은 값이 들어간다.
            logger?.SetScore(tally.Total, tally.Kills);

            GameManager.Instance?.SetPhase(GamePhase.Tally);
            ReportMatchResult(tally);

            // unit 10 — **화면만 늦춘다.** 위 집계·로그·서버 제출은 이미 끝났다
            // (「제출이 표시보다 앞」 계약 — 화면을 기다리다 앱이 죽어도 기록은 갔다).
            // 지연은 `GamePhase.Tally` 가 잡는다: 새 페이즈를 만들지 않는 이유는 Tally 가
            // 원래 「전투종료 → Tally → 결과화면」의 중간 박자 자리이고 HUD 게이팅이 이미
            // 그 페이즈를 알기 때문이다(ScoreHudView 가 점수 패널을 유지한다).
            //
            // 터지는 판에만 박자를 준다 — 3분 만료·제출은 터지는 것이 없다. 이건 「종료 사유
            // 표기」가 아니라 **사건이 있을 때만 그 사건의 연출이 나가는 것**이다.
            bool burst = outcome == "stress_full" && coreBurstHoldSec > 0f;
            if (!burst) { ShowResult(tally); return; }
            if (_coreBurstRoutine != null) StopCoroutine(_coreBurstRoutine);
            _coreBurstRoutine = StartCoroutine(HoldThenShowResult(tally));
        }

        private void ShowResult(MatchTally tally)
        {
            GameManager.Instance?.SetPhase(GamePhase.Result);
            resultScreen?.Show(tally);
        }

        // unit 10 — 붕괴 박자. **대기는 unscaled** 다(`WaitForSecondsRealtime`) — 스케일된
        // 시간으로 기다리면 아래 슬로우가 대기 자체를 늘려 박자가 배로 길어진다.
        private System.Collections.IEnumerator HoldThenShowResult(MatchTally tally)
        {
            // `Time.timeScale` 금지 — 시간제어는 도메인 리스로만 한다(기존 선례:
            // MenuPopup 일시정지 · 드래그 슬로우모 전부 priority 로 겹친다).
            if (TimeManager.Instance != null && coreBurstTimeScale < 1f)
            {
                _coreBurstLease = TimeManager.Instance.Request(
                    TimeDomain.Battle, coreBurstTimeScale, priority: 100);
                _coreBurstLeased = true;
            }
            yield return new WaitForSecondsRealtime(coreBurstHoldSec);
            ReleaseCoreBurstHold(stopRoutine: false);
            ShowResult(tally);
        }

        // unit 10 — 박자 정리. 코루틴 정상 종료와 판 경계 양쪽에서 부른다(멱등).
        private void ReleaseCoreBurstHold(bool stopRoutine)
        {
            if (stopRoutine && _coreBurstRoutine != null) StopCoroutine(_coreBurstRoutine);
            _coreBurstRoutine = null;
            if (!_coreBurstLeased) return;
            _coreBurstLeased = false;
            _coreBurstLease.Dispose();
        }

        // unit 10 — 붕괴한 마음 셀의 연출. 규칙(유출 전환)은 하나도 하지 않는다.
        private void PlayCoreBurst(List<Vector2Int> cells)
        {
            if (cells == null) return;
            for (int i = 0; i < cells.Count; i++)
            {
                var cell = cells[i];
                tileHealthGaugeLayer?.Hide(cell);
                vfxSpawner?.SpawnGoalCollapse(GridToWorldCenterVector(cell));
                // map-diorama-stage 병합 수선 — 붕괴 연출의 host 는 스테이지 GoalMarker(unit 4).
                if (_goalMarkersByCell.TryGetValue(cell, out var burstMarker) && burstMarker != null)
                    burstMarker.MarkCollapsed();
            }
        }

        // unit 7 — 흩어진 재료를 판 성적 하나로 옮기는 **유일한** 지점. 재료가 늘거나 줄면
        // 고칠 곳이 여기 하나다(종료 경로는 재료를 만지지 않는다).
        //
        // 점수는 처치로만 번다 — 시간·스트레스 축과 그 배점(ScoreRulesData)은 폐기됐고,
        // three-minute-kill-race unit 0 이후로는 승패 자체가 없어 분기가 하나도 없다.
        private MatchTally BuildTally(string outcome)
            => new MatchTally(outcome, _killCount,
                _goalStability, _goalStabilityMax, ReachedWaveNumber, _rusherArrivalCount);

        // 도달 웨이브 = 마지막으로 큐잉된 웨이브 번호. _nextWaveIndex 는 "다음에 나올" 인덱스라
        // 그대로 쓰면 아직 안 나온 웨이브를 도달로 센다.
        private int ReachedWaveNumber => _nextWaveIndex > 0 ? _nextWaveIndex : 0;

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

        // defender-footprint unit 1 — footprint 전체의 공간 판정. 셀 규칙은 위 SpatialPlacementCheck
        // 를 셀마다 재사용한다(단일 술어 계약 — 규칙 이중화 금지). perCell 이 오면 타일별 사유를
        // 채운다 — UI(고스트)는 이 목록을 재판정 없이 그대로 그린다.
        // 종합 사유 우선순위 = Occupied > NotBuildable > OutOfBounds. 셋이 섞이면 플레이어가
        // 조치할 수 있는 사유가 먼저 보인다(거부 라벨 문자화가 Occupied 만 구분하는 것과 정합).
        // ignoreOccupied: 재배치의 자기 footprint — 그 rect 안 셀은 Occupied 로 치지 않는다.
        public static PlacementRejectReason SpatialFootprintCheck(
            GeneratedMap map, HashSet<Vector2Int> occupied, Vector2Int anchor, Vector2Int size,
            PlacementLayer layers, List<FootprintCellReason> perCell = null, RectInt? ignoreOccupied = null)
        {
            perCell?.Clear();
            if (!map.IsCreated) return PlacementRejectReason.MissingMap;
            var rect = FootprintMath.Cells(anchor, size);
            bool anyOccupied = false, anyNotBuildable = false, anyOutOfBounds = false;
            for (int y = rect.yMin; y < rect.yMax; y++)
            {
                for (int x = rect.xMin; x < rect.xMax; x++)
                {
                    var cell = new Vector2Int(x, y);
                    var reason = SpatialPlacementCheck(map, occupied, new int2(x, y), layers);
                    if (reason == PlacementRejectReason.Occupied
                        && ignoreOccupied.HasValue && ignoreOccupied.Value.Contains(cell))
                        reason = PlacementRejectReason.None;
                    perCell?.Add(new FootprintCellReason(cell, reason));
                    switch (reason)
                    {
                        case PlacementRejectReason.Occupied: anyOccupied = true; break;
                        case PlacementRejectReason.NotBuildable: anyNotBuildable = true; break;
                        case PlacementRejectReason.OutOfBounds: anyOutOfBounds = true; break;
                    }
                }
            }
            if (anyOccupied) return PlacementRejectReason.Occupied;
            if (anyNotBuildable) return PlacementRejectReason.NotBuildable;
            if (anyOutOfBounds) return PlacementRejectReason.OutOfBounds;
            return PlacementRejectReason.None;
        }

        // defender-footprint unit 1 — UI(고스트)가 그릴 타일별 판정 결과의 공개 seam.
        // 판정과 같은 술어를 그대로 노출한다(재판정 금지). 반환 = 공간 종합 사유 —
        // 비용/풀/상한은 CanPlaceDefenderAt 이 별도로 본다(기존 계약 그대로).
        public PlacementRejectReason GetPlacementCellReasons(
            Vector2Int anchor, Vector2Int size, DefenderUnitData unit, List<FootprintCellReason> results)
        {
            var layers = unit != null ? unit.EffectivePlacementLayers : PlacementLayer.Ground;
            return SpatialFootprintCheck(_generatedMap, _occupiedTiles, anchor, size, layers, results);
        }

        // defender-footprint unit 2 — 자석 스냅: desiredAnchor 가 공간 사유로 무효일 때 반경 내
        // 최근접 유효 앵커. 탐색 순서 고정(row-major) + 동률 first-win 으로 **결정론**
        // (구조적 결정론 원칙 — seeded RNG 아닌 index 결정론). 반경 밖이면 false = 배치 불가 유지.
        public bool TryFindNearestPlaceableAnchor(
            DefenderUnitData unit, Vector2Int desiredAnchor, float maxRadiusCells, out Vector2Int anchor)
        {
            anchor = desiredAnchor;
            if (unit == null || maxRadiusCells <= 0f) return false;
            var layers = unit.EffectivePlacementLayers;
            var size = unit.Footprint;
            int r = Mathf.CeilToInt(maxRadiusCells);
            float maxSq = maxRadiusCells * maxRadiusCells;
            float bestSq = float.MaxValue;
            bool found = false;
            for (int dy = -r; dy <= r; dy++)
            {
                for (int dx = -r; dx <= r; dx++)
                {
                    if (dx == 0 && dy == 0) continue;
                    float dsq = dx * dx + dy * dy;
                    if (dsq > maxSq || dsq >= bestSq) continue;
                    var cand = desiredAnchor + new Vector2Int(dx, dy);
                    if (SpatialFootprintCheck(_generatedMap, _occupiedTiles, cand, size, layers)
                        != PlacementRejectReason.None) continue;
                    bestSq = dsq;
                    anchor = cand;
                    found = true;
                }
            }
            return found;
        }

        // defender-footprint unit 2 — footprint 고스트 게이트웨이(뷰 포워딩, ECS 쓰기 0).
        public void SetPlacementGhostCells(IReadOnlyList<Vector2Int> cells, IReadOnlyList<Color> colors)
            => tilemapMapView?.SetGhostCells(cells, colors);

        // unit 2 rev 2 — 사거리 표시 칸 여부(전역 고스트가 그 칸을 비켜 가는 판정).
        public bool IsPlacementRangeCell(Vector2Int cell)
            => tilemapMapView != null && tilemapMapView.IsPlacementRangeCell(cell);

        public void ClearPlacementGhostCells()
            => tilemapMapView?.ClearGhostCells();

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
            // defender-footprint unit 1 — (tileX,tileY) = footprint **앵커**(min 코너). 1×1 은
            // 앵커=대표 셀이라 기존과 동일. footprint 전체를 검사한다(단일 술어 재사용).
            var size = unitData != null ? unitData.Footprint : Vector2Int.one;
            var spatial = SpatialFootprintCheck(_generatedMap, _occupiedTiles, new Vector2Int(tileX, tileY), size, layers);
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

            // defender-board-limit 0 — 판 위 동시 존재 상한. 코스트 검사 **앞**이다: 사유
            // 우선순위가 구조 > 자원이라야 로그와 트레이 표현(소진 > 쿨타임 > 코스트)이 일치한다.
            // 재배치는 이 함수를 지나지 않으므로(CanRelocateDefender → RelocationCheck) 영향 없다.
            if (DeployedCountOf(unitData) >= unitData.EffectiveMaxOnBoard)
            {
                reason = PlacementRejectReason.LimitReached;
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

        // defender-board-limit 0 — 이 유닛 타입이 지금 판에 몇 기 있나. 상한 판정과 트레이
        // 소진 표현의 단일 출처다.
        //
        // **세는 것이지 저장하는 게 아니다.** _defenderByTile 이 판 위 유닛의 유일한 진실원이고
        // 사망은 거기서 지워지고(DrainDefenderDeathEvents) 재배치는 엔티티를 유지한 채 키만
        // 옮긴다 — 세기만 하면 세 경우가 전부 맞는다. 별도 카운터를 두면 대기배치 취소·teardown·
        // 매치 리셋마다 어긋날 구멍이 생긴다. 파생이라 리셋 훅도 필요 없다(dict 가 비면 자동 0).
        // 보드는 최대 수십 칸이라 선형 스캔 비용은 무시 가능(TryGetDefenderCell 선례).
        public int DeployedCountOf(DefenderUnitData unit)
        {
            if (unit == null) return 0;
            int n = 0;
            foreach (var kv in _defenderByTile)
            {
                if (kv.Value.data != unit) continue;
                // 사망 프레임과 드레인(DrainDefenderDeathEvents) 사이에는 바인딩이 파괴된
                // 엔티티를 가리킨다. 그건 "판에 서 있는 기수" 가 아니므로 세지 않는다 —
                // TryGetDeployedEntity 와 같은 판정을 써야 «소진인데 데려갈 유닛이 없는》
                // 상태가 안 생긴다. _em 이 없으면 확인할 수 없으므로 **세는 쪽**으로 둔다
                // (상한이 조용히 풀리는 것보다 한 프레임 더 막히는 게 안전하다).
                if (_em != null && !_em.Exists(kv.Value.entity)) continue;
                n++;
            }
            return n;
        }

        // defender-board-limit 2 — 이 유닛 타입이 판에 있으면 한 기를 돌려준다(트레이 소진 셀에서
        // 그 유닛으로 데려가는 경로). 2기 이상일 때 만질 때마다 다음 기로 가는 순환은 후속 후보 —
        // 상한 1 이 기본이라 후보가 항상 1기다.
        public bool TryGetDeployedEntity(DefenderUnitData unit, out Entity entity)
        {
            if (unit != null)
            {
                foreach (var kv in _defenderByTile)
                {
                    if (kv.Value.data != unit) continue;
                    if (_em == null || !_em.Exists(kv.Value.entity)) continue;
                    entity = kv.Value.entity;
                    return true;
                }
            }
            entity = Entity.Null;
            return false;
        }

        // first-run-tutorial low-health cue — UI/tutorial code stays outside ECS.
        // Queue ordinary damage through the Units-owned channel; PendingDeployment
        // keeps it buffered until the defender becomes active.
        public bool TryQueueDeployedDefenderMaxHealthDamage(
            DefenderUnitData unit, float damageRatio)
        {
            if (!HasLiveEntityManager() || !TryGetDeployedEntity(unit, out var entity)) return false;
            if (!_em.Exists(entity)) return false;
            if (!_em.HasComponent<Health>(entity) || !_em.HasBuffer<IncomingDamage>(entity)) return false;

            var health = _em.GetComponentData<Health>(entity);
            float amount = Health.ComputeMaxHealthDamage(health.max, damageRatio);
            if (amount <= 0f) return false;

            _em.GetBuffer<IncomingDamage>(entity).Add(new IncomingDamage
            {
                amount = amount,
                source = Entity.Null,
            });
            return true;
        }

        // placement-eligible-tile-highlight unit 2 — 배치 가능 셀 하이라이트 게이트웨이(뷰 포워딩, ECS 쓰기 0).
        // 공간 술어로 밝힐 셀을 수집 → TilemapMapView. 비용/풀은 안 본다(계약: 하이라이트=공간, hover=전체 판정).
        private bool _placeableHlShown;
        private readonly List<Vector2Int> _placeableHlScratch = new();
        // unit 4 — 하이라이트는 유닛 종속: 드는 유닛의 층으로 스캔한다(Ground 유닛이면 배치지면이,
        // Path 유닛이면 경로가 빛난다). 유닛 미상이면 Ground 폴백.
        private DefenderUnitData _placeableHlUnit;
        // defender-relocation unit 9 — 스캔이 뺀 칸을 되돌려 넣는 예외 1칸(재배치 소스 = 제자리
        // 재정비 목적지). 상태로 들고 있어야 리페인트를 살아남는다 — ShowPlacementHighlight 주석 참조.
        private Vector2Int? _placeableHlExtraCell;

        // unit 4 리뷰 M-1 — 라이브 맵에서 한 셀의 모든 배치 층을 닫는다(스폰·골 불변식용).
        private void CloseCellLayers(int2 cell)
        {
            if (cell.x < 0 || cell.x >= _generatedMap.gridSize.x
                || cell.y < 0 || cell.y >= _generatedMap.gridSize.y) return;
            _generatedMap.placeMask[_generatedMap.CellIndex(cell)] = 0;
        }

        // 표시 여부 read seam — 컨트롤러가 자기 래치와 실제 상태를 대조해 자기치유하기 위함(unit 4 리뷰 C-1).
        public bool IsPlacementHighlightShown => _placeableHlShown;

        // defender-relocation unit 9 — extraCell 은 스캔이 빼는 칸을 되돌려 넣는 자리다(재배치
        // 소스 칸은 자기가 점유 중이라 SpatialPlacementCheck 가 Occupied 로 뺀다). 제자리 재정비가
        // 확정이 된 지금 그 칸은 "못 놓는 칸"이 아니라 "여기 놓으면 재정비" 로 읽혀야 한다.
        //
        // ⚠ 인자가 아니라 **상태**로 들고 있어야 한다. RepaintPlacementHighlight 는 매번 처음부터
        // 다시 계산하고, RefreshPlacementHighlightIfShown 을 통해 배치·재배치 확정마다 다시 불린다 —
        // 일회성 인자로 넘기면 첫 리페인트에서 조용히 사라진다.
        public void ShowPlacementHighlight(DefenderUnitData unit, Vector2Int? extraCell = null)
        {
            _placeableHlShown = true;
            _placeableHlUnit = unit;
            _placeableHlExtraCell = extraCell;
            RepaintPlacementHighlight();
        }

        public void HidePlacementHighlight()
        {
            _placeableHlShown = false;
            _placeableHlUnit = null;
            _placeableHlExtraCell = null;
            if (tilemapMapView != null) tilemapMapView.ClearPlacementHighlight();
        }

        // first-run-tutorial unit 1 — 배치 **불가** 칸(가능 칸의 여집합 전체). 온보딩 맵 설명 전용.
        //
        // 여집합을 따로 계산하지 않고 **같은 스캔의 else 가지**를 쓴다 — 두 벌의 판정식을 두면
        // 어느 날 갈려서 같은 칸이 양쪽에 칠해진다. 그래서 리페인트도 아래 한 함수가 소유한다.
        //
        // ⚠ 자기 플래그가 따로 있어야 한다. RepaintPlacementHighlight 는 _placeableHlShown 으로
        // early-return 하므로, 그것만 보면 가능 하이라이트를 안 켠 상태에서 불가가 안 칠해진다.
        // 기준 유닛은 _placeableHlUnit 하나로 충분하다 — 두 하이라이트는 한 스캔의 두 갈래라
        // 정의상 같은 층 마스크를 쓴다. 필드를 둘로 두면 «서로 다른 유닛으로 켜진» 상태가
        // 표현 가능해지고, 그건 unit 1 이 피하려던 «판정식이 갈린다» 와 같은 종류의 여지다.
        private bool _blockedHlShown;
        private readonly List<Vector2Int> _blockedHlScratch = new List<Vector2Int>();

        // first-run-tutorial unit 5 — «적이 내 목표에서 몇 칸 안까지 들어왔는가».
        //
        // 온보딩이 "적들의 머리위에 배치해보세요" 를 적이 아직 멀리 있을 때 띄우면 문장과
        // 화면이 어긋나고, 그렇게 놓은 배치 스킬은 아무도 못 때려서 이어지는 «전황을
        // 유리하게» 가 통째로 희석된다. 그 순간을 기다리는 판정이다. 적 위치는 ECS 에
        // 있으므로 **이 게이트웨이 안에** 둔다(제약 1).
        //
        // 기준을 **내 목표까지의 거리**로 잡는 이유: «내 영역으로 얼마나 들어왔나» 를 직접
        // 말하는 유일한 척도이고, 목표는 모든 맵에 있어서 «이 맵엔 기준이 없다» 가 생기지
        // 않는다(강/Env 타일을 기준으로 삼던 시절엔 강 없는 맵을 위한 폴백이 따로 필요했다).
        //
        // 체비셰프 거리다 — 격자 이동이 8방향이라 «몇 칸 남았나» 의 체감과 일치한다.
        // 목표가 여럿이면 가장 가까운 것 기준(멀티골 맵은 각 골이 자기 복도를 갖는다).
        public bool AnyEnemyWithinTilesOfGoal(int tiles)
        {
            if (!_aliveAttackersQueryCreated || _em == null || !_generatedMap.IsCreated) return false;
            int r = Mathf.Max(0, tiles);
            var size = _generatedMap.gridSize;
            var entities = _aliveAttackersQuery.ToEntityArray(Allocator.Temp);
            try
            {
                for (int i = 0; i < entities.Length; i++)
                {
                    var e = entities[i];
                    if (!_em.HasComponent<LocalTransform>(e)) continue;
                    var pos = _em.GetComponentData<LocalTransform>(e).Position;
                    var cell = GridMath.WorldToCell(pos, tileSize, size, origin: _boardOrigin);
                    if (NearestGoalDistance(cell) <= r) return true;
                }
            }
            finally { entities.Dispose(); }
            return false;
        }

        private int NearestGoalDistance(int2 cell)
        {
            int best = int.MaxValue;
            if (_generatedMap.goals.IsCreated && _generatedMap.goals.Length > 0)
            {
                for (int g = 0; g < _generatedMap.goals.Length; g++)
                    best = math.min(best, GridMath.ChebyshevDistance(cell, _generatedMap.goals[g]));
            }
            else
            {
                best = GridMath.ChebyshevDistance(cell, _generatedMap.goal);   // goals 미생성 폴백(GeneratedMap 계약)
            }
            return best;
        }

        public void ShowBlockedHighlight(DefenderUnitData unit)
        {
            _blockedHlShown = true;
            _placeableHlUnit = unit;
            RepaintPlacementHighlight();
        }

        public void HideBlockedHighlight()
        {
            _blockedHlShown = false;
            if (tilemapMapView != null) tilemapMapView.ClearBlockedHighlight();
        }

        // 술어를 RepaintPlacementHighlight 와 맞춘다 — 둘이 갈리면 불가 하이라이트만 켜진
        // 상태에서 배치/재배치 확정 후 리페인트가 조용히 건너뛰어진다.
        public void RefreshPlacementHighlightIfShown()
        {
            if (_placeableHlShown || _blockedHlShown) RepaintPlacementHighlight();
        }

        private void RepaintPlacementHighlight()
        {
            if (tilemapMapView == null || !_generatedMap.IsCreated) return;
            if (!_placeableHlShown && !_blockedHlShown) return;

            // first-run-tutorial unit 1 — 두 하이라이트는 **한 스캔의 두 갈래**다.
            // 기준 유닛은 켜져 있는 쪽이 준다(둘 다 켜져 있으면 같은 유닛이어야 정합).
            var layers = _placeableHlUnit != null ? _placeableHlUnit.EffectivePlacementLayers : PlacementLayer.Ground;
            _placeableHlScratch.Clear();
            _blockedHlScratch.Clear();
            int w = _generatedMap.gridSize.x, h = _generatedMap.gridSize.y;
            for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                var cell = new Vector2Int(x, y);
                if (SpatialPlacementCheck(_generatedMap, _occupiedTiles, new int2(x, y), layers) == PlacementRejectReason.None)
                    _placeableHlScratch.Add(cell);
                else
                    _blockedHlScratch.Add(cell);
            }
            // unit 9 — 스캔이 뺀 소스 칸을 되돌려 넣는다(제자리 재정비도 유효 목적지다).
            // ⚠ 되넣은 칸은 위 else 가지에서 이미 blocked 로 갔다 — 동시 표시일 때 양쪽에 들지
            // 않도록 여기서 빼준다. 맵 설명은 extraCell 이 없어 실무상 안 걸리지만 규칙은 한 곳에 둔다.
            if (_placeableHlExtraCell.HasValue && !_placeableHlScratch.Contains(_placeableHlExtraCell.Value))
            {
                _placeableHlScratch.Add(_placeableHlExtraCell.Value);
                _blockedHlScratch.Remove(_placeableHlExtraCell.Value);
            }

            if (_placeableHlShown) tilemapMapView.SetPlacementHighlight(_placeableHlScratch);
            if (_blockedHlShown) tilemapMapView.SetBlockedHighlight(_blockedHlScratch);
        }

        // Explicit-type placement (Phase 4). Used by DefenderSelector after the
        // player chooses which picked defender they want on the tile.
        public bool PlaceDefenderAs(int tileX, int tileY, DefenderUnitData unitData)
        {
            // defender-footprint unit 1 — (tileX,tileY) = footprint 앵커. 바인딩·DefenderTile·
            // sim 위치·on-place/시너지/로그는 전부 대표 셀 기준(1×1 은 앵커=대표 셀).
            var cell = new Vector2Int(tileX, tileY);
            if (!CanPlaceDefenderAt(tileX, tileY, unitData, out var reason))
            {
                LogPlacementReject("PlaceDefenderAs", unitData, reason);
                return false;
            }

            var primary = FootprintMath.PrimaryCell(cell, unitData.Footprint);
            OccupyDefenderFootprint(cell, unitData.Footprint);
            RefreshPlacementHighlightIfShown(); // placement-eligible-tile-highlight unit 2
            GameManager.Instance?.Logger?.RecordPlacement(unitData.displayName, primary, Time.time - _startTime, unitData.cost);

            var entity = CreateDefenderEntity(primary, unitData, pendingDeployment: false, spawnPlacementVfx: true);
            TriggerOnPlaceAndSynergy(unitData, primary, entity);

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

            // defender-footprint unit 1 — cell = 앵커, 등록·바인딩은 대표 셀 기준 (PlaceDefenderAs 와 동형).
            var primary = FootprintMath.PrimaryCell(cell, unitData.Footprint);
            OccupyDefenderFootprint(cell, unitData.Footprint);
            RefreshPlacementHighlightIfShown(); // placement-eligible-tile-highlight unit 2
            GameManager.Instance?.Logger?.RecordPlacement(unitData.displayName, primary, Time.time - _startTime, unitData.cost);
            entity = CreateDefenderEntity(primary, unitData, pendingDeployment: true, spawnPlacementVfx: false);
            _cancellableDeployments.Add(entity); // unit 5 리뷰 H-1 — 신규 배치만 취소 유예 대상
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
            // defender-footprint unit 1 — 호출자(배치 컨트롤러)는 앵커를 들고 있을 수 있다 — 대표 셀로 해석.
            if (TryResolveDefenderKey(cell, out var key)) cell = key;
            if (!_defenderByTile.TryGetValue(cell, out var binding) || binding.entity != entity) return;

            if (!_onPlaceTriggeredEntities.Contains(entity))
                TriggerDeploymentOnPlaceSkill(cell, entity);

            // ⚠ **이 두 줄 사이에 시스템 갱신이 끼면 안 된다**(skill-layer-migration, 재리뷰 M-6).
            // 위가 `JustDeployed` 를 달고 배치 스킬은 **다음 시스템 갱신**에 실행되는데,
            // 실드 부여 같은 배치 스킬은 후보에서 «배치 중인 유닛»을 뺀다. 아래 제거가
            // 늦어지면 **방금 놓인 그 유닛이 자기 배치 스킬의 후보에서 빠진다** —
            // 레거시가 `d.entity != placedEntity && HasComponent<PendingDeployment>` 라는
            // 한 줄 예외를 들고 있던 이유가 이것이고, 지금은 그 예외 대신 **순서**가 지킨다.
            // 둘 다 브리지 동기 구간이라 오늘은 성립한다. 떼어 놓지 말 것.
            if (_em.HasComponent<PendingDeployment>(entity))
                _em.RemoveComponent<PendingDeployment>(entity);
            _cancellableDeployments.Remove(entity); // unit 5 리뷰 H-1 — 활성화 = 유예 종료
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
            // defender-footprint unit 1 — 대표 셀 해석(효과 타일·on-place 는 대표 셀에서 발동).
            if (TryResolveDefenderKey(cell, out var resolvedKey)) cell = resolvedKey;
            if (!_defenderByTile.TryGetValue(cell, out var binding) || binding.entity != entity) return false;

            MarkJustDeployedForRules(entity);   // unit 0 — D&D 경로 + 재배치 재무장(이 함수를 재호출한다)
            FireOnPlaceCameraShake(binding.data);   // camera-direction unit 17
            _onPlaceTriggeredEntities.Add(entity);
            ApplyEffectTileOnce(cell, entity); // unit 8 — 자기 가드(재배치 재무장에 딸려오지 않는다)
            LogSynergy(binding.data, cell);
            return true;
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
            // aimStyle=false — 여기는 아직 배치 단계다. 조준 해치는 드롭 뒤에 나온다(unit 4).
            // bomb-thrower-defender unit 9 — 폭탄병 전용 분기(착지 후보 4셀)는 삭제됐다.
            // 이제 최근접 적을 노리므로 네모 사거리가 참말이다 — 다른 유닛과 같은 줄로 떨어진다.
            // summon-patrol-defender unit 9 — 소환사 전용 분기를 **삭제**했다.
            // unit 5 는 "소환사에게 공격범위는 거짓말"이라며 leash 반경을 walk 셀에 스냅해
            // 따로 그렸는데, 그러면 프리뷰가 그리는 박스와 순찰병이 지키는 박스와 소환
            // 게이트가 보는 박스가 셋으로 갈린다(실제로 갈려 있었다). 이제 소환사의
            // 공격범위가 곧 담당 구역이므로 다른 방어유닛과 **같은 줄**로 떨어진다 —
            // 화면 언어("이 유닛의 공격범위")가 소환사에게도 그대로 성립한다.
            if (unit.RequiresFacing) PaintLanes(center, tileRange, null, AimLaneDimAlpha, aimStyle: false);
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

        // defender-footprint unit 1 — cell = **대표 셀**. footprint 점유 등록(OccupyDefenderFootprint)은
        // 호출자(배치 2경로) 몫이고, 이 함수는 바인딩·DefenderTile·sim 위치를 전부 그 한 칸에 건다.
        private Entity CreateDefenderEntity(
            Vector2Int cell,
            DefenderUnitData unitData,
            bool pendingDeployment,
            bool spawnPlacementVfx)
        {
            var entity = _em.CreateEntity();
            AttachSimEntityId(entity);
            // Phase 4: defenders can now take damage from enemy attackers, so
            // they need an IncomingDamage buffer just like attack units have.
            _em.AddBuffer<IncomingDamage>(entity);
            // combat-action-lock unit 2 — defender 도 CC(Sleep/Stun) 수신하도록 CcEffect 버퍼
            // 사전 부착. ApplyActiveDcEffectsTo(3641, placement Sleep 적용) 이전이어야 함(MED4).
            _em.AddBuffer<Wassup.Battle.Effects.CcEffect>(entity);
            _em.AddBuffer<Wassup.Battle.Effects.DotEffect>(entity); // dot-effect-extraction unit 0
            _defenderByTile[cell] = (entity, unitData);
            // defender-board-limit 1 — 바인딩이 생긴 바로 이 자리가 «판에 올라왔다» 의 유일한
            // 지점이다(모든 배치 경로가 여기를 지난다). 재배치는 이 함수를 지나지 않으므로
            // 발화하지 않는다 — 기수가 안 변하니 알릴 것도 없다.
            DefenderPlaced?.Invoke(entity, unitData);
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
                // battle-structures unit 8 — 저작 타겟 마스크(기본 = 적 진영 전부).
                // 아군 타게팅(힐러)은 여전히 DefenderUnit 단독이고 저작 마스크를 이긴다 —
                // AnyDefender 로 넓히면 IncomingHeal 버퍼가 없는 거점이 후보에 들어
                // ECB playback 에서 던진다. 판정 전체는 DefenderTargetDefaults 소관.
                targetMask = Wassup.Battle.Combat.DefenderTargetDefaults.Resolve(
                    (int)unitData.targetFactions, unitData.targetAllies),
                targetTraversalLayers = unitData.targetAllies
                    ? (byte)0
                    : (byte)unitData.EffectiveAttackTargetLayers,
                hitDelaySec = unitData.hitDelaySec,
            });
            // target-persistence unit 4 — 방어유닛 지속 락의 그릇. 신규 컴포넌트를 만들지
            // 않는다 — `FocusTarget` 은 «내가 문 대상»이지 진영을 함의하지 않고, 유지 술어도
            // `TargetPersistence.KeepsLock` 하나를 공유한다.
            // 이 경로(배치 방어유닛)는 EnemyBehavior 가 없으므로 AttackSystem 의 **unit 4
            // 블록**이 처리한다(순찰병은 EnemyBehavior 를 가져 unit 3 적 블록으로 간다).
            _em.AddComponentData(entity, new Wassup.Battle.Combat.FocusTarget { current = Entity.Null });
            // aggro-targeting Unit 4 — expose defender class so enemies can filter/prioritize.
            _em.AddComponentData(entity, new Wassup.Battle.Units.DefenderClassTag { value = unitData.role });
            // aggro-targeting Unit 10 — guardians (aggroCapacity > 0) carry AggroCapacity
            // (존재=가디언 표식). Fighter/Ranger (aggroCapacity == 0) get none. 획득은
            // 히트 구동(AttackSystem RESOLVE→AggroAcquireEvent) — 별도 range 없음.
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
                    // unit 5a — 라우팅 키. 이 능력은 슬롯이 아니라 자기 상태를 가지므로
                    // 키도 거기 산다(`DamagedCounter` 와 같은 자리).
                    skillId = Wassup.Skills.Concrete.CastHazardSkill.Id,
                    range = hazardAbility.castRange,
                    cooldownDuration = hazardAbility.cooldown,
                    cooldownRemaining = 0f,
                    targetMask = (int)Faction.EnemyUnit,
                    targetTraversalLayers = (byte)unitData.EffectiveAttackTargetLayers,
                    dataIndex = hazardDataIndex,
                    kind = hazardAbility.kind,
                    footprintWidth = math.max(1, hazardAbility.footprintWidth),
                    footprintHeight = math.max(1, hazardAbility.footprintHeight),
                });
            }
            // shield-guardian-defender unit 1 — 실드 캐스트 베이크. 범위 = attackRange
            // 재사용(계약 5). 첫 캐스트는 배치 A초 후(cooldownRemaining = A).
            // skill-layer-migration unit 5b — **저작은 그대로, bake 가 규칙 슬롯으로 굽는다.**
            // 전용 상태(`ShieldCastState`)와 전용 시스템이 여기서 은퇴한다 — 주기 트리거는
            // 이미 있고(`DcTriggerSlot.periodSeconds`), 실드 부여도 이미 있다(`GrantShield`).
            // ⚠ 첫 캐스트 시점이 같다: 레거시는 `cooldownRemaining = A`(A초 후 첫 발),
            // 주기 슬롯은 `elapsed 0` 에서 A초를 채워야 발화 — 같은 타이밍이다.
            var shieldAbility = unitData.GetAbility<ShieldCastAbility>();
            if (shieldAbility != null && shieldAbility.cooldown > 0f && shieldAbility.amount > 0f)
            {
                var shieldSlots = _em.HasBuffer<DcTriggerSlot>(entity)
                    ? _em.GetBuffer<DcTriggerSlot>(entity)
                    : _em.AddBuffer<DcTriggerSlot>(entity);
                shieldSlots.Add(new DcTriggerSlot
                {
                    skillId = SkillIdForPayload(Wassup.Data.DcPayloadKind.GrantShield),
                    instanceId = _dcInstanceCounter++,
                    trigger = Wassup.Data.DcTriggerKind.PeriodicTimer,
                    periodSeconds = shieldAbility.cooldown,
                    payload = Wassup.Data.DcPayloadKind.GrantShield,
                    magnitude = shieldAbility.amount,
                    // 계약 5 — 실드 범위는 유닛 `attackRange` 재사용이라 에셋에 range 가 없다.
                    tileRange = GridMath.RangeToTiles(unitData.attackRange),
                    // 저작 filter 는 도메인 enum 과 값이 같다(Self·Nearest·MostHurt).
                    shieldFilter = (byte)shieldAbility.filter,
                    // ⚠ **셔틀은 자기를 포함한다**(계약 6). 카드 경로(악몽의 가호)는 제외인데,
                    // 그 이유가 「같은 host 의 두 실드 능력이 한 슬롯을 공유한다」라서 —
                    // 셔틀엔 겹칠 상대가 없다. 그 사실은 저작자가 아니라 bake 만 안다.
                    shieldIncludesSelf = true,
                    shieldTargetCount = math.max(1, shieldAbility.targetCount),
                    projectileDataIndex = -1,
                    patternIndex = -1,
                    hazardDataIndex = -1,
                });
            }
            // bomb-thrower-defender unit 3 — 폭탄 발사 상태 베이크.
            // defender-ability-assets unit 2 — 게이트 = 능력 에셋 존재 + 유효 수치.
            // unit 10 — 캐스터별 RNG 는 3종 무작위와 함께 은퇴했다(폭탄은 피해 한 종).
            var bombAbility = unitData.GetAbility<BombThrowAbility>();
            if (bombAbility != null && bombAbility.travelSec > 0f)
            {
                _em.AddComponentData(entity, new Wassup.Battle.Combat.BombLauncherState
                {
                    travelSec = bombAbility.travelSec,
                    fuseSec = bombAbility.fuseSec,
                    aoeTileRange = bombAbility.aoeTileRange,
                    aoeTargetCap = bombAbility.aoeTargetCap,
                    arcHeight = bombAbility.arcHeight,
                    dmgBombDamage = bombAbility.damage,
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
                    unitData.Footprint.x, // unit 9 — Spine 경로와 같은 블롭 지름
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

            // on-place-skill-rework unit 0 — 방어유닛 자기 규칙(트리거 × 페이로드) bake.
            //
            // ⚠ **반드시 BakeDefenderDirectionalPattern 뒤**다. `AttackSystem` 의 다연발 경로는
            // `PatternSlot` 버퍼의 **slots[0] 하나만** 읽는데, `AddBuffer` 가 add-or-get 이라 두
            // 슬롯이 공존하고 index 0 을 누가 갖느냐가 이 호출 순서로만 정해진다. 앞으로 옮기면
            // **머신거너 다연발이 배치 스킬 패턴을 쏜다.** EditMode 가 이 순서를 고정한다.
            var skillAbility = unitData.GetAbility<UnitSkillAbility>();
            if (skillAbility != null && skillAbility.mechanics != null && skillAbility.mechanics.Length > 0)
            {
                // skill-layer-migration unit 2g — **과도기 충돌 경고 둘이 함께 은퇴했다.**
                // 「레거시 enum 과 규칙이 동시에 돈다」와 「밀쳐냄과 규칙이 겹친다」는
                // 둘 다 «두 경로가 공존하던 동안»의 안전장치였고, 이제 그 두 경로가
                // 사라져 겹칠 대상이 없다(enum·밀쳐냄 필드군 모두 철거).

                BakeUnitMechanics(entity, skillAbility.mechanics, hostIsEnemy: false,
                    maxHpRef: unitData.health, ownerLabel: unitData.displayName, enemyOwner: null);
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
        // unit 9 — centerCell 은 박스 중심(소환사 셀)이고 homeCell 은 **이 유닛이 밟을 수
        // 있는 주변 칸**이다. 스폰 위치 = homeCell. 호출자가 TryGetPatrolHomeCell 로 통행
        // 층을 대조해 넘긴다 — 설 수 없는 칸을 집으로 주면 그 칸을 향해 영원히 전진한다.
        // owner == Entity.Null 이면 SummonedBy 미부착 = 연쇄 소멸 대상 아님(디버그 스폰).
        private Entity CreatePatrolEntity(
            DefenderUnitData unitData,
            int2 centerCell,
            int2 homeCell,
            int tileRadius,
            Entity owner)
        {
            if (unitData == null || _em == default) return Entity.Null;

            var entity = _em.CreateEntity();
            AttachSimEntityId(entity);
            _em.AddBuffer<IncomingDamage>(entity);
            _em.AddBuffer<Wassup.Battle.Effects.CcEffect>(entity);
            _em.AddBuffer<Wassup.Battle.Effects.DotEffect>(entity);

            var cellV2 = new Vector2Int(homeCell.x, homeCell.y);
            var pos = GridToWorldCenter(cellV2, spawnHeight);
            _em.AddComponentData(entity, LocalTransform.FromPositionRotationScale(pos, quaternion.identity, CharacterVisualScale));
#if UNITY_EDITOR
            _em.SetName(entity, $"Patrol_{unitData.displayName}_{homeCell.x}_{homeCell.y}");
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
                // battle-structures unit 8 — 순찰 아군도 같은 저작 축을 쓴다. 배치 방어유닛과
                // 같은 SO 타입이라 «순찰만 거점을 못 때린다» 는 예외를 만들 이유가 없다.
                targetMask = Wassup.Battle.Combat.DefenderTargetDefaults.Resolve(
                    (int)unitData.targetFactions, unitData.targetAllies),
                targetTraversalLayers = unitData.targetAllies
                    ? (byte)0
                    : (byte)unitData.EffectiveAttackTargetLayers,
                hitDelaySec = unitData.hitDelaySec,
            });
            // target-persistence unit 4 — 순찰병도 락을 받는다. 다만 이 유닛은 EnemyBehavior
            // (적 AI 스택)를 물려받으므로 **unit 3 의 적 focus 블록**이 처리한다 — unit 4
            // 블록은 `!EnemyBehavior` 로 이쪽을 비켜준다(이중 처리 방지).
            _em.AddComponentData(entity, new Wassup.Battle.Combat.FocusTarget { current = Entity.Null });
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
                // traversal-layers unit 2 — sim 은 SO 를 못 읽으므로 스폰 시 1회 주입한다.
                // 폴백(EffectiveTraversalLayers)이 Path 라 저작 전엔 현행과 동일하다.
                traversalLayers = (byte)unitData.EffectiveTraversalLayers,
            });
            _em.AddComponentData(entity, new Wassup.Battle.Movement.PatrolAnchor
            {
                cell = centerCell,
                homeCell = homeCell,
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
                // unit 6 의 발밑 아군 링은 unit 8 에서 제거했다 — 표식이 필요했던 이유가
                // "순찰병이 적과 같은 스켈레톤·같은 실루엣으로 걸어다닌다" 였는데, 고유 리그(Doll)가
                // 그 전제를 없앴다. 자세한 경위는 docs/spec/summon-patrol-defender/6_ally_readability.md.
                spineSpawned = spineUnitPool.TrySpawn(unitData, unitData, entity, spineWorld, "SpinePat", out _);
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
                    unitData.displayName, entity, fallbackWorld, mesh, material, CharacterVisualScale,
                    unitData.Footprint.x, out _); // unit 9 — Spine 경로와 같은 블롭 지름
            }

            return entity;
        }

        // summon-patrol-defender unit 2 — 디버그 스폰 공개 API (DebugSpawnHazardAt 동형).
        // 호출자가 준 셀을 walk 셀로 스냅한다. 스냅 실패(맵 미생성/walk 셀 없음) = Entity.Null.
        // summon-patrol-defender unit 9 — **거점 = 소환사 셀.** 스냅은 퇴화 분기로만 남는다.
        //
        // unit 2 시절엔 무조건 최근접 walk 셀로 스냅했다. 전제는 "방어유닛 셀은 걸을 수
        // 없다"였는데 traversal-layers 가 그 전제를 없앴다 — 순찰병이 Ground 를 열면
        // 소환사가 서 있는 칸에 그대로 설 수 있다. 그래서 통상 경로에서 거점·프리뷰·소환
        // 게이트가 **같은 셀**을 중심으로 갖는다(셋이 갈려 있던 것이 unit 9 의 동기).
        //
        // 퇴화 분기가 필요한 이유는 통행 층이 **저작 값**이기 때문이다. Path 전용으로 저작된
        // 소환물은 배치지에 설 수 없고, 그대로 두면 "절대 설 수 없는 칸을 향해 영원히 전진"
        // 하는 원래의 실패가 재현된다. 그때만 구역 안에서 가장 가까운 통행 가능 셀로 물러선다.
        //
        // 탐색을 구역 안으로 제한하는 것은 비용이 아니라 **규칙**이다: 순찰병의 집은 소환사가
        // 주장하는 구역 안에 있어야 한다. 전 그리드 스캔은 경로에서 먼 곳에 소환사를 놓았을 때
        // 거점을 화면 저쪽으로 날려버린다. radius 0 은 자기 셀만 남으므로 최소 1로 본다.
        private bool TryGetPatrolHomeCell(
            int2 ownerCell, int tileRadius, byte traversalLayers, out int2 homeCell)
        {
            homeCell = ownerCell;
            if (!_generatedMap.IsCreated) return false;

            // 0 = 미저작. traversal-layers 계약대로 Path 로 읽어 현행을 재현한다.
            byte layers = traversalLayers != 0 ? traversalLayers : (byte)PlacementLayer.Path;

            // **소환사 셀은 후보에서 뺀다** — 같은 칸에 스폰되면 둘이 겹쳐 서고, 플레이어에겐
            // 소환물이 소환사에 박혀 안 움직이는 것으로 읽힌다(사용자 지적 2026-08-10).
            // 탐색을 구역 안으로 제한하는 것은 비용이 아니라 규칙이다: 순찰병의 집은 소환사가
            // 주장하는 구역 안에 있어야 한다. 전 그리드 스캔은 경로에서 먼 곳에 소환사를 놓았을
            // 때 집을 화면 저쪽으로 날려버린다. 최근접부터 고르므로 통상 **인접 칸**이 잡힌다.
            // 동률은 (y, x) 오름차순 첫 칸 — 결정론(같은 배치 = 같은 집).
            int limit = math.max(1, tileRadius);
            int bestDistSq = int.MaxValue;
            bool found = false;
            for (int y = ownerCell.y - limit; y <= ownerCell.y + limit; y++)
            for (int x = ownerCell.x - limit; x <= ownerCell.x + limit; x++)
            {
                var candidate = new int2(x, y);
                if (candidate.Equals(ownerCell)) continue;
                if (!IsInGeneratedMapBounds(candidate)) continue;
                if ((PlacementLayers.Derive(_generatedMap.TileAt(candidate)) & layers) == 0) continue;

                int dx = x - ownerCell.x, dy = y - ownerCell.y;
                int distSq = dx * dx + dy * dy;
                if (distSq >= bestDistSq) continue;

                bestDistSq = distSq;
                homeCell = candidate;
                found = true;
            }
            if (found) return true;

            // 주변에 설 칸이 하나도 없다 — 소환을 취소하느니 소환사 셀에 세운다.
            // (그 칸조차 못 밟으면 진짜 실패다: 설 수 없는 칸을 집으로 주면 영원히 전진한다.)
            return IsInGeneratedMapBounds(ownerCell)
                && (PlacementLayers.Derive(_generatedMap.TileAt(ownerCell)) & layers) != 0;
        }

        public Entity DebugSpawnPatrolAt(DefenderUnitData unitData, int2 cell, int tileRadius)
        {
            if (unitData == null) return Entity.Null;
            byte layers = (byte)unitData.EffectiveTraversalLayers;
            if (!TryGetPatrolHomeCell(cell, tileRadius, layers, out var homeCell)) return Entity.Null;
            return CreatePatrolEntity(unitData, cell, homeCell, tileRadius, Entity.Null);
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
                Wassup.Core.Trace.LegacyTraceRecorder.Ev(Wassup.Core.Trace.TraceChannel.PatrolSpawn,
                    SimIdOf(req.owner), i: req.patrolDataIndex);
                _em.DestroyEntity(carrier);

                if (!_em.Exists(req.owner)) continue;
                if (req.patrolDataIndex < 0 || req.patrolDataIndex >= _patrolUnitRegistry.Count)
                {
                    Debug.LogWarning($"[Summon] Invalid patrol unit index {req.patrolDataIndex}; dropping.");
                    continue;
                }
                var so = _patrolUnitRegistry[req.patrolDataIndex];
                if (so == null) continue;

                if (!TryGetPatrolHomeCell(
                        req.ownerCell, req.coverTileRadius,
                        (byte)so.EffectiveTraversalLayers, out var homeCell)) continue;

                var patrol = CreatePatrolEntity(so, req.ownerCell, homeCell, req.coverTileRadius, req.owner);
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
        // duration=∞ (시너지 관용) — revocation 경로가 없다.
        // ⚠ defender-relocation unit 8 — "재배치 기능이 없어 불요" 라고 적혀 있던 자리다. 재배치는
        // 이제 존재하고, 그래서 이 함수는 **엔티티당 1회**로 봉인돼 있다(ApplyEffectTileOnce).
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

        // defender-relocation unit 8 — 효과 타일 적용의 유일한 관문(엔티티당 1회).
        //
        // on-place 와 가드를 **공유하면 안 된다**: 재배치는 on-place 를 재무장하는데(README 계약 4),
        // 효과 타일까지 딸려 재적용되면 새 칸 효과가 붙되 **옛 칸 효과가 회수되지 않는다**.
        // 같은 stat 이면 병합키(source=자기 + stackId=EffectTileStackId + stat)가 refresh 라 겹치지
        // 않지만, **stat 이 다르면 슬롯이 갈린다** — 공속 타일에서 공격력 타일로 옮기면 공격력이
        // 붙고 공속이 영원히 남는다(duration=∞ + revocation 없음). 회수를 먼저 만들기 전엔 봉인.
        //
        // 효과 타일이 없는 칸에 배치돼도 마킹한다 — 종전 동작 그대로다(옛 가드도 타일 유무와
        // 무관하게 _onPlaceTriggeredEntities 에 넣었다).
        private void ApplyEffectTileOnce(Vector2Int cell, Entity entity)
        {
            if (entity == Entity.Null) return;
            if (!_effectTileAppliedEntities.Add(entity)) return;
            ApplyEffectTileIfAny(cell, entity);
        }

        // camera-direction unit 17 — 배치 스킬 발동 순간의 카메라 셰이크.
        //
        // **파이프라인을 묻지 않는 것이 핵심이다.** 배치 스킬은 두 어휘로 구현돼 있다 —
        // 레거시 `onPlaceEffect` enum(말파이트)과 `abilities` 의 규칙(`UnitSkillAbility`,
        // 캐논·샷건맨). 둘의 실행 지점은 다르지만 **발동이 확정되는 순간은 이 seam 하나**라,
        // 여기서 울리면 어느 쪽으로 만든 스킬이든 같은 대접을 받는다.
        //
        // 세기·길이는 유닛이 저작하고(제약 6) 진폭은 카메라 config 소유 — 「이 유닛이 얼마나
        // 크게 울리나」와 「셰이크가 물리적으로 어떤 느낌인가」는 서로 다른 튜닝 축이다.
        private void FireOnPlaceCameraShake(DefenderUnitData unitData)
        {
            if (unitData == null || unitData.onPlaceShakeStrength <= 0f) return;
            EnsureCameraDirector()?.Shake(unitData.onPlaceShakeStrength, unitData.onPlaceShakeDuration);
        }

        // on-place-skill-rework unit 0 — 규칙 경로(`DcTriggerKind.OnPlace`)의 발화 신호.
        // 브리지는 **사건만 알리고** 실행은 BossPeriodicTriggerSystem 이 한다 — 그래야
        // payload arm 사본이 늘지 않고, 배치 확정 지점이 셋이어도 태그 부착만 지키면 된다.
        //
        // ⚠ `DcTriggerSlot` 버퍼가 있는 유닛에만 붙인다. 소비 시스템이 그 버퍼를
        // `RequireForUpdate` 하므로, 슬롯이 하나도 없는 세계에서 태그만 붙으면 시스템이 안 돌아
        // 태그가 다음 프레임으로 새어 나간다.
        // ⚠ 레거시 `_onPlaceTriggeredEntities` 와 **권위가 다르다**(JustDeployed.cs 주석 참조).
        private void MarkJustDeployedForRules(Entity entity)
        {
            if (_em == null || entity == Entity.Null || !_em.Exists(entity)) return;
            if (!_em.HasBuffer<DcTriggerSlot>(entity)) return;
            if (!_em.HasComponent<Wassup.Battle.Units.JustDeployed>(entity))
                _em.AddComponent<Wassup.Battle.Units.JustDeployed>(entity);
        }

        private void TriggerOnPlaceAndSynergy(DefenderUnitData unitData, Vector2Int cell, Entity entity)
        {
            // Fixed order: onPlace → synergy recompute → log (PHASE4 §2.5 P4-05).
            // onPlace is a standalone snapshot effect and must fire before the
            // new defender's SynergyBuff is computed. Individual on-place effect
            // rules decide whether the placed defender is included.
            MarkJustDeployedForRules(entity);   // unit 0 — 즉시 배치(탭) 경로
            FireOnPlaceCameraShake(unitData);   // camera-direction unit 17
            _onPlaceTriggeredEntities.Add(entity);
            ApplyEffectTileOnce(cell, entity); // unit 8 — 자기 가드(재배치 재무장에 딸려오지 않는다)
            RecomputeSynergyFor(cell);
            LogSynergy(unitData, cell);
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

        public Unity.Entities.Entity SpawnHazardWithVisual(
            HazardSO so, Unity.Mathematics.int2 cell, byte targetTraversalLayers = 0)
        {
            if (so == null || _em == null)
                return Unity.Entities.Entity.Null;
            if (!TryGetNearestWalkCell(cell, out cell))
            {
                Debug.LogWarning("[BattleBridge] Cannot spawn hazard: generated map has no walk cells.");
                return Unity.Entities.Entity.Null;
            }

            var e = Wassup.Battle.Effects.EffectSpawner.SpawnHazard(
                _em, so, cell, targetTraversalLayers);
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
            // bomb-barrel-on-place unit 0 — 폭발 탄 SO→index 는 레지스트리를 가진 여기서 푼다.
            // sim 은 index 만 나른다(해저드 캐스트·투사체와 같은 관례).
            int explodeIndex = so.explodeProjectile != null
                ? GetOrCreateProjectileDataIndex(so.explodeProjectile)
                : -1;
            var entity = Wassup.Battle.Effects.EffectSpawner.SpawnBlockingHazard(_em, so, cell, dataIndex, explodeIndex);
            if (entity == Unity.Entities.Entity.Null)
            {
                RecordBlockingHazard(so, cell, "spawn_rejected", "EffectSpawner rejected spawn");
                return entity;
            }
            // battle-sim-extraction M0 unit 1 — 길막 해저드는 FactionTag+Health 를 들어
            // **타겟 후보**다(폭탄 배럴이 그 실례 — 적들이 때려 부순다). 스폰 자체는
            // EffectSpawner 가 하므로 발급은 그것을 부른 이 자리에서 한다.
            AttachSimEntityId(entity);

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
            // ⚠ SO 의 `spawnVfxPrefab` 은 여기서 넘기지 않으면 **죽은 저작**이다.
            // 프리젠터는 자기 직렬화 필드만 보고, 비어 있으면 코드로 만든 «떨어지는 돌»
            // 폴백을 돌린다 — 그래서 폭탄 배럴이 서는데 돌덩이가 쏟아졌다.
            // (기존 방벽도 SO 에 스폰 VFX 를 저작해 두고 같은 이유로 폴백을 쓰고 있었다.)
            presenter.SetSpawnVfxPrefab(so.spawnVfxPrefab);
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
                Wassup.Core.Trace.LegacyTraceRecorder.Ev(Wassup.Core.Trace.TraceChannel.HazardRuntime, SimIdOf(evt.target),
                    i: (int)evt.eventType * 100 + (int)evt.kind, f: evt.amount);
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
                Wassup.Core.Trace.LegacyTraceRecorder.Ev(Wassup.Core.Trace.TraceChannel.CastHazardSpawn,
                    SimIdOf(req.caster), SimIdOf(req.target), i: req.dataIndex);
                // bomb-barrel-on-place unit 2 — 시전자 생존 검사는 **존 해저드에만** 건다.
                // 존은 시전자에서 통행 층을 도출하므로 시전자가 사라지면 계약이 비지만,
                // 길막 설치물은 모양·체력·수명이 전부 SO 라 시전자를 안 쓴다. 그리고
                // 배럴은 비행 중 폭탄맨이 죽어도 서야 한다(spec 계약 7 — 투사체 자립).
                // 착탄 스폰은 owner 가 아예 Null 일 수도 있다(설치물 폭발 경로).
                //
                // ⚠ **층을 이미 실어 온 요청에는 이 검사가 성립하지 않는다**(ECS 리뷰 H-1).
                // 위 근거가 「존은 시전자에서 층을 도출한다」인데, 스킬 레이어의 요청은
                // **발화 시점 스냅샷**으로 층을 갖고 온다 — 그러면 시전자는 계약에 필요가
                // 없다. 그대로 두면 **동귀어진(킬러가 같은 프레임에 죽는 킬)에서만**
                // 불씨가 조용히 안 깔린다. 레거시 경로엔 이 게이트가 없었다.
                bool needsCaster = req.kind != HazardCastKind.Blocking
                                   && req.targetTraversalLayers == 0;
                if (needsCaster && !_em.Exists(req.caster)) continue;

                if (req.kind == HazardCastKind.Zone)
                {
                    if (req.dataIndex < 0 || req.dataIndex >= _zoneHazardRegistry.Count)
                    {
                        Debug.LogWarning($"[HazardCast] Invalid zone hazard index {req.dataIndex}; dropping.");
                        continue;
                    }

                    var so = _zoneHazardRegistry[req.dataIndex];
                    if (so == null) continue;
                    SpawnHazardWithVisual(so, req.centerCell, req.targetTraversalLayers);
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

        // bomb-barrel-on-place unit 8 — 길막 설치물의 머리 위 체력 바.
        //
        // 이 바가 unit 6 의 퓨즈 틴트를 **대체**한다. 배럴이 시간으로 안 터지게 된 뒤로
        // 「언제 터지나」의 답은 시계가 아니라 **남은 체력**이고, 그건 색으로 뭉뚱그릴 값이
        // 아니라 적이 얼마나 때렸는지를 그대로 읽어야 하는 값이다.
        //
        // 유닛·거점과 **같은 오버헤드 창**(Begin/EndFrame) 안에서 Set 한다 — 밖에서 부르면
        // EndFrame 의 `_seen` 소거에 걸려 매 프레임 사라진다(골 안정도 바의 선례).
        //
        // 순회 대상은 시각화된 설치물(`_blockingHazardVisualMap`)이다. 뷰가 없는 설치물에
        // 바를 띄울 자리가 없으므로 등록부가 곧 대상 목록이다.
        private void SyncBlockingHazardOverheadGauges(bool unifiedOverhead)
        {
            if (!unifiedOverhead || unitOverheadUiLayer == null) return;
            if (_blockingHazardVisualMap.Count == 0 || !HasLiveEntityManager()) return;
            var cam = Camera.main;
            if (cam == null) return;

            foreach (var pair in _blockingHazardVisualMap)
            {
                if (pair.Value == null) continue;
                var entity = pair.Key;
                if (!_em.Exists(entity)) continue;
                if (!_em.HasComponent<Wassup.Battle.Effects.BlockingHazard>(entity)) continue;
                if (!_em.HasComponent<Wassup.Battle.Effects.Obstacle>(entity)) continue;
                if (!_em.HasComponent<Health>(entity)) continue;

                int idx = _em.GetComponentData<Wassup.Battle.Effects.BlockingHazard>(entity).hazardSoIndex;
                if (idx < 0 || idx >= _blockingHazardSoRegistry.Count) continue;
                var so = _blockingHazardSoRegistry[idx];
                if (so == null || so.overheadHeight <= 0f) continue; // 0 = 바 없음(기존 설치물 선택권)

                var h = _em.GetComponentData<Health>(entity);
                var world = _em.GetComponentData<Wassup.Battle.Effects.Obstacle>(entity).worldPosition;
                var baseView = (Vector3)Wassup.Core.BoardSpace.ToView(new Vector3(world.x, 0f, world.z));
                Vector3 baseScreen = cam.WorldToScreenPoint(baseView);
                Vector3 topScreen = cam.WorldToScreenPoint(baseView + Vector3.up * so.overheadHeight);
                Vector3 a = cam.WorldToScreenPoint(baseView - Vector3.right * (tileSize * 0.5f));
                Vector3 bScreen = cam.WorldToScreenPoint(baseView + Vector3.right * (tileSize * 0.5f));
                float tileScreenWidth = Vector2.Distance(new Vector2(a.x, a.y), new Vector2(bScreen.x, bScreen.y));

                // 배럴은 **플레이어가 놓은 물건**이라 방어유닛 스킨을 쓴다. 카드 행은
                // 저절로 비는데(`_cardsByHost` 에 없다) 설치물엔 드림캐쳐를 못 붙이기 때문이다.
                unitOverheadUiLayer.SetUnit(entity, true,
                    Health.ComputeRatio(h.value, h.max),
                    new Vector2(baseScreen.x, topScreen.y), tileScreenWidth);
            }
        }

        private void DrainHazardDestroyedEvents()
        {
            if (!_hazardDestroyedQueue.IsCreated) return;
            while (_hazardDestroyedQueue.TryDequeue(out var evt))
            {
                Wassup.Core.Trace.LegacyTraceRecorder.Ev(Wassup.Core.Trace.TraceChannel.HazardDestroyed,
                    SimIdOf(evt.hazardEntity), i: evt.hazardSoIndex);
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
                Wassup.Core.Trace.LegacyTraceRecorder.Ev(Wassup.Core.Trace.TraceChannel.GoalCollapsed,
                    SimIdOf(evt.entity), i: evt.goalIndex);
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
                // heart-stress-axis unit 9 — **마음도 이 루프에서 바를 받는다.**
                // 아래 마음 분기는 스트레스 연출(틴트·심박·비네트)만 하고 **`continue` 하지
                // 않는다** — 그대로 공용 바 경로로 떨어져 본능·유닛과 같은 코드로 그려진다.
                // unit 1 rev 1 이 머리 위 바를 반려했던 이유(「다른 바와 문법이 같아 임팩트 0」)는
                // 그 바가 **연출까지 겸직**하려 했기 때문이다. 연출을 다른 세 채널이 다 가져간
                // 지금 바가 지는 짐은 정보뿐이고, 그래서 «같은 문법» 이 장점이 된다.
                bool isHeart = faction == Faction.DefenderCore;
                if (!_em.Exists(entity) || !_em.HasComponent<Health>(entity)) continue; // 붕괴 정리는 EndFrame/드레인
                var h = _em.GetComponentData<Health>(entity);
                float ratio = Health.ComputeRatio(h.value, h.max);
                // heart-stress-axis unit 9 rev 2 — **마음 바만 다른 값·다른 스킨·다른 크기를 쓴다.**
                // rev 1 은 순수 체력비(줄어드는 바)였고 rev 2 에서 사용자가 **차오르는 스트레스
                // 0~100** 으로 되돌렸다. 되돌린 값은 아래 마음 분기가 채우고, **호출은 여전히
                // 공용 경로 한 곳**이다 — 마음 전용 `SetUnit` 을 새로 만들면 그때부터
                // 「마음 바는 왜 따로 도나」가 시작된다(rev 1 이 산 교훈이라 유지한다).
                float barValue = ratio;                              // 비-마음: 남은 체력
                Wassup.Data.OverheadBarSkin? barSkin = null;         // 비-마음: 진영 기본 스킨
                float barScale = 1f;                                 // 비-마음: 안 튄다
                bool showBar = true;                                 // 비-마음: 항상 건다
                // 연출 캐리어가 네 번 갈렸다: 머리 위 바(rev 1 — 문법 중복) → 보드 3×3 잠식
                // (rev 2 — 「주변 타일 하이라이트는 쓸모없다」) → **마음 프랍 붉은 틴트 + 심박**
                // (확정) → 화면은 URP 포스트 비네트(unit 8 rev 2). **보드 잠식은 되살리지 말 것.**
                // (머리 위 바는 unit 9 가 «정보 전용» 으로 되살렸다 — 위 주석 참조.)
                //
                // ⚠ `OverheadBarSkin.Stress`·`fadeAtEmpty`·`SetGoalCrack` 은 **호출처 0 인 휴면**이다.
                // unit 9 가 «순수 체력» 을 못박아 Stress 스킨은 **영구 휴면**이 됐다(마음 바는
                // Defender 스킨을 쓴다). README 후속 후보 「휴면 코드 정리」 소관.
                //
                // ⚠ 머리 위 숫자(`87 / 100`)는 unit 9 에서 **꺼졌다** — `ScoreHudView`
                // `showHeartStressReadout`(기본 false). `SetHeartStress` 는 계속 호출된다
                // (비네트·심박이 그 통로를 쓴다) — 숫자만 안 그린다.
                if (isHeart)
                {
                    float stress = Wassup.Core.StressMath.FromHealth(h.value, h.max);
                    float stress01 = stress / Wassup.Core.StressMath.Max;

                    // heart-stress-axis unit 1 rev 2 — **심박의 계산 주체는 여기 하나다.**
                    // 마음 프랍과 화면 림이 같은 배율을 받아야 «마음과 화면이 같이 뛴다».
                    // 각자 돌리면 파라미터가 갈리는 순간 위상이 어긋나 두 개로 읽힌다.
                    // 위상을 시각에서 파생하지 않고 **누적**하는 이유: 심박이 스트레스에 따라
                    // 빨라지는데 `time × bpm` 으로 접으면 bpm 이 바뀔 때 위상이 튄다(박이 끊긴다).
                    // heart-stress-axis unit 8 — **단계가 모든 채널의 공통 클록이다.**
                    // 히스테리시스라 직전 단계를 넘긴다(경계에서 깜빡이면 늑대소년이 된다).
                    int prevStage = _heartStage;
                    _heartStage = Wassup.Presentation.HeartStressPulse.StageOf(stress01, _heartStage);
                    if (_heartStage != prevStage)
                    {
                        // 단계 전이는 판당 몇 번뿐이라 로그가 싸다. 그리고 「연출이 적용됐나」를
                        // 콘솔로 확정할 수 있는 유일한 지점이다 — 화면에 안 보이는 것이
                        // «값이 안 움직여서» 인지 «연출이 약해서» 인지 이 줄이 가른다.
                        Debug.Log($"[BattleBridge] 마음 스트레스 단계 {prevStage} → {_heartStage} "
                                + $"(스트레스 {stress:F0}/100)");
                    }
                    float bpm = Wassup.Presentation.HeartStressPulse.Bpm(_heartStage, heartRestBpm, heartMaxBpm);
                    _heartBeatPhase = Wassup.Presentation.HeartStressPulse.AdvancePhase(
                        _heartBeatPhase, Time.unscaledDeltaTime, bpm);
                    float beat = Wassup.Presentation.HeartStressPulse.Beat(_heartBeatPhase);
                    float beatScale = Wassup.Presentation.HeartStressPulse.BeatScale(beat, heartBeatDepth);

                    // map-diorama-stage 병합 수선 — 마음 틴트의 host 는 뷰 프랍이 아니라 스테이지
                    // GoalMarker 다(unit 4: 마커 뷰가 골 연출 소유). 구 SetGoalStressTint 는 뷰와 함께 은퇴.
                    if (_goalMarkersByCell.TryGetValue(cell, out var stressMarker) && stressMarker != null)
                        stressMarker.SetStressTint(stress01, beatScale);

                    // 화면 연출의 스파이크 입력은 **넷 상승분**이다. 마음 피해를 실어 나르는
                    // 이벤트가 없어(데미지 폰트는 AttackUnitTag 적 전용) 폴링이 유일한 소스이고,
                    // 같은 프레임에 킬 회복이 상쇄하면 안 튀는 것이 옳다(실제로 안 올랐으므로).
                    float rise = Mathf.Max(0f, stress - _lastHeartStress);
                    _lastHeartStress = stress;

                    // 숫자는 **마음 위**에 뜬다 — 보드 위 상시 숫자는 마음만의 기호라
                    // 반려된 «머리 위 바»(본능·적 마음·유닛과 같은 문법)의 사유를 안 밟는다.
                    bool anchorOk = false;
                    Vector2 labelAnchor = default;
                    if (cam != null)
                    {
                        var w = GridToWorldCenter(cell);
                        var bv = (Vector3)Wassup.Core.BoardSpace.ToView(new Vector3(w.x, 0f, w.z));
                        Vector3 top = cam.WorldToScreenPoint(bv + Vector3.up * goalOverheadHeight);
                        Vector3 bs = cam.WorldToScreenPoint(bv);
                        anchorOk = top.z > 0f;
                        labelAnchor = new Vector2(bs.x, top.y);
                    }
                    scoreHud?.SetHeartStress(stress01, beatScale, rise, _heartStage, labelAnchor, anchorOk);

                    // 바가 그리는 것 = **차오르는 스트레스**(0 → 1). `OverheadBarSkin.Stress` 는
                    // 색이 아니라 **방향** 때문에 필요하다 — 이 바는 만점에서 감쇠하면 안 되고
                    // (`fadeAtEmpty`) 빈 쪽이 「정보 없음」이다. 스킨의 fillLow→fillHigh 램프가
                    // 그대로 **파랑 → 빨강** 틴트가 된다(에셋 저작, 코드에 색 없음 — 제약 6).
                    barValue = stress01;
                    barSkin = Wassup.Data.OverheadBarSkin.Stress;
                    // **스트레스 0 이면 바를 아예 안 건다**(사용자 지시). 흐리게 두는 것과
                    // 다르다 — 알파만 낮추면 «빈 바» 가 계속 자리를 차지해, 아직 아무 일도
                    // 안 일어난 판에서 마음이 «이미 뭔가 재고 있는» 것처럼 보인다.
                    // 이 프레임에 `SetUnit` 을 안 부르면 `EndFrame` 이 뷰를 회수하고(_seen 미포함),
                    // 다시 오를 때 `resetHealth` 로 새 값에서 시작한다(옛 값에서 안 늘어난다).
                    showBar = stress01 > 0f;
                    // 펀치는 «상승분» 이 입력이다 — 같은 프레임에 킬 회복이 상쇄하면 안 튀는 것이
                    // 옳다(실제로 안 올랐으므로). `rise` 는 위에서 이미 그렇게 구했다.
                    _heartBarPunch = Wassup.Presentation.HeartStressPulse.AdvancePunch(
                        _heartBarPunch, rise, heartBarPunchFullRise,
                        Time.unscaledDeltaTime, heartBarPunchDecayPerSec);
                    barScale = Wassup.Presentation.HeartStressPulse.PunchScale(_heartBarPunch, heartBarPunchDepth);

                    // ⚠ **여기서 `continue` 하지 않는다.** 아래 공용 경로로 떨어져 바를 받는다.
                    // 되돌리면 바가 조용히 사라진다.
                }
                if (!showBar)
                {
                    // 레거시 경로는 «안 그림» 이 자동이 아니다 — 명시적으로 지운다.
                    // (통합 경로는 `SetUnit` 을 안 부른 것만으로 `EndFrame` 이 회수한다.)
                    if (!unifiedOverhead) tileHealthGaugeLayer?.Hide(cell);
                    continue;
                }
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
                    // 마음(`DefenderCore`)도 `Factions.AnyDefender` 에 포함이라 Defender 스킨을
                    // 받는다 — 마음만의 분기를 만들지 않는 것이 unit 9 의 요점이다.
                    unitOverheadUiLayer.SetUnit(entity,
                        ((int)faction & Wassup.Battle.Units.Factions.AnyDefender) != 0,
                        barValue, anchor, tileScreenWidth, 0f,
                        GatherOverheadStacks(entity), barSkin, barScale);
                }
                else if (!unifiedOverhead && tileHealthGaugeLayer != null)
                {
                    tileHealthGaugeLayer.Set(cell, baseView, tileSize, barValue);
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


        // skill-layer-migration unit 2g — **배치 효과 로그가 은퇴했다.** 레거시 enum 이
        // 사라지면서 「무슨 효과가 몇 명에게」를 브리지가 알 수 없게 됐다 — 그건 이제
        // 스킬 레이어의 사실이고, 필요하면 그쪽 계측(`ExecutedCountOf`)이 답한다.
        // 남은 것은 시너지 통계뿐이라 이름도 그에 맞췄다.
        private void LogSynergy(DefenderUnitData unitData, Vector2Int cell)
        {
            var logger = GameManager.Instance?.Logger;
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

        // nightmare-catcher unit 5 — 특수 메커닉 bake (병렬 경로): nightmareMechanics 를 선언한
        // 적에게 DcTriggerSlot 을 부착한다. defender 부착 API(ApplyDreamcatcherCardToUnit —
        // defender 가드 + 손패 회수 레지스트리)는 의도적으로 미사용: 이 슬롯은
        // 손패 순환과 무관하고, teardown 은 AttackUnitTag 적 경로 상속(신규 0).
        //
        // elite-enemy-tier unit 0 — ★**메커닉 bake 와 보스 부속물이 갈렸다.** 그 앞까지는
        // 「mechanics 가 비어있지 않다 = 보스」였고, 그래서 특수 메커닉을 준 엘리트가 자동으로
        // BossTag 를 얻어 CC·어그로 면역까지 딸려왔다(면역 술어들이 전부 BossTag 를 탄다).
        // 이제 보스 부속물은 `tier == Boss` 만 받고, 슬롯은 티어 무관으로 붙는다.
        // skill-layer-foundation unit 5 — 레지스트리에 concrete 를 등록하고 디스패처에
        // 넘긴다. 매치 경계마다 부른다 — 레지스트리 자체는 무상태라 재등록만 막으면 된다.
        //
        // ⚠ **여기 없는 concrete 는 발동하지 않는다.** `SkillIdForPayload` 가 skillId 를
        // 굽는데 레지스트리에 없으면 디스패처가 loud 하게 버린다. 둘은 항상 같이 는다.
        private void InstallSkillLayer()
        {
            if (_skillRegistry.Count == 0)
            {
                _skillRegistry.Register(new Wassup.Skills.Concrete.AreaSleepSkill());
                _skillRegistry.Register(new Wassup.Skills.Concrete.AllySpeedAuraSkill());
                _skillRegistry.Register(new Wassup.Skills.Concrete.GrantShieldSkill());
                _skillRegistry.Register(new Wassup.Skills.Concrete.SelfAreaBlastSkill());
                _skillRegistry.Register(new Wassup.Skills.Concrete.BlinkToClusterSkill());
                _skillRegistry.Register(new Wassup.Skills.Concrete.UltimateLeapSkill());
                _skillRegistry.Register(new Wassup.Skills.Concrete.EmitPatternSkill());
                _skillRegistry.Register(new Wassup.Skills.Concrete.AreaTauntSkill());
                _skillRegistry.Register(new Wassup.Skills.Concrete.ConeBreathSkill());
                _skillRegistry.Register(new Wassup.Skills.Concrete.AllyStatAuraSkill());
                _skillRegistry.Register(new Wassup.Skills.Concrete.OpponentStatAuraSkill());
                _skillRegistry.Register(new Wassup.Skills.Concrete.GainCostSkill());
                _skillRegistry.Register(new Wassup.Skills.Concrete.ReduceSkillCooldownSkill());
                _skillRegistry.Register(new Wassup.Skills.Concrete.AreaStackSkill());
                _skillRegistry.Register(new Wassup.Skills.Concrete.AreaCcSkill());
                _skillRegistry.Register(new Wassup.Skills.Concrete.AreaDotSkill());
                _skillRegistry.Register(new Wassup.Skills.Concrete.TargetCcSkill());
                _skillRegistry.Register(new Wassup.Skills.Concrete.TargetStackSkill());
                _skillRegistry.Register(new Wassup.Skills.Concrete.SelfStatBuffSkill());
                _skillRegistry.Register(new Wassup.Skills.Concrete.ThresholdSelfBuffSkill());
                _skillRegistry.Register(new Wassup.Skills.Concrete.TargetProjectileSkill());
                _skillRegistry.Register(new Wassup.Skills.Concrete.DeathSiteBlastSkill());
                _skillRegistry.Register(new Wassup.Skills.Concrete.DeathSiteHazardSkill());
                _skillRegistry.Register(new Wassup.Skills.Concrete.GrantSelfChargeSkill());
                _skillRegistry.Register(new Wassup.Skills.Concrete.OrbitProjectileSkill());
                _skillRegistry.Register(new Wassup.Skills.Concrete.SelfBuffLethalSkill());
                _skillRegistry.Register(new Wassup.Skills.Concrete.DreamCocoonSkill());
                _skillRegistry.Register(new Wassup.Skills.Concrete.BountyMarkSkill());
                _skillRegistry.Register(new Wassup.Skills.Concrete.CastHazardSkill());
                _skillRegistry.Register(new Wassup.Skills.Concrete.TileStatBurstSkill());
                _skillRegistry.Register(new Wassup.Skills.Concrete.AllyBuffFieldSkill());
                _skillRegistry.Register(new Wassup.Skills.Concrete.PullFieldSkill());
                _skillRegistry.Register(new Wassup.Skills.Concrete.PortalSkill());
                _skillRegistry.Register(new Wassup.Skills.Concrete.TileMeteorSkill());
            }
            // 스택 상한 표 — 저작 SO 가 권위다. 도메인은 상한을 모르고 어댑터가 푼다.
            var caps = new byte[System.Enum.GetValues(typeof(Wassup.Battle.Effects.StackKind)).Length];
            if (stackModifierAuthoring != null)
                foreach (var so in stackModifierAuthoring)
                    if (so != null) caps[(int)so.kind] = so.maxStack;
            _skillContext.BindStackCaps(caps);

            // 빔 싱크 — 대상별 빔 세션은 뷰라 프레젠터가 연다. 어댑터는 프리팹을 모르고
            // **index 만** 넘긴다(투사체 dataIndex 와 같은 규약).
            _skillContext.BindBeamSink((src, dst, idx, ttl) =>
            {
                if (idx < 0 || idx >= _skillVfxPrefabs.Count) return;
                var prefab = _skillVfxPrefabs[idx];
                if (prefab == null) return;
                EnsureBeamPresenter().Open(dst, prefab, source: src, target: dst, ttlSec: ttl);
            });

            // 판 밖 런타임 싱크 — 이 델리게이트가 스킬 레이어와 Mono 자원 사이의 유일한 통로다.
            _skillContext.BindMetaSink(intent =>
            {
                switch (intent.Kind)
                {
                    case Wassup.Skills.MetaIntentKind.GainCost:
                        GameManager.Instance?.CostRuntime?.AddCost(Mathf.RoundToInt(intent.Amount));
                        break;
                    case Wassup.Skills.MetaIntentKind.ReduceSkillCooldown:
                        skillRuntime?.ReduceAllCooldowns(intent.Amount);
                        break;
                }
            });
            Wassup.Battle.Skills.SkillDispatchSystemBase.Install(_skillRegistry, _skillContext);
        }

        // skill-layer-foundation unit 5 — payload → concrete 라우팅.
        //
        // **스킬은 전부 여기 있다.** 없는 payload 는 0 을 받는데, 그 뜻이 이전 도중
        // 뒤집혔다 — 예전엔 「arm 이 처리한다」였고 지금은 **「아무도 처리 안 한다」**다.
        // 그래서 bake 게이트가 「스킬인데 여기 없음」을 거절한다(`SkillPayloadPolicy`).
        // 여기 한 줄을 빠뜨리면 그 조합은 슬롯조차 안 생기고 loud 경고가 난다.
        // skill-layer-migration unit 3d — **라우팅 키는 (트리거 × payload) 다.**
        // 저작의 키가 원래 그것이고(`DcMechanic`), payload 하나가 트리거에 따라 다른
        // 스킬이 되는 조합이 실재한다: `SelfTileAoe` 는 **내가 맞은 자리**에서 터지지만
        // (`HealthThreshold`), 처치 트리거에서는 **내가 죽인 자리**에서 터진다.
        // 그 둘은 같은 「광역 폭발」이라도 게임에서 완전히 다른 그림이다.
        // skill-layer-migration unit 4a — **부착 seam 을 그 자리에서 돌린다.**
        //
        // 부착은 동기 트랜잭션이다(preflight → 쓰기 → 핸들/−1). 큐에 넣고 프레임을
        // 기다리면 그 결정 뒤에 쓰기가 도착하므로, 여기서 드레인까지 끝낸다.
        // 시스템이 그룹에도 있는 이유는 안전망이다 — 브리지 밖에서 누가 이 seam 으로
        // 넣었을 때 다음 틱에 소진된다.
        private void RunImmediateSkills()
        {
            if (_world == null || !_world.IsCreated) return;
            _world.GetExistingSystemManaged<Wassup.Battle.Skills.SkillDispatchImmediateSystem>()
                ?.Update();
        }

        private static int SkillIdForMechanic(Wassup.Data.DcTriggerKind trigger,
                                              Wassup.Data.DcPayloadKind kind)
        {
            if (trigger == Wassup.Data.DcTriggerKind.OnKill)
            {
                if (kind == Wassup.Data.DcPayloadKind.SelfTileAoe)
                    return Wassup.Skills.Concrete.DeathSiteBlastSkill.Id;
                if (kind == Wassup.Data.DcPayloadKind.SpawnHazard)
                    return Wassup.Skills.Concrete.DeathSiteHazardSkill.Id;
            }
            // unit 3d″ — **작별 선물.** `OnKill × SelfTileAoe`(시체폭발)와 같은 concrete 를
            // 쓴다. 「실려 온 자리에서 터진다」가 같은 규칙이고, **누구의 자리인가**는
            // 스킬이 아니라 감지자가 정하기 때문이다(죽인 자리 ↔ 죽은 자리).
            // ⚠ `SkillIdForPayload(SelfTileAoe)` 로 가면 **안 된다** — 그건 살아 있는
            // 시전자 발밑을 묻는 `SelfAreaBlastSkill` 이고, 드레인 시점엔 시전자가 없다.
            if (trigger == Wassup.Data.DcTriggerKind.OnDeath
                && kind == Wassup.Data.DcPayloadKind.SelfTileAoe)
                return Wassup.Skills.Concrete.DeathSiteBlastSkill.Id;
            // unit 3d‴ — 피격 N회. **자기 자리 폭발**은 살아 있는 시전자 발밑이라
            // `SelfAreaBlastSkill` 이 맞다(작별 선물과 반대 축이다).
            if (trigger == Wassup.Data.DcTriggerKind.OnDamagedN)
            {
                if (kind == Wassup.Data.DcPayloadKind.SelfTileAoe)
                    return Wassup.Skills.Concrete.SelfAreaBlastSkill.Id;
                // ⚠ `NextAttackDoubleFire` 는 여기가 아니라 **트리거 무관 스위치**에 있다.
                // 여기 두면 `OnPlace × 충전` 이 라우팅을 못 찾아 0(=스킬 아님)이 되는데,
                // 그 payload 의 arm 은 이미 철거돼서 **아무 일도 안 하고 아무 말도 안 한다.**
                // 그 침묵을 PlayMode 가 잡았다(unit 8).
            }
            // unit 3e — 실드 파열. 피격 N회와 **같은 실행기**를 쓰므로 모양이 같다.
            // ⚠ `AreaSleep` 은 concrete 가 「재우자마자 내가 깨울 자리」를 뺀다 — 레거시
            // 파열엔 없던 규칙이다. 재우는 **수**는 그대로고(뺄 만큼 더 뽑는다) 달라지는
            // 것은 «누가» 자느냐다. 자장가의 계약이 그쪽이 옳다고 보므로 concrete 를
            // 둘로 가르지 않고 이 차이를 여기 적어 둔다.
            if (trigger == Wassup.Data.DcTriggerKind.OnShieldBreak)
            {
                if (kind == Wassup.Data.DcPayloadKind.SelfTileAoe)
                    return Wassup.Skills.Concrete.SelfAreaBlastSkill.Id;
                if (kind == Wassup.Data.DcPayloadKind.AreaSleep)
                    return Wassup.Skills.Concrete.AreaSleepSkill.Id;
            }
            // unit 3e — 퇴근 운석. 죽은 자리 폭발과 **같은 규칙**이다(실려 온 자리에서
            // 터진다). 다른 것은 값뿐 — 자리의 주인이 「비워진 칸」이고 예고 시간이 있다.
            if (trigger == Wassup.Data.DcTriggerKind.OnRetire
                && kind == Wassup.Data.DcPayloadKind.SelfTileAoe)
                return Wassup.Skills.Concrete.DeathSiteBlastSkill.Id;
            // unit 4a — **부착되는 순간** 발동하는 것들(트리거 없음). 이 조합은 감지자가
            // 아니라 **부착 지점**이 발화시킨다.
            if (trigger == Wassup.Data.DcTriggerKind.None)
            {
                if (kind == Wassup.Data.DcPayloadKind.SelfBuffLethal)
                    return Wassup.Skills.Concrete.SelfBuffLethalSkill.Id;
                if (kind == Wassup.Data.DcPayloadKind.DreamCocoon)
                    return Wassup.Skills.Concrete.DreamCocoonSkill.Id;
                if (kind == Wassup.Data.DcPayloadKind.BountyMark)
                    return Wassup.Skills.Concrete.BountyMarkSkill.Id;
            }
            // 경계에서 켜진 자기 버프는 **출처가 다르다**(「빈사에서 켜졌다」).
            if (trigger == Wassup.Data.DcTriggerKind.HealthThreshold
                && kind == Wassup.Data.DcPayloadKind.SelfStatBuff)
                return Wassup.Skills.Concrete.ThresholdSelfBuffSkill.Id;
            return SkillIdForPayload(kind);
        }

        private static int SkillIdForPayload(Wassup.Data.DcPayloadKind kind)
        {
            switch (kind)
            {
                case Wassup.Data.DcPayloadKind.AreaSleep:
                    return Wassup.Skills.Concrete.AreaSleepSkill.Id;
                case Wassup.Data.DcPayloadKind.AllyMoveSpeedAura:
                    return Wassup.Skills.Concrete.AllySpeedAuraSkill.Id;
                case Wassup.Data.DcPayloadKind.GrantShield:
                    return Wassup.Skills.Concrete.GrantShieldSkill.Id;
                case Wassup.Data.DcPayloadKind.SelfTileAoe:
                    return Wassup.Skills.Concrete.SelfAreaBlastSkill.Id;
                case Wassup.Data.DcPayloadKind.SelfBlink:
                    return Wassup.Skills.Concrete.BlinkToClusterSkill.Id;
                case Wassup.Data.DcPayloadKind.UltimateLeap:
                    return Wassup.Skills.Concrete.UltimateLeapSkill.Id;
                case Wassup.Data.DcPayloadKind.EmitProjectilePattern:
                    return Wassup.Skills.Concrete.EmitPatternSkill.Id;
                case Wassup.Data.DcPayloadKind.AreaTaunt:
                    return Wassup.Skills.Concrete.AreaTauntSkill.Id;
                case Wassup.Data.DcPayloadKind.AreaBreath:
                    return Wassup.Skills.Concrete.ConeBreathSkill.Id;
                // 충전 부여는 **트리거를 모른다** — 「다음 공격이 세진다」는 무엇이 그것을
                // 불렀든 같은 일이다. 트리거별 블록에 두면 그 트리거 밖 조합이 조용히 죽는다.
                case Wassup.Data.DcPayloadKind.NextAttackDoubleFire:
                    return Wassup.Skills.Concrete.GrantSelfChargeSkill.Id;
                // 장판도 **실려 온 자리**에 깔린다 — 「누구의 자리인가」는 감지자가 정한다
                // (`DeathSiteBlastSkill` 과 같은 논리). `SelfTileAoe` 가 concrete 둘로 갈린
                // 것은 「죽은 자리 ↔ 내 발밑」이 **다른 규칙**이어서인데, 장판은 그 갈림이
                // 없다. OnKill 블록에만 두면 나머지 조합이 조용히 죽는다.
                case Wassup.Data.DcPayloadKind.SpawnHazard:
                    return Wassup.Skills.Concrete.DeathSiteHazardSkill.Id;
                case Wassup.Data.DcPayloadKind.AllyStatAura:
                    return Wassup.Skills.Concrete.AllyStatAuraSkill.Id;
                case Wassup.Data.DcPayloadKind.OpponentStatAura:
                    return Wassup.Skills.Concrete.OpponentStatAuraSkill.Id;
                case Wassup.Data.DcPayloadKind.GainCost:
                    return Wassup.Skills.Concrete.GainCostSkill.Id;
                case Wassup.Data.DcPayloadKind.ReduceSkillCooldown:
                    return Wassup.Skills.Concrete.ReduceSkillCooldownSkill.Id;
                case Wassup.Data.DcPayloadKind.AreaApplyStack:
                    return Wassup.Skills.Concrete.AreaStackSkill.Id;
                case Wassup.Data.DcPayloadKind.AreaCc:
                    return Wassup.Skills.Concrete.AreaCcSkill.Id;
                case Wassup.Data.DcPayloadKind.AreaDot:
                    return Wassup.Skills.Concrete.AreaDotSkill.Id;
                case Wassup.Data.DcPayloadKind.ApplyCcToTarget:
                    return Wassup.Skills.Concrete.TargetCcSkill.Id;
                case Wassup.Data.DcPayloadKind.ApplyStackToTarget:
                    return Wassup.Skills.Concrete.TargetStackSkill.Id;
                case Wassup.Data.DcPayloadKind.SelfStatBuff:
                    return Wassup.Skills.Concrete.SelfStatBuffSkill.Id;
                case Wassup.Data.DcPayloadKind.ProjectileToTarget:
                    return Wassup.Skills.Concrete.TargetProjectileSkill.Id;
                case Wassup.Data.DcPayloadKind.SelfOrbitProjectile:
                    return Wassup.Skills.Concrete.OrbitProjectileSkill.Id;
                default:
                    return Wassup.Skills.SkillRegistry.NotRouted;
            }
        }

        // skill-layer-migration unit 3g — **카드와 유닛이 같은 규칙을 쓴다.**
        //
        // 이 함수는 원래 카드 전용 화이트리스트였다. 이전 중에는 그게 안전장치였다 —
        // arm 이 검증되기 전에 카드가 새 경로를 타면 「슬롯은 붙는데 아무도 안 읽는」
        // 조용한 죽음이 되고, 컴파일러도 테스트도 그 연결을 안 잡는다.
        //
        // 이제 도달 가능한 행이 전부 이전됐고 각자 그물을 갖췄으므로 화이트리스트를 은퇴한다.
        // ⚠ **두 벌로 두는 것 자체가 이제 위험이다** — 특수 케이스(자리의 주인이 다른 폭발
        // 셋 등)를 한쪽에만 추가하면 같은 저작이 host 종류에 따라 다른 스킬로 간다.
        //
        // 여전히 concrete 가 없는 payload 는 `SkillIdForPayload` 의 default 가 0 을 준다.
        // 그건 이제 **「스킬이 아니다」**이고, 스킬인데 0 인 조합은 bake 게이트가 거절한다.
        private static int SkillIdForCardPayload(Wassup.Data.DcTriggerKind trigger,
                                                 Wassup.Data.DcPayloadKind kind)
            => SkillIdForMechanic(trigger, kind);

        // skill-layer-migration unit 8 — **그물용 창.** 라우팅이 0 을 돌려주는 조합은
        // 이제 「arm 이 처리한다」가 아니라 **「아무도 처리 안 한다」**를 뜻한다(arm 이
        // 철거됐으므로). 그 침묵을 EditMode 가 전수로 잡을 수 있게 연다 —
        // PlayMode 가 `OnPlace × 충전` 하나를 잡아낸 뒤에 판 창이다.
        public static int RoutingProbe(Wassup.Data.DcTriggerKind trigger,
                                       Wassup.Data.DcPayloadKind kind)
            => SkillIdForMechanic(trigger, kind);

        private void BakeNightmareMechanics(Entity entity, AttackUnitData unitType)
        {
            var mechanics = unitType.nightmareMechanics;
            if (mechanics == null || mechanics.Length == 0) return;

            // ⚠ 보스 부속물은 **아래 AddBuffer<DcTriggerSlot> 보다 앞**이어야 한다 — 그 핸들은
            // 「마지막 AddBuffer」라는 전제로 루프까지 캐시된다(아래 주석). 여기에 구조 변경을
            // 추가할 때도 이 순서를 지킬 것.
            if (unitType.tier == Wassup.Data.EnemyTier.Boss)
            {
                _em.AddComponent<BossTag>(entity);
                // boss-wave-cadence unit 2 — 보스 판별의 단일 진실 지점. 여기서만 경보를 구동해
                // SpawnUnit 재판정(로직 이중화·이중 발화)을 피한다. 재진입 코얼레스는 뷰가 담당.
                _bossWarning?.Show();
                // 위협 테이블은 보스와 항상 동행 — 텔레포트 arm 의 타겟 소스.
                // defender 히트가 쌓기 전까지 빈 버퍼(ThreatHitEvent 드레인이 채움).
                _em.AddBuffer<ThreatEntry>(entity);
            }
            BakeUnitMechanics(entity, mechanics, hostIsEnemy: true, maxHpRef: unitType.health,
                ownerLabel: unitType.displayName, enemyOwner: unitType);
        }

        // on-place-skill-rework unit 0 — **진영 중립 메커닉 bake.** 구 BakeNightmareMechanics 의
        // 본문이며, 적 전용이던 세 가지를 파라미터로 끌어올렸다:
        //   ① hostIsEnemy — BuildPatternTemplate 이 이 값으로 targetFaction 을 파생시킨다.
        //      빠뜨리면 **방어유닛이 쏜 패턴이 방어유닛을 때린다.**
        //   ② maxHpRef    — HealthThreshold 트리거의 기준 최대 체력.
        //   ③ 허용 트리거 화이트리스트 — 적/방어유닛이 여는 문이 다르다(DcTrigger).
        // 보스 부속물(BossTag·ThreatEntry·경보)은 적 호출처에 남는다.
        //
        // enemyOwner 는 SplitOnDeath 사슬 검증에만 쓰이는 적 전용 참조다(방어유닛은 null).
        private void BakeUnitMechanics(Entity entity, Wassup.Data.DcMechanic[] mechanics,
            bool hostIsEnemy, float maxHpRef, string ownerLabel, AttackUnitData enemyOwner)
        {

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
                    Debug.LogWarning($"[BattleBridge] {ownerLabel} mechanic {i}: None kind — skipped.");
                    continue;
                }
                // elite-enemy-tier unit 5 — 분열은 **의도적 무슬롯**이다. sim 이 쓸 값이 없고
                // 실행은 브리지 킬 드레인이 SO 를 직독해서 한다(DcPayloadKind.SplitOnDeath 주석).
                // 아래 화이트리스트에 걸리게 두면 슬라임을 스폰할 때마다 거짓 경고가 뜬다.
                // 저작 검증만 여기서 하고 슬롯 생성을 건너뛴다.
                if (m.trigger.kind == Wassup.Data.DcTriggerKind.OnDeath &&
                    m.payload.kind == Wassup.Data.DcPayloadKind.SplitOnDeath)
                {
                    if (m.payload.splitUnit == null)
                        Debug.LogError($"[BattleBridge] {ownerLabel} mechanic {i}: SplitOnDeath 인데 splitUnit 이 비었다 — 죽어도 안 갈라진다.");
                    else if (m.payload.magnitude < 1f)
                        Debug.LogError($"[BattleBridge] {ownerLabel} mechanic {i}: SplitOnDeath magnitude({m.payload.magnitude}) < 1 — 자식이 0기다.");
                    // 조용한 clamp 는 clamp 를 둔 이유를 스스로 무력화한다 — 100 을 타이핑한
                    // 저작자가 8기를 받고 아무 메시지도 못 받는 것은 위 두 거절과 비대칭이다.
                    else if (m.payload.magnitude > MaxSplitChildren)
                        Debug.LogError($"[BattleBridge] {ownerLabel} mechanic {i}: SplitOnDeath magnitude({m.payload.magnitude}) > {MaxSplitChildren} — {MaxSplitChildren}기로 잘린다.");
                    // ★«자식이 메커닉을 갖고 있나» 가 아니라 «사슬이 순환하나» 를 본다.
                    // 다단계 분열(슬라임 → 중간 → 작은)은 의도이고, 무한 분열을 만드는 것은
                    // 사슬이 자기에게 돌아오는 것뿐이다. 판정은 순수 함수 1곳이 소유한다.
                    else if (enemyOwner != null && !Wassup.Data.SplitChain.Validate(enemyOwner, out string splitError))
                        Debug.LogError($"[BattleBridge] {ownerLabel} mechanic {i}: {splitError}");
                    continue;
                }
                // skill-layer-migration unit 8 — **묻는 질문이 바뀌었다.**
                // 예전엔 「이 트리거를 이 진영에 붙여도 «안전»한가」였다(자기진영 타격 방지).
                // 실행이 스킬 레이어로 가면서 그 위험이 사라졌고, 이제 남은 질문은
                // 「이 조합을 «잡는 감지자가 있나»」 하나다. 판정은 순수 술어 1곳이 소유한다.
                if (!Wassup.Battle.Combat.DcTrigger.HasDetector(m.trigger.kind, hostIsEnemy))
                {
                    Debug.LogWarning(
                        $"[BattleBridge] {ownerLabel} mechanic {i}: trigger '{m.trigger.kind}' 를 "
                        + $"{(hostIsEnemy ? "적" : "방어유닛")} 에 붙였는데 그 조합을 잡는 감지자가 없다 — 건너뛴다. "
                        + "슬롯만 만들면 아무도 안 잡는 침묵 no-op 이 된다(DcTrigger.HasDetector 표 참조).");
                    continue;
                }

                // skill-layer-migration unit 8 — **침묵 금지 게이트.**
                // arm 이 철거된 뒤로 `skillId == 0` 은 「아직 arm 이 처리한다」가 아니라
                // 「아무도 처리 안 한다」다. 슬롯은 구워지고 트리거는 발화하고 그 다음에
                // 아무 일도 안 일어나며 **로그조차 없다**(미처리 payload 경고가 arm 과
                // 함께 사라졌다). 실제로 `OnPlace × 충전` 이 그렇게 죽어 있었다.
                // 부착 전용 payload 를 트리거에 매단 저작은 그 자체가 오류다 — 봐주지 않는다.
                if (Wassup.Data.SkillPayloadPolicy.OnlyValidWithNoTrigger(m.payload.kind)
                    && m.trigger.kind != Wassup.Data.DcTriggerKind.None)
                {
                    Debug.LogWarning(
                        $"[BattleBridge] {ownerLabel} mechanic {i}: '{m.payload.kind}' 는 부착 즉시 전용인데 "
                        + $"'{m.trigger.kind}' 에 매달렸다 — 라우팅이 없어 슬롯만 생기고 조용히 죽는다. skipped.");
                    continue;
                }
                if (Wassup.Data.SkillPayloadPolicy.IsSkill(m.payload.kind)
                    && SkillIdForMechanic(m.trigger.kind, m.payload.kind)
                       == Wassup.Skills.SkillRegistry.NotRouted)
                {
                    Debug.LogWarning(
                        $"[BattleBridge] {ownerLabel} mechanic {i}: '{m.trigger.kind} × {m.payload.kind}' "
                        + "조합에 라우팅이 없다 — 슬롯을 만들면 발화하고도 아무 일이 안 일어난다(로그도 없다). "
                        + "트리거 무관 concrete 면 SkillIdForPayload 로 옮기고, 정말 스킬이 아니면 "
                        + "SkillPayloadPolicy 에 이유와 함께 올려라 — skipped.");
                    continue;
                }

                var slot = new DcTriggerSlot
                {
                    // skill-layer-foundation unit 5 — 이전된 payload 만 새 경로로 라우팅한다.
                    // 0 은 「스킬이 아니다」이고, 스킬인데 0 인 조합은 **위 게이트가 이미
                    // 거절했다** — 여기 도달하는 0 은 발동 규칙·공격의 성질뿐이다.
                    skillId = SkillIdForMechanic(m.trigger.kind, m.payload.kind),
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
                    maxHpRef = maxHpRef,
                    duration = math.max(0f, m.payload.duration),
                    // struct default 0 은 유효 index 라 미배선 슬롯이 0번 패턴을 쏘게
                    // 된다 — 명시 -1 초기화가 계약이다(unit 3).
                    patternIndex = -1,
                    // ⚠ **저작 선택자 셋을 여기서 옮긴다**(cc · 스탯 · 스택). 유닛 bake 가
                    // 여태 셋 다 안 옮기고 있었고, 셋 다 **기본값이 진짜처럼 보이는** 함정이다:
                    //   · `CcKind` 0 = 감속 → 「기절」 저작이 조용히 감속이 된다
                    //   · `StatKind` 0 = 공격력 → 「이동속도 감쇠」가 조용히 공격력 오라가 된다
                    //   · `StackKind` 0 = None → 「출혈 도포」가 조용히 아무것도 안 건다
                    // 셋 다 「안 붙는다」가 아니라 「다른 게 붙는다」라 로그도 안 난다.
                    ccKind = MapDcCc(m.payload.ccKind),
                    stackKind = MapDcStack(m.payload.stackKind),
                    // skill-layer-migration unit 2b — 스탯 축을 슬롯에 옮긴다.
                    // ⚠ 안 옮기면 **기본값 0(공격력)** 이 되어, 「이동속도 감쇠」로 저작한
                    // 유닛이 조용히 공격력 오라가 된다. 번역은 `MapDcBuff` 단일 지점을 쓴다
                    // (정의 계층이 `Battle.StatKind` 를 모르게 유지하는 유일한 자리).
                    buffStat = MapDcBuff(m.payload.buffStat, 0f, out var mappedStat, out _)
                        ? mappedStat : Wassup.Battle.Effects.StatKind.DamageMul,
                    // content-5 리뷰 M1 — 카드 bake 는 걸었는데 여기만 빠져 있었다.
                    // struct 기본값 0 은 **유효한 장판 index** 라, OnKill 이 이 경로에
                    // 열리는 날 조용히 «0번 장판» 이 깔린다(DcTriggerSlot 필드 주석의 계약).
                    hazardDataIndex = -1,
                    // boss-jjangssen unit 7 — SelfBlink 착지 슬램(0 = 이동만).
                    slamDamage = math.max(0f, m.payload.slamDamage),
                    slamTileRange = math.max(0, m.payload.slamTileRange),
                    // elite-enemy-tier unit 4 — 저작 도(degree) → 런타임 코사인². **변환은 여기 1회**
                    // 이고 sim 은 삼각함수를 부르지 않는다. 정의역 검증은 아래 AreaBreath 분기.
                    coneHalfAngleDeg = m.payload.coneHalfAngleDeg,
                    coneCosSq = ConeCosSq(m.payload.coneHalfAngleDeg),
                };
                if (m.payload.kind == Wassup.Data.DcPayloadKind.AreaBarrage)
                {
                    // projectile-emission-pattern unit 4 — AreaBarrage arm 은 제거됐다
                    // (융단폭격은 EmitProjectilePattern + Pattern_* asset 으로 이관).
                    // enum 값은 append-only 계약상 남아 있으므로, 옛 authoring 이
                    // 조용한 no-op 으로 죽는 대신 여기서 거절 사유를 남긴다.
                    Debug.LogWarning($"[BattleBridge] {ownerLabel} mechanic {i}: AreaBarrage 는 EmitProjectilePattern 으로 이관됐다(arm 제거) — skipped. 패턴 asset 을 지정하라.");
                    continue;
                }
                // skill-layer-migration unit 8 리뷰 H-1 — **거절 사유가 바뀌었다.**
                //
                // 옛 사유는 「PathHit 후보 풀이 AttackUnitTag 하드코딩이라 적이 쏘면
                // 자기편을 때린다」였고 그건 해소됐다(풀이 양 진영 + 주인의 상대).
                // 그런데 **다른 이유로 여전히 못 쓴다**: 이 공통 슬롯 조립에는
                // `speed`/`hitThreshold`/`projectileDataIndex` 분기가 없어 `speed == 0` 이
                // 되고, `OrbitProjectileSkill` 이 첫 줄에서 그냥 돌아간다.
                //
                // ⚠ 한 번 걷었다가 되살린 가드다. 걷었을 때 생긴 것은 「자기편 타격」이
                // 아니라 **침묵**이었다 — 슬롯이 구워지고 발화하고 아무 일도 안 난다.
                // unit 8 이 없애려던 바로 그 형태라, 배선이 생길 때까지 loud 로 둔다.
                if (m.payload.kind == Wassup.Data.DcPayloadKind.SelfOrbitProjectile)
                {
                    Debug.LogWarning(
                        $"[BattleBridge] {ownerLabel} mechanic {i}: SelfOrbitProjectile 은 유닛 규칙 bake 가 "
                        + "speed/탄 SO 를 안 채워 발동해도 아무 일이 안 일어난다 — skipped. "
                        + "쓰려면 이 조립에 speed·hitThreshold·projectileDataIndex 를 먼저 배선하라.");
                    continue;
                }

                // elite-enemy-tier unit 4 — 화염 브레스 정의역 검증. 판정이 `normalize` 없는 제곱
                // 비교라 부호 가드가 필요하고(없으면 등 뒤에 대칭 콘) 그 가드가 90° 에서 정의역을
                // 자른다. 게다가 cos²θ = cos²(180−θ) 라 **저작 120° 는 조용히 60° 콘으로 동작**한다.
                // 조용한 오동작보다 거절이 낫다.
                else if (m.payload.kind == Wassup.Data.DcPayloadKind.AreaBreath)
                {
                    if (m.payload.coneHalfAngleDeg >= 90f)
                    {
                        Debug.LogError($"[BattleBridge] {ownerLabel} mechanic {i}: AreaBreath 반각({m.payload.coneHalfAngleDeg}°) >= 90 — 제곱 비교의 정의역 밖이라 조용히 (180−각) 콘으로 동작한다. skipped.");
                        continue;
                    }
                    if (m.payload.coneHalfAngleDeg <= 0f)
                        Debug.LogWarning($"[BattleBridge] {ownerLabel} mechanic {i}: AreaBreath 반각이 0 이하 — 정면 한 줄만 맞는다.");
                    if (m.payload.tileRange <= 0)
                        Debug.LogWarning($"[BattleBridge] {ownerLabel} mechanic {i}: AreaBreath 사거리(tileRange)가 0 — 같은 셀만 맞는다.");
                    if (m.payload.magnitude <= 0f)
                        Debug.LogWarning($"[BattleBridge] {ownerLabel} mechanic {i}: AreaBreath 피해가 0 이하 — 발동해도 아무 일도 없다.");
                }
                else if (m.payload.kind == Wassup.Data.DcPayloadKind.EmitProjectilePattern)
                {
                    // projectile-emission-pattern unit 3 — 발사 명세 bake. SO 해석은
                    // 브리지가 유일 seam 이므로 spec 변환과 template 조립이 여기서 끝난다.
                    var pattern = m.payload.pattern;
                    if (pattern == null || pattern.barrel == null)
                    {
                        Debug.LogWarning($"[BattleBridge] {ownerLabel} mechanic {i}: EmitProjectilePattern needs a pattern with a barrel — skipped.");
                        continue;
                    }
                    if (!_em.HasBuffer<Wassup.Battle.Combat.Projectile.Emission.PatternSlot>(entity))
                    {
                        // 사전 스캔과 어긋난 경우(도달 불가) — 조용한 오발사보다 경고.
                        Debug.LogWarning($"[BattleBridge] {ownerLabel} mechanic {i}: pattern buffer missing — skipped.");
                        continue;
                    }
                    // on-place-shuttle-shotgun unit 2 — 방향 패턴의 사거리는 **payload 저작값**이다
                    // (`slot.tileRange × tileSize` → `maxDistance`). 0 이면 arm 이 방향을 채워도
                    // 사거리 0 인 탄이 나가 즉시 착탄 판정에 걸리고, 스윕 길이가 0 이라 넉백 조건도
                    // 거짓이 된다 — **완전 무동작**. 여기서 끊는다(TryBuildPatternSlot 은 payload 를
                    // 모르므로 이 검증만 호출처 몫이다).
                    if (Wassup.Battle.Combat.Projectile.Emission.MovementBinding.Of(
                            ResolveProjectileAxes(pattern.barrel.flightMode).movement)
                            == Wassup.Battle.Combat.Projectile.Emission.BindingClass.Direction
                        && m.payload.tileRange <= 0)
                    {
                        Debug.LogWarning($"[BattleBridge] {ownerLabel} mechanic {i}: 방향 패턴인데 payload tileRange 가 0 이다 — 사거리 0 인 탄이라 발사해도 아무 일도 안 일어난다 — skipped. 사거리를 지정하라.");
                        continue;
                    }
                    if (!TryBuildPatternSlot(pattern, entity, hostIsEnemy,
                                             $"{ownerLabel} mechanic {i}", out var builtSlot))
                        continue;
                    // 사용 직전 재획득(위 주석 참조).
                    var patternSlots = _em.GetBuffer<Wassup.Battle.Combat.Projectile.Emission.PatternSlot>(entity);
                    patternSlots.Add(builtSlot);
                    slot.patternIndex = patternSlots.Length - 1;
                }
                else if ((m.payload.kind == Wassup.Data.DcPayloadKind.SelfBlink ||
                          m.payload.kind == Wassup.Data.DcPayloadKind.AllyMoveSpeedAura ||
                          m.payload.kind == Wassup.Data.DcPayloadKind.SelfTileAoe ||
                          // ultimate-leap unit 0 — 착지 슬램도 ProjectileSpawnRequest 로 나가므로
                          // SelfTileAoe 와 같은 이유로 dataIndex 가 필수다(아래 loud 거절 참조).
                          m.payload.kind == Wassup.Data.DcPayloadKind.UltimateLeap ||
                          // boss-mamemo unit 1 — 자장가 펄스 연출(whip 과 같은 hit-VFX 경로).
                          // 여기선 **선택**이다: 없으면 연출만 없고 수면은 그대로 나간다.
                          m.payload.kind == Wassup.Data.DcPayloadKind.AreaSleep) &&
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
                else if (m.payload.kind == Wassup.Data.DcPayloadKind.AreaDot)
                {
                    // 빔 프리팹은 **선택**이다 — 없으면 연출만 없고 도트는 그대로 나간다.
                    slot.projectileDataIndex = GetOrCreateSkillVfxIndex(m.payload.auraPrefab);
                    // 틱 간격. 0 이면 `magnitude` 가 DPS 로 해석된다(저작의 뜻이다).
                    slot.speed = math.max(0f, m.payload.tickIntervalSec);
                }
                else if ((m.payload.kind == Wassup.Data.DcPayloadKind.AllyStatAura ||
                          m.payload.kind == Wassup.Data.DcPayloadKind.OpponentStatAura) &&
                         m.payload.buffStat == Wassup.Data.CardBuffKind.EffectiveHealth)
                {
                    // ⚠ `EffectiveHealth` 는 번역 산식이 **역수**(1/(1+p/100))다. 스탯 오라
                    // concrete 는 퍼센트→배율을 (1+p/100) 하나로만 하므로 이 축만 값이 갈린다.
                    // 조용히 틀린 배율을 주느니 거절한다 — 필요해지면 concrete 에 그 산식을
                    // 명시적으로 열어야 한다(그때 이 거절을 지운다).
                    Debug.LogWarning($"[BattleBridge] {ownerLabel} mechanic {i}: 스탯 오라에 EffectiveHealth 는 아직 배선되지 않았다(번역 산식이 역수) — skipped.");
                    continue;
                }
                else if (m.payload.kind == Wassup.Data.DcPayloadKind.SelfTileAoe)
                {
                    // boss-jjangssen unit 2 — 위 분기에 안 걸렸다 = projectile 미지정.
                    // 조용히 inert 가 되면 "왜 폭발이 없는지" 를 영영 알 수 없으므로 loud 하게
                    // 거절한다(bake 의 기존 loud 거절 선례와 동일 표현). defender 슬롯 경로도
                    // 같은 규칙을 쓴다.
                    Debug.LogWarning($"[BattleBridge] {ownerLabel} mechanic {i}: SelfTileAoe 에 ProjectileData(AOE view) 가 없어 폭발 요청이 드롭된다 — skipped. payload.projectile 을 지정하라.");
                    continue;
                }
                else if (m.payload.kind == Wassup.Data.DcPayloadKind.GrantShield &&
                         m.payload.magnitude <= 0f)
                {
                    // boss-mamemo unit 2 — 실드량 0 은 매 발동 조용한 no-op 이다
                    // (ShieldMath.Merge 가 amount<=0 을 그냥 return 한다). loud 거절.
                    Debug.LogWarning($"[BattleBridge] {ownerLabel} mechanic {i}: GrantShield 에 magnitude(실드량 >0) 가 없어 매 발동 no-op 이 된다 — skipped.");
                    continue;
                }
                else if (m.payload.kind == Wassup.Data.DcPayloadKind.GrantShield &&
                         ((m.trigger.kind == Wassup.Data.DcTriggerKind.HealthThreshold && m.payload.tileRange > 0) ||
                          (m.trigger.kind == Wassup.Data.DcTriggerKind.PeriodicTimer && m.payload.tileRange <= 0) ||
                          // skill-layer-migration(투트랙 리뷰 M2) — **경고문이 이미 이 조합을
                          // 거절한다고 말하는데 조건이 빠져 있었다.** legacy arm 은 tileRange<=0
                          // 에서 조용한 no-op 이었지만, concrete 는 그것을 「자기 실드」로 읽는다
                          // (경계 자기 실드와 같은 규약). 그래서 이전이 미검증 조합 하나의 의미를
                          // 열어버린다 — 저작 단계에서 loud 하게 막는 것이 맞다.
                          (m.trigger.kind == Wassup.Data.DcTriggerKind.OnPlace && m.payload.tileRange <= 0)))
                {
                    // boss-mamemo unit 3 — **미배선 조합 거절.** 실드는 두 능력을 겸하지만 배선은
                    // 트리거별로 갈라져 있다: 경계 arm = 자기(tileRange 0) · 주기 arm = 반경 확산
                    // (tileRange>0). 반대로 저작하면 슬롯은 생기는데 아무 arm 도 안 잡아 **조용한
                    // no-op** 이 된다. 미사용 라이브 경로를 만들지 않는 것이 dreamcatcher-trigger-gates
                    // 계약("v1 배선 조합 외는 bake loud 거절")의 선례다. 새 조합은 그걸 쓰는 능력이
                    // 생길 때 배선·테스트와 함께 연다.
                    //
                    // on-place-shuttle-shotgun unit 0 — **`OnPlace × tileRange>0` 이 그렇게 열린
                    // 세 번째 조합이다**(실드셔틀 배치 보호막). arm 은 트리거가 아니라 payload 로
                    // 분기하므로 코드 추가 없이 반경 확산을 그대로 탄다.
                    // ⚠ 이 거절을 «화이트리스트» 로 조이지 말 것 — 위 두 조합만 남기면 배치
                    // 보호막이 **조용히** 죽는다. 현재 형태(블랙리스트 2조합)를 유지한다.
                    Debug.LogWarning($"[BattleBridge] {ownerLabel} mechanic {i}: GrantShield 미배선 조합 — HealthThreshold 는 tileRange 0(자기), PeriodicTimer/OnPlace 는 tileRange>0(주변 아군)만 배선돼 있다 (현재 trigger={m.trigger.kind}, tileRange={m.payload.tileRange}) — skipped.");
                    continue;
                }
                else if (m.payload.kind == Wassup.Data.DcPayloadKind.AreaTaunt)
                {
                    // on-place-skill-rework unit 4 — 범위 도발 저작 검증. arm 은 [BurstCompile]
                    // 이라 로그를 못 내므로 여기서 loud 하게 끊는다(기존 bake 거절 선례와 동형).
                    if (m.payload.duration <= 0f || m.payload.tileRange <= 0)
                    {
                        Debug.LogWarning($"[BattleBridge] {ownerLabel} mechanic {i}: AreaTaunt 에 duration(도발 초 >0) 또는 tileRange(반경 >0) 가 없어 매 발동 no-op 이 된다 — skipped.");
                        continue;
                    }
                    // 어그로는 `AggroCapacity` 보유(=가디언)에서만 성립한다. 비-가디언에 붙이면
                    // 드레인이 조용히 버리므로 "왜 아무도 안 끌려오는지" 를 영영 알 수 없다.
                    if (!_em.HasComponent<Wassup.Battle.Effects.AggroCapacity>(entity))
                    {
                        Debug.LogWarning($"[BattleBridge] {ownerLabel} mechanic {i}: AreaTaunt 는 가디언 전용이다(aggroCapacity > 0 이어야 AggroCapacity 가 붙는다) — skipped.");
                        continue;
                    }
                }
                else if (m.payload.kind == Wassup.Data.DcPayloadKind.AreaSleep &&
                         (m.payload.magnitude < 1f || m.payload.duration <= 0f))
                {
                    // boss-mamemo unit 1 — 자장가는 magnitude(인원)·duration(초) 둘 다 필요하다.
                    // 하나라도 비면 arm 이 매 주기 조용히 no-op 이 되어 "왜 아무도 안 자는지"를
                    // 영영 알 수 없다. SelfTileAoe 의 loud 거절과 같은 이유로 여기서 끊는다.
                    // (projectile 은 선택이다 — 없으면 연출만 없고 수면은 나간다.)
                    Debug.LogWarning($"[BattleBridge] {ownerLabel} mechanic {i}: AreaSleep 에 magnitude(재울 인원 >=1) 또는 duration(수면 초 >0) 이 없어 매 주기 no-op 이 된다 — skipped.");
                    continue;
                }
                else if (m.payload.kind == Wassup.Data.DcPayloadKind.AreaSleep &&
                         m.payload.tileRange <= 0)
                {
                    // boss-mamemo 리뷰 M6 — 반경 0 은 host 셀만 보므로 사실상 아무도 못 잰다
                    // (방어유닛은 보스와 같은 칸에 서지 않는다). 위 가드가 막겠다고 선언한
                    // "왜 아무도 안 자는지 모른다" 가 여기서도 재현되므로 같은 급으로 끊는다.
                    //
                    // ⚠ 한때 이 가드는 «tileRange <= 사거리» 였다(도넛 설계 시절). 도넛은
                    // **실측으로 폐기**됐다 — 붙는 보스는 사거리 안에서 대부분의 시간을 보내
                    // 도넛 후보가 마르고 조우당 1회밖에 안 터졌다. 지금은 전 범위 + rank 제외다.
                    Debug.LogWarning($"[BattleBridge] {ownerLabel} mechanic {i}: AreaSleep 의 tileRange 가 0 이라 host 셀만 본다 — skipped.");
                    continue;
                }
                else if (m.payload.kind == Wassup.Data.DcPayloadKind.UltimateLeap)
                {
                    // ultimate-leap unit 0 — SelfTileAoe 와 같은 함정: 착지 슬램이
                    // ProjectileSpawnRequest 하나로 표현되고 드레인이 dataIndex<0 이면 요청을
                    // 통째로 버린다 → **연출뿐 아니라 피해까지 사라진다.** 조용히 "이탈만 하고
                    // 아무 일도 안 일어나는" 궁극기가 되므로 loud 하게 거절한다.
                    Debug.LogWarning($"[BattleBridge] {ownerLabel} mechanic {i}: UltimateLeap 에 ProjectileData(착지 슬램 view) 가 없어 슬램 요청이 드롭된다 — skipped. payload.projectile 을 지정하라.");
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
                if (m.payload.kind == Wassup.Data.DcPayloadKind.GrantShield && m.payload.duration > 0f)
                {
                    // boss-mamemo unit 2 — 실드에는 시간 만료가 없다(ShieldMath 에 TTL 축 없음).
                    // duration 을 적어두면 "몇 초 뒤 사라진다" 고 읽히지만 런타임은 무시한다 —
                    // 조용히 다르게 도는 대신 저작 시점에 말해준다. skip 하지는 않는다.
                    Debug.LogWarning($"[BattleBridge] {ownerLabel} mechanic {i}: GrantShield 의 duration({m.payload.duration}) 은 무시된다 — 이 엔진의 실드는 시간이 아니라 피해로만 사라진다.");
                }
                if (m.payload.kind == Wassup.Data.DcPayloadKind.AreaSleep &&
                    m.trigger.kind == Wassup.Data.DcTriggerKind.PeriodicTimer &&
                    m.payload.duration >= m.trigger.periodSeconds)
                {
                    // boss-mamemo 리뷰 M7 — **whip 오라와 정반대 방향의 저작 함정.**
                    // 버프는 duration <= period 면 펄스 사이에 끊겨 점멸하지만(아래 경고),
                    // CC 는 duration >= period 면 매 주기 같은 대상이 갱신돼 **끊김이 없다** —
                    // "잠시 재운다" 가 "생존 내내 고착" 이 된다. 깨우는 유일한 수단이 적 평타라
                    // 단일 대상 보스는 재운 인원을 사실상 회수하지 못한다.
                    // 의도일 수 있으므로 skip 하지 않고 경고만 한다.
                    Debug.LogWarning($"[BattleBridge] {ownerLabel} mechanic {i}: AreaSleep duration({m.payload.duration}) >= periodSeconds({m.trigger.periodSeconds}) — 수면이 끊기지 않아 대상이 생존 내내 고착합니다.");
                }
                if (m.payload.kind == Wassup.Data.DcPayloadKind.AllyMoveSpeedAura &&
                    m.payload.duration <= m.trigger.periodSeconds)
                {
                    // nightmare-whip-aura unit 1 — authoring 계약: duration >
                    // periodSeconds (merge-refresh 유지). 위반은 펄스 사이 버프
                    // 만료(점멸) — 경고만, skip 하지 않는다(테스트 자유 유지).
                    Debug.LogWarning($"[BattleBridge] {ownerLabel} mechanic {i}: AllyMoveSpeedAura duration({m.payload.duration}) <= periodSeconds({m.trigger.periodSeconds}) — 버프가 펄스 사이에 만료(점멸)합니다.");
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
        // dreamcatcher-content-5 unit 5 — 발사 명세 → PatternSlot 값 조립 + 저작 유효성.
        // 적/유닛 bake 와 **카드 bake 가 공유**한다(전에는 적 경로에만 있었다). 버퍼 취급은
        // 호출자가 한다 — 적 경로는 사전 스캔과의 불일치를 거절하고, 카드 경로는 살아 있는
        // 엔티티에 붙이므로 add-or-get 이 필요해 요구가 서로 다르다.
        private bool TryBuildPatternSlot(
            Wassup.Data.ProjectilePatternData pattern, Entity host, bool hostIsEnemy, string label,
            out Wassup.Battle.Combat.Projectile.Emission.PatternSlot built)
        {
            built = default;
            int barrelIndex = GetOrCreateProjectileDataIndex(pattern.barrel);
            // SkyFall 패턴은 낙하 예고가 곧 그 스킬의 정체다 — 0 이면 텔레그래프
            // 없이 즉착탄하므로 조용히 넘기지 않는다(구 arm 은 authoring 이 duration 을
            // 요구했다).
            if (pattern.barrel.flightMode == Wassup.Data.ProjectileFlightMode.SkyFall
                && pattern.telegraphSec <= 0f)
            {
                Debug.LogWarning($"[BattleBridge] {label}: SkyFall 패턴의 telegraphSec 가 0 — 예고 없이 즉착탄합니다.");
            }
            // on-place-skill-rework 리뷰 반영 — fan-out 은 후보 **전원**에게 1발씩
            // 나가므로 범위 제한이 없으면 **맵 전체 적 수만큼** 캐리어가 한 shot 에
            // 생긴다(상한도 없다). 조용한 폭주를 bake 에서 끊는 이 파일의 관례대로 거절.
            if (pattern.fanOutToAllCandidates && pattern.scopeTileRange <= 0)
            {
                Debug.LogWarning($"[BattleBridge] {label}: fanOutToAllCandidates 인데 scopeTileRange 가 0 — 맵 전체 적에게 동시 발사가 된다 — skipped. 반경을 지정하라.");
                return false;
            }
            // on-place-skill-rework unit 11 — fan-out 은 **적 조준 궤적 전용**이다.
            // 셀 조준 궤적은 발사 시점의 칸에 위치를 고정하고 다시 조준하지 않는다(예고의
            // 사양). 거기에 «적 전원에게 1발씩» 을 얹으면 한 탄에 조준이 둘이 되어 예고
            // 시간만큼 어긋난다 — 실측 예고 0.40s × 적 속도 2.00 = 0.80타일 > 칸 유지 폭
            // 0.50타일 이라 피해가 통째로 0 이 됐다(unit 8 회귀). 칸 폭격을 원하면
            // fan-out 을 끄고 단일 선택으로 쏘고, 적 단위 폭격은 `SkyFallOnTarget` 을 쓴다.
            var fanBinding = Wassup.Battle.Combat.Projectile.Emission.MovementBinding.Of(
                ResolveProjectileAxes(pattern.barrel.flightMode).movement);
            if (pattern.fanOutToAllCandidates
                && fanBinding != Wassup.Battle.Combat.Projectile.Emission.BindingClass.Entity)
            {
                Debug.LogWarning(
                    $"[BattleBridge] {label}: fanOutToAllCandidates 인데 탄의 조준이 {fanBinding} 다 — " +
                    "적 단위 fan-out 은 Entity 조준 궤적만 쓸 수 있다(SkyFallOnTarget 등) — skipped.");
                return false;
            }
            // (BezierHoming 재조준 봉인은 authoring 표면이 없어 경고가 불필요하다 —
            //  ProjectileData 에 재조준 필드 자체가 없다. 그 필드를 여는 후속 작업이
            //  재조준 개통과 한 묶음이라는 점은 README 후속 후보에 적혀 있다.)
            // on-place-shuttle-shotgun unit 2 — **방향 바인딩 패턴의 조용한 no-op 3종.**
            // 이 함수는 규칙 경로(유닛 능력 · 드림캐쳐 카드) 전용이다 — 평타 다연발은
            // `BakeDefenderDirectionalPattern` 이 따로 굽고 `AttackSystem` 이 발사한다.
            // 그래서 여기 경고는 평타 저작을 건드리지 않는다.
            if (fanBinding == Wassup.Battle.Combat.Projectile.Emission.BindingClass.Direction)
            {
                if (pattern.damage <= 0f)
                    Debug.LogWarning($"[BattleBridge] {label}: 방향 패턴인데 damage 가 0 이하다 — 규칙 경로는 패턴 SO 의 damage 를 그대로 쓴다(평타처럼 output 이 덮지 않는다). 넉백만 노린 저작이 아니라면 값을 넣어라.");
                if (pattern.randomizeShotsPerTrigger)
                    Debug.LogWarning($"[BattleBridge] {label}: randomizeShotsPerTrigger 는 **규칙 경로에서 아무 일도 하지 않는다**(랜덤화는 AttackSystem 평타 경로에만 있다). 발마다 다르게 퍼뜨리려면 shots 의 directionT 를 저작으로 벌려라 — 안 그러면 전탄이 같은 방향으로 겹친다.");
            }
            if (!pattern.TryToSpec(barrelIndex, out var patternSpec))
            {
                int shotCount = pattern.shots?.Length ?? 0;
                Debug.LogWarning(
                    $"[BattleBridge] {label}: " +
                    $"invalid projectile shot sequence/binding contract (shots={shotCount}, " +
                    $"capacity={Wassup.Data.ProjectilePatternData.MaxShotCount}, " +
                    $"angles={pattern.minAngleDeg}..{pattern.maxAngleDeg}, " +
                    $"selection={pattern.selection}, flight={pattern.barrel.flightMode}) — skipped.");
                return false;
            }
            built = new Wassup.Battle.Combat.Projectile.Emission.PatternSlot
            {
                spec = patternSpec,
                template = BuildPatternTemplate(pattern, barrelIndex, host, hostIsEnemy: hostIsEnemy),
                fireCountBase = 0,
            };
            return true;
        }

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
                // bomb-barrel-on-place unit 3 — SpawnBlocker 탄이 세울 설치물. SO→index 해석은
                // 레지스트리를 가진 여기서만 한다(sim 은 index 만 나른다).
                blockerDataIndex = barrel.spawnBlocker != null
                    ? RegisterBlockingHazardSO(barrel.spawnBlocker)
                    : -1,
                owner = owner,
                // 진영은 host 에서 도출한다(계약 7) — 패턴 SO 에 faction 필드 없음.
                targetFaction = hostIsEnemy
                    ? ProjectileTargetFaction.Defender
                    : ProjectileTargetFaction.Enemy,
                // on-place-skill-rework 리뷰 반영 — **통행 층도 host 사양을 따른다.**
                // 안 실으면 0 = 무제한이라(`PlacementLayers.CanTarget` 이 0 을 무조건 통과)
                // **지상만 때리는 유닛의 패턴이 비행 적을 때린다.** 캐논의 배치 폭격이 정확히
                // 그랬다 — 레거시 `MeleeBurst` 는 `CanDefenderTargetMover` 로 비행을 뺐는데
                // 규칙 경로로 옮기면서 그 게이트를 잃었다(같은 spec 의 도발은 명시적으로 막았다).
                // 방어유닛 발 투사체가 전부 `AttackState.targetTraversalLayers` 를 싣는 것과
                // 같은 규약이며, 궤도 화염구 arm 의 선례와도 동형이다.
                targetTraversalLayers =
                    _em.HasComponent<Wassup.Battle.Combat.AttackState>(owner)
                        ? _em.GetComponentData<Wassup.Battle.Combat.AttackState>(owner).targetTraversalLayers
                        : (byte)0,
            };
        }

        // elite-enemy-tier unit 5 — 레인 스폰은 **얇은 래퍼**다. 엔티티 조립 본문은 아래
        // CreateEnemyEntity 가 갖고, 분열(DrainEnemyKilledEvents)이 그 본문을 재사용한다.
        //
        // ★가드 두 개(spawns 비었음 · laneIndex 범위 폴백)는 **래퍼에만** 둔다. 본문으로
        // 내리면 스폰 지점이 없는 맵에서 분열이 조용히 막힌다 — 「위치 파라미터를 옵셔널로
        // 하나 추가」안을 기각한 이유가 이것이다(두 경로의 가드가 한 몸이 된다).
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

            // waypoint-routing unit 7 — 큐잉 때 상세 펼침이 확정한 실제 lane을 그대로 소비한다.
            int spawnIndex = pending.laneIndex;
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

            // waypoint-routing unit 9 — 레인 기본 경로는 **래퍼가 결정한다.** 위 가드가 클램프한
            // spawnIndex 를 쓰므로 본문은 가드를 다시 갖지 않는다(★ 규약 유지). 본문은 plain int
            // 하나만 받고, 분열 경로는 레인이 없으므로 기본값 -1 = 현행(적 SO 지정만 본다).
            CreateEnemyEntity(entry.unitType, spawnWorldPos,
                _generatedMap.RouteForSpawn(spawnIndex), pending.pathIndex);
        }

        // 적 엔티티 조립의 단일 지점. 호출처 **3곳** — 레인 스폰(위) · 분열(DrainEnemyKilledEvents) ·
        // 보너스 웨이브(BattleBridge.BonusWave.cs SpawnBonusUnit, bonus-wave-pull unit 4).
        // CreatePatrolEntity 처럼 병렬 복제하지 않은 이유: 분열 자식은 적의 **표준 세트 전부**
        // (Health·FactionTag·버퍼 6종·PathFollowState·AttackState·behavior·뷰 등록)가
        // 필요해서, 복제하면 다음에 적 스폰에 뭔가 추가될 때 한쪽만 갱신된다.
        // waypoint-routing unit 9 — laneDefaultPathIndex 는 «이 레인에서 나온 적의 기본 경로».
        // 래퍼가 결정해 넘기고, 분열 호출처는 레인이 없으므로 기본값 -1(현행 = 적 SO 지정만).
        // duel-route-tours unit 1 — conceptPathIndex 는 «이번 편성이 지정한 경로»(컨셉 슬롯).
        // 분열 자식은 웨이브 편성 밖이라 역시 -1 이다 — 부모의 경로를 물려주지 않는다.
        // 물려주면 분열 자식이 부모의 남은 경유점이 아니라 **처음부터** 그 투어를 다시 돈다.
        private Entity CreateEnemyEntity(Wassup.Data.AttackUnitData unitType, Vector3 spawnWorldPos,
            int laneDefaultPathIndex = -1, int conceptPathIndex = -1)
        {
            if (unitType.visualMaterial == null)
            {
                Debug.LogWarning("[BattleBridge] visualMaterial null — entity will not render.");
                return Entity.Null;
            }

            var entity = _em.CreateEntity();
            AttachSimEntityId(entity);
#if UNITY_EDITOR
            _em.SetName(entity, $"Enemy_{unitType.displayName}");
#endif
            _em.AddComponentData(entity, LocalTransform.FromPositionRotationScale(spawnWorldPos, quaternion.identity, CharacterVisualScale));

            _em.AddComponent<AttackUnitTag>(entity);
            // dreamcatcher-orb-dock unit 6 — 스폰 시 적 데이터 등록(킬 각성 피규어 스킨 소스).
            _enemyTypeByEntity[entity] = unitType;
            _em.AddComponentData(entity, new Health { value = unitType.health, max = unitType.health });
            _em.AddComponentData(entity, new FactionTag { value = Faction.EnemyUnit });
            // dreamcatcher-awakening-hand unit 1 — bake the death grant so
            // DamageApplicationSystem can stamp it into EnemyKilledEvent.
            // Unconditional attach (0 allowed) keeps the lookup branch-free.
            _em.AddComponentData(entity, new AwakeningReward
            {
                value = Mathf.Max(0, unitType.awakeningReward),
            });
            // three-minute-kill-race unit 1 — `KillScore` bake 은 사라졌다(1킬 = 1점).
            // 위 AwakeningReward 는 남는다 — 각성치는 여전히 적별로 다르다.
            // Pre-attach empty buffers so downstream systems never need structural AddBuffer on hot paths.
            _em.AddBuffer<IncomingDamage>(entity);
            _em.AddBuffer<CcEffect>(entity);
            _em.AddBuffer<DotEffect>(entity); // dot-effect-extraction unit 0
            // boss-mamemo unit 2 — 적도 실드를 받을 수 있다(마메모의 꿈의 장막·악몽의 가호).
            // **쌍으로** 붙인다: IncomingShield 드레인이 ShieldSlot 존재로 게이팅돼 있어
            // (DamageApplicationSystem) 한쪽만 붙이면 부여가 영영 드레인되지 않고 버퍼가
            // 무한 성장한다. 보스만이 아니라 **적 전원**인 이유는 악몽의 가호의 수혜자가
            // 호위 잡몹이기 때문 — 조건부 부착은 "누가 받을 수 있나" 를 스폰 시점에 못 박아
            // arm 의 대상 선정을 왜곡한다. 흡수·오버헤드 게이지는 이미 진영 중립이다.
            // (거점은 이 경로를 안 타므로 battle-structures 계약 8 은 그대로 지켜진다.)
            _em.AddBuffer<Wassup.Battle.Units.ShieldSlot>(entity);
            _em.AddBuffer<Wassup.Battle.Units.IncomingShield>(entity);

            // bonus-wave-pull unit 0 — 사냥 성질 bake. ★**여기서 붙인다** —
            // BakeNightmareMechanics 안(BossTag 옆)에 두면 그 메서드가 nightmareMechanics 가
            // 비었을 때 조기 반환하므로 **메커닉 없는 사냥꾼에게 태그가 안 붙는다**. 보스는
            // 무회귀이고 테스트도 전부 초록인 채 사냥만 조용히 죽는다.
            if (unitType.tier == Wassup.Data.EnemyTier.Boss || unitType.huntsDefenders)
                _em.AddComponent<Wassup.Battle.Combat.DefenderHunterTag>(entity);

            // nightmare-catcher unit 5 — 보스 분기 베이크. nightmareMechanics 없는
            // 일반 적은 이 호출이 즉시 return(무변경).
            BakeNightmareMechanics(entity, unitType);

            // enemy-behavior-components Unit 2 — attackMethod decides attack components.
            // Defensive (Critic C1): Melee/Projectile with empty outputs → walk-only
            // (no AttackState), never a damage-0 attacker. All hit effects come
            // through outputs[] (AttackOutputElement).
            var attackMethod = unitType.attackMethod;
            bool hasAttackOutputs = unitType.outputs != null && unitType.outputs.Length > 0;
            bool wantsAttack = attackMethod != Wassup.Data.EnemyAttackMethod.None;
            if (wantsAttack && !hasAttackOutputs)
            {
                Debug.LogWarning($"[BattleBridge] {unitType.displayName}: attackMethod={attackMethod} but outputs empty — baked as walk-only.");
                wantsAttack = false;
            }
            // battle-structures unit 1 — 저작 타겟 마스크를 한 번 푼다. 아래 두 곳이 **같은
            // 값**을 써야 한다: AttackState.targetMask(런타임 초기값)와
            // EnemyTargetFilter.factionMask(저작 의도, 불변). 갈리면 도발 게이트(unit 2)가
            // 실제 조준과 다른 의도를 읽는다.
            // 미저작(None=0)은 레거시 마스크로 폴백 — 저작자가 인스펙터에서 마스크를 비웠을
            // 때 그 적이 조용히 무장 해제되는 것을 막는다.
            int authoredTargetMask = Wassup.Battle.Combat.EnemyTargetDefaults.Resolve(
                (int)unitType.targetFactions);

            if (wantsAttack)
            {
                _em.AddComponentData(entity, new AttackState
                {
                    range = unitType.attackRange,
                    cooldownDuration = unitType.attackCooldown,
                    cooldownRemaining = 0f,
                    attackTargetCount = Mathf.Max(1, unitType.attackTargetCount),
                    targetMask = authoredTargetMask,
                    hitDelaySec = unitType.hitDelaySec,
                });
                var outputBuf = _em.AddBuffer<Wassup.Battle.Combat.AttackOutputElement>(entity);
                foreach (var output in unitType.outputs)
                    outputBuf.Add(new Wassup.Battle.Combat.AttackOutputElement { value = output });

                if (attackMethod == Wassup.Data.EnemyAttackMethod.Projectile && unitType.projectile != null)
                    BakeProjectileRef(entity, unitType.projectile);   // 리뷰 A-M3 — 단일 베이크
            }

            // aggro-targeting Unit 1 — taunt-attack profile for enemies with no normal outputs.
            // ⚠ heart-stress-axis unit 7 이후 **Runner·Swift 는 더 이상 여기 해당하지 않는다** —
            // 둘은 진짜 공격(`outputs` 피해 10)을 갖고 `aggroAttackDamage: 0` 이라 이 프로필이
            // 아예 안 붙는다. 도발은 마스크의 `DefenderUnit` 비트로 **일반 적과 같은 경로**
            // (AttackSystem 의 aggro sticky)를 탄다. 지금 이 분기를 타는 라이브 적은 없다.
            if (unitType.aggroAttackDamage > 0f)
                _em.AddComponentData(entity, new Wassup.Battle.Combat.AggroAttackProfile
                {
                    damage = unitType.aggroAttackDamage,
                    cooldown = unitType.aggroAttackCooldown,
                    range = unitType.aggroAttackRange,
                });

            // battle-structures unit 0 — goal-stability 의 walk-only 골 공격 grant 를 제거했다.
            // 게이트가 _hasStabilityGoals(= SpawnGoalEntities 산물)라 전 맵 M=0 에서 한 번도
            // 발화하지 않았다. 그때의 우려는 «Runner·Swift 가 AttackState 를 얻으면 canSiege=true
            // 가 되어 골에서 안 죽고 «필드에 적 0기» 판정을 막는다» 였다.
            //
            // ⚠ heart-stress-axis unit 7 이 **그 우려를 해소한 뒤 실제로 공격을 줬다** — 이 문단을
            // 「금지」로 읽고 unit 7 을 되돌리지 말 것. 해소 방법은 `UnitLifecycleSystem` 의
            // `canSiege` 정밀화다: 「AttackState 보유」 → 「**마스크에 DefenderCore 포함**」.
            // 돌격형 마스크(21)에는 마음이 없어 공격이 있어도 canSiege=false = 도달 시 산화한다.

            // enemy-behavior-components Unit 2 — behavior + filter from SO (enemyClass
            // hardcode removed). EnemyBehavior drives targeting/aim; FocusTarget is
            // pre-attached for FocusUntilDead (AttackSystem only writes its value).
            _em.AddComponentData(entity, new Wassup.Battle.Combat.EnemyBehavior
            {
                targetMode = unitType.targetMode,
                // enemy-ai-fsm — SO 의 engageMovement 직접 bake(값 세팅은 unit 4 SO 마이그레이션).
                engageMovement = unitType.engageMovement,
            });
            // enemy-ai-fsm Unit 0 — FSM 상태 초기값. EnemyAiStateSystem(unit 1)이 매 틱 갱신.
            _em.AddComponentData(entity, new Wassup.Battle.Combat.EnemyAiState
            {
                value = Wassup.Battle.Combat.AiState.Marching,
            });
            // target-persistence unit 3 — 공격 가능한 **전 적**에게 부착한다(구 FocusUntilDead 한정).
            // Nearest 4종(Tanker·Debuffer·보스 2종)도 락을 받는다 — D4.
            if (unitType.targetMode != Wassup.Data.EnemyTargetMode.None)
                _em.AddComponentData(entity, new Wassup.Battle.Combat.FocusTarget { current = Entity.Null });

            int priorityClass = unitType.targetPriorityClass == Wassup.Data.DefenderClass.None
                ? -1
                : (int)unitType.targetPriorityClass;
            // 이 부착은 wantsAttack 게이트 **밖**이다 — 무기 없는 적(러너·스위프트)도 저작
            // 의도를 갖는다. 계약 2 의 도발 게이트가 이것을 읽는다.
            _em.AddComponentData(entity, new Wassup.Battle.Combat.EnemyTargetFilter
            {
                classMask = (int)unitType.targetClassMask,
                priorityClass = priorityClass,
                factionMask = authoredTargetMask,
            });

            _em.AddComponentData(entity, new PathFollowState
            {
                speed = unitType.moveSpeed,
                // traversal-layers unit 2 — 위와 같은 규약(스폰 시 1회 주입, Path 폴백).
                traversalLayers = (byte)unitType.EffectiveTraversalLayers,
                // continuous-agent-movement unit 3 — 반지름은 월드 단위로 넘긴다(sim 은 타일을 모른다).
                radius = agentRadiusTiles * tileSize,
            });

            // waypoint-routing unit 3 — 저작 opt-in. 유효한 경로만 Movement 상태를 붙인다.
            // 실패는 골 직행으로 안전 폴백하되, 사람에게 보이는 경고는 스폰 1회만 남긴다.
            // unit 9 — 경로 선택 축이 둘이 됐다(계약 10): 적 SO 지정 = 종의 정체성,
            // 레인 기본 = 맵의 성질. 겹치면 좁은 쪽(개체)이 이긴다. 우선순위는 순수 함수가
            // 소유해 EditMode 로 고정한다 — 여기서 삼항으로 풀면 계약이 코드에만 남는다.
            // duel-route-tours unit 1 — 그 사이에 웨이브 컨셉(이번 편성의 성격)이 들어왔다.
            int waypointPathIndex = WaypointRouting.ResolvePathIndex(
                unitType.waypointPathIndex, conceptPathIndex, laneDefaultPathIndex);
            if (waypointPathIndex >= 0)
            {
                bool validPath = waypointPathIndex < _generatedMap.WaypointPathCount;
                if (validPath)
                {
                    _em.AddComponentData(entity, new WaypointFollow
                    {
                        pathIndex = waypointPathIndex,
                        index = 0,
                    });
                }
                else
                {
                    // 어느 축에서 온 값인지 같이 남긴다 — 저작자가 SO 와 맵 중 어디를 고칠지
                    // 알아야 한다(계약 9 — 슬롯/경로 폴백은 조용하면 안 된다).
                    string source = unitType.waypointPathIndex >= 0 ? "unit SO" : "map lane default";
                    Debug.LogWarning(
                        $"[BattleBridge] {unitType.displayName}: waypointPathIndex={waypointPathIndex} ({source}) is invalid "
                        + $"for map paths={_generatedMap.WaypointPathCount} — using goal route.", this);
                }
            }

            EnsureMonoViewPools();
            bool spineSpawned = spineUnitPool != null &&
                                spineUnitPool.TrySpawn(unitType, null, entity, spawnWorldPos, "SpineEnemy", out _);
            if (!spineSpawned)
            {
                var mesh = unitType.visualMesh != null
                    ? unitType.visualMesh
                    : Resources.GetBuiltinResource<Mesh>("Quad.fbx");
                enemyViewPool.TrySpawn(
                    unitType.displayName,
                    entity,
                    spawnWorldPos,
                    mesh,
                    CreateAttackUnitRuntimeMaterial(unitType.visualMaterial),
                    CharacterVisualScale,
                    1, // unit 9 — 적/보스는 sim 이 1칸 점유(AttackUnitData.FootprintWidthCells 와 같은 참값)
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

            return entity;
        }


        // elite-enemy-tier unit 4 — 저작 반각(도) → 런타임 코사인². 정의역 밖 값은 위 bake 분기가
        // 이미 거절했으므로 여기서는 변환만 한다(0 이하는 cos²=1 = 정면 한 줄로 자연 귀결).
        private static float ConeCosSq(float halfAngleDeg)
        {
            float c = Mathf.Cos(Mathf.Deg2Rad * Mathf.Max(0f, halfAngleDeg));
            return c * c;
        }

        // elite-enemy-tier unit 5 — 분열 상한. 밸런스 값이 아니라 **저작 사고 방어선**이다
        // (magnitude 에 오타로 100 이 들어가면 한 마리 죽음이 판을 끝낸다).
        private const int MaxSplitChildren = 8;

        // 죽은 적의 SO 가 `OnDeath × SplitOnDeath` 를 선언했으면 그 자리에 자식을 스폰한다.
        // 호출처 1곳(DrainEnemyKilledEvents) — 유출 경로는 이 이벤트를 안 타므로 «체력 소진
        // 시에만 분열» 이 코드 추가 없이 성립한다.
        private void SpawnSplitChildren(Wassup.Data.AttackUnitData killedType, Vector3 deathWorldPos)
        {
            // ★null 은 저작 실수가 아니라 **등록부 버그**다 — 모든 적이 CreateEnemyEntity 에서
            // _enemyTypeByEntity 에 등록되므로, 킬 드레인이 SO 를 못 찾았다면 등록/제거 순서가
            // 깨진 것이다. 조용히 넘기면 «분열이 안 되는데 이유가 안 보이는» 상태가 된다.
            if (killedType == null)
            {
                Debug.LogWarning("[BattleBridge] 분열 검사: 죽은 적의 AttackUnitData 를 " +
                                 "_enemyTypeByEntity 에서 못 찾았다 — 등록부 누락(분열 유닛이면 분열이 안 된다).");
                return;
            }

            var mechanics = killedType.nightmareMechanics;
            if (mechanics == null || mechanics.Length == 0) return;

            for (int i = 0; i < mechanics.Length; i++)
            {
                var m = mechanics[i];
                if (m.trigger.kind != Wassup.Data.DcTriggerKind.OnDeath ||
                    m.payload.kind != Wassup.Data.DcPayloadKind.SplitOnDeath) continue;

                var child = m.payload.splitUnit;
                // bake 가 이미 loud 거절했으므로 여기서는 조용히 빠진다(같은 에러 2중 스팸 방지).
                if (child == null) return;
                // 런타임 최후 방어선 — 직접 자기순환은 bake 경고를 무시하고 플레이하면 판이
                // 끝나지 않는다(킬마다 개체 배가 → 전멸 판정 영영 불성립). bake 의 사슬 검증이
                // 정본이고 이 한 줄은 그것을 무시한 경우의 안전판이다(간접 순환은 bake 가 잡는다).
                if (child == killedType)
                {
                    Debug.LogError($"[BattleBridge] {killedType.displayName}: splitUnit 이 자기 자신이다 — " +
                                   "분열을 건너뛴다(무한 분열 방지). 저작을 고칠 것.");
                    return;
                }

                int count = Mathf.Clamp((int)m.payload.magnitude, 0, MaxSplitChildren);
                if (count <= 0) return;

                // 결정론 — 인덱스 기반 고정 배치다(RNG 금지: 비동기 토너먼트 양측 동일 시뮬).
                //
                // ★기준점은 **부모의 셀 중심**이다. 부모의 연속 좌표에 오프셋을 더하면 안 된다 —
                // MovementCellTrim 이 유닛을 셀 중심에서 `0.5·tileSize − 1e-3` 까지 벗어나게
                // 허용하므로(충돌·분리·평활화가 상시 만드는 상태), 거기에 0.25 를 더하면 자식이
                // **인접 셀**에 태어난다. 그 셀이 골이면 MovementSystem 이 다음 틱에 PastGoalTag 를
                // 찍어 «처치했는데 유출» 이 되고, Build/Blocked 셀이면 그 셀 flow 가 0 이라 한
                // 프레임을 FlowRecovery 로 버린다. (2026-08-12 ECS 리뷰 H1 — 초판 주석이 성립하지
                // 않는 불변식을 주장했다.)
                //
                // 셀 중심 기준 + 반경 0.25 면 |오프셋| < 0.49 라 자식은 부모와 **같은 셀**에 남고
                // flow/goal/cell-trim 이 불변이다 — ComputeSpawnLateralOffset(셀 중심에 더하고
                // SpawnSpread.MaxHalfFraction 0.49 로 클램프)과 같은 형태가 된다.
                // 겹침은 AgentSeparationSystem 이 푼다. y 는 부모 것을 유지(sim 높이 연속).
                int2 grid = _generatedMap.IsCreated ? _generatedMap.gridSize : FallbackGridSize;
                int2 parentCell = GridMath.WorldToCell(deathWorldPos, tileSize, grid, origin: _boardOrigin);
                Vector3 cellCenter = GridToWorldCenterVector(
                    new Vector2Int(parentCell.x, parentCell.y), deathWorldPos.y);

                float radius = tileSize * 0.25f;
                for (int c = 0; c < count; c++)
                {
                    float angle = (Mathf.PI * 2f * c) / count;
                    var offset = new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius);
                    CreateEnemyEntity(child, cellCenter + offset);
                }
                return; // 첫 SplitOnDeath 슬롯만 (v1) — OnDeath 폭발 선례와 같은 규약
            }
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
