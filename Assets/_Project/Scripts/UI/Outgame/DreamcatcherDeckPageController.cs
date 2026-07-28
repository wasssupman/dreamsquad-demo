using System;
using System.Collections.Generic;
using UnityEngine;
using Wassup.Core;
using Wassup.Data;

namespace Wassup.UI
{
    // dreamcatcher-deck-page unit 3 — orchestrator. Owns the detail view, card
    // browser and deck strip and drives the working deck. Feature parity with the
    // old DreamcatcherDeckBuilderView: add(cap)/remove/duplicates, Subconscious
    // excluded from the add pool (removable if already in deck). Edits persist
    // immediately; DeckRules.Validate remains the start gate's responsibility.
    public class DreamcatcherDeckPageController : MonoBehaviour
    {
        [SerializeField] private DreamcatcherCardCatalog catalog;
        [SerializeField] private PlayerProfileSO profileSO;
        [SerializeField] private DreamcatcherCardDetailView detailView;
        [SerializeField] private DreamcatcherCardBrowser browser;
        [SerializeField] private DreamcatcherDeckStrip deckStrip;

        private const string DeckId = "deck_1";

        [NonSerialized] internal Action<PlayerProfile> ProfileSaver = ProfileStore.Save;

        private readonly List<string> _working = new List<string>();
        private readonly List<DreamcatcherCard> _pool = new List<DreamcatcherCard>(); // addable (non-Subconscious)
        private string _selectedCardId; // grid/deck 어느 쪽을 눌러도 이 카드가 상세 대상
        private bool _wired;

        private void OnEnable()
        {
            WireOnce();
            BuildPool();
            LoadWorking();
            if (browser != null) browser.ShowCards(SortedPool());
            _selectedCardId = _pool.Count > 0 ? _pool[0].id : null;
            RefreshAll();
        }

        private void WireOnce()
        {
            if (_wired) return;
            _wired = true;
            if (browser != null) browser.CardSelected += OnCardSelected;
            if (deckStrip != null) deckStrip.SlotTapped += OnSlotTapped;
            if (detailView != null) { detailView.AddClicked += OnAdd; detailView.RemoveClicked += OnRemove; }
        }

        private void BuildPool()
        {
            _pool.Clear();
            if (catalog == null) return;
            foreach (var id in catalog.AllIds())
            {
                var c = catalog.ById(id);
                if (c == null) continue;
                if (c.category == CardCategory.Subconscious) continue; // gift-phase only, not addable
                // dreamcatcher-card-visibility unit 1 — 시트에서 숨긴 카드. _pool 이 곧
                // 그리드 소스이자 추가 가능 목록이라 이 한 줄이 "보이지도, 넣을 수도 없다"를
                // 동시에 만든다. 이미 덱에 있는 숨김 카드를 여기서 빼지는 않는다 — 페이지
                // 진입은 읽기 전용이고, 장착 해제는 로그인 prune 담당이다.
                if (c.visible == 0) continue;
                _pool.Add(c);
            }
        }

        // unit 6 — deck cards first (deck order, matching the strip; pool members
        // only, so Subconscious stays out of the grid), the rest in catalog order.
        // Re-shown on every deck edit so the invariant holds live. The seen set
        // collapses duplicate ids from legacy saved decks to one cell.
        private List<DreamcatcherCard> SortedPool()
        {
            var sorted = new List<DreamcatcherCard>(_pool.Count);
            var seen = new HashSet<string>();
            for (int i = 0; i < _working.Count; i++)
            {
                string id = _working[i];
                if (string.IsNullOrEmpty(id) || !seen.Add(id)) continue;
                for (int p = 0; p < _pool.Count; p++)
                    if (_pool[p].id == id) { sorted.Add(_pool[p]); break; }
            }
            for (int i = 0; i < _pool.Count; i++)
                if (!seen.Contains(_pool[i].id)) sorted.Add(_pool[i]);
            return sorted;
        }

        private void LoadWorking()
        {
            _working.Clear();
            var deck = (profileSO != null && profileSO.profile != null) ? profileSO.profile.SelectedDeck() : null;
            if (deck != null && deck.cardIds != null)
                foreach (var id in deck.cardIds) if (!string.IsNullOrEmpty(id)) _working.Add(id);
        }

        // ---- selection / refresh -----------------------------------------

        private void RefreshAll()
        {
            if (deckStrip != null) { deckStrip.Refresh(_working); deckStrip.SetSelected(_selectedCardId); }
            if (browser != null) { browser.SetBadged(BadgedSet()); browser.SetSelected(_selectedCardId); }
            ShowSelectedDetail();
        }

