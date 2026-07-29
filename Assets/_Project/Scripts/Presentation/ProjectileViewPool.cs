using System.Collections;
using System.Collections.Generic;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;
using Wassup.Battle.Combat.Projectile;
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
            // bomb-thrower-defender unit 5 — GrenadeToCell 퓨즈 점멸 스케일 펄스의 기준값.
            public float baseScale;
            public float3 lastPosition;
            public float heightOffset;   // view 공간 Y 렌더 오프셋 (ECS/velocity 엔 미반영)
            // unit 9 — SkyFall 낙하 압축(뷰 전용): 낙하가 비행 후반 이 비율에 압축된다.
            // 1 = 전체 구간 등속. ECS state 를 늘리지 않고 view 딕셔너리에 태운다.
            public float fallPortion;
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
        // initialDropOffset (unit 9, SkyFall): 첫 프레임 뷰를 낙하 시작 높이에서 시작시킨다 —
        // 지면에 스폰 후 첫 Sync 에서 하늘로 점프하면 풀링 TrailRenderer 가 스트릭을 긋는다.
        public void Spawn(Entity entity, ProjectileData data, float3 initialPosition, float initialDropOffset = 0f)
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
            // 낙하 오프셋은 SyncTransforms 의 pos(y+=drop) 와 같은 축(view-Y)에 선반영 —
            // lastPosition 에도 포함해야 첫 프레임 velocity 가 ≈0 이 되어(지면→하늘
            // 오차분이 안 섞여) 잘못된 위쪽 페이싱 플래시가 없다.
            spawnView.y += initialDropOffset;
            view.transform.position = new Vector3(spawnView.x, spawnView.y + data.visualHeightOffset, spawnView.z);
            ResetVfx(view);

            _active[entity] = new ProjectileViewState
            {
                view = view,
                prefab = data.projectilePrefab,
                facing = data.facing,
                spinSpeed = data.spinSpeed,
                baseScale = data.visualScale * scaleMul,
                // tilemap-view-backend unit 3 — lastPosition 은 view 좌표로 보존(velocity 를 view 공간에서 계산).
                lastPosition = spawnView,   // Fix 1 (heightOffset 미포함 = 순수 위치, velocity 정확)
                heightOffset = data.visualHeightOffset,
                fallPortion = data.fallPortion,
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

                // Ballistic arc height is a presentation concern: BoardSpace.ToView drops
                // sim Y on the flat board, so the visible parabola is added here in view
                // space. Folding it into `pos` (before velocity) also pitches the shell
                // along the arc for AlongVelocity facing.
                if (em.HasComponent<ProjectileState>(entity))
                {
                    var ps = em.GetComponentData<ProjectileState>(entity);
                    if (ps.movement == MovementKind.BallisticArcToPoint && ps.flightTime > 0f)
                        pos.y += BallisticArc.ArcHeight(ps.arcHeight, math.saturate(ps.elapsed / ps.flightTime));
                    // projectile-emission-pattern unit 1 — 베지어 호밍의 3축 중 Y.
                    // sim 은 XZ 곡선만 굴리므로(BoardSpace 가 sim-Y 를 drop) 높이는
                    // 여기서만 생긴다. ArcHeight 재사용 = 신규 수학 0줄, pos 에 접혀
                    // AlongVelocity 페이싱이 곡선을 따라 피칭한다.
                    else if (ps.movement == MovementKind.BezierHomingToEntity && ps.flightTime > 0f)
                        pos.y += BallisticArc.ArcHeight(ps.arcHeight, math.saturate(ps.elapsed / ps.flightTime));
                    // unit 9 — SkyFall 낙하: arcHeight 슬롯 = 낙하 시작 높이. sim 은 착탄 셀에
                    // 고정이므로 화면 낙하는 전부 여기 view-Y 로 표현된다. pos 에 접혀
                    // AlongVelocity 페이싱이 아래를 향하고 트레일이 위로 남는다.
                    // fallPortion < 1 이면 낙하를 비행 후반에 압축하고, 대기(pre-fall)
                    // 구간엔 뷰를 숨긴다 — 상공 호버가 화면에 보이는 어색함 제거. 낙하
                    // 시작 프레임에 시작 높이에서 등장(transform 이 그 높이를 유지하고
                    // 있어 트레일 스트릭 없음, 파티클은 활성화 시점에 신선 재생).
                    // 텔레그래프(flightTime·데미지 타이밍)는 불변, 시각만 바뀐다.
                    else if (ps.movement == MovementKind.SkyFall)
                    {
                        float p = SkyFall.Progress(ps.elapsed, ps.flightTime);
                        float fp = state.fallPortion;
                        bool falling = fp >= 1f || p >= 1f - fp;
                        if (state.view.activeSelf != falling)
                        {
                            state.view.SetActive(falling);
                            // reveal 시 파티클/트레일 명시 재생 — prefab 의 playOnAwake
                            // 에 암묵 의존하지 않는다(리뷰 M1). 위치는 대기 내내 시작
                            // 높이에 고정돼 있어 트레일 스트릭 없음.
                            if (falling) ResetVfx(state.view);
                        }
                        pos.y += ps.arcHeight * (1f - SkyFall.FallProgress(p, fp));
                    }
                    // bomb-thrower-defender unit 5 — 구르기 arc(travel 낮은 arc; 퓨즈엔 t=1
                    // → ArcHeight 0 = 지면 정지) + 착지 후 폭발 예고 스케일 점멸.
                    else if (ps.movement == MovementKind.GrenadeToCell)
                    {
                        float gt = ps.flightTime > 0f ? math.saturate(ps.elapsed / ps.flightTime) : 1f;
                        // 통통 튀기며 굴러감: 감쇠하는 다중 바운스(착지점 gt=1 에서 0 → 지면).
                        // arcHeight = 첫 바운스 최대 높이(SO), BounceHops = 이동 중 튀는 횟수.
                        const float BounceHops = 3f;
                        pos.y += ps.arcHeight * math.abs(math.sin(math.PI * gt * BounceHops)) * (1f - gt);
                        if (ps.elapsed >= ps.flightTime)
                        {
                            float fuseT = ps.elapsed - ps.flightTime;
                            float pulse = 1f + 0.18f * math.sin(fuseT * 18f);
                            state.view.transform.localScale = Vector3.one * (state.baseScale * pulse);
                        }
                    }
                }

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
                    case ProjectileFacing.RollAlongPath:
                    {
                        // bomb-thrower-defender unit 5 — 데굴데굴: 진행방향 수직 수평축 기준
                        // tumble. 이동 중일 때만(퓨즈/착지 시 vel≈0 → 정지). spinSpeed = 굴림 속도.
                        var rvel = pos - state.lastPosition;
                        var rflat = new Vector3(rvel.x, 0f, rvel.z);
                        if (rflat.sqrMagnitude > 0.0001f)
                            view.transform.Rotate(Vector3.Cross(Vector3.up, rflat.normalized), state.spinSpeed * dt, Space.World);
                        break;
                    }
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
                        baseScale = s.baseScale,
                        lastPosition = pos,
                        heightOffset = s.heightOffset,
                        fallPortion = s.fallPortion,
                    };
            }

            foreach (var e in _toReturn) Return(e);
        }

        // Fix 5: hitVfxLifetime > 0 overrides auto-detect.
        // facingViewDir: 타격 방향(**view 공간**). 지정하면 VFX 가 그 방향으로 회전한다
        // (말파이트 흙 폭발처럼 방향성이 있는 히트용). 기본 default = 회전 없음(기존 동작).
        public void PlayHit(GameObject hitPrefab, float3 position, float hitVfxLifetime = 0f,
                            float heightOffset = 0f, float scale = 1f, Vector3 facingViewDir = default,
                            Vector3 eulerOffset = default)
        {
            var view = GetOrCreate(hitPrefab);
            view.SetActive(true);
            view.transform.localScale = Vector3.one * scale;   // 원본이 작으면 키움
            // ⚠ 위 줄이 프리팹 스케일을 **덮는다** — 크기 조절은 프리팹이 아니라 이 인자로.
            if (facingViewDir.sqrMagnitude > 0.0001f)
            {
                // ⚠ 방향을 그대로 forward 로 주면 안 된다. LookRotation 은 up 을 "가능한 한"
                // 맞출 뿐이라, 방향이 up 쪽 성분을 가지면 이펙트가 통째로 기운다 — 근접 유닛은
                // 대상이 화면상 위아래로 붙는 일이 많아 폭발이 바닥을 뚫고 들어갔다(제보).
                //
                // 그래서 **up 축을 고정**하고 방향은 그 축 둘레의 회전(yaw)만 담당하게 한다:
                // 방향을 up 에 수직인 평면으로 투영하면 up 이 정확히 보존된다.
                // 지면형 폭발이므로 up = 월드 up(위로 솟는다). 카메라 pitch 는 알아서 비춘다.
                Vector3 upRef = Vector3.up;
                Vector3 planar = Vector3.ProjectOnPlane(facingViewDir, upRef);
                if (planar.sqrMagnitude > 1e-6f)
                    view.transform.rotation = Quaternion.LookRotation(planar, upRef);
                // 완전히 수직인 방향(투영이 0)이면 회전을 건드리지 않는다 — 프리팹 기본 자세 유지.
            }
            float3 hitView = Wassup.Core.BoardSpace.ToView(position); // sim→view
            // heightOffset: 바닥에 깔리지 않게 view Y 로 띄움 (투사체 visualHeightOffset 과 동일 개념).
            view.transform.position = new Vector3(hitView.x, hitView.y + heightOffset, hitView.z);
            // 기본 자세 보정. 계산된 회전 **뒤에** 곱해 로컬 축 기준으로 돈다.
            // facing 을 안 쓰는 이펙트도 이 값만으로 자세를 잡을 수 있다.
            if (eulerOffset != Vector3.zero)
                view.transform.rotation = view.transform.rotation * Quaternion.Euler(eulerOffset);

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
