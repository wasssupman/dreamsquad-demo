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
    }
}
