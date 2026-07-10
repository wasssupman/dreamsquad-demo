using System.Collections.Generic;
using Unity.Entities;
using UnityEngine;
using Wassup.Bridge;
using Wassup.Data;

namespace Wassup.Core
{
    // dreamcatcher-awakening-hand unit 4 — match controller for the awakening
    // currency + CR-style cycling hand. Replaces the retired 3-choose-1 flow
    // (DreamcatcherController is scene-dormant, its code untouched).
    //
    // Owns: awakening gauge (Mono state), the 12-entry cycle deck (attach deck 10
    // + common Active cards from the per-match SkillLoadoutController roll), and
    // the entryId↔entity attach registry. Views (units 5~8) subscribe to
    // GaugeChanged/HandChanged and call the Commit* APIs at pending-commit time —
    // pending/cancel UX is entirely the view's job (spec contract 9).
    //
    // NO pause and NO slomo here: realtime is the contract (7); the slomo lease
    // belongs to the hand view (unit 6).
    public class DreamcatcherHandController : MonoBehaviour
    {
        [SerializeField] private BattleBridge bridge;
        [SerializeField] private AwakeningConfig config;
        // Attach-deck resolve chain (same pattern as the dormant controller):
        // validated saved deck via catalog → serialized fallback deck.
        [SerializeField] private PlayerProfileSO profileSO;
        [SerializeField] private DreamcatcherCardCatalog cardCatalog;
        [SerializeField] private DreamcatcherDeck fallbackDeck;
        // Active(common) wiring: the existing per-match skill roll stays the
        // source of truth (seed + logging untouched); each rolled SkillData is
        // translated to its wrapping Active card via this serialized list.
        [SerializeField] private SkillLoadoutController skillLoadout;
        [SerializeField] private DreamcatcherCard[] activeCards;

        public enum HandChangeReason { Reset, Used, Recovered }

        public event System.Action<int> GaugeChanged;
        public event System.Action<HandChangeReason> HandChanged;

        public int Gauge { get; private set; }
        public int GaugeMax => config != null ? config.gaugeMax : 100;
        public int HandSize => config != null ? config.handSize : 5;

        private DreamcatcherCycleDeck _deck;
        // entryId → (host defender, revocation handle). Unit cards: handle=-1
        // (their slots die with the entity — nothing to revoke). Squad cards
        // (unit 9): handle>0, revoked on host death so the squad-wide effect
        // ends with its owner. Reverse scan on death is O(attached).
        private readonly Dictionary<int, (Entity host, int handle)> _attachedTo =
            new Dictionary<int, (Entity, int)>();
        private readonly List<int> _recoverScratch = new List<int>();

        private void OnEnable()
        {
            if (GameManager.Instance != null)
                GameManager.Instance.PhaseChanged += OnPhaseChanged;
            if (bridge != null)
            {
                bridge.EnemyKilledAwakening += OnEnemyKilledAwakening;
                bridge.DefenderDied += OnDefenderDied;
            }
        }

        private void OnDisable()
        {
            if (GameManager.Instance != null)
                GameManager.Instance.PhaseChanged -= OnPhaseChanged;
            if (bridge != null)
            {
                bridge.EnemyKilledAwakening -= OnEnemyKilledAwakening;
                bridge.DefenderDied -= OnDefenderDied;
            }
        }

        // Reset invariants (critic M3): every Placement entry rebuilds the deck,
        // clears the attach registry, and resets the gauge. Subscriptions live in
        // OnEnable/OnDisable symmetrically, so no re-subscribe here.
        private void OnPhaseChanged(GamePhase phase)
        {
            if (phase != GamePhase.Placement) return;

            var cards = new List<DreamcatcherCard>(ResolveAttachDeck());
            AppendActiveCards(cards);
            int seed = GameManager.Instance != null ? GameManager.Instance.MatchSeed : 0;
            _deck = new DreamcatcherCycleDeck(cards, seed);
            _attachedTo.Clear();
            Gauge = config != null ? Mathf.Clamp(config.gaugeStart, 0, config.gaugeMax) : 0;
            GaugeChanged?.Invoke(Gauge);
            HandChanged?.Invoke(HandChangeReason.Reset);
            LogDeck(cards);
        }

