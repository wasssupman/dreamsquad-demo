using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Wassup.BattleCore;
using Wassup.Data;
using Wassup.Data.BattleView;
using Wassup.Presentation;

namespace Wassup.BattleCoreUnity.View
{
    // battle-core-rebuild unit 5a — 유닛 머리 위 체력 바. 옛 `UnitOverheadUiLayer` 의 후계다.
    //
    // 옛 것은 브리지가 매 프레임 `BeginFrame → SetUnit ×N → EndFrame` 를 불러 줬다. 새 것은
    // **자기가 읽는다**(계약 12) — 코어 읽기 모델(체력·진영)과 유닛 뷰(화면 사각형)만 본다.
    //
    // 화면에 그리는 일은 옛 `UnitOverheadView` 가 그대로 한다(UI 뿐이라 키 타입이 없다).
    // 그래서 여기서 복사한 것은 **창(프레임 경계)과 풀링**이지 바 그리기가 아니다.
    //
    // unit 7c — **부착 카드 줄**(아이콘만). 옛 `UnitOverheadUiLayer.RebuildCards` 가 손패 컨트롤러의 `AttachmentsChanged` 로
    // 다시 모으던 것을, 이 층이 **부착 사건**(`CardAttached`/`CardDetached`)을 직접 들어 숙주별 목록으로 든다(계약 12).
    // 순서 = 부착 번호(묶음 핸들 — 판 수명 단조). 발동 펄스(`TriggerFired` × **카드 규칙 줄**)는 옛 `PulseCards` 그대로다
    // (UI 펄스는 코얼레스하지 않는다 — 뷰가 타이머 재시작으로 자체 흡수한다).
    //
    // unit 6c 가 개통한 것 셋(5a 가 0/`null` 로 흘린 자리):
    //   · **실드 비율** = 실드 합 / 최대 체력. 정규화(체력+실드 > 100% 압축)는 뷰(`UnitOverheadView`)가 한다.
    //     진영 무관이다 — 옛 브리지는 적 분기에 리터럴 0 을 넘겨 적 실드가 안 그려졌다(boss-mamemo 정정).
    //   · **스택 아이콘** — 피로(스택 슬롯마다 한 줄) · 열기(개체당 한 줄). 옛 매핑 그대로 이 둘만 아이콘화.
    //   · **길막 게이지** — 설치물 저작 `overheadHeight` 가 0 보다 클 때만(0 = 바 없음, 옛 옵트인).
    //     플레이어가 놓은 물건이라 방어유닛 스킨이다(옛 판단).
    // 셋 다 **읽기 모델**을 매 프레임 읽는다(체력 바와 같은 규약) — 사건이 아니라 연속값이다.
    [DisallowMultipleComponent]
    public sealed class CoreUnitOverheadUiLayer : MonoBehaviour
    {
        [SerializeField] private BattleDriver _driver;
        [SerializeField] private CoreUnitViewPool _units;
        [SerializeField] private UnitOverheadUiStyle _style;
        [SerializeField] private CharacterViewConfig _characterView;
        [SerializeField] private int _sortingOrder = 3;

        [Tooltip("스택 아이콘 등록부의 주인(unit 6c). 비어 있으면 아이콘을 생략한다.")]
        [SerializeField] private StatusFxConfig _statusFx;

        [Tooltip("길막 게이지의 저작 높이를 묻는 풀(unit 6c). 비어 있으면 길막 바가 안 뜬다.")]
        [SerializeField] private CoreHazardViewPool _hazards;

        private readonly List<OverheadStackEntry> _stackScratch = new List<OverheadStackEntry>(2);

        private readonly Dictionary<int, UnitOverheadView> _active = new Dictionary<int, UnitOverheadView>();
        private readonly Queue<UnitOverheadView> _idle = new Queue<UnitOverheadView>();
        private readonly HashSet<int> _seen = new HashSet<int>();
        private readonly List<int> _toHide = new List<int>();

        private Canvas _canvas;
        private RectTransform _canvasRect;
        private UnitOverheadSpriteSet _sprites;
        private bool _missingStyleLogged;

        public int ActiveCount => _active.Count;

        private readonly Dictionary<int, List<(int handle, DreamcatcherCard card)>> _cardsByHost =
            new Dictionary<int, List<(int, DreamcatcherCard)>>();
        private readonly Dictionary<int, List<DreamcatcherCard>> _cardViews = new Dictionary<int, List<DreamcatcherCard>>();

        /// <summary>그 숙주의 오버헤드 카드 아이콘 수(테스트 — 「부착 1 → 아이콘 1」).</summary>
        public int CardIconCountOf(SimEntityId host)
            => _cardViews.TryGetValue(host.Value, out var l) ? l.Count : 0;

        private void OnEnable()
        {
            if (_driver != null) _driver.Subscribe(ViewOrder.Overhead, OnCoreEvent);
        }

