using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Wassup.Core;
using Wassup.Data;

namespace Wassup.UI
{
    // Pre-draft attack timeline preview. Shows every spawn entry in the
    // current AttackDeck so the player can plan their draft picks before
    // committing. Mirrors DraftView's runtime-build pattern.
    public class TimelineBriefingView : MonoBehaviour
    {
        [SerializeField] private AttackDeck deck;
        [SerializeField] private DraftController draftController;

        private GameObject _panel;
        private GameObject _mapSettingsPanel;
        private Transform _listContainer;
        private Button _confirmButton;
        private Button _settingsToggleButton;
        private Button _straightButton;
        private Button _freeButton;
        private Button _densityLowButton;
        private Button _densityMediumButton;
        private Button _densityHighButton;
        private TMP_InputField _widthInput;
        private TMP_InputField _heightInput;
        private TMP_InputField _spawnLaneInput;
        private MapPathShape _selectedPathShape = MapPathShape.Straight;
        private MapObstacleDensity _selectedDensity = MapObstacleDensity.Low;
        private bool _built;

        public System.Action BriefingConfirmed;

        private void Awake()
        {
            BuildCanvas();
            if (_panel != null) _panel.SetActive(false);
        }

        public void Show()
        {
            if (!_built) BuildCanvas();
            RebuildList();
            _panel.SetActive(true);
        }

        public void Hide()
        {
            if (_panel != null) _panel.SetActive(false);
        }

        private void OnConfirmClicked()
        {
            if (draftController != null)
                draftController.SetMapGenerationOptions(ReadMapGenerationOptions());
            Hide();
            BriefingConfirmed?.Invoke();
        }

        private MapGenerationOptions ReadMapGenerationOptions()
        {
            int width = ParsePositiveInt(_widthInput, 20);
            int height = ParsePositiveInt(_heightInput, 10);
            int spawnLanes = ParsePositiveInt(_spawnLaneInput, 2);

            return new MapGenerationOptions
            {
                pathShape = _selectedPathShape,
                gridSize = new Unity.Mathematics.int2(width, height),
                obstacleDensity = _selectedDensity,
                spawnLaneCount = spawnLanes,
            }.Normalized();
        }

        private static int ParsePositiveInt(TMP_InputField input, int fallback)
        {
            if (input == null) return fallback;
            return int.TryParse(input.text, out int value) ? Mathf.Max(1, value) : fallback;
        }

        private static readonly Color TankerColor = new Color(1f, 0.3f, 0.3f, 1f);
        private static readonly Color BasicColor = new Color(0.55f, 0.25f, 0.8f, 1f);
        private static readonly Color SwiftColor = new Color(0.95f, 0.85f, 0.2f, 1f);

        private void RebuildList()
        {
            for (int i = _listContainer.childCount - 1; i >= 0; i--)
                Destroy(_listContainer.GetChild(i).gameObject);

            if (deck == null) return;

            GeneratedWavePlan plan;
            try
            {
                plan = WavePatternGenerator.Generate(deck);
            }
            catch (System.Exception ex)
            {
                AddMessageRow("WAVE PREVIEW UNAVAILABLE", ex.Message);
                return;
            }

            AddSummaryHeader(plan);
            for (int i = 0; i < plan.waves.Count; i++)
                AddWaveRow(plan.waves[i]);
        }

        private void AddSummaryHeader(GeneratedWavePlan plan)
        {
            var go = new GameObject("WaveSummaryHeader", typeof(RectTransform));
            go.transform.SetParent(_listContainer, false);
            var rt = (RectTransform)go.transform;
            rt.sizeDelta = new Vector2(0f, 58f);
            var tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.text = $"Seed {plan.seed}   |   {plan.waves.Count} Waves   |   Every {plan.waveIntervalSec:0.##}s   |   {plan.timerDurationSec:0}s";
            tmp.fontSize = 24;
            tmp.color = new Color(0.72f, 0.78f, 0.88f, 1f);
            tmp.alignment = TextAlignmentOptions.Center;
        }

