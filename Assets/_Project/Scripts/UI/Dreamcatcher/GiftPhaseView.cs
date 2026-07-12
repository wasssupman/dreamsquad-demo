using System.Collections.Generic;
using PrimeTween;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Wassup.Core;
using Wassup.Data;
using Wassup.UI.Layout;

namespace Wassup.UI
{
    // gift-phase unit 3~5 — 선물 페이즈 풀스크린 뷰. 배치 직전 진입 신호
    // (DraftConfirmed / PlacementRequested) 와 재시작(BattleBridge)을 받아
    // GamePhase.Gift 로 전환하고, DreamcatcherHandController 가 구성한 확정 12장을
    // 발라트로식 셔플 연출로 보여준 뒤 PlacementPhaseView.BeginPlacementPhase() 로 넘긴다.
    //
    // 착지 순서 == 실제 사이클 큐 순서(계약 3): pre-shuffle 슬롯 k = entryId k 이고,
    // GiftFinalOrder()(=deck.Hand(전체))의 entryId 로 최종 위치를 매핑한다.
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
        // 각성 버튼(우하단 dock, 앵커 1,0 · -40,220 · 250x96)에 대응하는 cardsRoot(중앙) 로컬
        // 근사 좌표(고정). AwakeningPanel 은 배치 전까지 inactive 라 rect 직접 참조를 피한다(m4).
        private static readonly Vector2 FlyTarget = new Vector2(780f, -300f);

        private static readonly Color Dim = new Color(0.03f, 0.04f, 0.08f, 0.92f);
        private static readonly Color FallbackNormal = new Color(0.22f, 0.28f, 0.44f, 1f);
        private static readonly Color FallbackSubconscious = new Color(0.46f, 0.28f, 0.62f, 1f);

        private GameObject _panel;
        private TextMeshProUGUI _title;
        private CanvasGroup _titleGroup;
        private RectTransform _cardsRoot;
        private readonly List<GiftCardWidget> _cardWidgets = new();
        private Sequence _seq;
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
            StopSequence();
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
            _panel.SetActive(true);

