// battle-sim-extraction unit 18-E/2 — 공간 토대의 **오라클 복제 + 이식 핀**.
//
// 복제(어서션 동일): `GridMathTests`(13) · `FlowRecoveryTests`(4).
// 이식 핀(구 오라클은 `MovementCellTrimTests`·`MovementCellTrimApplyTests`·
// `LateralRecenterTests`·`FillWalkMaskTests`·`FlowFieldBuilderTests` 가 계속 진다 —
// unit 20 스왑 때 그 다섯과 함께 지운다): 트림 불변식 · 코너 복원 · BFS 타이브레이크.
using System.Collections.Generic;
using NUnit.Framework;
using Wassup.Sim;
using Wassup.Sim.Effects;
using Wassup.Sim.Movement;

namespace Wassup.Tests.EditMode
{
    public class SimGridMathTests
    {
        [Test]
        public void WorldToCell_Origin_ReturnsZero()
            => Assert.AreEqual(new SimInt2(0, 0),
                GridMath.WorldToCell(new SimVec3(0, 0, 0), 1f, new SimInt2(20, 10)));

        [Test]
        public void WorldToCell_ExactCellCenter_ReturnsCell()
            => Assert.AreEqual(new SimInt2(5, 3),
                GridMath.WorldToCell(new SimVec3(5, 0, 3), 1f, new SimInt2(20, 10)));

        [Test]
        public void WorldToCell_Rounds_NotFloors()
            // 0.6 은 1 로 올라간다(floor 면 0). `math.round` 의 짝수 반올림도 아니다.
            => Assert.AreEqual(new SimInt2(1, 0),
                GridMath.WorldToCell(new SimVec3(0.6f, 0, 0.4f), 1f, new SimInt2(20, 10)));

        [Test]
        public void WorldToCell_HalfSnapsUp_NotToEven()
        {
            // 2.5 → 3. `math.round`(banker's)면 2 가 되어 셀이 하나 밀린다.
            Assert.AreEqual(new SimInt2(3, 3),
                GridMath.WorldToCell(new SimVec3(2.5f, 0, 2.5f), 1f, new SimInt2(20, 10)));
        }

        [Test]
        public void WorldToCell_OutOfBounds_ClampsToEdge()
        {
            Assert.AreEqual(new SimInt2(19, 9),
                GridMath.WorldToCell(new SimVec3(100, 0, 100), 1f, new SimInt2(20, 10)));
            Assert.AreEqual(new SimInt2(0, 0),
                GridMath.WorldToCell(new SimVec3(-10, 0, -10), 1f, new SimInt2(20, 10)));
        }

        [Test]
        public void WorldToCellUnclamped_OutOfBounds_KeepsCellOutside()
        {
            Assert.AreEqual(new SimInt2(100, 100),
                GridMath.WorldToCellUnclamped(new SimVec3(100, 0, 100), 1f));
            Assert.AreEqual(new SimInt2(-10, -10),
                GridMath.WorldToCellUnclamped(new SimVec3(-10, 0, -10), 1f));
        }

        [Test]
        public void WorldToCell_IsUnclampedThenClamped_SameRounding()
        {
            var grid = new SimInt2(20, 10);
            var origin = new SimVec3(3f, 0f, -2f);
            foreach (var p in new[] { new SimVec3(0.6f, 0, 0.4f), new SimVec3(7.5f, 0, 2.5f), new SimVec3(-4f, 0, 30f) })
            {
                var raw = GridMath.WorldToCellUnclamped(p, 2f, origin);
                var clamped = GridMath.WorldToCell(p, 2f, grid, origin);
                Assert.AreEqual(SimMath.Clamp(raw.x, 0, grid.x - 1), clamped.x, $"x @ {p}");
                Assert.AreEqual(SimMath.Clamp(raw.y, 0, grid.y - 1), clamped.y, $"y @ {p}");
            }
        }

        [Test]
        public void WorldToCell_DifferentTileSize_Scales()
            => Assert.AreEqual(new SimInt2(5, 3),
                GridMath.WorldToCell(new SimVec3(10, 0, 5), 2f, new SimInt2(20, 10)));

