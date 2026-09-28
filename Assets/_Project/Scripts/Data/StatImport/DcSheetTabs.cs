namespace Wassup.Data.StatImport
{
    /// <summary>
    /// 드림캐쳐 · 스킬 시트 탭 계약(위치 고정 — 에디터 창 · 런타임 refresher · export · push 가 같은 순서를 쓴다).
    ///
    /// skill-data-table unit 5 — 옛 `DcMechanics`(카드 전용 · 옛 겸직 칸 값 overlay)는 **은퇴**했다. 그 자리는 새 두 탭이다:
    /// `Skills`(효과 한 줄 = 효과 하나) · `SkillOwners`(카드 · 방어유닛 · 적의 소유 줄 — U19). `DcSkills`(액티브 `SkillData`)는
    /// U18 로 남는다 — 이름이 비슷하지만 다른 탭이다.
    /// </summary>
    public static class DcSheetTabs
    {
        public const string Cards = "DcCards";
        public const string CardEffects = "DcCardEffects";
        public const string AttackMods = "DcAttackMods";
        public const string ActiveSkills = "DcSkills";
        public const string Config = "DcConfig";
        public const string Skills = "Skills";
        public const string SkillOwners = "SkillOwners";

        public const int CardsAt = 0, CardEffectsAt = 1, AttackModsAt = 2, ActiveSkillsAt = 3, ConfigAt = 4, SkillsAt = 5, SkillOwnersAt = 6;
        public const int Count = 7;

        /// <summary>새 배열(호출처가 고쳐도 계약이 안 흔들린다).</summary>
        public static string[] Default() => new[] { Cards, CardEffects, AttackMods, ActiveSkills, Config, Skills, SkillOwners };
    }
}
