using NUnit.Framework;
using Unity.Collections;
using Unity.Core;
using Unity.Entities;
using Unity.Mathematics;
using Wassup.Battle.Effects;
using Wassup.Battle.Units;

namespace Wassup.Tests.EditMode
{
    public class ObstacleLifetimeTests
    {
        private World _world;
        private EntityManager _em;
        private SimulationSystemGroup _simGroup;
        private NativeHashSet<int2> _blockedCells;

        [SetUp]
        public void SetUp()
        {
            _world = new World("ObstacleLifetimeTestWorld");
            _em = _world.EntityManager;
            _simGroup = _world.CreateSystemManaged<SimulationSystemGroup>();
            var handle = _world.CreateSystem<ObstacleLifetimeSystem>();
            _simGroup.AddSystemToUpdateList(handle);

            _blockedCells = new NativeHashSet<int2>(16, Allocator.Persistent);
            var singleton = _em.CreateEntity();
            _em.AddComponentData(singleton, new ObstacleSingleton { blockedCells = _blockedCells });
        }

        [TearDown]
        public void TearDown()
        {
            if (_blockedCells.IsCreated) _blockedCells.Dispose();
            _world?.Dispose();
        }

        private void Tick(float dt)
        {
            _world.SetTime(new TimeData(_world.Time.ElapsedTime + dt, dt));
            _simGroup.Update();
        }

        [Test]
        public void Decrements_RemainingLife_By_DeltaTime()
        {
            var e = _em.CreateEntity();
            _em.AddComponentData(e, new Obstacle { cell = new int2(1, 1), remainingLife = 2f });

            Tick(0.5f);

            Assert.IsTrue(_em.Exists(e));
            Assert.AreEqual(1.5f, _em.GetComponentData<Obstacle>(e).remainingLife, 1e-5f);
        }

        [Test]
        public void Destroys_Expired_Obstacle()
        {
            var e = _em.CreateEntity();
            _em.AddComponentData(e, new Obstacle { cell = new int2(2, 2), remainingLife = 0.1f });

            Tick(1f);

            Assert.IsFalse(_em.Exists(e));
        }

        [Test]
        public void BlockedCells_Contains_Only_Alive_Cells()
        {
            var alive = _em.CreateEntity();
            _em.AddComponentData(alive, new Obstacle { cell = new int2(3, 0), remainingLife = 5f });
            var dead = _em.CreateEntity();
            _em.AddComponentData(dead, new Obstacle { cell = new int2(4, 0), remainingLife = 0.01f });

            Tick(1f);

            Assert.IsTrue(_blockedCells.Contains(new int2(3, 0)));
            Assert.IsFalse(_blockedCells.Contains(new int2(4, 0)));
            Assert.AreEqual(1, _blockedCells.Count);
        }

        [Test]
        public void No_Obstacles_Leaves_BlockedCells_Empty()
        {
            Tick(0.016f);
            Assert.AreEqual(0, _blockedCells.Count);
        }

        [Test]
        public void Duplicate_Cell_Deduplicates_In_HashSet()
        {
            var a = _em.CreateEntity();
            _em.AddComponentData(a, new Obstacle { cell = new int2(5, 5), remainingLife = 5f });
            var b = _em.CreateEntity();
            _em.AddComponentData(b, new Obstacle { cell = new int2(5, 5), remainingLife = 5f });

            Tick(0.016f);

            Assert.AreEqual(1, _blockedCells.Count);
        }

        [Test]
        public void BlockingHazard_Adds_All_Buffer_Cells()
        {
            var e = CreateBlockingHazard(new int2(0, 0), new int2(0, 1), new int2(1, 0));

            Tick(0.016f);

            Assert.IsTrue(_em.Exists(e));
            Assert.IsTrue(_blockedCells.Contains(new int2(0, 0)));
            Assert.IsTrue(_blockedCells.Contains(new int2(0, 1)));
            Assert.IsTrue(_blockedCells.Contains(new int2(1, 0)));
            Assert.AreEqual(3, _blockedCells.Count);
        }

        [Test]
        public void BlockingHazard_Duplicate_Cells_Deduplicate_In_HashSet()
        {
            CreateBlockingHazard(new int2(2, 2), new int2(2, 3));
            CreateBlockingHazard(new int2(2, 2), new int2(3, 2));

            Tick(0.016f);

            Assert.IsTrue(_blockedCells.Contains(new int2(2, 2)));
            Assert.IsTrue(_blockedCells.Contains(new int2(2, 3)));
            Assert.IsTrue(_blockedCells.Contains(new int2(3, 2)));
            Assert.AreEqual(3, _blockedCells.Count);
        }

        [Test]
        public void SingleObstacle_And_BlockingHazard_Union_Cells()
        {
            var obstacle = _em.CreateEntity();
            _em.AddComponentData(obstacle, new Obstacle { cell = new int2(8, 8), remainingLife = 5f });
            CreateBlockingHazard(new int2(8, 9), new int2(9, 9));

            Tick(0.016f);

            Assert.IsTrue(_blockedCells.Contains(new int2(8, 8)));
            Assert.IsTrue(_blockedCells.Contains(new int2(8, 9)));
            Assert.IsTrue(_blockedCells.Contains(new int2(9, 9)));
            Assert.AreEqual(3, _blockedCells.Count);
        }

        [Test]
        public void Dead_BlockingHazard_Is_Excluded_From_BlockedCells()
        {
            var e = CreateBlockingHazard(new int2(6, 6), new int2(6, 7));
            _em.AddComponent<DeadTag>(e);

            Tick(0.016f);

            Assert.AreEqual(0, _blockedCells.Count);
        }

        private Entity CreateBlockingHazard(params int2[] cells)
        {
            var e = _em.CreateEntity();
            _em.AddComponentData(e, new Obstacle
            {
                cell = cells.Length > 0 ? cells[0] : int2.zero,
                remainingLife = float.PositiveInfinity,
            });
            _em.AddComponentData(e, new BlockingHazard { hazardSoIndex = -1, maxHp = 10f });
            var buffer = _em.AddBuffer<BlockingHazardCellsBuffer>(e);
            for (int i = 0; i < cells.Length; i++)
                buffer.Add(new BlockingHazardCellsBuffer { cell = cells[i] });
            return e;
        }
    }
}
