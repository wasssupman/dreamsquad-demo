using UnityEngine;

namespace Wassup.Presentation
{
    // Phase 8 §12 — lightweight VFX layer. Creates procedural Shuriken
    // ParticleSystem GameObjects at world positions in response to ECS events
    // drained by BattleBridge. Non-singleton; BattleBridge holds a SerializeField.
    //
    // Design notes:
    //   - Shuriken only (VFX Graph deliberately out of scope for Android compat).
    //   - All particles are code-authored here so Phase 8 ships zero prefab
    //     assets. Materials are picked up from URP's default Particles/Unlit
    //     shader; if absent, falls back to plain particles material.
    //   - Each Spawn* helper creates a throwaway GameObject whose lifetime is
    //     bounded either by ParticleSystem.main.duration (one-shot) or an
    //     explicit Destroy timer (looping effects like tornado/portal).
    public class VfxSpawner : MonoBehaviour
    {
        [SerializeField] private Color placementRingColor = new(0.3f, 0.8f, 1f, 1f);
        [SerializeField] private Color meteorBurstColor = new(1f, 0.45f, 0.1f, 1f);
        [SerializeField] private Color tornadoColor = new(0.6f, 0.95f, 1f, 1f);
        [SerializeField] private Color portalColor = new(0.8f, 0.4f, 1f, 1f);

        // Optional overrides — assign a pre-made Material here in the inspector
        // to guarantee the underlying shader is bundled in player builds. When
        // left null, Shader.Find fallback chain still works in the editor.
        [SerializeField] private Material particleMaterialOverride;
        [SerializeField] private Material lineMaterialOverride;

        private Material _particleMaterial;
        private Material _lineMaterial;

        private Material ParticleMaterial
        {
            get
            {
                if (particleMaterialOverride != null) return particleMaterialOverride;
                if (_particleMaterial != null) return _particleMaterial;
                var shader = Shader.Find("Universal Render Pipeline/Particles/Unlit")
                          ?? Shader.Find("Particles/Standard Unlit")
                          ?? Shader.Find("Sprites/Default");
                _particleMaterial = new Material(shader) { enableInstancing = false };
                return _particleMaterial;
            }
        }

        // LineRenderer cannot feed the per-particle vertex stream that the
        // Particles shader expects; using it on a line would render pink/missing.
        // Dedicated non-particle unlit shader keeps the beam visible on URP.
        private Material LineMaterial
        {
            get
            {
                if (lineMaterialOverride != null) return lineMaterialOverride;
                if (_lineMaterial != null) return _lineMaterial;
                var shader = Shader.Find("Universal Render Pipeline/Unlit")
                          ?? Shader.Find("Unlit/Color")
                          ?? Shader.Find("Sprites/Default");
                _lineMaterial = new Material(shader) { enableInstancing = false };
                return _lineMaterial;
            }
        }

        private void OnDestroy()
        {
            if (_particleMaterial != null) Destroy(_particleMaterial);
            if (_lineMaterial != null) Destroy(_lineMaterial);
        }

