using System.Collections.Generic;
using UnityEngine;

namespace Wassup.Presentation
{
    // Lightweight object pool for DamageNumberView instances. Plain C# (not a
    // MonoBehaviour); owned by DamageNumberSpawner. Recycles popups to avoid GC
    // spikes when many enemies are hit at once.
    public class DamageNumberPool
    {
        private readonly GameObject _prefab;
        private readonly Transform _parent;
        private readonly Queue<DamageNumberView> _idle = new Queue<DamageNumberView>();

        public DamageNumberPool(GameObject prefab, Transform parent)
        {
            _prefab = prefab;
            _parent = parent;
        }

        public DamageNumberView Get()
        {
            while (_idle.Count > 0)
            {
                var pooled = _idle.Dequeue();
                if (pooled != null) return pooled;
            }
            var go = Object.Instantiate(_prefab, _parent);
            return go.GetComponent<DamageNumberView>();
        }

        public void Return(DamageNumberView view)
        {
            if (view == null) return;
            _idle.Enqueue(view);
        }
    }
}
