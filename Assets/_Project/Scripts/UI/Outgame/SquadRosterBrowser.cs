using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Wassup.Data;

namespace Wassup.UI
{
    // squad-character-page Unit 2 — the right 2/3 roster grid. Scrollable cells
    // (portrait + rarity frame + name + selection highlight + "편성중" badge). A
    // tap raises EntrySelected(id); the orchestrator (unit 4) drives the detail
    // panel and calls SetSelected/SetBadged back. Cells are built from generic
    // entries (id/sprite/frame/label) so the same grid machinery serves the stone
    // mode (unit 3) — only the entry mapping differs.
    //
    // unit 18 — **유닛 그리드의 열 수는 이제 저작 대상이다.** 예전에는
    // GridLayoutGroup 이 Flexible 이라 열 수가 «패널폭 ÷ (셀폭+간격)» 으로 화면비에서
    // 파생됐다. 캔버스가 1920×1080 높이 매치라 참조 폭이 «1080 × 화면비» 이고, 그래서
    // 16:9 에서 7열이던 것이 20:9 폰에서는 9열이 됐다 — 어디에도 적히지 않은 숫자였다.
    // 지금은 반대로 «열 수 고정 → 셀 폭을 뷰포트에서 파생» 한다.
    //
    // 스톤 모드는 Flexible 그대로다(2026-08-20 사용자 결정). 스톤 130개를 큰 셀로 그리면
    // 스크롤이 과해진다 — 유닛만 키운다.
    public class SquadRosterBrowser : MonoBehaviour
    {
        [SerializeField] private TMP_FontAsset font;
        // unit 18 — 유닛 그리드 열 수. 셀 폭이 여기서 파생된다(그 반대가 아니다).
        [SerializeField] private int unitColumns = 5;
        // 기준 셀. 스톤 모드가 그대로 쓰고, 유닛 모드는 배율의 **기준선**으로만 쓴다.
        // unit 12 — 178→200: 라벨을 초상화 아래 독립 밴드로 내리기 위한 세로 여유.
        // 런타임 AddComponent 생성이라 씬 직렬화 값이 없다 → 이 기본값이 곧 실제 값.
        [SerializeField] private Vector2 cellSize = new Vector2(150, 200);
        [SerializeField] private Vector2 spacing = new Vector2(14, 14);

        public event Action<string> EntrySelected;

        private struct Entry { public string id; public Sprite sprite; public Color frame; public string label; }

        private static readonly Color CellBg = new Color(0.12f, 0.13f, 0.17f, 1f);
        private static readonly Color SelOverlayColor = new Color(1f, 0.95f, 0.6f, 0.20f);
        private static readonly Color BadgeColor = new Color(0.20f, 0.55f, 0.32f, 0.96f);
        // unit 12 — 라벨 밴드. 밝은 포트레이트 위에서도 흰 글씨가 읽히도록 깔아주는 바닥.
        private static readonly Color LabelBandColor = new Color(0f, 0f, 0f, 0.72f);

        // unit 18 — 기준 셀(150×200)에서의 치수. 전부 배율 하나(_scale)로만 파생된다.
        // 셀 높이 예산의 내역: 안쪽여백(6×2) + 초상화상단(6) + 초상화(cellW−24) + 간격(10)
        // + 라벨밴드(46) = cellW + 50. 150+50 = 200 으로 현행과 일치하므로 높이는
        // cellSize.y 를 그대로 비례 확대하면 된다.
        private const int GridPadding = 12;
        private const float BaseInnerPad = 6f;
        private const float BasePortraitMargin = 24f;
        private const float BasePortraitTop = 6f;
        private const float BaseLabelBandHeight = 46f;
        private const float BaseLabelFont = 24f;
        private const float BaseBadgeWidth = 88f;
        private const float BaseBadgeHeight = 34f;
        private const float BaseBadgeFont = 19f;
        private const float BaseBadgeMargin = 6f;

        private bool _built;
        private RectTransform _grid;
        private GridLayoutGroup _gridLayout;

        // 현재 적용 중인 셀 치수. AddCell 이 이 값들만 읽는다.
        private float _cellW;
        private float _cellH;
        private float _scale = 1f;

        private class Cell
        {
            public string id;
            public GameObject root;
            public Image portrait;
            public GameObject badge;
            public GameObject selOverlay;
        }

        private readonly List<Cell> _cells = new List<Cell>();
        private string _selectedId;
        private readonly HashSet<string> _badged = new HashSet<string>();

