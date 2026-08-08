using Unity.Collections;
using Unity.Mathematics;

namespace Wassup.Battle.Movement
{
    // continuous-agent-movement unit 7·10 — 경로 평활화(string pulling).
    //
    // unit 4 로 L 자는 사라졌지만 방향이 8단계로 양자화돼 있어, 기울기가 45°가 아니면
    // 대각 구간과 직축 구간이 꺾여 붙는다("꺾인 빗변"). 진짜 직선은 여기서 나온다.
    //
    // flow field 는 **명시 경로를 주지 않는다.** 그래서 필드를 따라 앞으로 K 셀 전진시켜
    // 후보 지점을 만들고(D2-(a)), 그중 **벽 없이 보이는 가장 먼 지점**으로 직행한다.
    // 필드를 대체하는 게 아니라 필드 위에 얹는다 — 전역 필드를 버리면 오목 지형에서 갇힌다.
    //
    // unit 10 — 가시선이 막히면 조준 대상을 "직전 셀 중심"이 아니라 **막은 벽 타일의
    // 코너 꼭짓점(+반지름 오프셋)** 으로 바꾼다. 셀 중심 + 움직이는 관찰자는 코너에서
    // 가시성 판정이 매 프레임 뒤집혀 조준이 왕복했다(6.5타일↔0.9타일, 헤딩 ±20~85° —
    // 2026-08-08 실측).
    //
    // ⚠ 정정(2026-08-09 감사) — 이전 주석은 "왕복이 **구조적으로 불가능**하다"고 썼다.
    // **거짓이다.** 참인 것은 그보다 좁다: `(차단 셀, 코너)` 쌍이 **같으면** 나오는 좌표가
    // 관찰자와 무관하다는 것뿐이다. 그 쌍 자체는 관찰자가 고른다 — 어느 셀이 먼저 가림으로
    // 걸리는지, 두 코너 중 어느 쪽을 쓰는지가 위치에 의존한다(아래 apex 2-키 선택). 즉
    // 왕복은 **구조적으로 배제된 게 아니라 억제**됐고, 억제가 약한 지형이 실제로 남아 있다 —
    // 기둥이 흩어진 구역에서 조준이 여전히 진동한다(MovementStress 스폰 (12,2): 13.4타일
    // 주행에 헤딩 45°↑ 전환 6회 · 총회전 1161°, 2026-08-09 실측). 해소는 조준 기준을
    // "레이가 처음 막히는 지점"에서 **전역 비용 최소**로 바꾸는 별도 작업 소관이다.
    //
    // 순수 함수. NavGrid 와 plain 값만 받는다.
    //
    // 공개 표면 규칙 — **프로덕션 진입점은 `TryStepTarget` 하나다**(MovementSystem·BattleBridge).
    // `TryFurthestVisible`·`TryCornerAim`·`IsVisible` 은 그 내부 단계이면서, 동시에 이 spec 의
    // 계약(가장 먼 가시점 / 코너 apex 선택 / 반지름 가시선)을 테스트가 직접 못박는 표면이다.
    // 프로덕션 호출자가 0 이라는 이유로 지우지 말 것 — 지우면 회귀 검증이 통째로 사라진다.
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

        // 현재 위치에서 필드를 따라 K 셀 앞까지 훑어, 가시선이 뚫린 가장 먼 지점 —
        // 막혔다면 막은 코너의 오프셋 꼭짓점 — 을 준다.
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
                // 2026-08-08 사용자 제보). 다음 셀 중심은 정의상 몸이 들어가는 자리다
                // (walkable 셀 + r < 타일/2). 되돌리지 말 것 — 이게 교착을 막는 지점이다.
                if (i > 0 && TryFindFirstBlocked(from, candidate, radius, in nav, out int2 blocker))
                {
                    // unit 10 — 가시 구간이 꺾이는 코너(apex)의 오프셋 꼭짓점을 조준한다.
                    // 오프셋이 다른 벽 안에 떨어지면(좁은 대각 틈) 마지막 가시 후보 유지.
                    // 자기 위치와 겹치면(이미 코너 위) 조준 의미가 없어 역시 유지.
                    float3 lastVisible = found ? target : from;
                    if (TryCornerAim(blocker, lastVisible, from, radius, in nav, out float3 cornerAim)
                        && math.distancesq(cornerAim, from) > 1e-4f)
                    {
                        target = cornerAim;
                        found = true;
                    }
                    break;   // 코너가 이번 프레임의 한계 — 지나면 다음 프레임에 열린다
                }

