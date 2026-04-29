using Unity.Entities;

namespace Wassup.Battle.Effects
{
    // Marker + meta for blocking hazards. Spawn owns writes; runtime systems read.
    public struct BlockingHazard : IComponentData
    {
        public int hazardSoIndex;
        public float maxHp;
    }
}
