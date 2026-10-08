using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Somnia.Battle.BattleCore;
using Somnia.Battle.Data;

namespace Somnia.Battle.BattleCoreUnity.Hud
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
    // unit 7c — **부착 카드 줄**(옛 `DcInspectPanelView.BuildAttachRows`). 줄은 **부착 사건**(`CardAttached`/`CardDetached`)이
    // 세우고 거둔다 — 이 패널이 자기 구독으로 숙주별 목록을 든다(계약 12). 순서 = **부착 번호 오름차순**(D20 — 부착 순서가 곧
    // 기능이다; 사건의 묶음 핸들이 판 수명 단조라 그 순서다). 카드가 없으면 섹션째 안 그린다(빈 칸 광고 금지). 문안은
    // formatter 의 효과 줄만(`EffectOnly` — 옛 unit 11 rev: 이미 붙은 카드는 「언제」보다 「무엇이 달라지나」).
    [DisallowMultipleComponent]
    public sealed class CoreSelectionPanel : MonoBehaviour
    {
        /// <summary>손패(5)·항아리 독(7) 위 — 옛 `DcInspectPanelView.PanelSortingOrder`.</summary>
        private const int PanelSortingOrder = 9;

        [SerializeField] private BattleDriver _driver;

        [Header("자리")]
        [Tooltip("패널 폭(px, 1920×1080 기준).")]
        [SerializeField, Min(120f)] private float _width = 320f;
        [Tooltip("화면 왼쪽에서 띄우는 거리. 옛 패널과 같이 **좌측 고정**이다.")]
        [SerializeField] private Vector2 _anchoredPos = new Vector2(24f, 0f);

        [Header("부착 카드 줄 (unit 7c — 옛 DcInspectPanelView)")]
        [SerializeField, Min(24f)] private float _attachArtHeight = 78f;
        [SerializeField, Min(0)] private int _descMaxLines = 2;
        [SerializeField] private DefenderCatalog _defenderCatalog;

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

        private sealed class AttachRow
        {
            public RectTransform Root;
            public Image Art;
            public TextMeshProUGUI Name;
            public TextMeshProUGUI Kind;
            public TextMeshProUGUI Desc;
        }

        private const float BaseHeight = 268f;
        // 스탯 셋이 끝나는 자리(-78 − 2×34 − 30 ≈ -176)보다 조금 아래 — 섹션이 그 사이에 끼고 액션 슬롯은 패널 바닥에 남는다.
        private const float SectionTop = 186f;
        private readonly Dictionary<int, List<(int handle, int cardIndex)>> _cardsByHost =
            new Dictionary<int, List<(int, int)>>();
        private readonly List<AttachRow> _attachRows = new List<AttachRow>(3);
        private RectTransform _attachSection;
        private TextMeshProUGUI _attachLabel;
        private SimEntityId _shownHost = SimEntityId.None;

        /// <summary>지금 보여 주는 부착 카드 줄 수(테스트 — 「사건 1 → 줄 1」).</summary>
        public int AttachRowCount
        {
            get
            {
                int n = 0;
                for (int i = 0; i < _attachRows.Count; i++) if (_attachRows[i].Root.gameObject.activeSelf) n++;
                return IsVisible && _attachSection != null && _attachSection.gameObject.activeSelf ? n : 0;
            }
        }

        /// <summary>그 숙주에 붙은 카드 줄(부착 순). 테스트·진단.</summary>
        public int AttachedCountOf(SimEntityId host)
            => _cardsByHost.TryGetValue(host.Value, out var l) ? l.Count : 0;

        private void OnEnable()
        {
            if (_driver != null) _driver.Subscribe(ViewOrder.Hand, OnCoreEvent);
        }

        private void OnDisable()
        {
            if (_driver != null) _driver.Unsubscribe(OnCoreEvent);
            _cardsByHost.Clear();
        }

        private void OnCoreEvent(CoreEvent e)
        {
            switch (e.Kind)
            {
                case CoreEventKind.MatchStarted:
                    _cardsByHost.Clear();
                    break;
                case CoreEventKind.CardAttached:
                {
                    if (!_cardsByHost.TryGetValue(e.A.Value, out var list))
                        _cardsByHost[e.A.Value] = list = new List<(int, int)>(3);
                    list.Add(((int)e.Amount, e.DefIndex));
                    list.Sort((a, b) => a.handle.CompareTo(b.handle));
                    if (e.A == _shownHost) RebuildAttachRows();
                    break;
                }
                case CoreEventKind.CardDetached:
                {
                    if (_cardsByHost.TryGetValue(e.A.Value, out var list))
                    {
                        list.RemoveAll(x => x.handle == (int)e.Amount);
                        if (list.Count == 0) _cardsByHost.Remove(e.A.Value);
                    }
                    if (e.A == _shownHost) RebuildAttachRows();
                    break;
                }
                // 숙주가 사라지면 줄도 간다(카드 사건이 먼저 오지만 — 유령 방지).
                case CoreEventKind.UnitDestroyed:
                    _cardsByHost.Remove(e.A.Value);
                    break;
            }
        }

        /// <summary>그 유닛의 부착 카드 줄을 띄운다(`Show` 뒤에 부른다).</summary>
        public void ShowAttachedCardsOf(SimEntityId host)
        {
            _shownHost = host;
            RebuildAttachRows();
        }

        private void RebuildAttachRows()
        {
            if (!_built) return;
            _cardsByHost.TryGetValue(_shownHost.Value, out var list);
            int count = list != null ? list.Count : 0;
            _attachSection.gameObject.SetActive(count > 0);
            while (_attachRows.Count < count) _attachRows.Add(BuildAttachRow(_attachRows.Count));
            float inner = _width - 24f;
            float artW = _attachArtHeight * (2f / 3f);
            float y = 34f;
            var assets = _driver != null ? _driver.ViewAssets : null;
            var def = _driver != null ? _driver.Definition : null;
            for (int i = 0; i < _attachRows.Count; i++)
            {
                var row = _attachRows[i];
                bool used = i < count;
                row.Root.gameObject.SetActive(used);
                if (!used) continue;
                int cardIndex = list[i].cardIndex;
                var card = assets != null ? assets.Card(cardIndex) : null;
                bool isSquad = card != null && card.type == CardType.Squad;
                row.Name.text = card != null && !string.IsNullOrEmpty(card.displayName) ? card.displayName
                              : (def != null && cardIndex >= 0 && cardIndex < def.Cards.Length ? def.Cards[cardIndex].Id : "");
                int cost = def != null && cardIndex >= 0 && cardIndex < def.Cards.Length ? def.Cards[cardIndex].Cost : 0;
                row.Kind.text = (isSquad ? "스쿼드" : "유닛") + "  ·  " + cost;
                row.Desc.text = card != null
                    ? Somnia.Battle.UI.DreamcatcherCardText.EffectOnly(card,
                        _defenderCatalog != null ? _defenderCatalog.DisplayNameOf : (System.Func<string, string>)null)
                    : "";
                row.Desc.maxVisibleLines = _descMaxLines > 0 ? _descMaxLines : 99999;
                row.Art.sprite = card != null ? card.art : null;
                row.Art.enabled = row.Art.sprite != null;

                float tx = 8f + artW + 12f, tw = inner - tx - 10f;
                float descH = string.IsNullOrEmpty(row.Desc.text) ? 0f : row.Desc.GetPreferredValues(row.Desc.text, tw, 0f).y;
                if (descH > 0f && _descMaxLines > 0) descH = Mathf.Min(descH, _descMaxLines * row.Desc.fontSize * 1.3f);
                float rowH = Mathf.Max(_attachArtHeight + 12f, 12f + 28f + (descH > 0f ? descH + 4f : 0f) + 10f);
                row.Root.anchoredPosition = new Vector2(0f, -y);
                row.Root.sizeDelta = new Vector2(inner, rowH);
                ((RectTransform)row.Art.transform).anchoredPosition = new Vector2(8f, -6f);
                ((RectTransform)row.Art.transform).sizeDelta = new Vector2(artW, _attachArtHeight);
                float kindW = tw * 0.34f;
                H(row.Name).anchoredPosition = new Vector2(tx, -12f);
                H(row.Name).sizeDelta = new Vector2(tw - kindW - 6f, 28f);
                H(row.Kind).anchoredPosition = new Vector2(tx + tw - kindW, -12f);
                H(row.Kind).sizeDelta = new Vector2(kindW, 28f);
                H(row.Desc).anchoredPosition = new Vector2(tx, -44f);
                H(row.Desc).sizeDelta = new Vector2(tw, Mathf.Max(0f, descH));
                y += rowH + 6f;
            }
            _attachLabel.text = "부착 드림캐쳐 " + count;
            float sectionH = count > 0 ? y : 0f;
            _attachSection.sizeDelta = new Vector2(inner, sectionH);
            _root.sizeDelta = new Vector2(_width, BaseHeight + (count > 0 ? sectionH + 8f : 0f));
        }

        private AttachRow BuildAttachRow(int i)
        {
            var root = CoreHudUi.Rect("Attach" + i, _attachSection, new Vector2(0f, 1f), new Vector2(0f, 1f),
                                      Vector2.zero, new Vector2(_width - 24f, _attachArtHeight + 12f));
            CoreHudUi.Fill("Bg", root, new Color(1f, 1f, 1f, 0.05f));
            var art = CoreHudUi.Rect("Art", root, new Vector2(0f, 1f), new Vector2(0f, 1f), Vector2.zero,
                                     new Vector2(_attachArtHeight * (2f / 3f), _attachArtHeight)).gameObject.AddComponent<Image>();
            art.preserveAspect = true;
            art.raycastTarget = false;
            TextMeshProUGUI Text(string n, float size, Color c, TextAlignmentOptions a)
            {
                var host = CoreHudUi.Rect(n + "Row", root, new Vector2(0f, 1f), new Vector2(0f, 1f), Vector2.zero, new Vector2(10f, 10f));
                var t = CoreHudUi.Label(n, host, "", size, c, a);
                return t;
            }
            var row = new AttachRow
            {
                Root = root,
                Art = art,
                Name = Text("Name", 22f, CoreHudUi.Ink, TextAlignmentOptions.TopLeft),
                Kind = Text("Kind", 18f, CoreHudUi.Accent, TextAlignmentOptions.TopRight),
                Desc = Text("Desc", 18f, CoreHudUi.InkDim, TextAlignmentOptions.TopLeft),
            };
            row.Desc.textWrappingMode = TextWrappingModes.Normal;
            row.Desc.overflowMode = TextOverflowModes.Ellipsis;
            // 라벨 헬퍼는 부모를 늘인다 — 행 레이아웃이 위치·폭을 직접 민다(부모 RT 를 쓴다).
            row.Name = Reparent(row.Name); row.Kind = Reparent(row.Kind); row.Desc = Reparent(row.Desc);
            return row;
        }

        private static RectTransform H(TextMeshProUGUI t) => (RectTransform)t.transform.parent;

        // 라벨이 든 호스트 RT 자체를 줄 좌상단 기준으로 쓴다(`CoreHudUi.Label` 은 호스트를 채운다).
        private static TextMeshProUGUI Reparent(TextMeshProUGUI t)
        {
            var host = (RectTransform)t.transform.parent;
            host.pivot = new Vector2(0f, 1f);
            return t;
        }
        private (bool enabled, string label)? _actionState;
        private bool _built;

        /// <summary>지금 떠 있나. 입력이 「빈 곳 탭 = 닫기」를 판단하는 창구.</summary>
        public bool IsVisible => _built && _root != null && _root.gameObject.activeSelf;

        /// <summary>액션 버튼이 눌릴 수 있나. 테스트가 묻는다.</summary>
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
            _shownHost = SimEntityId.None;
            _root.gameObject.SetActive(false);
        }

        // ── 조립 ─────────────────────────────────────────────────────────────

        private void SetRow(int index, string valueText, float baseValue, float effValue, string fmt)
        {
            var row = _rows[index];
            row.Value.text = valueText;

            // **변화가 없으면 칩을 그리지 않는다.** 항상 ▲0 을 띄우면 노이즈다. unit 6c 부터
            // `ReadoutOf` 가 실효 값을 싣는다 — 버프·디버프가 걸리면 여기서 칩이 선다.
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

            // 플레이 3차 — 선택이 손패를 연다(7c). 손패 캔버스(order 5)의 전화면 바깥 탭 캐처가 이 패널 **위**에 있으면
            // 퇴근 버튼 탭이 캐처로 가서 「선택 닫기」가 된다. 옛 패널도 같은 이유로 손패 위였다
            // (`DcInspectPanelView.cs:26` — `PanelSortingOrder = 9`, 항아리 독 7 위 · 메뉴 팝업 아래).
            var canvas = CoreHudUi.EnsureCanvas(gameObject, PanelSortingOrder);
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

            // 부착 카드 섹션 — 스탯·액션 **아래**(패널이 그만큼 아래로 자란다 — 옛 섹션 배치).
            _attachSection = CoreHudUi.Rect("AttachSection", _root, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                                            new Vector2(0f, -SectionTop), new Vector2(_width - 24f, 0f));
            _attachLabel = CoreHudUi.Label("AttachLabel",
                CoreHudUi.Rect("AttachLabelRow", _attachSection, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                               new Vector2(0f, -4f), new Vector2(_width - 24f, 26f)),
                "", 20f, CoreHudUi.InkDim, TextAlignmentOptions.Left);
            _attachSection.gameObject.SetActive(false);

            _root.gameObject.SetActive(false);
        }

        // ── 읽기 모델 ────────────────────────────────────────────────────────

        /// <summary>
        /// 코어의 개체 + 정의표 → 화면이 읽는 값. **실효 = 정의표 × 살아 있는 모디파이어**다(unit 6c 개통 —
        /// 5b 는 생산자가 없어 기본값과 같았다).
        ///
        /// ⚠ **재곱 금지.** 최대 체력은 `unit.MaxHealth` 를 **그대로** 읽는다 — 최대체력 배율이 이미
        /// 반영돼 있다(6a 구현 14 · `MaxHealthScale`). 여기서 `MaxHealthMul` 을 한 번 더 곱하면 칩이
        /// 두 배로 거짓말한다.
        /// ⚠ **조건부 배율은 뺀다** — 「군중 제어에 걸린 적에게」(`DamageVsCcMul`) · 최전방 · 바운스 감쇠는
        /// 대상·시점에 달린 값이라, 한 숫자로 접으면 거짓 표시가 된다. 그래서 공격력은 무조건 배율
        /// `DamageMul` 만, 공격 속도는 `AttackSpeedMul` 만 곱한다(선딜 바닥은 보이지 않는다 — 초당 횟수는
        /// 간격 기준이다, 옛 규약).
        /// 배율 결합은 코어가 이미 했다(`ModifierSet.Effective`) — 여기는 **읽고 곱할 뿐**이다.
        /// </summary>
        public static UnitStatReadout ReadoutOf(Unit unit, in UnitDef def)
        {
            float damage = 0f;
            var outputs = def.Attack.Outputs;
            if (outputs != null)
                for (int i = 0; i < outputs.Length; i++)
                    if (outputs[i].Kind == Somnia.Battle.BattleCore.AttackOutputKind.Damage) { damage = outputs[i].Magnitude; break; }

            // 큰 숫자 = 빠름이 직관적이라 쿨다운 초가 아니라 초당 발사 횟수로 낸다(옛 규약).
            float rate = def.AttackCooldown > 0f ? 1f / def.AttackCooldown : 0f;

            var eff = unit != null ? unit.Modifiers.Effective : Somnia.Battle.BattleCore.Effects.EffectiveStats.Identity;
            float speed = eff.AttackSpeedMul > 0f ? eff.AttackSpeedMul : 1f;   // 코어 `IntervalMul` 과 같은 접기

            return new UnitStatReadout
            {
                hp = unit != null ? unit.Health : 0f,
                hpMax = unit != null && unit.MaxHealth > 0f ? unit.MaxHealth : def.Health,
                hpMaxBase = def.Health,
                damage = damage * eff.DamageMul,
                damageBase = damage,
                attackRate = rate * speed,
                attackRateBase = rate,
            };
        }
    }
}
