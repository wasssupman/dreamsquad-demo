using PrimeTween;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Wassup.Bridge;
using Wassup.Core;
using Wassup.UI.Layout;

namespace Wassup.UI
{
    // Phase 6 placement countdown overlay. Sits between Draft confirm and
    // battle start: grants the starting cost, shows a countdown, and lets the
    // player either wait for the timer to elapse or tap START BATTLE to begin.
    public class PlacementPhaseView : MonoBehaviour
    {
        public event System.Action PlacementReady;

        [SerializeField] private BattleBridge bridge;
        // gift-phase-removal — draftController 구독은 이관 후 미사용,
        // 필드 제거.
        [SerializeField] private GameManager gameManager;
        // DirectionAimController is created at runtime by DefenderSelector, so the
        // phase owner observes it through the selector's read-only seam. This is a
        // gameplay safety gate, not tutorial state: normal placement, Skip and the
        // countdown must all wait until a drag/directional aim has settled.
        [SerializeField] private DefenderSelector defenderSelector;

        [Header("자동 시작 카운트다운 (placementPhaseEnabled=false)")]
        [Tooltip("중앙 대형 숫자 폰트. 미지정 시 startLabelFont → TMP 기본 순으로 폴백")]
        [SerializeField] private TMP_FontAsset countdownFont;
        [SerializeField] private float countdownFontSize = 260f;
        [SerializeField] private Color countdownColor = new Color(1f, 0.97f, 0.88f, 1f);
        [Tooltip("마지막 1초와 GO! 강조색")]
        [SerializeField] private Color countdownFinalColor = new Color(1f, 0.72f, 0.25f, 1f);
        [Tooltip("숫자가 바뀔 때 찍히는 펀치 배수")]
        [SerializeField] private float countdownPunchScale = 1.6f;
        [SerializeField] private float countdownPunchDuration = 0.25f;
        [Tooltip("GO! 가 커지며 사라지는 시간. 전투는 이미 시작된 뒤라 연출만 남는다")]
        [SerializeField] private float countdownOutroDuration = 0.35f;

        // ingame-ui-upgrade unit 0 — START 버튼을 우하단(dock 코너)에 배치 + 캐주얼
        // 배경 그래픽 슬롯. startButtonBackground 할당 시 그 스프라이트, 비면 UiRoundedSprite
        // 절차 플레이트(다크 네이비 + 골드 테두리) 폴백. 실제 그래픽은 unit 1(Codex).
        [Header("Start button (casual bg image + TMP label)")]
        [Tooltip("Codex 생성 캐주얼 버튼 배경. 미할당 시 절차 플레이트 폴백")]
        [SerializeField] private Sprite startButtonBackground;
        // 코너에서도 눈에 띄도록 밝은 앰버 바디 + 밝은 골드 림. 폴백 플레이트 톤.
        [SerializeField] private Color startFillColor = new Color(0.96f, 0.58f, 0.14f, 1f);
        [SerializeField] private Color startBorderColor = new Color(1f, 0.9f, 0.55f, 1f);
        [SerializeField] private float startBorderWidth = 5f;
        [SerializeField] private float startCornerRadius = 30f;
        [Tooltip("라벨색(캐주얼 버튼: 크림/화이트 + 아래 외곽선)")]
        [SerializeField] private Color startLabelColor = new Color(1f, 0.97f, 0.88f, 1f);
        [Tooltip("게임용 디스플레이 폰트(Bangers SDF 권장). 미지정 시 TMP 기본")]
        [SerializeField] private TMP_FontAsset startLabelFont;
        [Tooltip("라벨 외곽선 색(스티커 느낌)")]
        [SerializeField] private Color startLabelOutlineColor = new Color(0.35f, 0.13f, 0.02f, 1f);
        [Range(0f, 0.5f)]
        [SerializeField] private float startLabelOutlineWidth = 0.22f;
        [Header("Start button juice (코너에서 잘 보이도록)")]
        [Tooltip("버튼 뒤 골드 오라 색(펄스로 시선 유도)")]
        [SerializeField] private Color startAuraColor = new Color(1f, 0.72f, 0.25f, 0.5f);
        [Tooltip("아이들 브리딩 펄스 배수(1=없음)")]
        [SerializeField] private float startPulseScale = 1.06f;
        [SerializeField] private float startPulsePeriod = 0.9f;

