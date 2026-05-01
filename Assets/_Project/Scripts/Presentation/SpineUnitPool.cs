using System.Collections.Generic;
using Unity.Entities;
using UnityEngine;
using Wassup.Data;

namespace Wassup.Presentation
{
    public class SpineUnitPool : MonoBehaviour
    {
        private readonly Dictionary<Entity, SpineUnitView> _byEntity = new();
        private readonly List<Entity> _scratch = new();

        public bool TrySpawn(ISpineUnitVisualData visualData, IDefenderSpineExtras defenderExtras, Entity entity, Vector3 worldPos, string namePrefix, out SpineUnitView view)
        {
            view = null;
            if (visualData == null || visualData.SpineSkeletonDataAsset == null) return false;
            if (_byEntity.TryGetValue(entity, out view) && view != null) return true;

            string safeName = string.IsNullOrEmpty(visualData.SpineDisplayName) ? "Unit" : visualData.SpineDisplayName;
            var go = new GameObject($"{namePrefix}_{safeName}_{entity.Index}");
            go.transform.SetParent(transform, worldPositionStays: false);
            view = go.AddComponent<SpineUnitView>();
            view.Spawn(visualData, defenderExtras, entity, worldPos);
            _byEntity[entity] = view;
            return true;
        }

        public bool TryGet(Entity entity, out SpineUnitView view)
            => _byEntity.TryGetValue(entity, out view) && view != null;

        public void NotifyAttack(Entity entity, Vector3? targetWorld = null)
        {
            if (!_byEntity.TryGetValue(entity, out var view) || view == null) return;
            if (targetWorld.HasValue) view.FaceToward(targetWorld.Value);
            view.PlayAttack();
        }

        public bool TryResolveAnchor(Entity entity, out Vector3 worldPos)
        {
            if (TryGet(entity, out var view))
            {
                worldPos = view.ResolveCastAnchor();
                return true;
            }
            worldPos = default;
            return false;
        }

        public void NotifyDeath(Entity entity)
        {
            if (_byEntity.TryGetValue(entity, out var view) && view != null)
                view.Kill();
            _byEntity.Remove(entity);
        }

        public void Despawn(Entity entity)
        {
            if (!_byEntity.TryGetValue(entity, out var view)) return;
            _byEntity.Remove(entity);
            if (view != null) view.Dispose();
        }

        public void DespawnMissing(EntityManager entityManager)
        {
            _scratch.Clear();
            foreach (var kv in _byEntity)
            {
                if (kv.Value == null || !entityManager.Exists(kv.Key))
                    _scratch.Add(kv.Key);
            }
            for (int i = 0; i < _scratch.Count; i++)
                Despawn(_scratch[i]);
            _scratch.Clear();
        }

        public void DisposeAll()
        {
            foreach (var kv in _byEntity)
            {
                if (kv.Value != null) kv.Value.Dispose();
            }
            _byEntity.Clear();
        }
    }
}
