using UnityEngine;

namespace Wassup.Data
{
    // defender-ability-assets unit 0 — 방향 지정 사격. 배치 시 4방향 조준 후
    // 레인 게이트 발사(defender-directional-volley).
    //
    // projectile-shot-sequence unit 2 — 발수·방향·개별 interval은 공용
    // ProjectilePatternData가 소유한다. ability는 defender의 배치/공격 의미와
    // pattern 연결만 소유해 boss와 같은 emitter 계약을 쓴다.
    [CreateAssetMenu(fileName = "Ability_Volley", menuName = "Wassup/Ability/Directional Volley", order = 40)]
    public class DirectionalVolleyAbility : DefenderAbilityData
    {
        [Tooltip("한 번의 공격 trigger가 실행할 방향 발사 패턴.")]
        public ProjectilePatternData pattern;

        public override bool RequiresFacing => true;
    }
}
