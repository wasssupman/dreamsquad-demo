using System.Collections;
using System.Collections.Generic;
using PrimeTween;
using TMPro;
using Unity.Entities;
using UnityEngine;
using UnityEngine.UI;
using Wassup.Core;
using Wassup.Core.TimeControl;
using Wassup.Data;
using Wassup.UI.Layout;

namespace Wassup.UI
{
    // dreamcatcher-awakening-hand unit 6 — StS-style hand strip that flips in
    // place of the defender placement strip (mutually exclusive by design).
    //
    // Single owner of the strip↔hand transition state (the gauge button only
    // signals Toggled). While the hand is open a battle slomo lease is held
    // (TimeManager, NEVER 0 — realtime contract); closing disposes it.
    // Phase guard (critic H2): leaving Placement/Battle force-closes the hand,
    // drops any pending drag state, and releases the lease.
    public class DreamcatcherHandView : MonoBehaviour
    {
        [SerializeField] private DreamcatcherHandController handController;
        [SerializeField] private AwakeningGaugeView gaugeView;
        [SerializeField] private DefenderSelector defenderSelector;
        // battle-hud-layout 1 rev — 배지가 중앙 핸드와 겹치므로 핸드 오픈 중
        // 스트립과 함께 배지도 퇴장시킨다. 표시 결정은 CostDisplay 가 소유.
        [SerializeField] private CostDisplay costDisplay;
        [SerializeField] private AwakeningConfig config;
        // unit 7 — card drag targeting (screen ray → board cell → defender).
        [SerializeField] private Wassup.Bridge.BattleBridge bridge;
        [SerializeField] private Camera mainCamera;
        [SerializeField] private TMP_FontAsset labelFont;  // Jua — Korean battle UI
        [SerializeField] private TMP_FontAsset numberFont; // Anton — cost digits
        // action-tray unit 3 — 트레이 공유 외곽 문법(폭/y/fill/border). 미할당 시
        // 기존 단색 배킹 그대로(무회귀 폴백).
        [SerializeField] private Wassup.Data.BattleHudTrayConfig trayConfig;
        [SerializeField] private float flipHalfDuration = 0.14f;
        // hand-deal-in unit 0 — StS/HS 아치 부채 기하 + 스프링 추종.
        [SerializeField] private float cardOverlap = 54f;  // 카드 겹침(step = cardW - overlap)
        [SerializeField] private float arcHeight = 46f;    // 포물선 아치 높이(가운데 솟음)
        [SerializeField] private float rotMax = 10f;       // 바깥 카드 접선 회전(도)
        [SerializeField] private float handBaseY = 16f;    // 부채 밑단 y
        [SerializeField] private float springK = 14f;      // 스프링 추종 계수(클수록 빠름)
        // hand-deal-in unit 2 — 덱-드로우 딜(하단에서 곡선 상승 → 아치 안착).
        [SerializeField] private float dealStaggerSec = 0.05f;
        [SerializeField] private float dealDurationSec = 0.34f;
        [SerializeField] private float dealStartScale = 0.62f;
        [SerializeField] private float trayFadeSec = 0.12f;
        [SerializeField] private float dealRise = 220f;   // 하단 바깥 시작 깊이
        [SerializeField] private float dealTiltX = 50f;    // 누운 카드 → 세움(원근 ①)
        [SerializeField] private float clusterK = 0.3f;    // 시작 x 모임(덱 뭉침)
        // card-crumple-unfold unit 2 — 딜 안착과 동기로 구김이 풀림(살짝 늦게 끝, D3).
        [SerializeField] private float crumpleUnfoldSec = 0.6f;
        [SerializeField] private float textFadeSec = 0.18f;
        // hand-deal-in unit 4 — 퇴장 침강(딜의 거울: 하단 덱으로 InBack).
        [SerializeField] private float sinkDurationSec = 0.26f;
        [SerializeField] private float sinkStaggerSec = 0.04f;
        // hand-deal-in unit 1 — 눌러서 들기(press-to-lift, 모바일: hover 아님).
        [SerializeField] private float focusRaise = 100f;
        [SerializeField] private float focusScale = 1.28f;
        [SerializeField] private float scatter = 42f;      // 양옆 밀어냄
        [SerializeField] private int scatterNeighbors = 2;
        // hand-deal-in unit 3 — 상시 미세 흔들림(무입력 역동감). 0 이면 정지.
        [SerializeField] private float idleBobY = 5f;
        [SerializeField] private float idleSwayX = 3f;
        [SerializeField] private float idleFreq = 1.6f;
        [SerializeField] private float idlePhase = 0.7f;
        // rev 4 — 카드 드래그 중 호버된 수비수의 스파인 틴트(포커스 대상 시각화).
        // rev 4-4 — 붉은색 계열로 시인성 상향(사용자 확정). 유일한 포커스 표시(타일 하이라이트 제거).
        [SerializeField] private Color unitHoverTint = new Color(1f, 0.28f, 0.22f, 1f);

