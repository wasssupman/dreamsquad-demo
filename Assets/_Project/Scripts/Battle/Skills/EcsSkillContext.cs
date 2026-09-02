using System;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using Wassup.Battle.Movement;
using Wassup.Battle.Units;
using Wassup.Skills;

namespace Wassup.Battle.Skills
{
    // skill-layer-foundation unit 3 — 포트의 ECS 어댑터.
    //
    // 도메인이 물어보는 것을 컴포넌트에서 읽어 주고, 방출한 의도를 소유 맥락 채널에
    // 넣는다. **도메인은 이 클래스의 존재를 모른다** — 그게 포트의 요점이다.
    //
    // ⚠ `SystemAPI` 를 쓰지 않는다. 그건 시스템 타입 안에서만 도는 source-generated
    // API 라 독립 클래스에서 호출할 수 없다(unit 0 실측). 호스트가 lookup 을 **주입**한다.
    //
    // ⚠ **구현하지 않은 동사는 loud 하게 거절한다.** 미리 다 채우면 아무도 안 지나가는
    // 코드가 400줄 생기고(제약 8) 검증도 안 된다. migration 이 그 동사를 처음 요구할 때
    // 그 unit 에서 채운다 — 그때 그것을 쓰는 concrete 와 그물이 같이 온다.
    public sealed class EcsSkillContext : ISkillContext
    {
        private EntityManager _em;
        private ComponentLookup<LocalTransform> _transform;
        private ComponentLookup<FactionTag> _faction;
        private ComponentLookup<AttackUnitTag> _enemyTag;
        private ComponentLookup<DefenderUnitTag> _defTag;
        private ComponentLookup<Wassup.Battle.Combat.AttackState> _attack;
        private ComponentLookup<Health> _health;
        // 통행 층 게이트가 쓴다 — 후보의 «어느 층으로 다니나» 는 Movement 소유 값이다.
        private ComponentLookup<Wassup.Battle.Movement.PathFollowState> _pathFollow;
        private ComponentLookup<Wassup.Battle.Units.HitRadius> _hitRadius;   // unit 14 — 몸 걸침

        // 격자 파라미터. 도메인은 셀↔월드 변환을 이름으로만 부르고 이 셋을 모른다.
        private float _tileSize;
        private int2 _gridSize;
        private float3 _origin;

        // 후보 풀 — 호스트가 프레임당 한 번 지어 공유한다(fire 당 재구축 금지, unit 0 계약).
        private NativeArray<Entity> _enemyPool, _defPool;
        // `SimEntityId` → Entity. 풀과 함께 프레임당 1회 지어진다(BindPools).
        private readonly System.Collections.Generic.Dictionary<int, Entity> _byId
            = new System.Collections.Generic.Dictionary<int, Entity>(256);
        private NativeArray<LocalTransform> _enemyPoolXf, _defPoolXf;

        // 구조 변경용 ECB. **어댑터가 재생하지 않는다** — 호스트 시스템이 자기
        // OnUpdate 끝에 Playback 한다(계약 3: 구조 변경은 소유 맥락이 수행).
        private EntityCommandBuffer _ecb;
        private bool _hasEcb;

        // 의도 싱크.
        private NativeQueue<Wassup.Battle.Effects.EnemyCcEvent> _ccQueue;
        private bool _hasCcQueue;
        private NativeQueue<Wassup.Battle.Effects.StatModifierApplyEvent> _statQueue;
        private bool _hasStatQueue;
        private NativeQueue<Wassup.Battle.Effects.StackModifierApplyEvent> _stackQueue;
        private bool _hasStackQueue;
        private NativeQueue<Wassup.Battle.Effects.DotApplyEvent> _dotQueue;
        private bool _hasDotQueue;
        private NativeQueue<Wassup.Battle.Combat.KnockupVisualEvent> _knockupQueue;
        private bool _hasKnockupQueue;
        private NativeQueue<Wassup.Battle.Effects.HazardSpawnRequest> _hazardQueue;
        private bool _hasHazardQueue;
        private NativeQueue<Wassup.Battle.Combat.Projectile.ProjectileHitEvent> _hitQueue;
        private bool _hasHitQueue;
        private NativeQueue<Wassup.Battle.Effects.ShieldGrantedEvent> _shieldVfxQueue;
        private bool _hasShieldVfxQueue;
        private NativeQueue<Wassup.Battle.Movement.BlinkRequestEvent> _blinkQueue;
        private bool _hasBlinkQueue;
        private NativeQueue<Wassup.Battle.Combat.BossLeapVisualEvent> _leapVfxQueue;
        private bool _hasLeapVfxQueue;
        private NativeQueue<Wassup.Battle.Combat.UltimateLeapVisualEvent> _ultVfxQueue;
        private bool _hasUltVfxQueue;
        private NativeQueue<Wassup.Battle.Effects.AggroAcquireEvent> _acquireQueue;
        private bool _hasAcquireQueue;

        // 격자 라우팅 — 착지 질의가 쓴다. 도메인은 이 구조를 모르고 질의로만 만난다.
        private Wassup.Battle.Effects.FlowFieldSingleton _ff;
        private bool _hasFf;

        public void Bind(
            EntityManager em,
            in ComponentLookup<LocalTransform> transform,
            in ComponentLookup<FactionTag> faction,
            in ComponentLookup<AttackUnitTag> enemyTag,
            in ComponentLookup<DefenderUnitTag> defTag,
            in ComponentLookup<Wassup.Battle.Combat.AttackState> attack,
            in ComponentLookup<Health> health,
            in ComponentLookup<Wassup.Battle.Movement.PathFollowState> pathFollow,
            in ComponentLookup<Wassup.Battle.Units.HitRadius> hitRadius,
            float tileSize, int2 gridSize, float3 origin)
        {
            _em = em;
            _transform = transform; _faction = faction;
            _enemyTag = enemyTag; _defTag = defTag;
            _attack = attack; _health = health; _pathFollow = pathFollow;
            _hitRadius = hitRadius;
            _tileSize = tileSize; _gridSize = gridSize; _origin = origin;
        }

        public void BindPools(
            NativeArray<Entity> enemyPool, NativeArray<LocalTransform> enemyPoolXf,
            NativeArray<Entity> defPool, NativeArray<LocalTransform> defPoolXf)
        {
            _enemyPool = enemyPool; _enemyPoolXf = enemyPoolXf;
            _defPool = defPool; _defPoolXf = defPoolXf;

            // 핸들 역변환 사전. 풀과 **같은 수명**이라 여기서 같이 짓는다 —
            // 따로 지으면 풀만 갱신되고 사전이 묵는 프레임이 생긴다.
            // 미발급(`Unassigned`)은 넣지 않는다: 여럿이 같은 키를 갖고, 넣으면
            // 「아무나 한 명」이 나온다(그 유령이 조준을 훔치는 증상이었다).
            _byId.Clear();
            for (int i = 0; i < enemyPool.Length; i++) Index(enemyPool[i]);
            for (int i = 0; i < defPool.Length; i++) Index(defPool[i]);
        }

        private void Index(Entity e)
        {
            int id = SimIdOf(e);
            if (id == SimEntityId.Unassigned) return;
            // ⚠ **충돌은 조용하면 안 된다**(ECS 리뷰 N-1). id 는 단일 카운터에서 나오니
            // 겹칠 일이 없어야 하지만, 겹치면 역변환이 «엉뚱한 엔티티»를 돌려주고
            // 그 오폭은 어디에도 안 뜬다. 예전 선형 스캔은 적 풀을 먼저 훑어
            // first-wins 였고 지금은 last-writer-wins 라 승자까지 뒤집힌다 —
            // 「어느 쪽이 이기나」를 정하는 대신 **겹쳤다는 사실 자체**를 신고한다.
            if (!_byId.TryAdd(id, e))
            {
                UnityEngine.Debug.LogError(
                    $"[SkillDispatch] SimEntityId {id} 가 둘 이상에 붙어 있다 — 핸들 역변환이 "
                    + "엉뚱한 엔티티를 돌려준다. 발급기(BattleBridge.AttachSimEntityId)를 확인하라.");
            }
        }

        public void BindCcSink(NativeQueue<Wassup.Battle.Effects.EnemyCcEvent> q, bool has)
        {
            _ccQueue = q; _hasCcQueue = has;
        }

        public void BindStatSink(NativeQueue<Wassup.Battle.Effects.StatModifierApplyEvent> q, bool has)
        {
            _statQueue = q; _hasStatQueue = has;
        }

        public void BindStackSink(NativeQueue<Wassup.Battle.Effects.StackModifierApplyEvent> q, bool has)
        {
            _stackQueue = q; _hasStackQueue = has;
        }

