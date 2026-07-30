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
    [UpdateInGroup(typeof(BattleSimGroup))]
    public partial struct EffectTickSystem : ISystem
    {
        // OnCreate is not Burst-compiled because RequireAnyForUpdate takes a
        // managed params array (BC1028 otherwise); OnUpdate stays Burst.
        public void OnCreate(ref SystemState state)
        {
            state.RequireAnyForUpdate(
                state.GetEntityQuery(ComponentType.ReadOnly<TornadoField>()),
                state.GetEntityQuery(ComponentType.ReadOnly<PortalLink>()),
                state.GetEntityQuery(ComponentType.ReadOnly<AllyBuffField>()));
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            float dt = SystemAPI.Time.DeltaTime;
            var ecb = new EntityCommandBuffer(Allocator.Temp);

            // Phase 8 §17 — TornadoField: carrier entity (PortalLink pattern).
            // Tick remaining + destroy the whole entity on expiry.
            foreach (var (effect, entity) in
                     SystemAPI.Query<RefRW<TornadoField>>().WithEntityAccess())
            {
                effect.ValueRW.remaining -= dt;
                if (effect.ValueRO.remaining <= 0f)
                {
                    ecb.DestroyEntity(entity);
                }
            }

            // active-ally-zone unit 0 — AllyBuffField: 같은 캐리어 형태.
            // 파괴되면 AllyBuffFieldSystem 이 더 이상 재발행하지 않아 버프가 자연 소멸한다.
            // (이 시스템과 ModifierApplySystem 사이에 명시 순서가 없어 파괴되는 프레임에 한 번 더
            //  갱신될 수 있으나, 수용된 AllyBuffApplySec 지연 안이라 무해하다 — [UpdateAfter] 를
            //  얹지 말 것.)
            foreach (var (field, entity) in
                     SystemAPI.Query<RefRW<AllyBuffField>>().WithEntityAccess())
            {
                field.ValueRW.remaining -= dt;
                if (field.ValueRO.remaining <= 0f)
                {
                    ecb.DestroyEntity(entity);
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
