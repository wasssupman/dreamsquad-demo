using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Wassup.UI.Layout;

namespace Wassup.UI.Tutorial
{
    // first-session-tutorial unit 1 — one short message plus one focus target.
    // Everything except Skip is visual-only and never consumes gameplay input.
    public sealed class TutorialGuidanceView : MonoBehaviour
    {
        private const int SortingOrder = 10;
        // unit 8 — 선물 튜토리얼 문구는 GiftPanel(sortingOrder 30) 위에 떠야 한다.
        private const int ElevatedSortingOrder = 40;
        private const float SafeEdgePadding = 20f;
        private const float PointerGap = 30f;
        private const float MessageHorizontalPadding = 56f;
        private const float MessageVerticalPadding = 24f;
        private const float TapCatcherDimAlpha = 0.35f;

        [SerializeField] private TutorialGuidanceStyle style;

        public event Action SkipRequested;
        // unit 11 — 읽고 넘기는 스텝에서만 쓰는 진행 신호.
        public event Action ContinueTapped;

        public float GoalBeatSeconds => Style.goalBeatSeconds;
        public float AwakeningPromptSeconds => Style.awakeningPromptSeconds;
        public float CardInstructionSeconds => Style.cardInstructionSeconds;
        public Color SpawnMarkerColor => Style.spawnMarkerColor;
        public Color GoalMarkerColor => Style.goalMarkerColor;

        private GameObject _overlay;
        private Canvas _canvas;
        private RectTransform _safeRoot;
        private RectTransform _fullBleedRoot;
        private GameObject _tapCatcher;
        private GameObject _messagePanel;
        private TextMeshProUGUI _message;
        private GameObject _skipObject;
        private RectTransform _focusRing;
        private RectTransform _pointer;
        private RectTransform _uiTarget;
        private Sprite _plateSprite;
        private Sprite _focusSprite;
        private Sprite _pointerSprite;
        private TutorialGuidanceStyle _runtimeFallback;
        private float _pulseClock;
        private string _preferredText;
        private float _preferredTextWidth = -1f;
        private float _preferredTextHeight;
        private bool _built;

        private readonly Vector3[] _worldCorners = new Vector3[4];
        private readonly List<WorldPulse> _worldPulses = new List<WorldPulse>();

        private sealed class WorldPulse
        {
            public RectTransform rect;
            public RectTransform label;
            public CanvasGroup group;
            public Camera camera;
            public Vector3 world;
            public float age;
            public float duration;
            public bool persistent;
        }

        private TutorialGuidanceStyle Style
        {
            get
            {
                if (style != null) return style;
                if (_runtimeFallback == null)
                    _runtimeFallback = ScriptableObject.CreateInstance<TutorialGuidanceStyle>();
                return _runtimeFallback;
            }
        }

        private void Awake()
        {
            BuildCanvas();
            Hide();
        }

        public void ShowMessage(string text, bool showSkip)
        {
            if (!_built) BuildCanvas();
            _overlay.SetActive(true);
            _messagePanel.SetActive(!string.IsNullOrEmpty(text));
            _message.text = text ?? string.Empty;
            _skipObject.SetActive(showSkip);
            UpdateMessageLayout();
        }

        public void FocusUi(RectTransform target)
        {
            if (!_built) BuildCanvas();
            _uiTarget = target;
            _focusRing.gameObject.SetActive(target != null && target.gameObject.activeInHierarchy);
            _pointer.gameObject.SetActive(_focusRing.gameObject.activeSelf);
            UpdateUiFocus();
        }

        public void ClearFocus()
        {
            _uiTarget = null;
            if (_focusRing != null) _focusRing.gameObject.SetActive(false);
            if (_pointer != null) _pointer.gameObject.SetActive(false);
        }

        public void PulseWorldPoint(Camera camera, Vector3 worldPosition, float duration = -1f)
        {
            CreateWorldPulse(camera, worldPosition, Style.focusColor, label: null,
                persistent: false,
                duration: duration > 0f ? duration : Mathf.Max(0.2f, Style.goalBeatSeconds * 0.55f));
        }

        // Step 1 map comprehension marker. Unlike PulseWorldPoint this remains
        // until gameplay input explicitly advances the tutorial, so reading speed
        // never determines whether the player gets to see spawn/goal context.
        public void ShowWorldMarker(Camera camera, Vector3 worldPosition, string label, Color color)
        {
            CreateWorldPulse(camera, worldPosition, color, label, persistent: true, duration: 0f);
        }