        private GameObject _panel;
        private TextMeshProUGUI _countdownLabel;
        private Button _startButton;
        private RectTransform _startButtonRt;
        private GameObject _startButtonWrap;
        private RectTransform _startAuraRt;
        private Tween _startPulse;
        private Tween _startAuraPulse;
        private float _remaining;
        private bool _active;
        private bool _built;
        private bool _startAvailable;
        // match-intro-phase-toggles — 자동 시작 모드 상태. BeginPlacementPhase 에서 1회 확정하고
        // 창이 도는 동안 재평가하지 않는다.
        private bool _autoStart;
        private GameObject _banner;
        private GameObject _blocker;
        private TextMeshProUGUI _bigLabel;
        private RectTransform _bigLabelRt;
        private CanvasGroup _bigLabelGroup;
        private Sequence _bigSeq;
        private int _shownTick = -1;

        public RectTransform StartButtonRect => _startButtonRt;
        public bool IsPlacementActive => _active;

        private void Awake()
        {
            BuildCanvas();
            if (_panel != null) _panel.SetActive(false);
        }

        // gift-phase unit 3 — 진입 신호(DraftConfirmed/PlacementRequested)는 이제
        // GiftPhaseView 가 받아 선물 페이즈를 거친 뒤 BeginPlacementPhase() 를 직접 호출한다.
        // 여기서 직접 구독하면 선물을 건너뛰고 이중 진입하므로 구독하지 않는다.
        private void OnDisable()
        {
            SetStartJuice(false);
            _startAvailable = false;
            // ⚠ 살아있는 시퀀스는 반드시 멈춘다. pending ChainCallback 을 문 채 파괴되면
            // PrimeTween 이 "OnComplete callback was ignored" 를 에러로 찍는다
            // (GimmickPhaseView.OnDisable 에서 실제로 재현된 경로).
            if (_bigSeq.isAlive) _bigSeq.Stop();
        }

        public void BeginPlacementPhase()
        {
            if (!_built) BuildCanvas();
            var cfg = gameManager != null ? gameManager.CostConfig : null;
            var battleCfg = gameManager != null ? gameManager.BattleConfig : null;

            // match-intro-phase-toggles unit 0 — 배치 창을 열지, 3초 뒤 자동으로 닫을지.
            // 아래 진입 묶음(페이즈 전이·코스트·쿨타임·bridge.BeginPlacement)은 **두 경로 공통**이다:
            // 유닛 트레이가 Placement 진입 신호에서 슬롯을 구성하므로(DefenderSelector.OnPhaseChanged)
            // 페이즈를 건너뛰면 전투 내내 트레이가 빈 채로 남는다.
            _autoStart = PlacementPhasePolicy.UseAutoStart(
                battleCfg == null || battleCfg.placementPhaseEnabled);
            float duration = _autoStart
                ? (battleCfg != null ? battleCfg.autoStartCountdownSeconds : 3f)
                : (cfg != null ? cfg.placementPhaseDuration : 30f);

            if (gameManager != null) gameManager.SetPhase(GamePhase.Placement);
            if (gameManager != null && gameManager.CostRuntime != null) gameManager.CostRuntime.ResetToStart();
            // defender-placement-cooldown 0 — 배치 페이즈 진입마다 잔여 쿨타임 소거(매치 시작·재시작·리드로우 커버).
            if (gameManager != null && gameManager.CooldownRuntime != null) gameManager.CooldownRuntime.ResetAll();
            if (bridge != null) bridge.BeginPlacement();

            _remaining = duration;
            _active = true;
            _panel.SetActive(true);
            ApplyOverlayMode();
            _startAvailable = false;
            RefreshStartAvailability();
            PlacementReady?.Invoke();
        }

