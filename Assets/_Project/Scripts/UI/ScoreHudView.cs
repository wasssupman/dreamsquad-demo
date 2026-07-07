using PrimeTween;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Wassup.Core;

namespace Wassup.UI
{
    // Runtime-built live score, screen top-center just below the timer. Increments
    // per enemy kill (BattleBridge.DrainEnemyKilledEvents -> OnEnemyKilled). Stylish
    // increase driven by PrimeTween: an elastic punch + white-hot->gold flash, using
    // the Kanit Bold Italic SDF font (dynamic sporty oblique, deliberately distinct
    // from the Bangers SDF damage popups). Display-only — does not feed ResultScreen.
    // Same-frame kills (AoE wipes) coalesce into one intensity-scaled hit.
    public class ScoreHudView : MonoBehaviour
    {
        [Header("Style font (Kanit Bold Italic SDF + outline). Null -> TMP default.")]
        [SerializeField] private TMP_FontAsset scoreFont;
        [SerializeField] private Material scoreMaterial;

        [Header("Scoring")]
        [Tooltip("적 1처치당 가점")]
        [SerializeField] private int pointsPerKill = 10;

        [Header("Increase feel")]
        [Tooltip("표시 숫자가 목표로 따라붙는 속도(클수록 빠름)")]
        [SerializeField] private float rollLerp = 14f;
        [Tooltip("처치 시 펀치 스케일 배수(1=없음)")]
        [SerializeField] private float punchScale = 1.5f;
        [Tooltip("펀치/플래시 지속(초)")]
        [SerializeField] private float punchDuration = 0.28f;
        [Tooltip("같은 프레임 다처치(AoE)마다 펀치 강도 증가분")]
        [SerializeField] private float multiKillBoost = 0.35f;
        [Tooltip("다처치 펀치 강도 상한 배수")]
        [SerializeField] private float maxMultiKillBoost = 2.5f;
        [Tooltip("처치 순간 화이트핫 플래시 색")]
        [SerializeField] private Color flashColor = new Color(1f, 0.97f, 0.85f);
        [Tooltip("안착 리치 골드 색")]
        [SerializeField] private Color baseColor = new Color(1f, 0.78f, 0.28f);

        [Header("Impact burst (procedural gold spark quads)")]
        [SerializeField] private ScoreBurstStyle burst = new ScoreBurstStyle();

        [Header("Glow & shine (additive, Wassup/UI/Additive)")]
        [Tooltip("Additive UI 머티리얼. Null → 기본(알파 블렌드).")]
        [SerializeField] private Material additiveMaterial;
        [Tooltip("소프트 라디얼 글로우 스프라이트")]
        [SerializeField] private Sprite glowSprite;
        [Tooltip("소프트 세로 바 샤인 스프라이트")]
        [SerializeField] private Sprite shineSprite;
        [SerializeField] private Color glowColor = new Color(1f, 0.68f, 0.24f, 1f);
        [SerializeField] private float glowSize = 200f;
        [Tooltip("평상시 은은한 글로우 알파 (숫자 가독 우선 — 낮게)")]
        [SerializeField] private float glowRestAlpha = 0.05f;
        [Tooltip("처치 순간 플래시 글로우 알파 (숫자를 덮지 않게 절제)")]
        [SerializeField] private float glowFlashAlpha = 0.22f;
        [SerializeField] private float glowFlashDuration = 0.35f;
        [SerializeField] private float glowPulseScale = 1.35f;
        [Tooltip("은은한 글린트 — 알파 낮게(직선 이동이 튀지 않게)")]
        [SerializeField] private Color shineColor = new Color(1f, 0.96f, 0.82f, 0.22f);
        [SerializeField] private float shineWidth = 24f;
        [Tooltip("대각 기울기(도)")]
        [SerializeField] private float shineTiltDeg = 18f;
        [Tooltip("좌→우 스윕 거리(px)")]
        [SerializeField] private float shineTravel = 340f;
        [Tooltip("스윕 시간 — 짧게(빠른 섬광)")]
        [SerializeField] private float shineDuration = 0.25f;

