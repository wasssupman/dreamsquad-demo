using Unity.Entities;
using Unity.Mathematics;

namespace Wassup.Battle.Effects
{
    // Central choke point for adding/updating Effects-context components from
    // outside the Effects systems (typically BattleBridge.CastSkill*). Keeping
    // all writes behind this helper makes it straightforward to audit that only
    // Effects code mutates SlowEffect / DamageBoost / CooldownReduction.
    //
    // Apply semantics: if the entity already carries the effect, the longer
    // remaining time wins and the newly supplied multiplier replaces the old
    // one. This matches Phase 2's non-stackable assumption — re-casting just
    // refreshes/extends the effect.
    public static class EffectSpawner
    {
        public static void ApplySlow(EntityManager em, Entity entity, float duration, float multiplier)
            => Apply<SlowEffect>(em, entity,
                () => new SlowEffect { remaining = duration, multiplier = multiplier },
                existing => new SlowEffect
                {
                    remaining = existing.remaining > duration ? existing.remaining : duration,
                    multiplier = multiplier,
                });

        public static void ApplyDamageBoost(EntityManager em, Entity entity, float duration, float multiplier)
            => Apply<DamageBoost>(em, entity,
                () => new DamageBoost { remaining = duration, multiplier = multiplier },
                existing => new DamageBoost
                {
                    remaining = existing.remaining > duration ? existing.remaining : duration,
                    multiplier = multiplier,
                });

        public static void ApplyCooldownReduction(EntityManager em, Entity entity, float duration, float multiplier)
            => Apply<CooldownReduction>(em, entity,
                () => new CooldownReduction { remaining = duration, multiplier = multiplier },
                existing => new CooldownReduction
                {
                    remaining = existing.remaining > duration ? existing.remaining : duration,
                    multiplier = multiplier,
                });

        // Phase 8 §17 — Tornado: carrier entity with area data. MovementSystem
        // queries live TornadoField entities each frame and applies pull to any
        // attacker inside the radius (continuous, not snapshot). Re-cast spawns
        // an independent field; multiple fields can coexist.
        public static Entity SpawnTornadoField(EntityManager em, float3 centerWorld, float radius, float pullSpeed, float duration)
        {
            var e = em.CreateEntity();
            em.AddComponentData(e, new TornadoField
            {
                centerWorld = centerWorld,
                radius = radius,
                pullSpeed = pullSpeed,
                remaining = duration,
            });
            return e;
        }

        // Phase 7 — Meteor: unlike Slow/Tornado, this spawns a dedicated carrier
        // entity. MeteorResolutionSystem consumes + destroys it when warningRemaining <= 0.
        public static Entity SpawnMeteor(EntityManager em, float3 centerWorld, float radius, float damage, float warningSec)
        {
            var e = em.CreateEntity();
            em.AddComponentData(e, new MeteorPending
            {
                centerWorld = centerWorld,
                radius = radius,
                damage = damage,
                warningRemaining = warningSec,
            });
            return e;
        }

        // Phase 7 — Portal: carrier entity with the two endpoints. Re-cast spawns a
        // separate link (player-decided overlap) rather than merging.
        // Phase 9: exitWaypointIndex parameter dropped. After teleport, next-frame
        // flow field lookup supplies the exit direction.
        public static Entity SpawnPortal(EntityManager em, float3 entryWorld, float3 exitWorld, float entryRadius, float duration)
        {
            var e = em.CreateEntity();
            em.AddComponentData(e, new PortalLink
            {
                entryWorld = entryWorld,
                exitWorld = exitWorld,
                entryRadius = entryRadius,
                remaining = duration,
            });
            return e;
        }

        // Phase 4 adjacency synergy — no duration, no merge logic. RecomputeSynergyFor
        // is the only writer and calls Set/Remove with an authoritative value each time.
        public static void SetSynergy(EntityManager em, Entity entity, float damageMul)
        {
            if (em.HasComponent<SynergyBuff>(entity))
                em.SetComponentData(entity, new SynergyBuff { damageMul = damageMul });
            else
                em.AddComponentData(entity, new SynergyBuff { damageMul = damageMul });
        }

        public static void RemoveSynergy(EntityManager em, Entity entity)
        {
            if (em.HasComponent<SynergyBuff>(entity))
                em.RemoveComponent<SynergyBuff>(entity);
        }

        private static void Apply<T>(EntityManager em, Entity entity,
            System.Func<T> create, System.Func<T, T> merge) where T : unmanaged, IComponentData
        {
            if (em.HasComponent<T>(entity))
            {
                em.SetComponentData(entity, merge(em.GetComponentData<T>(entity)));
            }
            else
            {
                em.AddComponentData(entity, create());
            }
        }
    }
}
