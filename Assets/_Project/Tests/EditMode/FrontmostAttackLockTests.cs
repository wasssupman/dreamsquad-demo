using NUnit.Framework;
using Unity.Collections;
using Unity.Core;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using Wassup.Battle.Combat;
using Wassup.Battle.Effects;
using Wassup.Battle.Movement;
using Wassup.Battle.Units;
using Wassup.Data;

namespace Wassup.Tests.EditMode
{
    // dreamcatcher-content-2 unit 2 — AttackSystem frontmost lock lifecycle.
    // Uses a 5x1 linear flow field (goal at x=4, dist[x] = 4-x) so "nearer the goal"
    // = higher x = lower flow distance, decoupled from world distance to the attacker.
    public class FrontmostAttackLockTests
    {
        private World _world;
        private EntityManager _em;
        private SimulationSystemGroup _simGroup;
        private NativeQueue<UnitAttackVisualEvent> _attackEventQueue;
        private NativeQueue<EnemyCcEvent> _ccQueue;
        private Entity _fieldEntity;

        [SetUp]
        public void SetUp()
        {
            _world = new World("FrontmostAttackLockTests");
            _em = _world.EntityManager;
            _simGroup = _world.CreateSystemManaged<SimulationSystemGroup>();
            _simGroup.AddSystemToUpdateList(_world.CreateSystem<AttackSystem>());

            _attackEventQueue = new NativeQueue<UnitAttackVisualEvent>(Allocator.Persistent);
            _em.AddComponentData(_em.CreateEntity(), new UnitAttackVisualEventsSingleton { queue = _attackEventQueue });
            _ccQueue = new NativeQueue<EnemyCcEvent>(Allocator.Persistent);
            _em.AddComponentData(_em.CreateEntity(), new EnemyCcEventsSingleton { queue = _ccQueue });
        }

        [TearDown]
        public void TearDown()
        {
            if (_attackEventQueue.IsCreated) _attackEventQueue.Dispose();
            if (_ccQueue.IsCreated) _ccQueue.Dispose();
            if (_fieldEntity != Entity.Null && _em.Exists(_fieldEntity) && _em.HasComponent<FlowFieldSingleton>(_fieldEntity))
                _em.GetComponentData<FlowFieldSingleton>(_fieldEntity).Dispose();
            _world?.Dispose();
        }

        private void Tick(float dt = 0.016f)
        {
            _world.SetTime(new TimeData(_world.Time.ElapsedTime + dt, dt));
            _simGroup.Update();
        }

        private void CreateLinearFlowField(int width = 5, float tileSize = 1f)
        {
            int n = width;
            var flow = new NativeArray<float2>(n, Allocator.Persistent);
            var dist = new NativeArray<int>(n, Allocator.Persistent);
            for (int i = 0; i < width - 1; i++) { flow[i] = new float2(1, 0); dist[i] = (width - 1) - i; }
            flow[width - 1] = float2.zero; dist[width - 1] = 0;
            _fieldEntity = _em.CreateEntity();
            _em.AddComponentData(_fieldEntity, new FlowFieldSingleton
            {
                flow = flow, dist = dist,
                gridSize = new int2(width, 1),
                goalCell = new int2(width - 1, 0),
                tileSize = tileSize, version = 1,
            });
        }

        // A melee defender (no ProjectileRef → RESOLVE outputs path applies IncomingDamage
        // directly) carrying a FrontmostTarget mod + lock.
        private Entity CreateFrontmostDefender(float3 pos, float range, float hitDelaySec = 0f, bool withLock = true)
        {
            var e = _em.CreateEntity();
            _em.AddComponentData(e, LocalTransform.FromPosition(pos));
            _em.AddComponentData(e, new FactionTag { value = Faction.Defender });
            _em.AddComponentData(e, new Health { value = 10f, max = 10f });
            _em.AddBuffer<IncomingDamage>(e);
            _em.AddComponentData(e, new AttackState
            {
                range = range, cooldownDuration = 1f, cooldownRemaining = 0f,
                attackTargetCount = 1, targetMask = (int)Faction.Enemy, hitDelaySec = hitDelaySec,
            });
            _em.AddComponent<DefenderUnitTag>(e);
            var outputs = _em.AddBuffer<AttackOutputElement>(e);
            outputs.Add(new AttackOutputElement { value = new AttackOutput { kind = AttackOutputKind.Damage, magnitude = 5f } });
            if (withLock)
            {
                _em.AddComponentData(e, new FrontmostAttackLock { active = false, target = Entity.Null, damageMulSnapshot = 1f, targetIsPriority = false });
                var mods = _em.AddBuffer<DcAttackModSlot>(e);
                mods.Add(new DcAttackModSlot { instanceId = 1, kind = DcAttackModKind.FrontmostTarget, count = 0, tileRange = 0, damageMul = 1.2f });
            }
            return e;
        }

