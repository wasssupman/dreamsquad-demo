using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Somnia.Battle.BattleCore;
using Somnia.Battle.Data;

namespace Somnia.Battle.BattleCoreUnity.Hud
{
    // battle-core-rebuild unit 5b — **트레이.** 옛 `DefenderSelector`(1,281줄)의 후계다.
    //
    // 옛것과의 결정적 차이: **자기 셈이 없다.** 옛 트레이는 「판 위에 몇이지(소진)」·「쿨이
    // 남았나」·「살 수 있나」를 각자 세어 도색했고, 그래서 배치 판정과 답이 갈릴 수 있었다.
    // 여기서는 그 답을 코어에 **한 번 묻는다**(`PlacementService.SlotBlock`) — 도색과 드롭
    // 거절이 같은 함수에서 나오므로 「초록인데 놓으면 거절」이 구조적으로 불가능하다.
    //
    // 우선순위 「**소진 &gt; 쿨타임 &gt; 코스트**」도 여기 있지 않다. 그 순서는 `SlotBlock` 의
    // 것이고 이 파일은 돌아온 사유를 색으로 옮길 뿐이다 — 순서를 뷰가 들면 그것이 두 번째 자다.
    [DisallowMultipleComponent]
    public sealed class CoreDefenderTray : MonoBehaviour
    {
        [SerializeField] private BattleDriver _driver;

        [Header("칸")]
        [SerializeField, Min(40f)] private float _slotSize = 132f;
        [SerializeField, Min(0f)] private float _slotGap = 12f;
        [SerializeField] private Vector2 _anchoredPos = new Vector2(0f, 26f);

        [Header("거절 표시")]
        [Tooltip("거절 사유 문구가 떠 있는 시간(초).")]
        [SerializeField, Min(0.1f)] private float _rejectHoldSeconds = 1.4f;

        private sealed class Slot
        {
            public int DefIndex;
            public RectTransform Root;
            public Image Frame;
            public Image Portrait;
            public Image CooldownFill;
            public TextMeshProUGUI CooldownText;
            public TextMeshProUGUI Cost;
            public TextMeshProUGUI Name;
            public TextMeshProUGUI Block;
        }

        private readonly List<Slot> _slots = new List<Slot>(8);
        private RectTransform _row;
        private TextMeshProUGUI _reject;
        private float _rejectUntil;
        private int _draggingDefIndex = -1;
        private bool _built;

        /// <summary>드래그 입력이 「이 칸을 집었다」고 알리는 자리. -1 = 안 집었다.</summary>
        public int DraggingDefIndex
        {
            get => _draggingDefIndex;
            set => _draggingDefIndex = value;
        }

        public int SlotCount => _slots.Count;

        /// <summary>
        /// unit 7c — 칸 줄. 손패가 열릴 때 **이 줄이 접히고** 그 자리에 부채가 선다(옛 「트레이 ↔ 손패 뒤집기」 — 둘은 배타다).
        /// 접힌 동안은 칸 픽이 안 된다(`TryPickSlot`).
        /// </summary>
        public RectTransform StripRect => _row;

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
            // 거절은 **판 전체에 알리는 사건**이고 receipt 와 별개다. 트레이가 이것을 읽는
            // 이유: 드롭이 아닌 경로(자동 배치·디버그)로 거절이 나도 화면이 말해야 한다.
            if (e.Kind != CoreEventKind.PlacementRejected) return;
            ShowReject((RejectReason)e.Arg);
        }

        private void Update()
        {
            if (_driver == null || !_driver.Running) return;
            if (!_built) Build();
            Paint();
        }

        // ── 조립 ─────────────────────────────────────────────────────────────

