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
    // unit 7c — 옛 풀의 다른 절반, **메커닉이 선언한 부착 오라**(`DcPayloadSpec.auraPrefab` — 옛 `DcAuraVisualPool.Register`,
    // 보스 나이트메어의 바람 오라 등). 옛 bake 는 숙주 스폰 때 등록했고, 새 코어에서는 그 규칙 줄이 **붙는 사건**
    // (`BindingAttached`)이 그 계기다 — 줄 번호 → 프리팹은 번호를 매긴 빌더가 채운 뷰 표(`MatchViewAssets.TryGetBindingAura`)다.
    // 옛 규약 그대로 **숙주당 하나**(먼저 붙은 것이 이긴다) · 앵커 위치 추종 · 줄이 떨어지거나 숙주가 사라지면 거둔다.
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

        private struct Declared
        {
            public int Row;
            public GameObject Prefab;
            public float Scale;
            public GameObject Instance;
        }
        private readonly Dictionary<int, Declared> _declared = new Dictionary<int, Declared>();
        private readonly List<int> _declaredKeys = new List<int>();
        private Transform _declaredRoot;

        /// <summary>메커닉 선언 오라가 선 숙주 수(테스트).</summary>
        public int DeclaredCount => _declared.Count;
        public bool HasDeclaredAura(SimEntityId host) => _declared.ContainsKey(host.Value);

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
                    DropDeclared(e.B.Value, -1);
                    break;
                case CoreEventKind.UnitDestroyed:
                    Drop(e.A.Value);
                    DropDeclared(e.A.Value, -1);
                    break;

                case CoreEventKind.BindingAttached:
                    if (_driver != null && !_declared.ContainsKey(e.A.Value)
                        && _driver.ViewAssets.TryGetBindingAura(e.DefIndex, out var prefab, out float scale))
                        _declared[e.A.Value] = new Declared { Row = e.DefIndex, Prefab = prefab, Scale = scale <= 0f ? 1f : scale };
                    break;
                case CoreEventKind.BindingDetached:
                    DropDeclared(e.A.Value, e.DefIndex);
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

        // row = -1 → 그 숙주의 것 전부. 줄이 떨어진 경우는 **그 줄이 세운 오라**만 거둔다.
        private void DropDeclared(int host, int row)
        {
            if (!_declared.TryGetValue(host, out var d)) return;
            if (row >= 0 && d.Row != row) return;
            if (d.Instance != null) Destroy(d.Instance);
            _declared.Remove(host);
        }

        private void SyncDeclared()
        {
            if (_declared.Count == 0) return;
            _declaredKeys.Clear();
            foreach (var k in _declared.Keys) _declaredKeys.Add(k);
            for (int i = 0; i < _declaredKeys.Count; i++)
            {
                int host = _declaredKeys[i];
                var d = _declared[host];
                var anchor = AnchorOf(new SimEntityId(host));
                if (anchor == null) { if (d.Instance != null) d.Instance.SetActive(false); continue; }
                if (d.Instance == null)
                {
                    if (_declaredRoot == null)
                    {
                        _declaredRoot = new GameObject("CoreDeclaredAuras").transform;
                        _declaredRoot.SetParent(transform, false);
                    }
                    d.Instance = Instantiate(d.Prefab, _declaredRoot);
                    d.Instance.transform.localScale = Vector3.one * d.Scale;
                    _declared[host] = d;
                }
                if (!d.Instance.activeSelf) d.Instance.SetActive(true);
                d.Instance.transform.position = anchor.position;
            }
        }

        private void LateUpdate()
        {
            if (_driver == null || !_driver.Running || _units == null) return;
            SyncDeclared();

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
            foreach (var host in _declared.Keys)
                if (!_driver.IsAlive(new SimEntityId(host)) && !_scratch.Contains(host)) _scratch.Add(host);
            for (int i = 0; i < _scratch.Count; i++)
            {
                Debug.LogWarning($"[CoreDcAuraVisualPool] 유령 오라 회수 — 숙주 {_scratch[i]} 는 판에 없다. "
                    + "소멸 사건을 안 낸 경로가 있다(계약 7).", this);
                Drop(_scratch[i]);
                DropDeclared(_scratch[i], -1);
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
            foreach (var kv in _declared) if (kv.Value.Instance != null) Destroy(kv.Value.Instance);
            _declared.Clear();
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
