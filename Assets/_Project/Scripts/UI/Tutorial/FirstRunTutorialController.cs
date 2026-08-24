using System.Collections;
using UnityEngine;
using Unity.Entities;
using Wassup.Bridge;
using Wassup.Core;
using Wassup.Core.TimeControl;
using Wassup.Data;

namespace Wassup.UI.Tutorial
{
    // first-run-tutorial units 3~6 — 계정 첫 판 온보딩의 배틀 구간.
    //
    // 이 컨트롤러는 **게임 규칙을 하나도 소유하지 않는다**(계약 1). 배치·부착·배치 스킬·
    // 코스트·게이지는 전부 기존 경로가 처리하고, 여기서 정하는 것은 «언제 멈출지»와
    // «무엇을 열어둘지» 둘뿐이다. 대신 눌러주는 동작은 없다.
    public sealed class FirstRunTutorialController : MonoBehaviour
    {
        // units 0~7 의 문구는 사용자 원문 그대로다(띄어쓰기 포함). unit 9 가 새로 넣은
        // 문구는 초안이며 조정 가능하다.
        //
        // ⚠ **유닛 이름이 박힌 문구는 배선된 유닛과 같아야 한다.** 포맷(`{0}`)으로 빼지
        // 않은 이유는 한국어 조사다 — «말파이트는 / 샷건맨은», «말파이트를 / 샷건맨을» 처럼
        // 받침에 따라 갈려서 한 포맷으로는 문법이 깨진다. 대신 이름이 필요 없는 자리
        // (선택 유도)는 조사 중립형 포맷을 쓴다.

        // 공통
        private const string PlaceableText = "배치가능영역";
        private const string BlockedText = "배치 불가 영역";
        private const string GoalText = "게임목표: 최대한 많은 악몽 처치";
        private const string GoalIntroText = "스폰된 악몽은 목표지점을 향합니다.";
        private const string DefendIntroText = "유닛의 배치와 드림캐쳐의 조합으로 악몽을 막아보세요";
        private const string GoalMarkerLabel = "목표지점";
        private const string EnemyApproachText = "악몽이 배치 영역 안으로 들어오면!";

        // B3a — 말파이트(주변 2타일 3초 광역 스턴)
        private const string PickText = "유닛을 터치 해보세요";
        private const string PrimarySkillText = "말파이트는 주변 악몽을 3초간 기절시킵니다!";
        private const string PrimaryPlaceText = "악몽 무리 위에 말파이트를 배치 해보세요!";
        private const string PrimaryDoneText = "배치 스킬은 놓는 즉시 한 번 터집니다";

        // B3b — 퇴근. ⚠ 문구의 «철수» 는 **버튼 라벨과 같은 말이어야 한다**
        // (`DcInspectController.RetireLabel`). 기능 이름은 퇴근(defender-clock-out)이지만
        // 화면에 뜨는 글자가 정본이다 — 안내가 «퇴근», 버튼이 «철수» 면 무엇을 누르라는지가
        // 어긋난다. 라벨을 바꾸면 이 줄도 같이 바꾼다.
        private const string RetireHintText = "지친 유닛은 철수시켜 자리를 비울 수 있습니다";

        // B3c — 샷건맨(가까운 적 쪽 부채꼴 산탄 + 밀어냄)
        private const string SecondaryPickFormat = "이번엔 {0} 유닛을 터치 해보세요";
        private const string SecondarySkillText = "샷건맨은 가까운 악몽들을 밀어냅니다!";
        private const string SecondaryPlaceText = "빈 자리에 샷건맨을 배치 해보세요!";
        private const string OnPlaceText = "강력한 배치스킬들을 활용하여 전황을 유리하게 이끌어 보세요";

        // B4 — 드림캐쳐 부착
        private const string JarHintText = "충분한 양의 에너지를 모았네요!";
        private const string ReselectFormat = "다시 {0} 유닛을 선택 해보세요";
        private const string SelectHostFormat = "{0} 유닛을 선택 해보세요";
        private const string CardText = "하단 드림캐쳐 4개중 맘에 드는것을 터치 해보세요";
        private const string CardFallbackText = "하단 드림캐쳐 중 맘에 드는것을 터치 해보세요";
        private const string AttachDoneText = "드림캐쳐를 유닛에게 부착하여 더 강해질 가능성을 열어보세요!";

        // B5 — 남은 시간 자유 플레이. 안내와 포커스만 남기고 입력·전투는 막지 않는다.
        private const string SurvivalText =
            "유닛 배치, 드림캐쳐등 기능을 활용하여 튜토리얼 1분을 버텨보세요!";

        [SerializeField] private PlayerProfileSO profileSO;
        [SerializeField] private FirstRunTutorialConfig config;
        [Tooltip("B3a·B3b 가 가리킬 첫 유닛(말파이트). 배치 스킬 문구가 이 유닛의 효과를 " +
                 "주장하므로 바꾸면 PrimarySkillText·PrimaryPlaceText 도 같이 바꾼다.")]
        [SerializeField] private DefenderUnitData primaryUnit;
        [Tooltip("B3c 가 가리킬 두 번째 유닛(샷건맨). 퇴근으로 빈 자리를 메운다. " +
                 "SecondarySkillText·SecondaryPlaceText 가 이 유닛을 주장한다. " +
                 "**B4 의 부착 대상도 이쪽이 우선**이다.")]
        [SerializeField] private DefenderUnitData secondaryUnit;

        [Header("안내 도구 (teardown 이 남긴 것만 쓴다)")]
        [SerializeField] private TutorialGuidanceView guidance;
        [Tooltip("Guidance 와 **다른 GameObject** 여야 한다 — 둘 다 자기 캔버스를 만들어 sortingOrder 를 다툰다.")]
        [SerializeField] private OutgameTutorialOverlay overlay;