        private Entity CreateEnemy(float3 pos)
        {
            var e = _em.CreateEntity();
            _em.AddComponentData(e, LocalTransform.FromPosition(pos));
            _em.AddComponentData(e, new FactionTag { value = Faction.Enemy });
            _em.AddComponentData(e, new Health { value = 100f, max = 100f });
            _em.AddBuffer<IncomingDamage>(e);
            return e;
        }

        private float IncomingSum(Entity e)
        {
            var buf = _em.GetBuffer<IncomingDamage>(e);
            float s = 0f;
            for (int i = 0; i < buf.Length; i++) s += buf[i].amount;
            return s;
        }

        [Test]
        public void Frontmost_PicksLowestFlowDistance_NotNearestWorld()
        {
            CreateLinearFlowField();
            var def = CreateFrontmostDefender(new float3(0, 0, 0), range: 10f);
            var near = CreateEnemy(new float3(1, 0, 0)); // world-near, flowDist 3
            var far = CreateEnemy(new float3(3, 0, 0));  // world-far, flowDist 1 (more frontmost)
            Tick(); // hitDelay 0 → START+RESOLVE same frame
            Assert.Greater(IncomingSum(far), 0f, "frontmost (nearer the goal) should be hit");
            Assert.AreEqual(0f, IncomingSum(near), "the world-nearer but farther-along enemy is not the frontmost");
        }

        [Test]
        public void Frontmost_HeldThroughWindup_IgnoresNewCloserToGoal()
        {
            CreateLinearFlowField();
            var def = CreateFrontmostDefender(new float3(0, 0, 0), range: 10f, hitDelaySec: 0.05f);
            var locked = CreateEnemy(new float3(2, 0, 0)); // flowDist 2
            Tick(); // START: locks `locked`
            var fl = _em.GetComponentData<FrontmostAttackLock>(def);
            Assert.IsTrue(fl.active, "attack should be locked after START");
            Assert.AreEqual(locked, fl.target);
            // A new enemy appears nearer the goal mid-windup; must NOT steal the lock.
            var newer = CreateEnemy(new float3(4, 0, 0)); // flowDist 0
            for (int i = 0; i < 6; i++) Tick();
            Assert.Greater(IncomingSum(locked), 0f, "the START-locked target is the one hit");
            Assert.AreEqual(0f, IncomingSum(newer), "a closer-to-goal enemy appearing mid-windup is ignored");
        }

        [Test]
        public void Frontmost_StrictLapse_WhenLockedTargetDiesMidWindup()
        {
            CreateLinearFlowField();
            var def = CreateFrontmostDefender(new float3(0, 0, 0), range: 10f, hitDelaySec: 0.05f);
            var locked = CreateEnemy(new float3(3, 0, 0));
            var other = CreateEnemy(new float3(1, 0, 0)); // alive alternative
            Tick(); // START locks `locked`
            Assert.AreEqual(locked, _em.GetComponentData<FrontmostAttackLock>(def).target);
            // Kill the locked target mid-windup.
            _em.SetComponentData(locked, new Health { value = 0f, max = 100f });
            _em.AddComponent<DeadTag>(locked);
            for (int i = 0; i < 6; i++) Tick();
            Assert.AreEqual(0f, IncomingSum(other), "strict lapse: does NOT re-target another enemy on locked death");
            var fl = _em.GetComponentData<FrontmostAttackLock>(def);
            Assert.IsFalse(fl.active, "lock is released after the lapsed resolve");
        }