            // Test 모드 fast-forward: 연출 스킵(현재 TestModeContext 는 배치 전 Clear 되어
            // 거의 미발동 — 향후 테스트 훅이 컨텍스트를 유지하면 즉시 배치로).
            if (giftConfig != null && giftConfig.fastForwardInTestMode && TestModeContext.Active)
            {
                ProceedToPlacement();
                return;
            }
            PlayGiftSequence();
        }

        private void ProceedToPlacement()
        {
            // BeginPlacementPhase 를 먼저(도달 보장, review m4). 그 SetPhase(Placement) 가
            // OnPhaseChanged 를 통해 패널 숨김 + 시퀀스 정리를 수행한다.
            if (placementPhaseView != null) placementPhaseView.BeginPlacementPhase();
            else if (_panel != null) _panel.SetActive(false);
        }

        private void OnPhaseChanged(GamePhase phase)
        {
            if (phase != GamePhase.Gift && _panel != null)
            {
                StopSequence();
                _panel.SetActive(false);
            }
        }

        private void StopSequence()
        {
            // 모든 트윈은 _seq 의 멤버라 Sequence.Stop() 이 함께 정리한다.
            if (_seq.isAlive) _seq.Stop();
        }

        // ── sequence (unit 4 core + unit 5 juice) ─────────────────────────────
        private void PlayGiftSequence()
        {
            StopSequence();
            CancelInvoke();

            var kind = handController.GiftKind;
            _title.text = kind == GiftKind.Lucid ? "루시드의 선물" : "림의 선물";

            var baseCards = handController.GiftBaseCards;   // 10 (pre-shuffle 앞부분)
            var giftCards = handController.GiftAddedCards;  // 2  (pre-shuffle 뒷부분)
            var finalOrder = handController.GiftFinalOrder(); // 확정 순서(entryId 포함)
            int n = finalOrder.Count;
            int baseN = baseCards.Count;

            // entryId → 최종 순서 인덱스. pre-shuffle 슬롯 k 의 entryId 는 k(삽입 순서).
            var finalPos = new Dictionary<int, int>();
            for (int f = 0; f < finalOrder.Count; f++) finalPos[finalOrder[f].entryId] = f;

            EnsureCardWidgets(n);
            for (int k = 0; k < _cardWidgets.Count; k++)
            {
                bool used = k < n;
                _cardWidgets[k].go.SetActive(used);
                if (!used) continue;
                var card = k < baseN ? baseCards[k] : giftCards[k - baseN];
                BindCardVisual(_cardWidgets[k], card);
                var rt = _cardWidgets[k].rt;
                rt.anchoredPosition = new Vector2(PreX(k, n), 80f); // 위에서 드롭인
                rt.localScale = Vector3.zero;                        // 스케일 팝인
            }

            // 타이틀 초기 상태
            _titleGroup.alpha = 0f;
            _title.rectTransform.localScale = Vector3.one * 0.6f;

            float intro = giftConfig != null ? giftConfig.introTextSec : 1f;
            float baseIn = giftConfig != null ? giftConfig.baseCardsInSec : 0.5f;
            float appendDelay = giftConfig != null ? giftConfig.giftAppendDelaySec : 1f;
            float appendDur = giftConfig != null ? giftConfig.giftAppendSec : 0.5f;
            float shuffle = giftConfig != null ? giftConfig.shuffleSec : 2f;
            float hold = giftConfig != null ? giftConfig.holdSec : 2f;
            float flyOut = giftConfig != null ? giftConfig.flyOutSec : 0.6f;

            float introIn = intro * 0.3f, introHold = intro * 0.4f, introOut = intro * 0.3f;

            _seq = Sequence.Create();

            // 4-1 "X의 선물" 텍스트 등장 → 멋지게 사라짐
            _seq.Chain(Tween.Alpha(_titleGroup, 1f, introIn, Ease.OutQuad))
                .Group(Tween.Scale(_title.rectTransform, Vector3.one, introIn, Ease.OutBack));
            _seq.ChainDelay(introHold);
            _seq.Chain(Tween.Alpha(_titleGroup, 0f, introOut, Ease.InQuad))
                .Group(Tween.Scale(_title.rectTransform, Vector3.one * 1.35f, introOut, Ease.InQuad));

            // 4-2a 보유 10장 등장(스태거 드롭인). baseCardsInSec 이 스태거 창과 카드
            // 드롭인 길이를 함께 스케일하도록 duration 도 config 파생(review m5).
            float baseStagger = baseN > 1 ? baseIn / baseN : 0f;
            float cardInDur = Mathf.Clamp(baseIn * 0.5f, 0.2f, 0.5f);
            for (int k = 0; k < baseN; k++)
            {
                var rt = _cardWidgets[k].rt;
                float d = k * baseStagger;
                ChainOrGroup(k == 0,
                    Tween.UIAnchoredPosition(rt, new Vector2(PreX(k, n), 0f), cardInDur, Ease.OutQuad, startDelay: d));
                _seq.Group(Tween.Scale(rt, Vector3.one, cardInDur, Ease.OutBack, startDelay: d));
            }

            // 4-2b 1초 뒤 선물 2장이 임팩트 있게 덱 뒤에 배열
            _seq.ChainDelay(appendDelay);
            for (int g = 0; g < giftCards.Count; g++)
            {
                int k = baseN + g;
                if (k >= n) break;
                var rt = _cardWidgets[k].rt;
                float d = g * 0.12f;
                ChainOrGroup(g == 0,
                    Tween.UIAnchoredPosition(rt, new Vector2(PreX(k, n), 0f), appendDur, Ease.OutBack, startDelay: d));
                _seq.Group(Tween.Scale(rt, Vector3.one * 1.12f, appendDur * 0.6f, Ease.OutBack, startDelay: d));
                _seq.Group(Tween.Scale(rt, Vector3.one, appendDur * 0.4f, Ease.OutQuad, startDelay: d + appendDur * 0.6f));
            }

            // 4-3 촤라락 셔플: 중앙으로 모였다가 확정 순서로 재배열
            float scatterDur = shuffle * 0.45f, settleDur = shuffle * 0.55f;
            for (int k = 0; k < n; k++)
            {
                var rt = _cardWidgets[k].rt;
                float jitterY = ((k % 3) - 1) * 55f;               // index 기반 결정론 지터
                ChainOrGroup(k == 0,
                    Tween.UIAnchoredPosition(rt, new Vector2(PreX(k, n) * 0.12f, jitterY), scatterDur,
                        Ease.InOutSine, startDelay: (k % 4) * 0.03f));
            }
            for (int k = 0; k < n; k++)
            {
                var rt = _cardWidgets[k].rt;
                int f = finalPos.TryGetValue(k, out var idx) ? idx : k;
                ChainOrGroup(k == 0,
                    Tween.UIAnchoredPosition(rt, new Vector2(FinalX(f, n), 0f), settleDur, Ease.OutBack,
                        startDelay: f * 0.02f));
            }

            // 4-4 확인 홀드
            _seq.ChainDelay(hold);

            // 4-5 12장이 각성 버튼으로 날아가며 scale→0
            for (int k = 0; k < n; k++)
            {
                var rt = _cardWidgets[k].rt;
                float d = k * 0.03f;
                ChainOrGroup(k == 0,
                    Tween.UIAnchoredPosition(rt, FlyTarget, flyOut, Ease.InBack, startDelay: d));
                _seq.Group(Tween.Scale(rt, Vector3.zero, flyOut, Ease.InBack, startDelay: d));
            }

            // 4-6 배치 진입
            _seq.ChainCallback(ProceedToPlacement);
        }

        private void ChainOrGroup(bool chain, Tween t)
        {
            if (chain) _seq.Chain(t); else _seq.Group(t);
        }

        private static float PreX(int k, int n) => (k - (n - 1) * 0.5f) * CardSpacing;
        private static float FinalX(int f, int n) => (f - (n - 1) * 0.5f) * CardSpacing;

        private void BindCardVisual(GiftCardWidget w, DreamcatcherCard card)
        {
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
            _titleGroup = titleGO.AddComponent<CanvasGroup>();

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
