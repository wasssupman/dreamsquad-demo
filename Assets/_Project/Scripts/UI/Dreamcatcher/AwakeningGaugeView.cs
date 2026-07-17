using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Wassup.Core;
using Wassup.UI.Layout;

namespace Wassup.UI
{
    // awakening-hud-resource-button — bottom-right Dreamcatcher resource action.
    // The exact reserve is the primary read; a continuous ring is only the ratio cue.
    // Tapping emits Toggled while DreamcatcherHandView owns the actual hand state.
    public class AwakeningGaugeView : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
    {
        [SerializeField] private DreamcatcherHandController handController;
        [SerializeField] private TMP_FontAsset labelFont;
        [SerializeField] private TMP_FontAsset numberFont;

        [Header("Casual Dream Orb")]
        [SerializeField] private Sprite dreamOrbFrameSprite;
        [SerializeField] private Color backingColor = new Color(0.07f, 0.05f, 0.14f, 0.94f);
        [SerializeField] private Color fillColor = new Color(0.7f, 0.43f, 1f, 1f);
        [SerializeField] private Color chargedColor = new Color(0.43f, 0.92f, 1f, 1f);
        [SerializeField] private Color haloColor = new Color(0.56f, 0.28f, 1f, 0.5f);
        [SerializeField] private Color dormantFrameColor = new Color(0.68f, 0.65f, 0.76f, 0.78f);
        [SerializeField] private float valuePunchScale = 1.18f;

        public event System.Action Toggled;

        private GameObject _panel;
        private TextMeshProUGUI _valueLabel;
        private TextMeshProUGUI _actionLabel;
        private TextMeshProUGUI _gainLabel;
        private Image _halo;
        private Image _frame;
        private Image _chargeArc;
        private bool _built;
        private bool _open;
        private int _lastShown = -1;
        private float _normalized;
        private Coroutine _punch;
        private Coroutine _pulse;
        private Coroutine _gain;
        private Coroutine _pressRelease;

        public void Pulse()
        {
            if (_panel == null || !_panel.activeInHierarchy) return;
            if (_pulse != null) StopCoroutine(_pulse);
            _pulse = StartCoroutine(PulseRoutine());
        }

        // HandView calls this at the same state boundary that owns slomo/strip switching.
        public void SetOpen(bool open)
        {
            _open = open;
            UpdateVisualState();
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (_panel == null) return;
            if (_pressRelease != null) StopCoroutine(_pressRelease);
            _panel.transform.localScale = Vector3.one * 0.95f;
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            if (_panel == null) return;
            if (_pressRelease != null) StopCoroutine(_pressRelease);
            _pressRelease = StartCoroutine(PressReleaseRoutine());
        }

        private void Awake()
        {
            BuildCanvas();
            if (_panel != null) _panel.SetActive(false);
        }

        private void OnEnable()
        {
            if (handController != null)
                handController.GaugeChanged += OnGaugeChanged;
            if (GameManager.Instance != null)
                GameManager.Instance.PhaseChanged += OnPhaseChanged;
        }

        private void OnDisable()
        {
            if (handController != null)
                handController.GaugeChanged -= OnGaugeChanged;
            if (GameManager.Instance != null)
                GameManager.Instance.PhaseChanged -= OnPhaseChanged;
            if (_panel != null) _panel.transform.localScale = Vector3.one;
        }

        private void OnPhaseChanged(GamePhase phase)
        {
            if (_panel == null) return;
            if (phase == GamePhase.Battle)
            {
                _panel.SetActive(true);
                Refresh(handController != null ? handController.Gauge : 0, punch: false);
            }
            else
            {
                _panel.SetActive(false);
            }
        }

        private void OnGaugeChanged(int value) => Refresh(value, punch: true);

        private void Refresh(int value, bool punch)
        {
            if (_valueLabel == null) return;
            int max = handController != null ? handController.GaugeMax : 100;
            _valueLabel.text = value.ToString();
            _normalized = max > 0 ? Mathf.Clamp01((float)value / max) : 0f;
            if (_chargeArc != null)
            {
                _chargeArc.fillAmount = _normalized;
                _chargeArc.color = Color.Lerp(fillColor, chargedColor, _normalized);
            }

            int delta = _lastShown >= 0 ? value - _lastShown : 0;
            if (punch && value != _lastShown && _panel != null && _panel.activeInHierarchy)
            {
                if (_punch != null) StopCoroutine(_punch);
                _punch = StartCoroutine(PunchValue());
                if (delta > 0)
                {
                    if (_gain != null) StopCoroutine(_gain);
                    _gain = StartCoroutine(ShowGain(delta));
                }
            }
            _lastShown = value;
            UpdateVisualState();
        }

        private void UpdateVisualState()
        {
            bool dormant = _normalized <= 0.001f && !_open;
            if (_frame != null)
                _frame.color = dormant ? dormantFrameColor : Color.white;
            if (_halo != null)
            {
                var c = haloColor;
                c.a *= dormant ? 0f : (_open ? 1f : Mathf.Lerp(0.2f, 0.72f, _normalized));
                _halo.color = c;
            }
            if (_actionLabel != null)
                _actionLabel.color = dormant
                    ? new Color(0.67f, 0.64f, 0.75f, 0.9f)
                    : (_open ? chargedColor : Color.white);
        }

