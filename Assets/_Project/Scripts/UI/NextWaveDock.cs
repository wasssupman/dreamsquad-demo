using PrimeTween;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Wassup.Bridge;
using Wassup.Core;
using Wassup.UI.Layout;

namespace Wassup.UI
{
    // 좌하단 「다음 웨이브」 알약 버튼.
    //
    // ─────────────────────────────────────────────────────────────────────────
    // rev 9 — 보너스 당기기 알약이 **위에** 얹힌다 (bonus-wave-pull unit 7, 2026-08-24).
    //   가로 예산이 0 이라(아래 rev 8 설명) 세로로 쌓는 것이 유일한 자리다. 폭·x 는 일반
    //   알약과 똑같이 두고 **색만** 갈라, 「같은 계열의 다른 버튼」으로 읽히게 한다.
    //   상태원은 `BattleBridge.BonusPullAvailable` 하나다 — 도크는 임계를 계산하지 않는다.
    //
    // rev 8 — 원뎁스 + HUD 색 통일 (사용자 결정 2026-08-21)
    //
    // rev 7 은 **투뎁스**였다: 1탭 = 다음 적 예고 말풍선, 2탭 = 실제 당김.
    // 그 첫 뎁스가 존재한 이유는 「확인시키려고」가 아니라 **자리가 없어서**였다 —
    // 배치 트레이가 `SafeAreaWidth − cornerReservedWidth(640)` 로 클램프돼 항상
    // x 320~1600 을 먹으므로 도크 예산은 **좌측 0~320** 뿐이고, 그 폭에 예고를 상시로
    // 두면서 읽히게 만들 방법이 없었다. 그래서 정보를 탭 뒤로 숨겼고, 두 번째 탭은
    // 그 결정의 파생일 뿐이었다.
    //
    // rev 8 은 그 전제를 지운다. **예고를 없애고 탭 1회 = 즉시 투입.**
    //   · 말풍선·`Armed` 연출 단계·`BattleBridge.TryGetNextWaveComposition` 이 함께 은퇴한다
    //     (Armed 는 「한 번 더 누르면 당겨진다」는 신호였으므로 원뎁스에서 의미가 없다).
    //   · **잠금 사유는 라벨이 인수한다.** 말풍선이 갖고 있던 「정리하면 다시」가 알약 얼굴로
    //     올라온다 — spec 계약 3(「잠금 상태와 언제 풀리는지를 같은 자리에」)은 유지된다.
    //   · 예고 제거는 `wave-pull-revival` 계약 4 · PRD §6.3 을 **미채택으로 뒤집는** 결정이다.
    //     근거는 `docs/spec/wave-pull-revival/7_one_depth_pull.md`.
    //
    // 그리고 **색을 상단 HUD 와 한 벌로** 맞춘다. 점수·시간·웨이브 배지는 전부
    // «어두운 판 + 금테 + 금색 캡션 탭»(`ScoreHudView`) 인데 도크만 파란 알약이라 따로 놀았다.
    // 도크는 그 언어에서 **탭의 금색 채움**을 가져간다 — HUD 판은 전부 어두우므로
    // 화면에서 「금색 덩어리 = 누르는 것」이 색만으로 갈린다.
    //
    // rev 7 에서 그대로 가져가는 두 규칙:
    //   · **알약은 절대 리사이즈하지 않는다.** 고정 크기·고정 위치.
    //   · **x ≤ 300 을 넘지 않는다.** 넘으면 배치 트레이와 겹친다(Awake 가 경고한다).
    //
    // 남은시간·웨이브 진행은 **화면 최상단 바**에 있다(`ScoreHudView.SetTopBar`).
    // ─────────────────────────────────────────────────────────────────────────
    public class NextWaveDock : MonoBehaviour
    {
        [SerializeField] private BattleBridge bridge;

