using Unity.Entities;

namespace Wassup.Battle.Effects
{
    public enum StatKind : byte { DamageMul, AttackSpeedMul, DmgTakenMul, RegenPerSec }

    public enum StackKind : byte { None, Fire, Ice, Bleed, Poison }

    public enum CombineOp : byte { Multiplicative, Additive, Override }

    // 임베딩 컨벤션 — IComponentData/IBufferElementData 아님. 두 Slot struct 에 직접 임베딩.
    public struct ModifierHeader
    {
        public float remaining;
        public Entity source;
        public ushort stackId;
    }
}
