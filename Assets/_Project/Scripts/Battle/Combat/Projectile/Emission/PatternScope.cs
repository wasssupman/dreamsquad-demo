using Unity.Collections;
using Unity.Mathematics;

namespace Wassup.Battle.Combat.Projectile.Emission
{
    // on-place-skill-rework unit 1 — 후보 풀을 host 주변으로 좁히는 순수 필터.
    // plain 값 in/out static (제약 10 — sim-critical 타겟팅), EditMode 로 고정되고
    // 아키텍처를 모른다. `PatternTargeting` 과 같은 계층·같은 규율.
    //
    // v1 의 후보 풀은 맵 전체 고정이었다. "주변 N타일 안 적에게" 를 데이터로 표현하려면
    // 선택 **앞**에 반경 게이트가 필요하다 — projectile-emission-pattern 후속 후보
    // 「사거리 내 범위(scope) [S] · 필드 1개」.
    public static class PatternScope
    {
        // 반경 안 후보의 **원본 index** 를 outIndices 앞쪽에 채우고 그 개수를 반환한다.
        // outIndices 는 candidateCells 와 같은 길이여야 한다(호출자 소유 스크래치).
        //
        // ⚠ 반환값은 **항상 원본 풀 index** 다. 스코프 배열의 지역 index 를 밖으로 흘리면
        // 잠금 경로(`IndexOf(poolEntities, target)` → `poolCells[cellIdx]`)가 다른 index
        // 공간을 섞어 엉뚱한 칸을 때리거나 범위를 벗어난다.
        //
        // ⚠ **셀 중복을 제거하지 않는다.** 같은 칸에 적이 둘이면 후보도 둘이다 —
        // fan-out 의 사양이 «적 1기당 1발»(1:1 타격)이라 dedupe 하면 한 명이 공짜로 산다.
        // (rev2 초안은 dedupe 를 넣었다가 이 사양에서 뒤집혔다. 되돌리지 말 것.)
        //
        // tileRange <= 0 = 전량 통과(현행 동작) — 이 arm 이 무회귀의 근거다.
        public static int Filter(in NativeArray<int2> candidateCells, int2 hostCell,
                                 int tileRange, NativeArray<int> outIndices)
        {
            int n = candidateCells.Length;
            int count = 0;
            if (tileRange <= 0)
            {
                for (int i = 0; i < n && i < outIndices.Length; i++) outIndices[count++] = i;
                return count;
            }
            for (int i = 0; i < n && count < outIndices.Length; i++)
            {
                int2 d = math.abs(candidateCells[i] - hostCell);
                if (math.max(d.x, d.y) <= tileRange) outIndices[count++] = i;
            }
            return count;
        }
    }
}
