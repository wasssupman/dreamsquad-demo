using System.Collections.Generic;
using Wassup.Sim.Movement;

namespace Wassup.Sim.Combat
{
    /// <summary>
    /// battle-sim-extraction unit 18-H/1 — 타일 범위 판정. 구 `TileAoe` 이식.
    /// **체비셰프 거리**다 — 대각 한 칸이 1 이고, 이 프로젝트의 사각 사거리 관례와 같다.
    /// </summary>
    public static class TileAoe
    {
        public static int TileDistance(SimInt2 a, SimInt2 b)
            => SimMath.Max(SimMath.Abs(a.x - b.x), SimMath.Abs(a.y - b.y));

        public static bool IsInTileRange(SimInt2 candidateCell, SimInt2 centerCell, int tileRange)
            => TileDistance(candidateCell, centerCell) <= tileRange;
    }

    /// <summary>
    /// battle-sim-extraction unit 18-H/1 — 포물선 궤적 수학. 구 `BallisticArc` 이식.
    ///
    /// ⚠ **아치 높이는 뷰가 더한다.** 보드가 평면이라 sim 의 Y 는 화면에서 사라지고, sim Y 에 구운
    /// 아치는 보이지 않는다. 그래서 sim 은 XZ 만 굴리고 뷰가 <see cref="ArcHeight"/> 를 얹는다 —
    /// 같은 순수 함수를 두 계층이 나눠 소비하는 형태이고, 아키텍처 종속이면 불가능한 공유다.
    /// </summary>
    public static class BallisticArc
    {
        /// 사인 범프 — 양 끝 0, `t=0.5` 에서 정점.
        public static float ArcHeight(float arcHeight, float t)
            => SimMath.Sin(t * SimMath.PI) * arcHeight;

        /// `t=0` → origin, `t=1` → impact (아치 항이 양 끝에서 0 이라 정확히 닿는다).
        public static SimVec3 ArcPosition(SimVec3 origin, SimVec3 impact, float arcHeight, float t)
        {
            SimVec3 p = SimMath.Lerp(origin, impact, t);
            return new SimVec3(p.x, p.y + ArcHeight(arcHeight, t), p.z);
        }

        /// <summary>
        /// 수평(XZ) 거리와 속도로 비행 시간을 유도하되 `minTime` 을 바닥으로 둔다 —
        /// 코앞 사격도 첫 프레임에 해결되지 않고 눈에 보이게 아치를 그린다.
        /// `speed &lt;= 0` 은 `minTime` 폴백.
        /// </summary>
        public static float FlightTime(SimVec3 origin, SimVec3 impact, float speed, float minTime)
        {
            float dx = impact.x - origin.x;
            float dz = impact.z - origin.z;
            float dist = SimMath.Sqrt(dx * dx + dz * dz);
            float t = speed > 0f ? dist / speed : minTime;
            return SimMath.Max(t, minTime);
        }
    }

    /// <summary>
    /// battle-sim-extraction unit 18-H/1 — 3차 베지어 궤적. 구 `Bezier3` 이식.
    /// </summary>
    public static class Bezier3
    {
        /// 표준 3차 베지어. `t` 의 클램프는 호출 측 책임이다.
        public static SimVec3 Position(SimVec3 p0, SimVec3 p1, SimVec3 p2, SimVec3 p3, float t)
        {
            float u = 1f - t;
            float uu = u * u;
            float tt = t * t;
            return p0 * (uu * u)
                 + p1 * (3f * uu * t)
                 + p2 * (3f * u * tt)
                 + p3 * (tt * t);
        }

        /// <summary>
        /// 제어점 **결정론** 생성 — seeded RNG 를 쓰지 않는다. 진행 방향의 수직으로 좌우 교대
        /// 스윙하고 `swingIndex` 가 커질수록 더 벌어진다. 그래서 발수를 올리면 같은 대상으로 가는
        /// 여러 발이 각각 다른 곡선으로 갈라진다 — 저작 값 하나로 살포가 나온다.
        ///
        /// ⚠ **퇴화 입력(origin ≈ dest)은 직선으로 붕괴시킨다** — 수직축이 정의되지 않아 그대로
        /// 계산하면 NaN 이 나온다. 런타임 파생 축으로 NaN 을 만들지 않는 것이 이 계층의 규칙이다.
        /// </summary>
        public static void ControlPoints(SimVec3 origin, SimVec3 dest, int swingIndex,
                                         float lateral, float forwardBias,
                                         out SimVec3 c1, out SimVec3 c2)
        {
            var delta = new SimVec3(dest.x - origin.x, 0f, dest.z - origin.z);
            float lenSq = SimMath.LengthSq(delta);
            if (lenSq < 1e-6f)
            {
                c1 = dest;
                c2 = dest;
                return;
            }

            float len = SimMath.Sqrt(lenSq);
            SimVec3 dir = delta / len;
            var perp = new SimVec3(-dir.z, 0f, dir.x);

            int s = SimMath.Abs(swingIndex);
            float sign = (s & 1) == 0 ? 1f : -1f;
            float mag = lateral * (1f + (s / 2) * 0.35f);
            SimVec3 forward = dir * (len * forwardBias);

            c1 = origin + forward + perp * (sign * mag);
            c2 = dest - forward + perp * (sign * mag * 0.5f);
        }
    }

