using PrimeTween;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Wassup.Bridge;
using Wassup.Core;
using Wassup.UI.Layout;

namespace Wassup.UI
{
    // battle-hud-score-timer-menu Unit 1 — bottom-left combo dock. Top row shows the
    // match countdown (moved off the top-center, which the score now owns).
    //
    // three-minute-survival unit 2 — 아래 행은 **더 이상 버튼이 아니다**. 웨이브 당기기가
    // 사라졌으므로(전멸하면 즉시 다음, 못 잡으면 상한 간격에 자동) 같은 자리에서 진행 상황만
    // 읽어준다: `웨이브 N / M` + 다음 웨이브까지 남은 초. 클릭·프레스 피드백·클리어 어필
    // (nextwave-clear-attention)은 전부 제거됐다 — 누를 것이 없으면 어필도 거짓말이 된다.
    //
    // BattleBridge 는 읽기 전용 웨이브 상태만 노출한다(NextWaveAvailable / NextWaveHasNext /
    // NextWaveNumber / WaveCountTotal / NextWaveSecondsRemaining).
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

        [Header("Wave progress plate")]
        [SerializeField] private Sprite dockFrameSprite;
        [SerializeField] private Sprite buttonFaceSprite;
        [SerializeField] private Color buttonColor = new Color(0.12f, 0.42f, 0.82f, 0.95f);
        [SerializeField] private float buttonFontSize = 28f;
        [SerializeField] private float buttonMinFontSize = 22f;
        [Tooltip("x=left, y=top, z=right, w=bottom")]
        [SerializeField] private Vector4 buttonContentPadding = new Vector4(24f, 14f, 24f, 14f);
        [Tooltip("다음 웨이브 잔여 초 라벨 색")]
        [SerializeField] private Color countdownColor = new Color(0.62f, 0.95f, 1f, 0.95f);
        [SerializeField] private float countdownFontSize = 22f;

        [Header("Backing")]
        [Tooltip("타이머 캡슐 Sprite 누락 시 대체색")]
        [SerializeField] private Color backingColor = new Color(0f, 0f, 0f, 0.45f);

        private GameObject _panel;
        private Image _backingImage;
        private TextMeshProUGUI _timerCaption;
        private TextMeshProUGUI _timerLabel;
        private GameObject _plateRoot;
        private Image _plateImage;
        private TextMeshProUGUI _waveLabel;
        private TextMeshProUGUI _countdownLabel;
        private bool _built;

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
            if (_tickTween.isAlive) _tickTween.Stop();
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

        // The dock (match countdown + wave progress) is a Battle-phase readout: shown only
        // during Battle, hidden in Draft/Placement/None. At game-over the phase stays
        // Battle, but the result overlay covers the dock.
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

            // Wave progress row — visible only for generated-wave battles.
            bool available = bridge.NextWaveAvailable;
            if (_plateRoot != null && _plateRoot.activeSelf != available)
                _plateRoot.SetActive(available);
            if (!available) return;

            bool hasNext = bridge.NextWaveHasNext;
            if (_waveLabel != null)
            {
                int total = bridge.WaveCountTotal;
                // **진행 중인** 웨이브 번호다. `NextWaveNumber`(= _nextWaveIndex + 1)는 "다음에
                // 나올" 번호라 그대로 쓰면 3번 웨이브와 싸우는 동안 `웨이브 4` 가 뜬다.
                // 마지막 웨이브가 큐잉된 뒤에는 hasNext 가 false 이고 이 식이 total 을 준다.
                int current = Mathf.Clamp(bridge.NextWaveNumber - 1, 1, Mathf.Max(1, total));
                _waveLabel.text = total > 0 ? $"웨이브 {current} / {total}" : $"웨이브 {current}";
            }
            if (_countdownLabel != null)
            {
                float toNext = bridge.NextWaveSecondsRemaining;
                bool showCountdown = hasNext && toNext > 0f;
                if (_countdownLabel.gameObject.activeSelf != showCountdown)
                    _countdownLabel.gameObject.SetActive(showCountdown);
                if (showCountdown)
                    _countdownLabel.text = $"다음 {Mathf.CeilToInt(toNext)}초";
            }
        }

        private void BuildCanvas()
        {
            if (_built) return;
            _built = true;

            var roots = UiCanvasSetup.Ensure(gameObject, sortingOrder: 7);

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

            // 진행 표시 플레이트 — 구 NextWaveButton 의 자리·크기·아트를 그대로 쓰되
            // Button/EventTrigger 를 붙이지 않는다(raycast 대상도 아니다).
            _plateRoot = new GameObject("WaveProgressPlate", typeof(RectTransform), typeof(Image));
            _plateRoot.transform.SetParent(_panel.transform, false);
            var brt = (RectTransform)_plateRoot.transform;
            brt.anchorMin = Vector2.zero;
            brt.anchorMax = Vector2.zero;
            brt.pivot = Vector2.zero;
            brt.anchoredPosition = Vector2.zero;
            brt.sizeDelta = buttonSize;
            _plateImage = _plateRoot.GetComponent<Image>();
            _plateImage.sprite = buttonFaceSprite;
            _plateImage.color = buttonFaceSprite != null ? Color.white : buttonColor;
            _plateImage.preserveAspect = buttonFaceSprite != null;
            _plateImage.raycastTarget = false;

            var labelGO = new GameObject("WaveLabel", typeof(RectTransform));
            labelGO.transform.SetParent(_plateRoot.transform, false);
            var lrt = (RectTransform)labelGO.transform;
            lrt.anchorMin = new Vector2(0f, 0.42f);
            lrt.anchorMax = new Vector2(1f, 1f);
            lrt.offsetMin = new Vector2(buttonContentPadding.x, 0f);
            lrt.offsetMax = new Vector2(-buttonContentPadding.z, -buttonContentPadding.y);
            _waveLabel = labelGO.AddComponent<TextMeshProUGUI>();
            _waveLabel.text = "웨이브 1";
            _waveLabel.fontSize = buttonFontSize;
            _waveLabel.enableAutoSizing = true;
            _waveLabel.fontSizeMin = Mathf.Min(buttonMinFontSize, buttonFontSize);
            _waveLabel.fontSizeMax = buttonFontSize;
            _waveLabel.color = Color.white;
            _waveLabel.fontStyle = FontStyles.Bold;
            _waveLabel.alignment = TextAlignmentOptions.Center;
            _waveLabel.raycastTarget = false;
            ApplyOutline(_waveLabel, new Color(0.02f, 0.08f, 0.2f, 1f), 0.16f);

            var countdownGO = new GameObject("Countdown", typeof(RectTransform));
            countdownGO.transform.SetParent(_plateRoot.transform, false);
            var crt = (RectTransform)countdownGO.transform;
            crt.anchorMin = new Vector2(0f, 0f);
            crt.anchorMax = new Vector2(1f, 0.42f);
            crt.offsetMin = new Vector2(buttonContentPadding.x, buttonContentPadding.w);
            crt.offsetMax = new Vector2(-buttonContentPadding.z, 0f);
            _countdownLabel = countdownGO.AddComponent<TextMeshProUGUI>();
            _countdownLabel.text = "다음 20초";
            _countdownLabel.fontSize = countdownFontSize;
            _countdownLabel.color = countdownColor;
            _countdownLabel.alignment = TextAlignmentOptions.Center;
            _countdownLabel.raycastTarget = false;
            ApplyOutline(_countdownLabel, new Color(0.02f, 0.08f, 0.2f, 1f), 0.12f);

            _plateRoot.SetActive(false);

            UiLayer.Apply(gameObject);
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
