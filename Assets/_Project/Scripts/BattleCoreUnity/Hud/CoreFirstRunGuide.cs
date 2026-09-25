using System.Collections;
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;
using Wassup.BattleCore;
using Wassup.Core;
using Wassup.Core.TimeControl;
using Wassup.Data;
using Wassup.UI;
using Wassup.UI.Tutorial;

namespace Wassup.BattleCoreUnity.Hud
{
    // battle-core-rebuild unit 8b — **계정 첫 판 온보딩의 전투 구간**(사용자 결정 ① (a) 2026-09-25: 옮긴다).
    // 옛 `UI/Tutorial/FirstRunTutorialController.cs`(908줄 · first-run-tutorial units 3~9)의 이식이다.
    //
    // 이 컴포넌트는 **게임 규칙을 하나도 소유하지 않는다**(옛 계약 1). 배치·부착·배치 스킬·코스트·게이지는 전부 코어가
    // 처리하고, 여기서 정하는 것은 «언제 멈출지»와 «무엇을 열어둘지» 둘뿐이다. 대신 눌러 주는 동작은 없다.
    // 읽는 것은 **읽기 모델**(드라이버·담당자·뷰의 공개 창)과 **사건**(`Placed`·`Retired`)이고, 쓰는 것은 커맨드 하나
    // (첫 유닛 체력 낮추기 — `DamageMaxHealthRatio`)와 시간 리스·안내 도구뿐이다.
    //
    // **완료 기록(`firstRunTutorialDone`)은 이 컴포넌트가 갖는다**(옛 `:719` — 그 플래그를 쓰는 곳은 이것 하나뿐이다).
    // 로비는 그 플래그가 거짓인 동안 참가 신청을 생략하므로(`OutgameMenuController.cs:280~292`), 완주 → 기록 → 다음 판
    // 참가 신청이 이 파일의 검증 질문이다.
    //
    // 옛 것과 다른 점(8b 「고친 것」):
    //   · 「온보딩 판인가」는 **드라이버의 진입 해석**(`BattleDriver.Entry.Kind == Onboarding`)을 읽는다 — 옛 컨트롤러가 직접
    //     `ShouldRun` 을 부르던 자리다. 술어 소비처를 늘리지 않는다는 옛 계약(GameManager 주석)을 더 좁힌 것이다.
    //   · 카운트다운 홀드 = Battle 도메인 정지 리스(상한 자가 해제). 새 배치 창의 카운트다운은 **판의 틱**이라 정지가 곧 홀드다.
    //   · 배치 가능/불가 설명 = `CoreMapOverlay.ShowBriefing`(칸의 상태 — 유닛 층 무관. 옛 것은 말파이트의 층으로 칠했다).
    //   · 「철수」 버튼 라벨 = `CoreSelectionPanel` 의 액션 라벨(`SelectionInput.RetireLabel`). 문구의 「철수」와 같은 말이다.
    [DisallowMultipleComponent]
    public sealed class CoreFirstRunGuide : MonoBehaviour
    {
        // 문구는 옛 컨트롤러의 const 그대로다(사용자 원문 · 띄어쓰기 포함). ⚠ 유닛 이름이 박힌 문구는 배선된 유닛과 같아야 한다 —
        // 한국어 조사(는/은 · 를/을) 때문에 포맷으로 빼지 않았다(옛 주석). 이름이 필요 없는 자리만 조사 중립형 포맷을 쓴다.
        private const string PlaceableText = "배치가능영역";
        private const string BlockedText = "배치 불가 영역";
        private const string GoalText = "게임목표: 제한시간 동안\n최대한 많은 악몽 처치!";
        private const string GoalIntroText = "스폰된 악몽은 목표지점을 향합니다.";
        private const string DefendIntroText = "유닛의 배치와 드림캐쳐의 조합으로\n악몽을 막아보세요";
        private const string GoalMarkerLabel = "목표지점";
        private const string EnemyApproachText = "악몽이 배치 영역 안으로 들어오면!";

        private const string PickText = "유닛을 터치 해보세요";
        private const string PrimarySkillText = "말파이트는 주변 악몽을\n3초간 기절시킵니다!";
        private const string PrimaryPlaceText = "악몽 무리 위에 말파이트를\n배치 해보세요!";
        private const string PrimaryDoneText = "말파이트 체력이 낮아졌습니다.\n철수시켜 보세요";
        private const string RetireHintText = "체력이 적거나 재배치가 필요한 경우,\n철수시켜 전략적으로 활용할 수 있습니다.";

        private const string SecondaryPickFormat = "이번엔 {0} 유닛을\n터치 해보세요";
        private const string SecondarySkillText = "샷건맨은 가까운 악몽들을 밀어냅니다!";
        private const string SecondaryPlaceText = "빈 자리에 샷건맨을 배치 해보세요!";
        private const string OnPlaceText = "강력한 배치스킬들을 활용하여\n전황을 유리하게 이끌어 보세요";

