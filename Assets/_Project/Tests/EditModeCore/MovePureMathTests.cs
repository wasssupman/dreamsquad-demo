// 적응: Assets/_Project/Tests/EditMode/{SpawnSpreadTests, WaypointProgressTests, FlowRecoveryTests,
//       StructureDestinationTests(→ StructureChoice 부분), AggroChaseMathTests}.cs
// (battle-core-rebuild unit 2)
using NUnit.Framework;
using Unity.Mathematics;
using Wassup.Battle.Units;
using Wassup.BattleCore.Map;
using Wassup.BattleCore.Move;

namespace Wassup.Tests.EditMode.Core
{
    public class SpawnSpreadTests
    {
        [Test]
        public void 오프셋은_반_칸을_절대_못_넘는다()
        {
            // M14 — 넘으면 옆 칸을 침범해 칸 환산·골 판정·칸 트림이 다른 칸으로 본다.
            for (int i = 0; i < 64; i++)
            {
                float frac = SpawnSpread.LaneFraction(i, 7, spreadFraction: 10f, topScale: 1f);
                Assert.Less(math.abs(frac), 0.5f, $"index {i}");
            }
            var off = SpawnSpread.LateralOffset(99f, 1f, new float2(1f, 0f));
            Assert.Less(math.length(new float2(off.x, off.z)), 0.5f);
        }

        [Test]
        public void 같은_순번이면_같은_레인이다()
        {
            // RNG 없는 이산 N-레인 round-robin — 결정론이 구조에서 나온다.
            Assert.AreEqual(SpawnSpread.LaneFraction(5, 4, 0.4f, 1f),
                            SpawnSpread.LaneFraction(5, 4, 0.4f, 1f));
            Assert.AreEqual(SpawnSpread.LaneFraction(1, 4, 0.4f, 1f),
                            SpawnSpread.LaneFraction(5, 4, 0.4f, 1f), "4 레인이면 1 과 5 가 같다");
        }

        [Test]
        public void 레인이_하나면_중앙이다()
        {
            Assert.AreEqual(0f, SpawnSpread.LaneFraction(3, 1, 0.4f, 1f), 1e-6f);
        }

        [Test]
        public void 음수_순번도_안전하다()
        {
            float f = SpawnSpread.LaneFraction(-3, 4, 0.4f, 1f);
            Assert.Less(math.abs(f), 0.5f);
        }

        [Test]
        public void 오프셋은_진행방향_수직이다()
        {
            var off = SpawnSpread.LateralOffset(0.3f, 1f, new float2(1f, 0f));
            Assert.AreEqual(0f, off.x, 1e-6f);
            Assert.AreNotEqual(0f, off.z);
        }
    }

    public class WaypointProgressTests
    {
        [Test]
        public void 인접_칸이면_지났다()
        {
            // 정확한 칸 일치는 스웜에서 분리가 서로 밀어내 한 칸에 수렴하지 못한다.
            WaypointProgress.Step(new int2(1, 1), new int2(2, 2), true, 0, 3,
                                  out int next, out bool advanced, out bool done);
            Assert.IsTrue(advanced);
            Assert.AreEqual(1, next);
            Assert.IsFalse(done);
        }

        [Test]
        public void 아직_멀면_안_넘어간다()
        {
            WaypointProgress.Step(new int2(0, 0), new int2(5, 5), true, 0, 3,
                                  out int next, out bool advanced, out _);
            Assert.IsFalse(advanced);
            Assert.AreEqual(0, next);
        }

        [Test]
        public void 도달_불가면_건너뛴다()
        {
            // 못 가는 경유점에 갇히지 않는다.
            WaypointProgress.Step(new int2(0, 0), new int2(5, 5), reachable: false, index: 0, count: 2,
                                  out int next, out bool advanced, out bool done);
            Assert.IsTrue(advanced);
            Assert.AreEqual(1, next);
            Assert.IsFalse(done);
        }

        [Test]
        public void 마지막을_지나면_끝난다()
        {
            WaypointProgress.Step(new int2(2, 2), new int2(2, 2), true, 1, 2,
                                  out _, out _, out bool done);
            Assert.IsTrue(done);
        }

        [Test]
        public void 경로_선택은_좁은_쪽이_이긴다()
        {
            // 적 정의 > 웨이브 컨셉 > 레인 기본 > 골 직행.
            Assert.AreEqual(2, WaypointRouting.ResolvePathIndex(2, 1, 0));
            Assert.AreEqual(1, WaypointRouting.ResolvePathIndex(-1, 1, 0));
            Assert.AreEqual(0, WaypointRouting.ResolvePathIndex(-1, -1, 0));
            Assert.AreEqual(-1, WaypointRouting.ResolvePathIndex(-1, -1, -1));
        }
    }

    public class FlowRecoveryTests
    {
        [Test]
        public void 더_가까운_이웃_쪽으로_내려간다()
        {
            var grid = new int2(3, 1);
            var dist = new[] { 20, 10, 0 };
            Assert.AreEqual(new float2(1, 0), FlowRecovery.RecoveryDir(new int2(0, 0), dist, grid));
        }

        [Test]
        public void 더_나은_이웃이_없으면_zero_다()
        {
            var grid = new int2(3, 1);
            var dist = new[] { 0, 10, 20 };
            Assert.AreEqual(float2.zero, FlowRecovery.RecoveryDir(new int2(0, 0), dist, grid));
        }