        // unit 18 — 뷰포트 폭에서 셀을 파생시키므로, 폭이 확정되기 전(빌드 직후 첫 프레임)이나
        // 해상도가 바뀌면 다시 그려야 한다. 마지막으로 그린 것을 들고 있다가 재구성한다.
        private readonly List<Entry> _entries = new List<Entry>();
        private bool _unitMode = true;
        private float _laidOutWidth = -1f;

        public void ShowUnits(IReadOnlyList<DefenderUnitData> units)
        {
            _unitMode = true;
            _entries.Clear();
            if (units != null)
                for (int i = 0; i < units.Count; i++)
                {
                    var u = units[i];
                    if (u == null) continue;
                    _entries.Add(new Entry
                    {
                        id = u.id,
                        sprite = u.portrait,
                        frame = UnitRarityStyle.Frame(u.rarity),
                        label = string.IsNullOrEmpty(u.displayName) ? u.name : u.displayName,
                    });
                }
            Rebuild();
        }

        // Unit 3 — stone mode reuses the same cell machinery; only the entry
        // mapping differs (icon + grade frame + effect summary).
        public void ShowStones(IReadOnlyList<DreamstoneData> stones)
        {
            _unitMode = false;
            _entries.Clear();
            if (stones != null)
                for (int i = 0; i < stones.Count; i++)
                {
                    var s = stones[i];
                    if (s == null) continue;
                    _entries.Add(new Entry
                    {
                        id = s.id,
                        sprite = s.icon,
                        frame = DreamstoneStyle.Frame(s.grade),
                        label = DreamstoneStyle.Summary(s),
                    });
                }
            Rebuild();
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
                foreach (var id in ids)
                    if (!string.IsNullOrEmpty(id)) _badged.Add(id);
            ApplyBadges();
        }

        // 뷰포트 폭이 확정되거나 바뀌면(첫 레이아웃 패스 · 해상도/종횡비 변경) 셀 폭이
        // 달라지므로 다시 그린다. 유닛 모드만 폭에 의존한다(스톤은 고정 셀).
        private void OnRectTransformDimensionsChange()
        {
            if (!_built || !_unitMode || _entries.Count == 0) return;
            if (Mathf.Abs(AvailableWidth() - _laidOutWidth) < 0.5f) return;
            Rebuild();
        }

        private void Rebuild()
        {
            EnsureGridBuilt();
            ApplyCellLayout();
            ClearCells();
            for (int i = 0; i < _entries.Count; i++) AddCell(_entries[i]);
            ApplyBadges();
            ApplySelection();
        }

        // 그리드가 쓸 수 있는 가로폭. **자기 rect** 에서 읽는다 — Scroll/Viewport/Grid 가
        // 전부 offset 0 stretch 라 폭이 동일하고, OnRectTransformDimensionsChange 는
        // 부모→자식 순서로 불려서 그 시점에 자식(Viewport)의 rect 는 아직 옛값일 수 있다.
        private float AvailableWidth() => ((RectTransform)transform).rect.width;

