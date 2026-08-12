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

        // ───────────── unit 9 — 중심과 집의 분리 ─────────────
        //
        // 소환사 셀(중심)에 소환물이 겹쳐 스폰되던 것을 고치면서 두 개념이 갈렸다.
        // 이 두 테스트가 «정지의 기준은 집이지 중심이 아니다»를 못박는다 — 겸직으로
        // 되돌리면 둘 중 하나는 반드시 빨개진다.

        [Test]
        public void Idle_Is_Anchored_To_Home_Not_To_Center()
        {
            var center = new int2(4, 4);
            var home = new int2(5, 4);
            var dir = Step(center, home, radius: 3, self: home, enemies: new int2[0]);
            Assert.AreEqual(0f, math.lengthsq(dir), 1e-5f, "집에 서 있으면 정지한다");
        }

        [Test]
        public void Standing_On_The_Center_Walks_Back_To_Home()
        {
            var center = new int2(4, 4);
            var home = new int2(5, 4);
            var dir = Step(center, home, radius: 3, self: center, enemies: new int2[0]);
            Assert.Greater(dir.x, 0.5f, "중심은 집이 아니다 — 집(+x)으로 물러난다");
        }

        // unit 9 이전 형태 — 중심과 집이 같은 칸. 기존 테스트의 기대값을 그대로 재현한다.
        private float2 Step(int2 anchor, int radius, int2 self, int2[] enemies, int attackTiles = 1)
            => Step(anchor, anchor, radius, self, enemies, attackTiles);

        private float2 Step(int2 center, int2 home, int radius, int2 self, int2[] enemies, int attackTiles = 1)
            => StepAt(center, home, radius, self, CellCenter(self), enemies, null, attackTiles);

        private static float3 CellCenter(int2 cell) => new float3(cell.x, 0f, cell.y);

        // 위치까지 주는 형태. selfPos/enemyPos 를 주지 않으면 셀 중심에 선 것으로 본다 —
        // 기존 테스트의 의미(칸 중앙 정렬)를 그대로 유지한다.
        private float2 StepAt(int2 center, int2 home, int radius, int2 self, float3 selfPos,
            int2[] enemies, float3[] enemyPos, int attackTiles = 1, float tileSize = 1f)
        {
            for (int i = 0; i < _box.Length; i++) _box[i] = 0;
            PatrolAreaMath.FillAreaMask(_full, Grid, center, radius, _box);

            var enemyCells = new NativeArray<int2>(enemies.Length, Allocator.Persistent);
            var enemyWorld = new NativeArray<float3>(enemies.Length, Allocator.Persistent);
            try
            {
                for (int i = 0; i < enemies.Length; i++)
                {
                    enemyCells[i] = enemies[i];
                    enemyWorld[i] = enemyPos != null ? enemyPos[i] : CellCenter(enemies[i]);
                }
                return PatrolAreaMath.StepDir(
                    _box, _full, Grid, center, home, radius, self, attackTiles,
                    enemyCells, _flow, _dist, selfPos, enemyWorld, tileSize);
            }
            finally
            {
                enemyCells.Dispose();
                enemyWorld.Dispose();
            }
        }

        // ---- 사거리 2차 게이트(물리 거리) 회귀 ------------------------------------
        // 격자상 «사격 칸» 에 도착해도, 둘 다 연속 이동이라 칸 안에서 밀려 있으면 실제로는
        // 못 때린다(사거리 1이 실측 2칸까지 벌어진다). 그때 멈추면 «멈추는데 못 때리는»
        // 교착이 된다 — 2026-08-12 실측 182프레임. 계속 다가가야 한다.

        [Test]
        public void OnFiringCell_ButPhysicallyTooFar_KeepsClosing()
        {
            var anchor = new int2(5, 5);
            // 셀은 (6,5)/(7,5) 로 인접(사거리 1 통과)인데 월드는 1.8칸 떨어져 있다.
            var dir = StepAt(anchor, anchor, radius: 2,
                self: new int2(6, 5), selfPos: new float3(5.6f, 0f, 5f),
                enemies: new[] { new int2(7, 5) }, enemyPos: new[] { new float3(7.4f, 0f, 5f) });

            Assert.Greater(dir.x, 0.5f, "물리적으로 머니 적 쪽(+x)으로 계속 다가가야 한다");
        }

        [Test]
        public void OnFiringCell_AndPhysicallyClose_Stops()
        {
            var anchor = new int2(5, 5);
            // 같은 셀 배치인데 월드 거리는 1.0 — 상한(1.5) 안이라 정지가 맞다.
            var dir = StepAt(anchor, anchor, radius: 2,
                self: new int2(6, 5), selfPos: new float3(6f, 0f, 5f),
                enemies: new[] { new int2(7, 5) }, enemyPos: new[] { new float3(7f, 0f, 5f) });

            Assert.AreEqual(float2.zero, dir, "사거리 안이면 멈춘다");
        }

        [Test]
        public void OnFiringCell_DoesNotChaseAnEnemyOutsideTheBox()
        {
            // 코드 리뷰 지적(H2) — 구역 **안** 적 덕에 사격 칸에 섰는데(그 적은 이미 사거리 안),
            // 구역 **밖** 적이 셀로는 인접하고 물리적으로 멀 때 그쪽으로 끌려가면 안 된다.
            // 끌려가면 박스를 나가고 다음 프레임 DescendToHome 이 되돌려 경계에서 진동한다.
            var anchor = new int2(5, 5);
            var dir = StepAt(anchor, anchor, radius: 1,
                self: new int2(6, 5), selfPos: new float3(5.6f, 0f, 5f),
                enemies: new[] { new int2(6, 6), new int2(7, 5) },      // 박스 안 / 박스 밖
                enemyPos: new[] { new float3(5.6f, 0f, 6f), new float3(7.4f, 0f, 5f) });

            Assert.AreNotEqual(new float2(1f, 0f), dir, "구역 밖 적(+x)을 쫓아 박스를 나가면 안 된다");
        }

        [Test]
        public void OnFiringCell_BlockedTowardEnemy_DoesNotPushIntoWall()
        {
            // 코드 리뷰 지적(H2) — 접근 방향이 막혔으면 벽으로 밀지 않는다.
            // 밀면 AgentCollision 이 변위를 먹어 «걷는 애니로 제자리» 가 된다.
            var anchor = new int2(5, 5);
            _full[GridMath.CellIndex(new int2(7, 5), Grid)] = 0;   // 적 쪽 진행 칸이 벽

            var dir = StepAt(anchor, anchor, radius: 2,
                self: new int2(6, 5), selfPos: new float3(5.6f, 0f, 5f),
                enemies: new[] { new int2(7, 5) }, enemyPos: new[] { new float3(7.4f, 0f, 5f) });

            Assert.AreNotEqual(new float2(1f, 0f), dir, "벽 쪽(+x)으로 밀면 안 된다");
        }

        [Test]
        public void OnFiringCell_TooFarDiagonally_ClosesOnDominantAxis()
        {
            var anchor = new int2(5, 5);
            var dir = StepAt(anchor, anchor, radius: 2,
                self: new int2(6, 5), selfPos: new float3(5.6f, 0f, 4.6f),
                enemies: new[] { new int2(7, 6) }, enemyPos: new[] { new float3(7.4f, 0f, 6.4f) });

            // 체비셰프 상한이므로 지배축을 줄이는 것이 곧 거리를 줄이는 것이다.
            Assert.AreNotEqual(float2.zero, dir, "대각으로 멀어도 멈추면 안 된다");
        }
    }
}
