using TMPro;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.UI;
using Wassup.Bridge;
using Wassup.Core;
using Wassup.Data;
using Wassup.Data.MapGrid;

namespace Wassup.UI.Draft
{
    // Dev-only map options toggle, top-left of the draft canvas. Supports two modes:
    //  - Legacy: pathShape, gridSize, obstacleDensity, spawnLaneCount (ProceduralMapGenerator).
    //  - MapGrid: W/H input (default 20x10) + quick-fill preset buttons for the new MapGridGenerator.
    public class MapSettingsPanelView : MonoBehaviour
    {
        [SerializeField] private DraftController controller;

        private GameObject _panel;
        private Button _toggleButton;

        // Map source row
        private Button _sourceLegacyButton;
        private Button _sourceMapGridButton;

        // MapGrid preset (quick-fill) row + size inputs
        private GameObject _mapGridSection;
        private Button _presetAutoButton;
        private Button _presetWide30x15Button;
        private Button _presetSquare20x20Button;
        private Button _presetTall10x20Button;
        private TMP_InputField _mapGridWidthInput;
        private TMP_InputField _mapGridHeightInput;

        // Legacy rows (containers so we can hide them when MapGrid is active)
        private GameObject _legacyPathSection;
        private GameObject _legacySizeSection;
        private GameObject _legacyDensitySection;
        private GameObject _legacySpawnSection;
        private Button _straightButton;
        private Button _freeButton;
        private Button _densityLowButton;
        private Button _densityMediumButton;
        private Button _densityHighButton;
        private TMP_InputField _widthInput;
        private TMP_InputField _heightInput;
        private TMP_InputField _spawnLaneInput;

        private const int DefaultMapGridWidth = 20;
        private const int DefaultMapGridHeight = 10;

        private MapSource _selectedMapSource = MapSource.Legacy;
        // null = Auto (seed-based). Non-null = explicit override.
        private int2? _selectedMapGridSize = new int2(DefaultMapGridWidth, DefaultMapGridHeight);
        private MapPathShape _selectedPathShape = MapPathShape.Straight;
        private MapObstacleDensity _selectedDensity = MapObstacleDensity.Low;
        private bool _built;
        private bool _suppressInputCallback;

