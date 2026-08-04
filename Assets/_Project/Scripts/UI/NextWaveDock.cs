using System.Collections;
using PrimeTween;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Wassup.Bridge;
using Wassup.Core;
using Wassup.Core.Session;
using Wassup.UI.Layout;

namespace Wassup.UI
{
    // battle-hud-score-timer-menu Unit 1 — bottom-left combo dock. Top row shows the
    // match countdown (moved off the top-center, which the score now owns); bottom row
    // is the "NEXT WAVE {n}" / "NO WAVES" control that early-summons the next wave.
    //
    // The NextWave button used to be built inside BattleBridge (the ECS gateway). It now
    // lives here in the View layer.
    //
    // battle-sim-extraction unit 13(A) — 웨이브·타이머 폴링은 `bridge.X` 직독에서
    // `MatchSession.Current.ReadModel` 스냅샷으로 옮겼다. 프로퍼티를 5번 따로 읽으면 값들이
    // 서로 다른 tick 을 볼 수 있다(지금은 단일 스레드라 실害가 없지만 계약이 그렇다) — 그래서
    // Update 당 **1회 읽고** 그 스냅샷만 쓴다. 버튼의 ForceNextWave 는 아직 직접 호출이며
    // 커맨드 전환은 bundle C 의 몫이다.
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

        [Header("Layout")]
        [SerializeField] private Vector2 panelSize = new Vector2(288f, 168f);
        [SerializeField] private Vector2 panelOffset = new Vector2(40f, 40f);
        [SerializeField] private Vector2 timerPlateSize = new Vector2(264f, 80f);
        [SerializeField] private Vector2 timerPlateOffset = new Vector2(8f, 104f);
        [SerializeField] private Vector2 buttonSize = new Vector2(288f, 108f);

        [Header("Next wave button")]
        [SerializeField] private Sprite dockFrameSprite;
        [SerializeField] private Sprite buttonFaceSprite;
        [SerializeField] private Sprite attentionRingSprite;
        [SerializeField] private Color buttonColor = new Color(0.12f, 0.42f, 0.82f, 0.95f);
        [SerializeField] private float buttonFontSize = 28f;
        [SerializeField] private float buttonMinFontSize = 22f;
        [Tooltip("x=left, y=top, z=right(화살표 예약폭 포함), w=bottom")]
        [SerializeField] private Vector4 buttonContentPadding = new Vector4(30f, 18f, 86f, 18f);
        [SerializeField] private Color disabledContentColor = new Color(0.58f, 0.66f, 0.78f, 0.9f);
        [SerializeField] private Vector3 pressScale = new Vector3(1.02f, 0.92f, 1f);
        [SerializeField] private float releaseDuration = 0.14f;
        [SerializeField] private float releaseOvershoot = 1.04f;

        [Header("Backing")]
        [Tooltip("타이머 캡슐 Sprite 누락 시 대체색")]
        [SerializeField] private Color backingColor = new Color(0f, 0f, 0f, 0.45f);

        [Header("Clear ready attention")]
        [SerializeField] private Color clearReadyColor = new Color(1f, 0.72f, 0.12f, 1f);
        [SerializeField] private float attentionEntryDuration = 0.48f;
        [SerializeField] private float attentionEntryScale = 1.1f;
        [SerializeField] private float attentionPeriod = 1.5f;
        [SerializeField] private float attentionHopDuration = 0.34f;
        [SerializeField] private float attentionHopHeight = 7f;
        [SerializeField] private float attentionBumpScale = 0.08f;
        [SerializeField] private float attentionLean = -4f;
        [SerializeField] private Vector2 attentionNudge = new Vector2(7f, 2f);
        [SerializeField] private float chevronKick = 9f;
        [SerializeField] private float pulseRingDuration = 0.72f;
        [SerializeField] private float pulseRingStagger = 0.18f;
        [SerializeField] private float pulseRingExpansion = 1.28f;
        [SerializeField] private float pulseRingThickness = 4f;

