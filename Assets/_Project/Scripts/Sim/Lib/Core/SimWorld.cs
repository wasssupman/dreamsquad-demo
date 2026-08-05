using System;
using System.Collections.Generic;

namespace Wassup.Sim
{
    /// <summary>
    /// battle-sim-extraction unit 18-A — sim 엔티티 식별자.
    ///
    /// **매치 내 비재사용**이 계약이다(feature-wide: `SimEntityId`(spawnOrdinal)가 타겟팅 동률·
    /// RNG seed·커맨드·이벤트·스냅샷·뷰 키의 유일 축). 파괴된 id 를 다시 발급하면 뷰 키가
    /// 재사용돼 죽은 유닛의 연출이 새 유닛에 붙고, 동률 판정이 시간에 따라 뒤집힌다.
    /// </summary>
    public readonly struct SimEntityId : IEquatable<SimEntityId>
    {
        public readonly int Value;
        public SimEntityId(int value) { Value = value; }

        public static readonly SimEntityId Null = default;
        public bool IsNull => Value == 0;

        public bool Equals(SimEntityId o) => Value == o.Value;
        public override bool Equals(object o) => o is SimEntityId e && Equals(o);
        public override int GetHashCode() => Value;
        public static bool operator ==(SimEntityId a, SimEntityId b) => a.Value == b.Value;
        public static bool operator !=(SimEntityId a, SimEntityId b) => a.Value != b.Value;
        public override string ToString() => IsNull ? "sim:null" : "sim:" + Value;
    }

    internal interface ISimStore
    {
        bool Remove(int id);
    }

    internal sealed class SimStore<T> : ISimStore where T : struct
    {
        internal readonly Dictionary<int, T> Map = new Dictionary<int, T>();
        public bool Remove(int id) => Map.Remove(id);
    }

    internal sealed class SimBufferStore<T> : ISimStore where T : struct
    {
        internal readonly Dictionary<int, List<T>> Map = new Dictionary<int, List<T>>();
        public bool Remove(int id) => Map.Remove(id);
    }

    /// <summary>
    /// battle-sim-extraction unit 18-A — 신 sim 의 엔티티/컴포넌트 저장소.
    ///
    /// 구 ECS 의 아키타입 청크를 흉내내지 않는다. 필요한 성질은 셋뿐이다:
    /// **① 선택적 컴포넌트**(부재가 정상 상태 — 게이트 53개가 그 위에 서 있다)
    /// **② 결정적 순회 순서** **③ 지연 구조 변경**(청사진 ⑤).
    ///
    /// ⚠ **순회는 생성 순서다** — 사전(Dictionary) 순서가 아니다. 동률 5지점 중 두 곳
    /// (KillAttribution 등량 = 버퍼 적재 순서 · Aggro capacity FIFO)이 순회 순서에 걸려 있고,
    /// `SimEntityId` 가 생성 순서로 증가하므로 이 선택이 unit 1 의 동률 축(simId)과 일치한다.
    /// 사전 순회로 바꾸면 런타임마다 결과가 달라진다.
    ///
    /// ⚠ **파괴는 여기서 즉시 일어나지 않는다.** 청사진 ③ 의 사망 4단계 릴레이가 "죽었지만 아직
    /// 있는" 1틱 창을 요구한다 — 마킹(P9/P3)은 파괴하지 않고 파괴(P12)는 마킹하지 않는다.
    /// 그래서 <see cref="Destroy"/> 는 P12 의 파괴 루프만 부른다(호출 지점이 계약이다).
    /// </summary>
    public sealed class SimWorld
    {
        /// <summary>
        /// 매치 저작 스냅샷. **생성자가 요구한다** — 기본값 경로를 두면 조각이 config 를 관통시키지
        /// 않은 채로 컴파일되고, 그 결과가 "규칙이 없어서" 인지 "배선이 빠져서" 인지 구분되지 않는다
        /// (critic M5: `StackModifierTick` 이 6세션 동안 조용히 no-op 이 되는 경로).
        /// </summary>
        public SimConfig Config { get; }

