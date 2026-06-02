using UnityEngine;

namespace Wassup.Data
{
    // ingame-dreamcatcher Unit 1 — the 10-card deck brought into a match.
    // Duplicate card references are allowed (they stack, per design). The fixed
    // default deck lives in DreamcatcherDeck_Default.asset; the save-deck builder
    // is follow-up D.
    [CreateAssetMenu(fileName = "DreamcatcherDeck", menuName = "Wassup/DreamcatcherDeck", order = 21)]
    public class DreamcatcherDeck : ScriptableObject
    {
        public DreamcatcherCard[] cards;
    }
}
