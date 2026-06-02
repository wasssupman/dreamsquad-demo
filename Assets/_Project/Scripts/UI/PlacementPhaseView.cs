using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Wassup.Bridge;
using Wassup.Core;

namespace Wassup.UI
{
    // Phase 6 placement countdown overlay. Sits between Draft confirm and
    // battle start: grants the starting cost, shows a countdown, and lets the
    // player either wait for the timer to elapse or tap START BATTLE to begin.
    public class PlacementPhaseView : MonoBehaviour
    {
        [SerializeField] private BattleBridge bridge;
        [SerializeField] private DraftController draftController;
        [SerializeField] private GameManager gameManager;

        private GameObject _panel;
        private TextMeshProUGUI _countdownLabel;
        private Button _startButton;
        private float _remaining;
        private bool _active;
        private bool _built;

        private void Awake()
        {
            BuildCanvas();
            if (_panel != null) _panel.SetActive(false);
        }

        private void OnEnable()
        {
            if (draftController != null) draftController.DraftConfirmed += OnDraftConfirmed;
            // squad-loadout Unit 3 — squad mode has no draft; GameManager signals
            // placement directly.
            if (gameManager != null) gameManager.PlacementRequested += BeginPlacementPhase;
        }

        private void OnDisable()
        {
            if (draftController != null) draftController.DraftConfirmed -= OnDraftConfirmed;
            if (gameManager != null) gameManager.PlacementRequested -= BeginPlacementPhase;
        }

        private void OnDraftConfirmed()
        {
            BeginPlacementPhase();
        }

        public void BeginPlacementPhase()
        {
            if (!_built) BuildCanvas();
            var cfg = gameManager != null ? gameManager.CostConfig : null;
            float duration = cfg != null ? cfg.placementPhaseDuration : 30f;

            if (gameManager != null) gameManager.SetPhase(GamePhase.Placement);
            if (gameManager != null && gameManager.CostRuntime != null) gameManager.CostRuntime.ResetToStart();
            if (bridge != null) bridge.BeginPlacement();

            _remaining = duration;
            _active = true;
            _panel.SetActive(true);
        }

        private void Update()
        {
            if (!_active) return;
            _remaining -= Time.deltaTime;
            if (_remaining <= 0f) { _remaining = 0f; FinishPlacement(); return; }
            _countdownLabel.text = $"배치 페이즈  ·  {Mathf.CeilToInt(_remaining)}s";
        }

        private void OnStartClicked() => FinishPlacement();

        private void FinishPlacement()
        {
            _active = false;
            _panel.SetActive(false);
            if (gameManager != null) gameManager.SetPhase(GamePhase.Battle);
            if (gameManager != null && gameManager.CostRuntime != null) gameManager.CostRuntime.BeginRegen();
            if (bridge != null) bridge.StartBattle();
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

            _panel = new GameObject("PlacementPanel", typeof(RectTransform));
            _panel.transform.SetParent(transform, false);
            var prt = (RectTransform)_panel.transform;
            prt.anchorMin = Vector2.zero;
            prt.anchorMax = Vector2.one;
            prt.offsetMin = Vector2.zero;
            prt.offsetMax = Vector2.zero;

            // Top-center countdown banner
            var banner = new GameObject("Banner", typeof(RectTransform), typeof(Image));
            banner.transform.SetParent(_panel.transform, false);
            var brt = (RectTransform)banner.transform;
            brt.anchorMin = new Vector2(0.5f, 1f);
            brt.anchorMax = new Vector2(0.5f, 1f);
            brt.pivot = new Vector2(0.5f, 1f);
            brt.anchoredPosition = new Vector2(0f, -90f);
            brt.sizeDelta = new Vector2(560f, 72f);
            banner.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.7f);

            var labelGO = new GameObject("Label", typeof(RectTransform));
            labelGO.transform.SetParent(banner.transform, false);
            var lrt = (RectTransform)labelGO.transform;
            lrt.anchorMin = Vector2.zero; lrt.anchorMax = Vector2.one;
            lrt.offsetMin = Vector2.zero; lrt.offsetMax = Vector2.zero;
            _countdownLabel = labelGO.AddComponent<TextMeshProUGUI>();
            _countdownLabel.text = "배치 페이즈";
            _countdownLabel.fontSize = 36;
            _countdownLabel.color = Color.yellow;
            _countdownLabel.alignment = TextAlignmentOptions.Center;

            // Bottom-center START BATTLE button
            var btnGO = new GameObject("StartButton", typeof(RectTransform), typeof(Image), typeof(Button));
            btnGO.transform.SetParent(_panel.transform, false);
            var btnRt = (RectTransform)btnGO.transform;
            btnRt.anchorMin = new Vector2(0.5f, 0f);
            btnRt.anchorMax = new Vector2(0.5f, 0f);
            btnRt.pivot = new Vector2(0.5f, 0f);
            btnRt.anchoredPosition = new Vector2(0f, 180f);
            btnRt.sizeDelta = new Vector2(320f, 80f);
            btnGO.GetComponent<Image>().color = new Color(0.2f, 0.7f, 0.3f, 1f);
            _startButton = btnGO.GetComponent<Button>();
            _startButton.onClick.AddListener(OnStartClicked);

            var btnLabelGO = new GameObject("Label", typeof(RectTransform));
            btnLabelGO.transform.SetParent(btnGO.transform, false);
            var blrt = (RectTransform)btnLabelGO.transform;
            blrt.anchorMin = Vector2.zero; blrt.anchorMax = Vector2.one;
            blrt.offsetMin = Vector2.zero; blrt.offsetMax = Vector2.zero;
            var bl = btnLabelGO.AddComponent<TextMeshProUGUI>();
            bl.text = "START BATTLE";
            bl.fontSize = 32;
            bl.color = Color.white;
            bl.alignment = TextAlignmentOptions.Center;
        }
    }
}
