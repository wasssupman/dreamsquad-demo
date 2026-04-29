using NUnit.Framework;
using Unity.Collections;
using Unity.Core;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using Wassup.Battle.Combat;
using Wassup.Battle.Combat.Projectile;
using Wassup.Battle.Effects;
using Wassup.Battle.Units;

namespace Wassup.Tests.EditMode
{
    // Regression gate for the unified attacker loop (spec: attack-system-loop-unify Unit 0).
    // Covers EditMode equivalents of PlayMode scenarios U1–U8, plus three additional guards
    // (self-exclusion, PendingDeployment exclusion, DeadTag exclusion).
    public class AttackSystemUnifiedLoopTests
    {
        private World _world;
        private EntityManager _em;
        private SimulationSystemGroup _simGroup;
        private NativeQueue<DefenderAttackEvent> _attackEventQueue;
        private NativeQueue<EnemyCcEvent> _ccQueue;

        [SetUp]
        public void SetUp()
        {
            _world = new World("AttackSystemUnifiedLoopTests");
            _em = _world.EntityManager;
            _simGroup = _world.CreateSystemManaged<SimulationSystemGroup>();
            _simGroup.AddSystemToUpdateList(_world.CreateSystem<AttackSystem>());

            // Singleton: defender attack events (Spine animation trigger)
            _attackEventQueue = new NativeQueue<DefenderAttackEvent>(Allocator.Persistent);
            var attackSingleton = _em.CreateEntity();
            _em.AddComponentData(attackSingleton, new DefenderAttackEventsSingleton { queue = _attackEventQueue });

            // Singleton: enemy CC events (knockback)
            _ccQueue = new NativeQueue<EnemyCcEvent>(Allocator.Persistent);
            var ccSingleton = _em.CreateEntity();
            _em.AddComponentData(ccSingleton, new EnemyCcEventsSingleton { queue = _ccQueue });
        }

        [TearDown]
        public void TearDown()
        {
            if (_attackEventQueue.IsCreated) _attackEventQueue.Dispose();
            if (_ccQueue.IsCreated) _ccQueue.Dispose();
            _world?.Dispose();
        }

        private void Tick(float dt = 0.016f)
        {
            _world.SetTime(new TimeData(_world.Time.ElapsedTime + dt, dt));
            _simGroup.Update();
        }

        // Creates an attacker entity with the given faction, position, and AttackState.
        private Entity CreateAttacker(
            Faction faction,
            float3 position,
            float damage,
            float range,
            float cooldownDuration,
            int targetMask,
            int attackTargetCount = 1,
            bool defenderTag = false,
            bool attackerTag = false)
        {
            var e = _em.CreateEntity();
            _em.AddComponentData(e, LocalTransform.FromPosition(position));
            _em.AddComponentData(e, new FactionTag { value = faction });
            _em.AddComponentData(e, new Health { value = 10f, max = 10f });
            _em.AddBuffer<IncomingDamage>(e);
            _em.AddComponentData(e, new AttackState
            {
                damage = damage,
                range = range,
                cooldownDuration = cooldownDuration,
                cooldownRemaining = 0f,
                attackTargetCount = attackTargetCount,
                targetMask = targetMask,
            });
            if (defenderTag) _em.AddComponent<DefenderUnitTag>(e);
            if (attackerTag) _em.AddComponent<AttackUnitTag>(e);
            return e;
        }

        // Creates a pure target entity (no AttackState).
        private Entity CreateTarget(
            Faction faction,
            float3 position,
            bool defenderTag = false,
            bool attackerTag = false,
            bool pendingDeployment = false,
            bool deadTag = false)
        {
            var e = _em.CreateEntity();
            _em.AddComponentData(e, LocalTransform.FromPosition(position));
            _em.AddComponentData(e, new FactionTag { value = faction });
            _em.AddComponentData(e, new Health { value = 10f, max = 10f });
            _em.AddBuffer<IncomingDamage>(e);
            if (defenderTag) _em.AddComponent<DefenderUnitTag>(e);
            if (attackerTag) _em.AddComponent<AttackUnitTag>(e);
            if (pendingDeployment) _em.AddComponent<PendingDeployment>(e);
            if (deadTag) _em.AddComponent<DeadTag>(e);
            return e;
        }

