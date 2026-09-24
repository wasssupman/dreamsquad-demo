using System.Collections.Generic;
using UnityEngine;
using Wassup.BattleCore;
using Wassup.BattleCore.Effects;
using Wassup.Data;
using Wassup.Data.BattleView;

namespace Wassup.BattleCoreUnity.View
{
    // battle-core-rebuild unit 6c — **몸에 붙는 상태 표식**. 옛 `StatusFxSpawner`(118줄) +
    // 브리지 `ReconcileStatusFx`(151줄)의 후계다.
    //
    // 옛 것은 브리지가 **매 프레임 월드를 폴링해** 「지금 기절한 놈 · 불타는 놈 · 번아웃인 놈」을
    // 다시 모으고(`BeginFrame → Ensure ×N → EndFrame`), 그 프레임에 안 모인 표식을 내렸다.
    // 새 것은 **사건을 구독한다**(계약 12) — 걸림 사건이 켜고, 풀림 사건이 끄고, 숙주의 소멸 사건이
    // 그 몸에 붙은 표식을 전부 거둔다. 폴링이 없으므로 「언제 끄나」를 매 프레임 되묻지 않는다.
    //
    // 켜는 사건 → 표식:
    //   · 군중 제어 `CcApplied`/`CcCleared` — 기절(별) · 수면(Zz). 넉백은 행동 잠금이 아니라 표식이 없다.
    //   · 지속 피해 `DotApplied`/`DotCleared` — **원소**만 본다(출혈·화염·얼음·독). 출처(스택·장판·배치)는
    //     그림에 관여하지 않는다 — 장판 화염이든 스택 폭발 화염이든 화면에는 같은 불이다(옛 규칙).
    //     슬롯은 (출처, 원소) 2축이라 **같은 원소가 두 출처에서 동시에** 설 수 있다 — 둘 중 하나가
    //     풀려도 다른 하나가 살아 있으면 불은 계속 탄다. 그래서 (숙주, 슬롯 묶음) 집합으로 센다.
    //   · 스탯 `ModifierApplied`/`ModifierRevoked` — 번아웃(피로 임계 파생). 사건은 「무엇이 바뀌었나」
    //     만 알고 「그 결과 번아웃인가」는 모르므로, 사건을 **계기로** 그 몸의 슬롯 목록을 순수 판정에
    //     넘긴다(`ModifierAuraClassifier`). 판정은 코어 함수이고 뷰는 결과만 그린다.
    //   · 어그로 `AggroAcquired`/`AggroReleased` — 끌려간 적(`A`)의 머리 위 표식. 도발이 다른 가디언으로
    //     갈아타도 표식은 적의 것이라 그대로다(획득 사건이 한 번 더 올 뿐). 풀림 사건이 코어의 해제 함수
    //     하나에서 나오므로 여기서는 받기만 한다 — 옛 것은 `Aggroed` 보유를 매 프레임 폴링했다.
    //   · 라스트런 `PickupTaken`(레드불)/`LastRunEnded` — 먹은 자(`B`)에 켜고, 창이 닫히면(crash · 사망 ·
    //     퇴근 · 제거) 끈다. 레드불을 먹는 것이 곧 창의 개시다 — 코어의 소비자 필터가 창이 열린 유닛을
    //     이미 거르므로 「먹었는데 창이 안 열린」 경우가 없다. 닫힘 사건은 코어의 한 곳(`BattleWorld`)에서
    //     나온다. 6c 의 임시 다리(스탯 회수 계기 + 초당 정본 플래그 확인)는 이 사건으로 대체됐다.
    //
    // ⚠ **자가 치유는 정상 경로가 아니다.** 초당 한 번 숙주가 아직 판 위에 있나를 보고, 없는데
    // 표식이 남아 있으면 **경고를 남기고** 거둔다 — 그것은 어떤 소멸 경로가 소멸 사건을 안 냈다는
    // 신호다(계약 7, 5a 의 유닛 뷰 풀과 같은 규율).
    //
    // ⚠ 한 몸에 표식이 여럿이면 **전부** 뜬다(옛 동작 — 키가 (유닛, 종류)). 무엇이 이겨 보여야
    // 하나는 옛 코드의 암묵 순서를 베끼지 않고 사용자 플레이에서 정한다(6c 이식 제외).
    [DisallowMultipleComponent]
    public sealed class CoreStatusFxSpawner : MonoBehaviour
    {
        [SerializeField] private BattleDriver _driver;
        [Tooltip("표식이 따라갈 유닛 뷰(앵커). 비어 있으면 표식이 안 뜬다.")]
        [SerializeField] private CoreUnitViewPool _units;
        [SerializeField] private StatusFxConfig _config;
        [Tooltip("미할당 시 Camera.main")]
        [SerializeField] private Camera _billboardCamera;

