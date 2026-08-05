using System.Collections.Generic;

namespace Wassup.Sim.Combat
{
    /// <summary>
    /// battle-sim-extraction unit 18-H/4 — 발사 스케줄 전진. 구 `EmitterTick` 이식.
    /// plain 값 in/out 순수 static — 아키텍처를 모른다.
    /// </summary>
    public static class EmitterTick
    {
        /// <summary>
        /// 인스턴스 시작. ⚠ `baseFireCount` 를 0 으로 시드하면 **선택 규칙이 고정된다**
        /// (RoundRobin 이 영원히 rank 0). durable 소유자의 카운터를 넘겨야 한다.
        /// </summary>
        public static void Begin(ref EmitterRuntime rt, in PatternSpec spec, int baseFireCount)
        {
            rt.burstRemaining = spec.ShotCount;
            rt.timer = 0f;
            rt.fireCount = baseFireCount;
            rt.shotIndex = 0;
        }

        /// <summary>
        /// 이번 프레임에 나갈 발수. `timer` 가 0 에서 시작하므로 **시작 프레임에 첫 발**이 나간다.
        /// 간격 0 인 연속 step 은 같은 프레임에 전부 나가고, 느린 프레임은 여러 발을 한 번에 돌려준다.
        ///
        /// ⚠ `nextShotIndex` 를 `burstRemaining` 에서 역산하는 것이 계약이다 — 스케줄 진행도는
        /// **스케줄러가 소유**한다. 소비자가 아직 `shotIndex` 를 전진시키지 않았어도 다음 간격을
        /// 정확히 읽어야 한다.
        /// </summary>
        public static int Advance(ref EmitterRuntime rt, float dt, in PatternSpec spec)
        {
            if (rt.burstRemaining <= 0) return 0;

            rt.timer -= SimMath.Max(0f, dt);
            int fired = 0;
            int nextShotIndex = spec.ShotCount - rt.burstRemaining;
            while (rt.timer <= 0f && rt.burstRemaining > 0)
            {
                fired++;
                rt.burstRemaining--;
                nextShotIndex++;

                if (rt.burstRemaining > 0)
                    rt.timer += SimMath.Max(0f, spec.shots[nextShotIndex].intervalAfterPreviousSec);
            }

            if (rt.burstRemaining == 0) rt.timer = 0f;
            return fired;
        }

        public static bool IsComplete(in EmitterRuntime rt) => rt.burstRemaining <= 0;

        /// <summary>
        /// 첫 탄부터 마지막 탄까지 걸리는 시간. ⚠ **첫 step 의 간격은 계약상 무시**되고
        /// 이후 step 의 "직전 탄 이후" 값만 합산한다. 공격 루프가 다음 쿨다운을 마지막 탄 뒤로
        /// 미루는 데 쓴다.
        /// </summary>
        public static float TotalDuration(in PatternSpec spec)
        {
            float duration = 0f;
            for (int i = 1; i < spec.ShotCount; i++)
                duration += SimMath.Max(0f, spec.shots[i].intervalAfterPreviousSec);
            return duration;
        }
    }

    /// <summary>
    /// battle-sim-extraction unit 18-H/4 — 패턴 방향 해석. 구 `PatternDirection` 이식.
    /// 정규화 값을 실제 평면 방향으로 회전시킨다.
    /// </summary>
    public static class PatternDirection
    {
        public static SimVec2 Resolve(SimVec2 baseDirection, float minAngleDeg,
                                      float maxAngleDeg, float directionT)
        {
            float angleDeg = SimMath.Lerp(minAngleDeg, maxAngleDeg, SimMath.Saturate(directionT));
            SimMath.SinCos(SimMath.Radians(angleDeg), out float sin, out float cos);
            return new SimVec2(
                baseDirection.x * cos - baseDirection.y * sin,
                baseDirection.x * sin + baseDirection.y * cos);
        }
    }

