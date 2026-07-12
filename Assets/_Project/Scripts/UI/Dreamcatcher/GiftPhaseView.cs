using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Wassup.Core;
using Wassup.Data;
using Wassup.UI.Layout;

namespace Wassup.UI
{
    // gift-phase unit 3 — 선물 페이즈 풀스크린 뷰. 배치 직전 진입 신호
    // (DraftConfirmed / PlacementRequested) 와 재시작(BattleBridge)을 받아
    // GamePhase.Gift 로 전환하고, DreamcatcherHandController 가 구성한 확정 12장을
    // 보여준 뒤 PlacementPhaseView.BeginPlacementPhase() 로 넘긴다.
    // 이 단계(unit 3)는 정적 레이아웃 + 라우팅; 발라트로식 연출/트위닝은 unit 4~5.
    public class GiftPhaseView : MonoBehaviour
    {
        [SerializeField] private GameManager gameManager;
        [SerializeField] private DraftController draftController;
        [SerializeField] private DreamcatcherHandController handController;
        [SerializeField] private PlacementPhaseView placementPhaseView;
        [SerializeField] private GiftConfig giftConfig;

        private const int SortingOrder = 30; // 배치 HUD(7)/각성(7) 위
        private const float CardW = 130f;
        private const float CardH = 180f;
        private const float CardSpacing = 138f;

        private static readonly Color Dim = new Color(0.03f, 0.04f, 0.08f, 0.92f);
        private static readonly Color FallbackNormal = new Color(0.22f, 0.28f, 0.44f, 1f);
        private static readonly Color FallbackSubconscious = new Color(0.46f, 0.28f, 0.62f, 1f);

        private GameObject _panel;
        private TextMeshProUGUI _title;
        private RectTransform _cardsRoot;
        private readonly List<GiftCardWidget> _cardWidgets = new();
        private bool _built;

        private struct GiftCardWidget
        {
            public GameObject go;
            public RectTransform rt;
            public Image art;
            public TextMeshProUGUI name;
        }

        private void Awake()
        {
            BuildCanvas();
            if (_panel != null) _panel.SetActive(false);
        }

        private void OnEnable()
        {
            if (draftController != null) draftController.DraftConfirmed += BeginGift;
            if (gameManager != null) gameManager.PlacementRequested += BeginGift;
            if (handController != null) handController.GiftDeckReady += OnGiftDeckReady;
            if (gameManager != null) gameManager.PhaseChanged += OnPhaseChanged;
        }

        private void OnDisable()
        {
            if (draftController != null) draftController.DraftConfirmed -= BeginGift;
            if (gameManager != null) gameManager.PlacementRequested -= BeginGift;
            if (handController != null) handController.GiftDeckReady -= OnGiftDeckReady;
            if (gameManager != null) gameManager.PhaseChanged -= OnPhaseChanged;
            CancelInvoke();
        }

        // 진입점(라우팅): 드래프트 확정 / 배치 요청 / 재시작(BattleBridge)이 호출.
        public void BeginGift()
        {
            // 미배선 폴백: 그대로 배치로(HandController 가 Placement 에서 폴백 구성).
            if (handController == null || gameManager == null)
            {
                if (placementPhaseView != null) placementPhaseView.BeginPlacementPhase();
                return;
            }
            // SetPhase(Gift) → HandController.BuildGiftDeck → GiftDeckReady → OnGiftDeckReady.
            gameManager.SetPhase(GamePhase.Gift);
        }

        private void OnGiftDeckReady()
        {
            if (!_built) BuildCanvas();
            ShowLayout();
            _panel.SetActive(true);
            float hold = giftConfig != null ? giftConfig.holdSec : 2f;
            CancelInvoke(nameof(ProceedToPlacement));
            Invoke(nameof(ProceedToPlacement), Mathf.Max(0.1f, hold));
        }

        private void ProceedToPlacement()
        {
            if (_panel != null) _panel.SetActive(false);
            if (placementPhaseView != null) placementPhaseView.BeginPlacementPhase();
        }

        private void OnPhaseChanged(GamePhase phase)
        {
            if (phase != GamePhase.Gift && _panel != null) _panel.SetActive(false);
        }

