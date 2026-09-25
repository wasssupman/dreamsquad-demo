using System;
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;
using Wassup.BattleCore;
using Wassup.Core.TimeControl;
using Wassup.Data;

namespace Wassup.BattleCoreUnity
{
    // battle-core-rebuild unit 1 → 5a — **시간과 문(門)만** 갖는 Unity 층(UML §6).
    //
    // 코어는 프레임을 모른다. 이 컴포넌트가 프레임 시간을 누산해 **정수 틱**으로 잘라
    // `BattleMatch.Tick()` 을 부른다. 그래서 슬로모·정지가 규칙을 건드리지 않는다 —
    // 느린 것은 판이 아니라 **틱 발행률**이고, `dt` 는 언제나 1/60 이다(계약 5).
    //
    // `Time.timeScale` 을 쓰지 않는다. 전역 시간 배율은 UI 애니메이션·연출까지 같이
    // 끌고 가고, 이 프로젝트는 그래서 도메인 시간 제어(`TimeManager`)를 따로 두었다.
    // 여기서는 «이 판의 틱만» 느려져야 한다 — 발행률 = Battle 도메인 스케일.
    //
    // unit 5a 가 더한 것 셋:
    //   ① **발행률을 `TimeManager` 에서 읽는다.** unit 1 은 자기 `_timeScale` 필드를 들고
    //      있었는데, 그러면 「전투 시간의 주인」이 둘이 되고 카드 슬로모(0.3)가 이 판에만
    //      안 걸린다. 그 필드와 `SetTimeScale` 은 은퇴했다(5a 「고친 것」).
    //   ② **정의표를 짓는 문.** 스테이지 프리팹 → 격자·거점 → `MatchDefinitionBuilder`.
    //   ③ **이벤트를 `ViewOrder` 로 정렬해 다시 방출한다** + 뷰가 보간할 읽기 모델.
    //
    // 이 컴포넌트는 **규칙을 하나도 소유하지 않는다.** 판정·상태·저장이 여기 들어오면
    // 그것이 새 브리지의 첫 줄이다(절대 제약 1).
    [DisallowMultipleComponent]
    public sealed class BattleDriver : MonoBehaviour
    {
        [Header("판 저작 (판 밖 → 정의표)")]
        [Tooltip("기본 매치 모드 SO — 모드 선택 3단의 **셋째 칸**. 아무도 안 고르면 이것으로 짓는다.")]
        [SerializeField] private MatchModeData _mode;

        [Tooltip("에디터 직접 진입 편성(로비를 거치지 않은 판). 로비 판은 저장 편성이 이긴다(unit 8b · `MatchEntry`).")]
        [SerializeField] private DefenderUnitData[] _defenders = Array.Empty<DefenderUnitData>();
        [Tooltip("⑦ 드라이버 저작 덱 — 모드 덱·맵 풀 엔트리 덱이 없을 때만.")]
        [SerializeField] private AttackDeck _deck;
        [SerializeField] private WavePlanAsset _plan;
        [SerializeField] private BonusWaveData _bonus;

        [Tooltip("맵 풀이 비었을 때만 쓰는 스테이지 프리팹(에디터·테스트 고정구). 인스턴스가 곧 비주얼이고 격자·거점의 정본이다.")]
        [SerializeField] private Wassup.Core.MapStage _stagePrefab;

        [Header("판 진입 (unit 8b — 로비 입력 → 정의표)")]
        [Tooltip("기본 맵 풀(모드 `mapPool` 이 비었을 때). 맵·덱·플랜이 같은 인덱스로 잠긴다. 비우면 `_stagePrefab`.")]
        [SerializeField] private MapStagePool _mapPool;
        [Tooltip("디버그 고정 맵 시드(옛 브리지 `fixedMapSeed`). 0 = 끔 → 서버 토너먼트 시드 → 0번.")]
        [SerializeField] private int _fixedMapSeed;
        [SerializeField] private DefenderCatalog _defenderCatalog;
        [SerializeField] private DreamstoneCatalog _stoneCatalog;

        [Tooltip("보드 평면 선언(격자). 비어 있으면 뷰가 sim→view 변환을 못 한다.")]
        [SerializeField] private View.CoreBoardPlane _boardPlane;

        [SerializeField, Min(0.01f)] private float _tileSize = 1f;

