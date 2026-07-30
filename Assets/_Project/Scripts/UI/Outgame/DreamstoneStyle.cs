using UnityEngine;
using Wassup.Data;

namespace Wassup.UI
{
    // squad-character-page Unit 3 — shared dreamstone presentation (grade frame
    // color + "ATK +7.5%" effect summary). Lifted from the retired SquadBuilderView's private
    // GradeColor/StoneSummary so the character page's stone cells (browser), stone
    // slots (header), and stone detail all read one source. The legacy view is
    // retired in unit 4.
    public static class DreamstoneStyle
    {
        private static readonly Color Common = new Color(0.45f, 0.45f, 0.48f, 1f);
        private static readonly Color Rare = new Color(0.20f, 0.42f, 0.78f, 1f);
        private static readonly Color Epic = new Color(0.52f, 0.24f, 0.72f, 1f);
        private static readonly Color Unique = new Color(0.85f, 0.48f, 0.10f, 1f);

        public static Color Frame(DreamstoneGrade grade)
        {
            switch (grade)
            {
                case DreamstoneGrade.Rare: return Rare;
                case DreamstoneGrade.Epic: return Epic;
                case DreamstoneGrade.Unique: return Unique;
                default: return Common;
            }
        }

        public static string GradeLabel(DreamstoneGrade grade)
        {
            switch (grade)
            {
                case DreamstoneGrade.Rare: return "희귀";
                case DreamstoneGrade.Epic: return "영웅";
                case DreamstoneGrade.Unique: return "유니크";
                default: return "일반";
            }
        }

        // Mirrors DreamcatcherSelectionView.Summary's CardBuffKind abbreviations.
        public static string Summary(DreamstoneData stone)
        {
            if (stone == null) return "";
            string abbr = stone.effect.kind == CardBuffKind.AttackDamage ? "ATK"
                        : stone.effect.kind == CardBuffKind.AttackSpeed ? "AS"
                        : stone.effect.kind == CardBuffKind.EffectiveHealth ? "HP"
                        : stone.effect.kind == CardBuffKind.CostRate ? "COST"
                        : "MOVE";
            string sign = stone.effect.percent >= 0 ? "+" : "";
            return abbr + " " + sign + stone.effect.percent.ToString("0.#") + "%";
        }
    }
}
