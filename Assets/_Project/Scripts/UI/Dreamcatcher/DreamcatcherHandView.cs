using System.Collections;
using System.Collections.Generic;
using TMPro;
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
        [SerializeField] private float flipHalfDuration = 0.14f;
        [SerializeField] private float fanAngle = 4f; // slight StS-style fan per slot
        // rev 4 — 카드 드래그 중 호버된 수비수의 스파인 틴트(포커스 대상 시각화).
        // rev 4-4 — 붉은색 계열로 시인성 상향(사용자 확정). 유일한 포커스 표시(타일 하이라이트 제거).
        [SerializeField] private Color unitHoverTint = new Color(1f, 0.28f, 0.22f, 1f);

        public enum HandState { UnitStrip, Hand }
        public HandState State { get; private set; } = HandState.UnitStrip;
        public bool Transitioning => _flip != null;

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

        public class CardSlot
        {
            public GameObject root;
            public RectTransform rect;
            public Image frame;
            public Image art;
            public GameObject nameTag; // rev 4-5 — 하단 네임 밴드(어두운 배킹+이름, 항상 표시)
            public TextMeshProUGUI nameLabel;
            public GameObject costBadge;
            public TextMeshProUGUI costLabel;
            public CanvasGroup group;
            public DreamcatcherCardDragSlot dragSlot;
            public Vector2 homePos;        // unit 7 — restore anchor after drag/cancel
            public float homeRotZ;
            public int entryId = -1;       // -1 = empty slot
            public DreamcatcherCard card;
            public bool usable;
        }

        private GameObject _panel;
        private readonly List<CardSlot> _slots = new List<CardSlot>();
        private bool _built;
        private Coroutine _flip;
        private TimeLease _slomoLease;
        private DreamcatcherTargetArrow _targetArrow;
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
            // ESC = cancel rule (spec unit 7 §6): drop any drag/portal-aim, no spend.
            if (State != HandState.Hand) return;
            var kb = UnityEngine.InputSystem.Keyboard.current;
            if (kb == null || !kb.escapeKey.wasPressedThisFrame) return;
            CancelAllCardInteraction();
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
                if (slot.dragSlot != null && (slot.dragSlot.IsDragging || slot.dragSlot.IsPortalAiming))
                    return true;
            return false;
        }

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
            StartFlip(from: StripPanel(), to: _panel);
        }

        private void Close()
        {
            if (State == HandState.UnitStrip) return;
            State = HandState.UnitStrip;
            _slomoLease.Dispose();
            if (costDisplay != null) costDisplay.SetSuppressed(false);
            StartFlip(from: _panel, to: StripPanel());
        }

        // No animation: phase exits, disable, and Placement resets land here.
        private void ForceClose()
        {
            // critic H2 — drop any in-flight drag/pending first (no spend).
            CancelAllCardInteraction();
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

        private void StartFlip(GameObject from, GameObject to)
        {
            if (_flip != null) StopCoroutine(_flip);
            _flip = StartCoroutine(FlipRoutine(from, to));
        }

        // X-axis fold: `from` rotates 0→90 (edge-on, vanishes), then `to`
        // rotates 90→0. Unscaled time — the flip must not slow under slomo.
        private IEnumerator FlipRoutine(GameObject from, GameObject to)
        {
            if (from != null)
            {
                var rt = (RectTransform)from.transform;
                yield return RotateX(rt, 0f, 90f);
                from.SetActive(false);
                rt.localEulerAngles = Vector3.zero;
            }
            if (to != null)
            {
                var rt = (RectTransform)to.transform;
                rt.localEulerAngles = new Vector3(90f, 0f, 0f);
                to.SetActive(true);
                yield return RotateX(rt, 90f, 0f);
                rt.localEulerAngles = Vector3.zero;
            }
            _flip = null;
        }

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

        // ── hand rendering ───────────────────────────────────────────────────

        private void Refresh()
        {
            if (!_built || handController == null) return;
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

            // Bottom-center hand panel.
            _panel = new GameObject("HandPanel", typeof(RectTransform), typeof(Image));
            _panel.transform.SetParent(roots.SafeAreaRoot, false);
            var prt = (RectTransform)_panel.transform;
            prt.anchorMin = new Vector2(0.5f, 0f);
            prt.anchorMax = new Vector2(0.5f, 0f);
            prt.pivot = new Vector2(0.5f, 0f);
            prt.anchoredPosition = new Vector2(0f, 32f);
            prt.sizeDelta = new Vector2(980f, 232f);
            var backing = _panel.GetComponent<Image>();
            backing.color = new Color(0.05f, 0.04f, 0.1f, 0.72f);
            // The backing IS the cancel region (unit 7): keep it a raycast target.
            backing.raycastTarget = true;

            // rev 4-6 — 타겟팅 화살표는 패널 뒤 sibling 으로 붙여 카드 위에 그려진다.
            _targetArrow = DreamcatcherTargetArrow.Create(transform);
        }

        private void EnsureSlots(int count)
        {
            if (_slots.Count == count) return;
            foreach (var s in _slots) if (s.root != null) Destroy(s.root);
            _slots.Clear();

            float cardW = 172f, cardH = 200f, spacing = 16f;
            float total = count * cardW + (count - 1) * spacing;
            for (int i = 0; i < count; i++)
            {
                var slot = new CardSlot();
                slot.root = new GameObject($"Card_{i}", typeof(RectTransform), typeof(Image), typeof(CanvasGroup));
                slot.root.transform.SetParent(_panel.transform, false);
                slot.rect = (RectTransform)slot.root.transform;
                slot.rect.anchorMin = new Vector2(0.5f, 0f);
                slot.rect.anchorMax = new Vector2(0.5f, 0f);
                slot.rect.pivot = new Vector2(0.5f, 0f);
                float x = -total * 0.5f + cardW * 0.5f + i * (cardW + spacing);
                slot.rect.anchoredPosition = new Vector2(x, 16f);
                slot.rect.sizeDelta = new Vector2(cardW, cardH);
                // slight fan: outer cards tilt more (StS nod without arc math)
                float mid = (count - 1) * 0.5f;
                slot.rect.localEulerAngles = new Vector3(0f, 0f, -(i - mid) * fanAngle);

                slot.frame = slot.root.GetComponent<Image>();
                slot.group = slot.root.GetComponent<CanvasGroup>();

                var artGO = new GameObject("Art", typeof(RectTransform), typeof(Image));
                artGO.transform.SetParent(slot.root.transform, false);
                var art = (RectTransform)artGO.transform;
                art.anchorMin = Vector2.zero;
                art.anchorMax = Vector2.one;
                art.offsetMin = new Vector2(6f, 6f);
                art.offsetMax = new Vector2(-6f, -6f);
                slot.art = artGO.GetComponent<Image>();
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
                slot.homeRotZ = slot.rect.localEulerAngles.z;
                slot.dragSlot = slot.root.AddComponent<DreamcatcherCardDragSlot>();
                slot.dragSlot.Bind(this, i);

                _slots.Add(slot);
            }
            UiLayer.Apply(gameObject);
        }
    }
}