        [Tooltip("시즌 등록부(옛 브리지 `seasonRegistry` · bridge-fields 21). 활성 시즌의 **맵 테마**가 효과 타일 종류·개수를 준다. "
                 + "판을 짓기 전에 `SeasonRuntime` 에 묶는다 — 새 씬에 묶는 자가 없으면 효과 타일이 0 이 된다(unit 8a2).")]
        [SerializeField] private Wassup.Data.Season.SeasonRegistry _seasonRegistry;

        [Tooltip("적이 어떻게 서고 어떻게 퍼지나. 비우면 코어 기본값(= 옛 씬 값)이 쓰인다.")]
        [SerializeField] private MovementTuningConfig _movementTuning;

        [Tooltip("스택 저작(불·얼음·출혈·피로도). 비우면 스택은 쌓이기만 하고 임계가 안 터진다.")]
        [SerializeField] private StackModifierSO[] _stackModifiers = Array.Empty<StackModifierSO>();

        [Tooltip("탄 부여 상한(한 발이 얼마까지 나르나). 줄이 없는 키의 부여는 관문이 거절한다.")]
        [SerializeField] private ImbueCapConfig _imbueCaps;

        [Tooltip("이 판에 깔릴 수 있는 존 장판 SO. 배열 순서 = 정의표 줄 번호. **까는 자는 unit 7** 이라 "
                 + "오늘 라이브에서는 디버그 메뉴만 깐다(unit 6c). 비우면 장판 0.")]
        [SerializeField] private HazardSO[] _hazards = Array.Empty<HazardSO>();

        [Tooltip("탄이 참조하지 않는 길막 SO(디버그·unit 7 생산자 전용). 탄이 참조하는 것은 탄 표에서 자동으로 모인다.")]
        [SerializeField] private Wassup.Battle.Effects.BlockingHazardSO[] _extraBlockers
            = Array.Empty<Wassup.Battle.Effects.BlockingHazardSO>();

        [Tooltip("개발용 덱 덮어쓰기(구성 순서 그대로). **비우면** 프로필 확정 덱 + 판마다 굴린 액티브로 짓는다(unit 7c) — "
                 + "채우면 이 목록이 곧 덱이다(테스트·개발 판).")]
        [SerializeField] private DreamcatcherCard[] _cards = Array.Empty<DreamcatcherCard>();

        [Header("드림캐쳐 덱 (unit 7c — 프로필 확정 덱 + 판마다 굴린 액티브)")]
        [Tooltip("프로필(씬 간 메모리 캐시). 확정 덱이 없거나 검증에 실패하면 부착 덱은 비어 있다(기본 덱 폴백 없음 — D3).")]
        [SerializeField] private Wassup.Core.PlayerProfileSO _profile;
        [SerializeField] private DreamcatcherCardCatalog _cardCatalog;
        [Tooltip("판마다 굴리는 공용 액티브의 스킬 풀. 굴림 시드 = 판 시드(재현).")]
        [SerializeField] private SkillData[] _activePool = Array.Empty<SkillData>();
        [SerializeField, Min(0)] private int _activeCount = 2;
        [Tooltip("스킬을 감싸는 액티브 카드. 굴린 스킬을 감싸는 카드가 없으면 그 장만 빠진다(D4).")]
        [SerializeField] private DreamcatcherCard[] _activeCards = Array.Empty<DreamcatcherCard>();

        [Tooltip("판 진입 드림스톤(스탯 돌 = 배치 유닛 상속 · 코스트 돌 = 재생 배율). 비우면 없음.")]
        [SerializeField] private DreamstoneData[] _dreamstones = Array.Empty<DreamstoneData>();

        [Tooltip("판 시드 고정 노브(G3 — 옛 `debugFixedMatchSeed`). 0 = 판마다 새 난수. 재현의 두 축 중 하나(나머지는 modeId).")]
        [SerializeField] private int _seed;

        [SerializeField] private bool _beginOnStart = true;

        [Header("틱 발행")]
        [SerializeField, Min(1)]
        [Tooltip("한 프레임에 밀어 넣을 틱의 상한. 프레임이 튀었을 때 나선형 지연을 막는다")]
        private int _maxTicksPerFrame = 8;

        private BattleMatch _match;
        private MatchDefinition _definition;
        private MatchModeData _resolvedMode;
        private float _accumulator;
        private bool _paused;

