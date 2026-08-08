using Unity.Collections;
using Unity.Mathematics;

namespace Wassup.Battle.Movement
{
    // continuous-agent-movement unit 7 — 경로 평활화(string pulling).
    //
    // unit 4 로 L 자는 사라졌지만 방향이 8단계로 양자화돼 있어, 기울기가 45°가 아니면
    // 대각 구간과 직축 구간이 꺾여 붙는다("꺾인 빗변"). 진짜 직선은 여기서 나온다.
    //
    // flow field 는 **명시 경로를 주지 않는다.** 그래서 필드를 따라 앞으로 K 셀 전진시켜
    // 후보 지점을 만들고(D2-(a)), 그중 **벽 없이 보이는 가장 먼 지점**으로 직행한다.
    // 필드를 대체하는 게 아니라 필드 위에 얹는다 — 전역 필드를 버리면 오목 지형에서 갇힌다.
    //
    // 순수 함수. NavGrid 와 plain 값만 받는다.
    public static class PathSmoothing
    {
        // 전방 탐색 셀 수.
        //
        // 8 은 짧았다 — 실측(2026-08-08, 20x14 열린 격자 · 기울기 17:8)에서 총회전 32° 가
        // 남고 주행거리가 직선 대비 +1.4% 였다. 24 면 총회전 0°, 주행거리가 직선거리와 일치한다.
        // 그 이상(64)은 결과가 같다 — 가림이 없으면 어차피 골 근처에서 멈추기 때문이다.
        //
        // 비용: 가림을 만나면 즉시 끊기므로 복도에선 1~2 회에 끝난다. 열린 구간에서만
        // 길어지는데, 그때가 정확히 직선이 필요한 상황이다.
        public const int DefaultLookahead = 24;

        // 현재 위치에서 필드를 따라 K 셀 앞까지 훑어, 가시선이 뚫린 **가장 먼** 지점을 준다.
        // 반환 false = 쓸 만한 후보 없음(호출자는 기존 flow 방향을 그대로 쓴다).
        public static bool TryFurthestVisible(
            float3 from,
            in NavGrid nav,
            in NativeArray<float2> flow,
            float radius,
            int lookahead,
            out float3 target)
        {
            target = default;
            if (!flow.IsCreated) return false;

            int2 cell = GridMath.WorldToCell(from, nav.tileSize, nav.gridSize, origin: nav.origin);
            bool found = false;

            // 필드를 따라 전진하며 매 스텝의 셀 중심을 후보로 본다. 뒤에서 앞으로 가며
            // 보이는 것을 계속 갱신하므로, 루프 종료 시 `target` 은 가장 먼 가시 지점이다.
            for (int i = 0; i < lookahead; i++)
            {
                if (!nav.InBounds(cell)) break;
                float2 dir = flow[GridMath.CellIndex(cell, nav.gridSize)];
                int2 step = GridMath.FlowStep(dir);      // 대각 성분 반올림 — 단일 정의
                if (step.x == 0 && step.y == 0) break;   // 골 도착 또는 고립
                cell += step;

                float3 candidate = GridMath.CellToWorldCenter(cell, nav.tileSize, from.y, origin: nav.origin);

                // ⚠ 첫 후보(= 바로 다음 셀의 중심)는 **가시성과 무관하게 채택한다.**
                //
                // 필드는 셀 중심 기준의 방향을 준다. 유닛이 셀 안에서 한쪽으로 치우쳐 있으면
                // 그 방향으로는 몸이 안 들어갈 수 있는데, 방향 벡터에는 비켜설 성분이 없어
                // 영구 교착이 난다(장애물 모서리에 끼어 뒤에서 밀어야 빠지던 사고 —
                // 2026-08-08 사용자 제보. 셀 (8,7) 에서 flow=(0,1), 몸이 이웃 열을 침범).
                //
                // 다음 셀 중심은 **정의상 몸이 들어가는 자리**다(walkable 셀 + r < 타일/2).
                // 거기를 조준하면 방향에 측면 성분이 생겨 유닛이 통로 쪽으로 비켜서고,
                // 다음 프레임에 통과한다. 되돌리지 말 것 — 이게 교착을 막는 지점이다.
                if (i > 0 && !IsVisible(from, candidate, radius, in nav)) break;

                target = candidate;
                found = true;
            }
            return found;
        }

        // 두 점 사이가 반지름 r 의 원이 지나갈 만큼 뚫려 있는가.
        //
        // 선분을 일정 간격으로 샘플링해 각 지점에서 원이 벽에 겹치는지 본다. 간격은 반지름
        // 기반이라 어떤 셀도 건너뛰지 않는다. Bresenham 대신 이걸 쓰는 이유는 **반지름을
        // 함께 봐야** 하기 때문이다 — 선분만 뚫려 있고 몸통이 걸리는 통로로 직행하면
        // AgentCollision 이 매 프레임 막아 제자리 진동이 난다.
        public static bool IsVisible(float3 a, float3 b, float radius, in NavGrid nav)
        {
            float dx = b.x - a.x, dz = b.z - a.z;
            float len = math.sqrt(dx * dx + dz * dz);
            if (len < 1e-5f) return true;

            float step = math.max(0.1f, math.min(radius, nav.tileSize * 0.5f));
            int steps = (int)math.ceil(len / step);
            for (int i = 1; i <= steps; i++)
            {
                float t = (float)i / steps;
                float px = a.x + dx * t;
                float pz = a.z + dz * t;
                if (OverlapsBlocked(px, pz, radius, in nav)) return false;
            }
            return true;
        }

        // 중심 (px,pz), 반지름 r 의 원이 걸치는 셀들 중 막힌 것이 있는가.
        // r < tileSize 전제라 3x3 이웃이면 충분하다(AgentCollision 과 같은 전제).
        private static bool OverlapsBlocked(float px, float pz, float radius, in NavGrid nav)
        {
            int x0 = CellCoord(px - radius, nav.origin.x, nav.tileSize);
            int x1 = CellCoord(px + radius, nav.origin.x, nav.tileSize);
            int z0 = CellCoord(pz - radius, nav.origin.z, nav.tileSize);
            int z1 = CellCoord(pz + radius, nav.origin.z, nav.tileSize);

            for (int cz = z0; cz <= z1; cz++)
            for (int cx = x0; cx <= x1; cx++)
                if (nav.IsBlocked(new int2(cx, cz))) return true;
            return false;
        }

        private static int CellCoord(float world, float origin, float tileSize)
            => (int)math.floor((world - origin) / tileSize + 0.5f);
    }
}