        [Test]
        public void Frontmost_PastGoalEnemy_IsExcluded()
        {
            CreateLinearFlowField();
            var def = CreateFrontmostDefender(new float3(0, 0, 0), range: 10f);
            var atGoal = CreateEnemy(new float3(4, 0, 0)); // flowDist 0 but leak-pending
            _em.AddComponent<PastGoalTag>(atGoal);
            var normal = CreateEnemy(new float3(2, 0, 0)); // flowDist 2
            Tick();
            Assert.Greater(IncomingSum(normal), 0f, "the non-PastGoal enemy is the frontmost");
            Assert.AreEqual(0f, IncomingSum(atGoal), "a PastGoal (leak-pending) enemy is excluded from frontmost");
        }

        [Test]
        public void Frontmost_FallsBackToNearest_WhenNoFlowField_NonPriority()
        {
            // No flow field → no reachable frontmost → nearest fallback, non-priority.
            var def = CreateFrontmostDefender(new float3(0, 0, 0), range: 10f);
            var near = CreateEnemy(new float3(1, 0, 0));
            var far = CreateEnemy(new float3(3, 0, 0));
            Tick();
            Assert.Greater(IncomingSum(near), 0f, "fallback targets the nearest enemy");
            var fl = _em.GetComponentData<FrontmostAttackLock>(def);
            Assert.IsFalse(fl.targetIsPriority, "a fallback (non-frontmost) pick is not a +20% priority target");
        }

        private Entity CreateFrontmostGuardian(float3 pos, float range)
        {
            var e = CreateFrontmostDefender(pos, range);
            var atk = _em.GetComponentData<AttackState>(e);
            atk.attackTargetCount = 2;
            _em.SetComponentData(e, atk);
            _em.AddComponentData(e, new Wassup.Battle.Effects.AggroCapacity { max = 2, held = 0 });
            return e;
        }

        [Test]
        public void Guardian_ForcesFrontmostPrimary_NoDoubleHit()
        {
            // ecs-review M1: a guardian (multi-target) with the card must hit the locked
            // frontmost exactly once, never twice, even if SelectTargets also ranks it.
            CreateLinearFlowField();
            var def = CreateFrontmostGuardian(new float3(0, 0, 0), range: 10f);
            var frontmost = CreateEnemy(new float3(3, 0, 0)); // flowDist 1 → forced primary
            var other = CreateEnemy(new float3(1, 0, 0));     // flowDist 3 → secondary
            Tick();
            Assert.AreEqual(1, _em.GetBuffer<IncomingDamage>(frontmost).Length,
                "the locked frontmost is hit exactly once (no primary+secondary duplicate)");
            Assert.Greater(IncomingSum(other), 0f, "the other in-range enemy still fills a secondary slot");
        }

        [Test]
        public void Frontmost_LapsesWhenLockedTargetBecomesPastGoal_MidWindup()
        {
            CreateLinearFlowField();
            var def = CreateFrontmostDefender(new float3(0, 0, 0), range: 10f, hitDelaySec: 0.05f);
            var locked = CreateEnemy(new float3(3, 0, 0));
            Tick(); // START locks `locked`
            Assert.AreEqual(locked, _em.GetComponentData<FrontmostAttackLock>(def).target);
            _em.AddComponent<PastGoalTag>(locked); // reaches the goal mid-windup
            for (int i = 0; i < 6; i++) Tick();
            Assert.AreEqual(0f, IncomingSum(locked), "a locked target that becomes PastGoal lapses (no hit)");
            Assert.IsFalse(_em.GetComponentData<FrontmostAttackLock>(def).active, "lock released after lapse");
        }

        [Test]
        public void NoCard_UsesNearest_NoRegression()
        {
            CreateLinearFlowField();
            var def = CreateFrontmostDefender(new float3(0, 0, 0), range: 10f, withLock: false);
            var near = CreateEnemy(new float3(1, 0, 0)); // flowDist 3
            var far = CreateEnemy(new float3(3, 0, 0));  // flowDist 1
            Tick();
            Assert.Greater(IncomingSum(near), 0f, "without the card, plain nearest targeting is unchanged");
            Assert.AreEqual(0f, IncomingSum(far), "the far (but nearer-goal) enemy is not chosen without the card");
        }
    }
}
