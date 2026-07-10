using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using Wassup.Battle.Effects;
using Wassup.Battle.Movement;
using Wassup.Battle.Units;

namespace Wassup.Battle.Combat
{
    // nightmare-catcher unit 3 — HealthThreshold × SelfBlink arm + the threat
    // channel drain. Gated on buffer presence only (faction-neutral — no
    // BossTag/DefenderUnitTag in the gate, spec unit 4).
    //
    // Two responsibilities, both Combat-owned:
    //  1. Drain ThreatHitEvents into the victims' ThreatEntry tables (the
    //     accumulation write — unit 1 staged the channel, this closes it).
    //  2. Evaluate HealthThreshold slots against current Health (Units, RO)
    //     and resolve the blink destination: threat leader → nearest living
    //     defender → skip (래치는 이미 전진 — 재발동 없음). The position write
    //     itself is Movement-owned, so the destination goes out through
    //     BlinkRequestEventsSingleton (BlinkApplySystem 소비, 맥락 경계).
    //
    // Runs after DamageApplicationSystem so same-tick damage is visible to the
    // threshold, and after the same-tick threat hits have been enqueued
    // (AttackSystem → DamageApplication chain).
    [BurstCompile]
    [UpdateInGroup(typeof(BattleSimGroup))]
    [UpdateAfter(typeof(DamageApplicationSystem))]
    public partial struct BossHealthThresholdSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<ThreatEntry>(); // 보스(위협 테이블 보유) 존재 시에만
            state.RequireForUpdate<FlowFieldSingleton>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var transformLookup = SystemAPI.GetComponentLookup<LocalTransform>(isReadOnly: true);
            var threatLookup = SystemAPI.GetBufferLookup<ThreatEntry>(isReadOnly: false);

            // 1. Threat drain — accumulate this frame's attributed hits. A victim
            // destroyed since enqueue simply drops its events (HasBuffer guard).
            if (SystemAPI.TryGetSingletonRW<ThreatHitEventsSingleton>(out var threatEventsRW))
            {
                var queue = threatEventsRW.ValueRW.queue;
                while (queue.TryDequeue(out var evt))
                {
                    if (!threatLookup.HasBuffer(evt.victim)) continue;
                    ThreatTable.Accumulate(threatLookup[evt.victim], evt.attacker, evt.amount);
                }
            }

            bool hasBlinkQ = SystemAPI.TryGetSingletonRW<BlinkRequestEventsSingleton>(out var blinkRW);
            if (!hasBlinkQ) return; // 채널 없으면 평가 무의미 (인프라 미기동 프레임)
            var blinkQueue = blinkRW.ValueRW.queue;
            var ff = SystemAPI.GetSingleton<FlowFieldSingleton>();

            // rev 3 (실플레이 피드백) — blink 연출: 출발/도착 퍼프를 기존 hit-VFX
            // 채널(Combat→Presentation)로 재생. 슬롯에 베이크된 퍼프 dataIndex 사용.
            bool hasHitQ = SystemAPI.TryGetSingletonRW<Projectile.ProjectileHitEventsSingleton>(out var hitRW);
            NativeQueue<Projectile.ProjectileHitEvent> hitQueue = hasHitQ ? hitRW.ValueRW.queue : default;

            // Fallback pool = living defenders (payload targets the caster's
            // opposing side — same axis as the barrage epicenter pool).
            var defQuery = SystemAPI.QueryBuilder().WithAll<DefenderUnitTag, LocalTransform>().Build();
            var defEntities = defQuery.ToEntityArray(Allocator.Temp);
            var defTransforms = defQuery.ToComponentDataArray<LocalTransform>(Allocator.Temp);