        [Header("Screen feedback")]
        [Tooltip("처치 시 패널 UI-space 셰이크 강도(px). 배틀 카메라는 안 건드림.")]
        [SerializeField] private float kickStrength = 11f;
        [Tooltip("처치 시 패널 회전 펀치(도)")]
        [SerializeField] private float kickRotation = 3f;
        [SerializeField] private float kickDuration = 0.3f;
        [Tooltip("마일스톤 간격(점). 통과 시 화면 가장자리 플래시.")]
        [SerializeField] private int milestoneInterval = 100;
        [Tooltip("풀스크린 비네트 스프라이트(가장자리 밝음). Null → 플래시 생략.")]
        [SerializeField] private Sprite vignetteSprite;
        [SerializeField] private Color milestoneColor = new Color(1f, 0.8f, 0.35f, 1f);
        [SerializeField] private float milestoneFlashAlpha = 0.5f;
        [SerializeField] private float milestoneDuration = 0.55f;

        [Header("Sound (SoundManager)")]
        [Tooltip("처치 틱 기본 피치")]
        [SerializeField] private float soundPitchBase = 1f;
        [Tooltip("빠른 연속 처치 시 피치 상한")]
        [SerializeField] private float soundPitchMax = 1.7f;
        [Tooltip("처치당 피치 상승분(연속 시 누적)")]
        [SerializeField] private float soundPitchPerKill = 0.06f;
        [Tooltip("피치 heat 감쇠(1/s) — 처치 멈추면 기본 피치로")]
        [SerializeField] private float soundHeatDecay = 1.4f;

        [Header("Layout")]
        // Sits just below the timer (timer panel: y -12, height 60 -> bottom ~-72).
        [SerializeField] private float topOffset = -76f;
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
        private int _pendingKills;
        private Tween _punchTween;
        private Tween _colorTween;
        private ScoreBurstPool _burstPool;
        private Image _glowImage;
        private RectTransform _glowRect;
        private Image _shineImage;
        private RectTransform _shineRect;
        private float _glowFlash;
        private float _shineT = 2f;
        private float _shineBaseY;
        private Image _vignetteImage;
        private float _milestoneFlash;
        private int _lastMilestone;
        private Tween _kickPosTween;
        private Tween _kickRotTween;
        private float _soundHeat;

        private void Awake()
        {
            BuildCanvas();
            if (_panel != null) _panel.SetActive(false);
        }

        private void OnDisable()
        {
            Unsubscribe();
            StopFeedbackTweens();
        }

        private void Update()
        {
            EnsureSubscribed();

            if (_panel == null || !_panel.activeSelf) return;

            // Count-up roll: ease the shown number toward the target (unscaled so it
            // keeps climbing during the timeScale=0 drag-catcher modal).
            _shownScore = Mathf.Lerp(_shownScore, _targetScore, Mathf.Clamp01(Time.unscaledDeltaTime * rollLerp));
            if (Mathf.Abs(_targetScore - _shownScore) < 0.5f) _shownScore = _targetScore;
            if (_value != null) _value.text = Mathf.CeilToInt(_shownScore).ToString();

            _burstPool?.Tick(Time.unscaledDeltaTime);
            UpdateGlowShine(Time.unscaledDeltaTime);
            if (_soundHeat > 0f)
                _soundHeat = Mathf.Max(0f, _soundHeat - soundHeatDecay * Time.unscaledDeltaTime);
        }

        // Flush accumulated kills once per frame (after all Update() drains), so an
        // AoE wipe that calls OnEnemyKilled many times this frame produces one scaled
        // slam rather than N stacked punches.
        private void LateUpdate()
        {
            if (_panel == null || !_panel.activeSelf) return;
            if (_pendingKills <= 0) return;
            int k = _pendingKills;
            _pendingKills = 0;
            TriggerHit(k);
        }

        // Called by BattleBridge once per enemy kill drained from EnemyKilledEvents.
        // Accumulates only; the visual hit is flushed in LateUpdate (see above).
        public void OnEnemyKilled()
        {
            OnEnemyKilled(pointsPerKill);
        }

        // Overload: 가변 per-kill 점수 (origin 3751a612 — battle-log/가변 스코어링 보존).
        // 병합 유지: 누적만, 연출 트리거는 LateUpdate flush.
        public void OnEnemyKilled(int points)
        {
            _targetScore += Mathf.Max(0, points);
            _pendingKills++;
        }

