using NUnit.Framework;
using Wassup.Battle.Effects;
using Wassup.Skills;

namespace Wassup.Tests.EditMode
{
    // skill-layer-foundation unit 5 — 도메인 CC enum 과 Runtime CC enum 의 **값 일치**.
    //
    // 어댑터가 `(CcKind)intent.Selector` 로 캐스트한다. 두 enum 이 갈리면 컴파일은
    // 통과하고 **재우려던 것이 조용히 다른 상태이상이 된다.** 이 파일의 첫 판이
    // 실제로 그렇게 틀렸다(Sleep 을 3 으로 추측했는데 실제는 4 — 3 은 Stun 이다).
    //
    // 어셈블리가 갈려 있어 컴파일러가 못 잡는 자리라, 테스트가 유일한 그물이다.
    public class SkillCcKindPinTests
    {
        [Test]
        public void EveryDomainCcKind_MatchesRuntimeValue()
        {
            Assert.AreEqual((byte)CcKind.Slow, (byte)SkillCcKind.Slow);
            Assert.AreEqual((byte)CcKind.Impulse, (byte)SkillCcKind.Impulse);
            Assert.AreEqual((byte)CcKind.DoT, (byte)SkillCcKind.DoT);
            Assert.AreEqual((byte)CcKind.Stun, (byte)SkillCcKind.Stun);
            Assert.AreEqual((byte)CcKind.Sleep, (byte)SkillCcKind.Sleep);
        }

        // 한쪽에만 값이 늘면 캐스트가 조용히 «없는 kind» 를 만든다.
        [Test]
        public void BothEnums_HaveTheSameMemberCount()
        {
            Assert.AreEqual(
                System.Enum.GetValues(typeof(CcKind)).Length,
                System.Enum.GetValues(typeof(SkillCcKind)).Length,
                "한쪽 enum 에만 kind 가 늘었다 — 어댑터 캐스트가 없는 값을 만든다.");
        }
    }
}