        [Header("게임 계층")]
        [SerializeField] private GameManager gameManager;
        [SerializeField] private BattleBridge bridge;
        [Tooltip("B5가 포커스할 좌상단 시간 배지의 소유자.")]
        [SerializeField] private ScoreHudView scoreHud;
        [SerializeField] private PlacementPhaseView placementPhaseView;
        [SerializeField] private DefenderSelector defenderSelector;
        [SerializeField] private DreamcatcherHandView handView;
        [SerializeField] private DreamcatcherHandController handController;
        [Tooltip("각성 항아리 독. 4.0 에서 «에너지가 찼다» 를 가리킨다. 비면 그 구간을 건너뛴다.")]
        [SerializeField] private AwakeningGaugeView gaugeView;
        [Tooltip("유닛 인스펙트 패널. B3b 가 여기서 «퇴근» 버튼 rect 를 얻는다. 비면 퇴근 구간을 건너뛴다.")]
        [SerializeField] private DcInspectPanelView inspectPanel;
        [SerializeField] private Camera boardCamera;

        private bool _running;
        private bool _briefingDone;
        private bool _battleStarted;

        // 정지 lease 는 **구간**이 소유한다(계약 7). 성공·타임아웃·스킵·취소·씬 이탈이 전부
        // 아래 Unfreeze 하나를 지난다 — 해제를 성공 경로에만 걸면 스텝 하나를 흘려보낸
        // 순간 판이 0배속으로 남는다. EndMatch 에는 TimeManager.ResetAll 이 없다.
        private TimeLease _freeze;
        private bool _frozen;

        // 스텝 완료 신호
        private bool _armed;
        private bool _placed;
        private bool _selectionSet;
        private bool _retired;
        // ⚠ 지금 어느 유닛의 배치를 기다리는가. 두 유닛이 같은 `_placed`/`_armed` 를 쓰므로
        // 이것이 없으면 «샷건맨을 놓으세요» 구간에서 다른 유닛을 놓아도 통과한다
        // (그 구간은 차단막을 내리므로 실제로 가능한 조작이다).
        private DefenderUnitData _awaitingUnit;
        private int _attachBaseline = -1;
        private Entity _hostEntity = Entity.Null;

        // 구간이 «제대로» 끝났는가. 하나라도 스킵/타임아웃이면 완료로 기록하지 않는다(계약 11).
        private bool _b3Completed;
        private bool _b4Completed;
        private bool _b5Completed;


        private void Start()
        {
            if (profileSO == null || config == null || guidance == null || overlay == null)
            {
                enabled = false;
                return;
            }
            if (!profileSO.IsLoadedThisSession || !FirstRunTutorialConfig.ShouldRun(profileSO.profile))
            {
                enabled = false;
                return;
            }
            _running = true;
            if (gameManager != null) gameManager.PhaseChanged += OnPhaseChanged;
            // 배치 페이즈는 이 컨트롤러의 Start 보다 먼저 열릴 수 있다 — 그 경우도 잡는다.
            if (gameManager != null && gameManager.CurrentPhase == GamePhase.Placement) OnPhaseChanged(GamePhase.Placement);
        }

        private void OnDestroy()
        {
            if (gameManager != null) gameManager.PhaseChanged -= OnPhaseChanged;
            Unsubscribe();
            Unfreeze();
            if (placementPhaseView != null) placementPhaseView.ReleaseIntroHold();
        }

        private void OnDisable() => Unfreeze();

        private void OnPhaseChanged(GamePhase phase)
        {
            if (!_running) return;
            if (phase == GamePhase.Placement && !_briefingDone)
            {
                _briefingDone = true;
                StartCoroutine(RunBriefing());
            }
            else if (phase == GamePhase.Battle && !_battleStarted)
            {
                _battleStarted = true;
                StartCoroutine(RunBattle());
            }
            else if (phase == GamePhase.Result || phase == GamePhase.Tally)
            {
                // ⚠ 판이 먼저 끝날 수 있다. 온보딩 판은 60초이고 스텝에는 타임아웃이 없어
                // (계약 11) 대기 중인 코루틴은 **스스로 깨어나지 않는다** — 끊어주지 않으면
                // 딤과 0배속이 결과 화면 위에 그대로 남는다.
                //
                // Close 가 완료 기록까지 판정한다: 직전에 B4 를 끝냈으면 기록되고, 대기 중에
                // 끝났으면 기록되지 않아 다음 판에 처음부터 다시 뜬다.
                StopAllCoroutines();
                Close();
            }
        }

        // ── B1 맵 설명 ──────────────────────────────────────────────────────
        //
        // 카운트다운을 붙잡아 두고 돈다. 전투가 시작되지 않았고 적도 없으므로 정지가
        // 필요 없고, 입력은 홀드의 전면 차단막이 이미 막고 있다. 딤도 쓰지 않는다 —
        // 보여줄 것이 보드 전체라 그 위를 덮으면 무의미하다.
        private IEnumerator RunBriefing()
        {
            if (placementPhaseView != null) placementPhaseView.BeginIntroHold(config.introHoldMaxSeconds);

            for (int i = 0; i < config.briefingCycles; i++)
            {
                ShowPlaceable();
                guidance.ShowMessage(PlaceableText, false);
                yield return WaitUnscaled(config.briefingHoldSeconds);
                ShowBlocked();
                guidance.ShowMessage(BlockedText, false);
                yield return WaitUnscaled(config.briefingHoldSeconds);
            }
            ClearHighlights();
            guidance.ShowMessage(GoalText, false);
            yield return WaitUnscaled(config.goalMessageSeconds);
            guidance.Hide();

            if (placementPhaseView != null) placementPhaseView.ReleaseIntroHold();
        }

        private void ShowPlaceable()
        {
            if (bridge == null) return;
            bridge.HideBlockedHighlight();
            bridge.ShowPlacementHighlight(primaryUnit);
        }

        private void ShowBlocked()
        {
            if (bridge == null) return;
            bridge.HidePlacementHighlight();
            bridge.ShowBlockedHighlight(primaryUnit);
        }

        private void ClearHighlights()
        {
            if (bridge == null) return;
            bridge.HidePlacementHighlight();
            bridge.HideBlockedHighlight();
        }

