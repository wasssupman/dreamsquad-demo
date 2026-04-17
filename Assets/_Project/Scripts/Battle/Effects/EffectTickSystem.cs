using Unity.Burst;
using Unity.Collections;
using Unity.Entities;

namespace Wassup.Battle.Effects
{
    // Owns the lifetime of every Effects-context component: ticks `remaining`
    // down by DeltaTime each frame and removes the component once it expires.
    // Combat / Movement remain read-only consumers.
    //
    // Runs after MovementSystem + AttackSystem so this frame's consumers see the
    // pre-tick value; the removal only takes effect next frame via ECB playback.
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial struct EffectTickSystem : ISystem
    {
        // OnCreate is not Burst-compiled because RequireAnyForUpdate takes a
        // managed params array (BC1028 otherwise); OnUpdate stays Burst.
        public void OnCreate(ref SystemState state)
        {
            state.RequireAnyForUpdate(
                state.GetEntityQuery(ComponentType.ReadOnly<SlowEffect>()),
                state.GetEntityQuery(ComponentType.ReadOnly<DamageBoost>()),
                state.GetEntityQuery(ComponentType.ReadOnly<CooldownReduction>()),
                state.GetEntityQuery(ComponentType.ReadOnly<TornadoPull>()),
                state.GetEntityQuery(ComponentType.ReadOnly<PortalLink>()));
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            float dt = SystemAPI.Time.DeltaTime;
            var ecb = new EntityCommandBuffer(Allocator.Temp);

            foreach (var (effect, entity) in
                     SystemAPI.Query<RefRW<SlowEffect>>().WithEntityAccess())
            {
                effect.ValueRW.remaining -= dt;
                if (effect.ValueRO.remaining <= 0f)
                {
                    ecb.RemoveComponent<SlowEffect>(entity);
                }
            }

            foreach (var (effect, entity) in
                     SystemAPI.Query<RefRW<DamageBoost>>().WithEntityAccess())
            {
                effect.ValueRW.remaining -= dt;
                if (effect.ValueRO.remaining <= 0f)
                {
                    ecb.RemoveComponent<DamageBoost>(entity);
                }
            }

            foreach (var (effect, entity) in
                     SystemAPI.Query<RefRW<CooldownReduction>>().WithEntityAccess())
            {
                effect.ValueRW.remaining -= dt;
                if (effect.ValueRO.remaining <= 0f)
                {
                    ecb.RemoveComponent<CooldownReduction>(entity);
                }
            }

            // Phase 7 — TornadoPull: attacker-attached. Tick + remove when expired
            // so MovementSystem falls back to normal waypoint traversal.
            foreach (var (effect, entity) in
                     SystemAPI.Query<RefRW<TornadoPull>>().WithEntityAccess())
            {
                effect.ValueRW.remaining -= dt;
                if (effect.ValueRO.remaining <= 0f)
                {
                    ecb.RemoveComponent<TornadoPull>(entity);
                }
            }

            // Phase 7 — PortalLink: carrier entity. Destroy the whole entity on
            // expiry since the component IS the portal.
            foreach (var (effect, entity) in
                     SystemAPI.Query<RefRW<PortalLink>>().WithEntityAccess())
            {
                effect.ValueRW.remaining -= dt;
                if (effect.ValueRO.remaining <= 0f)
                {
                    ecb.DestroyEntity(entity);
                }
            }

            ecb.Playback(state.EntityManager);
            ecb.Dispose();
        }
    }
}
