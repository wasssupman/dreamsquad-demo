using NUnit.Framework;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
using Wassup.Data;

namespace Wassup.Tests.EditMode
{
    public class BackgroundPropPlacerTests
    {
        [Test]
        public void CanFit_RequiresBackgroundTilesInsideFootprint()
        {
            var prefab = new GameObject("PropPrefab");
            var prop = CreateProp(2, 1, prefab);
            var map = CreateMap(new int2(3, 2), MapTileType.Deco);
            var occupied = new NativeArray<bool>(6, Allocator.Temp);
            try
            {
                map.tiles[0 * map.gridSize.x + 1] = MapTileType.Walk;

                Assert.IsFalse(BackgroundPropPlacer.CanFit(map, prop, occupied, 0, 0));

                map.tiles[1] = MapTileType.Deco;
                Assert.IsTrue(BackgroundPropPlacer.CanFit(map, prop, occupied, 0, 0));
            }
            finally
            {
                occupied.Dispose();
                map.Dispose();
                Object.DestroyImmediate(prop);
                Object.DestroyImmediate(prefab);
            }
        }

        [Test]
        public void CanFit_RejectsOutOfBoundsAndOccupiedCells()
        {
            var prefab = new GameObject("PropPrefab");
            var prop = CreateProp(2, 2, prefab);
            var map = CreateMap(new int2(3, 3), MapTileType.Env);
            var occupied = new NativeArray<bool>(9, Allocator.Temp);
            try
            {
                Assert.IsFalse(BackgroundPropPlacer.CanFit(map, prop, occupied, 2, 0));
                Assert.IsFalse(BackgroundPropPlacer.CanFit(map, prop, occupied, 0, 2));

                occupied[1 * map.gridSize.x + 1] = true;
                Assert.IsFalse(BackgroundPropPlacer.CanFit(map, prop, occupied, 0, 0));

                occupied[4] = false;
                Assert.IsTrue(BackgroundPropPlacer.CanFit(map, prop, occupied, 0, 0));
            }
            finally
            {
                occupied.Dispose();
                map.Dispose();
                Object.DestroyImmediate(prop);
                Object.DestroyImmediate(prefab);
            }
        }

        [Test]
        public void Generate_IsDeterministicForSameSeed()
        {
            var prefab = new GameObject("PropPrefab");
            var prop = CreateProp(2, 1, prefab);
            var theme = ScriptableObject.CreateInstance<MapThemeData>();
            theme.tileProps = new[] { prop };
            theme.tilePropDensity = 1f;
            var a = CreateMap(new int2(5, 3), MapTileType.Deco);
            var b = CreateMap(new int2(5, 3), MapTileType.Deco);
            try
            {
                var placementsA = BackgroundPropPlacer.Generate(a, theme, 1234);
                var placementsB = BackgroundPropPlacer.Generate(b, theme, 1234);

                Assert.AreEqual(placementsA.Count, placementsB.Count);
                for (int i = 0; i < placementsA.Count; i++)
                {
                    Assert.AreEqual(placementsA[i].propIndex, placementsB[i].propIndex);
                    Assert.AreEqual(placementsA[i].x, placementsB[i].x);
                    Assert.AreEqual(placementsA[i].y, placementsB[i].y);
                    Assert.AreEqual(placementsA[i].width, placementsB[i].width);
                    Assert.AreEqual(placementsA[i].height, placementsB[i].height);
                }
            }
            finally
            {
                a.Dispose();
                b.Dispose();
                Object.DestroyImmediate(theme);
                Object.DestroyImmediate(prop);
                Object.DestroyImmediate(prefab);
            }
        }

