using System;
using System.Collections.Generic;
using PrimeTween;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Wassup.Data;

namespace Wassup.UI.Draft
{
    // "Incoming Waves" announcement panel.
    // Unroll(): dramatic drop + staggered card fade (auto-called at draft start).
    // FadeIn(): soft reveal — used when toggle re-opens the panel after dwell.
    // Roll(): fly-up exit → SnapHidden.
    // Cards are displayed in a horizontal ScrollRect (left-aligned, swipeable).
    public class WavePatternStripView : MonoBehaviour
    {
        public enum State { Hidden, Unrolling, Shown, Rolling }

        [SerializeField] private AttackDeck deck;

        // Header: top-anchored. Rests 100px below screen top; slams in from above.
        private const float HeaderRestY      = -100f;
        private const float HeaderStartY     =  150f;   // above top edge → fully off-screen
        private const float HeaderDropDurSec =   0.40f;

        // Card scroll view: left-anchored, avoids right-side skill panel (240px wide).
        private const float CardGridRestX    =  24f;
        private const float CardGridRestY    = -30f;
        private const float ScrollViewWidth  = 1600f;   // 1920 ref - left margin - skill panel gap
        private const float CardWidth        = 260f;
        private const float CardHeight       = 160f;
        private const float CardSpacing      =  16f;
        private const float CardStaggerSec   =  0.06f;
        private const float CardFadeDurSec   =  0.30f;

        private const float OverlayAlpha         = 0.60f;
        private const float OverlayFadeInDurSec  = 0.15f;
        private const float OverlayFadeDurSec    = 0.20f;
        private const float ExitDurSec           = 0.38f;
        private const float CardGridExitY        = 700f;  // well above screen center → off-screen

        private CanvasGroup _overlayGroup;
        private RectTransform _headerRect;
        private CanvasGroup _headerGroup;
        private RectTransform _cardGrid;     // ScrollRect root (for position/alpha tweening)
        private RectTransform _cardContent;  // HorizontalLayoutGroup content (cards go here)
        private CanvasGroup _cardGridGroup;
        private Button _toggleButton;
        private readonly List<RectTransform> _cardRects = new();
        private State _state = State.Hidden;
        private Sequence _activeTween;
        private bool _toggleEnabled = true;
        private bool _built;

        private static readonly Color TankerColor = new(1f, 0.3f, 0.3f, 1f);
        private static readonly Color BasicColor  = new(0.55f, 0.25f, 0.8f, 1f);
        private static readonly Color SwiftColor  = new(0.95f, 0.85f, 0.2f, 1f);

        public State CurrentState => _state;
        public event Action OnDwellInterrupt;

        // ── Public API ────────────────────────────────────────────────────────

        public void RebuildFromDeck()
        {
            if (!_built) Build();
            ClearCards();
            if (deck == null) return;

            GeneratedWavePlan plan;
            try { plan = WavePatternGenerator.Generate(deck); }
            catch (Exception ex) { AddMessageCard("WAVE PREVIEW UNAVAILABLE", ex.Message); return; }

            for (int i = 0; i < plan.waves.Count; i++) AddWaveCard(plan.waves[i]);
        }