        private readonly struct Key : System.IEquatable<Key>
        {
            public readonly int Host;
            public readonly StatusFxKind Kind;
            public Key(int host, StatusFxKind kind) { Host = host; Kind = kind; }
            public bool Equals(Key o) => Host == o.Host && Kind == o.Kind;
            public override bool Equals(object obj) => obj is Key o && Equals(o);
            public override int GetHashCode() => (Host * 31) ^ (int)Kind;
        }

        // 「켜져 있어야 하는 것」과 「실제로 선 뷰」를 나눠 든다 — 사건이 온 순간 숙주 뷰가 아직 없을
        // 수 있다(같은 틱에 태어나 걸린 적). 원하는 것만 적어 두고 앵커가 생기는 프레임에 세운다.
        private readonly HashSet<Key> _wanted = new HashSet<Key>();
        private readonly Dictionary<Key, CoreStatusFxView> _active = new Dictionary<Key, CoreStatusFxView>();
        private readonly Dictionary<StatusFxKind, Queue<CoreStatusFxView>> _pool =
            new Dictionary<StatusFxKind, Queue<CoreStatusFxView>>();

        // 지속 피해 슬롯 — (숙주, (출처×100+원소)). 원소 표식은 이 집합에 그 원소가 하나라도 있을 때 켜진다.
        private readonly HashSet<(int host, int packed)> _dots = new HashSet<(int, int)>();

        private readonly List<Key> _scratch = new List<Key>();
        private float _nextSweep;
        private bool _missingConfigLogged;

        /// <summary>실제로 선 표식 수. 「사건 1 → 뷰 1」 검사의 오른쪽 항이다.</summary>
        public int ActiveCount => _active.Count;

        /// <summary>그 숙주에 그 종류가 떠 있나(테스트·진단).</summary>
        public bool IsShown(SimEntityId host, StatusFxKind kind) => _active.ContainsKey(new Key(host.Value, kind));

        /// <summary>그 숙주에 붙은 표식 수(원하는 것 기준 — 뷰가 서기 전 프레임도 센다).</summary>
        public int WantedCountOf(SimEntityId host)
        {
            int n = 0;
            foreach (var k in _wanted) if (k.Host == host.Value) n++;
            return n;
        }

        /// <summary>그 종류로 **켜져야 하는** 표식 수(뷰가 서기 전 프레임도 센다). 부팅 스모크의 「표식 수 = 코어 표식 수」.</summary>
        public int WantedCountOfKind(StatusFxKind kind)
        {
            int n = 0;
            foreach (var k in _wanted) if (k.Kind == kind) n++;
            return n;
        }

        private void OnEnable()
        {
            if (_driver != null) _driver.Subscribe(ViewOrder.Status, OnCoreEvent);
        }

        private void OnDisable()
        {
            if (_driver != null) _driver.Unsubscribe(OnCoreEvent);
            Clear();
        }

