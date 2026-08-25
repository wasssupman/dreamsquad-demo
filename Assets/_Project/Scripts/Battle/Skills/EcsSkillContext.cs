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
            float tileSize, int2 gridSize, float3 origin)
        {
            _em = em;
            _transform = transform; _faction = faction;
            _enemyTag = enemyTag; _defTag = defTag;
            _attack = attack; _health = health; _pathFollow = pathFollow;
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
            if (id != SimEntityId.Unassigned) _byId[id] = e;
        }

        public void BindCcSink(NativeQueue<Wassup.Battle.Effects.EnemyCcEvent> q, bool has)
        {
            _ccQueue = q; _hasCcQueue = has;
        }

        public void BindStatSink(NativeQueue<Wassup.Battle.Effects.StatModifierApplyEvent> q, bool has)
        {
            _statQueue = q; _hasStatQueue = has;
        }

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
            if (!_attack.HasComponent(e)) return 0f;
            var a = _attack[e];
            switch (stat)
            {
                case UnitStat.AttackRange: return a.range;
                case UnitStat.AttackTargetCount: return a.attackTargetCount;
                case UnitStat.TargetTraversalLayers: return a.targetTraversalLayers;
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
            var centerCell = CellOfPosition(center);
            var casterEntity = Resolve(caster.Unit);

            int n = 0;
            for (int i = 0; i < pool.Length && n < into.Length; i++)
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
                if (SimIdOf(e) == SimEntityId.Unassigned) continue;

                var p = poolXf[i].Position;
                bool inRange;
                if (metric == RangeMetric.Chebyshev)
                {
                    var cell = CellOfPosition(p);
                    inRange = Wassup.Battle.Combat.TileAoe.IsInTileRange(cell, centerCell, tileRange);
                }
                else
                {
                    float dx = p.x - center.x, dz = p.z - center.z;
                    float r = tileRange * _tileSize;
                    inRange = dx * dx + dz * dz <= r * r;
                }
                if (!inRange) continue;

                into[n++] = Handle(e);
            }
            return n;
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
                        },
                    });
                    return;
                }
                case SimIntentKind.ApplyStatModifier:
                {
                    if (!_hasStatQueue) return;
                    var target = Resolve(intent.Target);
                    if (target == Entity.Null) return;
                    _statQueue.Enqueue(new Wassup.Battle.Effects.StatModifierApplyEvent
                    {
                        target = target,
                        stat = (Wassup.Battle.Effects.StatKind)intent.Selector,
                        op = (Wassup.Battle.Effects.CombineOp)intent.Op,
                        magnitude = intent.Amount,
                        duration = intent.Duration,
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
                case SimIntentKind.Report:
                {
                    // 문장은 여기서 만든다 — 도메인은 코드만 보낸다.
                    if (intent.Report == SkillReport.NoLandingSpot)
                        UnityEngine.Debug.LogWarning(
                            "[Skill] 착지점 해석 실패로 발동 skip — 상대 진영 앵커가 없거나 " +
                            "밀집 셀 주변 링 안에 갈 수 있는 칸이 없다. 임계는 소모됐고 재시도는 없다.");
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
                        impact = intent.Position,
                        damage = intent.Amount,
                        impactTileRange = intent.TileRange,
                        flightTime = intent.Duration,
                        dataIndex = intent.DataIndex,
                        // 저작이 0 이면 1 — 원본 arm 의 규약이다.
                        visualScale = intent.HitThreshold > 0f ? intent.HitThreshold : 1f,
                        owner = owner,
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

        public void Emit(in MetaIntent intent) => throw NotWired($"Emit(MetaIntent.{intent.Kind})");

        // 조용한 no-op 이 아니라 loud 거절. 배선 누락이 「스킬이 안 나가는데 아무도
        // 모르는」 상태로 가지 않게 한다(레지스트리의 fail-closed 와 같은 판단).
        private static NotSupportedException NotWired(string verb)
            => new NotSupportedException(
                $"[EcsSkillContext] '{verb}' 는 아직 배선되지 않았다. " +
                "이 동사를 처음 요구하는 migration unit 에서 채운다 — " +
                "그때 그것을 쓰는 concrete 와 그물이 같이 온다.");
    }
}
