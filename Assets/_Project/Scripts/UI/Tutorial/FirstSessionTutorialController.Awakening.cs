using System.Collections;
using UnityEngine;
using Wassup.Core;
using Wassup.UI;

namespace Wassup.UI.Tutorial
{
    // 각성 안내(0·A·B단계) + 첫 판 각성 봉인.
    //
    // 이 partial 로 분리한 이유는 BattleBridge.BossLeap.cs 와 같다: 본체 파일은 여러 세션이
    // 동시 편집하고 이미 900줄을 넘겼다. 공유 파일에는 lifecycle 호출만 남기고 이 관심사의
    // 상태·SerializeField·코루틴은 전부 여기 둔다.
    //
    // 3단계 구조: 0단계(전투 시작 — 덱을 여는 문 둘) → A단계(낼 수 있는 카드 생김)
    // → B단계(손패 열림 — 오픈 경로로 문구가 갈림). 상세 계약은 spec units 12·16·17.
    public sealed partial class FirstSessionTutorialController
    {
        // unit 16 — 손패를 여는 문이 둘이다(항아리 · 유닛 선택). 포커스 링이 첫 번째 문을
        // 시연하는 동안 문구가 두 번째 문을 알린다.
        private const string AwakeningIntroText =
            "항아리를 누르거나 캐릭터를 탭하면\n드림캐쳐 덱이 열립니다";

        // unit 16 — B단계는 **오픈 경로**로 갈린다. 항아리로 연 손패에는 즉발 대상이 없어
        // 드래그가 유일한 사용법이고, 선택으로 연 손패에는 탭 즉발이 있다
        // (selection-hand-attach unit 3). 같은 문구를 쓰면 절반이 거짓말이 된다.
        private const string CardHintDragText =
            "포커스된 카드를 원하는 캐릭터로 끌어보세요!";
        private const string CardHintSelectionText =
            "카드를 탭하면 이 캐릭터에 바로 부착됩니다\n왼쪽에서 능력치와 부착 상태를 볼 수 있어요";

        [Header("Awakening")]
        [SerializeField] private DreamcatcherHandController handController;
        [SerializeField] private AwakeningGaugeView gaugeView;
        [SerializeField] private DreamcatcherHandView handView;

        private Coroutine _awakeningRoutine;
        private Coroutine _awakeningIntroRoutine;
        private bool _awakeningOfferedThisBattle;
        // unit 17 — 부착 안내는 경로별로 독립이다. 판당 래치도 경로별로 둔다: 저장은
        // TrySaveProfile 이 예외를 삼키므로 실패할 수 있는데, 그때도 한 판에서 같은 안내가
        // 반복되면 안 된다. (unit 12 의 `_awakeningArmedThisBattle` 을 대체한다 — 그 필드는
        // B 가 유일한 독자였고, A 선행 요구를 뗀 지금 아무도 읽지 않는다. unit 17 C절.)
        private bool _dragHintShownThisBattle;
        private bool _tapHintShownThisBattle;
        // unit 12 — 배틀 시작 인트로(0단계). _awakeningOfferedThisBattle 을 쓰면
        // A 단계("드림캐쳐 사용 준비 완료!")가 영영 안 뜨므로 전용 플래그를 둔다.
        private bool _awakeningIntroShownThisBattle;
        // hand-drag-tooltip unit 3 — "끌어보세요" 배너가 떠 있는 동안만 참. 카드 press 로
        // 조기 해제할 때 A 단계 프롬프트(다른 단계지만 _awakeningRoutine 을 공유)를
        // 잘못 걷지 않도록 단계를 명시적으로 구분한다.
        private bool _cardInstructionShowing;

        // ── lifecycle (본체가 부른다) ────────────────────────────────────────

        private void SubscribeAwakening()
        {
            if (handController != null)
            {
                handController.GaugeChanged += OnGaugeChanged;
                handController.HandChanged += OnHandChanged;
            }
            if (handView == null) return;
            handView.HandOpened += OnHandOpened;
            // unit 17 rev — 손패가 이미 열린 채 선택으로 **전환**되는 경우는 HandOpened 가
            // 안 온다(OpenForSelection 이 no-op). 두 신호를 함께 받아야 두 경로가 대칭이 된다.
            handView.SelectionTargetSet += OnSelectionTargetSet;
            handView.CardPeeked += OnCardPeeked;
        }