        // ─── U1: Defender with ProjectileRef → produces ProjectileSpawnRequest, no direct damage ───

        [Test]
        public void U1_Defender_ProjectileRef_Produces_SpawnRequest_Not_Direct_Damage()
        {
            var defender = CreateAttacker(
                Faction.Defender, new float3(0f, 0f, 0f),
                damage: 5f, range: 10f, cooldownDuration: 1f,
                targetMask: (int)Faction.Enemy,
                defenderTag: true);

            _em.AddComponentData(defender, new ProjectileRef
            {
                speed = 10f,
                hitThreshold = 0.3f,
                visualScale = 1f,
                dataIndex = 0,
                splashRadius = 0f,
                splashDamageMul = 1f,
            });

            var enemy = CreateTarget(Faction.Enemy, new float3(2f, 0f, 0f), attackerTag: true);

            Tick();

            Assert.IsTrue(_em.HasComponent<ProjectileSpawnRequest>(defender),
                "Defender with ProjectileRef should have ProjectileSpawnRequest added");
            Assert.AreEqual(0, _em.GetBuffer<IncomingDamage>(enemy).Length,
                "No direct IncomingDamage when projectile path is taken");
        }

        // ─── U2: Defender melee AoE (attackTargetCount=2) hits two enemies ───

        [Test]
        public void U2_Defender_Melee_AoE_Hits_Two_Targets()
        {
            var defender = CreateAttacker(
                Faction.Defender, new float3(0f, 0f, 0f),
                damage: 4f, range: 10f, cooldownDuration: 1f,
                targetMask: (int)Faction.Enemy,
                attackTargetCount: 2,
                defenderTag: true);

            var enemy1 = CreateTarget(Faction.Enemy, new float3(1f, 0f, 0f), attackerTag: true);
            var enemy2 = CreateTarget(Faction.Enemy, new float3(2f, 0f, 0f), attackerTag: true);

            Tick();

            Assert.AreEqual(1, _em.GetBuffer<IncomingDamage>(enemy1).Length, "Enemy1 should be hit");
            Assert.AreEqual(4f, _em.GetBuffer<IncomingDamage>(enemy1)[0].amount, 1e-4f);
            Assert.AreEqual(1, _em.GetBuffer<IncomingDamage>(enemy2).Length, "Enemy2 should be hit by AoE");
            Assert.AreEqual(4f, _em.GetBuffer<IncomingDamage>(enemy2)[0].amount, 1e-4f);
        }

        // ─── U3: Defender with DefenderCcData enqueues EnemyCcEvent (knockback) ───

        [Test]
        public void U3_Defender_DefenderCcData_Enqueues_Knockback_CC()
        {
            var defender = CreateAttacker(
                Faction.Defender, new float3(0f, 0f, 0f),
                damage: 3f, range: 10f, cooldownDuration: 1f,
                targetMask: (int)Faction.Enemy,
                defenderTag: true);

            _em.AddComponentData(defender, new DefenderCcData
            {
                knockbackDistance = 2f,
                knockbackDuration = 0.5f,
            });

            var enemy = CreateTarget(Faction.Enemy, new float3(3f, 0f, 0f), attackerTag: true);

            Tick();

            Assert.AreEqual(1, _ccQueue.Count, "One knockback CC event should be enqueued");
            var ev = _ccQueue.Dequeue();
            Assert.AreEqual(enemy, ev.target);
            Assert.AreEqual(CcKind.Impulse, ev.effect.kind);
        }

        // ─── U4: Defender with DamageBoost + SynergyBuff + CooldownReduction applies multipliers ───

