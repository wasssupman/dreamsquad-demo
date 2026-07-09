using System.Collections.Generic;
using Unity.Entities;
using UnityEngine;
using Wassup.Data;

namespace Wassup.Presentation
{
    // unit-status-fx Unit 1 — 상태 연출 스포너(구 AggroIconSpawner 일반화). BattleBridge 가
    // 매 프레임 reconcile: 상태별 ECS 소스로 찾은 (유닛, kind)마다 Ensure, 프레임 끝에
    // 사라진(=상태 해제/사망) 것 회수. 한 유닛에 여러 kind 동시 부착. 풀은 kind별
    // (프리팹이 kind마다 다르므로 같은 kind 로만 재사용).
    public class StatusFxSpawner : MonoBehaviour
    {
        [SerializeField] private StatusFxRegistry registry;
        [Tooltip("미할당 시 Camera.main 사용")]
        [SerializeField] private Camera billboardCamera;

        private readonly Dictionary<(Entity, StatusFxKind), StatusFxView> _active = new();
        private readonly Dictionary<StatusFxKind, Queue<StatusFxView>> _pool = new();
        private readonly HashSet<(Entity, StatusFxKind)> _seen = new();
        private readonly List<(Entity, StatusFxKind)> _toHide = new();
        private bool _missingRegistryLogged;
        private bool _missingCameraLogged;

        public void BeginFrame() => _seen.Clear();

        // 상태 활성인 유닛 1개: 연출 표시(없으면 풀/신규, 있으면 anchor 갱신).
        public void Ensure(Entity entity, StatusFxKind kind, Transform anchor)
        {
            if (registry == null)
            {
                if (!_missingRegistryLogged)
                {
                    Debug.LogError("[StatusFxSpawner] StatusFxRegistry 미할당 — 연출 스킵.");
                    _missingRegistryLogged = true;
                }
                return;
            }
            if (!registry.TryGet(kind, out var entry)) return; // 미등록 kind → 무시
            var cam = billboardCamera != null ? billboardCamera : Camera.main;
            if (cam == null)
            {
                if (!_missingCameraLogged)
                {
                    Debug.LogError("[StatusFxSpawner] 빌보드 카메라 없음 (billboardCamera/Camera.main 모두 null).");
                    _missingCameraLogged = true;
                }
                return;
            }

            var key = (entity, kind);
            _seen.Add(key);
            if (_active.TryGetValue(key, out var existing) && existing != null)
            {
                existing.Refresh(anchor);
                return;
            }
            var view = Get(kind);
            _active[key] = view;
            view.Show(entity, kind, anchor, entry, cam);
        }

        // 프레임 끝 — 이번 프레임에 Ensure 안 된(해제/사망) 연출 회수.
        public void EndFrame()
        {
            _toHide.Clear();
            foreach (var kv in _active)
                if (!_seen.Contains(kv.Key)) _toHide.Add(kv.Key);
            for (int i = 0; i < _toHide.Count; i++)
            {
                var key = _toHide[i];
                if (_active.TryGetValue(key, out var v) && v != null) { v.Hide(); Recycle(key.Item2, v); }
                _active.Remove(key);
            }
        }

        // 전투 teardown 시 전량 파괴(재시작마다 kind별 풀 누적 방지, critic M2).
        public void Clear()
        {
            foreach (var kv in _active)
                if (kv.Value != null) Destroy(kv.Value.gameObject);
            _active.Clear();
            _seen.Clear();
            foreach (var q in _pool.Values)
                while (q.Count > 0)
                {
                    var v = q.Dequeue();
                    if (v != null) Destroy(v.gameObject);
                }
            _pool.Clear();
        }

        private StatusFxView Get(StatusFxKind kind)
        {
            if (_pool.TryGetValue(kind, out var q))
            {
                while (q.Count > 0)
                {
                    var pooled = q.Dequeue();
                    if (pooled != null) return pooled;
                }
            }
            var go = new GameObject("StatusFx_" + kind);
            go.transform.SetParent(transform, false);
            return go.AddComponent<StatusFxView>();
        }

        private void Recycle(StatusFxKind kind, StatusFxView view)
        {
            if (!_pool.TryGetValue(kind, out var q))
            {
                q = new Queue<StatusFxView>();
                _pool[kind] = q;
            }
            q.Enqueue(view);
        }
    }
}
