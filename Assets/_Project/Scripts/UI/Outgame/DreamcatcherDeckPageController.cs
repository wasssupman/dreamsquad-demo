using System;
using System.Collections.Generic;
using UnityEngine;
using Wassup.Core;
using Wassup.Data;

namespace Wassup.UI
{
    // dreamcatcher-deck-page unit 3 — orchestrator. Owns the detail view, card
    // browser and deck strip and drives the working deck. Feature parity with the
    // retired DreamcatcherDeckBuilderView: add(cap)/remove/duplicates, Subconscious
    // excluded from the add pool (removable if already in deck).
    //
    // page-local-presets unit 4 — **편집은 더 이상 즉시 저장되지 않는다.** 예전 주석의
    // "Edits persist immediately" 는 폐기됐다: 편집은 작업본만 바꾸고 [저장]이 유일한
    // 기록 경로다. DeckRules.Validate 는 여전히 START 게이트 책임이라 유효하지 않은
    // 중간 덱도 저장할 수 있다.
    public class DreamcatcherDeckPageController : MonoBehaviour
    {
        [SerializeField] private DreamcatcherCardCatalog catalog;
        [SerializeField] private PlayerProfileSO profileSO;
        [SerializeField] private DreamcatcherCardDetailView detailView;
        [SerializeField] private DreamcatcherCardBrowser browser;
        [SerializeField] private DreamcatcherDeckStrip deckStrip;
        [SerializeField] private PresetBarView presetBar;
        [SerializeField] private ConfirmPopup confirmPopup;

        // page-local-presets unit 4 — 구 `DeckId = "deck_1"` 하드코딩은 제거됐다. 프리셋이
        // 30개인 세계에서 "덱은 deck_1 하나"는 틀린 전제이고, 매 편집이 selectedDeckId 를
        // 강제 대입하던 것도 [선택] 전용 권한을 침범한다.
        private const string IdPrefix = "deck_";

        [NonSerialized] internal Action<PlayerProfile> ProfileSaver = ProfileStore.Save;

        private readonly List<string> _working = new List<string>();
        private readonly List<DreamcatcherCard> _pool = new List<DreamcatcherCard>(); // addable (non-Subconscious)
        private string _selectedCardId; // grid/deck 어느 쪽을 눌러도 이 카드가 상세 대상
        private bool _wired;

        // ---- 작업본 ---------------------------------------------------------
        private string _viewingPresetId;
        private string _workingName = "";
        private readonly List<PresetBarView.Entry> _entries = new List<PresetBarView.Entry>();

        private PlayerProfile Profile => profileSO != null ? profileSO.profile : null;

        private DreamcatcherPreset StoredPreset(string id)
        {
            var p = Profile;
            if (p == null || p.dreamcatcherDecks == null || string.IsNullOrEmpty(id)) return null;
            for (int i = 0; i < p.dreamcatcherDecks.Count; i++)
                if (p.dreamcatcherDecks[i] != null && p.dreamcatcherDecks[i].id == id) return p.dreamcatcherDecks[i];
            return null;
        }

        private void OnEnable()
        {
            WireOnce();
            BuildPool();

            var p = Profile;
            if (p != null)
            {
                p.NormalizePresets();
                // 페이지 진입은 **확정 프리셋**을 디폴트로 보여준다.
                _viewingPresetId = p.selectedDeckId;
            }
            LoadWorking();
            if (browser != null) browser.ShowCards(SortedPool());
            _selectedCardId = _pool.Count > 0 ? _pool[0].id : null;
            RefreshAll();
            RefreshBarEntries();   // 페이지 진입 시 목록 1회 구성(없으면 팝업이 빈 채로 열린다)
        }

