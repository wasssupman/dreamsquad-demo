using System.Collections.Generic;
using NUnit.Framework;
using Wassup.BattleCore;
using Wassup.BattleCore.Wave;
using Random = Unity.Mathematics.Random;

namespace Wassup.Tests.EditMode.Core
{
    // battle-core-rebuild unit 4 — 웨이브 생성기 이식.
    //
    // ⚠ **rng 소비 순서가 계약이다.** 옛 EditMode 테스트는 SO(`AttackUnitData`)를 만들어
    // 돌렸고 헤드리스 lane 에서 컴파일되지 않는다. 그래서 그 테스트가 지키던 것을
    // **오라클**로 옮겼다: 옛 구현이 rng 를 소비하던 차례를 이 파일이 손으로 재현하고,
    // 생성기의 결과가 그것과 같은지 본다. 순서가 한 칸이라도 바뀌면 값이 갈린다.
    [TestFixture]
    public class WaveGeneratorTests
    {
        private static EnemyDef[] Enemies() => new[]
        {
            CoreMatchFixtures.Enemy("basic", 60f, cls: 1, reward: 2),
            CoreMatchFixtures.Enemy("swift", 60f, cls: 2, reward: 2),
            CoreMatchFixtures.Enemy("tanker", 120f, cls: 1, reward: 3),
            CoreMatchFixtures.Enemy("boss", 300f, cls: 3, reward: 5),
        };

        private static WaveDeckDef Deck()
        {
            var d = WaveDeckDef.Empty();
            d.GeneratorVersion = 1;
            d.WaveSeed = 4242;
            d.TimerDurationSec = 180f;
            d.MinWaveCount = 10;
            d.MaxWaveCount = 15;
            d.MinUnitsPerWave = 10;
            d.MaxUnitsPerWave = 15;
            d.WaveCountJitter = 1;
            d.IntraWaveSpacingSec = 0.35f;
            d.MaxWaveIntervalSec = 20f;
            d.UnitGrowthPerWave = 1f;
            d.EnemyPool = new[] { 0, 1, 2 };
            d.BossPool = System.Array.Empty<int>();
            return d;
        }

        // ── 오라클 ───────────────────────────────────────────────────────────
        //
        // 옛 `WavePatternGenerator` 가 rng 를 소비하던 **차례 그대로**다. 순수 함수
        // (`ExponentialWaveTotal` · `ResolveWaveEligibleIndex` · `ClampGroupCounts`)는
        // rng 를 안 건드리므로 생성기의 것을 그대로 불러 쓴다 — 그래야 이 오라클이
        // 「소비 순서」만 증언하고 산식을 복제하지 않는다.
        private static List<(int a, int ca, int b, int cb)> Oracle(
            in WaveDeckDef deck, EnemyDef[] enemies, int seed, out int waveCount)
        {
            var pool = new List<int>(deck.EnemyPool);
            var bosses = new List<int>(deck.BossPool);
            for (int i = 0; i < bosses.Count; i++) pool.Remove(bosses[i]);

            var rng = new Random((uint)System.Math.Abs(seed != 0 ? seed : 1));
            int minWaves = System.Math.Max(2, System.Math.Min(deck.MinWaveCount, deck.MaxWaveCount));
            int maxWaves = System.Math.Max(minWaves, System.Math.Max(deck.MinWaveCount, deck.MaxWaveCount));
            waveCount = rng.NextInt(minWaves, maxWaves + 1);          // ① 웨이브 수

            int minUnits = System.Math.Max(2, System.Math.Min(deck.MinUnitsPerWave, deck.MaxUnitsPerWave));
            int maxUnits = System.Math.Max(minUnits, System.Math.Max(deck.MinUnitsPerWave, deck.MaxUnitsPerWave));

            var result = new List<(int, int, int, int)>(waveCount);
            for (int i = 0; i < waveCount; i++)
            {
                int aIndex = rng.NextInt(0, pool.Count);              // ② 종 A
                int bIndex = rng.NextInt(0, pool.Count - 1);          // ③ 종 B
                if (bIndex >= aIndex) bIndex++;

                int waveNumber = i + 1;
                aIndex = WaveGenerator.ResolveWaveEligibleIndex(pool, enemies, aIndex, waveNumber);
                bIndex = WaveGenerator.ResolveWaveEligibleIndex(pool, enemies, bIndex, waveNumber, aIndex);

                float jitter01 = rng.NextFloat();                     // ④ 수량 지터
                int total = WaveGenerator.ExponentialWaveTotal(
                    i, minUnits, maxUnits, deck.UnitGrowthPerWave, deck.WaveCountJitter, jitter01,
                    deck.RampBreakWave, deck.RampBreakUnits);
                int countA = rng.NextInt(1, total);                   // ⑤ 배분
                int countB = total - countA;

                WaveGenerator.ClampGroupCounts(
                    enemies[pool[aIndex]].MaxPerWave, enemies[pool[bIndex]].MaxPerWave,
                    ref countA, ref countB);

                result.Add((pool[aIndex], countA, pool[bIndex], countB));
            }

            // ⑥ 보스 후처리 — 랜덤 루프 **뒤**다. 보스가 1종이면 선택 rng 를 **안 쓴다**.
            if (bosses.Count > 0 && deck.BossWaveInterval > 0)
            {
                int escortMin = System.Math.Max(1, System.Math.Min(deck.BossEscortMin, deck.BossEscortMax));
                int escortMax = System.Math.Max(escortMin, System.Math.Max(deck.BossEscortMin, deck.BossEscortMax));
                for (int i = 0; i < waveCount; i++)
                {
                    if ((i + 1) % deck.BossWaveInterval != 0) continue;
                    int boss = bosses.Count == 1 ? bosses[0] : bosses[rng.NextInt(0, bosses.Count)];
                    int escortCount = rng.NextInt(escortMin, escortMax + 1);
                    int escortType = pool[WaveGenerator.ResolveWaveEligibleIndex(
                        pool, enemies, rng.NextInt(0, pool.Count), i + 1)];
                    int cap = enemies[escortType].MaxPerWave;
                    if (cap > 0) escortCount = System.Math.Min(escortCount, cap);
                    result[i] = (boss, 1, escortType, escortCount);
                }
            }
            return result;
        }