        [Test]
        public void CellToWorldCenter_MatchesWorldToCellInverse()
        {
            var w = GridMath.CellToWorldCenter(new SimInt2(7, 4), 1f);
            Assert.AreEqual(7f, w.x);
            Assert.AreEqual(0f, w.y);
            Assert.AreEqual(4f, w.z);
        }

        [Test]
        public void WorldToCell_DefaultOrigin_IdenticalToExplicitZero()
        {
            var a = GridMath.WorldToCell(new SimVec3(5, 0, 3), 1f, new SimInt2(20, 10));
            var b = GridMath.WorldToCell(new SimVec3(5, 0, 3), 1f, new SimInt2(20, 10), SimVec3.Zero);
            Assert.AreEqual(a, b);
        }

        [Test]
        public void WorldToCell_NonZeroOrigin_SubtractsBeforeCellifying()
            => Assert.AreEqual(new SimInt2(5, 3),
                GridMath.WorldToCell(new SimVec3(15, 0, 8), 1f, new SimInt2(20, 10), new SimVec3(10, 0, 5)));

        [Test]
        public void CellToWorldCenter_NonZeroOrigin_AddsOrigin()
        {
            var w = GridMath.CellToWorldCenter(new SimInt2(7, 4), 1f, 0f, new SimVec3(10, 0, 5));
            Assert.AreEqual(17f, w.x);
            Assert.AreEqual(0f, w.y);
            Assert.AreEqual(9f, w.z);
        }

        [Test]
        public void RoundTrip_NonZeroOrigin_PreservesCell()
        {
            var origin = new SimVec3(12.5f, 0, -3.5f);
            var grid = new SimInt2(20, 10);
            for (int x = 0; x < 8; x++)
            for (int z = 0; z < 6; z++)
            {
                var cell = new SimInt2(x, z);
                var world = GridMath.CellToWorldCenter(cell, 2f, 0f, origin);
                Assert.AreEqual(cell, GridMath.WorldToCell(world, 2f, grid, origin),
                    $"round-trip failed for cell ({x},{z})");
            }
        }

        [Test]
        public void ChebyshevDistance_DiagonalIsOne()
        {
            Assert.AreEqual(1, GridMath.ChebyshevDistance(new SimInt2(0, 0), new SimInt2(1, 1)));
            Assert.AreEqual(3, GridMath.ChebyshevDistance(new SimInt2(0, 0), new SimInt2(3, 2)));
            Assert.AreEqual(2, GridMath.ChebyshevDistance(new SimInt2(1, 1), new SimInt2(-1, 0)));
        }

        [Test]
        public void RangeToTiles_IsHalfAwayFromZero()
        {
            Assert.AreEqual(1, GridMath.RangeToTiles(1f));
            Assert.AreEqual(2, GridMath.RangeToTiles(1.5f), "1.5 → 2 (짝수 반올림이면 2 지만 2.5 에서 갈린다)");
            Assert.AreEqual(3, GridMath.RangeToTiles(2.5f), "2.5 → 3 — banker's 라면 2 다");
            Assert.AreEqual(0, GridMath.RangeToTiles(0.4f));
        }

        [Test]
        public void Perpendicular_ZeroInput_FallsBackToXAxis()
        {
            var p = SpawnSpread.Perpendicular(SimVec2.Zero);
            // fallback (1,0) 의 수직 = (0,1)
            Assert.AreEqual(0f, p.x, 1e-6f);
            Assert.AreEqual(1f, p.y, 1e-6f);
        }
    }

    public class SimFlowRecoveryTests
    {
        [Test]
        public void RecoveryDir_PicksSmallestDistNeighbor()
        {
            // 3x1: dist [2, 9, 5]. 가운데(9)에서 좌(2) < 우(5) → -x.
            var dist = new[] { 2, 9, 5 };
            Assert.AreEqual(new SimVec2(-1, 0),
                FlowRecovery.RecoveryDir(new SimInt2(1, 0), dist, new SimInt2(3, 1)));
        }

