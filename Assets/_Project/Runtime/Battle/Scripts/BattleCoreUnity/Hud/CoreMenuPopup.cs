using UnityEngine;
using UnityEngine.UI;
using Somnia.Battle.Core.TimeControl;

namespace Somnia.Battle.BattleCoreUnity.Hud
{
    // battle-core-rebuild unit 5b — **메뉴.** 옛 `MenuPopup`(294줄)의 후계다.
    //
    // 하는 일은 하나: **판의 시간을 멈춘다.** `Time.timeScale` 을 쓰지 않는다 — 전역 배율은
    // UI 애니메이션·연출까지 같이 끌고 가서 메뉴 자체가 얼어붙는다. 이 프로젝트가 도메인
    // 시간 제어(`TimeManager`)를 따로 둔 이유가 그것이고, 여기서는 Battle 도메인만 0 으로
    // 리스한다. 그러면 드라이버의 **틱 발행률**이 0 이 되고 코어는 배율을 모른 채 멈춘다(계약 5).
    //
    // ⚠ 리스는 **반드시 반납한다**(`Dispose`). 반납을 빠뜨리면 메뉴를 닫아도 판이 멈춰 있고,
    // 그 증상은 「게임이 멈췄다」로만 보여 원인이 이 파일이라는 걸 아무도 못 찾는다.
    // 그래서 비활성·파괴 경로에도 반납을 건다.
    //
    // unit 8b — **「나가기」·「성적 확정」**(옛 `MenuPopup.cs:113~161`). 버튼 하나가 **제출 해금을 기점으로 정체를 바꾼다**:
    // 해금 전 = 나가기(참가 포기 0점 마감 → 기록 → 로비), 해금 뒤 = 성적 확정(현재 킬로 판을 끝내고 결과 화면으로 —
    // 로비 직행이 아니다, 확정해 놓고 리더보드를 못 보면 안 된다). 여는 순간 한 번만 판정한다 — 열려 있는 동안
    // 판이 멈춰 있어 시계가 안 흐르므로 열린 채로 정체가 바뀌는 일이 구조적으로 없다(옛 주석 그대로).
    // 「포기」류 어휘를 제출 버튼에 쓰지 않는다(옛 사용자 확정). 무엇을 정리하는지는 이 파일이 정하지 않는다 —
    // 마감·기록은 `CoreMatchOutcomePresenter`, 판 종료는 코어 `Submit` 커맨드다.
    //
    // unit 8a — **공격 패턴 브리핑**(옛 `MenuPopup.cs:80~106`). 메뉴가 열리면 이번 판의 웨이브 카드
    // 스트립이 위에서 펼쳐지고, 닫히면 말려 올라간다. 입력은 코어가 실제로 쓰는 플랜이다
    // (`CoreBriefingPlan` — 옛 경로처럼 생성기를 다시 부르지 않는다). 층 배치도 옛 것 그대로다:
    // 스트립 950 위에 버튼 960, 스트립의 자체 딤이 배경이라 메뉴는 딤을 겹쳐 칠하지 않는다.
    [DisallowMultipleComponent]
    public sealed class CoreMenuPopup : MonoBehaviour
    {
        [SerializeField] private BattleDriver _driver;

        // 옛 `MenuPopup` 의 두 상수 그대로 — 스트립을 팝업 **아래**로 올리고 버튼은 그 위.
        private const int StripSortingOrder = 950;
        private const int PopupSortingOrder = 960;

        private RectTransform _panel;
        private Image _dim;
        private Somnia.Battle.UI.WavePatternStripView _strip;
        private Button _open;
        private TimeLease _lease;
        private bool _paused;
        private bool _built;
        private Button _exit;
        private Image _exitImage;
        private TMPro.TextMeshProUGUI _exitLabel;
        private bool _submitMode;