        // Saved deck (validated, catalog-resolved) → serialized fallback. Mirrors
        // the dormant DreamcatcherController.ResolveDeck (that code stays as-is).
        private List<DreamcatcherCard> ResolveAttachDeck()
        {
            var result = new List<DreamcatcherCard>();
            var save = (profileSO != null && profileSO.profile != null) ? profileSO.profile.SelectedDeck() : null;
            if (save != null && cardCatalog != null && DeckRules.Validate(save.cardIds, cardCatalog, out _))
            {
                foreach (var id in save.cardIds)
                {
                    var card = cardCatalog.ById(id);
                    if (card != null) result.Add(card);
                }
                if (result.Count > 0) return result;
            }
            if (fallbackDeck != null && fallbackDeck.cards != null)
                foreach (var c in fallbackDeck.cards)
                    if (c != null) result.Add(c);
            return result;
        }

        // Common Active cards from the existing per-match roll. Fewer than the
        // rolled count (missing mapping / empty pool) → warn and proceed with
        // what exists; the hand/cycle logic is size-agnostic (critic M2).
        private void AppendActiveCards(List<DreamcatcherCard> cards)
        {
            if (skillLoadout == null || skillLoadout.Picked == null) return;
            foreach (var skill in skillLoadout.Picked)
            {
                if (skill == null) continue;
                var card = FindActiveCard(skill);
                if (card != null) cards.Add(card);
                else Debug.LogWarning($"[DreamcatcherHandController] No Active card wraps skill '{skill.id}' — skipped (queue runs short).");
            }
        }

        private DreamcatcherCard FindActiveCard(SkillData skill)
        {
            if (activeCards == null) return null;
            foreach (var card in activeCards)
                if (card != null && card.type == CardType.Active && card.skill == skill)
                    return card;
            return null;
        }

        // ── Gauge ────────────────────────────────────────────────────────────

        private void OnEnemyKilledAwakening(int reward) => GainAwakening(reward);

        private void OnDefenderDied(Entity entity, DefenderUnitData data)
        {
            GainAwakening(data != null ? data.awakeningReward : 0);

            // Card recovery: every entry hosted by the dead defender rejoins the
            // queue at the back (death order = recovery order). Squad entries
            // (handle>0) also revoke their squad-wide effect (unit 9).
            if (_deck == null || _attachedTo.Count == 0) return;
            _recoverScratch.Clear();
            foreach (var kv in _attachedTo)
                if (kv.Value.host == entity) _recoverScratch.Add(kv.Key);
            if (_recoverScratch.Count == 0) return;
            foreach (var entryId in _recoverScratch)
            {
                int handle = _attachedTo[entryId].handle;
                if (handle > 0 && bridge != null)
                    bridge.RevokeDreamcatcherEffects(handle);
                _attachedTo.Remove(entryId);
                _deck.Recover(entryId);
            }
            HandChanged?.Invoke(HandChangeReason.Recovered);
        }

        private void GainAwakening(int reward)
        {
            if (reward <= 0) return;
            int next = Mathf.Min(Gauge + reward, GaugeMax); // overflow is lost
            if (next == Gauge) return;
            Gauge = next;
            GaugeChanged?.Invoke(Gauge);
        }

        // ── Hand / use API (views call Commit* at pending-commit time) ───────

        public List<DreamcatcherCycleDeck.Entry> Hand() =>
            _deck != null ? _deck.Hand(HandSize) : new List<DreamcatcherCycleDeck.Entry>();

        public int CostOf(DreamcatcherCard card) =>
            (config != null && card != null) ? config.CostFor(card.type) : int.MaxValue;

        // Drag-start / dim gate: in hand + affordable. Unit-type target caps are
        // per-target and re-checked in CommitUnit.
        public bool CanUse(int entryId)
        {
            if (_deck == null || !_deck.TryGetCard(entryId, out var card)) return false;
            return Gauge >= CostOf(card);
        }

        // unit 9 — squad cards are HOST-BOUND: the effect hits the whole squad
        // but belongs to the host defender; host death revokes it and recycles
        // the card (same out-of-pool lifecycle as Unit cards).
        public bool CommitSquad(int entryId, Entity host)
        {
            if (!TryGetUsable(entryId, CardType.Squad, out var card)) return false;
            if (AtAttachCap(host, card)) return false;
            int handle = bridge.ApplyDreamcatcherCardHosted(card);
            if (handle < 0) return false; // contributed nothing — no spend
            return AttachAndSpend(entryId, card, host, handle);
        }