        // ── 사건 ─────────────────────────────────────────────────────────────
        private void OnCoreEvent(CoreEvent e)
        {
            switch (e.Kind)
            {
                case CoreEventKind.MatchStarted:
                    Clear();   // 판 경계 — 이전 판의 표식이 새 판의 같은 번호 개체에 붙지 않게
                    break;

                case CoreEventKind.CcApplied:
                    if (TryCcKind((CcSlotKind)e.Arg, out var onKind)) _wanted.Add(new Key(e.B.Value, onKind));
                    break;

                case CoreEventKind.CcCleared:
                    // 풀림 사건은 **숙주가 주체**다(`A`).
                    if (TryCcKind((CcSlotKind)e.Arg, out var offKind)) Unwant(new Key(e.A.Value, offKind));
                    break;

                case CoreEventKind.DotApplied:
                    _dots.Add((e.B.Value, e.Arg));
                    RefreshDot(e.B.Value, DotElementMap.ElementOfArg(e.Arg));
                    break;

                case CoreEventKind.DotCleared:
                    _dots.Remove((e.A.Value, e.Arg));
                    RefreshDot(e.A.Value, DotElementMap.ElementOfArg(e.Arg));
                    break;

                case CoreEventKind.ModifierApplied:
                case CoreEventKind.ModifierRevoked:
                    RefreshBurnout(e.B);
                    break;

                case CoreEventKind.AggroAcquired:
                    _wanted.Add(new Key(e.A.Value, StatusFxKind.Aggro));
                    break;

                case CoreEventKind.AggroReleased:
                    Unwant(new Key(e.A.Value, StatusFxKind.Aggro));
                    break;

                case CoreEventKind.PickupTaken:
                    // `B` = 먹은 자. 레드불을 먹은 순간이 창의 개시다(헤더).
                    if (e.Arg == (int)PickupKind.RedBull) _wanted.Add(new Key(e.B.Value, StatusFxKind.LastRun));
                    break;

                case CoreEventKind.LastRunEnded:
                    Unwant(new Key(e.A.Value, StatusFxKind.LastRun));
                    break;

                // unit 7c — **표식**(살찌운 제물). 적에게 붙은 카드 규칙이 곧 표식이다(7b — 표식 등록부 없음). 옛 브리지는 등록부를
                // 매 프레임 훑어 `Marked` 표식을 세웠다 — 여기는 부착 사건이 켜고, 떨어짐(처치·유출 = 숙주 소멸)이 끈다.
                // 「적을 겨누는 카드인가」는 코어의 한 칸(`TargetsEnemies`)이다 — 메커닉을 뒤져 추측하지 않는다.
                case CoreEventKind.CardAttached:
                    if (IsMarkCard(e.DefIndex)) _wanted.Add(new Key(e.A.Value, StatusFxKind.Marked));
                    break;
                case CoreEventKind.CardDetached:
                    if (IsMarkCard(e.DefIndex)) Unwant(new Key(e.A.Value, StatusFxKind.Marked));
                    break;

                // 숙주가 사라지면 그 몸의 표식은 **전부** 간다. 피해로 죽은 순간(`UnitSlain`)에도 거둔다 —
                // 사망 모션 동안 별이 도는 시체는 「아직 기절 중」으로 읽힌다.
                case CoreEventKind.UnitSlain:
                    DropHost(e.B.Value);
                    break;
                case CoreEventKind.UnitDestroyed:
                    DropHost(e.A.Value);
                    break;
            }
        }

        private bool IsMarkCard(int cardIndex)
        {
            var def = _driver != null ? _driver.Definition : null;
            return def != null && cardIndex >= 0 && cardIndex < def.Cards.Length && def.Cards[cardIndex].TargetsEnemies;
        }

        private static bool TryCcKind(CcSlotKind kind, out StatusFxKind fx)
        {
            switch (kind)
            {
                case CcSlotKind.Stun: fx = StatusFxKind.Stun; return true;
                case CcSlotKind.Sleep: fx = StatusFxKind.Sleep; return true;
                default: fx = default; return false;   // 넉백 — 외력이라 표식이 없다
            }
        }

        private static bool TryDotKind(DotElement element, out StatusFxKind fx)
        {
            switch (element)
            {
                case DotElement.Bleed: fx = StatusFxKind.Bleed; return true;
                case DotElement.Fire: fx = StatusFxKind.Fire; return true;
                case DotElement.Ice: fx = StatusFxKind.Ice; return true;
                case DotElement.Poison: fx = StatusFxKind.Poison; return true;
                default: fx = default; return false;
            }
        }

        private void RefreshDot(int host, DotElement element)
        {
            if (!TryDotKind(element, out var fx)) return;
            bool any = false;
            foreach (var d in _dots)
                if (d.host == host && DotElementMap.ElementOfArg(d.packed) == element) { any = true; break; }
            var key = new Key(host, fx);
            if (any) _wanted.Add(key); else Unwant(key);
        }

        private void RefreshBurnout(SimEntityId host)
        {
            var key = new Key(host.Value, StatusFxKind.Burnout);
            var u = _driver != null ? _driver.Find(host) : null;
            // 판정은 코어 함수다 — 뷰는 「그 출처의 슬롯이 살아 있나」를 스스로 세지 않는다.
            if (u != null && !u.Dead
                && ModifierAuraClassifier.HasAnyFromOrigin(u.Modifiers.Slots, ModifierOrigin.Burnout))
                _wanted.Add(key);
            else Unwant(key);
        }

        private void Unwant(in Key key)
        {
            _wanted.Remove(key);
            if (_active.TryGetValue(key, out var view))
            {
                _active.Remove(key);
                Recycle(key.Kind, view);
            }
        }

        private void DropHost(int host)
        {
            _scratch.Clear();
            foreach (var k in _wanted) if (k.Host == host) _scratch.Add(k);
            foreach (var k in _active.Keys) if (k.Host == host && !_scratch.Contains(k)) _scratch.Add(k);
            for (int i = 0; i < _scratch.Count; i++) Unwant(_scratch[i]);
            _scratch.Clear();
            _dots.RemoveWhere(d => d.host == host);
        }

