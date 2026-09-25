using System.Collections;
using System.Collections.Generic;
using PrimeTween;
using TMPro;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.UI;
using Wassup.BattleCore;
using Wassup.BattleCoreUnity.Hud;
using Wassup.BattleCoreUnity.Input;
using Wassup.BattleCoreUnity.View;
using Wassup.Core;
using Wassup.Core.TimeControl;
using Wassup.Data;
using Wassup.Presentation;
using Wassup.UI;
using Wassup.UI.Layout;

namespace Wassup.BattleCoreUnity.Cards
{
    // battle-core-rebuild unit 7c — **드림캐쳐 손패.** 옛 `DreamcatcherHandView`(1,782줄)의 이식이다. 룩·손맛은 옛 것 그대로
    // (StS/HS 아치 부채 + 스프링 target 모델 — focus/idle/드래그/딜 공유 · 덱-드로우 딜 · 침강 · 눌러서 들기 · 상단 중앙 브리핑 ·
    // 조준 중 손패 하강 · 취소 존 · 바깥 탭 물러나기 · 흡수 비행 · 거절 움찔). 달라진 것은 **읽는 곳**이다:
    //
    //   · 손패 = 코어의 **읽기 모델**(`HandDeck.Hand` — 큐 앞 N 의 비파괴 창). 이 뷰는 자기 목록을 들지 않는다 — 들면 창
    //     멤버십 가드가 갈린다(구현 2). 슬롯은 그 창을 **그리는 칸**일 뿐이다.
    //   · 「쓸 수 있나」·「이 유닛에 붙나」는 코어 preflight(`CardInput` → `HandDeck.UsableReason`/`WouldAttach`)의 답이다.
    //     딤·드래그 게이트·거절 문구가 **같은 함수**를 본다(구현 6·9).
    //   · 카드 문안은 formatter(`CoreCardText.Body`)가 이긴다(구현 3).
    //   · 손패는 **유닛 선택으로만** 열린다(2026-08-19 사용자 결정 — 항아리 탭 진입구를 껐다, `AwakeningGaugeView.JarTapEnabled`).
    //     선택 상태의 주인은 `SelectionInput` 이고 이 뷰는 전달만 받는다(옛 `selection-hand-attach` 계약 1).
    //
    // ⚠ 시간: 딜·스프링·툴팁은 `Time.deltaTime`(프로젝트는 timeScale 1 — 도메인 시간 제어). **규칙은 틱**이다 — 카드를 잡는 동안의
    // 감속은 이 뷰가 `TimeManager` 에 빌리는 리스이고, 판은 그 발행률로만 느려진다(D23 · 계약 5).
    [DisallowMultipleComponent]
    public sealed class CoreHandView : MonoBehaviour
    {
        [Header("씬 배선 (비면 같은 씬에서 찾는다)")]
        [SerializeField] private BattleDriver _driver;
        [SerializeField] private CoreUnitViewPool _units;
        [SerializeField] private CoreMapOverlay _overlay;
        [SerializeField] private SelectionInput _selection;
        [SerializeField] private CoreDefenderTray _tray;
        [SerializeField] private CoreCostDisplay _costDisplay;
        [SerializeField] private CoreAwakeningGaugeView _gauge;
        [SerializeField] private CoreVfxSpawner _vfx;
        [SerializeField] private Camera _mainCamera;

        [Header("저작")]
        [Tooltip("부착 조준 포커스 연출 노브(옛 씬 자산). 미할당이면 포커스 연출 없이 돈다(옛 무회귀 폴백).")]
        [SerializeField] private DreamcatcherFocusConfig _focusConfig;
        [Tooltip("「{유닛명} 전용」 접두를 표시명으로 푼다. 비면 판의 유닛 저작에서 찾는다.")]
        [SerializeField] private DefenderCatalog _defenderCatalog;
        [SerializeField] private TMP_FontAsset _labelFont;   // Jua — 한국어 전투 UI
        [SerializeField] private TMP_FontAsset _numberFont;  // Anton — 코스트 숫자
        [Tooltip("트레이 공유 외곽 문법(폭/y/fill/border). 미할당 시 단색 배킹.")]
        [SerializeField] private BattleHudTrayConfig _trayConfig;
        [Tooltip("각성 저작(`AwakeningConfig.slomoTimeScale`)이 없을 때의 조준 감속 배율.")]
        [SerializeField, Range(0.01f, 1f)] private float _slomoFallback = 0.3f;

        // ── 옛 `DreamcatcherHandView` 의 노브 — 값은 옛 씬 저작 그대로 ───────────
        [SerializeField] private float flipHalfDuration = 0.14f;
        [SerializeField] private float cardOverlap = 16f;
        [SerializeField] private float arcHeight = 46f;
        [SerializeField] private float rotMax = 10f;
        [SerializeField] private float handBaseY = 16f;
        [SerializeField] private float springK = 14f;
        [SerializeField] private float dealStaggerSec = 0.05f;
        [SerializeField] private float dealDurationSec = 0.34f;
        [SerializeField] private float dealStartScale = 0.62f;
        [SerializeField] private float trayFadeSec = 0.12f;
        [SerializeField] private float dealRise = 220f;
        [SerializeField] private float dealTiltX = 50f;
        [SerializeField] private float clusterK = 0.3f;
        [SerializeField] private float crumpleUnfoldSec = 0.6f;
        [SerializeField] private float textFadeSec = 0.18f;
        [SerializeField] private float sinkDurationSec = 0.26f;
        [SerializeField] private float sinkStaggerSec = 0.04f;
        [SerializeField] private float dragClearanceDrop = 210f;
        [SerializeField] private float dragClearanceSpring = 320f;
        [SerializeField] private float dragClearanceDamping = 24f;
        [SerializeField] private float cancelZoneHeight = 310f;
        [SerializeField] private bool slomoOnOpen = false;
        [SerializeField] private float focusRaise = 100f;
        [SerializeField] private float focusScale = 1.28f;
        [SerializeField] private float scatter = 42f;
        [SerializeField] private int scatterNeighbors = 2;
        [SerializeField] private float idleBobY = 5f;
        [SerializeField] private float idleSwayX = 3f;
        [SerializeField] private float idleFreq = 1.6f;
        [SerializeField] private float idlePhase = 0.7f;
        [SerializeField] private Color unitHoverTint = new Color(1f, 0.28f, 0.22f, 1f);
        [SerializeField] private float tooltipWidth = 480f;
        [SerializeField] private float tooltipTopOffset = 24f;
        [SerializeField] private float tooltipHeaderFont = 27f;
        [SerializeField] private float tooltipBodyFont = 23f;
        [SerializeField] private float tooltipBobY = 6f;
        [SerializeField] private float tooltipBobX = 3f;
        [SerializeField] private float tooltipBobFreq = 1.2f;
        [SerializeField] private float boardTapMoveThreshold = 24f;
        [SerializeField] private float flinchStrengthX = 14f;
        [SerializeField] private float flinchDuration = 0.26f;
        [SerializeField] private float flinchFrequency = 14f;
        [SerializeField] private float rejectKickStrength = 0.45f;
        [SerializeField] private float rejectKickDuration = 0.1f;

        public enum HandState { UnitStrip, Hand }
        public HandState State { get; private set; } = HandState.UnitStrip;
        public bool IsOpen => State == HandState.Hand;
        public bool Transitioning => _flip != null || _dealSeq.isAlive;

