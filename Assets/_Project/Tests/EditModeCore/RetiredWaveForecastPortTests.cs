using System.Collections.Generic;
using NUnit.Framework;
using Unity.Mathematics;
using Wassup.BattleCore;
using Wassup.BattleCore.Map;
using Wassup.BattleCore.Move;
using Wassup.BattleCore.Wave;

namespace Wassup.Tests.EditMode.Core
{
    // battle-core-rebuild unit 9 구현 2 — 옛 예고·펼침·명목 간격 테스트(`SpawnAlertForecastTests` ·
    // `WaveSpawnForecastTests` · `WaveNominalIntervalTests`)의 **규칙**을 코어로 옮긴다.
    //
    // 옛 예고는 브리지가 큐잉 때 한 번 구운 배열(`TryGetSpawnGuideForecast`)이었고, 코어는 대기열을
    // **읽는다**(`WaveScheduler.CollectForecast`). 그래서 판 위 규칙은 `BattleMatch` 로 굴리고,
    // 펼침 산식(`Expand` · `EffectiveSpawnIndex`)은 직접 부른다. `WaveGeneratorTests` 의
    // 「라운드로빈 소진 그룹 건너뜀」·「저작 플랜 타임라인」과 겹치는 단언은 뺐다.
    public class RetiredWaveForecastPortTests
    {
        private const float Interval = 10f;
        private const float LeadIn = 2f;
        private const float Spacing = 1f;
        private const float Tol = 1e-4f;

        // ── 판 픽스처 ────────────────────────────────────────────────────────

        /// <summary>레인 3 판 · 4웨이브 · 웨이브당 4기(2종 레거시 경로) · 리드인 2 · 간격 1 · 상한 10.</summary>
        private static MatchDefinition ThreeLaneDef(System.Action<MatchDefinition> tweak = null)
        {
            var def = CoreMatchFixtures.Definition();
            def.Map.Spawns = new[] { new int2(0, 1), new int2(0, 2), new int2(0, 3) };
            var deck = CoreMatchFixtures.Deck(waveCount: 4, interval: Interval, pullCap: 3);
            deck.MinUnitsPerWave = 4;
            deck.MaxUnitsPerWave = 4;
            deck.IntraWaveSpacingSec = Spacing;
            deck.SpawnLeadInSec = LeadIn;
            def.WaveDeck = deck;
            tweak?.Invoke(def);
            def.ConfigHash = def.ComputeConfigHash();
            return def;
        }

        private static List<WaveScheduler.SpawnForecast> Forecast(BattleMatch m)
        {
            var into = new List<WaveScheduler.SpawnForecast>();
            m.Waves.CollectForecast(into);
            return into;
        }

        private static float FirstAt(List<WaveScheduler.SpawnForecast> f, int lane)
        {
            float first = -1f;
            for (int i = 0; i < f.Count; i++)
                if (f[i].Lane == lane && (first < 0f || f[i].FirstSpawnSec < first)) first = f[i].FirstSpawnSec;
            return first;
        }

        /// <summary>전투 첫 틱 — 웨이브 1 이 그 틱의 **시작 시각**에 예약된다(시계는 파이프라인 끝에서 센다).</summary>
        private static float QueueWaveOne(BattleMatch m)
        {
            float at = m.Clock.BattleTime;
            m.Tick();
            return at;
        }

        private static void TickUntil(BattleMatch m, float battleTime)
        {
            for (int guard = 0; guard < 60 * 60 && m.Clock.BattleTime < battleTime; guard++) m.Tick();
        }

        // ════════════════════════════════════════════════════════════════════
        // 26 · 예고 창 (옛 SpawnAlertForecastTests)
        // ════════════════════════════════════════════════════════════════════

        // 옛 SpawnAlertForecastTests::NoQueuedWave_HasNoForecast — 큐잉 전에는 예고가 없다
        [Test]
        public void 웨이브를_올리기_전에는_예고가_없다()
        {
            var m = CoreMatchFixtures.BeginBattle(ThreeLaneDef());
            Assert.AreEqual(0, m.Waves.CollectForecast(new List<WaveScheduler.SpawnForecast>()), "큐잉 전 예고");
        }

