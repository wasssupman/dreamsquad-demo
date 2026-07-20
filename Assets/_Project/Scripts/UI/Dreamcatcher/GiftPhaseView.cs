using System.Collections.Generic;
using PrimeTween;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Wassup.Core;
using Wassup.Data;
using Wassup.UI.Layout;

namespace Wassup.UI
{
    // gift-phase unit 3 라우팅 + gift-phase-presentation unit 2 안무.
    // 배치 직전 진입 신호(DraftConfirmed / PlacementRequested)와 재시작(BattleBridge)을
    // 받아 GamePhase.Gift 로 전환하고, DreamcatcherHandController 가 구성한 확정 12장을
    // 5 서사 비트로 보여준 뒤 PlacementPhaseView.BeginPlacementPhase() 로 넘긴다:
    //   ①내가 짠 덱(하단 중앙 딜-인, 5×2 그리드) → ②존재의 개입(kind별 강림/침투 + 플립 리빌)
    //   → ③융합(스택 수렴 + 리플 셔플 + 글로우 리플) → ④이번 판의 내 덱(부채꼴 + 스윕)
    //   → ⑤각성치에 장전(수신 앵커 링 + 가속 흡수 + 12번째 피니셔).
    //
    // 착지/부채꼴 순서 == 실제 사이클 큐 순서(gift-phase 계약 3): pre-shuffle 슬롯 k =
    // entryId k 이고, GiftFinalOrder()(=deck.Hand(전체))의 entryId 로 최종 위치를 매핑한다.
    // 좌표·타이밍은 전부 GiftConfig, 좌표 수학은 GiftPhaseLayout(순수), 카드는 GiftCardWidget.
    public class GiftPhaseView : MonoBehaviour
    {
        [SerializeField] private GameManager gameManager;
        [SerializeField] private DraftController draftController;
        [SerializeField] private DreamcatcherHandController handController;
        [SerializeField] private PlacementPhaseView placementPhaseView;
        [SerializeField] private GiftConfig giftConfig;
        // first-session-tutorial unit 7 — 첫 판 연출 억제 / 선물 튜토리얼 판정.
        // 미배선·미로드 세션이면 항상 일반 연출(fail-open).
        [SerializeField] private PlayerProfileSO profileSO;

        private const int SortingOrder = 30; // 배치 HUD(7)/각성(7) 위
        // 각성 버튼(우하단 dock)에 대응하는 cardsRoot(중앙) 로컬 근사 좌표(고정).
        // AwakeningPanel 은 배치 전까지 inactive 라 rect 직접 참조를 피한다(gift-phase m4).
        private static readonly Vector2 FlyTarget = new Vector2(780f, -300f);

        private static readonly Color Dim = new Color(0.03f, 0.04f, 0.08f, 0.92f);

        private GameObject _panel;
        private TextMeshProUGUI _title;
        private CanvasGroup _titleGroup;
        private RectTransform _cardsRoot;
        private Image _revealDim; // unit 6 — 리빌 뒷판 딤 스크림(그리드 위·선물 아래)
        private RectTransform _fxRoot;
        private Image _ambienceTop;
        private Image _ambienceBottom;
        private readonly List<GiftCardWidget> _cardWidgets = new();
        private GiftCardWidget.Shared _shared;
        private Sprite _gradientSprite;
        private Sequence _seq;
        private float _seqStartTime;
        private bool _built;

        // first-session-tutorial unit 7 — 선물 튜토리얼 홀드 seam. 문구는 구독자
        // (FirstSessionTutorialController) 소관 — view 는 홀드 진입/해제와 탭 진행만 소유하고,
        // 구독자가 없어도 탭만으로 진행된다. 튜토리얼 판에선 기존 탭 스킵을 비활성한다.
        public enum GiftTutorialHold { Reveal, Shuffle }
        public event System.Action<GiftTutorialHold> TutorialHoldEntered;
        public event System.Action<GiftTutorialHold> TutorialHoldReleased;
        private bool _tutorialMode;
        private bool _holding;
        private GiftTutorialHold _hold;

        // 탭 스킵(unit 5 rev1) — 전/후반부 판정과 후반부 시퀀스가 쓰는 판 컨텍스트.
        private int _stage; // 0 = 리빌 포커스 전(인트로~플립), 1 = 리빌 이후(홀드~흡수)
        private int _n, _baseN;
        private int[] _keyByF;
        private readonly Dictionary<int, int> _finalPos = new();
        private Color _kindColor;
        private Image _amb;
        private Color _ambColor;

        // 시퀀스 도중 생성되는 일회성 이펙트(잔상/틱/리플/링) — 중단 경로에서도 정리.
        private readonly List<GameObject> _transientFx = new();
        private readonly List<Tween> _fxTweens = new();
        private RectTransform _ring;
        private readonly List<Image> _ringDots = new();