        public enum HandState { UnitStrip, Hand }
        public HandState State { get; private set; } = HandState.UnitStrip;
        // hand-deal-in unit 0 — 딜/수렴 시퀀스도 전이 상태(드래그/토글 가드).
        public bool Transitioning => _flip != null || _dealSeq.isAlive;

        // unit 7 consumes these: card slots for drag sources + hand rect for
        // the cancel-region test.
        public RectTransform HandPanelRect => _panel != null ? (RectTransform)_panel.transform : null;
        public IReadOnlyList<CardSlot> Slots => _slots;

        // Drag-slot service surface (unit 7).
        public DreamcatcherHandController Controller => handController;
        public Wassup.Bridge.BattleBridge Bridge => bridge;
        public Camera MainCamera => mainCamera != null ? mainCamera : (mainCamera = Camera.main);
        public AwakeningConfig Config => config;
        public Color UnitHoverTint => unitHoverTint;
        // rev 4-6 — StS식 유닛 타겟팅 화살표(카드 고정 + 카드→포인터 점선 아크).
        public DreamcatcherTargetArrow TargetArrow => _targetArrow;

        // card-fly-to-target-absorb unit 0 — 커밋 성공 시 손패 카드가 유닛으로
        // 날아가 찰싹 흡수. 발사점/스프라이트는 소비 전 호출부에서 캡처된 값.
        // 타겟은 유닛 뷰 앵커 Transform 을 매프레임 추적(행진 중에도 안착).
        public void FlyCardToUnit(Vector3 startUiWorld, Vector2 ghostSize, Sprite face, Entity host)
        {
            EnsureFlightPresenter();
            if (_flightPresenter == null) return;
            var b = bridge;
            var h = host;
            _flightPresenter.Fly(startUiWorld, ghostSize, face, MainCamera,
                () => (b != null && b.TryGetUnitViewAnchor(h, out var tr) && tr != null)
                    ? tr.position : (Vector3?)null);
        }

        private void EnsureFlightPresenter()
        {
            if (_flightPresenter != null) return;
            var canvas = GetComponentInParent<Canvas>();
            if (canvas == null) return;
            canvas = canvas.rootCanvas != null ? canvas.rootCanvas : canvas;
            var go = new GameObject("CardAbsorbFlight", typeof(RectTransform));
            go.transform.SetParent(canvas.transform, false);
            _flightPresenter = go.AddComponent<CardAbsorbFlightPresenter>();
            _flightPresenter.Init(canvas);
        }

        public class CardSlot
        {
            public GameObject root;
            public RectTransform rect;
            public Image frame;
            public Image art;
            public UiCardFaceMesh face;  // card-crumple-unfold — art = 서브디바이드 크럼플 그래픽
            public GameObject nameTag; // rev 4-5 — 하단 네임 밴드(어두운 배킹+이름, 항상 표시)
            public TextMeshProUGUI nameLabel;
            public CanvasGroup nameGroup; // 크럼플 중 숨겼다 펴짐 완료 시 페이드-인(TMP 비크럼플)
            public GameObject costBadge;
            public TextMeshProUGUI costLabel;
            public CanvasGroup costGroup;
            public CanvasGroup group;
            public DreamcatcherCardDragSlot dragSlot;
            public Vector2 homePos;        // unit 7 — restore anchor after drag/cancel
            public float homeRotZ;
            // hand-deal-in unit 0 — 스프링 목표(호버/복귀). base = home.
            public Vector2 targetPos;
            public float targetRotZ;
            public float targetScale = 1f;
            public int entryId = -1;       // -1 = empty slot
            public DreamcatcherCard card;
            public bool usable;
        }

        private GameObject _panel;
        private Image _backing;        // tray frame — deal 무대(카드와 별개로 페이드 인)
        private float _backingAlpha = 1f;
        private readonly List<CardSlot> _slots = new List<CardSlot>();
        private bool _built;
        private Coroutine _flip;
        private Sequence _dealSeq;      // hand-deal-in — 딜/수렴 트윈(teardown 에서 Stop)
        private int _focusIndex = -1;  // hand-deal-in unit 1 — press-lift 대상 슬롯
        private TimeLease _slomoLease;
        private DreamcatcherTargetArrow _targetArrow;
        private CardAbsorbFlightPresenter _flightPresenter; // card-fly unit 0 — lazy, 캔버스 루트 하위
        // Recovered-refresh deferral: rebinding slots mid-drag would snap the
        // floating card home and swap its entryId under the pointer.
        private bool _refreshQueued;

        private void Awake()
        {
            BuildCanvas();
            if (_panel != null) _panel.SetActive(false);
        }

        private void OnEnable()
        {
            if (gaugeView != null) gaugeView.Toggled += OnToggled;
            if (handController != null)
            {
                handController.HandChanged += OnHandChanged;
                handController.GaugeChanged += OnGaugeChangedRefreshDim;
            }
            if (GameManager.Instance != null)
                GameManager.Instance.PhaseChanged += OnPhaseChanged;
        }

