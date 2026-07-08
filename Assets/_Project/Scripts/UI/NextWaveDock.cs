using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Wassup.Bridge;
using Wassup.Core;

namespace Wassup.UI
{
    // battle-hud-score-timer-menu Unit 1 — bottom-right combo dock. Top row shows the
    // match countdown (moved off the top-center, which the score now owns); bottom row
    // is the "NEXT WAVE {n}" / "NO WAVES" control that early-summons the next wave.
    //
    // The NextWave button used to be built inside BattleBridge (the ECS gateway). It now
    // lives here in the View layer; BattleBridge only exposes read-only wave state
    // (NextWaveAvailable / NextWaveHasNext / NextWaveNumber) which this dock polls.
    public class NextWaveDock : MonoBehaviour
    {
        [SerializeField] private BattleBridge bridge;
        [SerializeField] private DraftController draftController;

        [Header("Timer")]
        [SerializeField] private float timerFontSize = 40f;
        [SerializeField] private Color timerColor = Color.white;
        [SerializeField] private Color timerWarnColor = Color.red;
        [Tooltip("이 초 미만이면 타이머를 경고색으로")]
        [SerializeField] private float warnSeconds = 30f;

        [Header("Next wave button")]
        [SerializeField] private Color buttonColor = new Color(0.12f, 0.42f, 0.82f, 0.95f);
        [SerializeField] private float buttonFontSize = 28f;

        [Header("Backing")]
        [Tooltip("타이머+버튼 뒤 dimmed 배경 판")]
        [SerializeField] private Color backingColor = new Color(0f, 0f, 0f, 0.45f);

        private GameObject _panel;
        private TextMeshProUGUI _timerLabel;
        private GameObject _buttonRoot;
        private Button _waveButton;
        private TextMeshProUGUI _waveLabel;
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
            // Squad mode has no draft — placement entry comes via PlacementRequested.
            // (GameManager sets Instance in Awake, before this OnEnable.)
            if (GameManager.Instance != null)
                GameManager.Instance.PlacementRequested += OnBattleStart;
        }

        private void OnDisable()
        {
            if (draftController != null)
            {
                draftController.DraftConfirmed -= OnBattleStart;
                draftController.DraftStarted -= OnDraftStarted;
            }
            if (GameManager.Instance != null)
                GameManager.Instance.PlacementRequested -= OnBattleStart;
        }

        private void OnBattleStart() { if (_panel != null) _panel.SetActive(true); }
        private void OnDraftStarted() { if (_panel != null) _panel.SetActive(false); }

        private void Update()
        {
            if (bridge == null || _panel == null || !_panel.activeSelf) return;

            // Timer row — always visible while the dock is shown.
            if (_timerLabel != null)
            {
                float remaining = bridge.TimerRemaining;
                if (remaining < 0f) remaining = 0f;
                int min = (int)(remaining / 60f);
                int sec = (int)(remaining % 60f);
                _timerLabel.text = $"{min}:{sec:D2}";
                _timerLabel.color = remaining < warnSeconds ? timerWarnColor : timerColor;
            }

            // Next-wave row — visible only for generated-wave battles; label/interactable
            // track the remaining waves.
            bool available = bridge.NextWaveAvailable;
            if (_buttonRoot != null && _buttonRoot.activeSelf != available)
                _buttonRoot.SetActive(available);
            if (available)
            {
                bool hasNext = bridge.NextWaveHasNext;
                if (_waveButton != null) _waveButton.interactable = hasNext;
                if (_waveLabel != null)
                    _waveLabel.text = hasNext ? $"NEXT WAVE {bridge.NextWaveNumber}" : "NO WAVES";
            }
        }

        private void OnWaveButtonClicked()
        {
            if (bridge != null) bridge.ForceNextWave();
        }