        // ── B3 · B4 ─────────────────────────────────────────────────────────
        private IEnumerator RunBattle()
        {
            // 계약 5 — GO! 부터 딤이 떠 있다. 이 창을 열어두면 플레이어가 캐논을 먼저 놓거나
            // (maxOnBoard 1) 각성 카드를 먼저 써서(게이지 여유 정확히 0) 아래 구간이
            // 통째로 스킵 조건에 걸린다. 강제는 연출이 아니라 시퀀스의 성립 조건이다.
            DimOnly();

            // B2b 목표 소개 — GO! 직후의 빈 구간을 채운다.
            //
            // 여기는 원래 «아무 입력도 안 되는데 아무 말도 안 하는» 4초였다. 첫 판 유저에게
            // 그 침묵은 «멈춘 건가?» 로 읽힌다. 적이 어디로 가는지와 무엇으로 막는지를
            // 이 자리에서 말한다 — 뒤따르는 모든 스텝의 전제다.
            float introSpent = 0f;
            if (bridge != null && boardCamera != null && bridge.TryGetGoalViewAnchor(out var goalWorld))
                guidance.ShowWorldMarker(boardCamera, goalWorld, GoalMarkerLabel, guidance.GoalMarkerColor);
            guidance.ShowMessage(GoalIntroText, false);
            yield return WaitUnscaled(config.goalIntroSeconds);
            introSpent += config.goalIntroSeconds;

            guidance.ShowMessage(DefendIntroText, false);
            yield return WaitUnscaled(config.goalIntroSeconds);
            introSpent += config.goalIntroSeconds;
            guidance.ClearWorldMarkers();

            // 소개가 짧게 끝나면 남은 만큼만 더 기다린다 — 길면 그대로 이어간다.
            if (introSpent < config.battleFreezeAtSeconds)
                yield return WaitUnscaled(config.battleFreezeAtSeconds - introSpent);

            yield return RunB3();
            yield return RunAttach();
            yield return RunSurvivalHint();

            Close();
        }

        // B3 전체 — 세 블록을 순서대로 돌리고, **셋 다 완주해야** 완료로 찍는다(계약 11·19).
        //
        // 블록 하나가 선행조건 부재로 스킵되면 `_b3Completed` 는 거짓으로 남고, 그 판은
        // 완료로 기록되지 않아 다음 판에 처음부터 다시 뜬다. 퇴근을 검증 질문에 넣어놓고
        // 스킵해도 완료로 찍으면 「핵심을 한 번도 못 본 계정」이 그대로 생긴다.
        private IEnumerator RunB3()
        {
            bool a = false, b = false, c = false;

            yield return RunPickAndPlace(
                primaryUnit, waitForApproach: true,
                PickText, PrimarySkillText, PrimaryPlaceText, PrimaryDoneText,
                ok => a = ok);
            if (!a) yield break;

            yield return RunRetire(primaryUnit, ok => b = ok);
            if (!b) yield break;

            // ⚠ 접근 대기는 B3a 의 소유다(계약 17). 여기서 다시 기다리면
            // `AnyEnemyWithinTilesOfGoal` 이 이미 참이라 문구가 0프레임 노출된다.
            yield return RunPickAndPlace(
                secondaryUnit, waitForApproach: false,
                SecondaryUnitName(SecondaryPickFormat), SecondarySkillText, SecondaryPlaceText, OnPlaceText,
                ok => c = ok);
            if (!c) yield break;

            _b3Completed = true;
        }

        private string SecondaryUnitName(string format)
            => string.Format(format, secondaryUnit != null ? secondaryUnit.displayName : "다음");

