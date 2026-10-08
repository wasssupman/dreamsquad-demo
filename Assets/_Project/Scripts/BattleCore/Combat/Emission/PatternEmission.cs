// salvaged from Assets/_Project/Scripts/Battle/Combat/Projectile/Emission/
//   {EmitterRuntime, EmitterTick, PatternTargeting, PatternScope, PatternDirection,
//    PatternShotRandomizer}.cs (battle-core-rebuild unit 3)
// 이식 시 바뀐 것: `FixedList128Bytes`/`NativeArray` → 배열 + count · `PatternSpec`(SO 미러) →
//   정의표의 `PatternDef` · `ShotOrder`/`PatternLogic` 은 **안 옮겼다**(안에 든 것이 전부 발사기의
//   지역 변수라 타입이 필요 없다 — 옛것은 ECS 가 Entity 를 못 나르는 제약의 산물이었다).
using Unity.Mathematics;

namespace Somnia.Battle.BattleCore.Combat.Emission
{
    /// <summary>
    /// 후보 선정 규칙. **번호는 옛 저작(`Somnia.Battle.Data.PatternSelectionRule`)과 같다** —
    /// 그 값이 이미 구워진 `ProjectilePatternData` 에셋에 직렬화돼 있기 때문이다.
    ///
    /// ⚠ 초판이 이 순서를 「읽기 좋게」 재배열해 `None = 0` 으로 두었고, 빌더가 통짜
    /// 캐스트로 옮기는 바람에 **12개 저작 중 11개가 다른 규칙으로 읽혔다** —
    /// 저작 0(순회 폭격)이 「선택 안 함」이 되고 2(방향 발사)가 「무작위 저격」이 됐다.
    /// 번호는 append-only 계약이다. 보기 좋은 순서가 필요하면 그건 저작 쪽에서 한다.
    /// `PatternSelectionRulePinTests` 가 두 enum 의 이름↔값 일치를 고정한다.
    /// </summary>
    public enum PatternSelectionRule : byte
    {
        RoundRobin = 0,
        DeterministicShuffle = 1,
        None = 2,
        Nearest = 3,
    }

    /// <summary>
    /// 발사 인스턴스의 **순수 스케줄 상태**. 값이 넷뿐이고 아무 아키텍처 타입도 모른다 —
    /// 그래서 이 struct 는 ECS 든 순수 C# 이든 그대로 산다(옛것이 이 형태를 고른 이유).
    /// </summary>
    public struct EmitterRuntime
    {
        /// <summary>버스트가 아직 빚진 발수.</summary>
        public int BurstRemaining;
        /// <summary>다음 발까지 남은 초. 잔여를 이월해 드리프트가 0 이다.</summary>
        public float Timer;
        /// <summary>선택 규칙의 결정론 소스. durable 소유자에게서 시드받는다.</summary>
        public int FireCount;
        /// <summary>현재 버스트 내 순번(베지어 스윙 소스).</summary>
        public int ShotIndex;
    }

    // 발사 스케줄 전진. plain 값 in/out.
    public static class EmitterTick
    {
        /// <summary>
        /// 인스턴스 시작. `baseFireCount` 는 **durable 소유자**가 든 영속 카운터다 —
        /// 0 으로 시드하면 RoundRobin 이 영원히 같은 순위를 고르고 셔플은 `hash(0)` 에 고정된다.
        /// </summary>
        public static void Begin(ref EmitterRuntime rt, int shotCount, int baseFireCount)
        {
            rt.BurstRemaining = shotCount;
            rt.Timer = 0f;
            rt.FireCount = baseFireCount;
            rt.ShotIndex = 0;
        }

        /// <summary>
        /// 이번 틱에 나갈 발수. `Timer` 가 0 에서 시작하므로 **시작 틱에 첫 발**이 나간다.
        /// 간격 0 이 이어지면 같은 틱에 여러 발이 나간다(느린 틱도 같은 이유로 몰아 낸다).
        /// </summary>
        public static int Advance(ref EmitterRuntime rt, float dt, float[] intervals)
        {
            if (rt.BurstRemaining <= 0) return 0;

            rt.Timer -= math.max(0f, dt);
            int fired = 0;
            // 스케줄 진행도는 **스케줄러가 소유한다.** 소비자가 아직 `ShotIndex` 를 전진시키지
            // 않았어도 다음 간격을 정확히 읽어야 한다.
            int nextShotIndex = intervals.Length - rt.BurstRemaining;
            while (rt.Timer <= 0f && rt.BurstRemaining > 0)
            {
                fired++;
                rt.BurstRemaining--;
                nextShotIndex++;

                if (rt.BurstRemaining > 0 && nextShotIndex < intervals.Length)
                    rt.Timer += math.max(0f, intervals[nextShotIndex]);
            }

            if (rt.BurstRemaining == 0) rt.Timer = 0f;
            return fired;
        }

