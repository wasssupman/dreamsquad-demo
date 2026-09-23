using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Wassup.BattleCore;
using Wassup.Data;

namespace Wassup.BattleCoreUnity.Hud
{
    // battle-core-rebuild 5b 수정 — **선택 패널.** 옛 `DcInspectPanelView`(802줄) +
    // `DcInspectController`(637줄)에서 이번에 필요한 몫만 옮겼다.
    //
    // 사용자 플레이 3차의 문장: **「배치된 유닛 터치/유닛 셀 누르면 상세 UI 아직 미구현인가?
    // 퇴근이 확인 불가」**. 5b 는 이 패널을 unit 7 로 미루고 퇴근을 **길게 누르기로 새로**
    // 지었는데, 그건 옛 게임에 없던 축이다 — 퇴근은 **이 패널의 액션 슬롯 버튼**이다
    // (`defender-clock-out/2`). 길게 누르기는 이 이식으로 은퇴했다.
    //
    // ⚠ **액션 슬롯은 기능 이름을 갖지 않는다**(옛 README 계약 그대로). `SetActionState(
    // enabled, label)` 이고 라벨의 주인은 호출자다 — 슬롯을 「퇴근 버튼」으로 재특화하면
    // 다음 동사가 올 때 시그니처부터 되돌려야 한다.
    //
    // ⚠ **부착 카드 줄·손패는 안 옮겼다**(unit 7). 빈 칸을 그리지 않는다 — 빈 슬롯은
    // 「여기서 조절된다」고 광고하고, 그 광고는 아직 거짓이다.
    [DisallowMultipleComponent]
    public sealed class CoreSelectionPanel : MonoBehaviour
    {
        [SerializeField] private BattleDriver _driver;

        [Header("자리")]
        [Tooltip("패널 폭(px, 1920×1080 기준).")]
        [SerializeField, Min(120f)] private float _width = 320f;
        [Tooltip("화면 왼쪽에서 띄우는 거리. 옛 패널과 같이 **좌측 고정**이다.")]
        [SerializeField] private Vector2 _anchoredPos = new Vector2(24f, 0f);

        private sealed class StatRow
        {
            public TextMeshProUGUI Label;
            public TextMeshProUGUI Value;
            public TextMeshProUGUI Chip;
        }

        private RectTransform _root;
        private TextMeshProUGUI _unitName;
        private Image _hpFill;
        private readonly StatRow[] _rows = new StatRow[3];
        private Button _action;
        private TextMeshProUGUI _actionLabel;
        private System.Action _onAction;
        private (bool enabled, string label)? _actionState;
        private bool _built;

        /// <summary>지금 떠 있나. 입력이 「빈 곳 탭 = 닫기」를 판단하는 창구.</summary>
        public bool IsVisible => _built && _root != null && _root.gameObject.activeSelf;

        /// <summary>액션 버튼이 눌릴 수 있나. 테스트와 온보딩이 묻는다.</summary>
        public bool ActionEnabled => _built && _action != null && _action.interactable
                                     && _action.gameObject.activeInHierarchy;

        /// <summary>지금 보여 주는 이름. 「갈아탔나」를 밖에서 확인하는 창구.</summary>
        public string ShownName => _built && _unitName != null ? _unitName.text : "";

        /// <summary>액션 버튼을 누른다(포인터 없이 같은 경로를 탄다).</summary>
        public void InvokeAction()
        {
            if (ActionEnabled) _onAction?.Invoke();
        }

        /// <summary>
        /// 그 유닛을 띄운다. `onAction` 이 null 이면 액션 슬롯을 **감춘다** — 배선이 없는데
        /// 눌리지 않는 버튼을 남기면 「고장났다」로 읽힌다.
        /// </summary>
        public void Show(string unitName, Sprite portrait, System.Action onAction)
        {
            Build();
            _onAction = onAction;
            _action.gameObject.SetActive(onAction != null);
            _actionState = null;                     // 대상이 바뀌었으니 잠금 래치를 비운다
            _unitName.text = string.IsNullOrEmpty(unitName) ? "" : unitName;
            _root.gameObject.SetActive(true);
        }

        /// <summary>
        /// 실효 스탯. **매 프레임** 들어온다(대상 1체라 비용은 무시 가능).
        ///
        /// `has=false` 는 코어가 그 개체를 못 내주는 프레임이다(사망 직후 등) — **직전 값을
        /// 유지한다.** 대시로 바꾸면 사망 순간 숫자가 깜빡이고, 어차피 곧 닫힌다.
        /// </summary>
        public void SetStats(in UnitStatReadout readout, bool has)
        {
            if (!_built || !has || !IsVisible) return;

            SetRow(0, Mathf.Round(readout.hp) + " / " + Mathf.Round(readout.hpMax),
                   readout.hpMaxBase, readout.hpMax, "F0");
            SetRow(1, readout.damage.ToString("0.#"), readout.damageBase, readout.damage, "0.#");
            SetRow(2, readout.attackRate.ToString("0.0") + "/s",
                   readout.attackRateBase, readout.attackRate, "0.0");

            _hpFill.fillAmount = readout.hpMax > 0f ? Mathf.Clamp01(readout.hp / readout.hpMax) : 0f;
        }