        private void TriggerHit(int killCount)
        {
            if (_valueRect == null || _value == null) return;

            float intensity = Mathf.Min(1f + Mathf.Max(0, killCount - 1) * multiKillBoost,
                                        Mathf.Max(1f, maxMultiKillBoost));

            // Elastic slam. PunchScale animates around the current scale and returns to
            // it, so reset to 1 first to avoid drift when a prior punch is interrupted.
            if (_punchTween.isAlive) _punchTween.Stop();
            _valueRect.localScale = Vector3.one;
            float strength = Mathf.Max(0f, punchScale - 1f) * intensity;
            _punchTween = Tween.PunchScale(_valueRect, Vector3.one * strength, punchDuration, useUnscaledTime: true);

            // White-hot -> rich gold flash (distinct from the damage numbers' multicolor).
            if (_colorTween.isAlive) _colorTween.Stop();
            _value.color = flashColor;
            _colorTween = Tween.Color(_value, flashColor, baseColor, punchDuration, Ease.OutQuad, useUnscaledTime: true);

            // Radial gold spark burst from the number center (behind the digits).
            if (_burstPool != null)
            {
                Vector2 center = _valueRect.anchoredPosition + new Vector2(0f, -_valueRect.rect.height * 0.5f);
                _burstPool.Emit(center, killCount);
            }

            // Flare the glow and launch a shine sweep.
            _glowFlash = 1f;
            _shineT = 0f;

            // UI-space panel kick (battle camera is never touched).
            if (_panel != null)
            {
                if (_kickPosTween.isAlive) _kickPosTween.Stop();
                if (_kickRotTween.isAlive) _kickRotTween.Stop();
                float ks = kickStrength * intensity;
                _kickPosTween = Tween.ShakeLocalPosition(_panel.transform,
                    new Vector3(ks, ks * 0.6f, 0f), kickDuration, useUnscaledTime: true);
                _kickRotTween = Tween.PunchLocalRotation(_panel.transform,
                    new Vector3(0f, 0f, kickRotation * intensity), kickDuration, useUnscaledTime: true);
            }

            // Milestone edge-flash on crossing each interval (display-only trigger).
            if (milestoneInterval > 0)
            {
                int m = _targetScore / milestoneInterval;
                if (m > _lastMilestone) { _lastMilestone = m; _milestoneFlash = 1f; }
            }

            // Score tick — pitch climbs on rapid consecutive kills (heat), decays over time.
            _soundHeat = Mathf.Min(_soundHeat + killCount * soundPitchPerKill,
                                   Mathf.Max(0f, soundPitchMax - soundPitchBase));
            SoundManager.Instance?.PlayScoreTick(soundPitchBase + _soundHeat);
        }

        private void UpdateGlowShine(float dt)
        {
            if (_glowImage != null)
            {
                if (_glowFlash > 0f)
                    _glowFlash = Mathf.Max(0f, _glowFlash - dt / Mathf.Max(0.0001f, glowFlashDuration));
                var gc = glowColor;
                gc.a = Mathf.Lerp(glowRestAlpha, glowFlashAlpha, _glowFlash);
                _glowImage.color = gc;
                if (_glowRect != null)
                {
                    float s = Mathf.Lerp(1f, glowPulseScale, _glowFlash);
                    _glowRect.localScale = new Vector3(s, s, 1f);
                }
            }

            if (_shineImage != null && _shineT <= 1f)
            {
                _shineT += dt / Mathf.Max(0.0001f, shineDuration);
                float t = Mathf.Clamp01(_shineT);
                if (_shineRect != null)
                    _shineRect.anchoredPosition = new Vector2(
                        Mathf.Lerp(-shineTravel * 0.5f, shineTravel * 0.5f, t), _shineBaseY);
                var sc = shineColor;
                sc.a = shineColor.a * Mathf.Sin(t * Mathf.PI); // fade in then out
                _shineImage.color = sc;
            }

            if (_vignetteImage != null && _milestoneFlash > 0f)
            {
                _milestoneFlash = Mathf.Max(0f, _milestoneFlash - dt / Mathf.Max(0.0001f, milestoneDuration));
                var vc = milestoneColor;
                vc.a = milestoneFlashAlpha * _milestoneFlash * _milestoneFlash; // ease-out fade
                _vignetteImage.color = vc;
            }
        }

