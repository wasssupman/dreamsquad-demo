namespace Somnia.Battle.Core
{
    /// <summary>
    /// 단일 매치 시드에서 맵/웨이브/비주얼 시드를 결정론적·decorrelated 하게 파생한다.
    /// match-seed-unification spec — 라이브 매치 시드의 유일한 파생 경로.
    /// GenerateRandom() 만 비결정론(진입점 1회 호출용). Derive* 는 순수 함수.
    /// </summary>
    public static class MatchSeed
    {
        // salt 는 임의 고정 상수. 같은 matchSeed 라도 계열을 분리해 상관 제거.
        const uint MapSalt    = 0x9E3779B1u; // map stream
        const uint WaveSalt   = 0x85EBCA77u; // wave stream
        const uint VisualSalt = 0xC2B2AE3Du; // projectile jitter stream
        const uint PickupSalt = 0x27D4EB2Fu; // season-gimmick-overwork — 픽업 스폰 셀 stream
        const uint GimmickSalt = 0x165667B1u; // gimmick-match-integration — 기믹 배정 stream
        const uint MeteorSalt = 0xD6E8FEB8u;  // season-gimmick-clockout — 메테오 착탄 셀 stream

        /// <summary>
        /// 미지정(0) 시 매 판 새 시드. 시간 + 프로세스 고유값 혼합으로 같은 tick 충돌 회피.
        /// 결정론 함수가 아니다 — 매치 진입점에서 1회만 호출한다.
        /// </summary>
        // battle-core-rebuild unit 1 — 이 파일이 `Somnia.Battle.BattleCore`(noEngineReferences)로
        // 이사하면서 난수원을 `UnityEngine.Random.Range` 에서 `Guid` 로 바꿨다. 호출 계약은
        // 그대로다(매 호출 다른 int, 같은 tick 에도 충돌 없음) — 이 함수는 **정의상 비결정론**
        // 이라 난수원이 무엇인지가 규칙에 영향을 주지 않는 유일한 자리다.
        // 시그니처를 유지하는 것이 중요하다: 호출처 둘(`GameManager` · 동결된 `BattleBridge`)이
        // 이 이름을 그대로 부르고, 동결 경로는 고칠 수 없다.
        public static int GenerateRandom() => unchecked(
            System.Environment.TickCount ^ System.Guid.NewGuid().GetHashCode());

        public static int DeriveMapSeed(int matchSeed)    => Mix((uint)matchSeed, MapSalt);
        public static int DeriveWaveSeed(int matchSeed)   => Mix((uint)matchSeed, WaveSalt);
        public static int DeriveVisualSeed(int matchSeed) => Mix((uint)matchSeed, VisualSalt);
        public static int DerivePickupSeed(int matchSeed) => Mix((uint)matchSeed, PickupSalt);
        public static int DeriveGimmickSeed(int matchSeed) => Mix((uint)matchSeed, GimmickSalt);
        public static int DeriveMeteorSeed(int matchSeed)  => Mix((uint)matchSeed, MeteorSalt);

        // 결정론적 32-bit 믹스(xorshift-multiply 류). 0 입력도 0 아닌 출력 보장.
        static int Mix(uint seed, uint salt)
        {
            uint h = seed ^ salt;
            h ^= h >> 16; h *= 0x7FEB352Du;
            h ^= h >> 15; h *= 0x846CA68Bu;
            h ^= h >> 16;
            int v = unchecked((int)h);
            return v != 0 ? v : 1; // 다운스트림 생성기들이 0 을 별도 폴백 처리하므로 0 회피
        }
    }
}
