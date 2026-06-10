using Unity.Mathematics;
using UnityEngine;
using Wassup.Data;
using Wassup.Data.Season;

namespace Wassup.Presentation.Backdrop
{
    public static class BackdropMounter
    {
        private static Material _previousSkybox;
        private static CameraClearFlags _previousClearFlags;
        private static Color _previousBgColor;
        private static Camera _patchedCamera;
        private static bool _skyboxOverridden;
        private static Material _activeSkyboxMat;

        // origin = board 월드 원점(= MapView.transform.position). 엣지 프롭이 옮겨진
        // 보드 둘레에 정렬되도록 boardCenter 에 가산. map-origin-placement.
        public static GameObject Mount(GeneratedMap map, Camera camera,
                                       SeasonBackdropData data, float tileSize, Vector3 origin = default)
        {
            var root = new GameObject("_Backdrop");
            root.transform.position = Vector3.zero;

            // --- Skybox-based far backdrop ---
            // Latitude-longitude panorama wraps the whole camera view. Texture aspect 2:1 expected.
            if (data.farBackdropTexture != null)
            {
                if (!_skyboxOverridden)
                {
                    _previousSkybox = RenderSettings.skybox;
                    _patchedCamera = camera;
                    if (camera != null)
                    {
                        _previousClearFlags = camera.clearFlags;
                        _previousBgColor = camera.backgroundColor;
                    }
                    _skyboxOverridden = true;
                }

                var skyShader = Shader.Find("Skybox/Panoramic");
                if (skyShader != null)
                {
                    var sky = new Material(skyShader) { name = "_BackdropSkybox" };
                    sky.SetTexture("_MainTex", data.farBackdropTexture);
                    sky.SetColor("_Tint", data.backdropTint);
                    sky.SetFloat("_Exposure", data.skyboxExposure);
                    sky.SetFloat("_Rotation", data.skyboxRotationDegrees);
                    sky.SetFloat("_Mapping", 1f);      // 1 = Latitude Longitude Layout
                    sky.SetFloat("_ImageType", 0f);    // 0 = 360 Degrees
                    sky.SetFloat("_MirrorOnBack", 0f);
                    sky.SetFloat("_Layout", 0f);
                    RenderSettings.skybox = sky;
                    _activeSkyboxMat = sky;

                    if (camera != null)
                    {
                        camera.clearFlags = CameraClearFlags.Skybox;
                    }
                }
            }

            // --- Edge Props ---
            var edgePropsParent = new GameObject("_EdgeProps");
            edgePropsParent.transform.SetParent(root.transform, false);

            int2 gs = map.gridSize;
            var boardHalfWorld = new Vector2(gs.x * tileSize * 0.5f, gs.y * tileSize * 0.5f);
            var boardCenter    = origin + new Vector3(gs.x * tileSize * 0.5f, 0f, gs.y * tileSize * 0.5f);

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
            if (_skyboxOverridden)
            {
                RenderSettings.skybox = _previousSkybox;
                if (_patchedCamera != null)
                {
                    _patchedCamera.clearFlags = _previousClearFlags;
                    _patchedCamera.backgroundColor = _previousBgColor;
                }
                if (_activeSkyboxMat != null) Object.Destroy(_activeSkyboxMat);
                _activeSkyboxMat = null;
                _previousSkybox = null;
                _patchedCamera = null;
                _skyboxOverridden = false;
            }
            if (root != null)
            {
                Object.Destroy(root);
                root = null;
            }
        }
    }
}
