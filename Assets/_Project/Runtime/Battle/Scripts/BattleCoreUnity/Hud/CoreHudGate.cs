using UnityEngine;
using Somnia.Battle.BattleCore;

namespace Somnia.Battle.BattleCoreUnity.Hud
{
    // battle-core-rebuild unit 8a — **결과 화면 뒤로 전투 HUD 를 숨긴다**(README 고지 ⑴ · rules X19).
    //
    // 옛 씬은 HUD 조각마다 `GameManager.PhaseChanged` 를 구독해 Battle/Placement 가 아니면 스스로 숨었다
    // (`ScoreHudView.cs:842~873` · `DefenderSelector.cs:283~295` · `CostDisplay.cs:147` ·
    // `AwakeningGaugeView.cs:385` · `DreamcatcherHandView.cs:902` · `NextWaveDock.cs:189`).
    // 새 HUD 는 페이즈를 안 읽고 드라이버 읽기 모델만 보므로 결과 패널 뒤에 그대로 남았다(5c Play 육안 ⚠)
    //
    // 조각마다 게이트를 다시 심지 않고 **한 곳에서 캔버스를 끈다**: HUD 조각들은 캔버스를 코드로 세우고
    // (`CoreHudUi.EnsureCanvas`·`UiCanvasSetup.Ensure`) 여럿이 한 캔버스를 공유해서, 조각 단위로는 끌 수 없다.
    //
    // demo-diet unit 0 — 결과 화면이 전투 밖(App)으로 갔다. 그래서 「결과가 떴다」를 여기서 읽을 수 없고, **덮는 쪽이
    // 말해 준다**: 결과를 띄우는 바깥이 `Hide(true)` 를 부르고, 새 판(`MatchStarted`)에서 자동으로 다시 켠다.
    // 규칙은 없다 — 끄고 켜는 것뿐이다. 바깥이 없는 판(에디터 직접 Play)은 끝나도 HUD 가 남아 마지막 점수를 보여 준다.
    [DisallowMultipleComponent]
    public sealed class CoreHudGate : MonoBehaviour
    {
        [SerializeField] private BattleDriver _driver;

        [Tooltip("결과 뒤에 숨길 HUD 루트(자기 캔버스를 가진 오브젝트). 캔버스는 런타임에 세워지므로 오브젝트로 가리킨다.")]
        [SerializeField] private GameObject[] _hudRoots = System.Array.Empty<GameObject>();

        /// <summary>지금 HUD 를 숨기고 있나. 테스트의 증언 창.</summary>
        public bool Hidden { get; private set; }

        private void OnEnable()
        {
            if (_driver != null) _driver.Subscribe(ViewOrder.Outcome, OnCoreEvent);
        }

        private void OnDisable()
        {
            if (_driver != null) _driver.Unsubscribe(OnCoreEvent);
            Apply(false);
        }

        private void OnCoreEvent(CoreEvent e)
        {
            if (e.Kind == CoreEventKind.MatchStarted) Apply(false);
        }

        /// <summary>바깥(결과를 덮는 쪽)이 부른다. 판이 끝난 뒤에만 의미가 있지만 시점은 부르는 쪽이 안다.</summary>
        public void Hide(bool hide)
        {
            if (hide != Hidden) Apply(hide);
        }

        private void Apply(bool hide)
        {
            Hidden = hide;
            for (int i = 0; i < _hudRoots.Length; i++)
            {
                var root = _hudRoots[i];
                if (root == null) continue;
                // 루트 캔버스만 끈다 — 오브젝트를 끄면 그 위 컴포넌트의 구독·리스까지 같이 꺼진다.
                var canvases = root.GetComponentsInChildren<Canvas>(true);
                for (int c = 0; c < canvases.Length; c++)
                    if (canvases[c].isRootCanvas || canvases[c].overrideSorting) canvases[c].enabled = !hide;
            }
        }
    }
}