        /// <summary>지금 그 버튼이 「성적 확정」인가(여는 순간 판정). 테스트의 창.</summary>
        public bool ExitIsSubmit => _submitMode;
        /// <summary>나가기/성적 확정 버튼을 누른다(테스트 창 — 사람 손과 같은 경로).</summary>
        public void PressExit() => OnExit();
        /// <summary>메뉴를 연다(테스트 창 — 「II」 버튼과 같은 경로).</summary>
        public void Open() { if (!_built && _driver != null) Build(); SetPaused(true); }

        public bool IsOpen => _paused;

        /// <summary>브리핑 스트립. 테스트가 「카드 수 = 코어 플랜 웨이브 수」를 증언하는 창이다.</summary>
        public Somnia.Battle.UI.WavePatternStripView Strip => _strip;

        /// <summary>마지막으로 스트립에 넘긴 플랜의 웨이브 수(카드 상한 12 와 무관한 입력 쪽 수).</summary>
        public int BriefedWaveCount { get; private set; }

        private void Update()
        {
            if (_driver == null) return;
            if (!_built) Build();
        }

        private void Build()
        {
            CoreHudUi.EnsureCanvas(gameObject);
            _built = true;

            _open = CoreHudUi.Button("MenuButton", transform, new Vector2(1f, 1f), new Vector2(1f, 1f),
                                     new Vector2(-40f, -34f), new Vector2(96f, 66f), CoreHudUi.Panel);
            CoreHudUi.Label("MenuLabel", _open.transform, "II", 38f, CoreHudUi.Ink);
            _open.onClick.AddListener(() => SetPaused(true));

            // 브리핑 스트립 — 패널보다 **먼저** 세워 형제 순서로도 아래에 둔다. 스트립은 자기 캔버스를
            // 덧씌워 950 으로 뜨고(`SetSortingOverride`) 패널은 960 이다.
            var stripRt = CoreHudUi.Rect("WavePatternStrip", transform, new Vector2(0.5f, 0.5f),
                                         new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(10f, 10f));
            CoreHudUi.Stretch(stripRt);
            _strip = stripRt.gameObject.AddComponent<Somnia.Battle.UI.WavePatternStripView>();

            _panel = CoreHudUi.Rect("MenuPanel", transform, new Vector2(0.5f, 0.5f),
                                    new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(10f, 10f));
            CoreHudUi.Stretch(_panel);
            var panelCanvas = _panel.gameObject.AddComponent<Canvas>();
            panelCanvas.overrideSorting = true;
            panelCanvas.sortingOrder = PopupSortingOrder;
            _panel.gameObject.AddComponent<GraphicRaycaster>();
            // 막을 레이캐스트 대상으로 둬야 뒤쪽 보드 입력이 안 샌다 — 멈춘 판에 배치가
            // 들어가면 「정지 중인데 유닛이 놓였다」가 된다. 색은 투명 — 딤은 스트립의 것이다
            // (옛 `MenuPopup` 「no double-dim」). 스트립이 없으면 이 막이 옛 5b 딤을 그린다.
            _dim = CoreHudUi.Fill("Dim", _panel, new Color(0f, 0f, 0f, 0.62f));
            _dim.raycastTarget = true;

            // 버튼은 **하단**이다(옛 `MenuPopup.BuildCanvas` — 「clear of the upper-third strip」).
            // 화면 가운데에 두면 브리핑 카드 줄과 겹친다. 자리·크기는 옛 「재개」 버튼 그대로이고
            // 오른쪽 짝(`(150, 120)` 「나가기」)은 8b 가 채운다.
            var resume = CoreHudUi.Button("Resume", _panel, new Vector2(0.5f, 0f),
                                          new Vector2(0.5f, 0f), new Vector2(-150f, 120f),
                                          new Vector2(260f, 96f), CoreHudUi.Accent);
            CoreHudUi.Label("ResumeLabel", resume.transform, "계속하기", 44f,
                            new Color(0.1f, 0.08f, 0.05f, 1f));
            resume.onClick.AddListener(() => SetPaused(false));

            // 오른쪽 짝 — 옛 「나가기」 자리·크기(하단 +150,120 · 260×96). 색은 옛 두 값(확정 = 파랑 · 포기 = 빨강).
            _exit = CoreHudUi.Button("Exit", _panel, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                                     new Vector2(150f, 120f), new Vector2(260f, 96f), CoreHudUi.Panel);
            _exitImage = _exit.targetGraphic as Image;
            _exitLabel = CoreHudUi.Label("ExitLabel", _exit.transform, "나가기", 44f, Color.white);
            _exit.onClick.AddListener(OnExit);

            _panel.gameObject.SetActive(false);
        }