        /// <summary>
        /// 이번 틱의 델타 시간. 구 sim 의 `SystemAPI.Time.DeltaTime`(=`World.Time`) 대응 —
        /// **시간은 시스템이 아니라 월드가 소유한다**는 배치를 그대로 옮긴 것이다.
        ///
        /// 18-A 는 시스템이 하나도 없어서 이 표면이 없었고, 18-C 의 P7 틱 계열(#28·#29·#32)이
        /// 처음 요구했다. **저장소·채널 표현은 건드리지 않는다** — 중단 기준 ③ 이 말하는
        /// 재설계 신호가 아니라 틱 골격의 누락분이다.
        ///
        /// 정책(고정 스텝·슬로모 처분)은 여기 없다 — unit 19 와 18-K 가 소유한다. 여기서는
        /// 값을 실어 나르기만 한다.
        /// </summary>
        public float DeltaTime { get; private set; }

        /// <summary>
        /// 프로덕션 호출자는 <see cref="SimTick.Run"/> 하나다(구 sim 에서 하네스가
        /// `World.SetTime` 뒤 그룹을 돌리던 것과 같은 배치). public 인 것은 틱 골격 없이
        /// 단일 시스템만 돌리는 테스트를 위해서다.
        /// </summary>
        public void SetDeltaTime(float dt) => DeltaTime = dt;

        public SimWorld(SimConfig config)
        {
            Config = config ?? throw new ArgumentNullException(nameof(config),
                "sim 은 저작 스냅샷 없이 만들 수 없다 — 배선 누락이 규칙 부재로 위장하는 것을 막는다.");
        }

        // id 0 은 Null 예약. **감소하지 않는다** — 파괴돼도 재사용 없음.
        private int _nextId = 1;
        private readonly List<int> _order = new List<int>();      // 생성 순서(파괴돼도 안 지운다)
        private readonly HashSet<int> _alive = new HashSet<int>();
        private readonly Dictionary<Type, ISimStore> _stores = new Dictionary<Type, ISimStore>();

        /// 발급된 총 개수(비재사용 계약의 관측점).
        public int SpawnedCount => _nextId - 1;
        public int AliveCount => _alive.Count;

        public SimEntityId Create()
        {
            int id = _nextId++;
            _order.Add(id);
            _alive.Add(id);
            return new SimEntityId(id);
        }

        public bool Exists(SimEntityId e) => !e.IsNull && _alive.Contains(e.Value);

        /// <summary>
        /// ⚠ **계약의 적용 범위가 18-A 의 초판보다 좁다**(18-E/3 실측 정정).
        ///
        /// 초판은 *"P12(UnitLifecycle)만 부른다"* 였다. 그건 **`DeadTag` 로 마킹된 유닛**에
        /// 대해서만 참이다 — 그 릴레이(마킹 #11/#34 → 관찰 P10 → 파괴 #41)의 1틱 창이
        /// 사라지면 사직서 드랍·순찰병 전파·DefenderDeath 베이크가 깨진다(청사진 ③ §3).
        ///
        /// 그러나 구 sim 에는 **수명 만료 파괴자가 P1 에 둘 더 있다** —
        /// `HazardLifetimeSystem`(#2)과 `ObstacleLifetimeSystem`(#6)이다. 그 둘은 릴레이에
        /// 참여하지 않는다(마킹 없이 즉시 파괴). 같은 해저드가 **피해로** 죽는 경로는 별개이고
        /// 그건 `DeadTag` 를 거쳐 #41 로 간다.
        ///
        /// ⇒ 정확한 계약: **`DeadTag` 를 가진 엔티티를 파괴하는 것은 #41 뿐이다.**
        /// 수명 만료(#2·#6)는 자기 phase 에서 즉시 파괴한다.
        /// </summary>
        public void Destroy(SimEntityId e)
        {
            if (!_alive.Remove(e.Value)) return;
            foreach (var store in _stores.Values) store.Remove(e.Value);
            // `_order` 에서는 지우지 않는다 — 순회가 생존 검사로 거르고, 지우면 O(n) 이동이 생긴다.
        }

        // ── 컴포넌트 ──────────────────────────────────────────────────────────
        private SimStore<T> Store<T>() where T : struct
        {
            if (!_stores.TryGetValue(typeof(T), out var s)) _stores[typeof(T)] = s = new SimStore<T>();
            return (SimStore<T>)s;
        }

        public bool Has<T>(SimEntityId e) where T : struct
            => Exists(e) && Store<T>().Map.ContainsKey(e.Value);