        private Wassup.Core.MapStage _stageInstance;
        private GeneratedMap _map;
        private readonly List<StructureEntry> _stageStructures = new List<StructureEntry>();
        private AttackUnitData[] _enemyAssets = Array.Empty<AttackUnitData>();
        private readonly MatchViewAssets _viewAssets = new MatchViewAssets();
        private DefenderUnitData[] _activeDefenders = Array.Empty<DefenderUnitData>();
        private MatchEntryPlan _entry;

        // ── 이벤트 방출 ───────────────────────────────────────────────────────
        // C# 이벤트(`+=`)를 쓰지 않는 이유: 그 호출 순서는 곧 **씬 컴포넌트의 나열 순서**이고,
        // 하이어라키에서 오브젝트 하나를 끌어 올리면 연출 순서가 조용히 뒤집힌다. 순서는
        // `ViewOrder` 상수가 말한다 — 코어가 `EventOrder` 로 하는 것과 같은 결이다.
        private readonly struct ViewSub
        {
            public readonly int Order;
            public readonly Action<CoreEvent> Handler;
            public ViewSub(int order, Action<CoreEvent> handler) { Order = order; Handler = handler; }
        }

        private readonly List<ViewSub> _subs = new List<ViewSub>(8);
        // 방출 중에 구독이 바뀌어도(풀이 꺼지거나 켜져도) 이번 방출의 명단은 흔들리지 않는다.
        private readonly List<ViewSub> _dispatch = new List<ViewSub>(8);
        private bool _subsDirty;

        // ── 읽기 모델 ────────────────────────────────────────────────────────
        // 틱은 60Hz, 화면은 그보다 빠르거나 느리다. 뷰가 «틱 위치»를 그대로 쓰면 60Hz 계단이
        // 보이므로 **직전 틱 위치와 현재 위치 사이를 보간**한다. 그 「직전」을 여기 든다.
        // ⚠ 코어에 두지 않는다 — 보간은 화면의 사정이고, 코어에 들어가면 판이 프레임을 알게 된다.
        private readonly Dictionary<int, float3> _prevPos = new Dictionary<int, float3>(256);

        public BattleMatch Match => _match;
        public MatchDefinition Definition => _definition;
        public bool Paused => _paused;

        /// <summary>이 판을 지은 모드 SO(선택 3단을 푼 결과). 손패 화면이 각성 저작(감속 배율 · 표식 반경)을 읽는 창구다.</summary>
        public MatchModeData Mode => _resolvedMode;
        public bool Running => _match != null;
        public float TileSize => _tileSize;

        /// <summary>이 판에 세워진 스테이지 저작 거점 수. 「저작 = 월드」 대조의 왼쪽 항이다.</summary>
        public int StageStructureCount => _stageStructures.Count;

        /// <summary>격자 크기(셀). 뷰 소팅이 읽는다.</summary>
        public int2 GridSize => _map.IsCreated ? _map.gridSize : default;

        // unit 5b — 뷰가 «판이 화면 어디에 있나»를 묻는 두 창구. 둘 다 **읽기 전용**이고,
        // 값을 만드는 것이 아니라 이미 소유한 곳을 가리킨다(제약 12 의 판단 순서 ⓐ).

        /// <summary>보드 격자. 카메라 fit 이 판의 월드 bounds 를 재는 근거다.</summary>
        public GridLayout BoardGrid => _boardPlane != null ? _boardPlane.Grid : null;

        /// <summary>이 판에 선 스테이지 인스턴스. 스테이지가 소유한 것(포스트 볼륨)을 찾는 입구.</summary>
        public Wassup.Core.MapStage StageRoot => _stageInstance;

        // ── 저작 자산 되찾기 ─────────────────────────────────────────────────
        // 정의표는 plain 이라 스켈레톤·시트·프리팹을 모른다(계약 6). 그런데 뷰는 그것이 있어야
        // 무엇을 그릴지 안다. 그래서 **인덱스**로 되찾는다 — 사건이 나르는 `DefIndex` 가
        // 이 목록의 줄 번호다. 목록을 만드는 쪽과 인덱스를 매기는 쪽이 같아야 하므로
        // 둘 다 `MatchDefinitionBuilder` 가 쓰는 것과 **같은 함수**에서 나온다.
        // unit 8b — 진입 해석을 거친 판은 그 편성(로비 판 = 저장 편성), 정의표를 직접 건 판(테스트)은 저작 편성.
        public IReadOnlyList<DefenderUnitData> DefenderAssets => _entry != null ? _activeDefenders : _defenders;

