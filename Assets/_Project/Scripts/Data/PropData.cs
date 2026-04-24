using Spine.Unity;
using UnityEngine;

namespace Wassup.Data
{
    [CreateAssetMenu(fileName = "PropData", menuName = "Wassup/PropData", order = 20)]
    public class PropData : ScriptableObject
    {
        [Header("Identity")]
        public string id;
        public string displayName;

        [Header("Placement")]
        [Min(1)] public int footprintX = 1;
        [Min(1)] public int footprintY = 1;
        [Min(0)] public int placementWeight = 10;
        [Min(0)] public int minDistanceCells;
        public Vector3 visualOffset;
        [Min(0.01f)] public float visualScale = 1f;
        public BoardDecorAnchorType[] preferredAnchorTypes;
        [Min(0)] public int preferredRegionSizeMin;
        [Min(0)] public int clusterRadius = 1;
        [Min(1)] public int clusterCount = 1;
        [Range(0f, 1f)] public float clusterProbability;
        [Min(0f)] public float rotationJitterDegrees;
        [Range(0f, 0.8f)] public float scaleJitter;
        public Vector2Int pathProximityRange = new Vector2Int(0, 255);
        public Vector2Int borderProximityRange = new Vector2Int(0, 255);

        [Header("Generated Prefab")]
        public GameObject prefab;

        [Header("Sprite Billboard")]
        public Sprite sprite;
        public Texture2D sourceTexture;
        public Color spriteColor = Color.white;
        public int sortingOrder;

        [Header("Spine Billboard")]
        public SkeletonDataAsset skeletonDataAsset;
        public string spineSkinName;
        public string idleAnimation = "idle";

        [Header("Billboard")]
        public PropBillboardMode billboardMode = PropBillboardMode.FullCamera;

        public Vector2Int Footprint => new Vector2Int(Mathf.Max(1, footprintX), Mathf.Max(1, footprintY));
        public bool HasSpriteVisual => sprite != null || sourceTexture != null;
        public bool HasSpineVisual => skeletonDataAsset != null;
    }

    public enum PropBillboardMode
    {
        FullCamera,
        YAxis,
        None,
    }
}
