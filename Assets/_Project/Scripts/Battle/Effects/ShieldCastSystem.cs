using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using Wassup.Battle.Movement;
using Wassup.Battle.Units;
using Wassup.Data;

namespace Wassup.Battle.Effects
{
    // shield-guardian-defender unit 1 — A초마다 공격범위(Chebyshev 타일, attackRange
    // 복사값) 내 아군 defender C명을 필터로 골라 실드 B를 부여하는 생산자.
    // 구조는 HazardCastSystem 선례(스냅샷 배열 + per-caster 스캔 + 쿨다운).
    // 쓰기는 IncomingShield 버퍼(Units 소유 이벤트 버퍼) append 만 — 병합(같은 출처
    // max·교차 출처 합산)은 DamageApplicationSystem drain 이 담당(계약 1).
    [BurstCompile]
    [UpdateInGroup(typeof(BattleSimGroup))]
    [UpdateAfter(typeof(MovementSystem))]
    public partial struct ShieldCastSystem : ISystem
    {
        private BufferLookup<IncomingShield> _incomingShieldLookup;
        private BufferLookup<ShieldSlot> _shieldSlotLookup;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<ShieldCastState>();
            state.RequireForUpdate<FlowFieldSingleton>();
            _incomingShieldLookup = state.GetBufferLookup<IncomingShield>(isReadOnly: false);
            _shieldSlotLookup = state.GetBufferLookup<ShieldSlot>(isReadOnly: true);
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            float dt = SystemAPI.Time.DeltaTime;
            var flowField = SystemAPI.GetSingleton<FlowFieldSingleton>();
            _incomingShieldLookup.Update(ref state);
            _shieldSlotLookup.Update(ref state);

            // 후보 스냅샷: 생존·배치완료 아군 defender (자신 포함 — 계약 6).
            var candidateQuery = SystemAPI.QueryBuilder()
                .WithAll<DefenderUnitTag, Health, LocalTransform>()
                .WithNone<PendingDeployment>()
                .WithNone<DeadTag>()
                .Build();
            var candEntities = candidateQuery.ToEntityArray(Allocator.Temp);
            var candTransforms = candidateQuery.ToComponentDataArray<LocalTransform>(Allocator.Temp);
            var candHealths = candidateQuery.ToComponentDataArray<Health>(Allocator.Temp);

            foreach (var (cast, transform, casterEntity) in
                     SystemAPI.Query<RefRW<ShieldCastState>, RefRO<LocalTransform>>()
                         .WithAll<DefenderUnitTag>()
                         .WithNone<PendingDeployment>()
                         .WithNone<DeadTag>()
                         .WithEntityAccess())
            {
                if (cast.ValueRO.cooldownRemaining > 0f)
                {
                    cast.ValueRW.cooldownRemaining = math.max(0f, cast.ValueRO.cooldownRemaining - dt);
                    continue;
                }

                float3 casterPos = transform.ValueRO.Position;
                int2 casterCell = GridMath.WorldToCell(casterPos, flowField.tileSize, flowField.gridSize, origin: flowField.origin);
                int tileRange = GridMath.RangeToTiles(cast.ValueRO.range);

                var candidates = new NativeList<ShieldCandidate>(candEntities.Length, Allocator.Temp);
                var candidateTargets = new NativeList<Entity>(candEntities.Length, Allocator.Temp);
                int selfIndex = -1;

                for (int i = 0; i < candEntities.Length; i++)
                {
                    float3 targetPos = candTransforms[i].Position;
                    int2 targetCell = GridMath.WorldToCell(targetPos, flowField.tileSize, flowField.gridSize, origin: flowField.origin);
                    int tileDist = math.max(math.abs(targetCell.x - casterCell.x), math.abs(targetCell.y - casterCell.y));
                    if (tileDist > tileRange) continue;

                    float shieldSum = _shieldSlotLookup.HasBuffer(candEntities[i])
                        ? ShieldMath.Sum(_shieldSlotLookup[candEntities[i]])
                        : 0f;
                    float maxHp = math.max(1f, candHealths[i].max);
                    candidates.Add(new ShieldCandidate
                    {
                        distanceSq = math.distancesq(casterPos, targetPos),
                        effectiveHpRatio = (candHealths[i].value + shieldSum) / maxHp,
                    });
                    candidateTargets.Add(candEntities[i]);
                    if (candEntities[i] == casterEntity) selfIndex = candidateTargets.Length - 1;
                }

                var selected = new NativeList<int>(cast.ValueRO.targetCount, Allocator.Temp);
                ShieldTargeting.Select(cast.ValueRO.filter, cast.ValueRO.targetCount, selfIndex,
                    candidates.AsArray(), ref selected);

                for (int s = 0; s < selected.Length; s++)
                {
                    Entity target = candidateTargets[selected[s]];
                    if (!_incomingShieldLookup.HasBuffer(target)) continue;
                    _incomingShieldLookup[target].Add(new IncomingShield
                    {
                        source = casterEntity,
                        amount = cast.ValueRO.amount,
                    });
                }

                // 자신이 항상 후보라 매 주기 발화 — 대상 유무와 무관하게 쿨다운 리셋
                // (미발화 시 매 프레임 재스캔 방지).
                cast.ValueRW.cooldownRemaining = cast.ValueRO.cooldownDuration;

                selected.Dispose();
                candidates.Dispose();
                candidateTargets.Dispose();
            }

            candEntities.Dispose();
            candTransforms.Dispose();
            candHealths.Dispose();
        }
    }
}
