using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Wassup.Core;
using Wassup.UI.Layout;

namespace Wassup.UI
{
    // dreamcatcher-awakening-hand unit 5 — bottom-right awakening gauge: a
    // button-shaped readout of the current awakening value. Clicking it emits
    // Toggled; the hand view (unit 6) owns the actual strip↔hand flip state —
    // this view holds no toggle state of its own.
    //
    // Runtime-built canvas (DefenderSelector/NextWaveDock pattern, zero prefabs).
    // Sits directly ABOVE the NextWaveDock block (dock: -40,40 size 250x150).
    // Shown alongside the placement strip (Placement entry → Result), matching
    // spec unit 5 §6 — visible in Placement too; gaugeStart is the natural gate.
    public class AwakeningGaugeView : MonoBehaviour
    {
        [SerializeField] private DreamcatcherHandController handController;
        // In-battle Korean label font (Jua) + number font (Anton) — battle-ui-korean convention.
        [SerializeField] private TMP_FontAsset labelFont;
        [SerializeField] private TMP_FontAsset numberFont;
        [SerializeField] private Color backingColor = new Color(0.07f, 0.05f, 0.14f, 0.88f);
        [SerializeField] private Color fillColor = new Color(0.62f, 0.4f, 1f, 0.95f);
        [SerializeField] private float valuePunchScale = 1.18f;

        public event System.Action Toggled;

        private GameObject _panel;
        private TextMeshProUGUI _valueLabel;
        private Image _fill;
        private bool _built;
        private int _lastShown = -1;
        private Coroutine _punch;

        private void Awake()
        {
            BuildCanvas();
            if (_panel != null) _panel.SetActive(false);
        }

        private void OnEnable()
        {
            if (handController != null)
                handController.GaugeChanged += OnGaugeChanged;
            // gift-phase unit 3 — 노출을 PhaseChanged(Placement) 로 이관. 예전 PlacementRequested
            // 는 이제 선물 페이즈 시작점이라 게이지가 선물 도중 튀어나온다.
            if (GameManager.Instance != null)
                GameManager.Instance.PhaseChanged += OnPhaseChanged;
        }

        private void OnDisable()
        {
            if (handController != null)
                handController.GaugeChanged -= OnGaugeChanged;
            if (GameManager.Instance != null)
                GameManager.Instance.PhaseChanged -= OnPhaseChanged;
        }

        private void OnPhaseChanged(GamePhase phase)
        {
            if (_panel == null) return;
            if (phase == GamePhase.Placement)
            {
                // 선물 종료 후 배치 진입에서 노출.
                _panel.SetActive(true);
                Refresh(handController != null ? handController.Gauge : 0, punch: false);
            }
            else if (phase != GamePhase.Battle)
            {
                // Battle 은 유지(배치에서 켜진 상태), 그 외(None/Draft/Gift/Result)는 숨김.
                _panel.SetActive(false);
            }
        }

        private void OnGaugeChanged(int value) => Refresh(value, punch: true);

        private void Refresh(int value, bool punch)
        {
            if (_valueLabel == null) return;
            int max = handController != null ? handController.GaugeMax : 100;
            _valueLabel.text = value.ToString();
            if (_fill != null) _fill.fillAmount = max > 0 ? (float)value / max : 0f;
            if (punch && value != _lastShown && _panel != null && _panel.activeInHierarchy)
            {
                if (_punch != null) StopCoroutine(_punch);
                _punch = StartCoroutine(PunchValue());
            }
            _lastShown = value;
        }

        private System.Collections.IEnumerator PunchValue()
        {
            var rt = _valueLabel.rectTransform;
            const float dur = 0.16f;
            float t = 0f;
            while (t < dur)
            {
                t += Time.unscaledDeltaTime; // UI must not slow under battle slomo
                float k = 1f - Mathf.Abs(2f * Mathf.Clamp01(t / dur) - 1f); // up then down
                rt.localScale = Vector3.one * Mathf.Lerp(1f, valuePunchScale, k);
                yield return null;
            }
            rt.localScale = Vector3.one;
            _punch = null;
        }