        private void BuildCanvas()
        {
            if (_built) return;
            _built = true;

            var canvas = gameObject.GetComponent<Canvas>();
            if (canvas == null) canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 7;
            if (gameObject.GetComponent<CanvasScaler>() == null)
            {
                var scaler = gameObject.AddComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920, 1080);
            }
            if (gameObject.GetComponent<GraphicRaycaster>() == null)
                gameObject.AddComponent<GraphicRaycaster>();

            // Bottom-right container (inherits the old NextWave button anchor).
            _panel = new GameObject("DockPanel", typeof(RectTransform));
            _panel.transform.SetParent(transform, false);
            var prt = (RectTransform)_panel.transform;
            prt.anchorMin = new Vector2(1f, 0f);
            prt.anchorMax = new Vector2(1f, 0f);
            prt.pivot = new Vector2(1f, 0f);
            prt.anchoredPosition = new Vector2(-40f, 40f);
            prt.sizeDelta = new Vector2(250f, 150f);

            // Dimmed backing plate behind the timer + button rows (first child → behind).
            var backing = new GameObject("Backing", typeof(RectTransform), typeof(Image));
            backing.transform.SetParent(_panel.transform, false);
            var bkrt = (RectTransform)backing.transform;
            bkrt.anchorMin = Vector2.zero;
            bkrt.anchorMax = Vector2.one;
            bkrt.offsetMin = new Vector2(-10f, -10f);
            bkrt.offsetMax = new Vector2(10f, 10f);
            var bkimg = backing.GetComponent<Image>();
            bkimg.color = backingColor;
            bkimg.raycastTarget = false;

            // Timer row (top).
            var timerGO = new GameObject("Timer", typeof(RectTransform));
            timerGO.transform.SetParent(_panel.transform, false);
            var trt = (RectTransform)timerGO.transform;
            trt.anchorMin = new Vector2(0f, 1f);
            trt.anchorMax = new Vector2(1f, 1f);
            trt.pivot = new Vector2(0.5f, 1f);
            trt.offsetMin = new Vector2(0f, -66f);
            trt.offsetMax = new Vector2(0f, 0f);
            _timerLabel = timerGO.AddComponent<TextMeshProUGUI>();
            _timerLabel.text = "3:00";
            _timerLabel.fontSize = timerFontSize;
            _timerLabel.color = timerColor;
            _timerLabel.alignment = TextAlignmentOptions.Center;
            _timerLabel.raycastTarget = false;

            // Next-wave button row (bottom).
            _buttonRoot = new GameObject("NextWaveButton", typeof(RectTransform), typeof(Image), typeof(Button));
            _buttonRoot.transform.SetParent(_panel.transform, false);
            var brt = (RectTransform)_buttonRoot.transform;
            brt.anchorMin = new Vector2(0f, 0f);
            brt.anchorMax = new Vector2(1f, 0f);
            brt.pivot = new Vector2(0.5f, 0f);
            brt.offsetMin = new Vector2(0f, 0f);
            brt.offsetMax = new Vector2(0f, 72f);
            _buttonRoot.GetComponent<Image>().color = buttonColor;
            _waveButton = _buttonRoot.GetComponent<Button>();
            _waveButton.onClick.AddListener(OnWaveButtonClicked);

            var labelGO = new GameObject("Label", typeof(RectTransform));
            labelGO.transform.SetParent(_buttonRoot.transform, false);
            var lrt = (RectTransform)labelGO.transform;
            lrt.anchorMin = Vector2.zero;
            lrt.anchorMax = Vector2.one;
            lrt.offsetMin = Vector2.zero;
            lrt.offsetMax = Vector2.zero;
            _waveLabel = labelGO.AddComponent<TextMeshProUGUI>();
            _waveLabel.text = "NEXT WAVE";
            _waveLabel.fontSize = buttonFontSize;
            _waveLabel.color = Color.white;
            _waveLabel.alignment = TextAlignmentOptions.Center;
            _waveLabel.raycastTarget = false;

            _buttonRoot.SetActive(false);

            UiLayer.Apply(gameObject);
        }
    }
}
