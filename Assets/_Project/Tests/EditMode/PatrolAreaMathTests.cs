using NUnit.Framework;
using Unity.Collections;
using Unity.Mathematics;
using Wassup.Battle.Effects;
using Wassup.Battle.Movement;

namespace Wassup.Tests.EditMode
{
    // summon-patrol-defender unit 1 — 거점 박스 이동 방향 순수 계산.
    // 이동은 sim-critical 이라 회귀 테스트가 필수다(제약 10 (c)).
    public class PatrolAreaMathTests
    {
        private const int W = 10;
        private const int H = 10;
        private static readonly int2 Grid = new int2(W, H);

        private NativeArray<byte> _full;
        private NativeArray<byte> _box;
        private NativeArray<float2> _flow;
        private NativeArray<int> _dist;

        [SetUp]
        public void SetUp()
        {
            int n = W * H;
            _full = new NativeArray<byte>(n, Allocator.Persistent);
            _box = new NativeArray<byte>(n, Allocator.Persistent);
            _flow = new NativeArray<float2>(n, Allocator.Persistent);
            _dist = new NativeArray<int>(n, Allocator.Persistent);
            for (int i = 0; i < n; i++) _full[i] = 1;   // 전부 walkable 기본
        }

        [TearDown]
        public void TearDown()
        {
            if (_full.IsCreated) _full.Dispose();
            if (_box.IsCreated) _box.Dispose();
            if (_flow.IsCreated) _flow.Dispose();
            if (_dist.IsCreated) _dist.Dispose();
        }

        [Test]
        public void Enemy_In_Box_Pulls_Toward_Enemy()
        {
            var anchor = new int2(5, 5);
            var dir = Step(anchor, radius: 2, self: new int2(5, 5), enemies: new[] { new int2(7, 5) });

            Assert.AreEqual(new float2(1, 0), dir, "박스 안 적 쪽으로 cardinal 전진해야 한다");
        }

        [Test]
        public void Standing_On_Firing_Cell_Stops()
        {
            // 사거리 1 이면 (6,5) 는 (7,5) 의 사격 위치 = dist 0 → 더 갈 곳 없음.
            var anchor = new int2(5, 5);
            var dir = Step(anchor, radius: 2, self: new int2(6, 5), enemies: new[] { new int2(7, 5) });

            Assert.AreEqual(float2.zero, dir, "사격 가능 위치에 도달했으면 정지해야 한다");
        }

        [Test]
        public void Enemy_Outside_Box_Is_Ignored_And_Unit_Returns_To_Anchor()
        {
            var anchor = new int2(5, 5);
            var dir = Step(anchor, radius: 2, self: new int2(6, 5), enemies: new[] { new int2(9, 9) });

            Assert.AreEqual(new float2(-1, 0), dir, "박스 밖 적은 무시하고 거점으로 복귀해야 한다");
        }

        [Test]
        public void No_Enemy_At_Anchor_Stops()
        {
            var anchor = new int2(5, 5);
            var dir = Step(anchor, radius: 2, self: new int2(5, 5), enemies: new int2[0]);

            Assert.AreEqual(float2.zero, dir);
        }

        [Test]
        public void Pushed_Outside_Box_Returns_Toward_Anchor()
        {
            // 포털/토네이도/임펄스는 faction 을 안 보고 순찰병을 박스 밖으로 민다(계약 5).
            // 박스 마스크로는 dist 가 MaxValue 라 영구 정지하므로 fullMask 경로가 살아야 한다.
            var anchor = new int2(5, 5);
            var dir = Step(anchor, radius: 2, self: new int2(9, 5), enemies: new int2[0]);

            Assert.AreEqual(new float2(-1, 0), dir, "박스 밖에서는 거점으로 복귀해야 한다");
            Assert.AreNotEqual(float2.zero, dir, "박스 밖에서 정지하면 영구 고착이다");
        }

        [Test]
        public void Pushed_Outside_Box_Ignores_Enemies_Until_Back_In_Box()
        {
            var anchor = new int2(5, 5);
            var dir = Step(anchor, radius: 2, self: new int2(9, 5), enemies: new[] { new int2(9, 6) });

            Assert.AreEqual(new float2(-1, 0), dir, "복귀가 우선 — 박스 밖 적을 쫓지 않는다");
        }

        [Test]
        public void Wall_Split_Box_Falls_Back_To_Anchor_Instead_Of_Sticking()
        {
            // x=6 열을 박스 전 구간(y 3..7) 막으면 적 쪽이 박스 안에서 도달 불가.
            for (int y = 3; y <= 7; y++) SetWall(new int2(6, y));

            var anchor = new int2(5, 5);
            var dir = Step(anchor, radius: 2, self: new int2(5, 5), enemies: new[] { new int2(7, 5) });

            Assert.AreEqual(float2.zero, dir, "도달 불가 적을 향해 벽에 붙어 고착하면 안 된다");
        }

