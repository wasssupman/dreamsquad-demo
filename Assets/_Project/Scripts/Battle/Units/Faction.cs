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
        Goal = 1 << 3,   // goal-stability — 안정도 골 엔티티. 적 targetMask 전용(방어측 지원 시스템은 Defender 만 본다)
    }
}
