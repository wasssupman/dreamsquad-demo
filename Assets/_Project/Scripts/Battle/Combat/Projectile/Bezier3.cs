using Unity.Mathematics;

namespace Wassup.Battle.Combat.Projectile
{
    // projectile-emission-pattern unit 1 — 3차 베지어 궤적 수학. 순수 static,
    // Burst 호환, EditMode 고정 (BallisticArc/SkyFall 과 같은 형태·같은 거주지).
    //
    // 이동 수학은 로직 계층에 산다: 이 함수를 ECS sim(XZ 위치)과 Mono view(Y 아치)가
    // 나눠 소비한다 — 아키텍처 종속이면 불가능한 공유다. arm 은 "상태 읽기 →
    // 호출 → 결과 쓰기" 소비자일 뿐이다.
    public static class Bezier3
    {
        // 표준 3차 베지어. t 는 호출 측에서 saturate 한다.
        public static float3 Position(float3 p0, float3 p1, float3 p2, float3 p3, float t)
        {
            float u = 1f - t;
            float uu = u * u;
            float tt = t * t;
            return uu * u * p0
                 + 3f * uu * t * p1
                 + 3f * u * tt * p2
                 + tt * t * p3;
        }

        // 제어점 결정론 생성 (README 계약 6 — seeded RNG 금지). 진행 방향의 수직으로
        // 좌우 교대 스윙하고, swingIndex 가 커질수록 더 크게 벌어진다. 그래서
        // shotCount 를 올리면 같은 타겟으로 가는 여러 발이 각각 다른 곡선을 그리며
        // 갈라진다 — authoring 값 하나로 살포가 나온다.
        //
        // 퇴화 입력(origin ≈ dest)은 수직축이 정의되지 않으므로 직선으로 붕괴시킨다
        // (BlinkMath.FallbackAxis 와 같은 결 — 런타임 파생 축으로 NaN 을 만들지 않는다).
        public static void ControlPoints(float3 origin, float3 dest, int swingIndex,
                                        float lateral, float forwardBias,
                                        out float3 c1, out float3 c2)
        {
            float3 delta = dest - origin;
            delta.y = 0f;
            float lenSq = math.lengthsq(delta);
            if (lenSq < 1e-6f)
            {
                c1 = dest;
                c2 = dest;
                return;
            }

            float len = math.sqrt(lenSq);
            float3 dir = delta / len;
            float3 perp = new float3(-dir.z, 0f, dir.x);

            int s = math.abs(swingIndex);
            float sign = (s & 1) == 0 ? 1f : -1f;
            float mag = lateral * (1f + (s / 2) * 0.35f);
            float3 forward = dir * (len * forwardBias);

            c1 = origin + forward + perp * (sign * mag);
            c2 = dest - forward + perp * (sign * mag * 0.5f);
        }
    }
}