        // 구 `panelSize`·`timerPlate*`·`buttonSize`·`callout*`·`armed*` 등은 은퇴했다.
        // 씬 asset 에 orphan 키로 남지만 무해하다(`fixedWaveIntervalSec` 선례).
        //
        // ⚠ **`panelOffset.x + pillSize.x ≤ 300` 을 지켜라.** 넘으면 배치 트레이와 겹친다.
        // 알약을 키우고 싶으면 폭이 아니라 높이를 키운다. Awake 가 경고한다.
        [Header("Pill (고정 — 리사이즈 금지)")]
        [SerializeField] private Vector2 panelOffset = new Vector2(40f, 40f);
        [SerializeField] private Vector2 pillSize = new Vector2(256f, 96f);
        [SerializeField] private float pillCornerRadius = 28f;
        // 색 정본은 `ScoreHudView` 다 — 아래 4개는 그 값을 그대로 옮긴 것이다.
        // 바꿀 일이 생기면 **양쪽을 같이** 바꾼다(한쪽만 바꾸면 다시 따로 논다).
        [Tooltip("HUD 캡션 탭과 같은 금색 채움 (ScoreHudView.tabColor)")]
        [SerializeField] private Color pillFill = new Color(1f, 0.78f, 0.28f, 1f);
        [Tooltip("HUD 웨이브 값과 같은 밝은 금 (ScoreHudView.waveValueColor)")]
        [SerializeField] private Color pillBorder = new Color(1f, 0.9f, 0.66f, 1f);
        [SerializeField] private Color pillLockedFill = new Color(0.24f, 0.28f, 0.34f, 0.95f);
        [SerializeField] private Color pillLockedBorder = new Color(0.42f, 0.47f, 0.55f, 0.9f);
        [Tooltip("알약 아래 립(입체감). 누르면 이만큼 내려앉는다.")]
        [SerializeField] private float pillLip = 6f;
        [SerializeField] private Color pillLipColor = new Color(0.42f, 0.28f, 0.06f, 1f);
        [SerializeField] private Color pillLipLockedColor = new Color(0.13f, 0.16f, 0.20f, 0.95f);
        [SerializeField] private float pillFontSize = 32f;
        [Tooltip("금색 채움 위의 어두운 글자 (ScoreHudView.tabTextColor)")]
        [SerializeField] private Color pillTextColor = new Color(0.1f, 0.08f, 0.04f, 1f);
        [SerializeField] private Color pillLockedTextColor = new Color(0.80f, 0.84f, 0.90f, 0.9f);

        // 연출은 한 단계뿐이다(rev 7 의 Ready). 당길 수 있을 때만 살아 움직이고 잠기면 멈춘다 —
        // 「지금은 아니다」가 문장이 아니라 정지로 읽힌다.
        [Header("연출 — 말 대신 이것이 «누를 수 있음»을 알린다")]
        [Tooltip("화살표가 오르내리는 폭(px)")]
        [SerializeField] private float arrowBob = 6f;
        [SerializeField] private float arrowBobSeconds = 0.7f;
        [Tooltip("광택이 훑는 시간 / 그 뒤 쉬는 시간(초)")]
        [SerializeField] private float shineSweepSeconds = 1.1f;
        [SerializeField] private float shineRestSeconds = 1.4f;
        [SerializeField] private float shineWidth = 44f;
        [Tooltip("HUD 점수판 광택과 같은 값 (ScoreHudView.shineColor)")]
        [SerializeField] private Color shineColor = new Color(1f, 0.96f, 0.82f, 0.22f);
        [SerializeField] private float tapPunch = 0.10f;

        // ── 보너스 당기기 알약 (bonus-wave-pull unit 7) ───────────────────────────
        // **일반 알약 위에** 세로로 쌓는다. 가로는 예산이 없다 — 배치 트레이가 x 320~1600 을
        // 항상 먹어 도크는 x ≤ 300 이고 일반 알약이 40+256=296 으로 이미 꽉 찼다.
        // 폭·x 를 일반 알약과 **똑같이** 두는 것이 「같은 계열의 버튼」이라는 신호다.
        //
        // 색만 금색과 갈린다 — 금색 = 일반 당김이라는 학습을 깨지 않기 위해서다.
        // ⚠ 이 필드들은 **신규**라 씬 YAML 에 없다 → C# 기본값이 그대로 적용된다.
        // (「도크 색은 씬에 박혀 있다」 함정은 기존 필드를 재사용할 때만 해당한다.)
        [Header("Bonus pill (보너스 당기기)")]
        [Tooltip("일반 알약과의 세로 간격.")]
        [SerializeField] private float bonusPillGap = 14f;
        [SerializeField] private Color bonusPillFill = new Color(0.62f, 0.32f, 0.86f, 1f);
        [SerializeField] private Color bonusPillBorder = new Color(0.86f, 0.70f, 1f, 1f);
        [SerializeField] private Color bonusPillLipColor = new Color(0.22f, 0.08f, 0.34f, 1f);
        [SerializeField] private Color bonusPillTextColor = new Color(1f, 0.97f, 1f, 1f);
        [Tooltip("등장/퇴장 시간(초).")]
        [SerializeField] private float bonusFadeSeconds = 0.22f;
        [Tooltip("등장 시 아래에서 올라오는 거리.")]
        [SerializeField] private float bonusRiseDistance = 18f;

