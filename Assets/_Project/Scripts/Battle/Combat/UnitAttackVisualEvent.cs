using Unity.Entities;
using Unity.Mathematics;

namespace Wassup.Battle.Combat
{
    // ECS→MonoBehaviour visual trigger for any attacker (defender or enemy) firing
    // an attack. SpineUnitPool consumes this to play attack animation + face the
    // target. Defender-specific side effects (cast VFX, attack VFX prefab) are
    // applied in BattleBridge by checking whether the attacker has DefenderUnitData.
    public struct UnitAttackVisualEvent
    {
        public Entity attacker;
        public float3 targetWorld;
    }
}
