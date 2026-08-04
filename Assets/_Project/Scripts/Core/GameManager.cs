using System.Collections.Generic;
using UnityEngine;
using Wassup.Bridge;
using Wassup.Data;
using Wassup.Data.MapGrid;
using Wassup.Logging;

namespace Wassup.Core
{
    // gift-phase unit 0 — Gift 를 Placement 앞에 삽입(선물 페이즈=배치 직전).
    // 직렬화 필드/순서비교 없음(전부 == 비교)이라 정수 시프트 무해.
    // score-tally-sequence unit 1 — Tally 는 Battle 과 Result 사이의 결과 연출 구간이다.
    // 흐름상으론 Battle 다음이지만 **값은 반드시 맨 뒤**에 둔다: CameraDirectionConfig 의
    // CameraPhasePose.phase / breathPhases 가 이 enum 을 int 로 직렬화하고 있어
    // (Assets/_Project/Data/Camera/CameraDirectionConfig.asset — phase: 1/3/4/5),
    // 중간에 끼우면 Result 가 5→6 으로 밀려 카메라 설정이 통째로 어긋난다.
    // 그래서 값 순서 != 시간 순서다. Tally 는 Battle 뒤에, Gimmick 은 Gift 와 Placement
    // 사이에 일어나지만 둘 다 맨 뒤에 붙는다. 전 코드가 == 비교라 순서 의존은 없다.
    // 미등록 페이즈는 CameraDirector 가 hold 로 처리하므로 포즈 엔트리도 필요 없다.
    public enum GamePhase { None, Draft, Gift, Placement, Battle, Result, Tally, Gimmick }

    [DefaultExecutionOrder(-100)]
    public class GameManager : MonoBehaviour
    {
        public static GameManager Instance { get; private set; }

        [SerializeField] private BattleLogger logger;
        [SerializeField] private BattleBridge battleBridge;
        [SerializeField] private DraftController draftController;
        [SerializeField] private CostRuntime costRuntime;
        // defender-placement-cooldown 0 — 유닛 타입별 배치 쿨타임 소유(비싱글턴, CostRuntime 미러).
        [SerializeField] private PlacementCooldownRuntime cooldownRuntime;
        [SerializeField] private CostConfig costConfig;
        // gimmick-match-integration unit 1 — 매치 기믹 게이팅/목록 소스(시즌 대체). 미할당이면 기믹 없음.
        [SerializeField] private BattleConfig battleConfig;
        [SerializeField] private SkillLoadoutController skillLoadout;
        // squad-loadout Unit 3 — squad carry-in source (drives the squad branch
        // in Start; null/empty → existing draft path).
        [SerializeField] private PlayerProfileSO profileSO;
        [SerializeField] private DefenderCatalog catalog;
        // dreamstone-loadout Unit 3 — resolves SquadPreset.stoneIds to assets for carry-in.
        [SerializeField] private DreamstoneCatalog stoneCatalog;

        // match-seed-unification — 단일 매치 시드 소유. 맵·웨이브가 여기서 파생된다.
        [Header("Match Seed")]
        [Tooltip("0 이면 매 판 새 시드. 0 이 아니면 재현용 고정 — 맵·웨이브가 매 판 동일.")]
        [SerializeField] private int debugFixedMatchSeed = 0;
        public int MatchSeed { get; private set; }
        private bool _matchSeedFixed;
        public bool MatchSeedFixed => _matchSeedFixed;

        public BattleLogger Logger => logger;
        public DraftController DraftController => draftController;
        // random-map-pool unit 7 — 일시정지 메뉴가 선택된 맵의 실전 웨이브 플랜을 프리뷰하는 통로.
        // bridge 부재/비생성이면 default(waves==null) → MenuPopup 이 마지막 빌드 유지.
        public GeneratedWavePlan BuildBriefingWavePlan()
            => battleBridge != null ? battleBridge.BuildBriefingWavePlan() : default;
        public CostRuntime CostRuntime => costRuntime;
        public PlacementCooldownRuntime CooldownRuntime => cooldownRuntime;
        public CostConfig CostConfig => costConfig;
        public SkillLoadoutController SkillLoadout => skillLoadout;
        public bool IsAiming { get; set; }
        public DefenderUnitData SelectedDefender { get; set; }
        // gimmick-match-integration unit 1 — 매치당 배정된 기믹(없으면 null). GimmickPhaseView 가 읽는다.
        public GimmickData AssignedGimmick { get; private set; }

