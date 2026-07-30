using System.Collections.Generic;
using Wassup.Data;

namespace Wassup.Core
{
    // game-start-loadout-gate Unit 0 — pure "may this profile start a match?"
    // check, driven by the lobby START button. Static utility next to ProfileStore
    // (not a Manager, per constraint 5); no scene or UI dependency so EditMode
    // tests can drive it directly.
    //
    // The squad rule lives here — this is its single owner. The deck rule does
    // not: it is delegated to DeckRules so the deck builder's save gate and this
    // start gate can never disagree.
    public enum LoadoutTarget { Squad, Deck }

    // One unmet requirement. `have`/`need` drive the "5/7" line; `reason` carries
    // failures that counts cannot explain (e.g. a full-size deck holding an id the
    // catalog does not know), which would otherwise render as a baffling "8/8".
    public readonly struct LoadoutShortfall
    {
        public readonly LoadoutTarget target;
        public readonly int have;
        public readonly int need;
        public readonly string reason;

        public LoadoutShortfall(LoadoutTarget target, int have, int need, string reason)
        {
            this.target = target;
            this.have = have;
            this.need = need;
            this.reason = reason;
        }
    }

    public static class LoadoutGate
    {
        // True = every requirement met (shortfalls left empty). False = shortfalls
        // holds one entry per unmet requirement, always Squad before Deck so the
        // popup's line order is stable. `shortfalls` is cleared on entry (callers
        // reuse one list) and may be null.
        //
        // Both catalogs must be assigned. A null catalog is a wiring error, not a
        // player-fixable shortfall: it would report requirements no amount of
        // editing can clear (a null card catalog falls back to DeckRules'
        // DefaultDeckSize of 10, which the deck builder caps below). Callers
        // pre-block on null refs — see OutgameMenuController.OnStartGame.
        public static bool Check(PlayerProfile p, DefenderCatalog units,
                                 DreamcatcherCardCatalog cards, List<LoadoutShortfall> shortfalls)
        {
            shortfalls?.Clear();
            bool ok = true;

            // What a correctly-filled squad actually fields. SquadPreset.SlotCount
            // (how many slots exist) and SquadDraw.FieldCount (how many deploy) are
            // independent constants that both happen to be 7; taking the lower keeps
            // the requirement reachable if they ever drift apart.
            int squadNeed = SquadPreset.SlotCount < SquadDraw.FieldCount ? SquadPreset.SlotCount : SquadDraw.FieldCount;
            int squadHave = DeployableUnitCount(p?.CommittedSquad(), units);
            if (squadHave != squadNeed)
            {
                shortfalls?.Add(new LoadoutShortfall(LoadoutTarget.Squad, squadHave, squadNeed, null));
                ok = false;
            }

            var deck = p?.CommittedDeck();
            // Validate handles a null card list (count 0 -> "need exactly N (have 0)"),
            // so an unselected deck needs no separate branch.
            if (!DeckRules.Validate(deck?.cardIds, cards, out var reason))
            {
                shortfalls?.Add(new LoadoutShortfall(LoadoutTarget.Deck,
                    deck != null ? deck.Count() : 0, DeckRules.EffectiveDeckSize(cards), reason));
                ok = false;
            }

            return ok;
        }

        // Units that would actually reach the field. SquadDraw.Resolve owns "which
        // ids deploy" (drop empties, de-dup, cap at FieldCount) and GameManager
        // runs it at match start; re-deriving that here would let the gate and the
        // match disagree. The gate only adds what Resolve deliberately omits —
        // catalog resolution — so a squad of stale ids reads as 0, not 7.
        private static int DeployableUnitCount(SquadPreset squad, DefenderCatalog units)
        {
            if (squad == null || units == null) return 0;

            int n = 0;
            var deployed = SquadDraw.Resolve(squad.unitIds);
            for (int i = 0; i < deployed.Count; i++)
                if (units.ById(deployed[i]) != null) n++;
            return n;
        }
    }
}
