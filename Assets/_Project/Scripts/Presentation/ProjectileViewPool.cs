using System.Collections;
using System.Collections.Generic;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;
using Wassup.Data;

namespace Wassup.Presentation
{
    public class ProjectileViewPool : MonoBehaviour
    {
        private struct ProjectileViewState
        {
            public GameObject view;
            public GameObject prefab;
            public ProjectileFacing facing;
            public float spinSpeed;
            public float3 lastPosition;
        }

        private readonly Dictionary<Entity, ProjectileViewState> _active = new();
        private readonly Dictionary<GameObject, Stack<GameObject>> _pool = new();
        private readonly List<Entity> _toReturn = new(8);
        private readonly List<(Entity entity, float3 pos)> _posUpdates = new(8);

        public int ActiveCount => _active.Count;

        public void Spawn(Entity entity, GameObject prefab, float scale,
            ProjectileFacing facing = ProjectileFacing.AlongVelocity, float spinSpeed = 0f)
        {
            var view = GetOrCreate(prefab);
            view.SetActive(true);
            view.transform.localScale = Vector3.one * scale;
            _active[entity] = new ProjectileViewState
            {
                view = view,
                prefab = prefab,
                facing = facing,
                spinSpeed = spinSpeed,
                lastPosition = float3.zero,
            };
        }

        public void SyncTransforms(EntityManager em)
        {
            _toReturn.Clear();
            _posUpdates.Clear();
            float dt = Time.deltaTime;

            foreach (var (entity, state) in _active)
            {
                if (!em.Exists(entity))
                {
                    _toReturn.Add(entity);
                    continue;
                }

                var pos = em.GetComponentData<LocalTransform>(entity).Position;
                var view = state.view;
                view.transform.position = new Vector3(pos.x, pos.y, pos.z);

                switch (state.facing)
                {
                    case ProjectileFacing.AlongVelocity:
                        var vel = pos - state.lastPosition;
                        if (math.lengthsq(vel) > 0.0001f)
                            view.transform.rotation = Quaternion.LookRotation(
                                new Vector3(vel.x, vel.y, vel.z), Vector3.up);
                        break;
                    case ProjectileFacing.SpinAroundUp:
                        view.transform.Rotate(0f, state.spinSpeed * dt, 0f);
                        break;
                    case ProjectileFacing.FixedUp:
                    default:
                        break;
                }

                _posUpdates.Add((entity, pos));
            }

            // Apply lastPosition updates outside the foreach to avoid modifying during iteration
            foreach (var (entity, pos) in _posUpdates)
            {
                if (_active.TryGetValue(entity, out var s))
                    _active[entity] = new ProjectileViewState
                    {
                        view = s.view, prefab = s.prefab,
                        facing = s.facing, spinSpeed = s.spinSpeed,
                        lastPosition = pos,
                    };
            }

            foreach (var e in _toReturn) Return(e);
        }

        public void PlayHit(GameObject hitPrefab, float3 position)
        {
            var view = GetOrCreate(hitPrefab);
            view.SetActive(true);
            view.transform.position = new Vector3(position.x, position.y, position.z);
            StartCoroutine(DespawnAfter(view, hitPrefab, GetParticleLifetime(view)));
        }

        public void DespawnAll()
        {
            foreach (var (_, state) in _active)
                ReturnToPool(state.view, state.prefab);
            _active.Clear();
        }

        private float GetParticleLifetime(GameObject view)
        {
            float max = 0f;
            foreach (var ps in view.GetComponentsInChildren<ParticleSystem>())
            {
                var main = ps.main;
                float candidate = main.duration + main.startLifetime.constantMax;
                if (candidate > max) max = candidate;
            }
            return max > 0f ? max : 1.5f;
        }

        private IEnumerator DespawnAfter(GameObject view, GameObject prefab, float delay)
        {
            yield return new WaitForSeconds(delay);
            ReturnToPool(view, prefab);
        }

        private GameObject GetOrCreate(GameObject prefab)
        {
            if (_pool.TryGetValue(prefab, out var stack) && stack.Count > 0)
                return stack.Pop();
            return Instantiate(prefab, transform);
        }

        private void Return(Entity entity)
        {
            if (!_active.TryGetValue(entity, out var state)) return;
            ReturnToPool(state.view, state.prefab);
            _active.Remove(entity);
        }

        private void ReturnToPool(GameObject view, GameObject prefab)
        {
            view.SetActive(false);
            if (!_pool.ContainsKey(prefab)) _pool[prefab] = new Stack<GameObject>();
            _pool[prefab].Push(view);
        }
    }
}
