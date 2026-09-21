namespace Wassup.UnitAi
{
    // defender-deploy-phase — 배치 페이즈 시계의 규칙. 「Deploying 이면 남은 시간을 줄이고, 0 에 닿으면 활성화」.
    // 적용 레이어(DeploymentActivationSystem)는 컴포넌트를 읽어 이 함수를 부르고, true 면 태그 제거·JustDeployed·이벤트를 실행한다.
    // 부동소수 누적으로 「정확히 0」에 기대지 않는다 — ≤ 0 이 판정이다.
    public static class DeployPhaseClock
    {
        // 반환 = 이번 틱에 활성화해야 하는가. deploying 이 아니면(비행 중) 시계는 흐르지 않는다.
        public static bool Advance(bool deploying, ref float remaining, float dt)
        {
            if (!deploying) return false;
            remaining -= dt;
            return remaining <= 0f;
        }
    }
}