        // V1 — Placement pulse. One-shot outward radial burst that fades in 0.8s.
        public void SpawnPlacementRing(Vector3 worldPos)
        {
            var go = new GameObject("VFX_PlacementRing");
            go.transform.position = new Vector3(worldPos.x, worldPos.y + 0.02f, worldPos.z);

            var ps = go.AddComponent<ParticleSystem>();
            var main = ps.main;
            main.duration = 0.4f;
            main.loop = false;
            main.startLifetime = 0.8f;
            main.startSpeed = 0f;
            main.startSize = 0.15f;
            main.startColor = placementRingColor;
            main.gravityModifier = 0f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;

            var emission = ps.emission;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 40) });

            var shape = ps.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = 0.1f;
            shape.arc = 360f;
            shape.radiusThickness = 0f; // surface only
            shape.rotation = new Vector3(90f, 0f, 0f); // lay flat on ground

            var vol = ps.velocityOverLifetime;
            vol.enabled = true;
            vol.space = ParticleSystemSimulationSpace.Local;
            vol.radial = new ParticleSystem.MinMaxCurve(2.5f);

            var col = ps.colorOverLifetime;
            col.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) });
            col.color = new ParticleSystem.MinMaxGradient(gradient);

            var sol = ps.sizeOverLifetime;
            sol.enabled = true;
            sol.size = new ParticleSystem.MinMaxCurve(1f,
                new AnimationCurve(new Keyframe(0f, 0.3f), new Keyframe(1f, 1f)));

            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = ParticleMaterial;
            renderer.renderMode = ParticleSystemRenderMode.Billboard;

            // Runtime-added ParticleSystems honour playOnAwake before we finish
            // configuring modules, so the initial emission uses default settings
            // and subsequent SetBursts / rate changes don't always retrigger.
            // Explicit Play() after full configuration guarantees the intended
            // burst/loop fires once.
            ps.Play();

            Destroy(go, main.duration + main.startLifetime.constant + 0.1f);
        }

        // V2 — Meteor explosion burst. Big omnidirectional puff + upward smoke.
        public void SpawnMeteorBurst(Vector3 worldPos, float radiusWorld)
        {
            var go = new GameObject("VFX_MeteorBurst");
            go.transform.position = new Vector3(worldPos.x, worldPos.y + 0.05f, worldPos.z);

            var ps = go.AddComponent<ParticleSystem>();
            var main = ps.main;
            main.duration = 0.3f;
            main.loop = false;
            main.startLifetime = 0.9f;
            main.startSpeed = new ParticleSystem.MinMaxCurve(3f, 6f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.15f, 0.35f);
            main.startColor = meteorBurstColor;
            main.gravityModifier = 0.4f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;

            var emission = ps.emission;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 80) });

            var shape = ps.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Hemisphere;
            shape.radius = Mathf.Max(0.1f, radiusWorld * 0.25f);

            var col = ps.colorOverLifetime;
            col.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(
                new[] {
                    new GradientColorKey(new Color(1f, 0.95f, 0.5f), 0f),
                    new GradientColorKey(meteorBurstColor, 0.4f),
                    new GradientColorKey(new Color(0.3f, 0.15f, 0.08f), 1f),
                },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0.8f, 0.3f), new GradientAlphaKey(0f, 1f) });
            col.color = new ParticleSystem.MinMaxGradient(gradient);

            var sol = ps.sizeOverLifetime;
            sol.enabled = true;
            sol.size = new ParticleSystem.MinMaxCurve(1f,
                new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(1f, 1.8f)));

            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = ParticleMaterial;
            renderer.renderMode = ParticleSystemRenderMode.Billboard;

            // Runtime-added ParticleSystems honour playOnAwake before we finish
            // configuring modules, so the initial emission uses default settings
            // and subsequent SetBursts / rate changes don't always retrigger.
            // Explicit Play() after full configuration guarantees the intended
            // burst/loop fires once.
            ps.Play();

            Destroy(go, main.duration + main.startLifetime.constantMax + 0.1f);
        }

        // V3 — Tornado swirl. Continuous ring of particles orbiting the center
        // for `durationSec`, then gracefully fades as emission stops and each
        // surviving particle runs out its lifetime.
        public void SpawnTornado(Vector3 centerWorld, float radiusWorld, float durationSec)
        {
            var go = new GameObject("VFX_Tornado");
            go.transform.position = new Vector3(centerWorld.x, centerWorld.y + 0.05f, centerWorld.z);

            var ps = go.AddComponent<ParticleSystem>();
            var main = ps.main;
            main.duration = Mathf.Max(0.1f, durationSec);
            main.loop = false;
            main.startLifetime = 0.8f;
            main.startSpeed = 0f;
            main.startSize = new ParticleSystem.MinMaxCurve(0.12f, 0.22f);
            main.startColor = tornadoColor;
            main.gravityModifier = 0f;
            main.simulationSpace = ParticleSystemSimulationSpace.Local;

            var emission = ps.emission;
            emission.rateOverTime = 50f;

            var shape = ps.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Donut;
            shape.radius = Mathf.Max(0.2f, radiusWorld * 0.9f);
            shape.donutRadius = 0.1f;
            shape.arc = 360f;
            shape.rotation = new Vector3(90f, 0f, 0f);

            var vol = ps.velocityOverLifetime;
            vol.enabled = true;
            vol.space = ParticleSystemSimulationSpace.Local;
            vol.orbitalY = new ParticleSystem.MinMaxCurve(6f);
            vol.y = new ParticleSystem.MinMaxCurve(1.2f);

            var col = ps.colorOverLifetime;
            col.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(tornadoColor, 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(0.9f, 0.3f), new GradientAlphaKey(0f, 1f) });
            col.color = new ParticleSystem.MinMaxGradient(gradient);

            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = ParticleMaterial;
            renderer.renderMode = ParticleSystemRenderMode.Billboard;

            // Runtime-added ParticleSystems honour playOnAwake before we finish
            // configuring modules, so the initial emission uses default settings
            // and subsequent SetBursts / rate changes don't always retrigger.
            // Explicit Play() after full configuration guarantees the intended
            // burst/loop fires once.
            ps.Play();

            Destroy(go, main.duration + main.startLifetime.constantMax + 0.2f);
        }

        // V4 — Portal swirl + link beam. Two swirls (entry/exit) + a LineRenderer
        // pulsing between them for the duration. Looped particle emission stops
        // after durationSec; GameObject destroys a hair later so lingering
        // particles finish their lifetime before the whole thing disappears.
        public void SpawnPortal(Vector3 entryWorld, Vector3 exitWorld, float durationSec)
        {
            var root = new GameObject("VFX_Portal");
            root.transform.position = Vector3.zero;

            CreatePortalSwirl(root.transform, entryWorld, durationSec, "Entry");
            CreatePortalSwirl(root.transform, exitWorld, durationSec, "Exit");

            // Link beam: a single LineRenderer between the two points.
            var beamGo = new GameObject("Beam");
            beamGo.transform.SetParent(root.transform, worldPositionStays: false);
            var line = beamGo.AddComponent<LineRenderer>();
            line.positionCount = 2;
            line.SetPosition(0, new Vector3(entryWorld.x, entryWorld.y + 0.15f, entryWorld.z));
            line.SetPosition(1, new Vector3(exitWorld.x, exitWorld.y + 0.15f, exitWorld.z));
            line.startWidth = 0.12f;
            line.endWidth = 0.12f;
            line.sharedMaterial = LineMaterial;
            line.startColor = portalColor;
            line.endColor = portalColor;
            line.useWorldSpace = true;

            Destroy(root, durationSec + 1f);
        }

        private void CreatePortalSwirl(Transform parent, Vector3 worldPos, float durationSec, string suffix)
        {
            var go = new GameObject("Swirl_" + suffix);
            go.transform.SetParent(parent, worldPositionStays: false);
            go.transform.position = new Vector3(worldPos.x, worldPos.y + 0.05f, worldPos.z);

            var ps = go.AddComponent<ParticleSystem>();
            var main = ps.main;
            main.duration = Mathf.Max(0.1f, durationSec);
            main.loop = false;
            main.startLifetime = 0.7f;
            main.startSpeed = 0f;
            main.startSize = new ParticleSystem.MinMaxCurve(0.1f, 0.2f);
            main.startColor = portalColor;
            main.simulationSpace = ParticleSystemSimulationSpace.Local;

            var emission = ps.emission;
            emission.rateOverTime = 35f;

            var shape = ps.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Donut;
            shape.radius = 0.4f;
            shape.donutRadius = 0.08f;
            shape.rotation = new Vector3(90f, 0f, 0f);

            var vol = ps.velocityOverLifetime;
            vol.enabled = true;
            vol.space = ParticleSystemSimulationSpace.Local;
            vol.orbitalY = new ParticleSystem.MinMaxCurve(5f);

            var col = ps.colorOverLifetime;
            col.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(
                new[] { new GradientColorKey(portalColor, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.3f), new GradientAlphaKey(0f, 1f) });
            col.color = new ParticleSystem.MinMaxGradient(gradient);

            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = ParticleMaterial;
            renderer.renderMode = ParticleSystemRenderMode.Billboard;

            // Runtime-added ParticleSystems honour playOnAwake before we finish
            // configuring modules, so the initial emission uses default settings
            // and subsequent SetBursts / rate changes don't always retrigger.
            // Explicit Play() after full configuration guarantees the intended
            // burst/loop fires once.
            ps.Play();
        }
    }
}
