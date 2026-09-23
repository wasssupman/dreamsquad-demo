using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Wassup.BattleCore;

namespace Wassup.BattleCoreUnity.Hud
{
    // battle-core-rebuild unit 5b — **코스트.** 옛 `CostDisplay`(720줄)의 후계다.
    //
    // ⚠ **화면의 수는 내림이다**(`CostLedger.CurrentInt`). 판정은 실수로 하므로 「9.9 인데
    // 10 을 못 놓는다」가 정상이고, 화면이 반올림해서 10 이라고 쓰면 그 정상이 버그로 읽힌다.
    // 그 내림을 여기서 다시 하지 않는 이유도 같다 — 내림의 주인은 담당자다.
    //
    // 재생은 **사건을 내지 않는다**(연속값이라 매 틱 쏘면 판당 만 건). 그래서 바는 매 프레임
    // 읽고, 「불연속으로 움직였다」(지불·환급)만 `CostChanged` 로 튕긴다.
    [DisallowMultipleComponent]
    public sealed class CoreCostDisplay : MonoBehaviour
    {
        [SerializeField] private BattleDriver _driver;

        [SerializeField] private Vector2 _anchoredPos = new Vector2(40f, 40f);
        [SerializeField] private Vector2 _size = new Vector2(300f, 56f);

        private TextMeshProUGUI _value;
        private Image _fill;
        private RectTransform _root;
        private float _pop;
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
            if (e.Kind != CoreEventKind.CostChanged) return;
            // 지불(음수)과 획득(양수)을 같은 펀치로 튕긴다 — 「방금 움직였다」가 두 경우 다
            // 같은 사건이고, 방향은 숫자가 이미 말한다.
            _pop = 1f;
        }

        private void Update()
        {
            if (_driver == null || !_driver.Running) return;
            if (!_built) Build();

            var cost = _driver.Match.Cost;
            _value.text = $"{cost.CurrentInt} / {Mathf.FloorToInt(cost.Max)}";
            _fill.fillAmount = cost.Max > 0f ? Mathf.Clamp01(cost.Current / cost.Max) : 0f;

            // 재생이 멈춰 있는 동안(배치 창)은 바를 식힌다 — 「왜 안 차지」를 화면이 답한다.
            _fill.color = cost.RegenActive ? CoreHudUi.Accent : new Color(0.45f, 0.45f, 0.5f, 1f);

            _pop = Mathf.MoveTowards(_pop, 0f, Time.unscaledDeltaTime * 4f);
            float s = 1f + 0.12f * _pop * _pop;
            _root.localScale = new Vector3(s, s, 1f);
        }

        private void Build()
        {
            CoreHudUi.EnsureCanvas(gameObject);
            _built = true;

            _root = CoreHudUi.Rect("Cost", transform, new Vector2(0f, 0f), new Vector2(0f, 0f),
                                   _anchoredPos, _size);
            _fill = CoreHudUi.Bar(_root, new Color(0.07f, 0.08f, 0.12f, 0.85f), CoreHudUi.Accent);
            _value = CoreHudUi.Label("Value", _root, "0 / 0", 34f, CoreHudUi.Ink);
        }
    }
}