        private GameObject _panel;
        private GameObject _pillRoot;          // 립(그림자판) — 포커스 링이 가리키는 대상
        private RectTransform _pillFaceRect;
        private Image _pillImage;
        private Image _pillLipImage;
        private Button _pillButton;
        private TextMeshProUGUI _pillLabel;
        private RectTransform _arrowRect;
        private TextMeshProUGUI _arrowLabel;
        private RectTransform _shineRect;
        private Image _shineImage;

        private Sprite _pillSpriteReady, _pillSpriteLocked;
        private Sprite _pillLipSpriteReady, _pillLipSpriteLocked;

        private Tween _tapTween, _arrowTween;
        // 스윕+대기를 Chain 으로 잇기 때문에 Sequence 다(Tween 이 아니다).
        private Sequence _shineSeq;
        private bool _built, _subscribed, _pressed, _idleRunning;

        // 매 프레임 문자열 조립을 막는 직전값 캐시.
        private int _lastStateKey = int.MinValue;

        // 보너스 알약 — 상태원은 `BattleBridge.BonusPullAvailable` 하나다. 도크가 임계를
        // 다시 계산하면 두 곳이 갈린다(브리지가 카운터의 유일 소유자).
        private GameObject _bonusRoot;
        private RectTransform _bonusRect;
        private CanvasGroup _bonusGroup;
        private Button _bonusButton;
        private TextMeshProUGUI _bonusLabel;
        private Tween _bonusTween;      // 위치
        private Tween _bonusFadeTween;  // 알파 — 별도로 잡아둔다(아래 SetBonusShown 주석)
        private bool _bonusShown;
        private float _bonusRestY;

        // wave-pull-revival unit 4 — 튜토리얼 포커스 링이 감쌀 대상.
        // 선례: AwakeningGaugeView.HitRect.
        public RectTransform PullButtonRect =>
            _pillRoot != null ? (RectTransform)_pillRoot.transform : null;

        private void Awake()
        {
            BuildCanvas();
            if (_panel != null) _panel.SetActive(false);
        }

        private void OnDisable()
        {
            if (_subscribed && GameManager.Instance != null)
                GameManager.Instance.PhaseChanged -= OnPhaseChanged;
            _subscribed = false;
            // 트윈을 남기면 비활성화 중 스케일·위치가 굳고, 씬 언로드 중이면 PrimeTween 이
            // «OnComplete 무시» 에러를 남긴다.
            StopIdleAnimation();
            if (_tapTween.isAlive) _tapTween.Stop();
            // bonus-wave-pull unit 7 — 같은 이유로 보너스 알약 트윈도 끊는다. 상태 플래그도
            // 되돌려야 다시 켜졌을 때 SetBonusShown 이 전이를 인식한다(전이 기반이라서).
            if (_bonusTween.isAlive) _bonusTween.Stop();
            if (_bonusFadeTween.isAlive) _bonusFadeTween.Stop();
            _bonusShown = false;
            if (_bonusRoot != null) _bonusRoot.SetActive(false);
            if (_pillRoot != null) _pillRoot.transform.localScale = Vector3.one;
        }

        // GameManager.Instance may not be set when OnEnable runs (scene load order), so
        // subscribe lazily in Update — mirrors ScoreHudView.
        private void EnsureSubscribed()
        {
            if (_subscribed) return;
            if (GameManager.Instance == null) return;
            GameManager.Instance.PhaseChanged += OnPhaseChanged;
            _subscribed = true;
            OnPhaseChanged(GameManager.Instance.CurrentPhase);
        }

