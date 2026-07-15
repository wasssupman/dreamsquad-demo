using NUnit.Framework;
using Wassup.Battle.Effects;

namespace Wassup.Tests.EditMode
{
    // unit-buff-debuff-aura Unit 0 — 순 버프/디버프 판정 회귀 테스트.
    // 핵심 가드: dmgTakenMul 역방향(리뷰 H1), regenPerSec 버프 전용(M2),
    // damageVsCcMul 판정 제외(M1), 버프+디버프 동시, epsilon 노이즈.
    public class ModifierAuraClassifierTests
    {
        // base identity (BattleBridge 스폰 시 부여하는 기본값과 동일).
        private static ModifierStats Base() => new ModifierStats
        {
            damageMul = 1f,
            attackSpeedMul = 1f,
            dmgTakenMul = 1f,
            regenPerSec = 0f,
            moveSpeedMul = 1f,
            damageVsCcMul = 1f,
        };

        [Test]
        public void Base_NeitherBuffedNorDebuffed()
        {
            ModifierAuraClassifier.Classify(Base(), out bool buffed, out bool debuffed);
            Assert.IsFalse(buffed);
            Assert.IsFalse(debuffed);
        }

        [Test]
        public void DamageBuff_IsBuffed()
        {
            var s = Base(); s.damageMul = 1.3f;
            ModifierAuraClassifier.Classify(s, out bool buffed, out bool debuffed);
            Assert.IsTrue(buffed);
            Assert.IsFalse(debuffed);
        }

        [Test]
        public void DmgTakenReduction_IsBuffed_Reversed()
        {
            // eHP/방어 버프: dmgTakenMul < 1 (예 0.87). 역방향 → 버프.
            var s = Base(); s.dmgTakenMul = 0.87f;
            ModifierAuraClassifier.Classify(s, out bool buffed, out bool debuffed);
            Assert.IsTrue(buffed);
            Assert.IsFalse(debuffed);
        }

        [Test]
        public void DmgTakenIncrease_IsDebuffed_Reversed()
        {
            // 타일 취약 디버프: dmgTakenMul > 1 (예 1.4). 역방향 → 디버프.
            var s = Base(); s.dmgTakenMul = 1.4f;
            ModifierAuraClassifier.Classify(s, out bool buffed, out bool debuffed);
            Assert.IsFalse(buffed);
            Assert.IsTrue(debuffed);
        }

        [Test]
        public void Slow_IsDebuffed()
        {
            var s = Base(); s.moveSpeedMul = 0.4f;
            ModifierAuraClassifier.Classify(s, out bool buffed, out bool debuffed);
            Assert.IsFalse(buffed);
            Assert.IsTrue(debuffed);
        }

        [Test]
        public void BuffAndDebuff_Simultaneous_BothTrue()
        {
            var s = Base(); s.damageMul = 1.3f; s.moveSpeedMul = 0.4f;
            ModifierAuraClassifier.Classify(s, out bool buffed, out bool debuffed);
            Assert.IsTrue(buffed);
            Assert.IsTrue(debuffed);
        }

        [Test]
        public void Regen_IsBuffOnly()
        {
            var s = Base(); s.regenPerSec = 5f;
            ModifierAuraClassifier.Classify(s, out bool buffed, out bool debuffed);
            Assert.IsTrue(buffed);
            Assert.IsFalse(debuffed);
        }

        [Test]
        public void DamageVsCcMul_Excluded_NeitherFlag()
        {
            // 조건부 스탯 — 판정에서 제외(M1). 상시 오라 오도 방지.
            var s = Base(); s.damageVsCcMul = 2f;
            ModifierAuraClassifier.Classify(s, out bool buffed, out bool debuffed);
            Assert.IsFalse(buffed);
            Assert.IsFalse(debuffed);
        }

        [Test]
        public void EpsilonNoise_NotBuffed()
        {
            // 부동소수 노이즈(1+2e-5 < 1+ε) → 상태 아님.
            var s = Base(); s.damageMul = 1.00002f;
            ModifierAuraClassifier.Classify(s, out bool buffed, out bool debuffed);
            Assert.IsFalse(buffed);
            Assert.IsFalse(debuffed);
        }
    }
}