        public void BindDotSink(NativeQueue<Wassup.Battle.Effects.DotApplyEvent> q, bool has)
        {
            _dotQueue = q; _hasDotQueue = has;
        }

        public void BindKnockupSink(NativeQueue<Wassup.Battle.Combat.KnockupVisualEvent> q, bool has)
        {
            _knockupQueue = q; _hasKnockupQueue = has;
        }

        public void BindHazardSink(NativeQueue<Wassup.Battle.Effects.HazardSpawnRequest> q, bool has)
        {
            _hazardQueue = q; _hasHazardQueue = has;
        }

        // 빔은 큐가 없다 — 브리지의 프레젠터가 직접 여는 뷰 세션이라, 코스트·쿨다운과
        // 같은 델리게이트 형태로 넘긴다(어댑터가 뷰를 직접 만지지 않게).
        private System.Action<Entity, Entity, int, float> _beamSink;
        public void BindBeamSink(System.Action<Entity, Entity, int, float> sink) => _beamSink = sink;

        // 연출 채널. 시뮬 상태를 안 바꾸지만 「언제 트는가」는 스킬의 판단이라
        // 의도 어휘에 있다(`PlayVisual`).
        public void BindVisualSink(NativeQueue<Wassup.Battle.Combat.Projectile.ProjectileHitEvent> q, bool has)
        {
            _hitQueue = q; _hasHitQueue = has;
        }

        public void BindShieldVisualSink(NativeQueue<Wassup.Battle.Effects.ShieldGrantedEvent> q, bool has)
        {
            _shieldVfxQueue = q; _hasShieldVfxQueue = has;
        }

        public void BindEcb(EntityCommandBuffer ecb, bool has)
        {
            _ecb = ecb; _hasEcb = has;
        }

        public void BindBlinkSink(NativeQueue<Wassup.Battle.Movement.BlinkRequestEvent> q, bool has)
        {
            _blinkQueue = q; _hasBlinkQueue = has;
        }

        public void BindLeapVisualSink(NativeQueue<Wassup.Battle.Combat.BossLeapVisualEvent> q, bool has)
        {
            _leapVfxQueue = q; _hasLeapVfxQueue = has;
        }

        public void BindUltimateVisualSink(NativeQueue<Wassup.Battle.Combat.UltimateLeapVisualEvent> q, bool has)
        {
            _ultVfxQueue = q; _hasUltVfxQueue = has;
        }

        public void BindTauntSink(NativeQueue<Wassup.Battle.Effects.AggroAcquireEvent> q, bool has)
        {
            _acquireQueue = q; _hasAcquireQueue = has;
        }

        public void BindFlowField(in Wassup.Battle.Effects.FlowFieldSingleton ff, bool has)
        {
            _ff = ff; _hasFf = has;
        }

        // ── 핸들 변환 — 이 클래스가 유일한 번역자다 ──────────────────
        // 도메인 핸들은 `SimEntityId.value` 를 그대로 싣는다. 역변환은 풀 스캔인데,
        // 풀이 프레임당 한 번 지어지고 스킬 발동이 초당 수 회라 비용이 무시할 만하다
        // (unit 0 성능 실측: 최악 정렬 프레임 ~50발, 발당 <0.1ms).
        // ⚠ **프레임당 1회 사전을 짓고 O(1) 로 되찾는다.**
        //
        // 예전엔 호출마다 두 풀을 선형으로 훑었다. 리뷰 M5 가 그 비용이 어디서
        // 터지는지 짚었다 — 조준하는 스킬은 **후보마다** 위치를 물으므로
        // `64후보 × 풀 170` ≈ 1만 회 컴포넌트 조회가 발사 한 번에 들어간다.
        // (오늘 라이브가 안 뜨거운 것은 저작 덕이다: 조준이 필요한 저작은 전부
        //  배치 1회성이고, 주기 저작은 타겟 바인딩이라 후보를 안 모은다. 즉
        //  **저작이 바뀌면 바로 뜨거워지는** 자리다.)
        //
        // 사전을 여기 두는 이유: 이 비용은 `Position` 하나가 아니라 **모든 질의**가
        // 낸다. 호출처를 고치면 다음 concrete 가 같은 함정을 다시 판다.
        private Entity Resolve(SkillEntityId id)
        {
            if (!id.IsValid) return Entity.Null;
            return _byId != null && _byId.TryGetValue(id.Value, out var e) ? e : Entity.Null;
        }

        private int SimIdOf(Entity e)
            => _em.HasComponent<SimEntityId>(e)
                ? _em.GetComponentData<SimEntityId>(e).value
                : SimEntityId.Unassigned;

        private SkillEntityId Handle(Entity e)
            => e == Entity.Null ? SkillEntityId.None : new SkillEntityId(SimIdOf(e));

        // ── 질의: 자리 ──────────────────────────────────────────────
        public float3 Position(SkillEntityId id)
        {
            var e = Resolve(id);
            return _transform.HasComponent(e) ? _transform[e].Position : float3.zero;
        }

        public int2 CellOf(SkillEntityId id) => CellOfPosition(Position(id));

        public int2 CellOfPosition(float3 world)
            => GridMath.WorldToCell(world, _tileSize, _gridSize, origin: _origin);

        public float TileSize => _tileSize;

        public float3 CellCenter(int2 cell)
            => GridMath.CellToWorldCenter(cell, _tileSize, origin: _origin);

        public bool TryFacing(SkillEntityId id, out float2 dirXZ)
        {
            var e = Resolve(id);
            if (e != Entity.Null && _em.HasComponent<DeployedFacing>(e))
            {
                var v = _em.GetComponentData<DeployedFacing>(e).value;
                dirXZ = new float2(v.x, v.y);
                return true;
            }
            dirXZ = default;
            return false;
        }

        // ── 질의: 정체 ──────────────────────────────────────────────
        public Faction FactionOf(SkillEntityId id)
            => FactionQuery.Of(Resolve(id), in _faction, in _enemyTag, in _defTag);

        public float Health(SkillEntityId id)
        {
            var e = Resolve(id);
            return _health.HasComponent(e) ? _health[e].value : 0f;
        }

        public float MaxHealth(SkillEntityId id)
        {
            var e = Resolve(id);
            return _health.HasComponent(e) ? _health[e].max : 0f;
        }

        public float Stat(SkillEntityId id, UnitStat stat)
        {
            var e = Resolve(id);
            // ⚠ **공격 스탯이 아닌 질의가 먼저다**(unit 5b). 아래 게이트는 `AttackState`
            // 없는 유닛을 0 으로 접는데, 「얼마나 다쳤나」는 공격과 무관한 값이라
            // 그 게이트에 걸리면 안 때리는 아군이 전부 0 으로 동률이 된다.
            if (stat == UnitStat.EffectiveHpRatio)
            {
                if (!_health.HasComponent(e)) return 0f;
                var h = _health[e];
                float maxHp = h.max > 0f ? h.max : 1f;
                float shieldSum = _em.HasBuffer<Wassup.Battle.Units.ShieldSlot>(e)
                    ? Wassup.Battle.Units.ShieldMath.Sum(_em.GetBuffer<Wassup.Battle.Units.ShieldSlot>(e))
                    : 0f;
                // 실드 합산 규칙은 `ShieldMath` 가 소유한다 — 도메인은 비율만 본다.
                return (h.value + shieldSum) / maxHp;
            }
            if (!_attack.HasComponent(e)) return 0f;
            var a = _attack[e];
            switch (stat)
            {
                case UnitStat.AttackRange: return a.range;
                case UnitStat.AttackTargetCount: return a.attackTargetCount;
                case UnitStat.TargetTraversalLayers: return a.targetTraversalLayers;
                case UnitStat.KnockupVisualHeight:
                    return _em.HasComponent<Wassup.Battle.Combat.DefenderCcData>(e)
                        ? _em.GetComponentData<Wassup.Battle.Combat.DefenderCcData>(e).knockupVisualHeight : 0f;
                case UnitStat.KnockupHopSeconds:
                    return _em.HasComponent<Wassup.Battle.Combat.DefenderCcData>(e)
                        ? _em.GetComponentData<Wassup.Battle.Combat.DefenderCcData>(e).knockupOnHitSec : 0f;
                case UnitStat.AttackCooldownRemaining: return a.cooldownRemaining;
                default: throw NotWired($"Stat({stat})");
            }
        }

