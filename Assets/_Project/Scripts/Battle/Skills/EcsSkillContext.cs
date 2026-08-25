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

        // 격자 파라미터. 도메인은 셀↔월드 변환을 이름으로만 부르고 이 셋을 모른다.
        private float _tileSize;
        private int2 _gridSize;
        private float3 _origin;

        // 후보 풀 — 호스트가 프레임당 한 번 지어 공유한다(fire 당 재구축 금지, unit 0 계약).
        private NativeArray<Entity> _enemyPool, _defPool;
        private NativeArray<LocalTransform> _enemyPoolXf, _defPoolXf;

        // 의도 싱크.
        private NativeQueue<Wassup.Battle.Effects.EnemyCcEvent> _ccQueue;
        private bool _hasCcQueue;

        public void Bind(
            EntityManager em,
            in ComponentLookup<LocalTransform> transform,
            in ComponentLookup<FactionTag> faction,
            in ComponentLookup<AttackUnitTag> enemyTag,
            in ComponentLookup<DefenderUnitTag> defTag,
            in ComponentLookup<Wassup.Battle.Combat.AttackState> attack,
            in ComponentLookup<Health> health,
            float tileSize, int2 gridSize, float3 origin)
        {
            _em = em;
            _transform = transform; _faction = faction;
            _enemyTag = enemyTag; _defTag = defTag;
            _attack = attack; _health = health;
            _tileSize = tileSize; _gridSize = gridSize; _origin = origin;
        }

        public void BindPools(
            NativeArray<Entity> enemyPool, NativeArray<LocalTransform> enemyPoolXf,
            NativeArray<Entity> defPool, NativeArray<LocalTransform> defPoolXf)
        {
            _enemyPool = enemyPool; _enemyPoolXf = enemyPoolXf;
            _defPool = defPool; _defPoolXf = defPoolXf;
        }

        public void BindCcSink(NativeQueue<Wassup.Battle.Effects.EnemyCcEvent> q, bool has)
        {
            _ccQueue = q; _hasCcQueue = has;
        }

        // ── 핸들 변환 — 이 클래스가 유일한 번역자다 ──────────────────
        // 도메인 핸들은 `SimEntityId.value` 를 그대로 싣는다. 역변환은 풀 스캔인데,
        // 풀이 프레임당 한 번 지어지고 스킬 발동이 초당 수 회라 비용이 무시할 만하다
        // (unit 0 성능 실측: 최악 정렬 프레임 ~50발, 발당 <0.1ms).
        private Entity Resolve(SkillEntityId id)
        {
            if (!id.IsValid) return Entity.Null;
            for (int i = 0; i < _enemyPool.Length; i++)
                if (SimIdOf(_enemyPool[i]) == id.Value) return _enemyPool[i];
            for (int i = 0; i < _defPool.Length; i++)
                if (SimIdOf(_defPool[i]) == id.Value) return _defPool[i];
            return Entity.Null;
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
                default: throw NotWired($"Has({pred})");
            }
        }

        public byte TraversalLayers(SkillEntityId id)
            => (byte)Stat(id, UnitStat.TargetTraversalLayers);

        public float ShieldValueFrom(SkillEntityId target, SkillEntityId source)
            => throw NotWired(nameof(ShieldValueFrom));

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
        public bool TryDensestOpponentCluster(CasterRef caster, int densityRadius, out int2 cell, out int count)
            => throw NotWired(nameof(TryDensestOpponentCluster));

        public bool TryLandingCellNear(int2 desired, int maxRing, out int2 cell)
            => throw NotWired(nameof(TryLandingCellNear));

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