        private void AddWaveRow(GeneratedWave wave)
        {
            var row = new GameObject($"Wave_{wave.waveIndex + 1:00}", typeof(RectTransform), typeof(Image), typeof(HorizontalLayoutGroup));
            row.transform.SetParent(_listContainer, false);
            var rt = (RectTransform)row.transform;
            rt.sizeDelta = new Vector2(0f, 104f);
            row.GetComponent<Image>().color = new Color(0.09f, 0.11f, 0.16f, 0.96f);

            var layout = row.GetComponent<HorizontalLayoutGroup>();
            layout.padding = new RectOffset(24, 24, 12, 12);
            layout.spacing = 18f;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = true;

            AddRowText(row.transform, $"WAVE {wave.waveIndex + 1:00}\n{wave.triggerTimeSec:0.#}s", 170f, 26, new Color(1f, 0.86f, 0.24f, 1f), TextAlignmentOptions.MidlineLeft);
            AddRowText(row.transform, $"{UnitName(wave.unitA)}  x{wave.countA}", 360f, 32, UnitColor(wave.unitA), TextAlignmentOptions.MidlineLeft);
            AddRowText(row.transform, $"{UnitName(wave.unitB)}  x{wave.countB}", 360f, 32, UnitColor(wave.unitB), TextAlignmentOptions.MidlineLeft);
            AddRowText(row.transform, $"TOTAL\n{wave.totalCount}", 140f, 26, Color.white, TextAlignmentOptions.MidlineRight);
        }

        private void AddMessageRow(string title, string message)
        {
            var go = new GameObject("WaveMessage", typeof(RectTransform));
            go.transform.SetParent(_listContainer, false);
            var rt = (RectTransform)go.transform;
            rt.sizeDelta = new Vector2(0f, 140f);
            var tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.text = $"{title}\n{message}";
            tmp.fontSize = 28;
            tmp.color = Color.white;
            tmp.alignment = TextAlignmentOptions.Center;
        }

        private static void AddRowText(Transform parent, string text, float width, int fontSize, Color color, TextAlignmentOptions alignment)
        {
            var go = new GameObject("Text", typeof(RectTransform), typeof(LayoutElement));
            go.transform.SetParent(parent, false);
            var layout = go.GetComponent<LayoutElement>();
            layout.preferredWidth = width;
            layout.minWidth = width;
            var tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.fontSize = fontSize;
            tmp.color = color;
            tmp.alignment = alignment;
            tmp.enableWordWrapping = false;
        }

        private static string UnitName(AttackUnitData unit)
        {
            return unit != null && !string.IsNullOrWhiteSpace(unit.displayName) ? unit.displayName : "?";
        }

        private static Color UnitColor(AttackUnitData unit)
        {
            string name = UnitName(unit);
            if (name.Contains("Tanker")) return TankerColor;
            if (name.Contains("Basic")) return BasicColor;
            if (name.Contains("Swift")) return SwiftColor;
            return new Color(0.55f, 0.85f, 1f, 1f);
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

            _panel = new GameObject("BriefingPanel", typeof(RectTransform), typeof(Image));
            _panel.transform.SetParent(transform, false);
            var prt = (RectTransform)_panel.transform;
            prt.anchorMin = Vector2.zero;
            prt.anchorMax = Vector2.one;
            prt.offsetMin = Vector2.zero;
            prt.offsetMax = Vector2.zero;
            _panel.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.9f);

            // Title
            var titleGO = new GameObject("Title", typeof(RectTransform));
            titleGO.transform.SetParent(_panel.transform, false);
            var trt = (RectTransform)titleGO.transform;
            trt.anchorMin = new Vector2(0f, 1f);
            trt.anchorMax = new Vector2(1f, 1f);
            trt.pivot = new Vector2(0.5f, 1f);
            trt.anchoredPosition = new Vector2(0f, -40f);
            trt.sizeDelta = new Vector2(0f, 60f);
            var title = titleGO.AddComponent<TextMeshProUGUI>();
            title.text = "ATTACK WAVES";
            title.fontSize = 48;
            title.color = Color.yellow;
            title.alignment = TextAlignmentOptions.Center;

            // List container
            var scrollGO = new GameObject("WaveScroll", typeof(RectTransform), typeof(Image), typeof(ScrollRect));
            scrollGO.transform.SetParent(_panel.transform, false);
            var srt = (RectTransform)scrollGO.transform;
            srt.anchorMin = new Vector2(0.5f, 0.5f);
            srt.anchorMax = new Vector2(0.5f, 0.5f);
            srt.pivot = new Vector2(0.5f, 0.5f);
            srt.sizeDelta = new Vector2(1180f, 620f);
            srt.anchoredPosition = new Vector2(0f, -5f);
            scrollGO.GetComponent<Image>().color = new Color(0.02f, 0.025f, 0.035f, 0.84f);

            var viewportGO = new GameObject("Viewport", typeof(RectTransform), typeof(Image), typeof(Mask));
            viewportGO.transform.SetParent(scrollGO.transform, false);
            var vrt = (RectTransform)viewportGO.transform;
            vrt.anchorMin = Vector2.zero;
            vrt.anchorMax = Vector2.one;
            vrt.offsetMin = new Vector2(16f, 16f);
            vrt.offsetMax = new Vector2(-16f, -16f);
            viewportGO.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.01f);
            viewportGO.GetComponent<Mask>().showMaskGraphic = false;