        private void ShowSelectedDetail()
        {
            if (detailView == null) return;
            var card = catalog != null ? catalog.ById(_selectedCardId) : null;
            if (card == null) { detailView.Clear(); return; }
            // 유니크: 편성됨이면 [덱에서 제거]만, 아니면 [덱에 추가]만.
            bool inDeck = _working.Contains(_selectedCardId);
            bool canAdd = CanAdd(card, out string hint);
            detailView.ShowCard(card, canAdd, hint, inDeck);
        }

        // 편성된 카드 id 집합(그리드 "편성중" 뱃지).
        private HashSet<string> BadgedSet()
        {
            var set = new HashSet<string>();
            for (int i = 0; i < _working.Count; i++)
                if (!string.IsNullOrEmpty(_working[i])) set.Add(_working[i]);
            return set;
        }

        private bool CanAdd(DreamcatcherCard card, out string hint)
        {
            hint = "";
            if (card == null || catalog == null) return false;
            // 유니크: 이미 덱에 있으면 추가 불가(중복 금지).
            if (_working.Contains(card.id)) return false;
            int deckSize = DeckRules.EffectiveDeckSize(catalog);
            if (_working.Count >= deckSize) { hint = "덱이 가득 참 (" + deckSize + "/" + deckSize + ")"; return false; }
            int typeMax = DeckRules.EffectiveMax(catalog, card.type);
            if (typeMax >= 0 && DeckRules.TypeCount(_working, catalog, card.type) >= typeMax)
            {
                hint = card.type + " 제한 (" + typeMax + "/" + typeMax + ")";
                return false;
            }
            return true;
        }

        // ---- edits --------------------------------------------------------

        private void AddCard(string id)
        {
            var card = catalog != null ? catalog.ById(id) : null;
            if (card == null) return;
            if (!CanAdd(card, out _)) return; // dedup(이미 있으면 CanAdd=false)
            _working.Add(id);
            PersistWorking();
            if (browser != null) browser.ShowCards(SortedPool()); // unit 6 — live re-sort
            RefreshAll();
        }

        // 카드 id 기준으로 덱에서 한 장 제거(마지막 occurrence). 중복(Normal ×N)이면
        // 한 번에 한 장씩 줄어든다.
        private void RemoveOccurrence(string id)
        {
            if (string.IsNullOrEmpty(id)) return;
            int idx = _working.LastIndexOf(id);
            if (idx < 0) return;
            _working.RemoveAt(idx);
            PersistWorking();
            if (browser != null) browser.ShowCards(SortedPool()); // unit 6 — live re-sort
            RefreshAll();
        }

        // ---- events -------------------------------------------------------

        // 그리드 셀 탭 = 그 카드를 상세 대상으로. 덱 슬롯 탭도 같은 카드를 상세 대상으로
        // (두 경로 통일 — 편성된 카드면 상세에 [덱에서 제거]가 뜬다).
        private void OnCardSelected(string id)
        {
            _selectedCardId = id;
            RefreshAll();
        }

        private void OnSlotTapped(int index)
        {
            if (index < 0 || index >= _working.Count) return;
            _selectedCardId = _working[index];
            RefreshAll();
        }

        private void OnAdd() => AddCard(_selectedCardId);
        private void OnRemove() => RemoveOccurrence(_selectedCardId);

        // Every user edit persists, including an invalid intermediate deck. START is
        // still blocked by LoadoutGate; page entry/LoadWorking never call this.
        private void PersistWorking()
        {
            if (profileSO == null || profileSO.profile == null) return;
            if (!profileSO.IsLoadedThisSession) return;
            var profile = profileSO.profile;
            if (profile.dreamcatcherDecks == null) profile.dreamcatcherDecks = new List<DeckSave>();
            var deck = profile.SelectedDeck();
            if (deck == null || deck.id != DeckId)
            {
                deck = null;
                foreach (var d in profile.dreamcatcherDecks) if (d != null && d.id == DeckId) deck = d;
                if (deck == null)
                {
                    deck = new DeckSave { id = DeckId, name = "Deck 1" };
                    profile.dreamcatcherDecks.Add(deck);
                }
            }
            deck.cardIds = new List<string>(_working);
            profile.selectedDeckId = DeckId;
            (ProfileSaver ?? ProfileStore.Save)(profile);
        }
    }
}
