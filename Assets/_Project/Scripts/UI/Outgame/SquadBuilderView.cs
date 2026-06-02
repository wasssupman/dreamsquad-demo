using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Wassup.Core;
using Wassup.Data;

namespace Wassup.UI
{
    // squad-loadout Unit 2 — squad编성 screen inside SquadPanel. Builds 7 slot
    // buttons + an owned-unit grid at runtime from the catalog/profile, lets the
    // player assign/clear slots, and saves to disk. MVP: no class/trait/condition.
    public class SquadBuilderView : MonoBehaviour
    {
        [SerializeField] private DefenderCatalog catalog;
        [SerializeField] private PlayerProfileSO profileSO;
        [SerializeField] private RectTransform slotsContainer;
        [SerializeField] private RectTransform ownedContainer;
        [SerializeField] private TMP_Text statusText;
        [SerializeField] private TMP_FontAsset font;

        private readonly List<TMP_Text> _slotLabels = new List<TMP_Text>();
        private bool _built;

        private SquadSave Squad =>
            (profileSO != null && profileSO.profile != null) ? profileSO.profile.SelectedSquad() : null;

        private void OnEnable()
        {
            EnsureBuilt();
            Refresh();
        }

        private void EnsureBuilt()
        {
            if (_built) return;
            _built = true;
            BuildSlots();
            BuildOwned();
        }

        private void BuildSlots()
        {
            for (int i = 0; i < SquadSave.SlotCount; i++)
            {
                int index = i;
                var btn = CreateButton("＋", slotsContainer, new Vector2(120, 120), new Color(0.16f, 0.18f, 0.24f, 1f), out var label);
                btn.onClick.AddListener(() => ClearSlot(index));
                _slotLabels.Add(label);
            }
        }

        private void BuildOwned()
        {
            if (catalog == null || profileSO == null || profileSO.profile == null) return;
            foreach (var id in profileSO.profile.ownedUnitIds)
            {
                var unit = catalog.ById(id);
                string label = unit != null ? DisplayName(unit) : id;
                string captured = id;
                var btn = CreateButton(label, ownedContainer, new Vector2(150, 70), new Color(0.18f, 0.32f, 0.62f, 1f), out _);
                btn.onClick.AddListener(() => AssignUnit(captured));
            }
        }

        public void Refresh()
        {
            var squad = Squad;
            for (int i = 0; i < _slotLabels.Count; i++)
            {
                string id = (squad != null && i < squad.unitIds.Count) ? squad.unitIds[i] : "";
                if (string.IsNullOrEmpty(id)) { _slotLabels[i].text = "＋"; continue; }
                var unit = catalog != null ? catalog.ById(id) : null;
                _slotLabels[i].text = unit != null ? DisplayName(unit) : id;
            }
            if (statusText != null)
                statusText.text = squad != null ? $"{squad.name}: {squad.FilledCount()}/{SquadSave.SlotCount}" : "No squad";
        }

        private void AssignUnit(string id)
        {
            var squad = Squad;
            if (squad == null || string.IsNullOrEmpty(id)) return;
            // ignore if already slotted (squad is a set of units)
            for (int i = 0; i < squad.unitIds.Count; i++)
                if (squad.unitIds[i] == id) return;
            for (int i = 0; i < squad.unitIds.Count; i++)
            {
                if (string.IsNullOrEmpty(squad.unitIds[i])) { squad.unitIds[i] = id; break; }
            }
            Refresh();
        }

        private void ClearSlot(int index)
        {
            var squad = Squad;
            if (squad == null || index < 0 || index >= squad.unitIds.Count) return;
            squad.unitIds[index] = "";
            Refresh();
        }

        public void OnSave()
        {
            if (profileSO == null || profileSO.profile == null) return;
            ProfileStore.Save(profileSO.profile);
            if (statusText != null) statusText.text = "Saved";
        }

        private static string DisplayName(DefenderUnitData u) =>
            string.IsNullOrEmpty(u.displayName) ? u.name : u.displayName;

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