        [Test]
        public void Generate_DoesNotOverlapOrUseNonBackgroundTiles()
        {
            var prefabA = new GameObject("PropPrefabA");
            var prefabB = new GameObject("PropPrefabB");
            var prop2x1 = CreateProp(2, 1, prefabA);
            var prop1x2 = CreateProp(1, 2, prefabB);
            var theme = ScriptableObject.CreateInstance<MapThemeData>();
            theme.tileProps = new[] { prop2x1, prop1x2 };
            theme.tilePropDensity = 1f;
            var map = CreateMap(new int2(5, 4), MapTileType.Deco);
            try
            {
                map.tiles[1 * map.gridSize.x + 2] = MapTileType.Walk;
                map.tiles[2 * map.gridSize.x + 2] = MapTileType.Place;

                var placements = BackgroundPropPlacer.Generate(map, theme, 77);
                var occupied = new bool[map.gridSize.x * map.gridSize.y];

                foreach (var placement in placements)
                {
                    for (int dy = 0; dy < placement.height; dy++)
                    for (int dx = 0; dx < placement.width; dx++)
                    {
                        int x = placement.x + dx;
                        int y = placement.y + dy;
                        Assert.GreaterOrEqual(x, 0);
                        Assert.GreaterOrEqual(y, 0);
                        Assert.Less(x, map.gridSize.x);
                        Assert.Less(y, map.gridSize.y);

                        int index = y * map.gridSize.x + x;
                        Assert.IsFalse(occupied[index], $"overlap at {x},{y}");
                        Assert.IsTrue(BackgroundPropPlacer.IsBackgroundTile(map.tiles[index]), $"non-background tile at {x},{y}");
                        occupied[index] = true;
                    }
                }
            }
            finally
            {
                map.Dispose();
                Object.DestroyImmediate(theme);
                Object.DestroyImmediate(prop2x1);
                Object.DestroyImmediate(prop1x2);
                Object.DestroyImmediate(prefabA);
                Object.DestroyImmediate(prefabB);
            }
        }

        [Test]
        public void Generate_CentersPropInAvailableRegion()
        {
            var prefab = new GameObject("PropPrefab");
            var prop = CreateProp(2, 2, prefab);
            var theme = ScriptableObject.CreateInstance<MapThemeData>();
            theme.tileProps = new[] { prop };
            theme.tilePropDensity = 1f;
            theme.maxTilePropCount = 1;
            var map = CreateMap(new int2(5, 5), MapTileType.Deco);
            try
            {
                var placements = BackgroundPropPlacer.Generate(map, theme, 100);

                Assert.AreEqual(1, placements.Count);
                Assert.AreEqual(1, placements[0].x);
                Assert.AreEqual(1, placements[0].y);
                Assert.AreEqual(2, placements[0].width);
                Assert.AreEqual(2, placements[0].height);
            }
            finally
            {
                map.Dispose();
                Object.DestroyImmediate(theme);
                Object.DestroyImmediate(prop);
                Object.DestroyImmediate(prefab);
            }
        }

        [Test]
        public void Generate_RespectsDensityAndMaxCount()
        {
            var prefab = new GameObject("PropPrefab");
            var prop = CreateProp(1, 1, prefab);
            var theme = ScriptableObject.CreateInstance<MapThemeData>();
            theme.tileProps = new[] { prop };
            var map = CreateMap(new int2(4, 4), MapTileType.Deco);
            try
            {
                theme.tilePropDensity = 0f;
                Assert.AreEqual(0, BackgroundPropPlacer.Generate(map, theme, 1).Count);

                theme.tilePropDensity = 1f;
                theme.maxTilePropCount = 3;
                var placements = BackgroundPropPlacer.Generate(map, theme, 1);
                Assert.AreEqual(3, placements.Count);
                Assert.LessOrEqual(placements.Count, 3);
            }
            finally
            {
                map.Dispose();
                Object.DestroyImmediate(theme);
                Object.DestroyImmediate(prop);
                Object.DestroyImmediate(prefab);
            }
        }

        private static PropData CreateProp(int width, int height, GameObject prefab)
        {
            var prop = ScriptableObject.CreateInstance<PropData>();
            prop.footprintX = width;
            prop.footprintY = height;
            prop.prefab = prefab;
            return prop;
        }

        private static GeneratedMap CreateMap(int2 gridSize, MapTileType fill)
        {
            int count = gridSize.x * gridSize.y;
            var tiles = new NativeArray<MapTileType>(count, Allocator.Temp);
            for (int i = 0; i < count; i++)
                tiles[i] = fill;

            return new GeneratedMap
            {
                tiles = tiles,
                gridSize = gridSize,
                spawns = new NativeArray<int2>(1, Allocator.Temp),
                goal = new int2(gridSize.x - 1, gridSize.y - 1),
                seed = 1,
                generatorVersion = 1,
            };
        }
    }
}
