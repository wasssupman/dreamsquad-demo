using NUnit.Framework;
using Wassup.Battle.Effects;

namespace Wassup.Tests.EditMode
{
    // combat-action-lock unit 0 — lock-set(공격+이동 정지) 단일 소스 검증.
    public class CcActionLockTests
    {
        [Test]
        public void IsLock_TrueForStunAndSleep()
        {
            Assert.IsTrue(CcActionLock.IsLock(CcKind.Stun), "Stun = lock");
            Assert.IsTrue(CcActionLock.IsLock(CcKind.Sleep), "Sleep = lock");
        }

        [Test]
        public void IsLock_FalseForNonLockKinds()
        {
            Assert.IsFalse(CcActionLock.IsLock(CcKind.Slow), "Slow != lock");
            Assert.IsFalse(CcActionLock.IsLock(CcKind.Impulse), "Impulse != lock");
            Assert.IsFalse(CcActionLock.IsLock(CcKind.DoT), "DoT != lock");
        }
    }
}
