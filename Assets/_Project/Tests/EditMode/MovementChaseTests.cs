using NUnit.Framework;
using Unity.Collections;
using Unity.Mathematics;
using Wassup.Battle.Effects;
using Wassup.Battle.Movement;

namespace Wassup.Tests.EditMode
{
    // enemy-hunter-targeting unit 2 — MovementChase.SlideStep 축분리 wall-slide 회귀.
    // 검증질문(critic M1): 대각선 clamp 시 x만/z만 슬라이드, 양축 벽이면 진행 0(current)
    // 반환 → softlock 가드(호출측 flow-march 폴백)를 지탱. 결정론.
    public class MovementChaseTests
    {
        private const float Eps = 1e-3f;
        private NativeArray<float2> _flow;
        private NativeArray<int> _dist;

        // 4x4 그리드. walkable = walkableCells 에 든 셀(비-zero flow), 나머지 벽(zero flow).
        // goalCell 은 테스트 셀과 겹치지 않게 (3,0) 고정(IsWallCell 이 goal 은 non-wall 취급).
        private FlowFieldSingleton MakeField(params int2[] walkableCells)
        {
            int w = 4, h = 4;
            _flow = new NativeArray<float2>(w * h, Allocator.Temp);
            _dist = new NativeArray<int>(w * h, Allocator.Temp);
            for (int i = 0; i < w * h; i++) { _flow[i] = float2.zero; _dist[i] = 0; }
            foreach (var c in walkableCells)
                _flow[c.y * w + c.x] = new float2(1, 0); // non-zero → walkable
            return new FlowFieldSingleton
            {
                flow = _flow, dist = _dist, gridSize = new int2(w, h),
                goalCell = new int2(3, 0), tileSize = 1f, origin = float3.zero, version = 0,
            };
        }

        [TearDown]
        public void TearDown()
        {
            if (_flow.IsCreated) _flow.Dispose();
            if (_dist.IsCreated) _dist.Dispose();
        }

        // 보스 (1,1) 셀 중심에서 (3,3) 방향(대각 +x+z)으로 step 1 이동.
        private static readonly float3 Current = new float3(1f, 0.5f, 1f);
        private static readonly float3 Anchor = new float3(3f, 0.5f, 3f);

        private float3 Slide(FlowFieldSingleton field)
            => MovementChase.SlideStep(Current, Anchor, 1f, new int2(1, 1), in field, false, new ObstacleSingleton());

        [Test]
        public void Diagonal_Walkable_MovesDiagonally()
        {
            // (2,2) walkable → 대각선 그대로.
            var moved = Slide(MakeField(new int2(1, 1), new int2(2, 2)));
            Assert.Greater(moved.x, Current.x + Eps, "x 전진");
            Assert.Greater(moved.z, Current.z + Eps, "z 전진");
        }

        [Test]
        public void DiagonalBlocked_XWalkable_SlidesX()
        {
            // (2,2) 벽, (2,1) walkable → x만 슬라이드.
            var moved = Slide(MakeField(new int2(1, 1), new int2(2, 1)));
            Assert.Greater(moved.x, Current.x + Eps, "x 전진(슬라이드)");
            Assert.AreEqual(Current.z, moved.z, Eps, "z 불변");
        }

        [Test]
        public void DiagonalAndX_Blocked_ZWalkable_SlidesZ()
        {
            // (2,2)·(2,1) 벽, (1,2) walkable → z만 슬라이드.
            var moved = Slide(MakeField(new int2(1, 1), new int2(1, 2)));
            Assert.AreEqual(Current.x, moved.x, Eps, "x 불변");
            Assert.Greater(moved.z, Current.z + Eps, "z 전진(슬라이드)");
        }

        [Test]
        public void FullyBoxed_ReturnsCurrent_NoProgress()
        {
            // (2,2)·(2,1)·(1,2) 전부 벽 → 진행 0 = current 반환(softlock 가드 신호).
            var moved = Slide(MakeField(new int2(1, 1)));
            Assert.AreEqual(Current.x, moved.x, Eps);
            Assert.AreEqual(Current.z, moved.z, Eps);
            Assert.LessOrEqual(math.distancesq(moved, Current), 1e-8f, "fully-boxed 는 정확히 current");
        }

        [Test]
        public void Deterministic_SameInput_SameResult()
        {
            var a = Slide(MakeField(new int2(1, 1), new int2(2, 1))); _flow.Dispose(); _dist.Dispose();
            var b = Slide(MakeField(new int2(1, 1), new int2(2, 1)));
            Assert.AreEqual(a.x, b.x, 1e-6f);
            Assert.AreEqual(a.z, b.z, 1e-6f);
        }

        [Test]
        public void AtAnchor_NoMovement()
        {
            // dist≈0 (앵커 도달) → current 반환(이동 없음).
            var field = MakeField(new int2(1, 1));
            var moved = MovementChase.SlideStep(Current, Current, 1f, new int2(1, 1), in field, false, new ObstacleSingleton());
            Assert.LessOrEqual(math.distancesq(moved, Current), 1e-8f);
        }
    }
}
