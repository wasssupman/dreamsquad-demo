using Unity.Burst;
using Unity.Collections;

namespace Wassup.Battle.Combat
{
    // dreamcatcher-attack-decoupling unit 2 — 비수(ProjectileToTarget)의 **폴백**
    // 대상 선정. host 가 bestTarget 을 확정했으면 그것을 쓰고(spec 계약 3 — B안),
    // host 가 구조적으로 대상을 못 고르는 경우(폭탄맨의 blind bombardment, 해저드
    // 캐스터의 attackRange 0)에만 이 선정이 돈다.
    //
    // FrontmostTargeting/LowestHealthTargeting 선례와 같은 형태: plain 값 입출력
    // 순수 함수라 ECS 를 모르고, 결정론 tie-break 를 EditMode 로 고정할 수 있다
    // (CLAUDE.md 제약 10 의 (c) sim-critical 타게팅).
    //
    // 호출부 책임(caller 가 후보를 만들 때 적용할 규칙):
    //   · 진영 = Faction.Enemy **고정**. host 의 targetMask 를 재사용하지 않는다 —
    //     재사용하면 아군을 겨누는 host(힐러)에서 니들이 아군을 때린다.
    //   · DeadTag / PendingDeployment / PastGoalTag(유출 대기) 제외.
    //   · tileDist = Chebyshev 타일 거리(GridMath.RangeToTiles 와 같은 자).
    // 위 조건을 통과한 후보만 eligible = true 로 넘긴다.
    [BurstCompile]
    public static class DcNeedleTargeting
    {
        public struct Candidate
        {
            public bool eligible;     // caller 의 진영/상태 필터 통과 여부
            public int tileDist;      // Chebyshev 타일 거리 (진입 판정용)
            public float sqDist;      // XZ 제곱 거리 (랭킹용)
            public int entityIndex;   // Entity.Index
            public int entityVersion; // Entity.Version
        }

        // 랭킹: 최근접 우선, 동거리는 엔티티 index→version 순.
        // 후보 배열 순서가 프레임마다 흔들려도 같은 대상이 뽑힌다(결정론).
        public static bool RanksBefore(in Candidate a, in Candidate b)
        {
            if (a.sqDist != b.sqDist) return a.sqDist < b.sqDist;
            if (a.entityIndex != b.entityIndex) return a.entityIndex < b.entityIndex;
            return a.entityVersion < b.entityVersion;
        }

        // 반경 안에서 가장 앞선 후보의 인덱스. 없으면 -1(호출부는 발사를 건너뛴다 —
        // 카운트는 이미 소비됐다, README 계약 5).
        // tileRange <= 0 은 폴백 비활성이다: 값이 없는 카드가 자기 셀만 뒤지는 것보다
        // 아무것도 안 하는 쪽이 명확하다(B안에서 0 은 "발동 불가"가 아니라 "폴백 없음").
        public static int SelectNearest(in NativeArray<Candidate> candidates, int tileRange)
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
