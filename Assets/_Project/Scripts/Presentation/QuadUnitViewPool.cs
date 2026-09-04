using System.Collections.Generic;
using Unity.Entities;
using UnityEngine;

namespace Wassup.Presentation
{
    [DisallowMultipleComponent]
    public class QuadUnitViewPool : MonoBehaviour
    {
        private readonly Dictionary<Entity, QuadUnitView> _byEntity = new();
        private readonly List<Entity> _scratch = new();

        public bool TrySpawn(
            string displayName,
            Entity entity,
            Vector3 worldPos,
            Mesh mesh,
            Material material,
            float visualScale,
            // unit 15 — 판정 몸 반경(타일). 블롭 지름 = 2r. 기본값을 두지 않는다:
            // 호출처마다 정답이 다르고(디펜더=가로/2 파생 / 적=티어), 기본값이 있으면 새
            // 호출처가 조용히 표준값을 받는다 — bodyRadiusTiles 시절 실제로 잡힌 사고다.
            float bodyRadiusTiles,
            out QuadUnitView view)
        {
            if (_byEntity.TryGetValue(entity, out view) && view != null)
                return true;

            var safeName = string.IsNullOrEmpty(displayName) ? "Unit" : displayName;
            var go = new GameObject($"QuadUnit_{safeName}_{entity.Index}");
            go.transform.SetParent(transform, worldPositionStays: false);
            go.transform.position = Wassup.Core.BoardSpace.ToView(worldPos); // sim→view (다음 UpdatePosition 이 sim 보존)
            view = go.AddComponent<QuadUnitView>();
            view.Configure(entity, mesh, material, visualScale, bodyRadiusTiles);
            _byEntity[entity] = view;
            return true;
        }

        public bool TryGet(Entity entity, out QuadUnitView view)
            => _byEntity.TryGetValue(entity, out view) && view != null;

        public void Despawn(Entity entity)
        {
            if (!_byEntity.TryGetValue(entity, out var view)) return;
            _byEntity.Remove(entity);
            if (view != null) Destroy(view.gameObject);
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
                if (kv.Value != null) Destroy(kv.Value.gameObject);
            }
            _byEntity.Clear();
        }
    }
}