        private void Build()
        {
            var def = _driver.Definition;
            if (def == null) return;

            CoreHudUi.EnsureCanvas(gameObject);
            _built = true;

            _row = CoreHudUi.Rect("Row", transform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                                  _anchoredPos, new Vector2(10f, _slotSize));

            var placement = _driver.Match.Placement;
            for (int i = 0; i < def.Units.Length; i++)
            {
                // 로스터가 정본이다 — 전투 빌더가 카탈로그 밖 에셋(순찰 소환물)을 정의표에
                // 편입하므로 「표에 있다 = 놓을 수 있다」가 언제나 참이 아니다.
                if (!placement.InRoster(i)) continue;
                _slots.Add(BuildSlot(i, def.Units[i]));
            }

            float width = _slots.Count * _slotSize + Mathf.Max(0, _slots.Count - 1) * _slotGap;
            _row.sizeDelta = new Vector2(width, _slotSize);
            for (int i = 0; i < _slots.Count; i++)
            {
                float x = -width * 0.5f + _slotSize * 0.5f + i * (_slotSize + _slotGap);
                _slots[i].Root.anchoredPosition = new Vector2(x, 0f);
            }

            _reject = CoreHudUi.Label("Reject", CoreHudUi.Rect(
                    "RejectRow", transform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                    new Vector2(0f, _anchoredPos.y + _slotSize + 16f), new Vector2(900f, 44f)),
                "", 34f, CoreHudUi.Bad);
            _reject.enabled = false;
        }

        private Slot BuildSlot(int defIndex, in UnitDef unit)
        {
            var root = CoreHudUi.Rect($"Slot_{defIndex}", _row, new Vector2(0.5f, 0.5f),
                                      new Vector2(0.5f, 0.5f), Vector2.zero,
                                      new Vector2(_slotSize, _slotSize));

            var slot = new Slot { DefIndex = defIndex, Root = root };
            slot.Frame = CoreHudUi.Fill("Frame", root, CoreHudUi.Panel);

            var asset = AssetOf(defIndex);
            var portraitRect = CoreHudUi.Rect("Portrait", root, new Vector2(0.5f, 0.5f),
                                              new Vector2(0.5f, 0.5f), new Vector2(0f, 8f),
                                              new Vector2(_slotSize - 22f, _slotSize - 42f));
            slot.Portrait = portraitRect.gameObject.AddComponent<Image>();
            slot.Portrait.raycastTarget = false;
            slot.Portrait.preserveAspect = true;
            slot.Portrait.sprite = asset != null ? asset.portrait : null;
            // 초상이 없는 저작도 **칸은 선다** — 안 세우면 「그 유닛이 사라졌다」로 읽힌다.
            slot.Portrait.color = slot.Portrait.sprite != null ? Color.white : new Color(1f, 1f, 1f, 0.12f);

            // 쿨타임은 칸을 **아래에서 덮는다.** 옛 트레이의 액체 셰이더는 안 옮겼다 —
            // 그 셰이더는 코스트 물통과 공유하는 자산이고, 이 unit 의 질문(「배치가 도나」)에
            // 답하는 데 필요하지 않다.
            var cdRect = CoreHudUi.Rect("Cooldown", root, new Vector2(0.5f, 0.5f),
                                        new Vector2(0.5f, 0.5f), Vector2.zero,
                                        new Vector2(_slotSize, _slotSize));
            slot.CooldownFill = cdRect.gameObject.AddComponent<Image>();
            slot.CooldownFill.raycastTarget = false;
            slot.CooldownFill.color = new Color(0.1f, 0.14f, 0.24f, 0.72f);
            slot.CooldownFill.type = Image.Type.Filled;
            slot.CooldownFill.fillMethod = Image.FillMethod.Vertical;
            slot.CooldownFill.fillOrigin = (int)Image.OriginVertical.Bottom;
            slot.CooldownFill.fillAmount = 0f;

            // 덮개만으로는 **얼마나 남았는지**를 못 읽는다(사용자 플레이 2차). 옛 트레이
            // (`defender-placement-cooldown` 2)와 같이 남은 초를 숫자로 얹는다 — 덮개는
            // 「얼마나 찼나」, 숫자는 「몇 초 뒤」다. 덮개 **뒤에** 만들어야 그 위에 그려진다.
            // 자리는 거절 문구(`Block`)와 같다 — 대기 중에는 그쪽이 빈 문자열이라(덮개가 이미
            // 말한다) 둘이 겹치는 프레임이 없다.
            slot.CooldownText = CoreHudUi.Label("CooldownText",
                CoreHudUi.Rect("CooldownRow", root, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                               Vector2.zero, new Vector2(_slotSize, 34f)),
                "", 28f, CoreHudUi.Ink);
            slot.CooldownText.enabled = false;

            slot.Name = CoreHudUi.Label("Name",
                CoreHudUi.Rect("NameRow", root, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                               new Vector2(0f, 4f), new Vector2(_slotSize, 26f)),
                asset != null && !string.IsNullOrEmpty(asset.displayName) ? asset.displayName : unit.Id,
                20f, CoreHudUi.InkDim);

            slot.Cost = CoreHudUi.Label("Cost",
                CoreHudUi.Rect("CostChip", root, new Vector2(1f, 1f), new Vector2(1f, 1f),
                               new Vector2(-6f, -6f), new Vector2(46f, 34f)),
                unit.Cost.ToString(), 28f, CoreHudUi.Accent, TextAlignmentOptions.Right);

            slot.Block = CoreHudUi.Label("Block",
                CoreHudUi.Rect("BlockRow", root, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                               Vector2.zero, new Vector2(_slotSize, 34f)),
                "", 24f, CoreHudUi.Ink);
            slot.Block.enabled = false;

            return slot;
        }