        /// <summary>unit 8b — 이 판이 어느 문으로 들어왔나(로비·테스트·에디터). 판 전엔 null.</summary>
        public MatchEntryPlan Entry => _entry;

        /// <summary>
        /// unit 8b — 이 판의 덱 스냅샷(`deckInfo`). 반입 때 유닛·돌, 덱 확정 뒤 카드까지(단조 증가 — G22).
        /// 제출(`ReportResult`)·나가기(`AbandonMatch`)가 같은 문자열을 싣는다.
        /// </summary>
        public string DeckInfoJson { get; private set; } = "";

        /// <summary>이 판을 짓는 데 쓴 맵 풀 인덱스(-1 = 풀 없이 `_stagePrefab`). 결정론 테스트의 창.</summary>
        public int MapPoolIndex { get; private set; } = -1;
        public IReadOnlyList<AttackUnitData> EnemyAssets => _enemyAssets;

        /// <summary>
        /// 보너스 웨이브 저작(unit 8a). 포탈 뷰가 **뷰 타이밍**(`portalAppearDelaySec`·`portalLingerSec`)을
        /// 읽는 창이다 — 그 둘은 화면의 사정이라 정의표(`configHash`)에 싣지 않는다.
        /// </summary>
        public BonusWaveData BonusAuthoring => _bonus;

        /// <summary>탄·거점의 줄 번호 → 저작 에셋. **번호를 매긴 빌더가 직접 채운다**.</summary>
        public MatchViewAssets ViewAssets => _viewAssets;

        /// <summary>스테이지 저작 거점. 거점 프랍은 **맵 수명**이라 유닛 뷰 풀이 아니라 여기서 온다.</summary>
        public IReadOnlyList<StructureEntry> StageStructures => _stageStructures;

        /// <summary>
        /// 틱 사이 보간 계수 [0,1). 뷰가 위치를 부드럽게 잇는 데 쓴다.
        /// 정지 중에는 0 이다 — 멈춘 판에서 뷰가 계속 미끄러지면 안 된다.
        /// </summary>
        public float Alpha => _paused ? 0f : Mathf.Clamp01(_accumulator / BattleMatch.Dt);

        /// <summary>판 위의 개체 전부. **읽기 전용**이다 — 뷰가 고치면 그것이 규칙이 된다.</summary>
        public IReadOnlyList<Unit> Units => _match != null ? _match.World.Units : Array.Empty<Unit>();

        public IReadOnlyList<Wassup.BattleCore.Combat.Projectile.Projectile> Projectiles
            => _match != null ? _match.World.Projectiles
                              : Array.Empty<Wassup.BattleCore.Combat.Projectile.Projectile>();

        /// <summary>
        /// 그 개체가 아직 판 위에 있나. **뷰의 자가 치유용**이지 정상 경로가 아니다 —
        /// 모든 소멸은 소멸 이벤트를 낸다(계약 7). 여기서 유령이 잡히면 그것은 버그의 신호다.
        /// </summary>
        public bool IsAlive(SimEntityId id) => _match != null && _match.World.IsAlive(id);

        public Unit Find(SimEntityId id) => _match?.World.Find(id);

        /// <summary>
        /// 이 프레임에 그릴 위치. 직전 틱 위치 → 현재 위치를 `Alpha` 로 보간한다.
        /// 이번 틱에 태어난 개체는 「직전」이 없으므로 현재 위치를 그대로 쓴다(보간하면
        /// 원점에서 날아온다).
        /// </summary>
        public bool TryGetRenderPosition(SimEntityId id, out float3 position)
        {
            var u = Find(id);
            if (u == null) { position = default; return false; }
            position = _prevPos.TryGetValue(id.Value, out var prev)
                ? math.lerp(prev, u.Position, Alpha)
                : u.Position;
            return true;
        }

        /// <summary>
        /// 뷰 풀이 자기 순서로 붙는 자리. `order` 가 낮을수록 먼저 받는다(`ViewOrder`).
        /// 같은 값이면 구독한 차례 — 둘 다 결정론이다.
        /// </summary>
        public void Subscribe(int order, Action<CoreEvent> handler)
        {
            if (handler == null) throw new ArgumentNullException(nameof(handler));
            int at = _subs.Count;
            while (at > 0 && _subs[at - 1].Order > order) at--;
            _subs.Insert(at, new ViewSub(order, handler));
            _subsDirty = true;
        }

