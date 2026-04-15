using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Wassup.Data;

namespace Wassup.UI
{
    // Runtime-built card in the draft grid. Holds a reference to its DefenderUnitData
    // so the parent view can route click events back to DraftController without an
    // extra lookup.
    public class DraftCardView : MonoBehaviour
    {
        private static readonly Color PickedTint = new(0.25f, 0.75f, 0.3f, 1f);
        private static readonly Color UnpickedTint = new(0.18f, 0.18f, 0.22f, 1f);

        public Button Button { get; private set; }
        public TextMeshProUGUI Label { get; private set; }
        public Image Background { get; private set; }
        public Image Swatch { get; private set; }
        public DefenderUnitData Unit { get; private set; }

        public void Bind(Button button, TextMeshProUGUI label, Image background, Image swatch, DefenderUnitData unit)
        {
            Button = button;
            Label = label;
            Background = background;
            Swatch = swatch;
            Unit = unit;
        }

        public void SetPicked(bool picked)
        {
            if (Background != null) Background.color = picked ? PickedTint : UnpickedTint;
        }
    }
}