        private const string JarHintText = "충분한 양의 에너지를 모았네요!";
        private const string ReselectFormat = "다시 {0} 유닛을\n선택 해보세요";
        private const string SelectHostFormat = "{0} 유닛을 선택 해보세요";
        private const string CardText = "하단 드림캐쳐 4개중\n맘에 드는것을 터치 해보세요";
        private const string CardFallbackText = "하단 드림캐쳐 중\n맘에 드는것을 터치 해보세요";
        private const string AttachDoneText = "드림캐쳐를 유닛에게 부착하여\n더 강해질 가능성을 열어보세요!";

        private const string SurvivalText =
            "유닛 배치, 드림캐쳐등 기능을\n활용하여 튜토리얼 1분을 버텨보세요!";

        [SerializeField] private BattleDriver _driver;
        [SerializeField] private PlayerProfileSO _profile;
        [SerializeField] private FirstRunTutorialConfig _config;
        [Tooltip("B3a·B3b 가 가리킬 첫 유닛(말파이트). 문구가 이 유닛의 효과를 주장한다 — 바꾸면 문구도 같이.")]
        [SerializeField] private DefenderUnitData _primaryUnit;
        [Tooltip("B3c 가 가리킬 두 번째 유닛(샷건맨). **B4 의 부착 대상도 이쪽이 우선**이다.")]
        [SerializeField] private DefenderUnitData _secondaryUnit;

        [Header("안내 도구 (teardown 이 남긴 것만 쓴다)")]
        [SerializeField] private TutorialGuidanceView _guidance;
        [Tooltip("Guidance 와 **다른 GameObject** 여야 한다 — 둘 다 자기 캔버스를 만들어 sortingOrder 를 다툰다.")]
        [SerializeField] private OutgameTutorialOverlay _overlay;

        [Header("새 전투 화면")]
        [SerializeField] private CoreDefenderTray _tray;
        [SerializeField] private Input.DragPlacementInput _placement;
        [SerializeField] private Input.SelectionInput _selection;
        [SerializeField] private CoreSelectionPanel _panel;
        [SerializeField] private Cards.CoreHandView _hand;
        [SerializeField] private Cards.CoreAwakeningGaugeView _gauge;
        [SerializeField] private CoreScoreHud _scoreHud;
        [SerializeField] private View.CoreMapOverlay _mapOverlay;
        [SerializeField] private Camera _boardCamera;

        /// <summary>
        /// 완료 기록 저장 seam. 테스트가 개발자의 실제 `profile.json` 을 재작성하지 않게 갈아 끼운다(옛 컨트롤러는 직접 `Save`).
        /// </summary>
        public System.Action<PlayerProfile> ProfileSaver { get; set; } = ProfileStore.Save;

        // ── 진행 상태(안내 스크립트의 커서 — 게임 상태가 아니다) ─────────────────────
        private bool _started;
        private bool _running;
        private bool _briefingDone;
        private bool _battleStarted;
        private bool _closed;

        // 정지 리스는 **구간**이 소유한다(옛 계약 7). 성공·스킵·판 종료·씬 이탈이 전부 `Unfreeze` 하나를 지난다.
        private TimeLease _freeze;
        private bool _frozen;
        private TimeLease _introHold;
        private bool _introHeld;
        private float _introHoldUntil;

        private int _awaitingDefIndex = -1;
        private bool _placed;
        private SimEntityId _placedId = SimEntityId.None;
        private SimEntityId _retireTarget = SimEntityId.None;
        private bool _retired;

        private bool _b3Completed, _b4Completed, _b5Completed;
        private bool _dimShown;

        /// <summary>테스트의 창 — 지금 도는가 · 세 구간이 제대로 끝났나 · 완료를 기록했나.</summary>
        public bool Running => _running;
        public bool B3Completed => _b3Completed;
        public bool B4Completed => _b4Completed;
        public bool B5Completed => _b5Completed;
        public bool CompletionRecorded { get; private set; }
        /// <summary>지금 구간(`B1`·`B2b`·`B3a`·`B3b`·`B3c`·`B4`·`B5`·`closed`)과 그 안의 대기(`.pick`·`.approach`·`.place`·`.select`·`.retire`·`.card`).</summary>
        public string Step { get; private set; } = "";
        private string _block = "";
        private void Sub(string wait) => Step = _block + "." + wait;
        private void Block(string name) { _block = name; Step = name; }

        private void OnEnable()
        {
            if (_driver != null) _driver.Subscribe(ViewOrder.Overhead, OnCoreEvent);
        }

        private void OnDisable()
        {
            if (_driver != null) _driver.Unsubscribe(OnCoreEvent);
            Unfreeze();
            ReleaseIntroHold();
        }

        private void OnDestroy()
        {
            Unfreeze();
            ReleaseIntroHold();
        }

        private void OnCoreEvent(CoreEvent e)
        {
            if (!_running) return;
            if (e.Kind == CoreEventKind.Placed)
            {
                if (_awaitingDefIndex < 0 || e.Arg == _awaitingDefIndex) { _placed = true; _placedId = e.A; }
            }
            else if (e.Kind == CoreEventKind.Retired)
            {
                if (_retireTarget.IsEntity && e.A == _retireTarget) _retired = true;
            }
        }

