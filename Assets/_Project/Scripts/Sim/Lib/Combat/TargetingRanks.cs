namespace Wassup.Sim.Combat
{
    /// <summary>
    /// battle-sim-extraction unit 18-I/2 arm C/3 — "최전방" 랭킹. 구 `FrontmostTargeting` 이식.
    ///
    /// 호출부 책임: 마스크·자기 제외·사거리·`PastGoal`·**도달 가능**을 이미 거른 뒤 비교한다.
    /// <see cref="UnreachableDist"/> 는 `FlowFieldSingleton.dist` 의 sentinel 과 같은 값이고,
    /// 호출부가 그 값을 만나면 후보로 삼지 않는다.
    ///
    /// 순서(완전 결정적): ① `flowDist` 오름차 — 골까지 남은 BFS 비용이 작을수록 앞
    /// ② `sqDist` 오름차 ③ `simId` 오름차(총 tie-break).
    ///
    /// ⚠ 구 `SelectFrontmost(NativeArray, count)` 는 **옮기지 않았다** — 구 sim 전체에서 호출처가
    /// 테스트뿐이었다(프로덕션은 후보 배열을 만들지 않고 running-best 로 <see cref="RanksBefore"/>
    /// 만 쓴다). 죽은 API 를 신 sim 에 들이는 것은 제약 8 위반이고, 그 함수가 하던 도달 불가 필터는
    /// 호출부에 이미 있다. 랭킹 계약은 `RanksBefore` 테스트가 그대로 덮는다.
    /// </summary>
    public static class FrontmostTargeting
    {
        /// `FlowFieldSingleton.dist` 의 도달 불가 sentinel 과 같은 값이어야 한다.
        public const int UnreachableDist = int.MaxValue;

        public struct Candidate
        {
            public int flowDist;
            /// 공격자→후보 XZ 제곱 거리.
            public float sqDist;
            public int simId;
        }

        public static bool RanksBefore(in Candidate a, in Candidate b)
        {
            if (a.flowDist != b.flowDist) return a.flowDist < b.flowDist;
            if (a.sqDist != b.sqDist) return a.sqDist < b.sqDist;
            return a.simId < b.simId;
        }
    }

    /// <summary>
    /// battle-sim-extraction unit 18-I/2 arm C/3 — "가장 다친 아군" 랭킹.
    /// 구 `LowestHealthTargeting` 이식. <see cref="FrontmostTargeting"/> 과 같은 모양으로 읽힌다.
    ///
    /// 순서: ① `hpRatio` 오름차(더 다친 쪽이 앞) ② `sqDist` 오름차 ③ `simId` 오름차.
    /// ⚠ 구 `SelectLowest` 를 옮기지 않은 이유는 위와 같다.
    /// </summary>
    public static class LowestHealthTargeting
    {
        public struct Candidate
        {
            /// `Health.ComputeRatio(value, max)` ∈ [0,1].
            public float hpRatio;
            public float sqDist;
            public int simId;
        }

        public static bool RanksBefore(in Candidate a, in Candidate b)
        {
            if (a.hpRatio != b.hpRatio) return a.hpRatio < b.hpRatio;
            if (a.sqDist != b.sqDist) return a.sqDist < b.sqDist;
            return a.simId < b.simId;
        }
    }

    /// <summary>
    /// battle-sim-extraction unit 18-I/2 arm C/3 — 방향 고정 유닛의 레인 판정. 구 `LaneMath` 이식.
    ///
    /// 레인 = facing 축을 따라 **폭 1타일**, 거리 `[1..rangeTiles]`. 공격자 자신의 타일은
    /// 절대 포함되지 않는다(`forward >= 1`).
    ///
    /// ⚠ `facing` 은 **정규화된 기본 방위 단위 벡터**여야 한다 — 호출부(`DeployedFacing`)가
    /// 보증하므로 여기서 정규화하지 않는다.
    /// </summary>
    public static class LaneMath
    {
        public static bool IsInLane(SimInt2 attackerTile, SimInt2 facing, int rangeTiles, SimInt2 targetTile)
        {
            int dx = targetTile.x - attackerTile.x;
            int dy = targetTile.y - attackerTile.y;
            int forward = dx * facing.x + dy * facing.y; // facing 축 투영
            int side = dx * facing.y - dy * facing.x;    // 수직 오프셋
            return side == 0 && forward >= 1 && forward <= rangeTiles;
        }
    }
}