        [Test]
        public void Wall_With_Gap_Routes_Around_Instead_Of_Straight()
        {
            // x=6 의 y 4..6 만 막고 y=3 / y=7 은 열어둔다 → 우회로 존재.
            for (int y = 4; y <= 6; y++) SetWall(new int2(6, y));

            var anchor = new int2(5, 5);
            var dir = Step(anchor, radius: 2, self: new int2(5, 5), enemies: new[] { new int2(7, 5) });

            Assert.AreNotEqual(float2.zero, dir, "우회로가 있으면 정지하면 안 된다");
            Assert.AreNotEqual(new float2(1, 0), dir, "벽인 +x 로 직진하면 안 된다");
        }

        [Test]
        public void Box_With_No_Walkable_Cell_Stops_Without_Throwing()
        {
            for (int y = 4; y <= 6; y++)
            for (int x = 4; x <= 6; x++)
                SetWall(new int2(x, y));

            var anchor = new int2(5, 5);
            float2 dir = float2.zero;
            Assert.DoesNotThrow(() =>
                dir = Step(anchor, radius: 1, self: new int2(5, 6), enemies: new int2[0]));
            Assert.AreEqual(float2.zero, dir);
        }

        [Test]
        public void Symmetric_Enemies_Resolve_Deterministically()
        {
            var anchor = new int2(5, 5);
            var enemies = new[] { new int2(3, 5), new int2(7, 5) };   // 양쪽 동거리(2)

            // N-소스 BFS 라 양쪽 사격 위치가 같은 dist 0 → 하강은 RecoveryDir 의 고정
            // cardinal 순서(+x,-x,+y,-y)로 갈린다. 값 자체보다 **프레임 간 불변**이 계약이다.
            var a = Step(anchor, radius: 2, self: new int2(5, 5), enemies: enemies);
            var b = Step(anchor, radius: 2, self: new int2(5, 5), enemies: enemies);
            var c = Step(anchor, radius: 2, self: new int2(5, 5), enemies: new[] { enemies[1], enemies[0] });

            Assert.AreEqual(a, b, "같은 입력은 같은 방향");
            Assert.AreEqual(a, c, "적 배열 순서가 바뀌어도 같은 방향(소스 집합은 순서 무관)");
            Assert.AreNotEqual(float2.zero, a);
        }

        [Test]
        public void Unreachable_Nearest_Enemy_Does_Not_Hide_A_Reachable_One()
        {
            // 벽 너머 최근접 적(도달 불가) 때문에 같은 구역의 도달 가능한 적을 포기하면 안 된다.
            // 최근접 1체만 고르던 구현은 여기서 거점으로 뒷걸음질쳤다.
            for (int y = 3; y <= 7; y++) SetWall(new int2(6, y));   // x=6 열 차단

            var anchor = new int2(5, 5);
            var enemies = new[] { new int2(7, 5), new int2(3, 5) };  // 가까운 쪽(7,5)이 벽 너머

            var dir = Step(anchor, radius: 2, self: new int2(5, 5), enemies: enemies);

            Assert.AreEqual(new float2(-1, 0), dir, "도달 가능한 (3,5) 쪽으로 가야 한다");
        }

        [Test]
        public void Blocked_Own_Cell_Still_Escapes()
        {
            // 차단형 해저드가 발밑에 깔리면 자기 셀이 마스크 0 이 된다. 여기서 정지하면
            // 순찰병이 장애물 안에 영구히 박힌다 — 유한한 이웃으로 빠져나가야 한다.
            var anchor = new int2(5, 5);
            SetWall(new int2(5, 6));                                  // self 셀 자체를 차단

            var dir = Step(anchor, radius: 2, self: new int2(5, 6), enemies: new int2[0]);

            Assert.AreNotEqual(float2.zero, dir, "차단된 셀에서 탈출해야 한다");
        }

        [Test]
        public void IsInArea_Uses_Chebyshev()
        {
            var anchor = new int2(5, 5);
            Assert.IsTrue(PatrolAreaMath.IsInArea(new int2(7, 7), anchor, 2), "대각 모서리도 박스 안");
            Assert.IsFalse(PatrolAreaMath.IsInArea(new int2(8, 5), anchor, 2));
        }

        private void SetWall(int2 cell) => _full[GridMath.CellIndex(cell, Grid)] = 0;

        private float2 Step(int2 anchor, int radius, int2 self, int2[] enemies, int attackTiles = 1)
        {
            for (int i = 0; i < _box.Length; i++) _box[i] = 0;
            PatrolAreaMath.FillAreaMask(_full, Grid, anchor, radius, _box);

            var enemyCells = new NativeArray<int2>(enemies.Length, Allocator.Persistent);
            try
            {
                for (int i = 0; i < enemies.Length; i++) enemyCells[i] = enemies[i];
                return PatrolAreaMath.StepDir(
                    _box, _full, Grid, anchor, radius, self, attackTiles,
                    enemyCells, _flow, _dist);
            }
            finally
            {
                enemyCells.Dispose();
            }
        }
    }
}
