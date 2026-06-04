using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Wassup.Core;
using Wassup.UI.Draft;

namespace Wassup.UI
{
    // squad map-setup — pre-placement step for squad mode. Shows the existing
    // MapSettingsPanel so the player can freely adjust the map (same panel the
    // draft stage used), plus a START button that advances to placement. Mirrors
    // how DraftView hosted the panel, minus the card draft.
    public class SquadPrepView : MonoBehaviour
    {
        [SerializeField] private GameManager gameManager;
        [SerializeField] private DraftController draftController;
        [SerializeField] private MapSettingsPanelView mapSettings;
        // squad map-setup — attack-pattern preview (the draft stage's wave strip).
        // Self-contained: previews WavePatternGenerator.Generate(deck) from its own
        // AttackDeck, wired to the same deck BattleBridge spawns from.
        [SerializeField] private WavePatternStripView wavePatternStrip;
        [SerializeField] private TMP_FontAsset font;

        private GameObject _panel;
        private bool _built;

        private void OnEnable()
        {
            if (gameManager != null) gameManager.MapSetupRequested += OnMapSetupRequested;
        }

        private void OnDisable()
        {
            if (gameManager != null) gameManager.MapSetupRequested -= OnMapSetupRequested;
        }

        private void OnMapSetupRequested()
        {
            if (!_built) BuildCanvas();
            if (mapSettings != null)
            {
                mapSettings.Initialize(draftController);
                mapSettings.gameObject.SetActive(true);
            }
            if (wavePatternStrip != null)
            {
                // Restore the draft stage's "공격패턴" preview into the prep step.
                wavePatternStrip.gameObject.SetActive(true);
                wavePatternStrip.RebuildFromDeck();
                wavePatternStrip.SnapHidden(); // rest positions, then soft fade in
                wavePatternStrip.FadeIn();
                wavePatternStrip.SetToggleEnabled(true);
            }
            _panel.SetActive(true);
        }

        private void OnStartClicked()
        {
            if (mapSettings != null) mapSettings.gameObject.SetActive(false);
            if (wavePatternStrip != null)
            {
                wavePatternStrip.SnapHidden();
                wavePatternStrip.gameObject.SetActive(false);
            }
            _panel.SetActive(false);
            if (gameManager != null) gameManager.RequestPlacement();
        }

        private void BuildCanvas()
        {
            if (_built) return;
            _built = true;

            var canvas = gameObject.GetComponent<Canvas>();
            if (canvas == null) canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 8;
            if (gameObject.GetComponent<CanvasScaler>() == null)
            {
                var scaler = gameObject.AddComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920, 1080);
            }
            if (gameObject.GetComponent<GraphicRaycaster>() == null)
                gameObject.AddComponent<GraphicRaycaster>();

            _panel = new GameObject("SquadPrepPanel", typeof(RectTransform));
            _panel.transform.SetParent(transform, false);
            var prt = (RectTransform)_panel.transform;
            prt.anchorMin = Vector2.zero; prt.anchorMax = Vector2.one; prt.offsetMin = Vector2.zero; prt.offsetMax = Vector2.zero;

            // Title banner (top-center)
            var titleGo = new GameObject("Title", typeof(RectTransform), typeof(TextMeshProUGUI));
            titleGo.transform.SetParent(_panel.transform, false);
            var trt = (RectTransform)titleGo.transform;
            trt.anchorMin = new Vector2(0.5f, 1f); trt.anchorMax = new Vector2(0.5f, 1f); trt.pivot = new Vector2(0.5f, 1f);
            trt.anchoredPosition = new Vector2(0f, -40f); trt.sizeDelta = new Vector2(700f, 70f);
            var ttmp = titleGo.GetComponent<TextMeshProUGUI>();
            ttmp.text = "MAP SETUP"; ttmp.alignment = TextAlignmentOptions.Center; ttmp.fontSize = 40; ttmp.color = Color.white;
            if (font != null) ttmp.font = font;

            // START button (bottom-center)
            var btnGo = new GameObject("StartButton", typeof(RectTransform), typeof(Image), typeof(Button));
            btnGo.transform.SetParent(_panel.transform, false);
            var brt = (RectTransform)btnGo.transform;
            brt.anchorMin = new Vector2(0.5f, 0f); brt.anchorMax = new Vector2(0.5f, 0f); brt.pivot = new Vector2(0.5f, 0f);
            brt.anchoredPosition = new Vector2(0f, 50f); brt.sizeDelta = new Vector2(340f, 84f);
            btnGo.GetComponent<Image>().color = new Color(0.2f, 0.7f, 0.3f, 1f);
            btnGo.GetComponent<Button>().onClick.AddListener(OnStartClicked);
            var blGo = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
            blGo.transform.SetParent(btnGo.transform, false);
            var blrt = (RectTransform)blGo.transform;
            blrt.anchorMin = Vector2.zero; blrt.anchorMax = Vector2.one; blrt.offsetMin = Vector2.zero; blrt.offsetMax = Vector2.zero;
            var bl = blGo.GetComponent<TextMeshProUGUI>();
            bl.text = "START"; bl.alignment = TextAlignmentOptions.Center; bl.fontSize = 34; bl.color = Color.white;
            if (font != null) bl.font = font;

            _panel.SetActive(false);
        }
    }
}