        // 선택 → (접근 대기) → 무기 소개 → 배치 → 배치 스킬 관람 → 마무리 문구.
        // 유닛과 문구를 인자로 받아 **두 번 돈다**(말파이트 · 샷건맨).
        private IEnumerator RunPickAndPlace(
            DefenderUnitData unit, bool waitForApproach,
            string pickText, string skillHintText, string placeText, string doneText,
            System.Action<bool> report)
        {
            // ⚠ 지불 판정은 **정지 전에** 한다. 정지 중에는 코스트도 쿨타임도 회복되지 않아
            // 「기다리면 가능해진다」가 없다 — 불가면 아예 멈추지 않고 건너뛴다.
            if (unit == null || defenderSelector == null) yield break;
            if (!defenderSelector.IsSlotUsableNow(unit)) yield break;
            if (!defenderSelector.TryGetSlotRect(unit, out var slotRect) || slotRect == null) yield break;

            Freeze();

            // 3.1 유닛 터치
            //
            // ⚠ 플래그는 **블록마다 리셋한다.** 두 유닛이 같은 필드를 공유하므로, 안 지우면
            // 두 번째 블록이 첫 배치의 잔상으로 진입 즉시 통과한다.
            var drag = defenderSelector.DragController;
            _awaitingUnit = unit;
            _armed = false;
            _placed = false;
            if (drag != null)
            {
                drag.Armed += OnArmed;          // 탭 배치
                drag.UserDragStarted += OnDragStarted;  // ⚠ 드래그는 Armed 를 안 낸다 — 둘 다 받아야 한다
                drag.PlacementCommitted += OnPlaced;
            }
            Focus(slotRect, pickText);
            yield return WaitFor(() => _armed || _placed);

            // 3.1b 악몽이 배치 영역 안으로 들어올 때까지 기다린다. **첫 블록만** 돈다.
            //
            // ⚠ 이 대기가 없으면 «적들 위에 배치해보세요» 가 적이 아직 배치 영역 밖에 있을 때
            // 뜬다 — 문장과 화면이 어긋나고, 그렇게 놓은 배치 스킬은 아무도 못 때려서 바로
            // 다음 문구가 통째로 희석된다(실제 Play 에서 그랬다).
            //
            // 입력은 막은 채(차단막 유지, 구멍 없음) **시간만 흘린다** — 정지 상태로 기다리면
            // 적이 영원히 안 온다. 배치 영역 하이라이트를 켜둬서 문구의 «배치 영역» 이
            // 화면에서 실제로 보이게 한다.
            //
            // ⚠ 기준은 **시간이 아니라 사건**이다 — 적이 **내 목표 가까이** 들어오는 순간.
            // 척도는 «내 목표까지의 거리»(approachGoalTiles): 배치 영역 진입은 너무 이르고
            // (Duel 은 배치 영역이 x≤14 라 스폰 직후 걸려 문구가 0초 노출된다), 강(Env 타일)
            // 도달은 아직 멀다. 목표 거리는 «얼마나 들어왔나» 를 직접 말하고, 목표는 모든
            // 맵에 있어 «이 맵엔 기준이 없다» 도 생기지 않는다.
            //
            // ⚠ 두 번째 블록은 이 대기를 **돌지 않는다**(계약 17) — 조건이 이미 참이라
            // 문구가 0프레임 뜬다.
            if (waitForApproach && !_placed && bridge != null)
            {
                ShowPlaceable();
                guidance.ClearFocus();
                DimOnly();
                Unfreeze();
                guidance.ShowMessage(EnemyApproachText, false);
                yield return WaitFor(() => _placed
                    || bridge.AnyEnemyWithinTilesOfGoal(config.approachGoalTiles));
                Freeze();
            }

            // 3.1c 무기 소개 — 왜 «무리 위에» 놓아야 하는지의 근거를 먼저 준다.
            //
            // 적이 몰려 선 화면을 정지시켜 둔 채 읽는다(입력은 차단막이 막는다). 여기서
            // 근거를 안 주면 다음 문구가 «시키니까 하는» 지시로만 남는다.
            //
            // ⚠ 이 문구들은 **유닛 에셋의 값을 주장한다** — 말파이트 `onPlaceDuration: 3`,
            // 샷건맨 `Pattern_Shotgunner_Blast`(밀어냄). 그 값을 바꾸면 문구도 같이 바꾼다.
            if (!_placed)
            {
                guidance.ShowMessage(skillHintText, false);
                yield return WaitUnscaled(config.skillHintSeconds);
            }

            // 3.2 배치 — 어느 칸에 놓든 통과시킨다.
            // 딤+구멍으로는 드롭 칸을 제한할 수 없다(트레이→보드 드래그는 이미 시작된 UGUI
            // 드래그라 딤을 통과하고 드롭 셀은 보드 레이캐스트가 정한다). 문구도 영역 지시다.
            if (!_placed)
            {
                ShowPlaceable();
                guidance.ClearFocus();
                // ⚠ **이 구간만 차단막을 내린다.** 배치는 보드 입력을 요구하는데 차단막은 보드
                // 탭을 막는다(슬롯을 탭해 arm 한 뒤 보드를 탭하는 경로가 그대로 죽는다).
                //
                // `SetHoles(null)` 로 열 수 없다 — 그건 «구멍 없는 풀 dim» 이다(도구의 문서
                // 주석 그대로). 보드는 UGUI 가 아니라 감쌀 RectTransform 이 없고, 애초에
                // 구멍으로는 드롭 «칸» 을 제한할 수도 없다.
                //
                // 대가: 이 짧은 구간에 플레이어가 손패를 열어 각성을 쓰거나 **다른 유닛을
                // 놓을** 수 있다. 배치하는 즉시 끝나는 구간이라 받아들인다 — 막으면 배치
                // 자체가 불가능해진다. 대신 `OnPlaced` 가 **유닛을 확인**해서 엉뚱한 배치로는
                // 넘어가지 않는다.
                //
                // ⚠⚠ **그 유닛 확인이 곧 앱 잠김의 재료다.** 이 구간은 차단막만 내리고
                // **정지는 유지**하는데, 코스트는 Battle 도메인이라 정지 중 1도 회복되지
                // 않는다. 플레이어가 다른 유닛들을 놓아 잔여 코스트를 대상 유닛 밑으로
                // 떨어뜨리면 «그 유닛의 배치» 는 **영영 불가능**해지고, 매치 타이머도 멈춰
                // 있어 Result 전이(계약 13 의 정리 경로)조차 오지 않는다 → 강종 외 탈출구가
                // 없다. 유닛 확인이 없던 시절엔 엉뚱한 배치가 그냥 통과해서 이 상태가
                // 없었다 — 가드를 조이면서 새로 생긴 구멍이다.
                //
                // 그래서 대기 술어에 **«아직 지불 가능한가»** 를 함께 건다(계약 11 —
                // 만족 불가면 기다리지 말고 나간다). 배치에 성공해도 슬롯은 «소진» 이라
                // 이 술어가 참이 되므로, 판정은 반드시 `_placed` 로 한다.
                HideDim();
                guidance.ShowMessage(placeText, false);
                yield return WaitFor(() => _placed || !defenderSelector.IsSlotUsableNow(unit));
                if (!_placed)
                {
                    Debug.Log($"[FirstRunTutorial] '{unit.displayName}' 를 더 이상 놓을 수 없다" +
                              " (코스트/쿨타임) — 이 배치 구간을 건너뛴다.", this);
                    if (drag != null)
                    {
                        drag.Armed -= OnArmed;
                        drag.UserDragStarted -= OnDragStarted;
                        drag.PlacementCommitted -= OnPlaced;
                    }
                    _awaitingUnit = null;
                    ClearHighlights();
                    yield break;
                }
            }
            ClearHighlights();
            if (drag != null)
            {
                drag.Armed -= OnArmed;
                drag.UserDragStarted -= OnDragStarted;
                drag.PlacementCommitted -= OnPlaced;
            }
            _awaitingUnit = null;

            // 3.3 배치 스킬 관람 — **정지를 푼다**(차단막은 유지). 멈춘 채 문구만 띄우면
            // "전황을 유리하게"가 말뿐이 된다. 발동 자체는 기존 경로가 한다.
            guidance.Hide();
            DimOnly();
            Unfreeze();
            yield return WaitUnscaled(config.onPlaceWatchSeconds);

            Freeze();
            guidance.ShowMessage(doneText, false);
            yield return WaitUnscaled(config.goalMessageSeconds);
            report(true);
        }

