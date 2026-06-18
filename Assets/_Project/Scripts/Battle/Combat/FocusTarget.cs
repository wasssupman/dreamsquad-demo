using Unity.Entities;

namespace Wassup.Battle.Combat
{
    // enemy-behavior-components Unit 0 — Combat-owned. Attached only to enemies with
    // targetMode == FocusUntilDead. AttackSystem records the locked target here and
    // keeps it until the target dies/despawns (range only gates firing, not the lock).
    public struct FocusTarget : IComponentData
    {
        public Entity current;
    }
}
