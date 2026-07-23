namespace Wassup.Data.MapGrid
{
    /// <summary>
    /// 맵 풀에서 seed 로 엔트리 인덱스를 결정론적으로 고르는 순수 함수(random-map-pool unit 0).
    /// 아키텍처 무참조 — UnityEngine/Entities 를 참조하지 않는다(MatchSeed 선례, 제약 10).
    /// 라이브 선택 seed 는 BattleBridge 의 로컬 map seed(fixedMapSeed override → DeriveMapSeed(matchSeed)).
    /// </summary>
    public static class MapPoolSelect
    {
        /// <summary>
        /// seed 를 [0, count) 인덱스로 매핑한다. 같은 (seed, count) → 같은 결과.
        /// uint 캐스트로 Math.Abs(int.MinValue) 오버플로·음수 modulo 를 원천 회피한다.
        /// count &lt;= 1 이면 항상 0(빈 풀·단일 엔트리 안전).
        /// </summary>
        public static int SelectIndex(int seed, int count)
        {
            if (count <= 1) return 0;
            return (int)((uint)seed % (uint)count);
        }

        /// <summary>
        /// 서버 토너먼트 시드(uint64, `/tournament/play` 응답의 tournament.seed)를
        /// [0, count) 인덱스로 매핑한다(tournament-seed-map-select unit 2).
        /// 같은 토너먼트 = 같은 시드 = 같은 인덱스 — 참가자 전원 같은 (맵, 덱).
        /// </summary>
        public static int SelectIndexFromTournamentSeed(ulong seed, int count)
        {
            if (count <= 1) return 0;
            return (int)(seed % (ulong)count);
        }
    }
}