            // 2. Threshold eval + blink resolve.
            foreach (var (slotsRef, health, transform, entity) in
                     SystemAPI.Query<DynamicBuffer<DcTriggerSlot>, RefRO<Health>, RefRO<LocalTransform>>()
                              .WithEntityAccess())
            {
                var slots = slotsRef; // CS1654 회피 — 뷰 struct 로컬 복사
                for (int si = 0; si < slots.Length; si++)
                {
                    var slot = slots[si];
                    if (slot.trigger != Wassup.Data.DcTriggerKind.HealthThreshold) continue;

                    int k = slot.nextBoundaryIndex;
                    bool fired = DcTrigger.HealthThresholdEval(health.ValueRO.value, slot.maxHpRef, slot.fraction, ref k);
                    slot.nextBoundaryIndex = k;
                    if (fired)
                    {
                        if (slot.payload != Wassup.Data.DcPayloadKind.SelfBlink)
                        {
                            UnityEngine.Debug.LogWarning("[BossHealthThreshold] HealthThreshold slot fired with unhandled payload kind.");
                        }
                        else if (TryResolveBlinkDest(entity, transform.ValueRO.Position, slot.tileRange,
                                     in transformLookup, in threatLookup, defEntities, defTransforms, in ff,
                                     out float3 destWorld))
                        {
                            blinkQueue.Enqueue(new BlinkRequestEvent { entity = entity, destWorld = destWorld });
                            // 출발지 + 도착지 퍼프 (dataIndex < 0 = 무연출 blink).
                            if (hasHitQ && slot.projectileDataIndex >= 0)
                            {
                                hitQueue.Enqueue(new Projectile.ProjectileHitEvent
                                {
                                    position = transform.ValueRO.Position,
                                    dataIndex = slot.projectileDataIndex,
                                    payload = Projectile.PayloadKind.SingleSplash,
                                    source = entity,
                                });
                                hitQueue.Enqueue(new Projectile.ProjectileHitEvent
                                {
                                    position = destWorld,
                                    dataIndex = slot.projectileDataIndex,
                                    payload = Projectile.PayloadKind.SingleSplash,
                                    source = entity,
                                });
                            }
                        }
                        // 목적지 실패(방어유닛 전멸/링 상한 초과) = skip — k 는
                        // 이미 전진(발동 소모 유지, 재발동 없음).
                    }
                    slots[si] = slot;
                }
            }

            defEntities.Dispose();
            defTransforms.Dispose();
        }

        // 폴백 체인: 위협 리더(alive) → 최근접 생존 방어유닛 → false(skip).
        // alive 정의 = 조회 시점 LocalTransform 존재 (unit 1 과 공유).
        private static bool TryResolveBlinkDest(
            Entity self, float3 selfPos, int maxRingRadius,
            in ComponentLookup<LocalTransform> transformLookup,
            in BufferLookup<ThreatEntry> threatLookup,
            in NativeArray<Entity> defEntities, in NativeArray<LocalTransform> defTransforms,
            in FlowFieldSingleton ff, out float3 destWorld)
        {
            destWorld = default;

            float3 leaderPos = default;
            bool hasLeader = false;
            if (threatLookup.HasBuffer(self))
            {
                var entries = threatLookup[self].AsNativeArray();
                var alive = new NativeArray<bool>(entries.Length, Allocator.Temp);
                for (int i = 0; i < entries.Length; i++)
                    alive[i] = transformLookup.HasComponent(entries[i].attacker);
                var leader = ThreatTable.Leader(entries, alive);
                alive.Dispose();
                if (leader != Entity.Null)
                {
                    leaderPos = transformLookup[leader].Position;
                    hasLeader = true;
                }
            }
            if (!hasLeader)
            {
                // 진짜 엣지 폴백(HIGH-2 격하): 위협 0 또는 리더 사망 → 최근접.
                // 동거리 동점은 entity index 오름차순 (결정론).
                float bestSq = float.MaxValue;
                int bestIdx = -1;
                for (int i = 0; i < defEntities.Length; i++)
                {
                    float3 d = defTransforms[i].Position - selfPos;
                    d.y = 0f;
                    float sq = math.lengthsq(d);
                    if (sq < bestSq || (sq == bestSq && bestIdx >= 0 && defEntities[i].Index < defEntities[bestIdx].Index))
                    {
                        bestSq = sq;
                        bestIdx = i;
                    }
                }
                if (bestIdx < 0) return false; // 방어유닛 전멸 → skip
                leaderPos = defTransforms[bestIdx].Position;
            }

            float3 desired = BlinkMath.OffsetDest(leaderPos, selfPos, ff.tileSize);
            int2 desiredCell = GridMath.WorldToCell(desired, ff.tileSize, ff.gridSize, origin: ff.origin);
            if (!BlinkMath.TryFindLandingCell(desiredCell, ff.dist, ff.gridSize, math.max(0, maxRingRadius), out int2 landing))
                return false; // 링 상한 내 착지 불가 → skip
            destWorld = GridMath.CellToWorldCenter(landing, ff.tileSize, 0f, origin: ff.origin);
            return true;
        }
    }
}
