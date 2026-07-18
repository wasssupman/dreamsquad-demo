using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Wassup.Data;

namespace Wassup.UI
{
    // dreamcatcher-deck-page unit 1 — the right 2/3 collection grid. Card cells
    // (art + category frame + name + deck-count badge). Mirrors SquadRosterBrowser's
    // scroll/grid machinery; the difference is a COUNT badge (a card may sit in the
    // deck multiple times) instead of a boolean. Subconscious filtering is the
    // caller's job (only non-addable cards are passed in).
    public class DreamcatcherCardBrowser : MonoBehaviour
    {
        [SerializeField] private TMP_FontAsset font;
        [SerializeField] private Vector2 cellSize = new Vector2(150, 225);
        [SerializeField] private Vector2 spacing = new Vector2(14, 14);

        public event Action<string> CardSelected;

        private static readonly Color SelOverlayColor = new Color(1f, 0.95f, 0.6f, 0.20f);
        private static readonly Color BadgeColor = new Color(0.20f, 0.55f, 0.32f, 0.96f);

        private bool _built;
        private RectTransform _grid;

        private class Cell
        {
            public string id;
            public GameObject root;
            public GameObject selOverlay;
            public GameObject badge; // "편성중" (덱에 있으면 노출)
        }

        private readonly List<Cell> _cells = new List<Cell>();
        private string _selectedId;
        private readonly HashSet<string> _badged = new HashSet<string>();

        public void ShowCards(IReadOnlyList<DreamcatcherCard> cards)
        {
            EnsureGridBuilt();
            ClearCells();
            if (cards == null) return;
            for (int i = 0; i < cards.Count; i++)
            {
                var c = cards[i];
                if (c == null) continue;
                AddCell(c);
            }
            ApplyBadges();
            ApplySelection();
        }

        public void SetSelected(string id)
        {
            _selectedId = id;
            ApplySelection();
        }

        public void SetBadged(ISet<string> ids)
        {
            _badged.Clear();
            if (ids != null)
                foreach (var id in ids) if (!string.IsNullOrEmpty(id)) _badged.Add(id);
            ApplyBadges();
        }

        private void ApplySelection()
        {
            for (int i = 0; i < _cells.Count; i++)
            {
                bool sel = _cells[i].id == _selectedId;
                if (_cells[i].selOverlay != null) _cells[i].selOverlay.SetActive(sel);
                if (_cells[i].root != null)
                    _cells[i].root.transform.localScale = sel ? new Vector3(1.06f, 1.06f, 1f) : Vector3.one;
            }
        }

        private void ApplyBadges()
        {
            for (int i = 0; i < _cells.Count; i++)
                if (_cells[i].badge != null) _cells[i].badge.SetActive(_badged.Contains(_cells[i].id));
        }

        private void ClearCells()
        {
            for (int i = 0; i < _cells.Count; i++)
                if (_cells[i].root != null) Destroy(_cells[i].root);
            _cells.Clear();
        }