        private void UnsubscribeAwakening()
        {
            if (handController != null)
            {
                handController.GaugeChanged -= OnGaugeChanged;
                handController.HandChanged -= OnHandChanged;
            }
            if (handView == null) return;
            handView.HandOpened -= OnHandOpened;
            handView.SelectionTargetSet -= OnSelectionTargetSet;
            handView.CardPeeked -= OnCardPeeked;
        }

        // unit 10 — 첫 판 각성 봉인. 게이지 뷰를 아는 것은 이 관심사뿐이라, 본체는 "첫 판이다"
        // 라는 사실만 넘기고 표시 억제는 여기서 한다.
        private void ApplyAwakeningSeal(bool locked) => gaugeView?.SetSuppressed(locked);

        // 봉인은 이 컴포넌트가 살아있는 동안만 유효하다. 단 Battle 도중 해제하면 캐시된
        // _phase(Battle)로 패널이 켜졌다 꺼지는 왕복이 생기고, 씬 파괴 순서에 따라 앰비언트
        // 코루틴이 되살아난다. Battle 중 비활성화는 여전히 첫 판이므로 봉인 유지가 의미상 맞다.
        private void ReleaseAwakeningSealIfNotBattle()
        {
            if (gameManager == null || gameManager.CurrentPhase != GamePhase.Battle)
                gaugeView?.SetSuppressed(false);
        }

        // 판당 래치만 되돌린다(코루틴은 건드리지 않는다 — ResetAwakeningSession 의 몫).
        private void ResetAwakeningLatches()
        {
            _awakeningOfferedThisBattle = false;
            // unit 17 — 경로별 래치도 함께(unit 12 의 비대칭 방지 규칙 계승).
            _dragHintShownThisBattle = false;
            _tapHintShownThisBattle = false;
            _awakeningIntroShownThisBattle = false;
        }

        private void ResetAwakeningSession(bool hide)
        {
            _cardInstructionShowing = false;
            if (_awakeningRoutine != null)
            {
                StopCoroutine(_awakeningRoutine);
                _awakeningRoutine = null;
            }
            if (_awakeningIntroRoutine != null)
            {
                StopCoroutine(_awakeningIntroRoutine);
                _awakeningIntroRoutine = null;
            }
            ResetAwakeningLatches();
            if (hide && !_coreActive) guidance?.Hide();
        }

        // ── 0단계 ────────────────────────────────────────────────────────────

        private void OnGaugeChanged(int _) => EvaluateAwakeningHint();

        private void OnHandChanged(DreamcatcherHandController.HandChangeReason _) => EvaluateAwakeningHint();

        // unit 12 — 0단계. gaugeView.OnPhaseChanged 가 같은 PhaseChanged 의 다른 구독자라
        // 패널 활성화 순서가 보장되지 않는다. Pulse() 는 비활성 패널에서 조용히 소실되므로
        // (포커스 링과 달리 다음 프레임에 복구되지 않는다) 한 프레임 미룬다.
        private void StartAwakeningIntro()
        {
            if (_awakeningLockedThisMatch || _awakeningIntroShownThisBattle) return;
            if (guidance == null || gaugeView == null) return;
            if (!TutorialProgress.ShouldRunAwakeningIntro(profileSO)) return;

            if (_awakeningIntroRoutine != null) StopCoroutine(_awakeningIntroRoutine);
            _awakeningIntroRoutine = StartCoroutine(AwakeningIntroRoutine());
        }