        private void OnCoreEvent(CoreEvent e)
        {
            switch (e.Kind)
            {
                case CoreEventKind.MatchStarted:
                    _cardsByHost.Clear();
                    _cardViews.Clear();
                    break;
                case CoreEventKind.CardAttached:
                {
                    var card = _driver.ViewAssets.Card(e.DefIndex);
                    if (!_cardsByHost.TryGetValue(e.A.Value, out var list))
                        _cardsByHost[e.A.Value] = list = new List<(int, DreamcatcherCard)>(3);
                    list.Add(((int)e.Amount, card));
                    list.Sort((a, b) => a.handle.CompareTo(b.handle));
                    RebuildCardView(e.A.Value);
                    break;
                }
                case CoreEventKind.CardDetached:
                    if (_cardsByHost.TryGetValue(e.A.Value, out var l))
                    {
                        l.RemoveAll(x => x.handle == (int)e.Amount);
                        if (l.Count == 0) _cardsByHost.Remove(e.A.Value);
                    }
                    RebuildCardView(e.A.Value);
                    break;
                case CoreEventKind.UnitDestroyed:
                    _cardsByHost.Remove(e.A.Value);
                    _cardViews.Remove(e.A.Value);
                    break;
                case CoreEventKind.TriggerFired:
                {
                    // 카드 규칙이 발동했다 — 그 숙주의 카드 줄을 튕긴다. 유닛 저작 스킬(배치 스킬 등)은 카드 보유 줄이 아니다(U16).
                    if (!_driver.Definition.IsCardRow(e.DefIndex)) break;
                    if (_active.TryGetValue(e.A.Value, out var view) && view != null) view.PulseCards();
                    break;
                }
            }
        }

        private void RebuildCardView(int host)
        {
            if (!_cardsByHost.TryGetValue(host, out var list) || list.Count == 0) { _cardViews.Remove(host); return; }
            if (!_cardViews.TryGetValue(host, out var cards)) _cardViews[host] = cards = new List<DreamcatcherCard>(3);
            cards.Clear();
            for (int i = 0; i < list.Count; i++) if (list[i].card != null) cards.Add(list[i].card);
        }

        private void OnDisable()
        {
            if (_driver != null) _driver.Unsubscribe(OnCoreEvent);
            _cardsByHost.Clear();
            _cardViews.Clear();
            Clear();
            _sprites?.Dispose();
            _sprites = null;
        }

        private void LateUpdate()
        {
            if (_driver == null || !_driver.Running || _units == null) return;
            if (_characterView != null
                && _characterView.HealthPresentationMode != UnitHealthPresentationMode.UnifiedOverhead) return;
            if (!EnsureCanvas()) return;

            var cam = Camera.main;
            if (cam == null) return;

            _seen.Clear();
            var units = _driver.Units;
            for (int i = 0; i < units.Count; i++)
            {
                var u = units[i];
                if (u.Dead || u.MaxHealth <= 0f) continue;
                // 거점 바는 골 안정도 게이지라 성격이 다르다 — 5b 의 HUD 몫이다.
                if (u.Kind == UnitKind.Structure) continue;
                if (u.Kind == UnitKind.BlockingHazard) { SetBlocker(cam, u); continue; }
                if (!_units.TryGet(u.Id, out var view) || !view.TryGetScreenRect(cam, out var rect)) continue;

                var anchor = view.transform;
                Vector2 screenAnchor = UnitOverheadLayout.ScreenAnchor(
                    cam.WorldToScreenPoint(anchor.position).x, rect);
                SetUnit(u.Id, ((int)u.Faction & Wassup.Skills.Factions.AnyDefender) != 0,
                        Mathf.Clamp01(u.Health / u.MaxHealth),
                        screenAnchor, ProjectTileScreenWidth(cam, anchor.position),
                        ShieldRatioOf(u), GatherStacks(u));
            }
            EndFrame();
        }

        private float ProjectTileScreenWidth(Camera cam, Vector3 anchor)
        {
            if (cam == null) return 1f;
            Vector3 half = Vector3.right * (_driver.TileSize * 0.5f);
            Vector3 a = cam.WorldToScreenPoint(anchor - half);
            Vector3 b = cam.WorldToScreenPoint(anchor + half);
            return Vector2.Distance(new Vector2(a.x, a.y), new Vector2(b.x, b.y));
        }

        // 실드 합 / 최대 체력. 합은 슬롯을 더할 뿐이다(FIFO 소모 순서는 코어 규칙이고 여기엔 무관).
        private static float ShieldRatioOf(Unit u)
        {
            if (u.MaxHealth <= 0f || !u.Shield.Any) return 0f;
            float sum = 0f;
            var slots = u.Shield.Slots;
            for (int i = 0; i < slots.Count; i++) sum += slots[i].Value;
            return sum / u.MaxHealth;
        }

