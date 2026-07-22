using NUnit.Framework;
using Unity.Mathematics;
using Wassup.UI;

namespace Wassup.Tests.EditMode
{
    // dreamcatcher-orb-dock unit 0 — 항아리 피규어 정착 물리 순수 코어 회귀.
    // 게이지 값이 아니라 렌더 위치·정착 채움 높이의 결정론/격리/수렴을 박제한다.
    // Verlet + 위치 제약. 파라미터·스텝 수는 러너(scratchpad)로 사전 수렴시킨 값.
    public class JarFigurePhysicsTests
    {
        const float Dt = 0.02f;

        static JarSimParams TestParams => new JarSimParams
        {
            gravity = 30f,
            damping = 0.9f,
            sleepMotionSq = 1e-5f,
        };

        // 결정론적 시작: 통 위쪽에서 살짝 어긋난 x 로 정지 상태로 놓는다(RNG 없음).
        static JarFigure[] Column(int count, float radius, float topY)
        {
            var arr = new JarFigure[count];
            for (int i = 0; i < count; i++)
            {
                float jitter = (i % 2 == 0 ? 1f : -1f) * radius * 0.05f;
                var pos = new float2(jitter, topY + i * radius * 2.1f);
                arr[i] = new JarFigure { pos = pos, prevPos = pos, radius = radius };
            }
            return arr;
        }

        static void Run(JarFigure[] figs, int count, in JarBounds b, in JarSimParams p, int steps)
        {
            for (int s = 0; s < steps; s++)
                JarFigurePhysics.Step(figs, count, b, p, Dt);
        }

        // 1. 결정론 — 동일 초기 상태·스텝 → 비트 동일 최종 상태.
        [Test]
        public void Step_IsDeterministic()
        {
            var bounds = new JarBounds { halfWidth = 1.5f, height = 10f };
            var p = TestParams;
            var a = new JarFigure[4];
            var b = new JarFigure[4];
            for (int i = 0; i < 4; i++)
            {
                var f = JarFigurePhysics.Create(
                    new float2(-1f + i * 0.6f, 3f + i * 0.7f),
                    new float2(0.5f - i * 0.3f, -1f), 0.4f, Dt);
                a[i] = f;
                b[i] = f;
            }

            Run(a, 4, bounds, p, 200);
            Run(b, 4, bounds, p, 200);

            for (int i = 0; i < 4; i++)
            {
                Assert.AreEqual(a[i].pos.x, b[i].pos.x, "pos.x[" + i + "]");
                Assert.AreEqual(a[i].pos.y, b[i].pos.y, "pos.y[" + i + "]");
                Assert.AreEqual(a[i].prevPos.x, b[i].prevPos.x, "prevPos.x[" + i + "]");
                Assert.AreEqual(a[i].prevPos.y, b[i].prevPos.y, "prevPos.y[" + i + "]");
            }
        }

        // 2. 격리 — 다수 스텝 후 모든 피규어가 벽·바닥 안.
        [Test]
        public void Figures_StayWithinBounds()
        {
            const float r = 0.4f;
            var bounds = new JarBounds { halfWidth = 1.5f, height = 20f };
            var figs = Column(6, r, 4f);

            Run(figs, 6, bounds, TestParams, 600);

            const float tol = 1e-3f;
            float xLim = bounds.halfWidth - r;
            for (int i = 0; i < 6; i++)
            {
                Assert.LessOrEqual(math.abs(figs[i].pos.x), xLim + tol, "x out of wall [" + i + "]");
                Assert.GreaterOrEqual(figs[i].pos.y, r - tol, "y below floor [" + i + "]");
            }
        }

        // 3. 정착 — drop 없이 N 스텝 후 총 잔여 이동 → ~0.
        [Test]
        public void Figures_SettleToRest()
        {
            const float r = 0.4f;
            var bounds = new JarBounds { halfWidth = 1.5f, height = 20f };
            var figs = Column(5, r, 4f);

            Run(figs, 5, bounds, TestParams, 1200);

            float motion = JarFigurePhysics.TotalMotionSq(figs, 5);
            Assert.Less(motion, 1e-3f, "not settled, residual motion=" + motion);
        }

        // 4. 단조 높이 — 좁은 통에서 k+1 개 정착 높이 ≥ k 개 정착 높이.
        [Test]
        public void FillHeight_IsMonotonicInCount()
        {
            const float r = 0.4f;
            // 좁은 통 → 세로 스택 강제(xLim = 0.2r).
            var bounds = new JarBounds { halfWidth = r * 1.2f, height = 40f };
            var p = TestParams;

            float prev = 0f;
            float last = 0f;
            for (int k = 1; k <= 5; k++)
            {
                var figs = Column(k, r, 6f);
                Run(figs, k, bounds, p, 1500);
                float h = JarFigurePhysics.FillHeight(figs, k);
                Assert.GreaterOrEqual(h, prev - 1e-3f,
                    "height dropped: k=" + k + " h=" + h + " prev=" + prev);
                prev = h;
                last = h;
            }
            Assert.Greater(last, 2f * r, "5 figures should stack above one layer");
        }

        // 5. 비침투 — 정착 후 모든 쌍 거리 ≥ r₁+r₂ − tol.
        [Test]
        public void Figures_DoNotOverlapAfterSettle()
        {
            const float r = 0.4f;
            var bounds = new JarBounds { halfWidth = 1.5f, height = 20f };
            var figs = Column(6, r, 4f);

            Run(figs, 6, bounds, TestParams, 1200);

            const float tol = 1e-2f;
            for (int i = 0; i < 6; i++)
            for (int j = i + 1; j < 6; j++)
            {
                float dist = math.distance(figs[i].pos, figs[j].pos);
                float minDist = figs[i].radius + figs[j].radius;
                Assert.GreaterOrEqual(dist, minDist - tol,
                    "overlap pair (" + i + "," + j + ") dist=" + dist + " min=" + minDist);
            }
        }

        // 빈 통·널 안전.
        [Test]
        public void FillHeight_EmptyIsZero()
        {
            Assert.AreEqual(0f, JarFigurePhysics.FillHeight(null, 0));
            Assert.AreEqual(0f, JarFigurePhysics.FillHeight(new JarFigure[0], 0));
        }
    }
}
