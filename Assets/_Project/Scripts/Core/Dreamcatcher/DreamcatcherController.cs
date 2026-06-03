using System.Collections.Generic;
using UnityEngine;
using Wassup.Bridge;
using Wassup.Data;
using Wassup.UI;

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
        [SerializeField] private DreamcatcherSelectionView selectionView;

        private void OnEnable()
        {
            // First pick happens BEFORE placement (changed flow: 맵 설정 → 첫 드캐
            // 선택 → 배치). Entering the Placement phase is the trigger; it fires
            // once per match (SetPhase only fires on change) and works for both
            // squad and draft entries. The 5-wave picks come from the bridge.
            if (GameManager.Instance != null)
                GameManager.Instance.PhaseChanged += OnPhaseChanged;
            if (bridge != null)
                bridge.WaveMilestoneReached += OnWaveMilestone;
        }

        private void OnDisable()
        {
            if (GameManager.Instance != null)
                GameManager.Instance.PhaseChanged -= OnPhaseChanged;
            if (bridge != null)
                bridge.WaveMilestoneReached -= OnWaveMilestone;
        }

        private void OnPhaseChanged(GamePhase phase)
        {
            if (phase == GamePhase.Placement) OnSelectionTrigger();
        }

        private void OnWaveMilestone(int waveNumber) => OnSelectionTrigger();

        private void OnSelectionTrigger()
        {
            var three = Draw3();
            if (three.Count == 0) return;

            if (selectionView != null)
            {
                // Pause so enemies/cooldowns freeze while the player chooses.
                Time.timeScale = 0f;
                selectionView.Show(three, OnPicked);
            }
            else
            {
                // No UI wired (tests / headless): auto-pick the first draw.
                Pick(three[0]);
            }
        }

        private void OnPicked(DreamcatcherCard card)
        {
            Time.timeScale = 1f;
            Pick(card);
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
