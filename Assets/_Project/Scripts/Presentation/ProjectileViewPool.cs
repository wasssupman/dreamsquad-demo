using System.Collections;
using System.Collections.Generic;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;
using Wassup.Data;

namespace Wassup.Presentation
{
    // Attached once to each instantiated view — caches component arrays so ApplyMpb,
    // ReturnToPool, and ResetVfx never call GetComponentsInChildren on hot paths.
    public class ViewRendererCache : MonoBehaviour
    {
        public Renderer[] renderers;
        public TrailRenderer[] trails;
        // Top-level particle systems only. Play(true) cascades to children/subemitters,
        // so restarting only roots preserves authored trigger relationships.
        public ParticleSystem[] rootParticles;
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
            public float heightOffset;   // view 공간 Y 렌더 오프셋 (ECS/velocity 엔 미반영)
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

            // ga-reskin unit 1: preserveVfxColors 면 데이터 recolor(tint/emission/texture)를 건너뛰고
            // 프리팹 머티리얼 고유 색을 그대로 쓴다. GA 처럼 _Color(HDR 밝기)·_EmissionColor 가 이미
            // authored 된 VFX 는 MPB 흰색 덮어쓰기로 밝기/색이 죽으므로 as-is 재현에 필수.
            // RNG draw 수는 위에서 항상 동일하게 소비 → 시각 결정성 유지.
            if (!data.preserveVfxColors)
            {
                Color finalTint = ApplyHueShift(data.tintColor, hueShift);
                ApplyMpb(view, finalTint, data.emissionMultiplier, SelectTexture(data));
            }

            // Fix 2: reset to prefab rotation before applying roll — no accumulation across pool reuse.
            view.transform.localRotation = data.projectilePrefab.transform.localRotation
                * Quaternion.Euler(0f, 0f, rollDeg);

            // ga-reskin unit 1: 첫 SyncTransforms 전에 스폰 위치를 즉시 세팅하고 trail/particle 을
            // 리셋한다. 안 그러면 풀 재사용 시 이전 사망 위치 → 새 스폰 위치로 world-space 파티클/
            // TrailRenderer 가 streak(줄) 을 그린다.
            float3 spawnView = Wassup.Core.BoardSpace.ToView(initialPosition);
            view.transform.position = new Vector3(spawnView.x, spawnView.y + data.visualHeightOffset, spawnView.z);
            ResetVfx(view);

            _active[entity] = new ProjectileViewState
            {
                view = view,
                prefab = data.projectilePrefab,
                facing = data.facing,
                spinSpeed = data.spinSpeed,
                // tilemap-view-backend unit 3 — lastPosition 은 view 좌표로 보존(velocity 를 view 공간에서 계산).
                lastPosition = spawnView,   // Fix 1 (오프셋 미포함 = 순수 위치, velocity 정확)
                heightOffset = data.visualHeightOffset,
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

                var simPos = em.GetComponentData<LocalTransform>(entity).Position;
                // sim→view 1회. 위치·속도·LookRotation 전부 view 공간끼리 (lastPosition 도 view).
                float3 pos = Wassup.Core.BoardSpace.ToView(simPos);
                var view = state.view;
                view.transform.position = new Vector3(pos.x, pos.y + state.heightOffset, pos.z);

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
                        heightOffset = s.heightOffset,
                    };
            }

            foreach (var e in _toReturn) Return(e);
        }

        // Fix 5: hitVfxLifetime > 0 overrides auto-detect.
        public void PlayHit(GameObject hitPrefab, float3 position, float hitVfxLifetime = 0f)
        {
            var view = GetOrCreate(hitPrefab);
            view.SetActive(true);
            float3 hitView = Wassup.Core.BoardSpace.ToView(position); // sim→view
            view.transform.position = new Vector3(hitView.x, hitView.y, hitView.z);
            ResetVfx(view);   // ga-reskin unit 1: 풀 재사용 시 파티클 재생 신선도
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
            ResetVfx(view);   // ga-reskin unit 1: 풀 재사용 시 파티클 재생 신선도
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
            rc.trails = view.GetComponentsInChildren<TrailRenderer>(includeInactive: true);
            rc.rootParticles = ComputeRootParticles(
                view.transform, view.GetComponentsInChildren<ParticleSystem>(includeInactive: true));
            // 투사체/hit/cast VFX 를 유닛 스프라이트 위로. Instantiate 당 1회만(풀 재사용은
            // stack.Pop 으로 빠져 스킵) → 누적 없음. 렌더러 간 상대 순서(mesh/trail/flare)는 보존.
            foreach (var r in rc.renderers)
                r.sortingOrder += BoardSortOrder.ProjectileOffset;
            return view;
        }

        // ga-reskin unit 1: 풀 재사용 시 잔상 제거 + 파티클 신선 재생.
        // 캐시된 배열만 순회하므로 핫패스 GetComponentsInChildren 없음.
        // 가정: top-level PS 는 스폰 시 재생돼야 하는 시스템(현재 GA 투사체/hit/muzzle 은 모두
        // "play now"). playOnAwake=false 로 지연 트리거되는 루트 시스템을 쓰는 프리팹이 생기면
        // 이 강제 Play(true) 가 authored 타이밍을 깨므로 그때 재검토.
        private static void ResetVfx(GameObject view)
        {
            if (!view.TryGetComponent<ViewRendererCache>(out var cache)) return;
            if (cache.trails != null)
                foreach (var t in cache.trails)
                    if (t != null) t.Clear();
            if (cache.rootParticles != null)
                foreach (var p in cache.rootParticles)
                    if (p != null) { p.Clear(true); p.Play(true); }
        }

        // 조상에 ParticleSystem 이 없는 top-level PS 만 추림. Play(true) 가 자식/서브에미터로
        // cascade 되므로, 루트만 재시작하면 authored 트리거 관계를 깨지 않는다.
        // 탐색은 프리팹 내부로 한정(viewRoot 위 풀 계층까지 올라가지 않음).
        private static ParticleSystem[] ComputeRootParticles(Transform viewRoot, ParticleSystem[] all)
        {
            var stopAt = viewRoot.parent; // 풀 컨테이너 — 여기 도달 전까지만 조상 검사.
            var roots = new List<ParticleSystem>(all.Length);
            foreach (var p in all)
            {
                bool nested = false;
                for (var t = p.transform.parent; t != null && t != stopAt; t = t.parent)
                {
                    if (t.GetComponent<ParticleSystem>() != null) { nested = true; break; }
                }
                if (!nested) roots.Add(p);
            }
            return roots.ToArray();
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