        [Test]
        public void RecoveryDir_NoBetterNeighbor_ReturnsZero()
        {
            var grid = new SimInt2(3, 1);
            var iso = new[] { int.MaxValue, int.MaxValue, int.MaxValue };
            Assert.AreEqual(SimVec2.Zero, FlowRecovery.RecoveryDir(new SimInt2(1, 0), iso, grid),
                "all-MAX isolated");

            var atMin = new[] { 5, 0, 5 };
            Assert.AreEqual(SimVec2.Zero, FlowRecovery.RecoveryDir(new SimInt2(1, 0), atMin, grid),
                "already at minimum");
        }

        [Test]
        public void RecoveryDir_CornerCell_NoOutOfBounds()
        {
            // 2x2 코너(0,0): 이웃은 (1,0),(0,1) 뿐 — OOB 접근 없이 최소 선택.
            var dist = new[] { 7, 3, 5, int.MaxValue };
            Assert.AreEqual(new SimVec2(1, 0),
                FlowRecovery.RecoveryDir(new SimInt2(0, 0), dist, new SimInt2(2, 2)));
        }

        [Test]
        public void RecoveryDir_DistArraySwap_ChangesDirection()
        {
            // 사냥 분기 계약: 같은 셀이라도 goal dist ↔ defender dist 에 따라 방향이 바뀐다.
            var grid = new SimInt2(3, 1);
            var goalDist = new[] { 9, 5, 1 };
            var huntDist = new[] { 1, 5, 9 };
            Assert.AreEqual(new SimVec2(1, 0),
                FlowRecovery.RecoveryDir(new SimInt2(1, 0), goalDist, grid), "goal field → +x");
            Assert.AreEqual(new SimVec2(-1, 0),
                FlowRecovery.RecoveryDir(new SimInt2(1, 0), huntDist, grid), "defender field → -x");
        }
    }

    public class SimFlowFieldBuilderTests
    {
        private static FlowFieldSingleton OpenField(int w, int h, params SimInt2[] goals)
        {
            int n = w * h;
            var f = new FlowFieldSingleton
            {
                flow = new SimVec2[n],
                dist = new int[n],
                gridSize = new SimInt2(w, h),
                tileSize = 1f,
                origin = SimVec3.Zero,
                goals = goals.Length > 0 ? goals : null,
                goalCell = goals.Length > 0 ? goals[0] : default,
            };
            var mask = new byte[n];
            for (int i = 0; i < n; i++) mask[i] = 1;
            FlowFieldBuilder.BuildFromSources(mask, f.gridSize,
                goals.Length > 0 ? goals : new[] { new SimInt2(0, 0) },
                goals.Length > 0 ? goals.Length : 1, f.flow, f.dist);
            return f;
        }

        [Test]
        public void NoValidSource_ResetsEveryCell_ForGoalFallback()
        {
            int n = 9;
            var mask = new byte[n];
            for (int i = 0; i < n; i++) mask[i] = 1;
            var flow = new SimVec2[n];
            var dist = new int[n];
            for (int i = 0; i < n; i++) dist[i] = 42;

            // 경계 밖 소스 하나 → 유효 소스 0.
            FlowFieldBuilder.BuildFromSources(mask, new SimInt2(3, 3),
                new[] { new SimInt2(99, 99) }, 1, flow, dist);

            for (int i = 0; i < n; i++)
            {
                Assert.AreEqual(int.MaxValue, dist[i], $"cell {i}");
                Assert.AreEqual(SimVec2.Zero, flow[i], $"cell {i}");
            }
        }

        [Test]
        public void BlockedSource_IsSkipped()
        {
            int n = 9;
            var mask = new byte[n];
            for (int i = 0; i < n; i++) mask[i] = 1;
            mask[GridMath.CellIndex(new SimInt2(1, 1), new SimInt2(3, 3))] = 0;   // 소스 셀을 벽으로

            var flow = new SimVec2[n];
            var dist = new int[n];
            FlowFieldBuilder.BuildFromSources(mask, new SimInt2(3, 3),
                new[] { new SimInt2(1, 1) }, 1, flow, dist);

            Assert.AreEqual(int.MaxValue, dist[0], "벽 소스는 무효 — 필드가 비어야 한다.");
        }

