using UnityEngine;

namespace Wassup.Data
{
    [CreateAssetMenu(fileName = "MapThemeData", menuName = "Wassup/MapThemeData")]
    public class MapThemeData : ScriptableObject
    {
        [Header("Tile Surface")]
        [Tooltip("Texture used for Place/buildable tile top surfaces.")]
        public Texture2D placeTileTexture;

        [Tooltip("Texture used for Walk/path tile top surfaces.")]
        public Texture2D walkTileTexture;

        [Tooltip("Texture used for Env background tile top surfaces.")]
        public Texture2D envTileTexture;

        [Tooltip("Texture used for Deco background tile top surfaces.")]
        public Texture2D decoTileTexture;

        [Min(0.01f)]
        [Tooltip("Visual tile block thickness below the y=0 gameplay plane.")]
        public float tileThickness = 0.16f;

        [Range(0.75f, 1f)]
        [Tooltip("Top surface size relative to tileSize. Lower values reveal more dark seams.")]
        public float tileTopScale = 0.9f;

        [Range(0.8f, 1.05f)]
        [Tooltip("Base cube size relative to tileSize. Slightly larger than the top creates visible side mass.")]
        public float tileBaseScale = 0.98f;

        [Tooltip("Shared dark side color for the low cube body under each tile.")]
        public Color tileSideColor = new Color(0.2f, 0.18f, 0.22f, 1f);

        [Header("Background Props")]
        [Tooltip("Generated map background tiles (Deco/Env) can receive these footprint-based props.")]
        public PropData[] tileProps;

        [Tooltip("Props reserved for designer-authored or future outer-map decoration placement.")]
        public PropData[] decorProps;

        [Range(0f, 1f)]
        [Tooltip("Chance to attempt a tile prop placement at each eligible background cell.")]
        public float tilePropDensity = 0.25f;

        [Min(0)]
        [Tooltip("Maximum tile props to place. 0 means unlimited.")]
        public int maxTilePropCount;

        [Header("Obstacle Prefabs (single-cell)")]
        [Tooltip("Place -> Deco converted tiles instantiate one random prefab from this list.")]
        public GameObject[] obstaclePrefabs;

        [Header("Density")]
        [Range(0.2f, 0.6f)]
        [Tooltip("Minimum ratio of original Place tiles preserved after obstacle conversion.")]
        public float minPlaceableRatio = 0.4f;
    }
}
