using NUnit.Framework;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using Wassup.Battle.Combat;
using Wassup.Battle.Units;
using Wassup.Data;

namespace Wassup.Tests.EditMode
{
    // aggro-targeting Unit 8 — regression for EnemyTargetFilter class priority in
    // AttackSystem (Shooter prioritizes Ranger over a closer non-priority target).
    public class EnemyTargetPriorityTests
    {
        private World _world;
        private EntityManager _em;
        private SimulationSystemGroup _simGroup;

        [SetUp]
        public void SetUp()
        {
            _world = new World("EnemyTargetPriorityTestWorld");
            _em = _world.EntityManager;
            _simGroup = _world.CreateSystemManaged<SimulationSystemGroup>();
            _simGroup.AddSystemToUpdateList(_world.CreateSystem<AttackSystem>());
        }

        [TearDown]
        public void TearDown()
        {
            if (_world != null && _world.IsCreated) _world.Dispose();
        }

        private Entity MakeShooter(float3 pos, int priorityClass)
        {
            var e = _em.CreateEntity();
            _em.AddComponentData(e, new Health { value = 1000f, max = 1000f });
            _em.AddComponentData(e, new FactionTag { value = Faction.EnemyUnit });
            _em.AddComponentData(e, LocalTransform.FromPosition(pos));
            _em.AddComponentData(e, new AttackState
            {
                range = 10f, cooldownDuration = 1f, cooldownRemaining = 0f,
                attackTargetCount = 1, targetMask = (int)Faction.DefenderUnit,
            });
            var ob = _em.AddBuffer<AttackOutputElement>(e);
            ob.Add(new AttackOutputElement { value = new AttackOutput { kind = AttackOutputKind.Damage, magnitude = 10f } });
            _em.AddComponentData(e, new EnemyTargetFilter { classMask = -1, priorityClass = priorityClass });
            return e;
        }

        private Entity MakeDefender(float3 pos, DefenderClass cls)
        {
            var e = _em.CreateEntity();
            _em.AddComponentData(e, new Health { value = 1000f, max = 1000f });
            _em.AddComponentData(e, new FactionTag { value = Faction.DefenderUnit });
            _em.AddComponentData(e, LocalTransform.FromPosition(pos));
            _em.AddComponentData(e, new DefenderClassTag { value = cls });
            _em.AddBuffer<IncomingDamage>(e);
            return e;
        }

        private static int IncomingCount(EntityManager em, Entity e) => em.GetBuffer<IncomingDamage>(e).Length;

        [Test]
        public void Shooter_PrioritizesRanger_OverCloserGuardian()
        {
            MakeShooter(float3.zero, priorityClass: (int)DefenderClass.Ranger);
            var guardian = MakeDefender(new float3(1f, 0, 0), DefenderClass.Guardian); // closer
            var ranger = MakeDefender(new float3(3f, 0, 0), DefenderClass.Ranger);     // farther, priority

            _simGroup.Update();

            Assert.Greater(IncomingCount(_em, ranger), 0, "priority Ranger is targeted");
            Assert.AreEqual(0, IncomingCount(_em, guardian), "closer non-priority guardian is skipped");
        }

        [Test]
        public void NoPriority_PicksNearest()
        {
            MakeShooter(float3.zero, priorityClass: -1);
            var guardian = MakeDefender(new float3(1f, 0, 0), DefenderClass.Guardian); // closer
            var ranger = MakeDefender(new float3(3f, 0, 0), DefenderClass.Ranger);     // farther

            _simGroup.Update();

            Assert.Greater(IncomingCount(_em, guardian), 0, "nearest target chosen when no priority");
            Assert.AreEqual(0, IncomingCount(_em, ranger), "farther target skipped");
        }
    }
}
