using Unity.Entities;
using Unity.Mathematics;

namespace Wassup.Battle.Combat
{
    // bomb-thrower-defender unit 0 — 폭탄맨 발사 상태 (Combat 소유).
    // AttackSystem 이 쿨다운마다 읽어 방향(DeployedFacing)×landingTiles 칸으로 폭탄을
    // 발사하고 rng 를 advance(unit 4). bake = BattleBridge.CreateDefenderEntity(unit 3).
    // 3변종(데미지/수면/스턴)은 정확히 3종이라 인라인 필드 — 배열/FixedList 불필요(제약 8).
    public struct BombLauncherState : IComponentData
    {
        public int landingTiles;    // N. 방향으로 몇 칸 앞에 착지
        public float travelSec;     // n. 발사→착지 고정 시간(거리 무관)
        public float fuseSec;       // m. 착지→폭발 고정 시간
        public int aoeTileRange;    // 착지 셀 기준 폭발 반경(Chebyshev 타일)
        public int aoeTargetCap;    // B. 가까운 순 최대 타격 수
        public float arcHeight;     // 구르기 arc(≈0 지면). 뷰 전용 높이
        public float dmgBombDamage; // 데미지탄 피해 C
        public float sleepSec;      // 수면탄 Sleep 지속
        public float stunSec;       // 스턴탄 Stun 지속

        // 캐스터별 결정론 stream (order-independent — 캐스터마다 독립 seed).
        // seed = MatchSeed.DeriveBombSeed(matchSeed) ^ cellHash (비0 보장, unit 3).
        public Random rng;
    }
}