        public static bool IsComplete(in EmitterRuntime rt) => rt.BurstRemaining <= 0;

        /// <summary>
        /// 한 트리거의 첫 탄부터 마지막 탄까지 걸리는 시간. 첫 간격은 계약상 무시한다.
        /// **다음 쿨다운을 마지막 탄 뒤로 미는** 값이다(버스트 중 쿨다운 연장).
        /// </summary>
        public static float TotalDuration(float[] intervals)
        {
            float duration = 0f;
            for (int i = 1; i < intervals.Length; i++) duration += math.max(0f, intervals[i]);
            return duration;
        }
    }

    // 후보 선정. 순수 수학이고 **순위 축은 `SimEntityId` 오름차순**이다.
    //
    // 구 row-major 셀 키는 격자 없이 정의되지 않아 교체됐다. 순회 순서에 기대면 같은 인덱스가
    // 틱마다 다른 대상을 가리킨다 — 스냅샷 순서와 무관해야 리플레이·테스트가 성립한다.
    public static class PatternTargeting
    {
        /// <summary>
        /// 선택된 후보의 인덱스. 후보 0 이면 -1(호출자가 발사를 **소비하고** 건너뛴다).
        /// 좌표는 **타일 단위**다.
        /// </summary>
        public static int Select(float2[] candidateXZTiles, int[] candidateSimIds, int count,
                                 PatternSelectionRule rule, int fireCount, float2 hostXZTiles)
        {
            if (count <= 0) return -1;

            int k;
            switch (rule)
            {
                case PatternSelectionRule.None:
                    return -1;

                case PatternSelectionRule.Nearest:
                {
                    int best = -1;
                    float bestSq = float.MaxValue;
                    int bestSim = int.MaxValue;
                    for (int i = 0; i < count; i++)
                    {
                        float dx = candidateXZTiles[i].x - hostXZTiles.x;
                        float dz = candidateXZTiles[i].y - hostXZTiles.y;
                        float d2 = dx * dx + dz * dz;
                        int sim = candidateSimIds[i];
                        if (best < 0 || d2 < bestSq || (d2 == bestSq && sim < bestSim))
                        {
                            best = i;
                            bestSq = d2;
                            bestSim = sim;
                        }
                    }
                    return best;
                }

                case PatternSelectionRule.DeterministicShuffle:
                    k = (int)(Hash((uint)math.max(0, fireCount)) % (uint)count);
                    break;

                default:
                    k = ((fireCount % count) + count) % count;
                    break;
            }

            // k 번째를 `SimEntityId` 오름차순 순위에서 뽑는다(동일 id 는 스폰 규약상 없지만
            // 방어적으로 낮은 스냅샷 인덱스가 이긴다).
            for (int i = 0; i < count; i++)
            {
                int rank = 0;
                for (int j = 0; j < count; j++)
                    if (candidateSimIds[j] < candidateSimIds[i]
                        || (candidateSimIds[j] == candidateSimIds[i] && j < i)) rank++;
                if (rank == k) return i;
            }
            return -1;   // 도달 불가
        }

        /// <summary>정수 해시(곱셈 + 시프트만). 탄막 난수 씨앗도 이것으로 만든다.</summary>
        public static uint Hash(uint x)
        {
            x *= 2654435761u;
            x ^= x >> 15;
            x *= 2246822519u;
            x ^= x >> 13;
            return x;
        }

        /// <summary>탄막 난수 씨앗 = `hash(사수 SimEntityId, 발사 카운터)`.</summary>
        public static uint ShotSeed(int shooterSimId, int fireCount)
            => Hash(unchecked((uint)shooterSimId * 2654435761u + (uint)fireCount));
    }

