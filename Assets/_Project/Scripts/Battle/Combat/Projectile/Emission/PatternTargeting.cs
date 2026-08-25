using Unity.Collections;
using Unity.Mathematics;
using Wassup.Data;

namespace Wassup.Battle.Combat.Projectile.Emission
{
    // projectile-emission-pattern unit 0 — 타겟 선택. 순수 수학 + EditMode 고정
    // (제약 10 — sim-critical 타겟팅). BarrageEpicenter 를 rule 축으로 일반화해
    // 흡수했다(RoundRobin 결과는 그 함수와 동일해야 한다 — unit 4 이관 무회귀 근거).
    //
    // 결정론 규칙(README 계약 6): 후보를 **row-major 셀 키 rank** 로 정렬한 순위에서
    // 뽑는다. ECS 청크 순서에 의존하면 같은 index 가 프레임마다 다른 대상을
    // 가리킨다 — 스냅샷 순서와 무관해야 리플레이/테스트가 성립한다.
    public static class PatternTargeting
    {
        // (cells 의) 선택된 후보 index. 후보 0 이면 -1(호출자가 발사를 소모하고 skip).
        // 중복 셀(타일 고정 유닛에겐 불가능, 방어적)은 낮은 스냅샷 index 로 tie-break.
        //
        // bomb-barrel-on-place unit 3 — `casterCell` 은 Nearest 전용이다. 나머지 규칙은 이 값을
        // **읽지 않으므로** 결과가 한 톨도 안 바뀐다(무회귀 단언이 이걸 고정한다). 오버로드를
        // 만들지 않는 이유: 진입점이 둘이면 어느 쪽이 최근접을 지원하는지가 흐려진다.
        public static int Select(in NativeArray<int2> candidateCells, PatternSelectionRule rule,
                                 int fireCount, int2 gridSize, int2 casterCell = default)
        {
            int n = candidateCells.Length;
            if (n <= 0) return -1;

            int k;
            switch (rule)
            {
                case PatternSelectionRule.None:
                    return -1;

                // 최근접은 rank 를 거치지 않고 **직접** 고른다. 거리는 셀 체비셰프(스코프
                // 필터 `PatternScope.Filter` 와 같은 자), 동률은 row-major 셀 키 → 스냅샷
                // index 순으로 아래 두 규칙과 같은 tie-break 를 쓴다.
                case PatternSelectionRule.Nearest:
                {
                    int best = -1;
                    int bestDist = int.MaxValue;
                    long bestKey = long.MaxValue;
                    for (int i = 0; i < n; i++)
                    {
                        int2 d = candidateCells[i] - casterCell;
                        int dist = math.max(math.abs(d.x), math.abs(d.y));
                        long key = (long)candidateCells[i].y * gridSize.x + candidateCells[i].x;
                        if (best < 0 || dist < bestDist || (dist == bestDist && key < bestKey))
                        {
                            best = i;
                            bestDist = dist;
                            bestKey = key;
                        }
                    }
                    return best;
                }

                case PatternSelectionRule.DeterministicShuffle:
                    // 해시 → rank. 순회처럼 예측 가능하지 않으면서 같은 fireCount 는
                    // 항상 같은 결과(리플레이·테스트 가능). 연속 중복은 허용 —
                    // 그게 랜덤의 성질이고, 회피하려면 이전 선택 상태가 필요해
                    // 순수성을 깬다.
                    k = (int)(Hash((uint)math.max(0, fireCount)) % (uint)n);
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
                    if (keyJ < keyI || (keyJ == keyI && j < i)) rank++;
                }
                if (rank == k) return i;
            }
            return -1; // unreachable: rank 는 0..n-1 의 순열
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