        public sealed class CardSlot
        {
            public GameObject root;
            public RectTransform rect;
            public Image frame;
            public Image art;
            public CoreCardFaceMesh face;
            public GameObject nameTag;
            public TextMeshProUGUI nameLabel;
            public CanvasGroup nameGroup;
            public TextMeshProUGUI bodyLabel;
            public CanvasGroup bodyGroup;
            public GameObject costBadge;
            public Image costBadgeBg;
            public TextMeshProUGUI costLabel;
            public CanvasGroup costGroup;
            public CanvasGroup group;
            public CoreCardDragSlot dragSlot;
            public Vector2 homePos;
            public float homeRotZ;
            public Vector2 targetPos;
            public float targetRotZ;
            public float targetScale = 1f;
            public int entryId = -1;       // -1 = 빈 칸
            public int cardIndex = -1;
            public DreamcatcherCard card;
            // 코어 preflight 의 답(손패·대기·각성). **뜻을 넓히지 말 것** — 거절 문구가 이 사유로 갈린다.
            public RejectReason usableReason = RejectReason.CardNotInHand;
            public bool usable => usableReason == RejectReason.None;
            // 선택 유닛에 이 카드가 못 붙는다(부착 카드 한정 · 코어 `WouldAttach`).
            public bool attachBlocked;
            public bool Playable => usable && !attachBlocked;
            public bool redealing;
            public bool flinching;
        }

        // 옛 카드 면 기하(`hand-card-face` — 본문 floor 예산의 교환비). 코드 기하이지 밸런스 값이 아니다(옛 그대로).
        private const float CardW = 184f, CardH = 230f, HeaderH = 76f;
        private const float TooltipLerpK = 16f, TooltipPad = 10f, TooltipHeaderGap = 4f, TooltipHiddenScale = 0.92f;

        // 옛 딤 어휘 — `FaceDim` 곱 틴트 + 텍스트·배지 직접 지정(`selection-hand-attach` 17 rev 3). 빨강 = 브리핑 실패색과 같은 값.
        private static readonly Color FaceNormal = Color.white;
        private static readonly Color FaceDim = new Color(0.72f, 0.36f, 0.36f, 1f);
        private static readonly Color NameNormal = Color.white;
        private static readonly Color BodyNormal = new Color(0.92f, 0.92f, 0.98f, 1f);
        private static readonly Color TextDim = new Color(1f, 0.608f, 0.541f, 1f);
        private static readonly Color CostBadgeNormal = new Color(0.62f, 0.4f, 1f, 0.95f);
        private static readonly Color CostBadgeDim = new Color(0.66f, 0.20f, 0.24f, 0.95f);

        private readonly Dictionary<(CardType type, bool subconscious), Sprite> _faceCache =
            new Dictionary<(CardType, bool), Sprite>();
        private readonly List<CardSlot> _slots = new List<CardSlot>();
        private readonly List<HandDeck.Entry> _hand = new List<HandDeck.Entry>(8);
        private readonly List<int> _prevIds = new List<int>();
        private readonly List<Vector3> _prevPose = new List<Vector3>();

        private GameObject _panel;
        private float _panelBaseY;
        private float _clearanceOffset;
        private float _clearanceVel;
        private RectTransform _cancelZone;
        private GameObject _dismissCatcher;
        private bool _pendingSelectionOpen;
        private Image _backing;
        private float _backingAlpha = 1f;
        private bool _built;
        private Coroutine _flip;
        private Sequence _dealSeq;
        private Sequence _redealSeq;
        private int _focusIndex = -1;
        private TimeLease _slomoLease;
        private bool _slomoActive;
        private DreamcatcherTargetArrow _targetArrow;
        private CoreCardFocusPresenter _focus;
        private CardAbsorbFlightPresenter _flightPresenter;
        private bool _refreshQueued;
        private GameObject _tooltipRoot;
        private RectTransform _tooltipRect;
        private CanvasGroup _tooltipGroup;
        private TextMeshProUGUI _tooltipHeader;
        private TextMeshProUGUI _tooltipBody;
        private bool _tooltipVisible;
        private string _briefingStatus;
        private Vector2 _tooltipBasePos;
        private CameraDirector _cameraDirector;
        private bool _cameraDirectorWarned;
        private CoreCardTargets _targets;
        private CardInput _input;
        private float _lastGauge = -1f;

        // ── 선택 파트너 표면 — 선택 상태의 주인은 `SelectionInput`(계약 1) ──────────
        public SimEntityId SelectionTarget { get; private set; } = SimEntityId.None;
        public bool InSelectionMode => SelectionTarget.IsEntity;

        // ── 슬롯·입력이 쓰는 표면 ────────────────────────────────────────────
        public IReadOnlyList<CardSlot> Slots => _slots;
        public RectTransform HandPanelRect => _panel != null ? (RectTransform)_panel.transform : null;
        public RectTransform CancelRect => _cancelZone != null ? _cancelZone : HandPanelRect;
        public Camera MainCamera => _mainCamera != null ? _mainCamera : (_mainCamera = Camera.main);
        public BattleDriver Driver => _driver;
        public CoreCardTargets Targets => _targets;
        public CardInput Input => _input;
        public DreamcatcherTargetArrow TargetArrow => _targetArrow;
        public CoreCardFocusPresenter Focus => _focus;
        public DreamcatcherFocusConfig FocusConfig => _focusConfig;
        public CoreMapOverlay Overlay => _overlay;
        public Color UnitHoverTint => unitHoverTint;

        /// <summary>표식 드롭의 손끝 반경(칸) — 각성 저작 노브(D21 · 입력의 값).</summary>
        public float EnemyPickRadiusTiles
        {
            get
            {
                var cfg = AwakeningAuthoring();
                return cfg != null ? cfg.enemyPickRadiusTiles : 0f;
            }
        }

        private AwakeningConfig AwakeningAuthoring()
            => _driver != null && _driver.Mode != null ? _driver.Mode.awakeningConfig : null;

        // ── 수명 ─────────────────────────────────────────────────────────────

        private void Awake()
        {
            ResolveSceneRefs();
            _targets = new CoreCardTargets(_driver, _units, () => MainCamera);
            _input = new CardInput(_driver);
            BuildCanvas();
            if (_panel != null) _panel.SetActive(false);
        }

        // 씬 배선이 비어 있으면 같은 씬의 짝을 찾는다 — 조립은 씬이지만, 한 칸이 빠졌다고 손패가 통째로 죽으면
        // 「카드가 안 뜬다」를 씬이 아니라 규칙에서 찾게 된다. 찾은 것은 경고로 남긴다(배선이 정본이다).
        private void ResolveSceneRefs()
        {
            if (_driver == null) _driver = Warned(FindAnyObjectByType<BattleDriver>(), nameof(_driver));
            if (_units == null) _units = Warned(FindAnyObjectByType<CoreUnitViewPool>(), nameof(_units));
            if (_overlay == null) _overlay = Warned(FindAnyObjectByType<CoreMapOverlay>(), nameof(_overlay));
            if (_selection == null) _selection = Warned(FindAnyObjectByType<SelectionInput>(), nameof(_selection));
            if (_tray == null) _tray = Warned(FindAnyObjectByType<CoreDefenderTray>(), nameof(_tray));
            if (_costDisplay == null) _costDisplay = Warned(FindAnyObjectByType<CoreCostDisplay>(), nameof(_costDisplay));
            if (_gauge == null) _gauge = Warned(FindAnyObjectByType<CoreAwakeningGaugeView>(), nameof(_gauge));
            if (_vfx == null) _vfx = Warned(FindAnyObjectByType<CoreVfxSpawner>(), nameof(_vfx));
        }

        private T Warned<T>(T found, string field) where T : Object
        {
            if (found != null)
                Debug.LogWarning($"[CoreHandView] {field} 미배선 — 씬에서 찾은 '{found.name}' 를 쓴다. 인스펙터에 연결할 것.", this);
            return found;
        }

        private void OnEnable()
        {
            if (_driver != null) _driver.Subscribe(ViewOrder.Hand, OnCoreEvent);
            if (_selection != null) _selection.BindHand(this);
        }

        private void OnDisable()
        {
            if (_driver != null) _driver.Unsubscribe(OnCoreEvent);
            ForceClose();   // 멱등 — 감속 리스를 절대 새지 않는다
        }

        private void OnCoreEvent(CoreEvent e)
        {
            switch (e.Kind)
            {
                case CoreEventKind.MatchStarted:
                    ForceClose();
                    ClearSelectionTarget();
                    break;
                // 숙주가 떠나 카드가 돌아왔다(옛 `HandChanged.Recovered`). 드래그 중이면 끝날 때까지 미룬다 — 떠 있는
                // 카드의 항목을 손가락 밑에서 갈아 끼우지 않게.
                case CoreEventKind.CardDetached:
                    if (State != HandState.Hand) break;
                    if (AnyInteractionActive()) _refreshQueued = true;
                    else Refresh();
                    break;
                case CoreEventKind.MatchEnded:
                    ForceClose();
                    break;
            }
        }