        // ── 매 프레임 ────────────────────────────────────────────────────────
        // 앵커는 유닛 뷰다 — 유닛 뷰 풀이 `LateUpdate` 에서 위치를 잡은 뒤 표식 뷰가 자기
        // `LateUpdate` 에서 따라간다(카메라 확정 뒤). 여기서는 **서야 하는데 안 선 것**만 세운다.
        private void LateUpdate()
        {
            if (_driver == null || !_driver.Running || _units == null) return;
            StatusFxRegistry registry = null;
            if (_wanted.Count > 0 && !TryRegistry(out registry)) return;

            if (_wanted.Count > _active.Count && registry != null)
            {
                var cam = _billboardCamera != null ? _billboardCamera : Camera.main;
                if (cam != null)
                    foreach (var key in _wanted)
                    {
                        if (_active.ContainsKey(key)) continue;
                        if (!registry.TryGet(key.Kind, out var entry)) continue;   // 미등록 종류 → 무시(옛 규약)
                        var anchor = AnchorOf(new SimEntityId(key.Host));
                        if (anchor == null) continue;                              // 앵커가 생기는 프레임까지 대기
                        var view = Rent(key.Kind);
                        view.Show(new SimEntityId(key.Host), key.Kind, anchor, entry, cam);
                        _active[key] = view;
                    }
            }

            // 뷰가 바뀌었을 수 있다(배치 비행 → 착지 등). 앵커를 다시 건다.
            foreach (var kv in _active)
            {
                var anchor = AnchorOf(new SimEntityId(kv.Key.Host));
                if (anchor != null && kv.Value != null) kv.Value.Refresh(anchor);
            }

            SelfHealOnce();
        }

        private Transform AnchorOf(SimEntityId id)
        {
            if (_units.TryGet(id, out var view)) return view.transform;
            if (_units.TryGetQuad(id, out var quad)) return quad.transform;
            return null;
        }

        private bool TryRegistry(out StatusFxRegistry registry)
        {
            registry = _config != null ? _config.Registry : null;
            if (registry != null) return true;
            if (!_missingConfigLogged)
            {
                Debug.LogError("[CoreStatusFxSpawner] StatusFxConfig(registry) 미할당 — 상태 표식 스킵.", this);
                _missingConfigLogged = true;
            }
            return false;
        }

        // 초당 한 번. **정상 경로가 아니다**(헤더).
        private void SelfHealOnce()
        {
            if (Time.unscaledTime < _nextSweep) return;
            _nextSweep = Time.unscaledTime + 1f;

            _scratch.Clear();
            foreach (var k in _wanted)
                if (!_driver.IsAlive(new SimEntityId(k.Host))) _scratch.Add(k);
            for (int i = 0; i < _scratch.Count; i++)
            {
                Debug.LogWarning($"[CoreStatusFxSpawner] 유령 표식 회수 — 숙주 {_scratch[i].Host} 는 판에 없는데 "
                    + $"'{_scratch[i].Kind}' 표식이 남아 있었다. 소멸 사건을 안 낸 경로가 있다(계약 7).", this);
                DropHost(_scratch[i].Host);
            }
            _scratch.Clear();
        }

        // ── 풀 ───────────────────────────────────────────────────────────────
        // 종류별 풀이다 — 프리팹이 종류마다 달라 같은 종류로만 재사용한다(옛 규약).
        private CoreStatusFxView Rent(StatusFxKind kind)
        {
            if (_pool.TryGetValue(kind, out var q))
                while (q.Count > 0)
                {
                    var pooled = q.Dequeue();
                    if (pooled != null) return pooled;
                }
            var go = new GameObject("CoreStatusFx_" + kind);
            go.transform.SetParent(transform, false);
            return go.AddComponent<CoreStatusFxView>();
        }

        private void Recycle(StatusFxKind kind, CoreStatusFxView view)
        {
            if (view == null) return;
            view.Hide();
            if (!_pool.TryGetValue(kind, out var q))
            {
                q = new Queue<CoreStatusFxView>();
                _pool[kind] = q;
            }
            q.Enqueue(view);
        }

        public void Clear()
        {
            foreach (var kv in _active) if (kv.Value != null) Destroy(kv.Value.gameObject);
            _active.Clear();
            _wanted.Clear();
            _dots.Clear();
            foreach (var q in _pool.Values)
                while (q.Count > 0)
                {
                    var v = q.Dequeue();
                    if (v != null) Destroy(v.gameObject);
                }
            _pool.Clear();
        }
    }
}
