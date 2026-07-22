using UnityEngine;

namespace Wassup.Data
{
    // defender-ability-assets unit 0 — 구르는 폭탄 투척(bomb-thrower-defender).
    // 쿨다운(유닛 attackCooldown)마다 방향×landingTiles 칸으로 blind 발사, travel n초 후
    // 착지 → fuse m초 후 가까운 순 aoeTargetCap 명 폭발. 3종 랜덤(데미지/수면/스턴).
    // bake: BombLauncherState(Combat) + 캐스터별 seeded RNG(bake 가 주입).
    [CreateAssetMenu(fileName = "Ability_Bomb", menuName = "Wassup/Ability/Bomb Throw", order = 43)]
    public class BombThrowAbility : DefenderAbilityData
    {
        public int landingTiles;         // N. 방향으로 몇 칸 앞에 착지 (조준 = 착지 타일 탭)
        public float travelSec;          // n. 발사→착지 고정 시간(거리 무관)
        public float fuseSec;            // m. 착지→폭발 고정 시간
        public int aoeTileRange = 1;     // 착지 셀 기준 폭발 반경(Chebyshev 타일)
        public int aoeTargetCap;         // B. 가까운 순 최대 타격 수 (0 = 무제한)
        public float arcHeight;          // 구르기 바운스 높이(뷰 전용)
        public float damage;             // 데미지탄 피해 C (수면/스턴탄은 피해 0)
        public float sleepSec;           // 수면탄 Sleep 지속(초)
        public float stunSec;            // 스턴탄 Stun 지속(초)

        public override bool RequiresFacing => true;
    }
}
