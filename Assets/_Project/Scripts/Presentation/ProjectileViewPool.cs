using System.Collections.Generic;
using Unity.Entities;
using Unity.Transforms;
using UnityEngine;

namespace Wassup.Presentation
{
    public class ProjectileViewPool : MonoBehaviour
    {
        private readonly Dictionary<Entity, GameObject> _active = new();
        private readonly Dictionary<GameObject, GameObject> _prefabFor = new();
        private readonly Dictionary<GameObject, Stack<GameObject>> _pool = new();

        public void Spawn(Entity entity, GameObject prefab, float scale)
        {
            var view = GetOrCreate(prefab);
            view.SetActive(true);
            view.transform.localScale = Vector3.one * scale;
            _active[entity] = view;
            _prefabFor[view] = prefab;
        }

        public void SyncTransforms(EntityManager em)
        {
            var toReturn = new List<Entity>(4);
            foreach (var (entity, view) in _active)
            {
                if (!em.Exists(entity))
                {
                    toReturn.Add(entity);
                    continue;
                }
                var pos = em.GetComponentData<LocalTransform>(entity).Position;
                view.transform.position = new Vector3(pos.x, pos.y, pos.z);
            }
            foreach (var e in toReturn) Return(e);
        }

        public void DespawnAll()
        {
            foreach (var (_, view) in _active)
                ReturnToPool(view);
            _active.Clear();
        }

        private GameObject GetOrCreate(GameObject prefab)
        {
            if (_pool.TryGetValue(prefab, out var stack) && stack.Count > 0)
                return stack.Pop();
            return Instantiate(prefab, transform);
        }

        private void Return(Entity entity)
        {
            if (!_active.TryGetValue(entity, out var view)) return;
            ReturnToPool(view);
            _active.Remove(entity);
        }

        private void ReturnToPool(GameObject view)
        {
            view.SetActive(false);
            if (_prefabFor.TryGetValue(view, out var prefab))
            {
                if (!_pool.ContainsKey(prefab)) _pool[prefab] = new Stack<GameObject>();
                _pool[prefab].Push(view);
            }
        }
    }
}