        // 자동 시작 창에는 입력이 없다(계약 5). 배치 경로가 트레이(캔버스 4)/손패(5) 드래그
        // 하나뿐이라 이 캔버스(7)의 전면 raycast 블로커로 닫힌다. 실측: 카운트다운 중 화면
        // 7x7 격자 49점 전부 최상단 히트가 InputBlocker.
        //
        // ⚠ 클릭 배치(현재 은퇴)를 되살릴 거면 이 블로커에 기대지 마라 — PlacementInput 의
        // 가드는 no-arg IsPointerOverGameObject 라 **터치에서 UI 를 못 거른다**(마우스
        // pointerId 만 조회). 그 함정의 수정 선례는 DefenderDragPlacementController.PointerOverUi.
        // 안드로이드가 주 타겟이라 에디터 마우스 검증으로는 잡히지 않는다.
        private void ApplyOverlayMode()
        {
            if (_banner != null) _banner.SetActive(!_autoStart);
            if (_blocker != null) _blocker.SetActive(_autoStart);
            _shownTick = -1;
            if (_bigSeq.isAlive) _bigSeq.Stop();
            if (_bigLabel != null)
            {
                _bigLabel.gameObject.SetActive(_autoStart);
                _bigLabel.text = string.Empty;
            }
            if (_bigLabelRt != null) _bigLabelRt.localScale = Vector3.one;
            // 지난 판의 아웃트로가 알파를 0으로 두고 끝난다 — 재진입마다 되돌린다.
            if (_bigLabelGroup != null) _bigLabelGroup.alpha = 1f;
        }

        private void Update()
        {
            if (!_active) return;
            if (_autoStart)
            {
                TickAutoStart();
                return;
            }
            bool interactionBlocked = IsPlacementInteractionBlocked(out bool aiming);
            RefreshStartAvailability(interactionBlocked);
            if (interactionBlocked)
            {
                _countdownLabel.text = aiming ? "공격 방향을 정해주세요" : "배치 중";
                return;
            }
            _remaining -= Time.deltaTime;
            if (_remaining <= 0f) { _remaining = 0f; FinishPlacement(); return; }
            _countdownLabel.text = $"배치 단계  ·  {Mathf.CeilToInt(_remaining)}초";
        }

        // match-intro-phase-toggles unit 1 — 브롤스타즈식 카운트다운. 남은 초의 올림값이
        // 바뀌는 프레임에만 숫자를 찍고 펀치를 한 번 친다(매 프레임 문자열 재대입 금지).
        // 0 은 표시하지 않는다 — 0에 닿는 순간이 곧 GO! 이자 전투 시작이다.
        private void TickAutoStart()
        {
            // ⚠ 종료를 거절할 상태면 시간도 흘리지 않는다. **아래 FinishPlacement 와 같은
            // 술어를 써야 한다** — 여기서 `interactionBlocked=false` 로 못박고 통과시키면,
            // 종료가 거절당한 프레임에 `_remaining` 이 0으로 눌리고 재시도 자물쇠(_shownTick)만
            // 남아 판이 벽돌이 된다(카운트다운 0 · 차단막 올라간 채 · 전투 미시작).
            //
            // 이 게이트가 잡는 것 둘:
            // ① 튜토리얼 홀드 — 계약 6은 **첫 판**(ShouldRunCore)만 자동 시작에서 빼는데,
            //    게이트를 쓰는 경로가 하나 더 있다. 효과 타일 안내는 **두 번째 판 이후**라
            //    ShouldRunCore=false 에서 돈다. 멈추지 않으면 안내가 3초에 잘린 채 완료로
            //    저장돼(CompleteEffectTileProgress) 플레이어가 영영 못 읽는다.
            //    (안내 캔버스 1500 이 차단막 7 위라 탭 진행은 살아 있다.)
            // ② 드래그/조준 진행 중 — 자동 시작 창에선 차단막이 새 드래그를 막지만, 창에
            //    **들어올 때** 이미 물고 있던 세션은 막지 못한다(재시작 경로가 되살아나면
            //    도달한다 — BattleBridge.OnRestartRequested 는 현재 미구독). 30초 경로가
            //    같은 상태에서 카운트다운을 멈추는 것과 동일하게 처리한다.
            if (!CanFinishPlacement()) return;

            _remaining -= Time.deltaTime;
            if (_remaining <= 0f)
            {
                _remaining = 0f;
                // GO! 는 한 번만. 위 게이트가 FinishPlacement 와 같은 술어라 여기 도달하면
                // 종료는 성공이 보장되고, 이 자물쇠는 벨트앤서스펜더로 남는다.
                // (게이트를 다시 느슨하게 바꾸면 이 자물쇠가 자가치유를 막는 쪽으로 돌변한다 —
                //  거절당한 뒤 영원히 재시도하지 않게 되므로 둘은 반드시 같이 움직인다.)
                if (_shownTick == 0) return;
                _shownTick = 0;
                ShowBigLabel("GO!", countdownFinalColor);
                FinishPlacement();
                return;
            }
            int tick = Mathf.CeilToInt(_remaining);
            if (tick == _shownTick) return;
            _shownTick = tick;
            ShowBigLabel(tick.ToString(), tick <= 1 ? countdownFinalColor : countdownColor);
        }