        // 옛 SpawnAlertForecastTests::WaveOne_GetsForecast_FromQueueTime — 웨이브 1 도 창을 얻는다 · 입구별 시각 = 실스폰
        [Test]
        public void 웨이브_1도_큐잉_시점부터_예고를_받고_입구별_시각은_실스폰과_같다()
        {
            var m = CoreMatchFixtures.BeginBattle(ThreeLaneDef());
            float t0 = QueueWaveOne(m);
            var f = Forecast(m);

            // 웨이브 0 엔트리 4개: 기준 t0+리드인에서 간격 1 → lane = (0×1000 + i) % 3 = 0,1,2,0.
            Assert.AreEqual(t0 + LeadIn, FirstAt(f, 0), Tol);
            Assert.AreEqual(t0 + LeadIn + Spacing, FirstAt(f, 1), Tol);
            Assert.AreEqual(t0 + LeadIn + 2 * Spacing, FirstAt(f, 2), Tol);
            Assert.Greater(FirstAt(f, 0), m.Clock.BattleTime, "리드인만큼의 창이 있다 — 첫 적보다 예고가 먼저다");

            // 실스폰: 입구 0 의 첫 적은 예고한 시각의 틱에 나온다.
            var spawned = CoreMatchFixtures.Listen(m, CoreEventKind.UnitSpawned);
            TickUntil(m, t0 + LeadIn - 0.5f);
            Assert.AreEqual(0, spawned.Count, "예고 시각 전에는 안 나온다");
            TickUntil(m, t0 + LeadIn + 0.5f * Spacing);
            Assert.AreEqual(1, spawned.Count, "예고 시각에 첫 적이 나온다");
        }

        // 옛 SpawnAlertForecastTests::ForcedWave_GetsForecast — 당긴 웨이브도 같은 창(당긴 시점 + 리드인)
        [Test]
        public void 당긴_웨이브도_당긴_시점_더하기_리드인의_예고를_받는다()
        {
            var m = CoreMatchFixtures.BeginBattle(ThreeLaneDef());
            float t0 = QueueWaveOne(m);
            // 웨이브 1 을 다 내보내 대기열을 비운다 — 그래야 예보가 당긴 웨이브만의 것이다.
            TickUntil(m, t0 + LeadIn + 3 * Spacing + 0.1f);
            Assert.AreEqual(0, m.Waves.PendingSpawns, "전제: 웨이브 1 이 다 나왔다");

            float pulledAt = m.Clock.BattleTime;
            Assert.IsTrue(m.Apply(Command.PullWave()).Accepted);
            var f = Forecast(m);
            Assert.Greater(f.Count, 0, "당긴 웨이브도 예고를 받아야 한다");
            float earliest = float.MaxValue;
            foreach (var e in f) earliest = System.Math.Min(earliest, e.FirstSpawnSec);
            Assert.AreEqual(pulledAt + LeadIn, earliest, Tol, "당긴 시점 + 리드인");
        }

        // 옛 SpawnAlertForecastTests::ForecastSurvivesUntilTheLastGuideSpawn — 마지막 스폰까지 유지 · 지나면 사라짐
        [Test]
        public void 예고는_그_입구의_마지막_스폰까지_남고_지나면_사라진다()
        {
            var m = CoreMatchFixtures.BeginBattle(ThreeLaneDef());
            float t0 = QueueWaveOne(m);   // 입구 시각 t0+2 / +3 / +4 / (입구 0 두 번째) +5

            TickUntil(m, t0 + LeadIn + 2.5f * Spacing);
            var f = Forecast(m);
            Assert.AreEqual(1, f.Count, "입구 0 의 두 번째 적이 남았다 — 뒷 적이 자기 유닛보다 먼저 사라지면 안 된다");
            Assert.AreEqual(0, f[0].Lane);
            Assert.AreEqual(t0 + LeadIn + 3 * Spacing, f[0].FirstSpawnSec, Tol, "남은 것 중 **첫** 시각으로 갱신된다");

            TickUntil(m, t0 + LeadIn + 3 * Spacing + 0.1f);
            Assert.AreEqual(0, Forecast(m).Count, "마지막 스폰이 지나면 예고가 사라진다");
        }

