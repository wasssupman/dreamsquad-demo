using Unity.Mathematics;
using Wassup.Battle.Effects;

namespace Wassup.Battle.Movement
{
    // enemy-hunter-targeting unit 2 — anchor 추격 한 스텝. cell-trim 불변식을 재사용해
    // 직선 이동을 walk 타일에 묶되, 직선이 벽에 막히면 축 분리(x만/z만)로 슬라이드한다.
    // 순수 static (플레인 값 입출력) → EditMode 로 결정론/축분리/fully-boxed 를 고정한다
    // (제약 10 — sim-critical 이동 로직). MovementSystem 이 이 결과의 진행 여부로
    // Chasing 유지(continue) vs flow-march 폴백(softlock 가드)을 정한다.
    public static class MovementChase
    {
        // clamp 감지 임계. kBoundaryEpsilon(1e-3) 이하의 경계 clamp 도 "막힘"으로 잡되,
        // 실제 이동 스텝(≫1e-4)과 확실히 구분되도록 작게. 부동소수 누적 노이즈보다는 큼.
        private const float ClampEpsilonSq = 1e-8f;

        // 반환 = 이번 프레임 이동 위치. **moved 가 current 와 같으면(진행 0) fully-boxed**
        // — 호출측이 이걸 감지해 flow-march 로 폴백한다(영구 freeze/softlock 방지).
        public static float3 SlideStep(
            float3 current, float3 anchor, float step, int2 chaseCell,
            in FlowFieldSingleton field, bool hasObstacles, in ObstacleSingleton obstacles)
        {
            float3 to = anchor - current; to.y = 0f;
            float dist = math.length(to);
            if (dist <= 1e-4f) return current; // 이미 앵커 위치(비-walkable 앵커면 사실상 도달) — 이동 없음

            float3 dir = to / dist;
            float3 full = (step >= dist)
                ? new float3(anchor.x, current.y, anchor.z)
                : current + dir * step;
            float3 moved = MovementCellTrim.Apply(full, chaseCell, in field, hasObstacles, in obstacles);

            if (math.distancesq(moved, full) > ClampEpsilonSq) // 직선 clamp(벽) → 축 분리 슬라이드
            {
                float3 xTry = new float3(current.x + dir.x * step, current.y, current.z);
                float3 xMoved = MovementCellTrim.Apply(xTry, chaseCell, in field, hasObstacles, in obstacles);
                if (math.distancesq(xMoved, xTry) <= ClampEpsilonSq)
                {
                    moved = xMoved;
                }
                else
                {
                    float3 zTry = new float3(current.x, current.y, current.z + dir.z * step);
                    float3 zMoved = MovementCellTrim.Apply(zTry, chaseCell, in field, hasObstacles, in obstacles);
                    // 둘 다 막힘 = fully-boxed → current 반환(진행 0 신호, 호출측 폴백).
                    moved = math.distancesq(zMoved, zTry) <= ClampEpsilonSq ? zMoved : current;
                }
            }
            return moved;
        }
    }
}
