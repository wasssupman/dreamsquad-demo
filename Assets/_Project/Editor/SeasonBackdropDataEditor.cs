using UnityEditor;
using UnityEngine;
using Wassup.Data.Season;

namespace Wassup.Editor
{
    [CustomEditor(typeof(SeasonBackdropData))]
    public class SeasonBackdropDataEditor : UnityEditor.Editor
    {
        private static Material _previousSkybox;
        private static CameraClearFlags _previousClearFlags;
        private static Color _previousBgColor;
        private static Camera _patchedCamera;
        private static SeasonBackdropData _activePreview;
        private static Material _previewMat;

        public override void OnInspectorGUI()
        {
            EditorGUI.BeginChangeCheck();
            DrawDefaultInspector();
            bool changed = EditorGUI.EndChangeCheck();

            var data = (SeasonBackdropData)target;
            bool isActive = _activePreview == data;

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Edit-mode Preview", EditorStyles.boldLabel);

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button(isActive ? "Reapply" : "Apply to Scene"))
                {
                    ApplyPreview(data);
                    isActive = true;
                }
                using (new EditorGUI.DisabledScope(!isActive))
                {
                    if (GUILayout.Button("Clear")) ClearPreview();
                }
            }

            if (isActive)
            {
                EditorGUILayout.HelpBox("Live preview active — value drag auto-reapplies. Click Clear to restore previous skybox.", MessageType.Info);
                if (changed) ApplyPreview(data);
            }
        }

        private static void ApplyPreview(SeasonBackdropData data)
        {
            if (data == null || data.farBackdropTexture == null) return;

            if (_activePreview == null)
            {
                _previousSkybox = RenderSettings.skybox;
                var cam = Camera.main;
                if (cam != null)
                {
                    _patchedCamera = cam;
                    _previousClearFlags = cam.clearFlags;
                    _previousBgColor = cam.backgroundColor;
                }
            }

            if (_previewMat == null)
            {
                var shader = Shader.Find("Skybox/Panoramic");
                if (shader == null)
                {
                    Debug.LogError("[SeasonBackdropDataEditor] Skybox/Panoramic shader not found.");
                    return;
                }
                _previewMat = new Material(shader)
                {
                    hideFlags = HideFlags.DontSave,
                    name = "_BackdropSkyboxPreview"
                };
            }

            _previewMat.SetTexture("_MainTex", data.farBackdropTexture);
            _previewMat.SetColor("_Tint", data.backdropTint);
            _previewMat.SetFloat("_Exposure", data.skyboxExposure);
            _previewMat.SetFloat("_Rotation", data.skyboxRotationDegrees);
            _previewMat.SetFloat("_Mapping", 1f);
            _previewMat.SetFloat("_ImageType", 0f);
            _previewMat.SetFloat("_MirrorOnBack", 0f);
            _previewMat.SetFloat("_Layout", 0f);

            RenderSettings.skybox = _previewMat;
            if (_patchedCamera != null) _patchedCamera.clearFlags = CameraClearFlags.Skybox;

            _activePreview = data;
            DynamicGI.UpdateEnvironment();
            SceneView.RepaintAll();
        }

        private static void ClearPreview()
        {
            RenderSettings.skybox = _previousSkybox;
            if (_patchedCamera != null)
            {
                _patchedCamera.clearFlags = _previousClearFlags;
                _patchedCamera.backgroundColor = _previousBgColor;
            }
            if (_previewMat != null)
            {
                Object.DestroyImmediate(_previewMat);
                _previewMat = null;
            }
            _previousSkybox = null;
            _patchedCamera = null;
            _activePreview = null;
            DynamicGI.UpdateEnvironment();
            SceneView.RepaintAll();
        }
    }
}