        private void ShowBigLabel(string text, Color color)
        {
            if (_bigLabel == null || _bigLabelRt == null) return;
            if (_bigSeq.isAlive) _bigSeq.Stop();
            _bigLabel.text = text;
            _bigLabel.color = color;
            _bigLabelRt.localScale = Vector3.one * countdownPunchScale;
            // useUnscaledTime — START 주스와 같은 규율. 인트로는 도메인 시간 제어 밖이다.
            _bigSeq = Sequence.Create(useUnscaledTime: true)
                .Group(Tween.Scale(_bigLabelRt, Vector3.one, countdownPunchDuration, Ease.OutBack));
        }

        private void OnStartClicked()
        {
            if (!CanFinishPlacement())
            {
                RefreshStartAvailability();
                return;
            }
            FinishPlacement();
        }

        private void FinishPlacement()
        {
            // All callers converge here so a same-frame Skip/Start click or countdown
            // expiry cannot bypass the runtime interaction guard.
            if (!CanFinishPlacement()) return;
            _active = false;
            _startAvailable = false;
            SetStartJuice(false);
            HideOverlay();
            if (gameManager != null) gameManager.SetPhase(GamePhase.Battle);
            if (gameManager != null && gameManager.CostRuntime != null) gameManager.CostRuntime.BeginRegen();
            if (bridge != null) bridge.StartBattle();
        }

        // 패널을 닫는 유일한 지점. 자동 시작 모드에서는 GO! 아웃트로가 끝난 뒤 닫히지만,
        // **입력 차단막은 즉시 내린다** — 전투는 이미 시작됐고 남은 것은 잔상뿐이다.
        private void HideOverlay()
        {
            if (_blocker != null) _blocker.SetActive(false);
            if (!_autoStart || _bigLabelRt == null || _bigLabelGroup == null)
            {
                _panel.SetActive(false);
                return;
            }

            var panel = _panel;
            if (_bigSeq.isAlive) _bigSeq.Stop();
            _bigSeq = Sequence.Create(useUnscaledTime: true)
                .Group(Tween.Scale(_bigLabelRt, Vector3.one * (countdownPunchScale * 1.25f),
                    countdownOutroDuration, Ease.OutQuad))
                .Group(Tween.Alpha(_bigLabelGroup, 0f, countdownOutroDuration, Ease.InQuad))
                .ChainCallback(() => { if (panel != null) panel.SetActive(false); });
        }

        private bool CanFinishPlacement()
        {
            if (!_active) return false;
            bool interactionBlocked = IsPlacementInteractionBlocked(out _);
            return PlacementPhasePolicy.CanFinish(interactionBlocked);
        }

        private bool IsPlacementInteractionBlocked(out bool aiming)
        {
            var drag = defenderSelector != null ? defenderSelector.DragController : null;
            aiming = drag != null && drag.IsAiming;
            return aiming || (drag != null && drag.IsDragging);
        }

