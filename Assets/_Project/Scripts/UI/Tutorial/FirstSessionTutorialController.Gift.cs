using UnityEngine;
using Wassup.Core;
using Wassup.Data;
using Wassup.UI;

namespace Wassup.UI.Tutorial
{
    // 선물 단계 워크스루 (spec units 6~9). 두 번째 판, core 완료 이후에만 노출된다.
    //
    // GiftPhaseView 가 홀드/탭 seam 을 소유하고 이 파일은 문구 · elevated 말풍선 · 완료 저장만
    // 공급한다. 카드 kind/장수는 실제 구성된 덱에서 읽으므로 문구가 실물과 어긋나지 않는다.
    //
    // 본체에서 분리한 이유는 BattleBridge.BossLeap.cs 와 같다 — 공유 파일에는 lifecycle
    // 호출만 남긴다.
    public sealed partial class FirstSessionTutorialController
    {
        [Header("Gift walkthrough")]
        [SerializeField] private GiftPhaseView giftView;

        private void SubscribeGift()
        {
            if (giftView != null)
            {
                giftView.TutorialHoldEntered += OnGiftHoldEntered;
                giftView.TutorialHoldReleased += OnGiftHoldReleased;
                return;
            }
            Debug.LogWarning("[FirstSessionTutorial] giftView 미배선 — 선물 튜토리얼 문구를 생략합니다(연출 홀드는 유지).", this);
        }

        private void UnsubscribeGift()
        {
            if (giftView == null) return;
            giftView.TutorialHoldEntered -= OnGiftHoldEntered;
            giftView.TutorialHoldReleased -= OnGiftHoldReleased;
        }

        private void OnGiftHoldEntered(GiftPhaseView.GiftTutorialHold hold)
        {
            if (guidance == null) return;
            guidance.SetElevated(true);
            int baseN = handController != null ? handController.GiftBaseCards.Count : 10;
            int added = handController != null ? handController.GiftAddedCards.Count : 2;
            if (hold == GiftPhaseView.GiftTutorialHold.Reveal)
            {
                string kind = handController != null && handController.GiftKind == GiftKind.Rim ? "림" : "루시드";
                guidance.ShowMessage(
                    $"{kind}의 선물은 내 덱 {baseN}장에 더해 꿈결의 집행자들이 {added}장의 추가 드림캐쳐를 제공합니다.",
                    showSkip: false);
            }
            else
            {
                guidance.ShowMessage(
                    $"{baseN}장 + {added}장의 카드가 무작위로 섞여서 덱 순서가 배정됩니다.",
                    showSkip: false);
            }
        }

        private void OnGiftHoldReleased(GiftPhaseView.GiftTutorialHold hold)
        {
            if (guidance == null) return;
            if (hold == GiftPhaseView.GiftTutorialHold.Reveal)
            {
                // 스택 수렴은 짧다 — 문구만 접고 elevated 는 셔플 홀드까지 유지.
                guidance.ShowMessage(null, showSkip: false);
                return;
            }
            // 셔플 홀드 해제 = 완료 저장 지점(사용자 결정 2026-07-20).
            CompleteGiftProgress();
            guidance.Hide();
            guidance.SetElevated(false);
        }

        private void CompleteGiftProgress()
        {
            if (profileSO == null || !profileSO.IsLoadedThisSession || profileSO.profile == null) return;
            if (!TutorialProgress.CompleteGiftTutorial(profileSO.profile)) return;
            TrySaveProfile();
        }
    }
}
