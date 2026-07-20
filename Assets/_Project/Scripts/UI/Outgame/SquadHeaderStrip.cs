using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Wassup.Core;
using Wassup.Data;

namespace Wassup.UI
{
    // squad-character-page Unit 3 — the always-visible "편성 현황" header above the
    // roster: 7 unit slots + 4 stone slots. Slots repaint from the squad save.
    // Tapping a filled unit slot selects it for the detail panel (unit 9 — removal
    // is the detail panel's [편성 해제] button); tapping a stone slot enters stone
    // mode. Pure view — the orchestrator (unit 4) owns the squad state and reacts
    // to the tap events.
    public class SquadHeaderStrip : MonoBehaviour
    {
        [SerializeField] private DefenderCatalog catalog;
        [SerializeField] private DreamstoneCatalog stoneCatalog;
        [SerializeField] private TMP_FontAsset font;
        [SerializeField] private Vector2 unitSlotSize = new Vector2(92, 92);
        [SerializeField] private Vector2 stoneSlotSize = new Vector2(72, 72);

        public event Action<int> UnitSlotTapped;
        public event Action<int> StoneSlotTapped;

        private static readonly Color EmptySlot = new Color(0.16f, 0.18f, 0.24f, 1f);
        private static readonly Color FilledUnit = new Color(0.18f, 0.32f, 0.62f, 1f);
        private static readonly Color ActiveOutline = new Color(1f, 0.9f, 0.45f, 1f);

        private class SlotW
        {
            public string id; // unit/stone id currently painted into the slot
            public Image bg;
            public Image icon;
            public TMP_Text label;
            public GameObject outline;
        }

        private bool _built;
        private string _selectedUnitId;
        private readonly List<SlotW> _unitSlots = new List<SlotW>();
        private readonly List<SlotW> _stoneSlots = new List<SlotW>();

        public void Refresh(SquadSave squad)
        {
            EnsureBuilt();
            for (int i = 0; i < _unitSlots.Count; i++)
            {
                string id = (squad != null && squad.unitIds != null && i < squad.unitIds.Count) ? squad.unitIds[i] : "";
                var unit = (!string.IsNullOrEmpty(id) && catalog != null) ? catalog.ById(id) : null;
                _unitSlots[i].id = id;
                PaintSlot(_unitSlots[i], unit != null ? unit.portrait : null, unit != null ? FilledUnit : EmptySlot, string.IsNullOrEmpty(id));
            }
            for (int i = 0; i < _stoneSlots.Count; i++)
            {
                string id = (squad != null && squad.stoneIds != null && i < squad.stoneIds.Count) ? squad.stoneIds[i] : "";
                var stone = (!string.IsNullOrEmpty(id) && stoneCatalog != null) ? stoneCatalog.ById(id) : null;
                _stoneSlots[i].id = id;
                PaintSlot(_stoneSlots[i], stone != null ? stone.icon : null, stone != null ? DreamstoneStyle.Frame(stone.grade) : EmptySlot, string.IsNullOrEmpty(id));
            }
            ApplyUnitSelection();
        }

        // -1 = none. Highlights the stone slot currently being edited in stone mode.
        public void SetActiveStoneSlot(int index)
        {
            for (int i = 0; i < _stoneSlots.Count; i++)
                if (_stoneSlots[i].outline != null) _stoneSlots[i].outline.SetActive(i == index);
        }

        // Unit 10 — outlines the unit slot holding the unit shown in the detail
        // panel. null/unequipped id clears (unit-mode only, driven by the orchestrator).
        public void SetSelectedUnit(string unitId)
        {
            _selectedUnitId = unitId;
            if (_built) ApplyUnitSelection();
        }

        private void ApplyUnitSelection()
        {
            for (int i = 0; i < _unitSlots.Count; i++)
                if (_unitSlots[i].outline != null)
                    _unitSlots[i].outline.SetActive(
                        !string.IsNullOrEmpty(_selectedUnitId) && _unitSlots[i].id == _selectedUnitId);
        }

