using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Wassup.Core;
using Wassup.Data;

namespace Wassup.UI
{
    // dreamcatcher-deck-builder Unit 2 — deck builder inside DreamcatcherPanel.
    // dreamcatcher-card-art Unit 2 — upgraded from flat color buttons to art
    // cards: each card is an image (tarot-style art from DreamcatcherCard.art)
    // stacked over an effect-text line (vertical column). The owned collection
    // is a 5-per-row vertically scrollable grid; deck slots use the same card
    // style in a 5-per-row grid. Layout is code-driven (containers are used as
    // anchors only) so no scene surgery is required.
    public class DreamcatcherDeckBuilderView : MonoBehaviour
    {
        [SerializeField] private DreamcatcherCardCatalog catalog;
        [SerializeField] private PlayerProfileSO profileSO;
        [SerializeField] private RectTransform deckContainer;
        [SerializeField] private RectTransform ownedContainer;
        [SerializeField] private TMP_Text statusText;
        [SerializeField] private Button saveButton;
        [SerializeField] private TMP_FontAsset font;

        private const string DeckId = "deck_1";
        private readonly List<string> _working = new List<string>();
        private bool _built;
        private RectTransform _deckGrid;   // grid parent for deck slot cards
        private RectTransform _ownedGrid;  // scroll content for owned cards

        // Detail popup widgets (built once, re-populated per tap).
        private GameObject _popupRoot;
        private Image _popupArt;
        private Image _popupArtFallback;
        private TMP_Text _popupTitle;
        private TMP_Text _popupEffect;
        private Button _popupActionBtn;
        private TMP_Text _popupActionLabel;
        private TMP_Text _popupActionHint;

        // Card geometry (reference resolution 1920x1080). Portrait art (2:3).
        private const int OwnedColumns = 5;
        private const int DeckColumns = 10; // deck tray = single row of 10 slots
        private static readonly Vector2 OwnedCell = new Vector2(190, 300);
        private static readonly Vector2 OwnedSpacing = new Vector2(16, 16);
        private static readonly Vector2 DeckSpacing = new Vector2(6, 6);
        // Deck slots (10 columns) are sized at runtime to fit the same content
        // width as the 5-column collection grid, so the "MY DECK" frame and the
        // collection line up on one shared, centered column.
        private Vector2 _deckCell = new Vector2(90, 135);
        private const float CardPad = 6f;

        // Confirm ("MY DECK") frame geometry + palette.
        // ui-tweak 2026-07-08 — 타이틀(PanelTitle, y -60..-180)과 겹치던 MY DECK 프레임을
        // 타이틀 박스 아래로 내려 타이틀이 가려지지 않게 한다.
        private const float DeckFrameTopY = -196f;
        private const float FrameHeaderH = 48f;
        private const float FramePadX = 26f;
        private const float FramePadBottom = 18f;
        private static readonly Color FrameBg = new Color(0.12f, 0.15f, 0.26f, 0.96f);
        private static readonly Color FrameAccent = new Color(0.90f, 0.72f, 0.34f, 1f);
        private static readonly Color HeaderGold = new Color(0.96f, 0.83f, 0.48f, 1f);
        private static readonly Color SectionMuted = new Color(0.68f, 0.72f, 0.82f, 1f);
        private static readonly Color ActionAdd = new Color(0.20f, 0.55f, 0.28f, 1f);
        private static readonly Color ActionRemove = new Color(0.55f, 0.22f, 0.24f, 1f);
        private static readonly Color ActionDisabled = new Color(0.28f, 0.30f, 0.36f, 1f);
        private static readonly Color DimOverlay = new Color(0.02f, 0.03f, 0.06f, 0.66f);