        public void ClearWorldMarkers() => ClearWorldPulses();

        private void CreateWorldPulse(Camera camera, Vector3 worldPosition, Color color,
            string label, bool persistent, float duration)
        {
            if (!_built) BuildCanvas();
            if (camera == null) return;
            _overlay.SetActive(true);

            var go = new GameObject(persistent ? "WorldMarker" : "WorldPulse",
                typeof(RectTransform), typeof(CanvasGroup), typeof(Image));
            go.transform.SetParent(_safeRoot, false);
            var rect = (RectTransform)go.transform;
            rect.sizeDelta = persistent ? new Vector2(96f, 96f) : new Vector2(86f, 86f);
            var image = go.GetComponent<Image>();
            image.sprite = _focusSprite;
            image.type = Image.Type.Sliced;
            image.color = color;
            image.raycastTarget = false;
            RectTransform labelRect = persistent && !string.IsNullOrEmpty(label)
                ? BuildWorldMarkerLabel(rect, label, color)
                : null;
            _worldPulses.Add(new WorldPulse
            {
                rect = rect,
                label = labelRect,
                group = go.GetComponent<CanvasGroup>(),
                camera = camera,
                world = worldPosition,
                duration = duration,
                persistent = persistent,
            });
            UpdateWorldPulse(_worldPulses[_worldPulses.Count - 1]);
            UiLayer.Apply(go);
        }

        private RectTransform BuildWorldMarkerLabel(RectTransform marker, string label, Color color)
        {
            var plateGo = new GameObject("Label", typeof(RectTransform), typeof(Image));
            plateGo.transform.SetParent(marker, false);
            var plateRect = (RectTransform)plateGo.transform;
            plateRect.anchorMin = plateRect.anchorMax = new Vector2(0.5f, 0.5f);
            plateRect.anchoredPosition = new Vector2(0f, -76f);
            plateRect.sizeDelta = new Vector2(168f, 46f);
            var plate = plateGo.GetComponent<Image>();
            plate.sprite = _plateSprite;
            plate.type = Image.Type.Sliced;
            plate.raycastTarget = false;

            var textGo = new GameObject("Text", typeof(RectTransform));
            textGo.transform.SetParent(plateRect, false);
            var textRect = (RectTransform)textGo.transform;
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = new Vector2(8f, 3f);
            textRect.offsetMax = new Vector2(-8f, -3f);
            var text = textGo.AddComponent<TextMeshProUGUI>();
            if (Style.koreanFont != null) text.font = Style.koreanFont;
            text.text = label;
            text.fontSize = Style.worldMarkerFontSize;
            text.fontStyle = FontStyles.Bold;
            text.color = color;
            text.alignment = TextAlignmentOptions.Center;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.raycastTarget = false;
            return plateRect;
        }

        public void Hide()
        {
            ClearFocus();
            ClearWorldPulses();
            SetTapToContinue(false);
            if (_overlay != null) _overlay.SetActive(false);
        }

        // unit 11 — 읽고 넘기는 스텝 전용 탭 수신. 이 뷰의 다른 요소는 전부
        // raycastTarget=false 라 탭을 받을 지점이 없다.
        //
        // 캐처는 반드시 FullBleedRoot 아래에 둔다. UiCanvasSetup 은 FullBleedRoot →
        // SafeAreaRoot 순으로 자식을 만들고 같은 캔버스에서는 나중 sibling 이 렌더와
        // 레이캐스트를 둘 다 이기므로, SafeAreaRoot 하위인 Skip 버튼이 캐처 위에 남는다.
        // 캐처를 SafeAreaRoot 쪽에 두면 유일한 이탈구가 사라진다.
        //
        // _overlay 와는 생명주기가 분리된 두 번째 상태다. ShowMessage 의
        // _overlay.SetActive(true) 는 캐처를 켜지 않고, 끄는 책임은 Hide() 에만 있다.
        // 잔류하면 화면 전체가 먹통이 되므로 종료 경로를 늘릴 때 함께 확인할 것.
        public void SetTapToContinue(bool active)
        {
            if (!active && _tapCatcher == null) return;
            if (!_built) BuildCanvas();
            EnsureTapCatcher();
            _tapCatcher.SetActive(active);
        }