        private static void PaintSlot(SlotW slot, Sprite sprite, Color bg, bool empty)
        {
            slot.bg.color = bg;
            slot.icon.sprite = sprite;
            slot.icon.enabled = sprite != null;
            slot.label.enabled = empty;
        }

        private void EnsureBuilt()
        {
            if (_built) return;
            _built = true;

            var hlg = gameObject.GetComponent<HorizontalLayoutGroup>();
            if (hlg == null) hlg = gameObject.AddComponent<HorizontalLayoutGroup>();
            hlg.spacing = 8;
            hlg.padding = new RectOffset(10, 10, 6, 6);
            hlg.childAlignment = TextAnchor.MiddleLeft;
            hlg.childControlWidth = false;
            hlg.childControlHeight = false;
            hlg.childForceExpandWidth = false;
            hlg.childForceExpandHeight = false;

            for (int i = 0; i < SquadSave.SlotCount; i++)
            {
                int index = i;
                _unitSlots.Add(MakeSlot(unitSlotSize, () => UnitSlotTapped?.Invoke(index)));
            }
            MakeSpacer(24);
            for (int i = 0; i < SquadSave.StoneSlotCount; i++)
            {
                int index = i;
                _stoneSlots.Add(MakeSlot(stoneSlotSize, () => StoneSlotTapped?.Invoke(index)));
            }

            UiLayer.Apply(gameObject);
        }

        private void MakeSpacer(float width)
        {
            var go = new GameObject("Spacer", typeof(RectTransform));
            go.transform.SetParent(transform, false);
            ((RectTransform)go.transform).sizeDelta = new Vector2(width, 1);
        }

        private SlotW MakeSlot(Vector2 size, Action onTap)
        {
            var root = new GameObject("Slot", typeof(RectTransform), typeof(Image), typeof(Button));
            root.transform.SetParent(transform, false);
            ((RectTransform)root.transform).sizeDelta = size;
            var bg = root.GetComponent<Image>();
            bg.color = EmptySlot;
            root.GetComponent<Button>().onClick.AddListener(() => onTap());

            var outline = new GameObject("Outline", typeof(RectTransform), typeof(Image));
            outline.transform.SetParent(root.transform, false);
            var ort = (RectTransform)outline.transform;
            ort.anchorMin = Vector2.zero; ort.anchorMax = Vector2.one;
            ort.offsetMin = new Vector2(-4, -4); ort.offsetMax = new Vector2(4, 4);
            var oimg = outline.GetComponent<Image>();
            oimg.color = ActiveOutline; oimg.raycastTarget = false;
            outline.SetActive(false);

            var iconGo = new GameObject("Icon", typeof(RectTransform), typeof(Image));
            iconGo.transform.SetParent(root.transform, false);
            var irt = (RectTransform)iconGo.transform;
            irt.anchorMin = Vector2.zero; irt.anchorMax = Vector2.one;
            irt.offsetMin = new Vector2(6, 6); irt.offsetMax = new Vector2(-6, -6);
            var icon = iconGo.GetComponent<Image>();
            icon.preserveAspect = true; icon.raycastTarget = false; icon.enabled = false;

            var labelGo = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
            labelGo.transform.SetParent(root.transform, false);
            var lrt = (RectTransform)labelGo.transform;
            lrt.anchorMin = Vector2.zero; lrt.anchorMax = Vector2.one; lrt.offsetMin = Vector2.zero; lrt.offsetMax = Vector2.zero;
            var label = labelGo.GetComponent<TextMeshProUGUI>();
            label.text = "+"; label.alignment = TextAlignmentOptions.Center; label.fontSize = 30; label.color = new Color(1, 1, 1, 0.6f);
            label.raycastTarget = false;
            if (font != null) label.font = font;

            return new SlotW { bg = bg, icon = icon, label = label, outline = outline };
        }
    }
}