        // unit 18 — 열 수를 박고 셀 폭을 뷰포트에서 파생시킨다.
        private void ApplyCellLayout()
        {
            float available = AvailableWidth();
            _laidOutWidth = available;

            if (_unitMode && unitColumns > 0)
            {
                float inner = available - GridPadding * 2f - spacing.x * (unitColumns - 1);
                // 폭이 아직 확정되지 않은 프레임에서는 기준 셀로 그린다.
                // OnRectTransformDimensionsChange 가 확정되는 즉시 다시 부른다.
                _cellW = inner > 0f ? inner / unitColumns : cellSize.x;
                _gridLayout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
                _gridLayout.constraintCount = unitColumns;
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

        private void AddCell(Entry e)
        {
            float innerPad = BaseInnerPad * _scale;

            var root = new GameObject("Cell", typeof(RectTransform), typeof(Image), typeof(Button));
            root.transform.SetParent(_grid, false);
            ((RectTransform)root.transform).sizeDelta = new Vector2(_cellW, _cellH);
            root.GetComponent<Image>().color = e.frame; // rarity frame shows as border

            var inner = CreateImage(root.transform, CellBg, false);
            var irt = inner.rectTransform;
            irt.anchorMin = Vector2.zero; irt.anchorMax = Vector2.one;
            irt.offsetMin = new Vector2(innerPad, innerPad); irt.offsetMax = new Vector2(-innerPad, -innerPad);

            var portrait = CreateImage(inner.transform, Color.white, false);
            portrait.preserveAspect = true;
            var prt = portrait.rectTransform;
            prt.anchorMin = new Vector2(0.5f, 1f); prt.anchorMax = new Vector2(0.5f, 1f); prt.pivot = new Vector2(0.5f, 1f);
            float portraitSide = _cellW - BasePortraitMargin * _scale;
            prt.sizeDelta = new Vector2(portraitSide, portraitSide);
            prt.anchoredPosition = new Vector2(0, -BasePortraitTop * _scale);
            portrait.sprite = e.sprite;
            portrait.enabled = e.sprite != null;

            // unit 12 — 라벨은 초상화와 겹치지 않는 독립 밴드. 예전에는 텍스트가 배경
            // 없이 초상화 하단 16px 위에 얹혀 밝은 아트에서 묻혔다.
            var band = CreateImage(inner.transform, LabelBandColor, false);
            var bandRt = band.rectTransform;
            bandRt.anchorMin = new Vector2(0, 0); bandRt.anchorMax = new Vector2(1, 0); bandRt.pivot = new Vector2(0.5f, 0);
            bandRt.sizeDelta = new Vector2(0, BaseLabelBandHeight * _scale); bandRt.anchoredPosition = Vector2.zero;

            var label = CreateText(band.transform, e.label, BaseLabelFont * _scale, TextAlignmentOptions.Center);
            var lrt = label.rectTransform;
            lrt.anchorMin = Vector2.zero; lrt.anchorMax = Vector2.one;
            lrt.offsetMin = new Vector2(4 * _scale, 0); lrt.offsetMax = new Vector2(-4 * _scale, 0);

            // "편성중" badge — top-right pill, toggled by membership.
            var badge = new GameObject("Badge", typeof(RectTransform), typeof(Image));
            badge.transform.SetParent(root.transform, false);
            badge.GetComponent<Image>().color = BadgeColor;
            var brt = (RectTransform)badge.transform;
            brt.anchorMin = new Vector2(1, 1); brt.anchorMax = new Vector2(1, 1); brt.pivot = new Vector2(1, 1);
            brt.sizeDelta = new Vector2(BaseBadgeWidth * _scale, BaseBadgeHeight * _scale);
            brt.anchoredPosition = new Vector2(-BaseBadgeMargin * _scale, -BaseBadgeMargin * _scale);
            var btext = CreateText(badge.transform, "편성중", BaseBadgeFont * _scale, TextAlignmentOptions.Center);
            var btrt = btext.rectTransform;
            btrt.anchorMin = Vector2.zero; btrt.anchorMax = Vector2.one; btrt.offsetMin = Vector2.zero; btrt.offsetMax = Vector2.zero;
            badge.SetActive(false);

            // Selection overlay — full-cell warm tint, toggled on select.
            var sel = CreateImage(root.transform, SelOverlayColor, false);
            var srt = sel.rectTransform;
            srt.anchorMin = Vector2.zero; srt.anchorMax = Vector2.one; srt.offsetMin = Vector2.zero; srt.offsetMax = Vector2.zero;
            sel.gameObject.SetActive(false);

            string captured = e.id;
            root.GetComponent<Button>().onClick.AddListener(() => EntrySelected?.Invoke(captured));

            _cells.Add(new Cell { id = e.id, root = root, portrait = portrait, badge = badge, selOverlay = sel.gameObject });
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
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 28f;

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
            _gridLayout.cellSize = cellSize;
            _gridLayout.spacing = spacing;
            _gridLayout.padding = new RectOffset(GridPadding, GridPadding, GridPadding, GridPadding);
            _gridLayout.childAlignment = TextAnchor.UpperLeft;
            var fitter = gridGo.GetComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            scroll.viewport = vrt;
            scroll.content = _grid;

            UiLayer.Apply(gameObject);
        }

        private Image CreateImage(Transform parent, Color color, bool raycast)
        {
            var go = new GameObject("Image", typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var img = go.GetComponent<Image>();
            img.color = color;
            img.raycastTarget = raycast;
            return img;
        }

        private TMP_Text CreateText(Transform parent, string text, float size, TextAlignmentOptions align)
        {
            var go = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
            go.transform.SetParent(parent, false);
            var t = go.GetComponent<TextMeshProUGUI>();
            t.text = text; t.fontSize = size; t.alignment = align; t.color = Color.white;
            t.raycastTarget = false;
            if (font != null) t.font = font;
            return t;
        }
    }
}