        private void EnsureTapCatcher()
        {
            if (_tapCatcher != null) return;

            _tapCatcher = new GameObject("TapToContinue", typeof(RectTransform), typeof(Image),
                typeof(OutgameTutorialTapZone));
            var rect = (RectTransform)_tapCatcher.transform;
            rect.SetParent(_fullBleedRoot, false);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            var image = _tapCatcher.GetComponent<Image>();
            // 완전 투명이면 "왜 안 눌리지"가 된다. 약한 dim 으로 읽는 시간임을 보인다.
            image.color = new Color(0f, 0f, 0f, TapCatcherDimAlpha);
            image.raycastTarget = true;

            _tapCatcher.GetComponent<OutgameTutorialTapZone>().Pressed = () => ContinueTapped?.Invoke();
            // BuildCanvas 의 UiLayer.Apply 이후에 lazy 생성되므로 여기서 직접 적용한다
            // (CreateWorldPulse 와 같은 이유).
            UiLayer.Apply(_tapCatcher);
            _tapCatcher.SetActive(false);
        }

        // unit 8 — 선물 튜토리얼 동안만 GiftPanel 위로 올린다. Hide 는 정렬을 건드리지
        // 않으므로 원복은 호출자(FirstSessionTutorialController)의 종료 경로가 명시 수행한다.
        public void SetElevated(bool elevated)
        {
            if (!_built) BuildCanvas();
            if (_canvas != null) _canvas.sortingOrder = elevated ? ElevatedSortingOrder : SortingOrder;
        }

        private void Update()
        {
            if (_overlay == null || !_overlay.activeSelf) return;
            _pulseClock += Time.unscaledDeltaTime;
            UpdateMessageLayout();
            UpdateUiFocus();

            float period = Mathf.Max(0.1f, Style.pulsePeriod);
            float wave = 0.5f + 0.5f * Mathf.Sin(_pulseClock * Mathf.PI * 2f / period);
            float scale = Mathf.Lerp(1f, Style.pulseScale, wave);
            if (_focusRing != null && _focusRing.gameObject.activeSelf)
            {
                _focusRing.localScale = Vector3.one * scale;
                _pointer.anchoredPosition = TutorialSafeAreaLayout.PlacePointer(
                    _safeRoot.rect, _focusRing.anchoredPosition, _focusRing.sizeDelta * scale,
                    _pointer.sizeDelta, PointerGap, wave * Style.pointerBob, SafeEdgePadding);
            }

            for (int i = _worldPulses.Count - 1; i >= 0; i--)
            {
                var pulse = _worldPulses[i];
                pulse.age += Time.unscaledDeltaTime;
                if (!pulse.persistent && pulse.age >= pulse.duration)
                {
                    Destroy(pulse.rect.gameObject);
                    _worldPulses.RemoveAt(i);
                    continue;
                }
                UpdateWorldPulse(pulse);
                if (pulse.persistent)
                {
                    float reveal = Mathf.Clamp01(pulse.age / 0.22f);
                    float intro = pulse.age < 0.9f
                        ? Mathf.Sin(Mathf.Clamp01(pulse.age / 0.9f) * Mathf.PI) * 0.28f
                        : 0f;
                    pulse.group.alpha = reveal;
                    pulse.rect.localScale = Vector3.one * (Mathf.Lerp(1f, 1.07f, wave) + intro);
                }
                else
                {
                    float t = Mathf.Clamp01(pulse.age / pulse.duration);
                    pulse.group.alpha = 1f - t;
                    pulse.rect.localScale = Vector3.one * Mathf.Lerp(0.72f, 1.35f, t);
                }
            }
        }

        private void UpdateUiFocus()
        {
            if (_uiTarget == null || !_uiTarget.gameObject.activeInHierarchy)
            {
                if (_focusRing != null) _focusRing.gameObject.SetActive(false);
                if (_pointer != null) _pointer.gameObject.SetActive(false);
                return;
            }

            _uiTarget.GetWorldCorners(_worldCorners);
            var targetCanvas = _uiTarget.GetComponentInParent<Canvas>();
            Camera eventCamera = targetCanvas != null && targetCanvas.renderMode != RenderMode.ScreenSpaceOverlay
                ? targetCanvas.worldCamera : null;
            Vector2 min = new Vector2(float.MaxValue, float.MaxValue);
            Vector2 max = new Vector2(float.MinValue, float.MinValue);
            for (int i = 0; i < 4; i++)
            {
                Vector2 screen = RectTransformUtility.WorldToScreenPoint(eventCamera, _worldCorners[i]);
                if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(_safeRoot, screen, null, out var local))
                    continue;
                min = Vector2.Min(min, local);
                max = Vector2.Max(max, local);
            }
            if (min.x == float.MaxValue) return;

