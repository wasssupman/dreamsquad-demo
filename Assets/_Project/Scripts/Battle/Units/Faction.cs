using System;

namespace Wassup.Battle.Units
{
    [Flags]
    public enum Faction : int
    {
        None = 0,
        Defender = 1 << 0,
        Enemy = 1 << 1,
        BlockingHazard = 1 << 2,
        // goal-tower-siege unit 0 — 골 타워. **base targetMask 에 넣지 않는다** — 골에
        // 도달한(PastGoalTag) 적에게만 부여한다(unit 1). base 에 넣으면 원거리 적이 골에서
        // 사거리만큼 떨어진 지점에서 Engaging 으로 멈춰 골에 영영 도달하지 않는다.
        GoalTower = 1 << 3,
    }
}