        private static void AssertMatchesOracle(in WaveDeckDef deck, EnemyDef[] enemies, int seed)
        {
            var plan = WaveGenerator.Generate(in deck, enemies, seed, laneCount: 2);
            var oracle = Oracle(in deck, enemies, seed, out int waveCount);

            Assert.AreEqual(waveCount, plan.WaveCount, "웨이브 수는 스트림의 첫 소비다");
            for (int i = 0; i < waveCount; i++)
            {
                var g = plan.Waves[i].Groups;
                Assert.AreEqual(2, g.Length, $"wave {i} 는 정확히 2그룹이다");
                Assert.AreEqual(oracle[i].a, g[0].EnemyIndex, $"wave {i} 종 A");
                Assert.AreEqual(oracle[i].ca, g[0].Count, $"wave {i} 수량 A");
                Assert.AreEqual(oracle[i].b, g[1].EnemyIndex, $"wave {i} 종 B");
                Assert.AreEqual(oracle[i].cb, g[1].Count, $"wave {i} 수량 B");
            }
        }

        [Test]
        public void 레거시_2종_경로의_rng_소비_순서가_그대로다()
            => AssertMatchesOracle(Deck(), Enemies(), 4242);

        [Test]
        public void 보스가_1종이면_선택_rng_를_소비하지_않는다()
        {
            var deck = Deck();
            deck.BossPool = new[] { 3 };
            deck.BossWaveInterval = 5;
            deck.BossEscortMin = 3;
            deck.BossEscortMax = 4;
            // 오라클도 같은 가드를 갖는다 — 일치하면 「미소비」가 증언된다.
            AssertMatchesOracle(deck, Enemies(), 777);
        }

        [Test]
        public void 등장_게이트는_rng_를_소비하지_않는다()
        {
            var enemies = Enemies();
            enemies[2].MinWaveNumber = 8;       // 탱커는 8웨이브부터
            var deck = Deck();
            AssertMatchesOracle(deck, enemies, 909);

            // 게이트에 걸리는 웨이브가 실제로 있었는지도 본다(안 그러면 아무것도 증언 못 한다).
            var plan = WaveGenerator.Generate(in deck, enemies, 909, 2);
            bool early = false;
            for (int i = 0; i < 7 && i < plan.WaveCount; i++)
                for (int g = 0; g < plan.Waves[i].Groups.Length; g++)
                    if (plan.Waves[i].Groups[g].EnemyIndex == 2) early = true;
            Assert.IsFalse(early, "뽑은 인덱스만 사후 보정하므로 8웨이브 전엔 안 나온다");
        }

        [Test]
        public void 동시_등장_상한은_rng_를_소비하지_않는다()
        {
            var enemies = Enemies();
            enemies[0].MaxPerWave = 3;
            AssertMatchesOracle(Deck(), enemies, 31337);
        }