        public void Initialize(DraftController draftController)
        {
            controller = draftController;
            if (!_built) Build();
            PushAllToController();
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

            var selfRt = (RectTransform)transform;
            selfRt.anchorMin = Vector2.zero;
            selfRt.anchorMax = Vector2.one;
            selfRt.offsetMin = Vector2.zero;
            selfRt.offsetMax = Vector2.zero;

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
            panelRt.anchoredPosition = new Vector2(280f, -40f);
            panelRt.sizeDelta = new Vector2(360f, 580f);
            _panel.GetComponent<Image>().color = new Color(0.08f, 0.1f, 0.14f, 0.94f);
            var layout = _panel.GetComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(12, 12, 12, 12);
            layout.spacing = 10f;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            // --- Map Source row (always visible) ---
            AddPanelLabel(_panel.transform, "Map Source", 22);
            var sourceRow = AddRow(_panel.transform, "SourceRow", 52f);
            _sourceLegacyButton = CreateButton(sourceRow, "Legacy", "LEGACY", Color.gray);
            _sourceMapGridButton = CreateButton(sourceRow, "MapGrid", "MAPGRID", Color.gray);
            _sourceLegacyButton.onClick.AddListener(() => SelectMapSource(MapSource.Legacy));
            _sourceMapGridButton.onClick.AddListener(() => SelectMapSource(MapSource.MapGrid));

            // --- MapGrid section (visible only when MapGrid) ---
            _mapGridSection = AddSection(_panel.transform, "MapGridSection");
            AddPanelLabel(_mapGridSection.transform, "Preset (quick-fill)", 20);
            var presetRow1 = AddRow(_mapGridSection.transform, "PresetRow1", 48f);
            _presetAutoButton = CreateButton(presetRow1, "Auto", "AUTO", Color.gray, fontSize: 18);
            _presetWide30x15Button = CreateButton(presetRow1, "Wide30x15", "30x15", Color.gray, fontSize: 18);
            var presetRow2 = AddRow(_mapGridSection.transform, "PresetRow2", 48f);
            _presetSquare20x20Button = CreateButton(presetRow2, "Square20x20", "20x20", Color.gray, fontSize: 18);
            _presetTall10x20Button = CreateButton(presetRow2, "Tall10x20", "10x20", Color.gray, fontSize: 18);
            _presetAutoButton.onClick.AddListener(() => SelectGridSize(null));
            _presetWide30x15Button.onClick.AddListener(() => SelectGridSize(new int2(30, 15)));
            _presetSquare20x20Button.onClick.AddListener(() => SelectGridSize(new int2(20, 20)));
            _presetTall10x20Button.onClick.AddListener(() => SelectGridSize(new int2(10, 20)));

            AddPanelLabel(_mapGridSection.transform, "Map Size", 20);
            var sizeRowMg = AddRow(_mapGridSection.transform, "MapGridSizeRow", 52f);
            _mapGridWidthInput = CreateInput(sizeRowMg, "MgWidth", DefaultMapGridWidth.ToString());
            _mapGridHeightInput = CreateInput(sizeRowMg, "MgHeight", DefaultMapGridHeight.ToString());
            _mapGridWidthInput.onEndEdit.AddListener(_ => OnMapGridSizeInputChanged());
            _mapGridHeightInput.onEndEdit.AddListener(_ => OnMapGridSizeInputChanged());

            // --- Legacy sections ---
            _legacyPathSection = AddSection(_panel.transform, "LegacyPathSection");
            AddPanelLabel(_legacyPathSection.transform, "Path Type", 22);
            var pathRow = AddRow(_legacyPathSection.transform, "PathRow", 52f);
            _straightButton = CreateButton(pathRow, "Straight", "STRAIGHT", Color.gray);
            _freeButton = CreateButton(pathRow, "Free", "FREE", Color.gray);
            _straightButton.onClick.AddListener(() => { _selectedPathShape = MapPathShape.Straight; RefreshButtonHighlights(); PushLegacyOptions(); });
            _freeButton.onClick.AddListener(() => { _selectedPathShape = MapPathShape.Free; RefreshButtonHighlights(); PushLegacyOptions(); });

            _legacySizeSection = AddSection(_panel.transform, "LegacySizeSection");
            AddPanelLabel(_legacySizeSection.transform, "Map Size", 22);
            var sizeRow = AddRow(_legacySizeSection.transform, "SizeRow", 52f);
            _widthInput = CreateInput(sizeRow, "Width", "20");
            _heightInput = CreateInput(sizeRow, "Height", "10");
            _widthInput.onEndEdit.AddListener(_ => PushLegacyOptions());
            _heightInput.onEndEdit.AddListener(_ => PushLegacyOptions());

            _legacyDensitySection = AddSection(_panel.transform, "LegacyDensitySection");
            AddPanelLabel(_legacyDensitySection.transform, "Object Density", 22);
            var densityRow = AddRow(_legacyDensitySection.transform, "DensityRow", 52f);
            _densityLowButton = CreateButton(densityRow, "Low", "LOW", Color.gray);
            _densityMediumButton = CreateButton(densityRow, "Medium", "MID", Color.gray);
            _densityHighButton = CreateButton(densityRow, "High", "HIGH", Color.gray);
            _densityLowButton.onClick.AddListener(() => { _selectedDensity = MapObstacleDensity.Low; RefreshButtonHighlights(); PushLegacyOptions(); });
            _densityMediumButton.onClick.AddListener(() => { _selectedDensity = MapObstacleDensity.Medium; RefreshButtonHighlights(); PushLegacyOptions(); });
            _densityHighButton.onClick.AddListener(() => { _selectedDensity = MapObstacleDensity.High; RefreshButtonHighlights(); PushLegacyOptions(); });

            _legacySpawnSection = AddSection(_panel.transform, "LegacySpawnSection");
            AddPanelLabel(_legacySpawnSection.transform, "Spawn Lanes", 22);
            var spawnRow = AddRow(_legacySpawnSection.transform, "SpawnRow", 52f);
            _spawnLaneInput = CreateInput(spawnRow, "SpawnLaneCount", "2");
            _spawnLaneInput.onEndEdit.AddListener(_ => PushLegacyOptions());

            RefreshSectionVisibility();
            RefreshButtonHighlights();
            _panel.SetActive(false);
        }

        private void TogglePanel()
        {
            if (_panel == null) return;
            _panel.SetActive(!_panel.activeSelf);
        }

