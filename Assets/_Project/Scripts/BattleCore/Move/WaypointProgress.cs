// salvaged from Assets/_Project/Scripts/Battle/Movement/WaypointProgress.cs (battle-core-rebuild unit 2)
// 이식 시 바뀐 것: 없음. `WaypointRouting` 도 같은 파일에 있었고 함께 옮겼다.
using Unity.Mathematics;

namespace Somnia.Battle.BattleCore.Move
{
    // 「몇 번째 경유점까지 왔나」. 이동 방식·거리장을 모르고, 호출자가 준 도달 가능성과
    // 현재 칸만 해석한다.
    public static class WaypointProgress
    {
        // 「인접 칸이면 지났다」는 8이웃 격자의 **위상**이지 튜닝 손잡이가 아니다.
        // 저작 필드로 노출하지 않는다. 원래는 정확한 칸 일치였는데, 스웜 20기에서 분리가
        // 서로 밀어내 한 칸에 수렴하지 못해 목표 칸을 스치고 지나가는 개체가 생겼다.
        private const int ArrivalChebyshevRadius = 1;

        public static void Step(int2 currentCell, int2 waypointCell, bool reachable,
                                int index, int count,
                                out int nextIndex, out bool advanced, out bool done)
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

        private static bool IsArrived(int2 currentCell, int2 waypointCell)
        {
            int2 delta = currentCell - waypointCell;
            return math.max(math.abs(delta.x), math.abs(delta.y)) <= ArrivalChebyshevRadius;
        }
    }

    // 레인 경로 해석. **좁은 쪽이 이긴다** — 종의 정체성 > 이번 편성의 성격 > 맵의 성질.
    //
    //     적 정의표  — 전 맵 공통, 그 적이 나올 때마다
    //     웨이브 컨셉 — 그 편성이 실린 웨이브에만
    //     레인 기본  — 그 맵의 모든 웨이브
    //
    // 컨셉이 적 정의를 못 이기는 이유: 비행 적의 경로는 강을 건너는 수단이라, 컨셉이 덮으면
    // 그 적이 지형에 갇힌다. 「좁은 쪽이 이긴다」가 여기서 안전 규칙으로도 작동한다.
    //
    // 호출부에서 삼항으로 풀지 않는 계약은 그대로다 — 풀면 계약이 코드에만 남고 테스트로
    // 고정할 지점이 사라진다.
    //
    // ⚠ 레인 기본 축은 **어느 맵도 저작하지 않았다**(M24 보류 · 전 맵 -1 = 골 직행).
    // 살릴 축인지 은퇴할 축인지 미결이라 규칙만 그대로 옮긴다.
    public static class WaypointRouting
    {
        public static int ResolvePathIndex(int authoredPathIndex, int conceptPathIndex, int laneDefaultPathIndex)
        {
            if (authoredPathIndex >= 0) return authoredPathIndex;
            if (conceptPathIndex >= 0) return conceptPathIndex;
            if (laneDefaultPathIndex >= 0) return laneDefaultPathIndex;
            return -1;
        }
    }
}