                target = candidate;
                found = true;
            }
            return found;
        }

        // 이동(MovementSystem)과 예고 라인(BattleBridge)이 공유하는 "다음 목표점" 규칙 —
        // 갈라지면 "라인 ≠ 이동선" 부류의 버그가 재발한다(2026-08-08). 호출처 2곳 +
        // sim-critical 이라 제약 10 의 추출 조건을 충족한다.
        // 평활화 성공 → 그 점. 실패 → 필드 한 스텝의 셀 중심. 골·고립 → false.
        public static bool TryStepTarget(
            float3 pos,
            in NavGrid nav,
            in NativeArray<float2> flow,
            float radius,
            int lookahead,
            out float3 target)
        {
            if (TryFurthestVisible(pos, in nav, in flow, radius, lookahead, out target)
                && math.distancesq(target, pos) > 1e-6f)
                return true;

            target = default;
            if (!flow.IsCreated) return false;
            int2 cell = GridMath.WorldToCell(pos, nav.tileSize, nav.gridSize, origin: nav.origin);
            if (!nav.InBounds(cell)) return false;
            int2 step = GridMath.FlowStep(flow[GridMath.CellIndex(cell, nav.gridSize)]);
            if (step.x == 0 && step.y == 0) return false;
            target = GridMath.CellToWorldCenter(cell + step, nav.tileSize, pos.y, origin: nav.origin);
            return true;
        }

        // 두 점 사이가 반지름 r 의 원이 지나갈 만큼 뚫려 있는가.
        //
        // 선분을 일정 간격으로 샘플링해 각 지점에서 원이 벽에 겹치는지 본다. 간격은 반지름
        // 기반이라 어떤 셀도 건너뛰지 않는다. Bresenham 대신 이걸 쓰는 이유는 **반지름을
        // 함께 봐야** 하기 때문이다 — 선분만 뚫려 있고 몸통이 걸리는 통로로 직행하면
        // AgentCollision 이 매 프레임 막아 제자리 진동이 난다.
        //
        // 프로덕션은 이걸 부르지 않는다(평활화는 차단 셀까지 필요해 TryFindFirstBlocked 를 쓴다).
        // 그래도 남긴다 — 이 술어가 곧 가시선 계약이고, PathSmoothingTests 의 IsVisible_* 케이스가
        // 그 계약(특히 "선분은 뚫려도 몸통이 걸리면 막힘")을 직접 검증한다.
        public static bool IsVisible(float3 a, float3 b, float radius, in NavGrid nav)
            => !TryFindFirstBlocked(a, b, radius, in nav, out _);

        // IsVisible 과 같은 판정이되, 처음 걸린 막힌 셀을 알려준다(unit 10 꼭짓점 조준 입력).
        // 샘플 순서(가까운 쪽부터)와 셀 스캔 순서가 고정이라 결과가 결정론적이다.
        // 클래스 안에서만 쓴다 — 외부에 필요한 건 가시 여부뿐이라 IsVisible 이 그 창구다.
        private static bool TryFindFirstBlocked(
            float3 a, float3 b, float radius, in NavGrid nav, out int2 blockedCell)
        {
            blockedCell = default;
            float dx = b.x - a.x, dz = b.z - a.z;
            float len = math.sqrt(dx * dx + dz * dz);
            if (len < 1e-5f) return false;

            float step = math.max(0.1f, math.min(radius, nav.tileSize * 0.5f));
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

        // unit 10 — 차단 셀 B 의 4꼭짓점 중 **에이전트에 가장 가까운 것**(= 지금 돌아야 할
        // 코너)을 골라, B 중심 반대 방향으로 (반지름+skin) 오프셋한 조준점을 만든다.
        //
        // funnel 의 apex 선택 — 1차 키: **마지막 가시점에 최근접**(가시 구간이 꺾이는 지점),
        // 2차 키(동률): **에이전트에 최근접**. 두 키가 모두 필요하다는 것이 각각 실측됐다
        // (2026-08-09):
        //  · 1차 키를 "에이전트 최근접"으로 하면, 이미 돌아 나온 코너를 뒤로 다시 조준해
        //    그 자리에 동결된다(장애물 언더사이드 — 전방 apex 를 버리고 후방 코너를 봄).
        //  · 2차 키 없이 1차 키만 쓰면 동률(두 코너가 꺾임점에서 등거리)이 스캔 순서로
        //    벽 건너편에 풀려, 방향이 막힌 축 위주가 되어 0.1배속 크리프(아레나 입구
        //    164프레임 정체).
        //
        // 반환 false = 오프셋 점에 반지름 원이 안 들어감(좁은 대각 틈) — 호출자는 폴백.
        public static bool TryCornerAim(
            int2 blockedCell, float3 lastVisible, float3 agentPos, float radius, in NavGrid nav, out float3 aim)
        {
            float ts = nav.tileSize;
            float cx = nav.origin.x + blockedCell.x * ts;
            float cz = nav.origin.z + blockedCell.y * ts;
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

            float off = radius + AgentCollision.Skin;
            var p = bestCorner + bestSign * off;
            aim = new float3(p.x, agentPos.y, p.y);
            return !TryFirstOverlapBlocked(p.x, p.y, radius, in nav, out _);
        }

        // 중심 (px,pz), 반지름 r 의 원이 걸치는 셀들 중 막힌 것이 있으면 그 첫 셀을 준다.
        // r < tileSize 전제라 3x3 이웃이면 충분하다(AgentCollision 과 같은 전제).
        private static bool TryFirstOverlapBlocked(
            float px, float pz, float radius, in NavGrid nav, out int2 blockedCell)
        {
            int x0 = CellCoord(px - radius, nav.origin.x, nav.tileSize);
            int x1 = CellCoord(px + radius, nav.origin.x, nav.tileSize);
            int z0 = CellCoord(pz - radius, nav.origin.z, nav.tileSize);
            int z1 = CellCoord(pz + radius, nav.origin.z, nav.tileSize);

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