        [Test]
        public void Bfs_SpreadsFourNeighbors_WithManhattanDistances()
        {
            var f = OpenField(5, 5, new SimInt2(2, 2));
            var g = f.gridSize;
            Assert.AreEqual(0, f.dist[GridMath.CellIndex(new SimInt2(2, 2), g)]);
            Assert.AreEqual(1, f.dist[GridMath.CellIndex(new SimInt2(3, 2), g)]);
            Assert.AreEqual(2, f.dist[GridMath.CellIndex(new SimInt2(4, 2), g)]);
            Assert.AreEqual(2, f.dist[GridMath.CellIndex(new SimInt2(3, 3), g)],
                "4-이웃 BFS — 대각선은 2 다(체비셰프 1 이 아니다)");
        }

        [Test]
        public void Flow_TieBreak_PrefersPlusXThenMinusXThenPlusY()
        {
            // (1,1) 에서 +x(2,1) 와 +y(1,2) 의 dist 가 같게 만든다. 순서 (+x,-x,+y,-y) + strict `<`
            // 라서 **먼저 검사된 +x** 가 이긴다. 이 순서가 경로 결정론의 계약이다.
            var g = new SimInt2(3, 3);
            int n = 9;
            var mask = new byte[n];
            for (int i = 0; i < n; i++) mask[i] = 1;
            var flow = new SimVec2[n];
            var dist = new int[n];
            // 두 소스: (2,1) 과 (1,2) — (1,1) 에서 둘 다 dist 1.
            FlowFieldBuilder.BuildFromSources(mask, g,
                new[] { new SimInt2(2, 1), new SimInt2(1, 2) }, 2, flow, dist);

            Assert.AreEqual(1, dist[GridMath.CellIndex(new SimInt2(1, 1), g)]);
            Assert.AreEqual(new SimVec2(1, 0), flow[GridMath.CellIndex(new SimInt2(1, 1), g)],
                "동률이면 +x 가 이긴다 — Dir 순서를 바꾸면 경로가 달라진다.");
        }

        [Test]
        public void SourceCells_HaveZeroFlow()
        {
            var f = OpenField(3, 3, new SimInt2(1, 1));
            Assert.AreEqual(SimVec2.Zero, f.flow[GridMath.CellIndex(new SimInt2(1, 1), f.gridSize)],
                "소스는 flow 0 — 그래서 `IsWallCell` 이 골을 예외로 빼야 한다.");
        }

        [Test]
        public void MismatchedArrayLengths_Throw_RatherThanCorruptSilently()
        {
            Assert.Throws<System.ArgumentException>(() =>
                FlowFieldBuilder.BuildFromSources(new byte[4], new SimInt2(3, 3),
                    new[] { new SimInt2(0, 0) }, 1, new SimVec2[9], new int[9]));
        }

        [Test]
        public void CollectDefenderSources_IsChebyshevDisc_ExcludingOwnCell()
        {
            var g = new SimInt2(7, 7);
            int n = 49;
            var mask = new byte[n];
            for (int i = 0; i < n; i++) mask[i] = 1;
            var outSources = new List<SimInt2>();

            int count = FlowFieldBuilder.CollectDefenderSources(
                mask, g, new[] { new SimInt2(3, 3) }, 1, rangeTiles: 1, outSources);

            Assert.AreEqual(8, count, "3×3 디스크에서 자기 셀 제외 = 8");
            Assert.IsFalse(outSources.Contains(new SimInt2(3, 3)), "자기 셀은 Place=벽이라 제외.");
            Assert.IsTrue(outSources.Contains(new SimInt2(4, 4)), "대각선도 포함(체비셰프).");
        }

