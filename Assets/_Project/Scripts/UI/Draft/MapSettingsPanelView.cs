using TMPro;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.UI;
using Wassup.Core;
using Wassup.Data;

namespace Wassup.UI.Draft
{
    // Dev-only map options toggle, top-left of the draft canvas. Adjusts pathShape,
    // gridSize, obstacleDensity and spawnLaneCount on the active DraftController.
    // Extracted from the now-removed TimelineBriefingView; visuals/layout sized to
    // stay clear of the wave-pattern strip (top), card fan (bottom), and skill
    // loadout (right). Rebuilt at runtime, no prefab assets.
    public class MapSettingsPanelView : MonoBehaviour
    {
        [SerializeField] private DraftController controller;

        private GameObject _panel;
        private Button _toggleButton;
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

        public void Initialize(DraftController draftController)
        {
            controller = draftController;
            if (!_built) Build();
            PushOptionsToController();
        }

        private void Awake()
        {
            if (!_built) Build();
            if (_panel != null) _panel.SetActive(false);
        }

        private void Build()
        {
            if (_built) return;
            _built = true;

            // Self stretch — required so child anchors resolve to canvas corners.
            var selfRt = (RectTransform)transform;
            selfRt.anchorMin = Vector2.zero;
            selfRt.anchorMax = Vector2.one;
            selfRt.offsetMin = Vector2.zero;
            selfRt.offsetMax = Vector2.zero;

            // Toggle button anchored top-left of the parent canvas.
            _toggleButton = CreateButton(transform, "MapSettingsToggle", "MAP SETTINGS",
                new Color(0.18f, 0.42f, 0.75f, 1f), fontSize: 22);
            var toggleRt = (RectTransform)_toggleButton.transform;
            toggleRt.anchorMin = new Vector2(0f, 1f);
            toggleRt.anchorMax = new Vector2(0f, 1f);
            toggleRt.pivot = new Vector2(0f, 1f);
            toggleRt.anchoredPosition = new Vector2(40f, -40f);
            toggleRt.sizeDelta = new Vector2(220f, 50f);
            _toggleButton.onClick.AddListener(TogglePanel);

            _panel = new GameObject("MapSettingsPanel", typeof(RectTransform), typeof(Image), typeof(VerticalLayoutGroup));
            _panel.transform.SetParent(transform, false);
            var panelRt = (RectTransform)_panel.transform;
            panelRt.anchorMin = new Vector2(0f, 1f);
            panelRt.anchorMax = new Vector2(0f, 1f);
            panelRt.pivot = new Vector2(0f, 1f);
            // Expand panel to the right of the toggle so the wave-pattern toggle
            // (placed at 40, -110) is not occluded.
            panelRt.anchoredPosition = new Vector2(280f, -40f);
            panelRt.sizeDelta = new Vector2(360f, 360f);
            _panel.GetComponent<Image>().color = new Color(0.08f, 0.1f, 0.14f, 0.94f);
            var layout = _panel.GetComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(12, 12, 12, 12);
            layout.spacing = 10f;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            AddPanelLabel(_panel.transform, "Path Type", 22);
            var pathRow = AddRow(_panel.transform, "PathRow", 52f);
            _straightButton = CreateButton(pathRow, "Straight", "STRAIGHT", Color.gray);
            _freeButton = CreateButton(pathRow, "Free", "FREE", Color.gray);
            _straightButton.onClick.AddListener(() => { _selectedPathShape = MapPathShape.Straight; RefreshButtonHighlights(); PushOptionsToController(); });
            _freeButton.onClick.AddListener(() => { _selectedPathShape = MapPathShape.Free; RefreshButtonHighlights(); PushOptionsToController(); });

            AddPanelLabel(_panel.transform, "Map Size", 22);
            var sizeRow = AddRow(_panel.transform, "SizeRow", 52f);
            _widthInput = CreateInput(sizeRow, "Width", "20");
            _heightInput = CreateInput(sizeRow, "Height", "10");
            _widthInput.onEndEdit.AddListener(_ => PushOptionsToController());
            _heightInput.onEndEdit.AddListener(_ => PushOptionsToController());

            AddPanelLabel(_panel.transform, "Object Density", 22);
            var densityRow = AddRow(_panel.transform, "DensityRow", 52f);
            _densityLowButton = CreateButton(densityRow, "Low", "LOW", Color.gray);
            _densityMediumButton = CreateButton(densityRow, "Medium", "MID", Color.gray);
            _densityHighButton = CreateButton(densityRow, "High", "HIGH", Color.gray);
            _densityLowButton.onClick.AddListener(() => { _selectedDensity = MapObstacleDensity.Low; RefreshButtonHighlights(); PushOptionsToController(); });
            _densityMediumButton.onClick.AddListener(() => { _selectedDensity = MapObstacleDensity.Medium; RefreshButtonHighlights(); PushOptionsToController(); });
            _densityHighButton.onClick.AddListener(() => { _selectedDensity = MapObstacleDensity.High; RefreshButtonHighlights(); PushOptionsToController(); });

            AddPanelLabel(_panel.transform, "Spawn Lanes", 22);
            var spawnRow = AddRow(_panel.transform, "SpawnRow", 52f);
            _spawnLaneInput = CreateInput(spawnRow, "SpawnLaneCount", "2");
            _spawnLaneInput.onEndEdit.AddListener(_ => PushOptionsToController());

            RefreshButtonHighlights();
            _panel.SetActive(false);
        }

        private void TogglePanel()
        {
            if (_panel == null) return;
            _panel.SetActive(!_panel.activeSelf);
        }

        private void PushOptionsToController()
        {
            if (controller == null) return;
            controller.SetMapGenerationOptions(ReadOptions());
        }

        private MapGenerationOptions ReadOptions()
        {
            int width = ParsePositiveInt(_widthInput, 20);
            int height = ParsePositiveInt(_heightInput, 10);
            int spawnLanes = ParsePositiveInt(_spawnLaneInput, 2);
            return new MapGenerationOptions
            {
                pathShape = _selectedPathShape,
                gridSize = new int2(width, height),
                obstacleDensity = _selectedDensity,
                spawnLaneCount = spawnLanes,
            }.Normalized();
        }

        private static int ParsePositiveInt(TMP_InputField input, int fallback)
        {
            if (input == null) return fallback;
            return int.TryParse(input.text, out int value) ? Mathf.Max(1, value) : fallback;
        }

        private void RefreshButtonHighlights()
        {
            SetSelected(_straightButton, _selectedPathShape == MapPathShape.Straight);
            SetSelected(_freeButton, _selectedPathShape == MapPathShape.Free);
            SetSelected(_densityLowButton, _selectedDensity == MapObstacleDensity.Low);
            SetSelected(_densityMediumButton, _selectedDensity == MapObstacleDensity.Medium);
            SetSelected(_densityHighButton, _selectedDensity == MapObstacleDensity.High);
        }

        private static void SetSelected(Button button, bool selected)
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

        private static Button CreateButton(Transform parent, string name, string text, Color color, int fontSize = 24)
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
            tmp.fontSize = fontSize;
            tmp.color = Color.white;
            tmp.alignment = TextAlignmentOptions.Center;
            return button;
        }
    }
}