            var listGO = new GameObject("List", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
            listGO.transform.SetParent(viewportGO.transform, false);
            _listContainer = listGO.transform;
            var lrt = (RectTransform)listGO.transform;
            lrt.anchorMin = new Vector2(0f, 1f);
            lrt.anchorMax = new Vector2(1f, 1f);
            lrt.pivot = new Vector2(0.5f, 1f);
            lrt.offsetMin = Vector2.zero;
            lrt.offsetMax = Vector2.zero;
            var vlg = listGO.GetComponent<VerticalLayoutGroup>();
            vlg.padding = new RectOffset(0, 0, 0, 0);
            vlg.spacing = 4f;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;
            var fitter = listGO.GetComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var scroll = scrollGO.GetComponent<ScrollRect>();
            scroll.viewport = vrt;
            scroll.content = lrt;
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;

            // Confirm button
            var btnGO = new GameObject("Confirm", typeof(RectTransform), typeof(Image), typeof(Button));
            btnGO.transform.SetParent(_panel.transform, false);
            var brt = (RectTransform)btnGO.transform;
            brt.anchorMin = new Vector2(0.5f, 0f);
            brt.anchorMax = new Vector2(0.5f, 0f);
            brt.pivot = new Vector2(0.5f, 0f);
            brt.anchoredPosition = new Vector2(0f, 60f);
            brt.sizeDelta = new Vector2(300f, 70f);
            btnGO.GetComponent<Image>().color = new Color(0.2f, 0.7f, 0.3f, 1f);
            _confirmButton = btnGO.GetComponent<Button>();
            _confirmButton.onClick.AddListener(OnConfirmClicked);

            var btnLabel = new GameObject("Label", typeof(RectTransform));
            btnLabel.transform.SetParent(btnGO.transform, false);
            var blrt = (RectTransform)btnLabel.transform;
            blrt.anchorMin = Vector2.zero;
            blrt.anchorMax = Vector2.one;
            blrt.offsetMin = Vector2.zero;
            blrt.offsetMax = Vector2.zero;
            var bl = btnLabel.AddComponent<TextMeshProUGUI>();
            bl.text = "DRAFT START";
            bl.fontSize = 32;
            bl.color = Color.white;
            bl.alignment = TextAlignmentOptions.Center;

            BuildMapSettingsPanel();
            RefreshMapSettingsButtons();
        }

        private void BuildMapSettingsPanel()
        {
            _settingsToggleButton = CreateBriefingButton(_panel.transform, "MapSettingsToggle", "MAP SETTINGS", new Color(0.18f, 0.42f, 0.75f, 1f));
            var toggleRt = (RectTransform)_settingsToggleButton.transform;
            toggleRt.anchorMin = new Vector2(0f, 1f);
            toggleRt.anchorMax = new Vector2(0f, 1f);
            toggleRt.pivot = new Vector2(0f, 1f);
            toggleRt.anchoredPosition = new Vector2(40f, -40f);
            toggleRt.sizeDelta = new Vector2(260f, 58f);
            _settingsToggleButton.onClick.AddListener(() =>
            {
                if (_mapSettingsPanel != null) _mapSettingsPanel.SetActive(!_mapSettingsPanel.activeSelf);
            });

            _mapSettingsPanel = new GameObject("MapSettingsPanel", typeof(RectTransform), typeof(Image), typeof(VerticalLayoutGroup));
            _mapSettingsPanel.transform.SetParent(_panel.transform, false);
            var panelRt = (RectTransform)_mapSettingsPanel.transform;
            panelRt.anchorMin = new Vector2(0f, 1f);
            panelRt.anchorMax = new Vector2(0f, 1f);
            panelRt.pivot = new Vector2(0f, 1f);
            panelRt.anchoredPosition = new Vector2(40f, -110f);
            panelRt.sizeDelta = new Vector2(360f, 360f);
            _mapSettingsPanel.GetComponent<Image>().color = new Color(0.08f, 0.1f, 0.14f, 0.94f);
            var layout = _mapSettingsPanel.GetComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(12, 12, 12, 12);
            layout.spacing = 10f;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            AddPanelLabel(_mapSettingsPanel.transform, "Path Type", 22);
            var pathRow = AddRow(_mapSettingsPanel.transform, "PathRow", 52f);
            _straightButton = CreateBriefingButton(pathRow, "Straight", "STRAIGHT", Color.gray);
            _freeButton = CreateBriefingButton(pathRow, "Free", "FREE", Color.gray);
            _straightButton.onClick.AddListener(() => { _selectedPathShape = MapPathShape.Straight; RefreshMapSettingsButtons(); });
            _freeButton.onClick.AddListener(() => { _selectedPathShape = MapPathShape.Free; RefreshMapSettingsButtons(); });

            AddPanelLabel(_mapSettingsPanel.transform, "Map Size", 22);
            var sizeRow = AddRow(_mapSettingsPanel.transform, "SizeRow", 52f);
            _widthInput = CreateInput(sizeRow, "Width", "20");
            _heightInput = CreateInput(sizeRow, "Height", "10");
            _widthInput.onEndEdit.AddListener(_ => RebuildList());
            _heightInput.onEndEdit.AddListener(_ => RebuildList());

            AddPanelLabel(_mapSettingsPanel.transform, "Object Density", 22);
            var densityRow = AddRow(_mapSettingsPanel.transform, "DensityRow", 52f);
            _densityLowButton = CreateBriefingButton(densityRow, "Low", "LOW", Color.gray);
            _densityMediumButton = CreateBriefingButton(densityRow, "Medium", "MID", Color.gray);
            _densityHighButton = CreateBriefingButton(densityRow, "High", "HIGH", Color.gray);
            _densityLowButton.onClick.AddListener(() => { _selectedDensity = MapObstacleDensity.Low; RefreshMapSettingsButtons(); });
            _densityMediumButton.onClick.AddListener(() => { _selectedDensity = MapObstacleDensity.Medium; RefreshMapSettingsButtons(); });
            _densityHighButton.onClick.AddListener(() => { _selectedDensity = MapObstacleDensity.High; RefreshMapSettingsButtons(); });

            AddPanelLabel(_mapSettingsPanel.transform, "Spawn Lanes", 22);
            var spawnRow = AddRow(_mapSettingsPanel.transform, "SpawnRow", 52f);
            _spawnLaneInput = CreateInput(spawnRow, "SpawnLaneCount", "2");
            _spawnLaneInput.onEndEdit.AddListener(_ => RebuildList());
        }