        // 옛 SpawnAlertForecastTests::WaveOne_GuideForecastPreservesSwarmAndActualLane — 같은 입구의 서로 다른 스웜은 병합하지 않는다
        // 옛 WaveSpawnForecastTests::DifferentSwarmsOnSameLane_RemainSeparateGuides — 같은 규칙
        [Test]
        [Ignore("unit 9 — 옛 규칙과 다름: 옛 예보는 (스웜 × 실제 입구)마다 한 줄이라 같은 입구의 두 종이 따로 남았다 "
              + "(Scripts/Data/WavePatternGenerator.cs `BuildSpawnGuideForecasts`). 코어는 (입구 × 컨셉 경로)로 병합하고 "
              + "먼저 나올 적 하나만 싣는다(Scripts/BattleCore/Owners/WaveScheduler.cs:156-178 `CollectForecast`) — "
              + "두 종의 통행 층·저작 경로가 달라도 예고선이 하나로 접힌다.")]
        public void 같은_입구의_서로_다른_종은_예고가_병합되지_않는다()
        {
            var m = CoreMatchFixtures.BeginBattle(ThreeLaneDef(d =>
            {
                d.Enemies[0].WaypointPathIndex = 0;
                d.Enemies[1].WaypointPathIndex = 1;
            }));
            float t0 = QueueWaveOne(m);
            var f = Forecast(m);

            // 기대 = 펼침의 distinct (입구 × 종). 4기 A·B 인터리브가 입구 0,1,2,0 을 돌아 한 입구에 두 종이 선다.
            var expanded = new List<PlannedSpawn>();
            WaveGenerator.Expand(m.Waves.WaveAt(0), t0 + LeadIn, 3, Spacing, expanded);
            var pairs = new HashSet<(int, int)>();
            foreach (var s in expanded) pairs.Add((s.LaneIndex, s.EnemyIndex));
            Assert.Less(new HashSet<int>(System.Linq.Enumerable.Select(pairs, p => p.Item1)).Count, pairs.Count,
                "전제: 한 입구에 두 종이 서는 편성이어야 이 규칙을 물을 수 있다");
            Assert.AreEqual(pairs.Count, f.Count, "같은 입구의 서로 다른 스웜을 병합하지 않는다");
        }

        // ════════════════════════════════════════════════════════════════════
        // 27 · 입구별 첫 스폰 예보 · 레인 회전 · 경로 해석 (옛 WaveSpawnForecastTests)
        // ════════════════════════════════════════════════════════════════════

        // 옛 WaveSpawnForecastTests::EffectiveSpawnIndex_TwoLanesClampsAuthoredIndex — 2레인 이하는 저작값 클램프
        [Test]
        public void 이레인_이하는_저작_입구를_클램프한다()
        {
            Assert.AreEqual(0, WaveGenerator.EffectiveSpawnIndex(0, 999, 2));
            Assert.AreEqual(1, WaveGenerator.EffectiveSpawnIndex(5, 0, 2));
            Assert.AreEqual(0, WaveGenerator.EffectiveSpawnIndex(-1, 0, 2));
        }

        // 옛 WaveSpawnForecastTests::EffectiveSpawnIndex_ThreePlusLanesRoundRobinsDeckIndex — 3레인+ 는 순번 라운드로빈
        [Test]
        public void 삼레인_이상은_펼침_순번으로_라운드로빈한다()
        {
            Assert.AreEqual(1001 % 3, WaveGenerator.EffectiveSpawnIndex(0, 1001, 3));
            Assert.AreEqual(3000 % 3, WaveGenerator.EffectiveSpawnIndex(7, 3000, 3));
        }

