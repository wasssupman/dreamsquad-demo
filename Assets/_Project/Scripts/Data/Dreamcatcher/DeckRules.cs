using System.Collections.Generic;

namespace Wassup.Data
{
    // dreamcatcher-deck-builder Unit 1 — pure deck validity: exactly 10 cards,
    // at most 2 Squad-type. Single source of truth for the builder (save gate)
    // and the in-game carry-in (fallback decision). cardIds holds filled cards
    // only (the builder excludes empty slots).
    // dreamcatcher-card-taxonomy — cap moved from CardCategory.Unique to
    // CardType.Squad (squad-wide buffs are limited; unit cards may fill the rest).
    public static class DeckRules
    {
        public const int DeckSize = 10;
        public const int MaxSquad = 2;

        public static bool Validate(IReadOnlyList<string> cardIds, DreamcatcherCardCatalog catalog, out string reason)
        {
            int count = cardIds != null ? cardIds.Count : 0;
            if (count != DeckSize)
            {
                reason = $"need exactly {DeckSize} (have {count})";
                return false;
            }
            if (catalog == null)
            {
                reason = "no card catalog";
                return false;
            }

            int squad = 0;
            for (int i = 0; i < cardIds.Count; i++)
            {
                var card = catalog.ById(cardIds[i]);
                if (card == null)
                {
                    reason = $"unknown card: {cardIds[i]}";
                    return false;
                }
                if (card.type == CardType.Squad) squad++;
            }
            if (squad > MaxSquad)
            {
                reason = $"too many squad ({squad}/{MaxSquad})";
                return false;
            }

            reason = "ok";
            return true;
        }

        public static int SquadCount(IReadOnlyList<string> cardIds, DreamcatcherCardCatalog catalog)
        {
            if (cardIds == null || catalog == null) return 0;
            int squad = 0;
            for (int i = 0; i < cardIds.Count; i++)
            {
                var card = catalog.ById(cardIds[i]);
                if (card != null && card.type == CardType.Squad) squad++;
            }
            return squad;
        }
    }
}
