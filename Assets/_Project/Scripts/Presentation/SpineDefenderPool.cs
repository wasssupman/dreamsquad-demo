using System.Collections.Generic;
using Unity.Entities;
using UnityEngine;
using Wassup.Data;

namespace Wassup.Presentation
{
    // Phase 8 — tracks one SpineDefenderView per live defender entity and
    // routes ECS-side triggers (attack / death / teardown) to the matching
    // view. Not a singleton — BattleBridge holds the SerializeField reference.
    public class SpineDefenderPool : MonoBehaviour
    {
        private readonly Dictionary<Entity, SpineDefenderView> _byEntity = new();

        public bool TrySpawn(DefenderUnitData unitData, Entity entity, Vector3 worldPos, out SpineDefenderView view)
        {
            view = null;
            if (unitData == null) return false;
            if (unitData.skeletonDataAsset == null || string.IsNullOrEmpty(unitData.spineSkinName)) return false;

            var go = new GameObject($"SpineDef_{unitData.displayName}_{entity.Index}");
            go.transform.SetParent(transform, worldPositionStays: false);
            view = go.AddComponent<SpineDefenderView>();
            view.Spawn(unitData, entity, worldPos);
            _byEntity[entity] = view;
            return true;
        }

        public bool TryGet(Entity entity, out SpineDefenderView view)
            => _byEntity.TryGetValue(entity, out view);

        public void NotifyAttack(Entity entity, Vector3? targetWorld = null)
        {
            if (_byEntity.TryGetValue(entity, out var view) && view != null)
            {
                if (targetWorld.HasValue) view.FaceToward(targetWorld.Value);
                view.PlayAttack();
            }
        }

        public void NotifyDeath(Entity entity)
        {
            if (_byEntity.TryGetValue(entity, out var view) && view != null) view.Kill();
            _byEntity.Remove(entity);
        }

        // BattleBridge.TeardownCurrentBattle calls this to drop every view
        // without waiting for death animations — a round reset is not a death.
        public void DisposeAll()
        {
            foreach (var kv in _byEntity)
            {
                if (kv.Value != null) kv.Value.Dispose();
            }
            _byEntity.Clear();
        }

        // Phase 8 (v2): facing is now snapped at fire time (NotifyAttack) rather
        // than chased continuously. Removing LateUpdate polling prevents the
        // "skeleton wobbles toward the closest passing enemy" artifact. If a
        // defender never fires, it keeps its natural skeleton orientation.
    }
}
