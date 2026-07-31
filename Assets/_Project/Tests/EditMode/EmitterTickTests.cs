using NUnit.Framework;
using Unity.Collections;
using Unity.Mathematics;
using Wassup.Battle.Combat.Projectile.Emission;
using Wassup.Data;

namespace Wassup.Tests.EditMode
{
    // projectile-shot-sequence unit 0 — World/EntityManager 없이 개별 interval
    // 스케줄을 고정한다. 소비 측 발-루프는 emitter 통합 테스트가 덮는다.
    public class EmitterTickTests
    {
        private static PatternSpec Spec(params float[] intervals)
        {
            var shots = default(FixedList128Bytes<PatternShotSpec>);
            for (int i = 0; i < intervals.Length; i++)
            {
                shots.Add(new PatternShotSpec
                {
                    directionT = intervals.Length <= 1 ? 0.5f : i / (float)(intervals.Length - 1),
                    intervalAfterPreviousSec = intervals[i],
                });
            }

            return new PatternSpec
            {
                barrelDataIndex = 7,
                damage = 40f,
                selection = PatternSelectionRule.RoundRobin,
                minAngleDeg = -20f,
                maxAngleDeg = 30f,
                shots = shots,
                reselectPerShot = false,
                telegraphSec = 1.5f,
            };
        }

        [Test]
        public void SingleShot_FiresOnStartFrame_ThenCompletes()
        {
            var spec = Spec(0f);
            var rt = default(EmitterRuntime);
            EmitterTick.Begin(ref rt, spec, baseFireCount: 0);

            Assert.AreEqual(1, EmitterTick.Advance(ref rt, 0.016f, spec));
            Assert.IsTrue(EmitterTick.IsComplete(rt));
            Assert.AreEqual(0, EmitterTick.Advance(ref rt, 0.016f, spec));
        }

        [Test]
        public void VariableIntervals_FireEachStepAtItsOwnOffset()
        {
            var spec = Spec(0f, 0.04f, 0.12f, 0.03f);
            var rt = default(EmitterRuntime);
            EmitterTick.Begin(ref rt, spec, 0);

            Assert.AreEqual(1, EmitterTick.Advance(ref rt, 0.016f, spec), "첫 탄은 trigger 프레임");
            Assert.AreEqual(0, EmitterTick.Advance(ref rt, 0.020f, spec));
            Assert.AreEqual(1, EmitterTick.Advance(ref rt, 0.005f, spec), "두 번째 탄은 0.04초 뒤");
            Assert.AreEqual(0, EmitterTick.Advance(ref rt, 0.100f, spec));
            Assert.AreEqual(1, EmitterTick.Advance(ref rt, 0.020f, spec), "세 번째 탄은 추가 0.12초 뒤");
            Assert.AreEqual(1, EmitterTick.Advance(ref rt, 0.030f, spec), "네 번째 탄은 추가 0.03초 뒤");
            Assert.IsTrue(EmitterTick.IsComplete(rt));
        }

        [Test]
        public void ZeroIntervals_DumpEntireSequenceInOneFrame()
        {
            var spec = Spec(0f, 0f, 0f, 0f, 0f);
            var rt = default(EmitterRuntime);
            EmitterTick.Begin(ref rt, spec, 0);

            Assert.AreEqual(5, EmitterTick.Advance(ref rt, 0.016f, spec));
            Assert.IsTrue(EmitterTick.IsComplete(rt));
        }

        [Test]
        public void LagSpike_FiresEveryCoveredStepInOneFrame()
        {
            var spec = Spec(0f, 0.1f, 0.1f, 0.1f);
            var rt = default(EmitterRuntime);
            EmitterTick.Begin(ref rt, spec, 0);

            Assert.AreEqual(4, EmitterTick.Advance(ref rt, 0.35f, spec));
        }

        [Test]
        public void IntervalRemainder_CarriesOver_NoDrift()
        {
            var spec = Spec(0f, 0.10f, 0.10f);
            var rt = default(EmitterRuntime);
            EmitterTick.Begin(ref rt, spec, 0);

            Assert.AreEqual(1, EmitterTick.Advance(ref rt, 0.07f, spec));
            Assert.AreEqual(1, EmitterTick.Advance(ref rt, 0.07f, spec));
            Assert.AreEqual(1, EmitterTick.Advance(ref rt, 0.07f, spec),
                "잔여 시간이 이월되지 않으면 세 번째 탄이 한 프레임 늦어진다");
            Assert.IsTrue(EmitterTick.IsComplete(rt));
        }

        [Test]
        public void Begin_SeedsFireCount_SoConsecutiveInstancesAdvanceSelection()
        {
            var spec = Spec(0f);

            var first = default(EmitterRuntime);
            EmitterTick.Begin(ref first, spec, baseFireCount: 0);
            var order1 = PatternLogic.BuildOrder(spec, ref first, 0);

            var second = default(EmitterRuntime);
            EmitterTick.Begin(ref second, spec, baseFireCount: spec.shots.Length);

            Assert.AreEqual(0, order1.shotIndex);
            Assert.AreEqual(1, second.fireCount, "두 번째 인스턴스가 영속 카운터를 이어받아야 한다");
        }

