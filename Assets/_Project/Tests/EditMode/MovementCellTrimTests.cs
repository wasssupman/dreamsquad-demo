using NUnit.Framework;
using Unity.Collections;
using Unity.Mathematics;
using Wassup.Battle.Effects;
using Wassup.Battle.Movement;

namespace Wassup.Tests.EditMode
{
    public class MovementCellTrimTests
    {
        // 3x1 grid: 전 셀 Walk 타일. (2,0) 은 골이며 골도 Walk 타일이다.
        // continuous-agent-movement unit 2 — 벽은 flow 가 아니라 walkMask 가 정한다.
        // (flow/dist 는 다른 소비자용으로 유지 — 벽 판정엔 더 이상 쓰이지 않는다.)
        private FlowFieldSingleton CreateField()
        {
            var flow = new NativeArray<float2>(3, Allocator.Temp);
            var dist = new NativeArray<int>(3, Allocator.Temp);
            var walk = new NativeArray<byte>(3, Allocator.Temp);
            flow[0] = new float2(1, 0); dist[0] = 2; walk[0] = 1;
            flow[1] = new float2(1, 0); dist[1] = 1; walk[1] = 1;
            flow[2] = float2.zero;      dist[2] = 0; walk[2] = 1;   // 골 = Walk 타일
            return new FlowFieldSingleton
            {
                flow = flow, dist = dist, walkMask = walk,
                gridSize = new int2(3, 1),
                goalCell = new int2(2, 0),
                tileSize = 1f, version = 1,
            };
        }

        // 벽 술어는 NavGrid 가 소유한다. 여기서는 어댑터(BuildNavGrid) 경유로 검증해
        // ECS 싱글턴 → NavGrid 조립까지 함께 커버한다.
        private static NavGrid Nav(in FlowFieldSingleton field)
            => MovementCellTrim.BuildNavGrid(in field, hasObstacles: false, default);

        [Test]
        public void IsBlocked_True_For_OOB_Cell()
        {
            var field = CreateField();
            var nav = Nav(in field);
            Assert.IsTrue(nav.IsBlocked(new int2(-1, 0)));
            Assert.IsTrue(nav.IsBlocked(new int2(3, 0)));
            Assert.IsTrue(nav.IsBlocked(new int2(0, 1)));
        }

        [Test]
        public void IsBlocked_False_For_Goal_Cell()
        {
            // 골은 flow=0 이지만 Walk 타일이라 통행 가능하다. unit 1 까지는 명시적 골 예외가
            // 이 결과를 만들었고, unit 2 부터는 마스크가 이미 1 이라 예외 자체가 불필요하다.
            var field = CreateField();
            Assert.IsFalse(Nav(in field).IsBlocked(new int2(2, 0)));
        }

        [Test]
        public void IsBlocked_False_For_Normal_Cell()
        {
            var field = CreateField();
            var nav = Nav(in field);
            Assert.IsFalse(nav.IsBlocked(new int2(0, 0)));
            Assert.IsFalse(nav.IsBlocked(new int2(1, 0)));
        }

        [Test]
        public void IsBlocked_True_For_NonWalk_Tile()
        {
            var flow = new NativeArray<float2>(2, Allocator.Temp);
            var dist = new NativeArray<int>(2, Allocator.Temp);
            var walk = new NativeArray<byte>(2, Allocator.Temp);
            flow[0] = float2.zero; dist[0] = int.MaxValue; walk[0] = 0;  // 비-Walk 타일
            flow[1] = float2.zero; dist[1] = 0;            walk[1] = 1;  // 골
            var field = new FlowFieldSingleton
            {
                flow = flow, dist = dist, walkMask = walk,
                gridSize = new int2(2, 1),
                goalCell = new int2(1, 0),
                tileSize = 1f, version = 1,
            };
            Assert.IsTrue(Nav(in field).IsBlocked(new int2(0, 0)));
        }

        [Test]
        public void IsolatedWalkCell_IsNotWall_AfterPredicateSwap()
        {
            // unit 2 의 유일한 의미 변화(구 IsWallCell_True_For_Zero_Flow_Non_Goal 대체).
            // 골에서 도달 불가한 Walk 셀은 flow=0 이라 이전 술어에선 벽이었지만, 지형상
            // 걸을 수 있는 칸이다. 이 성질이 없으면 D1-b 에서 봉쇄로 끊긴 구역 전체가 벽이
            // 되어 그 안의 적이 자기가 선 칸을 벽으로 인식한다.
            var flow = new NativeArray<float2>(2, Allocator.Temp);
            var dist = new NativeArray<int>(2, Allocator.Temp);
            var walk = new NativeArray<byte>(2, Allocator.Temp);
            flow[0] = float2.zero; dist[0] = int.MaxValue; walk[0] = 1;  // 고립됐지만 Walk 타일
            flow[1] = float2.zero; dist[1] = 0;            walk[1] = 1;
            var field = new FlowFieldSingleton
            {
                flow = flow, dist = dist, walkMask = walk,
                gridSize = new int2(2, 1),
                goalCell = new int2(1, 0),
                tileSize = 1f, version = 1,
            };
            Assert.IsFalse(Nav(in field).IsBlocked(new int2(0, 0)),
                "도달 가능성이 아니라 지형이 벽을 정한다");
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
            var obstacles = new ObstacleSingleton { blockedCells = blocked };

            // 지형은 통행 가능인데 장애물이 덮은 셀 — 두 층의 합성이 막는다.
            Assert.IsFalse(Nav(in field).IsBlocked(new int2(1, 0)), "지형만 보면 통행 가능");
            Assert.IsTrue(
                MovementCellTrim.BuildNavGrid(in field, hasObstacles: true, in obstacles)
                    .IsBlocked(new int2(1, 0)),
                "장애물 오버레이가 막는다");
        }

        [Test]
        public void Goal_Cell_Blocked_When_Covered_By_Obstacle()
        {
            // 골은 지형상 통행 가능이지만 장애물이 덮으면 막힌다.
            // 의미 변화 표(2_wall_predicate_swap.md) 3행: 골 셀 = 통행(이전 명시 예외 → 이후
            // 마스크가 이미 1). 즉 이 케이스는 **기대값이 바뀌지 않은** 항목이다.
            // 이는 unit 2 이전과 동일한 거동이다 — 구 Apply 도 `IsWallCell(goal)=false` 뒤에
            // 장애물 검사를 따로 돌려 막았다. (실제로 EffectSpawner 가 골 셀 차단 해저드
            // 배치를 거부하므로 프로덕션에선 발생하지 않는 방어적 계약이다.)
            var field = CreateField();
            using var blocked = new NativeHashSet<int2>(4, Allocator.Temp);
            blocked.Add(new int2(2, 0));
            var obstacles = new ObstacleSingleton { blockedCells = blocked };

            Assert.IsFalse(Nav(in field).IsBlocked(new int2(2, 0)), "장애물 없으면 통행 가능");
            Assert.IsTrue(
                MovementCellTrim.BuildNavGrid(in field, hasObstacles: true, in obstacles)
                    .IsBlocked(new int2(2, 0)));
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