        private IEnumerator AwakeningIntroRoutine()
        {
            yield return null;
            _awakeningIntroRoutine = null;

            if (_awakeningLockedThisMatch || _awakeningIntroShownThisBattle) yield break;
            if (gameManager == null || gameManager.CurrentPhase != GamePhase.Battle) yield break;
            if (guidance == null || gaugeView == null) yield break;
            if (!TutorialProgress.ShouldRunAwakeningIntro(profileSO)) yield break;

            // 지연의 목적이 패널 활성화를 기다리는 것이므로 실제로 활성인지 확인한다.
            // 아직 비활성이면 Pulse 가 조용히 no-op 되고 링도 안 뜨는데, 플래그만 소모돼
            // 0단계가 펄스 없이 사라진다. 소모하지 말고 다음 기회에 다시 시도한다.
            RectTransform hit = gaugeView.HitRect;
            if (hit == null || !hit.gameObject.activeInHierarchy) yield break;

            _awakeningIntroShownThisBattle = true;
            // arm 하지 않는다. 카드 사용법(B단계)은 A단계가 실제로 뜬 뒤에만 의미가 있고,
            // gaugeStart 는 SO/시트 튜너블이라 "전투 시작 게이지 0" 은 불변식이 아니다.
            // 0단계가 arm 하면 gaugeStart > 0 인 순간 B 가 앞당겨 발화해 완료가 저장되고
            // A 문구가 그 계정에서 영영 안 뜬다.
            gaugeView.Pulse();
            guidance.ShowMessage(AwakeningIntroText, showSkip: false);
            guidance.FocusUi(gaugeView.HitRect);
            if (_awakeningRoutine != null) StopCoroutine(_awakeningRoutine);
            _awakeningRoutine = StartCoroutine(HideAwakeningPromptRoutine());
        }

        // ── A단계 ────────────────────────────────────────────────────────────

        private void EvaluateAwakeningHint()
        {
            // unit 10 — 첫 판은 버튼이 없으므로 힌트도 뜨면 안 된다. 이 가드가 없으면
            // 없는 버튼을 가리키고, _awakeningOfferedThisBattle 이 Pulse 보다 먼저 세팅돼
            // 힌트가 조용히 소모된다.
            if (_awakeningLockedThisMatch) return;
            if (_coreActive || _awakeningOfferedThisBattle || gameManager == null ||
                gameManager.CurrentPhase != GamePhase.Battle || guidance == null || gaugeView == null ||
                handController == null || !TutorialProgress.ShouldRunAwakeningIntro(profileSO)) return;
            if (!HasAffordableCard()) return;

            _awakeningOfferedThisBattle = true;
            gaugeView.Pulse();
            guidance.ShowMessage("드림캐쳐 사용 준비 완료!", showSkip: false);
            guidance.FocusUi(gaugeView.HitRect);
            if (_awakeningRoutine != null) StopCoroutine(_awakeningRoutine);
            _awakeningRoutine = StartCoroutine(HideAwakeningPromptRoutine());
        }

        private bool HasAffordableCard()
        {
            if (handController == null) return false;
            var hand = handController.Hand();
            for (int i = 0; i < hand.Count; i++)
                if (handController.CanUse(hand[i].entryId)) return true;
            return false;
        }

        private IEnumerator HideAwakeningPromptRoutine()
        {
            yield return WaitUnscaled(guidance.AwakeningPromptSeconds);
            _awakeningRoutine = null;
            if (!_coreActive) guidance.Hide();
        }

        // ── B단계 ────────────────────────────────────────────────────────────

        // 손패가 닫혀 있다가 열렸다(항아리 오픈 · 선택 기인 오픈 둘 다 여기로 온다).
        private void OnHandOpened() => EvaluateCardHint();

        // unit 17 rev — 손패가 **이미 열린 채** 선택 대상이 잡혔다. 항아리로 먼저 열어둔 뒤
        // 유닛을 탭하는 경로가 이것뿐이라, 이 신호가 없으면 탭 즉발 안내가 영영 안 뜬다
        // (사용자 보고 2026-07-30). 손패가 닫힌 상태에서 온 신호는 아래 State 가드가 흘려보내고
        // 곧이어 오는 HandOpened 가 처리한다.
        private void OnSelectionTargetSet() => EvaluateCardHint();

