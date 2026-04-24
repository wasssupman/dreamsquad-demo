using NUnit.Framework;
using Unity.Collections;
using Unity.Mathematics;
using Wassup.Data;

namespace Wassup.Tests.EditMode
{
    public class BoardVisualPlanBuilderTests
    {
        [Test]
        public void Build_FoldsDecoIntoEnvAndGroupsRegions()
        {
            var map = CreateMap(4, 3, MapTileType.Deco);
            try
            {
                map.tiles[1] = MapTileType.Walk;
                map.tiles[2] = MapTileType.Walk;
                map.tiles[4] = MapTileType.Place;
                map.tiles[5] = MapTileType.Place;

                var plan = BoardVisualPlanBuilder.Build(map, 123);

                Assert.AreEqual(4, plan.Regions.Count);
                Assert.AreEqual(BoardZoneType.Env, plan.CellAt(new int2(3, 2)).zoneType);
                Assert.AreEqual(BoardZoneType.Place, plan.CellAt(new int2(0, 1)).zoneType);
                Assert.AreEqual(BoardZoneType.Walk, plan.CellAt(new int2(1, 0)).zoneType);
                Assert.AreEqual(MapTileType.Deco, plan.CellAt(new int2(3, 2)).sourceTileType);
                Assert.AreNotEqual(plan.CellAt(new int2(0, 0)).regionId, plan.CellAt(new int2(3, 2)).regionId);
            }
            finally
            {
                map.Dispose();
            }
        }

        [Test]
        public void Build_ComputesShapeAndTransitionMask()
        {
            var map = CreateMap(3, 3, MapTileType.Env);
            try
            {
                map.tiles[1 * map.gridSize.x + 1] = MapTileType.Walk;
                map.tiles[2 * map.gridSize.x + 1] = MapTileType.Walk;
                map.tiles[0 * map.gridSize.x + 1] = MapTileType.Walk;

                var plan = BoardVisualPlanBuilder.Build(map, 77);
                var center = plan.CellAt(new int2(1, 1));

                Assert.AreEqual(BoardZoneType.Walk, center.zoneType);
                Assert.AreEqual(5, center.sameZoneMask);
                Assert.AreEqual(10, center.transitionMask);
                Assert.AreEqual(10, center.envNeighborMask);
                Assert.AreEqual(BoardShapeType.StraightNS, center.shapeClass);
            }
            finally
            {
                map.Dispose();
            }
        }

        [Test]
        public void Build_CopiesGoalAndSpawnsIntoPlan()
        {
            var map = CreateMap(4, 4, MapTileType.Env);
            try
            {
                map.goal = new int2(3, 2);
                map.spawns[0] = new int2(0, 1);

                var plan = BoardVisualPlanBuilder.Build(map, 91);

                Assert.AreEqual(new int2(3, 2), plan.goal);
                Assert.AreEqual(1, plan.spawns.Length);
                Assert.AreEqual(new int2(0, 1), plan.spawns[0]);
            }
            finally
            {
                map.Dispose();
            }
        }

        [Test]
        public void Build_ComputesPathAndBorderProximity()
        {
            var map = CreateMap(5, 5, MapTileType.Env);
            try
            {
                map.tiles[2 * map.gridSize.x + 2] = MapTileType.Walk;

                var plan = BoardVisualPlanBuilder.Build(map, 17);

                Assert.AreEqual(0, plan.CellAt(new int2(2, 2)).pathProximity);
                Assert.AreEqual(1, plan.CellAt(new int2(2, 3)).pathProximity);
                Assert.AreEqual(4, plan.CellAt(new int2(0, 0)).pathProximity);

                Assert.AreEqual(0, plan.CellAt(new int2(0, 0)).borderProximity);
                Assert.AreEqual(1, plan.CellAt(new int2(1, 1)).borderProximity);
                Assert.AreEqual(2, plan.CellAt(new int2(2, 2)).borderProximity);
            }
            finally
            {
                map.Dispose();
            }
        }