        // 도크는 Battle 페이즈 표시물이다. 게임오버에도 페이즈는 Battle 이지만 결과
        // 오버레이가 도크를 덮는다.
        private void OnPhaseChanged(GamePhase phase)
        {
            if (_panel == null) return;
            bool battle = phase == GamePhase.Battle;
            if (_panel.activeSelf != battle) _panel.SetActive(battle);
            if (!battle) { StopIdleAnimation(); return; }
            // 판이 바뀌었는데 직전값이 같으면 조립을 건너뛰어 이전 판 문구가 남는다.
            _lastStateKey = int.MinValue;
        }

        private void Update()
        {
            EnsureSubscribed();
            if (bridge == null || _panel == null || !_panel.activeSelf) return;

            bool available = bridge.NextWaveAvailable;
            if (_pillRoot != null && _pillRoot.activeSelf != available)
                _pillRoot.SetActive(available);
            // 보너스 알약은 일반 알약의 가용성과 **독립**이다 — 브리지가 자기 술어를 갖는다.
            SetBonusShown(bridge.BonusPullAvailable);

            if (!available) { StopIdleAnimation(); return; }

            RefreshState();
        }

        // 알약이 말하는 것은 **한 문장**이다. 나머지는 연출이 말한다.
        //
        // 문구 3종. 「탭하면 ~」류 안내는 없다 — 그건 연출의 몫이다.
        //   다음 웨이브   — 지금 누르면 온다
        //   정리하면 다시 — 겹침 상한에 닿았다(계약 3: 사유가 아니라 **무엇을 하면 풀리는지**)
        //   마지막 웨이브 — 더 없다
        private void RefreshState()
        {
            bool hasNext = bridge.NextWaveHasNext;
            bool allowed = bridge.PullAllowed;
            bool live = hasNext && allowed;

            int key = (hasNext ? 1 : 0) | (allowed ? 2 : 0);
            if (key == _lastStateKey) return;
            _lastStateKey = key;

            // 잠겼을 때 눌리지 않는다. 라벨이 이유를 말하고 연출이 멈춰 있어
            // 「지금은 아니다」가 한 방향으로 읽힌다.
            _pillButton.interactable = live;
            _pillImage.sprite = live ? _pillSpriteReady : _pillSpriteLocked;
            _pillLipImage.sprite = live ? _pillLipSpriteReady : _pillLipSpriteLocked;
            _pillLabel.color = live ? pillTextColor : pillLockedTextColor;
            _arrowLabel.color = _pillLabel.color;
            SetPressed(false);

            _pillLabel.text = !hasNext ? "마지막 웨이브"
                            : allowed ? "다음 웨이브"
                            : "정리하면 다시";
            _arrowLabel.enabled = live;

            SetIdleRunning(live);
        }

        // ── 탭: 1회 = 당김 ──────────────────────────────────────────────────────
        //
        // **플레이어 경로는 여기 하나뿐이다.** `BattleBridge.ForceNextWave`(기제)를 직접
        // 부르지 않는다 — 부르면 겹침 상한이 우회된다.
        private void OnPillClicked()
        {
            if (bridge == null || !bridge.NextWaveAvailable) return;
            if (!bridge.TryPullNextWave()) return;
            Punch();
            // 상한 소진으로 방금 잠겼을 수 있다 — 다음 Update 가 라벨·연출을 갱신하게 한다.
            _lastStateKey = int.MinValue;
        }

        // ── 연출 ────────────────────────────────────────────────────────────────
        //
        // 정지 — 잠김 / 마지막 웨이브. 아무것도 안 움직인다.
        // 구동 — 누를 수 있다. 화살표가 천천히 오르내리고 광택이 가끔 훑는다.
        private void SetIdleRunning(bool on)
        {
            if (_idleRunning == on) return;
            _idleRunning = on;

            if (_arrowTween.isAlive) _arrowTween.Stop();
            if (_shineSeq.isAlive) _shineSeq.Stop();

            if (!on)
            {
                if (_arrowRect != null)
                    _arrowRect.anchoredPosition = new Vector2(_arrowRect.anchoredPosition.x, 0f);
                if (_shineImage != null) _shineImage.enabled = false;
                return;
            }

            _arrowRect.anchoredPosition = new Vector2(_arrowRect.anchoredPosition.x, 0f);
            _arrowTween = Tween.UIAnchoredPositionY(_arrowRect, arrowBob, arrowBobSeconds,
                Ease.InOutSine, cycles: -1, CycleMode.Yoyo, useUnscaledTime: true);

            _shineImage.enabled = true;
            _shineImage.color = shineColor;
            StartShineSweep();
        }

