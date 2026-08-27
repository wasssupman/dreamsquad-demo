using NUnit.Framework;
using Wassup.Battle.Effects;
using Wassup.Skills;

namespace Wassup.Tests.EditMode
{
    // skill-layer-migration — 도메인 모디파이어 enum 과 Runtime enum 의 **값 일치**.
    //
    // `SkillCcKind` 와 같은 위험이다. 어댑터가 캐스트로 번역하므로 갈리면 컴파일은
    // 통과하고 **이속 버프가 조용히 공격력 버프가 된다.** 어셈블리가 갈려 컴파일러가
    // 못 잡는 자리라 테스트가 유일한 그물이다.
    public class SkillModifierKindPinTests
    {
        [Test]
        public void StatKind_ValuesMatch()
        {
            Assert.AreEqual((byte)StatKind.DamageMul, (byte)SkillStatKind.DamageMul);
            Assert.AreEqual((byte)StatKind.AttackSpeedMul, (byte)SkillStatKind.AttackSpeedMul);
            Assert.AreEqual((byte)StatKind.DmgTakenMul, (byte)SkillStatKind.DmgTakenMul);
            Assert.AreEqual((byte)StatKind.RegenPerSec, (byte)SkillStatKind.RegenPerSec);
            Assert.AreEqual((byte)StatKind.MoveSpeedMul, (byte)SkillStatKind.MoveSpeedMul);
            Assert.AreEqual((byte)StatKind.DamageVsCcMul, (byte)SkillStatKind.DamageVsCcMul);
            Assert.AreEqual((byte)StatKind.MaxHealthMul, (byte)SkillStatKind.MaxHealthMul);
            Assert.AreEqual(
                System.Enum.GetValues(typeof(StatKind)).Length,
                System.Enum.GetValues(typeof(SkillStatKind)).Length,
                "한쪽에만 stat 이 늘었다");
        }

        [Test]
        public void CombineOp_ValuesMatch()
        {
            Assert.AreEqual((byte)CombineOp.Multiplicative, (byte)SkillCombineOp.Multiplicative);
            Assert.AreEqual((byte)CombineOp.Additive, (byte)SkillCombineOp.Additive);
            Assert.AreEqual((byte)CombineOp.Override, (byte)SkillCombineOp.Override);
        }

        // ⚠ Origin 은 **부분 미러**다(스킬이 실제로 쓰는 것만). 전량 일치를 요구하지 않고
        // 미러한 값들만 고정한다 — 나머지는 도메인 밖에서 나온다.
        [Test]
        public void ModifierOrigin_MirroredValuesMatch()
        {
            Assert.AreEqual((byte)ModifierOrigin.Unspecified, (byte)SkillModifierOrigin.Unspecified);
            Assert.AreEqual((byte)ModifierOrigin.Dreamcatcher, (byte)SkillModifierOrigin.Dreamcatcher);
            Assert.AreEqual((byte)ModifierOrigin.Boss, (byte)SkillModifierOrigin.Boss);
            Assert.AreEqual((byte)ModifierOrigin.HealthThreshold, (byte)SkillModifierOrigin.HealthThreshold);
        }
    }
}
