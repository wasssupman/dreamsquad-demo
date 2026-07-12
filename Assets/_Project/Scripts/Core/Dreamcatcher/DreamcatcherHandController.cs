using System.Collections.Generic;
using Unity.Entities;
using UnityEngine;
using Wassup.Bridge;
using Wassup.Data;

namespace Wassup.Core
{
    // dreamcatcher-awakening-hand unit 4 — match controller for the awakening
    // currency + CR-style cycling hand. Replaces the retired 3-choose-1 flow
    // (구 DreamcatcherController 는 dreamcatcher-bridge-partial-cleanup unit 1 에서 삭제).
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
        // gift-phase unit 1 — 선물 이벤트 가중치/무의식 수량. 없으면 Lucid 고정 폴백.
        [SerializeField] private GiftConfig giftConfig;

        public enum HandChangeReason { Reset, Used, Recovered }

        public event System.Action<int> GaugeChanged;
        public event System.Action<HandChangeReason> HandChanged;
        // unit-dreamcatcher-icons unit 0 — fires only when the attach registry
        // actually changes (attach / death recovery / placement reset).
        // HandChanged is a superset: Active use fires Used without an attach change.
        public event System.Action AttachmentsChanged;

        // gift-phase unit 1 — Gift 페이즈에서 덱 조합이 끝나 GiftPhaseView 가 읽을 수
        // 있게 됐음을 알린다(연출 시작 트리거).
        public event System.Action GiftDeckReady;

        public GiftKind GiftKind => _giftKind;
        public IReadOnlyList<DreamcatcherCard> GiftBaseCards => _giftBaseCards;
        public IReadOnlyList<DreamcatcherCard> GiftAddedCards => _giftAddedCards;
        // 확정 12장 순서 = 실제 사이클 큐 초기 순서(연출 착지 대상). handSize=TotalCount 로
        // 전체 큐를 순서대로 반환. 부착 0 인 Gift 시점엔 12장 전부.
        public List<DreamcatcherCycleDeck.Entry> GiftFinalOrder() =>
            _deck != null ? _deck.Hand(_deck.TotalCount) : new List<DreamcatcherCycleDeck.Entry>();

        public int Gauge { get; private set; }
        public int GaugeMax => config != null ? config.gaugeMax : 100;
        public int HandSize => config != null ? config.handSize : 5;

        private DreamcatcherCycleDeck _deck;
        // gift-phase unit 1 — 선물 페이즈에서 확정한 조합 캐시. 배치 진입 시 _deck 재사용
        // (이중 셔플 방지: DreamcatcherCycleDeck 무변경, 연출은 GiftFinalOrder 로 순서 읽음).
        private GiftKind _giftKind = GiftKind.Lucid;
        private List<DreamcatcherCard> _giftBaseCards = new List<DreamcatcherCard>();
        private List<DreamcatcherCard> _giftAddedCards = new List<DreamcatcherCard>();
        private List<DreamcatcherCard> _lastComposedCards = new List<DreamcatcherCard>();
        // gift-phase (review M1) — 이번 배치 진입에서 Gift 가 덱을 구성했는지. 배치 진입 시
        // 소비한다. false 면 매 진입마다 새로 구성(원래 "배치 진입마다 재구성" 불변식) →
        // gift 우회 폴백 경로에서 재시작 시 stale/소비된 _deck 재사용을 막는다.
        private bool _giftDeckComposed;
        // entryId → (host defender, revocation handle). handle 0 = 무회수(엔티티 부착
        // Unit 카드: 슬롯이 엔티티와 함께 소멸, revoke 대상 없음). handle>0 = host 사망 시
        // revoke(Squad hosted 버프 unit 9 · placement-aura 오라). placement-aura unit 2 —
        // 예전 -1 관례는 폐기(ApplyDreamcatcherCardToUnit 이 int 규약으로 0/>0 반환).
        // Reverse scan on death is O(attached).
        private readonly Dictionary<int, (Entity host, int handle)> _attachedTo =
            new Dictionary<int, (Entity, int)>();
        private readonly List<int> _recoverScratch = new List<int>();
        private readonly List<int> _attachReadScratch = new List<int>();

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