        // 스윕 1회 + 대기를 한 사이클로 돌린다. `cycles: -1` 로는 대기 구간을 표현할 수
        // 없어서(딜레이가 트윈이 아니다) 완료 콜백으로 다시 건다.
        private void StartShineSweep()
        {
            if (!_idleRunning || _shineRect == null || !isActiveAndEnabled) return;
            float from = -shineWidth;
            float to = pillSize.x + shineWidth;
            _shineRect.anchoredPosition = new Vector2(from, 0f);
            // ⚠ `useUnscaledTime` 은 **Sequence 가 소유한다** — 자식 트윈에 붙이면 PrimeTween 이
            // 「무시됐다」고 에러를 뱉는다(Sequence.cs:345). 시퀀스에 한 번만 준다.
            _shineSeq = Sequence.Create(useUnscaledTime: true)
                .Chain(Tween.UIAnchoredPositionX(_shineRect, from, to,
                    shineSweepSeconds, Ease.InOutQuad))
                .ChainDelay(shineRestSeconds)
                .OnComplete(this, dock => dock.StartShineSweep());
        }

        private void StopIdleAnimation() => SetIdleRunning(false);

        private void SetPressed(bool pressed)
        {
            if (_pillFaceRect == null) return;
            if (pressed && (_pillButton == null || !_pillButton.interactable)) return;
            if (_pressed == pressed) return;
            _pressed = pressed;
            _pillFaceRect.anchoredPosition = new Vector2(0f, pressed ? 0f : pillLip);
        }

        private void Punch()
        {
            if (_pillRoot == null) return;
            if (_tapTween.isAlive) _tapTween.Stop();
            var rt = (RectTransform)_pillRoot.transform;
            rt.localScale = Vector3.one;
            _tapTween = Tween.PunchScale(rt, Vector3.one * tapPunch, 0.20f, useUnscaledTime: true);
        }

        // ── 캔버스 ──────────────────────────────────────────────────────────────
        private void BuildCanvas()
        {
            if (_built) return;
            _built = true;

            // 이 경고가 뜨면 트레이와 겹친 것이다 — rev 1~6 이 반복한 실수를 여기서 잡는다.
            float right = panelOffset.x + pillSize.x;
            if (right > 300f)
                Debug.LogWarning(
                    $"[NextWaveDock] 도크 우측 끝이 {right:0} 라 예약구역(300)을 넘는다 — " +
                    "배치 트레이와 겹친다. 폭이 아니라 높이를 키워라.", this);

            var roots = UiCanvasSetup.Ensure(gameObject, sortingOrder: 7);

            _panel = new GameObject("DockPanel", typeof(RectTransform));
            _panel.transform.SetParent(roots.SafeAreaRoot, false);
            var prt = (RectTransform)_panel.transform;
            prt.anchorMin = Vector2.zero;
            prt.anchorMax = Vector2.zero;
            prt.pivot = Vector2.zero;
            prt.anchoredPosition = panelOffset;
            // 두 알약을 담는 높이. 폭은 그대로 — 세로만 늘어난다(계약 11).
            prt.sizeDelta = new Vector2(
                pillSize.x, (pillSize.y + pillLip) * 2f + bonusPillGap);

            BuildPill();
            BuildBonusPill();

            _pillRoot.SetActive(false);
            _bonusRoot.SetActive(false);
            UiLayer.Apply(gameObject);
        }

