using System.Collections.Generic;
using Unity.Mathematics;
using Wassup.Battle.Units;

namespace Wassup.BattleCore
{
    // battle-core-rebuild unit 1 — 개체 목록. 계약 5·7 의 자리다.
    //
    // 계약 7 (**모든 소멸 경로는 소멸 이벤트를 낸다**): 목록에서 개체를 빼는 함수는
    // `Destroy(SimEntityId)` **하나뿐**이고 그 함수가 반드시 `UnitDestroyed` 를 낸다.
    // 이것이 뷰의 「매 프레임 생존 폴링」(옛 풀 3곳)을 은퇴시키는 근거다 — 폴링은
    // 「사라진 걸 나중에 눈치챈다」이고, 이벤트는 「사라질 때 말해 준다」다.
    // 두 번째 제거 경로를 만들고 싶어지면 그건 설계 결함 신호다. `Destroy` 를 부르면 된다.
    //
    // 계약 5 (**순회는 `SimEntityId` 오름차순**): id 는 단조 증가로 발급하므로 목록에
    // **append 하면 정렬이 유지된다**. 그래서 매 틱 정렬하지 않는다. 대신 그 불변식이
    // 깨지면 결정론이 조용히 죽으므로, 발급은 `_nextId++` 한 곳에서만 한다.
    public sealed class BattleWorld
    {
        private readonly EventBus _bus;

        private readonly List<Unit> _units;
        private readonly Dictionary<int, Unit> _byId;
        private readonly Stack<Unit> _pool;

        private int _nextId = SimEntityId.FirstSpawnValue;

        /// <summary>id 오름차순 개체 목록. 순회 중 구조 변경 금지 — 소멸은 틱 단계가 모아서 한다.</summary>
        public IReadOnlyList<Unit> Units => _units;

        // unit 2 — 판 위에 깔린 장(포탈·당김). 유닛이 아니라 여기 산다(UML §2).
        // 이동이 매 틱 읽고, 생성·수명은 효과 레이어(unit 6)가 갖는다.
        private readonly List<FieldCarrier> _fields = new List<FieldCarrier>(8);
        public List<FieldCarrier> Fields => _fields;

        // unit 2 — 어그로 **요청** 줄. 「누가 누구에게 끌렸다」를 말하는 것은 히트를 낸 쪽
        // (unit 3 의 공격 루프)과 도발을 건 쪽(unit 7)이고, **게이트·추격판·이벤트는 여기**가 한다.
        // 요청과 부착을 나누는 이유: 수용량·선점 판정이 한 틱 안에서 일관되어야 하는데,
        // 생산자가 곧바로 붙이면 「먼저 온 쪽이 이긴다」가 생산자 순서에 매인다.
        private readonly List<AggroRequest> _aggroRequests = new List<AggroRequest>(16);
        public List<AggroRequest> AggroRequests => _aggroRequests;

        public int Count => _units.Count;

        public BattleWorld(EventBus bus, int capacity)
        {
            _bus = bus;
            _units = new List<Unit>(capacity);
            _byId = new Dictionary<int, Unit>(capacity);
            _pool = new Stack<Unit>(capacity);
            for (int i = 0; i < capacity; i++) _pool.Push(new Unit());
        }

        public Unit Find(SimEntityId id)
            => _byId.TryGetValue(id.Value, out var u) ? u : null;

        public bool IsAlive(SimEntityId id) => _byId.ContainsKey(id.Value);

        /// <summary>
        /// 개체 하나를 만든다. id 는 스폰 순번이고 재사용하지 않는다.
        /// 반드시 `UnitSpawned` 를 낸다(소멸의 짝).
        /// </summary>
        public Unit Spawn(UnitKind kind, Faction faction, int defIndex,
                          float3 position, float hitRadius, float maxHealth, bool deploying, int tick)
        {
            var u = _pool.Count > 0 ? _pool.Pop() : new Unit();
            u.Reset();
            u.Id = new SimEntityId(_nextId++);
            u.Kind = kind;
            u.Faction = faction;
            u.DefIndex = defIndex;
            u.Position = position;
            u.HitRadius = hitRadius;
            u.MaxHealth = maxHealth;
            u.Health = maxHealth;
            u.Deploying = deploying;

            _units.Add(u);      // id 단조 증가 → append 가 곧 오름차순
            _byId[u.Id.Value] = u;

            _bus.Publish(CoreEvent.Spawned(tick, u));
            return u;
        }

        /// <summary>
        /// **유일한 제거 경로.** 목록·사전에서 빼고, 풀에 돌려주고, `UnitDestroyed` 를 낸다.
        /// 이미 없는 id 는 false(중복 소멸은 사건이 아니다).
        /// </summary>
        public bool Destroy(SimEntityId id, int tick)
        {
            if (!_byId.TryGetValue(id.Value, out var u)) return false;

            // 소멸 이벤트는 **빼기 전에** 값을 읽어 만든다 — `Reset` 뒤에 읽으면 자리도
            // 몸 반경도 0 으로 새어 조용히 좁아진다(제약 13 의 사망 스냅샷과 같은 함정).
            var ev = CoreEvent.Destroyed(tick, u);

            _byId.Remove(id.Value);
            int index = IndexOf(id);
            if (index >= 0) _units.RemoveAt(index);   // RemoveAt 은 순서를 유지한다

            u.Reset();
            _pool.Push(u);

            _bus.Publish(ev);
            return true;
        }

        // 목록이 오름차순이라 이분 탐색이 성립한다. 선형 탐색을 쓰면 소멸이 O(n),
        // 전멸 웨이브에서 O(n²) 가 된다.
        private int IndexOf(SimEntityId id)
        {
            int lo = 0, hi = _units.Count - 1;
            while (lo <= hi)
            {
                int mid = (lo + hi) >> 1;
                int v = _units[mid].Id.Value;
                if (v == id.Value) return mid;
                if (v < id.Value) lo = mid + 1; else hi = mid - 1;
            }
            return -1;
        }

        /// <summary>
        /// 결정론 지문. 골든의 `finalStateHash` 이고, 「이벤트는 같은데 상태가 갈렸다」를
        /// 잡는 유일한 장치다. 연속값은 트레이스와 **같은 해상도**(1e-3)로 접는다.
        /// </summary>
        public ulong StateHash()
        {
            ulong h = 1469598103934665603UL; // FNV-1a 64 offset basis
            for (int i = 0; i < _units.Count; i++)
            {
                var u = _units[i];
                h = Fnv(h, u.Id.Value);
                h = Fnv(h, (int)u.Kind);
                h = Fnv(h, (int)u.Faction);
                h = Fnv(h, Quantize(u.Position.x));
                h = Fnv(h, Quantize(u.Position.y));
                h = Fnv(h, Quantize(u.Position.z));
                h = Fnv(h, Quantize(u.HitRadius));
                h = Fnv(h, Quantize(u.Health));
                h = Fnv(h, (u.Dead ? 1 : 0) | (u.Deploying ? 2 : 0));
            }
            return h;
        }

        private static int Quantize(float v) => Wassup.Core.Trace.TraceEvent.Quantize(v);

        private static ulong Fnv(ulong h, int value)
        {
            unchecked
            {
                uint v = (uint)value;
                for (int b = 0; b < 4; b++)
                {
                    h ^= (byte)(v >> (b * 8));
                    h *= 1099511628211UL;
                }
                return h;
            }
        }
    }
}
