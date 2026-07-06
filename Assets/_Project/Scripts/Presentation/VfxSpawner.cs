using UnityEngine;

namespace Wassup.Presentation
{
    // Phase 8 §13 — prefab-only VFX layer. Creates particle effects at world
    // positions in response to ECS events drained by BattleBridge. Non-singleton;
    // BattleBridge holds a SerializeField reference.
    //
    // Design notes:
    //   - Shuriken only (VFX Graph deliberately out of scope for Android compat).
    //   - All prefab slots must be assigned in the Inspector. If a slot is null,
    //     the method logs an error and silently returns — there is no longer a
    //     code-authored fallback (Step 4 이후 단일화, 2026-04-19).
    //   - Prefab authoring policy lives in .claude/skills/unity-vfx-authoring.
    public class VfxSpawner : MonoBehaviour
    {
        [Header("Phase 8 §13 — Prefab slots (all required)")]
        [SerializeField] private GameObject placementRingPrefab;
        [SerializeField] private GameObject meteorBurstPrefab;
        [SerializeField] private GameObject tornadoPrefab;
        [SerializeField] private GameObject portalPrefab;
        [SerializeField] private GameObject healAppliedPrefab;

        // V1 — Placement pulse. One-shot outward radial burst that fades in 0.35s.
        public void SpawnPlacementRing(Vector3 worldPos)
        {
            if (placementRingPrefab == null)
            {
                Debug.LogError("[VfxSpawner] placementRingPrefab 미할당 — Inspector에서 prefab을 연결해주세요.");
                return;
            }
            worldPos = Wassup.Core.BoardSpace.ToView(worldPos); // tilemap-view-backend: sim→view 1회 (진입부)
            var go = Instantiate(placementRingPrefab,
                new Vector3(worldPos.x, worldPos.y + 0.02f, worldPos.z),
                Quaternion.identity, transform);
            Destroy(go, 0.6f);
        }

        // V2b — Meteor explosion burst at impact.
        public void SpawnMeteorBurst(Vector3 worldPos, float radiusWorld)
        {
            if (meteorBurstPrefab == null)
            {
                Debug.LogError("[VfxSpawner] meteorBurstPrefab 미할당 — Inspector에서 prefab을 연결해주세요.");
                return;
            }
            worldPos = Wassup.Core.BoardSpace.ToView(worldPos); // sim→view 1회
            var pos = new Vector3(worldPos.x, worldPos.y + 0.05f, worldPos.z);
            var go = Instantiate(meteorBurstPrefab, pos, Quaternion.identity, transform);
            go.transform.localScale = Vector3.one * Mathf.Max(0.1f, radiusWorld);
            Destroy(go, 1.2f);
        }

        // V3 — Tornado swirl held for durationSec.
        public void SpawnTornado(Vector3 centerWorld, float radiusWorld, float durationSec)
        {
            if (tornadoPrefab == null)
            {
                Debug.LogError("[VfxSpawner] tornadoPrefab 미할당 — Inspector에서 prefab을 연결해주세요.");
                return;
            }
            centerWorld = Wassup.Core.BoardSpace.ToView(centerWorld); // sim→view 1회
            var pos = new Vector3(centerWorld.x, centerWorld.y + 0.05f, centerWorld.z);
            var go = Instantiate(tornadoPrefab, pos, Quaternion.identity, transform);
            if (HasPixPlaysVfx(go))
                PlayPixPlaysVfx(go, pos, pos + Vector3.forward, durationSec, radiusWorld);
            else
                go.transform.localScale = Vector3.one * Mathf.Max(0.1f, radiusWorld);
            Destroy(go, durationSec + 0.1f);
        }

        // V4 — Portal swirl + link beam for durationSec.
        public void SpawnPortal(Vector3 entryWorld, Vector3 exitWorld, float durationSec)
        {
            if (portalPrefab == null)
            {
                Debug.LogError("[VfxSpawner] portalPrefab 미할당 — Inspector에서 prefab을 연결해주세요.");
                return;
            }
            entryWorld = Wassup.Core.BoardSpace.ToView(entryWorld); // sim→view 1회
            exitWorld = Wassup.Core.BoardSpace.ToView(exitWorld);
            var root = Instantiate(portalPrefab, Vector3.zero, Quaternion.identity, transform);
            var entryT = root.transform.Find("Entry");
            var exitT = root.transform.Find("Exit");
            if (entryT != null) entryT.position = new Vector3(entryWorld.x, entryWorld.y + 0.05f, entryWorld.z);
            if (exitT != null) exitT.position = new Vector3(exitWorld.x, exitWorld.y + 0.05f, exitWorld.z);
            var linkBeam = root.transform.Find("LinkBeam");
            var beamLines = linkBeam != null
                ? linkBeam.GetComponentsInChildren<LineRenderer>(true)
                : null;
            if (beamLines != null)
            {
                var start = new Vector3(entryWorld.x, entryWorld.y + 0.15f, entryWorld.z);
                var end = new Vector3(exitWorld.x, exitWorld.y + 0.15f, exitWorld.z);
                for (int i = 0; i < beamLines.Length; i++)
                {
                    var beamLine = beamLines[i];
                    if (beamLine == null) continue;
                    beamLine.positionCount = 2;
                    beamLine.SetPosition(0, start);
                    beamLine.SetPosition(1, end);
                }
            }
            PlayPixPlaysPortalVfx(root, entryWorld, exitWorld, durationSec);
            Destroy(root, durationSec + 0.1f);
        }