        private void BuildPill()
        {
            _pillSpriteReady = UiRoundedSprite.Make(pillCornerRadius, 3f, pillFill, pillBorder);
            _pillSpriteLocked =
                UiRoundedSprite.Make(pillCornerRadius, 3f, pillLockedFill, pillLockedBorder);
            _pillLipSpriteReady =
                UiRoundedSprite.Make(pillCornerRadius, 0f, pillLipColor, pillLipColor);
            _pillLipSpriteLocked =
                UiRoundedSprite.Make(pillCornerRadius, 0f, pillLipLockedColor, pillLipLockedColor);

            // 립(그림자판) — 면 뒤에 pillLip 만큼 내려 깔린다. 이 한 장이 «떠 있다»를 만든다.
            _pillRoot = new GameObject("Pill", typeof(RectTransform), typeof(Image));
            _pillRoot.transform.SetParent(_panel.transform, false);
            var lipRect = (RectTransform)_pillRoot.transform;
            lipRect.anchorMin = Vector2.zero;
            lipRect.anchorMax = Vector2.zero;
            lipRect.pivot = Vector2.zero;
            lipRect.anchoredPosition = Vector2.zero;
            lipRect.sizeDelta = new Vector2(pillSize.x, pillSize.y + pillLip);
            _pillLipImage = _pillRoot.GetComponent<Image>();
            _pillLipImage.sprite = _pillLipSpriteReady;
            _pillLipImage.type = Image.Type.Sliced;
            _pillLipImage.color = Color.white;
            _pillLipImage.raycastTarget = false;

            var face = new GameObject("Face", typeof(RectTransform), typeof(Image));
            face.transform.SetParent(_pillRoot.transform, false);
            _pillFaceRect = (RectTransform)face.transform;
            _pillFaceRect.anchorMin = Vector2.zero;
            _pillFaceRect.anchorMax = Vector2.zero;
            _pillFaceRect.pivot = Vector2.zero;
            _pillFaceRect.anchoredPosition = new Vector2(0f, pillLip);
            _pillFaceRect.sizeDelta = pillSize;
            _pillImage = face.GetComponent<Image>();
            _pillImage.sprite = _pillSpriteReady;
            _pillImage.type = Image.Type.Sliced;
            _pillImage.color = Color.white;
            _pillImage.raycastTarget = true;

            _pillButton = face.AddComponent<Button>();
            _pillButton.targetGraphic = _pillImage;
            // 색은 상태 스프라이트가 소유한다(ColorTint 가 덧칠하면 소유자가 둘이 된다).
            _pillButton.transition = Selectable.Transition.None;
            _pillButton.onClick.AddListener(OnPillClicked);

            var trigger = face.AddComponent<EventTrigger>();
            AddTrigger(trigger, EventTriggerType.PointerDown, () => SetPressed(true));
            AddTrigger(trigger, EventTriggerType.PointerUp, () => SetPressed(false));
            AddTrigger(trigger, EventTriggerType.PointerExit, () => SetPressed(false));

            // 광택 밴드 — 면 위를 훑는다.
            //
            // ⚠ **반드시 잘려야 한다.** 알파를 낮게 두는 것만으로는 안 된다 — 밴드가 알약
            // 밖으로 나가 허공에 뜬 흰 막대로 보인다(사용자 확인). `RectMask2D` 로 자른다:
            // `Mask` 와 달리 스텐실 머티리얼 인스턴스를 만들지 않아 9-slice 가 안 흐려진다
            // (CostDisplay 가 Mask 를 피한 이유는 머티리얼 인스턴스였지 클리핑이 아니었다).
            var clip = new GameObject("ShineClip", typeof(RectTransform), typeof(RectMask2D));
            clip.transform.SetParent(face.transform, false);
            var clipRect = (RectTransform)clip.transform;
            clipRect.anchorMin = Vector2.zero;
            clipRect.anchorMax = Vector2.one;
            clipRect.offsetMin = Vector2.zero;
            clipRect.offsetMax = Vector2.zero;

            var shine = new GameObject("Shine", typeof(RectTransform), typeof(Image));
            shine.transform.SetParent(clip.transform, false);
            _shineRect = (RectTransform)shine.transform;
            _shineRect.anchorMin = new Vector2(0f, 0f);
            _shineRect.anchorMax = new Vector2(0f, 1f);
            _shineRect.pivot = new Vector2(0f, 0.5f);
            _shineRect.sizeDelta = new Vector2(shineWidth, 0f);
            _shineImage = shine.GetComponent<Image>();
            _shineImage.color = shineColor;
            _shineImage.raycastTarget = false;
            _shineImage.enabled = false;

            _pillLabel = AddLabel(face.transform, "Label", pillFontSize, pillTextColor,
                TextAlignmentOptions.Center, pillFontSize * 0.7f);
            var lr = _pillLabel.rectTransform;
            lr.anchorMin = Vector2.zero;
            lr.anchorMax = Vector2.one;
            lr.offsetMin = new Vector2(22f, 0f);
            lr.offsetMax = new Vector2(-44f, 0f);   // 우측은 화살표 몫
            _pillLabel.text = "다음 웨이브";

            _arrowLabel = AddLabel(face.transform, "Arrow", pillFontSize * 0.9f, pillTextColor,
                TextAlignmentOptions.Center, pillFontSize * 0.6f);
            _arrowRect = _arrowLabel.rectTransform;
            _arrowRect.anchorMin = new Vector2(1f, 0.5f);
            _arrowRect.anchorMax = new Vector2(1f, 0.5f);
            _arrowRect.pivot = new Vector2(1f, 0.5f);
            _arrowRect.anchoredPosition = new Vector2(-14f, 0f);
            _arrowRect.sizeDelta = new Vector2(34f, 40f);
            _arrowLabel.text = "▲";
        }

