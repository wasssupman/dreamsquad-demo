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

                var plan = BoardVisualPlanBuilder.Build(map, 1);
                Assert.IsFalse(BackgroundPropPlacer.CanFit(plan, prop, occupied, 0, 0));

                map.tiles[1] = MapTileType.Deco;
                plan = BoardVisualPlanBuilder.Build(map, 1);
                Assert.IsTrue(BackgroundPropPlacer.CanFit(plan, prop, occupied, 0, 0));
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
                var plan = BoardVisualPlanBuilder.Build(map, 1);
                Assert.IsFalse(BackgroundPropPlacer.CanFit(plan, prop, occupied, 2, 0));
                Assert.IsFalse(BackgroundPropPlacer.CanFit(plan, prop, occupied, 0, 2));

                occupied[1 * map.gridSize.x + 1] = true;
                Assert.IsFalse(BackgroundPropPlacer.CanFit(plan, prop, occupied, 0, 0));

                occupied[4] = false;
                Assert.IsTrue(BackgroundPropPlacer.CanFit(plan, prop, occupied, 0, 0));
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
                var placementsA = BackgroundPropPlacer.Generate(BoardVisualPlanBuilder.Build(a, 1234), theme, 1234);
                var placementsB = BackgroundPropPlacer.Generate(BoardVisualPlanBuilder.Build(b, 1234), theme, 1234);

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

                var plan = BoardVisualPlanBuilder.Build(map, 77);
                var placements = BackgroundPropPlacer.Generate(plan, theme, 77);
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
                        Assert.IsTrue(BackgroundPropPlacer.IsBackgroundCell(plan.CellAt(new int2(x, y))), $"non-background tile at {x},{y}");
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
            theme.largePropInnerWeightMultiplier = 1f;
            var map = CreateMap(new int2(5, 5), MapTileType.Deco);
            try
            {
                var placements = BackgroundPropPlacer.Generate(BoardVisualPlanBuilder.Build(map, 100), theme, 100);

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
        public void Generate_AvoidsRecentlyUsedPropWhenAlternativeFits()
        {
            var prefabA = new GameObject("PropPrefabA");
            var prefabB = new GameObject("PropPrefabB");
            var propA = CreateProp(1, 1, prefabA);
            var propB = CreateProp(1, 1, prefabB);
            propA.placementWeight = 1000;
            propB.placementWeight = 1;
            var theme = ScriptableObject.CreateInstance<MapThemeData>();
            theme.tileProps = new[] { propA, propB };
            theme.tilePropDensity = 1f;
            theme.maxTilePropCount = 2;
            theme.propRepeatAvoidanceWindow = 1;
            theme.spawnGoalPropAvoidRadius = 0;
            var map = CreateMap(new int2(4, 2), MapTileType.Deco);
            try
            {
                var placements = BackgroundPropPlacer.Generate(BoardVisualPlanBuilder.Build(map, 8), theme, 8);

                Assert.AreEqual(2, placements.Count);
                Assert.AreNotEqual(placements[0].propIndex, placements[1].propIndex);
            }
            finally
            {
                map.Dispose();
                Object.DestroyImmediate(theme);
                Object.DestroyImmediate(propA);
                Object.DestroyImmediate(propB);
                Object.DestroyImmediate(prefabA);
                Object.DestroyImmediate(prefabB);
            }
        }

        [Test]
        public void Generate_RespectsPropMinDistance()
        {
            var prefab = new GameObject("PropPrefab");
            var prop = CreateProp(1, 1, prefab);
            prop.minDistanceCells = 2;
            var theme = ScriptableObject.CreateInstance<MapThemeData>();
            theme.tileProps = new[] { prop };
            theme.tilePropDensity = 1f;
            theme.maxTilePropCount = 8;
            theme.spawnGoalPropAvoidRadius = 0;
            var map = CreateMap(new int2(6, 2), MapTileType.Deco);
            try
            {
                var placements = BackgroundPropPlacer.Generate(BoardVisualPlanBuilder.Build(map, 10), theme, 10);

                for (int i = 0; i < placements.Count; i++)
                for (int j = i + 1; j < placements.Count; j++)
                {
                    float ax = placements[i].x + (placements[i].width - 1) * 0.5f;
                    float ay = placements[i].y + (placements[i].height - 1) * 0.5f;
                    float bx = placements[j].x + (placements[j].width - 1) * 0.5f;
                    float by = placements[j].y + (placements[j].height - 1) * 0.5f;
                    Assert.GreaterOrEqual(Mathf.Max(Mathf.Abs(ax - bx), Mathf.Abs(ay - by)), 2f);
                }
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
        public void Generate_PrefersSmallPropsNextToWalkCells()
        {
            var prefabSmall = new GameObject("PropPrefabSmall");
            var prefabLarge = new GameObject("PropPrefabLarge");
            var small = CreateProp(1, 1, prefabSmall);
            var large = CreateProp(2, 2, prefabLarge);
            small.placementWeight = 1;
            large.placementWeight = 1000;
            var theme = ScriptableObject.CreateInstance<MapThemeData>();
            theme.tileProps = new[] { small, large };
            theme.tilePropDensity = 1f;
            theme.maxTilePropCount = 1;
            theme.spawnGoalPropAvoidRadius = 0;
            theme.pathAdjacentSmallPropMaxArea = 1;
            theme.pathAdjacentLargePropWeightMultiplier = 0f;
            var map = CreateMap(new int2(4, 3), MapTileType.Deco);
            try
            {
                for (int x = 0; x < map.gridSize.x; x++)
                    map.tiles[x] = MapTileType.Walk;

                var placements = BackgroundPropPlacer.Generate(BoardVisualPlanBuilder.Build(map, 12), theme, 12);

                Assert.AreEqual(1, placements.Count);
                Assert.AreEqual(0, placements[0].propIndex);
                Assert.AreEqual(1, placements[0].width * placements[0].height);
            }
            finally
            {
                map.Dispose();
                Object.DestroyImmediate(theme);
                Object.DestroyImmediate(small);
                Object.DestroyImmediate(large);
                Object.DestroyImmediate(prefabSmall);
                Object.DestroyImmediate(prefabLarge);
            }
        }

        [Test]
        public void Generate_ReducesPlacementNearSpawnAndGoal()
        {
            var prefab = new GameObject("PropPrefab");
            var prop = CreateProp(1, 1, prefab);
            var theme = ScriptableObject.CreateInstance<MapThemeData>();
            theme.tileProps = new[] { prop };
            theme.tilePropDensity = 1f;
            theme.maxTilePropCount = 1;
            theme.spawnGoalPropAvoidRadius = 10;
            theme.spawnGoalPropDensityMultiplier = 0f;
            var map = CreateMap(new int2(4, 4), MapTileType.Deco);
            try
            {
                map.goal = new int2(3, 3);
                map.spawns[0] = new int2(0, 0);

                var placements = BackgroundPropPlacer.Generate(BoardVisualPlanBuilder.Build(map, 14), theme, 14);

                Assert.AreEqual(0, placements.Count);
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
                Assert.AreEqual(0, BackgroundPropPlacer.Generate(BoardVisualPlanBuilder.Build(map, 1), theme, 1).Count);

                theme.tilePropDensity = 1f;
                theme.maxTilePropCount = 3;
                var placements = BackgroundPropPlacer.Generate(BoardVisualPlanBuilder.Build(map, 1), theme, 1);
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
