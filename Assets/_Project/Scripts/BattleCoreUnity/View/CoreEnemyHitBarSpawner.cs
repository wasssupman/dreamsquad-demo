using System.Collections.Generic;
using UnityEngine;
using Wassup.BattleCore;
using Wassup.Data;
using Wassup.Data.BattleView;

namespace Wassup.BattleCoreUnity.View
{
    // battle-core-rebuild unit 5a — 적 피격 마이크로바. 옛 `EnemyHitBarSpawner` 의 후계다.
    //
    // 옛 것은 브리지가 드레인하면서 `Show()` 를 불러 줬다. 새 것은 **자기 사건을 구독한다**
    // (계약 12). 적 1마리당 활성 바 1개 — 연타는 기존 바 갱신이고 스택하지 않는다.
    //
    // ⚠ 체력 비율을 **여기서 계산하지 않는다**(C7). 화면 숫자와 바가 같은 틱에 서로 다른
    // 비율을 나르면 안 되므로, 그 틱의 최종값을 `DamageApplied` 가 값으로 싣고 뷰는 읽기만 한다.
    [DisallowMultipleComponent]
    public sealed class CoreEnemyHitBarSpawner : MonoBehaviour
    {
        [SerializeField] private BattleDriver _driver;
        [SerializeField] private CoreUnitViewPool _units;
        [SerializeField] private CharacterViewConfig _characterView;

        [Tooltip("미할당 시 Camera.main 사용")]
        [SerializeField] private Camera _billboardCamera;

        private readonly Dictionary<int, CoreEnemyHitBarView> _active = new Dictionary<int, CoreEnemyHitBarView>();
        private readonly Queue<CoreEnemyHitBarView> _idle = new Queue<CoreEnemyHitBarView>();
        private bool _missingStyleLogged;
        private bool _missingCameraLogged;

        public int ActiveCount => _active.Count;

        private void OnEnable()
        {
            if (_driver != null) _driver.Subscribe(ViewOrder.Damage, OnCoreEvent);
        }

        private void OnDisable()
        {
            if (_driver != null) _driver.Unsubscribe(OnCoreEvent);
            Clear();
        }

        private void OnCoreEvent(CoreEvent e)
        {
            if (e.Kind != CoreEventKind.DamageApplied) return;
            // 적만 — 방어유닛 체력은 오버헤드 바가 든다.
            if (e.Faction != Wassup.Battle.Units.Faction.EnemyUnit) return;

            // ⚠ `SiteTarget.OriginBody` 자리에 **그 틱 최종 체력 비율**이 실려 온다(C7).
            // 몸 반경이 아니다 — 그 자리의 뜻을 사건 종류가 정한다.
            Show(e.B, (Vector3)Wassup.Core.BoardSpace.ToView(e.SiteTarget.Pos), e.SiteTarget.OriginBody);
        }

        private void Show(SimEntityId id, Vector3 fallbackViewBase, float hpRatio)
        {
            var style = _characterView != null ? _characterView.HealthDisplayStyle : null;
            if (style == null)
            {
                if (!_missingStyleLogged)
                {
                    Debug.LogError("[CoreEnemyHitBarSpawner] HealthDisplayStyle 미할당 — 마이크로바 스킵.", this);
                    _missingStyleLogged = true;
                }
                return;
            }
            var cam = _billboardCamera != null ? _billboardCamera : Camera.main;
            if (cam == null)
            {
                if (!_missingCameraLogged)   // 사건마다 도배 방지(스타일 게이팅과 대칭)
                {
                    Debug.LogError("[CoreEnemyHitBarSpawner] 빌보드 카메라를 찾을 수 없다.", this);
                    _missingCameraLogged = true;
                }
                return;
            }

            Transform anchor = _units != null && _units.TryGet(id, out var view) ? view.transform : null;

            if (_active.TryGetValue(id.Value, out var existing) && existing != null)
            {
                existing.Refresh(anchor, fallbackViewBase, hpRatio);   // 스택 금지: 갱신 + hold 리셋
                return;
            }

            var bar = Get();
            _active[id.Value] = bar;
            bar.Play(id, anchor, fallbackViewBase, hpRatio, style, cam, OnComplete);
        }

        public void Clear()
        {
            foreach (var kv in _active)
                if (kv.Value != null) { kv.Value.Deactivate(); _idle.Enqueue(kv.Value); }
            _active.Clear();
        }

        private CoreEnemyHitBarView Get()
        {
            while (_idle.Count > 0)
            {
                var pooled = _idle.Dequeue();
                if (pooled != null) return pooled;
            }
            var go = new GameObject("CoreEnemyHitBar");
            go.transform.SetParent(transform, false);
            return go.AddComponent<CoreEnemyHitBarView>();
        }

        private void OnComplete(CoreEnemyHitBarView view)
        {
            if (view == null) return;
            // 그 사이 새 바로 교체되지 않았을 때만 매핑 제거.
            if (_active.TryGetValue(view.Id.Value, out var cur) && cur == view)
                _active.Remove(view.Id.Value);
            _idle.Enqueue(view);
        }
    }
}