        public GamePhase CurrentPhase { get; private set; } = GamePhase.None;
        public event System.Action<GamePhase> PhaseChanged;

        // squad-loadout Unit 3 — raised when squad mode is ready for placement
        // (no draft). PlacementPhaseView subscribes, mirroring DraftConfirmed.
        public event System.Action PlacementRequested;

        // squad map-setup — raised after the squad map is built so the player can
        // freely adjust map settings before placement (replaces the draft stage's
        // map panel). SquadPrepView subscribes and calls RequestPlacement() on
        // confirm. If nothing subscribes (headless), squad mode goes straight to
        // placement.
        public event System.Action MapSetupRequested;
        public void RequestPlacement() => PlacementRequested?.Invoke();

        // Fired whenever a UI layer wants all *other* aim-style selections to
        // cancel (e.g. picking a defender should cancel any active skill aim,
        // picking a skill slot should clear the pending defender selection).
        // Subscribers clear their own local aim state; publisher does not know
        // who listens. Keeps input modes mutually exclusive — last click wins.
        public event System.Action AimCanceled;
        public void RaiseAimCanceled() => AimCanceled?.Invoke();

        public void SetPhase(GamePhase phase)
        {
            if (CurrentPhase == phase) return;
            CurrentPhase = phase;
            if (phase == GamePhase.Result) RecordMatchPlayed();
            PhaseChanged?.Invoke(phase);
        }

        // outgame-tutorial unit 8 rev — 로비 챕터 D 가 읽는 독립 신호.
        //
        // **세는 기준 = "히스토리에 남는 판"** 이다. 처음엔 "완주한 판" 으로 잡았는데 그건
        // 틀렸다: `나가기`(MenuPopup.OnExit)는 Result 를 거치지 않고 씬을 떠나지만
        // `AbandonMatch` 가 0점으로 마감해 **그 판도 자기 엔트리로 히스토리에 남는다**
        // (TournamentMatchReporter 주석). 챕터 D 가 가르치는 게 바로 그 히스토리이므로,
        // 카운터와 안내가 같은 것을 세야 한다. 그래서 호출처가 둘이다 —
        // `SetPhase(Result)` 와 `MenuPopup.OnExit`.
        //
        // Test Mode 도 엔드리스도 포함한다. `ReportMatchResult` 가 `IsEndless` 를 배제하는 것과
        // 대칭이 아닌 건 의도다(그쪽은 토너먼트 집계라 모드를 가려야 하고, 이쪽은 "판을
        // 해봤나" 라는 경험 신호다). 덧붙여 Test Mode 는 **가릴 수도 없다**: TestModeContext 는
        // 배치 전 StartTestModeMatch 에서 1회 소비돼 Result 시점엔 이미 Active=false 라,
        // 여기에 그 가드를 넣으면 아무 일도 하지 않는 죽은 코드가 된다.
        //
        // 호출처가 둘이 되면서 래치가 필수다. `GameManager` 는 배틀 스코프라 인스턴스 수명이
        // 곧 한 판이므로 평범한 필드로 충분하다. (dormant 인 `OnRestartRequested` 가 되살아나
        // 같은 씬에서 판을 다시 돌리게 되면 그때 이 래치를 리셋해야 한다.)
        private bool _matchRecorded;

        // 교체 가능한 저장 seam. 이 프로젝트의 프로필 쓰기 주체는 전부 이걸 갖는다
        // (FirstSessionTutorialController · OutgameTutorialController · SquadCharacterPageController).
        // 없으면 증가 로직에 테스트를 붙일 때 개발자의 실제 profile.json 을 재작성하게 된다.
        [System.NonSerialized] internal System.Action<PlayerProfile> ProfileSaver = ProfileStore.Save;

