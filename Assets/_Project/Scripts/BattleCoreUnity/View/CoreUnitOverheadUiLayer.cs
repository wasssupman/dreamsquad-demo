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
    // ⚠ **부착 카드는 아직 없다.** 그 사건(부착)이 코어에 없어서(unit 7) 카드 줄은 옮기지
    // 않았다 — 빈 카드 슬롯을 먼저 만들면 「카드가 안 뜬다」를 사건이 아니라 UI 에서 찾게 된다.
    [DisallowMultipleComponent]
    public sealed class CoreUnitOverheadUiLayer : MonoBehaviour
    {
        [SerializeField] private BattleDriver _driver;
        [SerializeField] private CoreUnitViewPool _units;
        [SerializeField] private UnitOverheadUiStyle _style;
        [SerializeField] private CharacterViewConfig _characterView;
        [SerializeField] private int _sortingOrder = 3;

        private readonly Dictionary<int, UnitOverheadView> _active = new Dictionary<int, UnitOverheadView>();
        private readonly Queue<UnitOverheadView> _idle = new Queue<UnitOverheadView>();
        private readonly HashSet<int> _seen = new HashSet<int>();
        private readonly List<int> _toHide = new List<int>();

        private Canvas _canvas;
        private RectTransform _canvasRect;
        private UnitOverheadSpriteSet _sprites;
        private bool _missingStyleLogged;

        public int ActiveCount => _active.Count;

        private void OnDisable()
        {
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
                if (!_units.TryGet(u.Id, out var view) || !view.TryGetScreenRect(cam, out var rect)) continue;

                var anchor = view.transform;
                Vector2 screenAnchor = UnitOverheadLayout.ScreenAnchor(
                    cam.WorldToScreenPoint(anchor.position).x, rect);
                SetUnit(u.Id, ((int)u.Faction & Wassup.Battle.Units.Factions.AnyDefender) != 0,
                        Mathf.Clamp01(u.Health / u.MaxHealth),
                        screenAnchor, ProjectTileScreenWidth(cam, anchor));
            }
            EndFrame();
        }

        private float ProjectTileScreenWidth(Camera cam, Transform anchor)
        {
            if (cam == null || anchor == null) return 1f;
            Vector3 half = Vector3.right * (_driver.TileSize * 0.5f);
            Vector3 a = cam.WorldToScreenPoint(anchor.position - half);
            Vector3 b = cam.WorldToScreenPoint(anchor.position + half);
            return Vector2.Distance(new Vector2(a.x, a.y), new Vector2(b.x, b.y));
        }

        private void SetUnit(SimEntityId id, bool defender, float healthRatio,
                             Vector2 screenAnchor, float tileScreenWidth)
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
            view.Show(local, tileScreenWidth / scale,
                      defender ? OverheadBarSkin.Defender : OverheadBarSkin.Enemy,
                      healthRatio, null, _style, _sprites, resetHealth,
                      shieldRatio: 0f, stacks: null, stackIcons: null, barScale: 1f);
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