        private void EvaluateCardHint()
        {
            // unit 10 — 첫 판엔 각성 안내가 하나도 뜨지 않는다. 예전엔 이 경로가
            // `_awakeningOfferedThisBattle`(A 가 떴다) 요구를 통해 **간접적으로** 보호됐는데,
            // unit 17 이 그 요구를 걷어내며 보호도 함께 사라졌다. 지금은 첫 판에 손패를 열
            // 방법이 없어(항아리 숨김 + unit 15 선택 봉인) 도달 불가지만, 그 두 장치에만
            // 기대지 않도록 여기서 직접 막는다.
            if (_awakeningLockedThisMatch) return;
            if (gameManager == null || gameManager.CurrentPhase != GamePhase.Battle ||
                handView == null || handView.State != DreamcatcherHandView.HandState.Hand ||
                guidance == null) return;

            // unit 17 — 오픈 경로가 곧 어떤 안내인지를 정한다. 두 안내는 서로 다른 조작을
            // 가르치므로 **각자의 pending 과 각자의 판당 래치**로 갈린다 — 한쪽을 본 것이
            // 다른 쪽을 소비하지 않는다(이 unit 의 요점).
            bool selection = handView.InSelectionMode;
            if (selection
                ? (_tapHintShownThisBattle || !TutorialProgress.ShouldRunTapAttachHint(profileSO))
                : (_dragHintShownThisBattle || !TutorialProgress.ShouldRunDragAttachHint(profileSO))) return;

            // unit 12 의 `_awakeningOfferedThisBattle && _awakeningArmedThisBattle`(= A 가 실제로
            // 떴다) 요구는 여기서 뺐다. 그 목적은 "B 가 A 의 완료 저장을 앞당겨 훔치는 것"을
            // 막는 것이었는데, 저장이 경로별이라 훔칠 대상이 없고 인트로는 파생이라 저장 자체가
            // 없다. 반대로 남겨두면 인트로가 끝난 뒤 A 가 안 떠서 `_awakeningOfferedThisBattle`
            // 이 false 로 고정되고, **못 배운 나머지 한쪽이 영영 발화하지 못한다**(unit 17 C절).
            // 가드의 나머지 의미("낼 수 있는 카드가 있을 때만")는 아래 탐색이 강제한다.
            // selection-hand-attach unit 17 — `usable`(각성치)이 아니라 `Playable` 을 본다.
            // 선택 중이면 각성치가 충분해도 그 유닛에 못 붙는 카드는 딤 + 조작 불가라,
            // 거기에 포커스를 주면 튜토리얼이 **거절당하는 제스처**를 가르친다. 후보가 없으면
            // 아래 null 반환으로 안내를 미룬다(래치 미소비 — 다음 기회에 다시 시도).
            RectTransform usable = null;
            var slots = handView.Slots;
            for (int i = 0; i < slots.Count; i++)
            {
                var slot = slots[i];
                if (slot != null && slot.Playable && slot.card != null && slot.rect != null)
                {
                    usable = slot.rect;
                    break;
                }
            }
            if (usable == null) return;

            if (_awakeningRoutine != null) StopCoroutine(_awakeningRoutine);
            // unit 16 — InSelectionMode 는 SelectionTarget 파생값이고 DcInspectController 가
            // SetSelectionTarget → OpenForSelection 순으로 부르므로, 두 진입 신호 어느 쪽에서든
            // 이 시점엔 이미 확정돼 있다(SelectionTargetSet 은 대입 직후, HandOpened 는 그 뒤).
            // 포커스는 두 경우 모두 usable 슬롯이다 — 지시의 대상은 카드다.
            guidance.ShowMessage(selection ? CardHintSelectionText : CardHintDragText, showSkip: false);
            guidance.FocusUi(usable);

            if (selection)
            {
                _tapHintShownThisBattle = true;
                if (TutorialProgress.CompleteTapAttachHint(profileSO.profile)) TrySaveProfile();
            }
            else
            {
                _dragHintShownThisBattle = true;
                if (TutorialProgress.CompleteDragAttachHint(profileSO.profile)) TrySaveProfile();
            }
            _cardInstructionShowing = true;
            _awakeningRoutine = StartCoroutine(HideCardInstructionRoutine());
        }

        private IEnumerator HideCardInstructionRoutine()
        {
            yield return WaitUnscaled(guidance.CardInstructionSeconds);
            _awakeningRoutine = null;
            _cardInstructionShowing = false;
            guidance.Hide();
        }

        // hand-drag-tooltip unit 3 — 카드를 누른 순간 "끌어보세요" 지시는 이행됐다.
        // 정보 가치가 소진됐고, 배너(상단 중앙 880x116, 거의 불투명)를 그대로 두면
        // 같은 대역에 뜨는 드래그 툴팁 본문을 덮는다. 남은 대기 시간을 버리고 걷는다.
        private void OnCardPeeked()
        {
            if (!_cardInstructionShowing) return;
            _cardInstructionShowing = false;
            if (_awakeningRoutine != null)
            {
                StopCoroutine(_awakeningRoutine);
                _awakeningRoutine = null;
            }
            guidance.Hide();
        }
    }
}