        private void Update()
        {
            if (_driver == null || !_driver.Running) return;
            if (!_started)
            {
                _started = true;
                // 「온보딩 판인가」는 진입 해석이 이미 답했다 — 여기서 술어를 다시 부르지 않는다.
                bool onboarding = _driver.Entry != null && _driver.Entry.Kind == MatchEntryKind.Onboarding;
                if (!onboarding || _config == null || _guidance == null || _overlay == null)
                {
                    if (onboarding) Debug.LogWarning("[CoreFirstRunGuide] 온보딩 판인데 config/guidance/overlay 가 배선되지 않았다 — 안내 없이 돈다(완료 기록 없음).", this);
                    enabled = false;
                    return;
                }
                _running = true;
            }
            if (!_running) return;

            TickIntroHold();

            var clock = _driver.Match.Clock;
            if (clock.Ended)
            {
                // ⚠ 판이 먼저 끝날 수 있다(60초 판 · 스텝 타임아웃 없음). 대기 중인 코루틴은 스스로 깨어나지 않으므로
                // 끊어 준다 — 안 끊으면 차단막과 정지가 결과 화면 위에 남는다. 완료 판정은 `Close` 가 한다.
                StopAllCoroutines();
                Close();
                return;
            }
            if (clock.Phase == MatchPhase.Placement && !_briefingDone)
            {
                _briefingDone = true;
                StartCoroutine(RunBriefing());
            }
            else if (clock.Phase == MatchPhase.Battle && !_battleStarted)
            {
                _battleStarted = true;
                StartCoroutine(RunBattle());
            }
        }

        // ── B1 맵 설명 ──────────────────────────────────────────────────────
        // 카운트다운을 붙잡아 두고 돈다. 입력은 보이지 않는 차단막이 막는다.
        private IEnumerator RunBriefing()
        {
            Block("B1");
            BeginIntroHold(_config.introHoldMaxSeconds);
            DimOnly();
            for (int i = 0; i < _config.briefingCycles; i++)
            {
                ShowBriefing(View.CoreMapOverlay.Briefing.Placeable);
                _guidance.ShowMessage(PlaceableText, false);
                yield return WaitUnscaled(_config.briefingHoldSeconds);
                ShowBriefing(View.CoreMapOverlay.Briefing.Blocked);
                _guidance.ShowMessage(BlockedText, false);
                yield return WaitUnscaled(_config.briefingHoldSeconds);
            }
            ShowBriefing(View.CoreMapOverlay.Briefing.None);
            _guidance.ShowMessage(GoalText, false);
            yield return WaitUnscaled(_config.goalMessageSeconds);
            _guidance.Hide();
            ReleaseIntroHold();
        }

        // ── B2b · B3 · B4 · B5 ──────────────────────────────────────────────
        private IEnumerator RunBattle()
        {
            // 옛 계약 5 — GO! 부터 차단막. 열어두면 유닛을 먼저 놓거나 각성을 먼저 써서 아래 구간이 스킵 조건에 걸린다.
            DimOnly();
            Block("B2b");

            float introSpent = 0f;
            if (_boardCamera != null && TryGetGoalAnchor(out var goalWorld))
                _guidance.ShowWorldMarker(_boardCamera, goalWorld, GoalMarkerLabel, _guidance.GoalMarkerColor);
            _guidance.ShowMessage(GoalIntroText, false);
            yield return WaitUnscaled(_config.goalIntroSeconds);
            introSpent += _config.goalIntroSeconds;

            _guidance.ShowMessage(DefendIntroText, false);
            yield return WaitUnscaled(_config.goalIntroSeconds);
            introSpent += _config.goalIntroSeconds;
            _guidance.ClearWorldMarkers();

            if (introSpent < _config.battleFreezeAtSeconds)
                yield return WaitUnscaled(_config.battleFreezeAtSeconds - introSpent);

            yield return RunB3();
            yield return RunAttach();
            yield return RunSurvivalHint();

            Close();
        }

        // B3 — 세 블록을 순서대로. **셋 다 완주해야** 완료로 찍는다(옛 계약 11·19).
        private IEnumerator RunB3()
        {
            bool a = false, b = false, c = false;

            Block("B3a");
            yield return RunPickAndPlace(_primaryUnit, true, _config.primaryPostPlacementDamageRatio,
                PickText, PrimarySkillText, PrimaryPlaceText, PrimaryDoneText, ok => a = ok);
            if (!a) yield break;

            Block("B3b");
            yield return RunRetire(_primaryUnit, ok => b = ok);
            if (!b) yield break;

            // ⚠ 접근 대기는 B3a 의 소유다(옛 계약 17) — 여기서 다시 기다리면 조건이 이미 참이라 문구가 0프레임 노출된다.
            Block("B3c");
            yield return RunPickAndPlace(_secondaryUnit, false, null,
                string.Format(SecondaryPickFormat, _secondaryUnit != null ? _secondaryUnit.displayName : "다음"),
                SecondarySkillText, SecondaryPlaceText, OnPlaceText, ok => c = ok);
            if (!c) yield break;

            _b3Completed = true;
        }