        private void SetPaused(bool on)
        {
            if (on == _paused) return;
            _paused = on;
            if (_panel != null) _panel.gameObject.SetActive(on);

            if (on) _lease = TimeManager.Instance.Request(TimeDomain.Battle, 0f);
            else Release();
            if (on) RefreshExitButton();
            Brief(on);
        }

        private void RefreshExitButton()
        {
            _submitMode = _driver != null && _driver.Running && !_driver.Match.Clock.Ended
                          && _driver.Match.Clock.SubmitUnlocked;
            if (_exitLabel != null) _exitLabel.text = _submitMode ? "제출" : "나가기";
            if (_exitImage != null)
                _exitImage.color = _submitMode
                    ? new Color(0.16f, 0.42f, 0.62f, 0.96f)   // 제출 = 확정. 경고색이 아니다
                    : new Color(0.6f, 0.2f, 0.2f, 0.96f);     // 나가기 = 0점 포기
        }

        private void OnExit()
        {
            if (_submitMode && _driver != null && _driver.Running)
            {
                // 일시정지 리스를 먼저 놓아야 마감이 정상 진행된다(옛 `Close()` → `SubmitMatch`).
                SetPaused(false);
                var receipt = _driver.Apply(Somnia.Battle.BattleCore.Command.Submit());
                if (!receipt.Accepted)
                    Debug.LogWarning($"[CoreMenuPopup] 성적 확정이 거절됐다 — {receipt.Reason}", this);
                return;
            }
            // 나가기 — 리스를 놓고 드라이버에 알린다. 0점 마감·기록·씬 복귀는 `BattleDriver.MatchAbandoned` 구독자 몫이다
            // (demo-diet unit 0 — 전투는 바깥으로 가는 길을 모른다). 구독자가 없으면 판은 그대로 멈춰 있다.
            if (_paused) { _paused = false; Release(); }
            if (_driver != null) _driver.Abandon();
            else Debug.LogWarning("[CoreMenuPopup] 드라이버가 배선되지 않았다 — 나가기를 알릴 곳이 없다.", this);
        }

        // 옛 `MenuPopup.Open/Close` 의 스트립 몫. 여는 순간 **이 판의 플랜**으로 다시 그린다.
        private void Brief(bool on)
        {
            if (_strip == null) return;
            if (on)
            {
                var plan = _driver != null && _driver.Running
                    ? CoreBriefingPlan.From(_driver.Match.Waves, _driver.EnemyAssets)
                    : default;
                BriefedWaveCount = plan.waves != null ? plan.waves.Count : 0;
                _strip.RebuildFromPlan(plan);
                _strip.SetSortingOverride(true, StripSortingOrder);
                _strip.FadeIn();
                if (_dim != null) _dim.color = new Color(0f, 0f, 0f, 0f);
            }
            else
            {
                _strip.Roll();
                _strip.SetSortingOverride(false);
            }
        }

        private void Release()
        {
            _lease.Dispose();
            _lease = default;
        }

        private void OnDisable()
        {
            if (!_paused) return;
            _paused = false;
            if (_panel != null) _panel.gameObject.SetActive(false);
            Release();
            if (_strip != null) { _strip.SnapHidden(); _strip.SetSortingOverride(false); }
        }
    }
}