        private void Awake()
        {
            // 한 시퀀스가 ~250 트윈을 동시 보유(최대 소비처) — 기본 풀 200 초과로 인한
            // 런타임 리사이즈 GC/경고를 선예약으로 차단(리뷰).
            PrimeTweenConfig.SetTweensCapacity(400);
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

        private void OnDestroy()
        {
            _shared?.Dispose();
            if (_gradientSprite != null)
            {
                if (_gradientSprite.texture != null) Destroy(_gradientSprite.texture);
                Destroy(_gradientSprite);
                _gradientSprite = null;
            }
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
            // first-session-tutorial unit 7 — 첫 판(핵심 안내 pending)은 선물 연출을 통째로
            // 생략한다. BuildGiftDeck 은 이미 끝났으므로 덱 데이터(12장·순서)는 동일하다.
            if (TutorialProgress.ShouldRunCore(profileSO))
            {
                ProceedToPlacement();
                return;
            }
            _tutorialMode = TutorialProgress.ShouldRunGiftTutorial(profileSO);

            if (!_built) BuildCanvas();
            _panel.SetActive(true);

            // 미배선 관용(리뷰): config 없이는 안무 불가 — 연출 없이 배치로.
            if (giftConfig == null)
            {
                Debug.LogWarning("[GiftPhaseView] giftConfig 미배선 — 연출 생략, 즉시 배치로.");
                ProceedToPlacement();
                return;
            }

            // Test 모드 fast-forward: 연출 스킵(현재 TestModeContext 는 배치 전 Clear 되어
            // 거의 미발동 — 향후 테스트 훅이 컨텍스트를 유지하면 즉시 배치로).
            if (giftConfig.fastForwardInTestMode && TestModeContext.Active)
            {
                ProceedToPlacement();
                return;
            }
            PlayGiftSequence();
        }

        private void ProceedToPlacement()
        {
            // BeginPlacementPhase 를 먼저(도달 보장, gift-phase m4). 그 SetPhase(Placement) 가
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
            _holding = false; // 홀드 중 이탈 — 잔여 이벤트 발행 없이 종료(unit 7)
            if (_seq.isAlive) _seq.Stop();
            foreach (var t in _fxTweens)
                if (t.isAlive) t.Stop();
            _fxTweens.Clear();
            foreach (var go in _transientFx)
                if (go != null) Destroy(go);
            _transientFx.Clear();
            _ring = null;
            _ringDots.Clear();
        }

        // 탭 스킵 (unit 5 rev1) — 2단: 리빌 전 탭은 리빌 포커스로 점프(받은 카드 확인은
        // 건너뛰지 않는다), 리빌 이후 탭은 배치로. 덱 순서는 데이터 확정 선행이라 연출만 끊는다.
        // 튜토리얼 모드(unit 7)는 홀드 탭 = 진행이 유일한 입력 — 스킵은 비활성.
        private void OnPanelTapped()
        {
            var cfg = giftConfig;
            if (cfg == null) return;
            if (Time.unscaledTime - _seqStartTime < cfg.tapSkipGraceSec) return;

            if (_tutorialMode)
            {
                if (!_holding) return;
                _holding = false;
                var hold = _hold;
                TutorialHoldReleased?.Invoke(hold);
                if (hold == GiftTutorialHold.Reveal) PlayStackConverge();
                else PlayShuffleToAbsorb();
                return;
            }

            if (!_seq.isAlive || !cfg.tapSkipEnabled) return;
            if (_stage == 0)
            {
                SkipToRevealFocus();
                return;
            }
            StopSequence();
            ProceedToPlacement();
        }

        // 탭 스킵 착지: 전반부 시퀀스를 끊고 리빌 포커스 상태(그리드 정착 + 선물 앞면·과시
        // 스케일 + 앰비언스 온)를 직접 세팅한 뒤 후반부(홀드~흡수)를 시작한다.
        private void SkipToRevealFocus()
        {
            var cfg = giftConfig;
            if (_seq.isAlive) _seq.Stop();

            _titleGroup.alpha = 0f;
            _amb.color = _ambColor;
            _cardsRoot.localScale = Vector3.one;
            // unit 6 — 착지 상태에 딤 포함(자연 진행과 동일한 리빌 포커스 화면).
            _revealDim.enabled = true;
            _revealDim.color = new Color(0f, 0f, 0f, cfg.revealDimAlpha);
            int giftN = _n - _baseN;
            for (int k = 0; k < _n && k < _cardWidgets.Count; k++)
            {
                var w = _cardWidgets[k];
                if (k < _baseN)
                {
                    w.Rt.anchoredPosition = GridSlot(k, _baseN, cfg);
                    w.Rt.localRotation = Quaternion.Euler(0f, 0f, ((k % 3) - 1) * cfg.restTiltDeg);
                    w.Rt.localScale = Vector3.one;
                }
                else
                {
                    w.Rt.anchoredPosition = new Vector2(RevealX(k - _baseN, giftN, cfg), cfg.revealY);
                    w.Rt.localRotation = Quaternion.identity;
                    w.Rt.localScale = Vector3.one * cfg.revealScale;
                    w.SetFace(true);
                }
            }
#if UNITY_ANDROID || UNITY_IOS
            if (cfg.vibrateOnReveal && !Application.isEditor) Handheld.Vibrate();
#endif
            _seqStartTime = Time.unscaledTime; // 재-grace — 연속 탭이 리빌까지 날리는 것 방지
            PlayFromRevealFocus();
        }

        // _panel Dim Image(풀블리드, raycastTarget on)에 부착되는 경량 탭 캐처.
        private sealed class TapCatcher : MonoBehaviour, IPointerClickHandler
        {
            public System.Action Clicked;
            public void OnPointerClick(PointerEventData eventData) => Clicked?.Invoke();
        }

        // ── 안무 (unit 2) ──────────────────────────────────────────────────
        private void PlayGiftSequence()
        {
            StopSequence();
            _seqStartTime = Time.unscaledTime;
            _stage = 0;

            var cfg = giftConfig;
            var kind = handController.GiftKind;
            bool lucid = kind == GiftKind.Lucid;
            Color kindColor = _kindColor = lucid ? cfg.lucidFrameColor : cfg.rimFrameColor;

            _title.text = lucid ? "루시드의 선물" : "림의 선물";
            _title.color = kindColor;

            var baseCards = handController.GiftBaseCards;   // 10 (pre-shuffle 앞부분)
            var giftCards = handController.GiftAddedCards;  // 2  (pre-shuffle 뒷부분)
            var finalOrder = handController.GiftFinalOrder(); // 확정 순서(entryId 포함)
            int n = _n = finalOrder.Count;
            int baseN = _baseN = baseCards.Count;

            // entryId → 최종 순서 인덱스. pre-shuffle 슬롯 k 의 entryId 는 k(삽입 순서).
            // 후반부 시퀀스(부채꼴/흡수)가 쓰므로 필드에 보관(탭 스킵 경로 공유).
            _finalPos.Clear();
            _keyByF = new int[n];
            for (int f = 0; f < finalOrder.Count; f++)
            {
                _finalPos[finalOrder[f].entryId] = f;
                _keyByF[f] = finalOrder[f].entryId;
            }

            EnsureCardWidgets(n);
            _cardsRoot.localScale = Vector3.one; // 중단된 squash 펀치 잔여 리셋(리뷰)

            // 초기 상태 — 내 덱은 하단 중앙 화면 밖("나로부터"), 선물은 kind 방향 밖 뒷면.
            int giftN = n - baseN;
            float revealFromY = lucid ? cfg.offscreenY : -cfg.offscreenY;
            for (int k = 0; k < _cardWidgets.Count; k++)
            {
                var w = _cardWidgets[k];
                bool used = k < n;
                w.Go.SetActive(used);
                if (!used) continue;

                bool gifted = k >= baseN;
                // 카운트 불일치 관용(리뷰): 초과분은 폴백 비주얼로 바인딩.
                var card = gifted
                    ? (k - baseN < giftCards.Count ? giftCards[k - baseN] : null)
                    : baseCards[k];
                w.Bind(card, cfg);
                w.SetOrigin(gifted ? (lucid ? GiftOrigin.Lucid : GiftOrigin.Rim) : GiftOrigin.None,
                            cfg, _shared);
                w.SetFace(!gifted); // 선물만 뒷면으로 등장(플립 리빌)
                w.Rt.localScale = Vector3.one;
                w.Rt.localRotation = Quaternion.identity;
                w.Rt.anchoredPosition = gifted
                    ? new Vector2(RevealX(k - baseN, giftN, cfg), revealFromY)
                    : new Vector2(0f, -cfg.offscreenY);
                w.Rt.SetSiblingIndex(k);
            }
            // unit 6 — 딤을 그리드/선물 사이에 삽입 + 재시작 리셋(잔류 방지).
            _revealDim.color = new Color(0f, 0f, 0f, 0f);
            _revealDim.enabled = false;
            _revealDim.rectTransform.SetSiblingIndex(baseN);

            // 앰비언스 — Lucid 는 상단 금빛, Rim 은 하단 적색. 페이즈 내내 유지.
            var amb = _amb = lucid ? _ambienceTop : _ambienceBottom;
            var ambOff = lucid ? _ambienceBottom : _ambienceTop;
            var ambColor = _ambColor = lucid ? cfg.lucidAmbientColor : cfg.rimAmbientColor;
            amb.color = new Color(ambColor.r, ambColor.g, ambColor.b, 0f);
            ambOff.color = Color.clear;

            _titleGroup.alpha = 0f;
            _title.rectTransform.localScale = Vector3.one * 0.6f;

            _seq = Sequence.Create();

            // ① 인트로 = 존재의 등장 (타이틀 아웃은 딜-인과 겹침)
            float introIn = cfg.introTextSec * 0.3f;
            float introHold = cfg.introTextSec * 0.3f;
            float introOut = cfg.introTextSec * 0.4f;
            _seq.Chain(Tween.Alpha(_titleGroup, 1f, introIn, Ease.OutQuad))
                .Group(Tween.Scale(_title.rectTransform, Vector3.one, introIn, Ease.OutBack))
                .Group(Tween.Alpha(amb, ambColor.a, cfg.introTextSec, Ease.OutQuad));
            _seq.ChainDelay(introHold);

            // ② 딜-인 = 내가 짠 덱 (타이틀 아웃과 동시 시작)
            _seq.Chain(Tween.Alpha(_titleGroup, 0f, introOut, Ease.InQuad))
                .Group(Tween.Scale(_title.rectTransform, Vector3.one * 1.3f, introOut, Ease.InQuad));
            float dealFlight = Mathf.Clamp(cfg.dealInSec * 0.3f, 0.18f, 0.4f);
            float dealStagger = baseN > 1 ? (cfg.dealInSec - dealFlight) / (baseN - 1) : 0f;
            for (int k = 0; k < baseN; k++)
            {
                var rt = _cardWidgets[k].Rt;
                float d = k * dealStagger;
                var slot = GridSlot(k, baseN, cfg);
                rt.localRotation = Quaternion.Euler(0f, 0f, -cfg.dealSpinDeg); // 딜러 스핀 시작 각
                _seq.Group(Tween.UIAnchoredPosition(rt, slot, dealFlight, Ease.OutQuad, startDelay: d));
                _seq.Group(Tween.LocalRotation(rt, Quaternion.Euler(0f, 0f, ((k % 3) - 1) * cfg.restTiltDeg),
                    dealFlight, Ease.OutBack, startDelay: d));
            }
            // 10번째 착지 → 그리드 전체 미세 펄스(덱 완성 박자)
            for (int k = 0; k < baseN; k++)
                _seq.Group(Tween.PunchScale(_cardWidgets[k].Rt, Vector3.one * cfg.gridPulseScale, cfg.gridPulseSec,
                    startDelay: cfg.dealInSec + 0.02f));

            // ③ 선물 리빌 = 존재의 개입 (kind 방향에서 뒷면 진입 → 내 덱 움찔 → 플립)
            // 과시 스케일로 홀드(revealHoldSec)까지 유지 — 축소는 스택 편입 비행에서(원근 서사).
            float approach = cfg.revealSec * 0.4f;
            float flipHalf = cfg.revealSec * 0.15f;
            float flaunt = cfg.revealSec * 0.45f;
            for (int g = 0; g < n - baseN; g++)
            {
                int k = baseN + g;
                var rt = _cardWidgets[k].Rt;
                var target = new Vector2(RevealX(g, giftN, cfg), cfg.revealY);
                ChainOrGroup(g == 0,
                    Tween.UIAnchoredPosition(rt, target, approach, Ease.OutQuad, startDelay: g * 0.07f));
            }
            // unit 6 — 뒷판 딤 페이드 인(개입과 동시) — 선물 강조, 그리드 톤 다운.
            _seq.Group(Tween.Custom(_revealDim, 0f, cfg.revealDimAlpha, cfg.revealDimFadeSec,
                (img, a) => { img.enabled = a > 0f; img.color = new Color(0f, 0f, 0f, a); }, Ease.OutQuad));
            // 내 덱 움찔 — 존재 반대쪽으로 밀렸다가 복귀(진입과 동시).
            float nudge = lucid ? -cfg.presenceNudge : cfg.presenceNudge;
            for (int k = 0; k < baseN; k++)
            {
                var rt = _cardWidgets[k].Rt;
                var slot = GridSlot(k, baseN, cfg);
                _seq.Group(Tween.UIAnchoredPosition(rt, slot + new Vector2(0f, nudge),
                    approach * 0.5f, Ease.OutQuad, startDelay: (k % 5) * 0.02f));
            }
            for (int k = 0; k < baseN; k++)
            {
                var rt = _cardWidgets[k].Rt;
                _seq.Group(Tween.UIAnchoredPosition(rt, GridSlot(k, baseN, cfg),
                    approach * 0.5f, Ease.OutBack, startDelay: approach * 0.5f + (k % 5) * 0.02f));
            }
            // 플립 — scale.x 0 에서 face 스왑 후 과시 스케일로 펼침 + 배경 펄스.
            for (int g = 0; g < n - baseN; g++)
            {
                int k = baseN + g;
                var rt = _cardWidgets[k].Rt;
                ChainOrGroup(g == 0,
                    Tween.Scale(rt, new Vector3(0f, 1f, 1f), flipHalf, Ease.InQuad, startDelay: g * 0.05f));
            }
            _seq.ChainCallback(() =>
            {
                for (int g = baseN; g < n; g++) _cardWidgets[g].SetFace(true);
#if UNITY_ANDROID || UNITY_IOS
                if (cfg.vibrateOnReveal && !Application.isEditor) Handheld.Vibrate();
#endif
            });
            for (int g = 0; g < n - baseN; g++)
            {
                int k = baseN + g;
                var rt = _cardWidgets[k].Rt;
                ChainOrGroup(g == 0,
                    Tween.Scale(rt, Vector3.one * cfg.revealScale, flaunt, Ease.OutBack, startDelay: g * 0.05f));
            }
            _seq.Group(Tween.Alpha(amb, Mathf.Min(1f, ambColor.a * 2.2f), flaunt * 0.4f, Ease.OutQuad));
            _seq.Group(Tween.Alpha(amb, ambColor.a, flaunt * 0.6f, Ease.InQuad, startDelay: flaunt * 0.4f));
            // 리빌 포커스 도달 — 이후는 후반부 시퀀스로(자연 진행과 탭 스킵 착지 공유, unit 5 rev1).
            _seq.ChainCallback(PlayFromRevealFocus);
        }

        // 후반부: 리빌 포커스 상태에서 시작 — 읽기 홀드 → 스택 수렴 → 리플 → 부채꼴 → 흡수.
        // 튜토리얼 모드(unit 7)는 여기서 홀드 1(리빌 문구) — 탭이 스택 수렴을 재개한다.
        private void PlayFromRevealFocus()
        {
            _stage = 1;
            if (_tutorialMode)
            {
                EnterHold(GiftTutorialHold.Reveal);
                return;
            }
            PlayStackConverge();
        }

        // 홀드 진입 — 시퀀스는 이미 끝난 상태(연출 정지). 해제는 OnPanelTapped 만이 한다.
        private void EnterHold(GiftTutorialHold hold)
        {
            _holding = true;
            _hold = hold;
            _seqStartTime = Time.unscaledTime; // 홀드 진입 직후 오탭 방지 — grace 재사용
            TutorialHoldEntered?.Invoke(hold);
        }

        // ④-a 스택 수렴 구간. 일반 모드는 읽기 홀드 후 자동 시작해 셔플로 이어지고,
        // 튜토리얼 모드는 리빌 홀드 해제(탭)로 시작해 셔플 직전 홀드 2 에서 멈춘다.
        private void PlayStackConverge()
        {
            var cfg = giftConfig;
            int n = _n, baseN = _baseN;

            _seq = Sequence.Create();
            // 읽기 홀드 — 어떤 카드를 받았는지 확인하는 여유(사용자 결정 2026-07-14, +1s).
            // 튜토리얼 모드는 무기한 리빌 홀드가 이 역할을 대신하므로 생략.
            if (!_tutorialMode) _seq.ChainDelay(cfg.revealHoldSec);

            // ④-a 스택 수렴 = 융합 시작 (선물 2장이 마지막에 파고듦 → 출렁)
            float stackMove = cfg.stackSec * 0.7f;
            for (int k = 0; k < n; k++)
            {
                var rt = _cardWidgets[k].Rt;
                var (jRot, jOff) = GiftPhaseLayout.StackJitter(k, cfg.stackJitterRotDeg, cfg.stackJitterOffset);
                bool gifted = k >= baseN;
                float d = gifted ? cfg.stackSec * 0.3f : (k % 5) * 0.015f;
                ChainOrGroup(k == 0,
                    Tween.UIAnchoredPosition(rt, cfg.stackCenter + jOff, stackMove, Ease.InOutQuad, startDelay: d));
                _seq.Group(Tween.LocalRotation(rt, Quaternion.Euler(0f, 0f, jRot), stackMove,
                    Ease.InOutQuad, startDelay: d));
                if (gifted) // 화면 앞에서 덱으로 — 편입 비행 중 원래 크기로 축소
                    _seq.Group(Tween.Scale(rt, Vector3.one, stackMove, Ease.InQuad, startDelay: d));
            }
            // unit 6 — 딤 페이드 아웃(수렴과 동시) — 스킵 착지 경로 공유(여기가 유일 퇴장점).
            _seq.Group(Tween.Custom(_revealDim, cfg.revealDimAlpha, 0f, stackMove * 0.6f,
                (img, a) => img.color = new Color(0f, 0f, 0f, a), Ease.InQuad));
            _seq.ChainCallback(() =>
            {
                _revealDim.enabled = false; // 픽셀필 절약(잔류 방지)
                for (int k = baseN; k < n; k++) _cardWidgets[k].Rt.SetAsLastSibling();
            });
            _seq.Chain(Tween.PunchScale(_cardsRoot,
                new Vector3(cfg.stackSquashScale * 0.5f, -cfg.stackSquashScale, 0f), 0.12f));

            // 수렴 완료 경계 — 튜토리얼은 셔플 직전 홀드 2, 일반은 곧장 셔플로(타임라인 동일).
            if (_tutorialMode) _seq.ChainCallback(() => EnterHold(GiftTutorialHold.Shuffle));
            else _seq.ChainCallback(PlayShuffleToAbsorb);
        }

        // ④-b 리플 셔플 → ⑤ 부채꼴 → ⑥ 흡수 → 배치 진입. 수렴 직후 콜백(일반) 또는
        // 셔플 홀드 해제 탭(튜토리얼)으로 시작한다.
        private void PlayShuffleToAbsorb()
        {
            var cfg = giftConfig;
            int n = _n, baseN = _baseN;
            Color kindColor = _kindColor;
            var finalPos = _finalPos;
            var keyByF = _keyByF;

            _seq = Sequence.Create();

            // ④-b 리플 셔플 = 융합 (좌/우 분리 → 지퍼 재적층 + 잔상 트레일 + 글로우 리플)
            float split = cfg.riffleSec * 0.25f;
            int half = (n + 1) / 2;
            for (int k = 0; k < n; k++)
            {
                var rt = _cardWidgets[k].Rt;
                bool left = k < half;
                var pilePos = cfg.stackCenter + new Vector2(left ? -cfg.riffleSplitX : cfg.riffleSplitX, 0f);
                ChainOrGroup(k == 0,
                    Tween.UIAnchoredPosition(rt, pilePos, split, Ease.OutQuad, startDelay: (k % 4) * 0.012f));
                _seq.Group(Tween.LocalRotation(rt,
                    Quaternion.Euler(0f, 0f, left ? cfg.riffleTiltDeg : -cfg.riffleTiltDeg),
                    split, Ease.OutQuad, startDelay: (k % 4) * 0.012f));
            }
            var riffle = GiftPhaseLayout.RiffleOrder(n);
            float zipWindow = cfg.riffleSec * 0.75f;
            float zipMove = Mathf.Min(0.14f, zipWindow * 0.3f);
            float zipStagger = n > 1 ? (zipWindow - zipMove) / (n - 1) : 0f;
            for (int w = 0; w < riffle.Length; w++)
            {
                int k = riffle[w];
                var widget = _cardWidgets[k];
                var rt = widget.Rt;
                float d = w * zipStagger;
                var (jRot, jOff) = GiftPhaseLayout.StackJitter(k + 7, cfg.stackJitterRotDeg, cfg.stackJitterOffset);
                ChainOrGroup(w == 0,
                    Tween.UIAnchoredPosition(rt, cfg.stackCenter + jOff, zipMove, Ease.InOutQuad, startDelay: d));
                _seq.Group(Tween.LocalRotation(rt, Quaternion.Euler(0f, 0f, jRot), zipMove,
                    Ease.InOutQuad, startDelay: d));
                if (k >= baseN) // 프레임 카드 — 섞여 드는 궤적 잔상
                {
                    var from = cfg.stackCenter + new Vector2(k < half ? -cfg.riffleSplitX : cfg.riffleSplitX, 0f);
                    _seq.Group(Tween.Delay(d, () => SpawnTrail(from, kindColor, cfg)));
                }
            }
            _seq.ChainCallback(() => SpawnRipple(cfg.stackCenter, kindColor, cfg));
            _seq.ChainDelay(0.06f);

            // ⑤ 부채꼴 딜 = 이번 판의 내 덱 (좌→우 = 최종순서 1→12) + 하이라이트 스윕
            _seq.ChainCallback(() =>
            {
                // 딜러 문법 — 나중 카드가 위로. f 오름차순으로 sibling 재배열.
                for (int f = 0; f < n; f++)
                {
                    int k = keyByF[f];
                    if (k < _cardWidgets.Count) _cardWidgets[k].Rt.SetAsLastSibling();
                }
            });
            float fanMove = Mathf.Min(0.3f, cfg.fanSec * 0.5f);
            float fanStagger = n > 1 ? (cfg.fanSec - fanMove) / (n - 1) : 0f;
            for (int k = 0; k < n; k++)
            {
                var rt = _cardWidgets[k].Rt;
                int f = finalPos.TryGetValue(k, out var idx) ? idx : k;
                var (pos, rot) = GiftPhaseLayout.FanSlot(f, n, cfg.fanRadius, cfg.fanArcDeg, cfg.fanBaseY);
                float d = f * fanStagger;
                ChainOrGroup(k == 0,
                    Tween.UIAnchoredPosition(rt, pos, fanMove, Ease.OutBack, startDelay: d));
                _seq.Group(Tween.LocalRotation(rt, Quaternion.Euler(0f, 0f, rot), fanMove,
                    Ease.OutBack, startDelay: d));
            }
            for (int k = 0; k < n; k++) // 스윕 — 좌→우 순차 펄스, 프레임 카드는 스파크
            {
                int f = finalPos.TryGetValue(k, out var idx) ? idx : k;
                _seq.Group(Tween.PunchScale(_cardWidgets[k].Rt, Vector3.one * cfg.sweepPunchScale, cfg.gridPulseSec,
                    startDelay: fanMove + f * cfg.sweepStaggerSec));
                if (k >= baseN)
                {
                    var (pos, _) = GiftPhaseLayout.FanSlot(f, n, cfg.fanRadius, cfg.fanArcDeg, cfg.fanBaseY);
                    _seq.Group(Tween.Delay(fanMove + f * cfg.sweepStaggerSec, () => SpawnTick(pos, kindColor, cfg, 0.6f)));
                }
            }

            // ⑥ 순차 흡수 = 각성치에 장전 (수신 앵커 링 + 가속 케이던스 + 12번째 피니셔)
            _seq.ChainCallback(() => BuildRing(n, kindColor, cfg));
            float rise = cfg.absorbFlightSec * 0.38f;
            float dive = cfg.absorbFlightSec * 0.62f;
            for (int f = 0; f < n; f++)
            {
                int k = keyByF[f];
                if (k >= _cardWidgets.Count) continue;
                var widget = _cardWidgets[k];
                var rt = widget.Rt;
                var (fanPos, _) = GiftPhaseLayout.FanSlot(f, n, cfg.fanRadius, cfg.fanArcDeg, cfg.fanBaseY);
                float d = GiftPhaseLayout.AbsorbDelay(f, cfg.absorbFirstGapSec, cfg.absorbMinGapSec, cfg.absorbDecay);
                bool finisher = f == n - 1;
                if (finisher) d += cfg.finisherPauseSec; // 반박자 멈춤

                float diveDur = dive * (finisher ? 1.25f : 1f); // 위치·스케일·완료 삼자 일치(리뷰)
                float stretchX = 1f - (cfg.absorbStretchY - 1f) * 0.5f;
                ChainOrGroup(f == 0,
                    Tween.UIAnchoredPosition(rt, fanPos + new Vector2(0f, cfg.absorbRiseY), rise, Ease.OutQuad, startDelay: d));
                _seq.Group(Tween.Scale(rt, new Vector3(stretchX, cfg.absorbStretchY, 1f), rise, Ease.OutQuad, startDelay: d));
                _seq.Group(Tween.UIAnchoredPosition(rt, FlyTarget, diveDur,
                    Ease.InQuad, startDelay: d + rise));
                _seq.Group(Tween.Scale(rt, Vector3.one * cfg.absorbImpactScale, diveDur, Ease.InQuad, startDelay: d + rise));
                int fi = f;
                _seq.Group(Tween.Delay(d + rise + diveDur, () =>
                {
                    widget.Go.SetActive(false);
                    LightSegment(fi, kindColor);
                    SpawnTick(FlyTarget, kindColor, cfg, finisher ? 1.6f : 0.9f);
                }));
            }
            // 링 찰칵 잠금 팝 → 각성 버튼 자리로 수축 소멸 → 배치 진입.
            _seq.ChainDelay(0.05f);
            _seq.ChainCallback(() =>
            {
                if (_ring == null) return;
                _fxTweens.Add(Tween.Scale(_ring, Vector3.one * cfg.ringPopScale, cfg.ringPopSec, Ease.OutQuad));
                _fxTweens.Add(Tween.Scale(_ring, Vector3.zero, cfg.ringShrinkSec, Ease.InBack, startDelay: cfg.ringPopSec));
            });
            _seq.ChainDelay(cfg.ringPopSec + cfg.ringShrinkSec + 0.02f);
            _seq.ChainCallback(ProceedToPlacement);
        }

        private void ChainOrGroup(bool chain, Tween t)
        {
            if (chain) _seq.Chain(t); else _seq.Group(t);
        }

        private Vector2 GridSlot(int k, int count, GiftConfig cfg)
            => cfg.gridCenter + GiftPhaseLayout.GridSlot(k, count, cfg.gridCols, cfg.gridCell);

        // 리빌 센터 무대 g번째 카드 x — 2장이면 ±spread, 3+장도 등간격(리뷰: 겹침 방지).
        private static float RevealX(int g, int giftN, GiftConfig cfg)
            => (g - (giftN - 1) * 0.5f) * 2f * cfg.revealSpreadX;

        // ── 일회성 이펙트 (전부 _transientFx/_fxTweens 등록 — 중단 경로 정리) ──
        private void SpawnTrail(Vector2 pos, Color color, GiftConfig cfg)
        {
            var img = MakeFx("Trail", pos, cfg.cardSize, _shared.PlateSprite);
            img.color = new Color(color.r, color.g, color.b, 0.3f);
            _fxTweens.Add(Tween.Alpha(img, 0f, cfg.trailFadeSec, Ease.OutQuad));
            _fxTweens.Add(Tween.Scale(img.rectTransform, Vector3.one * 0.85f, cfg.trailFadeSec, Ease.OutQuad));
        }

        private void SpawnRipple(Vector2 pos, Color color, GiftConfig cfg)
        {
            var img = MakeFx("Ripple", pos, cfg.cardSize * 1.15f, _shared.FrameSprite);
            img.type = Image.Type.Sliced;
            img.color = new Color(color.r, color.g, color.b, cfg.rippleAlpha);
            _fxTweens.Add(Tween.Scale(img.rectTransform, Vector3.one * 1.9f, cfg.rippleSec, Ease.OutQuad));
            _fxTweens.Add(Tween.Alpha(img, 0f, cfg.rippleSec, Ease.OutQuad));
        }

        private void SpawnTick(Vector2 pos, Color color, GiftConfig cfg, float strength)
        {
            var img = MakeFx("Tick", pos, Vector2.one * cfg.ringDotSize * 4f, _shared.DotSprite);
            img.color = new Color(color.r, color.g, color.b, 0.85f);
            img.rectTransform.localScale = Vector3.one * 0.4f;
            _fxTweens.Add(Tween.Scale(img.rectTransform, Vector3.one * strength, cfg.tickPopSec, Ease.OutQuad));
            _fxTweens.Add(Tween.Alpha(img, 0f, cfg.tickPopSec, Ease.OutQuad));
        }

        private Image MakeFx(string name, Vector2 pos, Vector2 size, Sprite sprite)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            var rt = (RectTransform)go.transform;
            rt.SetParent(_fxRoot, false);
            rt.sizeDelta = size;
            rt.anchoredPosition = pos;
            var img = go.GetComponent<Image>();
            img.sprite = sprite;
            img.raycastTarget = false;
            _transientFx.Add(go);
            return img;
        }