        // 선택 → (접근 대기) → 무기 소개 → 배치 → 배치 스킬 관람 → 마무리 문구. 유닛과 문구를 인자로 받아 두 번 돈다.
        private IEnumerator RunPickAndPlace(DefenderUnitData unit, bool waitForApproach, float? postPlacementDamageRatio,
                                            string pickText, string skillHintText, string placeText, string doneText,
                                            System.Action<bool> report)
        {
            // ⚠ 지불 판정은 **정지 전에** 한다. 정지 중엔 코스트도 쿨타임도 안 돌아 「기다리면 가능해진다」가 없다.
            int def = DefIndexOf(unit);
            if (def < 0 || _tray == null)
            {
                Debug.Log($"[CoreFirstRunGuide] '{(unit != null ? unit.displayName : "null")}' 가 이 판 편성에 없다 — 이 배치 구간을 건너뛴다.", this);
                yield break;
            }
            // 선행조건 부재는 진입 전에 건너뛴다(옛 그대로). 이유를 한 줄 남긴다 — 이 블록이 빠지면 그 계정은 계속 온보딩 판이다.
            if (!_tray.TryGetSlotRect(def, out var slotRect)) slotRect = null;
            if (!SlotUsableNow(def) || slotRect == null)
            {
                Debug.Log($"[CoreFirstRunGuide] '{unit.displayName}' 를 지금 놓을 수 없다(칸={_driver.Match.Placement.SlotBlock(def)} · 사각={(slotRect != null)}) — 이 배치 구간을 건너뛴다.", this);
                yield break;
            }

            Freeze();

            // 3.1 유닛 터치. ⚠ 플래그는 블록마다 리셋한다(두 유닛이 같은 칸을 쓴다).
            _awaitingDefIndex = def;
            _placed = false;
            _placedId = SimEntityId.None;
            Focus(slotRect, pickText);
            Sub("pick");
            yield return WaitFor(() => IsPicking(def) || _placed);

            // 3.1b 악몽이 **내 목표 가까이** 들어올 때까지 — 첫 블록만. 기준은 시간이 아니라 사건이다(옛 주석).
            if (waitForApproach && !_placed)
            {
                ShowBriefing(View.CoreMapOverlay.Briefing.Placeable);
                _guidance.ClearFocus();
                DimOnly();
                Unfreeze();
                _guidance.ShowMessage(EnemyApproachText, false);
                Sub("approach");
                yield return WaitFor(() => _placed || AnyEnemyWithinTilesOfGoal(_config.approachGoalTiles));
                Freeze();
            }

            // 3.1c 무기 소개 — 왜 «무리 위에» 놓아야 하는지의 근거. ⚠ 이 문구들은 유닛 에셋 값을 주장한다.
            if (!_placed)
            {
                _guidance.ShowMessage(skillHintText, false);
                yield return WaitUnscaled(_config.skillHintSeconds);
            }

            // 3.2 배치 — 어느 칸에 놓든 통과. **이 구간만 차단막을 내린다**(보드 입력이 필요하다).
            // ⚠⚠ 대기 술어에 «아직 지불 가능한가» 를 함께 건다 — 정지 중 다른 유닛을 놓아 코스트를 떨어뜨리면 그 배치가 영영
            // 불가능해지고 판 시계도 멈춰 있어 탈출구가 없다(옛 주석). 성공해도 슬롯은 「소진」이라 판정은 `_placed` 로 한다.
            if (!_placed)
            {
                ShowBriefing(View.CoreMapOverlay.Briefing.Placeable);
                _guidance.ClearFocus();
                HideDim();
                _guidance.ShowMessage(placeText, false);
                Sub("place");
                yield return WaitFor(() => _placed || !SlotUsableNow(def));
                if (!_placed)
                {
                    Debug.Log($"[CoreFirstRunGuide] '{unit.displayName}' 를 더 이상 놓을 수 없다(코스트/쿨타임) — 이 배치 구간을 건너뛴다.", this);
                    _awaitingDefIndex = -1;
                    ShowBriefing(View.CoreMapOverlay.Briefing.None);
                    yield break;
                }
            }
            ShowBriefing(View.CoreMapOverlay.Briefing.None);
            _awaitingDefIndex = -1;

            // 첫 유닛의 체력바를 낮춰 철수 안내의 이유를 화면에 만든다. 값이 있으면 반드시 성공해야 한다 — 실패하고도 저체력
            // 문구를 띄우면 화면과 안내가 갈린다. 피해는 **커맨드**다(출처 없는 최대 체력 비율 — 옛 `IncomingDamage{source=Null}`).
            if (postPlacementDamageRatio.HasValue)
            {
                bool ok = postPlacementDamageRatio.Value > 0f && _placedId.IsEntity
                          && _driver.Apply(Command.DamageMaxHealthRatio(_placedId, postPlacementDamageRatio.Value)).Accepted;
                if (!ok)
                {
                    Debug.LogWarning($"[CoreFirstRunGuide] '{unit.displayName}' 의 배치 후 체력 감소를 요청하지 못했다.", this);
                    report(false);
                    yield break;
                }
            }

            // 3.3 배치 스킬 관람 — **정지를 푼다**(차단막은 유지).
            _guidance.Hide();
            DimOnly();
            Unfreeze();
            yield return WaitUnscaled(_config.onPlaceWatchSeconds);

            Freeze();
            _guidance.ShowMessage(doneText, false);
            yield return WaitUnscaled(_config.goalMessageSeconds);
            report(true);
        }