        [Test]
        public void 같은_시드는_같은_플랜을_낸다()
        {
            var deck = Deck();
            var a = WaveGenerator.Generate(in deck, Enemies(), 555, 2);
            var b = WaveGenerator.Generate(in deck, Enemies(), 555, 2);

            Assert.AreEqual(a.WaveCount, b.WaveCount);
            for (int i = 0; i < a.WaveCount; i++)
                for (int g = 0; g < a.Waves[i].Groups.Length; g++)
                {
                    Assert.AreEqual(a.Waves[i].Groups[g].EnemyIndex, b.Waves[i].Groups[g].EnemyIndex);
                    Assert.AreEqual(a.Waves[i].Groups[g].Count, b.Waves[i].Groups[g].Count);
                }
        }

        [Test]
        public void 웨이브_수와_수량은_저작_범위_안이다()
        {
            var deck = Deck();
            var plan = WaveGenerator.Generate(in deck, Enemies(), 55, 2);

            Assert.GreaterOrEqual(plan.WaveCount, 10);
            Assert.LessOrEqual(plan.WaveCount, 15);
            for (int i = 0; i < plan.WaveCount; i++)
            {
                Assert.GreaterOrEqual(plan.Waves[i].TotalCount, 10);
                Assert.LessOrEqual(plan.Waves[i].TotalCount, 15);
                Assert.GreaterOrEqual(plan.Waves[i].Groups[0].Count, 1);
                Assert.GreaterOrEqual(plan.Waves[i].Groups[1].Count, 1);
                Assert.AreNotEqual(plan.Waves[i].Groups[0].EnemyIndex,
                                   plan.Waves[i].Groups[1].EnemyIndex, "한 웨이브 = 2종");
            }
        }

        [Test]
        public void 적이_2종_미만이면_생성이_거절된다()
        {
            var deck = Deck();
            deck.EnemyPool = new[] { 0 };
            Assert.Throws<System.ArgumentException>(
                () => WaveGenerator.Generate(in deck, Enemies(), 1, 2));
        }

        [Test]
        public void 보스는_잡몹_풀에서_방어적으로_제외된다()
        {
            var deck = Deck();
            deck.EnemyPool = new[] { 0, 1, 2, 3 };   // 실수로 보스가 섞였다
            deck.BossPool = new[] { 3 };
            deck.BossWaveInterval = 5;
            deck.BossEscortMin = 2;
            deck.BossEscortMax = 2;

            string warned = null;
            var plan = WaveGenerator.Generate(in deck, Enemies(), 12, 2, m => warned = m);

            Assert.IsNotNull(warned, "조용히 고치지 않는다");
            for (int i = 0; i < plan.WaveCount; i++)
            {
                if (plan.Waves[i].IsBoss) continue;
                for (int g = 0; g < plan.Waves[i].Groups.Length; g++)
                    Assert.AreNotEqual(3, plan.Waves[i].Groups[g].EnemyIndex,
                        "비-보스 웨이브에 보스가 나오면 안 된다");
            }
        }

        [Test]
        public void 보스_웨이브는_선봉이_보스고_표식이_붙는다()
        {
            var deck = Deck();
            deck.BossPool = new[] { 3 };
            deck.BossWaveInterval = 5;
            deck.BossEscortMin = 3;
            deck.BossEscortMax = 4;
            var plan = WaveGenerator.Generate(in deck, Enemies(), 4242, 2);

            Assert.IsTrue(plan.Waves[4].IsBoss, "5번째 웨이브");
            Assert.AreEqual(3, plan.Waves[4].Groups[0].EnemyIndex);
            Assert.AreEqual(1, plan.Waves[4].Groups[0].Count, "보스는 1기");
            Assert.IsFalse(plan.Waves[3].IsBoss);
        }

        [Test]
        public void 두_단계_곡선은_break_저작이_없으면_기존_지수다()
        {
            // rng 를 안 받는 순수 함수라 값만 본다.
            for (int i = 0; i < 20; i++)
                Assert.AreEqual(
                    WaveGenerator.ExponentialWaveTotal(i, 5, 24, 1.12f, 0, 0.5f),
                    WaveGenerator.ExponentialWaveTotal(i, 5, 24, 1.12f, 0, 0.5f, 0, 0),
                    $"i={i} — break 미저작은 레거시와 같은 경로다");

            // 저작하면 본편이 평탄 상승, break 부터 지수.
            Assert.AreEqual(5, WaveGenerator.ExponentialWaveTotal(0, 5, 24, 1.12f, 0, 0.5f, 15, 12));
            Assert.AreEqual(12, WaveGenerator.ExponentialWaveTotal(14, 5, 24, 1.12f, 0, 0.5f, 15, 12),
                "웨이브 15 = 인덱스 14 가 기점이다");
            Assert.Greater(WaveGenerator.ExponentialWaveTotal(18, 5, 24, 1.12f, 0, 0.5f, 15, 12), 12);
        }

