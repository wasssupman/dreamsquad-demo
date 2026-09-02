using Unity.Collections;
using Unity.Mathematics;
using Wassup.Data;

namespace Wassup.Battle.Combat.Projectile.Emission
{
    // projectile-emission-pattern unit 0 — 타겟 선택. 순수 수학 + EditMode 고정
    // (제약 10 — sim-critical 타겟팅). BarrageEpicenter 를 rule 축으로 일반화해
    // 흡수했다(RoundRobin 결과는 그 함수와 동일해야 한다 — unit 4 이관 무회귀 근거).
    //
    // 결정론 규칙: 순위 축은 **simId 오름차순**이다(unit 18 — 구 row-major 셀 키에서
    // 교체. 셀 키는 격자 없이는 정의되지 않는다). ECS 청크 순서에 의존하면 같은 index 가
    // 프레임마다 다른 대상을 가리킨다 — 스냅샷 순서와 무관해야 리플레이/테스트가 성립한다.
    public static class PatternTargeting
    {
        // (cells 의) 선택된 후보 index. 후보 0 이면 -1(호출자가 발사를 소모하고 skip).
        // 중복 셀(타일 고정 유닛에겐 불가능, 방어적)은 낮은 스냅샷 index 로 tie-break.
        //
        // bomb-barrel-on-place unit 3 — `casterCell` 은 Nearest 전용이다. 나머지 규칙은 이 값을
        // **읽지 않으므로** 결과가 한 톨도 안 바뀐다(무회귀 단언이 이걸 고정한다). 오버로드를
        // 만들지 않는 이유: 진입점이 둘이면 어느 쪽이 최근접을 지원하는지가 흐려진다.
        // unit 18 (distance-based-range) — **연속 자 + simId 결정론.**
        // row-major 셀 키 rank 는 격자 없이는 정의되지 않으므로 순위를 `SimEntityId`
        // 오름차순으로 재정의했다(구조적 결정론 — 스냅샷/청크 순서 무관, 리플레이 가능).
        // Nearest 는 거리²(연속) 최소, 동률은 낮은 simId. 좌표는 **타일 단위**.
        public static int Select(in NativeArray<float2> candidateXZTiles,
                                 in NativeArray<int> candidateSimIds,
                                 PatternSelectionRule rule,
                                 int fireCount, float2 hostXZTiles)
        {
            int n = candidateXZTiles.Length;
            if (n <= 0) return -1;

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
                    for (int i = 0; i < n; i++)
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
                    k = (int)(Hash((uint)math.max(0, fireCount)) % (uint)n);
                    break;
                default:
                    k = ((fireCount % n) + n) % n;
                    break;
            }

            // k 번째를 simId 오름차순 순위에서 뽑는다(동일 simId 는 스폰 규약상 없지만,
            // 방어적으로 낮은 스냅샷 index 가 이긴다).
            for (int i = 0; i < n; i++)
            {
                int rank = 0;
                for (int j2 = 0; j2 < n; j2++)
                    if (candidateSimIds[j2] < candidateSimIds[i]
                        || (candidateSimIds[j2] == candidateSimIds[i] && j2 < i)) rank++;
                if (rank == k) return i;
            }
            return -1; // unreachable
        }

        // Burst 안전 정수 해시(곱셈+시프트만). Wang/xorshift 계열.
        public static uint Hash(uint x)
        {
            x *= 2654435761u;
            x ^= x >> 15;
            x *= 2246822519u;
            x ^= x >> 13;
            return x;
        }
    }
}
