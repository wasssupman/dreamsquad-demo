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

        // ── boss-jjangssen unit 0 — bossPool 로테이션 ────────────────────────────────

        // 무회귀의 실체: 보스 1종이면 선택 rng 를 소비하지 않아야 한다. 소비하면 escortCount·
        // escortType 이 한 칸씩 밀려 **보스 웨이브 구성이 전부 바뀐다** → 라이브 7덱 회귀.
        [Test]
        public void SingleEntryBossPoolIsIdenticalToLegacyBossUnit()
        {
            var boss = CreateUnit("Boss");
            try
            {
                var legacy = GenerateBoss(4242, boss, 5, 3, 4);
                var pooled = GenerateBossPool(4242, new[] { boss });

                Assert.AreEqual(legacy.waves.Count, pooled.waves.Count);
                for (int i = 0; i < legacy.waves.Count; i++)
                {
                    Assert.AreEqual(legacy.waves[i].groups.Count, pooled.waves[i].groups.Count, $"wave {i} group count");
                    for (int g = 0; g < legacy.waves[i].groups.Count; g++)
                    {
                        Assert.AreSame(legacy.waves[i].groups[g].unit, pooled.waves[i].groups[g].unit, $"wave {i} g{g} unit");
                        Assert.AreEqual(legacy.waves[i].groups[g].count, pooled.waves[i].groups[g].count, $"wave {i} g{g} count");
                    }
                }
            }
            finally { Object.DestroyImmediate(boss); }
        }

        // 라이브 덱은 bossPool 이 비어 있다 — bossUnit 폴백이 끊기면 보스가 조용히 사라진다.
        [Test]
        public void EmptyBossPoolFallsBackToBossUnit()
        {
            var boss = CreateUnit("Boss");
            try
            {
                foreach (var empty in new IReadOnlyList<AttackUnitData>[] { null, new AttackUnitData[0] })
                {
                    var plan = GenerateBossPool(31, empty, fallbackBoss: boss);
                    for (int i = 0; i < plan.waves.Count; i++)
                    {
                        if ((i + 1) % 5 != 0) continue;
                        Assert.AreSame(boss, plan.waves[i].groups[0].unit, $"wave {i} 폴백 보스 선봉");
                    }
                }
            }
            finally { Object.DestroyImmediate(boss); }
        }

        // 보스가 하나도 없으면(폴백도 null) 보스 웨이브 자체가 없다 — 기존 graceful no-op 유지.
        [Test]
        public void NoBossAtAllProducesNoBossWaves()
        {
            var plan = GenerateBossPool(31, new AttackUnitData[0], fallbackBoss: null);
            foreach (var w in plan.waves)
                Assert.AreEqual(2, w.groups.Count, "보스 없음 → 일반 2그룹 웨이브");
        }

        [Test]
        public void BossSelectionIsDeterministicForSameSeed()
        {
            var a = CreateUnit("BossA");
            var b = CreateUnit("BossB");
            try
            {
                var first = GenerateBossPool(9001, new[] { a, b });
                var second = GenerateBossPool(9001, new[] { a, b });
                for (int i = 0; i < first.waves.Count; i++)
                {
                    if ((i + 1) % 5 != 0) continue;
                    Assert.AreSame(first.waves[i].groups[0].unit, second.waves[i].groups[0].unit, $"wave {i} 보스 선택 결정론");
                }
            }
            finally { Object.DestroyImmediate(a); Object.DestroyImmediate(b); }
        }

        // 선봉은 항상 pool 안의 보스이고, seed 를 바꾸면 **양쪽 보스가 다 나온다**(선택이 고정 아님).
        [Test]
        public void MultiBossPoolRotatesWithinPoolOnly()
        {
            var a = CreateUnit("BossA");
            var b = CreateUnit("BossB");
            try
            {
                var seen = new HashSet<AttackUnitData>();
                for (int seed = 1; seed <= 30; seed++)
                {
                    var plan = GenerateBossPool(seed, new[] { a, b });
                    for (int i = 0; i < plan.waves.Count; i++)
                    {
                        if ((i + 1) % 5 != 0) continue;
                        var leader = plan.waves[i].groups[0].unit;
                        Assert.IsTrue(leader == a || leader == b, $"seed {seed} wave {i} 선봉이 pool 밖");
                        Assert.AreNotSame(leader, plan.waves[i].groups[1].unit, $"seed {seed} wave {i} escort≠보스");
                        seen.Add(leader);
                    }
                }
                Assert.AreEqual(2, seen.Count, "30 seed 안에 두 보스가 모두 등장해야 로테이션이 실제로 도는 것");
            }
            finally { Object.DestroyImmediate(a); Object.DestroyImmediate(b); }
        }

        [Test]
        public void BossPoolWithNullEntriesIsFiltered()
        {
            var boss = CreateUnit("Boss");
            try
            {
                var plan = GenerateBossPool(55, new AttackUnitData[] { null, boss, null, boss });
                for (int i = 0; i < plan.waves.Count; i++)
                {
                    if ((i + 1) % 5 != 0) continue;
                    Assert.AreSame(boss, plan.waves[i].groups[0].unit, $"wave {i} null/중복 제거 후 단일 보스");
                }
            }
            finally { Object.DestroyImmediate(boss); }
        }

        // pool 방어가 단일 bossUnit 에서 pool 전체 루프로 확장됐는지 — 원소가 여럿이어도 전부 제외.
        [Test]
        public void AllBossPoolEntriesAreExcludedFromRegularSpawns()
        {
            var a = CreateUnit("BossA");
            var b = CreateUnit("BossB");
            var polluted = new List<AttackUnitData>(_units) { a, b }; // 둘 다 실수로 섞임
            try
            {
                var plan = GenerateBossPool(1357, new[] { a, b }, pool: polluted);
                for (int i = 0; i < plan.waves.Count; i++)
                {
                    var w = plan.waves[i];
                    if ((i + 1) % 5 == 0)
                    {
                        Assert.AreEqual(1, w.groups[0].count, $"wave {i} 보스 1기");
                        Assert.IsFalse(w.groups[1].unit == a || w.groups[1].unit == b, $"wave {i} escort 에 보스 누출");
                    }
                    else
                    {
                        foreach (var g in w.groups)
                            Assert.IsFalse(g.unit == a || g.unit == b, $"wave {i} 비-보스 웨이브 보스 누출");
                    }
                }
            }
            finally { Object.DestroyImmediate(a); Object.DestroyImmediate(b); }
        }

        // ── boss-mamemo unit 0 — 보스 3종 확장 ──────────────────────────────────────

        // 세 번째 보스를 pool 에 넣어도 **잡몹 편성은 안 바뀐다.** 근거는 rng 소비 "횟수" 다:
        // Random.NextInt(min,max) 는 range 와 무관하게 NextState() 1회만 쓰고(rejection 루프
        // 없음), 보스 치환은 일반 웨이브 루프가 끝난 뒤에 돈다. 그래서 pool 크기는 스트림의
        // **위치**를 바꾸지 않고 뽑히는 **값**만 바꾼다 — 보스 종류만 달라진다.
        //
        // 이 테스트가 지키는 것: 누군가 pool 크기에 따라 rng 콜 수가 달라지게 만들면
        // (예: 중복 배제 재추첨, Count>2 분기 추가) escortCount·escortType 이 밀려
        // **라이브 8덱의 보스 웨이브 구성이 통째로 바뀐다.**
        [Test]
        public void ThirdBossInPoolDoesNotChangeEscortComposition()
        {
            var a = CreateUnit("BossA");
            var b = CreateUnit("BossB");
            var c = CreateUnit("BossC");
            try
            {
                for (int seed = 1; seed <= 20; seed++)
                {
                    var two = GenerateBossPool(seed, new[] { a, b });
                    var three = GenerateBossPool(seed, new[] { a, b, c });
                    Assert.AreEqual(two.waves.Count, three.waves.Count, $"seed {seed} wave count");

                    for (int i = 0; i < two.waves.Count; i++)
                    {
                        var w2 = two.waves[i];
                        var w3 = three.waves[i];
                        Assert.AreEqual(w2.groups.Count, w3.groups.Count, $"seed {seed} wave {i} group count");

                        if ((i + 1) % 5 == 0)
                        {
                            // 보스 웨이브: 선봉은 종류가 달라질 수 있지만 **호위는 같아야** 한다.
                            Assert.AreEqual(1, w3.groups[0].count, $"seed {seed} wave {i} 보스 1기");
                            Assert.IsTrue(w3.groups[0].unit == a || w3.groups[0].unit == b || w3.groups[0].unit == c,
                                $"seed {seed} wave {i} 선봉이 pool 밖");
                            Assert.AreSame(w2.groups[1].unit, w3.groups[1].unit, $"seed {seed} wave {i} escortType");
                            Assert.AreEqual(w2.groups[1].count, w3.groups[1].count, $"seed {seed} wave {i} escortCount");
                        }
                        else
                        {
                            // 비-보스 웨이브: 완전히 동일.
                            for (int g = 0; g < w2.groups.Count; g++)
                            {
                                Assert.AreSame(w2.groups[g].unit, w3.groups[g].unit, $"seed {seed} wave {i} g{g} unit");
                                Assert.AreEqual(w2.groups[g].count, w3.groups[g].count, $"seed {seed} wave {i} g{g} count");
                            }
                        }
                    }
                }
            }
            finally { Object.DestroyImmediate(a); Object.DestroyImmediate(b); Object.DestroyImmediate(c); }
        }

        // 3종이 실제로 다 나온다 — 안 그러면 위 테스트는 "2종 pool 을 두 번 돌린 것"과 구분이 안 된다.
        [Test]
        public void ThreeBossPoolRotatesAllThree()
        {
            var a = CreateUnit("BossA");
            var b = CreateUnit("BossB");
            var c = CreateUnit("BossC");
            try
            {
                var seen = new HashSet<AttackUnitData>();
                for (int seed = 1; seed <= 30; seed++)
                {
                    var plan = GenerateBossPool(seed, new[] { a, b, c });
                    for (int i = 0; i < plan.waves.Count; i++)
                    {
                        if ((i + 1) % 5 != 0) continue;
                        seen.Add(plan.waves[i].groups[0].unit);
                    }
                }
                Assert.AreEqual(3, seen.Count, "30 seed 안에 세 보스가 모두 등장해야 3종 로테이션이 실제로 도는 것");
            }
            finally { Object.DestroyImmediate(a); Object.DestroyImmediate(b); Object.DestroyImmediate(c); }
        }

        // bossPool 경로는 GenerateBoss 에 인자 하나만 더한 것이다 — 호출을 복제하지 않는다.
        // 복제하면 "bossPool 만 다르다" 는 회귀 테스트의 전제가 두 헬퍼 사이 눈대중 대조로 남고,
        // 한쪽의 180f/10/15/0.35f 를 만지는 순간 서로 다른 두 설정의 비교가 된다.
        private GeneratedWavePlan GenerateBossPool(int seed, IReadOnlyList<AttackUnitData> bosses,
            AttackUnitData fallbackBoss = null, IReadOnlyList<AttackUnitData> pool = null, int waveCount = 12)
            => GenerateBoss(seed, fallbackBoss, 5, 3, 4, pool, waveCount, bosses);

        private GeneratedWavePlan GenerateBoss(int seed, AttackUnitData boss, int interval, int escMin, int escMax,
            IReadOnlyList<AttackUnitData> pool = null, int waveCount = 12,
            IReadOnlyList<AttackUnitData> bossPool = null)
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
                escMax,
                bossPool: bossPool);
        }

        private static AttackUnitData CreateUnit(string name)
        {
            var unit = ScriptableObject.CreateInstance<AttackUnitData>();
            unit.displayName = name;
            return unit;
        }
    }
}
