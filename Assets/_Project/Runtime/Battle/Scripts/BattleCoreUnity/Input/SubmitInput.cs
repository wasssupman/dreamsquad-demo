using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Somnia.Battle.BattleCore;
using Somnia.Battle.BattleCoreUnity.Hud;

namespace Somnia.Battle.BattleCoreUnity.Input
{
    // battle-core-rebuild unit 5b — **제출.** 판을 플레이어 쪽에서 끝내는 통로다.
    //
    // 종료 통로는 셋이고(만료·제출·붕괴) 끝내는 **함수는 하나**다. 이 버튼은 그중 하나를
    // 커맨드로 두드릴 뿐이고, 「지금 눌러도 되나」는 코어가 답한다(`SubmitLocked`).
    //
    // ⚠ **버튼을 숨기는 것으로 판정을 대신하지 않는다.** 해금 전에는 눌려도 receipt 가
    // 거절이고, 화면은 그 사유를 그대로 보여 준다 — 「눌리지 않는 버튼」은 왜 안 되는지를
    // 말하지 못한다. 대신 잠금 동안 남은 시간을 써 준다.
    //
    // 판이 끝난 뒤의 결과 화면·서버 제출 게이트는 **5c** 다. 여기서 만들면 그 게이트가
    // 입력 층에 살게 된다.
    [DisallowMultipleComponent]
    public sealed class SubmitInput : MonoBehaviour
    {
        [SerializeField] private BattleDriver _driver;
        [SerializeField] private CoreDefenderTray _tray;

        [SerializeField] private Vector2 _anchoredPos = new Vector2(-40f, 40f);
        [SerializeField] private Vector2 _size = new Vector2(240f, 84f);

        private Button _button;
        private TextMeshProUGUI _label;
        private Image _image;
        private bool _built;

        private void Update()
        {
            if (_driver == null || !_driver.Running) return;
            if (!_built) Build();

            var clock = _driver.Match.Clock;

            // 판이 끝났으면 버튼을 내린다. 이건 판정을 대신하는 숨김이 아니라 **그 화면이
            // 더 이상 이 판의 것이 아니라는** 표시다(결과 화면은 5c).
            bool alive = !clock.Ended;
            if (_button.gameObject.activeSelf != alive) _button.gameObject.SetActive(alive);
            if (!alive) return;

            bool unlocked = clock.SubmitUnlocked;
            _image.color = unlocked ? CoreHudUi.Accent : CoreHudUi.Panel;
            _label.color = unlocked ? new Color(0.1f, 0.08f, 0.05f, 1f) : CoreHudUi.InkDim;
            _label.text = unlocked
                ? "제출"
                : $"제출 {CoreHudUi.Clock((clock.SubmitUnlockTick - clock.BattleTicks) * BattleMatch.Dt)}";
        }

        private void OnSubmit()
        {
            if (_driver == null || !_driver.Running) return;
            var receipt = _driver.Apply(Command.Submit());
            if (receipt.Accepted) return;
            // 거절도 **화면이 말한다.** 조용한 무동작은 「버튼이 고장났다」로 읽힌다.
            if (_tray != null) _tray.ShowReject(receipt.Reason);
        }

        private void Build()
        {
            CoreHudUi.EnsureCanvas(gameObject);
            _built = true;

            _button = CoreHudUi.Button("Submit", transform, new Vector2(1f, 0f), new Vector2(1f, 0f),
                                       _anchoredPos, _size, CoreHudUi.Panel);
            _image = _button.targetGraphic as Image;
            _label = CoreHudUi.Label("SubmitLabel", _button.transform, "제출", 34f, CoreHudUi.InkDim);
            _button.onClick.AddListener(OnSubmit);
        }
    }
}