        [Test]
        public void U4_Defender_Buff_Multipliers_Applied_To_Damage_And_Cooldown()
        {
            var defender = CreateAttacker(
                Faction.Defender, new float3(0f, 0f, 0f),
                damage: 10f, range: 10f, cooldownDuration: 2f,
                targetMask: (int)Faction.Enemy,
                defenderTag: true);

            _em.AddComponentData(defender, new DamageBoost { multiplier = 1.5f, remaining = 5f });
            _em.AddComponentData(defender, new SynergyBuff { damageMul = 2f });
            _em.AddComponentData(defender, new CooldownReduction { multiplier = 0.5f, remaining = 5f });

            var enemy = CreateTarget(Faction.Enemy, new float3(1f, 0f, 0f), attackerTag: true);

            Tick();

            // damage = 10 * 1.5 (DamageBoost) * 2.0 (Synergy) = 30
            var dmgBuf = _em.GetBuffer<IncomingDamage>(enemy);
            Assert.AreEqual(1, dmgBuf.Length);
            Assert.AreEqual(30f, dmgBuf[0].amount, 1e-4f, "DamageBoost * SynergyBuff should multiply base damage");

            // cooldown reset = cooldownDuration * cooldownMul = 2.0 * 0.5 = 1.0
            var atkState = _em.GetComponentData<AttackState>(defender);
            Assert.AreEqual(1f, atkState.cooldownRemaining, 1e-4f, "CooldownReduction should halve reset interval");
        }

        // ─── U5: Enemy (AttackUnitTag) attacks defender → direct IncomingDamage, no projectile/CC ───

        [Test]
        public void U5_Enemy_Attacks_Defender_Direct_Damage_No_Event()
        {
            var enemy = CreateAttacker(
                Faction.Enemy, new float3(0f, 0f, 0f),
                damage: 6f, range: 10f, cooldownDuration: 1f,
                targetMask: (int)(Faction.Defender | Faction.BlockingHazard),
                attackerTag: true);

            var defender = CreateTarget(Faction.Defender, new float3(2f, 0f, 0f), defenderTag: true);

            Tick();

            var dmgBuf = _em.GetBuffer<IncomingDamage>(defender);
            Assert.AreEqual(1, dmgBuf.Length, "Defender should receive direct IncomingDamage from enemy");
            Assert.AreEqual(6f, dmgBuf[0].amount, 1e-4f);

            Assert.IsFalse(_em.HasComponent<ProjectileSpawnRequest>(enemy),
                "Enemy should not generate ProjectileSpawnRequest");
        }

        // ─── U6: Enemy attacks blocking hazard → hazard receives IncomingDamage ───

        [Test]
        public void U6_Enemy_Attacks_Hazard_Damages_Hazard()
        {
            var enemy = CreateAttacker(
                Faction.Enemy, new float3(0f, 0f, 0f),
                damage: 4f, range: 10f, cooldownDuration: 1f,
                targetMask: (int)(Faction.Defender | Faction.BlockingHazard),
                attackerTag: true);

            // Hazard is closer so it should be preferred target
            var hazard = CreateTarget(Faction.BlockingHazard, new float3(1f, 0f, 0f));
            var defender = CreateTarget(Faction.Defender, new float3(5f, 0f, 0f), defenderTag: true);

            Tick();

            var hazardDmg = _em.GetBuffer<IncomingDamage>(hazard);
            Assert.AreEqual(1, hazardDmg.Length, "Hazard should receive IncomingDamage");
            Assert.AreEqual(4f, hazardDmg[0].amount, 1e-4f);
            Assert.AreEqual(0, _em.GetBuffer<IncomingDamage>(defender).Length,
                "Farther defender should not be targeted");
        }

        // ─── U7: Defender fire enqueues exactly one DefenderAttackEvent ───