        private void RefreshStartAvailability(bool? interactionBlocked = null,
            bool animateWhenAvailable = true)
        {
            if (!_active) return;
            // 자동 시작 창에는 START 가 없다 — 기다릴 대상이 카운트다운뿐이다.
            if (_autoStart)
            {
                _startAvailable = false;
                if (_startButtonWrap != null) _startButtonWrap.SetActive(false);
                if (_startButton != null) _startButton.interactable = false;
                SetStartJuice(false);
                return;
            }
            bool blocked = interactionBlocked ?? IsPlacementInteractionBlocked(out _);
            bool available = PlacementPhasePolicy.CanFinish(blocked);
            if (_startAvailable == available &&
                (_startButtonWrap == null || _startButtonWrap.activeSelf == available) &&
                (_startButton == null || _startButton.interactable == available)) return;

            _startAvailable = available;
            if (_startButtonWrap != null) _startButtonWrap.SetActive(available);
            if (_startButton != null) _startButton.interactable = available;
            SetStartJuice(available && animateWhenAvailable);
        }

        // Idle "look here" juice: breathing button scale + a larger, faster gold aura
        // pulse. useUnscaledTime so it keeps living through any timeScale changes.
        private void SetStartJuice(bool on)
        {
            if (_startPulse.isAlive) _startPulse.Stop();
            if (_startAuraPulse.isAlive) _startAuraPulse.Stop();
            if (_startButtonRt != null) _startButtonRt.localScale = Vector3.one;
            if (_startAuraRt != null) _startAuraRt.localScale = Vector3.one;
            if (!on) return;

            if (_startButtonRt != null)
                _startPulse = Tween.Scale(_startButtonRt, Vector3.one, Vector3.one * startPulseScale,
                    startPulsePeriod, Ease.InOutSine, cycles: -1, cycleMode: CycleMode.Yoyo, useUnscaledTime: true);

            if (_startAuraRt != null)
            {
                // Aura breathes with roughly double the button's amplitude for a soft halo.
                float auraScale = 1f + (startPulseScale - 1f) * 2f;
                _startAuraPulse = Tween.Scale(_startAuraRt, Vector3.one, Vector3.one * auraScale,
                    startPulsePeriod, Ease.InOutSine, cycles: -1, cycleMode: CycleMode.Yoyo, useUnscaledTime: true);
            }
        }

        private void BuildCanvas()
        {
            if (_built) return;
            _built = true;

            var roots = UiCanvasSetup.Ensure(gameObject, sortingOrder: 7);

            _panel = new GameObject("PlacementPanel", typeof(RectTransform));
            _panel.transform.SetParent(roots.SafeAreaRoot, false);
            var prt = (RectTransform)_panel.transform;
            prt.anchorMin = Vector2.zero;
            prt.anchorMax = Vector2.one;
            prt.offsetMin = Vector2.zero;
            prt.offsetMax = Vector2.zero;

            // Top-center countdown banner
            var banner = new GameObject("Banner", typeof(RectTransform), typeof(Image));
            _banner = banner;
            banner.transform.SetParent(_panel.transform, false);
            var brt = (RectTransform)banner.transform;
            brt.anchorMin = new Vector2(0.5f, 1f);
            brt.anchorMax = new Vector2(0.5f, 1f);
            brt.pivot = new Vector2(0.5f, 1f);
            brt.anchoredPosition = new Vector2(0f, -90f);
            brt.sizeDelta = new Vector2(560f, 72f);
            banner.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.7f);

            var labelGO = new GameObject("Label", typeof(RectTransform));
            labelGO.transform.SetParent(banner.transform, false);
            var lrt = (RectTransform)labelGO.transform;
            lrt.anchorMin = Vector2.zero; lrt.anchorMax = Vector2.one;
            lrt.offsetMin = Vector2.zero; lrt.offsetMax = Vector2.zero;
            _countdownLabel = labelGO.AddComponent<TextMeshProUGUI>();
            _countdownLabel.text = "배치 단계";
            _countdownLabel.fontSize = 36;
            _countdownLabel.color = Color.yellow;
            _countdownLabel.alignment = TextAlignmentOptions.Center;

            // Bottom-right START BATTLE button. Awakening stays hidden during Placement,
            // so this remains the phase's only primary corner action. A pulsing gold aura
            // + idle breathing pull the eye to the
            // corner; background is the casual graphic if assigned, else a procedural
            // amber+gold plate fallback.
            const float btnW = 280f, btnH = 104f;