        private void AddCell(DreamcatcherCard card)
        {
            var root = new GameObject("Card", typeof(RectTransform), typeof(Image), typeof(Button));
            root.transform.SetParent(_grid, false);
            ((RectTransform)root.transform).sizeDelta = cellSize;
            root.GetComponent<Image>().color = CardCategoryStyle.Frame(card); // frame border

            var art = CreateImage(root.transform, Color.white, false);
            var art_rt = art.rectTransform;
            art_rt.anchorMin = Vector2.zero; art_rt.anchorMax = Vector2.one;
            art_rt.offsetMin = new Vector2(6, 42); art_rt.offsetMax = new Vector2(-6, -6);
            art.preserveAspect = true;
            if (card.art != null) { art.sprite = card.art; art.color = Color.white; }
            else { art.sprite = null; art.color = CardCategoryStyle.ArtFallback(card); }

            var label = CreateText(root.transform, string.IsNullOrEmpty(card.displayName) ? card.id : card.displayName, 20, TextAlignmentOptions.Center);
            var lrt = label.rectTransform;
            lrt.anchorMin = new Vector2(0, 0); lrt.anchorMax = new Vector2(1, 0); lrt.pivot = new Vector2(0.5f, 0);
            lrt.sizeDelta = new Vector2(0, 36); lrt.anchoredPosition = new Vector2(0, 4);

            // "편성중" 뱃지 — top-right, 덱에 있으면 노출(유니크: 있음/없음 불리언).
            var badge = new GameObject("InDeck", typeof(RectTransform), typeof(Image));
            badge.transform.SetParent(root.transform, false);
            badge.GetComponent<Image>().color = BadgeColor;
            var brt = (RectTransform)badge.transform;
            brt.anchorMin = new Vector2(1, 1); brt.anchorMax = new Vector2(1, 1); brt.pivot = new Vector2(1, 1);
            brt.sizeDelta = new Vector2(88, 34); brt.anchoredPosition = new Vector2(-6, -6);
            var btext = CreateText(badge.transform, "편성중", 19, TextAlignmentOptions.Center);
            btext.fontStyle = FontStyles.Bold;
            var ctrt = btext.rectTransform;
            ctrt.anchorMin = Vector2.zero; ctrt.anchorMax = Vector2.one; ctrt.offsetMin = Vector2.zero; ctrt.offsetMax = Vector2.zero;
            badge.SetActive(false);

            var sel = CreateImage(root.transform, SelOverlayColor, false);
            var srt = sel.rectTransform;
            srt.anchorMin = Vector2.zero; srt.anchorMax = Vector2.one; srt.offsetMin = Vector2.zero; srt.offsetMax = Vector2.zero;
            sel.gameObject.SetActive(false);

            string captured = card.id;
            root.GetComponent<Button>().onClick.AddListener(() => CardSelected?.Invoke(captured));

            _cells.Add(new Cell { id = card.id, root = root, selOverlay = sel.gameObject, badge = badge });
        }

        private void EnsureGridBuilt()
        {
            if (_built) return;
            _built = true;

            var selfRt = (RectTransform)transform;

            var scrollGo = new GameObject("Scroll", typeof(RectTransform), typeof(Image), typeof(ScrollRect));
            scrollGo.transform.SetParent(selfRt, false);
            var srt = (RectTransform)scrollGo.transform;
            srt.anchorMin = Vector2.zero; srt.anchorMax = Vector2.one; srt.offsetMin = Vector2.zero; srt.offsetMax = Vector2.zero;
            scrollGo.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0f);
            var scroll = scrollGo.GetComponent<ScrollRect>();
            scroll.horizontal = false; scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped; scroll.scrollSensitivity = 28f;

            var viewportGo = new GameObject("Viewport", typeof(RectTransform), typeof(Image), typeof(Mask));
            viewportGo.transform.SetParent(scrollGo.transform, false);
            var vrt = (RectTransform)viewportGo.transform;
            vrt.anchorMin = Vector2.zero; vrt.anchorMax = Vector2.one; vrt.offsetMin = Vector2.zero; vrt.offsetMax = Vector2.zero;
            viewportGo.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.02f);
            viewportGo.GetComponent<Mask>().showMaskGraphic = false;

            var gridGo = new GameObject("Grid", typeof(RectTransform), typeof(GridLayoutGroup), typeof(ContentSizeFitter));
            gridGo.transform.SetParent(viewportGo.transform, false);
            _grid = (RectTransform)gridGo.transform;
            _grid.anchorMin = new Vector2(0f, 1f); _grid.anchorMax = new Vector2(1f, 1f); _grid.pivot = new Vector2(0.5f, 1f);
            _grid.anchoredPosition = Vector2.zero; _grid.sizeDelta = new Vector2(0, cellSize.y);
            var gridLayout = gridGo.GetComponent<GridLayoutGroup>();
            gridLayout.cellSize = cellSize; gridLayout.spacing = spacing;
            gridLayout.padding = new RectOffset(12, 12, 12, 12);
            gridLayout.childAlignment = TextAnchor.UpperLeft;
            var fitter = gridGo.GetComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            scroll.viewport = vrt; scroll.content = _grid;

            UiLayer.Apply(gameObject);
        }

        private Image CreateImage(Transform parent, Color color, bool raycast)
        {
            var go = new GameObject("Image", typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var img = go.GetComponent<Image>();
            img.color = color; img.raycastTarget = raycast;
            return img;
        }

        private TMP_Text CreateText(Transform parent, string text, int size, TextAlignmentOptions align)
        {
            var go = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
            go.transform.SetParent(parent, false);
            var t = go.GetComponent<TextMeshProUGUI>();
            t.text = text; t.fontSize = size; t.alignment = align; t.color = Color.white; t.raycastTarget = false;
            if (font != null) t.font = font;
            return t;
        }
    }
}