        /// 이 판을 "플레이한 판" 으로 기록한다. 판당 1회만 먹히므로 두 종료 경로가 함께 불러도 안전하다.
        public void RecordMatchPlayed()
        {
            if (_matchRecorded) return;
            // 이번 세션에 로드된 프로필일 때만 쓴다 — BattleScene 직접 Play 는 프로필이 없고,
            // 그때 Save 하면 빈 인메모리 상태가 디스크의 스쿼드·덱을 덮는다.
            if (profileSO == null || !profileSO.IsLoadedThisSession || profileSO.profile == null)
            {
                // 조용히 빠지면 "가드에 막혔나 / 아예 안 불렸나" 를 구분할 수 없다(이 결함을
                // 실제로 진단하지 못했던 이유다). 정상 경로에선 안 울린다.
                Debug.Log("[GameManager] matchesPlayed 기록 생략 — 이번 세션에 로드된 프로필이 아니다.", this);
                return;
            }
            _matchRecorded = true;
            profileSO.profile.matchesPlayed++;
            try
            {
                (ProfileSaver ?? ProfileStore.Save)(profileSO.profile);
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[GameManager] matchesPlayed 저장 실패 — 무시하고 진행합니다: {e.Message}", this);
            }
        }

        // 타겟 프레임 60 고정 — 앱 전역 관심사라 씬/인스턴스와 무관하게 앱 시작 시 1회만 세팅한다.
        // GameManager 는 battle-scoped 라 Awake 로는 OutgameScene 콜드 스타트를 못 잡는다
        // (로비가 첫 씬이면 세팅 코드가 안 걸려 플랫폼 기본값 30fps 로 떨어짐).
        // BeforeSceneLoad 훅은 첫 씬 로드 전 1회 호출되고 인스턴스 존재 여부와 무관하다.
        // vSyncCount=0 이어야 90/120Hz 패널에서 vSync 가 targetFrameRate 를 덮어쓰지 않고
        // 60 캡이 확실히 적용된다(모바일 배터리 절감).
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void ApplyFrameRateCap()
        {
            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = 60;
        }

        private void Awake()
        {
            // 비율 고정 + 영역만 확장: 세로를 1080 으로 캡하고 가로는 물리 화면 aspect 로 계산.
            // 과거 SetResolution(1920,1080,true) 은 기기 aspect(20:9 등)와 달라 16:9 프레임버퍼가
            // 물리 화면에 비균등 stretch → 모든 오브젝트가 가로로 찌그러짐. 세로 기준 캡이라
            // 고DPI 다운스케일(GPU 부하 절감)은 유지하되, aspect 는 기기 그대로 → 왜곡 없이 가로만 확장.
            int sysW = Display.main.systemWidth;
            int sysH = Display.main.systemHeight;
            if (sysW > 0 && sysH > 0)
            {
                int targetH = Mathf.Min(1080, sysH);   // 네이티브보다 위로 올리지 않음(업스케일 방지)
                int targetW = Mathf.RoundToInt(targetH * ((float)sysW / sysH));
                Screen.SetResolution(targetW, targetH, true);
            }

            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            // outgame-scene-and-flow Unit 3 — GameManager is battle-scoped and
            // non-persistent. Loading OutgameScene must tear it down so each
            // battle re-entry starts fresh (no stale singleton / state leak).
            // (Previously DontDestroyOnLoad — meaningless in the old single-scene
            // setup, harmful in the two-scene flow.)

            if (logger == null)
            {
                Debug.LogWarning("[GameManager] BattleLogger reference missing. Attempting GetComponentInChildren.");
                logger = GetComponentInChildren<BattleLogger>();
            }
            if (costRuntime == null) costRuntime = GetComponentInChildren<CostRuntime>();
            if (cooldownRuntime == null) cooldownRuntime = GetComponentInChildren<PlacementCooldownRuntime>();
            if (costRuntime != null && costConfig != null)
            {
                costRuntime.Configure(costConfig.startingCost, costConfig.maxCost, costConfig.regenPerSec);
            }
            if (skillLoadout == null) skillLoadout = GetComponentInChildren<SkillLoadoutController>();
            if (skillLoadout == null)
            {
                skillLoadout = gameObject.AddComponent<SkillLoadoutController>();
                Debug.LogWarning("[GameManager] SkillLoadoutController missing; added fallback component on GameManager.", this);
            }
        }

