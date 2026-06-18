using NUnit.Framework;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using Wassup.Battle.Combat;
using Wassup.Battle.Effects;
using Wassup.Battle.Units;
using Wassup.Data;

namespace Wassup.Tests.EditMode
{
    // aggro-targeting Unit 6 — regression coverage for AggroAssignmentSystem
    // (capacity cap, preemption, release on guardian death, reassign, taunt grant/strip).
    public class AggroAssignmentTests
    {
        private World _world;
        private EntityManager _em;
        private SimulationSystemGroup _simGroup;

        [SetUp]
        public void SetUp()
        {
            _world = new World("AggroTestWorld");
            _em = _world.EntityManager;
            _simGroup = _world.CreateSystemManaged<SimulationSystemGroup>();
            _simGroup.AddSystemToUpdateList(_world.CreateSystem<AggroAssignmentSystem>());
        }

        [TearDown]
        public void TearDown()
        {
            if (_world != null && _world.IsCreated) _world.Dispose();
        }

        private Entity MakeGuardian(int capacity, float range, float3 pos)
        {
            var e = _em.CreateEntity();
            _em.AddComponentData(e, new Health { value = 100f, max = 100f });
            _em.AddComponentData(e, new FactionTag { value = Faction.Defender });
            _em.AddComponentData(e, LocalTransform.FromPosition(pos));
            _em.AddComponentData(e, new AggroProvider { capacity = capacity, range = range });
            return e;
        }

        private Entity MakeEnemy(float3 pos)
        {
            var e = _em.CreateEntity();
            _em.AddComponentData(e, new Health { value = 100f, max = 100f });
            _em.AddComponentData(e, new FactionTag { value = Faction.Enemy });
            _em.AddComponentData(e, LocalTransform.FromPosition(pos));
            return e;
        }

        private int AggroedCount()
        {
            using var q = _em.CreateEntityQuery(ComponentType.ReadOnly<Aggroed>());
            return q.CalculateEntityCount();
        }

        [Test]
        public void CapacityCap_AggrosUpToCapacity_AndIgnoresOutOfRange()
        {
            MakeGuardian(capacity: 2, range: 5f, pos: float3.zero);
            MakeEnemy(new float3(0.1f, 0, 0));
            MakeEnemy(new float3(0.2f, 0, 0));
            MakeEnemy(new float3(0.3f, 0, 0));
            var far = MakeEnemy(new float3(50f, 0, 0)); // out of range

            _simGroup.Update();

            Assert.AreEqual(2, AggroedCount(), "should aggro exactly capacity (2)");
            Assert.IsFalse(_em.HasComponent<Aggroed>(far), "out-of-range enemy must not be aggroed");
        }

        [Test]
        public void Preemption_FirstGuardianKeepsEnemy()
        {
            var g1 = MakeGuardian(2, 5f, float3.zero);
            var enemy = MakeEnemy(new float3(0.2f, 0, 0));
            _simGroup.Update();
            Assert.IsTrue(_em.HasComponent<Aggroed>(enemy));
            var first = _em.GetComponentData<Aggroed>(enemy).guardian;
            Assert.AreEqual(g1, first);

            // second guardian appears on the same enemy — must not steal it.
            MakeGuardian(2, 5f, new float3(0.2f, 0, 0));
            _simGroup.Update();
            Assert.AreEqual(first, _em.GetComponentData<Aggroed>(enemy).guardian, "preempted enemy keeps first guardian");
        }

        [Test]
        public void GuardianDeath_ReleasesAggro()
        {
            var g = MakeGuardian(2, 5f, float3.zero);
            var enemy = MakeEnemy(new float3(0.2f, 0, 0));
            _simGroup.Update();
            Assert.IsTrue(_em.HasComponent<Aggroed>(enemy));

            _em.SetComponentData(g, new Health { value = 0f, max = 100f });
            _simGroup.Update();
            Assert.IsFalse(_em.HasComponent<Aggroed>(enemy), "guardian death releases aggro");
        }

        [Test]
        public void ReleaseThenReassign_ToOtherGuardian()
        {
            var g1 = MakeGuardian(1, 5f, float3.zero);
            var g2 = MakeGuardian(1, 5f, new float3(0.3f, 0, 0));
            var enemy = MakeEnemy(new float3(0.2f, 0, 0));
            _simGroup.Update();
            var holder = _em.GetComponentData<Aggroed>(enemy).guardian;
            var other = holder == g1 ? g2 : g1;

            _em.SetComponentData(holder, new Health { value = 0f, max = 100f });
            _simGroup.Update(); // release
            _simGroup.Update(); // reassign
            Assert.IsTrue(_em.HasComponent<Aggroed>(enemy), "released enemy is reacquired");
            Assert.AreEqual(other, _em.GetComponentData<Aggroed>(enemy).guardian, "reassigned to the surviving guardian");
        }

        [Test]
        public void TauntAttack_GrantedOnAggro_StrippedOnRelease()
        {
            var g = MakeGuardian(2, 5f, float3.zero);
            var runner = MakeEnemy(new float3(0.2f, 0, 0)); // no AttackState/outputs
            _em.AddComponentData(runner, new AggroAttackProfile { damage = 5f, cooldown = 1f, range = 1f });

            _simGroup.Update();
            Assert.IsTrue(_em.HasComponent<Aggroed>(runner));
            Assert.IsTrue(_em.HasComponent<AttackState>(runner), "taunt attack granted");
            Assert.IsTrue(_em.HasComponent<TauntAttackGranted>(runner));
            Assert.IsTrue(_em.HasBuffer<AttackOutputElement>(runner), "taunt outputs granted");

            _em.SetComponentData(g, new Health { value = 0f, max = 100f });
            _simGroup.Update();
            Assert.IsFalse(_em.HasComponent<Aggroed>(runner));
            Assert.IsFalse(_em.HasComponent<AttackState>(runner), "taunt attack stripped on release");
            Assert.IsFalse(_em.HasComponent<TauntAttackGranted>(runner));
        }
    }
}