        private static float[] FirstSpawnTimes(in PlannedWave wave, float baseSec, int lanes, float spacing)
        {
            var result = new float[lanes];
            for (int i = 0; i < lanes; i++) result[i] = -1f;
            var into = new List<PlannedSpawn>();
            WaveGenerator.Expand(in wave, baseSec, lanes, spacing, into);
            foreach (var s in into)
                if (result[s.LaneIndex] < 0f || s.TriggerTimeSec < result[s.LaneIndex]) result[s.LaneIndex] = s.TriggerTimeSec;
            return result;
        }

        private static PlannedWave TwoGroups(int waveIndex, int a, int b)
            => new PlannedWave(waveIndex, 0f, new[] { new PlannedGroup(0, a), new PlannedGroup(1, b) });

        // 옛 WaveSpawnForecastTests::ThreeLanes_WaveIndexMultipleOfThree_LanesInEntryOrder — 웨이브 3 = 엔트리 순서대로 입구
        [Test]
        public void 삼레인_웨이브_번호가_3의_배수면_엔트리_순서대로_입구를_돈다()
        {
            const float sp = 0.35f, b = 30f;
            var first = FirstSpawnTimes(TwoGroups(3, 5, 5), b, 3, sp);
            Assert.AreEqual(b, first[0], Tol);
            Assert.AreEqual(b + sp, first[1], Tol);
            Assert.AreEqual(b + 2 * sp, first[2], Tol);
        }

        // 옛 WaveSpawnForecastTests::ThreeLanes_LaneRotationFollowsDeckIndexConvention — 웨이브 1 → 1000%3=1 에서 시작
        [Test]
        public void 삼레인_입구_회전은_펼침_순번_규약을_따른다()
        {
            const float sp = 0.35f;
            var first = FirstSpawnTimes(TwoGroups(1, 5, 5), 0f, 3, sp);
            Assert.AreEqual(2 * sp, first[0], Tol);
            Assert.AreEqual(0f, first[1], Tol);
            Assert.AreEqual(sp, first[2], Tol);
        }

        // 옛 WaveSpawnForecastTests::BossWave_BossVanguardLaneGetsBaseTime — 보스 선봉의 입구가 기준 시각
        [Test]
        public void 보스_선봉의_입구가_기준_시각을_받는다()
        {
            const float sp = 0.35f, b = 90f;
            var wave = new PlannedWave(4, b, new[] { new PlannedGroup(0, 1), new PlannedGroup(1, 4) },
                                       0f, WaveLayout.RoundRobin, "", isBoss: true);
            var first = FirstSpawnTimes(wave, b, 3, sp);
            Assert.AreEqual(b, first[1], Tol, "4000%3=1 → 보스(엔트리 0)가 입구 1");
            Assert.AreEqual(b + sp, first[2], Tol);
            Assert.AreEqual(b + 2 * sp, first[0], Tol);
        }

        // 옛 WaveSpawnForecastTests::TwoLanes_UsesAuthoredSpawnIndex — 2레인은 저작 입구(순번 % 2)
        [Test]
        public void 이레인은_저작_입구를_쓴다()
        {
            const float sp = 0.5f, b = 10f;
            var first = FirstSpawnTimes(TwoGroups(2, 2, 2), b, 2, sp);
            Assert.AreEqual(b, first[0], Tol);
            Assert.AreEqual(b + sp, first[1], Tol);
        }

        // 옛 WaveSpawnForecastTests::PerGroupTimeline_PerLaneMinIsNotWaveMin — 타임라인 입구별 최소 ≠ 웨이브 최소
        [Test]
        public void 타임라인_펼침의_입구별_첫_시각은_웨이브_첫_시각이_아니다()
        {
            var wave = new PlannedWave(0, 0f, new[]
            {
                new PlannedGroup(0, 2, 1f),
                new PlannedGroup(1, 1, 0.2f),
            }, 0.5f, WaveLayout.Timeline);
            var first = FirstSpawnTimes(wave, 5f, 3, 0.35f);
            Assert.AreEqual(6f, first[0], Tol);
            Assert.AreEqual(6.5f, first[1], Tol);
            Assert.AreEqual(5.2f, first[2], Tol);
        }

