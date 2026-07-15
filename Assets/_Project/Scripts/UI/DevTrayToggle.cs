using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Wassup.UI
{
    // outgame-login-gate unit 5 — the dev button tray grew to five buttons and ate
    // the lobby's top-right corner. Keep one always-visible toggle and fold the
    // rest under it; collapsed is the default. Sits on the DevButtons container
    // next to DevOnlyGroup (build gate) and the CanvasGroup OutgameMenuController
    // fades while a panel is up — this component owns neither, only content.
    public class DevTrayToggle : MonoBehaviour
    {
        // lobby fonts carry no Korean glyphs and no arrow glyphs — ASCII only,
        // same rule as StatRefreshButtonView's labels.
        private const string CollapsedLabel = "DEV +";
        private const string ExpandedLabel = "DEV -";

        [SerializeField] private Button toggleButton;
        [SerializeField] private GameObject content;
        [SerializeField] private TMP_Text label;

        public bool IsExpanded { get; private set; }

        private void Awake()
        {
            // content is also serialized inactive, so the tray is collapsed even if
            // this Awake never runs (e.g. DevOnlyGroup disables the container first).
            SetExpanded(false);
            if (toggleButton != null) toggleButton.onClick.AddListener(Toggle);
        }

        private void OnDestroy()
        {
            if (toggleButton != null) toggleButton.onClick.RemoveListener(Toggle);
        }

        public void Toggle() => SetExpanded(!IsExpanded);

        internal void SetExpanded(bool expanded)
        {
            IsExpanded = expanded;
            if (content != null) content.SetActive(expanded);
            if (label != null) label.text = expanded ? ExpandedLabel : CollapsedLabel;
        }
    }
}