        public bool Has(SkillEntityId id, UnitPredicate pred)
        {
            var e = Resolve(id);
            if (e == Entity.Null) return false;
            switch (pred)
            {
                case UnitPredicate.Alive: return !_em.HasComponent<DeadTag>(e);
                case UnitPredicate.PendingDeployment: return _em.HasComponent<PendingDeployment>(e);
                case UnitPredicate.CanReceiveDamage: return _em.HasBuffer<IncomingDamage>(e);
                // 실드는 두 버퍼가 다 있어야 성립한다 — 슬롯(잔량)과 인박스(부여).
                case UnitPredicate.HasShieldBuffer:
                    return _em.HasBuffer<ShieldSlot>(e) && _em.HasBuffer<IncomingShield>(e);
                // ⚠ `Position()` 은 부재를 0 으로 접는다. 조준하는 스킬은 이걸 먼저 묻고
                // 없으면 발사를 취소한다 — 안 그러면 (0,0) 방향 탄이 조용히 나간다.
                case UnitPredicate.HasPosition: return _transform.HasComponent(e);
                // 가디언 표식. 어그로가 「누구에게」 붙는지가 이 용량에 매여 있다.
                case UnitPredicate.HasAggroCapacity:
                    return _em.HasComponent<Wassup.Battle.Effects.AggroCapacity>(e);
                // ⚠ 아래 둘은 오늘 소비자가 없지만 **페이크가 답하므로 여기도 답한다**
                // (ECS 리뷰 M-4). 한쪽만 답하면 그 술어를 쓰는 첫 concrete 가
                // EditMode 초록 / 라이브 예외가 된다 — 디스패처가 예외를 삼키고
                // 그 발동만 버리므로 증상이 「그 스킬만 안 나감」이다.
                case UnitPredicate.InUltimateLeap:
                    return _em.HasComponent<Wassup.Battle.Combat.UltimateLeapState>(e);
                case UnitPredicate.IsPathFollowing:
                    return _pathFollow.HasComponent(e);
                default: throw NotWired($"Has({pred})");
            }
        }

        public byte TraversalLayers(SkillEntityId id)
            => (byte)Stat(id, UnitStat.TargetTraversalLayers);

        public float ShieldValueFrom(SkillEntityId target, SkillEntityId source)
        {
            var t = Resolve(target);
            if (t == Entity.Null || !_em.HasBuffer<ShieldSlot>(t)) return 0f;
            return ShieldMath.ValueFromSource(_em.GetBuffer<ShieldSlot>(t), Resolve(source));
        }

        // ── 질의: 후보 ──────────────────────────────────────────────
        public int Opponents(CasterRef caster, float3 center, int tileRange,
                             CandidateFilter filter, RangeMetric metric, SkillEntityId[] into)
        {
            var wanted = FactionRelation.OpponentUnitsOf(caster.Faction);
            return Collect(wanted, caster, center, tileRange, filter, metric, into);
        }

        public int Allies(CasterRef caster, float3 center, int tileRange,
                          CandidateFilter filter, RangeMetric metric, SkillEntityId[] into)
        {
            var wanted = FactionRelation.AllyUnitsOf(caster.Faction);
            return Collect(wanted, caster, center, tileRange, filter, metric, into);
        }

        private int Collect(Faction wanted, CasterRef caster, float3 center, int tileRange,
                            CandidateFilter filter, RangeMetric metric, SkillEntityId[] into)
        {
            // 진영 미상은 **아무도 안 고른다**. 기본값을 한쪽으로 두면 미지정 시전자가
            // 늘 한 진영을 때리고 그 오폭이 조용하다(FactionRelation 과 같은 판단).
            if (wanted == Faction.None) return 0;

            var pool = wanted == Faction.EnemyUnit ? _enemyPool : _defPool;
            var poolXf = wanted == Faction.EnemyUnit ? _enemyPoolXf : _defPoolXf;
            var casterEntity = Resolve(caster.Unit);

            int n = 0;
            int dropped = 0;
            // ⚠ **넘친 후보를 센다**(재리뷰 M-4). 예전엔 `n < into.Length` 를 루프 조건에
            // 걸어 64번째에서 조용히 멈췄다 — 레거시 장판은 쿼리 순회라 상한이 없었으니
            // **밀집 웨이브에서만 조용히 줄어드는** 형태였다(효과는 나가고 개수만 준다).
            // 이제 끝까지 훑어 넘친 수를 세고 짖는다. 풀 크기는 유닛 수라 비용은 무시할 만하다.
            int overflow = 0;
            for (int i = 0; i < pool.Length; i++)
            {
                var e = pool[i];
                if ((filter & CandidateFilter.ExcludeSelf) != 0 && e == casterEntity) continue;
                if ((filter & CandidateFilter.ExcludeDead) != 0 && _em.HasComponent<DeadTag>(e)) continue;
                if ((filter & CandidateFilter.ExcludePendingDeployment) != 0
                    && _em.HasComponent<PendingDeployment>(e)) continue;
                if ((filter & CandidateFilter.ExcludeInUltimateLeap) != 0
                    && _em.HasComponent<Wassup.Battle.Combat.UltimateLeapState>(e)) continue;
                if ((filter & CandidateFilter.RequireDamageable) != 0
                    && !_em.HasBuffer<IncomingDamage>(e)) continue;
                // ⚠ 풀 쿼리는 `DefenderUnitTag + LocalTransform` 뿐이라 **Health 를 보증하지
                // 않는다**. 레거시 실드 셔틀은 쿼리에 `WithAll<Health>` 가 박혀 있어 공짜로
                // 걸렀는데, 그 쿼리가 사라지면서 게이트도 같이 사라졌다.
                if ((filter & CandidateFilter.RequireHealth) != 0
                    && !_health.HasComponent(e)) continue;
                // ⚠ **통행 층 게이트.** 빼면 «내가 못 때리는 층» 의 후보가 총구를
                // 가져가고, 그 탄은 게이트에 막혀 아무도 못 맞힌다 — 발사 연출만
                // 나가는 조용한 no-op 이다(근접 가디언이 하늘의 적을 겨누는 형태).
                if ((filter & CandidateFilter.MatchTraversalLayers) != 0)
                {
                    byte hostLayers = _attack.HasComponent(casterEntity)
                        ? _attack[casterEntity].targetTraversalLayers : (byte)0;
                    byte candLayers = _pathFollow.HasComponent(e)
                        ? _pathFollow[e].traversalLayers : (byte)0;
                    if (!Wassup.Data.PlacementLayers.CanTarget(hostLayers, candLayers)) continue;
                }

                // ⚠ **역변환 불가 후보는 내보내지 않는다.** 핸들이 `SimEntityId` 라
                // 미발급 엔티티는 도로 찾을 수 없고, 그러면 concrete 가 받는 것은
                // 「없음」이 아니라 **월드 원점에 선 유령**이다(`Position` 이 부재를 0 으로
                // 접는다). 실제로 그 유령이 부채꼴의 조준을 훔쳐 등 뒤를 쏘게 만들었다.
                // 후보에서 빼면 「후보 0 = 무발사」로 흘러 조용한 오폭이 사라진다.
                //
                // ⚠ **다만 조용히 빼지는 않는다**(ECS 리뷰 H-2). 이 이전이 만든 **새 실패
                // 모드**라서다 — 예전 arm 은 `Entity` 를 직접 들어 ID 를 안 봤다. 스폰
                // 경로가 발급을 하나 빠뜨리면 그 유닛은 도발도 안 당하고 조준 후보도 못
                // 되는데, 증상은 「그 적만 안 끌려온다」이고 원인 신호가 0이다.
                // 캐스터 쪽(`BuildCaster`)은 이미 경고를 내므로 후보 쪽도 대칭으로 낸다.
                if (SimIdOf(e) == SimEntityId.Unassigned) { dropped++; continue; }

                var p = poolXf[i].Position;
                // unit 14 (결정 4 폐기) — 멤버십 = **몸 걸침**: SDF(접지점, 도형) ≤ targetR.
                // 대상 몸이 도형에 걸치면 히트 — 사거리(unit 12)·티어(unit 13)와 **같은 몸**을
                // 본다(값 하나). 종전(중심점 in 도형)보다 넓게만 맞으므로 표기(칸 하이라이트)는
                // 무통보 관용과 동류로 이번에 안 바꾼다 — 도형 윤곽 표기는 후속 후보.
                float targetR = _hitRadius.HasComponent(e) ? _hitRadius[e].value : 0f;
                // ⚠ 역수 가드 — `AttackReach.InReach` 와 같은 관용구. 테스트 월드처럼 흐름장이
                // 없으면 `_tileSize` 가 0 으로 들어오고, 그대로 나누면 0/0 = NaN 이 모든 비교를
                // false 로 만들어 **광역이 통째로 빈다**(구 셀 경로는 clamp 가 우연히 삼켰다).
                float invT = _tileSize > 1e-6f ? 1f / _tileSize : 1f;
                bool inRange;
                if (metric == RangeMetric.Chebyshev)
                {
                    // 사각 도형 = **받은 center 그대로** 중심, 반폭 (range + 0.5)칸.
                    // 칸 조준 concrete(TileStatBurst 등)는 center 를 CellCenter 로 만들어
                    // 넘기므로 종전(셀 스냅)과 byte-identical 이다. 자기시전(말파이트 착지
                    // 충격 등)은 몸 중심 그대로 — 종전엔 CellOfPosition 스냅이 2×2 기하
                    // 중심(셀 경계 위)을 우상단 칸으로 밀어 도형이 **반 칸 치우쳤다**
                    // (unit 10 이 은퇴시킨 「대표 셀」과 같은 병 — unit 14 잔여 해소).
                    // 이동 캐스터(보스 광역)도 칸 경계에서 도형이 튀지 않게 된다.
                    inRange = Wassup.Skills.SkillMath.BodyOverlapsSquare(
                        (p.x - center.x) * invT, (p.z - center.z) * invT,
                        tileRange + Wassup.Skills.SkillMath.CellHalfWidthTiles, targetR);
                }
                else
                {
                    // 원 도형 — 반경 합(민코프스키). 중심은 시전 지점 그대로(양자화 없음).
                    inRange = Wassup.Skills.SkillMath.InBodyReach(
                        (p.x - center.x) * invT, (p.z - center.z) * invT,
                        tileRange, 0f, targetR);
                }
                if (!inRange) continue;

                if (n >= into.Length) { overflow++; continue; }
                into[n++] = Handle(e);
            }

            if (overflow > 0 && !_warnedOverflowThisDrain)
            {
                _warnedOverflowThisDrain = true;
                UnityEngine.Debug.LogWarning(
                    $"[SkillDispatch] 사거리 안 후보 {overflow}기가 상한({into.Length})에 걸려 잘렸다 — "
                    + "효과는 정상 개수만큼 나가고 **대상 수만 조용히 준다**. "
                    + "밀집 웨이브에서 광역기가 «덜 맞는» 형태로 보인다. concrete 의 MaxTargets 를 확인하라.");
            }

            // 드레인당 한 번만 짖는다 — 후보마다 짖으면 로그가 프레임을 잡아먹는다.
            if (dropped > 0 && !_warnedUnassignedThisDrain)
            {
                _warnedUnassignedThisDrain = true;
                UnityEngine.Debug.LogWarning(
                    $"[SkillDispatch] 후보 {dropped}기가 SimEntityId 미발급이라 제외됐다 — "
                    + "스킬 레이어에서 그 유닛들은 존재하지 않는다(조준·도발 대상 불가). "
                    + "스폰 지점의 ID 발급을 확인하라.");
            }
            return n;
        }

