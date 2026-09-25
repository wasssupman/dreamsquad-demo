using System.Collections.Generic;
using Unity.Mathematics;
using Wassup.Skills;
using Wassup.BattleCore.Combat.Projectile;
using Wassup.BattleCore.Map;

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

        // 개체 **부분**의 풀(F4). 개체 자체는 `_pool` 이, 부분은 이쪽이 돌려쓴다 —
        // 어그로 획득·스폰이 틱 중에 도는 일이라 `new` 를 그대로 두면 쓰레기가 쌓인다.
        private readonly UnitPartPool _parts = new UnitPartPool();

        /// <summary>부분을 빌리는 곳. 부착 지점(커맨드·소환·도발)이 전부 여기를 지난다.</summary>
        public UnitPartPool Parts => _parts;

        // unit 7a — 규칙 등록부. 조립 지점(`BattleMatch`)이 한 번 꽂는다 — 없으면(테스트의 맨 월드) 규칙이 없다.
        private Trigger.BindingRegistry _bindings;
        internal void BindRegistry(Trigger.BindingRegistry registry)
        {
            _bindings = registry;
            registry?.Bind(this);
        }

        /// <summary>id 오름차순 개체 목록. 순회 중 구조 변경 금지 — 소멸은 틱 단계가 모아서 한다.</summary>
        public IReadOnlyList<Unit> Units => _units;

        // unit 2 — 판 위에 깔린 장(포탈·당김·아군 버프). 유닛이 아니라 여기 산다(UML §2).
        // 이동이 매 틱 읽고, 수명은 unit 6b 가 갖는다(`TickProjectilePhase` 끝 = 이동 뒤).
        //
        // unit 6b — **목록을 직접 못 고친다.** 계약 7(「모든 소멸은 소멸 이벤트를 낸다」)이
        // 유닛·탄에만 걸려 있으면 뷰가 장만 폴링으로 지켜보게 된다. 문은 아래 둘뿐이다.
        private readonly List<FieldCarrier> _fields = new List<FieldCarrier>(8);
        public IReadOnlyList<FieldCarrier> Fields => _fields;

        // unit 6b — 판 위에 깔린 존 장판. 장과 같은 자리에 살지만 **정의표 줄을 가리킨다**
        // (저작이 여럿이고 효과 배열이 그 줄에 있다). 순회는 발급 순서 = `SimEntityId` 오름차순.
        private readonly List<Hazard> _hazards = new List<Hazard>(8);
        private readonly Stack<Hazard> _hazardPool = new Stack<Hazard>(8);
        public IReadOnlyList<Hazard> Hazards => _hazards;

        /// <summary>
        /// 존 장판 하나를 깐다. 반드시 `HazardSpawned` 를 낸다(소멸의 짝).
        /// **판정은 없다** — 「어디에 깔 수 있나」는 까는 자(unit 7)의 질문이다.
        /// </summary>
        public Hazard SpawnHazard(int defIndex, int2 cell, float3 center, int radiusTiles, float lifetime,
                                  SimEntityId source, Faction faction, byte targetLayers, int tick)
        {
            var h = _hazardPool.Count > 0 ? _hazardPool.Pop() : new Hazard();
            h.Reset();
            h.Id = new SimEntityId(_nextId++);
            h.DefIndex = defIndex;
            h.OriginCell = cell;
            h.RadiusTiles = radiusTiles;
            h.Center = center;
            h.Remaining = lifetime;
            h.Source = source;
            h.Faction = faction;
            h.TargetLayers = targetLayers;

            _hazards.Add(h);   // id 단조 증가 → append 가 곧 오름차순
            _bus.Publish(CoreEvent.HazardSpawned(tick, h));
            return h;
        }

        /// <summary>
        /// **존의 유일한 제거 경로.** 반드시 `HazardDestroyed` 를 낸다.
        /// 사건은 **빼기 전에** 값을 읽어 만든다 — `Reset` 뒤에 읽으면 자리가 0 으로 샌다.
        /// </summary>
        public bool DestroyHazard(SimEntityId id, int tick)
        {
            for (int i = 0; i < _hazards.Count; i++)
            {
                if (_hazards[i].Id != id) continue;
                var h = _hazards[i];
                var ev = CoreEvent.HazardDestroyed(tick, h);
                _hazards.RemoveAt(i);
                h.Reset();
                _hazardPool.Push(h);
                _bus.Publish(ev);
                return true;
            }
            return false;
        }

        // unit 6b2 — **판 위에 놓인 먹을 것(픽업)과 떨어진 사직서.** 존과 같은 자리에 살고 같은
        // 규율이다: 목록을 직접 못 고치고, 문은 아래 넷뿐이며 **문마다 사건이 난다**(계약 7).
        // 순회는 발급 순서 = `SimEntityId` 오름차순(계약 5).
        private readonly List<Pickup> _pickups = new List<Pickup>(8);
        private readonly Stack<Pickup> _pickupPool = new Stack<Pickup>(8);
        public IReadOnlyList<Pickup> Pickups => _pickups;

        private readonly List<Resignation> _resignations = new List<Resignation>(8);
        private readonly Stack<Resignation> _resignationPool = new Stack<Resignation>(8);
        public IReadOnlyList<Resignation> Resignations => _resignations;

        /// <summary>픽업 하나를 놓는다. 반드시 `PickupSpawned` 를 낸다. 자리 검증은 `PickupSpawn` 이 한다.</summary>
        public Pickup SpawnPickup(PickupKind kind, int2 cell, float3 center, float lifetime, int tick)
        {
            var p = _pickupPool.Count > 0 ? _pickupPool.Pop() : new Pickup();
            p.Reset();
            p.Id = new SimEntityId(_nextId++);
            p.Kind = kind;
            p.Cell = cell;
            p.Center = center;
            p.Remaining = lifetime;
            p.SpawnTick = tick;
            _pickups.Add(p);
            _bus.Publish(CoreEvent.PickupSpawned(tick, p));
            return p;
        }

        /// <summary>
        /// **픽업의 유일한 제거 경로.** `taker` 가 있으면 `PickupTaken`(먹혔다), 없으면
        /// `PickupExpired`(수명 만료) — 어느 쪽이든 사건이 난다(계약 7). 사건은 빼기 전에 만든다.
        /// </summary>
        public bool RemovePickup(SimEntityId id, Unit taker, int tick)
        {
            for (int i = 0; i < _pickups.Count; i++)
            {
                if (_pickups[i].Id != id) continue;
                var p = _pickups[i];
                var ev = taker != null ? CoreEvent.PickupTaken(tick, p, taker) : CoreEvent.PickupExpired(tick, p);
                _pickups.RemoveAt(i);
                p.Reset();
                _pickupPool.Push(p);
                _bus.Publish(ev);
                return true;
            }
            return false;
        }

        /// <summary>
        /// 사직서 한 장을 떨어뜨린다. 반드시 `ResignationDropped` 를 내고, 그 사건이 **떨어뜨린 뒤의
        /// 판 위 장수**를 값으로 나른다(HUD 가 되묻지 않게).
        /// </summary>
        public Resignation DropResignation(int2 cell, float3 center, SimEntityId source, Faction faction, int tick)
        {
            var r = _resignationPool.Count > 0 ? _resignationPool.Pop() : new Resignation();
            r.Reset();
            r.Id = new SimEntityId(_nextId++);
            r.Cell = cell;
            r.Center = center;
            r.Source = source;
            r.Faction = faction;
            _resignations.Add(r);
            _bus.Publish(CoreEvent.ResignationDropped(tick, r, _resignations.Count));
            return r;
        }

        /// <summary>
        /// 사직서를 **가장 오래된 것부터** `count` 장 소모한다(`SimEntityId` 오름차순 = 떨어진 순서).
        /// 장마다 `ResignationConsumed` 가 난다(계약 7). 반환 = 실제로 소모한 장수.
        /// </summary>
        public int ConsumeResignations(int count, int tick)
        {
            int n = count < _resignations.Count ? count : _resignations.Count;
            for (int i = 0; i < n; i++)
            {
                var r = _resignations[0];
                var ev = CoreEvent.ResignationConsumed(tick, r);
                _resignations.RemoveAt(0);
                r.Reset();
                _resignationPool.Push(r);
                _bus.Publish(ev);
            }
            return n;
        }

        /// <summary>
        /// 장 하나를 깐다. 반드시 `FieldSpawned` 를 낸다. 인자가 아니라 **완성된 개체**를 받는
        /// 이유: 종류마다 읽는 필드가 달라(포탈 = 출구 · 당김 = 속도 · 아군 버프 = 스탯) 공용
        /// 인자 목록을 만들면 절반이 언제나 의미 없는 0 이 된다.
        /// </summary>
        public FieldCarrier SpawnField(FieldCarrier field, int tick)
        {
            if (field == null) return null;
            field.Id = new SimEntityId(_nextId++);
            _fields.Add(field);
            _bus.Publish(CoreEvent.FieldSpawned(tick, field));
            return field;
        }

        /// <summary>**장의 유일한 제거 경로.** 반드시 `FieldDespawned` 를 낸다.</summary>
        public bool DespawnField(SimEntityId id, int tick)
        {
            for (int i = 0; i < _fields.Count; i++)
            {
                if (_fields[i].Id != id) continue;
                var ev = CoreEvent.FieldDespawned(tick, _fields[i]);
                _fields.RemoveAt(i);
                _bus.Publish(ev);
                return true;
            }
            return false;
        }

        // unit 2 — 어그로 **요청** 줄. 「누가 누구에게 끌렸다」를 말하는 것은 히트를 낸 쪽
        // (unit 3 의 공격 루프)과 도발을 건 쪽(unit 7)이고, **게이트·추격판·이벤트는 여기**가 한다.
        // 요청과 부착을 나누는 이유: 수용량·선점 판정이 한 틱 안에서 일관되어야 하는데,
        // 생산자가 곧바로 붙이면 「먼저 온 쪽이 이긴다」가 생산자 순서에 매인다.
        private readonly List<AggroRequest> _aggroRequests = new List<AggroRequest>(16);
        public List<AggroRequest> AggroRequests => _aggroRequests;

        // unit 3 — 날아가는 것들. 유닛과 **같은 id 공간**을 쓴다(한 판 안에서 번호를 재사용하지
        // 않는다는 계약이 개체 종류를 가리지 않기 때문이다).
        private readonly List<Projectile> _projectiles = new List<Projectile>(32);
        private readonly Dictionary<int, Projectile> _projById = new Dictionary<int, Projectile>(32);
        private readonly Stack<Projectile> _projPool = new Stack<Projectile>(32);
        public IReadOnlyList<Projectile> Projectiles => _projectiles;

        // unit 3 — **요청 줄 셋.** 전부 「사실」이 아니라 「해 달라」이고, 게이트는 받는 쪽이 갖는다.
        //   · 발사 — 공격 루프가 넣고 `TickProjectilePhase` 가 소비한다(한 틱에 같은 주체가
        //     서로 독립인 발사를 여러 개 낼 수 있다 — 옛 캐리어 엔티티가 나르던 규칙).
        //   · 군중 제어 — 부여 측이 넣고 슬롯 적용은 unit 6 이 한다.
        //   · 기상 — 피격으로 잠을 깨우는 요청. **그 틱에 새로 걸린 수면은 대상에서 뺀다**(C9).
        private readonly List<ProjectileRequest> _projectileRequests = new List<ProjectileRequest>(16);
        public List<ProjectileRequest> ProjectileRequests => _projectileRequests;

        private readonly List<CcRequest> _ccRequests = new List<CcRequest>(16);
        public List<CcRequest> CcRequests => _ccRequests;

        /// <summary>
        /// 군중 제어 요청을 넣는 **유일한 문**. 생산자가 넷(공격 히트·광역 착탄·스윕 넉백·
        /// 기상)이라 각자 거르면 언젠가 하나가 빠진다 — 실제로 F3(「거점은 전면 면역」)이
        /// 옛 전투에서 진입 가드 셋에 흩어져 있었고 그래서 새 경로마다 구멍이 열렸다.
        /// 자격 판정은 `EffectEligibility` 하나이고, 못 받는 대상의 요청은 **버린다**.
        /// </summary>
        public bool RequestCc(in CcRequest req)
        {
            if (!EffectEligibility.AcceptsCc(Find(req.Target), req.Kind)) return false;
            _ccRequests.Add(req);
            return true;
        }

        private readonly List<WakeRequest> _wakeRequests = new List<WakeRequest>(8);
        public List<WakeRequest> WakeRequests => _wakeRequests;

        /// <summary>
        /// unit 6b2 — **스택 누적 요청.** 주기 바인딩(unit 7 — 번아웃 피로)이 넣고, 소비는
        /// `TickProjectilePhase` 의 **스탯 적용 뒤** 단계다. 그 자리 때문에 여기서 쌓은 피로가
        /// **한 틱 뒤에** 임계를 본다 — 옛 `FatigueAccrualSystem`(`[UpdateAfter(ModifierApplySystem)]`)의
        /// 1프레임 지연을 **단계 위치로** 박제한 것이다(rev 3 §4 「변경 없음」). 주기 바인딩이
        /// `[Periodic]` seam(장 준비 끝)에서 곧바로 스택을 더하면 그 지연이 사라져 밸런스가 바뀐다.
        /// </summary>
        private readonly List<Effects.StackAccrual> _stackAccruals = new List<Effects.StackAccrual>(8);
        public List<Effects.StackAccrual> StackAccruals => _stackAccruals;

        /// <summary>
        /// 실드를 건다. 반환 = **실제로 걸렸나.**
        ///
        /// ⚠ **이미 더 센 실드가 있으면 다시 걸지도 않고 사건도 안 낸다**(F20). 병합이
        /// 최댓값이라 그 경우 무동작인데, 연출만 나가면 화면에서 헛발동으로 보인다.
        /// 비교 대상은 **셋 다**다: 이미 든 슬롯 · 스테이징된 것(다음 틱 드레인 대기) ·
        /// 이번 틱에 쌓인 것. 부여가 한 틱 늦게 들어서 «걸었는데 아직 슬롯에 없는» 구간이
        /// 있고, 그 구간만 빼먹으면 약한 재부여가 그때만 통과해 헛발동이 난다.
        ///
        /// ⚠ **실드는 시간으로 사라지지 않는다.** 만료 경로가 «구조적으로 없는 것»이
        /// 파열 판정(합 &gt; 0 → 0)의 전제다 — 수명을 열면 「아무도 안 때렸는데 파열이
        /// 터진다」가 된다. 그래서 이 함수에 지속 인자가 없다.
        ///
        /// ⚠ 부여는 **한 틱 늦게** 든다(C17 · unit 3 결정). `ShieldPending` 에 쌓이고
        /// 틱 끝에서 `Shield` 로 옮겨진다 — 그 비대칭을 만드는 자리는 `Inbox.StageShield` 다.
        /// </summary>
        public bool GrantShield(SimEntityId target, SimEntityId source, float amount, int tick)
        {
            if (amount <= 0f) return false;
            var u = Find(target);
            if (u == null || u.Dead) return false;

            float already = Combat.ShieldMath.ValueFromSource(u.Shield.Slots, source);
            already = Highest(u.Inbox.Shield, source, already);
            already = Highest(u.Inbox.ShieldPending, source, already);
            if (amount <= already) return false;

            u.Inbox.ShieldPending.Add(new ShieldGrant { Source = source, Amount = amount });
            _bus.Publish(CoreEvent.ShieldGranted(tick, u, source, amount));
            return true;
        }

        private static float Highest(List<ShieldGrant> grants, SimEntityId source, float best)
        {
            for (int i = 0; i < grants.Count; i++)
                if (grants[i].Source == source && grants[i].Amount > best) best = grants[i].Amount;
            return best;
        }

        public int Count => _units.Count;

        /// <summary>
        /// unit 4 — 스폰 순번. 측면 분산의 레인 배정이 이 값을 쓴다(RNG 없음).
        ///
        /// ⚠ **가변 상태다**(X25 보류). 「N번째 스폰이 어디냐」를 알려면 앞의 N−1 을 재생해야
        /// 한다는 뜻이고, 스냅샷 부분 재시뮬·리플레이 점프의 전제 조건이 그래서 아직 없다.
        /// 현행 의미를 유지하는 것이 이 unit 의 결정이고, 순번 파생 전환은 후속 후보다.
        /// 스폰 경로가 셋(디버그·웨이브·보너스)이라 카운터를 **한 곳**에 둔다 —
        /// 각자 들면 같은 문에서 나온 적들이 서로 겹친다.
        /// </summary>
        public int SpawnOrdinal;

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
            u.Reset(_parts);
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
            // unit 7a — 저작 규칙은 **스폰 한 자리**에서 붙는다(배치·웨이브·소환·디버그 — 경로가 몇이든).
            // 「어떤 경로로 태어났나」가 규칙을 바꾸지 않는 것이 이 자리의 뜻이다.
            _bindings?.AttachAuthored(u, tick);
            return u;
        }

        /// <summary>
        /// **거점 하나를 세운다**(마음 타워 · 본능 · 적 마음). 스폰하는 쪽이 둘이라
        /// (`HeartMeter` 가 마음을, 장 준비 단계가 저작 거점을) 조립은 여기 한 곳이다 —
        /// 두 벌이면 「어느 경로로 섰나」가 몸 반경·점유를 바꾼다.
        ///
        /// 계약 셋을 이 함수가 **구조로** 진다:
        ///   · 몸 = 점유의 내접원(`StructureSize.BodyRadius`) — 제약 13 의 «대상의 몸».
        ///   · `cell` 은 **중심 칸**이다(옛 저작과 같다). 앵커(min 코너)는 여기서 파생한다.
        ///   · `healthExternal` 이면 `maxHealth` 를 **안 싣는다** — 그 체력은 담당자의 것이고
        ///     개체에 미러를 만드는 순간 둘이 갈린다(X29).
        ///
        /// ⚠ 이동 상태(`Move`)를 안 붙인다 = 거점은 **움직이지 않는다**. 그리고 장애물
        /// 수집이 `Kind == Defender` 만 막으므로 거점은 **통행을 막지 않는다**(점유만) —
        /// 「본능 footprint 는 벽」이라는 옛 계약은 2026-08-12 에 폐기됐다.
        /// </summary>
        public Unit SpawnStructure(Faction faction, int defIndex, int2 cell, float3 position,
                                   int footprint, float maxHealth, bool healthExternal, int tick)
        {
            int side = math.max(1, footprint);
            var u = Spawn(UnitKind.Structure, faction, defIndex, position,
                          StructureSize.BodyRadius(side),
                          healthExternal ? 0f : maxHealth, deploying: false, tick: tick);
            u.HealthExternal = healthExternal;
            u.Footprint = _parts.RentFootprint();
            u.Footprint.Anchor = new int2(cell.x - side / 2, cell.y - side / 2);
            u.Footprint.Width = side;
            u.Footprint.Height = side;
            return u;
        }

        /// <summary>
        /// 탄 하나를 만든다. 유닛과 같은 번호 발급기를 쓰고 `ProjectileSpawned` 를 낸다.
        /// 값은 전부 **발사 시점 스냅샷**이다 — 쏘고 나면 사수 스탯이 변해도 탄은 안 변한다.
        /// </summary>
        public Projectile SpawnProjectile(int tick)
        {
            var p = _projPool.Count > 0 ? _projPool.Pop() : new Projectile();
            p.Reset();
            p.Id = new SimEntityId(_nextId++);
            _projectiles.Add(p);          // id 단조 증가 → append 가 곧 오름차순
            _projById[p.Id.Value] = p;
            return p;
        }

        public Projectile FindProjectile(SimEntityId id)
            => _projById.TryGetValue(id.Value, out var p) ? p : null;

        /// <summary>
        /// **유일한 제거 경로.** 목록·사전에서 빼고, 풀에 돌려주고, 소멸 사건을 낸다.
        /// 유닛과 탄이 **같은 함수**를 쓰는 이유: 두 번째 제거 경로를 만들면 계약 7
        /// (「모든 소멸은 소멸 이벤트를 낸다」)이 종류마다 따로 지켜져야 하고, 그러면
        /// 언젠가 한쪽이 조용히 빠진다.
        /// 이미 없는 id 는 false(중복 소멸은 사건이 아니다).
        /// </summary>
        public bool Destroy(SimEntityId id, int tick)
        {
            if (_projById.TryGetValue(id.Value, out var proj)) return DestroyProjectile(proj, tick);
            if (!_byId.TryGetValue(id.Value, out var u)) return false;

            // 진행형 상태가 열린 채 사라지는 경로(유출 · 디버그 제거 등)도 닫힘을 알린다(계약 7).
            // 죽음·퇴근은 그 앞에서 이미 자기 사유로 닫았으므로 여기서는 아무 일도 안 일어난다.
            InterruptProgress(u, ProgressInterrupt.OwnerDestroyed, tick);
            // unit 7a — 소유자가 사라진다 = 규칙 전부가 떨어진다(사건 1건씩). **리셋 전**이어야 목록이 있다.
            _bindings?.OnOwnerRemoved(u, tick);

            // 소멸 이벤트는 **빼기 전에** 값을 읽어 만든다 — `Reset` 뒤에 읽으면 자리도
            // 몸 반경도 0 으로 새어 조용히 좁아진다(제약 13 의 사망 스냅샷과 같은 함정).
            var ev = CoreEvent.Destroyed(tick, u);

            _byId.Remove(id.Value);
            int index = IndexOf(id);
            if (index >= 0) _units.RemoveAt(index);   // RemoveAt 은 순서를 유지한다

            u.Reset(_parts);
            _pool.Push(u);

            _bus.Publish(ev);
            return true;
        }

        // ── 진행형 상태의 끝 ──────────────────────────────────────────────────
        //
        // 라스트런 창이 닫히는 문은 **둘뿐**이다 — 시간 끝(crash)과 중단 정책. 둘 다 여기를 지나고
        // 닫힘 사건은 `PublishLastRunEnded` 한 곳에서만 난다. 문을 소비처(전투·퇴근·제거)로 흩으면
        // 언젠가 한쪽이 사건을 빠뜨리고, 그 경로로 닫힌 창의 표식은 판이 끝날 때까지 떠 있는다.

        /// <summary>
        /// 중단 정책을 이행하고, 그 중단이 라스트런 창을 닫았으면 닫힘 사건을 낸다.
        /// 정책(무엇이 무엇을 멈추나)은 `ProgressiveStates.Interrupt` 가 소유한다.
        /// </summary>
        public void InterruptProgress(Unit u, ProgressInterrupt reason, int tick)
        {
            if (u.Progressive == null || !u.Progressive.Interrupt(reason)) return;
            PublishLastRunEnded(u, EndReasonOf(reason), tick);
        }

        /// <summary>라스트런 창의 시간이 끝났다. crash 피해는 부른 쪽(단계)이 인박스에 넣는다.</summary>
        public void CrashLastRun(Unit u, int tick)
        {
            var pg = u.Progressive;
            if (pg == null || !pg.LastRunActive) return;
            pg.LastRunActive = false;
            PublishLastRunEnded(u, LastRunEndReason.Crash, tick);
        }

        private void PublishLastRunEnded(Unit u, LastRunEndReason reason, int tick)
            => _bus.Publish(CoreEvent.LastRunEnded(tick, u, reason));

        private static LastRunEndReason EndReasonOf(ProgressInterrupt reason)
        {
            switch (reason)
            {
                case ProgressInterrupt.Death: return LastRunEndReason.Death;
                case ProgressInterrupt.Retire: return LastRunEndReason.Retire;
                default: return LastRunEndReason.Removed;   // OwnerDestroyed(군중 제어는 창을 안 닫는다)
            }
        }

        private bool DestroyProjectile(Projectile p, int tick)
        {
            // 소멸 이벤트는 **빼기 전에** 값을 읽어 만든다 — 자리도 원점 몸도 0 으로 새면
            // 뷰가 착탄 연출을 엉뚱한 곳에 튼다(유닛 쪽과 같은 함정).
            var ev = CoreEvent.ProjectileDespawned(tick, p);

            _projById.Remove(p.Id.Value);
            int index = IndexOfProjectile(p.Id);
            if (index >= 0) _projectiles.RemoveAt(index);

            p.Reset();
            _projPool.Push(p);

            _bus.Publish(ev);
            return true;
        }

        private int IndexOfProjectile(SimEntityId id)
        {
            int lo = 0, hi = _projectiles.Count - 1;
            while (lo <= hi)
            {
                int mid = (lo + hi) >> 1;
                int v = _projectiles[mid].Id.Value;
                if (v == id.Value) return mid;
                if (v < id.Value) lo = mid + 1; else hi = mid - 1;
            }
            return -1;
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
            // 날아가는 것도 상태다 — 빼면 「이벤트는 같은데 탄 위치가 갈렸다」를 못 잡는다.
            // 탄이 없는 판은 이 루프가 한 번도 안 돌아 unit 1·2 의 지문이 그대로 유지된다.
            for (int i = 0; i < _projectiles.Count; i++)
            {
                var p = _projectiles[i];
                h = Fnv(h, p.Id.Value);
                h = Fnv(h, (int)p.Movement);
                h = Fnv(h, (int)p.Payload);
                h = Fnv(h, Quantize(p.Position.x));
                h = Fnv(h, Quantize(p.Position.y));
                h = Fnv(h, Quantize(p.Position.z));
                h = Fnv(h, Quantize(p.Elapsed));
                h = Fnv(h, Quantize(p.Damage));
            }
            // unit 6b — 판 위에 깔린 것도 상태다. 빼면 「이벤트는 같은데 장판이 하나 더 남아
            // 있다」를 못 잡는다. 깔린 것이 없는 판은 두 루프가 한 번도 안 돌아 앞 unit 들의
            // 지문이 그대로 유지된다(탄 루프와 같은 규율).
            for (int i = 0; i < _hazards.Count; i++)
            {
                var z = _hazards[i];
                h = Fnv(h, z.Id.Value);
                h = Fnv(h, z.DefIndex);
                h = Fnv(h, z.OriginCell.x);
                h = Fnv(h, z.OriginCell.y);
                h = Fnv(h, Quantize(z.Remaining));
                h = Fnv(h, z.TargetLayers);
            }
            for (int i = 0; i < _fields.Count; i++)
            {
                var f = _fields[i];
                h = Fnv(h, f.Id.Value);
                h = Fnv(h, (int)f.Kind);
                h = Fnv(h, Quantize(f.Center.x));
                h = Fnv(h, Quantize(f.Center.z));
                h = Fnv(h, Quantize(f.Range));
                h = Fnv(h, Quantize(f.Duration));
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
