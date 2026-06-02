using System.Collections.Generic;
using UnityEngine;
using Wassup.Bridge;
using Wassup.Data;

namespace Wassup.Core
{
    // ingame-dreamcatcher Unit 3 — drives the in-game card selection. Subscribes to
    // BattleBridge triggers (first placement + every 5th wave), draws 3 cards from
    // the deck, and applies the chosen one. Scene-local MonoBehaviour, not a
    // singleton. Unit 4 wires the selection UI; until then OnTrigger auto-picks the
    // first drawn card so the flow is testable.
    public class DreamcatcherController : MonoBehaviour
    {
        [SerializeField] private BattleBridge bridge;
        [SerializeField] private DreamcatcherDeck deck;
        [SerializeField] private UnityEngine.Object selectionView; // Unit 4: DreamcatcherSelectionView

        private void OnEnable()
        {
            if (bridge == null) return;
            bridge.FirstDefenderPlaced += OnSelectionTrigger;
            bridge.WaveMilestoneReached += OnWaveMilestone;
        }

        private void OnDisable()
        {
            if (bridge == null) return;
            bridge.FirstDefenderPlaced -= OnSelectionTrigger;
            bridge.WaveMilestoneReached -= OnWaveMilestone;
        }

        private void OnWaveMilestone(int waveNumber) => OnSelectionTrigger();

        private void OnSelectionTrigger()
        {
            var three = Draw3();
            if (three.Count == 0) return;

            // Unit 4 replaces this with the selection modal. Fallback: auto-pick first.
            Pick(three[0]);
        }

        // Samples up to 3 cards from the deck. Duplicate deck entries stay as
        // independent draws (they stack on pick).
        private List<DreamcatcherCard> Draw3()
        {
            var result = new List<DreamcatcherCard>();
            if (deck == null || deck.cards == null) return result;

            var idx = new List<int>();
            for (int i = 0; i < deck.cards.Length; i++)
                if (deck.cards[i] != null) idx.Add(i);

            int take = Mathf.Min(3, idx.Count);
            for (int i = 0; i < take; i++)
            {
                int j = Random.Range(i, idx.Count);
                (idx[i], idx[j]) = (idx[j], idx[i]);
                result.Add(deck.cards[idx[i]]);
            }
            return result;
        }

        public void Pick(DreamcatcherCard card)
        {
            if (card == null || bridge == null) return;
            bridge.ApplyDreamcatcherCard(card);
        }
    }
}