        private void SelectMapSource(MapSource src)
        {
            _selectedMapSource = src;
            RefreshSectionVisibility();
            RefreshButtonHighlights();
            if (controller != null) controller.SetMapSource(src);
            if (src == MapSource.MapGrid)
            {
                if (controller != null) controller.SetMapGridGridSize(_selectedMapGridSize);
            }
            else
            {
                PushLegacyOptions();
            }
        }

        // Called by Auto / preset quick-fill buttons.
        private void SelectGridSize(int2? gridSize)
        {
            _selectedMapGridSize = gridSize;
            UpdateSizeInputsFromSelection();
            RefreshButtonHighlights();
            if (controller != null) controller.SetMapGridGridSize(gridSize);
        }

        private void OnMapGridSizeInputChanged()
        {
            if (_suppressInputCallback) return;
            int w = ParsePositiveInt(_mapGridWidthInput, DefaultMapGridWidth);
            int h = ParsePositiveInt(_mapGridHeightInput, DefaultMapGridHeight);
            w = Mathf.Max(MapGridBattleAdapter.MinGridDimension, w);
            h = Mathf.Max(MapGridBattleAdapter.MinGridDimension, h);
            _selectedMapGridSize = new int2(w, h);
            // Update the field text in case clamp adjusted it.
            UpdateSizeInputsFromSelection();
            RefreshButtonHighlights();
            if (controller != null) controller.SetMapGridGridSize(_selectedMapGridSize);
        }

        private void UpdateSizeInputsFromSelection()
        {
            if (_mapGridWidthInput == null || _mapGridHeightInput == null) return;
            _suppressInputCallback = true;
            if (_selectedMapGridSize.HasValue)
            {
                _mapGridWidthInput.text = _selectedMapGridSize.Value.x.ToString();
                _mapGridHeightInput.text = _selectedMapGridSize.Value.y.ToString();
                _mapGridWidthInput.interactable = true;
                _mapGridHeightInput.interactable = true;
            }
            else
            {
                _mapGridWidthInput.text = "auto";
                _mapGridHeightInput.text = "auto";
                _mapGridWidthInput.interactable = false;
                _mapGridHeightInput.interactable = false;
            }
            _suppressInputCallback = false;
        }

        private void RefreshSectionVisibility()
        {
            bool isMapGrid = _selectedMapSource == MapSource.MapGrid;
            if (_mapGridSection != null) _mapGridSection.SetActive(isMapGrid);
            if (_legacyPathSection != null) _legacyPathSection.SetActive(!isMapGrid);
            if (_legacySizeSection != null) _legacySizeSection.SetActive(!isMapGrid);
            if (_legacyDensitySection != null) _legacyDensitySection.SetActive(!isMapGrid);
            if (_legacySpawnSection != null) _legacySpawnSection.SetActive(!isMapGrid);

            if (isMapGrid) UpdateSizeInputsFromSelection();
        }

        private void PushAllToController()
        {
            if (controller == null) return;
            controller.SetMapSource(_selectedMapSource);
            if (_selectedMapSource == MapSource.MapGrid)
                controller.SetMapGridGridSize(_selectedMapGridSize);
            else
                PushLegacyOptions();
        }

        private void PushLegacyOptions()
        {
            if (controller == null) return;
            controller.SetMapGenerationOptions(ReadLegacyOptions());
        }

        private MapGenerationOptions ReadLegacyOptions()
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

        private bool MatchesPreset(int2 preset) =>
            _selectedMapGridSize.HasValue &&
            _selectedMapGridSize.Value.x == preset.x &&
            _selectedMapGridSize.Value.y == preset.y;

        private void RefreshButtonHighlights()
        {
            SetSelected(_sourceLegacyButton, _selectedMapSource == MapSource.Legacy);
            SetSelected(_sourceMapGridButton, _selectedMapSource == MapSource.MapGrid);

            SetSelected(_presetAutoButton, !_selectedMapGridSize.HasValue);
            SetSelected(_presetWide30x15Button, MatchesPreset(new int2(30, 15)));
            SetSelected(_presetSquare20x20Button, MatchesPreset(new int2(20, 20)));
            SetSelected(_presetTall10x20Button, MatchesPreset(new int2(10, 20)));

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

        private static GameObject AddSection(Transform parent, string name)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(VerticalLayoutGroup));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.sizeDelta = new Vector2(0f, 80f);
            var layout = go.GetComponent<VerticalLayoutGroup>();
            layout.spacing = 6f;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            return go;
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
