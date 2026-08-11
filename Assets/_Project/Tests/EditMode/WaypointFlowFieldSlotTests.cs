using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using Unity.Collections;
using Unity.Core;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using Wassup.Battle.Effects;
using Wassup.Battle.Movement;
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
                Assert.AreEqual(1, field.WaypointCountAt(0));
                Assert.AreEqual(2, field.WaypointCountAt(1));
                Assert.AreEqual(new int2(0, 2), field.WaypointAt(1, 1),
                    "목적지 중복 제거와 별개로 경로 순서는 보존");
            }
            finally
            {
                map.Dispose();
            }
        }

        [Test]
        public void Teardown_DisposesWaypointRouteProjection()
        {
            var map = MakeMap();
            try
            {
                SimFieldInstaller.InstallNavFields(
                    _em, in map, 1f, float3.zero, ref _handles);
                var field = _em.GetComponentData<FlowFieldSingleton>(_handles.flowField);

                SimFieldInstaller.Teardown(_world, _em, ref _handles);

                Assert.Catch(() => { var _ = field.waypointCells[0]; });
                Assert.Catch(() => { var _ = field.waypointRanges[0]; });
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

        [Test]
        public void SpawnGuidePath_PassesWaypointsInOrderThenGoal()
        {
            var map = MakeMap();
            GameObject go = null;
            BattleBridge bridge = null;
            try
            {
                SimFieldInstaller.InstallNavFields(
                    _em, in map, 1f, float3.zero, ref _handles);
                go = new GameObject("BattleBridge_WaypointGuideTest");
                bridge = go.AddComponent<BattleBridge>();
                SetField(bridge, "_em", _em);
                SetField(bridge, "_generatedMap", map);
                SetField(bridge, "_simFields", _handles);

                var path = new List<Vector3>();
                Assert.IsTrue(bridge.TryGetSpawnPathSim(
                    laneIndex: 0,
                    waypointPathIndex: 1,
                    traversalLayers: (byte)PlacementLayer.Path,
                    outPath: path));

                int firstWaypoint = FindCell(path, new int2(2, 1), map.gridSize);
                int secondWaypoint = FindCell(path, new int2(0, 2), map.gridSize);
                int goal = FindCell(path, map.goal, map.gridSize);
                Assert.GreaterOrEqual(firstWaypoint, 0, "첫 waypoint 를 지나야 한다");
                Assert.Greater(secondWaypoint, firstWaypoint, "저작 순서대로 두 번째 waypoint 를 지나야 한다");
                Assert.Greater(goal, secondWaypoint, "마지막 waypoint 뒤 goal 로 이어져야 한다");

                var explicitGoal = new List<Vector3>();
                Assert.IsTrue(bridge.TryGetSpawnPathSim(
                    0, -1, (byte)PlacementLayer.Path, explicitGoal));
                Assert.GreaterOrEqual(FindCell(explicitGoal, map.goal, map.gridSize), 0,
                    "미저작 적은 goal 경로를 사용한다");

            }
            finally
            {
                // BattleBridge 는 이 픽스처의 NativeArray owner 가 아니다. OnDestroy 의
                // 일반 teardown 전에 복사 핸들을 비우고 TearDown/finally 에서 한 번만 해제한다.
                if (bridge != null)
                {
                    SetField(bridge, "_generatedMap", default(GeneratedMap));
                    SetField(bridge, "_simFields", default(SimFieldHandles));
                }
                if (go != null) Object.DestroyImmediate(go);
                map.Dispose();
            }
        }

        private static int FindCell(List<Vector3> path, int2 target, int2 gridSize)
        {
            for (int i = 0; i < path.Count; i++)
            {
                int2 cell = GridMath.WorldToCell((float3)path[i], 1f, gridSize);
                if (cell.Equals(target)) return i;
            }
            return -1;
        }

        private static void SetField(object target, string name, object value)
        {
            var field = target.GetType().GetField(
                name, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            Assert.IsNotNull(field, $"Field '{name}' not found");
            field.SetValue(target, value);
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