        // B3b — 놓은 유닛을 **되돌린다**. 튜토리얼은 퇴근 버튼을 가리키기만 하고,
        // 퇴근 자체는 기존 경로(DcInspectController → BattleBridge.RetireDefender)가 한다.
        private IEnumerator RunRetire(DefenderUnitData unit, System.Action<bool> report)
        {
            // 선행조건: 그 유닛이 아직 살아 있고 **트레이 셀로 고를 수 있어야** 한다.
            // 죽었으면 조건이 영영 안 서고 타임아웃이 없다(계약 11) — 진입 전에 건너뛴다.
            // 미배선은 **조용히 지나가면 안 된다**: 이 블록이 스킵되면 `_b3Completed` 가
            // 거짓으로 남아 그 계정은 이후 모든 판이 온보딩 판(60초 · 토너먼트 제외)으로
            // 돈다. 콘솔에 이유가 찍혀야 배선 실수를 알아챌 수 있다.
            if (unit == null) yield break;
            if (inspectPanel == null || handView == null)
            {
                Debug.LogWarning("[FirstRunTutorial] inspectPanel/handView 미배선 — 퇴근 구간을 건너뛴다.", this);
                yield break;
            }
            if (!TrySelectableHost(unit, out var entity, out var slotRect))
            {
                Debug.Log($"[FirstRunTutorial] '{unit.displayName}' 를 트레이 셀로 고를 수 없다" +
                          " (사망/미배치) — 퇴근 구간을 건너뛴다.", this);
                yield break;
            }

            // ⑦ 트레이 셀 탭 → 유닛 선택. 손패도 같이 열리지만(선택의 부수 효과) 구멍이
            // 퇴근 버튼 하나뿐이라 조작은 막힌다. 손패의 슬로모(priority 50)는 정지(100)를
            // 이기지 못한다.
            _selectionSet = false;
            handView.SelectionTargetSet += OnSelectionSet;
            bool alreadySelected = handView.SelectionTarget == entity;
            if (!alreadySelected)
            {
                Focus(slotRect, string.Format(ReselectFormat, unit.displayName));
                yield return WaitFor(() => _selectionSet);
            }
            handView.SelectionTargetSet -= OnSelectionSet;

            // ⑧ 퇴근 버튼. 패널은 lazy build 라 선택 직후 몇 프레임 rect 가 null 이다 —
            // **상한 대기**로 받고(조건 대기가 아니다) 그래도 없으면 블록을 건너뛴다.
            float grace = 0f;
            while (inspectPanel.ActionRect == null && grace < config.retirePanelGraceSeconds)
            {
                grace += Time.unscaledDeltaTime;
                yield return null;
            }
            var actionRect = inspectPanel.ActionRect;
            if (actionRect == null)
            {
                Debug.Log("[FirstRunTutorial] 퇴근 버튼을 찾지 못했다 — 퇴근 구간을 건너뛴다.", this);
                yield break;
            }

            // ⚠ 대기 술어에 «패널이 아직 열려 있나» 를 함께 건다. 구멍이 트레이 셀에 남아
            // 있는 grace 구간에 같은 셀을 다시 탭하면 그것은 «닫기» 이고(선택 토글),
            // 패널이 접히면 이 버튼은 영영 안 눌린다 — 정지 중이라 판도 안 끝난다.
            // `ActionRect` 가 `_visible`·`interactable` 까지 보므로 그 순간 null 이 되고,
            // 여기서 빠져나가 다음 판에 처음부터 다시 뜬다(계약 11).
            _retired = false;
            if (bridge != null) bridge.DefenderRetired += OnRetired;
            Focus(actionRect, RetireHintText);
            yield return WaitFor(() => _retired || inspectPanel.ActionRect == null);
            if (bridge != null) bridge.DefenderRetired -= OnRetired;
            if (!_retired)
            {
                Debug.Log("[FirstRunTutorial] 퇴근 전에 선택이 풀렸다 — 퇴근 구간을 건너뛴다.", this);
                yield break;
            }

            // ⑨ 퇴근 비행 관람 — **정지를 푼다.**
            //
            // ⚠ DefenderRetireFlight 는 `TimeManager.DeltaTime(TimeDomain.Battle)` 로 도는
            // 3막 1.6초 연출이다(연결 0.25 → 저항 0.85 → 뽑힘 0.50). 정지한 채 기다리면
            // 유닛이 공중에 멈춘 채 B3c 의 정지 스텝 셋을 통과해 **샷건맨 관람 창에서야**
            // 마저 날아간다. `Time.timeScale` 이 항상 1이라 코루틴 WaitForSeconds 가 정지
            // 중에도 흐르는 것과 다른 이야기다 — 이 연출은 도메인 델타를 직접 읽는다.
            //
            // 이 1.7초는 보드에 방어 유닛이 **하나도 없는** 구간이고, 그게 「자리가 비었다」의
            // 그림이다. 대상이 이미 판을 떠났으므로 맞을 유닛도 없다.
            guidance.ClearFocus();
            guidance.Hide();
            DimOnly();
            Unfreeze();
            yield return WaitUnscaled(config.retireWatchSeconds);
            Freeze();
            report(true);
        }


