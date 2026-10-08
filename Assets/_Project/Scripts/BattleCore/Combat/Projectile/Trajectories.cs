// salvaged from Assets/_Project/Scripts/Battle/Combat/Projectile/{BallisticArc, SkyFall, Boomerang,
//   Orbit, Bezier3, SweepHitMath}.cs (battle-core-rebuild unit 3)
// 이식 시 바뀐 것: `Unity.Burst` 제거. 식·상수·경고는 그대로다 — 전부 실패에서 나온 문장이다.
//   여섯을 한 파일로 모은 이유: 전부 「이 궤적의 위치와 도착」 한 가지 일을 하고, 소비처가
//   `TickProjectilePhase` 의 한 switch 라 한 자리에서 읽히는 편이 낫다.
using Unity.Mathematics;

namespace Somnia.Battle.BattleCore.Combat.Projectile
{
    // 포물선(`BallisticArcToPoint`). XZ 는 선형 보간이고 Y 에 사인 융기를 얹는다.
    //
    // ⚠ 융기는 **뷰 공간의 것**이다 — 보드가 평면이라 sim Y 는 화면에 안 보인다.
    // sim 이 계산해 두는 이유는 뷰가 같은 식을 재유도하지 않게 하기 위해서다.
    public static class BallisticArc
    {
        public static float ArcHeight(float arcHeight, float t)
            => math.sin(t * math.PI) * arcHeight;

        public static float3 ArcPosition(float3 origin, float3 impact, float arcHeight, float t)
        {
            float3 p = math.lerp(origin, impact, t);
            p.y += ArcHeight(arcHeight, t);
            return p;
        }

        /// <summary>수평 거리 ÷ 속도, `minTime` 바닥. 코앞 사격도 한 틱에 끝나지 않게 한다.</summary>
        public static float FlightTime(float3 origin, float3 impact, float speed, float minTime)
        {
            float dx = impact.x - origin.x;
            float dz = impact.z - origin.z;
            float dist = math.sqrt(dx * dx + dz * dz);
            float t = speed > 0f ? dist / speed : minTime;
            return math.max(t, minTime);
        }
    }

    // 낙하 예고(`SkyFall`·`SkyFallOnEntity`). sim 위치는 착탄점에 머물고 시간만 흐른다.
    public static class SkyFall
    {
        /// <summary>`flightTime &lt;= 0` → 1(첫 틱에 즉시 도착 — 예고 없는 저작의 뜻).</summary>
        public static float Progress(float elapsed, float flightTime)
            => flightTime > 0f ? math.saturate(elapsed / flightTime) : 1f;

        /// <summary>도착 조건은 **궤적이 소유한다**.</summary>
        public static bool Arrived(float elapsed, float flightTime)
            => elapsed >= flightTime;

        /// <summary>뷰 전용 낙하 압축 재매핑. 게임플레이 타이밍(`flightTime`)은 불변이다.</summary>
        public static float FallProgress(float p, float fallPortion)
            => fallPortion >= 1f
                ? p
                : math.saturate((p - (1f - fallPortion)) / math.max(fallPortion, 0.0001f));
    }

    // 왕복(`BoomerangReturn`).
    public static class Boomerang
    {
        /// <summary>
        /// 발사 축을 따라 `maxDistance` 까지 갔다 같은 축을 되짚어 발사점으로 돌아온다.
        ///
        /// ⚠ `axis` 는 **불변 입력**이다. 호출부가 「돌아오는 중이니 뒤집자」로 상태를 갱신하면
        /// 다음 틱이 발사점 반대편을 계산해 **뒤로 날아간다**. 궤도와 정반대다 — 거긴 접선이
        /// 순수한 파생값이라 아무것도 되먹이지 않는다.
        ///
        /// ⚠ 완료 틱에는 진행량이 음수로 내려간다(오버슛). 0 으로 접지 않으면 **마지막 스윕
        /// 선분이 발사점 뒤로 뻗어** 뒤에 서 있던 대상을 때린다 — 계약에 없는 피해 사건이다.
        /// </summary>
        public static float3 Position(float3 origin, float2 axis, float maxDistance,
                                      float speed, float elapsed, out bool returning)
        {
            float traveled = speed * elapsed;
            returning = traveled > maxDistance;
            float along = math.max(returning ? 2f * maxDistance - traveled : traveled, 0f);
            return origin + new float3(axis.x, 0f, axis.y) * along;
        }

        public static float TotalTime(float maxDistance, float speed)
            => speed > 0f ? 2f * maxDistance / speed : float.PositiveInfinity;

