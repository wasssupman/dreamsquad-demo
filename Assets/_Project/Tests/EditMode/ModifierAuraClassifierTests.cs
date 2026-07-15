using NUnit.Framework;
using Unity.Collections;
using Wassup.Battle.Effects;

namespace Wassup.Tests.EditMode
{
    // dreamcatcher-empower-aura — 드림캐쳐 출처(ModifierOrigin.Dreamcatcher) 스탯 모디파이어 활성 판정.
    // 핵심 가드: 다른 출처(Dreamstone/Synergy/OnPlace) 제외, revoke(net=identity) 비활성, 방향 무관.
    public class ModifierAuraClassifierTests
    {
        private static StatModifierSlot Slot(StatKind stat, CombineOp op, float magnitude, ModifierOrigin origin)
            => new StatModifierSlot
            {
                header = new ModifierHeader { remaining = 1e9f, source = default, stackId = 0, origin = origin },
                stat = stat, op = op, magnitude = magnitude,
            };

        private static bool Eval(params StatModifierSlot[] arr)
        {
            var na = new NativeArray<StatModifierSlot>(arr, Allocator.Temp);
            try { return ModifierAuraClassifier.HasActiveDreamcatcherModifier(na); }
            finally { na.Dispose(); }
        }

        [Test]
        public void Empty_False()
            => Assert.IsFalse(Eval());

        [Test]
        public void DreamcatcherDamageBuff_True()
            => Assert.IsTrue(Eval(Slot(StatKind.DamageMul, CombineOp.Additive, 0.24f, ModifierOrigin.Dreamcatcher)));

        [Test]
        public void Dreamstone_SameStat_Excluded_False()
            // 드림스톤 로드아웃(같은 +24% 여도) 출처가 다르므로 제외 — 이번 버그의 핵심 가드.
            => Assert.IsFalse(Eval(Slot(StatKind.DamageMul, CombineOp.Additive, 0.24f, ModifierOrigin.Dreamstone)));

        [Test]
        public void Synergy_Excluded_False()
            => Assert.IsFalse(Eval(Slot(StatKind.DamageMul, CombineOp.Additive, 0.5f, ModifierOrigin.Synergy)));

        [Test]
        public void OnPlace_Excluded_False()
            => Assert.IsFalse(Eval(Slot(StatKind.DamageMul, CombineOp.Additive, 0.5f, ModifierOrigin.OnPlace)));

        [Test]
        public void DreamcatcherRevoked_Additive0_False()
            // revoke → 같은 slot 에 identity(additive +0). net 1.0 → 비활성.
            => Assert.IsFalse(Eval(Slot(StatKind.DamageMul, CombineOp.Additive, 0f, ModifierOrigin.Dreamcatcher)));

        [Test]
        public void DreamcatcherRevoked_Mult1_False()
            => Assert.IsFalse(Eval(Slot(StatKind.DamageMul, CombineOp.Multiplicative, 1f, ModifierOrigin.Dreamcatcher)));

        [Test]
        public void DreamcatcherDebuffShaped_StillActive_True()
            // 방향 무관 — 드림캐쳐가 감속(mult<1)을 걸어도 강화 오라 활성(단일 kind).
            => Assert.IsTrue(Eval(Slot(StatKind.MoveSpeedMul, CombineOp.Multiplicative, 0.4f, ModifierOrigin.Dreamcatcher)));

        [Test]
        public void DreamcatcherDmgTakenReduction_True()
            => Assert.IsTrue(Eval(Slot(StatKind.DmgTakenMul, CombineOp.Multiplicative, 0.87f, ModifierOrigin.Dreamcatcher)));

        [Test]
        public void DreamcatcherRegen_True()
            => Assert.IsTrue(Eval(Slot(StatKind.RegenPerSec, CombineOp.Additive, 5f, ModifierOrigin.Dreamcatcher)));

        [Test]
        public void DreamstonePlusDreamcatcher_True()
            // 드림스톤(제외) + 드림캐쳐(활성) 공존 → 드림캐쳐 기준 활성.
            => Assert.IsTrue(Eval(
                Slot(StatKind.DamageMul, CombineOp.Additive, 0.24f, ModifierOrigin.Dreamstone),
                Slot(StatKind.DamageMul, CombineOp.Additive, 0.24f, ModifierOrigin.Dreamcatcher)));

        [Test]
        public void DreamstonePlusRevokedDreamcatcher_False()
            // 드림스톤만 deviate, 드림캐쳐는 revoke(identity) → 강화 오라 비활성.
            => Assert.IsFalse(Eval(
                Slot(StatKind.DamageMul, CombineOp.Additive, 0.24f, ModifierOrigin.Dreamstone),
                Slot(StatKind.DamageMul, CombineOp.Additive, 0f, ModifierOrigin.Dreamcatcher)));

        [Test]
        public void DamageVsCcMul_Excluded_False()
            // 조건부 스탯은 판정 제외 — 드림캐쳐 출처여도 비활성.
            => Assert.IsFalse(Eval(Slot(StatKind.DamageVsCcMul, CombineOp.Multiplicative, 2f, ModifierOrigin.Dreamcatcher)));

        [Test]
        public void MaxHealthMul_Excluded_False()
            // 비체감 스탯은 판정 제외 — 드림캐쳐 출처여도 비활성 (classifier switch 에서 누락 방지 가드).
            => Assert.IsFalse(Eval(Slot(StatKind.MaxHealthMul, CombineOp.Multiplicative, 1.5f, ModifierOrigin.Dreamcatcher)));
    }
}
