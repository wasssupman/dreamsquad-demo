using Unity.Mathematics;

namespace Wassup.Battle.Movement
{
    // enemy-tile-movement-integrity unit 1 — 코너 엣지-허깅 복원(측면 recenter).
    // flow 진행방향 수직(perp) 성분을 dead-band 밖일 때만 셀 중심선(perp=0) 쪽으로 당긴다.
    // 안쪽으로만 이동(|perp| 감소) → 벽 침투 없음. dead-band 안(스폰 분산 범위)은 손대지 않아 보존.
    public static class LateralRecenter
    {
        // 내부 이동-품질 상수(게임플레이 값 아님; MovementCellTrim.kBoundaryEpsilon 와 같은 성격).
        // DeadbandFraction: 스폰 분산폭(기본 0.2)보다 크고 코너 드리프트(측정 0.29~0.49)보다 작게 →
        //   직진 분산 보존 + 코너만 교정. (스폰 분산폭을 0.25 이상으로 올리면 unit 3 에서 재검토.)
        public const float DeadbandFraction = 0.25f;
        // rate = RateK · speed (속도비례). 상수 rate 금지 — 빠른 적이 엣지를 거리상 더 오래 안 타게.
        public const float RateK = 0.4f;

        // current 의 셀 중심 대비 perp 오프셋을 deadband 밖이면 rate·dt 만큼(단, 밴드 가장자리까지만)
        // 0 쪽으로 당기는 월드 XZ 변위. flowDir=진행방향(perp 축 유도). speed=유효 이동속도.
        public static float3 Compute(float3 current, int2 cell, float2 flowDir,
                                     float speed, float dt, float tileSize, float3 origin)
        {
            float2 perpAxis = SpawnSpread.Perpendicular(flowDir);
            float3 center = GridMath.CellToWorldCenter(cell, tileSize, current.y, origin);
            float2 off = new float2(current.x - center.x, current.z - center.z);
            float perp = math.dot(off, perpAxis);          // 셀 중심선 대비 부호화 측면 오프셋
            float adp = math.abs(perp);
            float deadband = DeadbandFraction * tileSize;
            if (adp <= deadband) return float3.zero;        // 밴드 안 — 분산 보존
            // 밴드 가장자리까지만 당김(0 관통/오버슈트 방지). 코너 드리프트는 deadband 에 정착.
            float step = math.min(RateK * speed * dt, adp - deadband);
            float2 xz = -math.sign(perp) * perpAxis * step;
            return new float3(xz.x, 0f, xz.y);
        }
    }
}