        [Test]
        public void BuildOrder_CopiesStepAndSpecValues_AndAdvancesCounters()
        {
            var spec = Spec(0f, 0.1f);
            var rt = default(EmitterRuntime);
            EmitterTick.Begin(ref rt, spec, baseFireCount: 5);

            var first = PatternLogic.BuildOrder(spec, ref rt, 3);
            var second = PatternLogic.BuildOrder(spec, ref rt, 4);

            Assert.AreEqual(0, first.shotIndex);
            Assert.AreEqual(1, second.shotIndex);
            Assert.AreEqual(0f, first.directionT);
            Assert.AreEqual(1f, second.directionT);
            Assert.AreEqual(3, first.targetCandidateIndex);
            Assert.AreEqual(4, second.targetCandidateIndex);
            Assert.AreEqual(40f, first.damage);
            Assert.AreEqual(7, first.barrelDataIndex);
            Assert.AreEqual(1.5f, first.telegraphSec);
            Assert.AreEqual(7, rt.fireCount, "fireCount는 발마다 전진한다(5 + 2발)");
        }

        [Test]
        public void Advance_TotalReturnedShots_EqualsSequenceLength()
        {
            foreach (var intervals in new[]
                     {
                         new[] { 0f, 0f, 0f, 0f, 0f },
                         new[] { 0f, 0.1f, 0.03f },
                         new[] { 0f },
                         new[] { 0f, 0.05f, 0f, 0.02f, 0.09f, 0f, 0.01f },
                     })
            {
                var spec = Spec(intervals);
                var rt = default(EmitterRuntime);
                EmitterTick.Begin(ref rt, spec, 0);

                int total = 0;
                for (int frame = 0; frame < 200 && !EmitterTick.IsComplete(rt); frame++)
                    total += EmitterTick.Advance(ref rt, 0.02f, spec);

                Assert.AreEqual(spec.shots.Length, total, "반환 총합이 어긋나면 탄이 유실된다");
                Assert.IsTrue(EmitterTick.IsComplete(rt));
            }
        }

        [Test]
        public void EmptySequence_IsComplete_AndFiresNothing()
        {
            var spec = Spec();
            var rt = default(EmitterRuntime);
            EmitterTick.Begin(ref rt, spec, 0);

            Assert.IsTrue(EmitterTick.IsComplete(rt));
            Assert.AreEqual(0, EmitterTick.Advance(ref rt, 0.016f, spec));
        }

        [Test]
        public void TotalDuration_SumsIntervalsAfterTheFirstStep()
        {
            var spec = Spec(99f, 0.03f, 0.12f, -1f);

            Assert.AreEqual(0.15f, EmitterTick.TotalDuration(spec), 1e-5f,
                "첫 step interval은 trigger 즉발이라 무시하고 음수 값은 0으로 본다");
        }

        [Test]
        public void Randomizer_SameSeedReproduces_AndDifferentSeedChangesSequence()
        {
            var first = Spec(0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f);
            first.randomizeShotsPerTrigger = true;
            first.randomIntervalMinSec = 0.006f;
            first.randomIntervalMaxSec = 0.018f;
            var same = first;
            var different = first;

            PatternShotRandomizer.Apply(ref first, 123u);
            PatternShotRandomizer.Apply(ref same, 123u);
            PatternShotRandomizer.Apply(ref different, 456u);

            bool differs = false;
            for (int i = 0; i < first.shots.Length; i++)
            {
                Assert.AreEqual(first.shots[i].directionT, same.shots[i].directionT);
                Assert.AreEqual(first.shots[i].intervalAfterPreviousSec, same.shots[i].intervalAfterPreviousSec);
                Assert.That(first.shots[i].directionT, Is.InRange(0f, 1f));
                if (i == 0)
                    Assert.AreEqual(0f, first.shots[i].intervalAfterPreviousSec);
                else
                    Assert.That(first.shots[i].intervalAfterPreviousSec, Is.InRange(0.006f, 0.018f));

                if (math.abs(first.shots[i].directionT - different.shots[i].directionT) > 1e-5f
                    || math.abs(first.shots[i].intervalAfterPreviousSec
                                - different.shots[i].intervalAfterPreviousSec) > 1e-5f)
                    differs = true;
            }
            Assert.IsTrue(differs, "다른 trigger seed가 같은 10발 시퀀스를 반복하면 안 된다");
        }

        [Test]
        public void Randomizer_Disabled_PreservesAuthoredSteps()
        {
            var spec = Spec(0f, 0.03f, 0.12f);
            var before = spec;

            PatternShotRandomizer.Apply(ref spec, 999u);

            for (int i = 0; i < spec.shots.Length; i++)
            {
                Assert.AreEqual(before.shots[i].directionT, spec.shots[i].directionT);
                Assert.AreEqual(before.shots[i].intervalAfterPreviousSec, spec.shots[i].intervalAfterPreviousSec);
            }
        }
    }
}
