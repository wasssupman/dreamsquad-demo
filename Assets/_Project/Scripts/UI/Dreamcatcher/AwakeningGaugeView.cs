using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Serialization;
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

        [Header("Casual Burst Button")]
        [FormerlySerializedAs("dreamOrbFrameSprite")]
        [SerializeField] private Sprite burstFrameSprite;
        [SerializeField] private Color backingColor = new Color(0.07f, 0.05f, 0.14f, 0.94f);
        [SerializeField] private Color fillColor = new Color(0.7f, 0.43f, 1f, 1f);
        [SerializeField] private Color chargedColor = new Color(0.43f, 0.92f, 1f, 1f);
        [SerializeField] private Color haloColor = new Color(0.56f, 0.28f, 1f, 0.5f);
        [SerializeField] private Color maxColor = new Color(1f, 0.77f, 0.12f, 1f);
        [SerializeField] private Color dormantFrameColor = new Color(0.68f, 0.65f, 0.76f, 0.78f);
        [SerializeField] private float valuePunchScale = 1.18f;

        public event System.Action Toggled;

        private GameObject _panel;
        private RectTransform _visualRoot;
        private TextMeshProUGUI _valueLabel;
        private TextMeshProUGUI _actionLabel;
        private TextMeshProUGUI _gainLabel;
        private Image _halo;
        private Image _frame;
        private Image _chargeLiquid;
        private Image _liquidSurface;
        private bool _built;
        private bool _open;
        private bool _maxReady;
        private int _lastShown = -1;
        private float _normalized;
        private Coroutine _punch;
        private Coroutine _pulse;
        private Coroutine _gain;
        private Coroutine _gainBounce;
        private Coroutine _maxBurst;
        private Coroutine _maxIdle;
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
            StopMaxIdle();
        }

        private void OnPhaseChanged(GamePhase phase)
        {
            if (_panel == null) return;
            if (phase == GamePhase.Battle)
            {
                _panel.SetActive(true);
                Refresh(handController != null ? handController.Gauge : 0, punch: false);
                if (_maxReady && _maxBurst == null && _maxIdle == null)
                    _maxIdle = StartCoroutine(MaxIdleRoutine());
            }
            else
            {
                StopMaxIdle();
                _panel.SetActive(false);
            }
        }

        private void OnGaugeChanged(int value) => Refresh(value, punch: true);

        private void Refresh(int value, bool punch)
        {
            if (_valueLabel == null) return;
            int max = handController != null ? handController.GaugeMax : 100;
            _valueLabel.text = value.ToString();
            float previousNormalized = _normalized;
            _normalized = max > 0 ? Mathf.Clamp01((float)value / max) : 0f;
            if (_chargeLiquid != null)
            {
                _chargeLiquid.fillAmount = _normalized;
                _chargeLiquid.color = Color.Lerp(fillColor, chargedColor, _normalized);
            }
            if (_liquidSurface != null)
            {
                var surfaceRect = _liquidSurface.rectTransform;
                surfaceRect.anchoredPosition = new Vector2(0f, Mathf.Lerp(-58f, 58f, _normalized));
                _liquidSurface.gameObject.SetActive(_normalized > 0.01f && _normalized < 0.99f);
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
                    if (_gainBounce != null) StopCoroutine(_gainBounce);
                    _gainBounce = StartCoroutine(GainBounceRoutine());
                }
            }
            if (_normalized >= 0.999f && previousNormalized < 0.999f && _panel.activeInHierarchy)
            {
                if (_maxBurst != null) StopCoroutine(_maxBurst);
                _maxBurst = StartCoroutine(MaxReadyRoutine());
            }
            else if (_normalized < 0.999f && previousNormalized >= 0.999f)
            {
                StopMaxIdle();
            }
            _lastShown = value;
            UpdateVisualState();
        }

        private void UpdateVisualState()
        {
            bool dormant = _normalized <= 0.001f && !_open;
            _maxReady = _normalized >= 0.999f;
            if (_frame != null)
                _frame.color = dormant ? dormantFrameColor : Color.white;
            if (_halo != null)
            {
                var c = _maxReady ? maxColor : haloColor;
                c.a *= dormant ? 0f : (_maxReady || _open ? 1f : Mathf.Lerp(0.16f, 0.65f, _normalized));
                _halo.color = c;
            }
            if (_actionLabel != null)
            {
                _actionLabel.text = _maxReady ? "MAX!" : "/100";
                _actionLabel.color = dormant
                    ? new Color(0.67f, 0.64f, 0.75f, 0.9f)
                    : (_maxReady ? maxColor : (_open ? chargedColor : Color.white));
            }
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

        private IEnumerator GainBounceRoutine()
        {
            if (_visualRoot == null) yield break;
            const float duration = 0.24f;
            float time = 0f;
            while (time < duration)
            {
                time += Time.unscaledDeltaTime;
                float k = Mathf.Clamp01(time / duration);
                Vector3 scale;
                if (k < 0.38f)
                    scale = Vector3.Lerp(Vector3.one, new Vector3(1.1f, 0.91f, 1f), k / 0.38f);
                else if (k < 0.72f)
                    scale = Vector3.Lerp(new Vector3(1.1f, 0.91f, 1f), new Vector3(0.96f, 1.07f, 1f), (k - 0.38f) / 0.34f);
                else
                    scale = Vector3.Lerp(new Vector3(0.96f, 1.07f, 1f), Vector3.one, (k - 0.72f) / 0.28f);
                _visualRoot.localScale = scale;
                yield return null;
            }
            _visualRoot.localScale = Vector3.one;
            _gainBounce = null;
        }

        private IEnumerator MaxReadyRoutine()
        {
            StopMaxIdle();
            if (_visualRoot == null) yield break;
            const float duration = 0.42f;
            float time = 0f;
            while (time < duration)
            {
                time += Time.unscaledDeltaTime;
                float k = Mathf.Clamp01(time / duration);
                float scale = k < 0.34f
                    ? Mathf.Lerp(1f, 1.2f, k / 0.34f)
                    : (k < 0.68f
                        ? Mathf.Lerp(1.2f, 0.94f, (k - 0.34f) / 0.34f)
                        : Mathf.Lerp(0.94f, 1f, (k - 0.68f) / 0.32f));
                _visualRoot.localScale = Vector3.one * scale;
                if (_halo != null) _halo.color = Color.Lerp(maxColor, Color.white, Mathf.Sin(k * Mathf.PI));
                yield return null;
            }
            _visualRoot.localScale = Vector3.one;
            UpdateVisualState();
            _maxBurst = null;
            if (_maxReady && _panel.activeInHierarchy)
                _maxIdle = StartCoroutine(MaxIdleRoutine());
        }

        private IEnumerator MaxIdleRoutine()
        {
            while (_maxReady && _panel != null && _panel.activeInHierarchy)
            {
                float wait = 0f;
                while (wait < 1.15f && _maxReady)
                {
                    wait += Time.unscaledDeltaTime;
                    yield return null;
                }
                const float duration = 0.28f;
                float time = 0f;
                while (time < duration && _maxReady)
                {
                    time += Time.unscaledDeltaTime;
                    float k = Mathf.Sin(Mathf.Clamp01(time / duration) * Mathf.PI);
                    _visualRoot.localScale = Vector3.one * Mathf.Lerp(1f, 1.055f, k);
                    yield return null;
                }
                _visualRoot.localScale = Vector3.one;
            }
            _maxIdle = null;
        }

        private void StopMaxIdle()
        {
            if (_maxIdle != null)
            {
                StopCoroutine(_maxIdle);
                _maxIdle = null;
            }
            if (_visualRoot != null) _visualRoot.localScale = Vector3.one;
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

            var visualGO = new GameObject("BurstVisual", typeof(RectTransform));
            visualGO.transform.SetParent(_panel.transform, false);
            _visualRoot = (RectTransform)visualGO.transform;
            _visualRoot.anchorMin = _visualRoot.anchorMax = new Vector2(0.5f, 0.5f);
            _visualRoot.sizeDelta = new Vector2(220f, 220f);

            var haloGO = CreateImage("ChargeGlow", _visualRoot, Vector2.zero, new Vector2(196f, 196f));
            _halo = haloGO.GetComponent<Image>();
            _halo.sprite = UiRoundedSprite.MakeCircle(96, Color.white);
            _halo.color = Color.clear;

            // The center face is the gauge: a clipped liquid disc rises from 0 to 100.
            var wellGO = new GameObject("ChargeWell", typeof(RectTransform), typeof(Image), typeof(Mask));
            wellGO.transform.SetParent(_visualRoot, false);
            var wellRect = (RectTransform)wellGO.transform;
            wellRect.anchorMin = wellRect.anchorMax = new Vector2(0.5f, 0.5f);
            wellRect.anchoredPosition = Vector2.zero;
            wellRect.sizeDelta = new Vector2(132f, 132f);
            var well = wellGO.GetComponent<Image>();
            well.sprite = UiRoundedSprite.MakeCircle(112, Color.white);
            well.color = backingColor;
            well.raycastTarget = false;
            wellGO.GetComponent<Mask>().showMaskGraphic = true;

            var liquidGO = CreateImage("ChargeLiquid", wellGO.transform, Vector2.zero, new Vector2(132f, 132f));
            _chargeLiquid = liquidGO.GetComponent<Image>();
            _chargeLiquid.sprite = well.sprite;
            _chargeLiquid.type = Image.Type.Filled;
            _chargeLiquid.fillMethod = Image.FillMethod.Vertical;
            _chargeLiquid.fillOrigin = (int)Image.OriginVertical.Bottom;
            _chargeLiquid.fillAmount = 0f;
            _chargeLiquid.color = fillColor;

            var surfaceGO = CreateImage("LiquidSurface", wellGO.transform, new Vector2(0f, -58f), new Vector2(108f, 13f));
            _liquidSurface = surfaceGO.GetComponent<Image>();
            _liquidSurface.sprite = UiRoundedSprite.MakeCircle(64, Color.white);
            _liquidSurface.color = new Color(0.7f, 0.96f, 1f, 0.82f);
            surfaceGO.SetActive(false);

            // Generated frame is intentionally symbol-free: only chunky jelly color blocks.
            var frameGO = CreateImage("BurstFrame", _visualRoot, Vector2.zero, new Vector2(220f, 220f));
            _frame = frameGO.GetComponent<Image>();
            _frame.sprite = burstFrameSprite != null
                ? burstFrameSprite
                : UiRoundedSprite.MakeCircle(128, backingColor, 7f, new Color(1f, 0.72f, 0.25f, 1f));
            _frame.preserveAspect = true;
            button.targetGraphic = _frame;

            var valueGO = new GameObject("Value", typeof(RectTransform));
            valueGO.transform.SetParent(_visualRoot, false);
            var valueRect = (RectTransform)valueGO.transform;
            valueRect.anchorMin = valueRect.anchorMax = new Vector2(0.5f, 0.5f);
            valueRect.anchoredPosition = new Vector2(0f, 10f);
            valueRect.sizeDelta = new Vector2(144f, 82f);
            _valueLabel = valueGO.AddComponent<TextMeshProUGUI>();
            if (numberFont != null) _valueLabel.font = numberFont;
            _valueLabel.text = "0";
            _valueLabel.fontSize = 76f;
            _valueLabel.fontStyle = FontStyles.Bold;
            _valueLabel.color = Color.white;
            _valueLabel.alignment = TextAlignmentOptions.Center;
            _valueLabel.raycastTarget = false;
            ApplyNumberOutline(_valueLabel);

            var actionGO = new GameObject("Action", typeof(RectTransform));
            actionGO.transform.SetParent(_visualRoot, false);
            var actionRect = (RectTransform)actionGO.transform;
            actionRect.anchorMin = actionRect.anchorMax = new Vector2(0.5f, 0.5f);
            actionRect.anchoredPosition = new Vector2(0f, -43f);
            actionRect.sizeDelta = new Vector2(132f, 28f);
            _actionLabel = actionGO.AddComponent<TextMeshProUGUI>();
            if (labelFont != null) _actionLabel.font = labelFont;
            _actionLabel.text = "/100";
            _actionLabel.fontSize = 20f;
            _actionLabel.fontStyle = FontStyles.Bold;
            _actionLabel.color = Color.white;
            _actionLabel.alignment = TextAlignmentOptions.Center;
            _actionLabel.raycastTarget = false;

            var gainGO = new GameObject("GainDelta", typeof(RectTransform));
            gainGO.transform.SetParent(_visualRoot, false);
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
