using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Wassup.Core;

namespace Wassup.UI
{
    // Bottom-left cost readout. Renders one segment per integer cost so the
    // player can read their pool at a glance, with the in-progress segment
    // filling smoothly as regen approaches the next integer. Hidden outside
    // Placement / Battle phases (no regen data to show during draft/briefing).
    public class CostDisplay : MonoBehaviour
    {
        [SerializeField] private GameManager gameManager;

        private static readonly Color FilledColor = new(0.35f, 0.9f, 0.4f, 1f);
        private static readonly Color EmptyColor = new(0.18f, 0.18f, 0.2f, 1f);

        private GameObject _panel;
        private Transform _segmentRow;
        private TextMeshProUGUI _label;
        private Image[] _segments;
        private int _segmentCount;
        private bool _built;

        private void Awake()
        {
            BuildCanvas();
            if (_panel != null) _panel.SetActive(false);
        }

        private void OnEnable()
        {
            if (gameManager != null) gameManager.PhaseChanged += OnPhaseChanged;
        }

        private void OnDisable()
        {
            if (gameManager != null) gameManager.PhaseChanged -= OnPhaseChanged;
        }

        private void OnPhaseChanged(GamePhase phase)
        {
            bool show = phase == GamePhase.Placement || phase == GamePhase.Battle;
            if (_panel == null) return;
            if (show) EnsureSegments();
            _panel.SetActive(show);
        }

        private void EnsureSegments()
        {
            if (!_built) BuildCanvas();
            if (_segmentRow == null) return;
            if (gameManager == null || gameManager.CostRuntime == null) return;
            int max = Mathf.RoundToInt(gameManager.CostRuntime.Max);
            if (max <= 0) return;
            if (max == _segmentCount && _segments != null) return;

            for (int i = _segmentRow.childCount - 1; i >= 0; i--)
                Destroy(_segmentRow.GetChild(i).gameObject);

            _segments = new Image[max];
            for (int i = 0; i < max; i++)
            {
                var seg = new GameObject($"Seg{i}", typeof(RectTransform), typeof(Image));
                seg.transform.SetParent(_segmentRow, false);

                // Inner fill uses a child so we can keep an outline + tint the fill only.
                var fillGO = new GameObject("Fill", typeof(RectTransform), typeof(Image));
                fillGO.transform.SetParent(seg.transform, false);
                var frt = (RectTransform)fillGO.transform;
                frt.anchorMin = new Vector2(0f, 0f);
                frt.anchorMax = new Vector2(1f, 1f);
                frt.pivot = new Vector2(0.5f, 0f);
                frt.offsetMin = new Vector2(2f, 2f);
                frt.offsetMax = new Vector2(-2f, -2f);
                frt.localScale = new Vector3(1f, 0f, 1f);
                var fillImg = fillGO.GetComponent<Image>();
                fillImg.color = FilledColor;

                seg.GetComponent<Image>().color = EmptyColor;
                _segments[i] = fillImg;
            }
            _segmentCount = max;

            UiLayer.Apply(gameObject);
        }

        private void Update()
        {
            if (_panel == null || !_panel.activeSelf) return;
            if (gameManager == null || gameManager.CostRuntime == null) return;
            var rt = gameManager.CostRuntime;
            EnsureSegments();

            float current = rt.Current;
            for (int i = 0; i < _segments.Length; i++)
            {
                float fill = Mathf.Clamp01(current - i);
                _segments[i].rectTransform.localScale = new Vector3(1f, fill, 1f);
            }

            _label.text = $"{rt.CurrentInt} / {Mathf.RoundToInt(rt.Max)}";
        }

        private void BuildCanvas()
        {
            if (_built) return;
            _built = true;

            var canvas = gameObject.GetComponent<Canvas>();
            if (canvas == null) canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 5;
            if (gameObject.GetComponent<CanvasScaler>() == null)
            {
                var scaler = gameObject.AddComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920, 1080);
            }
            if (gameObject.GetComponent<GraphicRaycaster>() == null)
                gameObject.AddComponent<GraphicRaycaster>();

            _panel = new GameObject("CostPanel", typeof(RectTransform), typeof(Image));
            _panel.transform.SetParent(transform, false);
            var prt = (RectTransform)_panel.transform;
            prt.anchorMin = new Vector2(0f, 0f);
            prt.anchorMax = new Vector2(0f, 0f);
            prt.pivot = new Vector2(0f, 0f);
            prt.anchoredPosition = new Vector2(40f, 160f); // above DefenderSelector
            prt.sizeDelta = new Vector2(640f, 90f);
            _panel.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.72f);

            // Label (inline "N / Max")
            var labelGO = new GameObject("Label", typeof(RectTransform));
            labelGO.transform.SetParent(_panel.transform, false);
            var lrt = (RectTransform)labelGO.transform;
            lrt.anchorMin = new Vector2(0f, 1f);
            lrt.anchorMax = new Vector2(1f, 1f);
            lrt.pivot = new Vector2(0f, 1f);
            lrt.anchoredPosition = new Vector2(12f, -6f);
            lrt.sizeDelta = new Vector2(160f, 30f);
            _label = labelGO.AddComponent<TextMeshProUGUI>();
            _label.text = "0 / 0";
            _label.fontSize = 22;
            _label.color = Color.white;
            _label.alignment = TextAlignmentOptions.Left;

            // Segment row
            var rowGO = new GameObject("SegmentRow", typeof(RectTransform), typeof(HorizontalLayoutGroup));
            rowGO.transform.SetParent(_panel.transform, false);
            var rrt = (RectTransform)rowGO.transform;
            rrt.anchorMin = new Vector2(0f, 0f);
            rrt.anchorMax = new Vector2(1f, 1f);
            rrt.offsetMin = new Vector2(12f, 10f);
            rrt.offsetMax = new Vector2(-12f, -36f);
            var hlg = rowGO.GetComponent<HorizontalLayoutGroup>();
            hlg.spacing = 4f;
            hlg.childForceExpandWidth = true;
            hlg.childForceExpandHeight = true;
            _segmentRow = rowGO.transform;

            UiLayer.Apply(gameObject);
        }
    }
}
