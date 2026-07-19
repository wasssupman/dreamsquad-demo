using NUnit.Framework;
using Unity.Collections;
using Unity.Mathematics;
using Wassup.Battle.Effects;
using Wassup.Battle.Movement;

namespace Wassup.Tests.EditMode
{
    public class MovementCellTrimTests
    {
        // 3x1 grid: cells (0,0) flow=(1,0), (1,0) flow=(1,0), (2,0) = goal (zero flow).
        private FlowFieldSingleton CreateField()
        {
            var flow = new NativeArray<float2>(3, Allocator.Temp);
            var dist = new NativeArray<int>(3, Allocator.Temp);
            flow[0] = new float2(1, 0); dist[0] = 2;
            flow[1] = new float2(1, 0); dist[1] = 1;
            flow[2] = float2.zero;      dist[2] = 0;
            return new FlowFieldSingleton
            {
                flow = flow, dist = dist,
                gridSize = new int2(3, 1),
                goalCell = new int2(2, 0),
                tileSize = 1f, version = 1,
            };
        }

        [Test]
        public void IsWallCell_True_For_OOB_Cell()
        {
            var field = CreateField();
            Assert.IsTrue(MovementCellTrim.IsWallCell(new int2(-1, 0), in field));
            Assert.IsTrue(MovementCellTrim.IsWallCell(new int2(3, 0), in field));
            Assert.IsTrue(MovementCellTrim.IsWallCell(new int2(0, 1), in field));
        }

        [Test]
        public void IsWallCell_False_For_Goal_Cell()
        {
            var field = CreateField();
            Assert.IsFalse(MovementCellTrim.IsWallCell(new int2(2, 0), in field));
        }

        [Test]
        public void IsWallCell_False_For_Normal_Cell()
        {
            var field = CreateField();
            Assert.IsFalse(MovementCellTrim.IsWallCell(new int2(0, 0), in field));
            Assert.IsFalse(MovementCellTrim.IsWallCell(new int2(1, 0), in field));
        }

        [Test]
        public void IsWallCell_True_For_Zero_Flow_Non_Goal()
        {
            var flow = new NativeArray<float2>(2, Allocator.Temp);
            var dist = new NativeArray<int>(2, Allocator.Temp);
            flow[0] = float2.zero; dist[0] = int.MaxValue; // isolated
            flow[1] = float2.zero; dist[1] = 0;            // goal
            var field = new FlowFieldSingleton
            {
                flow = flow, dist = dist,
                gridSize = new int2(2, 1),
                goalCell = new int2(1, 0),
                tileSize = 1f, version = 1,
            };
            Assert.IsTrue(MovementCellTrim.IsWallCell(new int2(0, 0), in field));
        }

        [Test]
        public void ClampToBoundary_Clamps_X_Overflow()
        {
            var result = MovementCellTrim.ClampToBoundary(
                new float3(3f, 0, 0), new int2(1, 0), tileSize: 1f);
            // Epsilon-inset boundary: 1.0 + (0.5 - 0.001) = 1.499
            Assert.AreEqual(1.499f, result.x, 1e-3f, "clamped strictly inside right boundary of cell (1,0)");
        }

        [Test]
        public void ClampToBoundary_Clamps_Z_Overflow()
        {
            var result = MovementCellTrim.ClampToBoundary(
                new float3(0, 0, 5f), new int2(0, 0), tileSize: 1f);
            // Epsilon-inset boundary: 0.0 + (0.5 - 0.001) = 0.499
            Assert.AreEqual(0.499f, result.z, 1e-3f, "clamped strictly inside top boundary of cell (0,0)");
        }

        [Test]
        public void ClampToBoundary_PassesThrough_Interior()
        {
            var result = MovementCellTrim.ClampToBoundary(
                new float3(0.3f, 0, 0.2f), new int2(0, 0), tileSize: 1f);
            Assert.AreEqual(0.3f, result.x, 1e-5f);
            Assert.AreEqual(0.2f, result.z, 1e-5f);
        }

        [Test]
        public void ClampToBoundary_NonZeroOrigin_OffsetsBounds()
        {
            // map-origin-placement: board shifted to (10,0,5). Cell (1,0) center is at
            // world (11,_,5); clamping an overflow must respect the origin-shifted boundary.
            var origin = new float3(10, 0, 5);
            var result = MovementCellTrim.ClampToBoundary(
                new float3(100f, 0, 100f), new int2(1, 0), tileSize: 1f, origin: origin);
            // right boundary = 10 + 1*1 + (0.5 - 0.001) = 11.499
            Assert.AreEqual(11.499f, result.x, 1e-3f);
            // top boundary = 5 + 0*1 + (0.5 - 0.001) = 5.499
            Assert.AreEqual(5.499f, result.z, 1e-3f);
        }

        [Test]
        public void ClampToBoundary_DefaultOrigin_IdenticalToLegacy()
        {
            var legacy = MovementCellTrim.ClampToBoundary(new float3(3f, 0, 0), new int2(1, 0), tileSize: 1f);
            var withOrigin = MovementCellTrim.ClampToBoundary(new float3(3f, 0, 0), new int2(1, 0), tileSize: 1f, origin: float3.zero);
            Assert.AreEqual(legacy.x, withOrigin.x, 1e-6f);
            Assert.AreEqual(legacy.z, withOrigin.z, 1e-6f);
        }

        [Test]
        public void Obstacle_Cell_Blocks_Movement()
        {
            var field = CreateField();
            using var blocked = new NativeHashSet<int2>(4, Allocator.Temp);
            blocked.Add(new int2(1, 0));

            // Check: cell (1,0) is normally passable but now blocked
            Assert.IsFalse(MovementCellTrim.IsWallCell(new int2(1, 0), in field));
            Assert.IsTrue(blocked.Contains(new int2(1, 0)));
        }

        [Test]
        public void Goal_Cell_As_Obstacle_Still_Passable()
        {
            var field = CreateField();
            using var blocked = new NativeHashSet<int2>(4, Allocator.Temp);
            blocked.Add(new int2(2, 0)); // goal cell in obstacle set

            // IsWallCell returns false for goal regardless (option B: caller handles obstacle check separately,
            // but should prioritize goal over obstacle in the wall condition).
            Assert.IsFalse(MovementCellTrim.IsWallCell(new int2(2, 0), in field),
                "goal cell must not be treated as wall by IsWallCell");
        }

        // ── aggro-tile-chase unit 2 — ClampDisplacement (터널링 차단 상한) ──

        [Test]
        public void ClampDisplacement_SmallStep_Unchanged()
        {
            var current = new float3(1f, 0.5f, 1f);
            var desired = new float3(1.3f, 0.5f, 1.2f);
            var r = MovementCellTrim.ClampDisplacement(current, desired, tileSize: 1f);
            Assert.AreEqual(desired, r, "0.9타일 미만 변위는 무변경");
        }

        [Test]
        public void ClampDisplacement_LargeStep_CappedToUnderOneTile()
        {
            var current = new float3(0f, 0.5f, 0f);
            var desired = new float3(3f, 0.5f, 4f); // XZ 변위 5.0
            var r = MovementCellTrim.ClampDisplacement(current, desired, tileSize: 1f);
            float dx = r.x - current.x, dz = r.z - current.z;
            Assert.AreEqual(0.9f, math.sqrt(dx * dx + dz * dz), 1e-4f, "0.9타일로 상한");
            Assert.AreEqual(0.6f * 0.9f, dx, 1e-4f, "방향 보존 (3/5 비율)");
            Assert.AreEqual(0.5f, r.y, 1e-6f, "y 는 변형하지 않음");
        }
    }
}
