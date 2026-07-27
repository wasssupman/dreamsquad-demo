using NUnit.Framework;
using Wassup.Battle.Combat.Projectile.Emission;
using Wassup.Data;

namespace Wassup.Tests.EditMode
{
    // projectile-emission-pattern unit 0 — 발사 스케줄 순수 계층. World/EntityManager
    // 없이 도는 것이 계약 1(로직 계층 이식 가능성)의 실증이다.
    public class EmitterTickTests
    {
        private static PatternSpec Spec(int shotCount, float interval, bool reselect = false)
            => new PatternSpec
            {
                barrelDataIndex = 7,
                damage = 40f,
                selection = PatternSelectionRule.RoundRobin,
                shotCount = shotCount,
                shotIntervalSec = interval,
                reselectPerShot = reselect,
                telegraphSec = 1.5f,
            };

        [Test]
        public void SingleShot_FiresOnStartFrame_ThenCompletes()
        {
            var spec = Spec(1, 0.1f);
            var rt = default(EmitterRuntime);
            EmitterTick.Begin(ref rt, spec, baseFireCount: 0);

            Assert.AreEqual(1, EmitterTick.Advance(ref rt, 0.016f, spec.shotIntervalSec));
            Assert.IsTrue(EmitterTick.IsComplete(rt));
            Assert.AreEqual(0, EmitterTick.Advance(ref rt, 0.016f, spec.shotIntervalSec));
        }

        [Test]
        public void Burst_FiresExactCount_AcrossFrames()
        {
            var spec = Spec(3, 0.1f);
            var rt = default(EmitterRuntime);
            EmitterTick.Begin(ref rt, spec, 0);

            int total = 0;
            // 0.05s 씩 12 프레임 = 0.6s — 3발 전부 나가고 그 이상은 안 나간다.
            for (int i = 0; i < 12; i++) total += EmitterTick.Advance(ref rt, 0.05f, spec.shotIntervalSec);

            Assert.AreEqual(3, total);
            Assert.IsTrue(EmitterTick.IsComplete(rt));
        }

        [Test]
        public void ZeroInterval_DumpsEntireBurstInOneFrame()
        {
            var spec = Spec(5, 0f);
            var rt = default(EmitterRuntime);
            EmitterTick.Begin(ref rt, spec, 0);

            Assert.AreEqual(5, EmitterTick.Advance(ref rt, 0.016f, spec.shotIntervalSec));
            Assert.IsTrue(EmitterTick.IsComplete(rt));
        }

        [Test]
        public void LagSpike_FiresSeveralShotsInOneFrame()
        {
            var spec = Spec(4, 0.1f);
            var rt = default(EmitterRuntime);
            EmitterTick.Begin(ref rt, spec, 0);

            // 0.35s 한 프레임: 시작 발 + 0.1·0.2·0.3 경계 = 4발.
            Assert.AreEqual(4, EmitterTick.Advance(ref rt, 0.35f, spec.shotIntervalSec));
        }

        [Test]
        public void IntervalRemainder_CarriesOver_NoDrift()
        {
            var spec = Spec(3, 0.10f);
            var rt = default(EmitterRuntime);
            EmitterTick.Begin(ref rt, spec, 0);

            // 시작 발 1.
            Assert.AreEqual(1, EmitterTick.Advance(ref rt, 0.07f, spec.shotIntervalSec));
            // 누적 0.14 > 0.10 → 2발째. 잔여 0.04 가 이월돼야 한다.
            Assert.AreEqual(1, EmitterTick.Advance(ref rt, 0.07f, spec.shotIntervalSec));
            // 잔여 0.04 + 0.07 = 0.11 > 0.10 → 3발째. 드리프트가 누적되면 여기서 0 이 된다.
            Assert.AreEqual(1, EmitterTick.Advance(ref rt, 0.07f, spec.shotIntervalSec));
            Assert.IsTrue(EmitterTick.IsComplete(rt));
        }

        // spec-review C2 회귀 핀 — 인스턴스는 발화마다 새로 만들어지는 transient 다.
        // baseFireCount 시드가 없으면 fireCount 가 매번 0 에서 시작해 선택 규칙이
        // 고정되고(RoundRobin=항상 rank 0), 보스가 같은 대상만 영원히 때린다.
        [Test]
        public void Begin_SeedsFireCount_SoConsecutiveInstancesAdvanceSelection()
        {
            var spec = Spec(1, 0f);

            var first = default(EmitterRuntime);
            EmitterTick.Begin(ref first, spec, baseFireCount: 0);
            var order1 = PatternLogic.BuildOrder(spec, ref first, 0);

            // 두 번째 발화: durable 소유자가 shotCount 만큼 전진시킨 값을 시드로 받는다.
            var second = default(EmitterRuntime);
            EmitterTick.Begin(ref second, spec, baseFireCount: 1);

            Assert.AreEqual(0, order1.shotIndex);
            Assert.AreEqual(1, second.fireCount, "두 번째 인스턴스가 카운터를 이어받지 못하면 선택이 고정된다");
        }

        [Test]
        public void BuildOrder_CopiesSpecValues_AndAdvancesCounters()
        {
            var spec = Spec(2, 0.1f);
            var rt = default(EmitterRuntime);
            EmitterTick.Begin(ref rt, spec, baseFireCount: 5);

            var a = PatternLogic.BuildOrder(spec, ref rt, 3);
            var b = PatternLogic.BuildOrder(spec, ref rt, 4);

            Assert.AreEqual(0, a.shotIndex);
            Assert.AreEqual(1, b.shotIndex);
            Assert.AreEqual(3, a.targetCandidateIndex);
            Assert.AreEqual(4, b.targetCandidateIndex);
            Assert.AreEqual(40f, a.damage);
            Assert.AreEqual(7, a.barrelDataIndex);
            Assert.AreEqual(1.5f, a.telegraphSec);
            Assert.AreEqual(7, rt.fireCount, "fireCount 는 발마다 전진한다(5 + 2발)");
        }

        [Test]
        public void Begin_ClampsDegenerateShotCount()
        {
            var spec = Spec(0, 0f); // authoring 사고: 0발
            var rt = default(EmitterRuntime);
            EmitterTick.Begin(ref rt, spec, 0);

            Assert.AreEqual(1, EmitterTick.Advance(ref rt, 0.016f, spec.shotIntervalSec),
                "0발 패턴은 1발로 클램프 — 조용히 아무것도 안 하는 것보다 낫다");
        }
    }
}
