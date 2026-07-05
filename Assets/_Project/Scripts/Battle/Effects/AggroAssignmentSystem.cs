using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using Wassup.Battle.Movement;
using Wassup.Battle.Units;

namespace Wassup.Battle.Effects
{
    // aggro-targeting Unit 2 — single authority over aggro state (Effects context).
    // Each tick: (1) release aggro whose guardian died/vanished, (2) tally live
    // count per guardian, (3) acquire nearest in-range un-aggroed enemies up to
    // capacity. MovementSystem/AttackSystem only READ Aggroed.
    //
    // Acquisition is proximity + capacity based (instant up to cap). The rate model
    // ("시간당 어그로 수량" = per-attack target count × attack speed) is a follow-up;
    // see docs/spec/aggro-targeting/README.md.
    [BurstCompile]
    [UpdateInGroup(typeof(BattleSimGroup))]
    [UpdateBefore(typeof(MovementSystem))]
    public partial struct AggroAssignmentSystem : ISystem
    {
        // Not Bursted: RequireAnyForUpdate(params EntityQuery[]) allocates a managed array.
        public void OnCreate(ref SystemState state)
        {
            // Run whenever there is a provider OR an aggroed enemy. The latter keeps
            // the release pass alive after the last guardian is destroyed, so orphaned
            // Aggroed enemies are still released (HIGH 1).
            var providerQuery = state.GetEntityQuery(ComponentType.ReadOnly<AggroProvider>());
            var aggroedQuery = state.GetEntityQuery(ComponentType.ReadOnly<Aggroed>());
            state.RequireAnyForUpdate(providerQuery, aggroedQuery);
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var healthLookup = SystemAPI.GetComponentLookup<Health>(isReadOnly: true);
            var deadLookup = SystemAPI.GetComponentLookup<DeadTag>(isReadOnly: true);
            var ecb = new EntityCommandBuffer(Allocator.Temp);

            bool hasFlow = SystemAPI.TryGetSingleton<FlowFieldSingleton>(out var flow);
            float tileSize = hasFlow ? flow.tileSize : 1f;
            int2 gridSize = hasFlow ? flow.gridSize : new int2(128, 128);
            float3 ffOrigin = hasFlow ? flow.origin : float3.zero;

            // ── Pass 1: release invalid links + tally live count per guardian ──
            var countByGuardian = new NativeHashMap<Entity, int>(16, Allocator.Temp);
            foreach (var (aggro, enemyEntity) in
                     SystemAPI.Query<RefRO<Aggroed>>().WithEntityAccess())
            {
                Entity g = aggro.ValueRO.guardian;
                bool guardianAlive = g != Entity.Null
                    && state.EntityManager.Exists(g)
                    && !deadLookup.HasComponent(g)
                    && healthLookup.HasComponent(g) && healthLookup[g].value > 0f;
                if (!guardianAlive)
                {
                    // release → resume base behavior. TauntAttackGrantSystem (Combat)
                    // strips any granted AttackState once Aggroed is gone.
                    ecb.RemoveComponent<Aggroed>(enemyEntity);
                    continue;
                }
                if (deadLookup.HasComponent(enemyEntity)) continue; // dying enemy, ignore
                countByGuardian.TryGetValue(g, out int c);
                countByGuardian[g] = c + 1;
            }

            // ── Candidate pool: alive enemies not yet aggroed (선점 = WithNone<Aggroed>) ──
            var candQuery = SystemAPI.QueryBuilder()
                .WithAll<FactionTag, LocalTransform, Health>()
                .WithNone<DeadTag, Aggroed, PendingDeployment>()
                .Build();
            var candEntities = candQuery.ToEntityArray(Allocator.Temp);
            var candTransforms = candQuery.ToComponentDataArray<LocalTransform>(Allocator.Temp);
            var candFactions = candQuery.ToComponentDataArray<FactionTag>(Allocator.Temp);
            var assigned = new NativeArray<bool>(candEntities.Length, Allocator.Temp);

            // ── Pass 3: acquire nearest in-range free enemies up to capacity ──
            foreach (var (provider, gTransform, guardianEntity) in
                     SystemAPI.Query<RefRO<AggroProvider>, RefRO<LocalTransform>>().WithEntityAccess())
            {
                // A dead/dying guardian must not acquire new aggro (it releases instead).
                if (deadLookup.HasComponent(guardianEntity)) continue;
                if (healthLookup.HasComponent(guardianEntity) && healthLookup[guardianEntity].value <= 0f) continue;

                countByGuardian.TryGetValue(guardianEntity, out int count);
                int free = provider.ValueRO.capacity - count;
                if (free <= 0) continue;

                float3 gpos = gTransform.ValueRO.Position;
                int2 gCell = GridMath.WorldToCell(gpos, tileSize, gridSize, origin: ffOrigin);
                int tileRange = GridMath.RangeToTiles(provider.ValueRO.range);

                for (int slot = 0; slot < free; slot++)
                {
                    float bestSq = float.MaxValue;
                    int bestIdx = -1;
                    for (int i = 0; i < candEntities.Length; i++)
                    {
                        if (assigned[i]) continue;
                        if (((int)candFactions[i].value & (int)Faction.Enemy) == 0) continue;
                        float3 epos = candTransforms[i].Position;
                        int2 eCell = GridMath.WorldToCell(epos, tileSize, gridSize, origin: ffOrigin);
                        int td = math.max(math.abs(eCell.x - gCell.x), math.abs(eCell.y - gCell.y));
                        if (td > tileRange) continue;
                        float dx = epos.x - gpos.x, dz = epos.z - gpos.z;
                        float d2 = dx * dx + dz * dz;
                        if (d2 < bestSq) { bestSq = d2; bestIdx = i; }
                    }
                    if (bestIdx < 0) break; // no more in-range candidates
                    assigned[bestIdx] = true; // 선점: claimed for this tick, blocks other guardians
                    // Effects writes only the aggro link. TauntAttackGrantSystem (Combat)
                    // grants the taunt AttackState for outputs-less enemies (HIGH 2).
                    ecb.AddComponent(candEntities[bestIdx], new Aggroed { guardian = guardianEntity });
                }
            }

            ecb.Playback(state.EntityManager);
            ecb.Dispose();
            countByGuardian.Dispose();
            candEntities.Dispose();
            candTransforms.Dispose();
            candFactions.Dispose();
            assigned.Dispose();
        }
    }
}