        private DefenderUnitData AssetOf(int defIndex)
        {
            var assets = _driver.DefenderAssets;
            return assets != null && defIndex >= 0 && defIndex < assets.Count ? assets[defIndex] : null;
        }

        // ── 도색 ─────────────────────────────────────────────────────────────

        private void Paint()
        {
            var placement = _driver.Match.Placement;
            for (int i = 0; i < _slots.Count; i++)
            {
                var slot = _slots[i];
                // **여기가 유일한 질문이다.** 소진·쿨타임·코스트를 따로 세지 않는다.
                var block = placement.SlotBlock(slot.DefIndex);

                slot.Frame.color = FrameColorOf(block, slot.DefIndex == _draggingDefIndex);
                slot.CooldownFill.fillAmount = placement.CooldownFraction(slot.DefIndex);

                // 남은 초. **코어가 세고 화면은 옮겨 적기만 한다** — 뷰가 자기 타이머를 들면
                // 슬로모·정지에서 숫자와 판정이 갈린다. 올림이라 「1」이 뜬 동안은 아직 못 놓는다.
                float remain = placement.CooldownRemaining(slot.DefIndex);
                bool onCooldown = remain > 0f;
                slot.CooldownText.enabled = onCooldown;
                if (onCooldown)
                {
                    string secs = Mathf.CeilToInt(remain).ToString();
                    if (slot.CooldownText.text != secs) slot.CooldownText.text = secs;
                }

                string text = ShortTextOf(block);
                slot.Block.text = text;
                slot.Block.enabled = text.Length > 0;
                slot.Portrait.color = block == RejectReason.None || slot.Portrait.sprite == null
                    ? (slot.Portrait.sprite != null ? Color.white : new Color(1f, 1f, 1f, 0.12f))
                    : new Color(0.55f, 0.55f, 0.6f, 1f);
            }

            if (_reject != null && _reject.enabled && Time.unscaledTime >= _rejectUntil)
                _reject.enabled = false;
        }

