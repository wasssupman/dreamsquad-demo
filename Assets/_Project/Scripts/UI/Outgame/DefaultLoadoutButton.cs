using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Wassup.Core;
using Wassup.Data;

namespace Wassup.UI
{
    // outgame-login-gate unit 6 — dev button: put the squad and dreamcatcher deck
    // back to the starter defaults. Not a wipe — the profile ends up where a fresh
    // install would start. Written for the case where a deck rule change (e.g.
    // deckSize 10 -> 8) silently invalidates the saved deck and QA needs one click
    // back to a playable state.
    //
    // No confirm dialog, matching the RESET ACCOUNT precedent (internal demo).
    // The build gate and tray collapse live on DevButtons (DevOnlyGroup / unit 5),
    // so this component carries neither.
    public class DefaultLoadoutButton : MonoBehaviour
    {
        [SerializeField] private Button button;
        [SerializeField] private PlayerProfileSO profileSO;
        [SerializeField] private DefenderCatalog defenderCatalog;
        [SerializeField] private DreamcatcherCardCatalog cardCatalog;
        // The authored default deck. Dead data since the fallback deck was removed
        // (DreamcatcherHandController.cs:192) — this button revives it as the one
        // place the default deck is authored.
        [SerializeField] private DreamcatcherDeck defaultDeck;

        private void Awake()
        {
            if (button != null) button.onClick.AddListener(OnClick);
        }

        private void OnDestroy()
        {
            if (button != null) button.onClick.RemoveListener(OnClick);
        }

        private void OnClick()
        {
            if (profileSO == null)
            {
                Debug.LogWarning("[DefaultLoadout] profileSO unassigned — nothing reset.", this);
                return;
            }

            // Squad defaults come from the same path a fresh install takes; this
            // button must not invent a second definition of "default squad".
            var profile = ProfileStore.CreateDefault(defenderCatalog);

            var deck = BuildDefaultDeck(defaultDeck, DeckRules.EffectiveDeckSize(cardCatalog));
            profile.dreamcatcherDecks = new List<DeckSave> { deck };
            profile.selectedDeckId = deck.id;

            // Replace the in-memory profile before saving: saving while the SO still
            // holds the old (or an empty) profile is how the squad gets wiped.
            profileSO.profile = profile;
            ProfileStore.Save(profile);

            Debug.Log($"[DefaultLoadout] squad={profile.SelectedSquad()?.unitIds.Count ?? 0} units, "
                + $"deck={deck.cardIds.Count} cards → {ProfileStore.Path}", this);
        }

        // Take the authored deck in order, up to the current rule size, so a
        // deckSize change is followed automatically instead of re-authoring the asset.
        internal static DeckSave BuildDefaultDeck(DreamcatcherDeck source, int deckSize)
        {
            var save = new DeckSave { id = "deck_1", name = "Deck 1", cardIds = new List<string>() };
            if (source == null || source.cards == null) return save;
            for (int i = 0; i < source.cards.Length && save.cardIds.Count < deckSize; i++)
            {
                var card = source.cards[i];
                if (card != null && !string.IsNullOrEmpty(card.id)) save.cardIds.Add(card.id);
            }
            return save;
        }
    }
}
