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
        // 이력(같은 함정을 다시 파지 않도록). 이 한 줄짜리 문제가 네 번 돌았다:
        //  ① 여기서 dedupe 하려다 «적 1기당 1발이라 접으면 한 명이 공짜로 산다» 로 뺐다.
        //  ② 그 전제가 셀 바인딩에서 거짓이었다 — `TileAoe` 는 `impactTileRange 0` 이어도 그 칸
        //     전원을 때려서 같은 칸 2기가 각자 N배를 맞았다(실측 각 160). emitter 가 칸당 1발로 접었다.
        //  ③ 그러자 뭉친 적에게 1발만 떨어져 발수가 적 수와 어긋났다. unit 8 이 **착탄 쪽**에
        //     임자(`target`) 게이트를 넣어 접기를 없앴다.
        //  ④ 그 게이트가 실전에서 **피해를 0** 으로 만들었다. 궤적은 발사 시점의 칸에 고정되는데
        //     (예고가 움직이면 안 되므로 의도된 설계) 페이로드는 착탄 시점의 적을 봤다 —
        //     **한 탄에 조준이 둘**. 실측 예고 0.40s × 적 속도 2.00 = 0.80타일 이동 > 칸 유지 폭
        //     0.50타일 이라 임자는 거의 항상 자기 칸을 떠나 있었다. 게이트 이전엔 그 칸에 «누가
        //     있든» 때려서 뒤 적이 빈 칸을 채웠고, 조준이 낡았다는 사실이 그렇게 가려져 있었다.
        //
        // 결말(unit 10·11): 「한 칸에 몇 발」이라는 물음 자체가 **틀린 질문**이었다. 파이프라인에
        // 「하늘낙하 × 적 조준」 짝이 없어서 셀 조준으로 적 조준을 흉내낸 것이 원인이고, 축을
        // 정식으로 열자(`SkyFallOnEntity` + `SingleSplash`) 접기도 게이트도 필요 없어졌다.
        // 교훈: **탄 하나에 조준은 하나다.** 조준을 둘로 두면 그 둘이 갈리는 시간만큼 어긋난다.
        //
        // rangeTiles <= 0 = 전량 통과(현행 동작) — 이 arm 이 무회귀의 근거다.
        //
        // unit 18 (distance-based-range, 사용자 지시 2026-09-01) — 셀 체비셰프 → **사거리
        // 술어와 같은 자**(`InBodyReach`: 도달 = range + 내몸 + 상대몸)로 교체. 후보를 셀로
        // 접지 않으므로 칸 경계에서 스코프가 튀지 않는다. 좌표는 **타일 단위**(호출자 환산).
        public static int FilterByReach(in NativeArray<float2> candidateXZTiles, float2 hostXZTiles,
                                        float rangeTiles, float hostBodyRadiusTiles,
                                        in NativeArray<float> bodyRadiiTiles,
                                        NativeArray<int> outIndices)
        {
            int n = candidateXZTiles.Length;
            int count = 0;
            if (rangeTiles <= 0f)
            {
                for (int i = 0; i < n && i < outIndices.Length; i++) outIndices[count++] = i;
                return count;
            }
            for (int i = 0; i < n && count < outIndices.Length; i++)
            {
                float bodyR = bodyRadiiTiles.IsCreated && i < bodyRadiiTiles.Length ? bodyRadiiTiles[i] : 0f;
                if (Wassup.Skills.SkillMath.InBodyReach(
                        candidateXZTiles[i].x - hostXZTiles.x,
                        candidateXZTiles[i].y - hostXZTiles.y,
                        rangeTiles, hostBodyRadiusTiles, bodyR))
                    outIndices[count++] = i;
            }
            return count;
        }
    }
}