        [Test]
        public void Build_UsesNoPathProximitySentinelWhenWalkIsAbsent()
        {
            var map = CreateMap(3, 3, MapTileType.Env);
            try
            {
                var plan = BoardVisualPlanBuilder.Build(map, 23);
                var cell = plan.CellAt(new int2(1, 1));

                Assert.AreEqual(BoardVisualCell.NoPathProximity, cell.pathProximity);
                Assert.IsFalse(cell.HasPathProximity);
            }
            finally
            {
                map.Dispose();
            }
        }

        [Test]
        public void Build_CreatesBasicDecorAnchorsForEnvRegions()
        {
            var map = CreateMap(3, 3, MapTileType.Env);
            try
            {
                map.tiles[1] = MapTileType.Walk;
                map.tiles[3] = MapTileType.Place;

                var plan = BoardVisualPlanBuilder.Build(map, 5);

                int envRegionCount = 0;
                int envCenterAnchorCount = 0;
                for (int i = 0; i < plan.Regions.Count; i++)
                {
                    if (plan.Regions[i].zoneType == BoardZoneType.Env)
                        envRegionCount++;
                }

                for (int i = 0; i < plan.DecorAnchors.Count; i++)
                {
                    var anchor = plan.DecorAnchors[i];
                    Assert.AreEqual(BoardZoneType.Env, plan.Regions[anchor.regionId].zoneType);
                    if (anchor.anchorType == BoardDecorAnchorType.RegionCenter)
                        envCenterAnchorCount++;
                }

                Assert.AreEqual(envRegionCount, envCenterAnchorCount);
            }
            finally
            {
                map.Dispose();
            }
        }

        [Test]
        public void Build_DoesNotCreateDecorAnchorsForWalkOrPlaceRegions()
        {
            var map = CreateMap(3, 3, MapTileType.Env);
            try
            {
                map.tiles[0] = MapTileType.Walk;
                map.tiles[1] = MapTileType.Walk;
                map.tiles[3] = MapTileType.Place;
                map.tiles[4] = MapTileType.Place;

                var plan = BoardVisualPlanBuilder.Build(map, 31);

                for (int i = 0; i < plan.DecorAnchors.Count; i++)
                    Assert.AreEqual(BoardZoneType.Env, plan.Regions[plan.DecorAnchors[i].regionId].zoneType);
            }
            finally
            {
                map.Dispose();
            }
        }

        [Test]
        public void Build_DoesNotTreatBoardEdgeAsZoneTransition()
        {
            var map = CreateMap(3, 3, MapTileType.Env);
            try
            {
                var plan = BoardVisualPlanBuilder.Build(map, 9);
                var corner = plan.CellAt(new int2(0, 0));

                Assert.AreEqual(BoardZoneType.Env, corner.zoneType);
                Assert.AreEqual(3, corner.sameZoneMask);
                Assert.AreEqual(0, corner.transitionMask);
            }
            finally
            {
                map.Dispose();
            }
        }

        [Test]
        public void Build_ReturnsEmptyPlanForZeroSizedMapAndCellAtIsGuarded()
        {
            var map = new GeneratedMap
            {
                tiles = new NativeArray<MapTileType>(0, Allocator.Persistent),
                gridSize = int2.zero,
                spawns = new NativeArray<int2>(0, Allocator.Persistent),
                goal = int2.zero,
                seed = 1,
                generatorVersion = 1,
            };

            try
            {
                var plan = BoardVisualPlanBuilder.Build(map, 41);
                var cell = plan.CellAt(int2.zero);

                Assert.AreEqual(int2.zero, plan.gridSize);
                Assert.AreEqual(0, plan.Cells.Count);
                Assert.AreEqual(-1, cell.regionId);
                Assert.AreEqual(BoardVisualCell.NoPathProximity, cell.pathProximity);
            }
            finally
            {
                map.Dispose();
            }
        }

        private static GeneratedMap CreateMap(int width, int height, MapTileType fill)
        {
            int count = width * height;
            var tiles = new NativeArray<MapTileType>(count, Allocator.Persistent);
            for (int i = 0; i < count; i++)
                tiles[i] = fill;

            return new GeneratedMap
            {
                tiles = tiles,
                gridSize = new int2(width, height),
                spawns = new NativeArray<int2>(1, Allocator.Persistent),
                goal = new int2(width - 1, height - 1),
                seed = 1,
                generatorVersion = 1,
            };
        }
    }
}
