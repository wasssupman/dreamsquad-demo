using Unity.Entities;
using Unity.Mathematics;

namespace Wassup.Battle.Effects
{
    public struct Hazard : IComponentData
    {
        public float remainingLife;
        // unit 19 (distance-based-range) — 존 틱의 **연속 원** 정의. 판정은 이제 베이크된
        // 셀 집합이 아니라 「중심 원 + 피해자 몸」이다. radiusTiles < 0 = 존 효과 없음.
        // 저작 모양 매핑: SingleCell→0 · Square3x3→1 · RadiusSquare→max(1, radius).
        public int2 originCell;
        public int radiusTiles;
    }

    [InternalBufferCapacity(2)]
    public struct HazardEffectsBuffer : IBufferElementData
    {
        public HazardEffect effect;
    }

    [InternalBufferCapacity(9)]
    public struct HazardCellsBuffer : IBufferElementData
    {
        public int2 cell;
    }
}
