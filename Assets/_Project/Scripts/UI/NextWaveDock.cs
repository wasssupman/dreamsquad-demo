using System.Text;
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
    // 좌하단 「다음 웨이브」 알약 버튼 + 그 위 말풍선.
    //
    // ─────────────────────────────────────────────────────────────────────────
    // next-wave-dock-legibility rev 7 (시안 A) — 사용자 결정 2026-08-13
    //
    // rev 1~6 이 전부 반려된 원인은 두 개였다:
    //
    // ① **예약구역을 넘고 있었다.** 배치 트레이는 폭이
    //    `SafeAreaWidth − cornerReservedWidth(640)` 로 클램프되고 바닥 중앙 정렬이라
    //    (`DefenderSelector.cs:252·353`), 1920 기준 항상 x 320~1600 을 먹는다.
    //    **좌측 0~320 이 도크 예산**인데 구 도크는 우측 끝이 328(40+288), rev3 은 352 라
    //    각각 8px·32px 을 물고 있었다. 크기를 아무리 조정해도 겹침이 안 사라진 이유다.
    //    **슬롯이 10개가 돼도 트레이는 안 넓어진다** — 클램프가 같고 슬롯만 좁아지므로
    //    이 예산은 영구적이다.
    //
    // ② **상시 표시 면적과 가독성을 동시에 만족시키려 했다.** 크게 하면 맵을 가리고
    //    작게 하면 안 읽힌다 — 그 사이에 답이 없다.
    //
    // 그래서 이 rev 의 규칙은 둘뿐이다:
    //   · **알약은 절대 리사이즈하지 않는다.** 고정 크기·고정 위치. 커지는 것은 말풍선뿐이고
    //     말풍선은 탭했을 때만 존재한다.
    //   · **x ≤ 300 을 넘지 않는다.** 알약도 말풍선도 40~296.
    //
    // 그리고 **말 대신 연출**이다. 「탭하면 ~」류 안내문을 전부 걷고, 당길 수 있을 때만
    // 알약이 반짝이고 화살표가 오르내린다. 잠기면 둘 다 멈춘다 — 상태가 문장이 아니라
    // 움직임으로 읽힌다.
    //
    // 남은시간·웨이브 진행은 **화면 최상단 바**로 나갔다(`ScoreHudView.SetTopBar`).
    // 읽기만 하는 값이 조작 옆에 있을 이유가 없다.
    // ─────────────────────────────────────────────────────────────────────────
    public class NextWaveDock : MonoBehaviour
    {
        [SerializeField] private BattleBridge bridge;

        // 구 `panelSize`·`timerPlate*`·`buttonSize`·`buttonFaceSprite`·`dockFrameSprite`·
        // `buttonColor`·`buttonFontSize`·`buttonMinFontSize`·`buttonContentPadding`·
        // `backingColor`·`timer*`·`countdown*` 는 은퇴했다. 씬 asset 에 orphan 키로 남지만
        // 무해하다(`fixedWaveIntervalSec` 선례).
        //
        // ⚠ **`panelOffset.x + pillSize.x ≤ 300` 을 지켜라.** 넘으면 배치 트레이와 겹친다
        // (위 ① 참조). 알약을 키우고 싶으면 폭이 아니라 높이를 키운다. Awake 가 경고한다.
        [Header("Pill (고정 — 리사이즈 금지)")]
        [SerializeField] private Vector2 panelOffset = new Vector2(40f, 40f);
        [SerializeField] private Vector2 pillSize = new Vector2(256f, 96f);
        [SerializeField] private float pillCornerRadius = 28f;
        [SerializeField] private Color pillFill = new Color(0.13f, 0.46f, 0.86f, 1f);
        [SerializeField] private Color pillBorder = new Color(0.55f, 0.88f, 1f, 1f);
        [SerializeField] private Color pillLockedFill = new Color(0.24f, 0.28f, 0.34f, 0.95f);
        [SerializeField] private Color pillLockedBorder = new Color(0.42f, 0.47f, 0.55f, 0.9f);
        [Tooltip("알약 아래 립(입체감). 누르면 이만큼 내려앉는다.")]
        [SerializeField] private float pillLip = 6f;
        [SerializeField] private Color pillLipColor = new Color(0.05f, 0.20f, 0.44f, 1f);
        [SerializeField] private Color pillLipLockedColor = new Color(0.13f, 0.16f, 0.20f, 0.95f);
        [SerializeField] private float pillFontSize = 32f;
        [SerializeField] private Color pillTextColor = Color.white;
        [SerializeField] private Color pillLockedTextColor = new Color(0.80f, 0.84f, 0.90f, 0.9f);

        [Header("Callout (탭했을 때만)")]
        [Tooltip("말풍선 폭. 알약과 같게 두면 세로선이 맞아 한 덩어리로 읽힌다.")]
        [SerializeField] private float calloutWidth = 256f;
        [SerializeField] private float calloutGap = 14f;
        [SerializeField] private float calloutPadding = 14f;
        [SerializeField] private float calloutLineHeight = 34f;
        [SerializeField] private float calloutCornerRadius = 18f;
        [SerializeField] private Color calloutFill = new Color(0.04f, 0.09f, 0.22f, 0.96f);
        [SerializeField] private Color calloutBorder = new Color(0.29f, 0.72f, 0.96f, 1f);
        [SerializeField] private float calloutFontSize = 27f;
        [SerializeField] private float calloutMinFontSize = 19f;
        [SerializeField] private Color calloutTextColor = new Color(0.95f, 0.98f, 1f, 1f);
        [Tooltip("말풍선이 스스로 닫히기까지의 초(실시간)")]
        [SerializeField] private float calloutHoldSeconds = 5f;

        // 연출은 **두 단계**다. 말풍선을 열기 전(idle)과 연 뒤(armed).
        //
        // armed 가 따로 있는 이유: 말풍선을 연 순간이 「한 번 더 누르면 당겨진다」는 유일한
        // 신호 시점인데, idle 과 같은 세기면 «토글됐다»가 안 읽힌다(사용자 확인). armed 는
        // 알약이 **숨쉬듯 커졌다 작아지고** 광택이 쉬지 않고 빠르게 흐른다.
        [Header("연출 — 말 대신 이것이 «누를 수 있음»을 알린다")]
        [Tooltip("화살표가 오르내리는 폭(px)")]
        [SerializeField] private float arrowBob = 6f;
        [SerializeField] private float arrowBobSeconds = 0.7f;
        [Tooltip("[idle] 광택이 훑는 시간 / 그 뒤 쉬는 시간(초)")]
        [SerializeField] private float shineSweepSeconds = 1.1f;
        [SerializeField] private float shineRestSeconds = 1.4f;
        [Tooltip("[armed] 광택이 훑는 시간 / 쉬는 시간 — 쉬지 않고 빠르게")]
        [SerializeField] private float armedSweepSeconds = 0.55f;
        [SerializeField] private float armedRestSeconds = 0.12f;
        [SerializeField] private float shineWidth = 44f;
        [SerializeField] private Color shineColor = new Color(1f, 1f, 1f, 0.22f);
        [Tooltip("[armed] 광택 색 — 더 밝게")]
        [SerializeField] private Color armedShineColor = new Color(1f, 1f, 1f, 0.42f);
        [Tooltip("[armed] 알약이 숨쉬는 배율 / 주기(초)")]
        [SerializeField] private float armedBreathScale = 1.045f;
        [SerializeField] private float armedBreathSeconds = 0.42f;
        [Tooltip("[armed] 화살표를 이만큼 더 크게 — «지금이다»")]
        [SerializeField] private float armedArrowScale = 1.25f;
        [SerializeField] private float tapPunch = 0.10f;

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
        private GameObject _callout;
        private RectTransform _calloutRect;
        private TextMeshProUGUI _calloutLabel;

        private Sprite _pillSpriteReady, _pillSpriteLocked;
        private Sprite _pillLipSpriteReady, _pillLipSpriteLocked;

        private Tween _tapTween, _arrowTween, _breathTween;
        // 스윕+대기를 Chain 으로 잇기 때문에 Sequence 다(Tween 이 아니다).
        private Sequence _shineSeq;
        private bool _built, _subscribed, _pressed;
        // 지금 도는 연출 단계. null 상태 = 정지.
        private enum Idle { Off, Ready, Armed }
        private Idle _idleStage = Idle.Off;
        private bool _calloutOpen;
        private float _calloutClosesAt;

        // 매 프레임 문자열 조립을 막는 직전값 캐시.
        private int _lastStateKey = int.MinValue;
        private int _lastCalloutWave = -2;
        private bool _lastCalloutAllowed;
        private readonly StringBuilder _listBuilder = new StringBuilder(128);

        // wave-pull-revival unit 4 — 튜토리얼 포커스 링이 감쌀 대상.
        // 선례: AwakeningGaugeView.HitRect.
        // **알약만** 준다 — 말풍선은 탭해야 생기고, 링이 가리켜야 할 것은 «누를 대상»이다.
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
            CloseCallout();
            // 판이 바뀌었는데 직전값이 같으면 조립을 건너뛰어 이전 판 문구가 남는다.
            _lastStateKey = int.MinValue;
            _lastCalloutWave = -2;
        }

        private void Update()
        {
            EnsureSubscribed();
            if (bridge == null || _panel == null || !_panel.activeSelf) return;

            bool available = bridge.NextWaveAvailable;
            if (_pillRoot != null && _pillRoot.activeSelf != available)
                _pillRoot.SetActive(available);
            if (!available)
            {
                if (_calloutOpen) CloseCallout();
                StopIdleAnimation();
                return;
            }

            // 말풍선은 **실시간**으로 닫힌다(unscaled) — 튜토리얼이 Battle 도메인을 0으로
            // 세우는 구간에서 영영 안 닫히면 안 된다.
            if (_calloutOpen && Time.unscaledTime >= _calloutClosesAt) CloseCallout();

            RefreshState();
            if (_calloutOpen) RefreshCallout();
        }

        // 알약이 말하는 것은 **한 문장**이다. 나머지는 연출이 말한다.
        private void RefreshState()
        {
            bool hasNext = bridge.NextWaveHasNext;
            bool allowed = bridge.PullAllowed;
            bool live = hasNext && allowed;

            int key = (hasNext ? 1 : 0) | (allowed ? 2 : 0) | (_calloutOpen ? 4 : 0);
            if (key == _lastStateKey) return;
            _lastStateKey = key;

            _pillButton.interactable = hasNext;
            _pillImage.sprite = live ? _pillSpriteReady : _pillSpriteLocked;
            _pillLipImage.sprite = live ? _pillLipSpriteReady : _pillLipSpriteLocked;
            _pillLabel.color = live ? pillTextColor : pillLockedTextColor;
            _arrowLabel.color = _pillLabel.color;
            SetPressed(false);

            // 문구는 둘뿐이다. 「탭하면 ~」류는 없다 — 그건 연출의 몫이다.
            _pillLabel.text = hasNext ? "다음 웨이브" : "마지막 웨이브";
            _arrowLabel.enabled = hasNext;

            // 당길 수 있을 때만 살아 움직인다. 잠기면 멈춘다 —
            // 「지금은 아니다」가 문장이 아니라 정지로 읽힌다.
            // 말풍선이 열려 있으면 **한 단계 더 세게**(armed) — 「한 번 더」의 신호다.
            SetIdleStage(!live ? Idle.Off : _calloutOpen ? Idle.Armed : Idle.Ready);
        }

        private void RefreshCallout()
        {
            bool allowed = bridge.PullAllowed;
            int wave = bridge.NextWaveNumber;
            // 편성은 웨이브가 바뀔 때만 조립한다(플랜은 판 시작에 확정되고 이후 불변).
            if (wave == _lastCalloutWave && allowed == _lastCalloutAllowed) return;
            _lastCalloutWave = wave;
            _lastCalloutAllowed = allowed;

            string body;
            int lines;
            if (!allowed)
            {
                // 잠긴 이유가 아니라 **무엇을 하면 풀리는지**를 말한다.
                body = "정리하면 다시";
                lines = 1;
            }
            else
            {
                body = BuildEnemyList(out lines);
                if (lines == 0) { body = "—"; lines = 1; }
            }

            _calloutLabel.text = body;
            // 말풍선만 내용에 맞춰 자란다(알약은 고정). 평소 2~3종, 보스 웨이브에 최대 5종.
            float h = calloutPadding * 2f + lines * calloutLineHeight;
            _calloutRect.sizeDelta = new Vector2(calloutWidth, h);
        }

        // 예고는 실스폰과 **같은 데이터**에서 나온다(unit 1 계약 4) — 브리지의 구성 창구를
        // 그대로 읽는다. 한 줄에 적 하나씩. 이름 외에 아무 설명도 붙이지 않는다.
        private string BuildEnemyList(out int lines)
        {
            lines = 0;
            if (!bridge.TryGetNextWaveComposition(out var groups)) return "";

            _listBuilder.Clear();
            for (int i = 0; i < groups.Count; i++)
            {
                var g = groups[i];
                if (g.unit == null || g.count <= 0) continue;
                if (_listBuilder.Length > 0) _listBuilder.Append('\n');
                _listBuilder
                    .Append(string.IsNullOrWhiteSpace(g.unit.displayName)
                        ? g.unit.name
                        : g.unit.displayName)
                    .Append("  ×").Append(g.count);
                lines++;
            }
            return _listBuilder.ToString();
        }

        // ── 탭: 1 = 말풍선 / 2 = 당김 ───────────────────────────────────────────
        //
        // **플레이어 경로는 여기 하나뿐이다.** `BattleBridge.ForceNextWave`(기제)를 직접
        // 부르지 않는다 — 부르면 겹침 상한이 우회된다.
        private void OnPillClicked()
        {
            if (bridge == null || !bridge.NextWaveAvailable) return;
            Punch();

            if (!_calloutOpen) { OpenCallout(); return; }

            // 잠겨 있으면 닫지 않고 창을 연장한다 — 사유를 읽을 시간을 뺏지 않는다.
            if (!bridge.TryPullNextWave())
            {
                _calloutClosesAt = Time.unscaledTime + calloutHoldSeconds;
                return;
            }
            CloseCallout();
        }

        private void OpenCallout()
        {
            _calloutOpen = true;
            _calloutClosesAt = Time.unscaledTime + calloutHoldSeconds;
            _lastCalloutWave = -2;          // 내용 재조립 강제
            _lastStateKey = int.MinValue;   // 연출을 Armed 로 올리도록 상태 재평가
            _callout.SetActive(true);
            RefreshCallout();
            // 살짝 올라오며 나타난다 — 「알약에서 나왔다」가 읽히게.
            _calloutRect.localScale = new Vector3(1f, 0.86f, 1f);
            Tween.ScaleY(_calloutRect, 1f, 0.15f, Ease.OutBack, useUnscaledTime: true);
        }

        private void CloseCallout()
        {
            _calloutOpen = false;
            _lastStateKey = int.MinValue;   // 연출을 Ready 로 되돌리도록 상태 재평가
            if (_callout != null && _callout.activeSelf) _callout.SetActive(false);
        }

        // ── 연출 ────────────────────────────────────────────────────────────────
        //
        // Off   — 잠김 / 마지막 웨이브. 아무것도 안 움직인다.
        // Ready — 누를 수 있다. 화살표가 천천히 오르내리고 광택이 가끔 훑는다.
        // Armed — 말풍선이 열렸다 = **한 번 더 누르면 당겨진다.** 알약이 숨쉬고 화살표가
        //         커지고 광택이 쉬지 않는다. Ready 와 확실히 달라야 «토글됐다»가 읽힌다.
        private void SetIdleStage(Idle stage)
        {
            if (_idleStage == stage) return;
            _idleStage = stage;

            if (_arrowTween.isAlive) _arrowTween.Stop();
            if (_shineSeq.isAlive) _shineSeq.Stop();
            if (_breathTween.isAlive) _breathTween.Stop();
            if (_pillFaceRect != null) _pillFaceRect.localScale = Vector3.one;

            if (stage == Idle.Off)
            {
                if (_arrowRect != null)
                {
                    _arrowRect.anchoredPosition = new Vector2(_arrowRect.anchoredPosition.x, 0f);
                    _arrowRect.localScale = Vector3.one;
                }
                if (_shineImage != null) _shineImage.enabled = false;
                return;
            }

            bool armed = stage == Idle.Armed;

            // 화살표: 위아래로. armed 면 더 크게 — «위를 봐라»가 아니라 «지금 눌러라»가 된다.
            _arrowRect.anchoredPosition = new Vector2(_arrowRect.anchoredPosition.x, 0f);
            _arrowRect.localScale = Vector3.one * (armed ? armedArrowScale : 1f);
            _arrowTween = Tween.UIAnchoredPositionY(_arrowRect, armed ? arrowBob * 1.6f : arrowBob,
                armed ? arrowBobSeconds * 0.6f : arrowBobSeconds,
                Ease.InOutSine, cycles: -1, CycleMode.Yoyo, useUnscaledTime: true);

            // 숨쉬기는 armed 에서만. 크기가 변하는 것은 **면(face)** 이고 알약 자체의
            // 레이아웃 크기는 그대로다(«알약은 리사이즈하지 않는다» 규칙 유지 — 스케일이다).
            if (armed)
                _breathTween = Tween.Scale(_pillFaceRect, armedBreathScale, armedBreathSeconds,
                    Ease.InOutSine, cycles: -1, CycleMode.Yoyo, useUnscaledTime: true);

            _shineImage.enabled = true;
            _shineImage.color = armed ? armedShineColor : shineColor;
            StartShineSweep();
        }

        // 스윕 1회 + 대기를 한 사이클로 돌린다. `cycles: -1` 로는 대기 구간을 표현할 수
        // 없어서(딜레이가 트윈이 아니다) 완료 콜백으로 다시 건다.
        private void StartShineSweep()
        {
            if (_idleStage == Idle.Off || _shineRect == null || !isActiveAndEnabled) return;
            bool armed = _idleStage == Idle.Armed;
            float from = -shineWidth;
            float to = pillSize.x + shineWidth;
            _shineRect.anchoredPosition = new Vector2(from, 0f);
            // ⚠ `useUnscaledTime` 은 **Sequence 가 소유한다** — 자식 트윈에 붙이면 PrimeTween 이
            // 「무시됐다」고 에러를 뱉는다(Sequence.cs:345). 시퀀스에 한 번만 준다.
            _shineSeq = Sequence.Create(useUnscaledTime: true)
                .Chain(Tween.UIAnchoredPositionX(_shineRect, from, to,
                    armed ? armedSweepSeconds : shineSweepSeconds, Ease.InOutQuad))
                .ChainDelay(armed ? armedRestSeconds : shineRestSeconds)
                .OnComplete(this, dock => dock.StartShineSweep());
        }

        private void StopIdleAnimation() => SetIdleStage(Idle.Off);

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
            float right = panelOffset.x + Mathf.Max(pillSize.x, calloutWidth);
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
            prt.sizeDelta = new Vector2(Mathf.Max(pillSize.x, calloutWidth), pillSize.y + pillLip);

            BuildPill();
            BuildCallout();

            _pillRoot.SetActive(false);
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

        private void BuildCallout()
        {
            _callout = new GameObject("Callout", typeof(RectTransform), typeof(Image));
            _callout.transform.SetParent(_panel.transform, false);
            _calloutRect = (RectTransform)_callout.transform;
            _calloutRect.anchorMin = Vector2.zero;
            _calloutRect.anchorMax = Vector2.zero;
            // pivot 이 바닥이라 sizeDelta 를 키우면 **위로만** 자란다 — 알약을 안 덮는다.
            _calloutRect.pivot = Vector2.zero;
            _calloutRect.anchoredPosition = new Vector2(0f, pillSize.y + pillLip + calloutGap);
            _calloutRect.sizeDelta = new Vector2(calloutWidth, calloutLineHeight * 2f);

            var back = _callout.GetComponent<Image>();
            back.sprite = UiRoundedSprite.Make(calloutCornerRadius, 3f, calloutFill, calloutBorder);
            back.type = Image.Type.Sliced;
            back.color = Color.white;
            // 말풍선은 읽는 것이지 누르는 것이 아니다 — 히트를 먹으면 그 구간 탭이 죽는다.
            back.raycastTarget = false;

            _calloutLabel = AddLabel(_callout.transform, "List", calloutFontSize, calloutTextColor,
                TextAlignmentOptions.Center, calloutMinFontSize);
            var lr = _calloutLabel.rectTransform;
            lr.anchorMin = Vector2.zero;
            lr.anchorMax = Vector2.one;
            lr.offsetMin = new Vector2(calloutPadding, calloutPadding);
            lr.offsetMax = new Vector2(-calloutPadding, -calloutPadding);
            _calloutLabel.text = "";

            _callout.SetActive(false);
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
            // 긴 적 이름(`Waypoint Basic Alt`)도 잘리면 안 된다.
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
                m.SetColor(ShaderUtilities.ID_OutlineColor, new Color(0.02f, 0.06f, 0.16f, 1f));
                m.SetFloat(ShaderUtilities.ID_OutlineWidth, 0.16f);
            }
            return label;
        }
    }
}
