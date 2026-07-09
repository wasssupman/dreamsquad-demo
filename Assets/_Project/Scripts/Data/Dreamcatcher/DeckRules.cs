using System.Collections.Generic;

namespace Wassup.Data
{
    // dreamcatcher-deck-builder Unit 1 — pure deck validity: exact deck size +
    // per-type caps. Single source of truth for the builder (save gate) and the
    // in-game carry-in (fallback decision). cardIds holds filled cards only.
    // dreamcatcher-card-taxonomy — cap moved from CardCategory.Unique to per-type
    // (CardType.Squad ≤2 by default). unit 2 — the numbers come from the catalog's
    // DeckRuleConfig; the consts below are only the fallback when it is unassigned.
    public static class DeckRules
    {
        public const int DefaultDeckSize = 10;
        public const int DefaultMaxSquad = 2;
        public const int DefaultMaxUnit = -1; // <0 = unlimited

        // Effective numbers: catalog's config if present, else the fallback consts.
        public static int EffectiveDeckSize(DreamcatcherCardCatalog catalog)
            => catalog != null && catalog.ruleConfig != null ? catalog.ruleConfig.deckSize : DefaultDeckSize;

        public static int EffectiveMax(DreamcatcherCardCatalog catalog, CardType type)
        {
            if (catalog != null && catalog.ruleConfig != null) return catalog.ruleConfig.MaxFor(type);
            return type == CardType.Squad ? DefaultMaxSquad : DefaultMaxUnit;
        }

        public static bool Validate(IReadOnlyList<string> cardIds, DreamcatcherCardCatalog catalog, out string reason)
        {
            int deckSize = EffectiveDeckSize(catalog);
            int count = cardIds != null ? cardIds.Count : 0;
            if (count != deckSize)
            {
                reason = $"need exactly {deckSize} (have {count})";
                return false;
            }
            if (catalog == null)
            {
                reason = "no card catalog";
                return false;
            }

            int squad = 0, unit = 0;
            for (int i = 0; i < cardIds.Count; i++)
            {
                var card = catalog.ById(cardIds[i]);
                if (card == null)
                {
                    reason = $"unknown card: {cardIds[i]}";
                    return false;
                }
                if (card.type == CardType.Squad) squad++; else unit++;
            }

            int maxSquad = EffectiveMax(catalog, CardType.Squad);
            if (maxSquad >= 0 && squad > maxSquad)
            {
                reason = $"too many squad ({squad}/{maxSquad})";
                return false;
            }
            int maxUnit = EffectiveMax(catalog, CardType.Unit);
            if (maxUnit >= 0 && unit > maxUnit)
            {
                reason = $"too many unit ({unit}/{maxUnit})";
                return false;
            }

            reason = "ok";
            return true;
        }

        public static int TypeCount(IReadOnlyList<string> cardIds, DreamcatcherCardCatalog catalog, CardType type)
        {
            if (cardIds == null || catalog == null) return 0;
            int n = 0;
            for (int i = 0; i < cardIds.Count; i++)
            {
                var card = catalog.ById(cardIds[i]);
                if (card != null && card.type == type) n++;
            }
            return n;
        }

        public static int SquadCount(IReadOnlyList<string> cardIds, DreamcatcherCardCatalog catalog)
            => TypeCount(cardIds, catalog, CardType.Squad);
    }
}
