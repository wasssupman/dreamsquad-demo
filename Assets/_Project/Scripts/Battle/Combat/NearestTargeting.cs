using Unity.Collections;

namespace Wassup.Battle.Combat
{
    // 반경 내 **최근접** 대상 선정. FrontmostTargeting(최전방) / LowestHealthTargeting
    // (최저 체력) / AggroTargeting(어그로) 과 같은 계열의 순수 랭킹 유틸이며, 특정
    // 카드나 효과의 소유물이 아니다 — "반경 안에서 가장 가까운 하나"가 필요하면
    // 어디서든 쓴다. plain 값 입출력이라 ECS 를 모르고, 결정론 tie-break 를 EditMode
    // 로 고정할 수 있다(CLAUDE.md 제약 10 의 (c) sim-critical 타게팅).
    //
    // 호출부 책임: 후보를 만들 때 진영·상태 필터(DeadTag / PendingDeployment /
    // PastGoalTag 등)를 적용해 통과분만 eligible = true 로 넘긴다. **어떤 진영을
    // 고르는지는 호출 맥락의 결정**이지 이 유틸의 결정이 아니다.
    //   · tileDist = Chebyshev 타일 거리(GridMath.RangeToTiles 와 같은 자)
    //   · sqDist   = XZ 제곱 거리
    public static class NearestTargeting
    {
        public struct Candidate
        {
            public bool eligible;     // 호출부의 진영/상태 필터 통과 여부
            public int tileDist;      // Chebyshev 타일 거리 (반경 판정용)
            public float sqDist;      // XZ 제곱 거리 (랭킹용)
            public int simId;         // SimEntityId.value (미발급 = SimEntityId.Unassigned)
        }

        // 랭킹: 최근접 우선, 동거리는 낮은 `simId`(= 먼저 스폰된 쪽).
        // 후보 배열 순서(chunk 순서)가 흔들려도 같은 대상이 뽑힌다(결정론).
        public static bool RanksBefore(in Candidate a, in Candidate b)
        {
            if (a.sqDist != b.sqDist) return a.sqDist < b.sqDist;
            return a.simId < b.simId;
        }

        // 반경 안에서 가장 앞선 후보의 인덱스. 없으면 -1.
        //
        // sibling 들은 랭킹만 하고 반경 필터도 호출부에 맡기는데 여기만 안에 둔다 —
        // `tileRange <= 0 = 선정 없음`이 이 함수의 계약이고, 그 해석이 호출처마다
        // 갈리면 안 되기 때문이다(0 을 "자기 셀만 검색"으로 읽는 구현이 섞이면 조용히
        // 엉뚱한 대상이 뽑힌다). 그 0 이 무엇을 뜻하는지 — 기능 비활성인지 데이터
        // 누락인지 — 는 호출 맥락이 해석한다.
        public static int SelectNearest(NativeArray<Candidate> candidates, int tileRange)
        {
            if (tileRange <= 0) return -1;

            int best = -1;
            for (int i = 0; i < candidates.Length; i++)
            {
                var c = candidates[i];
                if (!c.eligible) continue;
                if (c.tileDist > tileRange) continue;
                if (best < 0 || RanksBefore(c, candidates[best])) best = i;
            }
            return best;
        }
    }
}