        // gift-phase unit 1 — Gift 진입 시 덱을 조합·구성(단일 셔플, _deck 생성),
        // Placement 진입 시 그 _deck 을 재사용하며 게이지/부착만 리셋. Gift 우회 경로면
        // Placement 에서 폴백 구성(기존 동작). 구독은 OnEnable/OnDisable 대칭 유지.
        private void OnPhaseChanged(GamePhase phase)
        {
            if (phase == GamePhase.Gift) { BuildGiftDeck(); return; }
            if (phase != GamePhase.Placement) return;

            // Gift 페이즈가 이번 진입에서 _deck 을 구성했으면 그대로 재사용(이중 셔플 방지,
            // 연출 순서 == 인게임 순서). 아니면(Gift 우회) 매 진입마다 새로 구성한다.
            // 플래그는 진입마다 소비 — 다음 진입은 다시 Gift 구성 or 폴백을 강제한다.
            if (!_giftDeckComposed) BuildFallbackDeck();
            _giftDeckComposed = false;
            _attachedTo.Clear();
            AttachmentsChanged?.Invoke();
            Gauge = config != null ? Mathf.Clamp(config.gaugeStart, 0, config.gaugeMax) : 0;
            GaugeChanged?.Invoke(Gauge);
            HandChanged?.Invoke(HandChangeReason.Reset);
            LogDeck(_lastComposedCards);
        }

        // Gift 진입 시 선물 이벤트를 결정하고 확정 12장 덱을 구성한다. _deck 을 1회
        // 생성(단일 Fisher-Yates)하고 배치 진입 시 재사용. 게이지/부착 리셋은 배치에서
        // — Gift 동안 인게임 핸드는 아직 등장하지 않는다.
        private void BuildGiftDeck()
        {
            int seed = GameManager.Instance != null ? GameManager.Instance.MatchSeed : 0;
            _giftBaseCards = ResolveAttachDeck();
            _giftKind = giftConfig != null
                ? GiftDeckComposer.PickKind(seed, giftConfig.lucidWeight, giftConfig.rimWeight)
                : GiftKind.Lucid;

            _giftAddedCards = new List<DreamcatcherCard>();
            if (_giftKind == GiftKind.Lucid) AppendActiveCards(_giftAddedCards);
            else _giftAddedCards.AddRange(ResolveRimGift(seed));

            _lastComposedCards = new List<DreamcatcherCard>(_giftBaseCards);
            _lastComposedCards.AddRange(_giftAddedCards);
            _deck = new DreamcatcherCycleDeck(_lastComposedCards, seed);
            _giftDeckComposed = true; // 배치 진입이 이 _deck 을 재사용하도록(review M1)
            GiftDeckReady?.Invoke();
        }

        // Gift 우회(직접 배치 진입) 안전 폴백 — 기존 동작 그대로(저장10 + 롤 Active).
        private void BuildFallbackDeck()
        {
            _giftKind = GiftKind.Lucid;
            _giftBaseCards = ResolveAttachDeck();
            _giftAddedCards = new List<DreamcatcherCard>();
            AppendActiveCards(_giftAddedCards);
            _lastComposedCards = new List<DreamcatcherCard>(_giftBaseCards);
            _lastComposedCards.AddRange(_giftAddedCards);
            int seed = GameManager.Instance != null ? GameManager.Instance.MatchSeed : 0;
            _deck = new DreamcatcherCycleDeck(_lastComposedCards, seed);
        }