        // B3b — 놓은 유닛을 **되돌린다**. 안내는 철수 버튼을 가리키기만 하고, 철수 자체는 선택 패널의 버튼(커맨드 `Retire`)이다.
        private IEnumerator RunRetire(DefenderUnitData unit, System.Action<bool> report)
        {
            // 미배선은 조용히 지나가면 안 된다 — 이 블록이 스킵되면 그 계정은 이후 모든 판이 온보딩 판으로 돈다.
            if (unit == null) yield break;
            if (_panel == null || _selection == null)
            {
                Debug.LogWarning("[CoreFirstRunGuide] 선택 패널/선택 입력 미배선 — 철수 구간을 건너뛴다.", this);
                yield break;
            }
            if (!TrySelectableHost(unit, out var id, out var slotRect))
            {
                Debug.Log($"[CoreFirstRunGuide] '{unit.displayName}' 를 트레이 칸으로 고를 수 없다(사망/미배치) — 철수 구간을 건너뛴다.", this);
                yield break;
            }

            // ⑦ 트레이 칸 탭 → 유닛 선택(선택이 손패도 연다 — 구멍이 철수 버튼 하나라 조작은 막힌다).
            if (_selection.Selected != id)
            {
                Focus(slotRect, string.Format(ReselectFormat, unit.displayName));
                Sub("select");
                yield return WaitFor(() => _selection.Selected == id);
            }

            // ⑧ 철수 버튼. 패널은 lazy build 라 선택 직후 몇 프레임 사각이 null 이다 — **상한 대기**(조건 대기가 아니다).
            float grace = 0f;
            while (_panel.ActionRect == null && grace < _config.retirePanelGraceSeconds)
            {
                grace += Time.unscaledDeltaTime;
                yield return null;
            }
            var actionRect = _panel.ActionRect;
            if (actionRect == null)
            {
                Debug.Log("[CoreFirstRunGuide] 철수 버튼을 찾지 못했다 — 철수 구간을 건너뛴다.", this);
                yield break;
            }

            // ⚠ 「패널이 아직 열려 있나」를 함께 건다 — 같은 칸을 다시 탭해 패널이 접히면 이 버튼은 영영 안 눌린다.
            _retired = false;
            _retireTarget = id;
            Focus(actionRect, RetireHintText);
            Sub("retire");
            yield return WaitFor(() => _retired || _panel.ActionRect == null);
            _retireTarget = SimEntityId.None;
            if (!_retired)
            {
                Debug.Log("[CoreFirstRunGuide] 철수 전에 선택이 풀렸다 — 철수 구간을 건너뛴다.", this);
                yield break;
            }

            // ⑨ 퇴근 비행 관람 — **정지를 푼다.** 비행은 Battle 도메인 델타로 도는 연출이라 멈춘 채 기다리면 유닛이 공중에 선다.
            _guidance.ClearFocus();
            _guidance.Hide();
            DimOnly();
            Unfreeze();
            yield return WaitUnscaled(_config.retireWatchSeconds);
            Freeze();
            report(true);
        }

