using System.Collections.Generic;
using UnityEngine;

namespace Wassup.Data
{
    // dreamcatcher-deck-builder Unit 0 — id -> DreamcatcherCard resolution for
    // deck save/load. Authoritative list of cards a deck can reference.
    [CreateAssetMenu(fileName = "DreamcatcherCardCatalog", menuName = "Wassup/DreamcatcherCardCatalog", order = 22)]
    public class DreamcatcherCardCatalog : ScriptableObject
    {
        public DreamcatcherCard[] cards;
        // dreamcatcher-card-taxonomy unit 2 — tunable deck constraints. Null =
        // DeckRules falls back to its default consts (10 / squad≤2 / unit∞).
        public DeckRuleConfig ruleConfig;

        public DreamcatcherCard ById(string id)
        {
            if (string.IsNullOrEmpty(id) || cards == null) return null;
            for (int i = 0; i < cards.Length; i++)
                if (cards[i] != null && cards[i].id == id) return cards[i];
            return null;
        }

        public IEnumerable<string> AllIds()
        {
            if (cards == null) yield break;
            for (int i = 0; i < cards.Length; i++)
                if (cards[i] != null && !string.IsNullOrEmpty(cards[i].id))
                    yield return cards[i].id;
        }
    }
}