        // 드레인 경계에서 호스트가 내린다. 프레임마다 한 번은 짖되 매 후보마다는 안 짖는다.
        private bool _warnedUnassignedThisDrain;
        private bool _warnedOverflowThisDrain;
        public void ResetDrainWarnings()
        {
            _warnedUnassignedThisDrain = false;
            _warnedOverflowThisDrain = false;
        }

        // ── 질의: 격자 위의 판단 ────────────────────────────────────
        // 순수 코어(`DefenderDensity`)가 셀 배열을 인자로 받는다 — 그래서 이 질의가
        // 포트를 넘을 수 있다. 어댑터는 풀에서 셀을 뽑아 넘겨주기만 한다.
        public bool TryDensestOpponentCluster(CasterRef caster, int densityRadius, out int2 cell, out int count)
        {
            cell = default; count = 0;
            if (!_hasFf) return false;
            var wanted = FactionRelation.OpponentUnitsOf(caster.Faction);
            if (wanted == Faction.None) return false;

            var poolXf = wanted == Faction.EnemyUnit ? _enemyPoolXf : _defPoolXf;
            var cells = new NativeArray<int2>(poolXf.Length, Allocator.Temp);
            for (int i = 0; i < poolXf.Length; i++) cells[i] = CellOfPosition(poolXf[i].Position);
            bool ok = Wassup.Battle.Combat.DefenderDensity.TryFindDensestCell(
                cells, densityRadius, _ff.gridSize, out cell, out count);
            cells.Dispose();
            return ok;
        }

        public bool TryLandingCellNear(int2 desired, int maxRing, out int2 cell)
        {
            cell = default;
            if (!_hasFf) return false;
            return Wassup.Battle.Combat.BlinkMath.TryFindLandingCell(
                desired,
                _ff.DistSlot(Wassup.Battle.Effects.FlowFieldSingleton.PrimarySlot),
                _ff.gridSize, math.max(0, maxRing), out cell);
        }

        // ── 질의: 발사 명세 ─────────────────────────────────────────
        // 「방향이 비어 있으면 아직 조준되지 않은 것」이라는 판정은 **여기 지식**이다 —
        // 템플릿의 이동 바인딩과 direction 을 봐야 안다. 도메인은 결론만 받는다.
        //
        // ⚠ 이 함수와 아래 `EmitPattern` 드레인은 **같은 술어**(`NeedsAim`)를 쓴다.
        // 둘이 갈리면 도메인이 조준해 보낸 값을 어댑터가 버리거나(무방향 탄),
        // 조준이 실린 템플릿을 host 현재 위치로 덮는다(무타겟 방향 패턴이 깨진다).
        public PatternAimNeed AimNeedOfPattern(SkillEntityId host, int patternIndex)
        {
            if (!TryPattern(Resolve(host), patternIndex, out var pat)) return PatternAimNeed.Missing;
            return NeedsAim(pat.template) ? PatternAimNeed.NeedsAim : PatternAimNeed.Preaimed;
        }

        private bool TryPattern(Entity e, int patternIndex,
                                out Wassup.Battle.Combat.Projectile.Emission.PatternSlot pat)
        {
            pat = default;
            if (e == Entity.Null || patternIndex < 0) return false;
            if (!_em.HasBuffer<Wassup.Battle.Combat.Projectile.Emission.PatternSlot>(e)) return false;
            if (!_em.HasBuffer<Wassup.Battle.Combat.Projectile.Emission.EmitterInstance>(e)) return false;
            var pats = _em.GetBuffer<Wassup.Battle.Combat.Projectile.Emission.PatternSlot>(e);
            if (patternIndex >= pats.Length) return false;
            pat = pats[patternIndex];
            return true;
        }

        // 아직 조준되지 않은 방향 바인딩 템플릿인가. **저작은 origin·direction·maxDistance 를
        // 하나도 채우지 않는다** — 그래서 «방향이 비어 있다» 가 그 표식으로 성립한다.
        // 방향 스냅샷을 미리 실어 보내는 소비자(무타겟 방향 패턴)는 여기서 false 가 되어
        // 템플릿을 지킨다.
        private static bool NeedsAim(in Wassup.Battle.Combat.Projectile.ProjectileSpawnRequest t)
            => math.lengthsq(t.direction) < SkillAim.AimEpsilonSq
               && Wassup.Battle.Combat.Projectile.Emission.MovementBinding.Of(t.movement)
                  == Wassup.Battle.Combat.Projectile.Emission.BindingClass.Direction;