        private void OnEnable()
        {
            if (logger != null) logger.StartSession();
            // tournament-flow-guards unit 8 — adopt-only: 로비 게이트가 발행한 attempt 를
            // 채택하고, 로비를 거치지 않은 진입(TestMode/에디터 직접 Play)은 상태 리셋만
            // 될 뿐 attempt 가 생기지 않는다(발행 창구는 BeginMatchFromLobby 하나).
            Wassup.Core.Api.TournamentMatchReporter.BeginMatch();
        }

        // match-seed-unification — 매치당 1회: 고정 노브 우선, 아니면 새 랜덤 시드.
        // BattleBridge 에 주입하면 맵·웨이브가 이 시드에서 파생된다(작업 2/3).
        private void EnsureMatchSeed()
        {
            // battle-sim-extraction unit 2 — 하네스 시드 최우선("같은 seed 2회 실행" 계약).
            // 라이브/일반 테스트에선 0 이라 기존 우선순위(고정 노브 → 랜덤) 그대로다.
            int harnessSeed = TestModeContext.ConsumeHarnessSeed();
            int fixedSeed = harnessSeed != 0
                ? harnessSeed
                : debugFixedMatchSeed;
            _matchSeedFixed = fixedSeed != 0;
            MatchSeed = fixedSeed != 0 ? fixedSeed : Wassup.Core.MatchSeed.GenerateRandom();
            if (battleBridge != null) battleBridge.SetMatchSeed(MatchSeed);
            if (logger != null) logger.SetMatchSeeds(MatchSeed, MatchSeedFixed);
            Debug.Log($"[GameManager] matchSeed={MatchSeed} (fixed={fixedSeed != 0})");
        }

        // gimmick-match-integration unit 1 — 매치당 1회 기믹 배정(모든 진입 경로 공통, 모든
        // PrepareDraftMap/StartBattle 이전). BattleConfig 게이트: 비활성/빈 pool → null(클린 플레이).
        // 결정론: 같은 matchSeed → 같은 기믹. Restart(씬 리로드 없음)는 초기 배정 유지(재호출 없음).
        private void AssignGimmick()
        {
            AssignedGimmick = null;
            if (battleConfig != null && battleConfig.gimmickEnabled)
            {
                var pool = battleConfig.gimmickPool;
                if (pool != null && pool.Length > 0)
                {
                    int idx = Wassup.Core.GimmickSelection.PickIndex(
                        pool.Length, (uint)Wassup.Core.MatchSeed.DeriveGimmickSeed(MatchSeed));
                    if (idx >= 0) AssignedGimmick = pool[idx];
                }
                else
                {
                    Debug.LogWarning("[GameManager] gimmickEnabled=true 이지만 gimmickPool 이 비어 있다 — 기믹 없이 진행.");
                }
            }
            if (battleBridge != null) battleBridge.SetAssignedGimmick(AssignedGimmick);
            Debug.Log($"[GameManager] gimmick={(AssignedGimmick != null ? AssignedGimmick.gimmickId : "none")}");
        }

