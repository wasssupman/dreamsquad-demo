using NUnit.Framework;
using Unity.Core;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using Wassup.Battle.Movement;

namespace Wassup.Tests.EditMode
{
    public class MovementSystemTests
    {
        private World _world;
        private EntityManager _em;
        private SystemHandle _movementHandle;

        [SetUp]
        public void SetUp()
        {
            _world = new World("MovementSystemTestWorld");
            _em = _world.EntityManager;

            var simGroup = _world.CreateSystemManaged<SimulationSystemGroup>();
            _movementHandle = _world.CreateSystem<MovementSystem>();
            simGroup.AddSystemToUpdateList(_movementHandle);
        }

        [TearDown]
        public void TearDown()
        {
            _world?.Dispose();
        }

        private Entity CreateUnit(float3 startPos, int2[] waypoints, int startIndex, float speed, float tileSize)
        {
            var e = _em.CreateEntity();
            _em.AddComponentData(e, LocalTransform.FromPosition(startPos));
            _em.AddComponentData(e, new PathFollowState
            {
                currentWaypointIndex = startIndex,
                speed = speed,
                tileSize = tileSize,
            });
            var buffer = _em.AddBuffer<PathWaypoint>(e);
            foreach (var wp in waypoints)
            {
                buffer.Add(new PathWaypoint { cell = wp });
            }
            return e;
        }

        private void TickWorld(float deltaTime)
        {
            _world.SetTime(new TimeData(
                elapsedTime: _world.Time.ElapsedTime + deltaTime,
                deltaTime: deltaTime));
            _world.GetExistingSystemManaged<SimulationSystemGroup>().Update();
        }

        [Test]
        public void Moves_Toward_Next_Waypoint_At_Configured_Speed()
        {
            // 1 second at speed 2 on a 10-unit distance → advance 2 units, stay at same waypoint index
            var waypoints = new[] { new int2(0, 0), new int2(10, 0) };
            var e = CreateUnit(
                startPos: new float3(0f, 0f, 0f),
                waypoints: waypoints,
                startIndex: 1,
                speed: 2f,
                tileSize: 1f);

            TickWorld(1f);

            var transform = _em.GetComponentData<LocalTransform>(e);
            Assert.AreEqual(2f, transform.Position.x, 1e-4f,
                "should have moved 2 units along +x in 1s at speed 2");
            Assert.AreEqual(0f, transform.Position.z, 1e-4f,
                "should stay on z=0 axis");
            Assert.AreEqual(1, _em.GetComponentData<PathFollowState>(e).currentWaypointIndex,
                "still 8 units from waypoint, must not advance yet");
            Assert.IsFalse(_em.HasComponent<PastGoalTag>(e),
                "not at goal yet");
        }

        [Test]
        public void Snaps_To_Waypoint_And_Advances_Index_When_Step_Exceeds_Remaining_Distance()
        {
            // Entity at x=0.5, waypoint at x=1, speed 10 over 1s covers 10 units
            // → distance 0.5 <= step 10 → snap to waypoint, advance index
            var waypoints = new[] { new int2(0, 0), new int2(1, 0) };
            var e = CreateUnit(
                startPos: new float3(0.5f, 0f, 0f),
                waypoints: waypoints,
                startIndex: 1,
                speed: 10f,
                tileSize: 1f);

            TickWorld(1f);

            var transform = _em.GetComponentData<LocalTransform>(e);
            Assert.AreEqual(1f, transform.Position.x, 1e-4f,
                "should snap exactly to waypoint x=1 (no overshoot)");
            Assert.AreEqual(2, _em.GetComponentData<PathFollowState>(e).currentWaypointIndex,
                "should advance past the last waypoint index");
        }

        [Test]
        public void Adds_PastGoalTag_When_Waypoint_Index_Exceeds_Count()
        {
            // Entity with index 2 on a 2-waypoint buffer → past last waypoint → tag on next tick
            var waypoints = new[] { new int2(0, 0), new int2(1, 0) };
            var e = CreateUnit(
                startPos: new float3(1f, 0f, 0f),
                waypoints: waypoints,
                startIndex: 2,
                speed: 1f,
                tileSize: 1f);

            TickWorld(1f);

            Assert.IsTrue(_em.HasComponent<PastGoalTag>(e),
                "MovementSystem must add PastGoalTag once current index >= waypoint count");
        }
    }
}