        // 옛 WaveSpawnForecastTests::LaneWithNoSpawnReturnsMinusOne — 스폰 없는 입구는 예보 없음
        [Test]
        public void 스폰이_없는_입구는_첫_시각이_없다()
        {
            const float sp = 0.35f;
            var first = FirstSpawnTimes(TwoGroups(0, 1, 1), 0f, 3, sp);
            Assert.AreEqual(0f, first[0], Tol);
            Assert.AreEqual(sp, first[1], Tol);
            Assert.AreEqual(-1f, first[2]);
        }

        // 옛 WaveSpawnForecastTests::Expansion_PreservesEntryOrderSwarmOriginAndResolvedLane — 펼침은 엔트리 순서·출신 그룹·실제 입구를 보존
        [Test]
        public void 펼침은_엔트리_순서와_출신_그룹과_실제_입구를_보존한다()
        {
            const float sp = 0.4f, b = 3f;
            var into = new List<PlannedSpawn>();
            WaveGenerator.Expand(TwoGroups(0, 2, 1), b, 2, sp, into);

            Assert.AreEqual(3, into.Count);
            CollectionAssert.AreEqual(new[] { 0, 1, 0 }, new[] { into[0].SwarmIndex, into[1].SwarmIndex, into[2].SwarmIndex });
            CollectionAssert.AreEqual(new[] { 0, 1, 0 }, new[] { into[0].LaneIndex, into[1].LaneIndex, into[2].LaneIndex });
            CollectionAssert.AreEqual(new[] { 0, 1, 0 }, new[] { into[0].EnemyIndex, into[1].EnemyIndex, into[2].EnemyIndex });
            Assert.AreEqual(b, into[0].TriggerTimeSec, Tol);
            Assert.AreEqual(b + sp, into[1].TriggerTimeSec, Tol);
            Assert.AreEqual(b + 2 * sp, into[2].TriggerTimeSec, Tol);
        }

        // 옛 WaveSpawnForecastTests::OneSwarmAcrossLanes_GetsGuidePerActualLane — 한 무리가 여러 입구로 가면 입구마다 예보
        [Test]
        public void 한_무리가_여러_입구로_나오면_입구마다_예보가_선다()
        {
            const float at = 7f, sp = 0.25f;
            var def = CoreMatchFixtures.Definition();
            def.Mode.WaveSource = WaveSourceKind.AuthoredPlan;
            def.WavePlan = new WavePlanDef
            {
                DisplayName = "t",
                Waves = new[]
                {
                    new AuthoredWaveDef
                    {
                        DurationSec = 20f,
                        IntervalSec = sp,
                        Groups = new[] { new AuthoredGroupDef { EnemyIndex = 0, Count = 3, TriggerTimeSec = at, LaneIndex = -1, PathIndex = -1 } },
                    },
                },
            };
            def.ConfigHash = def.ComputeConfigHash();
            var m = CoreMatchFixtures.BeginBattle(def);
            m.Tick();

            var f = Forecast(m);
            Assert.AreEqual(2, f.Count, "레인 2 판 · 3기 → 입구 0,1,0");
            Assert.AreEqual(at, FirstAt(f, 0), Tol);
            Assert.AreEqual(at + sp, FirstAt(f, 1), Tol);
        }

        // 옛 WaveSpawnForecastTests::NoLaneRoutes_KeepsShortestPathMarker — 경로 저작이 없으면 -1(골 직행)
        [Test]
        public void 경로_저작이_없으면_예보_경로는_마이너스1이다()
        {
            var m = CoreMatchFixtures.BeginBattle(ThreeLaneDef());
            QueueWaveOne(m);
            foreach (var e in Forecast(m)) Assert.AreEqual(-1, e.PathIndex);
        }

