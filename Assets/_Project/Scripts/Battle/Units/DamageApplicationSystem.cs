using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using Wassup.Battle.Combat;
using Wassup.Battle.Effects;

namespace Wassup.Battle.Units
{
    // Drains IncomingDamage and IncomingHeal buffers into Health each frame.
    // Also applies RegenPerSec from ModifierStats directly (not via IncomingHeal).
    // When health crosses zero the entity gets a DeadTag so UnitLifecycleSystem can destroy it.
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(AttackSystem))]
    public partial struct DamageApplicationSystem : ISystem
    {
        private ComponentLookup<ModifierStats> _buffStatsLookup;
        private ComponentLookup<LocalTransform> _transformLookup;
        private BufferLookup<IncomingHeal> _healBufferLookup;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<IncomingDamage>();
            _buffStatsLookup  = state.GetComponentLookup<ModifierStats>(isReadOnly: true);
            _transformLookup  = state.GetComponentLookup<LocalTransform>(isReadOnly: true);
            _healBufferLookup = state.GetBufferLookup<IncomingHeal>(isReadOnly: false);
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            float dt = SystemAPI.Time.DeltaTime;
            _buffStatsLookup.Update(ref state);
            _transformLookup.Update(ref state);
            _healBufferLookup.Update(ref state);
            bool hasHealAppliedQueue = SystemAPI.TryGetSingletonRW<HealAppliedEventsSingleton>(out var healAppliedSingleton);

            var ecb = new EntityCommandBuffer(Allocator.Temp);
            foreach (var (health, damageBuffer, entity) in
                     SystemAPI.Query<RefRW<Health>, DynamicBuffer<IncomingDamage>>()
                              .WithNone<DeadTag>()
                              .WithNone<PendingDeployment>()
                              .WithEntityAccess())
            {
                // ── ModifierStats lookup (read-only, defaults safe when absent) ────────
                bool hasModifierStats = _buffStatsLookup.HasComponent(entity);
                float dmgTakenMul = hasModifierStats ? _buffStatsLookup[entity].dmgTakenMul : 1f;
                float regenPerSec = hasModifierStats ? _buffStatsLookup[entity].regenPerSec  : 0f;

                // ── IncomingDamage drain ─────────────────────────────────────────
                float totalDamage = 0f;
                for (int i = 0; i < damageBuffer.Length; i++)
                    totalDamage += damageBuffer[i].amount;
                damageBuffer.Clear();
                totalDamage *= dmgTakenMul;

                // ── IncomingHeal drain (pulse channel — must Clear each frame) ───
                float pulseHeal = 0f;
                bool hasPulse = false;
                if (_healBufferLookup.HasBuffer(entity))
                {
                    var hBuf = _healBufferLookup[entity];
                    hasPulse = hBuf.Length > 0;
                    for (int i = 0; i < hBuf.Length; i++)
                        pulseHeal += hBuf[i].amount;
                    hBuf.Clear();
                }

                // ── RegenPerSec — direct per-frame addition, bypasses IncomingHeal
                float totalHeal = pulseHeal + regenPerSec * dt;

                // ── Health update with clamp ─────────────────────────────────────
                float newHp = math.min(health.ValueRO.max, health.ValueRO.value - totalDamage + totalHeal);
                health.ValueRW.value = newHp;
                // Only enqueue VFX for IncomingHeal pulses (hasPulse + positive amount).
                // RegenPerSec is excluded to avoid spamming VFX every frame.
                if (hasHealAppliedQueue && hasPulse && pulseHeal > 0f && _transformLookup.HasComponent(entity))
                {
                    healAppliedSingleton.ValueRW.queue.Enqueue(new HealAppliedEvent
                    {
                        position = _transformLookup[entity].Position,
                        amount = pulseHeal,
                    });
                }
                if (newHp <= 0f)
                {
                    ecb.AddComponent<DeadTag>(entity);
                }
            }
            ecb.Playback(state.EntityManager);
            ecb.Dispose();
        }
    }
}