        private void BuildCanvas()
        {
            if (_built) return;
            _built = true;

            var roots = UiCanvasSetup.Ensure(gameObject, sortingOrder: 7);

            // Bottom-right, stacked above the NextWaveDock block (dock top ≈ y 200).
            _panel = new GameObject("AwakeningPanel", typeof(RectTransform), typeof(Image), typeof(Button));
            _panel.transform.SetParent(roots.SafeAreaRoot, false);
            var prt = (RectTransform)_panel.transform;
            prt.anchorMin = new Vector2(1f, 0f);
            prt.anchorMax = new Vector2(1f, 0f);
            prt.pivot = new Vector2(1f, 0f);
            prt.anchoredPosition = new Vector2(-40f, 220f);
            prt.sizeDelta = new Vector2(250f, 96f);

            var backing = _panel.GetComponent<Image>();
            backing.color = backingColor;
            var button = _panel.GetComponent<Button>();
            button.onClick.AddListener(() => Toggled?.Invoke());

            // Radial-less horizontal fill bar along the bottom edge.
            var fillBg = new GameObject("FillBg", typeof(RectTransform), typeof(Image));
            fillBg.transform.SetParent(_panel.transform, false);
            var fbrt = (RectTransform)fillBg.transform;
            fbrt.anchorMin = new Vector2(0f, 0f);
            fbrt.anchorMax = new Vector2(1f, 0f);
            fbrt.pivot = new Vector2(0.5f, 0f);
            fbrt.offsetMin = new Vector2(8f, 8f);
            fbrt.offsetMax = new Vector2(-8f, 20f);
            var fbimg = fillBg.GetComponent<Image>();
            fbimg.color = new Color(1f, 1f, 1f, 0.12f);
            fbimg.raycastTarget = false;

            var fillGO = new GameObject("Fill", typeof(RectTransform), typeof(Image));
            fillGO.transform.SetParent(fillBg.transform, false);
            var frt = (RectTransform)fillGO.transform;
            frt.anchorMin = Vector2.zero;
            frt.anchorMax = Vector2.one;
            frt.offsetMin = Vector2.zero;
            frt.offsetMax = Vector2.zero;
            _fill = fillGO.GetComponent<Image>();
            _fill.color = fillColor;
            _fill.raycastTarget = false;
            _fill.type = Image.Type.Filled;
            _fill.fillMethod = Image.FillMethod.Horizontal;
            _fill.fillAmount = 0f;

            // "각성" label (top-left) — in-match vocabulary, no "카드/코스트" terms.
            var labelGO = new GameObject("Label", typeof(RectTransform));
            labelGO.transform.SetParent(_panel.transform, false);
            var lrt = (RectTransform)labelGO.transform;
            lrt.anchorMin = new Vector2(0f, 1f);
            lrt.anchorMax = new Vector2(0f, 1f);
            lrt.pivot = new Vector2(0f, 1f);
            lrt.anchoredPosition = new Vector2(12f, -8f);
            lrt.sizeDelta = new Vector2(110f, 34f);
            var label = labelGO.AddComponent<TextMeshProUGUI>();
            if (labelFont != null) label.font = labelFont;
            label.text = "각성";
            label.fontSize = 26;
            label.color = new Color(1f, 1f, 1f, 0.85f);
            label.alignment = TextAlignmentOptions.TopLeft;
            label.raycastTarget = false;

            // Big number (right side).
            var valueGO = new GameObject("Value", typeof(RectTransform));
            valueGO.transform.SetParent(_panel.transform, false);
            var vrt = (RectTransform)valueGO.transform;
            vrt.anchorMin = new Vector2(0f, 0f);
            vrt.anchorMax = new Vector2(1f, 1f);
            vrt.offsetMin = new Vector2(8f, 20f);
            vrt.offsetMax = new Vector2(-12f, -6f);
            _valueLabel = valueGO.AddComponent<TextMeshProUGUI>();
            if (numberFont != null) _valueLabel.font = numberFont;
            _valueLabel.text = "0";
            _valueLabel.fontSize = 46;
            _valueLabel.color = Color.white;
            _valueLabel.alignment = TextAlignmentOptions.MidlineRight;
            _valueLabel.raycastTarget = false;

            UiLayer.Apply(gameObject);
        }
    }
}
