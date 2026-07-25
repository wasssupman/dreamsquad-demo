using System.Collections;
using PrimeTween;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Wassup.Bridge;
using Wassup.Core;
using Wassup.UI.Layout;

namespace Wassup.UI
{
    // battle-hud-score-timer-menu Unit 1 — bottom-left combo dock. Top row shows the
    // match countdown (moved off the top-center, which the score now owns); bottom row
    // is the "NEXT WAVE {n}" / "NO WAVES" control that early-summons the next wave.
    //
    // The NextWave button used to be built inside BattleBridge (the ECS gateway). It now
    // lives here in the View layer; BattleBridge only exposes read-only wave state
    // (NextWaveAvailable / NextWaveHasNext / NextWaveNumber) which this dock polls.
    public class NextWaveDock : MonoBehaviour
    {
        [SerializeField] private BattleBridge bridge;

        [Header("Timer")]
        [Tooltip("타이머 전용 폰트(미지정 시 기본 TMP). Anton SDF 권장")]
        [SerializeField] private TMP_FontAsset timerFont;
        [SerializeField] private float timerFontSize = 44f;
        [SerializeField] private Color timerColor = Color.white;
        [SerializeField] private Color timerWarnColor = Color.red;
        [Tooltip("이 초 미만이면 타이머를 경고색으로")]
        [SerializeField] private float warnSeconds = 30f;
        [Tooltip("초가 바뀔 때 pop 강도(일반 / 경고구간)")]
        [SerializeField] private float tickPunch = 0.16f;
        [SerializeField] private float tickPunchWarn = 0.36f;

        [Header("Next wave button")]
        [SerializeField] private Sprite dockFrameSprite;
        [SerializeField] private Sprite buttonFaceSprite;
        [SerializeField] private Color buttonColor = new Color(0.12f, 0.42f, 0.82f, 0.95f);
        [SerializeField] private float buttonFontSize = 28f;

        [Header("Backing")]
        [Tooltip("타이머+버튼 뒤 dimmed 배경 판")]
        [SerializeField] private Color backingColor = new Color(0f, 0f, 0f, 0.45f);

        private GameObject _panel;
        private Image _backingImage;
        private TextMeshProUGUI _timerCaption;
        private TextMeshProUGUI _timerLabel;
        private GameObject _buttonRoot;
        private RectTransform _buttonVisual;
        private Image _buttonImage;
        private Button _waveButton;
        private TextMeshProUGUI _waveLabel;
        private bool _built;
        private Coroutine _buttonRelease;

        // 초 단위 변화 감지용(직전 표시된 총 초). -1 = 아직 표시 전(첫 표시엔 pop 생략).
        private int _lastShownSec = -1;
        private Tween _tickTween;

        private void Awake()
        {
            BuildCanvas();
            if (_panel != null) _panel.SetActive(false);
        }

        private bool _subscribed;

        private void OnDisable()
        {
            if (_subscribed && GameManager.Instance != null)
                GameManager.Instance.PhaseChanged -= OnPhaseChanged;
            _subscribed = false;
            if (_buttonRelease != null) StopCoroutine(_buttonRelease);
            if (_buttonVisual != null) _buttonVisual.localScale = Vector3.one;
        }

        // GameManager.Instance may not be set when OnEnable runs (scene load order), so
        // subscribe lazily in Update — mirrors ScoreHudView.
        private void EnsureSubscribed()
        {
            if (_subscribed) return;
            if (GameManager.Instance == null) return;
            GameManager.Instance.PhaseChanged += OnPhaseChanged;
            _subscribed = true;
            OnPhaseChanged(GameManager.Instance.CurrentPhase);
        }

        // The dock (match countdown + early-summon NextWave) is a Battle-phase control:
        // shown only during Battle, hidden in Draft/Placement/None. At game-over the
        // phase stays Battle, but the result overlay covers the dock.
        private void OnPhaseChanged(GamePhase phase)
        {
            if (_panel == null) return;
            bool battle = phase == GamePhase.Battle;
            if (_panel.activeSelf != battle) _panel.SetActive(battle);
            if (battle) _lastShownSec = -1;
        }