        private void Update()
        {
            TickTooltip();
            TickPendingSelectionOpen();
            if (State != HandState.Hand) return;
            if (_driver == null || !_driver.Running || _driver.Match.Clock.Ended) { ForceClose(); return; }

            EnsureCameraDirector()?.SetHandHeadroom();
            bool held = _focusIndex >= 0 || AnyInteractionActive();
            TickSlomo(held);
            TickHandClearance(held);
            // 각성·대기는 연속값이라 사건이 없다 — 창이 열린 동안 **값이 바뀐 프레임에** 딤을 다시 읽는다(코어 preflight).
            float g = _driver.Match.Hand.Gauge;
            if (g != _lastGauge || AnyActiveCooling()) { _lastGauge = g; RefreshUsability(); }
            SpringSlots();
            var kb = UnityEngine.InputSystem.Keyboard.current;
            if (kb != null && kb.escapeKey.wasPressedThisFrame) CancelAllCardInteraction();
        }

        private bool AnyActiveCooling()
        {
            for (int i = 0; i < _slots.Count; i++)
                if (_slots[i].cardIndex >= 0 && _slots[i].usableReason == RejectReason.CardOnCooldown) return true;
            return false;
        }

        private void TickSlomo(bool held)
        {
            bool want = slomoOnOpen || held;
            if (want == _slomoActive) return;
            _slomoActive = want;
            _slomoLease.Dispose();
            if (want)
            {
                var cfg = AwakeningAuthoring();
                float scale = cfg != null ? Mathf.Max(0.01f, cfg.slomoTimeScale) : _slomoFallback;
                _slomoLease = TimeManager.Instance.Request(TimeDomain.Battle, scale, priority: 50);
            }
        }

        private void TickHandClearance(bool held)
        {
            if (_panel == null) return;
            float target = held ? -dragClearanceDrop : 0f;
            if (Mathf.Abs(_clearanceOffset - target) < 0.05f && Mathf.Abs(_clearanceVel) < 0.05f)
            {
                if (_clearanceOffset == target) return;
                _clearanceOffset = target;
                _clearanceVel = 0f;
            }
            else
            {
                KeyringSim.SpringStep(ref _clearanceOffset, ref _clearanceVel, target,
                    dragClearanceSpring, dragClearanceDamping, 0f, Time.deltaTime);
            }
            var rt = (RectTransform)_panel.transform;
            var p = rt.anchoredPosition;
            rt.anchoredPosition = new Vector2(p.x, _panelBaseY + _clearanceOffset);
        }

        private void ResetHandClearance()
        {
            if (_panel == null) return;
            _clearanceOffset = 0f;
            _clearanceVel = 0f;
            var rt = (RectTransform)_panel.transform;
            var p = rt.anchoredPosition;
            if (!Mathf.Approximately(p.y, _panelBaseY)) rt.anchoredPosition = new Vector2(p.x, _panelBaseY);
        }