        public void Unsubscribe(Action<CoreEvent> handler)
        {
            for (int i = _subs.Count - 1; i >= 0; i--)
            {
                if (_subs[i].Handler != handler) continue;
                _subs.RemoveAt(i);
                _subsDirty = true;
                return;
            }
        }

        private void Start()
        {
            // 테스트·다른 진입점이 `Begin(definition)` 으로 이미 판을 걸었으면 저작 진입은 건너뛴다 —
            // 안 그러면 `_mode` 없는 드라이버가 매 부팅마다 에러 로그를 낸다.
            //
            // unit 5c — 씬 경계를 넘어온 선택을 **여기서 한 번 소비한다.** 이 자리가 유일한
            // 소비처라 「어느 판이 그 선택을 먹었나」를 물을 일이 없다.
            // unit 8b — G21: 토너먼트 참가는 **로비가 발행한 것만 채택**한다. 로비 게이트를 거치지 않은 진입(에디터·테스트)은
            // 상태만 리셋되고 참가가 생기지 않는다(옛 `GameManager.OnEnable`). 반입 기록(G22)보다 **먼저**여야 한다.
            Wassup.Core.Api.TournamentMatchReporter.BeginMatch();
            if (_beginOnStart && !Running) Begin(MatchEntryContext.Consume());
        }

        /// <summary>저작 그대로 짓는다(선택 없음 = 기본 모드 SO).</summary>
        public void Begin() => Begin(ModeSelection.None);

