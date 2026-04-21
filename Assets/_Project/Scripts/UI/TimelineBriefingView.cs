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

            if (deck == null || deck.spawns == null) return;

            float totalTime = deck.timerDurationSec > 0 ? deck.timerDurationSec : 180f;
            float graphWidth = 1400f;
            int previewLaneCount = ReadMapGenerationOptions().spawnLaneCount;

            // Time axis labels
            var axisGO = new GameObject("TimeAxis", typeof(RectTransform));
            axisGO.transform.SetParent(_listContainer, false);
            var axisRT = (RectTransform)axisGO.transform;
            axisRT.sizeDelta = new Vector2(graphWidth, 30f);
            for (int s = 0; s <= (int)totalTime; s += 30)
            {
                float x = (s / totalTime) * graphWidth;
                var label = new GameObject("T" + s, typeof(RectTransform));
                label.transform.SetParent(axisGO.transform, false);
                var lrt = (RectTransform)label.transform;
                lrt.anchorMin = new Vector2(0f, 0f);
                lrt.anchorMax = new Vector2(0f, 1f);
                lrt.pivot = new Vector2(0.5f, 0.5f);
                lrt.anchoredPosition = new Vector2(x, 0f);
                lrt.sizeDelta = new Vector2(50f, 30f);
                var tmp = label.AddComponent<TextMeshProUGUI>();
                tmp.text = $"{s}s";
                tmp.fontSize = 18;
                tmp.color = new Color(0.7f, 0.7f, 0.7f, 1f);
                tmp.alignment = TextAlignmentOptions.Center;
            }

            // One lane per selected spawn lane count.
            for (int spawnIndex = 0; spawnIndex < previewLaneCount; spawnIndex++)
            {
                // Lane container
                var laneGO = new GameObject("Lane_" + spawnIndex, typeof(RectTransform), typeof(Image));
                laneGO.transform.SetParent(_listContainer, false);
                var laneRT = (RectTransform)laneGO.transform;
                laneRT.sizeDelta = new Vector2(graphWidth, 50f);
                laneGO.GetComponent<Image>().color = new Color(0.15f, 0.15f, 0.2f, 0.8f);

                // Lane label
                var laneLabel = new GameObject("Label", typeof(RectTransform));
                laneLabel.transform.SetParent(laneGO.transform, false);
                var llrt = (RectTransform)laneLabel.transform;
                llrt.anchorMin = new Vector2(0f, 0f);
                llrt.anchorMax = new Vector2(0f, 1f);
                llrt.pivot = new Vector2(1f, 0.5f);
                llrt.anchoredPosition = new Vector2(-8f, 0f);
                llrt.sizeDelta = new Vector2(80f, 50f);
                var llTmp = laneLabel.AddComponent<TextMeshProUGUI>();
                llTmp.text = $"Spawn {spawnIndex}";
                llTmp.fontSize = 20;
                llTmp.color = Color.white;
                llTmp.alignment = TextAlignmentOptions.MidlineRight;

                // Plot markers
                for (int i = 0; i < deck.spawns.Count; i++)
                {
                    var sp = deck.spawns[i];
                    if (EffectiveSpawnIndex(sp.spawnIndex, i, previewLaneCount) != spawnIndex) continue;
                    float x = (sp.triggerTimeSec / totalTime) * graphWidth;
                    Color col = SwiftColor;
                    float markerH = 16f;
                    string unitName = "?";
                    if (sp.unitType != null)
                    {
                        unitName = sp.unitType.displayName;
                        if (unitName.Contains("Tanker")) { col = TankerColor; markerH = 36f; }
                        else if (unitName.Contains("Basic")) { col = BasicColor; markerH = 26f; }
                        else { col = SwiftColor; markerH = 18f; }
                    }

                    var marker = new GameObject("M", typeof(RectTransform), typeof(Image));
                    marker.transform.SetParent(laneGO.transform, false);
                    var mrt = (RectTransform)marker.transform;
                    mrt.anchorMin = new Vector2(0f, 0.5f);
                    mrt.anchorMax = new Vector2(0f, 0.5f);
                    mrt.pivot = new Vector2(0.5f, 0.5f);
                    mrt.anchoredPosition = new Vector2(x, 0f);
                    mrt.sizeDelta = new Vector2(8f, markerH);
                    marker.GetComponent<Image>().color = col;
                }
            }

            // Legend + summary
            var legendGO = new GameObject("Legend", typeof(RectTransform));
            legendGO.transform.SetParent(_listContainer, false);
            var legRT = (RectTransform)legendGO.transform;
            legRT.sizeDelta = new Vector2(graphWidth, 40f);
            var legTmp = legendGO.AddComponent<TextMeshProUGUI>();

            int nt = 0, nb = 0, ns = 0;
            foreach (var sp in deck.spawns)
            {
                if (sp.unitType == null) continue;
                if (sp.unitType.displayName.Contains("Tanker")) nt++;
                else if (sp.unitType.displayName.Contains("Basic")) nb++;
                else ns++;
            }
            legTmp.text = $"<color=#FF4D4D>■ Tanker ×{nt}</color>   <color=#8840CC>■ Basic ×{nb}</color>   <color=#F2D933>■ Swift ×{ns}</color>   |   총 {deck.spawns.Count}   |   lanes {previewLaneCount}   |   {totalTime:0}초   |   DEFEAT: {deck.defeatGoalReachedCount}마리 도달";
            legTmp.fontSize = 22;
            legTmp.color = Color.white;
            legTmp.alignment = TextAlignmentOptions.Center;
            legTmp.richText = true;
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
            title.text = "ATTACK TIMELINE";
            title.fontSize = 48;
            title.color = Color.yellow;
            title.alignment = TextAlignmentOptions.Center;

            // List container
            var listGO = new GameObject("List", typeof(RectTransform), typeof(VerticalLayoutGroup));
            listGO.transform.SetParent(_panel.transform, false);
            _listContainer = listGO.transform;
            var lrt = (RectTransform)listGO.transform;
            lrt.anchorMin = new Vector2(0.5f, 0.5f);
            lrt.anchorMax = new Vector2(0.5f, 0.5f);
            lrt.pivot = new Vector2(0.5f, 0.5f);
            lrt.sizeDelta = new Vector2(800f, 500f);
            lrt.anchoredPosition = new Vector2(0f, 20f);
            var vlg = listGO.GetComponent<VerticalLayoutGroup>();
            vlg.spacing = 4f;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;

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

        private static int EffectiveSpawnIndex(int authoredIndex, int deckIndex, int laneCount)
        {
            if (laneCount <= 0) return 0;
            if (laneCount <= 2)
                return Mathf.Clamp(authoredIndex, 0, laneCount - 1);
            return Mathf.Abs(deckIndex) % laneCount;
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