        private void Update()
        {
            EnsureSubscribed();
            if (bridge == null || _panel == null || !_panel.activeSelf) return;

            // Timer row — always visible while the dock is shown.
            if (_timerLabel != null)
            {
                float remaining = bridge.TimerRemaining;
                if (remaining < 0f) remaining = 0f;
                int min = (int)(remaining / 60f);
                int sec = (int)(remaining % 60f);
                int totalSec = min * 60 + sec;
                bool warn = remaining < warnSeconds;
                _timerLabel.text = $"{min}:{sec:D2}";
                _timerLabel.color = warn ? timerWarnColor : timerColor;
                if (_timerCaption != null)
                    _timerCaption.color = warn
                        ? new Color(timerWarnColor.r, timerWarnColor.g, timerWarnColor.b, 0.9f)
                        : new Color(0.5f, 0.92f, 1f, 0.95f);

                // 초가 바뀔 때마다 pop — 카운트다운이 살아있게 느껴지도록. 경고구간은 더 크게.
                // 첫 표시(_lastShownSec == -1)엔 생략, useUnscaledTime 로 정지/슬로우 중에도 동작.
                if (totalSec != _lastShownSec)
                {
                    if (_lastShownSec >= 0)
                    {
                        if (_tickTween.isAlive) _tickTween.Stop();
                        _timerLabel.rectTransform.localScale = Vector3.one;
                        float strength = warn ? tickPunchWarn : tickPunch;
                        float dur = warn ? 0.30f : 0.22f;
                        _tickTween = Tween.PunchScale(_timerLabel.rectTransform,
                            Vector3.one * strength, dur, useUnscaledTime: true);
                    }
                    _lastShownSec = totalSec;
                }
            }

            // Next-wave row — visible only for generated-wave battles; label/interactable
            // track the remaining waves.
            bool available = bridge.NextWaveAvailable;
            if (_buttonRoot != null && _buttonRoot.activeSelf != available)
                _buttonRoot.SetActive(available);
            if (available)
            {
                bool hasNext = bridge.NextWaveHasNext;
                if (_waveButton != null) _waveButton.interactable = hasNext;
                if (_waveLabel != null)
                {
                    _waveLabel.text = hasNext ? $"다음 웨이브 {bridge.NextWaveNumber}" : "웨이브 없음";
                    _waveLabel.color = hasNext ? Color.white : new Color(0.68f, 0.72f, 0.8f, 0.95f);
                }
            }
        }

        private void OnWaveButtonClicked()
        {
            SoundManager.Instance?.PlayNextWave();
            if (bridge != null) bridge.ForceNextWave();
            if (_backingImage != null)
                Tween.PunchScale(_backingImage.rectTransform, Vector3.one * 0.06f, 0.2f, useUnscaledTime: true);
        }

        private void BuildCanvas()
        {
            if (_built) return;
            _built = true;

            var roots = UiCanvasSetup.Ensure(gameObject, sortingOrder: 7);

            // Bottom-left container. The bottom-right safe corner belongs to the
            // Dreamcatcher awakening talisman.
            _panel = new GameObject("DockPanel", typeof(RectTransform));
            _panel.transform.SetParent(roots.SafeAreaRoot, false);
            var prt = (RectTransform)_panel.transform;
            prt.anchorMin = new Vector2(0f, 0f);
            prt.anchorMax = new Vector2(0f, 0f);
            prt.pivot = new Vector2(0f, 0f);
            prt.anchoredPosition = new Vector2(40f, 40f);
            prt.sizeDelta = new Vector2(250f, 150f);

            // Generated symbol-free jelly frame behind the unchanged two-row structure.
            var backing = new GameObject("Backing", typeof(RectTransform), typeof(Image));
            backing.transform.SetParent(_panel.transform, false);
            var bkrt = (RectTransform)backing.transform;
            bkrt.anchorMin = Vector2.zero;
            bkrt.anchorMax = Vector2.one;
            bkrt.offsetMin = new Vector2(-10f, -10f);
            bkrt.offsetMax = new Vector2(10f, 10f);
            _backingImage = backing.GetComponent<Image>();
            _backingImage.sprite = dockFrameSprite;
            _backingImage.color = dockFrameSprite != null ? Color.white : backingColor;
            _backingImage.raycastTarget = false;

            // Small caption supports the time number without changing timer ownership.
            var captionGO = new GameObject("TimerCaption", typeof(RectTransform));
            captionGO.transform.SetParent(_panel.transform, false);
            var captionRect = (RectTransform)captionGO.transform;
            captionRect.anchorMin = new Vector2(0f, 1f);
            captionRect.anchorMax = new Vector2(1f, 1f);
            captionRect.pivot = new Vector2(0.5f, 1f);
            captionRect.offsetMin = new Vector2(16f, -24f);
            captionRect.offsetMax = new Vector2(-16f, -4f);
            _timerCaption = captionGO.AddComponent<TextMeshProUGUI>();
            _timerCaption.text = "남은 시간";
            _timerCaption.fontSize = 17f;
            _timerCaption.fontStyle = FontStyles.Bold;
            _timerCaption.color = new Color(0.5f, 0.92f, 1f, 0.95f);
            _timerCaption.alignment = TextAlignmentOptions.Center;
            _timerCaption.raycastTarget = false;

            // Timer row (top).
            var timerGO = new GameObject("Timer", typeof(RectTransform));
            timerGO.transform.SetParent(_panel.transform, false);
            var trt = (RectTransform)timerGO.transform;
            trt.anchorMin = new Vector2(0f, 1f);
            trt.anchorMax = new Vector2(1f, 1f);
            trt.pivot = new Vector2(0.5f, 1f);
            trt.offsetMin = new Vector2(0f, -70f);
            trt.offsetMax = new Vector2(0f, -25f);
            _timerLabel = timerGO.AddComponent<TextMeshProUGUI>();
            if (timerFont != null) _timerLabel.font = timerFont;
            _timerLabel.text = "3:00";
            _timerLabel.fontSize = timerFontSize;
            _timerLabel.fontStyle = FontStyles.Bold;
            _timerLabel.color = timerColor;
            _timerLabel.alignment = TextAlignmentOptions.Center;
            _timerLabel.raycastTarget = false;
            ApplyOutline(_timerLabel, new Color(0.03f, 0.06f, 0.17f, 1f), 0.18f);

            // Next-wave button row (bottom).
            _buttonRoot = new GameObject("NextWaveButton", typeof(RectTransform), typeof(Image), typeof(Button));
            _buttonRoot.transform.SetParent(_panel.transform, false);
            var brt = (RectTransform)_buttonRoot.transform;
            brt.anchorMin = new Vector2(0f, 0f);
            brt.anchorMax = new Vector2(1f, 0f);
            brt.pivot = new Vector2(0.5f, 0f);
            brt.offsetMin = new Vector2(8f, 5f);
            brt.offsetMax = new Vector2(-8f, 70f);
            _buttonVisual = brt;
            _buttonImage = _buttonRoot.GetComponent<Image>();
            _buttonImage.sprite = buttonFaceSprite;
            _buttonImage.color = buttonFaceSprite != null ? Color.white : buttonColor;
            _waveButton = _buttonRoot.GetComponent<Button>();
            _waveButton.targetGraphic = _buttonImage;
            _waveButton.transition = Selectable.Transition.ColorTint;
            var colors = _waveButton.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1f, 1f, 1f, 1f);
            colors.pressedColor = new Color(0.82f, 0.9f, 1f, 1f);
            colors.selectedColor = Color.white;
            colors.disabledColor = new Color(0.42f, 0.46f, 0.56f, 0.82f);
            colors.colorMultiplier = 1f;
            colors.fadeDuration = 0.08f;
            _waveButton.colors = colors;
            _waveButton.onClick.AddListener(OnWaveButtonClicked);
            BuildPointerFeedback(_buttonRoot);

