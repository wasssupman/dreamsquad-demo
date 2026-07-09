using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Wassup.Core;

namespace Wassup.UI
{
    // Bottom-left cost readout. Compact energy badge (ingame-ui-upgrade unit 2, rev
    // 2026-07-09b): a casual HUD panel with an energy bolt icon, a big current value +
    // small "/max", and a short bar gauge (one segment per integer of the pool; the
    // leading segment fills bottom→top as regen approaches the next integer). Art kit
    // (panel / bolt / bar filled+empty) is authored; each sprite has a procedural
    // fallback so the badge never breaks when a slot is empty. Hidden outside
    // Placement / Battle phases. Pure presentation — reads CostRuntime, never mutates.
    public class CostDisplay : MonoBehaviour
    {
        [SerializeField] private GameManager gameManager;

        [Header("Cost art (미할당 시 절차 폴백)")]
        [Tooltip("HUD 패널 배경")]
        [SerializeField] private Sprite costPanelSprite;
        [Tooltip("에너지 아이콘(라이트닝 볼트)")]
        [SerializeField] private Sprite costEnergyIcon;
        [Tooltip("게이지 한 칸(채움) 바")]
        [SerializeField] private Sprite costBarFilled;
        [Tooltip("게이지 한 칸(빈칸) 바")]
        [SerializeField] private Sprite costBarEmpty;
        [Tooltip("큰 숫자용 폰트(Jua/Anton 권장). 미지정 시 TMP 기본 볼드")]
        [SerializeField] private TMP_FontAsset numberFont;

        // Fallback palette (used only when the matching sprite is missing).
        private static readonly Color PlateColor = new(0.07f, 0.09f, 0.13f, 0.92f);
        private static readonly Color PlateBorder = new(0.28f, 0.55f, 0.42f, 0.95f);
        private static readonly Color BarFilled = new(1f, 0.82f, 0.22f, 1f);
        private static readonly Color BarEmpty = new(0.20f, 0.21f, 0.26f, 1f);
        private static readonly Color IconFallback = new(1f, 0.78f, 0.24f, 1f);
        private static readonly Color ValueColor = Color.white;

        private const float PlateW = 363f;
        private const float PlateH = 130f;

        private GameObject _panel;
        private Transform _barRow;
        private TextMeshProUGUI _valueText;
        private Image[] _bars;      // per-integer leading-fill overlays (vertical)
        private int _barCount;
        private Sprite _barEmptyFallback;
        private Sprite _barFilledFallback;
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
            if (show) EnsureBars();
            _panel.SetActive(show);
        }

        private void EnsureBars()
        {
            if (!_built) BuildCanvas();
            if (_barRow == null) return;
            if (gameManager == null || gameManager.CostRuntime == null) return;
            int max = Mathf.RoundToInt(gameManager.CostRuntime.Max);
            if (max <= 0) return;
            if (max == _barCount && _bars != null) return;

            for (int i = _barRow.childCount - 1; i >= 0; i--)
                Destroy(_barRow.GetChild(i).gameObject);

            var emptySprite = costBarEmpty != null ? costBarEmpty : _barEmptyFallback;
            var filledSprite = costBarFilled != null ? costBarFilled : _barFilledFallback;

            _bars = new Image[max];
            for (int i = 0; i < max; i++)
            {
                var bar = new GameObject($"Bar{i}", typeof(RectTransform), typeof(Image));
                bar.transform.SetParent(_barRow, false);
                var bg = bar.GetComponent<Image>();
                bg.sprite = emptySprite;
                bg.preserveAspect = true;
                bg.raycastTarget = false;

                var fillGO = new GameObject("Fill", typeof(RectTransform), typeof(Image));
                fillGO.transform.SetParent(bar.transform, false);
                var frt = (RectTransform)fillGO.transform;
                frt.anchorMin = Vector2.zero; frt.anchorMax = Vector2.one;
                frt.offsetMin = Vector2.zero; frt.offsetMax = Vector2.zero;
                var fill = fillGO.GetComponent<Image>();
                fill.sprite = filledSprite;
                fill.preserveAspect = true;
                fill.type = Image.Type.Filled;
                fill.fillMethod = Image.FillMethod.Vertical;
                fill.fillOrigin = (int)Image.OriginVertical.Bottom;
                fill.fillAmount = 0f;
                fill.raycastTarget = false;

                _bars[i] = fill;
            }
            _barCount = max;

            UiLayer.Apply(gameObject);
        }

