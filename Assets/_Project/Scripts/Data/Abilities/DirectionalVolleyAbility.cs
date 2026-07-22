using UnityEngine;

namespace Wassup.Data
{
    // defender-ability-assets unit 0 — 방향 지정 사격(다연발 포함). 배치 시 4방향 조준 후
    // 레인 게이트 발사(defender-directional-volley). shotCount 1 = 단발 방향 유닛.
    // bake: shotCount>1 이면 VolleyFireState(Combat) — 게이트 의미는 기존과 동일(계약 6).
    [CreateAssetMenu(fileName = "Ability_Volley", menuName = "Wassup/Ability/Directional Volley", order = 40)]
    public class DirectionalVolleyAbility : DefenderAbilityData
    {
        public int shotCount = 1;        // 트리거당 발수. >1 이면 버스트/스프레드
        public float shotIntervalSec;    // >0 = 시간차 버스트, 0 = 동프레임 스프레드
        public float spreadAngleDeg;     // 총 확산각(부채꼴). 0 = 일직선

        public override bool RequiresFacing => true;
    }
}
