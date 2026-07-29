using NUnit.Framework;
using Wassup.Battle.Effects;

namespace Wassup.Tests.EditMode
{
    // boss-jjangssen unit 3 — 보스 CC 면역 술어 고정.
    //
    // 이 술어는 부여 지점 3곳(CcApplySystem 드레인 · EffectSpawner.ApplyCc ·
    // AttackSystem 넉업)이 공유하므로 한 곳에서 갈라지면 조용히 비대칭이 된다.
    // 그리고 CcKind 는 append-only 로 자라는 enum이라, 새 값이 추가될 때 면역이
    // 의도대로 동행하는지(또는 통과하는지)를 여기서 잡는다.
    public class BossCcImmunityTests
    {
        // 직접 걸린 행동정지·넉백만 막는다 — 사용자 확정 범위(스턴/수면/넉백).
        [TestCase(CcKind.Stun)]
        [TestCase(CcKind.Sleep)]
        [TestCase(CcKind.Impulse)]
        public void DirectLockAndKnockbackAreImmune(CcKind kind)
        {
            Assert.IsTrue(CcActionLock.IsBossImmune(kind, CcSource.Direct), $"{kind} 직접 = 면역");
        }

        // DoT/Slow 는 직접이어도 통과 — 승인 범위 밖이다(Bleed 데미지가 0이 되면 안 된다).
        [TestCase(CcKind.DoT)]
        [TestCase(CcKind.Slow)]
        public void DirectDamageAndSlowPassThrough(CcKind kind)
        {
            Assert.IsFalse(CcActionLock.IsBossImmune(kind, CcSource.Direct), $"{kind} 직접 = 통과");
        }

        // 스택 임계가 유발한 것은 kind 를 불문하고 전부 통과 —
        // "누적해서 임계를 넘긴 CC 는 통한다"가 규칙. Ice 5스택 스턴이 보스에게 통하는 근거.
        [TestCase(CcKind.Stun)]
        [TestCase(CcKind.Sleep)]
        [TestCase(CcKind.Impulse)]
        [TestCase(CcKind.DoT)]
        [TestCase(CcKind.Slow)]
        public void StackThresholdSourceAlwaysPassesThrough(CcKind kind)
        {
            Assert.IsFalse(CcActionLock.IsBossImmune(kind, CcSource.StackThreshold),
                $"{kind} 스택 출처 = 통과");
        }

        // 면역 집합은 lock-set + Impulse 와 정확히 일치해야 한다. 새 CcKind 가 추가되면
        // 이 테스트가 "lock 인데 면역이 아님" / "lock 이 아닌데 면역임" 을 즉시 드러낸다.
        [Test]
        public void ImmuneSetIsExactlyLockSetPlusImpulse()
        {
            foreach (CcKind kind in System.Enum.GetValues(typeof(CcKind)))
            {
                bool expected = CcActionLock.IsLock(kind) || kind == CcKind.Impulse;
                Assert.AreEqual(expected, CcActionLock.IsBossImmune(kind, CcSource.Direct),
                    $"{kind}: 면역 집합이 lock-set + Impulse 와 어긋났다");
            }
        }

        // 기본값 계약: source 를 채우지 않은 생산자는 "직접" 으로 취급돼야 한다.
        // 이게 깨지면 기존 CC 생산자 전부가 조용히 보스에게 통해버린다.
        [Test]
        public void DefaultSourceIsDirect()
        {
            Assert.AreEqual(CcSource.Direct, default(CcSource));
            Assert.AreEqual(CcSource.Direct, new EnemyCcEvent().source);
        }
    }
}
