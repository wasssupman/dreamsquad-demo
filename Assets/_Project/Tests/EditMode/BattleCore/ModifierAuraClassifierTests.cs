using System.Collections.Generic;
using NUnit.Framework;
using Somnia.Battle.BattleCore;
using Somnia.Battle.BattleCore.Effects;

namespace Somnia.Battle.Tests.EditMode.Core
{
    // battle-core-rebuild unit 6c — 오라 판정(순수 함수). 옛 `ModifierAuraClassifier` 의 규칙 둘 —
    // **출처 필터** + **net 편차** — 이 salvage 뒤에도 서 있는가.
    [TestFixture]
    public class ModifierAuraClassifierTests
    {
        private static ModifierSlot Slot(StatKind stat, CombineOp op, float magnitude, ModifierOrigin origin,
                                         int source = 1, float remaining = 5f)
            => new ModifierSlot
            {
                Key = new ModifierKey(new SimEntityId(source), stat, op, SlotTag.Default),
                Magnitude = magnitude,
                Remaining = remaining,
                Origin = origin,
            };

        [Test]
        public void 슬롯이_없으면_꺼진다()
        {
            Assert.IsFalse(ModifierAuraClassifier.HasActiveDreamcatcherModifier(new List<ModifierSlot>()));
            Assert.IsFalse(ModifierAuraClassifier.HasActiveDreamcatcherModifier(null));
        }

        [Test]
        public void 드림캐쳐_출처가_기준을_벗어나면_켜진다()
        {
            var slots = new List<ModifierSlot>
            {
                Slot(StatKind.DamageMul, CombineOp.Multiplicative, 1.3f, ModifierOrigin.Dreamcatcher),
            };
            Assert.IsTrue(ModifierAuraClassifier.HasActiveDreamcatcherModifier(slots));
        }

        [Test]
        public void 다른_출처는_세지_않는다()
        {
            var slots = new List<ModifierSlot>
            {
                Slot(StatKind.DamageMul, CombineOp.Multiplicative, 1.5f, ModifierOrigin.Zone),
                Slot(StatKind.AttackSpeedMul, CombineOp.Multiplicative, 1.5f, ModifierOrigin.Gimmick),
            };
            Assert.IsFalse(ModifierAuraClassifier.HasActiveDreamcatcherModifier(slots));
        }

        [Test]
        public void 같은_출처의_두_슬롯이_상쇄되면_꺼진다_net_편차()
        {
            // ×2 와 ×0.5 — 슬롯은 둘 있지만 접은 값이 1 이다. 「슬롯이 있다」만으로 켜면 거짓 오라다.
            var slots = new List<ModifierSlot>
            {
                Slot(StatKind.DamageMul, CombineOp.Multiplicative, 2f, ModifierOrigin.Dreamcatcher, source: 1),
                Slot(StatKind.DamageMul, CombineOp.Multiplicative, 0.5f, ModifierOrigin.Dreamcatcher, source: 2),
            };
            Assert.IsFalse(ModifierAuraClassifier.HasActiveDreamcatcherModifier(slots));
        }

        [Test]
        public void 판정_제외_스탯은_오라를_켜지_않는다()
        {
            var slots = new List<ModifierSlot>
            {
                Slot(StatKind.DamageVsCcMul, CombineOp.Multiplicative, 2f, ModifierOrigin.Dreamcatcher),
                Slot(StatKind.MaxHealthMul, CombineOp.Multiplicative, 2f, ModifierOrigin.Dreamcatcher),
            };
            Assert.IsFalse(ModifierAuraClassifier.HasActiveDreamcatcherModifier(slots));
        }

        [Test]
        public void 재생은_기준이_0_이다()
        {
            var slots = new List<ModifierSlot>
            {
                Slot(StatKind.RegenPerSec, CombineOp.Additive, 3f, ModifierOrigin.Dreamcatcher),
            };
            Assert.IsTrue(ModifierAuraClassifier.HasActiveDreamcatcherModifier(slots));
        }

        [Test]
        public void 번아웃_표식은_출처_슬롯의_존재만_본다()
        {
            var slots = new List<ModifierSlot>
            {
                Slot(StatKind.AttackSpeedMul, CombineOp.Multiplicative, 0.8f, ModifierOrigin.Burnout),
            };
            Assert.IsTrue(ModifierAuraClassifier.HasAnyFromOrigin(slots, ModifierOrigin.Burnout));
            Assert.IsFalse(ModifierAuraClassifier.HasAnyFromOrigin(slots, ModifierOrigin.Dreamcatcher));
        }
    }
}
