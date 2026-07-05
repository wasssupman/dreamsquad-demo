using System.Collections.Generic;
using Unity.Entities;
using UnityEngine;
using Wassup.Core.TimeControl;
using Wassup.Data;

namespace Wassup.Presentation
{
    public class SpineUnitPool : MonoBehaviour
    {
        private readonly Dictionary<Entity, SpineUnitView> _byEntity = new();
        private readonly List<Entity> _scratch = new();

        // time-manager Unit 4 — Battle 스케일 변화 시 활성 유닛 애니 속도 fan-out.
        // (스폰 순간 초기화는 SpineUnitView.Spawn 이 pull 로 처리 — 스폰 레이스 방지.)
        private void OnEnable() => TimeManager.Instance.ScaleChanged += OnBattleScaleChanged;
        private void OnDisable() => TimeManager.Instance.ScaleChanged -= OnBattleScaleChanged;

        private void OnBattleScaleChanged(TimeDomain domain, float scale)
        {
            if (domain != TimeDomain.Battle) return;
            foreach (var kv in _byEntity)
                if (kv.Value != null) kv.Value.SetAnimationTimeScale(scale);
        }

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
