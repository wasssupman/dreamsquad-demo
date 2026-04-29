using Unity.Entities;
using Unity.Mathematics;

namespace Wassup.Battle.Effects
{
    public enum CcKind : byte
    {
        Slow = 0,
        Impulse = 1,
        DoT = 2,
    }

    public struct CcEffect : IBufferElementData
    {
        public CcKind kind;
        public float3 vector;
        public float scalar;
        public float remainingTime;
    }
}