        [Test]
        public void CollectDefenderSources_SkipsBlockedAndOutOfBounds()
        {
            var g = new SimInt2(3, 3);
            int n = 9;
            var mask = new byte[n];
            for (int i = 0; i < n; i++) mask[i] = 1;
            mask[GridMath.CellIndex(new SimInt2(1, 0), g)] = 0;
            var outSources = new List<SimInt2>();

            // 코너 (0,0) 기준 range 1 → 이웃 3개((1,0)·(0,1)·(1,1)) 중 (1,0) 은 벽.
            int count = FlowFieldBuilder.CollectDefenderSources(
                mask, g, new[] { new SimInt2(0, 0) }, 1, rangeTiles: 1, outSources);

            Assert.AreEqual(2, count);
            Assert.IsFalse(outSources.Contains(new SimInt2(1, 0)), "벽 셀은 소스가 아니다.");
        }
    }

    public class SimMovementCellTrimTests
    {
        private FlowFieldSingleton _field;
        private ObstacleSingleton _obstacles;

        [SetUp]
        public void SetUp()
        {
            var g = new SimInt2(5, 5);
            int n = 25;
            _field = new FlowFieldSingleton
            {
                flow = new SimVec2[n], dist = new int[n],
                gridSize = g, tileSize = 1f, origin = SimVec3.Zero,
                goalCell = new SimInt2(4, 4),
            };
            var mask = new byte[n];
            for (int i = 0; i < n; i++) mask[i] = 1;
            FlowFieldBuilder.BuildFromSources(mask, g, new[] { new SimInt2(4, 4) }, 1,
                _field.flow, _field.dist);
            _obstacles = new ObstacleSingleton { blockedCells = new HashSet<SimInt2>() };
        }

        [Test]
        public void IsWallCell_OutOfBounds_IsWall()
        {
            Assert.IsTrue(MovementCellTrim.IsWallCell(new SimInt2(-1, 0), in _field));
            Assert.IsTrue(MovementCellTrim.IsWallCell(new SimInt2(5, 0), in _field));
        }

        [Test]
        public void IsWallCell_GoalIsExempt_EvenThoughItsFlowIsZero()
        {
            // 골은 flow 0 이라 zero-flow=wall 규칙에 걸린다. 예외로 빼지 않으면 적이 골 밖으로
            // clamp 돼 **누수가 영영 안 난다**.
            var goal = new SimInt2(4, 4);
            Assert.AreEqual(SimVec2.Zero, _field.flow[GridMath.CellIndex(goal, _field.gridSize)]);
            Assert.IsFalse(MovementCellTrim.IsWallCell(goal, in _field), "골은 벽 예외.");
        }

        [Test]
        public void Apply_SameCell_PassesThrough()
        {
            var cur = new SimVec3(1.0f, 0, 1.0f);
            var desired = new SimVec3(1.2f, 0, 1.1f);
            var cell = GridMath.WorldToCell(cur, 1f, _field.gridSize);
            Assert.AreEqual(desired, MovementCellTrim.Apply(desired, cell, in _field, false, in _obstacles));
        }

        [Test]
        public void Apply_IntoObstacle_ClampsInsideCurrentCell()
        {
            _obstacles.blockedCells.Add(new SimInt2(2, 1));
            var cell = new SimInt2(1, 1);
            var desired = new SimVec3(2.0f, 0, 1.0f);   // 인접 막힌 셀 중심으로

            var trimmed = MovementCellTrim.Apply(desired, cell, in _field, true, in _obstacles);

            Assert.AreEqual(cell, GridMath.WorldToCell(trimmed, 1f, _field.gridSize),
                "트림 불변식 — clamp 후에도 현재 셀 안이어야 한다(경계 epsilon 이 그것을 보장).");
            Assert.Less(trimmed.x, 1.5f);
        }

