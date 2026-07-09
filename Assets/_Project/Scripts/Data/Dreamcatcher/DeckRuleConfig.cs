using UnityEngine;

namespace Wassup.Data
{
    // dreamcatcher-card-taxonomy unit 2 — tunable deck constraints. Referenced by
    // DreamcatcherCardCatalog; DeckRules reads it so designers adjust deck size and
    // per-type caps without code. Values here become the real constraint (save gate
    // + deck-builder blocking). A negative per-type cap means "no limit".
    [CreateAssetMenu(fileName = "DeckRuleConfig", menuName = "Wassup/DeckRuleConfig", order = 23)]
    public class DeckRuleConfig : ScriptableObject
    {
        public int deckSize = 10;
        public int maxSquad = 2;   // CardType.Squad cap
        public int maxUnit = -1;   // CardType.Unit cap; <0 = unlimited

        // Per-type cap; negative = unlimited.
        public int MaxFor(CardType type) => type == CardType.Squad ? maxSquad : maxUnit;
    }
}
