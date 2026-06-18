using NUnit.Framework;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using Wassup.Battle.Combat;
using Wassup.Battle.Movement;
using Wassup.Battle.Units;
using Wassup.Data;

namespace Wassup.Tests.EditMode
{
    // enemy-behavior-components Unit 4 — AttackSystem behavior consumption:
    // FocusUntilDead lock/hold/reselect/range-gating + aimMode move-pause gating.
    public class EnemyBehaviorTests
    {
        private World _world;
        private EntityManager _em;
        private SimulationSystemGroup _simGroup;
        private NativeQueue<MovementPauseRequest> _pauseQueue;

        [SetUp]
        public void SetUp()
        {
            _world = new World("EnemyBehaviorTestWorld");
            _em = _world.EntityManager;
            _simGroup = _world.CreateSystemManaged<SimulationSystemGroup>();
            _simGroup.AddSystemToUpdateList(_world.CreateSystem<AttackSystem>());

            _pauseQueue = new NativeQueue<MovementPauseRequest>(Allocator.Persistent);
            var s = _em.CreateEntity();
            _em.AddComponentData(s, new MovementPauseRequestEventsSingleton { queue = _pauseQueue });
        }

        [TearDown]
        public void TearDown()
        {
            if (_world != null && _world.IsCreated) _world.Dispose();
            if (_pauseQueue.IsCreated) _pauseQueue.Dispose();
        }

        private Entity MakeDefender(float x, DefenderClass cls = DefenderClass.Guardian)
        {
            var e = _em.CreateEntity();
            _em.AddComponentData(e, new Health { value = 100000f, max = 100000f });
            _em.AddComponentData(e, new FactionTag { value = Faction.Defender });
            _em.AddComponentData(e, LocalTransform.FromPosition(new float3(x, 0, 0)));
            _em.AddComponentData(e, new DefenderClassTag { value = cls });
            _em.AddBuffer<IncomingDamage>(e);
            return e;
        }

        private Entity MakeEnemy(float x, EnemyTargetMode targetMode, EnemyAimMode aimMode, float movePause = 0.5f)
        {
            var e = _em.CreateEntity();
            _em.AddComponentData(e, new Health { value = 100000f, max = 100000f });
            _em.AddComponentData(e, new FactionTag { value = Faction.Enemy });
            _em.AddComponentData(e, LocalTransform.FromPosition(new float3(x, 0, 0)));
            _em.AddComponentData(e, new AttackState
            {
                damage = 10f, range = 5f, cooldownDuration = 1f, cooldownRemaining = 0f,
                attackTargetCount = 1, targetMask = (int)Faction.Defender, movePauseOnAttackSec = movePause,
            });
            var ob = _em.AddBuffer<AttackOutputElement>(e);
            ob.Add(new AttackOutputElement { value = new AttackOutput { kind = AttackOutputKind.Damage, magnitude = 10f } });
            _em.AddComponentData(e, new EnemyBehavior { targetMode = targetMode, aimMode = aimMode });
            _em.AddComponentData(e, new EnemyTargetFilter { classMask = -1, priorityClass = -1 });
            if (targetMode == EnemyTargetMode.FocusUntilDead)
                _em.AddComponentData(e, new FocusTarget { current = Entity.Null });
            return e;
        }

        private Entity Locked(Entity enemy) => _em.GetComponentData<FocusTarget>(enemy).current;
        private int Incoming(Entity e) => _em.GetBuffer<IncomingDamage>(e).Length;

        // EditMode world time does not advance, so cooldown never elapses between
        // manual updates. Reset it so a second update can fire.
        private void ReadyAttack(Entity e)
        {
            var st = _em.GetComponentData<AttackState>(e);
            st.cooldownRemaining = 0f;
            _em.SetComponentData(e, st);
        }

        [Test]
        public void FocusUntilDead_Locks_Holds_ThenReselectsOnDeath()
        {
            var f = MakeEnemy(0f, EnemyTargetMode.FocusUntilDead, EnemyAimMode.StopToAttack);
            var a = MakeDefender(1f);   // nearest
            MakeDefender(3f);           // farther
            _simGroup.Update();
            Assert.AreEqual(a, Locked(f), "locks nearest first");

            MakeDefender(0.5f); // closer than A appears
            _simGroup.Update();
            Assert.AreEqual(a, Locked(f), "lock held even though a closer target appeared");

            _em.DestroyEntity(a); // A dies → reselect
            _simGroup.Update();
            Assert.AreNotEqual(Entity.Null, Locked(f), "reselects after locked target dies");
            Assert.AreNotEqual(a, Locked(f));
        }

        [Test]
        public void FocusUntilDead_OutOfRange_HoldsFire_KeepsLock()
        {
            var f = MakeEnemy(0f, EnemyTargetMode.FocusUntilDead, EnemyAimMode.StopToAttack);
            var a = MakeDefender(1f);
            _simGroup.Update();
            Assert.AreEqual(a, Locked(f));

            _em.GetBuffer<IncomingDamage>(a).Clear();
            _em.SetComponentData(a, LocalTransform.FromPosition(new float3(50f, 0, 0))); // out of range
            ReadyAttack(f); // ensure no-fire is due to range, not cooldown
            _simGroup.Update();
            Assert.AreEqual(a, Locked(f), "lock kept while out of range");
            Assert.AreEqual(0, Incoming(a), "no fire while locked target out of range");
        }

        [Test]
        public void Nearest_RepicksClosest_UnlikeFocus()
        {
            var n = MakeEnemy(0f, EnemyTargetMode.Nearest, EnemyAimMode.StopToAttack);
            var a = MakeDefender(2f);
            _simGroup.Update();
            Assert.Greater(Incoming(a), 0, "nearest hits A");

            var c = MakeDefender(0.5f); // closer
            _em.GetBuffer<IncomingDamage>(a).Clear();
            ReadyAttack(n);
            _simGroup.Update();
            Assert.Greater(Incoming(c), 0, "nearest re-picks the now-closer C");
            Assert.AreEqual(0, Incoming(a), "old target no longer hit");
        }

        [Test]
        public void AimMode_StopToAttack_EnqueuesPause()
        {
            MakeEnemy(0f, EnemyTargetMode.Nearest, EnemyAimMode.StopToAttack);
            MakeDefender(1f);
            _simGroup.Update();
            Assert.AreEqual(1, _pauseQueue.Count, "StopToAttack enqueues a move-pause on fire");
        }

        [Test]
        public void AimMode_MoveAndShoot_NoPause()
        {
            MakeEnemy(0f, EnemyTargetMode.Nearest, EnemyAimMode.MoveAndShoot);
            MakeDefender(1f);
            _simGroup.Update();
            Assert.AreEqual(0, _pauseQueue.Count, "MoveAndShoot does not pause");
        }
    }
}