        [Test]
        public void U7_Defender_Fire_Enqueues_DefenderAttackEvent()
        {
            var defender = CreateAttacker(
                Faction.Defender, new float3(0f, 0f, 0f),
                damage: 5f, range: 10f, cooldownDuration: 1f,
                targetMask: (int)Faction.Enemy,
                defenderTag: true);

            var enemy = CreateTarget(Faction.Enemy, new float3(2f, 0f, 0f), attackerTag: true);

            Tick();

            Assert.AreEqual(1, _attackEventQueue.Count,
                "Exactly one DefenderAttackEvent should be enqueued when defender fires");
            var ev = _attackEventQueue.Dequeue();
            Assert.AreEqual(defender, ev.defender);
        }

        // ─── U8: Enemy fire does NOT enqueue DefenderAttackEvent ───

        [Test]
        public void U8_Enemy_Fire_Does_Not_Enqueue_DefenderAttackEvent()
        {
            var enemy = CreateAttacker(
                Faction.Enemy, new float3(0f, 0f, 0f),
                damage: 5f, range: 10f, cooldownDuration: 1f,
                targetMask: (int)(Faction.Defender | Faction.BlockingHazard),
                attackerTag: true);

            var defender = CreateTarget(Faction.Defender, new float3(2f, 0f, 0f), defenderTag: true);

            Tick();

            Assert.AreEqual(0, _attackEventQueue.Count,
                "Enemies must not enqueue DefenderAttackEvent (Spine animation is defender-only)");
        }

        // ─── Additional: Self-exclusion guard — attacker does not target itself ───

        [Test]
        public void SelfExclusion_Attacker_Does_Not_Target_Itself()
        {
            // Defender with both DefenderUnitTag and EnemyFaction mask — unusual but
            // tests the self-exclusion guard explicitly.
            var defender = CreateAttacker(
                Faction.Defender, new float3(0f, 0f, 0f),
                damage: 5f, range: 10f, cooldownDuration: 1f,
                targetMask: (int)Faction.Defender,   // would hit itself if no guard
                defenderTag: true);
            _em.AddBuffer<IncomingDamage>(defender); // already added by CreateAttacker

            Tick();

            // No valid target other than itself → no fire
            var selfDmg = _em.GetBuffer<IncomingDamage>(defender);
            Assert.AreEqual(0, selfDmg.Length,
                "Attacker must never select itself as target");
        }

        // ─── Additional: PendingDeployment excludes entity from attacker query ───

        [Test]
        public void PendingDeployment_Excludes_Attacker_From_Loop()
        {
            // Defender with PendingDeployment should not fire even if in range
            var pendingDefender = CreateAttacker(
                Faction.Defender, new float3(0f, 0f, 0f),
                damage: 5f, range: 10f, cooldownDuration: 1f,
                targetMask: (int)Faction.Enemy,
                defenderTag: true);
            _em.AddComponent<PendingDeployment>(pendingDefender);

            var enemy = CreateTarget(Faction.Enemy, new float3(1f, 0f, 0f), attackerTag: true);

            Tick();

            Assert.AreEqual(0, _em.GetBuffer<IncomingDamage>(enemy).Length,
                "Attacker with PendingDeployment must not fire");
            Assert.AreEqual(0, _attackEventQueue.Count,
                "No attack event when PendingDeployment attacker is excluded");
        }

        // ─── Additional: DeadTag excludes entity from target pool ───

        [Test]
        public void DeadTag_Excludes_Target_From_Pool()
        {
            var defender = CreateAttacker(
                Faction.Defender, new float3(0f, 0f, 0f),
                damage: 5f, range: 10f, cooldownDuration: 1f,
                targetMask: (int)Faction.Enemy,
                defenderTag: true);

            // Only available target has DeadTag → should not be selected
            var deadEnemy = CreateTarget(Faction.Enemy, new float3(1f, 0f, 0f), attackerTag: true, deadTag: true);

            Tick();

            Assert.AreEqual(0, _em.GetBuffer<IncomingDamage>(deadEnemy).Length,
                "DeadTag entity must be excluded from target pool");
            Assert.AreEqual(0, _attackEventQueue.Count,
                "No attack event when no valid targets exist");
        }
    }
}
