using Unity.Entities;
using UnityEngine;

namespace Wassup.Battle.Effects
{
    public class BlockingHazardPresenter : MonoBehaviour
    {
        [SerializeField] private GameObject spawnVfxPrefab;

        public Entity Entity { get; private set; }

        public void SetSpawnVfxPrefab(GameObject prefab)
        {
            spawnVfxPrefab = prefab;
        }

        public void Bind(Entity entity)
        {
            Entity = entity;
            SpawnVfx();
        }

        public void OnDestroyed(GameObject vfxPrefab)
        {
            if (vfxPrefab != null)
            {
                // ⚠ 파괴 VFX 는 **부모 없이** 뜬다(설치물이 곧 사라지므로 자식으로 달 수 없다).
                // 그래서 스스로 치우지 않으면 판에 영구히 쌓인다 — 실측으로 폭발 VFX 루트가
                // 42개까지 누적됐고, 화면에서는 「터졌는데 안 사라진다」로 읽혔다.
                // 벤더 VFX 는 stopAction 이 None 이라 자기소멸을 기대할 수 없다.
                var fx = Instantiate(vfxPrefab, transform.position, Quaternion.identity);
                Destroy(fx, EstimateVfxLifetime(fx));
            }
            else
            {
                SpawnProceduralDestroyVfx(transform.position);
            }

            Destroy(gameObject);
        }

        // 프리팹이 다 재생되는 데 걸리는 시간의 상한. 파티클마다 duration + 최대 수명 +
        // 최대 지연을 더해 가장 긴 것을 고른다. 파티클이 없으면 짧은 기본값.
        private static float EstimateVfxLifetime(GameObject instance)
        {
            const float fallback = 2f;
            const float cap = 12f;
            if (instance == null) return fallback;
            var systems = instance.GetComponentsInChildren<ParticleSystem>(true);
            float longest = 0f;
            for (int i = 0; i < systems.Length; i++)
            {
                var main = systems[i].main;
                float total = main.duration + main.startLifetime.constantMax + main.startDelay.constantMax;
                if (total > longest) longest = total;
            }
            if (longest <= 0f) return fallback;
            return Mathf.Min(longest + 0.5f, cap);
        }

        private void SpawnVfx()
        {
            if (spawnVfxPrefab != null)
            {
                Instantiate(spawnVfxPrefab, transform.position, Quaternion.identity, transform);
                return;
            }

            SpawnProceduralSpawnVfx(transform.position);
        }

        private static void SpawnProceduralSpawnVfx(Vector3 position)
        {
            var root = new GameObject("BlockingHazard_SpawnVFX_Runtime");
            root.transform.position = position + Vector3.up * 0.08f;

            var dust = CreateParticleMaterial(new Color(0.58f, 0.52f, 0.42f, 0.58f));
            var rock = CreateParticleMaterial(new Color(0.40f, 0.38f, 0.34f, 1f));
            CreateFallingRockParticles(root.transform, rock);
            CreateSettledRockStackParticles(root.transform, rock);
            CreateBurst(root.transform, "Spawn_Dust_Ring", dust, 30, 0.10f, 0.30f, 0.45f, 1.15f, 0.35f, 0.8f, 0.0f, ParticleSystemShapeType.Circle, 1.45f);
            CreateBurst(root.transform, "Spawn_Grit", dust, 18, 0.05f, 0.13f, 0.55f, 1.25f, 0.25f, 0.55f, 1.0f, ParticleSystemShapeType.Hemisphere, 0.85f);

            Destroy(root, 2.8f);
            Destroy(dust, 2.9f);
            Destroy(rock, 2.9f);
        }

        private static void SpawnProceduralDestroyVfx(Vector3 position)
        {
            var root = new GameObject("BlockingHazard_DestroyVFX_Runtime");
            root.transform.position = position + Vector3.up * 0.25f;

            var dust = CreateParticleMaterial(new Color(0.55f, 0.48f, 0.38f, 0.75f));
            CreateBurst(root.transform, "Destroy_Dust_Burst", dust, 52, 0.12f, 0.46f, 1.0f, 2.7f, 0.35f, 0.9f, 0.1f, ParticleSystemShapeType.Hemisphere, 1.4f);
            CreateBurst(root.transform, "Destroy_Debris_Burst", dust, 24, 0.07f, 0.18f, 0.75f, 1.75f, 0.45f, 0.85f, 1.7f, ParticleSystemShapeType.Hemisphere, 0.8f);

            Destroy(root, 1.4f);
            Destroy(dust, 1.5f);
        }

        private static void CreateFallingRockParticles(Transform parent, Material material)
        {
            var go = new GameObject("Spawn_Falling_Rock_Particles");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = new Vector3(0f, 40f, 0f);

            var ps = go.AddComponent<ParticleSystem>();
            PrepareForRuntimeConfiguration(ps);
            var main = ps.main;
            main.loop = false;
            main.duration = 0.2f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.88f, 1.08f);
            main.startSpeed = 0f;
            main.startSize = new ParticleSystem.MinMaxCurve(0.10f, 0.24f);
            main.gravityModifier = 0f;
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.startColor = new ParticleSystem.MinMaxGradient(
                new Color(0.24f, 0.23f, 0.21f, 0.95f),
                new Color(0.56f, 0.53f, 0.48f, 0.95f));

