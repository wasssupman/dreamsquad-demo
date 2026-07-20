using UnityEngine;
using Wassup.Data;

namespace Wassup.UI
{
    // squad-character-page Unit 2 — shared rarity frame color for the character
    // page (detail panel glow + roster cell frame). Extracted from the detail
    // view on the second consumer. Color is presentation, so it lives in the UI
    // layer (labels are in Wassup.Data.UnitLabels; color stays here).
    public static class UnitRarityStyle
    {
        private static readonly Color Common = new Color(0.50f, 0.52f, 0.55f, 1f);
        private static readonly Color Rare = new Color(0.24f, 0.48f, 0.85f, 1f);
        private static readonly Color Epic = new Color(0.60f, 0.30f, 0.82f, 1f);
        private static readonly Color Ego = new Color(0.92f, 0.62f, 0.16f, 1f);

        public static Color Frame(DefenderRarity rarity)
        {
            switch (rarity)
            {
                case DefenderRarity.Rare: return Rare;
                case DefenderRarity.Epic: return Epic;
                case DefenderRarity.Ego: return Ego;
                default: return Common;
            }
        }
    }
}
