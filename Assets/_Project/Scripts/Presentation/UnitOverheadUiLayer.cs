using System.Collections.Generic;
using Unity.Entities;
using UnityEngine;
using UnityEngine.UI;
using Wassup.Core;
using Wassup.Data;

namespace Wassup.Presentation
{
    // unit-overhead-ui — ScreenSpaceOverlay 한 장에서 health + defender cards를 함께 배치한다.
    // ECS를 모르며 BattleBridge가 plain ratio/screen rect를 push한다.
    public class UnitOverheadUiLayer : MonoBehaviour
    {
        [SerializeField] private UnitOverheadUiStyle style;
        [SerializeField] private DreamcatcherHandController hand;
        // 확장(unit 8) — 스택 아이콘 레지스트리(kind→sprite). 미할당/미매핑 = 스택 아이콘 생략.
        [SerializeField] private StackIconRegistry stackIcons;
        [SerializeField] private int sortingOrder = 3;

        private readonly Dictionary<Entity, UnitOverheadView> _active = new();
        // three-minute-survival unit 1 — 골 안정도 바(골 인덱스 키). 엔티티가 없는 뷰라
        // _active 와 분리한다 — EndFrame 의 _seen 소거에 걸리면 매 프레임 사라진다.
        private readonly Queue<UnitOverheadView> _idle = new();
        private readonly HashSet<Entity> _seen = new();
        private readonly List<Entity> _toHide = new();
        private readonly List<(Entity host, DreamcatcherCard card)> _attachments = new();
        private readonly Dictionary<Entity, List<DreamcatcherCard>> _cardsByHost = new();
        private Canvas _canvas;
        private RectTransform _canvasRect;
        private UnitOverheadSpriteSet _sprites;
        private bool _missingStyleLogged;

        private void OnEnable()
        {
            if (hand != null) hand.AttachmentsChanged += RebuildCards;
            RebuildCards();
        }

        private void OnDisable()
        {
            if (hand != null) hand.AttachmentsChanged -= RebuildCards;
            Clear();
            _sprites?.Dispose();
            _sprites = null;
        }

        public void BeginFrame()
        {
            if (!EnsureCanvas()) return;
            _seen.Clear();
        }

        // heart-stress-axis unit 1 — `skinOverride` 는 «진영으로 못 고르는 스킨» 을 위한 통로다.
        // 마음은 방어 진영이지만 체력바가 아니라 **차오르는 스트레스 바**를 단다. 기존 호출부
        // 5곳은 인자를 안 넘겨 그대로 동작한다(bool defender → Defender/Enemy).
        public void SetUnit(Entity entity, bool defender, float healthRatio, Vector2 screenAnchor, float tileScreenWidth,
            float shieldRatio = 0f, // shield-guardian-defender unit 2 — 실드합/maxHP (0 = 무실드)
            IReadOnlyList<OverheadStackEntry> stacks = null, // 확장(unit 8) — 스택 이상효과(피로도/열기)
            OverheadBarSkin? skinOverride = null)
        {
            if (!EnsureCanvas() || entity == Entity.Null
                || float.IsNaN(screenAnchor.x) || float.IsNaN(screenAnchor.y)) return;
            _seen.Add(entity);
            bool resetHealth = false;
            if (!_active.TryGetValue(entity, out var view) || view == null)
            {
                view = GetView();
                _active[entity] = view;
                resetHealth = true;
            }
            RectTransformUtility.ScreenPointToLocalPointInRectangle(_canvasRect, screenAnchor, null, out var local);
            float scale = Mathf.Max(0.001f, _canvas.scaleFactor);
            float tileRef = tileScreenWidth / scale;
            _cardsByHost.TryGetValue(entity, out var cards);
            view.Show(local, tileRef,
                skinOverride ?? (defender ? OverheadBarSkin.Defender : OverheadBarSkin.Enemy),
                healthRatio, cards, style, _sprites, resetHealth, shieldRatio, stacks, stackIcons);
        }

        // three-minute-kill-race unit 2 — `SetStability`/`HideStability` 와 전용 풀은 제거했다.
        // 마음의 남은 수치를 바·숫자로 그리지 않는다(게이지 형태 금지) — 그 정보는
        // 프랍의 균열 단계가 갖는다(TilemapMapView.SetGoalCrack).

        public void EndFrame()
        {
            _toHide.Clear();
            foreach (var kv in _active)
                if (!_seen.Contains(kv.Key)) _toHide.Add(kv.Key);
            for (int i = 0; i < _toHide.Count; i++)
            {
                var e = _toHide[i];
                if (_active.TryGetValue(e, out var view) && view != null)
                {
                    view.Hide();
                    _idle.Enqueue(view);
                }
                _active.Remove(e);
            }
        }

        public void Clear()
        {
            foreach (var kv in _active)
                if (kv.Value != null) Destroy(kv.Value.gameObject);
            _active.Clear();
            while (_idle.Count > 0)
            {
                var v = _idle.Dequeue();
                if (v != null) Destroy(v.gameObject);
            }
            _seen.Clear();
            _cardsByHost.Clear();
            if (_canvas != null) Destroy(_canvas.gameObject);
            _canvas = null;
            _canvasRect = null;
        }

        // 런타임에서 Legacy -> Unified로 되돌릴 때 AttachmentsChanged 재발행을 요구하지 않는다.
        public void RefreshAttachments() => RebuildCards();

        // use-flow unit 3 — 부착 카드 발동 신호(BattleBridge 드레인 호출). host 뷰가 화면에
        // 없으면(오프스크린/비활성) 조용히 무시 — 신호는 보이는 유닛에만 의미가 있다.
        public void PulseCards(Entity host)
        {
            if (_active.TryGetValue(host, out var view) && view != null)
                view.PulseCards();
        }

        private void RebuildCards()
        {
            _cardsByHost.Clear();
            if (hand == null) return;
            hand.GetAttachments(_attachments);
            for (int i = 0; i < _attachments.Count; i++)
            {
                var item = _attachments[i];
                if (!_cardsByHost.TryGetValue(item.host, out var list))
                {
                    list = new List<DreamcatcherCard>(3);
                    _cardsByHost[item.host] = list;
                }
                if (list.Count < 3) list.Add(item.card);
            }
        }

        private bool EnsureCanvas()
        {
            if (style == null)
            {
                if (!_missingStyleLogged)
                {
                    Debug.LogError("[UnitOverheadUiLayer] UnitOverheadUiStyle 미할당 — 신규 UI 스킵.", this);
                    _missingStyleLogged = true;
                }
                return false;
            }
            if (_canvas != null) return true;
            _sprites ??= new UnitOverheadSpriteSet(style);
            var go = new GameObject("UnifiedOverheadCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
            go.transform.SetParent(transform, false);
            _canvas = go.GetComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.overrideSorting = true;
            _canvas.sortingOrder = sortingOrder;
            var scaler = go.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = style.ReferenceResolution;
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
            var go = new GameObject("UnitOverhead", typeof(RectTransform));
            go.transform.SetParent(_canvasRect, false);
            return go.AddComponent<UnitOverheadView>();
        }
    }
}