        private IEnumerator RunAttach()
        {
            Block("B4");
            // 재개 구간은 **진짜 플레이**다 — 정지를 푼다(차단막은 유지 — 보기만 하는 구간).
            _guidance.Hide();
            Unfreeze();
            DimOnly();
            yield return WaitUnscaled(_config.resumeBeforeAttachSeconds);

            // 부착할 유닛이 없을 때만 차단막을 내려 **더 놓을 기회**를 준다(안전 밸브).
            if (!TryResolveHost(out _, out _, out _))
            {
                HideDim();
                Sub("host");
                yield return WaitFor(() => TryResolveHost(out _, out _, out _));
                DimOnly();
            }
            if (!TryResolveHost(out var host, out var hostUnit, out var hostRect)) yield break;

            Freeze();

            // 4.0 항아리 소개 — 가리키기만 한다(구멍 없음 — 지금 누르면 대상 없이 손패만 열린다).
            var jar = _gauge != null ? _gauge.HitRect : null;
            if (jar != null && jar.gameObject.activeInHierarchy)
            {
                _overlay.SetHoles(null);
                ShowDim();
                _guidance.ShowMessage(JarHintText, false);
                _guidance.FocusUi(jar);
                yield return WaitUnscaled(_config.jarHintSeconds);
                _guidance.ClearFocus();
            }

            // 4.1 유닛 선택. B3 를 흘려보낸 판은 유닛을 한 번도 안 골랐으므로 «다시» 를 뺀다.
            string hostName = hostUnit != null ? hostUnit.displayName : "배치한";
            string reselect = _b3Completed ? string.Format(ReselectFormat, hostName) : string.Format(SelectHostFormat, hostName);
            if (_selection == null || _selection.Selected != host)
            {
                Focus(hostRect, reselect);
                Sub("select");
                yield return WaitFor(() => _selection != null && _selection.Selected == host);
            }

            // 4.2 카드 — 지금 부착 가능한 부착 카드에만 구멍. ⚠ 딜인만 **상한 대기**하고, 그러고도 0 이면 건너뛴다 —
            // 조건 대기로 두면 낼 카드가 0 인 판에서 정지 + 멈춘 판 시계로 앱이 잠긴다(옛 주석).
            float grace = 0f;
            while (AttachableCardCount() == 0 && grace < _config.cardDealInGraceSeconds)
            {
                grace += Time.unscaledDeltaTime;
                yield return null;
            }
            if (AttachableCardCount() == 0)
            {
                Debug.Log("[CoreFirstRunGuide] 낼 수 있는 드림캐쳐가 없다 — 부착 구간을 건너뛴다.", this);
                yield break;
            }
            var hand = _driver.Match.Hand;
            int baseline = hand.CountAttachedTo(host);
            _guidance.ClearFocus();

            // ⚠ 구멍을 한 번만 잡으면 안 된다 — 딜인이 늦게 끝난 카드도 열리도록 구성이 바뀔 때마다 다시 잡는다.
            int shown = -1;
            Sub("card");
            while (hand.CountAttachedTo(host) <= baseline)
            {
                int now = AttachableCardCount();
                if (now != shown)
                {
                    shown = now;
                    _overlay.SetHoles(CollectAttachableCardRects());
                    _guidance.ShowMessage(now == 4 ? CardText : CardFallbackText, false);
                }
                if (!_driver.Match.World.IsAlive(host)) yield break;   // 숙주가 떠나면 조건이 영영 안 선다
                yield return null;
            }

            // 4.3 마무리 — 문구만(구멍 없음).
            _guidance.ClearFocus();
            yield return WaitUnscaled(_config.attachSettleSeconds);
            _overlay.SetHoles(null);
            _guidance.ShowMessage(AttachDoneText, false);
            yield return WaitUnscaled(_config.goalMessageSeconds);
            _b4Completed = true;
        }

        // B5 — 정지와 차단막을 **둘 다** 내린 뒤 시계만 가리킨다. 안내는 레이캐스트를 안 받아 플레이는 계속된다.
        private IEnumerator RunSurvivalHint()
        {
            if (!_b4Completed) yield break;
            Block("B5");
            Unfreeze();
            HideDim();

            var timer = _scoreHud != null ? _scoreHud.TimerFocusRect : null;
            if (timer == null || !timer.gameObject.activeInHierarchy)
            {
                Debug.LogWarning("[CoreFirstRunGuide] 시간 UI를 찾지 못했다 — 생존 안내를 건너뛴다.", this);
                yield break;
            }
            _guidance.ShowMessage(SurvivalText, false);
            _guidance.FocusUi(timer);
            // 행동을 요구하지 않는 전환 안내 — 연 순간 계약을 다 이행했다(표시 대기 중 판이 끝나도 완료로 친다).
            _b5Completed = true;
            yield return WaitUnscaled(_config.survivalHintSeconds);
            _guidance.ClearFocus();
            _guidance.Hide();
        }

        // ── 닫기 ────────────────────────────────────────────────────────────
        private void Close()
        {
            if (_closed) return;
            _closed = true;
            ShowBriefing(View.CoreMapOverlay.Briefing.None);
            _guidance.ClearFocus();
            _guidance.ClearWorldMarkers();
            _guidance.Hide();
            HideDim();
            Unfreeze();
            ReleaseIntroHold();
            Block("closed");

            // ⚠ 건너뛰었거나 대기 중에 판이 먼저 끝났으면 완료로 기록하지 않는다(옛 계약 11) — 1회성이라 기록해 버리면
            // 핵심을 한 번도 못 본 계정이 다시 볼 기회를 잃는다.
            if (_b3Completed && _b4Completed && _b5Completed && _profile != null && _profile.profile != null)
            {
                _profile.profile.firstRunTutorialDone = true;
                try { (ProfileSaver ?? ProfileStore.Save)(_profile.profile); }
                catch (System.Exception ex) { Debug.LogWarning($"[CoreFirstRunGuide] 완료 기록 저장 실패: {ex.Message}", this); }
                CompletionRecorded = true;
                Debug.Log("[CoreFirstRunGuide] 온보딩 완료 — 기록했다.", this);
            }
            else
            {
                Debug.Log($"[CoreFirstRunGuide] 미완료로 끝났다(b3={_b3Completed} b4={_b4Completed} b5={_b5Completed}) — 다음 판에 다시 뜬다.", this);
            }
            _running = false;
        }

