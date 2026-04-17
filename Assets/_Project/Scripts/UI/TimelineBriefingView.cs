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
        private Transform _listContainer;
        private Button _confirmButton;
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
            Hide();
            BriefingConfirmed?.Invoke();
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

            // Collect unique paths
            var pathIds = new List<string>();
            foreach (var sp in deck.spawns)
            {
                if (!pathIds.Contains(sp.pathId)) pathIds.Add(sp.pathId);
            }
            pathIds.Sort();

            // One lane per path
            foreach (var pathId in pathIds)
            {
                // Lane container
                var laneGO = new GameObject("Lane_" + pathId, typeof(RectTransform), typeof(Image));
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
                llTmp.text = $"Path {pathId}";
                llTmp.fontSize = 20;
                llTmp.color = Color.white;
                llTmp.alignment = TextAlignmentOptions.MidlineRight;

                // Plot markers
                foreach (var sp in deck.spawns)
                {
                    if (sp.pathId != pathId) continue;
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
            legTmp.text = $"<color=#FF4D4D>■ Tanker ×{nt}</color>   <color=#8840CC>■ Basic ×{nb}</color>   <color=#F2D933>■ Swift ×{ns}</color>   |   총 {deck.spawns.Count}   |   {totalTime:0}초   |   DEFEAT: {deck.defeatGoalReachedCount}마리 도달";
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
        }
    }
}
