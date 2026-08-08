using Unity.Mathematics;

namespace Wassup.Battle.Movement
{
    // continuous-agent-movement unit 3·11 — 에이전트 vs 벽 타일 충돌 + 접선 슬라이드.
    //
    // 이전(MovementCellTrim.Apply)은 유닛을 점으로 보고 "다음 위치가 벽 셀이면 현재 셀
    // 경계로 clamp" 했다. 그래서 벽에 비스듬히 부딪히면 미끄러지지 않고 그 축이 통째로
    // 막히고 코너에서 걸렸다 — 격자가 눈에 보이는 가장 큰 원인.
    //
    // ⚠ 명명 정정(감사 지적) — 판정 형상은 **원이 아니라 변 2r 의 축정렬 박스(AABB)** 다.
    // 아래 cross 범위(`at ± radius`)도 면 정지도 전부 박스 판정이다. 이 오기가 중요한 이유:
    // 박스 에이전트 × 박스 장애물의 민코프스키 합이 박스이므로 `corner ± (r+skin)`(PathSmoothing
    // 의 apex 오프셋)이 **근사가 아니라 정확한 C-space 꼭짓점**이라는 사실이 가려진다.
    //
    // 순수 함수. NavGrid(프레임 뷰)와 plain 값만 받는다.
    public static class AgentCollision
    {
        // 경계에 정확히 붙으면 다음 프레임 셀 판정이 흔들린다. 살짝 띄운다
        // (MovementCellTrim.kBoundaryEpsilon 과 같은 성격).
        // unit 10 — PathSmoothing 의 코너 꼭짓점 오프셋도 같은 값을 쓴다: 조준점과 충돌
        // 해결이 같은 여유를 가져야 "조준한 자리에 실제로 설 수 있다"가 성립한다.
        public const float Skin = 1e-3f;
        private const float kSkin = Skin;

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
            return PreserveTangentialSpeed(current, desired, new float3(x, desired.y, z), radius, in nav);
        }

        // unit 11 — **접선 속도 보존.** 축 clamp 만 하면 막힌 축의 성분이 그냥 버려져서
        // 실이동이 `speed · sinθ`(θ = 진행방향과 벽면이 이루는 각)로 붕괴한다.
        //
        // 실측 사고(2026-08-09): 좁은 통로 앞에서 조준이 통로 건너편을 가리켜 방향이 거의
        // 순수 벽 법선이 됐다. θ ≈ 0.9° → 실이동 0.0005 / 요청 0.0333 = **정상 속도의 1.5%**.
        // 유닛이 ~1초간 벽을 긁으며 기어갔고 뒤에서 밀려야 빠져나왔다.
        // ⚠ 그때 **Z 축은 전혀 안 막혀 있었다**(요청량 100% 통과). 느린 원인은 충돌이 아니라
        // "애초에 z 를 0.0005 밖에 요청하지 않은 것" 이다 — 이 함수가 고치는 지점이 정확히 그것.
        //
        // 처리: 막혀서 잃은 만큼을 **살아남은 방향으로 재분배**해 프레임 변위 크기를 요청량까지
        // 복원한다. 결과는 "벽을 따라 전속으로 미끄러짐" 이고, 그게 브롤스타즈류 감각의 실체다.
        //
        // 재분배분도 다시 충돌 해결을 태운다(다른 벽에 부딪힐 수 있다). 재귀는 1회로 끝낸다 —
        // 반복하면 코너에서 진동하고 프레임당 비용이 불정해진다.
        //
        // 요청량을 **넘지 않는다**: 재분배 예산은 잃은 양뿐이라 총 변위 ≤ 원래 요청 크기다.
        // 이 상한이 없으면 코너에서 순간 가속이 생긴다.
        private static float3 PreserveTangentialSpeed(
            float3 current, float3 desired, float3 resolved, float radius, in NavGrid nav)
        {
            float wantX = desired.x - current.x, wantZ = desired.z - current.z;
            float wantLen = math.sqrt(wantX * wantX + wantZ * wantZ);
            if (wantLen < 1e-6f) return resolved;

            float gotX = resolved.x - current.x, gotZ = resolved.z - current.z;
            bool xBlocked = math.abs(gotX) < math.abs(wantX) - 1e-6f;
            bool zBlocked = math.abs(gotZ) < math.abs(wantZ) - 1e-6f;

            // 정확히 한 축만 막혔을 때만 재분배한다.
            //  · 둘 다 자유 = 잃은 게 없다
            //  · 둘 다 막힘 = 접선이 없다(코너에 정면으로 박힘) — 억지로 방향을 만들지 않는다
            if (xBlocked == zBlocked) return resolved;

            // 자유 축의 목표 성분: |최종 변위| == |요청 변위| 가 되도록 정확히 푼다.
            //   free² + blocked² = wantLen²  →  free = √(wantLen² − blocked²)
            // 잃은 크기를 그냥 자유 축에 더하면(‖want‖−‖got‖) 과소 복원된다 — 막힌 축이
            // 부분 통과한 몫이 두 번 세어지기 때문이다.
            float blocked = xBlocked ? gotX : gotZ;
            float freeMag = math.sqrt(math.max(0f, wantLen * wantLen - blocked * blocked));
            float freeWant = xBlocked ? wantZ : wantX;
            float freeTarget = math.sign(freeWant) * freeMag;   // 접선 의도가 없으면(sign 0) 이동 0

            float3 slideTo = xBlocked
                ? new float3(resolved.x, desired.y, current.z + freeTarget)
                : new float3(current.x + freeTarget, desired.y, resolved.z);

            // 재분배분도 충돌 해결을 다시 태운다(다른 벽에 부딪힐 수 있다).
            float sx = ResolveAxis(resolved.x, slideTo.x, resolved.z, radius, in nav, xAxis: true);
            float sz = ResolveAxis(resolved.z, slideTo.z, sx,         radius, in nav, xAxis: false);
            return new float3(sx, desired.y, sz);
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