        // ── 보너스 당기기 ─────────────────────────────────────────────────────────
        //
        // **플레이어 경로는 여기 하나뿐이다.** `BattleBridge.ForceBonusWave`(기제)를 직접
        // 부르지 않는다 — 부르면 트리거와 「동시 1벌」이 함께 우회된다.
        private void OnBonusClicked()
        {
            if (bridge == null) return;
            if (!bridge.TryBonusPull()) return;
            Punch();
            // 눌린 순간 진행 중이 되어 술어가 거짓으로 떨어진다 — 다음 Update 가 퇴장시킨다.
        }

        private void BuildBonusPill()
        {
            var faceSprite = UiRoundedSprite.Make(
                pillCornerRadius, 3f, bonusPillFill, bonusPillBorder);
            var lipSprite = UiRoundedSprite.Make(
                pillCornerRadius, 0f, bonusPillLipColor, bonusPillLipColor);

            _bonusRoot = new GameObject("BonusPill",
                typeof(RectTransform), typeof(Image), typeof(CanvasGroup));
            _bonusRoot.transform.SetParent(_panel.transform, false);
            _bonusRect = (RectTransform)_bonusRoot.transform;
            _bonusRect.anchorMin = Vector2.zero;
            _bonusRect.anchorMax = Vector2.zero;
            _bonusRect.pivot = Vector2.zero;
            _bonusRestY = pillSize.y + pillLip + bonusPillGap;
            _bonusRect.anchoredPosition = new Vector2(0f, _bonusRestY);
            _bonusRect.sizeDelta = new Vector2(pillSize.x, pillSize.y + pillLip);

            var lip = _bonusRoot.GetComponent<Image>();
            lip.sprite = lipSprite;
            lip.type = Image.Type.Sliced;
            lip.color = Color.white;
            lip.raycastTarget = false;

            _bonusGroup = _bonusRoot.GetComponent<CanvasGroup>();
            _bonusGroup.alpha = 0f;

            var face = new GameObject("Face", typeof(RectTransform), typeof(Image));
            face.transform.SetParent(_bonusRoot.transform, false);
            var faceRect = (RectTransform)face.transform;
            faceRect.anchorMin = Vector2.zero;
            faceRect.anchorMax = Vector2.zero;
            faceRect.pivot = Vector2.zero;
            faceRect.anchoredPosition = new Vector2(0f, pillLip);
            faceRect.sizeDelta = pillSize;
            var faceImage = face.GetComponent<Image>();
            faceImage.sprite = faceSprite;
            faceImage.type = Image.Type.Sliced;
            faceImage.color = Color.white;
            faceImage.raycastTarget = true;

            _bonusButton = face.AddComponent<Button>();
            _bonusButton.targetGraphic = faceImage;
            _bonusButton.transition = Selectable.Transition.None;
            _bonusButton.onClick.AddListener(OnBonusClicked);

            _bonusLabel = AddLabel(face.transform, "Label", pillFontSize, bonusPillTextColor,
                TextAlignmentOptions.Center, pillFontSize * 0.7f);
            var lr = _bonusLabel.rectTransform;
            lr.anchorMin = Vector2.zero;
            lr.anchorMax = Vector2.one;
            lr.offsetMin = new Vector2(16f, 0f);
            lr.offsetMax = new Vector2(-16f, 0f);
            _bonusLabel.text = "보너스 웨이브";
        }

