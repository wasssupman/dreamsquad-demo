namespace Wassup.Data.Authoring
{
    // battle-core-rebuild unit 8c — 이 타입의 **집**만 옮겼다(옛 `Battle/Combat/EnemyTargetFilter.cs` 에서 떼어냄). 네임스페이스·값·번호 무변 —
    // 새 층 저작 SO 가 이것을 부르는데 옛 폴더는 unit 9 가 통째로 지운다. 이름 정리는 unit 9.

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
            Wassup.Skills.Factions.AnyDefender
            | (int)Wassup.Skills.Faction.BlockingHazard;

        // 0(Faction.None) = 미저작 → 기본값. 그 외는 저작값을 그대로 존중한다.
        // 저작이란 «이 적은 특수하다» 는 선언이다 — 마음사냥꾼(거점 전담)이 유일한 예다.
        public static int Resolve(int authoredMask)
            => authoredMask == 0 ? DefaultEnemyMask : authoredMask;
    }
}