        private void OnDisable()
        {
            if (gaugeView != null) gaugeView.Toggled -= OnToggled;
            if (handController != null)
            {
                handController.HandChanged -= OnHandChanged;
                handController.GaugeChanged -= OnGaugeChangedRefreshDim;
            }
            if (GameManager.Instance != null)
                GameManager.Instance.PhaseChanged -= OnPhaseChanged;
            ForceClose(); // idempotent — never leak the slomo lease
        }

        private void OnToggled()
        {
            if (Transitioning) return; // mash guard
            // Toggling mid-interaction drops any drag/portal-aim first (no spend).
            CancelAllCardInteraction();
            if (State == HandState.UnitStrip) Open();
            else Close();
        }

        private void Update()
        {
            if (State != HandState.Hand) return;
            SpringSlots(); // hand-deal-in unit 0 — 슬롯 target 으로 매프레임 추종
            // ESC = cancel rule (spec unit 7 §6): drop any drag/portal-aim, no spend.
            var kb = UnityEngine.InputSystem.Keyboard.current;
            if (kb == null || !kb.escapeKey.wasPressedThisFrame) return;
            CancelAllCardInteraction();
        }

        // Each card eases toward its target (arc base, or press-lifted in unit 1),
        // with a small idle bob/sway (unit 3) added for non-focused cards. Skips
        // slots owned by a transition (deal/sink/flip) or an active drag — those
        // write the rect directly. Realtime dt (timeScale=1, TimeManager domain).
        private void SpringSlots()
        {
            if (Transitioning) return;
            float a = 1f - Mathf.Exp(-Mathf.Max(0.01f, springK) * Time.deltaTime);
            float now = Time.time;
            for (int i = 0; i < _slots.Count; i++)
            {
                var slot = _slots[i];
                if (slot.entryId < 0 || OwnedByInteraction(slot)) continue;
                // idle 흔들림은 눌러서 든 카드(focus)만 제외 — 그 카드는 안정적으로 들려 있어야.
                Vector2 eff = slot.targetPos;
                if (i != _focusIndex)
                {
                    float ph = now * idleFreq + i * idlePhase;
                    eff += new Vector2(Mathf.Sin(ph * 0.7f) * idleSwayX, Mathf.Sin(ph) * idleBobY);
                }
                var rt = slot.rect;
                rt.anchoredPosition = Vector2.Lerp(rt.anchoredPosition, eff, a);
                float z = Mathf.LerpAngle(rt.localEulerAngles.z, slot.targetRotZ, a);
                rt.localEulerAngles = new Vector3(0f, 0f, z);
                float s = Mathf.Lerp(rt.localScale.x, slot.targetScale, a);
                rt.localScale = new Vector3(s, s, 1f);
            }
        }

        // ── press-to-lift focus (hand-deal-in unit 1, mobile) ─────────────────
        // Drag slots report pointer DOWN/UP (not hover — no hover on touch).
        // Focus only writes slot targets — the spring reads them — so press-lift,
        // idle, drag, and deal all share one motion model.

        public void SetFocus(int index)
        {
            // 손패 상태·전이·다른 카드 드래그 중이 아니고, 실제 카드가 든 슬롯일 때만 focus.
            if (State != HandState.Hand || Transitioning || AnyInteractionActive()) index = -1;
            else if (index >= 0 && (index >= _slots.Count || _slots[index].entryId < 0)) index = -1;
            if (_focusIndex == index) return;
            _focusIndex = index;
            ApplyFocusTargets();
        }

        // Release only clears if this slot is still the focused one (overlapping
        // cards can fire down(next) before up(prev)).
        public void ClearFocus(int index)
        {
            if (_focusIndex == index) SetFocus(-1);
        }

        private void ApplyFocusTargets()
        {
            for (int i = 0; i < _slots.Count; i++)
            {
                var slot = _slots[i];
                bool owned = OwnedByInteraction(slot);
                if (!owned) slot.rect.SetSiblingIndex(i); // base z-order (right over left)
                if (slot.entryId < 0 || owned) continue;

                if (i == _focusIndex)
                {
                    slot.targetPos = slot.homePos + new Vector2(0f, focusRaise);
                    slot.targetRotZ = 0f; // straighten
                    slot.targetScale = slot.usable ? focusScale : 1.06f; // dim 카드는 살짝만
                }
                else
                {
                    float push = 0f;
                    if (_focusIndex >= 0)
                    {
                        int delta = i - _focusIndex, ad = Mathf.Abs(delta);
                        if (ad >= 1 && ad <= scatterNeighbors) push = Mathf.Sign(delta) * scatter / ad;
                    }
                    slot.targetPos = slot.homePos + new Vector2(push, 0f);
                    slot.targetRotZ = slot.homeRotZ;
                    slot.targetScale = 1f;
                }
            }
            if (_focusIndex >= 0 && _focusIndex < _slots.Count)
            {
                var h = _slots[_focusIndex];
                if (!OwnedByInteraction(h)) h.rect.SetAsLastSibling(); // 눌린 카드가 최상단
            }
        }

        private void CancelAllCardInteraction()
        {
            foreach (var slot in _slots)
                if (slot.dragSlot != null && (slot.dragSlot.IsDragging || slot.dragSlot.IsPortalAiming))
                    slot.dragSlot.CancelDrag();
        }

