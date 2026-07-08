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
    // SquadSave.SetStoneSlot helper.
    //
    // dreamstone-loadout Unit 5 (rev 2026-07-06b) — stones are 64 flat, individually
    // owned items (stone_001..stone_064, no grouping/duplicate concept at all). The
    // picker lists every item; an equipped id is dimmed and unselectable — the exact
    // same "one item, one slot" rule as the unit picker (no special-case stone
    // policy anymore). Stat tiers within a kind (e.g. 4 Unique Attack Stones) differ
    // by %, so which specific item a player equips matters, unlike the discarded
    // first draft of this unit (grouped-by-kind, count badge, first-unequipped-pick).
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
        private readonly List<Image> _unitSlotIcons = new List<Image>();
        private readonly List<TMP_Text> _stoneSlotLabels = new List<TMP_Text>();
        private readonly List<Image> _stoneSlotBgs = new List<Image>();
        private readonly List<Image> _stoneSlotIcons = new List<Image>();
        private bool _built;

        // Picker modal — one runtime overlay, reused for unit slots and stone slots.
        private enum PickerMode { Unit, Stone }
        private bool _pickerBuilt;
        private GameObject _pickerPanel;
        private ScrollRect _pickerScrollRect;
        private RectTransform _pickerGrid;
        private GridLayoutGroup _pickerGridLayout;
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
                // ui-tweak 2026-07-08 — 편성 슬롯 포트레이트 확대(120→165). SlotsRow(scene)
                // 폭/높이도 함께 확대(1260x190).
                var btn = CreateButton("+", slotsContainer, new Vector2(165, 165), EmptySlotColor, out var label);
                var icon = CreateIconImage(btn.transform, new Vector2(130, 130), new Vector2(0, 18));
                btn.onClick.AddListener(() => OpenPicker(PickerMode.Unit, index));
                _unitSlotLabels.Add(label);
                _unitSlotBgs.Add(btn.GetComponent<Image>());
                _unitSlotIcons.Add(icon);
            }
        }

        private void BuildStoneSlots()
        {
            var container = EnsureStoneSlotsContainer();
            for (int i = 0; i < SquadSave.StoneSlotCount; i++)
            {
                int index = i;
                var btn = CreateButton("+", container, new Vector2(120, 120), EmptySlotColor, out var label);
                var icon = CreateIconImage(btn.transform, new Vector2(66, 66), new Vector2(0, 14));
                btn.onClick.AddListener(() => OpenPicker(PickerMode.Stone, index));
                _stoneSlotLabels.Add(label);
                _stoneSlotBgs.Add(btn.GetComponent<Image>());
                _stoneSlotIcons.Add(icon);
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
                    SetSlotLabelMode(_unitSlotLabels[i], false);
                    _unitSlotLabels[i].text = "+";
                    _unitSlotBgs[i].color = EmptySlotColor;
                    SetIcon(_unitSlotIcons[i], null);
                    continue;
                }
                var unit = catalog != null ? catalog.ById(id) : null;
                var portrait = unit != null ? unit.portrait : null;
                SetIcon(_unitSlotIcons[i], portrait);
                // 포트레이트가 있으면 이름을 하단 소형 라벨로, 없으면 기존 중앙 텍스트 폴백.
                SetSlotLabelMode(_unitSlotLabels[i], portrait != null);
                _unitSlotLabels[i].text = unit != null ? DisplayName(unit) : id;
                _unitSlotBgs[i].color = OwnedUnitColor;
            }

            for (int i = 0; i < _stoneSlotLabels.Count; i++)
            {
                string id = (squad != null && squad.stoneIds != null && i < squad.stoneIds.Count) ? squad.stoneIds[i] : "";
                if (string.IsNullOrEmpty(id))
                {
                    SetSlotLabelMode(_stoneSlotLabels[i], false);
                    _stoneSlotLabels[i].text = "+";
                    _stoneSlotBgs[i].color = EmptySlotColor;
                    SetIcon(_stoneSlotIcons[i], null);
                    continue;
                }
                var stone = stoneCatalog != null ? stoneCatalog.ById(id) : null;
                if (stone == null)
                {
                    // Catalog missing this id (asset deleted) — show the raw id, neutral
                    // color; still clearable via the picker's [해제] (squad-loadout policy).
                    SetSlotLabelMode(_stoneSlotLabels[i], false);
                    _stoneSlotLabels[i].text = id;
                    _stoneSlotBgs[i].color = EmptySlotColor;
                    SetIcon(_stoneSlotIcons[i], null);
                    continue;
                }
                SetSlotLabelMode(_stoneSlotLabels[i], true);
                _stoneSlotLabels[i].text = StoneSummary(stone);
                _stoneSlotBgs[i].color = GradeColor(stone.grade);
                SetIcon(_stoneSlotIcons[i], stone.icon);
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
                // ui-tweak 2026-07-08 — 유닛 포트레이트 아이템 50% 확대(150→225). 그리드는
                // 폭에 맞춰 열 수가 줄고 스크롤로 흐른다.
                _pickerGridLayout.cellSize = new Vector2(225, 225);
                _pickerGridLayout.spacing = new Vector2(16, 16);
                BuildUnitPickerItems();
            }
            else
            {
                _pickerTitle.text = "SELECT DREAMSTONE";
                // dreamstone-loadout Unit 7 — 64 flat items now render as icon cells
                // inside a ScrollRect instead of being squeezed into one text-only band.
                _pickerGridLayout.cellSize = new Vector2(150, 150);
                _pickerGridLayout.spacing = new Vector2(12, 12);
                BuildStonePickerItems();
            }

            if (_pickerScrollRect != null)
                _pickerScrollRect.verticalNormalizedPosition = 1f;
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

        // dreamstone-loadout Unit 2 (rev, 2026-07-04 육안 피드백) — dim already-slotted
        // items at build time. Items are recreated fresh on every OpenPicker, so the
        // dim reflects the squad state at open — no separate refresh path needed.
        private void BuildUnitPickerItems()
        {
            if (catalog == null || profileSO == null || profileSO.profile == null) return;
            var squad = Squad;
            // Units are not profile-owned — every catalog unit is always pickable.
            foreach (var id in catalog.AllIds())
            {
                var unit = catalog.ById(id);
                string label = unit != null ? DisplayName(unit) : id;
                string captured = id;
                bool alreadySlotted = squad != null && squad.unitIds.Contains(id);
                var bg = alreadySlotted ? DimColor(OwnedUnitColor, 0.4f) : OwnedUnitColor;
                var btn = CreateUnitPickerButton(unit, label, bg, alreadySlotted, out var lbl);
                if (alreadySlotted)
                {
                    // Unit picker: already-slotted units are NOT selectable — squad is a
                    // set of units. PickUnit's own dedup guard stays as a second line of
                    // defense (e.g. if this ever gets called from elsewhere).
                    lbl.color = new Color(lbl.color.r, lbl.color.g, lbl.color.b, 0.5f);
                    btn.interactable = false;
                }
                btn.onClick.AddListener(() => PickUnit(captured));
                _pickerItems.Add(btn.gameObject);
            }
        }

        // dreamstone-loadout Unit 5 (rev 2026-07-06b) — flat, individually-owned
        // items: 64 catalog stones, no grouping. An equipped id is dimmed and
        // unselectable (identical dim/interactable rule to BuildUnitPickerItems) —
        // "one item, one slot", no other restriction.
        private void BuildStonePickerItems()
        {
            if (stoneCatalog == null) return;
            var squad = Squad;
            var equipped = new HashSet<string>();
            if (squad != null && squad.stoneIds != null)
                foreach (var id in squad.stoneIds) if (!string.IsNullOrEmpty(id)) equipped.Add(id);

            foreach (var id in stoneCatalog.AllIds())
            {
                var stone = stoneCatalog.ById(id);
                if (stone == null) continue;
                string captured = id;
                bool alreadySlotted = equipped.Contains(id);
                var bg = alreadySlotted ? DimColor(GradeColor(stone.grade), 0.4f) : GradeColor(stone.grade);
                var btn = CreateStonePickerButton(stone, bg, alreadySlotted, out var lbl);
                if (alreadySlotted)
                {
                    lbl.color = new Color(lbl.color.r, lbl.color.g, lbl.color.b, 0.5f);
                    btn.interactable = false;
                }
                else
                {
                    btn.onClick.AddListener(() => PickStone(captured));
                }
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
            _pickerPanel.GetComponent<Image>().color = UiOverlay.Dim;

            var titleGo = new GameObject("Title", typeof(RectTransform), typeof(TextMeshProUGUI));
            titleGo.transform.SetParent(_pickerPanel.transform, false);
            var trt = titleGo.GetComponent<RectTransform>();
            trt.sizeDelta = new Vector2(800, 80); trt.anchoredPosition = new Vector2(0, 380);
            _pickerTitle = titleGo.GetComponent<TextMeshProUGUI>();
            _pickerTitle.alignment = TextAlignmentOptions.Center; _pickerTitle.fontSize = 36; _pickerTitle.color = Color.white;
            if (font != null) _pickerTitle.font = font;

            var scrollGo = new GameObject("Scroll", typeof(RectTransform), typeof(Image), typeof(ScrollRect));
            scrollGo.transform.SetParent(_pickerPanel.transform, false);
            var srt = scrollGo.GetComponent<RectTransform>();
            // RC2 (2026-07-04) — grid band pinned below the slot rows (unit row ~-180,
            // stone row ~-480) so a stray click at the same screen coords right after
            // the modal closes cannot land on a slot button underneath it.
            // ui-tweak 2026-07-08 — 그리드를 타이틀(y 380, 하단 340) 바로 아래(24px)로 올리고
            // 늘어난 세로 공간을 활용해 밴드를 키운다. top = -34+350 = 316 = 340-24.
            srt.sizeDelta = new Vector2(1400, 700); srt.anchoredPosition = new Vector2(0, -34);
            scrollGo.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0f);
            _pickerScrollRect = scrollGo.GetComponent<ScrollRect>();
            _pickerScrollRect.horizontal = false;
            _pickerScrollRect.vertical = true;
            _pickerScrollRect.movementType = ScrollRect.MovementType.Clamped;
            _pickerScrollRect.scrollSensitivity = 28f;

            var viewportGo = new GameObject("Viewport", typeof(RectTransform), typeof(Image), typeof(Mask));
            viewportGo.transform.SetParent(scrollGo.transform, false);
            var vrt = viewportGo.GetComponent<RectTransform>();
            vrt.anchorMin = Vector2.zero; vrt.anchorMax = Vector2.one; vrt.offsetMin = Vector2.zero; vrt.offsetMax = Vector2.zero;
            viewportGo.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.02f);
            viewportGo.GetComponent<Mask>().showMaskGraphic = false;

            var gridGo = new GameObject("Grid", typeof(RectTransform), typeof(GridLayoutGroup), typeof(ContentSizeFitter));
            gridGo.transform.SetParent(viewportGo.transform, false);
            _pickerGrid = gridGo.GetComponent<RectTransform>();
            _pickerGrid.anchorMin = new Vector2(0f, 1f);
            _pickerGrid.anchorMax = new Vector2(1f, 1f);
            _pickerGrid.pivot = new Vector2(0.5f, 1f);
            _pickerGrid.anchoredPosition = Vector2.zero;
            _pickerGrid.sizeDelta = new Vector2(0, 440);
            _pickerGridLayout = gridGo.GetComponent<GridLayoutGroup>();
            _pickerGridLayout.cellSize = new Vector2(150, 70);
            _pickerGridLayout.spacing = new Vector2(12, 12);
            _pickerGridLayout.childAlignment = TextAnchor.UpperCenter;
            var fitter = gridGo.GetComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            _pickerScrollRect.viewport = vrt;
            _pickerScrollRect.content = _pickerGrid;

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
                        // dreamstone-loadout Unit 6 — MoveSpeed stones are retired
                        // (kept only for enum serialization safety); CostRate is the
                        // stone kind that now lives where MOVE used to.
                        : stone.effect.kind == CardBuffKind.CostRate ? "COST"
                        : "MOVE";
            string sign = stone.effect.percent >= 0 ? "+" : "";
            return $"{abbr} {sign}{stone.effect.percent:0.#}%";
        }

        // dreamstone-loadout Unit 2 (rev, 2026-07-04) — dim an already-slotted picker
        // item's background by a flat RGB factor (alpha untouched).
        private static Color DimColor(Color c, float factor) => new Color(c.r * factor, c.g * factor, c.b * factor, c.a);

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

        // defender-portraits 2 — 유닛 피커 셀. CreateStonePickerButton 과 동형: 포트레이트
        // 아이콘 + 하단 이름 라벨. portrait 미할당 유닛은 아이콘 숨김(이름만).
        private Button CreateUnitPickerButton(DefenderUnitData unit, string label, Color bg, bool dimmed, out TMP_Text lbl)
        {
            var go = new GameObject("UnitItem", typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(_pickerGrid, false);
            go.GetComponent<RectTransform>().sizeDelta = _pickerGridLayout.cellSize;
            go.GetComponent<Image>().color = bg;

            var icon = CreateIconImage(go.transform, new Vector2(150, 150), new Vector2(0, 34));
            SetIcon(icon, unit != null ? unit.portrait : null);

            var l = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
            l.transform.SetParent(go.transform, false);
            var lrt = l.GetComponent<RectTransform>();
            lrt.anchorMin = new Vector2(0, 0);
            lrt.anchorMax = new Vector2(1, 0);
            lrt.pivot = new Vector2(0.5f, 0);
            lrt.sizeDelta = new Vector2(0, 48);
            lrt.anchoredPosition = new Vector2(0, 10);
            lbl = l.GetComponent<TextMeshProUGUI>();
            lbl.text = label;
            lbl.alignment = TextAlignmentOptions.Center;
            lbl.fontSize = 22;
            lbl.color = Color.white;
            if (font != null) lbl.font = font;

            if (dimmed)
            {
                var group = go.AddComponent<CanvasGroup>();
                group.alpha = 0.55f;
            }

            return go.GetComponent<Button>();
        }

        private Button CreateStonePickerButton(DreamstoneData stone, Color bg, bool dimmed, out TMP_Text label)
        {
            var go = new GameObject("StoneItem", typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(_pickerGrid, false);
            go.GetComponent<RectTransform>().sizeDelta = _pickerGridLayout.cellSize;
            go.GetComponent<Image>().color = bg;

            var icon = CreateIconImage(go.transform, new Vector2(78, 78), new Vector2(0, 26));
            SetIcon(icon, stone != null ? stone.icon : null);

            var l = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
            l.transform.SetParent(go.transform, false);
            var lrt = l.GetComponent<RectTransform>();
            lrt.anchorMin = new Vector2(0, 0);
            lrt.anchorMax = new Vector2(1, 0);
            lrt.pivot = new Vector2(0.5f, 0);
            lrt.sizeDelta = new Vector2(0, 44);
            lrt.anchoredPosition = new Vector2(0, 10);
            label = l.GetComponent<TextMeshProUGUI>();
            label.text = stone != null ? StoneSummary(stone) : "";
            label.alignment = TextAlignmentOptions.Center;
            label.fontSize = 17;
            label.color = Color.white;
            if (font != null) label.font = font;

            if (dimmed)
            {
                var group = go.AddComponent<CanvasGroup>();
                group.alpha = 0.55f;
            }

            return go.GetComponent<Button>();
        }

        private Image CreateIconImage(Transform parent, Vector2 size, Vector2 anchoredPosition)
        {
            var go = new GameObject("Icon", typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.sizeDelta = size;
            rt.anchoredPosition = anchoredPosition;
            var img = go.GetComponent<Image>();
            img.preserveAspect = true;
            img.raycastTarget = false;
            img.enabled = false;
            return img;
        }

        private static void SetIcon(Image image, Sprite sprite)
        {
            if (image == null) return;
            image.sprite = sprite;
            image.enabled = sprite != null;
        }

        private static void SetSlotLabelMode(TMP_Text label, bool occupied)
        {
            if (label == null) return;
            var rt = label.GetComponent<RectTransform>();
            if (occupied)
            {
                rt.anchorMin = new Vector2(0, 0);
                rt.anchorMax = new Vector2(1, 0);
                rt.pivot = new Vector2(0.5f, 0);
                rt.sizeDelta = new Vector2(0, 30);
                rt.anchoredPosition = new Vector2(0, 8);
                label.fontSize = 14;
            }
            else
            {
                rt.anchorMin = Vector2.zero;
                rt.anchorMax = Vector2.one;
                rt.pivot = new Vector2(0.5f, 0.5f);
                rt.offsetMin = Vector2.zero;
                rt.offsetMax = Vector2.zero;
                label.fontSize = 20;
            }
        }
    }
}
