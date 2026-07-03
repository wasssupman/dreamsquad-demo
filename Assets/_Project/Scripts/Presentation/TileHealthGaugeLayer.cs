using System.Collections.Generic;
using UnityEngine;
using Wassup.Data;

namespace Wassup.Presentation
{
    // unit-health-display unit 3 — 방어유닛 타일 게이지 레이어. cell 키로 게이지 뷰를
    // 풀링/관리한다. BattleBridge 가 defender 폴링에서 Set, 사망에서 Hide, teardown 에서 Clear.
    // 만피(hideWhenFull)면 자동 Hide — 배치 직후 바닥 클러터 방지.
    public class TileHealthGaugeLayer : MonoBehaviour
    {
        [SerializeField] private HealthDisplayStyle style;

        private readonly Dictionary<Vector2Int, TileHealthGaugeView> _active = new();
        private readonly Queue<TileHealthGaugeView> _idle = new();
        private bool _missingStyleLogged;

        public void Set(Vector2Int cell, Vector3 tileCenterView, float tileWorldSize, float ratio)
        {
            if (style == null)
            {
                if (!_missingStyleLogged)
                {
                    Debug.LogError("[TileHealthGaugeLayer] HealthDisplayStyle 미할당 — 타일 게이지 스킵.");
                    _missingStyleLogged = true;
                }
                return;
            }
            if (style.GaugeHideWhenFull && ratio >= 1f - style.GaugeFullEpsilon)
            {
                Hide(cell); // 만피 복귀(regen) 포함
                return;
            }
            if (!_active.TryGetValue(cell, out var view) || view == null)
            {
                view = Get();
                _active[cell] = view;
            }
            view.Set(tileCenterView, tileWorldSize, ratio, style);
        }

        public void Hide(Vector2Int cell)
        {
            if (!_active.TryGetValue(cell, out var view)) return;
            if (view != null) { view.Hide(); _idle.Enqueue(view); }
            _active.Remove(cell);
        }

        public void Clear()
        {
            foreach (var kv in _active)
                if (kv.Value != null) { kv.Value.Hide(); _idle.Enqueue(kv.Value); }
            _active.Clear();
        }

        private TileHealthGaugeView Get()
        {
            while (_idle.Count > 0)
            {
                var pooled = _idle.Dequeue();
                if (pooled != null) return pooled;
            }
            var go = new GameObject("TileHealthGauge");
            go.transform.SetParent(transform, false);
            return go.AddComponent<TileHealthGaugeView>();
        }
    }
}
