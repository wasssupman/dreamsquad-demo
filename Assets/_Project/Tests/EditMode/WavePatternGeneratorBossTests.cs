using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using Wassup.Data;

namespace Wassup.Tests.EditMode
{
    // boss-wave-cadence unit 0 — 매 N번째 웨이브 보스 편성(치환) 규칙의 회귀 고정.
    // 생성기는 nightmareMechanics 를 보지 않는다(스폰단 판별) — 여기선 bossUnit 참조가
    // 매 N번째 웨이브 선봉으로 주입되는지, 비-보스 웨이브 불변식·pool 방어만 검증한다.
    public class WavePatternGeneratorBossTests
    {
        private readonly List<AttackUnitData> _units = new();

        [SetUp]
        public void SetUp()
        {
            _units.Clear();
            _units.Add(CreateUnit("Basic"));
            _units.Add(CreateUnit("Swift"));
            _units.Add(CreateUnit("Tanker"));
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var unit in _units)
                Object.DestroyImmediate(unit);
            _units.Clear();
        }

        // 편성 + 선봉: interval=5, waveCount=12 → idx 4·9(웨이브 5·10)만 보스 웨이브.
        [Test]
        public void EveryNthWaveIsBossPlusEscortAndBossLeads()
        {
            var boss = CreateUnit("Boss");
            try
            {
                var plan = GenerateBoss(1234, boss, interval: 5, escMin: 3, escMax: 4);
                Assert.AreEqual(12, plan.waves.Count);

                for (int i = 0; i < plan.waves.Count; i++)
                {
                    var w = plan.waves[i];
                    if ((i + 1) % 5 == 0)
                    {
                        Assert.AreEqual(2, w.groups.Count, $"wave {i} groups");
                        Assert.AreSame(boss, w.groups[0].unit, $"wave {i} 선봉=보스");
                        Assert.AreEqual(1, w.groups[0].count, $"wave {i} 보스 1기");
                        Assert.AreNotSame(boss, w.groups[1].unit, $"wave {i} escort≠보스");
                        Assert.GreaterOrEqual(w.groups[1].count, 3, $"wave {i} escort min");
                        Assert.LessOrEqual(w.groups[1].count, 4, $"wave {i} escort max");
                    }
                    else
                    {
                        foreach (var g in w.groups)
                            Assert.AreNotSame(boss, g.unit, $"wave {i} 비-보스 웨이브에 보스 없음");
                    }
                }
            }
            finally { Object.DestroyImmediate(boss); }
        }

        // graceful: boss=null → 어떤 웨이브도 보스 없음(현행 base 동작).
        [Test]
        public void NullBossProducesNoBossWaves()
        {
            var plan = GenerateBoss(1234, null, interval: 5, escMin: 3, escMax: 4);
            foreach (var w in plan.waves)
            {
                Assert.AreEqual(2, w.groups.Count);
                Assert.GreaterOrEqual(w.totalCount, 10);
                Assert.LessOrEqual(w.totalCount, 15);
            }
        }

        // 결정론: 같은 seed 두 번 → 그룹 unit/count 완전 동일.
        [Test]
        public void SameSeedProducesIdenticalBossPlan()
        {
            var boss = CreateUnit("Boss");
            try
            {
                var a = GenerateBoss(4242, boss, 5, 3, 4);
                var b = GenerateBoss(4242, boss, 5, 3, 4);
                Assert.AreEqual(a.waves.Count, b.waves.Count);
                for (int i = 0; i < a.waves.Count; i++)
                {
                    Assert.AreEqual(a.waves[i].groups.Count, b.waves[i].groups.Count, $"wave {i} group count");
                    for (int g = 0; g < a.waves[i].groups.Count; g++)
                    {
                        Assert.AreSame(a.waves[i].groups[g].unit, b.waves[i].groups[g].unit, $"wave {i} g{g} unit");
                        Assert.AreEqual(a.waves[i].groups[g].count, b.waves[i].groups[g].count, $"wave {i} g{g} count");
                    }
                }
            }
            finally { Object.DestroyImmediate(boss); }
        }

        // 핵심 불변식: 같은 seed 에서 boss-ON 의 비-보스 웨이브 == boss-OFF 의 같은 인덱스 웨이브.
        // 후처리 rng 를 실수로 앞당기면(비-보스 웨이브 rng 오염) 이 테스트가 잡는다.
        [Test]
        public void NonBossWavesMatchBossOffPlanAtSameSeed()
        {
            var boss = CreateUnit("Boss");
            try
            {
                var on = GenerateBoss(777, boss, 5, 3, 4);
                var off = GenerateBoss(777, null, 5, 3, 4);
                Assert.AreEqual(off.waves.Count, on.waves.Count);
                for (int i = 0; i < on.waves.Count; i++)
                {
                    if ((i + 1) % 5 == 0) continue; // 보스 웨이브는 치환됐으므로 제외
                    Assert.AreEqual(off.waves[i].groups.Count, on.waves[i].groups.Count, $"wave {i} group count");
                    for (int g = 0; g < off.waves[i].groups.Count; g++)
                    {
                        Assert.AreSame(off.waves[i].groups[g].unit, on.waves[i].groups[g].unit, $"wave {i} g{g} unit");
                        Assert.AreEqual(off.waves[i].groups[g].count, on.waves[i].groups[g].count, $"wave {i} g{g} count");
                    }
                }
            }
            finally { Object.DestroyImmediate(boss); }
        }

        // pool 방어: bossUnit 이 실수로 attackUnitPool 에 있어도 비-보스 웨이브·escort 에 보스가 새지 않는다.
        [Test]
        public void BossInPoolDoesNotLeakIntoRegularSpawns()
        {
            var boss = CreateUnit("Boss");
            var pool = new List<AttackUnitData>(_units) { boss }; // 실수로 pool 에 섞임
            try
            {
                var plan = GenerateBoss(999, boss, 5, 3, 4, pool);
                for (int i = 0; i < plan.waves.Count; i++)
                {
                    var w = plan.waves[i];
                    if ((i + 1) % 5 == 0)
                    {
                        Assert.AreSame(boss, w.groups[0].unit, $"wave {i} 선봉=보스");
                        Assert.AreEqual(1, w.groups[0].count, $"wave {i} 보스 1기(중복 아님)");
                        Assert.AreNotSame(boss, w.groups[1].unit, $"wave {i} escort≠보스(보스 2기 방지)");
                    }
                    else
                    {
                        foreach (var g in w.groups)
                            Assert.AreNotSame(boss, g.unit, $"wave {i} 비-보스 웨이브 보스 누출");
                    }
                }
            }
            finally { Object.DestroyImmediate(boss); }
        }

        private GeneratedWavePlan GenerateBoss(int seed, AttackUnitData boss, int interval, int escMin, int escMax,
            IReadOnlyList<AttackUnitData> pool = null, int waveCount = 12)
        {
            return WavePatternGenerator.Generate(
                seed,
                1,
                180f,
                waveCount,
                waveCount,   // min==max → waveCount 고정(결정론적 인덱스)
                10,
                15,
                0.35f,
                pool ?? _units,
                boss,
                interval,
                escMin,
                escMax);
        }

        private static AttackUnitData CreateUnit(string name)
        {
            var unit = ScriptableObject.CreateInstance<AttackUnitData>();
            unit.displayName = name;
            return unit;
        }
    }
}
