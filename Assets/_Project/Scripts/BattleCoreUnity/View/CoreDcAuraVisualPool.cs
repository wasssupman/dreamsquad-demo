using System.Collections.Generic;
using UnityEngine;
using Wassup.BattleCore;
using Wassup.BattleCore.Effects;
using Wassup.Data;
using Wassup.Data.BattleView;

namespace Wassup.BattleCoreUnity.View
{
    // battle-core-rebuild unit 6c — **드림캐쳐가 건 스탯의 오라**(강화 오라). 옛 `DcAuraVisualPool`
    // (92줄) + 브리지 `ReconcileStatusFx` 의 `Empowered` 분기의 후계다.
    //
    // 켜짐의 판정은 **순수 함수**가 한다(`ModifierAuraClassifier.HasActiveDreamcatcherModifier` —
    // 출처 필터 + net 편차). 이 풀은 판정하지 않는다: 스탯 사건(`ModifierApplied`·`ModifierRevoked`)을
    // **계기로** 그 몸의 슬롯 목록을 그 함수에 넘기고, 답대로 오라를 세우거나 거둔다.
    //
    // 옛 풀과 달라진 것 — **매 프레임 생존 폴링이 없다**(`em.Exists` 로 숙주를 되묻던 자리).
    // Entities 누수 3곳 중 하나였고, 키 치환이 아니라 **계약**(모든 소멸은 소멸 사건을 낸다)으로
    // 푼다: 숙주의 `UnitDestroyed` 가 오라를 거둔다. 초당 한 번의 자가 치유는 **경고**다.
    //
    // ⚠ 옛 풀의 다른 절반 — **카드 페이로드가 선언한 오라 프리팹**(`DcPayloadSpec.auraPrefab` 을
    // 베이크가 숙주에 등록)은 부착 사건이 unit 7 이라 여기 없다(6c 이식 제외). 이 풀이 켜는 것은
    // 「드림캐쳐 출처 스탯이 살아 있다」 하나이고, 그 그림은 상태 표식 등록부의 `Empowered` 줄이다.
    [DisallowMultipleComponent]
    public sealed class CoreDcAuraVisualPool : MonoBehaviour
    {
        [SerializeField] private BattleDriver _driver;
        [SerializeField] private CoreUnitViewPool _units;
        [SerializeField] private StatusFxConfig _config;
        [Tooltip("미할당 시 Camera.main")]
        [SerializeField] private Camera _billboardCamera;

        private readonly HashSet<int> _wanted = new HashSet<int>();
        private readonly Dictionary<int, CoreStatusFxView> _active = new Dictionary<int, CoreStatusFxView>();
        private readonly Stack<CoreStatusFxView> _pool = new Stack<CoreStatusFxView>();
        private readonly List<int> _scratch = new List<int>();
        private float _nextSweep;

        /// <summary>실제로 선 오라 수.</summary>
        public int ActiveCount => _active.Count;

        public bool IsShown(SimEntityId host) => _active.ContainsKey(host.Value);

        public bool IsWanted(SimEntityId host) => _wanted.Contains(host.Value);

        private void OnEnable()
        {
            if (_driver != null) _driver.Subscribe(ViewOrder.Status, OnCoreEvent);
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
                case CoreEventKind.MatchStarted:
                    Clear();
                    break;

                case CoreEventKind.ModifierApplied:
                case CoreEventKind.ModifierRevoked:
                {
                    var u = _driver != null ? _driver.Find(e.B) : null;
                    if (u != null && !u.Dead && ModifierAuraClassifier.HasActiveDreamcatcherModifier(u.Modifiers.Slots))
                        _wanted.Add(e.B.Value);
                    else Drop(e.B.Value);
                    break;
                }

                case CoreEventKind.UnitSlain:
                    Drop(e.B.Value);
                    break;
                case CoreEventKind.UnitDestroyed:
                    Drop(e.A.Value);
                    break;
            }
        }

        private void Drop(int host)
        {
            _wanted.Remove(host);
            if (!_active.TryGetValue(host, out var view)) return;
            _active.Remove(host);
            if (view == null) return;
            view.Hide();
            _pool.Push(view);
        }

        private void LateUpdate()
        {
            if (_driver == null || !_driver.Running || _units == null) return;

            if (_wanted.Count > _active.Count)
            {
                var registry = _config != null ? _config.Registry : null;
                var cam = _billboardCamera != null ? _billboardCamera : Camera.main;
                if (registry != null && cam != null && registry.TryGet(StatusFxKind.Empowered, out var entry))
                    foreach (var host in _wanted)
                    {
                        if (_active.ContainsKey(host)) continue;
                        var anchor = AnchorOf(new SimEntityId(host));
                        if (anchor == null) continue;
                        var view = Rent();
                        view.Show(new SimEntityId(host), StatusFxKind.Empowered, anchor, entry, cam);
                        _active[host] = view;
                    }
            }

            foreach (var kv in _active)
            {
                var anchor = AnchorOf(new SimEntityId(kv.Key));
                if (anchor != null && kv.Value != null) kv.Value.Refresh(anchor);
            }

            if (Time.unscaledTime < _nextSweep) return;
            _nextSweep = Time.unscaledTime + 1f;
            _scratch.Clear();
            foreach (var host in _wanted)
                if (!_driver.IsAlive(new SimEntityId(host))) _scratch.Add(host);
            for (int i = 0; i < _scratch.Count; i++)
            {
                Debug.LogWarning($"[CoreDcAuraVisualPool] 유령 오라 회수 — 숙주 {_scratch[i]} 는 판에 없다. "
                    + "소멸 사건을 안 낸 경로가 있다(계약 7).", this);
                Drop(_scratch[i]);
            }
            _scratch.Clear();
        }

        private Transform AnchorOf(SimEntityId id)
        {
            if (_units.TryGet(id, out var view)) return view.transform;
            if (_units.TryGetQuad(id, out var quad)) return quad.transform;
            return null;
        }

        private CoreStatusFxView Rent()
        {
            while (_pool.Count > 0)
            {
                var pooled = _pool.Pop();
                if (pooled != null) return pooled;
            }
            var go = new GameObject("CoreDcAura");
            go.transform.SetParent(transform, false);
            return go.AddComponent<CoreStatusFxView>();
        }

        public void Clear()
        {
            foreach (var kv in _active) if (kv.Value != null) Destroy(kv.Value.gameObject);
            _active.Clear();
            _wanted.Clear();
            while (_pool.Count > 0)
            {
                var v = _pool.Pop();
                if (v != null) Destroy(v.gameObject);
            }
        }
    }
}