    /// <summary>
    /// battle-sim-extraction unit 18-H/1 — 스카이폴 진행 수학. 구 `SkyFall` 이식.
    /// sim 위치는 착탄점에 고정된 채 `elapsed` 만 흐른다.
    /// </summary>
    public static class SkyFall
    {
        /// ⚠ `flightTime &lt;= 0` → 1(즉시 도착). 저작이 경고 시간을 0 으로 둔 카드가 첫 틱에
        /// 해결되는 레거시 동작이다.
        public static float Progress(float elapsed, float flightTime)
            => flightTime > 0f ? SimMath.Saturate(elapsed / flightTime) : 1f;

        /// 도착 조건은 **궤적이 소유한다**.
        public static bool Arrived(float elapsed, float flightTime)
            => elapsed >= flightTime;

        /// <summary>
        /// 낙하 압축 재매핑(**뷰 전용**): 전체 진행 `p` 를 비행 후반 `fallPortion` 구간의 낙하
        /// 진행으로 옮긴다. 대기 구간은 0 이고 그동안 뷰는 숨는다. `fallPortion >= 1` 은 항등.
        /// ⚠ 게임플레이 타이밍(`flightTime`)은 이 함수와 무관하다.
        /// </summary>
        public static float FallProgress(float p, float fallPortion)
            => fallPortion >= 1f
                ? p
                : SimMath.Saturate((p - (1f - fallPortion)) / SimMath.Max(fallPortion, 0.0001f));
    }

    /// <summary>
    /// battle-sim-extraction unit 18-H/1 — 경로 스윕 명중 판정. 구 `SweepHitMath` 이식.
    /// 점-선분 거리다. ⚠ **길이 0 선분(정지 프레임)은 점 판정으로 퇴화**한다 — 0 나눗셈 방지.
    /// </summary>
    public static class SweepHitMath
    {
        public static bool SegmentHits(SimVec2 prevPos, SimVec2 currPos, SimVec2 targetPos, float hitRadius)
        {
            SimVec2 seg = currPos - prevPos;
            float lenSq = SimMath.LengthSq(seg);
            float t = lenSq < 1e-8f ? 0f : SimMath.Saturate(SimMath.Dot(targetPos - prevPos, seg) / lenSq);
            SimVec2 closest = prevPos + seg * t;
            return SimMath.DistanceSq(targetPos, closest) <= hitRadius * hitRadius;
        }
    }

    /// <summary>
    /// battle-sim-extraction unit 18-H/1 — 바운스 재조준 **결정**. 구 `BounceRetarget` 이식.
    ///
    /// 착탄 지점 기준 Chebyshev 반경 안에서 방금 맞은 대상을 빼고 가장 가까운 후보를 고른다.
    /// 순수 기하다 — 월드도 프레임도 엔티티도 모른다.
    ///
    /// ⚠ **동률은 낮은 인덱스가 이긴다**(strict `&lt;`). 후보 배열이 스냅샷 순서이므로 그게 곧 결정론이다.
    /// </summary>
    public static class BounceRetarget
    {
        /// 다음 대상의 **인덱스**를 돌려준다(없으면 -1). `tileRange &lt;= 0` → -1.
        public static int FindNext(
            SimVec3 hitPos, int excludeIndex,
            List<SimVec3> positions,
            int tileRange, float tileSize, SimInt2 gridSize, SimVec3 origin)
        {
            if (tileRange <= 0) return -1;
            SimInt2 centerCell = GridMath.WorldToCell(hitPos, tileSize, gridSize, origin);
            int best = -1;
            float bestSq = float.MaxValue;
            for (int i = 0; i < positions.Count; i++)
            {
                if (i == excludeIndex) continue;
                SimVec3 pos = positions[i];
                SimInt2 cell = GridMath.WorldToCell(pos, tileSize, gridSize, origin);
                if (!TileAoe.IsInTileRange(cell, centerCell, tileRange)) continue;
                float dx = pos.x - hitPos.x;
                float dz = pos.z - hitPos.z;
                float d2 = dx * dx + dz * dz;
                if (d2 < bestSq)
                {
                    bestSq = d2;
                    best = i;
                }
            }
            return best;
        }
    }
}
