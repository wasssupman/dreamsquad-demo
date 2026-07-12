using Unity.Entities;

namespace Wassup.Battle.Effects
{
    // dreamcatcher-new-abilities unit 0 — DamageVsCcMul: 활성 CcEffect(기절/수면/DoT/넉백)
    // 걸린 적 대상 데미지 배율 (base 1). Slow(이동감속)은 CcEffect 아니라 미포함. append-only.
    public enum StatKind : byte { DamageMul, AttackSpeedMul, DmgTakenMul, RegenPerSec, MoveSpeedMul, DamageVsCcMul }

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
