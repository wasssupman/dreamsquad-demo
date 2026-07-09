using System.Collections.Generic;
using Unity.Entities;
using UnityEngine;
using Wassup.Data;

namespace Wassup.Presentation
{
    // aggro-targeting Unit 13 — 어그로 아이콘 스포너. BattleBridge 가 SerializeField 로
    // 참조하고 매 프레임 reconcile 한다: 어그로된 적마다 Ensure(), 프레임 끝에 이번
    // 프레임에 안 보인(=해제된) 적 아이콘을 EndFrame() 이 회수. 적 1마리당 아이콘 1개 +
    // 뷰 풀링. EnemyHitBarSpawner 와 동형(단 상태 구동 — 일회성 아님).
    public class AggroIconSpawner : MonoBehaviour
    {
        [SerializeField] private AggroIconStyle style;
        [Tooltip("미할당 시 Camera.main 사용")]
        [SerializeField] private Camera billboardCamera;

        private readonly Dictionary<Entity, AggroIconView> _active = new Dictionary<Entity, AggroIconView>();
        private readonly Queue<AggroIconView> _idle = new Queue<AggroIconView>();
        private readonly HashSet<Entity> _seen = new HashSet<Entity>();
        private readonly List<Entity> _toHide = new List<Entity>();
        private bool _missingStyleLogged;
        private bool _missingCameraLogged;

        // 프레임 시작 — 이번 프레임에 본 적 집합 리셋.
        public void BeginFrame() => _seen.Clear();

        // 어그로된 적 1마리: 아이콘 표시(없으면 풀에서, 있으면 anchor 갱신).
        public void Ensure(Entity entity, Transform anchor)
        {
            if (style == null)
            {
                if (!_missingStyleLogged)
                {
                    Debug.LogError("[AggroIconSpawner] AggroIconStyle 미할당 — 아이콘 스킵.");
                    _missingStyleLogged = true;
                }
                return;
            }
            var cam = billboardCamera != null ? billboardCamera : Camera.main;
            if (cam == null)
            {
                if (!_missingCameraLogged)
                {
                    Debug.LogError("[AggroIconSpawner] 빌보드 카메라 없음 (billboardCamera/Camera.main 모두 null).");
                    _missingCameraLogged = true;
                }
                return;
            }

            _seen.Add(entity);
            if (_active.TryGetValue(entity, out var existing) && existing != null)
            {
                existing.Refresh(anchor);
                return;
            }
            var view = Get();
            _active[entity] = view;
            view.Show(entity, anchor, style, cam);
        }

        // 프레임 끝 — 이번 프레임에 Ensure 안 된(해제/사망) 적 아이콘 회수.
        public void EndFrame()
        {
            _toHide.Clear();
            foreach (var kv in _active)
                if (!_seen.Contains(kv.Key)) _toHide.Add(kv.Key);
            for (int i = 0; i < _toHide.Count; i++)
            {
                var e = _toHide[i];
                if (_active.TryGetValue(e, out var v) && v != null) { v.Hide(); _idle.Enqueue(v); }
                _active.Remove(e);
            }
        }

        // 전투 teardown 시 잔여 아이콘 정리 (EnemyHitBarSpawner.Clear 대칭).
        public void Clear()
        {
            foreach (var kv in _active)
                if (kv.Value != null) { kv.Value.Hide(); _idle.Enqueue(kv.Value); }
            _active.Clear();
            _seen.Clear();
        }

        private AggroIconView Get()
        {
            while (_idle.Count > 0)
            {
                var pooled = _idle.Dequeue();
                if (pooled != null) return pooled;
            }
            var go = new GameObject("AggroIcon");
            go.transform.SetParent(transform, false);
            return go.AddComponent<AggroIconView>();
        }
    }
}