        // 수신 앵커 링 — FlyTarget(각성 버튼 근사)에 n 세그먼트. 카드가 닿을 때마다 점등.
        private void BuildRing(int n, Color color, GiftConfig cfg)
        {
            var ringImg = MakeFx("AwakenRing", FlyTarget, Vector2.one * (cfg.ringRadius * 2f + 16f),
                _shared.RingSprite);
            ringImg.color = new Color(color.r, color.g, color.b, 0f);
            _ring = ringImg.rectTransform;
            _ringDots.Clear();
            for (int i = 0; i < n; i++)
            {
                float ang = (90f - i * 360f / n) * Mathf.Deg2Rad;
                var dotGo = new GameObject($"Dot{i}", typeof(RectTransform), typeof(Image));
                var drt = (RectTransform)dotGo.transform;
                drt.SetParent(_ring, false);
                drt.sizeDelta = Vector2.one * cfg.ringDotSize;
                drt.anchoredPosition = new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * cfg.ringRadius;
                var dot = dotGo.GetComponent<Image>();
                dot.sprite = _shared.DotSprite;
                dot.color = new Color(color.r, color.g, color.b, 0.18f);
                dot.raycastTarget = false;
                _ringDots.Add(dot);
            }
            _fxTweens.Add(Tween.Alpha(ringImg, 0.55f, 0.2f, Ease.OutQuad));
        }