        // Start runs after all Awake/OnEnable of peers, so DraftView has subscribed
        // to DraftController events before we emit DraftStarted.
        // 탭↔드래그 임계 DPI 보정 (1회, 부팅). EventSystem 기본값은 10px 고정이라 고DPI 기기에서
        // 물리적으로 ~0.6mm 밖에 안 된다 → 탭 중 손가락 미세 흔들림이 드래그로 오인식되고,
        // OnPointerClick 이 발화하지 않는다(탭 배치 arm·카드 탭 등이 통째로 안 먹음). DPI 기준 ~4mm 로 보정.
        // Max 로 적용해 임계를 절대 낮추지 않는다. Screen.dpi 는 미지원 플랫폼에서 0 → 그 경우 기본값 유지.
        private static void CalibrateDragThreshold()
        {
            var es = UnityEngine.EventSystems.EventSystem.current;
            if (es == null || Screen.dpi <= 0f) return;
            es.pixelDragThreshold = Mathf.Max(es.pixelDragThreshold, Mathf.RoundToInt(Screen.dpi / 6f));
        }

        private void Start()
        {
            CalibrateDragThreshold();

            // match-seed-unification — 맵을 빌드하는 PrepareDraftMap 보다 먼저 매치 시드를
            // 확정·주입한다. Draft·Squad 양 경로 공통으로 여기서 1회 보장.
            EnsureMatchSeed();
            AssignGimmick();

            // wave-authoring-test-mode unit 3 — 테스트 모드 최상위 분기. 작성 플랜 +
            // (아래 분기는 return 하므로 DPI 보정은 반드시 그 앞에서 1회 수행 — 위 CalibrateDragThreshold)
            // 디펜더 프리셋으로 드래프트/스쿼드를 모두 건너뛴다. 비활성이면 무변경.
            if (TestModeContext.Active && battleBridge != null)
            {
                StartTestModeMatch();
                return;
            }

            // squad-loadout Unit 3 — squad mode takes priority. Empty/unset squad
            // falls through to the existing draft path (A non-destructive).
            var squad = (profileSO != null && profileSO.profile != null) ? profileSO.profile.CommittedSquad() : null;
            if (squad != null && !squad.IsEmpty() && battleBridge != null && catalog != null)
            {
                StartSquadMatch(squad);
                return;
            }

            // page-local-presets unit 5 — 확정 프리셋이 비어 있으면 아래 draft 폴백으로
            // 떨어진다. 로비에서는 LoadoutGate 가 START 를 막으므로 도달 불가이고, 남은
            // 도달 경로는 게이트를 우회하는 것들(테스트 모드, 에디터에서 BattleScene 직접
            // Play)이다. 폴백 자체는 프리셋보다 오래된 안전망이라 이번 spec 에서 바꾸지
            // 않되, 조용히 다른 모드로 갈아타 원인 추적이 어려웠던 것만 로그로 드러낸다.
            if (squad == null || squad.IsEmpty())
                Debug.Log("[GameManager] 확정 스쿼드 프리셋이 비어 있음 — 레거시 draft 폴백으로 진입한다. "
                    + "(로비 START 는 LoadoutGate 가 막으므로 이 경로는 게이트 우회 진입이다.)");

            if (draftController != null)
            {
                // draft-stage-map-prebuild Unit 2 — build the map before entering Draft
                // so the playfield renders behind the card fan. (map-pipeline-cleanup:
                // 옵션 토글→RebuildDraftMap 경로는 디버그 패널과 함께 은퇴.)
                logger?.SetEntryMode("draft");
                if (battleBridge != null) battleBridge.PrepareDraftMap();

                SetPhase(GamePhase.Draft);
                draftController.BeginDraft();
            }
            else if (battleBridge != null)
            {
                Debug.LogWarning("[GameManager] draftController unset; starting battle with inspector defenderPool fallback.");
                logger?.SetEntryMode("direct");
                SetPhase(GamePhase.Battle);
                battleBridge.StartBattle();
            }
        }

