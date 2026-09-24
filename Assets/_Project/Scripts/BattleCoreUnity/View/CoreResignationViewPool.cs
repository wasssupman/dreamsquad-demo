using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;
using Wassup.BattleCore;
using Wassup.Data.BattleView;

namespace Wassup.BattleCoreUnity.View
{
    // battle-core-rebuild unit 6c — **판 위에 쌓인 사직서**. 옛 브리지 `ReconcileResignationViews` +
    // `ResignationPresenter`(83줄)의 후계다.
    //
    // 사직서는 유닛이 줍지 않는다 — 판 위에 쌓였다가 **전역 누적 수가 임계에 닿으면** 가장 오래된
    // 것부터 소모된다(6b2 구현 3). 그래서 소멸 문이 하나(`ResignationConsumed`)이고, 한 장마다 1건이다 —
    // 뷰가 「어느 장을 지울지」 되묻지 않게(계약 7). 옛 뷰는 매 프레임 쿼리 폴링이었다.
    //
    // 값의 정본은 `PickupViewConfig` 의 사직서 칸(5a 가 소비처 0 으로 세워 둔 자산 — 여기서 개통)이다.
    [DisallowMultipleComponent]
    public sealed class CoreResignationViewPool : MonoBehaviour
    {
        [SerializeField] private BattleDriver _driver;
        [SerializeField] private PickupViewConfig _config;

        private readonly Dictionary<int, GameObject> _views = new Dictionary<int, GameObject>();
        private readonly List<int> _scratch = new List<int>();
        private float _nextSweep;
        private bool _missingConfigLogged;

        /// <summary>선 사직서 뷰 수. 「뷰 수 = 코어 개체 수」의 오른쪽 항.</summary>
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
                case CoreEventKind.ResignationDropped: Spawn(e); break;
                case CoreEventKind.ResignationConsumed: Despawn(e.A.Value); break;
            }
        }

        private void Spawn(CoreEvent e)
        {
            if (_views.ContainsKey(e.A.Value)) return;
            if (_config == null && !_missingConfigLogged)
            {
                // 설정이 없어도 **자리 표시는 선다**(플레이스홀더) — 조용히 안 보이는 사직서를 만들지 않는다.
                Debug.LogWarning("[CoreResignationViewPool] PickupViewConfig 미할당 — 플레이스홀더로 그린다.", this);
                _missingConfigLogged = true;
            }
            var center = e.SiteFired.Pos;
            var view = (Vector3)Wassup.Core.BoardSpace.ToView(new float3(center.x, 0f, center.z));
            var go = new GameObject($"CoreResignation_{e.A.Value}");
            go.transform.SetParent(transform, worldPositionStays: false);
            go.transform.position = view + Vector3.up * (_config != null ? _config.ResignationHeight : 0f);
            go.AddComponent<CoreResignationPresenter>().Init(
                _config != null ? _config.ResignationPrefab : null, 0f);
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
            var live = _driver.Match.World.Resignations;
            foreach (var id in _views.Keys)
            {
                bool found = false;
                for (int i = 0; i < live.Count; i++) if (live[i].Id.Value == id) { found = true; break; }
                if (!found) _scratch.Add(id);
            }
            for (int i = 0; i < _scratch.Count; i++)
            {
                Debug.LogWarning($"[CoreResignationViewPool] 유령 사직서 회수 — id {_scratch[i]}. 소멸 사건을 안 낸 경로가 있다(계약 7).", this);
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
