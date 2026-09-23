using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Wassup.BattleCore;

namespace Wassup.BattleCoreUnity.Hud
{
    // battle-core-rebuild unit 5b — **배치 창.** 옛 `PlacementPhaseView`(571줄)의 후계다.
    //
    // 옛것이 들고 있던 규칙 둘이 여기 없다:
    //   ① **코스트 재생 스위치**(X24). 그 스위치가 UI 에 있어서 스크립트 진입이 그 뷰를 안
    //      지나면 자원이 0 에 멎었다 — 알려진 유일한 harness ≠ live 갭이었다. 지금은
    //      `CostLedger` 가 배치 창이 닫히는 **사건을 구독**해 스스로 켠다.
    //   ② **카운트다운 만료**. 창을 닫는 함수는 하나(`MatchClock.FinishPlacement`)이고,
    //      자동 시작도 그 함수로 합류한다. 여기서 만료를 재면 두 번째 경로가 생긴다.
    //
    // 남은 일은 둘뿐이다: 남은 시간을 **보여 주고**, 「시작」 커맨드를 **보낸다**.
    [DisallowMultipleComponent]
    public sealed class CorePlacementPhaseView : MonoBehaviour
    {
        [SerializeField] private BattleDriver _driver;

        [Tooltip("창이 닫힐 때 「시작!」이 커지며 사라지는 시간. 전투는 이미 시작한 뒤라 연출만 남는다.")]
        [SerializeField, Min(0f)] private float _outroSeconds = 0.35f;

        private RectTransform _root;
        private TextMeshProUGUI _countdown;
        private TextMeshProUGUI _caption;
        private Button _start;
        private RectTransform _outro;
        private TextMeshProUGUI _outroLabel;

        private float _outroLeft;
        private int _lastShownSecond = -1;
        private float _punch;
        private bool _built;

        private void OnEnable()
        {
            if (_driver != null) _driver.Subscribe(ViewOrder.Overhead, OnCoreEvent);
        }

        private void OnDisable()
        {
            if (_driver != null) _driver.Unsubscribe(OnCoreEvent);
        }

        private void OnCoreEvent(CoreEvent e)
        {
            if (e.Kind != CoreEventKind.PlacementPhaseChanged) return;
            if (e.Arg != 0) return;                 // 열림은 볼 것이 없다 — 아래가 매 프레임 그린다
            if (_outroSeconds > 0f) _outroLeft = _outroSeconds;
        }

        private void Update()
        {
            if (_driver == null || !_driver.Running) return;
            if (!_built) Build();

            var clock = _driver.Match.Clock;
            bool open = !clock.Ended && clock.Phase == MatchPhase.Placement;

            if (_root.gameObject.activeSelf != open) _root.gameObject.SetActive(open);
            if (open) PaintCountdown(clock);

            PaintOutro();
        }

        private void PaintCountdown(MatchClock clock)
        {
            // 길이 0 인 창은 «플레이어가 닫는다»는 뜻이다(자동으로 안 닫힌다) — 숫자 대신
            // 버튼만 말하게 둔다. 0 을 띄우면 「곧 시작한다」는 거짓 약속이 된다.
            int left = clock.PlacementTicksLeft;
            if (left <= 0)
            {
                _countdown.text = "";
                _caption.text = "배치하고 시작을 누른다";
                return;
            }

            float seconds = left * BattleMatch.Dt;
            int shown = Mathf.CeilToInt(seconds);
            _countdown.text = shown.ToString();
            _caption.text = "배치 중";

            if (shown != _lastShownSecond)
            {
                _lastShownSecond = shown;
                _punch = 1f;
            }
            _punch = Mathf.MoveTowards(_punch, 0f, Time.unscaledDeltaTime * 4f);
            float s = 1f + 0.6f * _punch * _punch;
            _countdown.rectTransform.localScale = new Vector3(s, s, 1f);
            _countdown.color = shown <= 3 ? CoreHudUi.Accent : CoreHudUi.Ink;
        }

        private void PaintOutro()
        {
            bool on = _outroLeft > 0f;
            if (_outro.gameObject.activeSelf != on) _outro.gameObject.SetActive(on);
            if (!on) return;

            _outroLeft -= Time.unscaledDeltaTime;
            float t = _outroSeconds > 0f ? Mathf.Clamp01(1f - _outroLeft / _outroSeconds) : 1f;
            float s = Mathf.Lerp(1f, 1.9f, t);
            _outro.localScale = new Vector3(s, s, 1f);
            var c = CoreHudUi.Accent;
            c.a = 1f - t;
            _outroLabel.color = c;
        }

        private void OnStart()
        {
            if (_driver == null || !_driver.Running) return;
            // **판정은 코어가 한다.** 같은 국면 재진입은 저쪽이 무시하므로 여기서 거르지 않는다 —
            // 거르면 「어느 쪽이 무시했나」가 두 곳이 된다.
            _driver.Apply(Command.FinishPlacement());
        }

        private void Build()
        {
            CoreHudUi.EnsureCanvas(gameObject);
            _built = true;

            // 화면 전체를 덮는 빈 판. 자식들이 화면 모서리에 앵커되므로 **늘려 둔다** —
            // 작은 사각에 붙이면 「우하단 시작 버튼」이 화면 중앙에 뜬다.
            _root = CoreHudUi.Rect("Placement", transform, new Vector2(0.5f, 0.5f),
                                   new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(10f, 10f));
            CoreHudUi.Stretch(_root);

            _countdown = CoreHudUi.Label("Countdown",
                CoreHudUi.Rect("CountdownRow", _root, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                               new Vector2(0f, 120f), new Vector2(420f, 280f)),
                "", 240f, CoreHudUi.Ink);

            _caption = CoreHudUi.Label("Caption",
                CoreHudUi.Rect("CaptionRow", _root, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                               new Vector2(0f, -20f), new Vector2(720f, 44f)),
                "", 32f, CoreHudUi.InkDim);

            _start = CoreHudUi.Button("Start", _root, new Vector2(1f, 0f), new Vector2(1f, 0f),
                                      new Vector2(-40f, 120f), new Vector2(300f, 92f), CoreHudUi.Accent);
            CoreHudUi.Label("StartLabel", _start.transform, "시작", 40f, new Color(0.1f, 0.08f, 0.05f, 1f));
            _start.onClick.AddListener(OnStart);

            _outro = CoreHudUi.Rect("Outro", transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                                    new Vector2(0f, 60f), new Vector2(720f, 160f));
            _outroLabel = CoreHudUi.Label("OutroLabel", _outro, "시작!", 130f, CoreHudUi.Accent);
            _outro.gameObject.SetActive(false);
        }
    }
}