        private IEnumerator RunAttach()
        {
            // 재개 구간은 **진짜 플레이**다 — 딤을 내린다.
            //
            // ⚠ 여기에 딤을 켜두면 B4 가 구조적으로 실패한다. Duel 은 배치 가능 칸이 곧 적이
            // 걷는 길이라, 방금 놓은 캐논 한 기가 5초를 혼자 버티지 못하고 죽는다. 그런데
            // 플레이어는 딤에 묶여 유닛을 더 놓지도 못하고 그걸 구경만 하게 된다 — 그리고
            // 부착할 대상이 없어 튜토리얼이 조용히 닫힌다(실제로 그렇게 물렸다).
            //
            // 대가로 이 창에 각성을 먼저 쓸 수 있지만(게이지 여유가 정확히 0), 안내가 손패를
            // 가리키지 않는 구간이라 위험이 낮고, 무엇보다 대상이 죽는 쪽이 확실한 실패다.
            guidance.Hide();
            Unfreeze();

            // ⚠ 이 구간은 **차단막을 유지한다**(보기만 하는 구간). 열어두면 플레이어가 트레이의
            // 캐논 셀을 탭할 수 있고, 배치 후 그 셀은 «소진» 이라 탭이 곧 **판 위 유닛 선택**이다
            // (DefenderDragSlot.GoToDeployedUnit). 그러면 손패가 미리 열린 채 항아리 안내가 뜨고,
            // 이어지는 «다시 유닛을 선택» 은 이미 끝난 일을 시키는 말이 된다.
            DimOnly();
            yield return WaitUnscaled(config.resumeBeforeAttachSeconds);

            // 부착할 유닛이 없을 때만 차단막을 내려 **더 놓을 기회**를 준다. 이게 안전 밸브다 —
            // 놓았던 캐논이 죽은 판도 B4 를 볼 수 있어야 하고, 그건 플레이어가 손을 써야 한다.
            // 있으면 열지 않는다(위 이유).
            if (!TryResolveHost(out _, out _, out _))
            {
                HideDim();
                yield return WaitFor(() => TryResolveHost(out _, out _, out _));
                DimOnly();
            }
            if (!TryResolveHost(out _hostEntity, out var hostUnit, out var hostRect)) yield break;

            Freeze();

            // 4.0 항아리 소개 — 「왜 지금 유닛을 다시 고르나」의 근거를 먼저 준다.
            //
            // 배치 뒤 여기까지가 길게 비어 있었다. 각성 항아리를 가리켜 «쓸 수 있는 힘이
            // 찼다» 를 먼저 말하면, 이어지는 «유닛을 선택» 이 그 힘을 쓰기 위한 동작으로 읽힌다.
            //
            // 구멍은 뚫지 않는다(SetHoles(null)) — 가리키기만 하는 구간이라 지금 항아리를
            // 누르면 대상 없이 손패만 열려서 다음 스텝이 꼬인다. 링은 «여길 봐라» 만 한다.
            if (gaugeView != null && gaugeView.HitRect != null)
            {
                overlay.SetHoles(null);
                ShowDim();
                guidance.ShowMessage(JarHintText, false);
                guidance.FocusUi(gaugeView.HitRect);
                yield return WaitUnscaled(config.jarHintSeconds);
                guidance.ClearFocus();
            }

            // 4.1 유닛 선택. 유닛 이름이 캐논이 아니면 문구도 그 이름으로 바꾸고, B3 를 건너뛴
            // 판은 유닛을 한 번도 안 골랐으므로 «다시» 를 뺀다.
            string hostName = hostUnit != null ? hostUnit.displayName : "배치한";
            // B3 를 흘려보낸 판은 유닛을 한 번도 안 골랐으므로 «다시» 를 뺀다.
            string reselectText = _b3Completed
                ? string.Format(ReselectFormat, hostName)
                : string.Format(SelectHostFormat, hostName);

            // 대상 rect 는 **트레이 셀**이다(TryResolveHost 가 이미 보장한다) — 보드에 놓인
            // 유닛을 직접 찍게 하지 않는다. 이미 있는 게임 어휘를 가르치는 것이고, 구멍도
            // 고정 UI rect 하나라 카메라를 따라다닐 필요가 없다.

            _selectionSet = false;
            if (handView != null) handView.SelectionTargetSet += OnSelectionSet;
            // ⚠ 이미 그 유닛이 선택돼 있으면 재탭이 «닫기»가 된다(SelectionTargetSet 미발화).
            bool alreadySelected = handView != null && handView.SelectionTarget == _hostEntity;
            if (!alreadySelected)
            {
                // 구멍을 매 프레임 다시 잡지 않는다 — 트레이 셀은 고정 UI rect 라 오버레이의
                // 코너 추적(LateUpdate)만으로 따라간다. 4.2 카드가 매 프레임 다시 잡는 건
                // 딜인으로 «없다가 생기는» 대상이라서고, 여기는 그 조건이 없다.
                Focus(hostRect, reselectText);
                yield return WaitFor(() => _selectionSet);
            }
            if (handView != null) handView.SelectionTargetSet -= OnSelectionSet;

            // 4.2 카드 선택 — 지금 부착 가능한 Unit 카드에만 구멍을 뚫는다.
            // 액티브 카드는 즉발 탭이 거절되고(끌어서 사용) 커밋 경로가 AttachmentsChanged 를
            // 발화하지 않아, 열어두면 각성을 쓰고도 안내가 안 넘어간다.
            //
            // ⚠ **구멍을 한 번만 잡으면 안 된다.** 선택 직후엔 손패가 아직 딜인 중이라 카드가
            // 비활성이고, SetHoles 는 비활성 대상을 버린 뒤 다시 담지 않는다(오버레이 LateUpdate
            // 는 코너 «변화» 만 추적한다) → 구멍 0개 = 풀 dim 이라 카드를 못 누른다. 실제로
            // 그렇게 물렸다. 그래서 **부착이 성사될 때까지 매 프레임 구성을 보고 바뀌면 다시
            // 잡는다** — 딜인이 늦게 끝난 카드도 그때 열린다.
            // ⚠⚠ **여기서 조건 대기를 하면 앱이 잠긴다.** 손패가 열릴 때까지 무조건 기다리면,
            // 낼 수 있는 카드가 0인 판에서 영영 안 깨어난다 — 각성 게이지는 **적 처치로만**
            // 오르는데(GainAwakening) 정지 중엔 킬이 없어 0에서 벗어날 길이 없다. 게다가 매치
            // 타이머도 Battle 도메인이라 멈춰 있어 Result 전이가 오지 않는다 → 계약 13 의
            // 정리 경로조차 발동하지 않는다. 풀 dim + 0배속 + 안 끝나는 판 = 강종 외 탈출구 없음.
            // 도달 경로는 흔하다: 재개 구간(딤 없음)에 각성 카드를 한 장 쓰면 여유가 0이 된다.
            //
            // 그래서 **딜인만 상한 대기**하고(카드가 활성화되는 데 몇 프레임 걸린다), 그러고도
            // 0이면 계약 11 대로 **기다리지 말고 건너뛴다**. 상한 대기는 조건 대기가 아니다.
            float grace = 0f;
            while (AttachableCardCount() == 0 && grace < config.cardDealInGraceSeconds)
            {
                grace += Time.unscaledDeltaTime;
                yield return null;
            }
            if (AttachableCardCount() == 0)
            {
                Debug.Log("[FirstRunTutorial] 낼 수 있는 드림캐쳐가 없다 — 부착 구간을 건너뛴다.", this);
                yield break;
            }
            _attachBaseline = handController != null ? handController.AttachCountOf(_hostEntity) : -1;
            guidance.ClearFocus();

            int shownCount = -1;
            while (!(handController != null && _attachBaseline >= 0
                     && handController.AttachCountOf(_hostEntity) > _attachBaseline))
            {
                int now = AttachableCardCount();
                if (now != shownCount)
                {
                    shownCount = now;
                    overlay.SetHoles(CollectAttachableCardRects());
                    // 구멍이 바뀔 때만 문구도 같이 갱신한다(매 프레임 문자열 재대입 회피).
                    guidance.ShowMessage(now == 4 ? CardText : CardFallbackText, false);
                }
                yield return null;
            }

            // 4.3 마무리 — 문구만(구멍 없음)
            guidance.ClearFocus();
            yield return WaitUnscaled(config.attachSettleSeconds);
            overlay.SetHoles(null);
            guidance.ShowMessage(AttachDoneText, false);
            yield return WaitUnscaled(config.goalMessageSeconds);
            _b4Completed = true;
        }

