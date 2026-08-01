using UnityEngine;
using Wassup.Core;
using Wassup.UI;

namespace Wassup.UI.Tutorial
{
    // 기믹 리빌 홀드 안내 (spec unit 24). 두 번째 판에서 리빌이 요약까지 뜬 순간 멈춰 세우고
    // 구조 한 줄을 얹는다 — 이번 판의 구체 룰은 리빌 자신(ruleLabel + summary)이 이미 말하므로
    // 중복하지 않는다. 첫 판은 GimmickPhaseView 가 리빌을 통째로 건너뛰므로, 리빌이 처음
    // 보이는 자리가 곧 가르칠 자리다.
    //
    // GimmickPhaseView 가 홀드/탭 seam 을 소유하고 이 파일은 문구 · 앵커 · 완료 저장만 공급한다.
    // 본체에서 분리한 이유는 .Gift.cs 와 같다 — 공유 파일에는 lifecycle 호출만 남긴다.
    public sealed partial class FirstSessionTutorialController
    {
        [Header("Gimmick reveal hint")]
        [SerializeField] private GimmickPhaseView gimmickView;

        // 이번 판의 구체 룰이 아니라 **매 판 바뀐다는 구조**를 말한다.
        private const string GimmickRevealHintText =
            "매 판마다 특수 룰이 하나 걸립니다. 이번 판은 이것!";

        // 정리 창구의 가드. 리빌은 **조용히 끝나는 경로**가 있다(재진입 · GimmickPhaseView
        // 비활성 — 둘 다 TutorialHoldReleased 를 발행하지 않는다, 발행하면 읽지도 않은 안내가
        // 완료 저장되므로). 그 경로에서 말풍선과 앵커를 아무도 되돌리지 않으면 배치 화면에
        // 문구가 남고, 다음 안내(각성)가 화면 하단의 엉뚱한 위치에 뜬다.
        private bool _gimmickHintActive;

        private void SubscribeGimmickReveal()
        {
            if (gimmickView != null)
            {
                gimmickView.TutorialHoldEntered += OnGimmickHoldEntered;
                gimmickView.TutorialHoldReleased += OnGimmickHoldReleased;
                return;
            }
            Debug.LogWarning("[FirstSessionTutorial] gimmickView 미배선 — 리빌 안내 문구를 생략합니다(연출은 유지).", this);
        }

        private void UnsubscribeGimmickReveal()
        {
            if (gimmickView == null) return;
            gimmickView.TutorialHoldEntered -= OnGimmickHoldEntered;
            gimmickView.TutorialHoldReleased -= OnGimmickHoldReleased;
        }

        private void OnGimmickHoldEntered()
        {
            if (guidance == null) return;
            // 탭의 주인은 리빌 패널이다. guidance 탭 캐처(SetTapToContinue)를 켜면 전체화면
            // raycastTarget 이 리빌의 TapCatcher 를 덮어 홀드가 폴백 만료로만 풀린다.
            // ContinueTapped 도 쓰지 않는다 — 클래스 안내·스트레스 정지와 소비자를 다투지 않는다.
            //
            // SetElevated 도 부르지 않는다: guidance 기본 order(1500)가 리빌 패널(20)을 이미
            // 압도한다. .Gift.cs 의 호출은 order 가 10↔40 이던 시절의 잔재다.
            _gimmickHintActive = true;
            guidance.SetMessageAnchor(TutorialGuidanceView.MessageAnchor.GimmickReveal);
            guidance.ShowMessage(GimmickRevealHintText, showSkip: false);
        }

        // 완료 저장은 **실제로 홀드를 통과한** 이 경로에만 있다. 아래 정리 창구는 저장하지
        // 않는다 — 조용히 끝난 판에서 안내가 소진되면 플레이어가 문구를 영영 못 본다.
        private void OnGimmickHoldReleased()
        {
            CompleteGimmickRevealProgress();
            StopGimmickRevealHint();
        }

        // 정리 단일 창구(StopBattleHudHint 와 같은 형태). 호출처 3곳 — 홀드 정상 해제 ·
        // OnPhaseChanged 의 비-Gimmick 경로 · OnDisable. `_gimmickHintActive` 가드가 본체다:
        // 이 함수는 안내가 없을 때도 불리므로(매 페이즈 전이) 가드가 없으면 남의 말풍선을 걷는다.
        private void StopGimmickRevealHint()
        {
            if (!_gimmickHintActive) return;
            _gimmickHintActive = false;
            guidance?.Hide();
            // 앵커를 되돌리지 않으면 다음 안내(각성 · 선물)가 화면 하단에 엉뚱하게 뜬다.
            guidance?.SetMessageAnchor(TutorialGuidanceView.MessageAnchor.Default);
        }

        private void CompleteGimmickRevealProgress()
        {
            if (profileSO == null || !profileSO.IsLoadedThisSession || profileSO.profile == null) return;
            if (!TutorialProgress.CompleteGimmickRevealHint(profileSO.profile)) return;
            TrySaveProfile();
        }
    }
}
