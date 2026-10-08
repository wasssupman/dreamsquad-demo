namespace Somnia.Battle.Data.StatImport
{
    /// <summary>
    /// 드림캐쳐 · 스킬 시트 탭 계약(위치 고정 — 에디터 창 · 런타임 refresher · export · push 가 같은 순서를 쓴다).
    ///
    /// skill-data-table unit 5 — 옛 `DcMechanics`(카드 전용 · 옛 겸직 칸 값 overlay)는 **은퇴**했다. 그 자리는 새 두 탭이다:
    /// `Skills`(효과 한 줄 = 효과 하나) · `SkillOwners`(카드 · 방어유닛 · 적의 소유 줄 — U19). `DcSkills`(액티브 `SkillData`)는
    /// U18 로 남는다 — 이름이 비슷하지만 다른 탭이다.
    ///
    /// skill-data-table unit 8 단계 B — 카드 전용 자식 탭 둘(`DcCardEffects` · `DcAttackMods`)도 **은퇴**했다(7 → 5탭). 스쿼드 스탯 효과 ·
    /// 공격 수식자는 효과 줄(`Skills`) + 카드 소유 줄(`SkillOwners` · 트리거 `None`)이다. 서버 시트의 옛 탭은 남아도 아무도 안 읽는다(보관 · 삭제 자유).
    /// unit 9 — `DcCards` → `Cards` 개명 · 전 탭 열 이름 스네이크(`SheetColumns` · 헤더 정본 = `5_sheet_io.md` 「실제 시트 설정」).
    /// </summary>
    public static class DcSheetTabs
    {
        // skill-data-table unit 9 — 옛 `DcCards` 개명(카드 고유 값 탭 — `tables.md` §7). `DcSkills` · `DcConfig` 이름은 후속(U18 · 설정 통합).
        public const string Cards = "Cards";
        public const string ActiveSkills = "DcSkills";
        public const string Config = "DcConfig";
        public const string Skills = "Skills";
        public const string SkillOwners = "SkillOwners";

        public const int CardsAt = 0, ActiveSkillsAt = 1, ConfigAt = 2, SkillsAt = 3, SkillOwnersAt = 4;
        public const int Count = 5;

        /// <summary>새 배열(호출처가 고쳐도 계약이 안 흔들린다).</summary>
        public static string[] Default() => new[] { Cards, ActiveSkills, Config, Skills, SkillOwners };
    }
}
