using Unity.Mathematics;

namespace Wassup.Battle.Combat.Projectile
{
    // Pure progress math for MovementKind.SkyFall. The sim position holds at the
    // cell-locked impact; only elapsed advances. Progress feeds the view-space
    // falling visual (unit 9) — like BallisticArc, height is presentation-side
    // because BoardSpace.ToView drops sim Y. Burst-compatible, EditMode-testable.
    public static class SkyFall
    {
        // t in [0,1]. Non-positive flightTime → 1 (arrive immediately, matching
        // the legacy warningSec=0 "resolve on first tick" behavior).
        public static float Progress(float elapsed, float flightTime)
            => flightTime > 0f ? math.saturate(elapsed / flightTime) : 1f;

        // Arrival condition owned by the trajectory (spec contract 3).
        public static bool Arrived(float elapsed, float flightTime)
            => elapsed >= flightTime;

        // 낙하 압축 재매핑(뷰 전용, unit 9): 전체 진행 p(0~1)를 비행 후반
        // fallPortion 구간의 낙하 진행(0~1)으로 재매핑한다. 대기 구간
        // (p < 1-fallPortion)은 0 — 뷰는 이 동안 숨겨진다. fallPortion >= 1 은
        // 항등(전 구간 등속 낙하). 게임플레이 타이밍(flightTime)은 불변.
        public static float FallProgress(float p, float fallPortion)
            => fallPortion >= 1f
                ? p
                : math.saturate((p - (1f - fallPortion)) / math.max(fallPortion, 0.0001f));
    }
}
