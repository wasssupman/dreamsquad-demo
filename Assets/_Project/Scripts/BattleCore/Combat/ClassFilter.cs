namespace Wassup.BattleCore.Combat
{
    // 직업 필터 — 「이 공격자는 이 직업의 방어유닛을 못 때린다」.
    //
    // 공격 후보 선정과 **감지 후보 선정이 같은 술어**를 부른다(unit 9c). 옛 `DetectionSystem.cs:313` 은
    // 공격(`AttackSystem`)과 같은 `EnemyTargetFilter.classMask` 로 감지 후보를 걸렀다 — 못 때리는 직업을
    // 감지하면 「발견은 했는데 때릴 수 없어 앞에서 얼어붙는」 적이 된다. 두 자리가 갈리면 그 교착이 돌아온다.
    public static class ClassFilter
    {
        /// <summary>
        /// **필터의 존재가 게이트다** — 필터가 있으면 마스크 0 은 아무도 못 때린다(옛 `AttackSystem` 의
        /// `hasFilter`). 직업이 없는 후보(적·거점·길막, -1)는 거르지 않는다.
        /// </summary>
        public static bool Allows(bool hasFilter, int classMask, int cls)
            => !hasFilter || cls < 0 || (classMask & (1 << cls)) != 0;

        /// <summary>후보의 직업. 방어유닛만 갖는다 — 그 외는 -1.</summary>
        public static int ClassOf(MatchDefinition def, Unit u)
        {
            if (u.Kind != UnitKind.Defender) return -1;
            if (u.DefIndex < 0 || u.DefIndex >= def.Units.Length) return -1;
            return def.Units[u.DefIndex].Role;
        }
    }
}