    // 후보 풀을 host 주변으로 좁히는 **반경 필터**.
    //
    // ⚠ **셀 중복을 제거하지 않는다.** 이 함수는 반경 필터이고 「한 칸에 몇 발이냐」는 소비자가
    // 정한다 — 판단을 여기 두지 않은 덕에 접기를 되돌릴 때 이 함수는 한 줄도 안 바뀌었다.
    //
    // 이력(같은 함정이 네 번 돌았다): 여기서 dedupe → 셀 바인딩에서 전제가 거짓 → 칸당 1발로
    // 접자 발수가 적 수와 어긋남 → 착탄 쪽 임자 게이트로 접기 제거 → 그 게이트가 **피해를 0**
    // 으로 만듦(한 탄에 조준이 둘). 결말: 「한 칸에 몇 발」이 **틀린 질문**이었다.
    public static class PatternScope
    {
        /// <summary>
        /// 반경 안 후보의 **원본 인덱스**를 `outIndices` 앞쪽에 채우고 개수를 반환한다.
        /// ⚠ 반환값은 항상 원본 풀 인덱스다 — 지역 인덱스를 밖으로 흘리면 잠금 경로가 다른
        /// 인덱스 공간을 섞어 엉뚱한 칸을 때린다. `rangeTiles &lt;= 0` = 전량 통과.
        /// </summary>
        public static int FilterByReach(float2[] candidateXZTiles, float[] bodyRadiiTiles, int count,
                                        float2 hostXZTiles, float rangeTiles, float hostBodyRadiusTiles,
                                        int[] outIndices)
        {
            int outCount = 0;
            if (rangeTiles <= 0f)
            {
                for (int i = 0; i < count && outCount < outIndices.Length; i++) outIndices[outCount++] = i;
                return outCount;
            }
            for (int i = 0; i < count && outCount < outIndices.Length; i++)
            {
                float bodyR = bodyRadiiTiles != null && i < bodyRadiiTiles.Length ? bodyRadiiTiles[i] : 0f;
                if (Somnia.Battle.Skills.SkillMath.ReachFromUnit(
                        candidateXZTiles[i].x - hostXZTiles.x,
                        candidateXZTiles[i].y - hostXZTiles.y,
                        rangeTiles, hostBodyRadiusTiles, bodyR))
                    outIndices[outCount++] = i;
            }
            return outCount;
        }
    }

    // 패턴의 정규화 방향값(`directionT`)을 실제 평면 방향으로.
    public static class PatternDirection
    {
        public static float2 Resolve(float2 baseDirection, float minAngleDeg,
                                     float maxAngleDeg, float directionT)
        {
            float angleDeg = math.lerp(minAngleDeg, maxAngleDeg, math.saturate(directionT));
            math.sincos(math.radians(angleDeg), out float sin, out float cos);
            return new float2(
                baseDirection.x * cos - baseDirection.y * sin,
                baseDirection.x * sin + baseDirection.y * cos);
        }
    }

    // 트리거 하나의 발사 스냅샷을 씨앗에서 만든다.
    //
    // **같은 씨앗에는 같은 N발**이고, 씨앗은 producer 가 `hash(사수 id, 발사 카운터)` 로 정한다.
    // 그래서 난수 저작이 있어도 리플레이가 갈리지 않는다.
    public static class PatternShotRandomizer
    {
        /// <summary>
        /// `directions`·`intervals` 를 제자리에서 다시 뽑는다. 저작이 난수가 아니면 아무것도 안 한다.
        /// 첫 발의 간격은 계약상 0 이다(트리거 즉시 나간다).
        /// </summary>
        public static void Apply(float[] directions, float[] intervals,
                                 bool randomize, float minIntervalSec, float maxIntervalSec, uint seed)
        {
            if (!randomize || intervals.Length == 0) return;

            float lo = math.max(0f, minIntervalSec);
            float hi = math.max(lo, maxIntervalSec);
            var rng = new Random(seed != 0u ? seed : 1u);

            for (int i = 0; i < intervals.Length; i++)
            {
                if (directions != null && i < directions.Length) directions[i] = rng.NextFloat();
                intervals[i] = i == 0 ? 0f : (lo < hi ? rng.NextFloat(lo, hi) : lo);
            }
        }
    }
}