            float maxScale = Mathf.Max(1f, Style.pulseScale);
            Vector2 requestedSize = (max - min) + Vector2.one * (Style.focusPadding * 2f);
            Vector2 fittedSize = TutorialSafeAreaLayout.FitSize(
                _safeRoot.rect, requestedSize, SafeEdgePadding, maxScale);
            Vector2 center = TutorialSafeAreaLayout.ClampCenter(
                _safeRoot.rect, (min + max) * 0.5f, fittedSize, SafeEdgePadding, maxScale);

            _focusRing.gameObject.SetActive(true);
            _pointer.gameObject.SetActive(true);
            _focusRing.anchoredPosition = center;
            _focusRing.sizeDelta = fittedSize;
        }

        private void UpdateWorldPulse(WorldPulse pulse)
        {
            if (pulse.camera == null || pulse.rect == null) return;
            Vector3 screen = pulse.camera.WorldToScreenPoint(pulse.world);
            Vector2 local = default;
            bool converted = screen.z > 0f &&
                RectTransformUtility.ScreenPointToLocalPointInRectangle(_safeRoot, screen, null, out local);
            float radius = Mathf.Max(pulse.rect.sizeDelta.x, pulse.rect.sizeDelta.y) * 0.5f * 1.35f;
            float inset = pulse.persistent ? SafeEdgePadding : radius + SafeEdgePadding;
            bool visible = converted && TutorialSafeAreaLayout.ContainsWithInset(
                _safeRoot.rect, local, inset);
            pulse.rect.gameObject.SetActive(visible);
            if (!visible) return;

            pulse.rect.anchoredPosition = local;
            if (pulse.label != null)
            {
                float halfWidth = pulse.label.sizeDelta.x * 0.5f;
                float labelCenterX = Mathf.Clamp(local.x,
                    _safeRoot.rect.xMin + halfWidth + SafeEdgePadding,
                    _safeRoot.rect.xMax - halfWidth - SafeEdgePadding);
                bool placeAbove = local.y - 76f - pulse.label.sizeDelta.y * 0.5f <
                    _safeRoot.rect.yMin + SafeEdgePadding;
                pulse.label.anchoredPosition = new Vector2(
                    labelCenterX - local.x, placeAbove ? 76f : -76f);
            }
        }

        private void UpdateMessageLayout()
        {
            if (_safeRoot == null || _messagePanel == null) return;
            var rect = (RectTransform)_messagePanel.transform;
            Vector2 requestedSize = Style.messageSize;
            if (_message != null && !string.IsNullOrEmpty(_message.text))
            {
                float panelWidth = Mathf.Min(requestedSize.x,
                    Mathf.Max(1f, _safeRoot.rect.width - SafeEdgePadding * 2f));
                float textWidth = Mathf.Max(1f, panelWidth - MessageHorizontalPadding);
                if (_preferredText != _message.text || !Mathf.Approximately(_preferredTextWidth, textWidth))
                {
                    _preferredText = _message.text;
                    _preferredTextWidth = textWidth;
                    _preferredTextHeight = _message.GetPreferredValues(_message.text, textWidth, 0f).y;
                }
                requestedSize.y = Mathf.Max(requestedSize.y,
                    _preferredTextHeight + MessageVerticalPadding);
            }
            Vector2 size = TutorialSafeAreaLayout.FitSize(
                _safeRoot.rect, requestedSize, SafeEdgePadding, 1f);
            rect.sizeDelta = size;

            // The panel uses a top pivot. Keep the authored offset when possible,
            // but never allow either edge to leave the current device safe rect.
            float maxTopOffset = Mathf.Max(SafeEdgePadding,
                _safeRoot.rect.height - size.y - SafeEdgePadding);
            float topOffset = Mathf.Clamp(Style.messageTopOffset, SafeEdgePadding, maxTopOffset);
            rect.anchoredPosition = new Vector2(0f, -topOffset);
        }