        private static readonly Color NormalFrame = new Color(0.14f, 0.17f, 0.28f, 1f);
        private static readonly Color UniqueFrame = new Color(0.42f, 0.30f, 0.08f, 1f);
        private static readonly Color ArtFallbackNormal = new Color(0.22f, 0.28f, 0.44f, 1f);
        private static readonly Color ArtFallbackUnique = new Color(0.55f, 0.40f, 0.14f, 1f);
        // dreamcatcher-subconscious-unit — 무의식(Subconscious) 등급 전용 프레임(보랏빛
        // dream 톤). category 우선 > 타입색. category 는 프레임 채색으로만 재활성.
        private static readonly Color SubconsciousFrame = new Color(0.34f, 0.18f, 0.48f, 1f);
        private static readonly Color ArtFallbackSubconscious = new Color(0.46f, 0.28f, 0.62f, 1f);

        private static bool IsSubconscious(DreamcatcherCard c) => c != null && c.category == CardCategory.Subconscious;
        private static Color FrameColorOf(DreamcatcherCard c)
            => IsSubconscious(c) ? SubconsciousFrame : (c != null && c.type == CardType.Unit ? UniqueFrame : NormalFrame);
        private static Color ArtFallbackOf(DreamcatcherCard c)
            => IsSubconscious(c) ? ArtFallbackSubconscious : (c != null && c.type == CardType.Unit ? ArtFallbackUnique : ArtFallbackNormal);

        private void OnEnable()
        {
            BuildLayoutOnce();
            LoadWorking();
            if (saveButton != null) saveButton.onClick.AddListener(OnSave);
            Refresh();
        }

        private void OnDisable()
        {
            if (saveButton != null) saveButton.onClick.RemoveListener(OnSave);
        }

        private void LoadWorking()
        {
            _working.Clear();
            var deck = (profileSO != null && profileSO.profile != null) ? profileSO.profile.SelectedDeck() : null;
            if (deck != null && deck.cardIds != null)
                foreach (var id in deck.cardIds) if (!string.IsNullOrEmpty(id)) _working.Add(id);
        }

        private void BuildLayoutOnce()
        {
            if (_built) return;
            _built = true;

            // One shared, centered content column drives both areas so the deck
            // frame and the collection grid line up on the same left/right edges.
            float contentWidth = OwnedColumns * OwnedCell.x + (OwnedColumns - 1) * OwnedSpacing.x;

            // Deck tray: single row of 10 slots inside a framed "confirm" area just
            // under the title. Slot width is derived so the tray fits inside the
            // shared content width (minus the frame padding).
            if (deckContainer != null)
            {
                float deckTrayWidth = contentWidth - 2f * FramePadX;
                float deckCellW = (deckTrayWidth - (DeckColumns - 1) * DeckSpacing.x) / DeckColumns;
                _deckCell = new Vector2(deckCellW, Mathf.Round(deckCellW * 1.5f));

                deckContainer.anchorMin = new Vector2(0.5f, 1f);
                deckContainer.anchorMax = new Vector2(0.5f, 1f);
                deckContainer.pivot = new Vector2(0.5f, 1f);
                deckContainer.anchoredPosition = new Vector2(0f, DeckFrameTopY - FrameHeaderH);
                deckContainer.sizeDelta = new Vector2(deckTrayWidth, _deckCell.y);
                BuildDeckFrame(deckContainer.parent, deckContainer.GetSiblingIndex(), deckTrayWidth);
                _deckGrid = BuildGrid(deckContainer, DeckColumns, _deckCell, DeckSpacing);
            }

            // Owned collection: scrollable 5-per-row grid filling the lower band
            // (above the status line + Save/Close buttons). Same centered width.
            if (ownedContainer != null)
            {
                ownedContainer.anchorMin = new Vector2(0.5f, 0f);
                ownedContainer.anchorMax = new Vector2(0.5f, 0f);
                ownedContainer.pivot = new Vector2(0.5f, 0f);
                // ui-tweak 2026-07-08 — 프레임을 내린 만큼 컬렉션 높이를 400 으로 줄여
                // 하단 상태텍스트(y 180..236)/버튼과 겹치지 않게 한다.
                const float ownedBottomY = 250f;
                const float ownedHeight = 400f;
                ownedContainer.anchoredPosition = new Vector2(0f, ownedBottomY);
                ownedContainer.sizeDelta = new Vector2(contentWidth, ownedHeight);
                _ownedGrid = BuildScrollGrid(ownedContainer, OwnedColumns, OwnedCell, OwnedSpacing);
                BuildSectionLabel(ownedContainer.parent, "COLLECTION", new Vector2(0f, ownedBottomY + ownedHeight + 4f), 22f, SectionMuted, FontStyles.Normal);
                // Owned cards are populated by Refresh() so equipped-card sorting
                // and dimming stay in sync with the current deck.
            }
        }