        // B5 — 부착 튜토리얼 뒤의 자유 플레이 전환. 여기서는 **정지와 차단막을 둘 다
        // 내린 뒤** 시간 UI만 가리킨다. Guidance의 문구·링·포인터는 raycastTarget=false라
        // 플레이어는 안내를 읽는 동안에도 유닛 배치와 드림캐쳐 사용을 계속할 수 있다.
        private IEnumerator RunSurvivalHint()
        {
            if (!_b4Completed) yield break;

            Unfreeze();
            HideDim();

            RectTransform timerRect = scoreHud != null ? scoreHud.TimerFocusRect : null;
            if (timerRect == null || !timerRect.gameObject.activeInHierarchy)
            {
                Debug.LogWarning("[FirstRunTutorial] 시간 UI를 찾지 못했다 — 생존 안내를 건너뛴다.", this);
                yield break;
            }

            guidance.ShowMessage(SurvivalText, false);
            guidance.FocusUi(timerRect);
            yield return WaitUnscaled(config.survivalHintSeconds);
            guidance.ClearFocus();
            guidance.Hide();
            _b5Completed = true;
        }

        // ── 닫기 ────────────────────────────────────────────────────────────
        private void Close()
        {
            ClearHighlights();
            guidance.ClearFocus();
            guidance.Hide();
            HideDim();
            Unfreeze();

            // ⚠ 선행조건 부재로 건너뛰었거나 대기 중에 판이 먼저 끝난 경우는 완료로 기록하지
            // 않는다(계약 11). 1회성이라 기록해버리면 핵심을 한 번도 못 본 계정이 다시 볼
            // 기회를 영영 잃는다.
            if (_b3Completed && _b4Completed && _b5Completed
                && profileSO != null && profileSO.profile != null)
            {
                profileSO.profile.firstRunTutorialDone = true;
                ProfileStore.Save(profileSO.profile);
                Debug.Log("[FirstRunTutorial] 온보딩 완료 — 기록했다.", this);
            }
            else
            {
                Debug.Log($"[FirstRunTutorial] 미완료로 끝났다(b3={_b3Completed} b4={_b4Completed} b5={_b5Completed}) — 다음 판에 다시 뜬다.", this);
            }
            _running = false;
        }

        // ── 도구 ────────────────────────────────────────────────────────────

        // ⚠ 우선순위 100 이 필수다. 손패·유닛 선택이 같은 도메인을 priority 50 으로 요청하므로
        // 기본 0 으로 잡으면 유닛을 고르는 순간 판이 0.3배로 다시 흐른다. 선례는 MenuPopup.
        private void Freeze()
        {
            if (_frozen) return;
            _freeze = TimeManager.Instance.Request(TimeDomain.Battle, 0f, priority: 100);
            _frozen = true;
        }

        private void Unfreeze()
        {
            if (!_frozen) return;
            _freeze.Dispose();
            _frozen = false;
        }

        // ⚠ Show() 는 멱등이 아니다 — 알파를 0으로 되돌리고 페이드를 다시 돌린다. 스텝마다
        // 부르면 계약 5 가 «전 구간» 이라고 못박은 딤이 경계마다 깜빡인다(입력 차단은 유지되고
        // 보이는 것만 어긋난다). 그래서 표시 여부를 여기서 기억하고 전이할 때만 부른다.
        private bool _dimShown;

        private void ShowDim()
        {
            overlay.SetSortingOrder(guidance.DimSortingOrder);
            if (_dimShown) return;
            // ⚠ **보이지 않는 차단막**(알파 0). 판이 도는 동안 화면을 어둡게 덮는 게 어색하다는
            // 판단이다 — 입력만 막고 시야는 그대로 둔다. 조각은 알파와 무관하게 raycastTarget 을
            // 들고 있고 레이캐스트는 blocksRaycasts 를 보므로 차단력은 그대로다.
            // 무엇을 눌러야 하는지는 딤 대비가 아니라 **포커스 링**이 말한다.
            overlay.Show(config.dimOpacity);
            _dimShown = true;
        }

        private void HideDim()
        {
            overlay.Hide();
            _dimShown = false;
        }

        private void DimOnly()
        {
            overlay.SetHoles(null);
            ShowDim();
        }

        private void Focus(RectTransform target, string text)
        {
            overlay.SetHoles(new[] { target });
            ShowDim();
            guidance.ShowMessage(text, false);
            guidance.FocusUi(target);
        }

        private void Unsubscribe()
        {
            var drag = defenderSelector != null ? defenderSelector.DragController : null;
            if (drag != null)
            {
                drag.Armed -= OnArmed;
                drag.UserDragStarted -= OnDragStarted;
                drag.PlacementCommitted -= OnPlaced;
            }
            if (handView != null) handView.SelectionTargetSet -= OnSelectionSet;
            if (bridge != null) bridge.DefenderRetired -= OnRetired;
        }

        // ⚠ **어느 유닛인지 본다.** 배치 스텝은 차단막을 내리는 구간이라 플레이어가 다른
        // 셀을 눌러 엉뚱한 유닛을 놓을 수 있고, 그걸 통과시키면 그 블록이 가르치려던
        // 배치 스킬을 한 번도 안 보고 넘어간다. `UserDragStarted` 만 유닛을 안 실어 오므로
        // 그것만 «기다리는 유닛이 있으면» 으로 받는다(드래그 대상은 arm 된 셀 하나다).
        private void OnArmed(DefenderUnitData unit) { if (Matches(unit)) _armed = true; }
        private void OnDragStarted() { if (_awaitingUnit != null) _armed = true; }
        private void OnPlaced(DefenderUnitData unit) { if (Matches(unit)) _placed = true; }
        private void OnSelectionSet() => _selectionSet = true;
        private void OnRetired(Entity _, DefenderUnitData __, Vector3 ___) => _retired = true;