        private void BuildCanvas()
        {
            if (_built) return;
            _built = true;
            var roots = UiCanvasSetup.Ensure(gameObject, SortingOrder);
            _canvas = roots.Canvas;
            _safeRoot = roots.SafeAreaRoot;
            _fullBleedRoot = roots.FullBleedRoot;

            if (style == null)
                Debug.LogWarning("[TutorialGuidanceView] style 미할당 — 런타임 기본값으로 폴백합니다.", this);
            else if (style.koreanFont == null)
                Debug.LogWarning("[TutorialGuidanceView] Korean TMP font 미할당 — 기본 TMP 폰트로 폴백합니다.", this);

            _overlay = new GameObject("TutorialOverlay", typeof(RectTransform));
            _overlay.transform.SetParent(_safeRoot, false);
            var ort = (RectTransform)_overlay.transform;
            ort.anchorMin = Vector2.zero;
            ort.anchorMax = Vector2.one;
            ort.offsetMin = Vector2.zero;
            ort.offsetMax = Vector2.zero;

            _plateSprite = UiRoundedSprite.Make(24f, 4f, Style.messageFill, Style.messageBorder);
            _focusSprite = UiRoundedSprite.Make(22f, Style.focusBorder, Color.clear, Color.white);
            _pointerSprite = UiRoundedSprite.MakeCircle(32, Color.white, 4f, Style.messageFill);

            _messagePanel = new GameObject("Message", typeof(RectTransform), typeof(Image));
            _messagePanel.transform.SetParent(_overlay.transform, false);
            var mrt = (RectTransform)_messagePanel.transform;
            mrt.anchorMin = mrt.anchorMax = new Vector2(0.5f, 1f);
            mrt.pivot = new Vector2(0.5f, 1f);
            mrt.anchoredPosition = new Vector2(0f, -Style.messageTopOffset);
            mrt.sizeDelta = Style.messageSize;
            var messageImage = _messagePanel.GetComponent<Image>();
            messageImage.sprite = _plateSprite;
            messageImage.type = Image.Type.Sliced;
            messageImage.raycastTarget = false;

            var textGo = new GameObject("Text", typeof(RectTransform));
            textGo.transform.SetParent(_messagePanel.transform, false);
            var trt = (RectTransform)textGo.transform;
            trt.anchorMin = Vector2.zero;
            trt.anchorMax = Vector2.one;
            trt.offsetMin = new Vector2(28f, 12f);
            trt.offsetMax = new Vector2(-28f, -12f);
            _message = textGo.AddComponent<TextMeshProUGUI>();
            if (Style.koreanFont != null) _message.font = Style.koreanFont;
            _message.fontSize = Style.messageFontSize;
            _message.fontStyle = FontStyles.Bold;
            _message.color = Style.messageText;
            _message.alignment = TextAlignmentOptions.Center;
            _message.textWrappingMode = TextWrappingModes.Normal;
            _message.raycastTarget = false;

            _skipObject = new GameObject("Skip", typeof(RectTransform), typeof(Image), typeof(Button));
            _skipObject.transform.SetParent(_overlay.transform, false);
            var srt = (RectTransform)_skipObject.transform;
            srt.anchorMin = srt.anchorMax = new Vector2(1f, 1f);
            srt.pivot = new Vector2(1f, 1f);
            srt.anchoredPosition = new Vector2(-28f, -28f);
            srt.sizeDelta = new Vector2(176f, 62f);
            var skipImage = _skipObject.GetComponent<Image>();
            skipImage.sprite = _plateSprite;
            skipImage.type = Image.Type.Sliced;
            skipImage.color = new Color(1f, 1f, 1f, 0.88f);
            var skipButton = _skipObject.GetComponent<Button>();
            skipButton.onClick.AddListener(() => SkipRequested?.Invoke());

            var skipTextGo = new GameObject("Label", typeof(RectTransform));
            skipTextGo.transform.SetParent(_skipObject.transform, false);
            var skipTextRt = (RectTransform)skipTextGo.transform;
            skipTextRt.anchorMin = Vector2.zero;
            skipTextRt.anchorMax = Vector2.one;
            skipTextRt.offsetMin = Vector2.zero;
            skipTextRt.offsetMax = Vector2.zero;
            var skipText = skipTextGo.AddComponent<TextMeshProUGUI>();
            if (Style.koreanFont != null) skipText.font = Style.koreanFont;
            skipText.text = "건너뛰기";
            skipText.fontSize = Style.skipFontSize;
            skipText.fontStyle = FontStyles.Bold;
            skipText.color = Style.messageText;
            skipText.alignment = TextAlignmentOptions.Center;
            skipText.raycastTarget = false;

            var focusGo = new GameObject("FocusRing", typeof(RectTransform), typeof(Image));
            focusGo.transform.SetParent(_overlay.transform, false);
            _focusRing = (RectTransform)focusGo.transform;
            var focusImage = focusGo.GetComponent<Image>();
            focusImage.sprite = _focusSprite;
            focusImage.type = Image.Type.Sliced;
            focusImage.color = Style.focusColor;
            focusImage.raycastTarget = false;

            var pointerGo = new GameObject("Pointer", typeof(RectTransform), typeof(Image));
            pointerGo.transform.SetParent(_overlay.transform, false);
            _pointer = (RectTransform)pointerGo.transform;
            _pointer.sizeDelta = new Vector2(32f, 48f);
            var pointerImage = pointerGo.GetComponent<Image>();
            pointerImage.sprite = _pointerSprite;
            pointerImage.type = Image.Type.Sliced;
            pointerImage.color = Style.focusColor;
            pointerImage.raycastTarget = false;

            UiLayer.Apply(gameObject);
            UpdateMessageLayout();
        }