        private void RefreshMapSettingsButtons()
        {
            SetSelectedButton(_straightButton, _selectedPathShape == MapPathShape.Straight);
            SetSelectedButton(_freeButton, _selectedPathShape == MapPathShape.Free);
            SetSelectedButton(_densityLowButton, _selectedDensity == MapObstacleDensity.Low);
            SetSelectedButton(_densityMediumButton, _selectedDensity == MapObstacleDensity.Medium);
            SetSelectedButton(_densityHighButton, _selectedDensity == MapObstacleDensity.High);
        }

        private static void SetSelectedButton(Button button, bool selected)
        {
            if (button == null) return;
            var image = button.GetComponent<Image>();
            if (image != null)
                image.color = selected ? new Color(0.25f, 0.68f, 0.95f, 1f) : new Color(0.16f, 0.18f, 0.22f, 1f);
        }

        private static Transform AddRow(Transform parent, string name, float height)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(HorizontalLayoutGroup));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.sizeDelta = new Vector2(0f, height);
            var layout = go.GetComponent<HorizontalLayoutGroup>();
            layout.spacing = 8f;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = true;
            return go.transform;
        }

        private static void AddPanelLabel(Transform parent, string text, int fontSize)
        {
            var go = new GameObject(text, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.sizeDelta = new Vector2(0f, 28f);
            var tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.fontSize = fontSize;
            tmp.color = Color.white;
            tmp.alignment = TextAlignmentOptions.MidlineLeft;
        }

        private static TMP_InputField CreateInput(Transform parent, string name, string value)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(TMP_InputField));
            go.transform.SetParent(parent, false);
            go.GetComponent<Image>().color = new Color(0.05f, 0.06f, 0.08f, 1f);
            var input = go.GetComponent<TMP_InputField>();

            var textGo = new GameObject("Text", typeof(RectTransform));
            textGo.transform.SetParent(go.transform, false);
            var textRt = (RectTransform)textGo.transform;
            textRt.anchorMin = Vector2.zero;
            textRt.anchorMax = Vector2.one;
            textRt.offsetMin = new Vector2(8f, 0f);
            textRt.offsetMax = new Vector2(-8f, 0f);
            var tmp = textGo.AddComponent<TextMeshProUGUI>();
            tmp.fontSize = 28;
            tmp.color = Color.white;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.text = value;

            input.textComponent = tmp;
            input.text = value;
            input.contentType = TMP_InputField.ContentType.IntegerNumber;
            return input;
        }

        private static Button CreateBriefingButton(Transform parent, string name, string text, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            go.GetComponent<Image>().color = color;
            var button = go.GetComponent<Button>();
            var labelGo = new GameObject("Label", typeof(RectTransform));
            labelGo.transform.SetParent(go.transform, false);
            var rt = (RectTransform)labelGo.transform;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            var tmp = labelGo.AddComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.fontSize = 24;
            tmp.color = Color.white;
            tmp.alignment = TextAlignmentOptions.Center;
            return button;
        }
    }
}
