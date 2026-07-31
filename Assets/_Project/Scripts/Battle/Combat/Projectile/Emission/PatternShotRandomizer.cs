using Unity.Mathematics;
using Wassup.Data;

namespace Wassup.Battle.Combat.Projectile.Emission
{
    // projectile-shot-sequence unit 5 — architecture-neutral trigger snapshot.
    // 같은 seed에는 같은 10발을 만들고, producer만 host/fire count에서 seed를 결정한다.
    public static class PatternShotRandomizer
    {
        public static void Apply(ref PatternSpec spec, uint seed)
        {
            if (!spec.randomizeShotsPerTrigger || spec.shots.Length == 0) return;

            float minInterval = math.max(0f, spec.randomIntervalMinSec);
            float maxInterval = math.max(minInterval, spec.randomIntervalMaxSec);
            var rng = Random.CreateFromIndex(seed);

            for (int i = 0; i < spec.shots.Length; i++)
            {
                var shot = spec.shots[i];
                shot.directionT = rng.NextFloat();
                shot.intervalAfterPreviousSec = i == 0
                    ? 0f
                    : (minInterval < maxInterval
                        ? rng.NextFloat(minInterval, maxInterval)
                        : minInterval);
                spec.shots[i] = shot;
            }
        }
    }
}