        // ── layout ──────────────────────────────────────────────────────────
        private void ShowLayout()
        {
            var kind = handController.GiftKind;
            if (_title != null)
                _title.text = kind == GiftKind.Lucid ? "루시드의 선물" : "림의 선물";

            var order = handController.GiftFinalOrder(); // 확정 12장(사이클 큐 초기 순서)
            EnsureCardWidgets(order.Count);
            for (int i = 0; i < _cardWidgets.Count; i++)
            {
                bool active = i < order.Count;
                _cardWidgets[i].go.SetActive(active);
                if (active) BindCard(_cardWidgets[i], order[i].card, i, order.Count);
            }
        }

        private void BindCard(GiftCardWidget w, DreamcatcherCard card, int index, int count)
        {
            float x = (index - (count - 1) * 0.5f) * CardSpacing;
            w.rt.anchoredPosition = new Vector2(x, 0f);
            w.rt.localScale = Vector3.one;

            if (card != null && card.art != null)
            {
                w.art.sprite = card.art;
                w.art.color = Color.white;
            }
            else
            {
                w.art.sprite = null;
                w.art.color = (card != null && card.category == CardCategory.Subconscious)
                    ? FallbackSubconscious : FallbackNormal;
            }
            if (w.name != null) w.name.text = card != null ? card.displayName : "";
        }

        private void EnsureCardWidgets(int count)
        {
            while (_cardWidgets.Count < count)
                _cardWidgets.Add(MakeCard(_cardWidgets.Count));
        }

        private GiftCardWidget MakeCard(int i)
        {
            var go = new GameObject($"GiftCard{i}", typeof(RectTransform), typeof(Image));
            var rt = (RectTransform)go.transform;
            rt.SetParent(_cardsRoot, false);
            rt.sizeDelta = new Vector2(CardW, CardH);
            var art = go.GetComponent<Image>();
            art.preserveAspect = true;

            var labelGO = new GameObject("Name", typeof(RectTransform));
            var lrt = (RectTransform)labelGO.transform;
            lrt.SetParent(rt, false);
            lrt.anchorMin = new Vector2(0f, 0f);
            lrt.anchorMax = new Vector2(1f, 0f);
            lrt.pivot = new Vector2(0.5f, 0f);
            lrt.anchoredPosition = new Vector2(0f, 6f);
            lrt.sizeDelta = new Vector2(-8f, 34f);
            var label = labelGO.AddComponent<TextMeshProUGUI>();
            label.text = "";
            label.fontSize = 18f;
            label.alignment = TextAlignmentOptions.Center;
            label.color = new Color(0.96f, 0.94f, 0.82f, 1f);
            label.raycastTarget = false;

            return new GiftCardWidget { go = go, rt = rt, art = art, name = label };
        }

        private void BuildCanvas()
        {
            if (_built) return;
            var roots = UiCanvasSetup.Ensure(gameObject, SortingOrder);

            _panel = new GameObject("GiftPanel", typeof(RectTransform), typeof(Image));
            var prt = (RectTransform)_panel.transform;
            prt.SetParent(roots.FullBleedRoot, false);
            prt.anchorMin = Vector2.zero; prt.anchorMax = Vector2.one;
            prt.offsetMin = Vector2.zero; prt.offsetMax = Vector2.zero;
            _panel.GetComponent<Image>().color = Dim;

            var titleGO = new GameObject("GiftTitle", typeof(RectTransform));
            var trt = (RectTransform)titleGO.transform;
            trt.SetParent(prt, false);
            trt.anchorMin = new Vector2(0.5f, 1f);
            trt.anchorMax = new Vector2(0.5f, 1f);
            trt.pivot = new Vector2(0.5f, 1f);
            trt.anchoredPosition = new Vector2(0f, -160f);
            trt.sizeDelta = new Vector2(1200f, 120f);
            _title = titleGO.AddComponent<TextMeshProUGUI>();
            _title.text = "";
            _title.fontSize = 88f;
            _title.alignment = TextAlignmentOptions.Center;
            _title.color = new Color(1f, 0.86f, 0.42f, 1f);
            _title.raycastTarget = false;

            var cardsGO = new GameObject("GiftCardsRoot", typeof(RectTransform));
            _cardsRoot = (RectTransform)cardsGO.transform;
            _cardsRoot.SetParent(prt, false);
            _cardsRoot.anchorMin = new Vector2(0.5f, 0.5f);
            _cardsRoot.anchorMax = new Vector2(0.5f, 0.5f);
            _cardsRoot.pivot = new Vector2(0.5f, 0.5f);
            _cardsRoot.anchoredPosition = new Vector2(0f, -20f);
            _cardsRoot.sizeDelta = new Vector2(1800f, 220f);

            UiLayer.Apply(gameObject);
            _built = true;
        }
    }
}
