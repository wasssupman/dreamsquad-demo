using Unity.Entities;

namespace Wassup.Battle.Effects
{
    public struct StatModifierSlot : IBufferElementData
    {
        public ModifierHeader header;
        public StatKind stat;
        public CombineOp op;
        public float magnitude;
    }
}