        // 옛 `GatherOverheadStacks` 그대로 — 피로는 **스택 슬롯마다** 한 줄(출처가 둘이면 둘), 열기는 개체당 한 줄.
        // 다른 종류(불·얼음·출혈)는 아이콘이 없다(옛 매핑이 피로만 아이콘화 — 나머지는 상태 표식이 말한다).
        private List<OverheadStackEntry> GatherStacks(Unit u)
        {
            _stackScratch.Clear();
            var slots = u.Stacks.Slots;
            for (int i = 0; i < slots.Count; i++)
                if (slots[i].Kind == Wassup.BattleCore.Effects.StackKind.Fatigue && slots[i].Count > 0)
                    _stackScratch.Add(new OverheadStackEntry { kind = OverheadStackKind.Fatigue, count = slots[i].Count });
            if (u.Stacks.Heat > 0)
                _stackScratch.Add(new OverheadStackEntry { kind = OverheadStackKind.Heat, count = u.Stacks.Heat });
            return _stackScratch;
        }

        // 길막 게이지. 높이는 **설치물 자신의 저작값**이다(1칸 배럴 ↔ 3×3 방벽 — 덩치가 다르다).
        private void SetBlocker(Camera cam, Unit u)
        {
            if (_hazards == null) return;
            var so = _hazards.BlockerAuthoringOf(u.Id);
            if (so == null || so.overheadHeight <= 0f) return;   // 0 = 바 없음(옛 옵트인)
            var baseView = (Vector3)Wassup.Core.BoardSpace.ToView(
                new Unity.Mathematics.float3(u.Position.x, 0f, u.Position.z));
            Vector3 baseScreen = cam.WorldToScreenPoint(baseView);
            Vector3 topScreen = cam.WorldToScreenPoint(baseView + Vector3.up * so.overheadHeight);
            SetUnit(u.Id, true, Mathf.Clamp01(u.Health / u.MaxHealth),
                    new Vector2(baseScreen.x, topScreen.y), ProjectTileScreenWidth(cam, baseView),
                    ShieldRatioOf(u), null);
        }

        private void SetUnit(SimEntityId id, bool defender, float healthRatio,
                             Vector2 screenAnchor, float tileScreenWidth,
                             float shieldRatio, IReadOnlyList<OverheadStackEntry> stacks)
        {
            if (float.IsNaN(screenAnchor.x) || float.IsNaN(screenAnchor.y)) return;
            _seen.Add(id.Value);

            bool resetHealth = false;
            if (!_active.TryGetValue(id.Value, out var view) || view == null)
            {
                view = GetView();
                _active[id.Value] = view;
                resetHealth = true;
            }
            RectTransformUtility.ScreenPointToLocalPointInRectangle(_canvasRect, screenAnchor, null, out var local);
            float scale = Mathf.Max(0.001f, _canvas.scaleFactor);
            _cardViews.TryGetValue(id.Value, out var cards);
            view.Show(local, tileScreenWidth / scale,
                      defender ? OverheadBarSkin.Defender : OverheadBarSkin.Enemy,
                      healthRatio, defender ? cards : null, _style, _sprites, resetHealth,
                      shieldRatio: shieldRatio, stacks: stacks,
                      stackIcons: _statusFx != null ? _statusFx.StackIcons : null, barScale: 1f);
        }

        private void EndFrame()
        {
            _toHide.Clear();
            foreach (var kv in _active)
                if (!_seen.Contains(kv.Key)) _toHide.Add(kv.Key);
            for (int i = 0; i < _toHide.Count; i++)
            {
                int key = _toHide[i];
                if (_active.TryGetValue(key, out var view) && view != null)
                {
                    view.Hide();
                    _idle.Enqueue(view);
                }
                _active.Remove(key);
            }
        }

        public void Clear()
        {
            foreach (var kv in _active) if (kv.Value != null) Destroy(kv.Value.gameObject);
            _active.Clear();
            while (_idle.Count > 0)
            {
                var v = _idle.Dequeue();
                if (v != null) Destroy(v.gameObject);
            }
            _seen.Clear();
            if (_canvas != null) Destroy(_canvas.gameObject);
            _canvas = null;
            _canvasRect = null;
        }

        private bool EnsureCanvas()
        {
            if (_style == null)
            {
                if (!_missingStyleLogged)
                {
                    Debug.LogError("[CoreUnitOverheadUiLayer] UnitOverheadUiStyle 미할당 — 오버헤드 UI 스킵.", this);
                    _missingStyleLogged = true;
                }
                return false;
            }
            if (_canvas != null) return true;
            _sprites ??= new UnitOverheadSpriteSet(_style);
            var go = new GameObject("CoreOverheadCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
            go.transform.SetParent(transform, false);
            _canvas = go.GetComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.overrideSorting = true;
            _canvas.sortingOrder = _sortingOrder;
            var scaler = go.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = _style.ReferenceResolution;
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 1f;
            _canvasRect = (RectTransform)go.transform;
            return true;
        }

        private UnitOverheadView GetView()
        {
            while (_idle.Count > 0)
            {
                var pooled = _idle.Dequeue();
                if (pooled != null) return pooled;
            }
            var go = new GameObject("CoreUnitOverhead", typeof(RectTransform));
            go.transform.SetParent(_canvasRect, false);
            return go.AddComponent<UnitOverheadView>();
        }
    }
}
