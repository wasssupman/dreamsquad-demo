using NUnit.Framework;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;
using Wassup.Battle.Effects;
using Wassup.Battle.Movement;
using Wassup.Battle.Units;

namespace Wassup.Tests.EditMode
{
    public class SpawnBlockingHazardTests
    {
        private World _world;
        private EntityManager _em;
        private NativeArray<float2> _flow;
        private NativeArray<int> _dist;
        private NativeHashSet<int2> _blockedCells;
        private BlockingHazardSO _so;

        [SetUp]
        public void SetUp()
        {
            _world = new World("SpawnBlockingHazardTests");
            _em = _world.EntityManager;

            _flow = new NativeArray<float2>(100, Allocator.Persistent);
            _dist = new NativeArray<int>(100, Allocator.Persistent);
            var ffEntity = _em.CreateEntity();
            _em.AddComponentData(ffEntity, new FlowFieldSingleton
            {
                flow = _flow,
                dist = _dist,
                gridSize = new int2(10, 10),
                goalCell = new int2(9, 9),
                tileSize = 1f,
                version = 1,
            });

            _blockedCells = new NativeHashSet<int2>(16, Allocator.Persistent);
            var obstacleEntity = _em.CreateEntity();
            _em.AddComponentData(obstacleEntity, new ObstacleSingleton { blockedCells = _blockedCells });

            _so = ScriptableObject.CreateInstance<BlockingHazardSO>();
            _so.shape = HazardShape.Square3x3;
            _so.maxHp = 100f;
        }

        [TearDown]
        public void TearDown()
        {
            if (_flow.IsCreated) _flow.Dispose();
            if (_dist.IsCreated) _dist.Dispose();
            if (_blockedCells.IsCreated) _blockedCells.Dispose();
            if (_so != null) Object.DestroyImmediate(_so);
            _world?.Dispose();
        }

        [Test]
        public void Spawn_Creates_BlockingHazard_With_Combat_Components_And_Cells()
        {
            var e = EffectSpawner.SpawnBlockingHazard(_em, _so, new int2(4, 4), hazardSoIndex: 3);

            Assert.AreNotEqual(Entity.Null, e);
            Assert.IsTrue(_em.HasComponent<Obstacle>(e));
            Assert.IsTrue(_em.HasComponent<BlockingHazard>(e));
            Assert.IsTrue(_em.HasBuffer<OccupiedCellsBuffer>(e));
            Assert.IsTrue(_em.HasComponent<Health>(e));
            Assert.IsTrue(_em.HasBuffer<IncomingDamage>(e));
            Assert.IsTrue(_em.HasComponent<FactionTag>(e));
            Assert.IsTrue(_em.HasComponent<LocalTransform>(e));

            Assert.AreEqual(Faction.BlockingHazard, _em.GetComponentData<FactionTag>(e).value);
            Assert.AreEqual(100f, _em.GetComponentData<Health>(e).max, 1e-4f);
            Assert.AreEqual(3, _em.GetComponentData<BlockingHazard>(e).hazardSoIndex);
            Assert.AreEqual(9, _em.GetBuffer<OccupiedCellsBuffer>(e).Length);
            Assert.AreEqual(new int2(4, 4), _em.GetComponentData<Obstacle>(e).cell);
            Assert.AreEqual(GridMath.CellToWorldCenter(new int2(4, 4), 1f), _em.GetComponentData<LocalTransform>(e).Position);
        }

        [Test]
        public void Spawn_Rejects_Out_Of_Bounds_Cell()
        {
            var e = EffectSpawner.SpawnBlockingHazard(_em, _so, new int2(0, 0), hazardSoIndex: 0);

            Assert.AreEqual(Entity.Null, e);
        }

        [Test]
        public void Spawn_Rejects_Goal_Cell_Overlap()
        {
            var e = EffectSpawner.SpawnBlockingHazard(_em, _so, new int2(8, 8), hazardSoIndex: 0);

            Assert.AreEqual(Entity.Null, e);
        }

        [Test]
        public void Spawn_Rejects_Existing_Blocked_Cell_Overlap()
        {
            _blockedCells.Add(new int2(4, 4));

            var e = EffectSpawner.SpawnBlockingHazard(_em, _so, new int2(4, 4), hazardSoIndex: 0);

            Assert.AreEqual(Entity.Null, e);
        }

        [Test]
        public void Spawn_Rejects_DefenderTile_Overlap()
        {
            var defender = _em.CreateEntity();
            _em.AddComponentData(defender, new DefenderTile { cell = new int2(4, 5) });

            var e = EffectSpawner.SpawnBlockingHazard(_em, _so, new int2(4, 4), hazardSoIndex: 0);

            Assert.AreEqual(Entity.Null, e);
        }
    }
}