        private bool AnyInteractionActive()
        {
            foreach (var slot in _slots)
                if (OwnedByInteraction(slot)) return true;
            return false;
        }

        // hand-deal-in — 이 슬롯을 드래그/포탈-조준이 소유하면 스프링/호버가 손대지 않는다.
        private static bool OwnedByInteraction(CardSlot slot) =>
            slot.dragSlot != null && (slot.dragSlot.IsDragging || slot.dragSlot.IsPortalAiming);

        // ── unit 7 drag-slot services ────────────────────────────────────────

        public bool CanStartDrag(int index)
        {
            if (State != HandState.Hand || Transitioning) return false;
            if (AnyInteractionActive()) return false; // one interaction at a time
            if (index < 0 || index >= _slots.Count) return false;
            var slot = _slots[index];
            return slot.entryId >= 0 && slot.usable;
        }

        public void RestoreSlotHome(int index)
        {
            if (index < 0 || index >= _slots.Count) return;
            var slot = _slots[index];
            slot.rect.anchoredPosition = slot.homePos;
            slot.rect.localEulerAngles = new Vector3(0f, 0f, slot.homeRotZ);
            slot.rect.localScale = Vector3.one; // rev 4-6 — 화살표 모드 확대 복원
            // hand-deal-in unit 0 — 스프링 목표도 base 로 복원(호버/스프링 재개 시 튐 방지).
            slot.targetPos = slot.homePos;
            slot.targetRotZ = slot.homeRotZ;
            slot.targetScale = 1f;
            // card-crumple-unfold unit 2 — 크럼플은 딜에서만: 평면(Unfold=1)+텍스트 표시 복원.
            if (slot.face != null) slot.face.Unfold = 1f;
            if (slot.nameGroup != null) slot.nameGroup.alpha = 1f;
            if (slot.costGroup != null) slot.costGroup.alpha = 1f;
        }

        // Drag slots call this at every interaction end (commit/cancel) so a
        // Recovered refresh deferred mid-drag can land.
        public void NotifyInteractionEnded()
        {
            if (_refreshQueued && !AnyInteractionActive()) { _refreshQueued = false; Refresh(); }
        }

        // Battle/Placement 이탈 → 강제 클로즈 (critic H2). Placement 재진입 리셋은
        // HandChanged(Reset) 가 처리.
        private void OnPhaseChanged(GamePhase phase)
        {
            if (phase != GamePhase.Placement && phase != GamePhase.Battle)
                ForceClose();
        }

        private void OnHandChanged(DreamcatcherHandController.HandChangeReason reason)
        {
            switch (reason)
            {
                case DreamcatcherHandController.HandChangeReason.Reset:
                    ForceClose();
                    break;
                case DreamcatcherHandController.HandChangeReason.Used:
                    Refresh();
                    Close(); // auto-return after a committed use (user-confirmed UX)
                    break;
                case DreamcatcherHandController.HandChangeReason.Recovered:
                    // A re-render mid-drag would snap the floating card home and
                    // rebind entryIds under the pointer — defer until it ends.
                    if (AnyInteractionActive()) _refreshQueued = true;
                    else Refresh(); // update only — no state change
                    break;
            }
        }

        private void OnGaugeChangedRefreshDim(int _)
        {
            if (State == HandState.Hand) RefreshUsability();
        }

        // ── open/close ───────────────────────────────────────────────────────

        private void Open()
        {
            if (State == HandState.Hand) return;
            State = HandState.Hand;
            Refresh();
            // Battle slows while shopping; UI/interaction stay realtime.
            _slomoLease.Dispose();
            float scale = config != null ? Mathf.Max(0.01f, config.slomoTimeScale) : 0.3f;
            _slomoLease = TimeManager.Instance.Request(TimeDomain.Battle, scale, priority: 50);
            if (costDisplay != null) costDisplay.SetSuppressed(true);
            // hand-deal-in unit 2 — 버튼 pulse(인과 힌트) + strip 접기 → 덱-드로우 딜.
            if (gaugeView != null) gaugeView.Pulse();
            if (_flip != null) StopCoroutine(_flip);
            _flip = StartCoroutine(OpenRoutine());
        }

        private void Close()
        {
            if (State == HandState.UnitStrip) return;
            State = HandState.UnitStrip;
            StopDeal();
            CancelAllCardInteraction(); // drop any in-flight drag (no spend)
            _focusIndex = -1;
            _slomoLease.Dispose(); // 슬로모 즉시 해제(연출은 realtime)
            if (costDisplay != null) costDisplay.SetSuppressed(false);
            // hand-deal-in unit 4 — 딜의 거울: 카드가 하단 덱으로 침강 → strip 폴드 인.
            StartSink();
        }

