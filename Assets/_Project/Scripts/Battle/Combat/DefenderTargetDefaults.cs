namespace Wassup.Battle.Combat
{
    // battle-structures unit 8 — 방어 측 저작 타겟 마스크의 해석. 순수 함수.
    // `EnemyTargetDefaults` 의 거울이며 같은 «0 = 미저작» 폴백 규칙을 쓴다.
    //
    // 방어 측에는 컴포넌트(EnemyTargetFilter 같은 것)가 없다 — 저작 SO 를 스폰 시점에
    // 브리지가 읽어 AttackState.targetMask 로 바로 굽기 때문이다. 그래서 여기 있는 것은
    // 해석 규칙뿐이고, 호출처는 배치 방어유닛과 순찰 아군 2곳이다.
    public static class DefenderTargetDefaults
    {
        // 저작 이전의 방어 base 마스크 = 적 유닛 단독. 방어유닛이 적 거점을 못 때린 이유가
        // 이 값이 리터럴로 박혀 있던 것이다(unit 8 이전).
        public const int LegacyDefenderMask = (int)Wassup.Battle.Units.Faction.EnemyUnit;

        // 아군 타게팅(힐러·버퍼)은 DefenderUnit **단독**이다. AnyDefender 로 넓히면
        // IncomingHeal 버퍼가 없는 거점이 후보에 들어 ECB playback 에서 던진다.
        public const int AllyMask = (int)Wassup.Battle.Units.Faction.DefenderUnit;

        // targetAllies 가 저작 마스크를 이긴다 — 힐러는 어떤 마스크가 저작돼 있어도 아군만
        // 본다(에셋 마이그레이션 없이 무회귀를 보장하는 자리이기도 하다).
        // 0(Faction.None) = 미저작 → 레거시. 그 외는 저작값을 존중한다.
        public static int Resolve(int authoredMask, bool targetAllies)
        {
            if (targetAllies) return AllyMask;
            return authoredMask == 0 ? LegacyDefenderMask : authoredMask;
        }
    }
}