        // squad-loadout Unit 3 — skip the draft and bring the selected squad
        // straight into placement. Deterministic: exactly the saved squad units
        // (rev 2026-06-05 — no random fill).
        private void StartSquadMatch(SquadPreset squad)
        {
            logger?.SetEntryMode("squad");
            // squad-loadout regression fix — build the themed map (tile style +
            // background props) before placement, exactly as the draft path does
            // in Start(). Without this the BeginPlacement fallback left the map
            // unstyled and prop-less. BeginPlacement then sees the map created and
            // skips its rebuild.
            battleBridge.PrepareDraftMap();

            var ids = SquadDraw.Resolve(squad.unitIds);
            var units = new List<DefenderUnitData>(ids.Count);
            foreach (var id in ids)
            {
                var u = catalog.ById(id);
                if (u != null) units.Add(u);
            }
            if (units.Count == 0)
            {
                Debug.LogWarning("[GameManager] squad resolved to no units; falling back to draft.");
                if (draftController != null)
                {
                    battleBridge.PrepareDraftMap();
                    SetPhase(GamePhase.Draft);
                    draftController.BeginDraft();
                }
                return;
            }
            battleBridge.SetDefenderPool(units.ToArray());
            LogSquadCarryIn(squad, units);

            // dreamstone-loadout Unit 3 — set-then-apply: stage the squad's equipped
            // stones now; BattleBridge applies them once BeginPlacement clears+reapplies
            // its match-effect registry (see BattleBridge.BeginPlacement/SetDreamstones).
            battleBridge.SetDreamstones(ResolveEquippedStones(squad));
            // dreamstone-loadout Unit 6 — CostRate stones route to CostRuntime instead
            // of the entity registry above. Match-entry is one of the only two call
            // sites allowed to touch this multiplier (see CostRuntime's contract
            // comment) — ResetToStart/Configure (called every placement entry,
            // including mid-match Restart) must never set it.
            if (costRuntime != null) costRuntime.SetRegenRateMultiplier(ResolveCostRateMultiplier(squad));
            LogDreamstoneCarryIn(squad);
            PersistTournamentDeckSnapshot();

            // Skills stay independent of units — roll a fresh loadout like draft does.
            if (skillLoadout != null)
            {
                skillLoadout.Roll();
                LogSkillLoadout();
                if (skillLoadout.Picked.Count > 0)
                {
                    var arr = new SkillData[skillLoadout.Picked.Count];
                    for (int i = 0; i < arr.Length; i++) arr[i] = skillLoadout.Picked[i];
                    battleBridge.SetSkillLoadout(arr);
                }
            }

            // squad map-setup — let the player adjust the map first if a prep view
            // is present; otherwise go straight to placement (headless/tests).
            if (MapSetupRequested != null) MapSetupRequested.Invoke();
            else PlacementRequested?.Invoke();
        }

        // wave-authoring-test-mode unit 3 — 드래프트/스쿼드를 스킵하고 작성 플랜 +
        // 디펜더 프리셋으로 바로 placement 진입. StartSquadMatch 미러.
        private void StartTestModeMatch()
        {
            logger?.SetEntryMode("test", testMode: true);
            var plan = TestModeContext.Plan;
            var fallbackPreset = TestModeContext.DefenderPreset;
            TestModeContext.Clear(); // 1회 소비

            battleBridge.PrepareDraftMap();

            battleBridge.SetAuthoredWavePlan(plan);

            // 디펜더는 기존에 저장된 스쿼드를 그대로 반입(StartSquadMatch 와 동일 해석).
            // 스쿼드가 비어 있을 때만 TestModeConfig 프리셋으로 폴백.
            var defenders = ResolveSquadDefenders();
            if ((defenders == null || defenders.Length == 0) && fallbackPreset != null && fallbackPreset.Length > 0)
                defenders = fallbackPreset;

            if (defenders != null && defenders.Length > 0)
            {
                battleBridge.SetDefenderPool(defenders);
                var squadForLog = (profileSO != null && profileSO.profile != null) ? profileSO.profile.CommittedSquad() : null;
                LogSquadCarryIn(squadForLog, new List<DefenderUnitData>(defenders));
            }
            else
                Debug.LogWarning("[GameManager] 테스트 모드 디펜더 없음 — 저장 스쿼드/프리셋 모두 비어 있음.");

            // dreamstone-loadout Unit 3 — StartSquadMatch 미러. 스톤은 스쿼드 소속이라
            // 디펜더가 프리셋으로 폴백해도(위 defenders) 저장 스쿼드의 장착 스톤은 그대로 반입한다.
            var stoneSquad = (profileSO != null && profileSO.profile != null) ? profileSO.profile.CommittedSquad() : null;
            battleBridge.SetDreamstones(ResolveEquippedStones(stoneSquad));
            // dreamstone-loadout Unit 6 — StartSquadMatch 미러 (CostRate 분리 적용).
            if (costRuntime != null) costRuntime.SetRegenRateMultiplier(ResolveCostRateMultiplier(stoneSquad));
            LogDreamstoneCarryIn(stoneSquad);
            PersistTournamentDeckSnapshot();

            // Skills stay independent of units — roll a fresh loadout like draft does.
            if (skillLoadout != null)
            {
                skillLoadout.Roll();
                LogSkillLoadout();
                if (skillLoadout.Picked.Count > 0)
                {
                    var arr = new SkillData[skillLoadout.Picked.Count];
                    for (int i = 0; i < arr.Length; i++) arr[i] = skillLoadout.Picked[i];
                    battleBridge.SetSkillLoadout(arr);
                }
            }

            Debug.Log($"[GameManager] 테스트 모드 진입 — plan='{(plan != null ? plan.displayName : "NULL")}' defenders={(defenders != null ? defenders.Length : 0)}.");

            // squad 와 동일하게 MAP SETUP 스텝이 있으면 거치고, 없으면 바로 placement.
            if (MapSetupRequested != null) MapSetupRequested.Invoke();
            else PlacementRequested?.Invoke();
        }