        private static Color FrameColorOf(RejectReason block, bool dragging)
        {
            if (dragging) return new Color(0.16f, 0.3f, 0.2f, 0.9f);
            switch (block)
            {
                case RejectReason.None: return CoreHudUi.Panel;
                case RejectReason.LimitReached: return new Color(0.35f, 0.24f, 0.05f, 0.82f);
                case RejectReason.OnCooldown: return new Color(0.1f, 0.14f, 0.24f, 0.82f);
                case RejectReason.InsufficientCost: return new Color(0.28f, 0.1f, 0.1f, 0.82f);
                default: return new Color(0.12f, 0.12f, 0.14f, 0.85f);
            }
        }

        // 칸 안에 들어가는 아주 짧은 말. 긴 문장은 아래 거절 줄이 맡는다.
        private static string ShortTextOf(RejectReason block)
        {
            switch (block)
            {
                case RejectReason.LimitReached: return "출전 중";
                case RejectReason.InsufficientCost: return "";     // 코스트 칩이 이미 말한다
                case RejectReason.OnCooldown: return "";           // 차오르는 덮개가 이미 말한다
                case RejectReason.None: return "";
                default: return "";
            }
        }

        /// <summary>드롭이 거절됐다. 드래그 입력이 receipt 를 받고 부른다.</summary>
        public void ShowReject(RejectReason reason)
        {
            if (_reject == null) return;
            _reject.text = TextOf(reason);
            _reject.enabled = true;
            _rejectUntil = Time.unscaledTime + _rejectHoldSeconds;
        }

        /// <summary>
        /// 사유 → 사람 말. **순서를 담지 않는다** — 어떤 사유가 먼저인지는 코어가 정한다.
        /// 여기 없는 사유는 열거명을 그대로 보여 준다(조용히 빈 줄을 내지 않는다).
        /// </summary>
        public static string TextOf(RejectReason reason)
        {
            switch (reason)
            {
                case RejectReason.None: return "";
                case RejectReason.LimitReached: return "이미 출전 중이다";
                case RejectReason.OnCooldown: return "재배치 대기 중";
                case RejectReason.InsufficientCost: return "코스트가 모자란다";
                case RejectReason.NotBuildable: return "여기엔 못 세운다";
                case RejectReason.Occupied: return "자리가 찼다";
                case RejectReason.OutOfBounds: return "판 밖이다";
                case RejectReason.NotInPickedPool: return "이번 판에 못 쓴다";
                case RejectReason.NotRunningOrPlacementClosed: return "지금은 배치할 수 없다";
                case RejectReason.MatchEnded: return "판이 끝났다";
                default: return reason.ToString();
            }
        }

        // ── 드래그 입력이 묻는 것 ────────────────────────────────────────────

        /// <summary>그 화면 좌표가 어느 칸인가. 칸 밖이면 false.</summary>
        public bool TryPickSlot(Vector2 screenPos, Camera uiCamera, out int defIndex)
        {
            defIndex = -1;
            // 손패가 칸 줄을 접어 둔 동안은 그 자리에 칸이 없다 — 보이지 않는 칸을 집으면 부채 밑에서 배치가 시작된다.
            if (_row == null || !_row.gameObject.activeInHierarchy) return false;
            for (int i = 0; i < _slots.Count; i++)
            {
                if (!RectTransformUtility.RectangleContainsScreenPoint(_slots[i].Root, screenPos, uiCamera))
                    continue;
                defIndex = _slots[i].DefIndex;
                return true;
            }
            return false;
        }

        /// <summary>그 칸의 화면 중심. 거절된 드롭이 「돌아가는」 목적지다.</summary>
        public bool TryGetSlotScreenCenter(int defIndex, Camera uiCamera, out Vector2 screenPos)
        {
            screenPos = default;
            for (int i = 0; i < _slots.Count; i++)
            {
                if (_slots[i].DefIndex != defIndex) continue;
                screenPos = RectTransformUtility.WorldToScreenPoint(uiCamera, _slots[i].Root.position);
                return true;
            }
            return false;
        }
    }
}
