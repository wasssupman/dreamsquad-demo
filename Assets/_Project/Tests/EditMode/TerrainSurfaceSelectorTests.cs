using NUnit.Framework;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
using Wassup.Data;

namespace Wassup.Tests.EditMode
{
    public class TerrainSurfaceSelectorTests
    {
        [Test]
        public void SelectTexture_PrefersPathRuleNearWalkCells()
        {
            var map = CreateMap(new int2(6, 5), MapTileType.Env);
            var pathTexture = NewTexture("path-near");
            var groundTexture = NewTexture("ground");
            try
            {
                map.tiles[2 * map.gridSize.x + 2] = MapTileType.Walk;
                var theme = ScriptableObject.CreateInstance<MapThemeData>();
                theme.pathSurfaceInfluence = 1f;
                theme.edgeSurfaceInfluence = 0f;
                theme.envSurfaceRules = new[]
                {
                    new TerrainSurfaceVariant
                    {
                        texture = pathTexture,
                        weight = 1f,
                        nearPathMultiplier = 2f,
                    },
                    new TerrainSurfaceVariant
                    {
                        texture = groundTexture,
                        weight = 1.4f,
                        nearPathMultiplier = 0f,
                    }
                };

                Assert.AreSame(pathTexture, TerrainSurfaceSelector.SelectTexture(map, theme, MapTileType.Env, 2, 1));
                Object.DestroyImmediate(theme);
            }
            finally
            {
                Object.DestroyImmediate(pathTexture);
                Object.DestroyImmediate(groundTexture);
                map.Dispose();
            }
        }

        [Test]
        public void SelectTexture_PrefersEdgeRuleOnOuterCells()
        {
            var map = CreateMap(new int2(8, 8), MapTileType.Env);
            var edgeTexture = NewTexture("edge");
            var innerTexture = NewTexture("inner");
            try
            {
                var theme = ScriptableObject.CreateInstance<MapThemeData>();
                theme.pathSurfaceInfluence = 0f;
                theme.edgeSurfaceInfluence = 1f;
                theme.envSurfaceRules = new[]
                {
                    new TerrainSurfaceVariant
                    {
                        texture = edgeTexture,
                        weight = 1f,
                        edgeMultiplier = 2f,
                    },
                    new TerrainSurfaceVariant
                    {
                        texture = innerTexture,
                        weight = 1.4f,
                        edgeMultiplier = 0f,
                    }
                };

                Assert.AreSame(edgeTexture, TerrainSurfaceSelector.SelectTexture(map, theme, MapTileType.Env, 0, 3));
                Object.DestroyImmediate(theme);
            }
            finally
            {
                Object.DestroyImmediate(edgeTexture);
                Object.DestroyImmediate(innerTexture);
                map.Dispose();
            }
        }

        [Test]
        public void SelectTexture_UsesLegacyVariantsWhenRulesAreEmpty()
        {
            var map = CreateMap(new int2(4, 4), MapTileType.Env);
            var a = NewTexture("a");
            var b = NewTexture("b");
            try
            {
                var theme = ScriptableObject.CreateInstance<MapThemeData>();
                theme.envTileVariants = new[] { a, b };

                var selected = TerrainSurfaceSelector.SelectTexture(map, theme, MapTileType.Env, 1, 1);

                Assert.IsTrue(selected == a || selected == b);
                Object.DestroyImmediate(theme);
            }
            finally
            {
                Object.DestroyImmediate(a);
                Object.DestroyImmediate(b);
                map.Dispose();
            }
        }

        [Test]
        public void SelectTexture_UsesWalkShapeTextureBeforeLegacyFallback()
        {
            var map = CreateMap(new int2(3, 3), MapTileType.Place);
            var straight = NewTexture("straight");
            var fallback = NewTexture("fallback");
            try
            {
                map.tiles[0 * map.gridSize.x + 1] = MapTileType.Walk;
                map.tiles[1 * map.gridSize.x + 1] = MapTileType.Walk;
                map.tiles[2 * map.gridSize.x + 1] = MapTileType.Walk;
                var theme = ScriptableObject.CreateInstance<MapThemeData>();
                theme.walkStraightNSTexture = straight;
                theme.walkTileTexture = fallback;

                Assert.AreSame(straight, TerrainSurfaceSelector.SelectTexture(map, theme, MapTileType.Walk, 1, 1));
                Object.DestroyImmediate(theme);
            }
            finally
            {
                Object.DestroyImmediate(straight);
                Object.DestroyImmediate(fallback);
                map.Dispose();
            }
        }

        private static GeneratedMap CreateMap(int2 gridSize, MapTileType fill)
        {
            int count = gridSize.x * gridSize.y;
            var tiles = new NativeArray<MapTileType>(count, Allocator.Persistent);
            for (int i = 0; i < tiles.Length; i++)
                tiles[i] = fill;

            return new GeneratedMap
            {
                tiles = tiles,
                spawns = new NativeArray<int2>(new[] { new int2(0, 0) }, Allocator.Persistent),
                gridSize = gridSize,
                goal = new int2(gridSize.x - 1, gridSize.y - 1),
                seed = 1234,
            };
        }

        private static Texture2D NewTexture(string name)
        {
            var texture = new Texture2D(1, 1);
            texture.name = name;
            return texture;
        }
    }
}
