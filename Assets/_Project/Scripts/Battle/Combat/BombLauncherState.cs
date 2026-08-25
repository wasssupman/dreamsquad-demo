using Unity.Entities;

namespace Wassup.Battle.Combat
{
    // bomb-thrower-defender unit 0 — 폭탄맨 발사 상태 (Combat 소유).
    // AttackSystem 이 쿨다운마다 읽어 **사거리(AttackState.range) 안 최근접 적의 칸**으로
    // 폭탄을 발사하고 rng 를 advance(unit 4·9). bake = BattleBridge.CreateDefenderEntity(unit 3).
    // 던지는 거리는 여기 없다 — 사거리의 집은 AttackState 하나다(unit 9).
    // unit 10 — 3변종 무작위(피해/수면/기절)는 폐기됐다. 폭탄은 피해 한 종이므로
    // 변종 필드도 캐스터별 rng 도 없다(사용자 결정 2026-08-21).
    public struct BombLauncherState : IComponentData
    {
        public float travelSec;     // n. 발사→착지 고정 시간(거리 무관)
        public float fuseSec;       // m. 착지→폭발 고정 시간
        public int aoeTileRange;    // 착지 셀 기준 폭발 반경(Chebyshev 타일)
        public int aoeTargetCap;    // B. 가까운 순 최대 타격 수
        public float arcHeight;     // 구르기 arc(≈0 지면). 뷰 전용 높이
        public float dmgBombDamage; // 폭탄 피해
    }
}
