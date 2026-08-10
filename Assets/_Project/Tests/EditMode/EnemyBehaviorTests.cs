using NUnit.Framework;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using Wassup.Battle.Combat;
using Wassup.Battle.Units;
using Wassup.Data;

namespace Wassup.Tests.EditMode
{
    // enemy-behavior-components Unit 4 — AttackSystem behavior consumption:
    // FocusUntilDead lock/hold/reselect/range-gating.
    public class EnemyBehaviorTests
    {
        private World _world;
        private EntityManager _em;
        private SimulationSystemGroup _simGroup;

        [SetUp]
        public void SetUp()
        {
            _world = new World("EnemyBehaviorTestWorld");
            _em = _world.EntityManager;
            _simGroup = _world.CreateSystemManaged<SimulationSystemGroup>();
            _simGroup.AddSystemToUpdateList(_world.CreateSystem<AttackSystem>());
        }

        [TearDown]
        public void TearDown()
        {
            if (_world != null && _world.IsCreated) _world.Dispose();
        }

        private Entity MakeDefender(float x, DefenderClass cls = DefenderClass.Guardian)
        {
            var e = _em.CreateEntity();
            _em.AddComponentData(e, new Health { value = 100000f, max = 100000f });
            _em.AddComponentData(e, new FactionTag { value = Faction.DefenderUnit });
            _em.AddComponentData(e, LocalTransform.FromPosition(new float3(x, 0, 0)));
            _em.AddComponentData(e, new DefenderClassTag { value = cls });
            _em.AddBuffer<IncomingDamage>(e);
            return e;
        }

        private Entity MakeEnemy(float x, EnemyTargetMode targetMode)
        {
            var e = _em.CreateEntity();
            _em.AddComponentData(e, new Health { value = 100000f, max = 100000f });
            _em.AddComponentData(e, new FactionTag { value = Faction.EnemyUnit });
            _em.AddComponentData(e, LocalTransform.FromPosition(new float3(x, 0, 0)));
            _em.AddComponentData(e, new AttackState
            {
                range = 5f, cooldownDuration = 1f, cooldownRemaining = 0f,
                attackTargetCount = 1, targetMask = (int)Faction.DefenderUnit,
            });
            var ob = _em.AddBuffer<AttackOutputElement>(e);
            ob.Add(new AttackOutputElement { value = new AttackOutput { kind = AttackOutputKind.Damage, magnitude = 10f } });
            _em.AddComponentData(e, new EnemyBehavior { targetMode = targetMode });
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
            var f = MakeEnemy(0f, EnemyTargetMode.FocusUntilDead);
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

        // target-persistence unit 2 (D2) — **계약이 뒤집혔다.**
        // 예전 이름은 `..._HoldsFire_KeepsLock` 이었고 "이탈해도 락 유지"를 고정했다.
        // 그 계약이 B2 의 원인이다 — 락을 붙든 적이 사거리 안의 다른 디펜더를 영원히 무시했다.
        // 이제 이탈은 해제 사유다. 여기선 대체 후보가 없으므로 락이 비고 발사도 없다.
        [Test]
        public void FocusUntilDead_OutOfRange_ReleasesLock_NoFireWhenNoOtherTarget()
        {
            var f = MakeEnemy(0f, EnemyTargetMode.FocusUntilDead);
            var a = MakeDefender(1f);
            _simGroup.Update();
            Assert.AreEqual(a, Locked(f));

            _em.GetBuffer<IncomingDamage>(a).Clear();
            _em.SetComponentData(a, LocalTransform.FromPosition(new float3(50f, 0, 0))); // out of range
            ReadyAttack(f); // ensure no-fire is due to range, not cooldown
            _simGroup.Update();
            Assert.AreEqual(Entity.Null, Locked(f), "이탈 = 해제 (D2)");
            Assert.AreEqual(0, Incoming(a), "사거리 밖 대상은 여전히 안 맞는다");
        }

        [Test]
        public void Nearest_RepicksClosest_UnlikeFocus()
        {
            var n = MakeEnemy(0f, EnemyTargetMode.Nearest);
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

    }
}
