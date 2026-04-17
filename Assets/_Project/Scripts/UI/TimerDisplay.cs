using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Wassup.Bridge;
using Wassup.Core;

namespace Wassup.UI
{
    // Runtime-built countdown timer at screen top-center. Shows remaining
    // seconds while a battle is active, hidden during draft / result.
    public class TimerDisplay : MonoBehaviour
    {
        [SerializeField] private BattleBridge bridge;
        [SerializeField] private DraftController draftController;

        private GameObject _panel;
        private TextMeshProUGUI _label;
        private bool _built;

        private void Awake()
        {
            BuildCanvas();
            if (_panel != null) _panel.SetActive(false);
        }

        private void OnEnable()
        {
            if (draftController != null)
            {
                draftController.DraftConfirmed += OnBattleStart;
                draftController.DraftStarted += OnDraftStarted;
            }
        }

        private void OnDisable()
        {
            if (draftController != null)
            {
                draftController.DraftConfirmed -= OnBattleStart;
                draftController.DraftStarted -= OnDraftStarted;
            }
        }

        private void OnBattleStart() { if (_panel != null) _panel.SetActive(true); }
        private void OnDraftStarted() { if (_panel != null) _panel.SetActive(false); }

        private void Update()
        {
            if (bridge == null || _label == null || _panel == null || !_panel.activeSelf) return;
            float remaining = bridge.TimerRemaining;
            if (remaining < 0f) remaining = 0f;
            int min = (int)(remaining / 60f);
            int sec = (int)(remaining % 60f);
            _label.text = $"{min}:{sec:D2}";
            _label.color = remaining < 30f ? Color.red : Color.white;
        }

        private void BuildCanvas()
        {
            if (_built) return;
            _built = true;

            var canvas = gameObject.GetComponent<Canvas>();
            if (canvas == null) canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 6;
            if (gameObject.GetComponent<CanvasScaler>() == null)
            {
                var scaler = gameObject.AddComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920, 1080);
            }
            if (gameObject.GetComponent<GraphicRaycaster>() == null)
                gameObject.AddComponent<GraphicRaycaster>();

            _panel = new GameObject("TimerPanel", typeof(RectTransform));
            _panel.transform.SetParent(transform, false);
            var prt = (RectTransform)_panel.transform;
            prt.anchorMin = new Vector2(0.5f, 1f);
            prt.anchorMax = new Vector2(0.5f, 1f);
            prt.pivot = new Vector2(0.5f, 1f);
            prt.anchoredPosition = new Vector2(0f, -20f);
            prt.sizeDelta = new Vector2(200f, 60f);

            var labelGO = new GameObject("Label", typeof(RectTransform));
            labelGO.transform.SetParent(_panel.transform, false);
            var lrt = (RectTransform)labelGO.transform;
            lrt.anchorMin = Vector2.zero;
            lrt.anchorMax = Vector2.one;
            lrt.offsetMin = Vector2.zero;
            lrt.offsetMax = Vector2.zero;
            _label = labelGO.AddComponent<TextMeshProUGUI>();
            _label.text = "3:00";
            _label.fontSize = 48;
            _label.color = Color.white;
            _label.alignment = TextAlignmentOptions.Center;
        }
    }
}
