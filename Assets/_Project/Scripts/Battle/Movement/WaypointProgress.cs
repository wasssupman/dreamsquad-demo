using Unity.Mathematics;

namespace Wassup.Battle.Movement
{
    // waypoint-routing unit 2 — 순서만 관리하는 아키텍처 중립 순수 함수.
    // 이동 방식·거리장·ECS 상태를 모르고, 호출자가 준 도달 가능성과 현재 셀만 해석한다.
    public static class WaypointProgress
    {
        public static void Step(
            int2 currentCell,
            int2 waypointCell,
            bool reachable,
            int index,
            int count,
            out int nextIndex,
            out bool advanced,
            out bool done)
        {
            nextIndex = index;
            advanced = false;
            done = index >= count;
            if (done) return;

            if (reachable && !currentCell.Equals(waypointCell)) return;

            nextIndex = index + 1;
            advanced = true;
            done = nextIndex >= count;
        }
    }
}