        // Dramatic entry: header drops from top, cards stagger-fade in left→right.
        public Sequence Unroll()
        {
            if (!_built) Build();
            _activeTween.Stop();
            _state = State.Unrolling;

            // Reset to start positions.
            _overlayGroup.alpha = 0f;
            _overlayGroup.gameObject.SetActive(true);
            _headerGroup.alpha = 0f;
            _headerRect.anchoredPosition = new Vector2(0f, HeaderStartY);
            _headerRect.localScale = Vector3.one;
            _cardGridGroup.alpha = 1f;
            _cardGrid.anchoredPosition = new Vector2(CardGridRestX, CardGridRestY);
            if (_cardContent != null) _cardContent.anchoredPosition = Vector2.zero; // scroll to start

            for (int i = 0; i < _cardRects.Count; i++)
            {
                var rt = _cardRects[i];
                if (rt == null) continue;
                rt.localScale = Vector3.one;
                var cg = rt.GetComponent<CanvasGroup>();
                if (cg != null) cg.alpha = 0f;
            }

            var seq = Sequence.Create();

            // 1) Overlay dim.
            seq.Group(Tween.Alpha(_overlayGroup, OverlayAlpha, OverlayFadeInDurSec, Ease.OutQuad));

            // 2) Header drop + shake.
            seq.Group(Tween.UIAnchoredPosition(_headerRect, new Vector2(0f, HeaderRestY), HeaderDropDurSec, Ease.OutBounce, startDelay: 0.05f));
            seq.Group(Tween.Alpha(_headerGroup, 1f, 0.20f, Ease.OutQuad, startDelay: 0.05f));
            seq.Group(Tween.ShakeLocalPosition(_headerRect, new Vector3(8f, 0f, 0f), 0.12f, frequency: 24f, startDelay: 0.05f + HeaderDropDurSec));

            // 3) Cards staggered alpha fade-in (left→right). Alpha on CanvasGroup
            //    avoids fighting with HorizontalLayoutGroup position management.
            for (int i = 0; i < _cardRects.Count; i++)
            {
                var rt = _cardRects[i];
                if (rt == null) continue;
                float delay = 0.20f + i * CardStaggerSec;
                var cg = rt.GetComponent<CanvasGroup>();
                if (cg != null)
                    seq.Group(Tween.Alpha(cg, 1f, CardFadeDurSec, Ease.OutQuad, startDelay: delay));
                seq.Group(Tween.PunchScale(rt, new Vector3(0.12f, 0.12f, 0f), 0.18f, frequency: 6f,
                    startDelay: delay + CardFadeDurSec - 0.04f));
            }

            // 4) Group pulse after all cards appear.
            float lastArrival = 0.20f + (_cardRects.Count > 0 ? (_cardRects.Count - 1) * CardStaggerSec : 0f) + CardFadeDurSec;
            float pulseStart  = lastArrival + 0.10f;
            for (int i = 0; i < _cardRects.Count; i++)
            {
                var rt = _cardRects[i];
                if (rt == null) continue;
                seq.Group(Tween.PunchScale(rt, new Vector3(0.05f, 0.05f, 0f), 0.18f, frequency: 4f, startDelay: pulseStart));
            }

            seq.OnComplete(() => _state = State.Shown);
            _activeTween = seq;
            return _activeTween;
        }

        // Soft re-open: simple alpha fade, positions already at rest from SnapHidden.
        public Sequence FadeIn()
        {
            if (!_built) Build();
            _activeTween.Stop();
            _state = State.Unrolling;

            _overlayGroup.gameObject.SetActive(true);

            var seq = Sequence.Create();
            seq.Group(Tween.Alpha(_overlayGroup, OverlayAlpha, 0.25f, Ease.OutQuad));
            seq.Group(Tween.Alpha(_headerGroup, 1f, 0.25f, Ease.OutQuad, startDelay: 0.05f));
            seq.Group(Tween.Alpha(_cardGridGroup, 1f, 0.25f, Ease.OutQuad, startDelay: 0.05f));
            seq.OnComplete(() => _state = State.Shown);
            _activeTween = seq;
            return _activeTween;
        }

        // Exit: header + cards fly off-screen top; overlay fades out.
        // No alpha fade on content — physical off-screen movement is the exit.
        public Sequence Roll()
        {
            if (!_built) Build();
            _activeTween.Stop();
            _state = State.Rolling;

            var seq = Sequence.Create();
            seq.Group(Tween.UIAnchoredPosition(_headerRect, new Vector2(0f, HeaderStartY), ExitDurSec, Ease.InCubic));
            seq.Group(Tween.UIAnchoredPosition(_cardGrid, new Vector2(CardGridRestX, CardGridExitY), ExitDurSec, Ease.InCubic));
            seq.Group(Tween.Alpha(_overlayGroup, 0f, OverlayFadeDurSec, Ease.InQuad, startDelay: ExitDurSec - OverlayFadeDurSec));
            seq.OnComplete(() => SnapHidden());
            _activeTween = seq;
            return _activeTween;
        }

        // Instant hide; positions stay at rest so FadeIn() only needs to tween alpha.
        // Overlay is deactivated to prevent input blocking during the draft phase.
        public void SnapHidden()
        {
            if (!_built) Build();
            _activeTween.Stop();
            _overlayGroup.gameObject.SetActive(false);
            _overlayGroup.alpha = 0f;
            _headerGroup.alpha = 0f;
            _headerRect.anchoredPosition = new Vector2(0f, HeaderRestY);
            _headerRect.localScale = Vector3.one;
            _cardGridGroup.alpha = 0f;
            _cardGrid.anchoredPosition = new Vector2(CardGridRestX, CardGridRestY);
            if (_cardContent != null) _cardContent.anchoredPosition = Vector2.zero;
            _state = State.Hidden;
        }

