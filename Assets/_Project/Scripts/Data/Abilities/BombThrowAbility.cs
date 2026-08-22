using UnityEngine;

namespace Wassup.Data
{
    // defender-ability-assets unit 0 — 구르는 폭탄 투척(bomb-thrower-defender).
    // 쿨다운(유닛 attackCooldown)마다 사거리 안 최근접 적의 **칸**으로 발사, travel n초
    // 후 착지 → fuse m초 후 가까운 순 aoeTargetCap 명 폭발. bake: BombLauncherState(Combat).
    //
    // unit 9 — 조준(2스텝 배치)과 착지 거리 `landingTiles` 는 은퇴했다. 던질 수 있는
    // 거리의 집은 유닛의 `attackRange` **하나**다(두 필드가 같은 숫자를 갖는 순간 갈린다).
    // unit 10 — 3종 무작위(피해/수면/기절)도 은퇴했다. 폭탄은 피해 한 종이다.
    [CreateAssetMenu(fileName = "Ability_Bomb", menuName = "Wassup/Ability/Bomb Throw", order = 43)]
    public class BombThrowAbility : DefenderAbilityData
    {
        public float travelSec;          // n. 발사→착지 고정 시간(거리 무관)
        public float fuseSec;            // m. 착지→폭발 고정 시간
        public int aoeTileRange = 1;     // 착지 셀 기준 폭발 반경(Chebyshev 타일)
        public int aoeTargetCap;         // B. 가까운 순 최대 타격 수 (0 = 무제한)
        public float arcHeight;          // 구르기 바운스 높이(뷰 전용)
        public float damage;             // 폭탄 피해

        // unit 9 — 폭탄맨은 최근접 적을 노린다. 배치 시 방향 지정 페이즈 없음.
        public override bool RequiresFacing => false;
    }
}
