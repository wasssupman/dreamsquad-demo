using System;
using System.Collections.Generic;
using Wassup.Data;

namespace Wassup.Core
{
    // Plain-old state bag that tracks one draft attempt: a sampled pool drawn from
    // the defender catalog and the player's running pick list (capped at MaxPicks).
    // DraftController owns the lifecycle; tests drive it directly without Unity.
    public class DraftSession
    {
        private readonly List<DefenderUnitData> _pool = new();
        private readonly List<DefenderUnitData> _picked = new();

        public IReadOnlyList<DefenderUnitData> Pool => _pool;
        public IReadOnlyList<DefenderUnitData> Picked => _picked;
        public int Seed { get; private set; }
        public int MaxPicks { get; private set; }
        public int PoolSize => _pool.Count;
        public int PickedCount => _picked.Count;
        public bool IsFull => _picked.Count >= MaxPicks && MaxPicks > 0;

        // Sample `poolSize` distinct units from `catalog` into the pool, clear picks,
        // and seed the RNG for reproducibility. Callers must provide a non-empty
        // catalog that contains at least `poolSize` entries.
        public void Reset(IReadOnlyList<DefenderUnitData> catalog, int poolSize, int maxPicks, int seed)
        {
            if (catalog == null) throw new ArgumentNullException(nameof(catalog));
            if (poolSize <= 0) throw new ArgumentOutOfRangeException(nameof(poolSize));
            if (maxPicks <= 0 || maxPicks > poolSize)
                throw new ArgumentOutOfRangeException(nameof(maxPicks));
            if (catalog.Count < poolSize)
                throw new ArgumentException(
                    $"catalog has {catalog.Count} entries, need at least {poolSize}", nameof(catalog));

            Seed = seed;
            MaxPicks = maxPicks;
            _picked.Clear();
            _pool.Clear();

            var indices = new int[catalog.Count];
            for (int i = 0; i < indices.Length; i++) indices[i] = i;

            var rng = new System.Random(seed);
            for (int i = 0; i < poolSize; i++)
            {
                int j = i + rng.Next(indices.Length - i);
                (indices[i], indices[j]) = (indices[j], indices[i]);
                var unit = catalog[indices[i]];
                if (unit != null) _pool.Add(unit);
            }
        }

        // Returns true if the pool/picks state changed.
        public bool TogglePick(DefenderUnitData unit)
        {
            if (unit == null) return false;
            if (!_pool.Contains(unit)) return false;
            int idx = _picked.IndexOf(unit);
            if (idx >= 0)
            {
                _picked.RemoveAt(idx);
                return true;
            }
            if (_picked.Count >= MaxPicks) return false;
            _picked.Add(unit);
            return true;
        }

        public bool IsPicked(DefenderUnitData unit) => unit != null && _picked.Contains(unit);

        public DefenderUnitData[] PickedArray() => _picked.ToArray();
    }
}