        /// <summary>
        /// 저작을 읽어 판을 짓고 건다. 스테이지가 없으면 **조용히 지나가지 않는다** —
        /// 맵 없는 판은 적이 갈 곳이 없어 콘솔 에러 0 으로 아무 일도 일어나지 않는다.
        ///
        /// unit 5c — **모드 선택 3단**: 테스트 모드 강제 &gt; 로비/서버 지정 &gt; 기본 모드 SO.
        /// 서열을 아는 함수는 `MatchDefinitionBuilder.ResolveMode` 하나이고 여기는 그것을
        /// 부르기만 한다 — 세 칸을 여기서 다시 비교하면 그것이 두 번째 자다.
        /// </summary>
        public void Begin(ModeSelection selection)
        {
            var mode = MatchDefinitionBuilder.ResolveMode(selection.TestMode, selection.Lobby, _mode);
            if (mode == null)
            {
                Debug.LogError("[BattleDriver] 매치 모드 SO 가 비었다 — 판을 짓지 않는다.", this);
                return;
            }
            // unit 8a2 행 1 — 시즌 등록부를 묶는다(옛 `BattleBridge.Awake` `:685-690` 그대로 — 옛 씬만 묶어서, 로비 → 새 씬
            // 경로에서는 `SeasonRuntime.Active` 가 null 이라 효과 타일이 **한 칸도** 안 뽑혔다). 규칙이 아니라 저작 선택이다.
            Wassup.Data.Season.SeasonRuntime.Bind(_seasonRegistry);
            var season = Wassup.Data.Season.SeasonRuntime.Active;
            if (season == null || season.mapTheme == null)
                Debug.LogError("[BattleDriver] SeasonRegistry / activeSeason / mapTheme 가 배선되지 않았다 — 효과 타일 없이 짓는다. "
                               + "BattleCoreScene 드라이버에 SeasonRegistry.asset 을 연결하라.", this);
            // unit 8b — **진입 해석**(G3·G5·G7·G13). 순서는 옛 것 그대로: 시드 → (기믹 = 코어가 시드로) → 맵.
            // 시드 0 은 「아무도 안 골랐다」다 — 고정 노브, 그것도 0 이면 새 난수(G3).
            var entry = MatchEntry.Resolve(new MatchEntry.Sources
            {
                Profile = _profile,
                DefenderCatalog = _defenderCatalog,
                StoneCatalog = _stoneCatalog,
                FixedSeed = _seed,
            }, MatchEntry.ConsumeTestMode(), selection.Seed);
            _entry = entry;
            int seed = entry.Seed;
            _activeDefenders = entry.Defenders ?? _defenders ?? Array.Empty<DefenderUnitData>();
            var stones = entry.Stones ?? _dreamstones;

            // G22 — 반입 편성·돌을 **배치 전에** 기록한다(앱이 죽어도 그 판이 편성을 갖는다). 참가가 없으면 저쪽이 no-op.
            DeckInfoJson = MatchEntry.DeckInfoJson(entry, null);
            Wassup.Core.Api.TournamentMatchReporter.PersistMatchDeck(DeckInfoJson);

            // 맵 풀 4갈래(옛 `BuildMapForBattle` `:1263~1300`). 모드 `mapPool` 이 비면 기본 풀.
            var pool = mode.mapPool != null ? mode.mapPool : _mapPool;
            var stagePrefab = _stagePrefab;
            var poolDeck = _deck;
            WavePlanAsset encounterPlan = null;
            MapPoolIndex = -1;
            if (MatchDefinitionBuilder.TrySelectEncounter(pool, Wassup.Core.DevMapOverride.Index, _fixedMapSeed,
                    Wassup.Core.Api.TournamentMatchReporter.HasTournamentSeed,
                    Wassup.Core.Api.TournamentMatchReporter.TournamentSeed,
                    out var encounter, out int poolIndex, out string poolSource))
            {
                stagePrefab = encounter.stage;
                if (encounter.deck != null) poolDeck = encounter.deck;
                encounterPlan = encounter.plan;   // 저작 플랜은 맵과 한 몸
                MapPoolIndex = poolIndex;
                Debug.Log($"[BattleDriver] map pool index={poolIndex}/{pool.Count}(+dev {pool.DevCount}) (source={poolSource}) entry={entry.Kind} seed={seed}", this);
            }
            else if (pool != null && pool.Count > 0)
                Debug.LogError($"[BattleDriver] 맵 풀 엔트리 {poolIndex} 에 스테이지가 없다 — `_stagePrefab` 으로 짓는다.", this);
            if (!BuildStage(stagePrefab)) return;
            _resolvedMode = mode;

            // unit 7c — 덱. 개발용 덮어쓰기가 비었으면 프로필 확정 덱 + 판 시드로 굴린 액티브(판 밖에서 한 번).
            // ⚠ 모드에 각성 저작이 없으면 카드 **값**을 모른다(값의 주인 = `AwakeningConfig`) — 그 모드는 카드 없는 판이다.
            // 짓다가 카드마다 에러를 내지 않고 한 번 말한다(테스트 모드 SO · 각성 없는 모드).
            // ⚠ 각성 가드가 **먼저**다 — 개발용 덮어쓰기(`_cards`)도 이 가드를 지난다. 덮어쓰기 분기를 앞에 두면 각성 없는
            // 모드(테스트 모드 SO)에서 카드마다 빌더 에러가 난다(dev 덱 `d06ae0bcd` 이 그 구멍을 열었다).
            // ⚠ unit 8b — **로비 판은 개발용 덮어쓰기(`_cards`)가 프로필 덱에 양보한다.** 덮어쓰기는 에디터 메뉴 판 전용이다.
            IReadOnlyList<DreamcatcherCard> cards;
            if (mode.awakeningConfig == null)
            {
                Debug.LogWarning($"[BattleDriver] 모드 '{mode.name}' 에 각성 저작(AwakeningConfig)이 없다 — 드림캐쳐 덱 없이 짓는다"
                                 + (_cards != null && _cards.Length > 0 ? "(개발용 덱 덮어쓰기도 버린다)." : "."), this);
                cards = Array.Empty<DreamcatcherCard>();
            }
            else if (!entry.FromLobby && _cards != null && _cards.Length > 0) cards = _cards;
            else cards = Cards.CoreDeckComposition.Compose(_profile, _cardCatalog, _activePool, _activeCount, _activeCards,
                                                                      seed, msg => Debug.LogWarning(msg, this));

            // ⚠ **거점 목록을 반드시 넘긴다**(`55688ef5`). 격자 투영에는 셀과 진영밖에 없어
            // 스탯이 없다 — 안 넘기면 마음 타워·본능이 한 기도 안 서고 콘솔 에러 0 으로
            // 조용히 실패한다. 이 인자를 지우지 말 것.
            // 적 목록은 **뷰도 같은 줄 번호로 읽어야** 해서 여기서도 한 번 모은다.
            // 순수 함수라 아래 `Build` 안의 호출과 같은 배열이 나온다(그래서 둘이 안 갈린다).
            // ⚠ 모드가 고른 덱·플랜을 **빌더와 같은 함수로** 푼다 — 여기서 드라이버 저작을 그대로
            // 모으면 모드 덱을 쓰는 판에서 뷰의 적 줄 번호가 정의표와 갈린다.
            var entryAuthoring = new EntryAuthoring
            {
                ForcedPlan = entry.ForcedPlan,
                EncounterPlan = encounterPlan,
            };
            _enemyAssets = MatchDefinitionBuilder.CollectEnemies(
                               MatchDefinitionBuilder.ResolveDeck(mode, poolDeck),
                               MatchDefinitionBuilder.ResolveWavePlan(mode, _plan, in entryAuthoring), _bonus)
                           ?? Array.Empty<AttackUnitData>();

            var def = MatchDefinitionBuilder.Build(
                mode, _activeDefenders, poolDeck, _plan, _bonus, seed,
                // 코스트 돌 배율(G10)은 `Build` 안에서 `CardDefinitionBuilder.CostRateOf(dreamstones)` 가 곱한다(7b) —
                // 여기 1 은 호출자 배율(항등)이다. 이 자리에 `CostRateOf` 를 넣으면 **두 번 곱한다.**
                costRateMultiplier: 1f, map: in _map, tileSize: _tileSize,
                structures: _stageStructures, viewAssets: _viewAssets,
                movement: _movementTuning, stackModifiers: _stackModifiers,
                imbueCaps: _imbueCaps,
                // unit 6b — 효과 타일은 **시즌 맵 테마**에서 온다(옛 `SeasonRuntime.Active.mapTheme`).
                // 테마가 없으면(시즌 미바인딩 진입) 효과 타일 0 — 조용히 기본값을 지어내지 않는다.
                board: new BoardEffectAuthoring
                {
                    Hazards = _hazards,
                    ExtraBlockers = _extraBlockers,
                    Theme = Wassup.Data.Season.SeasonRuntime.Active != null
                        ? Wassup.Data.Season.SeasonRuntime.Active.mapTheme : null,
                    SuppressEffectTiles = _stageInstance != null && _stageInstance.suppressEffectTiles,
                },
                cards: cards, dreamstones: stones, entry: entryAuthoring);

            // G22 — 덱 확정(카드)까지 **같은 통로로 갱신**한다(payload 단조 증가). 카드는 고른 덱만 — 굴린 액티브 제외.
            var baseIds = new List<string>();
            if (entry.FromLobby)
                foreach (var c in Cards.CoreDeckComposition.ResolveAttachDeck(_profile, _cardCatalog))
                    if (c != null) baseIds.Add(c.id);
            DeckInfoJson = MatchEntry.DeckInfoJson(entry, baseIds);
            Wassup.Core.Api.TournamentMatchReporter.PersistMatchDeck(DeckInfoJson);

            Begin(def);
        }