    /// <summary>
    /// battle-sim-extraction unit 18-H/4 — 트리거 스냅샷 무작위화. 구 `PatternShotRandomizer` 이식.
    /// 같은 시드에는 같은 발 목록을 만든다.
    ///
    /// ⚠ **새 배열을 만든다(제자리 수정이 아니다).** 구 `FixedList128Bytes` 는 값 타입이라
    /// `PatternSpec` 을 복사하면 발 목록도 복사됐지만, 신 sim 의 배열은 참조라 제자리로 고치면
    /// 원본 슬롯의 목록까지 오염된다. 이 한 줄이 값 의미론을 되돌려 놓는 자리다.
    /// </summary>
    public static class PatternShotRandomizer
    {
        public static void Apply(ref PatternSpec spec, uint seed)
        {
            if (!spec.randomizeShotsPerTrigger || spec.ShotCount == 0) return;

            float minInterval = SimMath.Max(0f, spec.randomIntervalMinSec);
            float maxInterval = SimMath.Max(minInterval, spec.randomIntervalMaxSec);
            var rng = SimRandom.CreateFromIndex(seed);

            var shots = new PatternShotSpec[spec.shots.Length];
            for (int i = 0; i < shots.Length; i++)
            {
                shots[i].directionT = rng.NextFloat();
                shots[i].intervalAfterPreviousSec = i == 0
                    ? 0f
                    : (minInterval < maxInterval ? rng.NextFloat(minInterval, maxInterval) : minInterval);
            }
            spec.shots = shots;
        }
    }

    /// <summary>
    /// battle-sim-extraction unit 18-H/4 — 대상 선택. 구 `PatternTargeting` 이식.
    ///
    /// ⚠ **결정론의 근거는 셀 키 rank 다.** 후보를 row-major 셀 키로 정렬한 순위에서 뽑으므로
    /// 스냅샷 순서(청크 순서)에 의존하지 않는다 — 그러지 않으면 같은 index 가 프레임마다 다른
    /// 대상을 가리켜 리플레이가 성립하지 않는다.
    /// </summary>
    public static class PatternTargeting
    {
        /// 선택된 후보 index. 후보 0 이면 -1(호출자가 발사를 소모하고 건너뛴다).
        public static int Select(List<SimInt2> candidateCells, PatternSelectionRule rule,
                                 int fireCount, SimInt2 gridSize)
        {
            int n = candidateCells.Count;
            if (n <= 0) return -1;

            int k;
            switch (rule)
            {
                case PatternSelectionRule.None:
                    return -1;

                case PatternSelectionRule.DeterministicShuffle:
                    // 해시 → rank. 순회처럼 예측되지 않으면서 같은 `fireCount` 는 항상 같은 결과다.
                    // ⚠ 연속 중복을 **허용**한다 — 그게 랜덤의 성질이고, 피하려면 이전 선택 상태가
                    //   필요해 순수성이 깨진다.
                    k = (int)(Hash((uint)SimMath.Max(0, fireCount)) % (uint)n);
                    break;
                default:
                    k = ((fireCount % n) + n) % n;
                    break;
            }

            for (int i = 0; i < n; i++)
            {
                long keyI = (long)candidateCells[i].y * gridSize.x + candidateCells[i].x;
                int rank = 0;
                for (int j = 0; j < n; j++)
                {
                    long keyJ = (long)candidateCells[j].y * gridSize.x + candidateCells[j].x;
                    // 중복 셀은 낮은 스냅샷 index 로 tie-break(타일 고정 유닛엔 불가능, 방어적).
                    if (keyJ < keyI || (keyJ == keyI && j < i)) rank++;
                }
                if (rank == k) return i;
            }
            return -1; // 도달 불가: rank 는 0..n-1 의 순열이다
        }

        /// 곱셈+시프트만 쓰는 정수 해시(Wang/xorshift 계열).
        public static uint Hash(uint x)
        {
            x *= 2654435761u;
            x ^= x >> 15;
            x *= 2246822519u;
            x ^= x >> 13;
            return x;
        }
    }

    /// <summary>
    /// battle-sim-extraction unit 18-H/4 — 발사 명령을 완성하는 **유일한 지점**. 구 `PatternLogic` 이식.
    /// 아키텍처 계층은 이 명령을 자기 형태로 번역만 하고 스케줄/선택 판단을 되풀이하지 않는다.
    /// </summary>
    public static class PatternLogic
    {
        public static ShotOrder BuildOrder(in PatternSpec spec, ref EmitterRuntime rt, int selectedCandidateIndex)
        {
            var order = new ShotOrder
            {
                shotIndex = rt.shotIndex,
                targetCandidateIndex = selectedCandidateIndex,
                damage = spec.damage,
                barrelDataIndex = spec.barrelDataIndex,
                telegraphSec = spec.telegraphSec,
                directionT = spec.shots[rt.shotIndex].directionT,
            };
            rt.shotIndex++;
            rt.fireCount++;
            return order;
        }
    }
}