        private void StopFeedbackTweens()
        {
            if (_punchTween.isAlive) _punchTween.Stop();
            if (_colorTween.isAlive) _colorTween.Stop();
            if (_valueRect != null) _valueRect.localScale = Vector3.one;
            if (_value != null) _value.color = baseColor;
            _burstPool?.ClearAll();

            _glowFlash = 0f;
            _shineT = 2f;
            if (_glowImage != null) { var gc = glowColor; gc.a = glowRestAlpha; _glowImage.color = gc; }
            if (_glowRect != null) _glowRect.localScale = Vector3.one;
            if (_shineImage != null) { var sc = shineColor; sc.a = 0f; _shineImage.color = sc; }

            if (_kickPosTween.isAlive) _kickPosTween.Stop();
            if (_kickRotTween.isAlive) _kickRotTween.Stop();
            if (_panel != null)
            {
                var prt = (RectTransform)_panel.transform;
                prt.anchoredPosition = new Vector2(0f, topOffset);
                prt.localRotation = Quaternion.identity;
            }
            _milestoneFlash = 0f;
            if (_vignetteImage != null) { var vc = milestoneColor; vc.a = 0f; _vignetteImage.color = vc; }
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
                _pendingKills = 0;
                _lastMilestone = 0;
                _soundHeat = 0f;
                StopFeedbackTweens();
                if (_value != null) { _value.text = "0"; _value.color = baseColor; }
                if (_valueRect != null) _valueRect.localScale = Vector3.one;
                if (_panel != null) _panel.SetActive(true);
            }
            else if (_panel != null)
            {
                _pendingKills = 0;
                StopFeedbackTweens();
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

            _burstPool = new ScoreBurstPool();
            _burstPool.Init((RectTransform)_panel.transform, burst);

            Vector2 valueCenter = _valueRect.anchoredPosition + new Vector2(0f, -_valueRect.rect.height * 0.5f);
            _shineBaseY = valueCenter.y;

            // Soft radial glow behind the number (subtle at rest, flares on each hit).
            _glowImage = MakeImage("Glow", _panel.transform, glowSprite);
            _glowRect = _glowImage.rectTransform;
            _glowRect.anchorMin = new Vector2(0.5f, 1f);
            _glowRect.anchorMax = new Vector2(0.5f, 1f);
            _glowRect.pivot = new Vector2(0.5f, 0.5f);
            _glowRect.anchoredPosition = valueCenter;
            _glowRect.sizeDelta = new Vector2(glowSize, glowSize);
            _glowRect.SetAsFirstSibling(); // behind burst + caption + value
            { var gc = glowColor; gc.a = glowRestAlpha; _glowImage.color = gc; }

            // Diagonal shine streak swept over the number on each hit (on top).
            _shineImage = MakeImage("Shine", _panel.transform, shineSprite);
            _shineRect = _shineImage.rectTransform;
            _shineRect.anchorMin = new Vector2(0.5f, 1f);
            _shineRect.anchorMax = new Vector2(0.5f, 1f);
            _shineRect.pivot = new Vector2(0.5f, 0.5f);
            _shineRect.anchoredPosition = new Vector2(0f, valueCenter.y);
            _shineRect.sizeDelta = new Vector2(shineWidth, 96f);
            _shineRect.localRotation = Quaternion.Euler(0f, 0f, shineTiltDeg);
            _shineRect.SetAsLastSibling();
            { var sc = shineColor; sc.a = 0f; _shineImage.color = sc; }

            // Fullscreen milestone edge-flash vignette (on the canvas, behind the panel).
            _vignetteImage = MakeImage("MilestoneVignette", transform, vignetteSprite);
            var vrt = _vignetteImage.rectTransform;
            vrt.anchorMin = Vector2.zero;
            vrt.anchorMax = Vector2.one;
            vrt.offsetMin = Vector2.zero;
            vrt.offsetMax = Vector2.zero;
            vrt.SetAsFirstSibling();
            { var vc = milestoneColor; vc.a = 0f; _vignetteImage.color = vc; }

            UiLayer.Apply(gameObject);
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

        private Image MakeImage(string name, Transform parent, Sprite sprite)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var img = go.AddComponent<Image>();
            if (sprite != null) img.sprite = sprite;
            if (additiveMaterial != null) img.material = additiveMaterial;
            img.raycastTarget = false;
            return img;
        }
    }
}
