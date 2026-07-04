using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Wassup.Core;
using Wassup.Data;

namespace Wassup.UI
{
    // squad-loadout Unit 2 — squad编성 screen inside SquadPanel. Builds slot
    // buttons at runtime from the catalog/profile, lets the player assign/clear
    // slots, and saves to disk. MVP: no class/trait/condition.
    //
    // dreamstone-loadout Unit 2 — reworked to "slot tap -> picker modal" (2026-07-04
    // UI review, option C). Main screen now shows slots only (unit 7 + stone 4);
    // the old inline owned-unit grid moved into a single runtime picker modal
    // shared by both unit and stone slots. Stone assignment goes through unit 1's
    // SquadSave.SetStoneSlot helper — duplicates allowed, no dedup.
    public class SquadBuilderView : MonoBehaviour
    {
        [SerializeField] private DefenderCatalog catalog;
        [SerializeField] private DreamstoneCatalog stoneCatalog;
        [SerializeField] private PlayerProfileSO profileSO;
        [SerializeField] private RectTransform slotsContainer;
        [SerializeField] private RectTransform stoneSlotsContainer;
        [SerializeField] private TMP_Text statusText;
        [SerializeField] private TMP_FontAsset font;

        private static readonly Color EmptySlotColor = new Color(0.16f, 0.18f, 0.24f, 1f);
        private static readonly Color OwnedUnitColor = new Color(0.18f, 0.32f, 0.62f, 1f);
        private static readonly Color GradeCommonColor = new Color(0.45f, 0.45f, 0.48f, 1f);
        private static readonly Color GradeRareColor = new Color(0.20f, 0.42f, 0.78f, 1f);
        private static readonly Color GradeEpicColor = new Color(0.52f, 0.24f, 0.72f, 1f);
        private static readonly Color GradeUniqueColor = new Color(0.85f, 0.48f, 0.10f, 1f);

        private readonly List<TMP_Text> _unitSlotLabels = new List<TMP_Text>();
        private readonly List<Image> _unitSlotBgs = new List<Image>();
        private readonly List<TMP_Text> _stoneSlotLabels = new List<TMP_Text>();
        private readonly List<Image> _stoneSlotBgs = new List<Image>();
        private bool _built;

        // Picker modal — one runtime overlay, reused for unit slots and stone slots.
        private enum PickerMode { Unit, Stone }
        private bool _pickerBuilt;
        private GameObject _pickerPanel;
        private RectTransform _pickerGrid;
        private TMP_Text _pickerTitle;
        private readonly List<GameObject> _pickerItems = new List<GameObject>();
        private PickerMode _pickerMode;
        private int _pickerSlotIndex = -1;

        private SquadSave Squad =>
            (profileSO != null && profileSO.profile != null) ? profileSO.profile.SelectedSquad() : null;

        private void OnEnable()
        {
            EnsureBuilt();
            Refresh();
        }

        // Structural fix (2026-07-04 리그 물리클릭 재현) — don't leave the picker modal
        // active on the root canvas after SquadPanel itself is disabled/closed.
        private void OnDisable()
        {
            ClosePicker();
        }

        private void EnsureBuilt()
        {
            if (_built) return;
            _built = true;
            BuildUnitSlots();
            BuildStoneSlots();
        }

        private void BuildUnitSlots()
        {
            for (int i = 0; i < SquadSave.SlotCount; i++)
            {
                int index = i;
                var btn = CreateButton("+", slotsContainer, new Vector2(120, 120), EmptySlotColor, out var label);
                btn.onClick.AddListener(() => OpenPicker(PickerMode.Unit, index));
                _unitSlotLabels.Add(label);
                _unitSlotBgs.Add(btn.GetComponent<Image>());
            }
        }

        private void BuildStoneSlots()
        {
            var container = EnsureStoneSlotsContainer();
            for (int i = 0; i < SquadSave.StoneSlotCount; i++)
            {
                int index = i;
                var btn = CreateButton("+", container, new Vector2(120, 120), EmptySlotColor, out var label);
                btn.onClick.AddListener(() => OpenPicker(PickerMode.Stone, index));
                _stoneSlotLabels.Add(label);
                _stoneSlotBgs.Add(btn.GetComponent<Image>());
            }
        }