        // Rebuilt whenever the deck changes: cards already in the deck are pushed
        // to the back (after the available ones, catalog order preserved in each
        // group) and rendered dimmed.
        private void RebuildOwnedCards()
        {
            if (_ownedGrid == null || catalog == null) return;
            for (int i = _ownedGrid.childCount - 1; i >= 0; i--) Destroy(_ownedGrid.GetChild(i).gameObject);

            var available = new List<string>();
            var equipped = new List<string>();
            foreach (var id in catalog.AllIds())
            {
                // gift-phase unit 2 — 무의식(Subconscious) 카드는 "림의 선물" 전용.
                // 덱빌더 선택 풀에서 제외(이미 저장 덱에 있으면 트레이에선 제거만 가능).
                if (IsSubconscious(catalog.ById(id))) continue;
                (_working.Contains(id) ? equipped : available).Add(id);
            }

            foreach (var id in available)
            {
                var card = catalog.ById(id);
                CreateCardView(_ownedGrid, card, () => ShowCardPopup(card, false, -1), OwnedCell, false);
            }
            foreach (var id in equipped)
            {
                var card = catalog.ById(id);
                CreateCardView(_ownedGrid, card, () => ShowCardPopup(card, false, -1), OwnedCell, true);
            }
        }

        private void Refresh()
        {
            if (_deckGrid != null)
            {
                for (int i = _deckGrid.childCount - 1; i >= 0; i--) Destroy(_deckGrid.GetChild(i).gameObject);
                for (int i = 0; i < _working.Count; i++)
                {
                    var card = catalog != null ? catalog.ById(_working[i]) : null;
                    int index = i;
                    CreateCardView(_deckGrid, card, () => ShowCardPopup(card, true, index), _deckCell);
                }
            }

            RebuildOwnedCards();

            bool valid = DeckRules.Validate(_working, catalog, out var reason);
            int squad = DeckRules.SquadCount(_working, catalog);
            if (statusText != null)
                statusText.text = $"{_working.Count}/{DeckRules.EffectiveDeckSize(catalog)}  ·  squad {squad}/{DeckRules.EffectiveMax(catalog, CardType.Squad)}  ·  {reason}";
            if (saveButton != null) saveButton.interactable = valid;
        }

        private void AddCard(string id)
        {
            if (catalog == null) return;
            if (_working.Count >= DeckRules.EffectiveDeckSize(catalog)) return;
            var card = catalog.ById(id);
            if (card == null) return;
            int typeMax = DeckRules.EffectiveMax(catalog, card.type);
            if (typeMax >= 0 && DeckRules.TypeCount(_working, catalog, card.type) >= typeMax) return;
            _working.Add(id);
            Refresh();
        }

        private void RemoveAt(int index)
        {
            if (index < 0 || index >= _working.Count) return;
            _working.RemoveAt(index);
            Refresh();
        }