        // 림의 선물: 카탈로그의 무의식(Subconscious) 카드에서 시드로 N장. 풀 부족분은
        // non-Active·non-Subconscious 카드에서 임의 폴백(안전장치, unit 2 저작 후 미발동).
        private List<DreamcatcherCard> ResolveRimGift(int seed)
        {
            var pool = new List<DreamcatcherCard>();
            var fallback = new List<DreamcatcherCard>();
            if (cardCatalog != null && cardCatalog.cards != null)
                foreach (var c in cardCatalog.cards)
                {
                    if (c == null) continue;
                    if (c.category == CardCategory.Subconscious) pool.Add(c);
                    else if (c.type != CardType.Active) fallback.Add(c);
                }
            int count = giftConfig != null ? giftConfig.rimGiftCount : 2;
            return GiftDeckComposer.PickRim(pool, fallback, count, seed);
        }

        // Saved deck (validated, catalog-resolved) → serialized fallback.
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
            AttachmentsChanged?.Invoke(); // 위 early-return 들을 지나면 회수가 1건 이상
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

        // Drag-start / dim gate: in hand + affordable. Per-target attach caps are
        // re-checked in CommitAttach.
        public bool CanUse(int entryId)
        {
            if (_deck == null || !_deck.TryGetCard(entryId, out var card)) return false;
            return Gauge >= CostOf(card);
        }

        // dreamcatcher-taxonomy-cleanup unit 1 — single attach commit for both
        // host-attached kinds. Squad (axis-set buff anchored at host) and Unit
        // (host-only mechanics) share the whole lifecycle: cap check, apply,
        // out-of-pool, host↔handle registry, spend, host-death revoke/recycle.
        // The bridge dispatcher picks the apply machine by CardType — the caller
        // no longer forks. Active cards use the Commit*Active* skill paths.
        //
        // Apply first: a failed attach (entity gone, non-defender, contributed
        // nothing) must not spend or cycle (contract 9). Handle 규약: <0 실패 /
        // 0 무회수(엔티티 부착형: 슬롯이 엔티티와 함께 소멸) / >0 회수핸들(host 사망 시
        // RevokeDreamcatcherEffects — squad 버프·placement-aura 오라 회수).
        public bool CommitAttach(int entryId, Entity host)
        {
            if (!TryGetUsableAttach(entryId, out var card)) return false;
            if (AtAttachCap(host, card)) return false;
            int handle = bridge.ApplyDreamcatcherCard(host, card);
            if (handle < 0) return false; // contributed nothing — no spend
            return AttachAndSpend(entryId, card, host, handle);
        }

        // Shared attach tail: out-of-pool, host registry, spend, notify.
        private bool AttachAndSpend(int entryId, DreamcatcherCard card, Entity host, int handle)
        {
            if (!_deck.UseUnit(entryId, HandSize)) return false; // guarded by TryGetUsable
            _attachedTo[entryId] = (host, handle);
            AttachmentsChanged?.Invoke();
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

        // dreamcatcher-taxonomy-cleanup unit 1 — attach gate for both host-attached
        // kinds (Squad|Unit). Active is rejected (it uses the skill-cast paths).
        private bool TryGetUsableAttach(int entryId, out DreamcatcherCard card)
        {
            card = null;
            if (_deck == null || bridge == null) return false;
            if (!_deck.TryGetCard(entryId, out card)) return false;
            if (card.type == CardType.Active) return false;
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

        // unit-dreamcatcher-icons unit 0 — read-only attachment snapshot for
        // presentation (icon strips). Fills the caller's list (no allocation);
        // entryId-ascending order keeps strip order stable across rebuilds
        // (dictionary iteration order is not deterministic after removals).
        public void GetAttachments(List<(Entity host, DreamcatcherCard card)> results)
        {
            results.Clear();
            if (_deck == null || _attachedTo.Count == 0) return;
            _attachReadScratch.Clear();
            foreach (var kv in _attachedTo) _attachReadScratch.Add(kv.Key);
            _attachReadScratch.Sort();
            foreach (var entryId in _attachReadScratch)
                if (_deck.TryGetCard(entryId, out var card))
                    results.Add((_attachedTo[entryId].host, card));
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
