// salvaged from Assets/_Project/Scripts/Battle/Movement/PathSmoothing.cs (battle-core-rebuild unit 2)
// 이식 시 바뀐 것: `NativeArray<float2> flow` → `float2[] flow`, `IsCreated` → null 검사.
// 규칙(첫 후보 무조건 채택 · apex 2-키 선택 · 반지름 가시선)은 그대로다 — M26 은 「보류」라
// 여기서 재설계하지 않는다.
using Unity.Mathematics;

namespace Somnia.Battle.BattleCore.Map
{
    // 경로 평활화(string pulling).
    //
    // 흐름장은 방향이 8단계로 양자화돼 있어 기울기가 45°가 아니면 대각 구간과 직축 구간이
    // 꺾여 붙는다("꺾인 빗변"). 진짜 직선은 여기서 나온다.
    //
    // 흐름장은 **명시 경로를 주지 않는다.** 그래서 필드를 따라 앞으로 K 칸 전진시켜 후보
    // 지점을 만들고, 그중 **벽 없이 보이는 가장 먼 지점**으로 직행한다. 필드를 대체하는 게
    // 아니라 필드 위에 얹는다 — 전역 필드를 버리면 오목 지형에서 갇힌다.
    //
    // ⚠ 조준 진동은 **미해소**다(M26 보류). 「왕복이 구조적으로 불가능」은 거짓이고 참인 것은
    // 좁다 — `(차단 칸, 코너)` 쌍이 같으면 나오는 좌표가 관찰자와 무관하다는 것뿐이다.
    // 기둥이 흩어진 구역에서는 여전히 진동한다(13.4칸 주행에 총회전 1161° 실측).
    public static class PathSmoothing
    {
        // 전방 탐색 칸 수.
        //
        // 8 은 짧았다 — 실측(20×14 열린 격자 · 기울기 17:8)에서 총회전 32° 가 남고 주행거리가
        // 직선 대비 +1.4% 였다. 24 면 총회전 0°, 주행거리가 직선거리와 일치한다. 그 이상(64)은
        // 결과가 같다 — 가림이 없으면 어차피 목적지 근처에서 멈춘다.
        public const int DefaultLookahead = 24;

        // 현재 위치에서 필드를 따라 K 칸 앞까지 훑어, 가시선이 뚫린 가장 먼 지점 —
        // 막혔다면 막은 코너의 오프셋 꼭짓점 — 을 준다.
        // 반환 false = 쓸 만한 후보 없음(호출자는 기존 흐름 방향을 그대로 쓴다).
        public static bool TryFurthestVisible(float3 from, in NavGrid nav, float2[] flow,
                                              float radius, int lookahead, out float3 target)
        {
            target = default;
            if (flow == null) return false;

            int2 cell = GridMath.WorldToCell(from, nav.TileSize, nav.GridSize, nav.Origin);
            bool found = false;

            for (int i = 0; i < lookahead; i++)
            {
                if (!nav.InBounds(cell)) break;
                float2 dir = flow[GridMath.CellIndex(cell, nav.GridSize)];
                int2 step = GridMath.FlowStep(dir);      // 대각 성분 반올림 — 단일 정의
                if (step.x == 0 && step.y == 0) break;   // 도착 또는 고립
                cell += step;

                float3 candidate = GridMath.CellToWorldCenter(cell, nav.TileSize, from.y, nav.Origin);

                // ⚠ 첫 후보(= 바로 다음 칸의 중심)는 **가시성과 무관하게 채택한다.**
                //
                // 필드는 칸 중심 기준의 방향을 준다. 유닛이 칸 안에서 한쪽으로 치우쳐 있으면
                // 그 방향으로는 몸이 안 들어갈 수 있는데, 방향 벡터에는 비켜설 성분이 없어
                // 영구 교착이 난다(장애물 모서리에 끼어 뒤에서 밀어야 빠지던 사고).
                // 다음 칸 중심은 정의상 몸이 들어가는 자리다. 되돌리지 말 것.
                if (i > 0 && TryFindFirstBlocked(from, candidate, radius, in nav, out int2 blocker))
                {
                    float3 lastVisible = found ? target : from;
                    if (TryCornerAim(blocker, lastVisible, from, radius, in nav, out float3 cornerAim)
                        && math.distancesq(cornerAim, from) > 1e-4f)
                    {
                        target = cornerAim;
                        found = true;
                    }
                    break;   // 코너가 이번 틱의 한계 — 지나면 다음 틱에 열린다
                }

                target = candidate;
                found = true;
            }
            return found;
        }

        // 이동과 예고 라인이 공유하는 「다음 목표점」 규칙 — 갈라지면 「라인 ≠ 이동선」 부류가
        // 재발한다. 평활화 성공 → 그 점. 실패 → 필드 한 스텝의 칸 중심. 도착·고립 → false.
        public static bool TryStepTarget(float3 pos, in NavGrid nav, float2[] flow,
                                         float radius, int lookahead, out float3 target)
        {
            if (TryFurthestVisible(pos, in nav, flow, radius, lookahead, out target)
                && math.distancesq(target, pos) > 1e-6f)
                return true;

            target = default;
            if (flow == null) return false;
            int2 cell = GridMath.WorldToCell(pos, nav.TileSize, nav.GridSize, nav.Origin);
            if (!nav.InBounds(cell)) return false;
            int2 step = GridMath.FlowStep(flow[GridMath.CellIndex(cell, nav.GridSize)]);
            if (step.x == 0 && step.y == 0) return false;
            target = GridMath.CellToWorldCenter(cell + step, nav.TileSize, pos.y, nav.Origin);
            return true;
        }

