using NUnit.Framework;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
using Wassup.Data;

namespace Wassup.Tests.EditMode
{
    public class TerrainTileRuleResolverTests
    {
        [Test]
        public void Resolve_DoesNotDrawBaseForContinuousTerrainTiles()
        {
            var map = CreateMap(new int2(2, 2), MapTileType.Env);
            try
            {
                var info = TerrainTileRuleResolver.Resolve(map, null, 0, 0, 0.86f);

                Assert.IsFalse(info.drawBase);
            }
            finally
            {
                map.Dispose();
            }
        }

        [Test]
        public void Resolve_ReturnsWalkTextureAndYawForCorner()
        {
            var map = CreateMap(new int2(3, 3), MapTileType.Place);
            var corner = NewTexture("corner");
            try
            {
                map.tiles[1 * map.gridSize.x + 1] = MapTileType.Walk;
                map.tiles[2 * map.gridSize.x + 1] = MapTileType.Walk;
                map.tiles[1 * map.gridSize.x + 2] = MapTileType.Walk;
                var theme = ScriptableObject.CreateInstance<MapThemeData>();
                theme.walkCornerTexture = corner;

                var info = TerrainTileRuleResolver.Resolve(map, theme, 1, 1, 0.86f);

                Assert.IsTrue(info.drawBase);
                Assert.AreSame(corner, info.baseTexture);
                Assert.AreEqual(0f, info.baseYaw);
                Assert.AreEqual(TerrainTileRuleResolver.WalkOverlayScale, info.baseScale);
                Object.DestroyImmediate(theme);
            }
            finally
            {
                Object.DestroyImmediate(corner);
                map.Dispose();
            }
        }

        [Test]
        public void Resolve_ReturnsPlaceTextureAndScale()
        {
            var map = CreateMap(new int2(2, 2), MapTileType.Place);
            var place = NewTexture("place");
            try
            {
                var theme = ScriptableObject.CreateInstance<MapThemeData>();
                theme.placeTileTexture = place;

                var info = TerrainTileRuleResolver.Resolve(map, theme, 1, 1, 0.86f);

                Assert.IsTrue(info.drawBase);
                Assert.AreSame(place, info.baseTexture);
                Assert.AreEqual(0f, info.baseYaw);
                Assert.AreEqual(0.86f, info.baseScale);
                Object.DestroyImmediate(theme);
            }
            finally
            {
                Object.DestroyImmediate(place);
                map.Dispose();
            }
        }

        [Test]
        public void SelectContinuousTerrainTexture_UsesFirstEnvRuleTexture()
        {
            var texture = NewTexture("terrain");
            try
            {
                var theme = ScriptableObject.CreateInstance<MapThemeData>();
                theme.envSurfaceRules = new[]
                {
                    new TerrainSurfaceVariant { texture = texture, weight = 1f }
                };

                Assert.AreSame(texture, TerrainTileRuleResolver.SelectContinuousTerrainTexture(theme));
                Object.DestroyImmediate(theme);
            }
            finally
            {
                Object.DestroyImmediate(texture);
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
                seed = 12,
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