        // 옛 WaveSpawnForecastTests::LaneDefaultRoute_ResolvesIntoForecast_WhenUnitHasNoAuthoredPath — 예보의 경로 해석 = 스폰의 경로 해석(레인 기본)
        // 옛 WaveSpawnForecastTests::AuthoredUnitPath_BeatsLaneDefaultRoute — 적 저작 경로가 레인 기본을 이긴다
        // 사용자 결정 ⑧-2(2026-09-25) — 옛 방식 복원: 예고선은 적이 **실제로 갈 길**을 그린다.
        [Test]
        public void 예보의_경로는_스폰과_같은_해석을_따른다()
        {
            var m = CoreMatchFixtures.BeginBattle(ThreeLaneDef(d =>
            {
                d.Map.SpawnRoutes = new[] { 2, -1, -1 };
                d.Enemies[0].WaypointPathIndex = -1;   // 레인 기본을 받는다
                d.Enemies[1].WaypointPathIndex = 0;    // 종의 저작이 레인 기본을 이긴다
            }));
            QueueWaveOne(m);

            foreach (var e in Forecast(m))
            {
                int expected = WaypointRouting.ResolvePathIndex(
                    m.Definition.Enemies[e.EnemyIndex].WaypointPathIndex, -1, m.Definition.Map.RouteForSpawn(e.Lane));
                Assert.AreEqual(expected, e.PathIndex, $"입구 {e.Lane}: 예고선이 실제 적과 다른 길을 그린다");
            }
        }

        // 사용자 결정 ⑧-2 — 해석 3단이 각각 예고선에 나타난다: 적 저작 경로(비행) > 레인 기본 > 최단(-1).
        // 편성 우연에 기대지 않게 입구를 지정한 저작 플랜으로 세 단을 한 판에 세운다.
        [Test]
        public void 예보_경로는_적_저작_레인_기본_최단_순으로_이긴다()
        {
            const int FlyerPath = 0, LaneDefaultPath = 2;
            const int Walker = 0, Flyer = 1;
            var def = ThreeLaneDef(d =>
            {
                d.Map.SpawnRoutes = new[] { -1, LaneDefaultPath, -1 };
                d.Enemies[Walker].WaypointPathIndex = -1;
                d.Enemies[Flyer].WaypointPathIndex = FlyerPath;       // 강을 건너는 비행 적의 저작 경로
                d.Enemies[Flyer].TraversalLayers = LayerBits.Path | LayerBits.Air;
                d.Mode.WaveSource = WaveSourceKind.AuthoredPlan;
                d.WavePlan = new WavePlanDef
                {
                    DisplayName = "t",
                    Waves = new[]
                    {
                        new AuthoredWaveDef
                        {
                            DurationSec = 20f,
                            IntervalSec = Spacing,
                            Groups = new[]
                            {
                                new AuthoredGroupDef { EnemyIndex = Walker, Count = 1, TriggerTimeSec = 5f, LaneIndex = 1, PathIndex = -1 },
                                new AuthoredGroupDef { EnemyIndex = Walker, Count = 1, TriggerTimeSec = 5f, LaneIndex = 2, PathIndex = -1 },
                                new AuthoredGroupDef { EnemyIndex = Flyer,  Count = 1, TriggerTimeSec = 6f, LaneIndex = 1, PathIndex = -1 },
                            },
                        },
                    },
                };
            });
            def.ConfigHash = def.ComputeConfigHash();
            var m = CoreMatchFixtures.BeginBattle(def);
            m.Tick();

            var f = Forecast(m);
            Assert.AreEqual(3, f.Count, "입구 1 의 두 종은 길이 달라 따로 선다");
            int flyer = 0, laneDefault = 0, shortest = 0;
            foreach (var e in f)
            {
                if (e.EnemyIndex == Flyer)
                {
                    Assert.AreEqual(1, e.Lane);
                    Assert.AreEqual(FlyerPath, e.PathIndex, "비행 적의 예고선은 레인 기본이 있어도 그 적의 저작 경로를 따른다");
                    flyer++;
                }
                else if (e.Lane == 1)
                {
                    Assert.AreEqual(LaneDefaultPath, e.PathIndex, "저작 없는 적은 레인 기본 경로");
                    laneDefault++;
                }
                else
                {
                    Assert.AreEqual(2, e.Lane);
                    Assert.AreEqual(-1, e.PathIndex, "저작도 레인 기본도 없으면 최단(골 직행)");
                    shortest++;
                }
            }
            Assert.AreEqual((1, 1, 1), (flyer, laneDefault, shortest), "세 단이 한 줄씩");

            // 예고선 = 실제 길: 나온 세 적의 (종 × 경로) 모음이 예고 세 줄의 모음과 같다.
            var forecastPaths = new List<(int, int)>();
            foreach (var e in f) forecastPaths.Add((e.EnemyIndex, e.PathIndex));
            TickUntil(m, 6.5f);
            var actualPaths = new List<(int, int)>();
            foreach (var u in m.World.Units)
                if (u.Kind == UnitKind.Enemy && u.Move != null) actualPaths.Add((u.DefIndex, u.Move.PathIndex));
            CollectionAssert.AreEquivalent(forecastPaths, actualPaths, "예고선이 그린 길 = 적이 실제로 가는 길");
        }