        // 두 점 사이가 반지름 r 의 몸이 지나갈 만큼 뚫려 있는가.
        //
        // 선분만 보지 않고 **반지름을 함께 보는** 이유: 선분만 뚫려 있고 몸통이 걸리는 통로로
        // 직행하면 `AgentCollision` 이 매 틱 막아 제자리 진동이 난다.
        //
        // 프로덕션은 이걸 부르지 않는다(평활화는 차단 칸까지 필요해 아래 private 을 쓴다).
        // 그래도 남긴다 — 이 술어가 곧 가시선 계약이고 테스트가 그것을 직접 못박는다.
        public static bool IsVisible(float3 a, float3 b, float radius, in NavGrid nav)
            => !TryFindFirstBlocked(a, b, radius, in nav, out _);

        private static bool TryFindFirstBlocked(float3 a, float3 b, float radius, in NavGrid nav,
                                                out int2 blockedCell)
        {
            blockedCell = default;
            float dx = b.x - a.x, dz = b.z - a.z;
            float len = math.sqrt(dx * dx + dz * dz);
            if (len < 1e-5f) return false;

            float step = math.max(0.1f, math.min(radius, nav.TileSize * 0.5f));
            int steps = (int)math.ceil(len / step);
            for (int i = 1; i <= steps; i++)
            {
                float t = (float)i / steps;
                float px = a.x + dx * t;
                float pz = a.z + dz * t;
                if (TryFirstOverlapBlocked(px, pz, radius, in nav, out blockedCell)) return true;
            }
            return false;
        }

        // 차단 칸 B 의 4꼭짓점 중 하나를 골라, B 중심 반대 방향으로 (반지름+skin) 오프셋한
        // 조준점을 만든다.
        //
        // funnel 의 apex 선택 — 1차 키: **마지막 가시점에 최근접**(가시 구간이 꺾이는 지점),
        // 2차 키(동률): **에이전트에 최근접**. 두 키가 모두 필요하다는 것이 각각 실측됐다:
        //  · 1차 키를 「에이전트 최근접」으로 하면 이미 돌아 나온 코너를 뒤로 다시 조준해 동결된다.
        //  · 2차 키 없이 1차 키만 쓰면 동률이 스캔 순서로 벽 건너편에 풀려 0.1배속 크리프가 난다.
        //
        // 반환 false = 오프셋 점에 몸이 안 들어감(좁은 대각 틈) — 호출자는 폴백.
        public static bool TryCornerAim(int2 blockedCell, float3 lastVisible, float3 agentPos,
                                        float radius, in NavGrid nav, out float3 aim)
        {
            float ts = nav.TileSize;
            float cx = nav.Origin.x + blockedCell.x * ts;
            float cz = nav.Origin.z + blockedCell.y * ts;
            float half = ts * 0.5f;

            float bestD = float.MaxValue, bestA = float.MaxValue;
            float2 bestCorner = default, bestSign = default;
            for (int sx = -1; sx <= 1; sx += 2)
            for (int sz = -1; sz <= 1; sz += 2)
            {
                var corner = new float2(cx + sx * half, cz + sz * half);
                float d = math.distancesq(new float2(lastVisible.x, lastVisible.z), corner);
                float a = math.distancesq(new float2(agentPos.x, agentPos.z), corner);
                bool better = d < bestD - 1e-6f
                              || (math.abs(d - bestD) <= 1e-6f && a < bestA - 1e-6f);
                if (better)
                {
                    bestD = d; bestA = a;
                    bestCorner = corner;
                    bestSign = new float2(sx, sz);
                }
            }

            float off = radius + AgentCollision.Skin;   // M12 — 충돌 여유와 **같은 값**
            var p = bestCorner + bestSign * off;
            aim = new float3(p.x, agentPos.y, p.y);
            return !TryFirstOverlapBlocked(p.x, p.y, radius, in nav, out _);
        }

        // 중심 (px,pz), 반지름 r 의 몸이 걸치는 칸들 중 막힌 것이 있으면 그 첫 칸을 준다.
        // r < tileSize 전제라 3×3 이웃이면 충분하다(`AgentCollision` 과 같은 전제).
        private static bool TryFirstOverlapBlocked(float px, float pz, float radius, in NavGrid nav,
                                                   out int2 blockedCell)
        {
            int x0 = CellCoord(px - radius, nav.Origin.x, nav.TileSize);
            int x1 = CellCoord(px + radius, nav.Origin.x, nav.TileSize);
            int z0 = CellCoord(pz - radius, nav.Origin.z, nav.TileSize);
            int z1 = CellCoord(pz + radius, nav.Origin.z, nav.TileSize);

            for (int cz = z0; cz <= z1; cz++)
            for (int cx = x0; cx <= x1; cx++)
            {
                var cell = new int2(cx, cz);
                if (nav.IsBlocked(cell)) { blockedCell = cell; return true; }
            }
            blockedCell = default;
            return false;
        }

        private static int CellCoord(float world, float origin, float tileSize)
            => (int)math.floor((world - origin) / tileSize + 0.5f);
    }
}