        private void SpringSlots()
        {
            if (Transitioning) return;
            float a = 1f - Mathf.Exp(-Mathf.Max(0.01f, springK) * Time.deltaTime);
            float now = Time.time;
            for (int i = 0; i < _slots.Count; i++)
            {
                var slot = _slots[i];
                if (slot.entryId < 0 || OwnedByInteraction(slot) || slot.redealing || slot.flinching) continue;
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

        // ── 눌러서 들기(press-to-lift) ─────────────────────────────────────────

        public void SetFocus(int index)
        {
            if (State != HandState.Hand || Transitioning || AnyInteractionActive()) index = -1;
            else if (index >= 0 && (index >= _slots.Count || _slots[index].entryId < 0 || _slots[index].redealing)) index = -1;
            if (_focusIndex == index) return;
            _focusIndex = index;
            ApplyFocusTargets();
            if (index >= 0) SoundManager.Instance?.PlayCardPickup();
        }

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
                if (!owned) slot.rect.SetSiblingIndex(i);
                if (slot.entryId < 0 || owned) continue;
                if (i == _focusIndex)
                {
                    slot.targetPos = slot.homePos + new Vector2(0f, focusRaise);
                    slot.targetRotZ = 0f;
                    slot.targetScale = slot.Playable ? focusScale : 1.06f;
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
                if (!OwnedByInteraction(h)) h.rect.SetAsLastSibling();
            }
        }

        private void CancelAllCardInteraction()
        {
            foreach (var slot in _slots)
                if (slot.dragSlot != null && (slot.dragSlot.IsDragging || slot.dragSlot.IsPortalAiming))
                    slot.dragSlot.CancelDrag();
        }

        private void BuildCancelZone(RectTransform panelRect)
        {
            var zoneGO = new GameObject("CancelZone", typeof(RectTransform));
            zoneGO.transform.SetParent(panelRect, false);
            _cancelZone = (RectTransform)zoneGO.transform;
            _cancelZone.anchorMin = new Vector2(0.5f, 0f);
            _cancelZone.anchorMax = new Vector2(0.5f, 0f);
            _cancelZone.pivot = new Vector2(0.5f, 0f);
            _cancelZone.anchoredPosition = Vector2.zero;
            _cancelZone.sizeDelta = new Vector2(panelRect.sizeDelta.x, cancelZoneHeight);
        }

        public bool AnyInteractionActive()
        {
            foreach (var slot in _slots)
                if (OwnedByInteraction(slot)) return true;
            return false;
        }

        private static bool OwnedByInteraction(CardSlot slot) =>
            slot.dragSlot != null && (slot.dragSlot.IsDragging || slot.dragSlot.IsPortalAiming);

        public bool CanStartDrag(int index)
        {
            if (State != HandState.Hand || Transitioning) return false;
            if (AnyInteractionActive()) return false;
            if (index < 0 || index >= _slots.Count) return false;
            var slot = _slots[index];
            return slot.entryId >= 0 && slot.Playable && !slot.redealing;
        }

        public bool CanPeek(int index)
        {
            if (State != HandState.Hand || Transitioning) return false;
            if (AnyInteractionActive()) return false;
            if (index < 0 || index >= _slots.Count) return false;
            var slot = _slots[index];
            return slot.entryId >= 0 && slot.card != null && !slot.redealing;
        }

        public void FlinchSlot(int index)
        {
            if (index < 0 || index >= _slots.Count) return;
            var slot = _slots[index];
            if (slot.entryId < 0 || slot.rect == null) return;
            if (OwnedByInteraction(slot) || slot.redealing || slot.flinching) return;
            slot.flinching = true;
            var captured = slot;
            Tween.ShakeLocalPosition(slot.rect, new Vector3(flinchStrengthX, 0f, 0f),
                    flinchDuration, frequency: flinchFrequency)
                // 표시 플래그만 되돌리는 콜백 — 움찔 도중 손패가 사라져도(판 종료·씬 전환) 할 일이 없으니 경고를 끈다.
                .OnComplete(() => captured.flinching = false, warnIfTargetDestroyed: false);
            SoundManager.Instance?.PlayCardReturn();
            EnsureCameraDirector()?.Kick(rejectKickStrength, rejectKickDuration);
        }

        public void RestoreSlotHome(int index)
        {
            if (index < 0 || index >= _slots.Count) return;
            var slot = _slots[index];
            if (slot.redealing) return;
            slot.rect.anchoredPosition = slot.homePos;
            slot.rect.localEulerAngles = new Vector3(0f, 0f, slot.homeRotZ);
            slot.rect.localScale = Vector3.one;
            slot.targetPos = slot.homePos;
            slot.targetRotZ = slot.homeRotZ;
            slot.targetScale = 1f;
            if (slot.face != null) slot.face.Unfold = 1f;
            if (slot.nameGroup != null) slot.nameGroup.alpha = 1f;
            if (slot.costGroup != null) slot.costGroup.alpha = 1f;
            if (slot.bodyGroup != null) slot.bodyGroup.alpha = 1f;
        }

        public void NotifyInteractionEnded()
        {
            if (_refreshQueued && !AnyInteractionActive()) { _refreshQueued = false; Refresh(); }
            // 조준 세션이 선택 리티클을 대체했다가 끝났다 — 선택이 살아 있으면 리티클을 되찾는다(옛 unit 4).
            ReclaimSelectionReticle();
        }

        // ── 선택 핸드오프(`SelectionInput` 이 부른다) ─────────────────────────

        public void SetSelectionTarget(SimEntityId target)
        {
            SelectionTarget = target;
            RefreshUsability();
            ReclaimSelectionReticle();
        }

        public void ClearSelectionTarget()
        {
            SelectionTarget = SimEntityId.None;
            _pendingSelectionOpen = false;
            if (_focus != null && !AnyInteractionActive()) _focus.End();
            RefreshUsability();
        }

        private void ReclaimSelectionReticle()
        {
            if (_focus == null || !InSelectionMode || AnyInteractionActive()) return;
            _focus.BeginSelection(SelectionTarget);
        }

        /// <summary>선택 기인 오픈. 전이 중이면 **래치**다(옛 critic H5 — 침강 창의 탭이 손패 없는 선택으로 굳지 않게).</summary>
        public void OpenForSelection()
        {
            if (State == HandState.Hand) { _pendingSelectionOpen = false; return; }
            if (Transitioning) { _pendingSelectionOpen = true; return; }
            Open();
        }

        public void CloseFromSelection()
        {
            _pendingSelectionOpen = false;
            if (State == HandState.UnitStrip) return;
            Close();
        }

        // 손패 열린 동안 보드 탭(옛 `BoardTapped` → `DcInspectController`): 유닛이면 선택 전환, 빈 곳이면 둘 다 닫는다.
        private void OnBoardTapCaught(Vector2 screenPos)
        {
            if (State != HandState.Hand) return;
            if (_selection != null) _selection.TapFromHand(screenPos);
            else Close();
        }

        /// <summary>비-부착 조준(액티브·표식)이 드래그로 확정됐다 — **선택만** 놓고 손패·감속은 유지한다(옛 active-ally-zone 3).</summary>
        public void NotifySelectionReleasedForAim()
        {
            if (_selection != null) _selection.ReleaseKeepHand();
        }

        // ── 커밋 뒤(드래그 슬롯이 부른다) ───────────────────────────────────

        /// <summary>
        /// 카드를 썼다(receipt 수락 직후 — 옛 `HandChanged.Used`). 쓸 카드가 남으면 유지하고(잔류 카드는 옛 포즈에서 새 자리로
        /// 스프링, 새로 든 카드만 1장 재딜), 0장이면 선택까지 풀고 닫는다(옛 use-flow 1 · selection-hand-attach 8).
        /// </summary>
        public void OnCardUsed()
        {
            if (_driver == null || !_driver.Running) return;
            var hand = _driver.Match.Hand;
            hand.Hand(_hand);
            bool anyUsable = false;
            for (int i = 0; i < _hand.Count; i++)
                if (hand.UsableReason(_hand[i].EntryId) == RejectReason.None) { anyUsable = true; break; }

            if (!anyUsable)
            {
                foreach (var slot in _slots)
                {
                    if (slot.entryId < 0) continue;
                    bool still = false;
                    for (int h = 0; h < _hand.Count; h++)
                        if (_hand[h].EntryId == slot.entryId) { still = true; break; }
                    if (!still) BindEmpty(slot);
                }
                if (_selection != null) _selection.CloseSelection();   // 선택까지 풀어 기본 진행 상태로(발화 = BindEmpty 뒤 · Close 앞)
                Close();
                return;
            }

            if (_redealSeq.isAlive) _redealSeq.Complete();
            _prevIds.Clear();
            _prevPose.Clear();
            foreach (var slot in _slots)
            {
                _prevIds.Add(slot.entryId);
                _prevPose.Add(new Vector3(slot.rect.anchoredPosition.x, slot.rect.anchoredPosition.y,
                                          slot.rect.localEulerAngles.z));
            }
            Refresh();
            for (int i = 0; i < _slots.Count; i++)
            {
                var slot = _slots[i];
                if (slot.entryId < 0) continue;
                int prevAt = _prevIds.IndexOf(slot.entryId);
                if (prevAt >= 0)
                {
                    var p = _prevPose[prevAt];
                    slot.rect.anchoredPosition = new Vector2(p.x, p.y);
                    slot.rect.localEulerAngles = new Vector3(0f, 0f, p.z);
                }
                else DealInSlot(slot);
            }
        }

        private void DealInSlot(CardSlot slot)
        {
            SoundManager.Instance?.PlayCardDeal();
            slot.redealing = true;
            var rt = slot.rect;
            rt.anchoredPosition = new Vector2(slot.homePos.x * clusterK, handBaseY - dealRise);
            rt.localScale = Vector3.one * dealStartScale;
            rt.localEulerAngles = new Vector3(dealTiltX, 0f, slot.homeRotZ);
            if (slot.face != null) slot.face.Unfold = 0f;
            if (slot.nameGroup != null) slot.nameGroup.alpha = 0f;
            if (slot.costGroup != null) slot.costGroup.alpha = 0f;
            if (slot.bodyGroup != null) slot.bodyGroup.alpha = 0f;
            _redealSeq = Sequence.Create();
            _redealSeq.Group(Tween.UIAnchoredPosition(rt, slot.homePos, dealDurationSec, Ease.OutBack));
            _redealSeq.Group(Tween.Scale(rt, Vector3.one, dealDurationSec, Ease.OutBack));
            _redealSeq.Group(Tween.LocalRotation(rt, Quaternion.Euler(0f, 0f, slot.homeRotZ), dealDurationSec, Ease.OutQuad));
            _redealSeq.Group(Tween.PunchScale(rt, new Vector3(0.06f, -0.10f, 0f), 0.16f, frequency: 2f, startDelay: dealDurationSec));
            if (slot.face != null)
                _redealSeq.Group(Tween.Custom(slot.face, 0f, 1f, crumpleUnfoldSec, (f, u) => f.Unfold = u, Ease.OutQuad));
            float textDelay = Mathf.Max(0f, crumpleUnfoldSec - textFadeSec);
            if (slot.nameGroup != null) _redealSeq.Group(Tween.Alpha(slot.nameGroup, 1f, textFadeSec, Ease.OutQuad, startDelay: textDelay));
            if (slot.costGroup != null) _redealSeq.Group(Tween.Alpha(slot.costGroup, 1f, textFadeSec, Ease.OutQuad, startDelay: textDelay));
            if (slot.bodyGroup != null) _redealSeq.Group(Tween.Alpha(slot.bodyGroup, 1f, textFadeSec, Ease.OutQuad, startDelay: textDelay));
            var captured = slot;
            _redealSeq.ChainCallback(() => captured.redealing = false);
        }

        // ── 흡수 비행(`card-fly-to-target`) ───────────────────────────────────

        public void FlyCardToUnit(Vector3 startUiWorld, Vector2 ghostSize, Sprite face, SimEntityId host)
        {
            EnsureFlightPresenter();
            if (_flightPresenter == null) return;
            var h = host;
            _flightPresenter.Fly(startUiWorld, ghostSize, face, MainCamera,
                () => _targets.TryGetUnitViewPosition(h, out var p) ? p : (Vector3?)null,
                worldPos => FireAbsorbImpact(h, worldPos));
        }

        /// <summary>
        /// 지연 커밋 비행(방어유닛 부착 전용 — `defender-footprint` 5). 비행 동안 그 슬롯을 숨기고, 도착 프레임에 커밋한다.
        /// 성공 = 흡수 임팩트 / 실패·취소 = 카드 복귀. 프리젠터가 없으면 false(호출부가 즉시 커밋으로 폴백).
        /// </summary>
        public bool FlyCardToUnitDeferred(int slotIndex, int entryId, Vector3 startUiWorld, Vector2 ghostSize,
                                          Sprite face, SimEntityId host, System.Func<bool> commit)
        {
            EnsureFlightPresenter();
            if (_flightPresenter == null) return false;
            var h = host;
            var reserved = ReserveSlotVisual(slotIndex);
            System.Action restore = () =>
            {
                ReleaseSlotVisual(reserved);
                int idx = IndexOfEntry(entryId);
                if (idx >= 0) RestoreSlotHome(idx);
                SoundManager.Instance?.PlayCardReturn();
            };
            bool started = _flightPresenter.FlyDeferred(startUiWorld, ghostSize, face, MainCamera,
                () => _targets.TryGetUnitViewPosition(h, out var p) ? p : (Vector3?)null,
                onArrive: worldPos =>
                {
                    // 지연 커밋은 자기 맥락보다 오래 살 수 있다 — 판이 끝났으면 커밋하지 않는다(옛 리뷰 M-1).
                    bool inMatch = _driver != null && _driver.Running && !_driver.Match.Clock.Ended;
                    if (inMatch && commit != null && commit())
                    {
                        ReleaseSlotVisual(reserved);
                        FireAbsorbImpact(h, worldPos);
                    }
                    else restore();
                },
                onCancel: restore);
            if (!started) ReleaseSlotVisual(reserved);
            return started;
        }

        public void FlyCardToCell(Vector3 startUiWorld, Vector2 ghostSize, Sprite face, int2 cell)
        {
            EnsureFlightPresenter();
            if (_flightPresenter == null) return;
            var c = cell;
            _flightPresenter.Fly(startUiWorld, ghostSize, face, MainCamera,
                () => _targets.CellViewCenter(c), FireAbsorbImpactWorld);
        }

        private CardSlot ReserveSlotVisual(int index)
        {
            if (index < 0 || index >= _slots.Count) return null;
            var s = _slots[index];
            if (s != null && s.root != null) s.root.SetActive(false);
            return s;
        }

        private static void ReleaseSlotVisual(CardSlot s)
        {
            if (s != null && s.root != null) s.root.SetActive(true);
        }

        public int IndexOfEntry(int entryId)
        {
            for (int i = 0; i < _slots.Count; i++)
                if (_slots[i] != null && _slots[i].entryId == entryId) return i;
            return -1;
        }

        private void FireAbsorbImpact(SimEntityId host, Vector3 worldViewPos)
        {
            if (_targets.TryGetUnitView(host, out var view))
            {
                view.PlayPunch();
                view.FlashWhite();
            }
            FireAbsorbImpactWorld(worldViewPos);
        }

        private void FireAbsorbImpactWorld(Vector3 worldViewPos)
        {
            if (_vfx != null) _vfx.SpawnCardAbsorb(worldViewPos);
            SoundManager.Instance?.PlayCardAbsorb();
            EnsureCameraDirector()?.Kick();
        }

        private CameraDirector EnsureCameraDirector()
        {
            if (_cameraDirector != null) return _cameraDirector;
            if (_cameraDirectorWarned) return null;
            var cam = MainCamera;
            if (cam == null) return null;
            _cameraDirector = cam.GetComponent<CameraDirector>();
            if (_cameraDirector == null)
            {
                Debug.LogWarning("[CoreHandView] CameraDirector 미배선 — 카메라 킥 생략.", this);
                _cameraDirectorWarned = true;
            }
            return _cameraDirector;
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

        // ── 열기·닫기 ────────────────────────────────────────────────────────

        private void TickPendingSelectionOpen()
        {
            if (!_pendingSelectionOpen) return;
            if (!InSelectionMode || State == HandState.Hand) { _pendingSelectionOpen = false; return; }
            if (Transitioning) return;
            Open();
        }

        private void Open()
        {
            if (State == HandState.Hand) return;
            if (_driver == null || !_driver.Running || _driver.Match.Clock.Ended) return;
            _pendingSelectionOpen = false;
            State = HandState.Hand;
            ResetHandClearance();
            if (_dismissCatcher != null) _dismissCatcher.SetActive(true);
            if (_gauge != null) _gauge.SetOpen(true);
            Refresh();
            _slomoLease.Dispose();
            _slomoActive = false;
            if (_costDisplay != null) _costDisplay.SetSuppressed(true);
            if (_flip != null) StopCoroutine(_flip);
            _flip = StartCoroutine(OpenRoutine());
        }

        private void Close()
        {
            if (State == HandState.UnitStrip) return;
            State = HandState.UnitStrip;
            if (_dismissCatcher != null) _dismissCatcher.SetActive(false);
            if (_gauge != null) _gauge.SetOpen(false);
            StopDeal();
            CancelAllCardInteraction();
            _focus?.End();
            HideDragTooltip(immediate: true);
            _focusIndex = -1;
            _slomoLease.Dispose();
            _slomoActive = false;
            if (_costDisplay != null) _costDisplay.SetSuppressed(false);
            // unit 8b — **열기 뒤집기가 도는 중에 닫히면 그 뒤집기를 끊는다.** 안 끊으면 뒤집기가 끝난 뒤 패널을 켜고 딜을
            // 시작해(`StartDeal` 이 방금 건 가라앉기를 멈춘다) 상태는 「칸 줄」인데 손패가 떠 있고 칸 줄이 영영 접힌 채 남는다.
            // 선택 직후 곧바로 철수하면(대상이 사라져 선택이 닫힌다) 나는 경로다.
            if (_flip != null) { StopCoroutine(_flip); _flip = null; }
            StartSink();
        }

        private void ForceClose()
        {
            CancelAllCardInteraction();
            _pendingSelectionOpen = false;
            _focus?.End();
            HideDragTooltip(immediate: true);
            StopDeal();
            _slomoLease.Dispose();
            _slomoActive = false;
            if (_flip != null) { StopCoroutine(_flip); _flip = null; }
            State = HandState.UnitStrip;
            ResetHandClearance();
            if (_dismissCatcher != null) _dismissCatcher.SetActive(false);
            if (_gauge != null) _gauge.SetOpen(false);
            if (_costDisplay != null) _costDisplay.SetSuppressed(false);
            if (_panel != null)
            {
                ((RectTransform)_panel.transform).localEulerAngles = Vector3.zero;
                _panel.SetActive(false);
            }
            var strip = StripPanel();
            if (strip != null)
            {
                ((RectTransform)strip.transform).localEulerAngles = Vector3.zero;
                strip.SetActive(true);
            }
        }

        private GameObject StripPanel() => _tray != null && _tray.StripRect != null ? _tray.StripRect.gameObject : null;

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

        private IEnumerator OpenRoutine()
        {
            var strip = StripPanel();
            if (strip != null && strip.activeInHierarchy)
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

        private void StartDeal()
        {
            StopDeal();
            SoundManager.Instance?.PlayCardDeal();
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
                if (slot.entryId < 0) continue;
                var rt = slot.rect;
                float jitter = ((i % 3) - 1) * 6f;
                rt.anchoredPosition = new Vector2(slot.homePos.x * clusterK, handBaseY - dealRise);
                rt.localScale = Vector3.one * dealStartScale;
                rt.localEulerAngles = new Vector3(dealTiltX, 0f, slot.homeRotZ + jitter);
                if (slot.face != null) slot.face.Unfold = 0f;
                if (slot.nameGroup != null) slot.nameGroup.alpha = 0f;
                if (slot.costGroup != null) slot.costGroup.alpha = 0f;
                if (slot.bodyGroup != null) slot.bodyGroup.alpha = 0f;
                float d = dealt * dealStaggerSec;
                _dealSeq.Group(Tween.UIAnchoredPosition(rt, slot.homePos, dealDurationSec, Ease.OutBack, startDelay: d));
                _dealSeq.Group(Tween.Scale(rt, Vector3.one, dealDurationSec, Ease.OutBack, startDelay: d));
                _dealSeq.Group(Tween.LocalRotation(rt, Quaternion.Euler(0f, 0f, slot.homeRotZ), dealDurationSec, Ease.OutQuad, startDelay: d));
                _dealSeq.Group(Tween.PunchScale(rt, new Vector3(0.06f, -0.10f, 0f), 0.16f, frequency: 2f, startDelay: d + dealDurationSec));
                if (slot.face != null)
                    _dealSeq.Group(Tween.Custom(slot.face, 0f, 1f, crumpleUnfoldSec, (f, u) => f.Unfold = u, Ease.OutQuad, startDelay: d));
                float textDelay = d + Mathf.Max(0f, crumpleUnfoldSec - textFadeSec);
                if (slot.nameGroup != null) _dealSeq.Group(Tween.Alpha(slot.nameGroup, 1f, textFadeSec, Ease.OutQuad, startDelay: textDelay));
                if (slot.costGroup != null) _dealSeq.Group(Tween.Alpha(slot.costGroup, 1f, textFadeSec, Ease.OutQuad, startDelay: textDelay));
                if (slot.bodyGroup != null) _dealSeq.Group(Tween.Alpha(slot.bodyGroup, 1f, textFadeSec, Ease.OutQuad, startDelay: textDelay));
                dealt++;
            }
        }

        private void StopDeal()
        {
            if (_dealSeq.isAlive) _dealSeq.Stop();
            if (_redealSeq.isAlive) _redealSeq.Complete();
        }

        /// <summary>딜 진행 중 카드를 누르면 즉시 완주 — 성급한 플레이어는 한 터치로 집는다(HS/StS 손맛).</summary>
        public bool TryFastForwardDeal()
        {
            if (!_dealSeq.isAlive) return false;
            _dealSeq.Complete();
            return true;
        }

        private void StartSink()
        {
            StopDeal();
            _dealSeq = Sequence.Create();
            if (_backing != null)
                _dealSeq.Chain(Tween.Alpha(_backing, 0f, sinkDurationSec * 0.8f, Ease.InQuad));
            int k = 0;
            for (int i = _slots.Count - 1; i >= 0; i--)
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
                ResetHandClearance();
                _panel.SetActive(false);
            }
            for (int i = 0; i < _slots.Count; i++) RestoreSlotHome(i);
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

        // ── 그리기 — 손패는 코어의 창을 옮겨 그릴 뿐이다 ─────────────────────

        private void Refresh()
        {
            if (!_built || _driver == null || !_driver.Running) return;
            if (_redealSeq.isAlive) _redealSeq.Complete();
            _focusIndex = -1;
            var deck = _driver.Match.Hand;
            EnsureSlots(deck.HandSize);
            deck.Hand(_hand);
            for (int i = 0; i < _slots.Count; i++)
            {
                var slot = _slots[i];
                RestoreSlotHome(i);
                if (i < _hand.Count) BindCard(slot, _hand[i].EntryId, _hand[i].CardIndex);
                else BindEmpty(slot);
            }
            _lastGauge = deck.Gauge;
            RefreshUsability();
        }

        private void RefreshUsability()
        {
            if (_input == null || !_input.Ready) return;
            foreach (var slot in _slots)
            {
                if (slot.entryId < 0) continue;
                slot.usableReason = _input.UsableReason(slot.entryId);
                slot.attachBlocked = IsAttachBlocked(slot);
                slot.group.alpha = 1f;
                bool ok = slot.Playable;
                if (slot.face != null) slot.face.color = ok ? FaceNormal : FaceDim;
                if (slot.nameLabel != null) slot.nameLabel.color = ok ? NameNormal : TextDim;
                if (slot.bodyLabel != null) slot.bodyLabel.color = ok ? BodyNormal : TextDim;
                if (slot.costBadgeBg != null) slot.costBadgeBg.color = ok ? CostBadgeNormal : CostBadgeDim;
            }
        }

        // 선택 중인 지금 이 카드를 **선택 유닛에** 쓸 수 없다 — 딤 대상은 방어유닛 부착 카드뿐이다(액티브·표식은 끌면 선택을
        // 놓고 필드 문맥으로 나온다 — 옛 rev 2). 판정은 커밋과 같은 코어 함수다(`WouldAttach` — 상한 포함).
        private bool IsAttachBlocked(CardSlot slot)
        {
            if (!InSelectionMode || slot.cardIndex < 0) return false;
            if (_input.AimOf(slot.cardIndex) != CoreCardAim.Defender) return false;
            return _input.WouldAttach(slot.cardIndex, SelectionTarget) != RejectReason.None;
        }

        private void BindCard(CardSlot slot, int entryId, int cardIndex)
        {
            var card = _driver.ViewAssets.Card(cardIndex);
            slot.entryId = entryId;
            slot.cardIndex = cardIndex;
            slot.card = card;
            slot.frame.color = Color.clear;
            slot.costBadge.SetActive(true);
            slot.costLabel.text = _driver.Definition.Cards[cardIndex].Cost.ToString();
            slot.nameTag.SetActive(true);
            slot.nameLabel.text = card != null ? card.displayName : _driver.Definition.Cards[cardIndex].Id;
            slot.bodyLabel.text = CoreCardText.Body(card, UnitNameOf);
            slot.art.enabled = card != null;
            slot.art.sprite = card != null ? FaceSpriteFor(card) : null;
            slot.art.color = FaceNormal;
        }

        private string UnitNameOf(string unitId)
        {
            if (_defenderCatalog != null) return _defenderCatalog.DisplayNameOf(unitId);
            var assets = _driver != null ? _driver.DefenderAssets : null;
            if (assets == null) return null;
            for (int i = 0; i < assets.Count; i++)
                if (assets[i] != null && assets[i].id == unitId)
                    return string.IsNullOrEmpty(assets[i].displayName) ? assets[i].id : assets[i].displayName;
            return null;
        }

        private Sprite FaceSpriteFor(DreamcatcherCard card)
        {
            var key = (card.type, card.category == CardCategory.Subconscious);
            if (_faceCache.TryGetValue(key, out var sprite) && sprite != null) return sprite;
            sprite = UiRoundedSprite.MakeCardFace(
                (int)(CardW * 2f), (int)(CardH * 2f), radius: 44f, border: 6f,
                CardCategoryStyle.HandHeader(card.type), CardCategoryStyle.HandBody(),
                CardCategoryStyle.HandBorder(card), HeaderH / CardH);
            _faceCache[key] = sprite;
            return sprite;
        }

        private void BindEmpty(CardSlot slot)
        {
            slot.entryId = -1;
            slot.cardIndex = -1;
            slot.card = null;
            slot.usableReason = RejectReason.CardNotInHand;
            slot.attachBlocked = false;
            slot.frame.color = new Color(1f, 1f, 1f, 0.06f);
            slot.art.enabled = false;
            slot.nameTag.SetActive(false);
            slot.nameLabel.text = "";
            slot.bodyLabel.text = "";
            slot.costBadge.SetActive(false);
            slot.group.alpha = 1f;
        }

        // ── 캔버스 ───────────────────────────────────────────────────────────

        private void BuildCanvas()
        {
            if (_built) return;
            _built = true;

            var roots = UiCanvasSetup.Ensure(gameObject, sortingOrder: 5);
            // 카드면 셰이더(CardCrumple)가 uv1/uv2 를 읽는다 — 캔버스가 넘기지 않으면 **조용히** 깨진다(구현 10).
            var canvas = GetComponent<Canvas>();
            if (canvas != null)
                canvas.additionalShaderChannels |=
                    AdditionalCanvasShaderChannels.TexCoord1 | AdditionalCanvasShaderChannels.TexCoord2;

            _dismissCatcher = new GameObject("HandDismissCatcher",
                typeof(RectTransform), typeof(Image), typeof(HandDismissTapCatcher));
            _dismissCatcher.transform.SetParent(roots.SafeAreaRoot, false);
            var dcRt = (RectTransform)_dismissCatcher.transform;
            dcRt.anchorMin = Vector2.zero; dcRt.anchorMax = Vector2.one;
            dcRt.offsetMin = Vector2.zero; dcRt.offsetMax = Vector2.zero;
            var dcImg = _dismissCatcher.GetComponent<Image>();
            dcImg.color = new Color(0f, 0f, 0f, 0.001f);
            _dismissCatcher.GetComponent<HandDismissTapCatcher>().Init(
                () => AnyInteractionActive(), OnBoardTapCaught, boardTapMoveThreshold);
            _dismissCatcher.transform.SetAsFirstSibling();
            _dismissCatcher.SetActive(false);

            _panel = new GameObject("HandPanel", typeof(RectTransform), typeof(Image));
            _panel.transform.SetParent(roots.SafeAreaRoot, false);
            var prt = (RectTransform)_panel.transform;
            prt.anchorMin = new Vector2(0.5f, 0f);
            prt.anchorMax = new Vector2(0.5f, 0f);
            prt.pivot = new Vector2(0.5f, 0f);
            prt.anchoredPosition = new Vector2(0f, _trayConfig != null ? _trayConfig.anchoredY : 32f);
            prt.sizeDelta = _trayConfig != null ? _trayConfig.handSize : new Vector2(980f, 232f);
            _panelBaseY = prt.anchoredPosition.y;
            var backing = _panel.GetComponent<Image>();
            if (_trayConfig != null)
            {
                backing.sprite = UiRoundedSprite.Make(22f, 2f, _trayConfig.fallbackFill, _trayConfig.fallbackBorder);
                backing.type = Image.Type.Sliced;
                backing.color = Color.white;
            }
            else backing.color = new Color(0.05f, 0.04f, 0.1f, 0.72f);
            backing.raycastTarget = true;
            _backing = backing;
            _backingAlpha = backing.color.a;

            BuildCancelZone(prt);

            _targetArrow = DreamcatcherTargetArrow.Create(transform);
            _targetArrow.Configure(_focusConfig);

            if (_focusConfig != null)
            {
                _focus = CoreCardFocusPresenter.Create(transform, _focusConfig, _labelFont);
                _focus.Bind(_targets, _driver != null && _driver.Running ? _driver.Match.Hand : null);
                _focus.EnforceSiblingOrder(_targetArrow.transform);
            }

            BuildTooltip(roots.SafeAreaRoot);
        }

        private void LateUpdate()
        {
            // 판은 `Awake` 뒤에 선다(드라이버 `Start`) — 포커스의 부착 수 창구를 그 판의 손패 담당자로 잇는다.
            if (_focus != null && _driver != null && _driver.Running) _focus.Bind(_targets, _driver.Match.Hand);
        }

        private void BuildTooltip(Transform parent)
        {
            _tooltipRoot = new GameObject("DragTooltip", typeof(RectTransform), typeof(Image), typeof(CanvasGroup));
            _tooltipRoot.transform.SetParent(parent, false);
            _tooltipRect = (RectTransform)_tooltipRoot.transform;
            _tooltipRect.anchorMin = new Vector2(0.5f, 1f);
            _tooltipRect.anchorMax = new Vector2(0.5f, 1f);
            _tooltipRect.pivot = new Vector2(0.5f, 1f);
            var bg = _tooltipRoot.GetComponent<Image>();
            var fill = new Color(0.07f, 0.06f, 0.13f, 1f);
            if (_trayConfig != null)
            {
                bg.sprite = UiRoundedSprite.Make(16f, 2f, fill, _trayConfig.fallbackBorder);
                bg.type = Image.Type.Sliced;
                bg.color = Color.white;
            }
            else bg.color = fill;
            bg.raycastTarget = false;
            var shadow = _tooltipRoot.AddComponent<Shadow>();
            shadow.effectDistance = new Vector2(0f, -5f);
            shadow.effectColor = new Color(0f, 0f, 0f, 0.45f);
            _tooltipGroup = _tooltipRoot.GetComponent<CanvasGroup>();
            _tooltipGroup.alpha = 0f;
            _tooltipGroup.blocksRaycasts = false;
            _tooltipGroup.interactable = false;
            _tooltipHeader = BuildTooltipLabel("Header", tooltipHeaderFont, TextAlignmentOptions.Left);
            _tooltipBody = BuildTooltipLabel("Body", tooltipBodyFont, TextAlignmentOptions.TopLeft);
            _tooltipRoot.SetActive(false);
        }

        private TextMeshProUGUI BuildTooltipLabel(string n, float size, TextAlignmentOptions align)
        {
            var go = new GameObject(n, typeof(RectTransform));
            go.transform.SetParent(_tooltipRoot.transform, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.offsetMin = new Vector2(TooltipPad, 0f);
            rt.offsetMax = new Vector2(-TooltipPad, 0f);
            var tmp = go.AddComponent<TextMeshProUGUI>();
            if (_labelFont != null) tmp.font = _labelFont;
            tmp.fontSize = size;
            tmp.color = Color.white;
            tmp.alignment = align;
            tmp.raycastTarget = false;
            return tmp;
        }

        /// <summary>지금 떠 있는 브리핑 상태 줄(테스트 — 거절 문구가 코어 답과 같은 문자열인지 본다).</summary>
        public string BriefingStatus => _tooltipVisible ? _briefingStatus ?? "" : "";

        public void ShowDragBriefing(string controls, string status)
        {
            if (_tooltipRoot == null || string.IsNullOrEmpty(controls)) return;
            _tooltipHeader.text = controls;
            _briefingStatus = null;
            UpdateDragBriefingStatus(status);
            _tooltipBasePos = new Vector2(0f, -tooltipTopOffset);
            _tooltipRect.anchoredPosition = _tooltipBasePos;
            _tooltipVisible = true;
            if (!_tooltipRoot.activeSelf)
            {
                _tooltipGroup.alpha = 0f;
                _tooltipRect.localScale = new Vector3(TooltipHiddenScale, TooltipHiddenScale, 1f);
                _tooltipRoot.SetActive(true);
            }
        }

        public void UpdateDragBriefingStatus(string status)
        {
            if (_tooltipRoot == null || status == _briefingStatus) return;
            _briefingStatus = status;
            _tooltipBody.text = status ?? "";
            float innerW = tooltipWidth - TooltipPad * 2f;
            float headerH = _tooltipHeader.GetPreferredValues(_tooltipHeader.text, innerW, 0f).y;
            float bodyH = _tooltipBody.GetPreferredValues(_tooltipBody.text, innerW, 0f).y;
            var hrt = (RectTransform)_tooltipHeader.transform;
            hrt.anchoredPosition = new Vector2(0f, -TooltipPad);
            hrt.sizeDelta = new Vector2(hrt.sizeDelta.x, headerH);
            var brt = (RectTransform)_tooltipBody.transform;
            float bodyTop = TooltipPad + headerH + TooltipHeaderGap;
            brt.anchoredPosition = new Vector2(0f, -bodyTop);
            brt.sizeDelta = new Vector2(brt.sizeDelta.x, bodyH);
            _tooltipRect.sizeDelta = new Vector2(tooltipWidth, bodyTop + bodyH + TooltipPad);
        }

        public void HideDragTooltip(bool immediate = false)
        {
            if (_tooltipRoot == null) return;
            _tooltipVisible = false;
            if (immediate)
            {
                _tooltipGroup.alpha = 0f;
                _tooltipRoot.SetActive(false);
            }
        }

        private void TickTooltip()
        {
            if (_tooltipRoot == null || !_tooltipRoot.activeSelf) return;
            float a = 1f - Mathf.Exp(-TooltipLerpK * Time.deltaTime);
            _tooltipGroup.alpha = Mathf.Lerp(_tooltipGroup.alpha, _tooltipVisible ? 1f : 0f, a);
            float s = Mathf.Lerp(_tooltipRect.localScale.x, _tooltipVisible ? 1f : TooltipHiddenScale, a);
            _tooltipRect.localScale = new Vector3(s, s, 1f);
            float t = Time.time;
            _tooltipRect.anchoredPosition = _tooltipBasePos + new Vector2(
                Mathf.Sin(t * tooltipBobFreq * 0.7f) * tooltipBobX,
                Mathf.Sin(t * tooltipBobFreq) * tooltipBobY);
            if (!_tooltipVisible && _tooltipGroup.alpha < 0.02f) _tooltipRoot.SetActive(false);
        }

        private void EnsureSlots(int count)
        {
            if (_slots.Count == count) return;
            foreach (var s in _slots) if (s.root != null) Destroy(s.root);
            _slots.Clear();

            float step = CardW - cardOverlap;
            if (_cancelZone != null && _panel != null)
            {
                float fanWidth = CardW + step * Mathf.Max(0, count - 1);
                _cancelZone.sizeDelta = new Vector2(
                    Mathf.Max(((RectTransform)_panel.transform).sizeDelta.x, fanWidth), cancelZoneHeight);
            }
            for (int i = 0; i < count; i++)
            {
                var slot = new CardSlot();
                slot.root = new GameObject($"Card_{i}", typeof(RectTransform), typeof(Image), typeof(CanvasGroup));
                slot.root.transform.SetParent(_panel.transform, false);
                slot.rect = (RectTransform)slot.root.transform;
                slot.rect.anchorMin = new Vector2(0.5f, 0f);
                slot.rect.anchorMax = new Vector2(0.5f, 0f);
                slot.rect.pivot = new Vector2(0.5f, 0f);
                float t = count == 1 ? 0f : (float)i / (count - 1) * 2f - 1f;
                float x = -((count - 1) * step) * 0.5f + i * step;
                float y = handBaseY + arcHeight * (1f - t * t);
                float rotZ = -t * rotMax;
                slot.rect.anchoredPosition = new Vector2(x, y);
                slot.rect.sizeDelta = new Vector2(CardW, CardH);
                slot.rect.localEulerAngles = new Vector3(0f, 0f, rotZ);
                slot.frame = slot.root.GetComponent<Image>();
                slot.group = slot.root.GetComponent<CanvasGroup>();

                var faceGO = new GameObject("Face", typeof(RectTransform), typeof(CoreCardFaceMesh));
                faceGO.transform.SetParent(slot.root.transform, false);
                var faceRt = (RectTransform)faceGO.transform;
                faceRt.anchorMin = Vector2.zero; faceRt.anchorMax = Vector2.one;
                faceRt.offsetMin = Vector2.zero; faceRt.offsetMax = Vector2.zero;
                slot.face = faceGO.GetComponent<CoreCardFaceMesh>();
                slot.art = slot.face;
                slot.art.preserveAspect = true;
                slot.art.raycastTarget = false;

                slot.nameTag = new GameObject("NameTag", typeof(RectTransform), typeof(CanvasGroup));
                slot.nameTag.transform.SetParent(slot.root.transform, false);
                var tagRt = (RectTransform)slot.nameTag.transform;
                tagRt.anchorMin = new Vector2(0f, 1f);
                tagRt.anchorMax = new Vector2(1f, 1f);
                tagRt.pivot = new Vector2(0.5f, 1f);
                tagRt.offsetMin = new Vector2(8f, -HeaderH);
                tagRt.offsetMax = new Vector2(-8f, -34f);
                slot.nameGroup = slot.nameTag.GetComponent<CanvasGroup>();

                var nameGO = new GameObject("Name", typeof(RectTransform));
                nameGO.transform.SetParent(slot.nameTag.transform, false);
                var nrt = (RectTransform)nameGO.transform;
                nrt.anchorMin = Vector2.zero; nrt.anchorMax = Vector2.one;
                nrt.offsetMin = new Vector2(2f, 0f); nrt.offsetMax = new Vector2(-2f, 0f);
                slot.nameLabel = nameGO.AddComponent<TextMeshProUGUI>();
                if (_labelFont != null) slot.nameLabel.font = _labelFont;
                slot.nameLabel.fontSize = 32;
                slot.nameLabel.enableAutoSizing = true;
                slot.nameLabel.fontSizeMin = 24;
                slot.nameLabel.fontSizeMax = 32;
                slot.nameLabel.color = NameNormal;
                slot.nameLabel.alignment = TextAlignmentOptions.Center;
                slot.nameLabel.textWrappingMode = TextWrappingModes.NoWrap;
                slot.nameLabel.raycastTarget = false;

                var bodyGO = new GameObject("Body", typeof(RectTransform), typeof(CanvasGroup));
                bodyGO.transform.SetParent(slot.root.transform, false);
                var brt = (RectTransform)bodyGO.transform;
                brt.anchorMin = Vector2.zero; brt.anchorMax = Vector2.one;
                brt.offsetMin = new Vector2(10f, 8f);
                brt.offsetMax = new Vector2(-10f, -(HeaderH + 4f));
                slot.bodyGroup = bodyGO.GetComponent<CanvasGroup>();
                slot.bodyLabel = bodyGO.AddComponent<TextMeshProUGUI>();
                if (_labelFont != null) slot.bodyLabel.font = _labelFont;
                slot.bodyLabel.fontSize = 24;
                slot.bodyLabel.enableAutoSizing = true;
                slot.bodyLabel.fontSizeMin = 18;
                slot.bodyLabel.fontSizeMax = 24;
                slot.bodyLabel.color = BodyNormal;
                slot.bodyLabel.alignment = TextAlignmentOptions.TopLeft;
                slot.bodyLabel.textWrappingMode = TextWrappingModes.Normal;
                slot.bodyLabel.overflowMode = TextOverflowModes.Ellipsis;
                slot.bodyLabel.raycastTarget = false;

                slot.costBadge = new GameObject("Cost", typeof(RectTransform), typeof(Image));
                slot.costBadge.transform.SetParent(slot.root.transform, false);
                var crt = (RectTransform)slot.costBadge.transform;
                crt.anchorMin = new Vector2(0f, 1f);
                crt.anchorMax = new Vector2(0f, 1f);
                crt.pivot = new Vector2(0.5f, 0.5f);
                crt.anchoredPosition = new Vector2(10f, -10f);
                crt.sizeDelta = new Vector2(44f, 44f);
                slot.costBadgeBg = slot.costBadge.GetComponent<Image>();
                slot.costBadgeBg.color = CostBadgeNormal;
                slot.costBadgeBg.raycastTarget = false;
                slot.costGroup = slot.costBadge.AddComponent<CanvasGroup>();

                var costTextGO = new GameObject("Value", typeof(RectTransform));
                costTextGO.transform.SetParent(slot.costBadge.transform, false);
                var ctrt = (RectTransform)costTextGO.transform;
                ctrt.anchorMin = Vector2.zero; ctrt.anchorMax = Vector2.one;
                ctrt.offsetMin = Vector2.zero; ctrt.offsetMax = Vector2.zero;
                slot.costLabel = costTextGO.AddComponent<TextMeshProUGUI>();
                if (_numberFont != null) slot.costLabel.font = _numberFont;
                slot.costLabel.fontSize = 24;
                slot.costLabel.color = Color.white;
                slot.costLabel.alignment = TextAlignmentOptions.Center;
                slot.costLabel.raycastTarget = false;

                slot.homePos = slot.rect.anchoredPosition;
                slot.homeRotZ = rotZ;
                slot.targetPos = slot.homePos;
                slot.targetRotZ = rotZ;
                slot.targetScale = 1f;
                slot.dragSlot = slot.root.AddComponent<CoreCardDragSlot>();
                slot.dragSlot.Bind(this, i);
                _slots.Add(slot);
            }
            if (_slots.Count > 0)
            {
                var cv = _slots[0].root.GetComponentInParent<Canvas>();
                if (cv != null)
                    cv.additionalShaderChannels |=
                        AdditionalCanvasShaderChannels.TexCoord1 | AdditionalCanvasShaderChannels.TexCoord2;
            }
            UiLayer.Apply(gameObject);
        }

        /// <summary>테스트 창구 — 그 칸의 화면 중심(포인터 장치 없이 제스처 창구를 부를 때).</summary>
        public Vector2 SlotScreenCenter(int index)
            => index >= 0 && index < _slots.Count
                ? RectTransformUtility.WorldToScreenPoint(null, _slots[index].rect.TransformPoint(_slots[index].rect.rect.center))
                : default;
    }
}
