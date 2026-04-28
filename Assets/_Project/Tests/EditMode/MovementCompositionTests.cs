using NUnit.Framework;
using Unity.Collections;
using Unity.Core;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using Wassup.Battle.Effects;
using Wassup.Battle.Movement;

namespace Wassup.Tests.EditMode
{
    // Verifies the CC composition math in MovementSystem:
    //   final_pos = current + flowStep * speedMul + impulseDisplacement
    // Uses speed=2, dt=1s, +x unit flow so expected deltas are easy to assert.
    public class MovementCompositionTests
    {
        private World _world;
        private EntityManager _em;
        private SimulationSystemGroup _simGroup;
        private Entity _fieldEntity;

        [SetUp]
        public void SetUp()
        {
            _world = new World("MovementCompositionTestWorld");
            _em = _world.EntityManager;
            _simGroup = _world.CreateSystemManaged<SimulationSystemGroup>();
            _simGroup.AddSystemToUpdateList(_world.CreateSystem<MovementSystem>());

            // 2-cell +x flow field. cell (0,0) → (+x), cell (1,0) = goal.
            var flow = new NativeArray<float2>(2, Allocator.Persistent);
            var dist = new NativeArray<int>(2, Allocator.Persistent);
            flow[0] = new float2(1, 0); dist[0] = 1;
            flow[1] = float2.zero;      dist[1] = 0;
            _fieldEntity = _em.CreateEntity();
            _em.AddComponentData(_fieldEntity, new FlowFieldSingleton
            {
                flow = flow, dist = dist,
                gridSize = new int2(2, 1),
                goalCell = new int2(1, 0),
                tileSize = 1f, version = 1,
            });
        }

        [TearDown]
        public void TearDown()
        {
            if (_em.Exists(_fieldEntity) && _em.HasComponent<FlowFieldSingleton>(_fieldEntity))
                _em.GetComponentData<FlowFieldSingleton>(_fieldEntity).Dispose();
            _world?.Dispose();
        }

        private Entity CreateUnit()
        {
            var e = _em.CreateEntity();
            _em.AddComponentData(e, LocalTransform.FromPosition(float3.zero));
            _em.AddComponentData(e, new PathFollowState { speed = 2f });
            return e;
        }

        private void Tick(float dt = 1f)
        {
            _world.SetTime(new TimeData(_world.Time.ElapsedTime + dt, dt));
            _simGroup.Update();
        }

        [Test]
        public void No_CC_Moves_By_FlowStep()
        {
            var e = CreateUnit();
            Tick();
            var pos = _em.GetComponentData<LocalTransform>(e).Position;
            Assert.AreEqual(2f, pos.x, 1e-4f, "speed 2 × dt 1 × no modifier = 2");
            Assert.AreEqual(0f, pos.z, 1e-4f);
        }

        [Test]
        public void Slow_Only_Scales_FlowStep()
        {
            var e = CreateUnit();
            var buf = _em.AddBuffer<CcEffect>(e);
            buf.Add(new CcEffect { kind = CcKind.Slow, scalar = 0.5f, remainingTime = 5f });

            Tick();

            var pos = _em.GetComponentData<LocalTransform>(e).Position;
            Assert.AreEqual(1f, pos.x, 1e-4f, "speed 2 × 0.5 slow × dt 1 = 1");
            Assert.AreEqual(0f, pos.z, 1e-4f);
        }

        [Test]
        public void Impulse_Only_Adds_Displacement()
        {
            var e = CreateUnit();
            var buf = _em.AddBuffer<CcEffect>(e);
            buf.Add(new CcEffect { kind = CcKind.Impulse, vector = new float3(0, 0, 3f), remainingTime = 5f });

            Tick();

            var pos = _em.GetComponentData<LocalTransform>(e).Position;
            Assert.AreEqual(2f, pos.x, 1e-4f, "flow step unaffected");
            Assert.AreEqual(3f, pos.z, 1e-4f, "impulse 3 u/s × dt 1 = 3");
        }

        [Test]
        public void Slow_And_Impulse_Compose_Independently()
        {
            var e = CreateUnit();
            var buf = _em.AddBuffer<CcEffect>(e);
            buf.Add(new CcEffect { kind = CcKind.Slow, scalar = 0.5f, remainingTime = 5f });
            buf.Add(new CcEffect { kind = CcKind.Impulse, vector = new float3(0, 0, 3f), remainingTime = 5f });

            Tick();

            var pos = _em.GetComponentData<LocalTransform>(e).Position;
            Assert.AreEqual(1f, pos.x, 1e-4f, "flow step halved by slow");
            Assert.AreEqual(3f, pos.z, 1e-4f, "impulse adds z displacement");
        }
    }
}
