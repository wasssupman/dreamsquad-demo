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

    public static class EnemyTargetDefaults
    {
        // 적의 기본 타겟 = **상대 진영 전부**.
        //
        // 방어측 대칭: `DefenderUnitData.targetFactions` 의 이니셜라이저가 `Factions.AnyEnemy`
        // 로 「적 진영 전부」를 말한다. 적측만 비트를 하나씩 열거하고 있었고, 그래서 방어 본능이
        // 라이브에 서자 **아무 적도 후보로 보지 못하는 무적 포탑**이 됐다(2026-08-12).
        //
        // 그 사고의 교훈은 «본능 비트를 빠뜨렸다» 가 아니라 **«기본값을 열거로 적었다»** 다.
        // 파생 그룹으로 적으면 방어측 종류가 늘어날 때 이 값이 **자동으로 따라간다** —
        // 종류를 추가한 사람이 이 파일을 기억해야 할 이유가 없어진다.
        //
        // `BlockingHazard` 는 `AnyDefender` 밖에 따로 있다 — 방벽은 진영×종류 축의 거점이
        // 아니라 «부술 수 있는 벽» 이고(Faction.cs 주석), 그 사실을 여기서 감추지 않는다.
        public const int DefaultEnemyMask =
            Wassup.Battle.Units.Factions.AnyDefender
            | (int)Wassup.Battle.Units.Faction.BlockingHazard;

        // 0(Faction.None) = 미저작 → 기본값. 그 외는 저작값을 그대로 존중한다.
        // 저작이란 «이 적은 특수하다» 는 선언이다 — 마음사냥꾼(거점 전담)이 유일한 예다.
        public static int Resolve(int authoredMask)
            => authoredMask == 0 ? DefaultEnemyMask : authoredMask;
    }
}
