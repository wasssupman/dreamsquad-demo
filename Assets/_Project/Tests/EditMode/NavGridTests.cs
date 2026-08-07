using NUnit.Framework;
using Unity.Collections;
using Unity.Mathematics;
using Wassup.Battle.Movement;

namespace Wassup.Tests.EditMode
{
    // continuous-agent-movement unit 1·2 — 벽 질의 단일 진입점의 직접 커버리지.
    //
    // unit 2 이후 술어의 근거는 정적 walk 마스크 하나다(이전엔 flow == 0). 여기 케이스는
    // 그 술어의 계약을 못박는다 — 특히 "골에서 도달 불가한 Walk 셀은 벽이 아니다" 는
    // D1-b(봉쇄 시 차단 구역 전체가 벽이 되는 사고)를 막는 핵심 성질이다.
    public class NavGridTests
    {
        private const int W = 3, H = 1;

        private static NativeArray<byte> Mask(byte a, byte b, byte c)
        {
            var m = new NativeArray<byte>(3, Allocator.Temp);
            m[0] = a; m[1] = b; m[2] = c;
            return m;
        }

        private static NavGrid Grid(
            NativeArray<byte> mask,
            NativeHashSet<int2> blocked = default,
            bool hasObstacles = false)
            => new NavGrid(
                staticWalk:   mask,
                blockedCells: blocked,
                hasObstacles: hasObstacles,
                gridSize:     new int2(W, H),
                tileSize:     1f,
                origin:       float3.zero);

        // ── 경계 ────────────────────────────────────────────────────────────────

        [Test]
        public void IsBlocked_True_Outside_Grid()
        {
            var nav = Grid(Mask(1, 1, 1));
            Assert.IsTrue(nav.IsBlocked(new int2(-1, 0)), "왼쪽 밖");
            Assert.IsTrue(nav.IsBlocked(new int2(3, 0)),  "오른쪽 밖");
            Assert.IsTrue(nav.IsBlocked(new int2(0, 1)),  "위쪽 밖");
        }

        // ── 정적 마스크 술어 (unit 2 의 본체) ───────────────────────────────────

        [Test]
        public void StaticMask_Decides_Walkability()
        {
            var nav = Grid(Mask(1, 0, 1));
            Assert.IsFalse(nav.IsBlocked(new int2(0, 0)), "Walk 타일");
            Assert.IsTrue(nav.IsBlocked(new int2(1, 0)),  "비-Walk 타일 = 벽");
            Assert.IsFalse(nav.IsBlocked(new int2(2, 0)), "Walk 타일");
        }

        [Test]
        public void IsolatedWalkCell_IsNotBlocked()
        {
            // unit 2 의 유일한 의미 변화. 골에서 도달 불가한 Walk 셀은 flow=0 이라 이전
            // 술어에선 벽이었지만, 지형상으로는 걸을 수 있는 칸이다.
            // 이 성질이 없으면 D1-b 에서 봉쇄로 끊긴 구역 전체가 벽이 되어 그 안의 적이
            // 자기가 선 칸을 벽으로 인식하고 clamp 거동이 무너진다.
            var nav = Grid(Mask(1, 1, 1));
            Assert.IsFalse(nav.IsBlocked(new int2(0, 0)),
                "경로 도달 가능성과 무관하게 지형이 Walk 면 통행 가능");
        }

        [Test]
        public void UncreatedMask_TreatedAsOpenGround()
        {
            // 마스크를 안 쓰는 EditMode 픽스처 보호 규약. 프로덕션은 SimFieldInstaller 가
            // 항상 채우므로 해당 없다.
            var nav = Grid(default);
            Assert.IsFalse(nav.IsBlocked(new int2(0, 0)));
            Assert.IsFalse(nav.IsBlocked(new int2(2, 0)));
            Assert.IsTrue(nav.IsBlocked(new int2(3, 0)), "경계 밖은 그래도 막힘");
        }

        // ── 동적 장애물 오버레이 ────────────────────────────────────────────────

        [Test]
        public void Obstacles_BlockWalkableCell()
        {
            var blocked = new NativeHashSet<int2>(4, Allocator.Temp);
            blocked.Add(new int2(1, 0));
            var nav = Grid(Mask(1, 1, 1), blocked, hasObstacles: true);

            Assert.IsFalse(nav.IsBlocked(new int2(0, 0)));
            Assert.IsTrue(nav.IsBlocked(new int2(1, 0)), "장애물이 walkable 셀을 막는다");
        }

        [Test]
        public void Obstacles_Ignored_When_HasObstacles_False()
        {
            var blocked = new NativeHashSet<int2>(4, Allocator.Temp);
            blocked.Add(new int2(1, 0));
            var nav = Grid(Mask(1, 1, 1), blocked, hasObstacles: false);

            Assert.IsFalse(nav.IsBlocked(new int2(1, 0)), "플래그가 false 면 집합을 보지 않는다");
        }

        [Test]
        public void Obstacles_UncreatedSet_IsHarmless()
        {
            // hasObstacles=true 인데 집합이 미생성인 조합(티어다운 경합)에서 죽지 않는다.
            var nav = Grid(Mask(1, 1, 1), default, hasObstacles: true);
            Assert.IsFalse(nav.IsBlocked(new int2(1, 0)));
        }

        // ── MaterializeWalkMask ────────────────────────────────────────────────

        [Test]
        public void MaterializeWalkMask_MirrorsIsBlocked()
        {
            var blocked = new NativeHashSet<int2>(4, Allocator.Temp);
            blocked.Add(new int2(0, 0));
            var nav = Grid(Mask(1, 1, 0), blocked, hasObstacles: true);

            var outMask = new NativeArray<byte>(3, Allocator.Temp);
            nav.MaterializeWalkMask(outMask);

            Assert.AreEqual(0, outMask[0], "장애물");
            Assert.AreEqual(1, outMask[1], "통행 가능");
            Assert.AreEqual(0, outMask[2], "비-Walk 타일");
        }

        [Test]
        public void MaterializeWalkMask_UncreatedMask_AllWalkable()
        {
            var nav = Grid(default);
            var outMask = new NativeArray<byte>(3, Allocator.Temp);
            nav.MaterializeWalkMask(outMask);

            Assert.AreEqual(1, outMask[0]);
            Assert.AreEqual(1, outMask[1]);
            Assert.AreEqual(1, outMask[2]);
        }
    }
}