        public bool CommitUnit(int entryId, Entity target)
        {
            if (!TryGetUsable(entryId, CardType.Unit, out var card)) return false;
            if (AtAttachCap(target, card)) return false;
            // Apply first: a failed attach (entity gone, non-defender) must not
            // spend or cycle (contract 9).
            if (!bridge.ApplyDreamcatcherCardToUnit(target, card)) return false;
            return AttachAndSpend(entryId, card, target, handle: -1); // slots die with the entity — no revoke
        }

        // Shared attach tail: out-of-pool, host registry, spend, notify.
        private bool AttachAndSpend(int entryId, DreamcatcherCard card, Entity host, int handle)
        {
            if (!_deck.UseUnit(entryId, HandSize)) return false; // guarded by TryGetUsable
            _attachedTo[entryId] = (host, handle);
            Spend(card);
            HandChanged?.Invoke(HandChangeReason.Used);
            return true;
        }

        // Shared cap (unit 9): Unit + Squad attachments count together.
        private bool AtAttachCap(Entity host, DreamcatcherCard card)
        {
            if (CountAttachedTo(host) < (config != null ? config.maxAttachPerUnit : 3)) return false;
            Debug.Log($"[DreamcatcherHandController] '{card.id}' rejected — host at attach cap.");
            return true;
        }

        public bool CommitActiveTile(int entryId, Vector2Int cell)
        {
            if (!TryGetUsableActive(entryId, out var card)) return false;
            if (!bridge.CastSkillAtTile(card.skill, cell, out _)) return false;
            SpendAndRecycle(entryId, card);
            return true;
        }

        public bool CommitActiveDefender(int entryId, Vector2Int cell)
        {
            if (!TryGetUsableActive(entryId, out var card)) return false;
            if (!bridge.CastSkillOnDefender(card.skill, cell, out _)) return false;
            SpendAndRecycle(entryId, card);
            return true;
        }

        public bool CommitActivePortal(int entryId, Vector2Int entryTile, Vector2Int exitTile)
        {
            if (!TryGetUsableActive(entryId, out var card)) return false;
            if (!bridge.CastPortal(card.skill, entryTile, exitTile, out _)) return false;
            SpendAndRecycle(entryId, card);
            return true;
        }

        // ── internals ────────────────────────────────────────────────────────

        private bool TryGetUsable(int entryId, CardType expected, out DreamcatcherCard card)
        {
            card = null;
            if (_deck == null || bridge == null) return false;
            if (!_deck.TryGetCard(entryId, out card)) return false;
            if (card.type != expected) return false;
            return Gauge >= CostOf(card);
        }

        private bool TryGetUsableActive(int entryId, out DreamcatcherCard card)
        {
            if (!TryGetUsable(entryId, CardType.Active, out card)) return false;
            if (card.skill == null)
            {
                Debug.LogWarning($"[DreamcatcherHandController] Active card '{card.id}' has no skill — config error.");
                return false;
            }
            return true;
        }

        private void SpendAndRecycle(int entryId, DreamcatcherCard card)
        {
            _deck.UseAndRecycle(entryId, HandSize);
            Spend(card);
            HandChanged?.Invoke(HandChangeReason.Used);
        }

        private void Spend(DreamcatcherCard card)
        {
            Gauge = Mathf.Max(0, Gauge - CostOf(card));
            GaugeChanged?.Invoke(Gauge);
        }

        private int CountAttachedTo(Entity target)
        {
            int count = 0;
            foreach (var kv in _attachedTo)
                if (kv.Value.host == target) count++;
            return count;
        }

        private void LogDeck(List<DreamcatcherCard> cards)
        {
            var logger = GameManager.Instance?.Logger;
            if (logger == null) return;
            var ids = new List<string>(cards.Count);
            foreach (var card in cards)
                if (card != null) ids.Add(card.id);
            var save = (profileSO != null && profileSO.profile != null) ? profileSO.profile.SelectedDeck() : null;
            logger.SetDreamcatcherDeck(save != null ? save.id : "default",
                save != null ? save.name : "Default+Active", ids);
        }
    }
}