        private void LightSegment(int i, Color color)
        {
            if (i < 0 || i >= _ringDots.Count || _ringDots[i] == null) return;
            _ringDots[i].color = color;
            _fxTweens.Add(Tween.Scale(_ringDots[i].rectTransform, Vector3.one * 1.35f, 0.1f, Ease.OutQuad));
            _fxTweens.Add(Tween.Scale(_ringDots[i].rectTransform, Vector3.one, 0.1f, Ease.InQuad, startDelay: 0.1f));
        }

        private void EnsureCardWidgets(int count)
        {
            _shared ??= GiftCardWidget.Shared.Build(giftConfig);
            while (_cardWidgets.Count < count)
                _cardWidgets.Add(GiftCardWidget.Create(_cardsRoot, $"GiftCard{_cardWidgets.Count}",
                    giftConfig, _shared));
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
            _panel.AddComponent<TapCatcher>().Clicked = OnPanelTapped;

            // kind별 앰비언스 — 상단(루시드 금빛 강림) / 하단(림 적색 침투) 그라데이션.
            _gradientSprite = MakeGradientSprite();
            var grad = _gradientSprite;
            _ambienceTop = MakeAmbience(prt, "AmbienceTop", grad, top: true);
            _ambienceBottom = MakeAmbience(prt, "AmbienceBottom", grad, top: false);

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
            _title.raycastTarget = false;
            _titleGroup = titleGO.AddComponent<CanvasGroup>();

            var cardsGO = new GameObject("GiftCardsRoot", typeof(RectTransform));
            _cardsRoot = (RectTransform)cardsGO.transform;
            _cardsRoot.SetParent(prt, false);
            _cardsRoot.anchorMin = new Vector2(0.5f, 0.5f);
            _cardsRoot.anchorMax = new Vector2(0.5f, 0.5f);
            _cardsRoot.pivot = new Vector2(0.5f, 0.5f);
            _cardsRoot.anchoredPosition = new Vector2(0f, -20f);
            _cardsRoot.sizeDelta = new Vector2(1800f, 900f);

            // unit 6 — 리빌 딤 스크림: 그리드(뒷판) 위 / 선물 카드 아래 sibling 로
            // 끼워 넣는 풀블리드 흑색 1장. 같은 부모(_cardsRoot)라 정렬이 보장된다.
            // 탭 캐치는 패널 Dim 소관 — 여긴 순수 비주얼(raycastTarget=false).
            var dimGO = new GameObject("RevealDim", typeof(RectTransform), typeof(Image));
            var drt = (RectTransform)dimGO.transform;
            drt.SetParent(_cardsRoot, false);
            drt.anchorMin = new Vector2(0.5f, 0.5f);
            drt.anchorMax = new Vector2(0.5f, 0.5f);
            drt.sizeDelta = new Vector2(4000f, 4000f); // cardsRoot 로컬 풀블리드 초과 커버
            _revealDim = dimGO.GetComponent<Image>();
            _revealDim.color = new Color(0f, 0f, 0f, 0f);
            _revealDim.raycastTarget = false;
            _revealDim.enabled = false;

            var fxGO = new GameObject("GiftFxRoot", typeof(RectTransform));
            _fxRoot = (RectTransform)fxGO.transform;
            _fxRoot.SetParent(prt, false);
            _fxRoot.anchorMin = new Vector2(0.5f, 0.5f);
            _fxRoot.anchorMax = new Vector2(0.5f, 0.5f);
            _fxRoot.pivot = new Vector2(0.5f, 0.5f);
            _fxRoot.anchoredPosition = _cardsRoot.anchoredPosition;
            _fxRoot.sizeDelta = _cardsRoot.sizeDelta;

            UiLayer.Apply(gameObject);
            _built = true;
        }

        private static Image MakeAmbience(RectTransform parent, string name, Sprite grad, bool top)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            rt.anchorMin = new Vector2(0f, top ? 0.55f : 0f);
            rt.anchorMax = new Vector2(1f, top ? 1f : 0.45f);
            rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
            if (!top) rt.localEulerAngles = new Vector3(0f, 0f, 180f);
            var img = go.GetComponent<Image>();
            img.sprite = grad;
            img.color = Color.clear;
            img.raycastTarget = false;
            return img;
        }

        // 세로 알파 그라데이션(위 진함 → 아래 투명) — 앰비언스 전용 절차 스프라이트.
        private static Sprite MakeGradientSprite()
        {
            const int h = 64;
            var tex = new Texture2D(1, h, TextureFormat.RGBA32, false)
            {
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear
            };
            for (int y = 0; y < h; y++)
            {
                float a = Mathf.Pow((float)y / (h - 1), 1.6f);
                tex.SetPixel(0, y, new Color(1f, 1f, 1f, a));
            }
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, 1, h), new Vector2(0.5f, 0.5f), 100f);
        }
    }
}
