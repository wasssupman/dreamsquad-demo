using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Wassup.Core;
using Wassup.Core.TimeControl;
using Wassup.Data;

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
        [SerializeField] private AwakeningConfig config;
        [SerializeField] private TMP_FontAsset labelFont;  // Jua — Korean battle UI
        [SerializeField] private TMP_FontAsset numberFont; // Anton — cost digits
        [SerializeField] private float flipHalfDuration = 0.14f;
        [SerializeField] private float fanAngle = 4f; // slight StS-style fan per slot

        public enum HandState { UnitStrip, Hand }
        public HandState State { get; private set; } = HandState.UnitStrip;
        public bool Transitioning => _flip != null;

        // unit 7 consumes these: card slots for drag sources + hand rect for
        // the cancel-region test.
        public RectTransform HandPanelRect => _panel != null ? (RectTransform)_panel.transform : null;
        public IReadOnlyList<CardSlot> Slots => _slots;
        public event System.Action HandRefreshed;

        public class CardSlot
        {
            public GameObject root;
            public RectTransform rect;
            public Image frame;
            public Image art;
            public TextMeshProUGUI nameLabel;
            public GameObject costBadge;
            public TextMeshProUGUI costLabel;
            public CanvasGroup group;
            public int entryId = -1;       // -1 = empty slot
            public DreamcatcherCard card;
            public bool usable;
        }

        private GameObject _panel;
        private readonly List<CardSlot> _slots = new List<CardSlot>();
        private bool _built;
        private Coroutine _flip;
        private TimeLease _slomoLease;

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
            // critic H1 — pending-cancel on toggle is unit 7's hook (PendingCanceler);
            // the view simply flips. Drag slots listen for CloseStarted.
            if (State == HandState.UnitStrip) Open();
            else Close();
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
                    Refresh(); // update only — no state change
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
            StartFlip(from: StripPanel(), to: _panel);
        }

        private void Close()
        {
            if (State == HandState.UnitStrip) return;
            State = HandState.UnitStrip;
            _slomoLease.Dispose();
            StartFlip(from: _panel, to: StripPanel());
        }

        // No animation: phase exits, disable, and Placement resets land here.
        private void ForceClose()
        {
            _slomoLease.Dispose();
            if (_flip != null) { StopCoroutine(_flip); _flip = null; }
            State = HandState.UnitStrip;
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
                if (i < hand.Count)
                    BindCard(slot, hand[i].entryId, hand[i].card);
                else
                    BindEmpty(slot);
            }
            RefreshUsability();
            HandRefreshed?.Invoke();
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

            if (card.art != null)
            {
                slot.art.enabled = true;
                slot.art.sprite = card.art;
                slot.art.color = Color.white;
                slot.nameLabel.text = "";
            }
            else
            {
                // Active cards ship without tarot art: skill uiTint + name fallback.
                slot.art.enabled = true;
                slot.art.sprite = null;
                slot.art.color = card.skill != null ? card.skill.uiTint : new Color(0.35f, 0.3f, 0.5f, 1f);
                slot.nameLabel.text = card.displayName;
            }
        }

        private void BindEmpty(CardSlot slot)
        {
            slot.entryId = -1;
            slot.card = null;
            slot.usable = false;
            slot.frame.color = new Color(1f, 1f, 1f, 0.06f); // empty frame
            slot.art.enabled = false;
            slot.nameLabel.text = "";
            slot.costBadge.SetActive(false);
            slot.group.alpha = 1f;
        }

        // ── canvas build ─────────────────────────────────────────────────────

        private void BuildCanvas()
        {
            if (_built) return;
            _built = true;

            var canvas = gameObject.GetComponent<Canvas>();
            if (canvas == null) canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 5; // above the placement strip (4), below dock (7)
            if (gameObject.GetComponent<CanvasScaler>() == null)
            {
                var scaler = gameObject.AddComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920, 1080);
            }
            if (gameObject.GetComponent<GraphicRaycaster>() == null)
                gameObject.AddComponent<GraphicRaycaster>();

            // Bottom-center hand panel.
            _panel = new GameObject("HandPanel", typeof(RectTransform), typeof(Image));
            _panel.transform.SetParent(transform, false);
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

                var nameGO = new GameObject("Name", typeof(RectTransform));
                nameGO.transform.SetParent(slot.root.transform, false);
                var nrt = (RectTransform)nameGO.transform;
                nrt.anchorMin = new Vector2(0f, 0.5f);
                nrt.anchorMax = new Vector2(1f, 1f);
                nrt.offsetMin = new Vector2(6f, 0f);
                nrt.offsetMax = new Vector2(-6f, -8f);
                slot.nameLabel = nameGO.AddComponent<TextMeshProUGUI>();
                if (labelFont != null) slot.nameLabel.font = labelFont;
                slot.nameLabel.fontSize = 22;
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

                _slots.Add(slot);
            }
            UiLayer.Apply(gameObject);
        }
    }
}
