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
using Wassup.Data;

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
        private NativeQueue<UnitAttackVisualEvent> _attackEventQueue;
        private NativeQueue<EnemyCcEvent> _ccQueue;

        [SetUp]
        public void SetUp()
        {
            _world = new World("AttackSystemUnifiedLoopTests");
            _em = _world.EntityManager;
            _simGroup = _world.CreateSystemManaged<SimulationSystemGroup>();
            _simGroup.AddSystemToUpdateList(_world.CreateSystem<StatModifierTickSystem>());
            _simGroup.AddSystemToUpdateList(_world.CreateSystem<ModifierStatsAggregateSystem>());
            _simGroup.AddSystemToUpdateList(_world.CreateSystem<AttackSystem>());

            // Singleton: unified attack visual events (Spine animation trigger for any attacker)
            _attackEventQueue = new NativeQueue<UnitAttackVisualEvent>(Allocator.Persistent);
            var attackSingleton = _em.CreateEntity();
            _em.AddComponentData(attackSingleton, new UnitAttackVisualEventsSingleton { queue = _attackEventQueue });

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
                range = range,
                cooldownDuration = cooldownDuration,
                cooldownRemaining = 0f,
                attackTargetCount = attackTargetCount,
                targetMask = targetMask,
            });
            if (defenderTag) _em.AddComponent<DefenderUnitTag>(e);
            if (attackerTag) _em.AddComponent<AttackUnitTag>(e);
            if ((defenderTag || attackerTag) && damage > 0f)
            {
                var outputs = _em.AddBuffer<AttackOutputElement>(e);
                outputs.Add(new AttackOutputElement
                {
                    value = new AttackOutput
                    {
                        kind = AttackOutputKind.Damage,
                        magnitude = damage,
                    }
                });
            }
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
            Assert.AreEqual(5f, _em.GetComponentData<ProjectileSpawnRequest>(defender).damage, 1e-4f,
                "Projectile defender should snapshot Damage output as projectile damage");
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

        // ─── U4: Defender with damageMul (×1.5 boost + ×2.0 synergy) and attackSpeedMul (×2) via ModifierStats ───

        [Test]
        public void U4_Defender_Buff_Multipliers_Applied_To_Damage_And_Cooldown()
        {
            var defender = CreateAttacker(
                Faction.Defender, new float3(0f, 0f, 0f),
                damage: 10f, range: 10f, cooldownDuration: 2f,
                targetMask: (int)Faction.Enemy,
                defenderTag: true);

            // Wire ModifierStats: damageMul = 1.5 × 2.0 = 3.0, attackSpeedMul = 2.0 (cooldown ÷ 2).
            _em.AddComponentData(defender, new ModifierStats { damageMul = 1f, attackSpeedMul = 1f, dmgTakenMul = 1f, moveSpeedMul = 1f });
            _em.AddComponent<ModifierStatsDirty>(defender);
            _em.SetComponentEnabled<ModifierStatsDirty>(defender, true);
            var slots = _em.AddBuffer<StatModifierSlot>(defender);
            slots.Add(new StatModifierSlot { header = new ModifierHeader { remaining = 10f }, stat = StatKind.DamageMul,      op = CombineOp.Multiplicative, magnitude = 1.5f });
            slots.Add(new StatModifierSlot { header = new ModifierHeader { remaining = 10f }, stat = StatKind.DamageMul,      op = CombineOp.Multiplicative, magnitude = 2f });
            slots.Add(new StatModifierSlot { header = new ModifierHeader { remaining = 10f }, stat = StatKind.AttackSpeedMul, op = CombineOp.Multiplicative, magnitude = 2f });

            var enemy = CreateTarget(Faction.Enemy, new float3(1f, 0f, 0f), attackerTag: true);

            Tick();

            // damage = 10 * 1.5 (boost) * 2.0 (synergy) = 30
            var dmgBuf = _em.GetBuffer<IncomingDamage>(enemy);
            Assert.AreEqual(1, dmgBuf.Length);
            Assert.AreEqual(30f, dmgBuf[0].amount, 1e-4f, "damageMul(boost) * damageMul(synergy) should multiply base damage");

            // cooldown reset = cooldownDuration / attackSpeedMul = 2.0 / 2.0 = 1.0
            var atkState = _em.GetComponentData<AttackState>(defender);
            Assert.AreEqual(1f, atkState.cooldownRemaining, 1e-4f, "attackSpeedMul 2.0 should halve reset interval");
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

        // ─── U7: Defender fire enqueues exactly one UnitAttackVisualEvent ───

        [Test]
        public void U7_Defender_Fire_Enqueues_UnitAttackVisualEvent()
        {
            var defender = CreateAttacker(
                Faction.Defender, new float3(0f, 0f, 0f),
                damage: 5f, range: 10f, cooldownDuration: 1f,
                targetMask: (int)Faction.Enemy,
                defenderTag: true);

            var enemy = CreateTarget(Faction.Enemy, new float3(2f, 0f, 0f), attackerTag: true);

            Tick();

            Assert.AreEqual(1, _attackEventQueue.Count,
                "Exactly one UnitAttackVisualEvent should be enqueued when defender fires");
            var ev = _attackEventQueue.Dequeue();
            Assert.AreEqual(defender, ev.attacker);
        }

        // ─── U8: Enemy fire also enqueues UnitAttackVisualEvent (unified channel) ───

        [Test]
        public void U8_Enemy_Fire_Enqueues_UnitAttackVisualEvent()
        {
            var enemy = CreateAttacker(
                Faction.Enemy, new float3(0f, 0f, 0f),
                damage: 5f, range: 10f, cooldownDuration: 1f,
                targetMask: (int)(Faction.Defender | Faction.BlockingHazard),
                attackerTag: true);

            var defender = CreateTarget(Faction.Defender, new float3(2f, 0f, 0f), defenderTag: true);

            Tick();

            Assert.AreEqual(1, _attackEventQueue.Count,
                "Enemy fire must enqueue a UnitAttackVisualEvent so SpineUnitPool can play the attack animation");
            var ev = _attackEventQueue.Dequeue();
            Assert.AreEqual(enemy, ev.attacker);
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
