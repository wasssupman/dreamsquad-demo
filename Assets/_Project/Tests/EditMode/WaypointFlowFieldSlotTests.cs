using NUnit.Framework;
using Unity.Collections;
using Unity.Core;
using Unity.Entities;
using Unity.Mathematics;
using Wassup.Battle.Effects;
using Wassup.Bridge;
using Wassup.Data;

namespace Wassup.Tests.EditMode
{
    public class WaypointFlowFieldSlotTests
    {
        private World _world;
        private EntityManager _em;
        private SimFieldHandles _handles;
        private NativeHashSet<int2> _blockedCells;

        [SetUp]
        public void SetUp()
        {
            _world = new World("WaypointFlowFieldSlotTests");
            _em = _world.EntityManager;
            _handles = default;
            _blockedCells = new NativeHashSet<int2>(8, Allocator.Persistent);
        }

        [TearDown]
        public void TearDown()
        {
            SimFieldInstaller.Teardown(_world, _em, ref _handles);
            if (_blockedCells.IsCreated) _blockedCells.Dispose();
            _world?.Dispose();
        }

        [Test]
        public void Install_IndexesGoalAndUniqueWaypointDestinations()
        {
            var map = MakeMap();
            try
            {
                SimFieldInstaller.InstallNavFields(
                    _em, in map, 1f, float3.zero, ref _handles);
                var field = _em.GetComponentData<FlowFieldSingleton>(_handles.flowField);

                int goalSlot = field.SlotFor(
                    FlowFieldSingleton.GoalSentinel, (byte)PlacementLayer.Path);
                int firstWaypointSlot = field.SlotFor(
                    new int2(2, 1), (byte)PlacementLayer.Path);
                int secondWaypointSlot = field.SlotFor(
                    new int2(0, 2), (byte)PlacementLayer.Path);

                Assert.AreEqual(FlowFieldSingleton.PrimarySlot, goalSlot,
                    "슬롯 0은 항상 (골, DefaultMask)");
                Assert.AreEqual(3, field.SlotCount,
                    "골 1 + 중복 제거된 웨이포인트 2");
                Assert.AreEqual(1, firstWaypointSlot);
                Assert.AreEqual(2, secondWaypointSlot);
                Assert.AreEqual(FlowFieldSingleton.PrimarySlot,
                    field.SlotFor(new int2(4, 2), (byte)PlacementLayer.Path),
                    "미등록 목적지 조합은 primary 폴백");
            }
            finally
            {
                map.Dispose();
            }
        }

        [Test]
        public void ObstacleRebuild_UsesEachSlotsOwnDestination()
        {
            var map = MakeMap();
            try
            {
                SimFieldInstaller.InstallNavFields(
                    _em, in map, 1f, float3.zero, ref _handles);
                var obstacleEntity = _em.CreateEntity();
                _blockedCells.Add(new int2(3, 0));
                _em.AddComponentData(obstacleEntity, new ObstacleSingleton
                {
                    blockedCells = _blockedCells,
                });

                var group = _world.CreateSystemManaged<SimulationSystemGroup>();
                var rebuild = _world.CreateSystem<FlowFieldRebuildSystem>();
                group.AddSystemToUpdateList(rebuild);
                _world.SetTime(new TimeData(0.016, 0.016f));
                group.Update();

                var field = _em.GetComponentData<FlowFieldSingleton>(_handles.flowField);
                int goalSlot = field.SlotFor(
                    FlowFieldSingleton.GoalSentinel, (byte)PlacementLayer.Path);
                int waypointSlot = field.SlotFor(
                    new int2(2, 1), (byte)PlacementLayer.Path);

                Assert.AreNotEqual(0u, field.blockedSignature, "장애물 변경이 재빌드를 촉발해야 한다");
                Assert.AreEqual(0, field.DistSlot(goalSlot)[map.CellIndex(map.goal)],
                    "골 슬롯은 골을 소스로 재빌드");
                Assert.AreEqual(0, field.DistSlot(waypointSlot)[map.CellIndex(new int2(2, 1))],
                    "웨이포인트 슬롯은 자기 목적지를 소스로 재빌드");
            }
            finally
            {
                map.Dispose();
            }
        }

        // 5x3 전부 Walk. waypointCells 는 두 경로를 flatten 한 모양이며 (2,1)을 공유한다.
        private static GeneratedMap MakeMap()
        {
            var gridSize = new int2(5, 3);
            int cellCount = gridSize.x * gridSize.y;
            var tiles = new NativeArray<MapTileType>(cellCount, Allocator.Persistent);
            for (int i = 0; i < cellCount; i++) tiles[i] = MapTileType.Walk;

            var spawns = new NativeArray<int2>(1, Allocator.Persistent);
            spawns[0] = new int2(0, 1);
            var goals = new NativeArray<int2>(1, Allocator.Persistent);
            goals[0] = new int2(4, 1);
            var waypointCells = new NativeArray<int2>(3, Allocator.Persistent);
            waypointCells[0] = new int2(2, 1);
            waypointCells[1] = new int2(2, 1);
            waypointCells[2] = new int2(0, 2);
            var waypointRanges = new NativeArray<int2>(2, Allocator.Persistent);
            waypointRanges[0] = new int2(0, 1);
            waypointRanges[1] = new int2(1, 2);

            return new GeneratedMap
            {
                tiles = tiles,
                spawns = spawns,
                goals = goals,
                goal = goals[0],
                waypointCells = waypointCells,
                waypointRanges = waypointRanges,
                gridSize = gridSize,
            };
        }
    }
}
