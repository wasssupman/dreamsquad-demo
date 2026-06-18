using System;

namespace Wassup.Data
{
    // enemy-behavior-components Unit 0 — enemy behavior axes selected per-SO and
    // baked to ECS components. enemyClass is a label only; these drive runtime.

    public enum EnemyAttackMethod { None, Melee, Projectile }

    public enum EnemyTargetMode { None, Nearest, FocusUntilDead }

    public enum EnemyAimMode { StopToAttack, MoveAndShoot }

    // Allowed defender classes for an enemy's target filter. bit = 1 << (int)DefenderClass
    // (matches EnemyTargetFilter.classMask). Everything == ~0 == -1 preserves the
    // existing "-1 = all" convention. Bit 0 is intentionally unused: DefenderClass.None
    // has no flag, and targets without DefenderClassTag (hazards) bypass the mask.
    [Flags]
    public enum DefenderClassFlags
    {
        None = 0,
        Ranger = 1 << 1,
        Guardian = 1 << 2,
        Fighter = 1 << 3,
        Caster = 1 << 4,
        Support = 1 << 5,
        Everything = ~0,
    }
}
