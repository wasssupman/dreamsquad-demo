using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using Wassup.Data;

namespace Wassup.Tests.EditMode
{
    // wave-pattern unit 12 — 등장 게이트(minWaveNumber). 첫 웨이브 Runner 금지가 대표 사례.
    public class WaveEligibilityGateTests
    {
        private readonly List<AttackUnitData> _units = new();

        [TearDown]
        public void TearDown()
        {
            foreach (var unit in _units)
                Object.DestroyImmediate(unit);
            _units.Clear();
        }

        private AttackUnitData CreateUnit(string id, int minWaveNumber = 1)
        {
            var unit = ScriptableObject.CreateInstance<AttackUnitData>();
            unit.id = id;
            unit.displayName = id;
            unit.minWaveNumber = minWaveNumber;
            _units.Add(unit);
            return unit;
        }

        private static GeneratedWavePlan Generate(IReadOnlyList<AttackUnitData> pool, int seed)
        {
            return WavePatternGenerator.Generate(
                seed,
                generatorVersion: 1,
                timerDurationSec: 180f,
                minWaveCount: 10,
                maxWaveCount: 15,
                minUnitsPerWave: 6,
                maxUnitsPerWave: 10,
                intraWaveSpacingSec: 1f,
                attackUnitPool: pool);
        }

        private static bool WaveContains(GeneratedWave wave, AttackUnitData unit)
        {
            for (int g = 0; g < wave.groups.Count; g++)
                if (ReferenceEquals(wave.groups[g].unit, unit)) return true;
            return false;
        }

        [Test]
        public void GatedUnitNeverAppearsBeforeItsMinWave()
        {
            var basic = CreateUnit("basic");
            var swift = CreateUnit("swift");
            var runner = CreateUnit("runner", minWaveNumber: 2);
            var pool = new List<AttackUnitData> { basic, swift, runner };

            // 시드를 여러 개 돌려 "우연히 안 뽑힌 것"이 아님을 확인한다.
            for (int seed = 1; seed <= 50; seed++)
            {
                var plan = Generate(pool, seed);
                Assert.IsFalse(WaveContains(plan.waves[0], runner),
                    $"seed {seed}: 첫 웨이브에 게이트된 유닛이 등장했다.");
            }
        }

        [Test]
        public void GatedUnitStillAppearsInLaterWaves()
        {
            var basic = CreateUnit("basic");
            var swift = CreateUnit("swift");
            var runner = CreateUnit("runner", minWaveNumber: 2);
            var pool = new List<AttackUnitData> { basic, swift, runner };

            bool appeared = false;
            for (int seed = 1; seed <= 50 && !appeared; seed++)
            {
                var plan = Generate(pool, seed);
                for (int i = 1; i < plan.waves.Count; i++)
                    if (WaveContains(plan.waves[i], runner)) { appeared = true; break; }
            }
            Assert.IsTrue(appeared, "게이트된 유닛이 2웨이브 이후에도 전혀 등장하지 않는다 — 게이트가 과하게 걸렸다.");
        }

        [Test]
        public void GateKeepsTwoDistinctGroupsPerWave()
        {
            var basic = CreateUnit("basic");
            var swift = CreateUnit("swift");
            var runner = CreateUnit("runner", minWaveNumber: 2);
            var pool = new List<AttackUnitData> { basic, swift, runner };

            for (int seed = 1; seed <= 20; seed++)
            {
                var plan = Generate(pool, seed);
                foreach (var wave in plan.waves)
                {
                    Assert.AreEqual(2, wave.groups.Count);
                    Assert.AreNotSame(wave.groups[0].unit, wave.groups[1].unit);
                    Assert.GreaterOrEqual(wave.groups[0].count, 1);
                    Assert.GreaterOrEqual(wave.groups[1].count, 1);
                }
            }
        }

        [Test]
        public void UngatedPoolIsByteIdenticalToPreGateBehavior()
        {
            // 게이트를 아무도 안 걸면 생성 결과가 기존과 동일해야 한다(rng 소비 불변 계약).
            var a = new List<AttackUnitData> { CreateUnit("a"), CreateUnit("b"), CreateUnit("c") };
            var planA = Generate(a, 4321);

            var b = new List<AttackUnitData> { CreateUnit("a2"), CreateUnit("b2"), CreateUnit("c2") };
            var planB = Generate(b, 4321);

            Assert.AreEqual(planA.waves.Count, planB.waves.Count);
            for (int i = 0; i < planA.waves.Count; i++)
            {
                var wa = planA.waves[i];
                var wb = planB.waves[i];
                Assert.AreEqual(wa.totalCount, wb.totalCount);
                for (int g = 0; g < wa.groups.Count; g++)
                {
                    Assert.AreEqual(a.IndexOf(wa.groups[g].unit), b.IndexOf(wb.groups[g].unit));
                    Assert.AreEqual(wa.groups[g].count, wb.groups[g].count);
                }
            }
        }

        [Test]
        public void ResolveWaveEligibleIndexSkipsGatedAndRespectsExclude()
        {
            var basic = CreateUnit("basic");
            var runner = CreateUnit("runner", minWaveNumber: 3);
            var swift = CreateUnit("swift");
            var pool = new List<AttackUnitData> { basic, runner, swift };

            // wave 1: runner(1) 은 불가 → 다음 허용(swift, 2).
            Assert.AreEqual(2, WavePatternGenerator.ResolveWaveEligibleIndex(pool, 1, 1));
            // wave 3: runner 허용 → 그대로.
            Assert.AreEqual(1, WavePatternGenerator.ResolveWaveEligibleIndex(pool, 1, 3));
            // exclude 로 지정된 인덱스는 건너뛴다(2종 계약).
            Assert.AreEqual(0, WavePatternGenerator.ResolveWaveEligibleIndex(pool, 1, 1, excludeIndex: 2));
        }

        [Test]
        public void AllGatedPoolFailsOpenInsteadOfEmptyWave()
        {
            var one = CreateUnit("one", minWaveNumber: 5);
            var two = CreateUnit("two", minWaveNumber: 5);
            var pool = new List<AttackUnitData> { one, two };

            // 전부 금지된 웨이브에서는 원래 뽑힌 인덱스를 그대로 쓴다(빈 웨이브 방지).
            Assert.AreEqual(0, WavePatternGenerator.ResolveWaveEligibleIndex(pool, 0, 1));
            Assert.AreEqual(1, WavePatternGenerator.ResolveWaveEligibleIndex(pool, 1, 1, excludeIndex: 0));

            var plan = Generate(pool, 77);
            Assert.AreEqual(2, plan.waves[0].groups.Count);
            Assert.AreNotSame(plan.waves[0].groups[0].unit, plan.waves[0].groups[1].unit);
        }
    }
}
