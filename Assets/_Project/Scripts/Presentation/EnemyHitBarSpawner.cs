using System.Collections.Generic;
using Unity.Entities;
using UnityEngine;
using Wassup.Data;

namespace Wassup.Presentation
{
    // unit-health-display unit 2 — 적 피격 마이크로바 스포너. BattleBridge 가
    // SerializeField 로 참조하고 DamageNumberEvent drain 시 Show() 호출.
    // 적 1마리당 활성 바 1개(連打 = 기존 바 갱신, 스택 금지) + 뷰 풀링.
    public class EnemyHitBarSpawner : MonoBehaviour
    {
        [SerializeField] private HealthDisplayStyle style;
        [Tooltip("미할당 시 Camera.main 사용")]
        [SerializeField] private Camera billboardCamera;

        private readonly Dictionary<Entity, EnemyHitBarView> _active = new Dictionary<Entity, EnemyHitBarView>();
        private readonly Queue<EnemyHitBarView> _idle = new Queue<EnemyHitBarView>();
        private bool _missingStyleLogged;
        private bool _missingCameraLogged;

        // anchor: 적 뷰 transform(view 좌표, nullable). fallbackViewBase: anchor 없을 때
        // 고정 위치(ToView(evt.position)). hpRatio: 정산 후 [0,1].
        public void Show(Entity entity, Transform anchor, Vector3 fallbackViewBase, float hpRatio)
        {
            if (style == null)
            {
                if (!_missingStyleLogged)
                {
                    Debug.LogError("[EnemyHitBarSpawner] HealthDisplayStyle 미할당 — 마이크로바 스킵.");
                    _missingStyleLogged = true;
                }
                return;
            }
            var cam = billboardCamera != null ? billboardCamera : Camera.main;
            if (cam == null)
            {
                if (!_missingCameraLogged) // 이벤트마다 도배 방지 (style 게이팅과 대칭)
                {
                    Debug.LogError("[EnemyHitBarSpawner] 빌보드 카메라를 찾을 수 없습니다 (billboardCamera/Camera.main 모두 null).");
                    _missingCameraLogged = true;
                }
                return;
            }

            if (_active.TryGetValue(entity, out var existing) && existing != null)
            {
                existing.Refresh(anchor, fallbackViewBase, hpRatio); // 스택 금지: 갱신 + hold 리셋
                return;
            }

            var view = Get();
            _active[entity] = view;
            view.Play(entity, anchor, fallbackViewBase, hpRatio, style, cam, OnComplete);
        }

        // 전투 teardown 시 잔여 마이크로바 정리 (TileHealthGaugeLayer.Clear 와 대칭).
        public void Clear()
        {
            foreach (var kv in _active)
                if (kv.Value != null) { kv.Value.Deactivate(); _idle.Enqueue(kv.Value); }
            _active.Clear();
        }

        private EnemyHitBarView Get()
        {
            while (_idle.Count > 0)
            {
                var pooled = _idle.Dequeue();
                if (pooled != null) return pooled;
            }
            var go = new GameObject("EnemyHitBar");
            go.transform.SetParent(transform, false);
            return go.AddComponent<EnemyHitBarView>();
        }

        private void OnComplete(EnemyHitBarView view)
        {
            if (view == null) return;
            // 그 사이 새 바로 교체되지 않았을 때만 매핑 제거.
            if (_active.TryGetValue(view.Entity, out var cur) && cur == view)
                _active.Remove(view.Entity);
            _idle.Enqueue(view);
        }
    }
}