        [Test]
        public void 자기_칸이_도달_불가여도_탈출한다()
        {
            // 차단 장판이 발밑에 깔린 경우 — 가드를 두면 순찰병이 장애물 안에 영구히 박힌다.
            var grid = new int2(3, 1);
            var dist = new[] { int.MaxValue, 10, 0 };
            Assert.AreEqual(new float2(1, 0), FlowRecovery.RecoveryDir(new int2(0, 0), dist, grid));
        }

        [Test]
        public void 동률에서_직교_순서가_결정론을_준다()
        {
            // (+x, −x, +y, −y) 고정 순서. 먼저 본 쪽이 이긴다.
            var grid = new int2(3, 3);
            var dist = new int[9];
            for (int i = 0; i < 9; i++) dist[i] = 100;
            dist[1 * 3 + 1] = 100;
            dist[1 * 3 + 2] = 10;   // +x
            dist[2 * 3 + 1] = 10;   // +y
            Assert.AreEqual(new float2(1, 0), FlowRecovery.RecoveryDir(new int2(1, 1), dist, grid));
        }
    }

    public class StructureChoiceTests
    {
        [Test]
        public void 칸_사전순이_동률을_가른다()
        {
            // M18 — 전투 코어와 예고선이 이 기준을 **공유한다**.
            Assert.IsTrue(StructureChoice.IsBefore(new int2(1, 5), new int2(2, 0)));
            Assert.IsTrue(StructureChoice.IsBefore(new int2(2, 0), new int2(2, 1)));
            Assert.IsFalse(StructureChoice.IsBefore(new int2(2, 1), new int2(2, 1)));
        }

        [Test]
        public void 팰_수_있는_것_중_최근접을_고른다()
        {
            var pos = new[] { new float2(10f, 0f), new float2(2f, 0f), new float2(1f, 0f) };
            var fac = new[]
            {
                (int)Faction.DefenderCore,
                (int)Faction.DefenderInstinct,
                (int)Faction.EnemyInstinct,   // 못 때리는 진영
            };
            int pick = StructureChoice.NearestIndex(float2.zero, pos, fac, 3,
                                                    Factions.AnyDefender);
            Assert.AreEqual(1, pick, "가장 가까운 «팰 수 있는» 거점");
        }

        [Test]
        public void 후보가_없으면_없다고_말한다()
        {
            var pos = new[] { new float2(1f, 0f) };
            var fac = new[] { (int)Faction.EnemyCore };
            Assert.AreEqual(-1, StructureChoice.NearestIndex(float2.zero, pos, fac, 1,
                                                             Factions.AnyDefender));
        }
    }

    public class AggroChaseMathTests
    {
        [Test]
        public void 공격_수단이_없으면_거부한다()
        {
            // 구 「공격 수단 없으면 추격 고착」의 원천 차단.
            Assert.AreEqual(AggroChaseMath.NoAttack,
                            AggroChaseMath.ResolveTileRange(false, 0f, false, 0f));
            Assert.AreEqual(3, AggroChaseMath.ResolveTileRange(true, 2.4f, false, 0f));
            Assert.AreEqual(2, AggroChaseMath.ResolveTileRange(false, 0f, true, 1.2f));
        }

        [Test]
        public void 접근_보정은_지배축_cardinal_이다()
        {
            // 대각을 쓰지 않는 이유는 순찰 보정과 같다 — 8-이웃 성분이 코너 슬립에 걸린다.
            AggroChaseMath.CloseInCardinals(3f, 1f, out var primary, out var secondary);
            Assert.AreEqual(new float2(1f, 0f), primary);
            Assert.AreEqual(new float2(0f, 1f), secondary);

            AggroChaseMath.CloseInCardinals(-1f, -4f, out primary, out secondary);
            Assert.AreEqual(new float2(0f, -1f), primary);
            Assert.AreEqual(new float2(-1f, 0f), secondary);
        }

        [Test]
        public void 추격판은_사격_칸을_소스로_굽는다()
        {
            var walk = new byte[25];
            for (int i = 0; i < 25; i++) walk[i] = 1;
            var grid = new int2(5, 5);
            var flow = new float2[25];
            var dist = new int[25];
            var sources = new int2[FlowFieldBuilder.DiscArea(1)];

            int n = AggroChaseMath.BuildChaseField(walk, grid, new int2(2, 2), 1,
                                                   sources, flow, dist, new CellQueue());

            Assert.AreEqual(8, n);
            Assert.AreEqual(0, dist[GridMath.CellIndex(new int2(2, 1), grid)], "사격 칸은 거리 0");
            Assert.Greater(dist[GridMath.CellIndex(new int2(0, 0), grid)], 0);
        }

        [Test]
        public void 소스가_하나도_안_서면_도달_불가로_채운다()
        {
            var walk = new byte[9];   // 전부 벽
            var grid = new int2(3, 3);
            var flow = new float2[9];
            var dist = new int[9];
            var sources = new int2[FlowFieldBuilder.DiscArea(1)];

            int n = AggroChaseMath.BuildChaseField(walk, grid, new int2(1, 1), 1,
                                                   sources, flow, dist, new CellQueue());
            Assert.AreEqual(0, n);
            for (int i = 0; i < 9; i++) Assert.AreEqual(int.MaxValue, dist[i]);
        }
    }
}