        // ── 의도 ────────────────────────────────────────────────────
        public void Emit(in SimIntent intent)
        {
            switch (intent.Kind)
            {
                case SimIntentKind.ApplyCc:
                {
                    if (!_hasCcQueue) return;
                    var target = Resolve(intent.Target);
                    if (target == Entity.Null) return;
                    _ccQueue.Enqueue(new Wassup.Battle.Effects.EnemyCcEvent
                    {
                        target = target,
                        effect = new Wassup.Battle.Effects.CcEffect
                        {
                            kind = (Wassup.Battle.Effects.CcKind)intent.Selector,
                            remainingTime = intent.Duration,
                            scalar = intent.Amount,
                            // ⚠ **밀쳐냄은 `vector` 로만 산다.** `MovementSystem` 은 `scalar` 를
                            // 안 읽는다(프로젝트 전체 소비자 0) — 여기서 벡터를 안 채우면
                            // 「행동만 잠기고 한 칸도 안 밀리는」 밀쳐냄이 된다. 조용하다:
                            // CC 는 붙고 지속도 맞고 그물의 「걸렸나」 단언도 통과한다.
                            // 실제로 그렇게 라이브 카드 하나를 죽였다(ECS 리뷰 C-1).
                            vector = (Wassup.Battle.Effects.CcKind)intent.Selector
                                     == Wassup.Battle.Effects.CcKind.Impulse
                                ? new float3(intent.DirectionXZ.x, 0f, intent.DirectionXZ.y) * intent.Amount
                                : float3.zero,
                        },
                    });
                    return;
                }
                case SimIntentKind.DealDamage:
                {
                    var victim = Resolve(intent.Target);
                    if (victim == Entity.Null || !_em.HasBuffer<IncomingDamage>(victim)) return;
                    // 인박스에 넣는다 — 정산은 소유 맥락(Units)이 자기 프레임 창에서 한다.
                    var inbox = _em.GetBuffer<IncomingDamage>(victim);
                    inbox.Add(new IncomingDamage { amount = intent.Amount });
                    return;
                }
                case SimIntentKind.ApplyDot:
                {
                    if (!_hasDotQueue) return;
                    var victim = Resolve(intent.Target);
                    if (victim == Entity.Null) return;
                    _dotQueue.Enqueue(new Wassup.Battle.Effects.DotApplyEvent
                    {
                        target = victim,
                        effect = new Wassup.Battle.Effects.DotEffect
                        {
                            // 원소 없음 = 오라 없음. 배치 도트에 원소를 주고 싶어지면
                            // 그때 저작 축을 신설한다(제약 8).
                            origin = Wassup.Battle.Effects.DotOrigin.OnPlace,
                            // ⚠ **틱당 피해지 DPS 가 아니다.**
                            scalar = intent.Amount,
                            tickInterval = intent.HitThreshold,
                            // 첫 틱 즉발(add-path 규약).
                            tickTimer = intent.HitThreshold,
                            remainingTime = intent.Duration,
                        },
                    });
                    return;
                }
                case SimIntentKind.DelaySelfAttack:
                {
                    var who = Resolve(intent.Target);
                    if (who == Entity.Null || !_attack.HasComponent(who)) return;
                    // ⚠ **`max` 다.** 이미 걸린 대기를 줄이지 않는다 — 줄이면 채널링이
                    // 오히려 공격을 앞당기는 자리가 된다(레거시 규칙 그대로).
                    // ⚠ **계약 3 폐쇄 목록의 직접 쓰기**(정본 = 토대 README 표). ECB 는 값을 지금
                    // 기록하고 나중에 재생해서 **읽고-고쳐-쓰기를 못 한다** — 그 사이의
                    // 다른 쓰기를 덮는다. 늘리려면 README 표와 `SkillAdapterDirectWriteTests`.
                    var atk = _em.GetComponentData<Wassup.Battle.Combat.AttackState>(who);
                    atk.cooldownRemaining = math.max(atk.cooldownRemaining, intent.Duration);
                    _em.SetComponentData(who, atk);
                    return;
                }
                case SimIntentKind.ApplyStatModifier:
                {
                    if (!_hasStatQueue) return;
                    var target = Resolve(intent.Target);
                    if (target == Entity.Null) return;
                    // ⚠ **저작 배율은 여기서 버킷으로 번역한다**(unit 3b). 규칙이
                    // 「배율 ≥ 1 은 가산 버킷에 `배율−1`」이라 자명하지 않고, 상한 계산이
                    // 그 선택에 매여 있다 — 두 벌이 되면 조용히 한 스택만큼 어긋난다.
                    // ⚠ 캐스트를 분기 **안**에서 한다 — `FromAuthoredMultiplier` 는
                    // `CombineOp` 의 정의역 밖 값이라 밖에서 캐스트하면 잠깐 무효값이 선다.
                    var op = default(Wassup.Battle.Effects.CombineOp);
                    float mag = intent.Amount;
                    float cap = 0f;
                    if (intent.Op == SkillCombineOp.FromAuthoredMultiplier)
                    {
                        Wassup.Battle.Effects.ModifierAuthoring.FromMultiplier(intent.Amount, out op, out mag);
                        // 상한도 같은 자리에서 — 저작 배율과 최대 중첩으로 계산한다.
                        cap = Wassup.Battle.Effects.ModifierAuthoring.StackCap(
                            intent.Amount, (int)intent.HitThreshold);
                    }
                    else op = (Wassup.Battle.Effects.CombineOp)intent.Op;
                    _statQueue.Enqueue(new Wassup.Battle.Effects.StatModifierApplyEvent
                    {
                        target = target,
                        stat = (Wassup.Battle.Effects.StatKind)intent.Selector,
                        op = op,
                        magnitude = mag,
                        magnitudeCap = cap,
                        // 저작이 「안 끝난다」를 표현하는 방법은 `<=0` 이다.
                        duration = intent.Duration > 0f ? intent.Duration : float.PositiveInfinity,
                        // ⚠ 병합 키 `(source, stat, op, stackId)` — 이 넷이 회수 가능성의
                        // 조건이다. 구성이 바뀌면 host 가 죽어도 버프가 안 풀린다.
                        source = Resolve(intent.Source),
                        stackId = (ushort)intent.StackId,
                        origin = (Wassup.Battle.Effects.ModifierOrigin)intent.Origin,
                    });
                    return;
                }
                case SimIntentKind.GrantShield:
                {
                    var target = Resolve(intent.Target);
                    if (target == Entity.Null || !_em.HasBuffer<IncomingShield>(target)) return;
                    // ⚠ **인박스 append 다.** 드레인 시점은 **seam 마다 다르고 그게 legacy 와
                    // 같다**: 주기 seam(#4대)은 같은 프레임 #36 에 흡수되고, 경계 seam(#45)은
                    // DamageApplication 이 앞이라 **다음 프레임**이다. 어느 쪽도 앞당기지 않는다.
                    _em.GetBuffer<IncomingShield>(target).Add(new IncomingShield
                    {
                        source = Resolve(intent.Source),
                        amount = intent.Amount,
                    });
                    return;
                }
                case SimIntentKind.BeginUltimateLeap:
                {
                    if (!_hasEcb) return;
                    var e = Resolve(intent.Target);
                    if (e == Entity.Null) return;
                    // ⚠ **함께 붙는다.** 잠금(LeapFlight)과 무적(UltimateLeapState)은
                    // 레이어가 갈리지만 수명이 하나다 — 어느 하나만 붙는 프레임이 있으면
                    // 잠금 없이 무적이거나 그 반대가 된다. 같은 ECB 라 원자적이다.
                    _ecb.AddComponent(e, new Wassup.Battle.Combat.UltimateLeapState
                    {
                        remaining = math.max(0.01f, intent.Duration),
                        landingCell = intent.Cell,
                        landingWorld = intent.Position,
                        slamDamage = intent.Amount,
                        slamTileRange = math.max(0, intent.TileRange),
                        projectileDataIndex = intent.DataIndex,
                    });
                    _ecb.AddComponent<Wassup.Battle.Combat.LeapFlight>(e);
                    return;
                }
                case SimIntentKind.PlayVisual
                    when (SkillVisualKind)intent.Selector == SkillVisualKind.KnockupHop:
                {
                    if (!_hasKnockupQueue) return;
                    var e = Resolve(intent.Target);
                    if (e == Entity.Null) return;
                    // ⚠ 심에서 넉업의 실체는 **짧은 스턴**이라 뷰가 `CcEffect.kind` 로는
                    // 일반 스턴과 구분할 수 없다 — 그래서 띄운 쪽이 대상을 직접 신호한다.
                    _knockupQueue.Enqueue(new Wassup.Battle.Combat.KnockupVisualEvent
                    {
                        target = e,
                        durationSec = intent.Duration,
                        height = intent.Amount,
                    });
                    return;
                }
                case SimIntentKind.PlayVisual
                    when (SkillVisualKind)intent.Selector == SkillVisualKind.Beam:
                {
                    if (_beamSink == null) return;
                    var src = Resolve(intent.Source);
                    var dst = Resolve(intent.Target);
                    if (dst == Entity.Null) return;
                    // 키가 «맞는 쪽» 이라 공격 세션(키 = 공격자)과 충돌하지 않는다.
                    _beamSink(src, dst, intent.DataIndex, intent.Duration);
                    return;
                }
                case SimIntentKind.PlayVisual
                    when (SkillVisualKind)intent.Selector == SkillVisualKind.UltimateAscend:
                {
                    if (!_hasUltVfxQueue) return;
                    var e = Resolve(intent.Source);
                    if (e == Entity.Null) return;
                    _ultVfxQueue.Enqueue(new Wassup.Battle.Combat.UltimateLeapVisualEvent
                    {
                        entity = e,
                        kind = Wassup.Battle.Combat.UltimateLeapVisualKind.Ascend,
                        world = intent.Position,
                        dataIndex = -1,
                    });
                    return;
                }
                case SimIntentKind.SpawnOrbitProjectile:
                {
                    if (!_hasEcb) return;
                    var orbOwner = Resolve(intent.Source);
                    var orbCarrier = _ecb.CreateEntity();
                    _ecb.AddComponent(orbCarrier, new Wassup.Battle.Combat.Projectile.ProjectileSpawnRequest
                    {
                        orbitPhase = intent.Phase,
                        movement = Wassup.Battle.Combat.Projectile.MovementKind.OrbitAroundPoint,
                        payload  = Wassup.Battle.Combat.Projectile.PayloadKind.PathHit,
                        origin   = intent.Position,   // 궤도 중심(발사 시점 고정)
                        impact   = intent.Position,
                        damage   = intent.Amount,
                        maxDistance = intent.Radius,  // 궤도 반경(스킬이 이미 월드로 환산했다)
                        speed    = intent.Speed,      // **각속도** — 나누기는 스킬이 했다
                        flightTime = intent.Duration, // 지속 초 → 수명
                        hitThreshold = intent.HitThreshold,
                        dataIndex = intent.DataIndex,
                        visualScale = intent.VisualScale > 0f ? intent.VisualScale : 1f,
                        owner = orbOwner,             // 위협 귀속
                        // ⚠ 진영 축을 안 싣는다 — **필요가 없어서**다(unit 8 리뷰 H-1).
                        // 한때 「PathHit 후보 풀이 AttackUnitTag 하드코딩이라 이 페이로드엔
                        // 진영 축이 없다」고 적혀 있었는데, 지금 그 풀은 양 진영이고
                        // `ProjectileHitSystem` 이 **주인**으로부터 상대 진영을 도출한다.
                        // 즉 진영은 owner 가 이미 나른다.
                        // `AttackUnitTag` 하드코딩이라 이 페이로드엔 진영 축이 없다.
                        targetTraversalLayers = intent.TargetTraversalLayers,
                    });
                    _ecb.AddComponent<Wassup.Battle.Combat.Projectile.ProjectileRequestCarrier>(orbCarrier);
                    return;
                }
                case SimIntentKind.ScaleKillReward:
                {
                    var marked = Resolve(intent.Target);
                    if (marked == Entity.Null || !_em.Exists(marked)) return;
                    // 보상 컴포넌트가 없는 적은 애초에 줄 것이 없다 — 만들지 않는다.
                    if (!_em.HasComponent<Wassup.Battle.Units.AwakeningReward>(marked)) return;
                    var reward = _em.GetComponentData<Wassup.Battle.Units.AwakeningReward>(marked);
                    // ⚠ **계약 3 폐쇄 목록의 직접 쓰기**(정본 = 토대 README 표).
                    // ⚠ **즉시 쓰기다**(ECB 아님). 표식은 그 적이 죽을 때 소비되는데
                    // 처치 이벤트가 enqueue 시점에 이 값을 복사하므로, 재생을 기다리면
                    // 같은 프레임에 죽는 적이 배율 없는 값을 싣는다.
                    reward.value = (int)UnityEngine.Mathf.Max(
                        1, UnityEngine.Mathf.RoundToInt(reward.value * intent.Amount));
                    _em.SetComponentData(marked, reward);
                    return;
                }
                case SimIntentKind.BeginDreamCocoon:
                {
                    // ⚠ ECB 를 안 쓰므로 `_hasEcb` 를 묻지 않는다(ECS 리뷰 LOW) —
                    // 물으면 ECB 미주입 호스트에서 잠이 조용히 안 걸린다.
                    // ⚠ **이 분기를 ECB 로 옮기면 가드를 다시 넣어야 한다**(재리뷰 LOW-F).
                    // 토대 계약 3(직접 쓰기 금지) 회수가 그 작업이다.
                    var sleeper = Resolve(intent.Target);
                    if (sleeper == Entity.Null || !_em.Exists(sleeper)) return;
                    // ⚠ **잠과 감시를 같이 붙인다 — 이것이 「개시」의 뜻이다.**
                    // 잠을 CC 큐로 보내면 한 프레임 늦게 도착해서, 그 사이에 맞으면 깨울
                    // 잠이 없어 파탄이 안 나고 감시만 남는다(공짜 버프). 그래서 여기서
                    // 즉시 쓰기로 나란히 놓는다 — 병합 규칙은 `CcEffectMerge` 가 소유한다.
                    Wassup.Battle.Effects.EffectSpawner.ApplyCc(_em, sleeper,
                        new Wassup.Battle.Effects.CcEffect
                        {
                            kind = Wassup.Battle.Effects.CcKind.Sleep,
                            remainingTime = intent.Duration,
                        });
                    // ⚠ **완주 타이머가 잠보다 Epsilon 만큼 짧다.** 그 차이가 「완주 프레임」과
                    // 「잠이 자연만료되는 프레임」이 겹치지 않게 하는 안전핀이고, 값의 주인은
                    // 그 상태를 굴리는 시스템이라 여기서 뺀다(도메인은 이 상수를 모른다).
                    // ⚠ **계약 3 폐쇄 목록의 직접 쓰기이고, 그중 유일한 «구조 변경»**(정본 = 토대 README 표).
                    // 위 `ApplyCc` 는 버퍼 append 라 예외가 아니다 — 예외는 이 한 줄이다.
                    _em.AddComponentData(sleeper, new Wassup.Battle.Effects.DreamCocoon
                    {
                        remaining = intent.Duration - Wassup.Battle.Effects.DreamCocoon.Epsilon,
                        stat = (Wassup.Battle.Effects.StatKind)intent.Selector,
                        mult = intent.Amount,
                        stackId = (ushort)intent.StackId,
                    });
                    return;
                }
                case SimIntentKind.StartLethalTimer:
                {
                    if (!_hasEcb) return;
                    var doomed = Resolve(intent.Target);
                    if (doomed == Entity.Null || !_em.Exists(doomed)) return;
                    // ⚠ **덮어쓴다.** 레거시가 `AddComponentData` 였고, 이중 부착은 bake 의
                    // preflight(`DcApplicability` 의 DuplicateState)가 앞에서 막는다 —
                    // 여기서 더하기로 바꾸면 그 거절이 무의미해지고 타이머가 늘어난다.
                    _ecb.AddComponent(doomed, new Wassup.Battle.Units.LethalTimer
                    {
                        remaining = intent.Duration,
                    });
                    return;
                }
                case SimIntentKind.GrantCharge:
                {
                    if (!_hasEcb) return;
                    var who = Resolve(intent.Target);
                    if (who == Entity.Null || !_em.Exists(who)) return;
                    // ⚠ **누적하지 않는다.** 레거시가 `AddComponent` 로 덮어썼고(v1 = 항상 1발),
                    // 여기서 더하기로 바꾸면 연타로 맞는 순간 충전이 쌓여 사양이 달라진다.
                    _ecb.AddComponent(who, new Wassup.Battle.Combat.NextAttackDoubleFire
                    {
                        charges = (int)intent.Amount,
                    });
                    return;
                }
                case SimIntentKind.Report:
                {
                    // 문장은 여기서 만든다 — 도메인은 코드만 보낸다.
                    if (intent.Report == SkillReport.NoLandingSpot)
                        UnityEngine.Debug.LogWarning(
                            "[Skill] 착지점 해석 실패로 발동 skip — 상대 진영 앵커가 없거나 " +
                            "밀집 셀 주변 링 안에 갈 수 있는 칸이 없다. 임계는 소모됐고 재시도는 없다.");
                    return;
                }
                case SimIntentKind.ApplyStack:
                {
                    if (!_hasStackQueue) return;
                    var victim = Resolve(intent.Target);
                    if (victim == Entity.Null) return;
                    _stackQueue.Enqueue(new Wassup.Battle.Effects.StackModifierApplyEvent
                    {
                        target = victim,
                        kind = (Wassup.Battle.Effects.StackKind)intent.Selector,
                        // 저작은 「몇 겹」이고 최소 1 이다(0 겹은 발동을 소모만 한다 —
                        // 그 판정은 concrete 가 이미 했다).
                        // 레거시와 같은 clamp — 무경계 캐스트는 256→0 wrap 으로 조용한 no-op 이 된다.
                        countDelta = (byte)math.clamp(intent.Count, 1, 255),
                        // ⚠ **상한은 저작이 아니라 스택 종류가 갖는다.** 유닛마다 다른
                        // 상한을 적는 게 아니라 「출혈은 몇 겹까지 쌓이나」가 스택의 성질이다.
                        maxStack = StackCap(intent.Selector),
                        perAppDuration = intent.Duration,
                        source = Resolve(intent.Source),
                    });
                    return;
                }
                case SimIntentKind.SpawnFieldCarrier:
                {
                    // ⚠ **즉시 스폰이다**(ECB 아님). 장판 뷰 등록부가 매 프레임 살아 있는
                    // 캐리어와 자기 목록을 맞추는데, 재생을 기다리면 시전한 프레임의
                    // 점등이 한 박자 늦는다.
                    switch ((SkillFieldKind)intent.Selector)
                    {
                        case SkillFieldKind.AllyBuff:
                            Wassup.Battle.Effects.EffectSpawner.SpawnAllyBuffField(
                                _em, intent.Cell, intent.TileRange,
                                (Wassup.Battle.Effects.StatKind)intent.Selector2,
                                intent.Amount, intent.Duration);
                            return;
                        case SkillFieldKind.Pull:
                            // 중심은 **월드 좌표**다 — `MovementSystem` 이 매 프레임 반경 안
                            // 적을 당길 때 셀이 아니라 거리를 본다.
                            Wassup.Battle.Effects.EffectSpawner.SpawnTornadoField(
                                _em, CellCenter(intent.Cell), intent.TileRange,
                                intent.Amount, intent.Duration);
                            return;
                        case SkillFieldKind.Portal:
                            // 입구 반지름은 **반 칸**이다 — 격자 값이라 어댑터가 안다.
                            Wassup.Battle.Effects.EffectSpawner.SpawnPortal(
                                _em, CellCenter(intent.Cell), CellCenter(intent.Cell2),
                                _tileSize * 0.5f, intent.Duration);
                            return;
                        default:
                            throw NotWired($"SpawnFieldCarrier({(SkillFieldKind)intent.Selector})");
                    }
                }
                case SimIntentKind.SpawnZoneCarrier:
                {
                    if (!_hasHazardQueue) return;
                    // 모양·반경·지속·효과·틱·뷰는 전부 해저드 저작 소유다 — 여기서 정하는 것은
                    // 「어디에 · 누구를 대상으로」뿐이고, 그래서 index 하나만 지나간다.
                    _hazardQueue.Enqueue(new Wassup.Battle.Effects.HazardSpawnRequest
                    {
                        // ⚠ **종류를 스킬이 정한다**(unit 5a). 0(=`None`)은 「저작이 종류를
                        // 안 말했다」라 존으로 읽는다 — 죽음 자리 장판이 그 경우다.
                        kind = (Wassup.Battle.Effects.HazardCastKind)intent.Selector
                                   == Wassup.Battle.Effects.HazardCastKind.Blocking
                               ? Wassup.Battle.Effects.HazardCastKind.Blocking
                               : Wassup.Battle.Effects.HazardCastKind.Zone,
                        dataIndex = intent.DataIndex,
                        centerCell = intent.Cell,
                        // ⚠ 「한 칸에」의 정직한 인코딩은 0 이 아니라 1 이다. 드레인은 이
                        // 필드를 안 읽지만(모양은 해저드 저작 소유), 0 을 흘리면 다음 사람이
                        // 「폭 없음」을 계약으로 읽는다. 죽음 자리 장판도 한 칸이다.
                        width = 1,
                        height = 1,
                        caster = Resolve(intent.Source),
                        // 드레인은 이 필드를 안 읽는다(계측·추적용). 실어 온 것만 그대로 넘긴다.
                        target = Resolve(intent.Target),
                        // ⚠ **발화 시점 사양이다.** 여기서 시전자를 다시 읽으면 동귀어진일 때
                        // 이미 파괴돼 0(= 무제한 통과)으로 샌다.
                        targetTraversalLayers = intent.TargetTraversalLayers,
                    });
                    return;
                }
                case SimIntentKind.Taunt:
                {
                    if (!_hasAcquireQueue) return;
                    var guardian = Resolve(intent.Source);
                    var victim = Resolve(intent.Target);
                    if (guardian == Entity.Null || victim == Entity.Null) return;
                    _acquireQueue.Enqueue(new Wassup.Battle.Effects.AggroAcquireEvent
                    {
                        guardian = guardian,
                        enemy = victim,
                        kind = Wassup.Battle.Effects.AggroAcquireKind.Taunt,
                        durationSec = intent.Duration,
                    });
                    return;
                }
                case SimIntentKind.EmitPattern:
                {
                    // ⚠ **성사와 카운터 전진은 여기서 원자다.** 전진(`fireCountBase`)과
                    // 인스턴스 추가가 같은 `if` 안에 있고 그 사이에 실패할 수 있는 것이
                    // 하나도 없다. 도메인은 「쏜다」만 말하고, 「쏘지 않는다」는 의도를
                    // 아예 안 보내는 것으로 말한다 — 그래서 「전진했는데 안 쏨」도
                    // 「쐈는데 전진 안 함」도 표현이 불가능하다.
                    var host = Resolve(intent.Source);
                    if (!TryPattern(host, intent.PatternIndex, out var pat)) return;

                    // spec/template 을 **값으로 복사**한다 — 발사 도중 무엇이 바뀌어도
                    // 이미 시작된 버스트는 불변이다.
                    var template = pat.template;
                    if (NeedsAim(template))
                    {
                        // 도메인이 정한 조준. `damage` 는 채우지 않는다 — emitter 가
                        // 명령값(패턴 SO)으로 항상 덮는다.
                        template.origin = intent.Position;
                        template.direction = intent.DirectionXZ;
                        // 사거리는 tile → world 변환이 **여기서** 일어난다. 도메인은
                        // 타일 수만 알고 tileSize 를 모른다.
                        template.maxDistance = intent.TileRange * _tileSize;
                    }

                    var inst = new Wassup.Battle.Combat.Projectile.Emission.EmitterInstance
                    {
                        spec = pat.spec,
                        template = template,
                        lockedTarget = Entity.Null,
                    };
                    Wassup.Battle.Combat.Projectile.Emission.EmitterTick.Begin(
                        ref inst.runtime, inst.spec, pat.fireCountBase);

                    // 영속시켜야 하는 것은 카운터 하나뿐이고, 그것만 durable 소유자
                    // (PatternSlot)에 남아 다음 발화가 이어받는다 — 안 그러면 선택
                    // 규칙이 고정된다.
                    pat.fireCountBase += pat.spec.shots.Length;
                    // ⚠ **계약 3 폐쇄 목록의 직접 쓰기**(정본 = 토대 README 표, 리뷰 M-1 이 찾아냈다).
                    // 인박스 append 가 아니라 **durable 버퍼 원소 덮어쓰기**이고, 바로 위에서
                    // `fireCountBase` 를 읽고 더한 값을 쓴다 — `DelaySelfAttack` 과 같은
                    // 읽고-고쳐-쓰기라 ECB 로 못 옮긴다.
                    var slots = _em.GetBuffer<Wassup.Battle.Combat.Projectile.Emission.PatternSlot>(host);
                    slots[intent.PatternIndex] = pat;
                    var instances =
                        _em.GetBuffer<Wassup.Battle.Combat.Projectile.Emission.EmitterInstance>(host);
                    instances.Add(inst);
                    return;
                }
                case SimIntentKind.Blink:
                {
                    if (!_hasBlinkQueue) return;
                    var e = Resolve(intent.Target);
                    if (e == Entity.Null) return;
                    // sim 은 **이번 프레임에** 텔레포트한다. 아치는 뷰의 일이다.
                    _blinkQueue.Enqueue(new Wassup.Battle.Movement.BlinkRequestEvent
                    {
                        entity = e,
                        destWorld = intent.Position,
                    });
                    return;
                }
                case SimIntentKind.PlayVisual
                    when (SkillVisualKind)intent.Selector == SkillVisualKind.LeapArc:
                {
                    if (!_hasLeapVfxQueue) return;
                    var e = Resolve(intent.Source);
                    if (e == Entity.Null) return;
                    _leapVfxQueue.Enqueue(new Wassup.Battle.Combat.BossLeapVisualEvent
                    {
                        entity = e,
                        fromWorld = intent.Position,
                        toWorld = CellCenter(intent.Cell),
                        dataIndex = intent.DataIndex,
                        // 슬램은 **뷰 도착 시점**에 터진다 — 그 타이밍을 이 채널이 소유한다.
                        slamDamage = intent.Amount,
                        slamTileRange = intent.TileRange,
                    });
                    return;
                }
                case SimIntentKind.SpawnProjectile
                    when intent.Target.IsValid:
                {
                    // ⚠ **대상이 있으면 「자리」가 아니라 「그 유닛」을 쫓는다.** 아래 자리
                    // 폭발과 같은 의도를 쓰는 이유는 둘 다 「탄 하나를 낸다」이기 때문이고,
                    // 갈리는 축은 대상 유무 하나다(의도 어휘의 원래 주석이 그렇게 적었다).
                    if (!_hasEcb) return;
                    var shooter = Resolve(intent.Source);
                    var victim = Resolve(intent.Target);
                    if (victim == Entity.Null) return;

                    var mv = (Wassup.Battle.Combat.Projectile.MovementKind)intent.ProjectileMovement;
                    // 방향 바인딩 궤적(왕복 = 부메랑)은 **타겟 엔티티를 안 잡는다** — 발사
                    // 시점의 대상 방향을 축으로 굳히고 거리로 산다. 재조준도 성립하지 않아
                    // 0 으로 명시한다(같은 필드가 두 뜻을 갖지 않게).
                    bool directional =
                        Wassup.Battle.Combat.Projectile.Emission.MovementBinding.Of(mv)
                        == Wassup.Battle.Combat.Projectile.Emission.BindingClass.Direction;

                    var carrier = _ecb.CreateEntity();
                    _ecb.AddComponent(carrier, new Wassup.Battle.Combat.Projectile.ProjectileSpawnRequest
                    {
                        movement = mv,
                        payload = (Wassup.Battle.Combat.Projectile.PayloadKind)intent.ProjectilePayload,
                        target = directional ? Entity.Null : victim,
                        origin = intent.Position,
                        // flat — 공격자 damageMul 을 안 태운다(계약 7).
                        damage = intent.Amount,
                        speed = intent.Speed,
                        hitThreshold = intent.HitThreshold,
                        visualScale = intent.VisualScale > 0f ? intent.VisualScale : 1f,
                        dataIndex = intent.DataIndex,
                        owner = shooter,
                        targetTraversalLayers = intent.TargetTraversalLayers,
                        // ⚠ **피해 풀 진영을 시전자에서 도출한다**(자리 폭발과 같은 규칙).
                        // 기본값이 Enemy 라 안 채우면 **적이 쏜 탄이 적을 때린다** — 레거시는
                        // 그 조합을 감지자에서 loud 거절해 막았는데, 라우팅이 그 가드보다
                        // 앞이라 이제 도달하지 않는다(ECS 리뷰 H-2).
                        targetFaction =
                            FactionQuery.OpponentsOf(shooter, in _faction, in _enemyTag, in _defTag)
                                == Faction.DefenderUnit
                                ? Wassup.Battle.Combat.Projectile.ProjectileTargetFaction.Defender
                                : Wassup.Battle.Combat.Projectile.ProjectileTargetFaction.Enemy,
                        // 같은 셀이면 축이 없다 — 0 을 그대로 보내 드레인이 loud 거절하게 둔다
                        // (조용히 임의 방향을 지어내면 저작 실수가 안 보인다).
                        direction = directional ? intent.DirectionXZ : float2.zero,
                        maxDistance = directional ? intent.TileRange * _tileSize : 0f,
                        retargetTileRange = directional ? 0 : intent.TileRange,
                    });
                    _ecb.AddComponent<Wassup.Battle.Combat.Projectile.ProjectileRequestCarrier>(carrier);
                    return;
                }
                case SimIntentKind.SpawnProjectile:
                {
                    if (!_hasEcb) return;
                    var owner = Resolve(intent.Source);
                    // 캐리어 = 요청을 나르는 엔티티. 브리지 드레인이 스폰 후 파괴한다.
                    // 구조 변경이라 ECB 로 스테이징하고, 재생은 호스트 몫이다.
                    var carrier = _ecb.CreateEntity();
                    _ecb.AddComponent(carrier, new Wassup.Battle.Combat.Projectile.ProjectileSpawnRequest
                    {
                        movement = Wassup.Battle.Combat.Projectile.MovementKind.SkyFall,
                        payload = Wassup.Battle.Combat.Projectile.PayloadKind.TileAoe,
                        // 예고를 걸 반경. 0 = 안 건다(죽음 자리 폭발·파열은 즉발이라 예고가 없다).
                        telegraphTileRange = intent.Telegraph ? intent.TileRange : 0,
                        impact = intent.Position,
                        damage = intent.Amount,
                        impactTileRange = intent.TileRange,
                        flightTime = intent.Duration,
                        dataIndex = intent.DataIndex,
                        // 저작이 0 이면 1 — 원본 arm 의 규약이다.
                        visualScale = intent.VisualScale > 0f ? intent.VisualScale : 1f,
                        owner = owner,
                        // ⚠ **시전자의 공격 층을 실어 보낸다**(skill-layer-migration unit 2a).
                        // 안 실으면 0 = 무제한이 되어 **근접 유닛의 폭발이 하늘의 적을 때린다.**
                        // 레거시 `MeleeBurst` arm 은 후보를 모으는 단계에서 이 마스크로 걸렀고,
                        // 광역 탄 경로는 후보를 안 모으므로 탄이 대신 들고 가야 한다.
                        // 보스 자폭이 여태 이 구멍을 안 밟은 건 보스가 전 층을 때려서다.
                        // ⚠ **재질의하지 않는다**(투트랙 리뷰 M-3 / HIGH-3). 예전엔 owner 를
                        // 다시 읽었는데 그러면 (a) 죽음 계열에서 시전자가 이미 파괴돼 0 으로
                        // 새고 (b) **concrete 가 「무제한」을 표현할 수 없다** — 0 을 보내도
                        // 재질의가 덮어써서 시체폭발이 조용히 좁아졌다.
                        //
                        // 이제 **0 = 무제한**이고 그게 `PlacementLayers.CanTarget` 의 뜻 그대로다.
                        // 「누구를 때리나」는 스킬의 판단이므로 concrete 가 정한다.
                        targetTraversalLayers = intent.TargetTraversalLayers,
                        // ⚠ 피해 풀 진영. 기본값이 Enemy 라 그냥 두면 **보스의 폭발이
                        // 자기 진영을 때린다**. caster 의 상대 진영에서 도출한다.
                        targetFaction =
                            FactionQuery.OpponentsOf(owner, in _faction, in _enemyTag, in _defTag)
                                == Faction.DefenderUnit
                                ? Wassup.Battle.Combat.Projectile.ProjectileTargetFaction.Defender
                                : Wassup.Battle.Combat.Projectile.ProjectileTargetFaction.Enemy,
                    });
                    _ecb.AddComponent<Wassup.Battle.Combat.Projectile.ProjectileRequestCarrier>(carrier);
                    return;
                }
                case SimIntentKind.PlayVisual
                    when (SkillVisualKind)intent.Selector == SkillVisualKind.ShieldGranted:
                {
                    if (!_hasShieldVfxQueue) return;
                    _shieldVfxQueue.Enqueue(new Wassup.Battle.Effects.ShieldGrantedEvent
                    {
                        position = intent.Position,
                    });
                    return;
                }
                case SimIntentKind.PlayVisual:
                {
                    if (!_hasHitQueue || intent.DataIndex < 0) return;
                    _hitQueue.Enqueue(new Wassup.Battle.Combat.Projectile.ProjectileHitEvent
                    {
                        position = intent.Position,
                        dataIndex = intent.DataIndex,
                        // ⚠ 원본 arm 이 쓰던 값과 같아야 한다 — 뷰가 이걸로 연출을 고른다.
                        payload = Wassup.Battle.Combat.Projectile.PayloadKind.SingleSplash,
                        source = Resolve(intent.Source),
                    });
                    return;
                }
                default:
                    throw NotWired($"Emit(SimIntent.{intent.Kind})");
            }
        }

