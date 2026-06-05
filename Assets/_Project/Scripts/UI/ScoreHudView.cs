using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Wassup.Core;

namespace Wassup.UI
{
    // Runtime-built live score, screen top-center just below the timer. Increments
    // per enemy kill (BattleBridge.DrainEnemyKilledEvents -> OnEnemyKilled). Stylish
    // increase: count-up roll + punch scale + color flash, using the Bangers SDF
    // font shared with the damage popups. Display-only — does not feed ResultScreen.
    public class ScoreHudView : MonoBehaviour
    {
        [Header("Style font (Bangers SDF + outline). Null → TMP default.")]
        [SerializeField] private TMP_FontAsset scoreFont;
        [SerializeField] private Material scoreMaterial;

        [Header("Scoring")]
        [Tooltip("적 1처치당 가점")]
        [SerializeField] private int pointsPerKill = 10;

        [Header("Increase feel")]
        [Tooltip("표시 숫자가 목표로 따라붙는 속도(클수록 빠름)")]
        [SerializeField] private float rollLerp = 12f;
        [Tooltip("처치 시 펀치 스케일 배수")]
        [SerializeField] private float punchScale = 1.45f;
        [Tooltip("펀치/플래시 지속(초)")]
        [SerializeField] private float punchDuration = 0.2f;
        [Tooltip("처치 순간 점멸 색")]
        [SerializeField] private Color flashColor = new Color(1f, 0.85f, 0.2f);
        [SerializeField] private Color baseColor = Color.white;

        [Header("Layout")]
        [SerializeField] private float topOffset = -8f;
        [SerializeField] private float valueFontSize = 83f;
        [SerializeField] private float captionFontSize = 29f;

        private GameObject _panel;
        private TextMeshProUGUI _caption;
        private TextMeshProUGUI _value;
        private RectTransform _valueRect;
        private bool _built;
        private bool _subscribed;

        private int _targetScore;
        private float _shownScore;
        private float _punchTimer;

        private void Awake()
        {
            BuildCanvas();
            if (_panel != null) _panel.SetActive(false);
        }

        private void OnDisable()
        {
            Unsubscribe();
        }

        private void Update()
        {
            EnsureSubscribed();

            if (_panel == null || !_panel.activeSelf) return;

            // Count-up roll: ease the shown number toward the target.
            _shownScore = Mathf.Lerp(_shownScore, _targetScore, Mathf.Clamp01(Time.unscaledDeltaTime * rollLerp));
            if (Mathf.Abs(_targetScore - _shownScore) < 0.5f) _shownScore = _targetScore;
            if (_value != null) _value.text = Mathf.CeilToInt(_shownScore).ToString();

            // Punch + flash decay.
            if (_punchTimer > 0f)
            {
                _punchTimer -= Time.unscaledDeltaTime;
                float f = Mathf.Clamp01(_punchTimer / Mathf.Max(0.0001f, punchDuration));
                if (_valueRect != null)
                    _valueRect.localScale = Vector3.one * Mathf.Lerp(1f, Mathf.Max(1f, punchScale), f);
                if (_value != null)
                    _value.color = Color.Lerp(baseColor, flashColor, f);
            }
            else if (_valueRect != null)
            {
                _valueRect.localScale = Vector3.one;
                if (_value != null) _value.color = baseColor;
            }
        }

        // Called by BattleBridge once per enemy kill drained from EnemyKilledEvents.
        public void OnEnemyKilled()
        {
            _targetScore += pointsPerKill;
            _punchTimer = punchDuration;
        }

        private void EnsureSubscribed()
        {
            if (_subscribed) return;
            if (GameManager.Instance == null) return;
            GameManager.Instance.PhaseChanged += OnPhaseChanged;
            _subscribed = true;
            // Apply current phase immediately in case Battle already started.
            OnPhaseChanged(GameManager.Instance.CurrentPhase);
        }

        private void Unsubscribe()
        {
            if (!_subscribed) return;
            if (GameManager.Instance != null) GameManager.Instance.PhaseChanged -= OnPhaseChanged;
            _subscribed = false;
        }

        private void OnPhaseChanged(GamePhase phase)
        {
            if (phase == GamePhase.Battle)
            {
                _targetScore = 0;
                _shownScore = 0f;
                _punchTimer = 0f;
                if (_value != null) { _value.text = "0"; _value.color = baseColor; }
                if (_valueRect != null) _valueRect.localScale = Vector3.one;
                if (_panel != null) _panel.SetActive(true);
            }
            else if (_panel != null)
            {
                _panel.SetActive(false);
            }
        }

        private void BuildCanvas()
        {
            if (_built) return;
            _built = true;

            var canvas = gameObject.GetComponent<Canvas>();
            if (canvas == null) canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 6;
            if (gameObject.GetComponent<CanvasScaler>() == null)
            {
                var scaler = gameObject.AddComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920, 1080);
            }
            if (gameObject.GetComponent<GraphicRaycaster>() == null)
                gameObject.AddComponent<GraphicRaycaster>();

            _panel = new GameObject("ScorePanel", typeof(RectTransform));
            _panel.transform.SetParent(transform, false);
            var prt = (RectTransform)_panel.transform;
            prt.anchorMin = new Vector2(0.5f, 1f);
            prt.anchorMax = new Vector2(0.5f, 1f);
            prt.pivot = new Vector2(0.5f, 1f);
            prt.anchoredPosition = new Vector2(0f, topOffset);
            prt.sizeDelta = new Vector2(420f, 140f);

            _caption = MakeText("Caption", _panel.transform, captionFontSize, new Vector2(0f, 1f));
            var crt = _caption.rectTransform;
            crt.anchorMin = new Vector2(0.5f, 1f);
            crt.anchorMax = new Vector2(0.5f, 1f);
            crt.pivot = new Vector2(0.5f, 1f);
            crt.anchoredPosition = new Vector2(0f, 0f);
            crt.sizeDelta = new Vector2(360f, 28f);
            _caption.text = "SCORE";
            _caption.color = new Color(1f, 1f, 1f, 0.8f);

            _value = MakeText("Value", _panel.transform, valueFontSize, new Vector2(0.5f, 1f));
            _valueRect = _value.rectTransform;
            _valueRect.anchorMin = new Vector2(0.5f, 1f);
            _valueRect.anchorMax = new Vector2(0.5f, 1f);
            _valueRect.pivot = new Vector2(0.5f, 1f);
            _valueRect.anchoredPosition = new Vector2(0f, -34f);
            _valueRect.sizeDelta = new Vector2(420f, 104f);
            _value.text = "0";
            _value.color = baseColor;
        }

        private TextMeshProUGUI MakeText(string name, Transform parent, float size, Vector2 pivot)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var tmp = go.AddComponent<TextMeshProUGUI>();
            if (scoreFont != null) tmp.font = scoreFont;
            if (scoreMaterial != null) tmp.fontSharedMaterial = scoreMaterial;
            tmp.fontSize = size;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.textWrappingMode = TextWrappingModes.NoWrap;
            tmp.raycastTarget = false;
            return tmp;
        }
    }
}