        private void ClearWorldPulses()
        {
            for (int i = 0; i < _worldPulses.Count; i++)
                if (_worldPulses[i].rect != null) Destroy(_worldPulses[i].rect.gameObject);
            _worldPulses.Clear();
        }

        private void OnDisable() => Hide();

        private void OnDestroy()
        {
            ClearWorldPulses();
            DestroyRuntimeSprite(_plateSprite);
            DestroyRuntimeSprite(_focusSprite);
            DestroyRuntimeSprite(_pointerSprite);
            if (_runtimeFallback != null) Destroy(_runtimeFallback);
        }

        private static void DestroyRuntimeSprite(Sprite sprite)
        {
            if (sprite == null) return;
            if (sprite.texture != null) Destroy(sprite.texture);
            Destroy(sprite);
        }
    }

    internal static class TutorialSafeAreaLayout
    {
        public static Vector2 FitSize(Rect safeRect, Vector2 requested, float padding, float visualScale)
        {
            float scale = Mathf.Max(0.001f, visualScale);
            float maxWidth = Mathf.Max(0f, safeRect.width - padding * 2f) / scale;
            float maxHeight = Mathf.Max(0f, safeRect.height - padding * 2f) / scale;
            return new Vector2(
                Mathf.Min(Mathf.Max(0f, requested.x), maxWidth),
                Mathf.Min(Mathf.Max(0f, requested.y), maxHeight));
        }

        public static Vector2 ClampCenter(Rect safeRect, Vector2 center, Vector2 size,
            float padding, float visualScale)
        {
            float scale = Mathf.Max(0.001f, visualScale);
            Vector2 half = size * (0.5f * scale);
            return new Vector2(
                ClampAxis(center.x, safeRect.xMin + padding + half.x,
                    safeRect.xMax - padding - half.x),
                ClampAxis(center.y, safeRect.yMin + padding + half.y,
                    safeRect.yMax - padding - half.y));
        }

        public static Vector2 PlacePointer(Rect safeRect, Vector2 ringCenter, Vector2 ringSize,
            Vector2 pointerSize, float gap, float bob, float padding)
        {
            float halfPointerY = pointerSize.y * 0.5f;
            float above = ringCenter.y + ringSize.y * 0.5f + gap + bob;
            float below = ringCenter.y - ringSize.y * 0.5f - gap - bob;
            float minY = safeRect.yMin + padding + halfPointerY;
            float maxY = safeRect.yMax - padding - halfPointerY;
            float y = above <= maxY ? above : below >= minY ? below : ClampAxis(above, minY, maxY);
            float halfPointerX = pointerSize.x * 0.5f;
            float x = ClampAxis(ringCenter.x,
                safeRect.xMin + padding + halfPointerX,
                safeRect.xMax - padding - halfPointerX);
            return new Vector2(x, y);
        }

        public static bool ContainsWithInset(Rect safeRect, Vector2 point, float inset)
        {
            if (safeRect.width < inset * 2f || safeRect.height < inset * 2f) return false;
            return point.x >= safeRect.xMin + inset && point.x <= safeRect.xMax - inset &&
                   point.y >= safeRect.yMin + inset && point.y <= safeRect.yMax - inset;
        }

        private static float ClampAxis(float value, float min, float max) =>
            min <= max ? Mathf.Clamp(value, min, max) : (min + max) * 0.5f;
    }
}