        public void SetToggleEnabled(bool value)
        {
            _toggleEnabled = value;
            if (_toggleButton != null) _toggleButton.interactable = value;
        }

        // ── Unity lifecycle ───────────────────────────────────────────────────

        private void Awake()
        {
            if (!_built) Build();
            SnapHidden();
        }

        private void OnDisable()
        {
            Tween.StopAll(this);
        }

        // ── Build ─────────────────────────────────────────────────────────────

        private void Build()
        {
            if (_built) return;
            _built = true;

            var selfRt = (RectTransform)transform;
            selfRt.anchorMin = Vector2.zero;
            selfRt.anchorMax = Vector2.one;
            selfRt.offsetMin = Vector2.zero;
            selfRt.offsetMax = Vector2.zero;

            // Overlay (full-screen dim + background tap → dwell interrupt).
            var overlayGo = new GameObject("Overlay", typeof(RectTransform), typeof(Image), typeof(CanvasGroup));
            overlayGo.transform.SetParent(transform, false);
            var overlayRt = (RectTransform)overlayGo.transform;
            overlayRt.anchorMin = Vector2.zero;
            overlayRt.anchorMax = Vector2.one;
            overlayRt.offsetMin = Vector2.zero;
            overlayRt.offsetMax = Vector2.zero;
            overlayGo.GetComponent<Image>().color = Color.black;
            var overlayBtn = overlayGo.AddComponent<Button>();
            overlayBtn.transition = Selectable.Transition.None;
            overlayBtn.onClick.AddListener(OnBackgroundClicked);
            _overlayGroup = overlayGo.GetComponent<CanvasGroup>();

            // Header: top-center anchor, drops 100px from screen top.
            var headerGo = new GameObject("Header", typeof(RectTransform), typeof(CanvasGroup));
            headerGo.transform.SetParent(transform, false);
            _headerRect = (RectTransform)headerGo.transform;
            _headerRect.anchorMin = new Vector2(0.5f, 1f);
            _headerRect.anchorMax = new Vector2(0.5f, 1f);
            _headerRect.pivot     = new Vector2(0.5f, 0.5f);
            _headerRect.anchoredPosition = new Vector2(0f, HeaderRestY);
            _headerRect.sizeDelta = new Vector2(900f, 110f);
            _headerGroup = headerGo.GetComponent<CanvasGroup>();

            var headerTmp = headerGo.AddComponent<TextMeshProUGUI>();
            headerTmp.text = "INCOMING WAVES";
            headerTmp.fontSize = 78;
            headerTmp.fontStyle = FontStyles.Bold;
            headerTmp.color = new Color(1f, 0.86f, 0.24f, 1f);
            headerTmp.alignment = TextAlignmentOptions.Center;
            headerTmp.enableWordWrapping = false;
            headerTmp.raycastTarget = false;

            // ScrollRect root (position + alpha tweening target).
            var scrollGo = new GameObject("CardScrollView",
                typeof(RectTransform), typeof(ScrollRect), typeof(Image), typeof(CanvasGroup));
            scrollGo.transform.SetParent(transform, false);
            _cardGrid = (RectTransform)scrollGo.transform;
            _cardGrid.anchorMin = new Vector2(0f, 0.5f);
            _cardGrid.anchorMax = new Vector2(0f, 0.5f);
            _cardGrid.pivot     = new Vector2(0f, 0.5f);
            _cardGrid.anchoredPosition = new Vector2(CardGridRestX, CardGridRestY);
            _cardGrid.sizeDelta = new Vector2(ScrollViewWidth, CardHeight);
            var scrollBg = scrollGo.GetComponent<Image>();
            scrollBg.color = Color.clear;
            scrollBg.raycastTarget = true; // needed for ScrollRect input
            _cardGridGroup = scrollGo.GetComponent<CanvasGroup>();

            // Viewport (clips cards that overflow the scroll area).
            var viewportGo = new GameObject("Viewport", typeof(RectTransform), typeof(Image), typeof(Mask));
            viewportGo.transform.SetParent(scrollGo.transform, false);
            var viewportRt = (RectTransform)viewportGo.transform;
            viewportRt.anchorMin = Vector2.zero;
            viewportRt.anchorMax = Vector2.one;
            viewportRt.offsetMin = Vector2.zero;
            viewportRt.offsetMax = Vector2.zero;
            viewportGo.GetComponent<Image>().color = new Color(1f, 1f, 1f, 0.01f);
            viewportGo.GetComponent<Mask>().showMaskGraphic = false;

            // Content (HorizontalLayoutGroup, expands to fit all cards).
            var contentGo = new GameObject("Content",
                typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(ContentSizeFitter));
            contentGo.transform.SetParent(viewportGo.transform, false);
            _cardContent = (RectTransform)contentGo.transform;
            _cardContent.anchorMin = new Vector2(0f, 0.5f);
            _cardContent.anchorMax = new Vector2(0f, 0.5f);
            _cardContent.pivot     = new Vector2(0f, 0.5f);
            _cardContent.anchoredPosition = Vector2.zero;
            _cardContent.sizeDelta = new Vector2(0f, CardHeight);

            var hlg = contentGo.GetComponent<HorizontalLayoutGroup>();
            hlg.spacing = CardSpacing;
            hlg.childAlignment = TextAnchor.MiddleLeft;
            hlg.childForceExpandWidth = false;
            hlg.childForceExpandHeight = false;
            hlg.padding = new RectOffset(0, (int)CardSpacing, 0, 0); // trailing gap

            var fitter = contentGo.GetComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;

            // Wire ScrollRect.
            var scrollRect = scrollGo.GetComponent<ScrollRect>();
            scrollRect.viewport          = viewportRt;
            scrollRect.content           = _cardContent;
            scrollRect.horizontal        = true;
            scrollRect.vertical          = false;
            scrollRect.movementType      = ScrollRect.MovementType.Elastic;
            scrollRect.elasticity        = 0.1f;
            scrollRect.inertia           = true;
            scrollRect.decelerationRate  = 0.135f;
            scrollRect.scrollSensitivity = 30f;

            // Toggle button: top-left corner, beneath map settings toggle.
            var toggleGo = new GameObject("WaveToggle", typeof(RectTransform), typeof(Image), typeof(Button));
            toggleGo.transform.SetParent(transform, false);
            var toggleRt = (RectTransform)toggleGo.transform;
            toggleRt.anchorMin = new Vector2(0f, 1f);
            toggleRt.anchorMax = new Vector2(0f, 1f);
            toggleRt.pivot     = new Vector2(0f, 1f);
            toggleRt.anchoredPosition = new Vector2(40f, -110f);
            toggleRt.sizeDelta = new Vector2(72f, 72f);
            toggleGo.GetComponent<Image>().color = new Color(0.95f, 0.74f, 0.2f, 1f);
            _toggleButton = toggleGo.GetComponent<Button>();
            _toggleButton.onClick.AddListener(OnToggleClicked);

            var toggleLabelGo = new GameObject("Label", typeof(RectTransform));
            toggleLabelGo.transform.SetParent(toggleGo.transform, false);
            var toggleLabelRt = (RectTransform)toggleLabelGo.transform;
            toggleLabelRt.anchorMin = Vector2.zero;
            toggleLabelRt.anchorMax = Vector2.one;
            toggleLabelRt.offsetMin = Vector2.zero;
            toggleLabelRt.offsetMax = Vector2.zero;
            var toggleTmp = toggleLabelGo.AddComponent<TextMeshProUGUI>();
            toggleTmp.text = "!";
            toggleTmp.fontSize = 48;
            toggleTmp.fontStyle = FontStyles.Bold;
            toggleTmp.color = new Color(0.12f, 0.10f, 0.06f, 1f);
            toggleTmp.alignment = TextAlignmentOptions.Center;
            toggleTmp.raycastTarget = false;
        }