            // Corner wrapper: places the whole widget at the dock corner. Aura + button
            // are centered siblings under it so each animates its own scale independently.
            _startButtonWrap = new GameObject("StartButtonWrap", typeof(RectTransform));
            _startButtonWrap.transform.SetParent(_panel.transform, false);
            var wrapRt = (RectTransform)_startButtonWrap.transform;
            wrapRt.anchorMin = new Vector2(1f, 0f);
            wrapRt.anchorMax = new Vector2(1f, 0f);
            wrapRt.pivot = new Vector2(1f, 0f);
            wrapRt.anchoredPosition = new Vector2(-40f, 40f);
            wrapRt.sizeDelta = new Vector2(btnW, btnH);

            // Pulsing gold aura behind the button (centered, larger; scale/alpha animate).
            var auraGO = new GameObject("Aura", typeof(RectTransform), typeof(Image));
            auraGO.transform.SetParent(_startButtonWrap.transform, false);
            _startAuraRt = (RectTransform)auraGO.transform;
            _startAuraRt.anchorMin = new Vector2(0.5f, 0.5f);
            _startAuraRt.anchorMax = new Vector2(0.5f, 0.5f);
            _startAuraRt.pivot = new Vector2(0.5f, 0.5f);
            _startAuraRt.sizeDelta = new Vector2(btnW + 44f, btnH + 44f);
            var auraImg = auraGO.GetComponent<Image>();
            auraImg.sprite = UiRoundedSprite.Make(startCornerRadius + 12f, 0f, startAuraColor, startAuraColor);
            auraImg.type = Image.Type.Sliced;
            auraImg.raycastTarget = false;

            // Button (centered). Color lives in the sprite (white Image tint) so the
            // Button's default ColorTint still multiplies white for hover/press feedback.
            var btnGO = new GameObject("StartButton", typeof(RectTransform), typeof(Image), typeof(Button));
            btnGO.transform.SetParent(_startButtonWrap.transform, false);
            _startButtonRt = (RectTransform)btnGO.transform;
            _startButtonRt.anchorMin = new Vector2(0.5f, 0.5f);
            _startButtonRt.anchorMax = new Vector2(0.5f, 0.5f);
            _startButtonRt.pivot = new Vector2(0.5f, 0.5f);
            _startButtonRt.sizeDelta = new Vector2(btnW, btnH);
            var btnImg = btnGO.GetComponent<Image>();
            if (startButtonBackground != null)
            {
                // Ornate fixed-aspect graphic — stretch as-is (9-slice would warp the
                // decorated corners). Button rect matches its ~2.6:1 aspect.
                btnImg.sprite = startButtonBackground;
                btnImg.type = Image.Type.Simple;
            }
            else
            {
                btnImg.sprite = UiRoundedSprite.Make(startCornerRadius, startBorderWidth, startFillColor, startBorderColor);
                btnImg.type = Image.Type.Sliced;
            }
            _startButton = btnGO.GetComponent<Button>();
            _startButton.onClick.AddListener(OnStartClicked);

            var btnLabelGO = new GameObject("Label", typeof(RectTransform));
            btnLabelGO.transform.SetParent(btnGO.transform, false);
            var blrt = (RectTransform)btnLabelGO.transform;
            blrt.anchorMin = Vector2.zero; blrt.anchorMax = Vector2.one;
            blrt.offsetMin = Vector2.zero; blrt.offsetMax = Vector2.zero;
            var bl = btnLabelGO.AddComponent<TextMeshProUGUI>();
            if (startLabelFont != null) bl.font = startLabelFont;
            bl.text = "전투 시작";
            bl.color = startLabelColor;
            bl.alignment = TextAlignmentOptions.Center;
            bl.textWrappingMode = TextWrappingModes.NoWrap;
            bl.enableAutoSizing = true;
            bl.fontSizeMin = 22f;
            bl.fontSizeMax = 46f;
            bl.characterSpacing = 2f;
            bl.raycastTarget = false;
            // Sticker outline for that casual-game punch (instanced material, shared
            // atlas untouched). Skipped if the font has no SDF material.
            if (startLabelOutlineWidth > 0f && bl.fontMaterial != null)
            {
                var mat = bl.fontMaterial; // instance
                mat.EnableKeyword(ShaderUtilities.Keyword_Outline);
                mat.SetColor(ShaderUtilities.ID_OutlineColor, startLabelOutlineColor);
                mat.SetFloat(ShaderUtilities.ID_OutlineWidth, startLabelOutlineWidth);
            }