            var labelGO = new GameObject("Label", typeof(RectTransform));
            labelGO.transform.SetParent(_buttonRoot.transform, false);
            var lrt = (RectTransform)labelGO.transform;
            lrt.anchorMin = Vector2.zero;
            lrt.anchorMax = Vector2.one;
            lrt.offsetMin = Vector2.zero;
            lrt.offsetMax = Vector2.zero;
            _waveLabel = labelGO.AddComponent<TextMeshProUGUI>();
            _waveLabel.text = "다음 웨이브";
            _waveLabel.fontSize = buttonFontSize;
            _waveLabel.color = Color.white;
            _waveLabel.fontStyle = FontStyles.Bold;
            _waveLabel.alignment = TextAlignmentOptions.Center;
            _waveLabel.raycastTarget = false;
            ApplyOutline(_waveLabel, new Color(0.02f, 0.08f, 0.2f, 1f), 0.16f);

            _buttonRoot.SetActive(false);

            UiLayer.Apply(gameObject);
        }

        private void BuildPointerFeedback(GameObject target)
        {
            var trigger = target.AddComponent<EventTrigger>();
            trigger.triggers = new System.Collections.Generic.List<EventTrigger.Entry>();

            var down = new EventTrigger.Entry { eventID = EventTriggerType.PointerDown };
            down.callback.AddListener(_ =>
            {
                if (_waveButton == null || !_waveButton.interactable || _buttonVisual == null) return;
                if (_buttonRelease != null) StopCoroutine(_buttonRelease);
                _buttonVisual.localScale = new Vector3(1.02f, 0.92f, 1f);
            });
            trigger.triggers.Add(down);

            var up = new EventTrigger.Entry { eventID = EventTriggerType.PointerUp };
            up.callback.AddListener(_ => BeginButtonRelease());
            trigger.triggers.Add(up);

            var exit = new EventTrigger.Entry { eventID = EventTriggerType.PointerExit };
            exit.callback.AddListener(_ => BeginButtonRelease());
            trigger.triggers.Add(exit);
        }

        private void BeginButtonRelease()
        {
            if (_buttonVisual == null) return;
            if (_buttonRelease != null) StopCoroutine(_buttonRelease);
            _buttonRelease = StartCoroutine(ButtonReleaseRoutine());
        }

        private IEnumerator ButtonReleaseRoutine()
        {
            Vector3 start = _buttonVisual.localScale;
            const float duration = 0.14f;
            float time = 0f;
            while (time < duration)
            {
                time += Time.unscaledDeltaTime;
                float k = Mathf.Clamp01(time / duration);
                Vector3 scale = k < 0.58f
                    ? Vector3.Lerp(start, new Vector3(1.04f, 1.04f, 1f), k / 0.58f)
                    : Vector3.Lerp(new Vector3(1.04f, 1.04f, 1f), Vector3.one, (k - 0.58f) / 0.42f);
                _buttonVisual.localScale = scale;
                yield return null;
            }
            _buttonVisual.localScale = Vector3.one;
            _buttonRelease = null;
        }

        private static void ApplyOutline(TextMeshProUGUI label, Color color, float width)
        {
            if (label.font == null) return;
            var material = label.fontMaterial;
            material.SetColor(ShaderUtilities.ID_OutlineColor, color);
            material.SetFloat(ShaderUtilities.ID_OutlineWidth, width);
        }
    }
}