        [Test]
        public void ClampToBoundary_StaysStrictlyInsideTheCell()
        {
            // 정확히 +0.5 인 위치는 반올림으로 인접 셀에 매핑된다 — epsilon 이 없으면 다음 프레임에
            // 트림 불변식(current != target)이 깨진다.
            var clamped = MovementCellTrim.ClampToBoundary(new SimVec3(99f, 0, 99f), new SimInt2(2, 2), 1f);
            Assert.AreEqual(new SimInt2(2, 2), GridMath.WorldToCell(clamped, 1f, new SimInt2(5, 5)));
            Assert.Less(clamped.x, 2.5f);
        }

        [Test]
        public void ClampDisplacement_CapsAtNinetyPercentOfTile_ToBlockTunneling()
        {
            var cur = new SimVec3(0, 0, 0);
            var far = new SimVec3(10f, 0, 0);
            var capped = MovementCellTrim.ClampDisplacement(cur, far, 1f);
            Assert.AreEqual(0.9f, capped.x, 1e-5f, "한 프레임 최대 0.9 타일 — Apply 의 인접셀 전제 보호.");

            var near = new SimVec3(0.3f, 0, 0.2f);
            Assert.AreEqual(near, MovementCellTrim.ClampDisplacement(cur, near, 1f), "상한 아래는 그대로.");
        }

        [Test]
        public void FillWalkMask_CombinesWallsAndObstacles()
        {
            _obstacles.blockedCells.Add(new SimInt2(3, 3));
            var mask = new byte[25];
            MovementCellTrim.FillWalkMask(in _field, true, in _obstacles, mask);

            Assert.AreEqual(0, mask[GridMath.CellIndex(new SimInt2(3, 3), _field.gridSize)], "장애물 = 막힘");
            Assert.AreEqual(1, mask[GridMath.CellIndex(new SimInt2(4, 4), _field.gridSize)], "골은 걸을 수 있다");
            Assert.AreEqual(1, mask[GridMath.CellIndex(new SimInt2(1, 1), _field.gridSize)]);
        }
    }

    public class SimLateralRecenterTests
    {
        [Test]
        public void InsideDeadband_NoPull()
        {
            // 중심에서 0.2 타일 오프셋(스폰 분산 범위) — deadband 0.25 안이라 손대지 않는다.
            var cell = new SimInt2(2, 2);
            var cur = new SimVec3(2f, 0, 2.2f);          // +z 로 0.2
            var d = LateralRecenter.Compute(cur, cell, new SimVec2(1, 0), 1f, 0.016f, 1f, SimVec3.Zero);
            Assert.AreEqual(SimVec3.Zero, d, "밴드 안 — 직진 분산 보존.");
        }

        [Test]
        public void OutsideDeadband_PullsInward_ButOnlyToTheBandEdge()
        {
            var cell = new SimInt2(2, 2);
            var cur = new SimVec3(2f, 0, 2.45f);          // +z 로 0.45 (밴드 0.25 밖)
            // rate = 0.4 * speed * dt = 0.4*1*1 = 0.4 이지만 밴드까지 남은 거리는 0.2 → 0.2 만 당긴다.
            var d = LateralRecenter.Compute(cur, cell, new SimVec2(1, 0), 1f, 1f, 1f, SimVec3.Zero);
            Assert.AreEqual(-0.2f, d.z, 1e-5f, "밴드 가장자리까지만 — 0 을 관통하지 않는다.");
            Assert.AreEqual(0f, d.x, 1e-5f, "진행방향 성분은 건드리지 않는다.");
        }

        [Test]
        public void PullIsSpeedProportional_NotConstant()
        {
            var cell = new SimInt2(2, 2);
            var cur = new SimVec3(2f, 0, 3f);             // 크게 벗어남 — rate 가 한계
            var slow = LateralRecenter.Compute(cur, cell, new SimVec2(1, 0), 1f, 0.1f, 1f, SimVec3.Zero);
            var fast = LateralRecenter.Compute(cur, cell, new SimVec2(1, 0), 4f, 0.1f, 1f, SimVec3.Zero);
            Assert.Less(SimMath.Abs(slow.z), SimMath.Abs(fast.z),
                "rate = RateK · speed — 상수 rate 면 빠른 적이 엣지를 거리상 더 오래 탄다.");
        }
    }
}
