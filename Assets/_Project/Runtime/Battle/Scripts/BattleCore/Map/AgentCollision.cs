// salvaged from Assets/_Project/Scripts/Battle/Movement/AgentCollision.cs (battle-core-rebuild unit 2)
// 이식 시 바뀐 것: 없음(타입 네임스페이스만). 순수 수학이라 그대로 옮겼다.
using Unity.Mathematics;

namespace Somnia.Battle.BattleCore.Map
{
    // 에이전트 vs 벽 칸 충돌 + 접선 슬라이드.
    //
    // ⚠ 판정 형상은 **원이 아니라 변 2r 의 축정렬 박스(AABB)** 다. 아래 cross 범위
    // (`at ± radius`)도 면 정지도 전부 박스 판정이다. 이 명명이 중요한 이유: 박스 에이전트 ×
    // 박스 장애물의 민코프스키 합이 박스이므로 `corner ± (r+skin)`(`PathSmoothing` 의 apex
    // 오프셋)이 **근사가 아니라 정확한 C-space 꼭짓점**이라는 사실이 가려진다.
    public static class AgentCollision
    {
        // 경계에 정확히 붙으면 다음 틱 칸 판정이 흔들린다. 살짝 띄운다.
        // ⚠ **`PathSmoothing` 의 코너 꼭짓점 오프셋과 같은 값이어야 한다**(M12) — 조준점과
        // 충돌 해결이 같은 여유를 가져야 「조준한 자리에 실제로 설 수 있다」가 성립한다.
        public const float Skin = 1e-3f;

        public static float3 Resolve(float3 current, float3 desired, float radius, in NavGrid nav)
        {
            if (radius <= 0f)
            {
                int2 currentCell = GridMath.WorldToCell(current, nav.TileSize, nav.GridSize, nav.Origin);
                return MovementCellTrim.Apply(desired, currentCell, in nav);
            }

            // 축 분리 해결 — X 를 먼저 풀고 그 결과 위치에서 Z 를 푼다.
            // 이 순서가 슬라이드를 공짜로 만든다: 막힌 축만 멈추고 자유로운 축은 계속 간다.
            float x = ResolveAxis(current.x, desired.x, current.z, radius, in nav, xAxis: true);
            float z = ResolveAxis(current.z, desired.z, x, radius, in nav, xAxis: false);
            return PreserveTangentialSpeed(current, desired, new float3(x, desired.y, z), radius, in nav);
        }

        // **접선 속도 보존.** 축 clamp 만 하면 막힌 축의 성분이 그냥 버려져서 실이동이
        // `speed · sinθ`(θ = 진행방향과 벽면이 이루는 각)로 붕괴한다.
        //
        // 실측 사고: 좁은 통로 앞에서 조준이 통로 건너편을 가리켜 방향이 거의 순수 벽 법선이
        // 됐다. θ ≈ 0.9° → 실이동 0.0005 / 요청 0.0333 = **정상 속도의 1.5%**. 유닛이 ~1초간
        // 벽을 긁으며 기어갔다. ⚠ 그때 Z 축은 전혀 안 막혀 있었다 — 느린 원인은 충돌이 아니라
        // 「애초에 z 를 0.0005 밖에 요청하지 않은 것」이고, 이 함수가 고치는 지점이 정확히 그것이다.
        //
        // 재분배분도 다시 충돌 해결을 태운다(다른 벽에 부딪힐 수 있다). 재귀는 1회로 끝낸다 —
        // 반복하면 코너에서 진동하고 틱당 비용이 불정해진다. 총 변위는 요청량을 넘지 않는다.
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

            //   free² + blocked² = wantLen²  →  free = √(wantLen² − blocked²)
            // 잃은 크기를 그냥 자유 축에 더하면 과소 복원된다 — 막힌 축이 부분 통과한 몫이
            // 두 번 세어지기 때문이다.
            float blocked = xBlocked ? gotX : gotZ;
            float freeMag = math.sqrt(math.max(0f, wantLen * wantLen - blocked * blocked));
            float freeWant = xBlocked ? wantZ : wantX;
            float freeTarget = math.sign(freeWant) * freeMag;   // 접선 의도가 없으면(sign 0) 이동 0

            float3 slideTo = xBlocked
                ? new float3(resolved.x, desired.y, current.z + freeTarget)
                : new float3(current.x + freeTarget, desired.y, resolved.z);

            float sx = ResolveAxis(resolved.x, slideTo.x, resolved.z, radius, in nav, xAxis: true);
            float sz = ResolveAxis(resolved.z, slideTo.z, sx, radius, in nav, xAxis: false);
            return new float3(sx, desired.y, sz);
        }

        // from → to 로 한 축을 움직인다. `at` 은 반대축 위치(몸이 걸치는 범위를 여기서 구한다).
        //
        // 전진 가장자리가 지나가는 **모든** 칸 열/행을 진행 순서대로 훑는다. 최종 위치만
        // 검사하면 중간 칸을 건너뛴다: 에이전트가 칸 경계에 있고 외력으로 전속 이동하면
        // 가장자리가 최대 `0.5 + 0.9 + r` 칸까지 가서 벽 칸 하나를 지나쳐 그 너머 빈 칸에
        // 도달할 수 있다. 스윕이 그 구멍을 막는다.
        private static float ResolveAxis(float from, float to, float at, float radius, in NavGrid nav, bool xAxis)
        {
            float delta = to - from;
            if (math.abs(delta) < 1e-9f) return from;

            float ts = nav.TileSize;
            float dir = math.sign(delta);
            float originMove = xAxis ? nav.Origin.x : nav.Origin.z;
            float originCross = xAxis ? nav.Origin.z : nav.Origin.x;

            int startCoord = CellCoord(from + dir * radius, originMove, ts);
            int endCoord = CellCoord(to + dir * radius, originMove, ts);
            int stepI = dir > 0f ? 1 : -1;

            // 몸이 걸치는 반대축 칸 범위 — 모서리 통과를 막으려면 전부 봐야 한다.
            int crossLo = CellCoord(at - radius + Skin, originCross, ts);
            int crossHi = CellCoord(at + radius - Skin, originCross, ts);

            for (int m = startCoord; ; m += stepI)
            {
                for (int c = crossLo; c <= crossHi; c++)
                {
                    int2 cell = xAxis ? new int2(m, c) : new int2(c, m);
                    if (!nav.IsBlocked(cell)) continue;

                    float face = originMove + m * ts - dir * ts * 0.5f;
                    float stopped = face - dir * (radius + Skin);

                    // 되돌아가지 않는다 — 이미 벽에 겹쳐 있어도(외력·순간이동) 뒤로 튕기지
                    // 않고 제자리에 머문다. 결과는 항상 [from, to] 안이다.
                    return dir > 0f
                        ? math.clamp(stopped, from, to)
                        : math.clamp(stopped, to, from);
                }
                if (m == endCoord) break;
            }
            return to;
        }

        // `GridMath.WorldToCellUnclamped` 와 같은 라운딩 규칙을 한 축에만 적용.
        // 클램프하지 않는다 — 경계 밖 좌표는 `NavGrid.IsBlocked` 가 막힘으로 판정해야 한다.
        private static int CellCoord(float world, float origin, float tileSize)
            => (int)math.floor((world - origin) / tileSize + 0.5f);
    }
}