        // ── 읽기 모델 질의(판정이 아니다 — 안내 스크립트의 «지금 무엇을 보여 줄까») ─────────────────

        private int DefIndexOf(DefenderUnitData unit)
        {
            if (unit == null) return -1;
            var assets = _driver.DefenderAssets;
            for (int i = 0; i < assets.Count; i++) if (assets[i] == unit) return i;
            return -1;
        }

        // 옛 `DefenderSelector.IsSlotUsableNow` — 코어의 슬롯 질문(`SlotBlock`) 하나가 소진·쿨타임·코스트를 다 답한다.
        private bool SlotUsableNow(int def) => _driver.Match.Placement.SlotBlock(def) == RejectReason.None;

        // 옛 `Armed`(탭 배치) · `UserDragStarted`(드래그) 두 신호의 후계 — 새 입력은 둘을 상태로 들고 있다.
        private bool IsPicking(int def)
            => _placement != null && (_placement.ArmedDefIndex == def
                                      || (_placement.IsDragging && _tray != null && _tray.DraggingDefIndex == def));

        private bool TryOnBoard(int def, out SimEntityId id)
        {
            id = SimEntityId.None;
            var units = _driver.Units;
            for (int i = 0; i < units.Count; i++)
            {
                var u = units[i];
                if (u.Kind != UnitKind.Defender || u.Dead || u.DefIndex != def) continue;
                id = u.Id;
                return true;
            }
            return false;
        }

        // 부착·철수 대상 = **트레이 칸으로 고를 수 있는** 배치 유닛(옛 B4 의 유일한 불변식). 구멍이 트레이 칸 하나이고,
        // 「소진」 칸만 그 유닛을 연다(`SelectionInput.SelectByTraySlot`) — 소진 아닌 칸을 가리키면 조건이 영영 안 선다.
        private bool TrySelectableHost(DefenderUnitData unit, out SimEntityId id, out RectTransform slotRect)
        {
            id = SimEntityId.None;
            slotRect = null;
            int def = DefIndexOf(unit);
            if (def < 0 || _tray == null) return false;
            if (!TryOnBoard(def, out id)) return false;
            if (_driver.Match.Placement.SlotBlock(def) != RejectReason.LimitReached) return false;
            return _tray.TryGetSlotRect(def, out slotRect) && slotRect != null;
        }

        // 우선순위: 샷건맨(방금 놓은 것) → 말파이트 → 트레이의 아무 유닛(첫 유닛이 죽은 판 대비).
        private bool TryResolveHost(out SimEntityId id, out DefenderUnitData unit, out RectTransform slotRect)
        {
            if (TrySelectableHost(_secondaryUnit, out id, out slotRect)) { unit = _secondaryUnit; return true; }
            if (TrySelectableHost(_primaryUnit, out id, out slotRect)) { unit = _primaryUnit; return true; }
            unit = null;
            var assets = _driver.DefenderAssets;
            for (int i = 0; i < assets.Count; i++)
            {
                var u = assets[i];
                if (u == null || u == _primaryUnit || u == _secondaryUnit) continue;
                if (!_driver.Match.Placement.InRoster(i)) continue;
                if (!TrySelectableHost(u, out id, out slotRect)) continue;
                unit = u;
                return true;
            }
            id = SimEntityId.None;
            slotRect = null;
            return false;
        }

        // 옛 `BattleBridge.AnyEnemyWithinTilesOfGoal` — 적 칸과 가장 가까운 골의 체비셰프 거리 ≤ N. 칸 = 칸 중심 격자
        // (`CellCenterSim` 과 같은 매핑). 옛 술어 그대로다(안내 박자의 트리거지 전투 판정이 아니다 — 제약 13 대상 밖).
        private bool AnyEnemyWithinTilesOfGoal(int tiles)
        {
            var goals = _driver.Definition.Map.Goals;
            if (goals == null || goals.Length == 0) return false;
            int r = Mathf.Max(0, tiles);
            float t = _driver.TileSize > 0f ? _driver.TileSize : 1f;
            var units = _driver.Units;
            for (int i = 0; i < units.Count; i++)
            {
                var u = units[i];
                if (u.Kind != UnitKind.Enemy || u.Dead) continue;
                var cell = new int2((int)math.round(u.Position.x / t), (int)math.round(u.Position.z / t));
                for (int g = 0; g < goals.Length; g++)
                {
                    var d = math.abs(cell - goals[g]);
                    if (math.max(d.x, d.y) <= r) return true;
                }
            }
            return false;
        }