        private bool Matches(DefenderUnitData unit) => _awaitingUnit == null || unit == _awaitingUnit;

        // 부착 호스트 해결: **마지막으로 놓은 유닛(샷건맨) 우선**, 그다음 말파이트, 그다음
        // 트레이의 아무 유닛. 퇴근한 말파이트는 배치 엔티티가 없어 자동 탈락하지만 순서를
        // 명시해 둔다 — 둘 다 살아 있는 판(퇴근 블록을 건너뛴 판)에서 문구가 가리키는 대상이
        // «방금 놓은 것» 이 되게 하려는 것이다.
        // TryGetDeployedEntity 는 _em.Exists 로 생존까지 본다 — 죽은 유닛은 여기서 걸러진다.
        // 부착 대상 = **트레이 셀로 선택할 수 있는** 배치 유닛. 이게 B4 의 유일한 불변식이고
        // 4.1 은 여기서 나온 rect 를 그대로 쓴다(분기 없음).
        //
        // 왜 «선택 가능» 까지 묶는가: 4.1 이 뚫는 구멍이 **트레이 셀 rect** 하나이기 때문이다.
        // 소진된 셀만 «판 위 그 유닛으로 데려간다»(GoToDeployedUnit → SelectDeployed) — 소진이
        // 아닌 셀을 가리키면 그 탭은 배치 드래그를 열 뿐이라 조건이 영영 안 서고 타임아웃이
        // 없어(계약 11) 그대로 멈춘다. 그래서 판정에 소진까지 포함한다.
        // (보드 유닛 직접 탭은 2026-08-20 에 다시 켜졌다 — DcInspectController.
        // BoardTapSelectEnabled=true. 플레이어가 그쪽으로 선택해도 완료 이벤트는 같다.)
        //
        // 우선순위는 온보딩이 가리키던 유닛 → 그 밖의 아무 유닛(캐논이 죽은 판 대비).
        private bool TryResolveHost(out Entity entity, out DefenderUnitData unit, out RectTransform slotRect)
        {
            if (TrySelectableHost(secondaryUnit, out entity, out slotRect)) { unit = secondaryUnit; return true; }
            if (TrySelectableHost(primaryUnit, out entity, out slotRect)) { unit = primaryUnit; return true; }
            unit = null;
            slotRect = null;
            entity = Entity.Null;
            if (defenderSelector == null) return false;
            for (int i = 0; i < defenderSelector.SlotCount; i++)
            {
                var u = defenderSelector.SlotUnitAt(i);
                if (u == null || u == primaryUnit || u == secondaryUnit) continue;
                if (!TrySelectableHost(u, out entity, out slotRect)) continue;
                unit = u;
                return true;
            }
            return false;
        }

        private bool TrySelectableHost(DefenderUnitData unit, out Entity entity, out RectTransform slotRect)
        {
            entity = Entity.Null;
            slotRect = null;
            if (unit == null || bridge == null || defenderSelector == null) return false;
            if (!bridge.TryGetDeployedEntity(unit, out entity) || entity == Entity.Null) return false;
            // 소진 = 보드 상한만큼 나가 있다 = 그 셀의 탭이 «판 위 유닛으로 데려간다».
            if (bridge.DeployedCountOf(unit) < unit.EffectiveMaxOnBoard) return false;
            return defenderSelector.TryGetSlotRect(unit, out slotRect) && slotRect != null;
        }

        // «지금 구멍을 뚫을 수 있는 카드» 의 단일 술어. 세는 곳과 모으는 곳이 갈리면
        // 「n장 있다고 세어놓고 0개를 넘기는」 상태가 생긴다 — 실제로 그렇게 물렸다.
        //
        // ⚠ activeInHierarchy 를 여기서 본다. SetHoles 는 비활성 대상을 **버리고**, 버린 것은
        // 다시 담기지 않는다(오버레이의 LateUpdate 는 코너 «변화» 만 추적한다). 손패 딜인이
        // 끝나기 전에 넘기면 구멍 0개 = 전면 차단이 되어 카드를 못 누른다.
        private bool IsAttachableSlot(DreamcatcherHandView.CardSlot slot)
        {
            if (slot == null || slot.rect == null || slot.card == null) return false;
            if (!slot.rect.gameObject.activeInHierarchy) return false;
            if (slot.card.type == CardType.Active) return false;   // 즉발 탭 거절 + AttachmentsChanged 미발화
            return slot.Playable;
        }

        private int AttachableCardCount()
        {
            if (handView == null || handView.Slots == null) return 0;
            int n = 0;
            var slots = handView.Slots;
            for (int i = 0; i < slots.Count; i++) if (IsAttachableSlot(slots[i])) n++;
            return n;
        }

        private RectTransform[] CollectAttachableCardRects()
        {
            if (handView == null || handView.Slots == null) return null;
            var list = new System.Collections.Generic.List<RectTransform>();
            var slots = handView.Slots;
            for (int i = 0; i < slots.Count; i++)
                if (IsAttachableSlot(slots[i])) list.Add(slots[i].rect);
            return list.ToArray();
        }

        // 러너 시계는 unscaled 다 — 자기가 Battle 을 멈춰놓고 그 시계를 기다리면 영영 안 온다.
        private static IEnumerator WaitUnscaled(float seconds)
        {
            float t = 0f;
            while (t < seconds) { t += Time.unscaledDeltaTime; yield return null; }
        }

        // **타임아웃이 없다**(사용자 결정). 안내가 요구한 행동을 할 때까지 기다린다 —
        // 흘려보내면 그 판은 어차피 완료로 기록되지 않아 다음 판에 처음부터 다시 뜬다.
        // 「기다린다」가 「또 처음부터」보다 낫다.
        //
        // 대신 **모든 조건 대기는 만족 가능해야 한다**. 선행조건이 없으면 기다리지 말고
        // 진입 전에 건너뛴다(캐논이 소진/쿨타임이면 3.1 을 안 열고, 낼 수 있는 카드가 0이면
        // 4.2 를 안 연다). 정지 중에는 코스트·쿨타임이 안 도니 «기다리면 가능해진다»가 없다.
        private static IEnumerator WaitFor(System.Func<bool> done)
        {
            while (!done()) yield return null;
        }
    }
}