        [Test]
        public void 슬롯_분배는_잔여를_앞_슬롯부터_준다()
        {
            var counts = new int[3];
            int scaled = WaveGenerator.DistributeSlotCounts(10, 1f, 3, 24, counts);
            Assert.AreEqual(10, scaled);
            Assert.AreEqual(new[] { 4, 3, 3 }, counts, "랜덤으로 흘리면 같은 시드에서 결과가 갈린다");

            // 배율이 하한(슬롯 수)에 먹히지 않는다.
            var counts2 = new int[2];
            Assert.AreEqual(4, WaveGenerator.DistributeSlotCounts(10, 0.4f, 2, 24, counts2));
        }

        [Test]
        public void 입구_배정은_같은_위상이면_같은_입구다()
        {
            var slots = new[]
            {
                new WaveSlotDef { LaneGroup = 0 },
                new WaveSlotDef { LaneGroup = 1 },
                new WaveSlotDef { LaneGroup = 0 },
                new WaveSlotDef { LaneGroup = -1 },
            };
            var lanes = new int[4];
            Assert.IsTrue(WaveGenerator.AssignLanes(slots, 2, 0, lanes));
            Assert.AreEqual(lanes[0], lanes[2], "같은 위상 = 같은 입구");
            Assert.AreNotEqual(lanes[0], lanes[1], "다른 위상 = 다른 입구");
            Assert.AreEqual(-1, lanes[3], "무지정은 무지정으로 통과한다");

            Assert.IsFalse(WaveGenerator.AssignLanes(slots, 1, 0, lanes),
                "요구 입구 수가 맵의 스폰 수를 넘으면 후보에서 버린다");
        }

        [Test]
        public void 라운드로빈_펼침은_소진된_그룹을_건너뛴다()
        {
            var wave = new PlannedWave(0, 0f, new[]
            {
                new PlannedGroup(0, 1),
                new PlannedGroup(1, 3),
                new PlannedGroup(2, 2),
            });
            var into = new List<PlannedSpawn>();
            WaveGenerator.Expand(in wave, 0f, 2, 0.35f, into);

            Assert.AreEqual(6, into.Count);
            Assert.AreEqual(new[] { 0, 1, 2, 1, 2, 1 },
                new[] { into[0].EnemyIndex, into[1].EnemyIndex, into[2].EnemyIndex,
                        into[3].EnemyIndex, into[4].EnemyIndex, into[5].EnemyIndex });
            Assert.AreEqual(0.35f * 5, into[5].TriggerTimeSec, 1e-4f);
        }

        [Test]
        public void 저작_플랜은_타임라인이고_리드인이_0이다()
        {
            var plan = new WavePlanDef
            {
                DisplayName = "t",
                TimerDurationSec = 0f,
                Waves = new[]
                {
                    new AuthoredWaveDef
                    {
                        DurationSec = 12f,
                        IntervalSec = 0.5f,
                        Groups = new[]
                        {
                            new AuthoredGroupDef { EnemyIndex = 0, Count = 2, TriggerTimeSec = 3f, LaneIndex = 1, PathIndex = -1 },
                        },
                    },
                    new AuthoredWaveDef { DurationSec = 8f, IntervalSec = 0f, Groups = System.Array.Empty<AuthoredGroupDef>() },
                },
            };
            var baked = WaveGenerator.FromAuthored(in plan);

            Assert.AreEqual(2, baked.WaveCount);
            Assert.AreEqual(0f, baked.SpawnLeadInSec, "겹쳐 주면 이중 가산이 된다");
            Assert.AreEqual(12f, baked.Waves[1].TriggerTimeSec, 1e-4f, "앞 웨이브 길이의 합");
            Assert.AreEqual(WaveLayout.Timeline, baked.Waves[0].Layout);

            var into = new List<PlannedSpawn>();
            WaveGenerator.Expand(in baked.Waves[0], 0f, 2, 0f, into);
            Assert.AreEqual(2, into.Count);
            Assert.AreEqual(3f, into[0].TriggerTimeSec, 1e-4f);
            Assert.AreEqual(3.5f, into[1].TriggerTimeSec, 1e-4f);
            Assert.AreEqual(1, into[0].LaneIndex, "저작한 입구는 라운드로빈을 우회한다");
        }
    }
}
