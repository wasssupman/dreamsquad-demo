using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Wassup.Data;

namespace Wassup.Editor.Portraits
{
    [Serializable]
    public sealed class DefenderPortraitFramingOverride
    {
        [SerializeField] private string defenderId;
        [SerializeField] private Vector2 offset;
        [SerializeField, Min(0.1f)] private float orthographicSizeMultiplier = 1f;

        public string DefenderId => defenderId;
        public Vector2 Offset => offset;
        public float OrthographicSizeMultiplier => Mathf.Max(0.1f, orthographicSizeMultiplier);
    }

    [CreateAssetMenu(
        fileName = "DefenderPortraitBakeProfile",
        menuName = "Wassup/Editor/Defender Portrait Bake Profile",
        order = 200)]
    public sealed class DefenderPortraitBakeProfile : ScriptableObject
    {
        [Header("Sources")]
        [SerializeField] private DefenderCatalog catalog;
        [SerializeField] private DefaultAsset outputFolder;

        [Header("Output")]
        [SerializeField, Min(64)] private int resolution = 512;
        [SerializeField, Range(1, 4)] private int supersample = 2;

        [Header("Pose")]
        [SerializeField] private string poseAnimation = "Idle";
        [SerializeField, Min(0f)] private float poseTimeSeconds;

        [Header("Framing")]
        [Tooltip("Head/Pelvis midpoint-relative camera offset in Spine rig units.")]
        [SerializeField] private Vector2 anchorOffset = new Vector2(-0.15f, 0.32f);
        [SerializeField, Min(0.1f)] private float orthographicSize = 0.9f;
        [SerializeField] private List<DefenderPortraitFramingOverride> framingOverrides = new();

        public DefenderCatalog Catalog => catalog;
        public DefaultAsset OutputFolder => outputFolder;
        public int Resolution => Mathf.Max(64, resolution);
        public int Supersample => Mathf.Clamp(supersample, 1, 4);
        public string PoseAnimation => poseAnimation;
        public float PoseTimeSeconds => Mathf.Max(0f, poseTimeSeconds);
        public Vector2 AnchorOffset => anchorOffset;
        public float OrthographicSize => Mathf.Max(0.1f, orthographicSize);

        public void ResolveFraming(
            string defenderId,
            out Vector2 offset,
            out float orthographicSizeMultiplier)
        {
            offset = Vector2.zero;
            orthographicSizeMultiplier = 1f;
            if (framingOverrides == null) return;

            for (int i = 0; i < framingOverrides.Count; i++)
            {
                DefenderPortraitFramingOverride entry = framingOverrides[i];
                if (entry == null || !string.Equals(entry.DefenderId, defenderId, StringComparison.Ordinal))
                    continue;

                offset = entry.Offset;
                orthographicSizeMultiplier = entry.OrthographicSizeMultiplier;
                return;
            }
        }

        private void OnValidate()
        {
            resolution = Mathf.Max(64, resolution);
            supersample = Mathf.Clamp(supersample, 1, 4);
            poseTimeSeconds = Mathf.Max(0f, poseTimeSeconds);
            orthographicSize = Mathf.Max(0.1f, orthographicSize);
        }
    }
}
