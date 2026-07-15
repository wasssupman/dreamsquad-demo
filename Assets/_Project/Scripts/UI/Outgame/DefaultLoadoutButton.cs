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
        // The authored default deck, handed to ProfileStore. game-start-loadout-gate
        // unit 1 moved ownership there so the fresh-install path seeds the same deck
        // this button restores; a dev-only component must not be the only code that
        // knows what "default" means.
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

            // Squad and deck defaults both come from the path a fresh install takes;
            // this button must not invent a second definition of "default".
            var profile = ProfileStore.CreateDefault(defenderCatalog, defaultDeck, cardCatalog);

            // Replace the in-memory profile before saving: saving while the SO still
            // holds the old (or an empty) profile is how the squad gets wiped.
            profileSO.profile = profile;
            ProfileStore.Save(profile);

            Debug.Log($"[DefaultLoadout] squad={profile.SelectedSquad()?.unitIds.Count ?? 0} units, "
                + $"deck={profile.SelectedDeck()?.cardIds.Count ?? 0} cards → {ProfileStore.Path}", this);
        }
    }
}