        // dreamstone-loadout Unit 2 (rev) — scene authoring runtime fallback (wiring
        // minimization decision 2026-07-04, editor locked). If OutgameScene later wires
        // stoneSlotsContainer via Inspector, that assignment wins — this only runs when
        // the field is still unassigned. Mirrors the scene's "SlotsRow" (unit slot row)
        // layout: same RectTransform anchor/pivot/size + HorizontalLayoutGroup config,
        // placed one row below it.
        private RectTransform EnsureStoneSlotsContainer()
        {
            if (stoneSlotsContainer != null) return stoneSlotsContainer;

            Transform parent = slotsContainer != null ? slotsContainer.parent : transform;
            var go = new GameObject("StoneSlotsRow(Runtime)", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();

            if (slotsContainer != null)
            {
                rt.anchorMin = slotsContainer.anchorMin;
                rt.anchorMax = slotsContainer.anchorMax;
                rt.pivot = slotsContainer.pivot;
                rt.sizeDelta = slotsContainer.sizeDelta;
                rt.anchoredPosition = slotsContainer.anchoredPosition + new Vector2(0f, -(slotsContainer.sizeDelta.y + 20f));
            }
            else
            {
                // Both containers unassigned (test / malformed scene safety net) — the
                // scene's real SlotsRow values, so the fallback still looks reasonable.
                rt.anchorMin = new Vector2(0.5f, 1f);
                rt.anchorMax = new Vector2(0.5f, 1f);
                rt.pivot = new Vector2(0.5f, 1f);
                rt.sizeDelta = new Vector2(1000, 140);
                rt.anchoredPosition = Vector2.zero;
            }

            var hlg = go.AddComponent<HorizontalLayoutGroup>();
            var sourceHlg = slotsContainer != null ? slotsContainer.GetComponent<HorizontalLayoutGroup>() : null;
            if (sourceHlg != null)
            {
                hlg.padding = new RectOffset(sourceHlg.padding.left, sourceHlg.padding.right, sourceHlg.padding.top, sourceHlg.padding.bottom);
                hlg.spacing = sourceHlg.spacing;
                hlg.childAlignment = sourceHlg.childAlignment;
                hlg.childControlWidth = sourceHlg.childControlWidth;
                hlg.childControlHeight = sourceHlg.childControlHeight;
                hlg.childForceExpandWidth = sourceHlg.childForceExpandWidth;
                hlg.childForceExpandHeight = sourceHlg.childForceExpandHeight;
            }
            else
            {
                hlg.spacing = 12;
                hlg.childAlignment = TextAnchor.MiddleCenter;
                hlg.childControlWidth = false;
                hlg.childControlHeight = false;
                hlg.childForceExpandWidth = false;
                hlg.childForceExpandHeight = false;
            }

            UiLayer.Apply(go);
            stoneSlotsContainer = rt;
            return rt;
        }

        public void Refresh()
        {
            var squad = Squad;

            for (int i = 0; i < _unitSlotLabels.Count; i++)
            {
                string id = (squad != null && i < squad.unitIds.Count) ? squad.unitIds[i] : "";
                if (string.IsNullOrEmpty(id))
                {
                    _unitSlotLabels[i].text = "+";
                    _unitSlotBgs[i].color = EmptySlotColor;
                    continue;
                }
                var unit = catalog != null ? catalog.ById(id) : null;
                _unitSlotLabels[i].text = unit != null ? DisplayName(unit) : id;
                _unitSlotBgs[i].color = OwnedUnitColor;
            }

            for (int i = 0; i < _stoneSlotLabels.Count; i++)
            {
                string id = (squad != null && squad.stoneIds != null && i < squad.stoneIds.Count) ? squad.stoneIds[i] : "";
                if (string.IsNullOrEmpty(id))
                {
                    _stoneSlotLabels[i].text = "+";
                    _stoneSlotBgs[i].color = EmptySlotColor;
                    continue;
                }
                var stone = stoneCatalog != null ? stoneCatalog.ById(id) : null;
                if (stone == null)
                {
                    // Catalog missing this id (asset deleted) — show the raw id, neutral
                    // color; still clearable via the picker's [해제] (squad-loadout policy).
                    _stoneSlotLabels[i].text = id;
                    _stoneSlotBgs[i].color = EmptySlotColor;
                    continue;
                }
                _stoneSlotLabels[i].text = StoneSummary(stone);
                _stoneSlotBgs[i].color = GradeColor(stone.grade);
            }

            if (statusText != null)
                statusText.text = squad != null ? $"{squad.name}: {squad.FilledCount()}/{SquadSave.SlotCount}" : "No squad";
        }

        // -- Picker modal --------------------------------------------------

        private void OpenPicker(PickerMode mode, int slotIndex)
        {
            if (Squad == null) return;
            BuildPickerOnce();

            _pickerMode = mode;
            _pickerSlotIndex = slotIndex;
            ClearPickerItems();

            if (mode == PickerMode.Unit)
            {
                _pickerTitle.text = "SELECT UNIT";
                BuildUnitPickerItems();
            }
            else
            {
                _pickerTitle.text = "SELECT DREAMSTONE";
                BuildStonePickerItems();
            }

            UiLayer.Apply(_pickerPanel);
            // Structural raycast-priority fix (2026-07-04 리그 물리클릭 재현) — a nested
            // canvas's overrideSorting only raises RENDER order, not GraphicRaycaster
            // event priority, so taps still resolved to the main page's slots underneath
            // even with the modal visually on top. Same-canvas last-sibling wins both
            // render order and raycast priority (see BuildPickerOnce for the panel's
            // parent — the root Canvas, not a nested one).
            _pickerPanel.transform.SetAsLastSibling();
            _pickerPanel.SetActive(true);
        }

        private void ClosePicker()
        {
            if (_pickerPanel != null) _pickerPanel.SetActive(false);
        }

        private void BuildUnitPickerItems()
        {
            if (catalog == null || profileSO == null || profileSO.profile == null) return;
            foreach (var id in profileSO.profile.ownedUnitIds)
            {
                var unit = catalog.ById(id);
                string label = unit != null ? DisplayName(unit) : id;
                string captured = id;
                var btn = CreateButton(label, _pickerGrid, new Vector2(150, 70), OwnedUnitColor, out _);
                btn.onClick.AddListener(() => PickUnit(captured));
                _pickerItems.Add(btn.gameObject);
            }
        }

        private void BuildStonePickerItems()
        {
            if (stoneCatalog == null) return;
            foreach (var id in stoneCatalog.AllIds())
            {
                var stone = stoneCatalog.ById(id);
                if (stone == null) continue;
                string captured = id;
                var btn = CreateButton(StoneSummary(stone), _pickerGrid, new Vector2(150, 70), GradeColor(stone.grade), out _);
                btn.onClick.AddListener(() => PickStone(captured));
                _pickerItems.Add(btn.gameObject);
            }
        }

        private void ClearPickerItems()
        {
            for (int i = 0; i < _pickerItems.Count; i++) if (_pickerItems[i] != null) Destroy(_pickerItems[i]);
            _pickerItems.Clear();
        }

        private void PickUnit(string id)
        {
            var squad = Squad;
            if (squad == null || string.IsNullOrEmpty(id)) return;
            if (_pickerSlotIndex < 0 || _pickerSlotIndex >= squad.unitIds.Count) return;
            // Duplicate-assignment guard unchanged from the pre-picker UI (squad is a
            // set of units) — no-op, keep the modal open so the player can pick again.
            for (int i = 0; i < squad.unitIds.Count; i++)
                if (i != _pickerSlotIndex && squad.unitIds[i] == id) return;
            squad.unitIds[_pickerSlotIndex] = id;
            ClosePicker();
            Refresh();
        }

        private void PickStone(string id)
        {
            var squad = Squad;
            if (squad == null) return;
            squad.SetStoneSlot(_pickerSlotIndex, id);
            ClosePicker();
            Refresh();
        }

        private void OnPickerClear()
        {
            var squad = Squad;
            if (squad == null) { ClosePicker(); return; }
            if (_pickerMode == PickerMode.Unit)
            {
                if (_pickerSlotIndex >= 0 && _pickerSlotIndex < squad.unitIds.Count)
                    squad.unitIds[_pickerSlotIndex] = "";
            }
            else
            {
                squad.SetStoneSlot(_pickerSlotIndex, "");
            }
            ClosePicker();
            Refresh();
        }

        // Structural fix (2026-07-04 리그 물리클릭 재현) — a nested Canvas's
        // overrideSorting only raises RENDER order; GraphicRaycaster event priority
        // still follows the enclosing canvas's raycast order, so taps "through" the
        // modal resolved to the main page's slots underneath it (a unit-slot pick was
        // silently hitting a stone slot instead). Fix: no separate canvas — the picker
        // panel is a same-canvas sibling parented directly under the root Canvas, and
        // OpenPicker calls SetAsLastSibling() on open so it wins both render order and
        // raycast priority. Scrim: near-opaque full-screen Image blocks background input.
        private void BuildPickerOnce()
        {
            if (_pickerBuilt) return;
            _pickerBuilt = true;

            var hostCanvas = GetComponentInParent<Canvas>();
            Transform parent = hostCanvas != null ? hostCanvas.rootCanvas.transform : transform.root;

            _pickerPanel = new GameObject("StonePickerPanel", typeof(RectTransform), typeof(Image));
            _pickerPanel.transform.SetParent(parent, false);
            var prt = (RectTransform)_pickerPanel.transform;
            prt.anchorMin = Vector2.zero; prt.anchorMax = Vector2.one; prt.offsetMin = Vector2.zero; prt.offsetMax = Vector2.zero;
            _pickerPanel.GetComponent<Image>().color = new Color(0.03f, 0.03f, 0.06f, 0.94f);

            var titleGo = new GameObject("Title", typeof(RectTransform), typeof(TextMeshProUGUI));
            titleGo.transform.SetParent(_pickerPanel.transform, false);
            var trt = titleGo.GetComponent<RectTransform>();
            trt.sizeDelta = new Vector2(800, 80); trt.anchoredPosition = new Vector2(0, 380);
            _pickerTitle = titleGo.GetComponent<TextMeshProUGUI>();
            _pickerTitle.alignment = TextAlignmentOptions.Center; _pickerTitle.fontSize = 36; _pickerTitle.color = Color.white;
            if (font != null) _pickerTitle.font = font;

            var gridGo = new GameObject("Grid", typeof(RectTransform), typeof(GridLayoutGroup));
            gridGo.transform.SetParent(_pickerPanel.transform, false);
            _pickerGrid = gridGo.GetComponent<RectTransform>();
            // RC2 (2026-07-04) — grid band pinned below the slot rows (unit row ~-180,
            // stone row ~-480) so a stray click at the same screen coords right after
            // the modal closes cannot land on a slot button underneath it.
            _pickerGrid.sizeDelta = new Vector2(1400, 440); _pickerGrid.anchoredPosition = new Vector2(0, -170);
            var glg = gridGo.GetComponent<GridLayoutGroup>();
            glg.cellSize = new Vector2(150, 70);
            glg.spacing = new Vector2(12, 12);
            glg.childAlignment = TextAnchor.UpperCenter;

            var clearBtn = CreateButton("CLEAR", _pickerPanel.transform, new Vector2(200, 60), new Color(0.5f, 0.16f, 0.16f, 1f), out _);
            ((RectTransform)clearBtn.transform).anchoredPosition = new Vector2(-160, -455);
            clearBtn.onClick.AddListener(OnPickerClear);

            var closeBtn = CreateButton("CLOSE", _pickerPanel.transform, new Vector2(200, 60), new Color(0.28f, 0.28f, 0.32f, 1f), out _);
            ((RectTransform)closeBtn.transform).anchoredPosition = new Vector2(160, -455);
            closeBtn.onClick.AddListener(ClosePicker);

            UiLayer.Apply(_pickerPanel);
            _pickerPanel.SetActive(false);
        }

        public void OnSave()
        {
            if (profileSO == null || profileSO.profile == null) return;
            ProfileStore.Save(profileSO.profile);
            if (statusText != null) statusText.text = "Saved";
        }

        private static string DisplayName(DefenderUnitData u) =>
            string.IsNullOrEmpty(u.displayName) ? u.name : u.displayName;

        // dreamstone-loadout Unit 2 — "ATK +7.5%" style summary, abbreviations mirror
        // DreamcatcherSelectionView.Summary's CardBuffKind labels.
        private static string StoneSummary(DreamstoneData stone)
        {
            string abbr = stone.effect.kind == CardBuffKind.AttackDamage ? "ATK"
                        : stone.effect.kind == CardBuffKind.AttackSpeed ? "AS"
                        : stone.effect.kind == CardBuffKind.EffectiveHealth ? "HP"
                        : "MOVE";
            string sign = stone.effect.percent >= 0 ? "+" : "";
            return $"{abbr} {sign}{stone.effect.percent:0.#}%";
        }

        private static Color GradeColor(DreamstoneGrade grade)
        {
            switch (grade)
            {
                case DreamstoneGrade.Rare: return GradeRareColor;
                case DreamstoneGrade.Epic: return GradeEpicColor;
                case DreamstoneGrade.Unique: return GradeUniqueColor;
                default: return GradeCommonColor;
            }
        }

        private Button CreateButton(string text, Transform parent, Vector2 size, Color bg, out TMP_Text label)
        {
            var go = new GameObject("Button", typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            go.GetComponent<RectTransform>().sizeDelta = size;
            go.GetComponent<Image>().color = bg;
            var l = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
            l.transform.SetParent(go.transform, false);
            var lrt = l.GetComponent<RectTransform>();
            lrt.anchorMin = Vector2.zero; lrt.anchorMax = Vector2.one; lrt.offsetMin = Vector2.zero; lrt.offsetMax = Vector2.zero;
            label = l.GetComponent<TextMeshProUGUI>();
            label.text = text; label.alignment = TextAlignmentOptions.Center; label.fontSize = 20; label.color = Color.white;
            if (font != null) label.font = font;
            return go.GetComponent<Button>();
        }
    }
}