        /// <summary>판을 건다. 이미 걸려 있으면 교체한다(이전 판의 구독은 호출자가 정리한다).</summary>
        public void Begin(MatchDefinition definition)
        {
            _definition = definition;
            _match = new BattleMatch(definition);
            _accumulator = 0f;
            _prevPos.Clear();
            _match.Begin();
            DrainEvents();
        }

        public void Pause(bool paused) => _paused = paused;

        /// <summary>
        /// 커맨드를 건다. 코어가 그 자리에서 판정하고, 그 커맨드가 만든 사건도
        /// 그 자리에서 배달된다 — 정지 중에도 배치가 화면에 보이는 이유다.
        /// </summary>
        public Receipt Apply(in Command command)
        {
            if (_match == null) return Receipt.Reject(RejectReason.NotRunningOrPlacementClosed);
            var receipt = _match.Apply(command);
            DrainEvents();
            return receipt;
        }

        private void Update()
        {
            if (_match == null) return;

            // 발행률. 카드를 잡는 동안 0.3, 메뉴에서 0. 코어는 이 배율을 모른다(계약 5).
            float rate = Mathf.Max(0f, TimeManager.Instance.ScaleOf(TimeDomain.Battle));
            if (_paused || rate <= 0f) { DrainEvents(); return; }

            _accumulator += Time.unscaledDeltaTime * rate;

            int ticks = 0;
            while (_accumulator >= BattleMatch.Dt && ticks < _maxTicksPerFrame)
            {
                SnapshotPositions();
                _accumulator -= BattleMatch.Dt;
                _match.Tick();
                ticks++;
            }

            // 상한에 걸렸으면 남은 누산을 **버린다**. 안 버리면 다음 프레임에 더 많은
            // 틱을 밀어야 하고, 그 빚이 계속 불어 프레임이 영영 못 따라잡는다.
            if (ticks >= _maxTicksPerFrame) _accumulator = 0f;

            DrainEvents();
        }