        // No animation: phase exits, disable, and Placement resets land here.
        private void ForceClose()
        {
            // critic H2 — drop any in-flight drag/pending first (no spend).
            CancelAllCardInteraction();
            StopDeal(); // hand-deal-in — 잔류 트윈/late-land 방지
            _slomoLease.Dispose();
            if (_flip != null) { StopCoroutine(_flip); _flip = null; }
            State = HandState.UnitStrip;
            // 억제 해제는 무조건 — 표시 여부는 CostDisplay 가 페이즈와 결합해 결정.
            if (costDisplay != null) costDisplay.SetSuppressed(false);
            if (_panel != null)
            {
                var rt = (RectTransform)_panel.transform;
                rt.localEulerAngles = Vector3.zero;
                _panel.SetActive(false);
            }
            var strip = StripPanel();
            if (strip != null)
            {
                ((RectTransform)strip.transform).localEulerAngles = Vector3.zero;
                // Only restore the strip inside the playable window — outside it
                // (Result 등) the selector hides itself via its own events.
                var gm = GameManager.Instance;
                if (gm != null && (gm.CurrentPhase == GamePhase.Placement || gm.CurrentPhase == GamePhase.Battle))
                    strip.SetActive(true);
            }
        }

        private GameObject StripPanel() =>
            defenderSelector != null ? defenderSelector.PanelGO : null;