        private GameObject _panel;
        private Image _backingImage;
        private TextMeshProUGUI _timerCaption;
        private TextMeshProUGUI _timerLabel;
        private GameObject _buttonRoot;
        private RectTransform _buttonMotionRoot;
        private RectTransform _buttonVisual;
        private Image _buttonImage;
        private Button _waveButton;
        private TextMeshProUGUI _waveLabel;
        private RectTransform _chevronRoot;
        private Vector2 _chevronRestPosition;
        private CanvasGroup _chevronGroup;
        private readonly Image[] _chevronImages = new Image[4];
        private RectTransform _goldRim;
        private CanvasGroup _goldRimGroup;
        private readonly RectTransform[] _pulseRings = new RectTransform[2];
        private readonly CanvasGroup[] _pulseRingGroups = new CanvasGroup[2];
        private bool _built;
        private Coroutine _buttonRelease;
        private Coroutine _attention;
        private bool _clearReadyVisual;
        private bool _pointerPressed;

        // 초 단위 변화 감지용(직전 표시된 총 초). -1 = 아직 표시 전(첫 표시엔 pop 생략).
        private int _lastShownSec = -1;
        private Tween _tickTween;

        private enum VisualState
        {
            Normal,
            ClearReady,
            Disabled
        }

        // first-session-tutorial unit 20 — 튜토리얼 포커스 링이 감쌀 대상(읽기 전용).
        // _panel(타이머 캡슐까지 포함한 dock 전체)이 아니라 **버튼만** 준다 — 링이 남은시간을
        // 함께 감싸면 지시 대상이 흐려진다.
        //
        // 이 오브젝트는 Awake 에서 SetActive(false) 로 시작하고 Update 의
        // `ReadModel.NextWaveAvailable` 폴링이 켠다. 즉 Battle 진입 프레임에는 아직 비활성이므로,
        // 튜토리얼은 활성을 기다린 뒤에 포커스를 걸어야 한다(unit 19 의 WaitForHintTarget).
        public RectTransform WaveButtonRect =>
            _buttonRoot != null ? (RectTransform)_buttonRoot.transform : null;

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
            _buttonRelease = null;
            if (_tickTween.isAlive) _tickTween.Stop();
            SetClearReadyVisual(false);
            _pointerPressed = false;
            ResetButtonPose();
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
            else SetClearReadyVisual(false);
        }

        private void Update()
        {
            EnsureSubscribed();
            if (_panel == null || !_panel.activeSelf) return;
            // 판이 없으면 그릴 것이 없다. dock 패널은 GamePhase.Battle 에서만 활성이고 세션 무장은
            // BeginPlacement(그보다 앞)에서 끝나므로 이 가드가 구 `bridge == null` 과 같은 창을 막는다.
            if (!MatchSession.IsActive) return;
            var rm = MatchSession.Current.ReadModel;

            // Timer row — always visible while the dock is shown.
            if (_timerLabel != null)
            {
                float remaining = rm.TimerRemaining;
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
            bool available = rm.NextWaveAvailable;
            if (_buttonRoot != null && _buttonRoot.activeSelf != available)
                _buttonRoot.SetActive(available);
            if (!available)
            {
                SetClearReadyVisual(false);
                return;
            }

            if (available)
            {
                bool hasNext = rm.NextWaveHasNext;
                if (_waveButton != null) _waveButton.interactable = hasNext;
                if (_waveLabel != null)
                {
                    _waveLabel.text = hasNext ? $"다음 웨이브 {rm.NextWaveNumber}" : "웨이브 없음";
                }

                VisualState state = !hasNext
                    ? VisualState.Disabled
                    : rm.NextWaveClearReady
                        ? VisualState.ClearReady
                        : VisualState.Normal;
                ApplyVisualState(state);
            }
        }

        private void OnWaveButtonClicked()
        {
            SetClearReadyVisual(false);
            SoundManager.Instance?.PlayNextWave();
            // unit 13-C — 직접 호출에서 커맨드로. 거절이면 아무 일도 일어나지 않는다(구
            // `bridge == null` 가드와 같은 결과) — 사운드·시각 피드백은 그대로라 체감 동일하다.
            MatchSession.Send(seq => MatchCommand.ForceNextWave(seq));
        }

        private void BuildCanvas()
        {
            if (_built) return;
            _built = true;

            var roots = UiCanvasSetup.Ensure(gameObject, sortingOrder: 7);

            // Bottom-left footprint. The timer is deliberately smaller than the CTA so
            // "advance the battle" reads before the supporting countdown information.
            _panel = new GameObject("DockPanel", typeof(RectTransform));
            _panel.transform.SetParent(roots.SafeAreaRoot, false);
            var prt = (RectTransform)_panel.transform;
            prt.anchorMin = new Vector2(0f, 0f);
            prt.anchorMax = new Vector2(0f, 0f);
            prt.pivot = new Vector2(0f, 0f);
            prt.anchoredPosition = panelOffset;
            prt.sizeDelta = panelSize;

            var timerPlate = new GameObject("TimerPlate", typeof(RectTransform), typeof(Image));
            timerPlate.transform.SetParent(_panel.transform, false);
            var timerPlateRect = (RectTransform)timerPlate.transform;
            timerPlateRect.anchorMin = Vector2.zero;
            timerPlateRect.anchorMax = Vector2.zero;
            timerPlateRect.pivot = Vector2.zero;
            timerPlateRect.anchoredPosition = timerPlateOffset;
            timerPlateRect.sizeDelta = timerPlateSize;
            _backingImage = timerPlate.GetComponent<Image>();
            _backingImage.sprite = dockFrameSprite;
            _backingImage.color = dockFrameSprite != null ? Color.white : backingColor;
            _backingImage.preserveAspect = dockFrameSprite != null;
            _backingImage.raycastTarget = false;

            var captionGO = new GameObject("TimerCaption", typeof(RectTransform));
            captionGO.transform.SetParent(timerPlate.transform, false);
            var captionRect = (RectTransform)captionGO.transform;
            captionRect.anchorMin = new Vector2(0f, 0f);
            captionRect.anchorMax = new Vector2(0f, 1f);
            captionRect.pivot = new Vector2(0f, 0.5f);
            captionRect.anchoredPosition = new Vector2(20f, 0f);
            // 캡슐 스프라이트(NextWaveDockFrame)는 476x144 캔버스 중 pill 아트가 세로
            // 102px(상하 21px 투명 여백) — 플레이트 264x80 기준 가시 pill 높이는 ~57px.
            // 캡션 박스는 pill 안쪽으로 인셋해 한 줄 "남은시간"이 패딩을 갖고 담긴다.
            captionRect.sizeDelta = new Vector2(100f, -34f);
            _timerCaption = captionGO.AddComponent<TextMeshProUGUI>();
            _timerCaption.text = "남은시간";
            _timerCaption.fontSize = 20f;
            _timerCaption.fontStyle = FontStyles.Bold;
            _timerCaption.color = new Color(0.62f, 0.95f, 1f, 1f);
            _timerCaption.alignment = TextAlignmentOptions.Center;
            _timerCaption.raycastTarget = false;
            ApplyOutline(_timerCaption, new Color(0.03f, 0.06f, 0.17f, 1f), 0.12f);

            var timerGO = new GameObject("Timer", typeof(RectTransform));
            timerGO.transform.SetParent(timerPlate.transform, false);
            var trt = (RectTransform)timerGO.transform;
            trt.anchorMin = Vector2.zero;
            trt.anchorMax = Vector2.one;
            trt.offsetMin = new Vector2(120f, 12f);
            trt.offsetMax = new Vector2(-20f, -12f);
            _timerLabel = timerGO.AddComponent<TextMeshProUGUI>();
            if (timerFont != null) _timerLabel.font = timerFont;
            _timerLabel.text = "3:00";
            _timerLabel.fontSize = timerFontSize;
            _timerLabel.fontStyle = FontStyles.Bold;
            _timerLabel.color = timerColor;
            _timerLabel.alignment = TextAlignmentOptions.Center;
            _timerLabel.raycastTarget = false;
            ApplyOutline(_timerLabel, new Color(0.03f, 0.06f, 0.17f, 1f), 0.18f);

            _buttonRoot = new GameObject("NextWaveButton", typeof(RectTransform), typeof(Image), typeof(Button));
            _buttonRoot.transform.SetParent(_panel.transform, false);
            var brt = (RectTransform)_buttonRoot.transform;
            brt.anchorMin = Vector2.zero;
            brt.anchorMax = Vector2.zero;
            brt.pivot = Vector2.zero;
            brt.anchoredPosition = Vector2.zero;
            brt.sizeDelta = buttonSize;

            var hitImage = _buttonRoot.GetComponent<Image>();
            hitImage.color = new Color(1f, 1f, 1f, 0f);
            hitImage.raycastTarget = true;

            for (int i = 0; i < _pulseRings.Length; i++)
            {
                CreateAttentionFrame(
                    _buttonRoot.transform,
                    $"PulseRing{i + 1}",
                    out _pulseRings[i],
                    out _pulseRingGroups[i]);
                _pulseRings[i].offsetMin = new Vector2(8f, 12f);
                _pulseRings[i].offsetMax = new Vector2(-8f, -14f);
            }

            var motionGO = new GameObject("MotionRoot", typeof(RectTransform));
            motionGO.transform.SetParent(_buttonRoot.transform, false);
            _buttonMotionRoot = (RectTransform)motionGO.transform;
            Stretch(_buttonMotionRoot);
            _buttonVisual = _buttonMotionRoot;

            var faceGO = new GameObject("Face", typeof(RectTransform), typeof(Image));
            faceGO.transform.SetParent(_buttonMotionRoot, false);
            var faceRect = (RectTransform)faceGO.transform;
            Stretch(faceRect);
            _buttonImage = faceGO.GetComponent<Image>();
            _buttonImage.sprite = buttonFaceSprite;
            _buttonImage.color = buttonFaceSprite != null ? Color.white : buttonColor;
            _buttonImage.preserveAspect = buttonFaceSprite != null;
            _buttonImage.raycastTarget = false;

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

            CreateAttentionFrame(
                _buttonMotionRoot,
                "GoldRim",
                out _goldRim,
                out _goldRimGroup);
            _goldRim.offsetMin = new Vector2(11f, 14f);
            _goldRim.offsetMax = new Vector2(-11f, -17f);

            var labelGO = new GameObject("Label", typeof(RectTransform));
            labelGO.transform.SetParent(_buttonMotionRoot, false);
            var lrt = (RectTransform)labelGO.transform;
            lrt.anchorMin = Vector2.zero;
            lrt.anchorMax = Vector2.one;
            lrt.offsetMin = new Vector2(buttonContentPadding.x, buttonContentPadding.w);
            lrt.offsetMax = new Vector2(-buttonContentPadding.z, -buttonContentPadding.y);
            _waveLabel = labelGO.AddComponent<TextMeshProUGUI>();
            _waveLabel.text = "다음 웨이브";
            _waveLabel.fontSize = buttonFontSize;
            _waveLabel.enableAutoSizing = true;
            _waveLabel.fontSizeMin = Mathf.Min(buttonMinFontSize, buttonFontSize);
            _waveLabel.fontSizeMax = buttonFontSize;
            _waveLabel.color = Color.white;
            _waveLabel.fontStyle = FontStyles.Bold;
            _waveLabel.alignment = TextAlignmentOptions.Center;
            _waveLabel.raycastTarget = false;
            ApplyOutline(_waveLabel, new Color(0.02f, 0.08f, 0.2f, 1f), 0.16f);

            var chevronGO = new GameObject("DoubleChevron", typeof(RectTransform), typeof(CanvasGroup));
            chevronGO.transform.SetParent(_buttonMotionRoot, false);
            _chevronRoot = (RectTransform)chevronGO.transform;
            _chevronRoot.anchorMin = new Vector2(1f, 0.5f);
            _chevronRoot.anchorMax = new Vector2(1f, 0.5f);
            _chevronRoot.pivot = new Vector2(0.5f, 0.5f);
            _chevronRoot.anchoredPosition = new Vector2(-42f, 3f);
            _chevronRoot.sizeDelta = new Vector2(58f, 58f);
            _chevronRestPosition = _chevronRoot.anchoredPosition;
            _chevronGroup = chevronGO.GetComponent<CanvasGroup>();
            _chevronGroup.blocksRaycasts = false;
            _chevronGroup.interactable = false;
            CreateChevronBar(_chevronRoot, 0, new Vector2(-10f, 8f), 45f);
            CreateChevronBar(_chevronRoot, 1, new Vector2(-10f, -8f), -45f);
            CreateChevronBar(_chevronRoot, 2, new Vector2(9f, 8f), 45f);
            CreateChevronBar(_chevronRoot, 3, new Vector2(9f, -8f), -45f);

            ResetButtonPose();
            _buttonRoot.SetActive(false);

            UiLayer.Apply(gameObject);
        }

        private void ApplyVisualState(VisualState state)
        {
            bool disabled = state == VisualState.Disabled;
            if (_waveLabel != null)
                _waveLabel.color = disabled ? disabledContentColor : Color.white;
            if (_chevronGroup != null)
                _chevronGroup.alpha = disabled ? 0.38f : 1f;
            for (int i = 0; i < _chevronImages.Length; i++)
                if (_chevronImages[i] != null)
                    _chevronImages[i].color = disabled ? disabledContentColor : Color.white;
            SetClearReadyVisual(state == VisualState.ClearReady);
        }

        private void SetClearReadyVisual(bool want)
        {
            if (_clearReadyVisual == want) return;
            _clearReadyVisual = want;
            if (want)
            {
                if (!_pointerPressed && _buttonRelease == null)
                    StartAttention(playEntry: true);
            }
            else
            {
                StopAttention();
            }
        }

        private void StartAttention(bool playEntry)
        {
            StopAttention();
            if (!_clearReadyVisual || _pointerPressed || !isActiveAndEnabled) return;
            _attention = StartCoroutine(AttentionRoutine(playEntry));
        }

        private void StopAttention()
        {
            if (_attention != null)
            {
                StopCoroutine(_attention);
                _attention = null;
            }
            ResetButtonPose();
        }

        private IEnumerator AttentionRoutine(bool playEntry)
        {
            SetPulseRingsActive(true);

            if (playEntry)
            {
                float elapsed = 0f;
                float duration = Mathf.Max(0.01f, attentionEntryDuration);
                while (elapsed < duration && _clearReadyVisual && !_pointerPressed)
                {
                    elapsed += Time.unscaledDeltaTime;
                    float t = Mathf.Clamp01(elapsed / duration);
                    float arc = Mathf.Sin(t * Mathf.PI);
                    float scale = t < 0.45f
                        ? Mathf.Lerp(1f, attentionEntryScale, t / 0.45f)
                        : Mathf.Lerp(attentionEntryScale, 1f, (t - 0.45f) / 0.55f);
                    ApplyAttentionPose(arc, scale, chevronKick * arc);
                    if (_goldRimGroup != null) _goldRimGroup.alpha = 1f - t * 0.45f;
                    yield return null;
                }
                ResetAttentionMotion();
            }

            float period = Mathf.Max(0.4f, attentionPeriod);
            while (_clearReadyVisual && !_pointerPressed)
            {
                float elapsed = 0f;
                while (elapsed < period && _clearReadyVisual && !_pointerPressed)
                {
                    elapsed += Time.unscaledDeltaTime;
                    float hopT = Mathf.Clamp01(elapsed / Mathf.Max(0.01f, attentionHopDuration));
                    float bump = elapsed <= attentionHopDuration ? Mathf.Sin(hopT * Mathf.PI) : 0f;
                    ApplyAttentionPose(
                        bump,
                        1f + bump * attentionBumpScale,
                        chevronKick * bump);

                    if (_goldRimGroup != null)
                    {
                        float breath = 0.5f + 0.5f * Mathf.Sin(elapsed / period * Mathf.PI * 2f);
                        _goldRimGroup.alpha = Mathf.Lerp(0.42f, 0.78f, breath);
                    }

                    UpdatePulseRings(elapsed);
                    yield return null;
                }
                ResetAttentionMotion();
            }

            _attention = null;
            ResetButtonPose();
        }

        private void ApplyAttentionPose(float amount, float scale, float chevronOffset)
        {
            if (_buttonMotionRoot != null)
            {
                _buttonMotionRoot.localScale = new Vector3(scale, scale, 1f);
                _buttonMotionRoot.anchoredPosition =
                    attentionNudge * amount + Vector2.up * (attentionHopHeight * amount);
                _buttonMotionRoot.localRotation =
                    Quaternion.Euler(0f, 0f, attentionLean * amount);
            }
            if (_chevronRoot != null)
                _chevronRoot.anchoredPosition =
                    _chevronRestPosition + Vector2.right * chevronOffset;
        }

        private void UpdatePulseRings(float elapsed)
        {
            float duration = Mathf.Max(0.01f, pulseRingDuration);
            for (int i = 0; i < _pulseRings.Length; i++)
            {
                float t = (elapsed - i * pulseRingStagger) / duration;
                bool active = t >= 0f && t <= 1f;
                if (_pulseRingGroups[i] != null)
                    _pulseRingGroups[i].alpha = active ? Mathf.Lerp(0.52f, 0f, t) : 0f;
                if (_pulseRings[i] != null)
                {
                    float scale = active ? Mathf.Lerp(1f, pulseRingExpansion, t) : 1f;
                    _pulseRings[i].localScale = new Vector3(scale, scale, 1f);
                }
            }
        }

        private void ResetAttentionMotion()
        {
            if (_buttonMotionRoot != null)
            {
                _buttonMotionRoot.anchoredPosition = Vector2.zero;
                _buttonMotionRoot.localRotation = Quaternion.identity;
                if (!_pointerPressed && _buttonRelease == null)
                    _buttonMotionRoot.localScale = Vector3.one;
            }
            if (_chevronRoot != null)
                _chevronRoot.anchoredPosition = _chevronRestPosition;
        }

        private void ResetButtonPose()
        {
            ResetAttentionMotion();
            if (_buttonMotionRoot != null && !_pointerPressed && _buttonRelease == null)
                _buttonMotionRoot.localScale = Vector3.one;
            if (_goldRimGroup != null) _goldRimGroup.alpha = 0f;
            SetPulseRingsActive(false);
        }

        private void SetPulseRingsActive(bool active)
        {
            for (int i = 0; i < _pulseRings.Length; i++)
            {
                if (_pulseRings[i] != null)
                {
                    _pulseRings[i].gameObject.SetActive(active);
                    _pulseRings[i].localScale = Vector3.one;
                }
                if (_pulseRingGroups[i] != null) _pulseRingGroups[i].alpha = 0f;
            }
        }

        private void CreateChevronBar(
            RectTransform parent,
            int index,
            Vector2 anchoredPosition,
            float angle)
        {
            var go = new GameObject($"Bar{index + 1}", typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var rect = (RectTransform)go.transform;
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = new Vector2(7f, 28f);
            rect.localRotation = Quaternion.Euler(0f, 0f, angle);
            _chevronImages[index] = go.GetComponent<Image>();
            _chevronImages[index].color = Color.white;
            _chevronImages[index].raycastTarget = false;
        }

        private void CreateAttentionFrame(
            Transform parent,
            string name,
            out RectTransform rect,
            out CanvasGroup group)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasGroup), typeof(Image));
            go.transform.SetParent(parent, false);
            rect = (RectTransform)go.transform;
            Stretch(rect);
            group = go.GetComponent<CanvasGroup>();
            group.alpha = 0f;
            group.interactable = false;
            group.blocksRaycasts = false;

            var image = go.GetComponent<Image>();
            image.sprite = attentionRingSprite;
            image.color = attentionRingSprite != null ? Color.white : new Color(0f, 0f, 0f, 0f);
            image.preserveAspect = attentionRingSprite != null;
            image.raycastTarget = false;

            if (attentionRingSprite != null) return;
            CreateOutlineBar(rect, "Top", clearReadyColor, new Vector2(0f, 1f), new Vector2(1f, 1f),
                new Vector2(0.5f, 1f), new Vector2(0f, pulseRingThickness));
            CreateOutlineBar(rect, "Bottom", clearReadyColor, new Vector2(0f, 0f), new Vector2(1f, 0f),
                new Vector2(0.5f, 0f), new Vector2(0f, pulseRingThickness));
            CreateOutlineBar(rect, "Left", clearReadyColor, new Vector2(0f, 0f), new Vector2(0f, 1f),
                new Vector2(0f, 0.5f), new Vector2(pulseRingThickness, 0f));
            CreateOutlineBar(rect, "Right", clearReadyColor, new Vector2(1f, 0f), new Vector2(1f, 1f),
                new Vector2(1f, 0.5f), new Vector2(pulseRingThickness, 0f));
        }

        private static void CreateOutlineBar(
            RectTransform parent,
            string name,
            Color color,
            Vector2 anchorMin,
            Vector2 anchorMax,
            Vector2 pivot,
            Vector2 sizeDelta)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var rect = (RectTransform)go.transform;
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = pivot;
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = sizeDelta;
            var image = go.GetComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        private void BuildPointerFeedback(GameObject target)
        {
            var trigger = target.AddComponent<EventTrigger>();
            trigger.triggers = new System.Collections.Generic.List<EventTrigger.Entry>();

            var down = new EventTrigger.Entry { eventID = EventTriggerType.PointerDown };
            down.callback.AddListener(_ =>
            {
                if (_waveButton == null || !_waveButton.interactable || _buttonVisual == null) return;
                _pointerPressed = true;
                StopAttention();
                if (_buttonRelease != null) StopCoroutine(_buttonRelease);
                _buttonRelease = null;
                _buttonVisual.localScale = pressScale;
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
            if (!_pointerPressed && _buttonRelease == null) return;
            _pointerPressed = false;
            if (_buttonRelease != null) StopCoroutine(_buttonRelease);
            _buttonRelease = StartCoroutine(ButtonReleaseRoutine());
        }

        private IEnumerator ButtonReleaseRoutine()
        {
            Vector3 start = _buttonVisual.localScale;
            float duration = Mathf.Max(0.01f, releaseDuration);
            float time = 0f;
            while (time < duration)
            {
                time += Time.unscaledDeltaTime;
                float k = Mathf.Clamp01(time / duration);
                Vector3 scale = k < 0.58f
                    ? Vector3.Lerp(start, Vector3.one * releaseOvershoot, k / 0.58f)
                    : Vector3.Lerp(Vector3.one * releaseOvershoot, Vector3.one, (k - 0.58f) / 0.42f);
                scale.z = 1f;
                _buttonVisual.localScale = scale;
                yield return null;
            }
            _buttonVisual.localScale = Vector3.one;
            _buttonRelease = null;
            if (_clearReadyVisual)
                StartAttention(playEntry: false);
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