        private void WireOnce()
        {
            if (_wired) return;
            _wired = true;
            if (browser != null) browser.CardSelected += OnCardSelected;
            if (deckStrip != null) deckStrip.SlotTapped += OnSlotTapped;
            if (detailView != null) { detailView.AddClicked += OnAdd; detailView.RemoveClicked += OnRemove; }
            if (presetBar != null)
            {
                presetBar.PresetPicked += OnPresetPicked;
                presetBar.CreateClicked += OnCreatePreset;
                presetBar.CommitClicked += OnCommitPreset;
                presetBar.SaveClicked += OnSavePreset;
                presetBar.ResetClicked += OnResetWorking;
                presetBar.DeleteClicked += OnDeletePreset;
                presetBar.NameCommitted += OnNameCommitted;
            }
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

        // 확정분이 아니라 **보고 있는 프리셋**의 저장본을 작업본으로 복제한다.
        private void LoadWorking()
        {
            _working.Clear();
            var deck = StoredPreset(_viewingPresetId);
            _workingName = deck != null ? (deck.name ?? "") : "";
            if (deck != null && deck.cardIds != null)
                foreach (var id in deck.cardIds) if (!string.IsNullOrEmpty(id)) _working.Add(id);
        }

        // ---- selection / refresh -----------------------------------------

        private void RefreshAll()
        {
            if (deckStrip != null) { deckStrip.Refresh(_working); deckStrip.SetSelected(_selectedCardId); }
            if (browser != null) { browser.SetBadged(BadgedSet()); browser.SetSelected(_selectedCardId); }
            ShowSelectedDetail();
            RefreshBarState();
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
            // 저장하지 않는다 — [저장]이 유일한 기록 경로다(page-local-presets unit 4).
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
            // 저장하지 않는다 — [저장]이 유일한 기록 경로다(page-local-presets unit 4).
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

        // page-local-presets unit 4 — 디스크 쓰기. 프리셋 구조 변경(생성/삭제/확정)과
        // [저장]에서만 불린다. 편집 경로에서는 부르지 않는다.
        // review MEDIUM-4 — 구조 변경(생성·삭제·확정)은 **변이 전에** 물어야 한다. Save() 안에서만
        // 걸면 메모리에는 프리셋이 생겼는데 디스크에는 없는 상태로 조용히 갈린다.
        private bool CanPersist() =>
            Profile != null && profileSO != null && profileSO.IsLoadedThisSession;

        private void Save()
        {
            if (!CanPersist()) return;
            (ProfileSaver ?? ProfileStore.Save)(Profile);
        }

        private bool IsDirty() =>
            PresetDiff.IsDeckDirty(_workingName, _working, StoredPreset(_viewingPresetId));

        // ---- 프리셋 바 --------------------------------------------------------

        // 가벼운 갱신 — 이름/dirty/버튼 활성만. **내용 편집 경로가 쓰는 것.**
        private void RefreshBarState()
        {
            if (presetBar == null) return;
            var p = Profile;

            int count = p != null && p.dreamcatcherDecks != null ? p.dreamcatcherDecks.Count : 0;
            bool isCommitted = p != null && _viewingPresetId == p.selectedDeckId;
            bool dirty = IsDirty();

            presetBar.SetName(_workingName);
            presetBar.SetButtonEnabled(
                commit: !isCommitted,
                save: dirty,
                reset: _working.Count > 0,
                delete: !isCommitted && count > 1);
            // SetDirty 는 SetButtonEnabled 뒤 — [저장] 엑센트 색이 interactable 을 읽는다.
            presetBar.SetDirty(dirty);
        }

        // 목록 셀 전체 재구성. **구조 변경에서만** 부른다(생성·삭제·확정·저장·전환).
        // 카드 토글마다 부르면 매 탭 30셀을 다시 만들고, 아직 저장하지 않은 내용이 목록에
        // 새어 나간다 — 목록은 "저장된 프리셋들"이다.
        private void RefreshBarEntries()
        {
            if (presetBar == null) return;
            var p = Profile;

            _entries.Clear();
            if (p != null && p.dreamcatcherDecks != null)
            {
                for (int i = 0; i < p.dreamcatcherDecks.Count; i++)
                {
                    var d = p.dreamcatcherDecks[i];
                    if (d == null) continue;
                    _entries.Add(new PresetBarView.Entry
                    {
                        id = d.id,
                        name = d.name,
                        thumbs = Thumbs(d),
                        committed = d.id == p.selectedDeckId,
                    });
                }
            }

            int count = p != null && p.dreamcatcherDecks != null ? p.dreamcatcherDecks.Count : 0;
            presetBar.SetEntries(_entries, _viewingPresetId, count < PlayerProfile.MaxPresets);
            RefreshBarState();
        }

        // 목록 셀 썸네일 — 저장본의 앞 카드 아트. 스쿼드의 7 초상과 같은 자리를 쓴다.
        private Sprite[] Thumbs(DreamcatcherPreset d)
        {
            const int Max = 7;
            var arr = new Sprite[Max];
            if (d == null || d.cardIds == null || catalog == null) return arr;
            for (int i = 0; i < Max && i < d.cardIds.Count; i++)
            {
                var c = !string.IsNullOrEmpty(d.cardIds[i]) ? catalog.ById(d.cardIds[i]) : null;
                arr[i] = c != null ? c.art : null;
            }
            return arr;
        }

        // ---- 프리셋 조작 (구조 변경은 즉시 저장, 내용은 [저장]만) ------------

        private void OnPresetPicked(string id)
        {
            if (string.IsNullOrEmpty(id) || id == _viewingPresetId) return;
            if (IsDirty())
            {
                // review MEDIUM-5 — fail-closed (스쿼드 컨트롤러와 동일 정책).
                if (confirmPopup == null)
                {
                    Debug.LogError("[DeckPreset] confirmPopup 미주입 — 미저장 변경이 있어 프리셋 "
                        + "전환을 차단했다. 페이지 빌더의 주입을 확인할 것.", this);
                    return;
                }
                string captured = id;
                confirmPopup.Show(
                    "저장하지 않은 변경이 있습니다.\n이동하면 변경은 사라집니다.",
                    () => SwitchTo(captured), "이동");
                return;
            }
            SwitchTo(id);
        }

        private void SwitchTo(string id)
        {
            _viewingPresetId = id;
            LoadWorking();
            if (browser != null) browser.ShowCards(SortedPool());
            RefreshAll();
            RefreshBarEntries();   // 선택 하이라이트 이동 = 구조 변경
        }

        private void OnCreatePreset()
        {
            if (!CanPersist()) return;   // review MEDIUM-4 — 변이 전에 묻는다
            var p = Profile;
            if (p == null || p.dreamcatcherDecks == null) return;
            if (p.dreamcatcherDecks.Count >= PlayerProfile.MaxPresets) return;

            var ids = new List<string>(p.dreamcatcherDecks.Count);
            for (int i = 0; i < p.dreamcatcherDecks.Count; i++)
                if (p.dreamcatcherDecks[i] != null) ids.Add(p.dreamcatcherDecks[i].id);

            var created = new DreamcatcherPreset
            {
                id = PresetIds.NextId(ids, IdPrefix),
                name = "덱 " + (p.dreamcatcherDecks.Count + 1),
                cardIds = new List<string>(),
            };
            p.dreamcatcherDecks.Add(created);
            p.NormalizePresets();
            Save();                       // 구조 변경 = 즉시 디스크
            SwitchTo(created.id);
        }

        private void OnCommitPreset()
        {
            if (!CanPersist()) return;   // review MEDIUM-4
            var p = Profile;
            if (p == null || string.IsNullOrEmpty(_viewingPresetId)) return;
            if (StoredPreset(_viewingPresetId) == null) return;

            // 내용은 건드리지 않는다 — 확정은 "이 프리셋의 저장본을 반입한다"는 뜻이다.
            p.selectedDeckId = _viewingPresetId;
            Save();
            RefreshBarEntries();   // 확정 뱃지 이동
        }

        private void OnSavePreset()
        {
            if (!CanPersist()) return;   // review MEDIUM-4
            var stored = StoredPreset(_viewingPresetId);
            if (stored == null) return;

            stored.name = _workingName;
            // 유효하지 않은 중간 덱(9/10)도 저장된다 — START 는 LoadoutGate 가 막는다(기존 계약).
            stored.cardIds = new List<string>(_working);
            Save();
            RefreshBarEntries();   // dirty 꺼짐 + 썸네일/이름 갱신
        }

        // 리셋은 작업본만 비운다. 고정 칸이 없으므로 빈 리스트다.
        private void OnResetWorking()
        {
            _working.Clear();
            if (browser != null) browser.ShowCards(SortedPool());
            RefreshAll();
        }

        private void OnDeletePreset()
        {
            if (!CanPersist()) return;   // review MEDIUM-4
            var p = Profile;
            if (p == null || p.dreamcatcherDecks == null) return;
            if (_viewingPresetId == p.selectedDeckId) return;    // 확정분 보호
            if (p.dreamcatcherDecks.Count <= 1) return;          // 최소 1개 유지

            p.dreamcatcherDecks.RemoveAll(d => d != null && d.id == _viewingPresetId);
            p.NormalizePresets();
            Save();
            SwitchTo(p.selectedDeckId);   // 확정분으로 복귀
        }

        private void OnNameCommitted(string value)
        {
            _workingName = value ?? "";
            // review MEDIUM-3 — 목록 셀은 **저장본**의 이름을 그리므로 작업본 이름이 바뀐
            // 것만으로 목록을 재구성할 이유가 없다. [저장] 시점에 RefreshBarEntries 가 돈다.
            RefreshBarState();
        }
    }
}