        // ── Event handlers ────────────────────────────────────────────────────

        private void OnBackgroundClicked()
        {
            if (_state == State.Shown) OnDwellInterrupt?.Invoke();
        }

        private void OnToggleClicked()
        {
            if (!_toggleEnabled) return;
            switch (_state)
            {
                case State.Hidden: FadeIn(); break;   // soft re-open; no dwell interrupt
                case State.Shown:  Roll();   break;
            }
        }

        // ── Card helpers ──────────────────────────────────────────────────────

        private void ClearCards()
        {
            for (int i = _cardRects.Count - 1; i >= 0; i--)
                if (_cardRects[i] != null) Destroy(_cardRects[i].gameObject);
            _cardRects.Clear();
        }

        private void AddWaveCard(GeneratedWave wave)
        {
            var go = new GameObject($"Wave_{wave.waveIndex + 1:00}",
                typeof(RectTransform), typeof(Image), typeof(CanvasGroup), typeof(LayoutElement));
            go.transform.SetParent(_cardContent, false);

            var rt = (RectTransform)go.transform;
            rt.sizeDelta = new Vector2(CardWidth, CardHeight);
            var le = go.GetComponent<LayoutElement>();
            le.preferredWidth  = CardWidth;
            le.minWidth        = CardWidth;
            le.preferredHeight = CardHeight;
            go.GetComponent<Image>().color = new Color(0.12f, 0.14f, 0.20f, 0.95f);

            var accentGo = new GameObject("Accent", typeof(RectTransform), typeof(Image));
            accentGo.transform.SetParent(go.transform, false);
            var accentRt = (RectTransform)accentGo.transform;
            accentRt.anchorMin = new Vector2(0f, 0f);
            accentRt.anchorMax = new Vector2(0f, 1f);
            accentRt.pivot     = new Vector2(0f, 0.5f);
            accentRt.anchoredPosition = Vector2.zero;
            accentRt.sizeDelta = new Vector2(8f, 0f);
            accentGo.GetComponent<Image>().color = new Color(1f, 0.86f, 0.24f, 1f);
            accentGo.GetComponent<Image>().raycastTarget = false;

            AddText(go.transform, "Index", $"W{wave.waveIndex + 1:00}", 38, Color.white, bold: true,
                new Vector2(0f, 0.55f), new Vector2(0.55f, 1f),
                new Vector2(20f, 0f), new Vector2(0f, -8f), TextAlignmentOptions.MidlineLeft);

            AddText(go.transform, "Time", $"{wave.triggerTimeSec:0.#}s", 22,
                new Color(0.7f, 0.78f, 0.92f, 1f), bold: false,
                new Vector2(0.55f, 0.6f), new Vector2(1f, 1f),
                new Vector2(0f, 0f), new Vector2(-14f, -6f), TextAlignmentOptions.TopRight);

            AddText(go.transform, "UnitA", $"{UnitName(wave.unitA)} ×{wave.countA}", 24,
                UnitColor(wave.unitA), bold: true,
                new Vector2(0f, 0.28f), new Vector2(1f, 0.58f),
                new Vector2(20f, 0f), new Vector2(-12f, 0f), TextAlignmentOptions.MidlineLeft);

            AddText(go.transform, "UnitB", $"{UnitName(wave.unitB)} ×{wave.countB}", 24,
                UnitColor(wave.unitB), bold: true,
                new Vector2(0f, 0f), new Vector2(1f, 0.30f),
                new Vector2(20f, 8f), new Vector2(-12f, 0f), TextAlignmentOptions.MidlineLeft);

            _cardRects.Add(rt);
        }