            var emission = ps.emission;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 46) });

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = 1.35f;
            shape.rotation = new Vector3(90f, 0f, 0f);

            var velocity = ps.velocityOverLifetime;
            velocity.enabled = true;
            velocity.x = new ParticleSystem.MinMaxCurve(0f);
            velocity.y = new ParticleSystem.MinMaxCurve(-39f);
            velocity.z = new ParticleSystem.MinMaxCurve(0f);

            var size = ps.sizeOverLifetime;
            size.enabled = true;
            var sizeCurve = new AnimationCurve(
                new Keyframe(0f, 0.65f),
                new Keyframe(0.12f, 1.0f),
                new Keyframe(0.82f, 1.0f),
                new Keyframe(1f, 0f));
            size.size = new ParticleSystem.MinMaxCurve(1f, sizeCurve);

            var rotation = ps.rotationOverLifetime;
            rotation.enabled = true;
            rotation.x = new ParticleSystem.MinMaxCurve(0f, 0f);
            rotation.y = new ParticleSystem.MinMaxCurve(0f, 0f);
            rotation.z = new ParticleSystem.MinMaxCurve(-10f, 10f);

            var renderer = ps.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = material;
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.minParticleSize = 0.02f;
            renderer.maxParticleSize = 0.30f;
            ps.Play();
        }

        private static void CreateSettledRockStackParticles(Transform parent, Material material)
        {
            var go = new GameObject("Spawn_Settled_Rock_Stack_Particles");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = new Vector3(0f, 0.16f, 0f);

            var ps = go.AddComponent<ParticleSystem>();
            PrepareForRuntimeConfiguration(ps);
            var main = ps.main;
            main.loop = false;
            main.duration = 0.12f;
            main.startDelay = 0.9f;
            main.startLifetime = 1.55f;
            main.startSpeed = 0f;
            main.startSize = new ParticleSystem.MinMaxCurve(0.11f, 0.26f);
            main.gravityModifier = 0f;
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.startColor = new ParticleSystem.MinMaxGradient(
                new Color(0.28f, 0.27f, 0.25f, 0.95f),
                new Color(0.62f, 0.59f, 0.52f, 0.95f));

            var emission = ps.emission;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 52) });

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = 1.35f;
            shape.rotation = new Vector3(90f, 0f, 0f);

            var velocity = ps.velocityOverLifetime;
            velocity.enabled = false;

            var size = ps.sizeOverLifetime;
            size.enabled = true;
            var sizeCurve = new AnimationCurve(
                new Keyframe(0f, 0.0f),
                new Keyframe(0.08f, 1.0f),
                new Keyframe(0.78f, 1.0f),
                new Keyframe(1f, 0.0f));
            size.size = new ParticleSystem.MinMaxCurve(1f, sizeCurve);

            var renderer = ps.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = material;
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.minParticleSize = 0.02f;
            renderer.maxParticleSize = 0.30f;
            ps.Play();
        }

        private static void CreateBurst(
            Transform parent,
            string name,
            Material material,
            short count,
            float minSize,
            float maxSize,
            float minSpeed,
            float maxSpeed,
            float minLifetime,
            float maxLifetime,
            float gravity,
            ParticleSystemShapeType shapeType,
            float radius)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var ps = go.AddComponent<ParticleSystem>();
            PrepareForRuntimeConfiguration(ps);

            var main = ps.main;
            main.loop = false;
            main.duration = 1f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(minLifetime, maxLifetime);
            main.startSpeed = new ParticleSystem.MinMaxCurve(minSpeed, maxSpeed);
            main.startSize = new ParticleSystem.MinMaxCurve(minSize, maxSize);
            main.gravityModifier = gravity;
            main.startColor = new ParticleSystem.MinMaxGradient(
                new Color(0.34f, 0.31f, 0.27f, 0.82f),
                new Color(0.72f, 0.66f, 0.55f, 0.50f));

            var emission = ps.emission;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, count) });

            var shape = ps.shape;
            shape.shapeType = shapeType;
            shape.radius = radius;
            if (shapeType == ParticleSystemShapeType.Circle)
                shape.rotation = new Vector3(90f, 0f, 0f);

            var renderer = ps.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = material;
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            ps.Play();
        }

        private static void PrepareForRuntimeConfiguration(ParticleSystem ps)
        {
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.playOnAwake = false;
        }

        private static Material CreateParticleMaterial(Color color)
        {
            var shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
            if (shader == null) shader = Shader.Find("Particles/Standard Unlit");
            if (shader == null) shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Standard");

            var material = new Material(shader);
            material.color = color;
            if (material.HasProperty("_BaseColor"))
                material.SetColor("_BaseColor", color);
            return material;
        }
    }
}