        private IEnumerator PunchValue()
        {
            var rt = _valueLabel.rectTransform;
            const float duration = 0.16f;
            float time = 0f;
            while (time < duration)
            {
                time += Time.unscaledDeltaTime;
                float k = 1f - Mathf.Abs(2f * Mathf.Clamp01(time / duration) - 1f);
                rt.localScale = Vector3.one * Mathf.Lerp(1f, valuePunchScale, k);
                yield return null;
            }
            rt.localScale = Vector3.one;
            _punch = null;
        }

        private IEnumerator ShowGain(int delta)
        {
            if (_gainLabel == null) yield break;
            const float duration = 0.58f;
            var rt = _gainLabel.rectTransform;
            Vector2 start = new Vector2(63f, 26f);
            Vector2 end = new Vector2(63f, 66f);
            _gainLabel.text = $"+{delta}";
            _gainLabel.gameObject.SetActive(true);
            float time = 0f;
            while (time < duration)
            {
                time += Time.unscaledDeltaTime;
                float k = Mathf.Clamp01(time / duration);
                rt.anchoredPosition = Vector2.Lerp(start, end, 1f - (1f - k) * (1f - k));
                var c = chargedColor;
                c.a = 1f - Mathf.Clamp01((k - 0.5f) * 2f);
                _gainLabel.color = c;
                yield return null;
            }
            _gainLabel.gameObject.SetActive(false);
            _gain = null;
        }

        private IEnumerator PulseRoutine()
        {
            var rt = (RectTransform)_panel.transform;
            const float duration = 0.22f;
            float time = 0f;
            Color baseHalo = _halo != null ? _halo.color : default;
            while (time < duration)
            {
                time += Time.unscaledDeltaTime;
                float k = 1f - Mathf.Abs(2f * Mathf.Clamp01(time / duration) - 1f);
                rt.localScale = Vector3.one * Mathf.Lerp(1f, 1.08f, k);
                if (_halo != null) _halo.color = Color.Lerp(baseHalo, Color.white, k * 0.45f);
                yield return null;
            }
            rt.localScale = Vector3.one;
            if (_halo != null) _halo.color = baseHalo;
            _pulse = null;
        }

        private IEnumerator PressReleaseRoutine()
        {
            var rt = (RectTransform)_panel.transform;
            const float duration = 0.13f;
            float time = 0f;
            while (time < duration)
            {
                time += Time.unscaledDeltaTime;
                float k = Mathf.Clamp01(time / duration);
                float scale = k < 0.6f
                    ? Mathf.Lerp(0.95f, 1.035f, k / 0.6f)
                    : Mathf.Lerp(1.035f, 1f, (k - 0.6f) / 0.4f);
                rt.localScale = Vector3.one * scale;
                yield return null;
            }
            rt.localScale = Vector3.one;
            _pressRelease = null;
        }