        // amount is reserved for future VFX scaling (e.g. large heal → larger burst).
        // Follow-up candidate: map amount → ParticleSystem.main.startSize.
        public void SpawnHealApplied(Vector3 worldPos, float amount = 1f)
        {
            if (healAppliedPrefab == null)
            {
                Debug.LogError("[VfxSpawner] healAppliedPrefab 미할당 — Inspector에서 prefab을 연결해주세요.");
                return;
            }
            worldPos = Wassup.Core.BoardSpace.ToView(worldPos); // sim→view 1회
            var pos = new Vector3(worldPos.x, worldPos.y + 0.08f, worldPos.z);
            var go = Instantiate(healAppliedPrefab, pos, Quaternion.identity, transform);
            Destroy(go, 1.1f);
        }

        private static void PlayPixPlaysVfx(GameObject root, Vector3 source, Vector3 target, float durationSec, float radiusWorld)
        {
            if (root == null) return;
            var effects = FindPixPlaysVfx(root);
            for (int i = 0; i < effects.Length; i++)
            {
                var effect = effects[i];
                if (effect == null) continue;
                PlayPixPlaysEffect(effect, source, target, source, durationSec, Mathf.Max(0.1f, radiusWorld));
            }
        }

        private static bool HasPixPlaysVfx(GameObject root)
        {
            return root != null && FindPixPlaysVfx(root).Length > 0;
        }

        private static void PlayPixPlaysPortalVfx(GameObject root, Vector3 entryWorld, Vector3 exitWorld, float durationSec)
        {
            if (root == null) return;
            var effects = FindPixPlaysVfx(root);
            var source = new Vector3(entryWorld.x, entryWorld.y + 0.15f, entryWorld.z);
            var target = new Vector3(exitWorld.x, exitWorld.y + 0.15f, exitWorld.z);
            var entry = root.transform.Find("Entry");
            var exit = root.transform.Find("Exit");
            for (int i = 0; i < effects.Length; i++)
            {
                var effect = effects[i];
                if (effect == null) continue;
                Vector3 effectSource = source;
                Vector3 effectTarget = target;
                float radius = 1f;

                if (exit != null && effect.transform.IsChildOf(exit))
                {
                    effectSource = target;
                    effectTarget = source;
                }
                else if (entry != null && effect.transform.IsChildOf(entry))
                {
                    effectSource = source;
                    effectTarget = target;
                }

                PlayPixPlaysEffect(effect, effectSource, effectTarget, effectSource, durationSec, radius);
            }
        }

        private static MonoBehaviour[] FindPixPlaysVfx(GameObject root)
        {
            if (root == null) return System.Array.Empty<MonoBehaviour>();
            var behaviours = root.GetComponentsInChildren<MonoBehaviour>(true);
            var result = new System.Collections.Generic.List<MonoBehaviour>();
            for (int i = 0; i < behaviours.Length; i++)
            {
                var behaviour = behaviours[i];
                if (behaviour == null) continue;
                var type = behaviour.GetType();
                while (type != null)
                {
                    if (type.FullName == "PixPlays.ElementalVFX.BaseVfx")
                    {
                        result.Add(behaviour);
                        break;
                    }
                    type = type.BaseType;
                }
            }
            return result.ToArray();
        }

        private static void PlayPixPlaysEffect(MonoBehaviour effect, Vector3 source, Vector3 target, Vector3 ground, float durationSec, float radiusWorld)
        {
            if (effect == null) return;
            var vfxDataType = effect.GetType().Assembly.GetType("PixPlays.ElementalVFX.VfxData");
            if (vfxDataType == null) return;
            var data = System.Activator.CreateInstance(vfxDataType, source, target, durationSec, radiusWorld);
            var setGround = vfxDataType.GetMethod("SetGround", new[] { typeof(Vector3) });
            setGround?.Invoke(data, new object[] { ground });
            var play = effect.GetType().GetMethod("Play", new[] { vfxDataType });
            play?.Invoke(effect, new[] { data });
        }
    }
}
