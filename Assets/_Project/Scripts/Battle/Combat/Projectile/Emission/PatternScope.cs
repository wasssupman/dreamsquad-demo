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
        // 셀 중복을 여기서 제거하지 않는다 — 이 함수는 **반경 필터**이고 「한 칸에 몇 발이냐」는
        // 소비자가 정한다. 판단을 여기 두지 않은 덕에 unit 8 에서 접기를 되돌릴 때 이 함수는
        // 한 줄도 안 바뀌었다.
        //
        // 이력(같은 함정을 다시 파지 않도록): 처음엔 여기서 dedupe 하려다 «적 1기당 1발이라
        // 접으면 한 명이 공짜로 산다» 는 이유로 뺐다 → 그 전제가 셀 바인딩에서 거짓이라
        // (`TileAoe` 는 `impactTileRange 0` 이어도 그 칸 전원을 때린다) 되레 각자 N배를 맞아
        // (실측 2기 → 각 160) emitter 가 칸당 1발로 접었다 → 그러자 뭉친 적에게 1발만 떨어져
        // 발수가 적 수와 어긋났다 → unit 8 이 **착탄 쪽**에 임자(`target`) 게이트를 넣어 접기를
        // 없앴다. 교훈: 「한 칸에 몇 발」은 발사가 아니라 **착탄의 성질**이 정한다.
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
