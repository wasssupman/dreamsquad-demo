using NUnit.Framework;
using Unity.Collections;
using Unity.Mathematics;
using Wassup.Battle.Effects;
using Wassup.Battle.Movement;

namespace Wassup.Tests.EditMode
{
    // enemy-tile-movement-integrity unit 2 — 공유 cell-trim Apply 회귀.
    // 4x2 맵: row0 (x=0..2 walk=+X, x=3 goal=zero), row1 전부 zero-flow(벽). tile=1, origin=0.
    public class MovementCellTrimApplyTests
    {
        private const float Eps = 1e-3f;

        // continuous-agent-movement unit 2 — 픽스처의 의도("row1 전부 벽")는 그대로이고
        // 그 의도를 표현하는 수단만 flow=0 → walkMask=0 으로 바뀐다. 기대값은 무변경.
        private static FlowFieldSingleton MakeField(
            out NativeArray<float2> flow, out NativeArray<int> dist, out NativeArray<byte> walk)
        {
            int w = 4, h = 2;
            flow = new NativeArray<float2>(w * h, Allocator.Temp);
            dist = new NativeArray<int>(w * h, Allocator.Temp);
            walk = new NativeArray<byte>(w * h, Allocator.Temp);
            flow[0] = new float2(1, 0); flow[1] = new float2(1, 0); flow[2] = new float2(1, 0); flow[3] = float2.zero;
            for (int i = 4; i < 8; i++) flow[i] = float2.zero;
            for (int i = 0; i < 4; i++) walk[i] = 1;            // row0 = walk (골 포함)
            for (int i = 4; i < 8; i++) walk[i] = 0;            // row1 = walls
            var f = new FlowFieldSingleton();
            f.flow = flow; f.dist = dist; f.walkMask = walk;
            f.gridSize = new int2(w, h); f.goalCell = new int2(3, 0);
            f.tileSize = 1f; f.origin = float3.zero; f.version = 0;
            return f;
        }

        [Test]
        public void SameCell_Unchanged()
        {
            NativeArray<float2> flow; NativeArray<int> dist; NativeArray<byte> walk; var field = MakeField(out flow, out dist, out walk);
            var d = new float3(1.2f, 0f, 0.1f); // stays in cell (1,0)
            var r = MovementCellTrim.Apply(d, new int2(1, 0), in field, false, new ObstacleSingleton());
            Assert.AreEqual(d.x, r.x, Eps); Assert.AreEqual(d.z, r.z, Eps);
            flow.Dispose(); dist.Dispose(); walk.Dispose();
        }

        [Test]
        public void IntoWalkCell_Allowed()
        {
            NativeArray<float2> flow; NativeArray<int> dist; NativeArray<byte> walk; var field = MakeField(out flow, out dist, out walk);
            var d = new float3(2.1f, 0f, 0f); // (1,0) -> (2,0) walk
            var r = MovementCellTrim.Apply(d, new int2(1, 0), in field, false, new ObstacleSingleton());
            Assert.AreEqual(2.1f, r.x, Eps); // 통과(불변)
            flow.Dispose(); dist.Dispose(); walk.Dispose();
        }

        [Test]
        public void IntoWallCell_ClampedToCurrentCell()
        {
            NativeArray<float2> flow; NativeArray<int> dist; NativeArray<byte> walk; var field = MakeField(out flow, out dist, out walk);
            var d = new float3(1.0f, 0f, 0.7f); // (1,0) -> (1,1) zero-flow 벽
            var r = MovementCellTrim.Apply(d, new int2(1, 0), in field, false, new ObstacleSingleton());
            Assert.Less(r.z, 0.5f); // (1,0) 안으로 clamp
            flow.Dispose(); dist.Dispose(); walk.Dispose();
        }

        // (참고) WorldToCell 은 셀을 grid 경계로 clamp → 경계 밖 위치는 가장자리 셀로 매핑된다.
        // 즉 Apply 는 OOB targetCell 을 보지 않으며, 경계 containment 는 가장자리 행이 wall 일 때
        // IntoWallCell 경로로 처리된다(실제 맵은 walk 경로가 grid 가장자리에 닿지 않음).

        [Test]
        public void ObstacleCell_Clamped()
        {
            NativeArray<float2> flow; NativeArray<int> dist; NativeArray<byte> walk; var field = MakeField(out flow, out dist, out walk);
            var obs = new ObstacleSingleton();
            obs.blockedCells = new NativeHashSet<int2>(4, Allocator.Temp);
            obs.blockedCells.Add(new int2(2, 0)); // walk 셀이지만 obstacle 로 차단
            var d = new float3(2.1f, 0f, 0f);      // (2,0) 진입 시도
            var r = MovementCellTrim.Apply(d, new int2(1, 0), in field, true, obs);
            Assert.Less(r.x, 2.0f); // (1,0) 안으로 clamp
            obs.blockedCells.Dispose(); flow.Dispose(); dist.Dispose(); walk.Dispose();
        }
    }
}
