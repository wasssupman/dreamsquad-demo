using NUnit.Framework;
using Wassup.Battle.Effects;

namespace Wassup.Tests.EditMode
{
    // season-gimmick-onsen unit 1 [중립] — 열기 반전 산식 회귀 고정.
    // 반전 경계(≤flip 회복 / >flip 손실) · 오버힐 클램프 · HP 1 바닥 · maxHp 스케일.
    public class HeatMathTests
    {
        private const float MaxHp = 100f;
        private const float Heal = 0.1f;   // 10%
        private const float Loss = 0.1f;   // 10%

        [Test]
        public void BelowThreshold_Heals()
        {
            // stacks 1 ≤ flip 5, 여유 있는 체력 → +10% maxHp.
            Assert.AreEqual(10f, HeatMath.Delta(1, 5, MaxHp, currentHp: 50f, Heal, Loss), 1e-5f);
        }

        [Test]
        public void AtThreshold_StillHeals()
        {
            // 경계 포함: stacks == flip 은 회복 구간(≤).
            Assert.AreEqual(10f, HeatMath.Delta(5, 5, MaxHp, currentHp: 50f, Heal, Loss), 1e-5f);
        }

        [Test]
        public void JustOverThreshold_Flips_ToLoss()
        {
            // 경계 반전: stacks == flip+1 은 손실 구간(>).
            Assert.AreEqual(-10f, HeatMath.Delta(6, 5, MaxHp, currentHp: 50f, Heal, Loss), 1e-5f);
        }

        [Test]
        public void Heal_ClampedToHeadroom_NoOverheal()
        {
            // 거의 만피(95): 10 요청이지만 여유 5 → +5.
            Assert.AreEqual(5f, HeatMath.Delta(1, 5, MaxHp, currentHp: 95f, Heal, Loss), 1e-5f);
        }

        [Test]
        public void Heal_AtFullHp_IsNoOp()
        {
            // 만피 → 여유 0 → 0(enqueue 스킵 신호).
            Assert.AreEqual(0f, HeatMath.Delta(1, 5, MaxHp, currentHp: MaxHp, Heal, Loss), 1e-5f);
        }

        [Test]
        public void Loss_FlooredAtHp1_NeverKills()
        {
            // 저체력(5) 과열: 10 손실이면 죽지만, HP 1 바닥 → -4 (5→1).
            Assert.AreEqual(-4f, HeatMath.Delta(6, 5, MaxHp, currentHp: 5f, Heal, Loss), 1e-5f);
        }

        [Test]
        public void Loss_AtHp1_IsNoOp()
        {
            // 이미 HP 1 → 더 못 깎음 → 0.
            Assert.AreEqual(0f, HeatMath.Delta(9, 5, MaxHp, currentHp: 1f, Heal, Loss), 1e-5f);
        }

        [Test]
        public void Loss_NormalWhenHealthy()
        {
            // 충분한 체력(100) 과열 → 정상 -10.
            Assert.AreEqual(-10f, HeatMath.Delta(6, 5, MaxHp, currentHp: 100f, Heal, Loss), 1e-5f);
        }

        [Test]
        public void Scales_WithMaxHp()
        {
            // maxHp 200, 여유 충분 → 회복 +20 / 과열 -20.
            Assert.AreEqual(20f, HeatMath.Delta(1, 5, 200f, currentHp: 100f, Heal, Loss), 1e-5f);
            Assert.AreEqual(-20f, HeatMath.Delta(6, 5, 200f, currentHp: 200f, Heal, Loss), 1e-5f);
        }
    }
}
