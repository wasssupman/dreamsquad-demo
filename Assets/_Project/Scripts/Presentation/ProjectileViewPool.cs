using System.Collections;
using System.Collections.Generic;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;
using Wassup.Data;

namespace Wassup.Presentation
{
    // Attached once to each instantiated view — caches Renderer[] so ApplyMpb
    // and ReturnToPool never call GetComponentsInChildren on hot paths.
    public class ViewRendererCache : MonoBehaviour
    {
        public Renderer[] renderers;
    }

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

        private static readonly int PropBaseColor    = Shader.PropertyToID("_BaseColor");
        private static readonly int PropColor        = Shader.PropertyToID("_Color");
        private static readonly int PropEmissionColor = Shader.PropertyToID("_EmissionColor");
        private static readonly int PropBaseMap      = Shader.PropertyToID("_BaseMap");
        private static readonly int PropMainTex      = Shader.PropertyToID("_MainTex");

        private readonly Dictionary<Entity, ProjectileViewState> _active = new();
        private readonly Dictionary<GameObject, Stack<GameObject>> _pool = new();
        private readonly List<Entity> _toReturn = new(8);
        private readonly List<(Entity entity, float3 pos)> _posUpdates = new(8);
        private readonly Dictionary<ProjectileData, int> _spawnCounters = new();
        private MaterialPropertyBlock _mpb;
        private System.Random _visualRng;

        private void Awake()
        {
            _mpb = new MaterialPropertyBlock();
            _visualRng = new System.Random();
        }

        // Call at battle-start with the session seed so visual jitter is reproducible.
        // Falls back to Awake's time-based seed when not called.
        public void Initialize(int seed)
        {
            _visualRng = new System.Random(seed);
            _spawnCounters.Clear();
        }

        public int ActiveCount => _active.Count;

        // Fix 1: initialPosition prevents first-frame wrong-direction rotation for AlongVelocity.
        public void Spawn(Entity entity, ProjectileData data, float3 initialPosition)
        {
            var view = GetOrCreate(data.projectilePrefab);
            view.SetActive(true);

            float scaleMul = 1f + (float)(_visualRng.NextDouble() * 2 - 1) * data.scaleJitter;
            view.transform.localScale = Vector3.one * (data.visualScale * scaleMul);

            float hueShift = (float)(_visualRng.NextDouble() * 2 - 1) * data.hueJitter;
            float rollDeg  = (float)(_visualRng.NextDouble() * 2 - 1) * data.rotationJitter;
            Color finalTint = ApplyHueShift(data.tintColor, hueShift);

            ApplyMpb(view, finalTint, data.emissionMultiplier, SelectTexture(data));

            // Fix 2: reset to prefab rotation before applying roll — no accumulation across pool reuse.
            view.transform.localRotation = data.projectilePrefab.transform.localRotation
                * Quaternion.Euler(0f, 0f, rollDeg);

            _active[entity] = new ProjectileViewState
            {
                view = view,
                prefab = data.projectilePrefab,
                facing = data.facing,
                spinSpeed = data.spinSpeed,
                lastPosition = initialPosition,   // Fix 1
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

        // Fix 5: hitVfxLifetime > 0 overrides auto-detect.
        public void PlayHit(GameObject hitPrefab, float3 position, float hitVfxLifetime = 0f)
        {
            var view = GetOrCreate(hitPrefab);
            view.SetActive(true);
            view.transform.position = new Vector3(position.x, position.y, position.z);
            float lifetime = hitVfxLifetime > 0f ? hitVfxLifetime : GetParticleLifetime(view);
            StartCoroutine(DespawnAfter(view, hitPrefab, lifetime));
        }

        public void PlayCast(GameObject castPrefab, Vector3 position, Vector3 facingDir, float lifetime = 0f)
        {
            var view = GetOrCreate(castPrefab);
            view.SetActive(true);
            view.transform.position = position;
            if (facingDir.sqrMagnitude > 0.0001f)
                view.transform.rotation = Quaternion.LookRotation(facingDir, Vector3.up);
            float life = lifetime > 0f ? lifetime : GetParticleLifetime(view);
            StartCoroutine(DespawnAfter(view, castPrefab, life));
        }

        public void DespawnAll()
        {
            foreach (var (_, state) in _active)
                ReturnToPool(state.view, state.prefab);
            _active.Clear();
        }

        private Texture2D SelectTexture(ProjectileData data)
        {
            if (data.textureVariants == null || data.textureVariants.Length == 0) return null;
            int idx = data.selectMode switch
            {
                TextureSelectMode.Sequential => GetAndIncrementCounter(data) % data.textureVariants.Length,
                TextureSelectMode.First => 0,
                _ => _visualRng.Next(data.textureVariants.Length),
            };
            return data.textureVariants[idx];
        }

        private int GetAndIncrementCounter(ProjectileData data)
        {
            _spawnCounters.TryGetValue(data, out int count);
            _spawnCounters[data] = count + 1;
            return count;
        }

        private void ApplyMpb(GameObject view, Color tint, float emissionMul, Texture2D texOverride = null)
        {
            Color emission = tint * emissionMul;
            // Fix 4: use cached renderers to avoid GC alloc on hot path.
            var renderers = view.TryGetComponent<ViewRendererCache>(out var cache)
                ? cache.renderers
                : view.GetComponentsInChildren<Renderer>(includeInactive: false);
            foreach (var r in renderers)
            {
                _mpb.Clear();
                _mpb.SetColor(PropBaseColor, tint);
                _mpb.SetColor(PropColor, tint);
                _mpb.SetColor(PropEmissionColor, emission);
                if (texOverride != null)
                {
                    _mpb.SetTexture(PropBaseMap, texOverride);
                    _mpb.SetTexture(PropMainTex, texOverride);
                }
                r.SetPropertyBlock(_mpb);
            }
        }

        public static Color ApplyHueShift(Color c, float hueShift)
        {
            Color.RGBToHSV(c, out float h, out float s, out float v);
            h = Mathf.Repeat(h + hueShift, 1f);
            var result = Color.HSVToRGB(h, s, v);
            result.a = c.a;
            return result;
        }

        private static float GetParticleLifetime(GameObject view)
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

        // Fix 4: attach ViewRendererCache on first Instantiate; pool reuse skips this.
        private GameObject GetOrCreate(GameObject prefab)
        {
            if (_pool.TryGetValue(prefab, out var stack) && stack.Count > 0)
                return stack.Pop();
            var view = Instantiate(prefab, transform);
            var rc = view.AddComponent<ViewRendererCache>();
            rc.renderers = view.GetComponentsInChildren<Renderer>(includeInactive: true);
            return view;
        }

        private void Return(Entity entity)
        {
            if (!_active.TryGetValue(entity, out var state)) return;
            ReturnToPool(state.view, state.prefab);
            _active.Remove(entity);
        }

        private void ReturnToPool(GameObject view, GameObject prefab)
        {
            // Fix 4: use cached renderers. Guard null — renderer may be destroyed
            // if coroutine fires after scene teardown (DespawnAfter timing).
            var renderers = view != null && view.TryGetComponent<ViewRendererCache>(out var cache)
                ? cache.renderers
                : (view != null ? view.GetComponentsInChildren<Renderer>(includeInactive: false) : System.Array.Empty<Renderer>());
            foreach (var r in renderers)
                if (r != null) r.SetPropertyBlock(null);
            if (view == null) return;
            view.SetActive(false);
            if (!_pool.ContainsKey(prefab)) _pool[prefab] = new Stack<GameObject>();
            _pool[prefab].Push(view);
        }
    }
}
