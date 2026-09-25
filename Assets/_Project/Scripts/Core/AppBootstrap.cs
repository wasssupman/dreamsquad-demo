using UnityEngine;

namespace Wassup.Core
{
    // battle-core-rebuild unit 8b — **앱 전역 시작 훅**(rule-holders G17). 옛 `GameManager` 안에 있던 두 훅을 그대로 옮겼다.
    // 그 매니저는 옛 전투 씬 스코프라 「그 씬이 없으면 이 훅도 없다」가 unit 9 에서 사실이 된다 — 훅은 씬·인스턴스와
    // 무관하게 첫 씬 로드 전 1회 돌아야 하므로 씬과 무관한 자리가 필요했다. **한 곳에만 둔다**(두 곳이면 한쪽만 튜닝된다).
    public static class AppBootstrap
    {
        // 타겟 프레임 60 고정 — 앱 전역 관심사라 씬/인스턴스와 무관하게 앱 시작 시 1회만 세팅한다.
        // 로비가 첫 씬이면 씬 컴포넌트의 Awake 로는 콜드 스타트를 못 잡는다(플랫폼 기본 30fps 로 떨어짐).
        // vSyncCount=0 이어야 90/120Hz 패널에서 vSync 가 targetFrameRate 를 덮어쓰지 않고
        // 60 캡이 확실히 적용된다(모바일 배터리 절감).
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void ApplyFrameRateCap()
        {
            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = 60;
        }

        // gift-phase-removal unit 1 (리뷰 H2) — PrimeTween 동시 트윈 풀 예약. 효과가 **프로세스 전역**이라(전투 데미지
        // 넘버·VFX·HUD juice 가 같은 풀을 쓴다) 기본값 200 을 넘기면 런타임 리사이즈(1회 GC 할당 + 경고)를 맞는다.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void ReserveTweenCapacity() => PrimeTween.PrimeTweenConfig.SetTweensCapacity(400);
    }
}