        // X-axis fold shared by the strip fold-out (OpenRoutine) and fold-in
        // (StripFoldInRoutine). Unscaled time — the fold must not slow under slomo.
        private IEnumerator RotateX(RectTransform rt, float fromDeg, float toDeg)
        {
            float t = 0f;
            float dur = Mathf.Max(0.01f, flipHalfDuration);
            while (t < dur)
            {
                t += Time.unscaledDeltaTime;
                float k = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / dur));
                rt.localEulerAngles = new Vector3(Mathf.Lerp(fromDeg, toDeg, k), 0f, 0f);
                yield return null;
            }
            rt.localEulerAngles = new Vector3(toDeg, 0f, 0f);
        }

        // ── deal-in (hand-deal-in unit 2) ─────────────────────────────────────

        // Strip folds edge-on and vanishes, then the hand stages (backing fade)
        // and cards deal up from the deck below the tray — no whole-panel fold-in.
        private IEnumerator OpenRoutine()
        {
            var strip = StripPanel();
            if (strip != null)
            {
                var srt = (RectTransform)strip.transform;
                yield return RotateX(srt, 0f, 90f);
                strip.SetActive(false);
                srt.localEulerAngles = Vector3.zero;
            }
            var prt = (RectTransform)_panel.transform;
            prt.localEulerAngles = Vector3.zero;
            _panel.SetActive(true);
            StartDeal();
            _flip = null;
        }

        // Cards rise from a deck below the tray (not the button): each starts
        // clustered off the bottom edge, laid back (X tilt), then springs up to
        // its arc home with an OutBack overshoot + settle squash. Backing fades
        // in as the stage. Unity timeScale stays 1 (TimeManager domain) →
        // PrimeTween's default Time.deltaTime timing is realtime (no slomo drag).
        private void StartDeal()
        {
            StopDeal();
            _dealSeq = Sequence.Create();
            if (_backing != null)
            {
                var c = _backing.color; c.a = 0f; _backing.color = c;
                _dealSeq.Chain(Tween.Alpha(_backing, _backingAlpha, trayFadeSec, Ease.OutQuad));
            }
            int dealt = 0;
            for (int i = 0; i < _slots.Count; i++)
            {
                var slot = _slots[i];
                if (slot.entryId < 0) continue; // deal only real cards
                var rt = slot.rect;
                float jitter = ((i % 3) - 1) * 6f; // index 결정론 흐트러짐
                rt.anchoredPosition = new Vector2(slot.homePos.x * clusterK, handBaseY - dealRise);
                rt.localScale = Vector3.one * dealStartScale;
                rt.localEulerAngles = new Vector3(dealTiltX, 0f, slot.homeRotZ + jitter);
                // card-crumple-unfold unit 2 — 시작 = 구겨짐 + 텍스트/코스트 숨김.
                if (slot.face != null) slot.face.Unfold = 0f;
                if (slot.nameGroup != null) slot.nameGroup.alpha = 0f;
                if (slot.costGroup != null) slot.costGroup.alpha = 0f;
                float d = dealt * dealStaggerSec;
                _dealSeq.Group(Tween.UIAnchoredPosition(rt, slot.homePos, dealDurationSec, Ease.OutBack, startDelay: d));
                _dealSeq.Group(Tween.Scale(rt, Vector3.one, dealDurationSec, Ease.OutBack, startDelay: d));
                _dealSeq.Group(Tween.LocalRotation(rt, Quaternion.Euler(0f, 0f, slot.homeRotZ), dealDurationSec, Ease.OutQuad, startDelay: d));
                // ②-B 안착 squash flex(4버텍스, 메인 트윈과 안 겹치게 안착 직후). 진짜 커브는 후속 spec.
                _dealSeq.Group(Tween.PunchScale(rt, new Vector3(0.06f, -0.10f, 0f), 0.16f, frequency: 2f, startDelay: d + dealDurationSec));
                // 구김 풀림(안착보다 살짝 늦게) + 텍스트/코스트는 펴짐 끝에 페이드-인.
                if (slot.face != null)
                    _dealSeq.Group(Tween.Custom(slot.face, 0f, 1f, crumpleUnfoldSec, (f, u) => f.Unfold = u, Ease.OutQuad, startDelay: d));
                float textDelay = d + Mathf.Max(0f, crumpleUnfoldSec - textFadeSec);
                if (slot.nameGroup != null) _dealSeq.Group(Tween.Alpha(slot.nameGroup, 1f, textFadeSec, Ease.OutQuad, startDelay: textDelay));
                if (slot.costGroup != null) _dealSeq.Group(Tween.Alpha(slot.costGroup, 1f, textFadeSec, Ease.OutQuad, startDelay: textDelay));
                dealt++;
            }
        }

        private void StopDeal()
        {
            if (_dealSeq.isAlive) _dealSeq.Stop();
        }

        // Mirror of the deck-draw: cards sink back down to the deck (reverse
        // stagger, InBack + scale-down + backing fade-out), then the defender
        // strip folds back in. ForceClose's hard stop skips OnSinkComplete —
        // it does the panel/strip teardown itself.
        private void StartSink()
        {
            StopDeal();
            _dealSeq = Sequence.Create();
            if (_backing != null)
                _dealSeq.Chain(Tween.Alpha(_backing, 0f, sinkDurationSec * 0.8f, Ease.InQuad));
            int k = 0;
            for (int i = _slots.Count - 1; i >= 0; i--) // 역순 침강
            {
                var slot = _slots[i];
                if (slot.entryId < 0) continue;
                var rt = slot.rect;
                Vector2 dst = new Vector2(slot.homePos.x * clusterK, handBaseY - dealRise);
                float d = k * sinkStaggerSec;
                _dealSeq.Group(Tween.UIAnchoredPosition(rt, dst, sinkDurationSec, Ease.InBack, startDelay: d));
                _dealSeq.Group(Tween.Scale(rt, Vector3.one * dealStartScale, sinkDurationSec, Ease.InBack, startDelay: d));
                k++;
            }
            _dealSeq.ChainCallback(OnSinkComplete);
        }

        private void OnSinkComplete()
        {
            if (_panel != null)
            {
                ((RectTransform)_panel.transform).localEulerAngles = Vector3.zero;
                _panel.SetActive(false);
            }
            for (int i = 0; i < _slots.Count; i++) RestoreSlotHome(i); // 다음 오픈 대비 home 복원
            var strip = StripPanel();
            if (strip != null)
            {
                strip.SetActive(true);
                if (_flip != null) StopCoroutine(_flip);
                _flip = StartCoroutine(StripFoldInRoutine((RectTransform)strip.transform));
            }
        }

        private IEnumerator StripFoldInRoutine(RectTransform rt)
        {
            rt.localEulerAngles = new Vector3(90f, 0f, 0f);
            yield return RotateX(rt, 90f, 0f);
            rt.localEulerAngles = Vector3.zero;
            _flip = null;
        }

        // ── hand rendering ───────────────────────────────────────────────────

        private void Refresh()
        {
            if (!_built || handController == null) return;
            _focusIndex = -1; // 재바인딩 시 stale focus 해제(다음 press 가 재설정)
            EnsureSlots(handController.HandSize);
            var hand = handController.Hand();
            for (int i = 0; i < _slots.Count; i++)
            {
                var slot = _slots[i];
                RestoreSlotHome(i); // undo any drag/pending float
                if (i < hand.Count)
                    BindCard(slot, hand[i].entryId, hand[i].card);
                else
                    BindEmpty(slot);
            }
            RefreshUsability();
        }

        private void RefreshUsability()
        {
            foreach (var slot in _slots)
            {
                if (slot.entryId < 0) continue;
                slot.usable = handController.CanUse(slot.entryId);
                slot.group.alpha = slot.usable ? 1f : 0.42f; // dim = unaffordable
            }
        }

        private void BindCard(CardSlot slot, int entryId, DreamcatcherCard card)
        {
            slot.entryId = entryId;
            slot.card = card;
            slot.frame.color = new Color(0.1f, 0.08f, 0.18f, 0.92f);
            slot.costBadge.SetActive(true);
            slot.costLabel.text = handController.CostOf(card).ToString();

            // rev 4-5 — 이름은 아트 유무와 무관하게 항상 하단 밴드에 표시(시인성).
            slot.nameTag.SetActive(true);
            slot.nameLabel.text = card.displayName;

            if (card.art != null)
            {
                slot.art.enabled = true;
                slot.art.sprite = card.art;
                slot.art.color = Color.white;
            }
            else
            {
                // Active cards ship without tarot art: skill uiTint fallback.
                slot.art.enabled = true;
                slot.art.sprite = null;
                slot.art.color = card.skill != null ? card.skill.uiTint : new Color(0.35f, 0.3f, 0.5f, 1f);
            }
        }

        private void BindEmpty(CardSlot slot)
        {
            slot.entryId = -1;
            slot.card = null;
            slot.usable = false;
            slot.frame.color = new Color(1f, 1f, 1f, 0.06f); // empty frame
            slot.art.enabled = false;
            slot.nameTag.SetActive(false);
            slot.nameLabel.text = "";
            slot.costBadge.SetActive(false);
            slot.group.alpha = 1f;
        }

        // ── canvas build ─────────────────────────────────────────────────────

        private void BuildCanvas()
        {
            if (_built) return;
            _built = true;

            var roots = UiCanvasSetup.Ensure(gameObject, sortingOrder: 5);

            // card-crumple-unfold — Canvas 는 기본적으로 uv1/uv2 를 셰이더로 안 넘긴다.
            // CardCrumple 셰이더가 접힘 데이터(TEXCOORD1/2)를 읽으려면 채널을 켜야 한다.
            var canvas = GetComponentInChildren<Canvas>(true);
            if (canvas != null)
                canvas.additionalShaderChannels |=
                    AdditionalCanvasShaderChannels.TexCoord1 | AdditionalCanvasShaderChannels.TexCoord2;

            // Bottom-center hand panel.
            _panel = new GameObject("HandPanel", typeof(RectTransform), typeof(Image));
            _panel.transform.SetParent(roots.SafeAreaRoot, false);
            var prt = (RectTransform)_panel.transform;
            prt.anchorMin = new Vector2(0.5f, 0f);
            prt.anchorMax = new Vector2(0.5f, 0f);
            prt.pivot = new Vector2(0.5f, 0f);
            prt.anchoredPosition = new Vector2(0f, trayConfig != null ? trayConfig.anchoredY : 32f);
            prt.sizeDelta = trayConfig != null ? trayConfig.handSize : new Vector2(980f, 232f);
            var backing = _panel.GetComponent<Image>();
            // action-tray unit 3 — 트레이와 같은 외곽 문법(라운드+골드 엣지+네이비 fill)
            // 으로 "같은 프레임의 앞뒷면" 시각 통일. config 미할당 시 기존 단색 유지.
            if (trayConfig != null)
            {
                backing.sprite = UiRoundedSprite.Make(22f, 2f, trayConfig.fallbackFill, trayConfig.fallbackBorder);
                backing.type = Image.Type.Sliced;
                backing.color = Color.white;
            }
            else
            {
                backing.color = new Color(0.05f, 0.04f, 0.1f, 0.72f);
            }
            // The backing IS the cancel region (unit 7): keep it a raycast target.
            backing.raycastTarget = true;
            // hand-deal-in unit 0 — 딜 무대 페이드용 참조(카드와 별개로 backing 만 페이드).
            _backing = backing;
            _backingAlpha = backing.color.a;

            // rev 4-6 — 타겟팅 화살표는 패널 뒤 sibling 으로 붙여 카드 위에 그려진다.
            _targetArrow = DreamcatcherTargetArrow.Create(transform);
        }

        private void EnsureSlots(int count)
        {
            if (_slots.Count == count) return;
            foreach (var s in _slots) if (s.root != null) Destroy(s.root);
            _slots.Clear();

            float cardW = 172f, cardH = 200f;
            float step = cardW - cardOverlap; // 겹친 부채(음수 간격 효과)
            for (int i = 0; i < count; i++)
            {
                var slot = new CardSlot();
                slot.root = new GameObject($"Card_{i}", typeof(RectTransform), typeof(Image), typeof(CanvasGroup));
                slot.root.transform.SetParent(_panel.transform, false);
                slot.rect = (RectTransform)slot.root.transform;
                slot.rect.anchorMin = new Vector2(0.5f, 0f);
                slot.rect.anchorMax = new Vector2(0.5f, 0f);
                slot.rect.pivot = new Vector2(0.5f, 0f);
                // StS/HS 포물선 아치: t∈[-1,1], 가운데 솟음 + 접선 회전.
                float t = count == 1 ? 0f : (float)i / (count - 1) * 2f - 1f;
                float x = -((count - 1) * step) * 0.5f + i * step;
                float y = handBaseY + arcHeight * (1f - t * t);
                float rotZ = -t * rotMax;
                slot.rect.anchoredPosition = new Vector2(x, y);
                slot.rect.sizeDelta = new Vector2(cardW, cardH);
                slot.rect.localEulerAngles = new Vector3(0f, 0f, rotZ);

                slot.frame = slot.root.GetComponent<Image>();
                slot.group = slot.root.GetComponent<CanvasGroup>();

                // card-crumple-unfold unit 0 — art 는 서브디바이드 Graphic(구김 토대).
                // frame(root Image)·드래그·CanvasGroup 은 그대로(D1 = art-only).
                var artGO = new GameObject("Art", typeof(RectTransform), typeof(UiCardFaceMesh));
                artGO.transform.SetParent(slot.root.transform, false);
                var art = (RectTransform)artGO.transform;
                art.anchorMin = Vector2.zero;
                art.anchorMax = Vector2.one;
                art.offsetMin = new Vector2(6f, 6f);
                art.offsetMax = new Vector2(-6f, -6f);
                slot.face = artGO.GetComponent<UiCardFaceMesh>();
                slot.art = slot.face;
                slot.art.preserveAspect = true;
                slot.art.raycastTarget = false;

                // rev 4-5 — 이름은 항상 표시: 아트 위에서도 읽히도록 하단 어두운
                // 밴드 + 흰 텍스트 오버레이 (DefenderSelector 포트레이트 이름 관례).
                slot.nameTag = new GameObject("NameTag", typeof(RectTransform), typeof(Image));
                slot.nameTag.transform.SetParent(slot.root.transform, false);
                var tagRt = (RectTransform)slot.nameTag.transform;
                tagRt.anchorMin = new Vector2(0f, 0f);
                tagRt.anchorMax = new Vector2(1f, 0f);
                tagRt.pivot = new Vector2(0.5f, 0f);
                tagRt.offsetMin = new Vector2(6f, 6f);   // 하단 밴드: y 6~36
                tagRt.offsetMax = new Vector2(-6f, 36f);
                var tagImg = slot.nameTag.GetComponent<Image>();
                tagImg.color = new Color(0f, 0f, 0f, 0.62f);
                tagImg.raycastTarget = false;
                slot.nameGroup = slot.nameTag.AddComponent<CanvasGroup>(); // 크럼플 중 페이드용



                var nameGO = new GameObject("Name", typeof(RectTransform));
                nameGO.transform.SetParent(slot.nameTag.transform, false);
                var nrt = (RectTransform)nameGO.transform;
                nrt.anchorMin = Vector2.zero;
                nrt.anchorMax = Vector2.one;
                nrt.offsetMin = new Vector2(2f, 0f);
                nrt.offsetMax = new Vector2(-2f, 0f);
                slot.nameLabel = nameGO.AddComponent<TextMeshProUGUI>();
                if (labelFont != null) slot.nameLabel.font = labelFont;
                slot.nameLabel.fontSize = 20;
                slot.nameLabel.enableAutoSizing = true; // 긴 영문 스탯명 축소 허용
                slot.nameLabel.fontSizeMin = 12;
                slot.nameLabel.fontSizeMax = 20;
                slot.nameLabel.color = Color.white;
                slot.nameLabel.alignment = TextAlignmentOptions.Center;
                slot.nameLabel.raycastTarget = false;

                // Cost badge: top-left round chip.
                slot.costBadge = new GameObject("Cost", typeof(RectTransform), typeof(Image));
                slot.costBadge.transform.SetParent(slot.root.transform, false);
                var crt = (RectTransform)slot.costBadge.transform;
                crt.anchorMin = new Vector2(0f, 1f);
                crt.anchorMax = new Vector2(0f, 1f);
                crt.pivot = new Vector2(0.5f, 0.5f);
                crt.anchoredPosition = new Vector2(10f, -10f);
                crt.sizeDelta = new Vector2(44f, 44f);
                var badgeImg = slot.costBadge.GetComponent<Image>();
                badgeImg.color = new Color(0.62f, 0.4f, 1f, 0.95f); // gauge fill color
                badgeImg.raycastTarget = false;
                slot.costGroup = slot.costBadge.AddComponent<CanvasGroup>(); // 크럼플 중 페이드용

                var costTextGO = new GameObject("Value", typeof(RectTransform));
                costTextGO.transform.SetParent(slot.costBadge.transform, false);
                var ctrt = (RectTransform)costTextGO.transform;
                ctrt.anchorMin = Vector2.zero;
                ctrt.anchorMax = Vector2.one;
                ctrt.offsetMin = Vector2.zero;
                ctrt.offsetMax = Vector2.zero;
                slot.costLabel = costTextGO.AddComponent<TextMeshProUGUI>();
                if (numberFont != null) slot.costLabel.font = numberFont;
                slot.costLabel.fontSize = 24;
                slot.costLabel.color = Color.white;
                slot.costLabel.alignment = TextAlignmentOptions.Center;
                slot.costLabel.raycastTarget = false;

                slot.homePos = slot.rect.anchoredPosition;
                slot.homeRotZ = rotZ; // 원값 저장(localEulerAngles.z 360 wrap 회피)
                slot.targetPos = slot.homePos;
                slot.targetRotZ = rotZ;
                slot.targetScale = 1f;
                slot.dragSlot = slot.root.AddComponent<DreamcatcherCardDragSlot>();
                slot.dragSlot.Bind(this, i);

                _slots.Add(slot);
            }
            // card-crumple-unfold — 카드가 실제 렌더되는 캔버스에 uv1/uv2 채널 보장(BuildCanvas 의
            // GetComponentInChildren 이 카드 렌더 캔버스와 다를 수 있어 실제 ancestor 로 확정).
            if (_slots.Count > 0)
            {
                var cv = _slots[0].root.GetComponentInParent<Canvas>();
                if (cv != null)
                    cv.additionalShaderChannels |=
                        AdditionalCanvasShaderChannels.TexCoord1 | AdditionalCanvasShaderChannels.TexCoord2;
            }
            UiLayer.Apply(gameObject);
        }
    }
}