        private void OnSave()
        {
            if (profileSO == null || profileSO.profile == null) return;
            if (!DeckRules.Validate(_working, catalog, out _)) return;
            var profile = profileSO.profile;
            var deck = profile.SelectedDeck();
            if (deck == null || deck.id != DeckId)
            {
                deck = null;
                if (profile.dreamcatcherDecks != null)
                    foreach (var d in profile.dreamcatcherDecks) if (d != null && d.id == DeckId) deck = d;
                if (deck == null)
                {
                    deck = new DeckSave { id = DeckId, name = "Deck 1" };
                    profile.dreamcatcherDecks.Add(deck);
                }
            }
            deck.cardIds = new List<string>(_working);
            profile.selectedDeckId = DeckId;
            ProfileStore.Save(profile);
            if (statusText != null) statusText.text = "Saved";
        }

        // --- card view -------------------------------------------------------

        private void CreateCardView(Transform parent, DreamcatcherCard card, UnityEngine.Events.UnityAction onClick)
        {
            CreateCardView(parent, card, onClick, OwnedCell);
        }

        // Image-only card: the full art fills the framed cell. Effect text now
        // lives in the detail popup (opened by onClick). Unique cards keep the
        // gold frame tint. The art blocks no raycasts so the root button gets taps.
        // dimmed cards (already in the deck) get a translucent dark overlay.
        private void CreateCardView(Transform parent, DreamcatcherCard card, UnityEngine.Events.UnityAction onClick, Vector2 cell, bool dimmed = false)
        {
            // dreamcatcher-card-taxonomy — frame keys on TYPE (Unit=gold, Squad=blue).
            // dreamcatcher-subconscious-unit — 무의식 등급은 타입색보다 우선(보랏빛).
            var go = new GameObject("Card", typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.sizeDelta = cell;
            go.GetComponent<Image>().color = FrameColorOf(card);
            go.GetComponent<Button>().onClick.AddListener(onClick);

            var artGo = new GameObject("Art", typeof(RectTransform), typeof(Image));
            artGo.transform.SetParent(go.transform, false);
            var art = (RectTransform)artGo.transform;
            art.anchorMin = Vector2.zero;
            art.anchorMax = Vector2.one;
            art.offsetMin = new Vector2(CardPad, CardPad);
            art.offsetMax = new Vector2(-CardPad, -CardPad);
            var artImg = artGo.GetComponent<Image>();
            artImg.raycastTarget = false;
            if (card != null && card.art != null)
            {
                artImg.sprite = card.art;
                artImg.preserveAspect = true;
                artImg.color = Color.white;
            }
            else
            {
                artImg.sprite = null;
                artImg.color = ArtFallbackOf(card);
            }

            if (dimmed)
            {
                var dimGo = new GameObject("Dim", typeof(RectTransform), typeof(Image));
                dimGo.transform.SetParent(go.transform, false);
                var drt = (RectTransform)dimGo.transform;
                drt.anchorMin = Vector2.zero; drt.anchorMax = Vector2.one; drt.offsetMin = Vector2.zero; drt.offsetMax = Vector2.zero;
                var dimImg = dimGo.GetComponent<Image>();
                dimImg.color = DimOverlay;
                dimImg.raycastTarget = false;

                var badge = new GameObject("InDeck", typeof(RectTransform), typeof(TextMeshProUGUI));
                badge.transform.SetParent(go.transform, false);
                var brt = (RectTransform)badge.transform;
                brt.anchorMin = new Vector2(0f, 0f); brt.anchorMax = new Vector2(1f, 0f); brt.pivot = new Vector2(0.5f, 0f);
                brt.offsetMin = new Vector2(0f, 8f); brt.offsetMax = new Vector2(0f, 34f);
                var btmp = badge.GetComponent<TextMeshProUGUI>();
                btmp.text = "IN DECK";
                btmp.alignment = TextAlignmentOptions.Center;
                btmp.fontSize = 16f;
                btmp.fontStyle = FontStyles.Bold;
                btmp.color = HeaderGold;
                btmp.raycastTarget = false;
                if (font != null) btmp.font = font;
            }
        }

        // --- detail popup ----------------------------------------------------

        // Modal card detail: large art + stylish effect text + an add/remove
        // action + close. Built once, re-populated per tap. Outside click or the
        // X button closes it.
        private void ShowCardPopup(DreamcatcherCard card, bool fromDeck, int deckIndex)
        {
            if (card == null) return;
            EnsurePopup();
            _popupRoot.transform.SetAsLastSibling();

            _popupArt.sprite = card.art;
            _popupArt.enabled = card.art != null;
            _popupArtFallback.color = ArtFallbackOf(card); // dreamcatcher-subconscious-unit — 무의식 우선
            _popupArtFallback.enabled = card.art == null;

            _popupTitle.text = string.IsNullOrEmpty(card.displayName) ? card.id : card.displayName;
            _popupEffect.text = DreamcatcherCardText.Body(card);

            _popupActionBtn.onClick.RemoveAllListeners();
            if (fromDeck)
            {
                _popupActionLabel.text = "REMOVE FROM DECK";
                _popupActionBtn.image.color = ActionRemove;
                _popupActionBtn.interactable = true;
                _popupActionHint.text = string.Empty;
                _popupActionBtn.onClick.AddListener(() => { RemoveAt(deckIndex); HidePopup(); });
            }
            else
            {
                int deckMax = DeckRules.EffectiveDeckSize(catalog);
                int typeMax = DeckRules.EffectiveMax(catalog, card.type);
                bool full = _working.Count >= deckMax;
                bool typeBlock = typeMax >= 0 && DeckRules.TypeCount(_working, catalog, card.type) >= typeMax;
                bool canAdd = !full && !typeBlock;
                _popupActionLabel.text = "ADD TO DECK";
                _popupActionBtn.image.color = canAdd ? ActionAdd : ActionDisabled;
                _popupActionBtn.interactable = canAdd;
                _popupActionHint.text = full ? $"Deck full ({deckMax}/{deckMax})"
                    : typeBlock ? $"{card.type} limit ({typeMax}/{typeMax})" : string.Empty;
                string capturedId = card.id;
                _popupActionBtn.onClick.AddListener(() => { AddCard(capturedId); HidePopup(); });
            }

            _popupRoot.SetActive(true);
        }

        private void HidePopup()
        {
            if (_popupRoot != null) _popupRoot.SetActive(false);
        }

        private void EnsurePopup()
        {
            if (_popupRoot != null) return;

            // Full-screen dim overlay; clicking it (outside the panel) closes.
            _popupRoot = new GameObject("CardPopup", typeof(RectTransform), typeof(Image), typeof(Button));
            _popupRoot.transform.SetParent(transform, false);
            var ort = (RectTransform)_popupRoot.transform;
            ort.anchorMin = Vector2.zero; ort.anchorMax = Vector2.one; ort.offsetMin = Vector2.zero; ort.offsetMax = Vector2.zero;
            _popupRoot.GetComponent<Image>().color = UiOverlay.Dim;
            _popupRoot.GetComponent<Button>().transition = Selectable.Transition.None;
            _popupRoot.GetComponent<Button>().onClick.AddListener(HidePopup);

            // Gold border (slightly larger rect behind the panel).
            var border = new GameObject("Border", typeof(RectTransform), typeof(Image));
            border.transform.SetParent(_popupRoot.transform, false);
            var brt = (RectTransform)border.transform;
            brt.anchorMin = brt.anchorMax = new Vector2(0.5f, 0.5f);
            brt.pivot = new Vector2(0.5f, 0.5f);
            brt.sizeDelta = new Vector2(736f, 996f);
            border.GetComponent<Image>().color = FrameAccent;

            // Panel body — carries a no-op Button so clicks inside the panel are
            // consumed here and do NOT bubble up to the overlay's close handler
            // (uGUI routes a click to the nearest ancestor click handler).
            var panel = new GameObject("Panel", typeof(RectTransform), typeof(Image), typeof(Button));
            panel.transform.SetParent(_popupRoot.transform, false);
            var prt = (RectTransform)panel.transform;
            prt.anchorMin = prt.anchorMax = new Vector2(0.5f, 0.5f);
            prt.pivot = new Vector2(0.5f, 0.5f);
            prt.sizeDelta = new Vector2(728f, 988f);
            panel.GetComponent<Image>().color = new Color(0.11f, 0.13f, 0.23f, 1f);
            panel.GetComponent<Button>().transition = Selectable.Transition.None;

            // Top gold accent bar.
            var accent = new GameObject("Accent", typeof(RectTransform), typeof(Image));
            accent.transform.SetParent(panel.transform, false);
            var acrt = (RectTransform)accent.transform;
            acrt.anchorMin = new Vector2(0f, 1f); acrt.anchorMax = new Vector2(1f, 1f); acrt.pivot = new Vector2(0.5f, 1f);
            acrt.offsetMin = new Vector2(0f, -5f); acrt.offsetMax = new Vector2(0f, 0f);
            accent.GetComponent<Image>().color = FrameAccent;

            // Large art.
            var artGo = new GameObject("Art", typeof(RectTransform), typeof(Image));
            artGo.transform.SetParent(panel.transform, false);
            var art = (RectTransform)artGo.transform;
            art.anchorMin = new Vector2(0.5f, 1f); art.anchorMax = new Vector2(0.5f, 1f); art.pivot = new Vector2(0.5f, 1f);
            art.anchoredPosition = new Vector2(0f, -34f);
            art.sizeDelta = new Vector2(380f, 540f);
            _popupArt = artGo.GetComponent<Image>();
            _popupArt.preserveAspect = true;
            _popupArt.raycastTarget = false;

            var fbGo = new GameObject("ArtFallback", typeof(RectTransform), typeof(Image));
            fbGo.transform.SetParent(artGo.transform, false);
            var fb = (RectTransform)fbGo.transform;
            fb.anchorMin = Vector2.zero; fb.anchorMax = Vector2.one; fb.offsetMin = Vector2.zero; fb.offsetMax = Vector2.zero;
            _popupArtFallback = fbGo.GetComponent<Image>();
            _popupArtFallback.raycastTarget = false;

            _popupTitle = MakePopupText(panel.transform, new Vector2(0f, -586f), new Vector2(640f, 50f), 34f, HeaderGold, FontStyles.Bold);
            _popupEffect = MakePopupText(panel.transform, new Vector2(0f, -640f), new Vector2(620f, 150f), 28f, Color.white, FontStyles.Normal);
            _popupActionHint = MakePopupText(panel.transform, new Vector2(0f, -806f), new Vector2(560f, 26f), 20f, SectionMuted, FontStyles.Italic);

            // Action button (add/remove). Transition=None so image.color sticks
            // (default ColorTint would override it with the white normal color).
            var actGo = new GameObject("Action", typeof(RectTransform), typeof(Image), typeof(Button));
            actGo.transform.SetParent(panel.transform, false);
            var actrt = (RectTransform)actGo.transform;
            actrt.anchorMin = new Vector2(0.5f, 1f); actrt.anchorMax = new Vector2(0.5f, 1f); actrt.pivot = new Vector2(0.5f, 1f);
            actrt.anchoredPosition = new Vector2(0f, -844f);
            actrt.sizeDelta = new Vector2(400f, 88f);
            _popupActionBtn = actGo.GetComponent<Button>();
            _popupActionBtn.transition = Selectable.Transition.None;
            _popupActionLabel = MakePopupText(actGo.transform, Vector2.zero, new Vector2(400f, 88f), 30f, Color.white, FontStyles.Bold);
            _popupActionLabel.rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
            _popupActionLabel.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            _popupActionLabel.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            _popupActionLabel.rectTransform.anchoredPosition = Vector2.zero;
            _popupActionLabel.raycastTarget = false;

            // Close (X) — top-right of the panel.
            var closeGo = new GameObject("Close", typeof(RectTransform), typeof(Image), typeof(Button));
            closeGo.transform.SetParent(panel.transform, false);
            var crt = (RectTransform)closeGo.transform;
            crt.anchorMin = new Vector2(1f, 1f); crt.anchorMax = new Vector2(1f, 1f); crt.pivot = new Vector2(1f, 1f);
            crt.anchoredPosition = new Vector2(-14f, -14f);
            crt.sizeDelta = new Vector2(64f, 64f);
            closeGo.GetComponent<Image>().color = new Color(0.30f, 0.10f, 0.12f, 1f);
            var closeBtn = closeGo.GetComponent<Button>();
            closeBtn.transition = Selectable.Transition.None;
            closeBtn.onClick.AddListener(HidePopup);
            var xLabel = MakePopupText(closeGo.transform, Vector2.zero, new Vector2(64f, 64f), 34f, Color.white, FontStyles.Bold);
            xLabel.text = "X";
            xLabel.raycastTarget = false;
            xLabel.rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
            xLabel.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            xLabel.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            xLabel.rectTransform.anchoredPosition = Vector2.zero;
        }

        private TMP_Text MakePopupText(Transform parent, Vector2 anchoredPos, Vector2 size, float fontSize, Color color, FontStyles style)
        {
            var go = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = new Vector2(0.5f, 1f); rt.anchorMax = new Vector2(0.5f, 1f); rt.pivot = new Vector2(0.5f, 1f);
            rt.anchoredPosition = anchoredPos;
            rt.sizeDelta = size;
            var tmp = go.GetComponent<TextMeshProUGUI>();
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.fontSize = fontSize;
            tmp.color = color;
            tmp.fontStyle = style;
            if (font != null) tmp.font = font;
            return tmp;
        }

        // --- layout helpers --------------------------------------------------

        // Configures/creates a plain (non-scrolling) grid directly on the
        // container. Reuses an existing GridLayoutGroup if present.
        private static RectTransform BuildGrid(RectTransform container, int columns, Vector2 cell, Vector2 spacing)
        {
            var grid = container.GetComponent<GridLayoutGroup>();
            if (grid == null) grid = container.gameObject.AddComponent<GridLayoutGroup>();
            grid.cellSize = cell;
            grid.spacing = spacing;
            grid.startCorner = GridLayoutGroup.Corner.UpperLeft;
            grid.startAxis = GridLayoutGroup.Axis.Horizontal;
            grid.childAlignment = TextAnchor.UpperCenter;
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = columns;
            return container;
        }

        // Wraps the container in a vertical ScrollRect (Viewport + masked
        // Content grid). Returns the content RectTransform to parent cards into.
        private RectTransform BuildScrollGrid(RectTransform container, int columns, Vector2 cell, Vector2 spacing)
        {
            var oldGrid = container.GetComponent<GridLayoutGroup>();
            if (oldGrid != null) Destroy(oldGrid);

            var scroll = container.GetComponent<ScrollRect>();
            if (scroll == null) scroll = container.gameObject.AddComponent<ScrollRect>();
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 32f;

            var viewportGo = new GameObject("Viewport", typeof(RectTransform), typeof(Image), typeof(RectMask2D));
            viewportGo.transform.SetParent(container, false);
            var vrt = (RectTransform)viewportGo.transform;
            vrt.anchorMin = Vector2.zero;
            vrt.anchorMax = Vector2.one;
            vrt.offsetMin = Vector2.zero;
            vrt.offsetMax = Vector2.zero;
            vrt.pivot = new Vector2(0.5f, 0.5f);
            viewportGo.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.001f); // near-invisible, mask needs a graphic

            var contentGo = new GameObject("Content", typeof(RectTransform), typeof(GridLayoutGroup), typeof(ContentSizeFitter));
            contentGo.transform.SetParent(viewportGo.transform, false);
            var crt = (RectTransform)contentGo.transform;
            crt.anchorMin = new Vector2(0f, 1f);
            crt.anchorMax = new Vector2(1f, 1f);
            crt.pivot = new Vector2(0.5f, 1f);
            crt.anchoredPosition = Vector2.zero;
            crt.sizeDelta = Vector2.zero;

            var grid = contentGo.GetComponent<GridLayoutGroup>();
            grid.cellSize = cell;
            grid.spacing = spacing;
            grid.startCorner = GridLayoutGroup.Corner.UpperLeft;
            grid.startAxis = GridLayoutGroup.Axis.Horizontal;
            grid.childAlignment = TextAnchor.UpperCenter;
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = columns;

            var fitter = contentGo.GetComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            scroll.viewport = vrt;
            scroll.content = crt;
            return crt;
        }

