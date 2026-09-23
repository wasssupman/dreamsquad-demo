using UnityEngine;
using UnityEngine.UI;
using Wassup.Core.TimeControl;

namespace Wassup.BattleCoreUnity.Hud
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
    // 판 종료·결과 화면·나가기는 **5c** 다. 여기서 「나가기」를 먼저 만들면 그 버튼이
    // 무엇을 정리해야 하는지를 이 파일이 결정하게 된다.
    [DisallowMultipleComponent]
    public sealed class CoreMenuPopup : MonoBehaviour
    {
        [SerializeField] private BattleDriver _driver;

        private RectTransform _panel;
        private Button _open;
        private TimeLease _lease;
        private bool _paused;
        private bool _built;

        public bool IsOpen => _paused;

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

            _panel = CoreHudUi.Rect("MenuPanel", transform, new Vector2(0.5f, 0.5f),
                                    new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(10f, 10f));
            CoreHudUi.Stretch(_panel);
            var dim = CoreHudUi.Fill("Dim", _panel, new Color(0f, 0f, 0f, 0.62f));
            // 막을 레이캐스트 대상으로 둬야 뒤쪽 보드 입력이 안 샌다 — 멈춘 판에 배치가
            // 들어가면 「정지 중인데 유닛이 놓였다」가 된다.
            dim.raycastTarget = true;

            var resume = CoreHudUi.Button("Resume", _panel, new Vector2(0.5f, 0.5f),
                                          new Vector2(0.5f, 0.5f), Vector2.zero,
                                          new Vector2(380f, 110f), CoreHudUi.Accent);
            CoreHudUi.Label("ResumeLabel", resume.transform, "계속하기", 44f,
                            new Color(0.1f, 0.08f, 0.05f, 1f));
            resume.onClick.AddListener(() => SetPaused(false));

            _panel.gameObject.SetActive(false);
        }

        private void SetPaused(bool on)
        {
            if (on == _paused) return;
            _paused = on;
            if (_panel != null) _panel.gameObject.SetActive(on);

            if (on) _lease = TimeManager.Instance.Request(TimeDomain.Battle, 0f);
            else Release();
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
        }
    }
}
