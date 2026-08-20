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
    // deck multiple times) instead of a boolean. 어떤 카드를 보여줄지는 호출측이
    // 정한다 — 이 뷰는 넘겨받은 목록을 그리기만 한다.
    //
    // squad-character-page unit 18 의 쌍 — **열 수가 저작 대상이다.** 예전에는
    // GridLayoutGroup 이 Flexible 이라 열 수가 «패널폭 ÷ (셀폭+간격)» 으로 화면비에서
    // 파생됐다(캔버스가 1080 높이 매치라 참조 폭 = 1080 × 화면비). 지금은 반대로
    // «열 수 고정 → 카드 폭을 패널에서 파생» 한다. 스쿼드 그리드와 같은 규칙·같은 폭이다
    // (두 페이지의 상세 패널이 똑같이 0.34 를 차지한다).
    public class DreamcatcherCardBrowser : MonoBehaviour
    {
        [SerializeField] private TMP_FontAsset font;
        // 카드 그리드 열 수. 카드 폭이 여기서 파생된다(그 반대가 아니다).
        [SerializeField] private int cardColumns = 5;
        // 기준 카드. 배율(_scale)의 기준선으로만 쓴다.
        [SerializeField] private Vector2 cellSize = new Vector2(150, 225);
        [SerializeField] private Vector2 spacing = new Vector2(14, 14);

        public event Action<string> CardSelected;

        private static readonly Color SelOverlayColor = new Color(1f, 0.95f, 0.6f, 0.20f);
        private static readonly Color BadgeColor = new Color(0.20f, 0.55f, 0.32f, 0.96f);

        // 기준 카드(150×225)에서의 치수. 전부 배율 하나로만 파생된다.
        private const int GridPadding = 12;
        // unit 7 — 떠 있는 [저장] 버튼을 비켜 마지막 행이 그 위로 스크롤되게 하는 여백.
        // **배율을 곱하지 않는다** — 버튼 크기는 카드와 무관한 화면 공간 값이다.
        private const int GridBottomPadding = 120;
        private const float BaseArtInset = 6f;
        private const float BaseArtBottom = 42f;
        private const float BaseLabelHeight = 36f;
        private const float BaseLabelOffsetY = 4f;
        private const float BaseLabelFont = 20f;
        private const float BaseBadgeWidth = 88f;
        private const float BaseBadgeHeight = 34f;
        private const float BaseBadgeFont = 19f;
        private const float BaseBadgeMargin = 6f;

        private bool _built;
        private RectTransform _grid;
        private GridLayoutGroup _gridLayout;

        // 현재 적용 중인 카드 치수. AddCell 이 이 값들만 읽는다.
        private float _cellW;
        private float _cellH;
        private float _scale = 1f;

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

        // 카드 폭을 패널 폭에서 파생시키므로, 폭이 확정되기 전(빌드 직후 첫 프레임)이나
        // 해상도가 바뀌면 다시 그려야 한다. 마지막으로 그린 목록을 들고 있다가 재구성한다.
        private readonly List<DreamcatcherCard> _shown = new List<DreamcatcherCard>();
        private float _laidOutWidth = -1f;

        public void ShowCards(IReadOnlyList<DreamcatcherCard> cards)
        {
            _shown.Clear();
            if (cards != null)
                for (int i = 0; i < cards.Count; i++)
                    if (cards[i] != null) _shown.Add(cards[i]);
            Rebuild();
        }

        private void OnRectTransformDimensionsChange()
        {
            if (!_built || _shown.Count == 0) return;
            if (Mathf.Abs(AvailableWidth() - _laidOutWidth) < 0.5f) return;
            Rebuild();
        }

        private void Rebuild()
        {
            EnsureGridBuilt();
            ApplyCellLayout();
            ClearCells();
            for (int i = 0; i < _shown.Count; i++) AddCell(_shown[i]);
            ApplyBadges();
            ApplySelection();
        }

        // 그리드가 쓸 수 있는 가로폭. **자기 rect** 에서 읽는다 — Scroll/Viewport/Grid 가
        // 전부 offset 0 stretch 라 폭이 같고, OnRectTransformDimensionsChange 는 부모→자식
        // 순서로 불려서 그 시점에 자식(Viewport)의 rect 는 아직 옛값일 수 있다.
        private float AvailableWidth() => ((RectTransform)transform).rect.width;

        // 열 수를 박고 카드 폭을 패널에서 파생시킨다.
        private void ApplyCellLayout()
        {
            float available = AvailableWidth();
            _laidOutWidth = available;

            if (cardColumns > 0)
            {
                float inner = available - GridPadding * 2f - spacing.x * (cardColumns - 1);
                // 폭이 아직 확정되지 않은 프레임에서는 기준 카드로 그린다.
                // OnRectTransformDimensionsChange 가 확정되는 즉시 다시 부른다.
                _cellW = inner > 0f ? inner / cardColumns : cellSize.x;
                _gridLayout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
                _gridLayout.constraintCount = cardColumns;
            }
            else
            {
                _cellW = cellSize.x;
                _gridLayout.constraint = GridLayoutGroup.Constraint.Flexible;
            }

            _scale = cellSize.x > 0f ? _cellW / cellSize.x : 1f;
            _cellH = cellSize.y * _scale;
            _gridLayout.cellSize = new Vector2(_cellW, _cellH);
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
            float inset = BaseArtInset * _scale;

            var root = new GameObject("Card", typeof(RectTransform), typeof(Image), typeof(Button));
            root.transform.SetParent(_grid, false);
            ((RectTransform)root.transform).sizeDelta = new Vector2(_cellW, _cellH);
            root.GetComponent<Image>().color = CardCategoryStyle.Frame(card); // frame border

            var art = CreateImage(root.transform, Color.white, false);
            var art_rt = art.rectTransform;
            art_rt.anchorMin = Vector2.zero; art_rt.anchorMax = Vector2.one;
            art_rt.offsetMin = new Vector2(inset, BaseArtBottom * _scale);
            art_rt.offsetMax = new Vector2(-inset, -inset);
            art.preserveAspect = true;
            if (card.art != null) { art.sprite = card.art; art.color = Color.white; }
            else { art.sprite = null; art.color = CardCategoryStyle.ArtFallback(card); }

            var label = CreateText(root.transform, string.IsNullOrEmpty(card.displayName) ? card.id : card.displayName,
                BaseLabelFont * _scale, TextAlignmentOptions.Center);
            var lrt = label.rectTransform;
            lrt.anchorMin = new Vector2(0, 0); lrt.anchorMax = new Vector2(1, 0); lrt.pivot = new Vector2(0.5f, 0);
            lrt.sizeDelta = new Vector2(0, BaseLabelHeight * _scale);
            lrt.anchoredPosition = new Vector2(0, BaseLabelOffsetY * _scale);

            // "편성중" 뱃지 — top-right, 덱에 있으면 노출(유니크: 있음/없음 불리언).
            var badge = new GameObject("InDeck", typeof(RectTransform), typeof(Image));
            badge.transform.SetParent(root.transform, false);
            badge.GetComponent<Image>().color = BadgeColor;
            var brt = (RectTransform)badge.transform;
            brt.anchorMin = new Vector2(1, 1); brt.anchorMax = new Vector2(1, 1); brt.pivot = new Vector2(1, 1);
            brt.sizeDelta = new Vector2(BaseBadgeWidth * _scale, BaseBadgeHeight * _scale);
            brt.anchoredPosition = new Vector2(-BaseBadgeMargin * _scale, -BaseBadgeMargin * _scale);
            var btext = CreateText(badge.transform, "편성중", BaseBadgeFont * _scale, TextAlignmentOptions.Center);
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
            _gridLayout = gridGo.GetComponent<GridLayoutGroup>();
            _gridLayout.cellSize = cellSize; _gridLayout.spacing = spacing;
            // unit 7 — bottom padding clears the floating save button so the last
            // row can scroll above it.
            _gridLayout.padding = new RectOffset(GridPadding, GridPadding, GridPadding, GridBottomPadding);
            _gridLayout.childAlignment = TextAnchor.UpperLeft;
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

        private TMP_Text CreateText(Transform parent, string text, float size, TextAlignmentOptions align)
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
