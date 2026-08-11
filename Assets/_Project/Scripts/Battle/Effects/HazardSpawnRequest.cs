using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace Wassup.Battle.Effects
{
    public struct HazardSpawnRequestsSingleton : IComponentData
    {
        public NativeQueue<HazardSpawnRequest> queue;
    }

    public struct HazardSpawnRequest
    {
        public HazardCastKind kind;
        public int dataIndex;
        public int2 centerCell;
        public int width;
        public int height;
        public Entity caster;
        public Entity target;
        // Cast-time target layer snapshot. The spawned zone carries this through
        // HazardEffect so later entrants obey the same target contract.
        public byte targetTraversalLayers;
    }
}