        public T Get<T>(SimEntityId e) where T : struct
            => Store<T>().Map.TryGetValue(e.Value, out var v) ? v : default;

        public bool TryGet<T>(SimEntityId e, out T value) where T : struct
        {
            if (Exists(e)) return Store<T>().Map.TryGetValue(e.Value, out value);
            value = default;
            return false;
        }

        /// 없으면 추가, 있으면 덮어쓴다(ECS 의 Add/Set 구분은 신 sim 에서 의미가 없다).
        public void Set<T>(SimEntityId e, in T value) where T : struct
        {
            if (!Exists(e)) return;
            Store<T>().Map[e.Value] = value;
        }

        public bool RemoveComponent<T>(SimEntityId e) where T : struct
            => Store<T>().Map.Remove(e.Value);

        // ── 버퍼 ──────────────────────────────────────────────────────────────
        private SimBufferStore<T> BufferStore<T>() where T : struct
        {
            if (!_stores.TryGetValue(typeof(List<T>), out var s))
                _stores[typeof(List<T>)] = s = new SimBufferStore<T>();
            return (SimBufferStore<T>)s;
        }

        public bool HasBuffer<T>(SimEntityId e) where T : struct
            => Exists(e) && BufferStore<T>().Map.ContainsKey(e.Value);

        /// <summary>
        /// ⚠ **부재와 빈 버퍼는 다른 상태다.** 게이트 중 `DamageApplication` 은 버퍼 **부재**만
        /// 보고(청사진 ② 함의 보존 3건), 빈 버퍼는 통과시킨다. 그래서 조회가 자동 생성하지 않는다.
        /// </summary>
        public List<T> GetBuffer<T>(SimEntityId e) where T : struct
            => BufferStore<T>().Map.TryGetValue(e.Value, out var l) ? l : null;

        public List<T> AddBuffer<T>(SimEntityId e) where T : struct
        {
            if (!Exists(e)) return null;
            var map = BufferStore<T>().Map;
            if (!map.TryGetValue(e.Value, out var l)) map[e.Value] = l = new List<T>();
            return l;
        }

        // ── 순회 ──────────────────────────────────────────────────────────────
        /// <summary>
        /// **생성 순서**로 살아 있는 엔티티를 훑는다. 순회 중 <see cref="Create"/>/<see cref="Destroy"/>
        /// 가 일어나도 안전하도록 인덱스 기반이다 — 다만 청사진 ⑤ 계약상 구조 변경은
        /// <see cref="SimCommandBuffer"/> 로 미루는 것이 원칙이고, 이 안전성은 그 원칙의 보조다.
        /// </summary>
        public IEnumerable<SimEntityId> Entities()
        {
            for (int i = 0; i < _order.Count; i++)
            {
                int id = _order[i];
                if (_alive.Contains(id)) yield return new SimEntityId(id);
            }
        }

        /// `T` 를 가진 엔티티만. 순서는 <see cref="Entities"/> 와 같다(생성 순서).
        public IEnumerable<SimEntityId> With<T>() where T : struct
        {
            var map = Store<T>().Map;
            for (int i = 0; i < _order.Count; i++)
            {
                int id = _order[i];
                if (_alive.Contains(id) && map.ContainsKey(id)) yield return new SimEntityId(id);
            }
        }

        /// <summary>
        /// `T` **버퍼**를 가진 엔티티만. 구 ECS 의 `SystemAPI.Query&lt;DynamicBuffer&lt;T&gt;&gt;()`
        /// 대응이다 — 컴포넌트 쿼리(<see cref="With{T}"/>)와 **다른 축**이라는 점이 중요하다.
        /// `StackModifierTick` 은 버퍼만 요구하고 `StatModifierTick` 은 컴포넌트도 요구하는데,
        /// 그 차이를 표현할 수 없으면 이식이 조건을 조용히 좁힌다.
        ///
        /// 빈 버퍼도 포함한다(**부재 ≠ 빈 버퍼**).
        /// </summary>
        public IEnumerable<SimEntityId> WithBuffer<T>() where T : struct
        {
            var map = BufferStore<T>().Map;
            for (int i = 0; i < _order.Count; i++)
            {
                int id = _order[i];
                if (_alive.Contains(id) && map.ContainsKey(id)) yield return new SimEntityId(id);
            }
        }
    }
}