        private void BuildCanvas()
        {
            if (_built) return;
            _built = true;

            var roots = UiCanvasSetup.Ensure(gameObject, sortingOrder: 7);

            // The 244px hit area stays inside the safe corner; visible art is 220px.
            _panel = new GameObject("AwakeningPanel", typeof(RectTransform), typeof(Image), typeof(Button));
            _panel.transform.SetParent(roots.SafeAreaRoot, false);
            var panelRect = (RectTransform)_panel.transform;
            panelRect.anchorMin = panelRect.anchorMax = new Vector2(1f, 0f);
            panelRect.pivot = new Vector2(1f, 0f);
            panelRect.anchoredPosition = new Vector2(-24f, 20f);
            panelRect.sizeDelta = new Vector2(244f, 244f);

            var hitGraphic = _panel.GetComponent<Image>();
            hitGraphic.color = new Color(1f, 1f, 1f, 0.001f);
            var button = _panel.GetComponent<Button>();
            button.transition = Selectable.Transition.None;
            button.onClick.AddListener(() =>
            {
                SoundManager.Instance?.PlayUiTick();
                Toggled?.Invoke();
            });

            var haloGO = CreateImage("DreamGlow", _panel.transform, new Vector2(0f, 3f), new Vector2(190f, 190f));
            _halo = haloGO.GetComponent<Image>();
            _halo.sprite = UiRoundedSprite.MakeCircle(96, Color.white);
            _halo.color = Color.clear;

            var frameGO = CreateImage("DreamOrbFrame", _panel.transform, Vector2.zero, new Vector2(220f, 220f));
            _frame = frameGO.GetComponent<Image>();
            _frame.sprite = dreamOrbFrameSprite != null
                ? dreamOrbFrameSprite
                : UiRoundedSprite.MakeCircle(128, backingColor, 7f, new Color(1f, 0.72f, 0.25f, 1f));
            _frame.preserveAspect = true;
            button.targetGraphic = _frame;

            // A filled disc plus opaque inner cover forms a continuous radial ring.
            var trackGO = CreateImage("ChargeTrack", _panel.transform, new Vector2(0f, 3f), new Vector2(144f, 144f));
            var track = trackGO.GetComponent<Image>();
            track.sprite = UiRoundedSprite.MakeCircle(112, Color.white);
            track.color = new Color(0.21f, 0.16f, 0.35f, 0.8f);

            var arcGO = CreateImage("ChargeArc", _panel.transform, new Vector2(0f, 3f), new Vector2(144f, 144f));
            _chargeArc = arcGO.GetComponent<Image>();
            _chargeArc.sprite = track.sprite;
            _chargeArc.type = Image.Type.Filled;
            _chargeArc.fillMethod = Image.FillMethod.Radial360;
            _chargeArc.fillOrigin = (int)Image.Origin360.Bottom;
            _chargeArc.fillClockwise = true;
            _chargeArc.fillAmount = 0f;
            _chargeArc.color = fillColor;

            var coreGO = CreateImage("NumberCore", _panel.transform, new Vector2(0f, 3f), new Vector2(124f, 124f));
            var core = coreGO.GetComponent<Image>();
            core.sprite = UiRoundedSprite.MakeCircle(112, Color.white);
            core.color = backingColor;

            var valueGO = new GameObject("Value", typeof(RectTransform));
            valueGO.transform.SetParent(_panel.transform, false);
            var valueRect = (RectTransform)valueGO.transform;
            valueRect.anchorMin = valueRect.anchorMax = new Vector2(0.5f, 0.5f);
            valueRect.anchoredPosition = new Vector2(0f, 13f);
            valueRect.sizeDelta = new Vector2(144f, 82f);
            _valueLabel = valueGO.AddComponent<TextMeshProUGUI>();
            if (numberFont != null) _valueLabel.font = numberFont;
            _valueLabel.text = "0";
            _valueLabel.fontSize = 72f;
            _valueLabel.fontStyle = FontStyles.Bold;
            _valueLabel.color = Color.white;
            _valueLabel.alignment = TextAlignmentOptions.Center;
            _valueLabel.raycastTarget = false;
            ApplyNumberOutline(_valueLabel);

            var actionGO = new GameObject("Action", typeof(RectTransform));
            actionGO.transform.SetParent(_panel.transform, false);
            var actionRect = (RectTransform)actionGO.transform;
            actionRect.anchorMin = actionRect.anchorMax = new Vector2(0.5f, 0.5f);
            actionRect.anchoredPosition = new Vector2(0f, -42f);
            actionRect.sizeDelta = new Vector2(132f, 28f);
            _actionLabel = actionGO.AddComponent<TextMeshProUGUI>();
            if (labelFont != null) _actionLabel.font = labelFont;
            _actionLabel.text = "드림캐쳐";
            _actionLabel.fontSize = 19f;
            _actionLabel.fontStyle = FontStyles.Bold;
            _actionLabel.color = Color.white;
            _actionLabel.alignment = TextAlignmentOptions.Center;
            _actionLabel.raycastTarget = false;

            var gainGO = new GameObject("GainDelta", typeof(RectTransform));
            gainGO.transform.SetParent(_panel.transform, false);
            var gainRect = (RectTransform)gainGO.transform;
            gainRect.anchorMin = gainRect.anchorMax = new Vector2(0.5f, 0.5f);
            gainRect.anchoredPosition = new Vector2(63f, 26f);
            gainRect.sizeDelta = new Vector2(90f, 44f);
            _gainLabel = gainGO.AddComponent<TextMeshProUGUI>();
            if (numberFont != null) _gainLabel.font = numberFont;
            _gainLabel.fontSize = 28f;
            _gainLabel.fontStyle = FontStyles.Bold;
            _gainLabel.alignment = TextAlignmentOptions.Center;
            _gainLabel.raycastTarget = false;
            ApplyNumberOutline(_gainLabel);
            gainGO.SetActive(false);

            UiLayer.Apply(gameObject);
            UpdateVisualState();
        }

        private static GameObject CreateImage(string name, Transform parent, Vector2 position, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var rect = (RectTransform)go.transform;
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            go.GetComponent<Image>().raycastTarget = false;
            return go;
        }

        private static void ApplyNumberOutline(TextMeshProUGUI label)
        {
            if (label.font == null) return;
            var material = label.fontMaterial;
            material.SetColor(ShaderUtilities.ID_OutlineColor, new Color(0.11f, 0.04f, 0.22f, 1f));
            material.SetFloat(ShaderUtilities.ID_OutlineWidth, 0.2f);
            material.SetFloat(ShaderUtilities.ID_UnderlayOffsetX, 0.35f);
            material.SetFloat(ShaderUtilities.ID_UnderlayOffsetY, -0.35f);
            material.SetColor(ShaderUtilities.ID_UnderlayColor, new Color(0.04f, 0.01f, 0.1f, 0.8f));
        }
    }
}