        // wave-authoring-test-mode — 저장 스쿼드를 디펜더 배열로 해석(StartSquadMatch 미러).
        // 스쿼드 없음/빈 경우 null 반환(호출부가 프리셋 폴백 판단).
        private DefenderUnitData[] ResolveSquadDefenders()
        {
            var squad = (profileSO != null && profileSO.profile != null) ? profileSO.profile.CommittedSquad() : null;
            if (squad == null || squad.IsEmpty() || catalog == null) return null;

            var ids = SquadDraw.Resolve(squad.unitIds);
            var units = new List<DefenderUnitData>(ids.Count);
            foreach (var id in ids)
            {
                var u = catalog.ById(id);
                if (u != null) units.Add(u);
            }
            return units.ToArray();
        }

        private void LogSquadCarryIn(SquadPreset squad, List<DefenderUnitData> units)
        {
            if (logger == null || units == null) return;

            var ids = new List<string>();
            if (squad != null && squad.unitIds != null)
                foreach (var id in squad.unitIds)
                    if (!string.IsNullOrEmpty(id)) ids.Add(id);

            var names = new List<string>();
            foreach (var unit in units)
                if (unit != null) names.Add(unit.displayName);

            logger.SetSquad(squad != null ? squad.id : string.Empty, squad != null ? squad.name : string.Empty, ids, names);
        }

        private void LogDreamstoneCarryIn(SquadPreset squad)
        {
            if (logger == null) return;

            var records = new List<DreamstoneRecord>();
            if (squad != null && squad.stoneIds != null && stoneCatalog != null)
            {
                for (int i = 0; i < squad.stoneIds.Count; i++)
                {
                    var id = squad.stoneIds[i];
                    if (string.IsNullOrEmpty(id)) continue;
                    var stone = stoneCatalog.ById(id);
                    if (stone == null)
                    {
                        // tournament-deck-info unit 5 — 미해석 id 를 버리지 않는다. 버리면
                        // 4개 장착한 판이 서버 기록엔 2개로 남고, 로비 게이트는 스톤을
                        // 검사하지 않으므로 실제 토너먼트 판에서도 벌어진다(시트에 새 스톤이
                        // 생겼는데 로컬 SO 가 stale 한 빌드). 유닛이 raw id 를 남기는 것과
                        // 같은 정책 = 계약 3(미해석 id 는 그 슬롯만 폴백).
                        Debug.LogWarning($"[GameManager] 드림스톤 '{id}' 를 카탈로그에서 찾지 못했다 — id 만 기록한다(슬롯 {i}).");
                        records.Add(new DreamstoneRecord
                        {
                            id = id,
                            name = id,             // 표시명 해석은 수신 측 카탈로그의 몫
                            grade = string.Empty,
                            kind = string.Empty,
                            percent = 0f,
                            slotIndex = i,
                        });
                        continue;
                    }
                    records.Add(new DreamstoneRecord
                    {
                        id = stone.id,
                        name = stone.displayName,
                        grade = stone.grade.ToString(),
                        kind = stone.effect.kind.ToString(),
                        percent = stone.effect.percent,
                        slotIndex = i,
                    });
                }
            }

            logger.SetDreamstones(records);
        }

