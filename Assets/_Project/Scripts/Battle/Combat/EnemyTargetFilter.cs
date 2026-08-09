using Unity.Entities;

namespace Wassup.Battle.Combat
{
    // aggro-targeting Unit 4 — enemy base (non-aggro) targeting rule.
    //   classMask    : allowed DefenderClass bits (bit = 1 << (int)DefenderClass).
    //                  -1 = all classes. Candidates without DefenderClassTag
    //                  (e.g. blocking hazards) are never filtered out.
    //   priorityClass: class picked first when any is in range ((int)DefenderClass);
    //                  -1 = no priority (nearest wins).
    // Aggro (Aggroed) overrides this entirely — see AttackSystem.
    //   factionMask  : battle-structures unit 1 — **저작 의도**. "이 적은 무엇을 노리는
    //                  놈인가"(진영 × 종류 교차 비트). SO 소유, 전투 중 불변.
    //                  런타임 마스크(AttackState.targetMask)와 분리된 이유: 도발 게이트가
    //                  런타임 마스크를 읽으면, 무기 없는 적(마스크가 거점 단독)은 도발이
    //                  나중에 유닛 비트를 OR 해주는 구조라 순환이 되어 영구 도발 불가가
    //                  된다. 이 컴포넌트는 wantsAttack 게이트 **밖**에서 부착되므로
    //                  AttackState 가 없는 적도 저작 의도를 갖는다.
    public struct EnemyTargetFilter : IComponentData
    {
        public int classMask;
        public int priorityClass;
        public int factionMask;
    }

    // battle-structures unit 1 — 저작 타겟 마스크의 «빈 값» 방어선. 순수 함수.
    //
    // Faction.None(0)은 인스펙터에서 표현 가능한 값이다. 저작자가 마스크를 비우면 그 적은
    // 아무것도 못 때리는 유령이 되므로, 0 을 «미저작 = 현행» 으로 읽어 조용한 무장 해제를
    // 막는다. 그래서 «무엇도 노리지 않는 적» 은 0 으로 표현할 수 없다 — 필요해지면 별도
    // 신호를 쓴다.
    //
    // (기존 에셋의 무회귀는 이 폴백이 아니라 SO 필드 이니셜라이저가 보장한다 — 실측:
    //  YAML 에 키가 없는 신규 필드는 이니셜라이저 값을 유지한다. 0 으로 로드되지 않는다.)
    public static class EnemyTargetDefaults
    {
        // 저작 이전의 적 base 마스크 = 방어유닛 + 방벽 + 방어 마음.
        // DefenderCore 가 빠지면 적이 골 타워를 못 때려 공성이 사라진다.
        public const int LegacyEnemyMask =
            (int)(Wassup.Battle.Units.Faction.DefenderUnit
                | Wassup.Battle.Units.Faction.BlockingHazard
                | Wassup.Battle.Units.Faction.DefenderCore);

        // 0(Faction.None) = 미저작 → 레거시 마스크. 그 외는 저작값을 그대로 존중한다.
        public static int Resolve(int authoredMask)
            => authoredMask == 0 ? LegacyEnemyMask : authoredMask;
    }
}
