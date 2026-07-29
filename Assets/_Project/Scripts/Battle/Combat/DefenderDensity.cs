using Unity.Collections;
using Unity.Mathematics;

namespace Wassup.Battle.Combat
{
    // boss-jjangssen unit 4 — SelfBlink 착지 앵커 선택: "방어유닛이 가장 많이 모인 셀".
    // 순수 수학 + EditMode 고정 (제약 10 — sim-critical 타겟팅). 같은 카테고리인
    // BlinkMath / PatternTargeting 과 같은 형태를 유지한다: plain 값 입력 → plain 값 출력,
    // Entities/Battle 런타임 타입 무참조.
    public static class DefenderDensity
    {
        // 후보 = 방어유닛이 서 있는 셀들. 각 후보에 대해 Chebyshev `radius` 안의 방어유닛
        // 수를 세고 최다인 셀을 고른다.
        //
        // **동점 tie-break 는 row-major 셀 키(y*w+x) 오름차순이다.** 청크 순회 순서에
        // 의존하면 같은 배치에서 프레임마다 다른 셀이 뽑혀 결정론이 깨진다 —
        // PatternTargeting 이 같은 이유로 같은 규칙을 쓴다.
        //
        // radius <= 0 은 "자기 셀만" 으로 취급한다(각 셀의 겹친 유닛 수 = 사실상 최근접).
        // 후보가 없으면(방어유닛 전멸) false — 호출부가 blink 를 skip 하고 임계 래치는
        // 소모된 채 남는다(기존 동작).
        public static bool TryFindDensestCell(
            in NativeArray<int2> defenderCells, int radius, int2 gridSize, out int2 densest, out int count)
        {
            densest = default;
            count = 0;
            if (defenderCells.Length == 0) return false;

            int r = math.max(0, radius);
            int bestCount = -1;
            long bestKey = long.MaxValue;

            for (int i = 0; i < defenderCells.Length; i++)
            {
                int2 c = defenderCells[i];
                int n = 0;
                for (int j = 0; j < defenderCells.Length; j++)
                {
                    // Chebyshev 디스크 멤버십은 이 폴더의 명명된 프리미티브를 쓴다 —
                    // `AuraPulse` 도 같은 판정을 "same idiom as the TileAoe impact check" 로 참조한다.
                    if (TileAoe.IsInTileRange(defenderCells[j], c, r)) n++;
                }

                // gridSize 를 폭으로 쓰는 row-major 키. 그리드 밖 좌표가 들어와도 단조
                // 순서만 필요하므로 클램프하지 않는다(음수 x 도 순서가 정의된다).
                long key = (long)c.y * math.max(1, gridSize.x) + c.x;
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
}
