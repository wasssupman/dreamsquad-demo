using Unity.Mathematics;

namespace Wassup.Battle.Movement
{
    // continuous-agent-movement unit 3 — 반지름 r 의 원 vs 벽 타일 AABB 충돌 + 접선 슬라이드.
    //
    // 이전(MovementCellTrim.Apply)은 유닛을 점으로 보고 "다음 위치가 벽 셀이면 현재 셀
    // 경계로 clamp" 했다. 그래서 벽에 비스듬히 부딪히면 미끄러지지 않고 그 축이 통째로
    // 막히고 코너에서 걸렸다 — 격자가 눈에 보이는 가장 큰 원인.
    //
    // 순수 함수. NavGrid(프레임 뷰)와 plain 값만 받는다.
    public static class AgentCollision
    {
        // 경계에 정확히 붙으면 다음 프레임 셀 판정이 흔들린다. 살짝 띄운다
        // (MovementCellTrim.kBoundaryEpsilon 과 같은 성격).
        private const float kSkin = 1e-3f;

        // radius <= 0 이면 기존 점 충돌(MovementCellTrim.Apply)에 위임한다.
        // 술어를 두 벌 두지 않기 위한 위임이지 폴백 분기의 중복이 아니다.
        public static float3 Resolve(float3 current, float3 desired, float radius, in NavGrid nav)
        {
            if (radius <= 0f)
            {
                int2 currentCell = GridMath.WorldToCell(current, nav.tileSize, nav.gridSize, origin: nav.origin);
                return MovementCellTrim.Apply(desired, currentCell, in nav);
            }

            // 축 분리 해결 — X 를 먼저 풀고 그 결과 위치에서 Z 를 푼다.
            // 이 순서가 슬라이드를 공짜로 만든다: 막힌 축만 멈추고 자유로운 축은 계속 간다.
            float x = ResolveAxis(current.x, desired.x, current.z, radius, in nav, xAxis: true);
            float z = ResolveAxis(current.z, desired.z, x,         radius, in nav, xAxis: false);
            return new float3(x, desired.y, z);
        }

        // from → to 로 한 축을 움직인다. `at` 은 반대축 위치(원이 걸치는 범위를 여기서 구한다).
        //
        // 전진 가장자리가 지나가는 **모든** 셀 열/행을 진행 순서대로 훑는다.
        // 최종 위치만 검사하면 중간 셀을 건너뛴다(ecs-review M1): 에이전트가 셀 경계에 있고
        // 외력으로 전속 이동하면 가장자리가 최대 `0.5 + 0.9 + r` 타일까지 가서, 벽 셀 하나를
        // 지나쳐 그 너머 빈 셀에 도달할 수 있다. 스윕이 그 구멍을 막는다.
        private static float ResolveAxis(
            float from, float to, float at, float radius, in NavGrid nav, bool xAxis)
        {
            float delta = to - from;
            if (math.abs(delta) < 1e-9f) return from;

            float ts  = nav.tileSize;
            float dir = math.sign(delta);
            float originMove  = xAxis ? nav.origin.x : nav.origin.z;
            float originCross = xAxis ? nav.origin.z : nav.origin.x;

            // 전진 가장자리의 출발 셀 → 도착 셀. 그 사이를 전부 본다.
            int startCoord = CellCoord(from + dir * radius, originMove, ts);
            int endCoord   = CellCoord(to   + dir * radius, originMove, ts);
            int stepI = dir > 0f ? 1 : -1;

            // 원이 걸치는 반대축 셀 범위 — 모서리 통과를 막으려면 전부 봐야 한다.
            int crossLo = CellCoord(at - radius + kSkin, originCross, ts);
            int crossHi = CellCoord(at + radius - kSkin, originCross, ts);

            for (int m = startCoord; ; m += stepI)
            {
                for (int c = crossLo; c <= crossHi; c++)
                {
                    int2 cell = xAxis ? new int2(m, c) : new int2(c, m);
                    if (!nav.IsBlocked(cell)) continue;

                    // 막힌 타일의 진입면 바로 앞에 원 가장자리를 세운다.
                    float face = originMove + m * ts - dir * ts * 0.5f;
                    float stopped = face - dir * (radius + kSkin);

                    // 되돌아가지 않는다 — 이미 벽에 겹쳐 있어도(외력·텔레포트) 뒤로 튕기지 않고
                    // 제자리에 머문다. 결과는 항상 [from, to] 안이다.
                    return dir > 0f
                        ? math.clamp(stopped, from, to)
                        : math.clamp(stopped, to, from);
                }
                if (m == endCoord) break;
            }
            return to;
        }

        // GridMath.WorldToCellUnclamped 와 같은 라운딩 규칙(floor(v + 0.5))을 한 축에만 적용.
        // 클램프하지 않는다 — 경계 밖 좌표는 NavGrid.IsBlocked 가 막힘으로 판정해야 한다.
        private static int CellCoord(float world, float origin, float tileSize)
            => (int)math.floor((world - origin) / tileSize + 0.5f);
    }
}
