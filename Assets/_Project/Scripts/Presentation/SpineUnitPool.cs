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

        public void NotifyAttack(Entity entity, Vector3? targetWorld = null, float attackAnimPeriod = 0f)
        {
            if (!_byEntity.TryGetValue(entity, out var view) || view == null) return;
            if (targetWorld.HasValue) view.FaceToward(targetWorld.Value);
            view.PlayAttack(attackAnimPeriod);
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

        public bool TryResolveProjectileLaunchAnchor(Entity entity, out Vector3 worldPos)
        {
            if (TryGet(entity, out var view))
            {
                worldPos = view.ResolveProjectileLaunchAnchor();
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

        // defender-clock-out unit 3 — **세 번째 출구.** 풀 관리에서만 떼고 파괴하지 않는다.
        //   NotifyDeath = 사망 애니 재생 후 자멸 · Despawn = 즉시 Dispose · Detach = 호출자에게 양도
        // 퇴근 연출이 엔티티 파괴 뒤에도 뷰를 아치로 날려야 해서 필요하다(sim 은 즉시 끝나고
        // 뷰만 남는 보스 도약과 같은 형태).
        //
        // ⚠ **반환된 뷰의 수명은 호출자 것이다.** 풀이 더 이상 모르므로 teardown/DespawnMissing 이
        // 치워주지 않는다 — 호출자가 반드시 Dispose 해야 고아 GameObject 가 안 남는다.
        public bool Detach(Entity entity, out SpineUnitView view)
        {
            if (!_byEntity.TryGetValue(entity, out view) || view == null) { view = null; return false; }
            _byEntity.Remove(entity);
            return true;
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
