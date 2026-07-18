namespace Wassup.Data
{
    // squad-character-page Unit 1 — shared enum -> Korean label mapping for
    // defender identity (class role, rarity). Extracted from UnitKitSummary on the
    // second consumer (the detail view badges) per the project's "extract on
    // repetition" rule. Pure presentation labels, not stats — colors stay in the
    // UI layer (rarity color is a view concern).
    public static class UnitLabels
    {
        public static string ClassLabel(DefenderClass role)
        {
            switch (role)
            {
                case DefenderClass.Ranger: return "레인저";
                case DefenderClass.Guardian: return "가디언";
                case DefenderClass.Fighter: return "파이터";
                case DefenderClass.Caster: return "캐스터";
                case DefenderClass.Support: return "서포트";
                default: return "";
            }
        }

        public static string RarityLabel(DefenderRarity rarity)
        {
            switch (rarity)
            {
                case DefenderRarity.Rare: return "희귀";
                case DefenderRarity.Epic: return "영웅";
                case DefenderRarity.Ego: return "에고";
                default: return "일반";
            }
        }
    }
}