        /// <summary>
        /// 액션 슬롯의 상태. 라벨의 주인은 **호출자**다(슬롯은 기능 이름을 갖지 않는다).
        /// 같은 값이면 아무것도 안 한다 — 매 프레임 불려도 TMP 를 다시 굽지 않는다.
        /// </summary>
        public void SetActionState(bool enabled, string label)
        {
            if (!_built || _action == null || !_action.gameObject.activeSelf) return;
            if (_actionState.HasValue && _actionState.Value.enabled == enabled
                && _actionState.Value.label == label) return;
            _actionState = (enabled, label);

            _action.interactable = enabled;
            if (_actionLabel != null)
            {
                _actionLabel.text = label ?? "";
                var c = CoreHudUi.Ink;
                c.a = enabled ? 1f : 0.4f;
                _actionLabel.color = c;
            }
            if (_action.targetGraphic is Image img)
            {
                var c = img.color;
                c.a = enabled ? 0.92f : 0.45f;
                img.color = c;
            }
        }

        /// <summary>멱등 — 미선택 상태에서도 불린다.</summary>
        public void Hide()
        {
            if (!_built) return;
            _onAction = null;
            _actionState = null;
            _root.gameObject.SetActive(false);
        }

        // ── 조립 ─────────────────────────────────────────────────────────────

        private void SetRow(int index, string valueText, float baseValue, float effValue, string fmt)
        {
            var row = _rows[index];
            row.Value.text = valueText;

            // **변화가 없으면 칩을 그리지 않는다.** 항상 ▲0 을 띄우면 노이즈이고, 지금은
            // 모디파이어 생산자가 없어(unit 6) 언제나 0 이다 — 그 축이 열리면 여기가 그대로 산다.
            int sign = UnitStatMath.ResolveDelta(baseValue, effValue,
                                                 UnitStatMath.DefaultDeltaEpsilon, out float magnitude);
            if (sign == 0) { row.Chip.enabled = false; return; }
            row.Chip.enabled = true;
            row.Chip.text = (sign > 0 ? "▲" : "▼") + magnitude.ToString(fmt);
            row.Chip.color = sign > 0 ? CoreHudUi.Good : CoreHudUi.Bad;
        }

        private void Build()
        {
            if (_built) return;
            _built = true;

            var canvas = CoreHudUi.EnsureCanvas(gameObject);
            _root = CoreHudUi.Rect("SelectionPanel", canvas.transform,
                                   new Vector2(0f, 0.5f), new Vector2(0f, 0.5f),
                                   _anchoredPos, new Vector2(_width, 268f));
            CoreHudUi.Fill("Bg", _root, CoreHudUi.Panel);

            _unitName = CoreHudUi.Label("UnitName",
                CoreHudUi.Rect("NameRow", _root, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                               new Vector2(0f, -14f), new Vector2(_width - 24f, 36f)),
                "", 30f, CoreHudUi.Ink, TextAlignmentOptions.Left);

            var barHost = CoreHudUi.Rect("HpBar", _root, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                                         new Vector2(0f, -54f), new Vector2(_width - 24f, 10f));
            _hpFill = CoreHudUi.Bar(barHost, new Color(0f, 0f, 0f, 0.45f), CoreHudUi.Good);

            string[] labels = { "체력", "공격력", "공격 속도" };
            for (int i = 0; i < _rows.Length; i++)
            {
                var host = CoreHudUi.Rect($"Stat{i}", _root, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                                          new Vector2(0f, -78f - i * 34f), new Vector2(_width - 24f, 30f));
                _rows[i] = new StatRow
                {
                    Label = CoreHudUi.Label("Label", host, labels[i], 22f, CoreHudUi.InkDim,
                                            TextAlignmentOptions.Left),
                    Value = CoreHudUi.Label("Value", host, "", 24f, CoreHudUi.Ink,
                                            TextAlignmentOptions.Right),
                };
                _rows[i].Chip = CoreHudUi.Label("Chip",
                    CoreHudUi.Rect("ChipRow", host, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f),
                                   new Vector2(-86f, 0f), new Vector2(64f, 26f)),
                    "", 20f, CoreHudUi.Good, TextAlignmentOptions.Right);
                _rows[i].Chip.enabled = false;
            }

            // 액션 슬롯 **한 칸**. 옛 패널의 그 자리이고, 지금 실린 동사는 퇴근 하나다.
            _action = CoreHudUi.Button("Action", _root, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                                       new Vector2(0f, 16f), new Vector2(_width - 24f, 48f),
                                       new Color(0.16f, 0.2f, 0.3f, 0.92f));
            _actionLabel = CoreHudUi.Label("ActionLabel", _action.transform, "", 26f, CoreHudUi.Ink);
            _action.onClick.AddListener(() => _onAction?.Invoke());

            _root.gameObject.SetActive(false);
        }

        // ── 읽기 모델 ────────────────────────────────────────────────────────

        /// <summary>
        /// 코어의 개체 + 정의표 → 화면이 읽는 값. **실효 = 정의표 × 살아 있는 모디파이어**인데
        /// 지금은 그 생산자가 없어(unit 6) 기본값과 같다 — 그래서 델타 칩이 안 그려진다.
        /// 그 축이 열리면 이 함수만 바뀐다.
        /// </summary>
        public static UnitStatReadout ReadoutOf(Unit unit, in UnitDef def)
        {
            float damage = 0f;
            var outputs = def.Attack.Outputs;
            if (outputs != null)
                for (int i = 0; i < outputs.Length; i++)
                    if (outputs[i].Kind == Wassup.BattleCore.AttackOutputKind.Damage) { damage = outputs[i].Magnitude; break; }

            // 큰 숫자 = 빠름이 직관적이라 쿨다운 초가 아니라 초당 발사 횟수로 낸다(옛 규약).
            float rate = def.AttackCooldown > 0f ? 1f / def.AttackCooldown : 0f;

            return new UnitStatReadout
            {
                hp = unit != null ? unit.Health : 0f,
                hpMax = def.Health,
                hpMaxBase = def.Health,
                damage = damage,
                damageBase = damage,
                attackRate = rate,
                attackRateBase = rate,
            };
        }
    }
}