        private void Update()
        {
            if (_panel == null || !_panel.activeSelf) return;
            if (gameManager == null || gameManager.CostRuntime == null) return;
            var rt = gameManager.CostRuntime;
            EnsureBars();

            float current = rt.Current;
            if (_bars != null)
                for (int i = 0; i < _bars.Length; i++)
                    _bars[i].fillAmount = Mathf.Clamp01(current - i);

            if (_valueText != null)
                _valueText.text = $"{rt.CurrentInt}<size=52%>/{Mathf.RoundToInt(rt.Max)}</size>";
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

            _barEmptyFallback = UiRoundedSprite.Make(4f, 0f, BarEmpty, BarEmpty);
            _barFilledFallback = UiRoundedSprite.Make(4f, 0f, BarFilled, BarFilled);

            // Compact HUD panel, bottom-left above the DefenderSelector.
            _panel = new GameObject("CostPanel", typeof(RectTransform), typeof(Image));
            _panel.transform.SetParent(transform, false);
            var prt = (RectTransform)_panel.transform;
            prt.anchorMin = new Vector2(0f, 0f);
            prt.anchorMax = new Vector2(0f, 0f);
            prt.pivot = new Vector2(0f, 0f);
            prt.anchoredPosition = new Vector2(40f, 150f);
            prt.sizeDelta = new Vector2(PlateW, PlateH);
            // Landscape badge with even inner padding (Pad). 9-sliced panel so the
            // rounded corners stay crisp when stretched wide. Top row = bolt + inline
            // "N/Max"; bottom row = bar gauge. Compact height, no floating whitespace.
            const float Pad = 18f;
            const float TopRowH = 50f;
            const float BarRowH = 34f;
            var plate = _panel.GetComponent<Image>();
            plate.sprite = costPanelSprite != null
                ? costPanelSprite
                : UiRoundedSprite.Make(20f, 3f, PlateColor, PlateBorder);
            plate.type = Image.Type.Sliced;
            plate.raycastTarget = false;

            // Energy bolt (top-left).
            const float iconSize = 44f;
            var iconGO = new GameObject("EnergyIcon", typeof(RectTransform), typeof(Image));
            iconGO.transform.SetParent(_panel.transform, false);
            var irt = (RectTransform)iconGO.transform;
            irt.anchorMin = new Vector2(0f, 1f);
            irt.anchorMax = new Vector2(0f, 1f);
            irt.pivot = new Vector2(0f, 1f);
            irt.anchoredPosition = new Vector2(Pad, -(Pad + (TopRowH - iconSize) * 0.5f));
            irt.sizeDelta = new Vector2(iconSize, iconSize);
            var iconImg = iconGO.GetComponent<Image>();
            iconImg.sprite = costEnergyIcon != null ? costEnergyIcon
                : UiRoundedSprite.Make(12f, 0f, IconFallback, IconFallback);
            iconImg.preserveAspect = true;
            iconImg.raycastTarget = false;

            // Big current value + inline "/max" (top row, vertically centered on bolt).
            float valueX = Pad + iconSize + 14f;
            _valueText = MakeText("Value", 40f, ValueColor, TextAlignmentOptions.MidlineLeft);
            _valueText.richText = true;
            var vrt = _valueText.rectTransform;
            vrt.anchorMin = new Vector2(0f, 1f);
            vrt.anchorMax = new Vector2(0f, 1f);
            vrt.pivot = new Vector2(0f, 1f);
            vrt.anchoredPosition = new Vector2(valueX, -Pad);
            vrt.sizeDelta = new Vector2(PlateW - valueX - Pad, TopRowH);
            _valueText.fontStyle = FontStyles.Bold;
            _valueText.text = "0";

            // Bar gauge along the bottom, inset by Pad on left/right/bottom.
            var rowGO = new GameObject("BarRow", typeof(RectTransform), typeof(HorizontalLayoutGroup));
            rowGO.transform.SetParent(_panel.transform, false);
            var rrt = (RectTransform)rowGO.transform;
            rrt.anchorMin = new Vector2(0f, 0f);
            rrt.anchorMax = new Vector2(1f, 0f);
            rrt.pivot = new Vector2(0.5f, 0f);
            rrt.offsetMin = new Vector2(Pad, Pad);
            rrt.offsetMax = new Vector2(-Pad, Pad + BarRowH);
            var hlg = rowGO.GetComponent<HorizontalLayoutGroup>();
            hlg.spacing = 4f;
            hlg.childForceExpandWidth = true;
            hlg.childForceExpandHeight = true;
            hlg.childControlWidth = true;
            hlg.childControlHeight = true;
            hlg.childAlignment = TextAnchor.MiddleCenter;
            _barRow = rowGO.transform;

            UiLayer.Apply(gameObject);
        }

        private TextMeshProUGUI MakeText(string name, float size, Color color, TextAlignmentOptions align)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(_panel.transform, false);
            var tmp = go.AddComponent<TextMeshProUGUI>();
            if (numberFont != null) tmp.font = numberFont;
            tmp.fontSize = size;
            tmp.color = color;
            tmp.alignment = align;
            tmp.textWrappingMode = TextWrappingModes.NoWrap;
            tmp.raycastTarget = false;
            return tmp;
        }
    }
}
