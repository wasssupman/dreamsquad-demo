using System.Collections.Generic;

namespace Wassup.Sim.Combat
{
    /// <summary>
    /// battle-sim-extraction unit 18-J/4 — 도약 착지점 수학. 구 `BlinkMath` 이식.
    ///
    /// **NaN 없음·종료 보장이 구조로 성립한다** — 퇴화 방향과 무한 링 탐색 둘 다 여기서 닫히고
    /// 호출부는 그것을 알 필요가 없다.
    /// </summary>
    public static class BlinkMath
    {
        /// <summary>
        /// 퇴화 방향 폴백 축(월드 -Z). ⚠ **컴파일 타임 상수인 것이 의도다** — 속도·경로 전방
        /// 같은 런타임 파생 축은 그 자체가 0/미정의일 수 있어 NaN 을 다시 들여온다.
        /// </summary>
        private static readonly SimVec3 FallbackAxis = new SimVec3(0f, 0f, -1f);

        /// <summary>
        /// 리더를 **지나쳐** 1타일. 방향이 epsilon 보다 짧으면(도약자가 리더 위/옆) 상수 축으로
        /// 폴백한다. Y 는 리더 값을 통과시킨다 — 소비자가 어차피 자기 Y 를 유지한다.
        /// </summary>
        public static SimVec3 OffsetDest(SimVec3 leaderPos, SimVec3 bossPos, float tileSize)
        {
            SimVec3 raw = leaderPos - bossPos;
            SimVec3 dir = new SimVec3(raw.x, 0f, raw.z);
            float lenSq = SimMath.LengthSq(dir);
            SimVec3 axis = lenSq < 1e-6f ? FallbackAxis : dir * SimMath.Rsqrt(lenSq);
            return leaderPos + axis * tileSize;
        }

        /// <summary>
        /// `desired` 에서 가장 가까운, **흐름장이 도달할 수 있는** 착지 셀
        /// (`dist != int.MaxValue` = walkable ∧ 연결 — 봉인된 포켓에 떨어지지 않는다).
        ///
        /// Chebyshev 링 `r = 0..maxRingRadius`, 링 안에서는 row-major — **완전 결정적**이다.
        /// 상한 안에 후보가 없으면 false(호출부가 skip 하고 임계 래치는 소모된 채 남는다).
        /// </summary>
        public static bool TryFindLandingCell(
            SimInt2 desired, int[] dist, SimInt2 gridSize, int maxRingRadius, out SimInt2 landing)
        {
            for (int r = 0; r <= maxRingRadius; r++)
            {
                for (int dy = -r; dy <= r; dy++)
                {
                    for (int dx = -r; dx <= r; dx++)
                    {
                        if (SimMath.Max(SimMath.Abs(dx), SimMath.Abs(dy)) != r) continue; // 링 껍질만
                        var c = new SimInt2(desired.x + dx, desired.y + dy);
                        if (c.x < 0 || c.y < 0 || c.x >= gridSize.x || c.y >= gridSize.y) continue;
                        if (dist[c.y * gridSize.x + c.x] == int.MaxValue) continue;
                        landing = c;
                        return true;
                    }
                }
            }
            landing = default;
            return false;
        }
    }

    /// <summary>
    /// battle-sim-extraction unit 18-J/4 — 착지 앵커 = **방어유닛이 가장 많이 모인 셀**.
    /// 구 `DefenderDensity` 이식.
    ///
    /// ⚠ **동점 tie-break 는 row-major 셀 키(y·w + x) 오름차순**이다. 순회 순서에 의존하면 같은
    /// 배치에서 프레임마다 다른 셀이 뽑혀 결정론이 깨진다.
    ///
    /// `radius &lt;= 0` 은 "자기 셀만" 으로 취급한다. 후보가 없으면(방어유닛 전멸) false —
    /// 호출부가 skip 하고 임계 래치는 **소모된 채** 남는다.
    /// </summary>
    public static class DefenderDensity
    {
        public static bool TryFindDensestCell(
            List<SimInt2> defenderCells, int radius, SimInt2 gridSize, out SimInt2 densest, out int count)
        {
            densest = default;
            count = 0;
            if (defenderCells == null || defenderCells.Count == 0) return false;

            int r = SimMath.Max(0, radius);
            int bestCount = -1;
            long bestKey = long.MaxValue;

            for (int i = 0; i < defenderCells.Count; i++)
            {
                SimInt2 c = defenderCells[i];
                int n = 0;
                for (int j = 0; j < defenderCells.Count; j++)
                    if (TileAoe.IsInTileRange(defenderCells[j], c, r)) n++;

                // ⚠ 그리드 밖 좌표가 들어와도 **단조 순서만** 필요하므로 클램프하지 않는다
                //   (음수 x 도 순서가 정의된다).
                long key = (long)c.y * SimMath.Max(1, gridSize.x) + c.x;
                if (n > bestCount || (n == bestCount && key < bestKey))
                {
                    bestCount = n;
                    bestKey = key;
                    densest = c;
                }
            }

            count = bestCount;
            return true;
        }
    }

    /// <summary>
    /// battle-sim-extraction unit 18-J/4 — 보스 도약 비행 신호. 구 `BossLeapVisualEvent` 이식.
    ///
    /// ⚠ sim 은 `BlinkRequest` 로 **즉시 텔레포트**하고 뷰만 아치로 날린다. 출발/착지 퍼프의
    /// 타이밍도 이 채널이 소유해서 **착지 VFX 가 뷰 도착보다 먼저 터지지 않는다** —
    /// 그래서 이 시스템은 퍼프를 직접 쏘지 않고 "언제·어디서·어디로" 만 신고한다.
    /// </summary>
    public struct BossLeapVisualEvent
    {
        public SimEntityId entity;
        public SimVec3 fromWorld;
        public SimVec3 toWorld;
        /// 퍼프 VFX(`&lt;0` = 무연출).
        public int dataIndex;
        public float slamDamage;
        public int slamTileRange;
    }
}
