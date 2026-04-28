using System;
using System.Collections.Generic;
using Wassup.Data;

namespace Wassup.Core
{
    // Plain-old state bag that tracks one draft attempt: a sampled pool drawn from
    // the defender catalog and the player's running discard list (capped at MaxDiscards).
    // Picked = pool minus discarded (pool order preserved). DraftController owns the
    // lifecycle; tests drive it directly without Unity.
    public class DraftSession
    {
        private readonly List<DefenderUnitData> _pool = new();
        private readonly HashSet<DefenderUnitData> _discarded = new();
        private readonly List<DefenderUnitData> _picked = new();

        public IReadOnlyList<DefenderUnitData> Pool => _pool;
        public IReadOnlyList<DefenderUnitData> Picked
        {
            get
            {
                _picked.Clear();
                foreach (var u in _pool) if (!_discarded.Contains(u)) _picked.Add(u);
                return _picked;
            }
        }
        public IReadOnlyCollection<DefenderUnitData> Discarded => _discarded;
        public int Seed { get; private set; }
        public int MaxDiscards { get; private set; }
        public int PoolSize => _pool.Count;
        public int DiscardedCount => _discarded.Count;
        public int PickedCount => _pool.Count - _discarded.Count;
        public bool IsFull => _discarded.Count >= MaxDiscards && MaxDiscards > 0;

        // Sample `poolSize` distinct units from `catalog` into the pool, clear discards,
        // and seed the RNG for reproducibility. Callers must provide a non-empty
        // catalog that contains at least `poolSize` entries.
        public void Reset(IReadOnlyList<DefenderUnitData> catalog, int poolSize, int maxDiscards, int seed)
        {
            if (catalog == null) throw new ArgumentNullException(nameof(catalog));
            if (poolSize <= 0) throw new ArgumentOutOfRangeException(nameof(poolSize));
            if (maxDiscards <= 0 || maxDiscards >= poolSize)
                throw new ArgumentOutOfRangeException(nameof(maxDiscards));
            if (catalog.Count < poolSize)
                throw new ArgumentException(
                    $"catalog has {catalog.Count} entries, need at least {poolSize}", nameof(catalog));

            Seed = seed;
            MaxDiscards = maxDiscards;
            _discarded.Clear();
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

        // Returns true if the discard set changed.
        public bool ToggleDiscard(DefenderUnitData unit)
        {
            if (unit == null) return false;
            if (!_pool.Contains(unit)) return false;
            if (_discarded.Contains(unit))
            {
                _discarded.Remove(unit);
                return true;
            }
            if (_discarded.Count >= MaxDiscards) return false;
            _discarded.Add(unit);
            return true;
        }

        public bool IsDiscarded(DefenderUnitData unit) => unit != null && _discarded.Contains(unit);
        public bool IsPicked(DefenderUnitData unit) => unit != null && _pool.Contains(unit) && !_discarded.Contains(unit);

        public DefenderUnitData[] PickedArray()
        {
            var arr = new DefenderUnitData[PickedCount];
            int k = 0;
            foreach (var u in _pool) if (!_discarded.Contains(u)) arr[k++] = u;
            return arr;
        }
    }
}