        // tournament-deck-info unit 4 — 캐리인 시점의 덱(유닛·스톤)을 pending 레코드에
        // 적어둔다. 카드는 배치 진입에서 확정되며 DreamcatcherHandController 가 같은
        // 통로로 갱신한다(payload 단조 증가). 여기서 한 번 적어두면 배치 전 하드킬도
        // 유닛·스톤이 남은 채 reconcile 된다.
        private void PersistTournamentDeckSnapshot()
        {
            if (logger == null) return;
            Wassup.Core.Api.TournamentMatchReporter.PersistMatchDeck(logger.DeckInfoJson());
        }

        private void LogSkillLoadout()
        {
            if (logger == null || skillLoadout == null) return;

            var pickedIds = new List<string>();
            foreach (var skill in skillLoadout.Picked)
                if (skill != null) pickedIds.Add(skill.id);
            logger.SetSkillLoadout(pickedIds);

            var poolIds = new List<string>();
            foreach (var skill in skillLoadout.Pool)
                if (skill != null) poolIds.Add(skill.id);
            logger.SetSkillPool(poolIds, skillLoadout.Seed);
        }

        // dreamstone-loadout Unit 3 — resolve a squad's equipped stoneIds to assets
        // via the catalog, for the entity-buff path (BattleBridge.SetDreamstones).
        // Missing catalog/squad/list, or an id the catalog no longer has (asset
        // deleted), are skipped — same "resolve at read time, don't fail storage"
        // policy as ResolveSquadDefenders/SquadDraw use for unitIds.
        // dreamstone-loadout Unit 6 — CostRate stones are excluded here; they have
        // no entity stat and route to CostRuntime instead (ResolveCostRateMultiplier).
        private List<DreamstoneData> ResolveEquippedStones(SquadPreset squad)
        {
            var stones = new List<DreamstoneData>();
            if (squad == null || squad.stoneIds == null || stoneCatalog == null) return stones;
            foreach (var id in squad.stoneIds)
            {
                if (string.IsNullOrEmpty(id)) continue;
                var stone = stoneCatalog.ById(id);
                if (stone != null && stone.effect.kind != CardBuffKind.CostRate) stones.Add(stone);
            }
            return stones;
        }

        // dreamstone-loadout Unit 6 — sum equipped CostRate stone percents into a
        // CostRuntime regen multiplier (1 + Σ%/100). Same resolve policy as
        // ResolveEquippedStones (skip missing catalog/squad/list/unresolved ids).
        // Only match-entry call sites (StartSquadMatch/StartTestModeMatch) call
        // this — see CostRuntime.SetRegenRateMultiplier for the full ownership
        // contract (ResetToStart/Configure must never touch the multiplier).
        private float ResolveCostRateMultiplier(SquadPreset squad)
        {
            float sum = 0f;
            if (squad == null || squad.stoneIds == null || stoneCatalog == null) return 1f;
            foreach (var id in squad.stoneIds)
            {
                if (string.IsNullOrEmpty(id)) continue;
                var stone = stoneCatalog.ById(id);
                if (stone != null && stone.effect.kind == CardBuffKind.CostRate) sum += stone.effect.percent;
            }
            return 1f + sum / 100f;
        }

        private void OnDisable()
        {
            if (battleBridge != null) battleBridge.StopBattle();
            if (logger != null) logger.EndSession();
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }
    }
}