            BuildAutoStartOverlay();

            UiLayer.Apply(gameObject);
        }

        // match-intro-phase-toggles unit 0/1 — 자동 시작 전용 오버레이. 두 조각 다 기본 비활성이라
        // 배치 페이즈가 켜져 있는 판에서는 존재하지 않는 것과 같다.
        // 형제 순서 = 차단막 → 숫자. 숫자는 차단막 위에 그려지고 raycast 는 차단막이 전부 먹는다.
        private void BuildAutoStartOverlay()
        {
            var blockerGO = new GameObject("InputBlocker", typeof(RectTransform), typeof(Image));
            _blocker = blockerGO;
            blockerGO.transform.SetParent(_panel.transform, false);
            var blrt2 = (RectTransform)blockerGO.transform;
            blrt2.anchorMin = Vector2.zero;
            blrt2.anchorMax = Vector2.one;
            blrt2.offsetMin = Vector2.zero;
            blrt2.offsetMax = Vector2.zero;
            var blockerImg = blockerGO.GetComponent<Image>();
            blockerImg.color = new Color(0f, 0f, 0f, 0f); // 보이지 않지만 포인터는 전부 먹는다
            blockerImg.raycastTarget = true;
            blockerGO.SetActive(false);

            var bigGO = new GameObject("CountdownNumber", typeof(RectTransform), typeof(CanvasGroup));
            bigGO.transform.SetParent(_panel.transform, false);
            _bigLabelRt = (RectTransform)bigGO.transform;
            _bigLabelRt.anchorMin = new Vector2(0.5f, 0.5f);
            _bigLabelRt.anchorMax = new Vector2(0.5f, 0.5f);
            _bigLabelRt.pivot = new Vector2(0.5f, 0.5f);
            _bigLabelRt.anchoredPosition = Vector2.zero;
            _bigLabelRt.sizeDelta = new Vector2(700f, 360f);
            _bigLabelGroup = bigGO.GetComponent<CanvasGroup>();
            _bigLabelGroup.blocksRaycasts = false;
            _bigLabelGroup.interactable = false;

            _bigLabel = bigGO.AddComponent<TextMeshProUGUI>();
            var font = countdownFont != null ? countdownFont : startLabelFont;
            if (font != null) _bigLabel.font = font;
            _bigLabel.text = string.Empty;
            _bigLabel.fontSize = countdownFontSize;
            _bigLabel.color = countdownColor;
            _bigLabel.alignment = TextAlignmentOptions.Center;
            _bigLabel.textWrappingMode = TextWrappingModes.NoWrap;
            _bigLabel.raycastTarget = false;
            // START 라벨과 같은 스티커 외곽선(인스턴스 머티리얼, 공용 아틀라스 무변경).
            if (startLabelOutlineWidth > 0f && _bigLabel.fontMaterial != null)
            {
                var mat = _bigLabel.fontMaterial;
                mat.EnableKeyword(ShaderUtilities.Keyword_Outline);
                mat.SetColor(ShaderUtilities.ID_OutlineColor, startLabelOutlineColor);
                mat.SetFloat(ShaderUtilities.ID_OutlineWidth, startLabelOutlineWidth);
            }
            bigGO.SetActive(false);
        }
    }

    internal static class PlacementPhasePolicy
    {
        public static bool CanFinish(bool placementInteractionBlocked) =>
            !placementInteractionBlocked;

        // match-intro-phase-toggles unit 0 — 배치 창을 열지 3초 뒤 자동으로 닫을지.
        // tutorial-content-teardown unit 0 — 첫 판 튜토리얼 예외는 걷혔다. 튜토리얼 콘텐츠가
        // 사라져 «첫 판만 30초» 가 유령 규칙이 되기 때문이다(그 spec 계약 3).
        // 이제 플래그가 곧 진실이다.
        public static bool UseAutoStart(bool placementPhaseEnabled) =>
            !placementPhaseEnabled;
    }
}
