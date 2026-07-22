using Unity.Mathematics;

namespace Wassup.UI
{
    // dreamcatcher-orb-dock unit 0 — 항아리 안 미니 피규어의 정착(settle) 물리 순수 시뮬.
    // 아키텍처 중립: Time/EntityManager/Spine 무관, plain 값 in/out → EditMode 회귀 대상.
    // 게이지 값 자체는 이 시뮬이 결정하지 않는다(DreamcatcherHandController.Gauge 가 SoT).
    // 이 코어는 렌더용 위치와, 정착 시 단조·결정론적 채움 높이(2순위 판독)만 제공한다.
    //
    // 기법: Verlet 적분 + 위치 제약(벽/바닥/피규어 겹침). 비탄성 정착이라 드롭 임팩트 순간만
    // 살아 움직이고 빠르게 멈춘다(spec "임팩트-only" 의도와 일치). 속도는 (pos-prevPos) 로 암시.

    // 항아리 로컬 좌표: x∈[-halfWidth,halfWidth], y=0 이 바닥, 위로 +.
    public struct JarFigure
    {
        public float2 pos;
        public float2 prevPos; // 직전 위치. 암시 속도 = (pos - prevPos).
        public float radius;
    }

    public struct JarBounds
    {
        public float halfWidth; // 좌우 벽: |pos.x| ≤ halfWidth - radius
        public float height;    // 천장(넘침 판정). 바닥은 y=0.
    }

    public struct JarSimParams
    {
        public float gravity;      // +값, 아래(−y)로 당김
        public float damping;      // 암시 속도 배수(0~1, settle)
        public float sleepMotionSq;// 스텝당 이동² 이 값 미만이면 prevPos=pos 로 스냅(정지)

        public static JarSimParams Default => new JarSimParams
        {
            gravity = 30f,
            damping = 0.9f,
            sleepMotionSq = 1e-5f,
        };
    }

    public static class JarFigurePhysics
    {
        // 초기 속도로 피규어 생성(prevPos 를 역산). 드롭 스폰(unit 2)에서 사용.
        public static JarFigure Create(float2 pos, float2 vel, float radius, float dt)
            => new JarFigure { pos = pos, prevPos = pos - vel * dt, radius = radius };

        // 고정 dt 한 스텝. figures[0..count) 를 제자리 갱신(호출자 소유 버퍼).
        // 결정론: RNG·Time 없음. 같은 입력 → 같은 출력. iterations = 위치 제약 완화 횟수.
        public static void Step(
            JarFigure[] figures, int count, in JarBounds bounds, in JarSimParams p,
            float dt, int iterations = 6)
        {
            if (figures == null || count <= 0)
                return;
            if (count > figures.Length)
                count = figures.Length;

            float gravDelta = p.gravity * dt * dt;

            // ① Verlet 적분: 암시 속도 = (pos - prevPos) * damping, 중력은 위치에 직접.
            for (int i = 0; i < count; i++)
            {
                var f = figures[i];
                float2 vel = (f.pos - f.prevPos) * p.damping;
                f.prevPos = f.pos;
                f.pos = f.pos + vel + new float2(0f, -gravDelta);
                figures[i] = f;
            }

            // ② 위치 제약 반복: 피규어 겹침 + 벽/바닥 클램프(비탄성 = pos 만 이동).
            for (int it = 0; it < iterations; it++)
            {
                for (int i = 0; i < count; i++)
                {
                    for (int j = i + 1; j < count; j++)
                    {
                        var a = figures[i];
                        var b = figures[j];
                        float2 d = b.pos - a.pos;
                        float distSq = math.lengthsq(d);
                        float minDist = a.radius + b.radius;
                        if (distSq >= minDist * minDist || distSq <= 1e-8f)
                            continue;

                        float dist = math.sqrt(distSq);
                        float2 n = d / dist;
                        float2 shift = n * ((minDist - dist) * 0.5f);
                        a.pos -= shift;
                        b.pos += shift;
                        figures[i] = a;
                        figures[j] = b;
                    }
                }

                for (int i = 0; i < count; i++)
                {
                    var f = figures[i];
                    float xLim = bounds.halfWidth - f.radius;
                    if (xLim < 0f) xLim = 0f;
                    f.pos.x = math.clamp(f.pos.x, -xLim, xLim);
                    if (f.pos.y < f.radius) f.pos.y = f.radius;
                    figures[i] = f;
                }
            }

            // ③ sleep 스냅: 스텝당 이동이 미세하면 암시 속도 0(prevPos=pos).
            for (int i = 0; i < count; i++)
            {
                var f = figures[i];
                if (math.lengthsq(f.pos - f.prevPos) < p.sleepMotionSq)
                    f.prevPos = f.pos;
                figures[i] = f;
            }
        }

        // 정착 채움 높이 = 가장 높은 피규어의 top(pos.y + radius). 빈 통 0. 결정론적 질의.
        public static float FillHeight(JarFigure[] figures, int count)
        {
            if (figures == null || count <= 0)
                return 0f;
            if (count > figures.Length)
                count = figures.Length;
            float top = 0f;
            for (int i = 0; i < count; i++)
            {
                float t = figures[i].pos.y + figures[i].radius;
                if (t > top) top = t;
            }
            return top;
        }

        // 총 잔여 이동²(∑|pos-prevPos|²). settle 감지·테스트용. 정착 시 → 0.
        public static float TotalMotionSq(JarFigure[] figures, int count)
        {
            if (figures == null || count <= 0)
                return 0f;
            if (count > figures.Length)
                count = figures.Length;
            float sum = 0f;
            for (int i = 0; i < count; i++)
                sum += math.lengthsq(figures[i].pos - figures[i].prevPos);
            return sum;
        }
    }
}
