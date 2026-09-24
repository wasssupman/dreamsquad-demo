using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;
using Wassup.BattleCore;
using Wassup.Data.BattleView;

namespace Wassup.BattleCoreUnity.View
{
    // battle-core-rebuild unit 6c — **바닥에 떨어진 픽업**(레드불). 옛 브리지 `ReconcilePickupViews` +
    // `PickupPresenter`(131줄)의 후계다.
    //
    // 옛 것은 **매 프레임 픽업 쿼리를 폴링해** 새 엔티티엔 뷰를 세우고 사라진 엔티티의 뷰를 지웠다
    // (「Pickup 은 순수 ECS 스폰이라 이벤트가 없다」). 새 코어에는 사건이 **문마다** 있다(계약 7):
    // `PickupSpawned` 가 세우고, 먹힘(`PickupTaken`)·만료(`PickupExpired`) 두 문이 각자 지운다.
    // 한 사건으로 접었다면 뷰가 「먹혔나 사라졌나」를 되물어야 했다.
    //
    // 값의 정본은 `PickupViewConfig`(5a 가 소비처 0 으로 세워 둔 자산 — 여기서 개통)다.
    // 자리는 사건의 `SiteFired`(칸 중심 · 자리형)이고 뜨는 높이는 view 세로(월드 +Y)로만 준다 —
    // `BoardSpace.ToView` 는 sim 높이를 버린다.
    [DisallowMultipleComponent]
    public sealed class CorePickupViewPool : MonoBehaviour
    {
        [SerializeField] private BattleDriver _driver;
        [SerializeField] private PickupViewConfig _config;

        private readonly Dictionary<int, GameObject> _views = new Dictionary<int, GameObject>();
        private readonly List<int> _scratch = new List<int>();
        private float _nextSweep;
        private bool _missingConfigLogged;

        /// <summary>선 픽업 뷰 수. 「뷰 수 = 코어 개체 수」의 오른쪽 항.</summary>
        public int ViewCount => _views.Count;

        private void OnEnable()
        {
            if (_driver != null) _driver.Subscribe(ViewOrder.Board, OnCoreEvent);
        }

        private void OnDisable()
        {
            if (_driver != null) _driver.Unsubscribe(OnCoreEvent);
            Clear();
        }

        private void OnCoreEvent(CoreEvent e)
        {
            switch (e.Kind)
            {
                case CoreEventKind.MatchStarted: Clear(); break;
                case CoreEventKind.PickupSpawned: Spawn(e); break;
                case CoreEventKind.PickupTaken:
                case CoreEventKind.PickupExpired:
                    Despawn(e.A.Value);
                    break;
            }
        }

        private void Spawn(CoreEvent e)
        {
            if (_views.ContainsKey(e.A.Value)) return;
            if (_config == null && !_missingConfigLogged)
            {
                // 설정이 없어도 **자리 표시는 선다**(플레이스홀더) — 조용히 안 보이는 픽업을 만들지 않는다.
                Debug.LogWarning("[CorePickupViewPool] PickupViewConfig 미할당 — 플레이스홀더로 그린다.", this);
                _missingConfigLogged = true;
            }
            var center = e.SiteFired.Pos;
            var view = (Vector3)Wassup.Core.BoardSpace.ToView(new float3(center.x, 0f, center.z));
            var go = new GameObject($"CorePickup_{(PickupKind)e.Arg}_{e.A.Value}");
            go.transform.SetParent(transform, worldPositionStays: false);
            go.transform.position = view + Vector3.up * (_config != null ? _config.PickupHeight : 0f);
            go.AddComponent<CorePickupPresenter>().Init(
                _config != null ? _config.PickupPrefab : null,
                _config != null ? _config.PickupModelScale : 1f,
                _config != null ? _config.PickupModelBaseY : 0f,
                _config != null ? _config.PickupOverrideMaterial : null);
            _views[e.A.Value] = go;
        }

        private void Despawn(int id)
        {
            if (!_views.TryGetValue(id, out var go)) return;
            _views.Remove(id);
            if (go != null) Destroy(go);
        }

        // 초당 1회 — 정상 경로가 아니다(계약 7).
        private void LateUpdate()
        {
            if (_driver == null || !_driver.Running || _views.Count == 0) return;
            if (Time.unscaledTime < _nextSweep) return;
            _nextSweep = Time.unscaledTime + 1f;
            _scratch.Clear();
            var live = _driver.Match.World.Pickups;
            foreach (var id in _views.Keys)
            {
                bool found = false;
                for (int i = 0; i < live.Count; i++) if (live[i].Id.Value == id) { found = true; break; }
                if (!found) _scratch.Add(id);
            }
            for (int i = 0; i < _scratch.Count; i++)
            {
                Debug.LogWarning($"[CorePickupViewPool] 유령 픽업 회수 — id {_scratch[i]}. 소멸 사건을 안 낸 경로가 있다(계약 7).", this);
                Despawn(_scratch[i]);
            }
            _scratch.Clear();
        }

        public void Clear()
        {
            foreach (var kv in _views) if (kv.Value != null) Destroy(kv.Value);
            _views.Clear();
        }
    }
}
