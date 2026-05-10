using Unity.Mathematics;
using UnityEngine;
using Wassup.Data;
using Wassup.Data.Season;

namespace Wassup.Presentation.Backdrop
{
    public static class BackdropMounter
    {
        public static GameObject Mount(GeneratedMap map, Camera camera,
                                       SeasonBackdropData data, float tileSize)
        {
            var root = new GameObject("_Backdrop");
            root.transform.position = Vector3.zero;

            // --- Far Backdrop Quad ---
            var quadGo = GameObject.CreatePrimitive(PrimitiveType.Quad);
            quadGo.name = "_BackdropQuad";
            quadGo.transform.SetParent(root.transform, false);

            // Remove collider — purely visual
            var collider = quadGo.GetComponent<Collider>();
            if (collider != null) Object.Destroy(collider);

            float heightWorld = data.backdropHeightWorld;
            float widthWorld  = heightWorld * camera.aspect;

            var camT = camera.transform;
            Vector3 quadPos = camT.position + camT.forward * data.backdropDistance;
            quadGo.transform.position = quadPos;
            quadGo.transform.LookAt(camT.position, Vector3.up);
            quadGo.transform.Rotate(0f, 180f, 0f, Space.Self);
            quadGo.transform.localScale = new Vector3(widthWorld, heightWorld, 1f);

            var meshRenderer = quadGo.GetComponent<MeshRenderer>();
            if (meshRenderer != null)
            {
                var shader = Shader.Find("Wassup/Backdrop_Unlit");
                if (shader == null) shader = Shader.Find("Unlit/Texture");
                var mat = new Material(shader);
                if (data.farBackdropTexture != null)
                    mat.SetTexture("_MainTex", data.farBackdropTexture);
                mat.SetColor("_TintColor", data.backdropTint);
                meshRenderer.sharedMaterial = mat;
            }

            // --- Edge Props ---
            var edgePropsParent = new GameObject("_EdgeProps");
            edgePropsParent.transform.SetParent(root.transform, false);

            int2 gs = map.gridSize;
            var boardHalfWorld = new Vector2(gs.x * tileSize * 0.5f, gs.y * tileSize * 0.5f);
            var boardCenter    = new Vector3(gs.x * tileSize * 0.5f, 0f, gs.y * tileSize * 0.5f);

            for (int i = 0; i < data.edgeProps.Length; i++)
            {
                ref var e = ref data.edgeProps[i];
                if (e.propData == null || e.propData.prefab == null) continue;

                Vector3 basePos  = BackdropAnchorTable.Resolve(e.anchor, boardCenter, boardHalfWorld,
                                                               data.edgePadding, tileSize);
                Vector3 worldPos = basePos + new Vector3(e.worldOffset.x, 0f, e.worldOffset.y);

                Vector3 lookDir = boardCenter - worldPos;
                lookDir.y = 0f;
                Quaternion rot = lookDir.sqrMagnitude > 1e-4f
                    ? Quaternion.LookRotation(lookDir, Vector3.up)
                    : Quaternion.identity;
                rot *= Quaternion.Euler(0f, e.yawDegrees, 0f);

                var go = Object.Instantiate(e.propData.prefab, worldPos, rot, edgePropsParent.transform);
                if (e.scaleMultiplier != 0f && !Mathf.Approximately(e.scaleMultiplier, 1f))
                    go.transform.localScale *= e.scaleMultiplier;

                // Disable PropBillboard — EdgeProps are static scenery (double safety net)
                if (go.TryGetComponent<PropBillboard>(out var pb))
                    pb.enabled = false;
            }

            return root;
        }

        public static void Unmount(ref GameObject root)
        {
            if (root != null)
            {
                Object.Destroy(root);
                root = null;
            }
        }
    }
}