        // 골 앵커 — 스테이지의 골 마커(옛 `TryGetGoalVisualAnchor`: 1번 골 마커의 `VisualAnchor`, 없으면 칸 중심).
        private bool TryGetGoalAnchor(out Vector3 world)
        {
            world = default;
            var goals = _driver.Definition.Map.Goals;
            if (goals == null || goals.Length == 0 || !BoardSpace.IsConfigured) return false;
            float t = _driver.TileSize;
            Vector3 center = BoardSpace.ToView(new float3(goals[0].x * t, 0f, goals[0].y * t));
            world = center;
            if (_driver.StageRoot == null) return true;
            float best = float.MaxValue;
            foreach (var m in _driver.StageRoot.GetComponentsInChildren<GoalMarker>(true))
            {
                float d = (m.transform.position - center).sqrMagnitude;
                if (d < best) { best = d; world = m.VisualAnchor(); }
            }
            return true;
        }

        // «지금 구멍을 뚫을 수 있는 카드» 의 단일 술어 — 세는 곳과 모으는 곳이 갈리면 n장이라 세어 놓고 0개를 넘긴다.
        private static bool IsAttachableSlot(Cards.CoreHandView.CardSlot slot)
        {
            if (slot == null || slot.rect == null || slot.card == null) return false;
            if (!slot.rect.gameObject.activeInHierarchy) return false;
            if (slot.card.type == CardType.Active) return false;   // 액티브는 끌어서 쓴다 — 부착 수가 안 오른다
            return slot.Playable;
        }

        private int AttachableCardCount()
        {
            if (_hand == null || _hand.Slots == null) return 0;
            int n = 0;
            var slots = _hand.Slots;
            for (int i = 0; i < slots.Count; i++) if (IsAttachableSlot(slots[i])) n++;
            return n;
        }

        private RectTransform[] CollectAttachableCardRects()
        {
            if (_hand == null || _hand.Slots == null) return null;
            var list = new List<RectTransform>();
            var slots = _hand.Slots;
            for (int i = 0; i < slots.Count; i++) if (IsAttachableSlot(slots[i])) list.Add(slots[i].rect);
            return list.ToArray();
        }

        // ── 도구 ────────────────────────────────────────────────────────────

        private void ShowBriefing(View.CoreMapOverlay.Briefing mode)
        {
            if (_mapOverlay != null) _mapOverlay.ShowBriefing(mode);
        }

        // ⚠ 우선순위 100 이 필수다 — 손패·유닛 선택이 같은 도메인을 priority 50 으로 요청한다(선례 메뉴).
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

        // 카운트다운 홀드. ⚠ 상한 자가 해제 — 이 컴포넌트가 죽거나 예외로 풀지 못해도 판은 시작된다(옛 `introHoldMaxSeconds`).
        private void BeginIntroHold(float maxSeconds)
        {
            if (_introHeld) return;
            _introHold = TimeManager.Instance.Request(TimeDomain.Battle, 0f, priority: 100);
            _introHeld = true;
            _introHoldUntil = Time.unscaledTime + Mathf.Max(1f, maxSeconds);
        }

        private void TickIntroHold()
        {
            if (!_introHeld || Time.unscaledTime < _introHoldUntil) return;
            Debug.LogWarning("[CoreFirstRunGuide] 인트로 홀드 상한 만료 — 자가 해제한다.", this);
            ReleaseIntroHold();
        }

        private void ReleaseIntroHold()
        {
            if (!_introHeld) return;
            _introHold.Dispose();
            _introHeld = false;
        }

        // ⚠ `Show()` 는 멱등이 아니다(알파를 되돌리고 페이드를 다시 돈다) — 전이할 때만 부른다.
        private void ShowDim()
        {
            _overlay.SetSortingOrder(_guidance.DimSortingOrder);
            if (_dimShown) return;
            // 보이지 않는 차단막(알파 = `dimOpacity`, 기본 0) — 입력만 막고 시야는 그대로 둔다.
            _overlay.Show(_config.dimOpacity);
            _dimShown = true;
        }

        private void HideDim()
        {
            if (_overlay != null) _overlay.Hide();
            _dimShown = false;
        }

        private void DimOnly()
        {
            _overlay.SetHoles(null);
            ShowDim();
        }

        private void Focus(RectTransform target, string text)
        {
            _overlay.SetHoles(new[] { target });
            ShowDim();
            _guidance.ShowMessage(text, false);
            _guidance.FocusUi(target);
        }

        // 러너 시계는 unscaled 다 — 자기가 Battle 을 멈춰 놓고 그 시계를 기다리면 영영 안 온다.
        private static IEnumerator WaitUnscaled(float seconds)
        {
            float t = 0f;
            while (t < seconds) { t += Time.unscaledDeltaTime; yield return null; }
        }

        // **타임아웃이 없다**(옛 사용자 결정). 대신 모든 조건 대기는 만족 가능해야 한다 — 선행조건이 없으면 진입 전에 건너뛴다.
        private static IEnumerator WaitFor(System.Func<bool> done)
        {
            while (!done()) yield return null;
        }
    }
}