        // 「직전 틱의 위치」. 틱 **앞**에서 찍는다 — 뒤에서 찍으면 현재와 같아져 보간이 항등이 된다.
        private void SnapshotPositions()
        {
            var units = _match.World.Units;
            for (int i = 0; i < units.Count; i++) _prevPos[units[i].Id.Value] = units[i].Position;
        }

        private void DrainEvents()
        {
            var events = _match.Events;
            if (events.Count == 0) return;

            if (_subsDirty)
            {
                _dispatch.Clear();
                _dispatch.AddRange(_subs);
                _subsDirty = false;
            }

            for (int i = 0; i < events.Count; i++)
            {
                var e = events[i];
                for (int s = 0; s < _dispatch.Count; s++) _dispatch[s].Handler(e);
            }
            _match.ClearEvents();
        }

        // ── 스테이지 → 격자·거점 ─────────────────────────────────────────────
        //
        // 옛 브리지의 맵 빌드에서 **규칙에 필요한 것만** 옮겼다. 안 옮긴 것은 5a 의
        // 「이식 제외」 표에 있다(맵 풀 선택·타일맵 페인팅·테마·카메라 bounds push).
        private bool BuildStage(Wassup.Core.MapStage stagePrefab)
        {
            TeardownStage();

            if (stagePrefab == null)
            {
                Debug.LogError("[BattleDriver] 맵 스테이지 프리팹이 없다 — 판을 짓지 않는다.", this);
                return false;
            }

            // 루트는 원점·무회전·스케일 1 로 고정한다. 스캐너는 로컬(스케일 나눔)로 양자화하고
            // 격자는 월드(스케일 곱)로 정렬하므로, 루트가 기울거나 늘어나면 프랍과 셀이
            // **조용히** 어긋난다(옛 전투에서 실제로 잡힌 사고).
            _stageInstance = Instantiate(stagePrefab);
            _stageInstance.name = stagePrefab.name;
            _stageInstance.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            _stageInstance.transform.localScale = Vector3.one;

            try
            {
                var scan = Wassup.Core.MapStageScanner.Scan(_stageInstance, _tileSize);
                _map = DioramaMapBuilder.Assemble(scan, Unity.Collections.Allocator.Persistent);
                _stageStructures.Clear();
                _stageStructures.AddRange(scan.structures);
                // 빌더와 같은 (y, x) 사전순 — 거점 인덱스가 저작 파일 순서에 흔들리지 않게.
                _stageStructures.Sort(DioramaMapBuilder.CompareStructureRowMajor);
            }
            catch (Wassup.Data.MapGrid.MapGenerationFailedException ex)
            {
                Debug.LogError($"[BattleDriver] 스테이지 조립 실패 — {ex.Message}", this);
                TeardownStage();
                return false;
            }

            if (!MapConnectivity.AllSpawnsReachGoal(_map))
            {
                Debug.LogError("[BattleDriver] 스테이지 연결성 실패(스폰→골 도달 불가).", this);
                TeardownStage();
                return false;
            }

            // 보드 평면 선언. 뷰의 sim→view 는 전부 이 격자를 기준으로 돈다.
            // sim origin 은 무조건 zero 다(맵 계약).
            if (_boardPlane != null)
                _boardPlane.Declare(_tileSize,
                    _stageInstance.transform.TransformPoint(_stageInstance.gridOriginLocal));
            else
                Debug.LogWarning("[BattleDriver] 보드 평면이 배선되지 않았다 — 뷰가 sim→view 를 못 한다.", this);

            return true;
        }

        private void TeardownStage()
        {
            if (_map.IsCreated) _map.Dispose();
            _map = default;
            _stageStructures.Clear();
            if (_stageInstance != null)
            {
                if (Application.isPlaying) Destroy(_stageInstance.gameObject);
                else DestroyImmediate(_stageInstance.gameObject);
                _stageInstance = null;
            }
        }

        private void OnDestroy() => TeardownStage();
    }
}
