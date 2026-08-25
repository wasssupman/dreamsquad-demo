using Unity.Mathematics;

namespace Wassup.Battle.Movement
{
    // waypoint-routing unit 2 — 순서만 관리하는 아키텍처 중립 순수 함수.
    // 이동 방식·거리장·ECS 상태를 모르고, 호출자가 준 도달 가능성과 현재 셀만 해석한다.
    public static class WaypointProgress
    {
        // waypoint-routing unit 9 — 「인접 칸이면 지났다」는 8이웃 격자의 위상이지
        // 튜닝 손잡이가 아니다. 저작 필드로 노출하지 않는다.
        private const int ArrivalChebyshevRadius = 1;

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

            if (reachable && !IsArrived(currentCell, waypointCell)) return;

            nextIndex = index + 1;
            advanced = true;
            done = nextIndex >= count;
        }

        // waypoint-routing unit 9 — 정확한 셀 일치는 판당 2기(Skimmer)에서는 맞았지만
        // 스웜(20기)은 축분리 스윕이 서로 밀어내 한 칸에 수렴하지 못한다. 밀려서
        // 목표 칸을 스치고 지나간 개체도 도달로 인정해야 되돌아오지 않는다.
        private static bool IsArrived(int2 currentCell, int2 waypointCell)
        {
            int2 delta = currentCell - waypointCell;
            return math.max(math.abs(delta.x), math.abs(delta.y)) <= ArrivalChebyshevRadius;
        }
    }

    // waypoint-routing unit 9 — 레인 경로 해석. 적 SO 지정(개체)이 레인 기본(맵)보다
    // 좁은 축이라 이긴다: Skimmer 의 Air 경로는 어느 레인에서 나오든 종의 정체성이고,
    // 레인 기본은 「지정 없는 적이 이 맵에서 어디로 오나」다.
    //
    // duel-route-tours unit 1 — 그 사이에 **웨이브 컨셉**이 들어와 3축이 됐다.
    // 좁은 순서대로: 종의 정체성 > 이번 편성의 성격 > 맵의 성질.
    //
    //     적 SO      (AttackUnitData.waypointPathIndex) — 전 맵 공통, 그 적이 나올 때마다
    //     컨셉       (WaveConceptSlot.pathIndex)        — 그 편성이 실린 웨이브에만
    //     레인 기본  (MapDocument.spawnRoutes[lane])    — 그 맵의 모든 웨이브
    //
    // 컨셉이 적 SO 를 못 이기는 이유: 비행 적의 경로는 강을 건너는 수단이라, 컨셉이 덮으면
    // 그 적이 지형에 갇힌다. 「좁은 쪽이 이긴다」가 여기서 안전 규칙으로도 작동한다.
    //
    // 우선순위를 호출부에서 삼항으로 풀지 않는 계약은 그대로다 — 풀면 계약이 코드에만 남고
    // EditMode 로 고정할 지점이 사라진다. 소비자는 스폰(BattleBridge)과 예고
    // (BuildSpawnGuideForecasts) 둘이며 **같은 이 함수**를 부른다.
    public static class WaypointRouting
    {
        public static int ResolvePathIndex(
            int authoredPathIndex, int conceptPathIndex, int laneDefaultPathIndex)
        {
            if (authoredPathIndex >= 0) return authoredPathIndex;
            if (conceptPathIndex >= 0) return conceptPathIndex;
            if (laneDefaultPathIndex >= 0) return laneDefaultPathIndex;
            return -1;
        }
    }
}
