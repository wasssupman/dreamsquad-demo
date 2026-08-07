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
        // three-minute-survival unit 2 — 표시 카드 상한(스크롤 폭·인트로 길이 보호).
        private const int   MaxCards         =  12;

        private static readonly float OverlayAlpha = UiOverlay.DimAlpha; // 통일 dim (단일 소스)
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
        private readonly List<RectTransform> _cardRects = new();
        private State _state = State.Hidden;
        private Sequence _activeTween;
        private bool _built;
        private Canvas _sortCanvas;   // overrideSorting canvas for popup layering (Unit 3)

        private static readonly Color TankerColor = new(1f, 0.3f, 0.3f, 1f);
        private static readonly Color BasicColor  = new(0.55f, 0.25f, 0.8f, 1f);
        private static readonly Color SwiftColor  = new(0.95f, 0.85f, 0.2f, 1f);

        public State CurrentState => _state;
        public event Action OnDwellInterrupt;

        // ── Public API ────────────────────────────────────────────────────────

        public void RebuildFromDeck()
        {
            if (deck == null) { RebuildFromPlan(default); return; }
            try { RebuildFromPlan(WavePatternGenerator.Generate(deck)); }
            catch (Exception ex)
            {
                if (!_built) Build();
                ClearCards();
                AddMessageCard("웨이브 미리보기 불가", ex.Message);
            }
        }

        // random-map-pool unit 6 — 외부(draft)에서 실전과 동일한 플랜을 넘겨 프리뷰를 만든다.
        // plan.waves==null(default) 이면 build+clear 만(빈 프리뷰).
        public void RebuildFromPlan(GeneratedWavePlan plan)
        {
            if (!_built) Build();
            ClearCards();
            if (plan.waves == null) return;

            // three-minute-survival unit 2 — 카드 수 상한. 웨이브 상한이 100(명목)이 되면서
            // 무제한 루프가 카드 100장을 만들었다: 인트로 = 0.20 + 99×CardStaggerSec + fade
            // ≈ 6.4초, 콘텐츠 폭 27,600px(ScrollViewWidth 1600). 드래프트 화면이 멈춘다.
            // 앞 MaxCards 장만 그린다 — 3분 안에 도달하는 것은 10~16웨이브뿐이라 앞부분이
            // 곧 실제로 만나는 전부다.
            int shown = Mathf.Min(plan.waves.Count, MaxCards);
            for (int i = 0; i < shown; i++) AddWaveCard(plan.waves[i]);
            UiLayer.Apply(gameObject);
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
            SetHiddenRaycasts(true); // SnapHidden 이 껐던 것 복원 — 카드가 다시 스와이프 가능해야 한다
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
            SetHiddenRaycasts(true); // SnapHidden 이 껐던 것 복원 (일시정지 메뉴 재오픈 경로)

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
        //
        // unit-dreamcatcher-inspect fix — header/cardGrid 도 같은 이유로 raycast 를 꺼야 한다.
        // CanvasGroup.alpha=0 은 **화면에서만** 숨긴다. blocksRaycasts 를 안 끄면 보이지 않는
        // 채로 입력을 계속 먹는다. 카드 그리드는 화면 x[24~1624] y[430~590] 를 덮으므로
        // (실측) 전투 중 보드 왼쪽 가로 띠가 통째로 탭 불가가 된다 — 유닛이 이 띠 경계에
        // 걸치면 "머리는 눌리는데 몸통은 안 눌리는" 증상이 된다. 오랫동안 안 걸린 이유는
        // 보드 raw 탭 소비자가 없었기 때문(클릭 배치 은퇴). DcInspectController 가 첫 소비자다.
        public void SnapHidden()
        {
            if (!_built) Build();
            _activeTween.Stop();
            _overlayGroup.gameObject.SetActive(false);
            _overlayGroup.alpha = 0f;
            SetHiddenRaycasts(false);
            _headerGroup.alpha = 0f;
            _headerRect.anchoredPosition = new Vector2(0f, HeaderRestY);
            _headerRect.localScale = Vector3.one;
            _cardGridGroup.alpha = 0f;
            _cardGrid.anchoredPosition = new Vector2(CardGridRestX, CardGridRestY);
            if (_cardContent != null) _cardContent.anchoredPosition = Vector2.zero;
            _state = State.Hidden;
        }

        // 숨김/표시와 raycast 차단을 한 곳에서 묶는다 — 알파만 되돌리고 이걸 잊으면
        // 카드가 안 눌리거나(끈 채 표시) 보드가 막힌다(켠 채 숨김).
        private void SetHiddenRaycasts(bool on)
        {
            if (_headerGroup != null) _headerGroup.blocksRaycasts = on;
            if (_cardGridGroup != null) _cardGridGroup.blocksRaycasts = on;
        }

        // battle-hud-score-timer-menu Unit 3 — lift the whole strip above another
        // overlay canvas (the pause menu popup) while shown, then release. The strip
        // normally renders on the DraftView canvas (low order); the popup needs it on
        // top so its own dim overlay isn't covered. Adds an overrideSorting Canvas +
        // raycaster on demand so the cards stay swipeable at the boosted order.
        public void SetSortingOverride(bool on, int order = 950)
        {
            if (on)
            {
                if (_sortCanvas == null)
                {
                    _sortCanvas = gameObject.GetComponent<Canvas>();
                    if (_sortCanvas == null) _sortCanvas = gameObject.AddComponent<Canvas>();
                    if (gameObject.GetComponent<GraphicRaycaster>() == null)
                        gameObject.AddComponent<GraphicRaycaster>();
                }
                _sortCanvas.overrideSorting = true;
                _sortCanvas.sortingOrder = order;
            }
            else if (_sortCanvas != null)
            {
                _sortCanvas.overrideSorting = false;
            }
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
            headerTmp.text = "다가오는 웨이브";
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

            // battle-hud-score-timer-menu Unit 3 — the in-battle "!" on-demand toggle is
            // retired; the attack pattern is now viewed from the pause menu popup, which
            // drives FadeIn()/Roll() directly.
            UiLayer.Apply(gameObject);
        }

        // ── Event handlers ────────────────────────────────────────────────────

        private void OnBackgroundClicked()
        {
            if (_state == State.Shown) OnDwellInterrupt?.Invoke();
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
            // dark-bg 대응 — near-black 오버레이(UiOverlay.Dim) 위에서 카드가 떠 보이도록
            // 밝은 슬레이트 테두리 + 그보다 어두운 채움의 2단 구조. 단색 카드가 배경에 묻히던 문제 해소.
            go.GetComponent<Image>().color = new Color(0.34f, 0.40f, 0.55f, 1f); // border/frame

            var fillGo = new GameObject("Fill", typeof(RectTransform), typeof(Image));
            fillGo.transform.SetParent(go.transform, false);
            var fillRt = (RectTransform)fillGo.transform;
            fillRt.anchorMin = Vector2.zero;
            fillRt.anchorMax = Vector2.one;
            fillRt.offsetMin = new Vector2(2f, 2f);
            fillRt.offsetMax = new Vector2(-2f, -2f);
            var fillImg = fillGo.GetComponent<Image>();
            fillImg.color = new Color(0.17f, 0.20f, 0.29f, 1f);
            fillImg.raycastTarget = false;

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

            AddText(go.transform, "Time", $"{wave.triggerTimeSec:0.#}초", 22,
                new Color(0.7f, 0.78f, 0.92f, 1f), bold: false,
                new Vector2(0.55f, 0.6f), new Vector2(1f, 1f),
                new Vector2(0f, 0f), new Vector2(-14f, -6f), TextAlignmentOptions.TopRight);

            // wave-authoring-test-mode unit 1 — 2줄 고정 → groups N줄 가변.
            // 하단 영역(y 0~0.58)을 그룹 수로 세로 분할, 그룹 수에 따라 폰트 축소.
            var groups = wave.groups;
            int n = groups != null ? groups.Count : 0;
            float regionTop = 0.58f;
            int fontSize = n <= 2 ? 24 : (n <= 4 ? 18 : 14);
            for (int g = 0; g < n; g++)
            {
                float hi = regionTop * (1f - (float)g / n);
                float lo = regionTop * (1f - (float)(g + 1) / n);
                AddText(go.transform, $"Unit{g}", $"{UnitName(groups[g].unit)} ×{groups[g].count}", fontSize,
                    UnitColor(groups[g].unit), bold: true,
                    new Vector2(0f, lo), new Vector2(1f, hi),
                    new Vector2(20f, 2f), new Vector2(-12f, -2f), TextAlignmentOptions.MidlineLeft);
            }

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