        // ⚠ **판 밖 런타임은 큐를 안 탄다.** 코스트·쿨다운은 sim 상태가 아니라 Mono 쪽
        // 자원이고 계약이 「즉시 반영」이다(큐에 실으면 코스트 획득이 한 프레임 늦는다).
        // 그래서 **브리지가 넣어 준 델리게이트**로 곧장 간다 — 어댑터가 `GameManager` 를
        // 직접 부르면 제약 1(브리지 유일 창구)이 조용히 무너진다.
        // 스택 종류별 상한. **유닛이 아니라 스택의 성질**이라 저작 SO 가 권위이고,
        // 브리지가 그 목록을 풀어서 넣어 준다(도메인은 상한을 아예 모른다).
        // index = `Battle.Effects.StackKind`. 미등록은 producer 선례 기본값.
        private byte[] _stackCaps;
        public void BindStackCaps(byte[] byKind) => _stackCaps = byKind;

        private byte StackCap(int kind)
            => _stackCaps != null && kind >= 0 && kind < _stackCaps.Length && _stackCaps[kind] > 0
                ? _stackCaps[kind] : Wassup.Data.StackModifierSO.DefaultMaxStack;

        private System.Action<MetaIntent> _metaSink;
        public void BindMetaSink(System.Action<MetaIntent> sink) => _metaSink = sink;

        public void Emit(in MetaIntent intent)
        {
            if (_metaSink == null) throw NotWired($"Emit(MetaIntent.{intent.Kind})");
            _metaSink(intent);
        }

        // 조용한 no-op 이 아니라 loud 거절. 배선 누락이 「스킬이 안 나가는데 아무도
        // 모르는」 상태로 가지 않게 한다(레지스트리의 fail-closed 와 같은 판단).
        private static NotSupportedException NotWired(string verb)
            => new NotSupportedException(
                $"[EcsSkillContext] '{verb}' 는 아직 배선되지 않았다. " +
                "이 동사를 처음 요구하는 migration unit 에서 채운다 — " +
                "그때 그것을 쓰는 concrete 와 그물이 같이 온다.");
    }
}
