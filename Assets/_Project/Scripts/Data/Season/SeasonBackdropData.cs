using System;
using UnityEngine;
using Wassup.Data;

namespace Wassup.Data.Season
{
    public enum EdgeAnchor
    {
        NorthLeft, NorthCenter, NorthRight,
        EastTop, EastMiddle, EastBottom,
        SouthRight, SouthCenter, SouthLeft,
        WestBottom, WestMiddle, WestTop,
    }

    [Serializable]
    public struct EdgePropEntry
    {
        public PropData propData;
        public EdgeAnchor anchor;
        public Vector2 worldOffset;
        public float yawDegrees;
        public float scaleMultiplier;
    }

    [CreateAssetMenu(menuName = "Wassup/Season/SeasonBackdropData", fileName = "backdrop")]
    public sealed class SeasonBackdropData : ScriptableObject
    {
        [Header("Far Backdrop")]
        public Texture2D farBackdropTexture;
        public float backdropDistance = 60f;
        public float backdropHeightWorld = 30f;
        public Color backdropTint = Color.white;

        [Header("Skybox")]
        [Range(0f, 8f)] public float skyboxExposure = 1f;
        [Range(0f, 360f)] public float skyboxRotationDegrees = 0f;

        [Header("Edge Props")]
        public EdgePropEntry[] edgeProps = Array.Empty<EdgePropEntry>();

        [Header("Edge Layout")]
        public float edgePadding = 1.5f;
    }
}