        private void AddMessageCard(string title, string message)
        {
            var go = new GameObject("Message",
                typeof(RectTransform), typeof(Image), typeof(CanvasGroup), typeof(LayoutElement));
            go.transform.SetParent(_cardContent, false);
            var rt = (RectTransform)go.transform;
            rt.sizeDelta = new Vector2(420f, CardHeight);
            var le = go.GetComponent<LayoutElement>();
            le.preferredWidth = le.minWidth = 420f;
            le.preferredHeight = CardHeight;
            go.GetComponent<Image>().color = new Color(0.20f, 0.10f, 0.10f, 0.95f);
            AddText(go.transform, "Msg", $"{title}\n{message}", 18, Color.white, bold: false,
                Vector2.zero, Vector2.one,
                new Vector2(16f, 8f), new Vector2(-16f, -8f), TextAlignmentOptions.Center);
            _cardRects.Add(rt);
        }

        private static void AddText(Transform parent, string name, string text, int fontSize, Color color,
            bool bold, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax,
            TextAlignmentOptions align)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.offsetMin = offsetMin;
            rt.offsetMax = offsetMax;
            var tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.fontSize = fontSize;
            tmp.color = color;
            if (bold) tmp.fontStyle = FontStyles.Bold;
            tmp.alignment = align;
            tmp.enableWordWrapping = false;
            tmp.raycastTarget = false;
        }

        private static string UnitName(AttackUnitData unit)
            => unit != null && !string.IsNullOrWhiteSpace(unit.displayName) ? unit.displayName : "?";

        private static Color UnitColor(AttackUnitData unit)
        {
            string name = UnitName(unit);
            if (name.Contains("Tanker")) return TankerColor;
            if (name.Contains("Basic"))  return BasicColor;
            if (name.Contains("Swift"))  return SwiftColor;
            return new Color(0.55f, 0.85f, 1f, 1f);
        }
    }
}
