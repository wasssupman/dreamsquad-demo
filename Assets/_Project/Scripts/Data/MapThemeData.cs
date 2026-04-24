using System;
using UnityEngine;

namespace Wassup.Data
{
    [CreateAssetMenu(fileName = "MapThemeData", menuName = "Wassup/MapThemeData")]
    public class MapThemeData : ScriptableObject
    {
        [Header("Deprecated Tile Surface")]
        [Tooltip("Texture used for Place/buildable tile top surfaces.")]
        public Texture2D placeTileTexture;

        [Tooltip("Texture used for Walk/path tile top surfaces.")]
        public Texture2D walkTileTexture;

        [Tooltip("Texture used for Env background tile top surfaces.")]
        public Texture2D envTileTexture;

        [Tooltip("Texture used for Deco background tile top surfaces.")]
        public Texture2D decoTileTexture;

        [Tooltip("Optional variants for Place/buildable tile top surfaces. When set, these override the single texture field.")]
        public Texture2D[] placeTileVariants;

        [Tooltip("Optional variants for Walk/path tile top surfaces. When set, these override the single texture field.")]
        public Texture2D[] walkTileVariants;

        [Tooltip("Optional variants for Env background tile top surfaces. When set, these override the single texture field.")]
        public Texture2D[] envTileVariants;

        [Tooltip("Optional variants for Deco background tile top surfaces. When set, these override the single texture field.")]
        public Texture2D[] decoTileVariants;

        [Header("Surface Variation Settings")]
        [Min(0.01f)]
        [Tooltip("Low frequency terrain variation scale. Larger values change tile variants more often.")]
        public float tileVariantNoiseScale = 0.18f;

        [Range(0f, 1f)]
        [Tooltip("Per-cell hash jitter mixed into coherent noise so variant boundaries do not look too banded.")]
        public float tileVariantJitter = 0.25f;

        [Tooltip("Theme-specific offset for deterministic tile variant selection.")]
        public int tileVariantSeedOffset;

        [Header("Env Base")]
        [Tooltip("Weighted terrain rules for Place/buildable tile top surfaces. When set, these override placeTileVariants.")]
        public TerrainSurfaceVariant[] placeSurfaceRules;

        [Tooltip("Weighted terrain rules for Walk/path tile top surfaces. When set, these override walkTileVariants.")]
        public TerrainSurfaceVariant[] walkSurfaceRules;

        [Tooltip("Weighted terrain rules for Env background tile top surfaces. When set, these override envTileVariants.")]
        public TerrainSurfaceVariant[] envSurfaceRules;

        [Header("Deprecated Deco Surface Rules")]
        [Tooltip("Weighted terrain rules for Deco background tile top surfaces. When set, these override decoTileVariants.")]
        public TerrainSurfaceVariant[] decoSurfaceRules;

        [Range(0f, 1f)]
        [Tooltip("How much nearby path cells influence terrain surface choice.")]
        public float pathSurfaceInfluence = 0.35f;

        [Range(0f, 1f)]
        [Tooltip("How much map border distance influences terrain surface choice.")]
        public float edgeSurfaceInfluence = 0.25f;

        [Header("Walk Shape Set")]
        [Tooltip("Optional path texture for isolated walk cells.")]
        public Texture2D walkSingleTexture;

        [Tooltip("Optional path texture for north/south straight walk cells.")]
        public Texture2D walkStraightNSTexture;

        [Tooltip("Optional path texture for east/west straight walk cells.")]
        public Texture2D walkStraightEWTexture;

        [Tooltip("Optional path texture for walk corner cells.")]
        public Texture2D walkCornerTexture;

        [Tooltip("Optional path texture for walk end cells.")]
        public Texture2D walkEndTexture;

        [Tooltip("Optional path texture for walk T-junction cells.")]
        public Texture2D walkTJunctionTexture;

        [Tooltip("Optional path texture for four-way cross walk cells.")]
        public Texture2D walkCrossTexture;

        [Header("Env Detail")]
        [Tooltip("Small terrain detail textures scattered on continuous background cells.")]
        public Texture2D[] terrainDetailTextures;

        [Range(0f, 1f)]
        [Tooltip("Chance to place a terrain detail overlay on each Env/Deco cell.")]
        public float terrainDetailDensity = 0.18f;

