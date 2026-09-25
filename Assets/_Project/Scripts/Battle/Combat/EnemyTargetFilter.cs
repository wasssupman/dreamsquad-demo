using Unity.Entities;

namespace Wassup.Battle.Combat
{
    // battle-structures unit 1 — 적의 «무엇을 노리는 놈인가» 저작 의도. 전투 중 불변.
    //
    // ⚠ 이 컴포넌트는 **무기 없는 적에게도 무조건 부착**된다(`wantsAttack` 게이트 밖).
    // 러너·스위프트처럼 `AttackState` 가 아예 없는 적도 저작 의도를 갖는다 — 도발 범위
    // 게이트(unit 2)가 런타임 마스크가 아니라 이 값을 읽는 이유다(순환 회피).
    public struct EnemyTargetFilter : IComponentData
    {
        public int classMask;      // DefenderClass 비트
        public int priorityClass;
        public int factionMask;    // 저작 의도(진영 × 종류)
    }
}
