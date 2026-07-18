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
    public class SquadRosterBrowser : MonoBehaviour
    {
        [SerializeField] private TMP_FontAsset font;
        [SerializeField] private Vector2 cellSize = new Vector2(150, 178);
        [SerializeField] private Vector2 spacing = new Vector2(14, 14);

        public event Action<string> EntrySelected;

        private struct Entry { public string id; public Sprite sprite; public Color frame; public string label; }

        private static readonly Color CellBg = new Color(0.12f, 0.13f, 0.17f, 1f);
        private static readonly Color SelOverlayColor = new Color(1f, 0.95f, 0.6f, 0.20f);
        private static readonly Color BadgeColor = new Color(0.20f, 0.55f, 0.32f, 0.96f);

        private bool _built;
        private RectTransform _grid;

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

        public void ShowUnits(IReadOnlyList<DefenderUnitData> units)
        {
            EnsureGridBuilt();
            ClearCells();
            if (units == null) return;
            for (int i = 0; i < units.Count; i++)
            {
                var u = units[i];
                if (u == null) continue;
                AddCell(new Entry
                {
                    id = u.id,
                    sprite = u.portrait,
                    frame = UnitRarityStyle.Frame(u.rarity),
                    label = string.IsNullOrEmpty(u.displayName) ? u.name : u.displayName,
                });
            }
            ApplyBadges();
            ApplySelection();
        }

        // Unit 3 — stone mode reuses the same cell machinery; only the entry
        // mapping differs (icon + grade frame + effect summary).
        public void ShowStones(IReadOnlyList<DreamstoneData> stones)
        {
            EnsureGridBuilt();
            ClearCells();
            if (stones == null) return;
            for (int i = 0; i < stones.Count; i++)
            {
                var s = stones[i];
                if (s == null) continue;
                AddCell(new Entry
                {
                    id = s.id,
                    sprite = s.icon,
                    frame = DreamstoneStyle.Frame(s.grade),
                    label = DreamstoneStyle.Summary(s),
                });
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
                foreach (var id in ids)
                    if (!string.IsNullOrEmpty(id)) _badged.Add(id);
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

        private void AddCell(Entry e)
        {
            var root = new GameObject("Cell", typeof(RectTransform), typeof(Image), typeof(Button));
            root.transform.SetParent(_grid, false);
            ((RectTransform)root.transform).sizeDelta = cellSize;
            root.GetComponent<Image>().color = e.frame; // rarity frame shows as border

            var inner = CreateImage(root.transform, CellBg, false);
            var irt = inner.rectTransform;
            irt.anchorMin = Vector2.zero; irt.anchorMax = Vector2.one;
            irt.offsetMin = new Vector2(6, 6); irt.offsetMax = new Vector2(-6, -6);

            var portrait = CreateImage(inner.transform, Color.white, false);
            portrait.preserveAspect = true;
            var prt = portrait.rectTransform;
            prt.anchorMin = new Vector2(0.5f, 1f); prt.anchorMax = new Vector2(0.5f, 1f); prt.pivot = new Vector2(0.5f, 1f);
            prt.sizeDelta = new Vector2(cellSize.x - 24, cellSize.x - 24);
            prt.anchoredPosition = new Vector2(0, -8);
            portrait.sprite = e.sprite;
            portrait.enabled = e.sprite != null;

            var label = CreateText(inner.transform, e.label, 23, TextAlignmentOptions.Center);
            var lrt = label.rectTransform;
            lrt.anchorMin = new Vector2(0, 0); lrt.anchorMax = new Vector2(1, 0); lrt.pivot = new Vector2(0.5f, 0);
            lrt.sizeDelta = new Vector2(0, 42); lrt.anchoredPosition = new Vector2(0, 6);

            // "편성중" badge — top-right pill, toggled by membership.
            var badge = new GameObject("Badge", typeof(RectTransform), typeof(Image));
            badge.transform.SetParent(root.transform, false);
            badge.GetComponent<Image>().color = BadgeColor;
            var brt = (RectTransform)badge.transform;
            brt.anchorMin = new Vector2(1, 1); brt.anchorMax = new Vector2(1, 1); brt.pivot = new Vector2(1, 1);
            brt.sizeDelta = new Vector2(88, 34); brt.anchoredPosition = new Vector2(-6, -6);
            var btext = CreateText(badge.transform, "편성중", 19, TextAlignmentOptions.Center);
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
            var gridLayout = gridGo.GetComponent<GridLayoutGroup>();
            gridLayout.cellSize = cellSize;
            gridLayout.spacing = spacing;
            gridLayout.padding = new RectOffset(12, 12, 12, 12);
            gridLayout.childAlignment = TextAnchor.UpperLeft;
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

        private TMP_Text CreateText(Transform parent, string text, int size, TextAlignmentOptions align)
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
