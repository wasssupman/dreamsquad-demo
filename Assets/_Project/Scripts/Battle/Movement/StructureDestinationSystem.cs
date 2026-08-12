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
    // 규칙은 하나다: **내가 팰 수 있는 거점 중 가장 가까운 것.** 맵 모드도, 거점 종류도
    // 묻지 않는다 — 마음도 본능도 같은 후보로 경쟁하고, 자격은 저작 마스크가 정한다.
    // 그래서 침략 맵에서는 후보가 마음뿐이라 「가장 가까운 마음」= 현행 골 라우팅으로 떨어지고,
    // 공성 맵에서는 본능이 끼어들어 자연히 「가까운 본능부터」가 된다. 분기가 아니라 콘텐츠다.
    //
    // 왜 매 프레임 최근접을 다시 고르지 않나: 그러면 적이 두 거점 사이에서 진동하고,
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

            // ── 살아 있는 거점 전부. **종류로 거르지 않는다** — 자격은 아래 마스크가 묻는다.
            // 마음(GoalTowerTag 동반)도 여기 들어온다: 「가장 가까운 거점」이 마음일 수 있어야
            // 코앞의 마음을 두고 먼 본능으로 걸어가는 일이 없다.
            var candidateCells = new NativeList<float2>(8, Allocator.Temp);
            var candidateGrid = new NativeList<int2>(8, Allocator.Temp);
            var candidateFactions = new NativeList<int>(8, Allocator.Temp);
            var candidateEntities = new NativeList<Entity>(8, Allocator.Temp);
            var candidateIsGoal = new NativeList<bool>(8, Allocator.Temp);
            foreach (var (tag, entity) in
                     SystemAPI.Query<RefRO<StructureTag>>()
                              .WithNone<DeadTag>()
                              .WithEntityAccess())
            {
                var world = GridMath.CellToWorldCenter(
                    tag.ValueRO.cell, field.tileSize, 0f, origin: field.origin);
                candidateCells.Add(new float2(world.x, world.z));
                candidateGrid.Add(tag.ValueRO.cell);
                candidateFactions.Add((int)tag.ValueRO.faction);
                candidateEntities.Add(entity);
                candidateIsGoal.Add(SystemAPI.HasComponent<GoalTowerTag>(entity));
            }

            // 정렬 기준은 `StructureChoice.IsBefore` — **예고선과 공유한다**(거기 주석 참조).
            // 후보는 많아야 서너 개라 삽입 정렬로 충분하다.
            for (int i = 1; i < candidateGrid.Length; i++)
                for (int j = i; j > 0 && StructureChoice.IsBefore(candidateGrid[j], candidateGrid[j - 1]); j--)
                {
                    (candidateGrid[j], candidateGrid[j - 1]) = (candidateGrid[j - 1], candidateGrid[j]);
                    (candidateCells[j], candidateCells[j - 1]) = (candidateCells[j - 1], candidateCells[j]);
                    (candidateFactions[j], candidateFactions[j - 1]) = (candidateFactions[j - 1], candidateFactions[j]);
                    (candidateEntities[j], candidateEntities[j - 1]) = (candidateEntities[j - 1], candidateEntities[j]);
                    (candidateIsGoal[j], candidateIsGoal[j - 1]) = (candidateIsGoal[j - 1], candidateIsGoal[j]);
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

                // 자격 = 저작 마스크. 못 부수는 건물은 후보에서 빠지므로 그 앞에서 굳지 않는다.
                int targetMask = filterLookup.HasComponent(entity)
                    ? EnemyTargetDefaults.Resolve(filterLookup[entity].factionMask)
                    : EnemyTargetDefaults.DefaultEnemyMask;

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
                    new float2(p.x, p.z), candidateCells.AsArray(),
                    candidateFactions.AsArray(), targetMask);

                // 마음이 가장 가까우면 **컴포넌트를 떼고 골 슬롯으로 돌아간다.** 골 슬롯은 이미
                // 골 전체를 소스로 하는 N-소스 필드라 「가장 가까운 마음」을 스스로 안다 —
                // 마음마다 슬롯을 새로 굽는 것은 같은 답을 두 벌로 만드는 일이다.
                if (pick < 0 || candidateIsGoal[pick])
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
            candidateIsGoal.Dispose();
            candidateEntities.Dispose();
            candidateFactions.Dispose();
            candidateGrid.Dispose();
            candidateCells.Dispose();
        }
    }
}