        [Range(0.1f, 1.5f)]
        [Tooltip("Base visual scale for terrain detail overlays relative to tile size.")]
        public float terrainDetailScale = 0.45f;

        [Header("Place Transition")]
        [Tooltip("Optional soft overlay used on Place tile edges touching background terrain.")]
        public Texture2D placeBackgroundEdgeTexture;

        [Tooltip("Soft strip overlay used on Place tile edges touching background terrain.")]
        public Texture2D placeEdgeTexture;

        [Tooltip("Overlay used on Place outer corners.")]
        public Texture2D placeOuterCornerTexture;

        [Tooltip("Overlay used for missing diagonal inner corners. Null skips inner overlays.")]
        public Texture2D placeInnerCornerTexture;

        [Range(0.2f, 0.5f)]
        public float placeInnerCornerScale = 0.36f;

        [Range(0f, 1f)]
        public float placeInnerCornerOpacity = 0.6f;

        [Range(0.2f, 0.8f)]
        public float placeOuterCornerScale = 0.48f;

        [Range(0f, 1f)]
        public float placeOuterCornerOpacity = 0.42f;

        [Range(0f, 1f)]
        public float placeEdgeOpacity = 0.38f;

        [Range(0.04f, 0.18f)]
        public float placeEdgeThickness = 0.1f;

        [Range(0f, 1f)]
        public float placeEdgeFalloff = 0.5f;

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

        [Header("Background Prop Rules")]
        [Min(1)]
        [Tooltip("Minimum cell distance between generated Poisson seed candidates.")]
        public int propPoissonMinDistance = 2;

        [Min(1)]
        [Tooltip("Region area divisor used to cap Poisson seed candidate count.")]
        public int propPoissonDensityPerRegionArea = 4;

        [Min(16)]
        [Tooltip("Maximum shuffled cells tested per region while building Poisson candidates.")]
        public int propPoissonMaxAttemptsPerRegion = 256;

        [Min(0)]
        [Tooltip("Placed prop indices inside this recent window are heavily down-weighted when alternatives exist.")]
        public int propRepeatAvoidanceWindow = 2;

        [Min(0)]
        [Tooltip("Candidates this close to spawn or goal cells receive reduced density. 0 disables the rule.")]
        public int spawnGoalPropAvoidRadius = 2;

        [Range(0f, 1f)]
        [Tooltip("Density multiplier for candidates near spawn or goal cells.")]
        public float spawnGoalPropDensityMultiplier = 0.25f;

        [Min(1)]
        [Tooltip("Footprint area allowed next to walk/path cells before large prop penalties apply.")]
        public int pathAdjacentSmallPropMaxArea = 1;

        [Range(0f, 1f)]
        [Tooltip("Weight multiplier for large props next to walk/path cells.")]
        public float pathAdjacentLargePropWeightMultiplier = 0.15f;

        [Range(0f, 1f)]
        [Tooltip("Weight multiplier for large props away from map edges. Lower values push large props toward outer background regions.")]
        public float largePropInnerWeightMultiplier = 0.35f;

        [Header("Obstacle Prefabs (single-cell)")]
        [Tooltip("Place -> Deco converted tiles instantiate one random prefab from this list.")]
        public GameObject[] obstaclePrefabs;

        [Header("Density")]
        [Range(0.2f, 0.6f)]
        [Tooltip("Minimum ratio of original Place tiles preserved after obstacle conversion.")]
        public float minPlaceableRatio = 0.4f;
    }

    [Serializable]
    public sealed class TerrainSurfaceVariant
    {
        [Tooltip("Top texture used when this surface rule wins.")]
        public Texture2D texture;

        [Min(0f)]
        [Tooltip("Base selection weight before terrain feature rules are applied.")]
        public float weight = 1f;

        [Tooltip("Preferred low-frequency terrain noise range. x=min, y=max.")]
        public Vector2 noiseRange = new Vector2(0f, 1f);

        [Tooltip("Preferred secondary terrain moisture/detail range. x=min, y=max.")]
        public Vector2 moistureRange = new Vector2(0f, 1f);

        [Range(0f, 2f)]
        [Tooltip("Multiplier near walk/path tiles. Values above 1 prefer path-adjacent cells.")]
        public float nearPathMultiplier = 1f;

        [Range(0f, 2f)]
        [Tooltip("Multiplier near map edges. Values above 1 prefer outer cells.")]
        public float edgeMultiplier = 1f;
    }
}