        /// <summary>
        /// 왕복 완료. 거리가 아니라 **누적 시간**으로 보는 이유: 발사점 복귀를 위치로 보면
        /// 부동소수 경계에서 한 틱 튄다.
        /// </summary>
        public static bool IsComplete(float maxDistance, float speed, float elapsed)
            => speed * elapsed >= 2f * maxDistance;
    }

    // 궤도(`OrbitAroundPoint`).
    public static class Orbit
    {
        /// <summary>
        /// 시작 각도는 0 고정이다 — 발사마다 위상을 흔들면 같은 입력의 리플레이가 갈린다
        /// (구조적 결정론 · seeded RNG 금지). 여러 구슬의 균등 배치는 `phase` 가 한다.
        /// `phase` 를 `elapsed` 오프셋으로 흉내낼 수 없다: 그건 수명도 앞당긴다.
        /// </summary>
        public static float3 Position(float3 center, float radius, float angularSpeed, float elapsed,
                                      float phase = 0f)
        {
            math.sincos(angularSpeed * elapsed + phase, out float s, out float c);
            return center + new float3(c, 0f, s) * radius;
        }

        /// <summary>구슬 i(0-based)의 위상 — n개를 원 둘레에 균등 배치한다.</summary>
        public static float PhaseOf(int index, int count)
            => count <= 1 ? 0f : 2f * math.PI * index / count;

        /// <summary>
        /// 진행 방향(단위 벡터). 크기를 버리고 **부호만** 남기는 이유 둘: 정렬은 방향만 보고,
        /// 각속도 0(저작 실수로 멈춘 궤도)에서도 단위 벡터가 나온다. 0 벡터를 남기면 정렬이
        /// 조용히 무의미해져 원인을 못 찾는 함정이 된다.
        /// </summary>
        public static float2 Tangent(float angularSpeed, float elapsed, float phase = 0f)
        {
            math.sincos(angularSpeed * elapsed + phase, out float s, out float c);
            float2 t = new float2(-s, c);
            return angularSpeed < 0f ? -t : t;
        }
    }

    // 3차 베지어(`BezierHomingToEntity`).
    public static class Bezier3
    {
        public static float3 Position(float3 p0, float3 p1, float3 p2, float3 p3, float t)
        {
            float u = 1f - t;
            float uu = u * u;
            float tt = t * t;
            return uu * u * p0
                 + 3f * uu * t * p1
                 + 3f * u * tt * p2
                 + tt * t * p3;
        }

        /// <summary>
        /// 제어점 **결정론 생성**(seeded RNG 금지). 진행 방향의 수직으로 좌우 교대 스윙하고,
        /// `swingIndex` 가 커질수록 크게 벌어진다 — 저작 값 하나로 살포가 나온다.
        /// 퇴화 입력(origin ≈ dest)은 수직축이 정의되지 않으므로 직선으로 붕괴시킨다.
        /// </summary>
        public static void ControlPoints(float3 origin, float3 dest, int swingIndex,
                                         float lateral, float forwardBias,
                                         out float3 c1, out float3 c2)
        {
            float3 delta = dest - origin;
            delta.y = 0f;
            float lenSq = math.lengthsq(delta);
            if (lenSq < 1e-6f)
            {
                c1 = dest;
                c2 = dest;
                return;
            }

            float len = math.sqrt(lenSq);
            float3 dir = delta / len;
            float3 perp = new float3(-dir.z, 0f, dir.x);

            int s = math.abs(swingIndex);
            float sign = (s & 1) == 0 ? 1f : -1f;
            float mag = lateral * (1f + (s / 2) * 0.35f);
            float3 forward = dir * (len * forwardBias);

            c1 = origin + forward + perp * (sign * mag);
            c2 = dest - forward + perp * (sign * mag * 0.5f);
        }
    }

    // 경로 스윕 히트 테스트(`PathHit`). 점 ↔ 선분 거리이고, 길이 0 선분(정지 틱)은
    // 0 으로 나누는 대신 점 판정으로 퇴화한다.
    public static class SweepHitMath
    {
        public static bool SegmentHits(float2 prevPos, float2 currPos, float2 targetPos, float hitRadius)
        {
            float2 seg = currPos - prevPos;
            float lenSq = math.lengthsq(seg);
            float t = lenSq < 1e-8f ? 0f : math.saturate(math.dot(targetPos - prevPos, seg) / lenSq);
            float2 closest = prevPos + seg * t;
            return math.distancesq(targetPos, closest) <= hitRadius * hitRadius;
        }
    }
}
