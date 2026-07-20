using UnityEngine;

namespace Wassup.UI
{
    // placement-cell-snap unit 3 — 타일 이동 판정을 **주기적(throttle)** 으로 커밋한다.
    // 공간 히스테리시스(PlacementCellSnap.Resolve) 위에서 동작.
    //
    // 매 프레임 실시간 추종(휙휙)도, 멈출 때까지 freeze 도 아닌 중간: interval(초)마다 현재 target 으로
    // 타일을 갱신하고 사이 구간엔 committed 유지 → 이동 중에도 interval 간격으로 "스텝" 이동, 정지하면
    // 다음 tick 에 현재 칸 확정(같으면 no-op). 예: interval=0.2 → 5Hz 스텝.
    public static class PlacementSnapDebounce
    {
        public struct State
        {
            public float elapsed; // 마지막 tick 이후 경과(초)
        }

        // committed = 현재 확정 셀, target = 이번 프레임 공간 해석(히스테리시스) 결과. 새 확정 셀 반환, state 갱신.
        public static Vector2Int Step(ref State state, Vector2Int committed, Vector2Int target,
                                      float dt, float interval)
        {
            if (interval <= 0f) return target; // 0 = 매 프레임(실시간)

            state.elapsed += dt;
            if (state.elapsed >= interval)
            {
                state.elapsed = 0f;
                return target; // tick: 현재 target 으로 갱신
            }
            return committed; // tick 사이 → 유지
        }
    }
}
