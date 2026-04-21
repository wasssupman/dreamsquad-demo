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
        [SerializeField] private GameObject meteorFallPrefab;
        [SerializeField] private GameObject tornadoPrefab;
        [SerializeField] private GameObject portalPrefab;

        // V1 — Placement pulse. One-shot outward radial burst that fades in 0.35s.
        public void SpawnPlacementRing(Vector3 worldPos)
        {
            if (placementRingPrefab == null)
            {
                Debug.LogError("[VfxSpawner] placementRingPrefab 미할당 — Inspector에서 prefab을 연결해주세요.");
                return;
            }
            var go = Instantiate(placementRingPrefab,
                new Vector3(worldPos.x, worldPos.y + 0.02f, worldPos.z),
                Quaternion.identity, transform);
            Destroy(go, 0.6f);
        }

        // V2a — Meteor falling streak during the warning window. Spawned alongside
        // the procedural warning ring (BattleBridge.SpawnMeteorWarningVisual).
        public void SpawnMeteorFall(Vector3 targetWorld, float warningSec)
        {
            if (meteorFallPrefab == null)
            {
                Debug.LogError("[VfxSpawner] meteorFallPrefab 미할당 — Inspector에서 prefab을 연결해주세요.");
                return;
            }
            var go = Instantiate(meteorFallPrefab, targetWorld, Quaternion.identity, transform);
            var fall = go.GetComponent<MeteorFall>();
            if (fall != null) fall.Launch(targetWorld, warningSec);
            else Destroy(go, warningSec + 0.1f);
        }

        // V2b — Meteor explosion burst at impact.
        public void SpawnMeteorBurst(Vector3 worldPos, float radiusWorld)
        {
            if (meteorBurstPrefab == null)
            {
                Debug.LogError("[VfxSpawner] meteorBurstPrefab 미할당 — Inspector에서 prefab을 연결해주세요.");
                return;
            }
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
            var pos = new Vector3(centerWorld.x, centerWorld.y + 0.05f, centerWorld.z);
            var go = Instantiate(tornadoPrefab, pos, Quaternion.identity, transform);
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
            Destroy(root, durationSec + 0.1f);
        }
    }
}