        // 등장/퇴장. 상태원은 브리지 술어 하나이고 이 메서드는 그 전이만 연출한다.
        private void SetBonusShown(bool show)
        {
            if (_bonusRoot == null || _bonusShown == show) return;
            _bonusShown = show;

            // ★**두 트윈을 다 끊는다.** 알파 핸들을 버리면 ⑴ OnDisable 이 위치만 멈춰 알파가
            // 씬 언로드까지 살아남고(PrimeTween «OnComplete 무시» 에러), ⑵ 0.22초 안에
            // show→hide 가 겹칠 때 같은 CanvasGroup 에 알파 트윈 둘이 경쟁해 «숨겼는데 alpha 1»
            // 이 된다. 위치만 Stop 되고 알파가 안 되는 비대칭이 그 창을 만든다.
            if (_bonusTween.isAlive) _bonusTween.Stop();
            if (_bonusFadeTween.isAlive) _bonusFadeTween.Stop();
            if (show) _bonusRoot.SetActive(true);

            // 퇴장 중에도 눌리면 「없어지는 버튼이 먹혔다」가 되므로 즉시 막는다.
            _bonusGroup.blocksRaycasts = show;
            _bonusGroup.interactable = show;

            _bonusRect.anchoredPosition = new Vector2(
                0f, show ? _bonusRestY - bonusRiseDistance : _bonusRestY);
            _bonusTween = Tween.UIAnchoredPositionY(
                _bonusRect, show ? _bonusRestY : _bonusRestY - bonusRiseDistance,
                bonusFadeSeconds, show ? Ease.OutBack : Ease.InQuad, useUnscaledTime: true);
            _bonusFadeTween = Tween.Alpha(_bonusGroup, show ? 1f : 0f, bonusFadeSeconds,
                show ? Ease.OutQuad : Ease.InQuad, useUnscaledTime: true)
                .OnComplete(this, dock =>
                {
                    if (!dock._bonusShown && dock._bonusRoot != null)
                        dock._bonusRoot.SetActive(false);
                });
        }

        private static void AddTrigger(
            EventTrigger trigger, EventTriggerType type, System.Action action)
        {
            var entry = new EventTrigger.Entry { eventID = type };
            entry.callback.AddListener(_ => action());
            trigger.triggers.Add(entry);
        }

        private static TextMeshProUGUI AddLabel(
            Transform parent, string name, float fontSize, Color color,
            TextAlignmentOptions alignment, float minFontSize)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var label = go.AddComponent<TextMeshProUGUI>();
            label.fontSize = fontSize;
            label.enableAutoSizing = true;
            label.fontSizeMin = Mathf.Min(minFontSize, fontSize);
            label.fontSizeMax = fontSize;
            label.color = color;
            label.fontStyle = FontStyles.Bold;
            label.alignment = alignment;
            label.overflowMode = TextOverflowModes.Ellipsis;
            // 부모가 버튼이면 자식 라벨이 레이캐스트를 먹어 탭이 통과하지 못한다.
            label.raycastTarget = false;
            if (label.font != null)
            {
                var m = label.fontMaterial;
                // 외곽선도 금색 언어에 맞춘 짙은 갈흑색이다. 구 남색(0.02,0.06,0.16)은
                // 파란 알약의 잔재라 금색 면 위에서 색이 튄다.
                m.SetColor(ShaderUtilities.ID_OutlineColor, new Color(0.10f, 0.08f, 0.03f, 1f));
                m.SetFloat(ShaderUtilities.ID_OutlineWidth, 0.16f);
            }
            return label;
        }
    }
}