        // Framed "confirm" area behind the deck tray: dark backing panel + gold
        // top accent + "MY DECK" header. Inserted just behind the deck tray so
        // the slot cards render on top.
        private void BuildDeckFrame(Transform parent, int siblingIndex, float deckWidth)
        {
            if (parent == null) return;

            var frameGo = new GameObject("DeckFrame", typeof(RectTransform), typeof(Image));
            frameGo.transform.SetParent(parent, false);
            frameGo.transform.SetSiblingIndex(siblingIndex);
            var frt = (RectTransform)frameGo.transform;
            frt.anchorMin = new Vector2(0.5f, 1f);
            frt.anchorMax = new Vector2(0.5f, 1f);
            frt.pivot = new Vector2(0.5f, 1f);
            frt.anchoredPosition = new Vector2(0f, DeckFrameTopY);
            frt.sizeDelta = new Vector2(deckWidth + 2f * FramePadX, FrameHeaderH + _deckCell.y + FramePadBottom);
            frameGo.GetComponent<Image>().color = FrameBg;

            var accentGo = new GameObject("Accent", typeof(RectTransform), typeof(Image));
            accentGo.transform.SetParent(frameGo.transform, false);
            var art = (RectTransform)accentGo.transform;
            art.anchorMin = new Vector2(0f, 1f);
            art.anchorMax = new Vector2(1f, 1f);
            art.pivot = new Vector2(0.5f, 1f);
            art.offsetMin = new Vector2(2f, -4f);
            art.offsetMax = new Vector2(-2f, 0f);
            accentGo.GetComponent<Image>().color = FrameAccent;

            var headerGo = new GameObject("Header", typeof(RectTransform), typeof(TextMeshProUGUI));
            headerGo.transform.SetParent(frameGo.transform, false);
            var hrt = (RectTransform)headerGo.transform;
            hrt.anchorMin = new Vector2(0f, 1f);
            hrt.anchorMax = new Vector2(1f, 1f);
            hrt.pivot = new Vector2(0.5f, 1f);
            hrt.sizeDelta = new Vector2(0f, FrameHeaderH);
            hrt.anchoredPosition = new Vector2(0f, -4f);
            var htmp = headerGo.GetComponent<TextMeshProUGUI>();
            htmp.text = "MY DECK";
            htmp.alignment = TextAlignmentOptions.Center;
            htmp.fontStyle = FontStyles.Bold;
            htmp.characterSpacing = 6f;
            htmp.fontSize = 26f;
            htmp.color = HeaderGold;
            if (font != null) htmp.font = font;
        }

        // A small section caption anchored to the bottom of the panel.
        private void BuildSectionLabel(Transform parent, string text, Vector2 anchoredPos, float size, Color color, FontStyles style)
        {
            if (parent == null) return;
            var go = new GameObject("SectionLabel", typeof(RectTransform), typeof(TextMeshProUGUI));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = new Vector2(0.5f, 0f);
            rt.anchorMax = new Vector2(0.5f, 0f);
            rt.pivot = new Vector2(0.5f, 0f);
            rt.anchoredPosition = anchoredPos;
            rt.sizeDelta = new Vector2(500f, 32f);
            var tmp = go.GetComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.fontStyle = style;
            tmp.characterSpacing = 6f;
            tmp.fontSize = size;
            tmp.color = color;
            if (font != null) tmp.font = font;
        }
    }
}