        // ════════════════════════════════════════════════════════════════════
        // 28 · 명목 트리거 간격 (옛 WaveNominalIntervalTests) — 표시 전용
        // ════════════════════════════════════════════════════════════════════

        private static WaveDeckDef NominalDeck(int waves, float maxInterval)
        {
            var d = CoreMatchFixtures.Deck(waveCount: waves, interval: maxInterval);
            d.MinUnitsPerWave = 8;
            d.MaxUnitsPerWave = 8;
            d.IntraWaveSpacingSec = 0.35f;
            return d;
        }

        private static WavePlan Nominal(int waves, float maxInterval, int seed)
        {
            var d = NominalDeck(waves, maxInterval);
            return WaveGenerator.Generate(in d, CoreMatchFixtures.Definition().Enemies, seed, 2);
        }

        // 옛 WaveNominalIntervalTests::MaxInterval_StampsNominalTriggerTimes — 상한 간격 > 0 이면 그리드 = i × 상한
        [Test]
        public void 상한_간격이_있으면_명목_그리드는_i_곱하기_상한이다()
        {
            const int waves = 30;
            const float cap = 10f;
            var plan = Nominal(waves, cap, 1);
            Assert.AreEqual(waves, plan.WaveCount);
            Assert.AreEqual(cap, plan.WaveIntervalSec, Tol, "상한 간격이 플랜에 반영");
            for (int i = 0; i < plan.WaveCount; i++)
                Assert.AreEqual(i * cap, plan.Waves[i].TriggerTimeSec, Tol, $"wave {i}");
        }

        // 옛 WaveNominalIntervalTests::NominalGrid_MayExceedTimerWindow — 명목 그리드는 제한시간을 넘어도 된다
        [Test]
        public void 명목_그리드는_제한시간을_넘어도_된다()
        {
            const int waves = 30;
            const float cap = 10f;
            var plan = Nominal(waves, cap, 7);
            Assert.AreEqual((waves - 1) * cap, plan.Waves[waves - 1].TriggerTimeSec, Tol);
            Assert.Greater(plan.Waves[waves - 1].TriggerTimeSec, plan.TimerDurationSec,
                "런타임이 이 값을 읽지 않으므로 「타이머 밖 웨이브」는 미스폰 사유가 아니다");
        }

        // 옛 WaveNominalIntervalTests::MaxIntervalZero_FallsBackToDurationOverCount — 상한 0 이면 duration / count
        [Test]
        public void 상한_간격이_0이면_명목_그리드는_판_길이_나누기_웨이브_수다()
        {
            const int waves = 10;
            var plan = Nominal(waves, 0f, 1);
            float expected = plan.TimerDurationSec / waves;
            Assert.AreEqual(waves, plan.WaveCount);
            Assert.AreEqual(expected, plan.WaveIntervalSec, Tol);
            for (int i = 0; i < plan.WaveCount; i++)
                Assert.AreEqual(i * expected, plan.Waves[i].TriggerTimeSec, Tol, $"wave {i}");
        }
    }
}
