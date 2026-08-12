using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using Wassup.Battle.Combat;
using Wassup.Battle.Effects;
using Wassup.Battle.Units;

namespace Wassup.Battle.Movement
{
    // instinct-content unit 3 — 적이 걸어갈 거점을 고른다. **스폰 시 1회 → 부서지면 재선정.**
    //
    // 왜 매 프레임 최근접을 다시 고르지 않나: 그러면 적이 두 본능 사이에서 진동하고,
    // 「가까운 것부터 하나씩 부순다」가 아니라 「가운데서 서성인다」가 된다. 선택은 사건
    // (스폰 · 대상 파괴)에서만 일어난다.
    //
    // 맥락: Movement 소유(`StructureDestination` 은 라우팅 상태다). Units 의 `StructureTag`
    // 와 Combat 의 `EnemyTargetFilter` 는 **읽기만** 한다.
    [BurstCompile]
    [UpdateInGroup(typeof(BattleSimGroup))]
    [UpdateBefore(typeof(MovementSystem))]
    public partial struct StructureDestinationSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<FlowFieldSingleton>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var field = SystemAPI.GetSingleton<FlowFieldSingleton>();

            // ── 살아 있는 방어 본능 목록 ──
            var candidateCells = new NativeList<float2>(4, Allocator.Temp);
            var candidateGrid = new NativeList<int2>(4, Allocator.Temp);
            var candidateEntities = new NativeList<Entity>(4, Allocator.Temp);
            foreach (var (tag, entity) in
                     SystemAPI.Query<RefRO<StructureTag>>()
                              .WithNone<DeadTag>()
                              .WithEntityAccess())
            {
                if (tag.ValueRO.faction != Faction.DefenderInstinct) continue;
                var world = GridMath.CellToWorldCenter(
                    tag.ValueRO.cell, field.tileSize, 0f, origin: field.origin);
                candidateCells.Add(new float2(world.x, world.z));
                candidateGrid.Add(tag.ValueRO.cell);
                candidateEntities.Add(entity);
            }

            var ecb = new EntityCommandBuffer(Allocator.Temp);
            var factionLookup = SystemAPI.GetComponentLookup<FactionTag>(isReadOnly: true);
            var filterLookup = SystemAPI.GetComponentLookup<EnemyTargetFilter>(isReadOnly: true);

            foreach (var (transform, follow, entity) in
                     SystemAPI.Query<RefRO<LocalTransform>, RefRO<PathFollowState>>()
                              .WithNone<PastGoalTag, DeadTag>()
                              .WithEntityAccess())
            {
                if (!factionLookup.HasComponent(entity)
                    || factionLookup[entity].value != Faction.EnemyUnit) continue;

                bool has = SystemAPI.HasComponent<StructureDestination>(entity);

                // 자기가 팰 수 있는 것만 목적지로 삼는다. 못 부수는 건물 앞에서 굳지 않도록,
                // 저작 마스크에 본능이 없으면 우회 자체를 하지 않는다(마음사냥꾼은 포함이라
                // 우회한다 — 거점 전담이니 당연하다).
                bool wantsInstincts =
                    filterLookup.HasComponent(entity)
                    && (EnemyTargetDefaults.Resolve(filterLookup[entity].factionMask)
                        & (int)Faction.DefenderInstinct) != 0;

                if (!wantsInstincts || candidateEntities.Length == 0)
                {
                    if (has) ecb.RemoveComponent<StructureDestination>(entity);
                    continue;
                }

                // 들고 있는 대상이 아직 살아 있으면 **바꾸지 않는다** — 이게 「1회」의 실체다.
                if (has)
                {
                    var current = SystemAPI.GetComponent<StructureDestination>(entity);
                    bool alive = false;
                    for (int i = 0; i < candidateEntities.Length; i++)
                        if (candidateEntities[i] == current.structure) { alive = true; break; }
                    if (alive) continue;
                }

                float3 p = transform.ValueRO.Position;
                int pick = StructureChoice.NearestIndex(
                    new float2(p.x, p.z), candidateCells.AsArray());
                if (pick < 0)
                {
                    if (has) ecb.RemoveComponent<StructureDestination>(entity);
                    continue;
                }

                var chosen = new StructureDestination
                {
                    cell = candidateGrid[pick],
                    structure = candidateEntities[pick],
                };
                if (has) ecb.SetComponent(entity, chosen);
                else ecb.AddComponent(entity, chosen);
            }

            ecb.Playback(state.EntityManager);
            ecb.Dispose();
            candidateEntities.Dispose();
            candidateGrid.Dispose();
            candidateCells.Dispose();
        }
    }
}
