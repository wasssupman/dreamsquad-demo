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
        public Vector3 visualOffset;
        [Min(0.01f)] public float visualScale = 1f;

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
