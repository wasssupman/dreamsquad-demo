using Unity.Entities;

namespace Wassup.Battle.Units
{
    // Transient marker added whenever an entity takes a projectile hit. The flash
    // system ticks `remaining` down and briefly scales the entity beyond its base
    // size, then restores. Phase 3 keeps `duration` a const (0.15s) — promote to
    // an SO field only if tuning becomes necessary.
    public struct HitFlashTag : IComponentData
    {
        public float remaining;
        public float duration;
        public float originalScale;
    }
}
