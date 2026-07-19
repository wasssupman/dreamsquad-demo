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
    // Verifies the read-only Effects ↔ Movement/Combat bridges without
    // coupling back to Effects write semantics. EffectTickSystem is NOT added
    // to the world here so effects stay applied for the full test duration.
    public class EffectIntegrationTests
    {
        [Test]
        public void Movement_Applies_MoveSpeedMul_From_ModifierStats_To_Step()
        {
            using var world = new World("EffectIntegrationTests_Movement");
            var em = world.EntityManager;
            var simGroup = world.CreateSystemManaged<SimulationSystemGroup>();
            simGroup.AddSystemToUpdateList(world.CreateSystem<MovementSystem>());

            // 2-cell +x flow field. goal = (1,0).
            var flow = new NativeArray<float2>(2, Allocator.Persistent);
            var dist = new NativeArray<int>(2, Allocator.Persistent);
            flow[0] = new float2(1, 0); dist[0] = 1;
            flow[1] = float2.zero;      dist[1] = 0;
            var fieldEntity = em.CreateEntity();
            em.AddComponentData(fieldEntity, new FlowFieldSingleton
            {
                flow = flow, dist = dist,
                gridSize = new int2(2, 1),
                goalCell = new int2(1, 0),
                tileSize = 1f, version = 1,
            });

            var e = em.CreateEntity();
            em.AddComponentData(e, LocalTransform.FromPosition(new float3(0f, 0f, 0f)));
            em.AddComponentData(e, new PathFollowState { speed = 2f });
            em.AddComponentData(e, new ModifierStats { moveSpeedMul = 0.5f });

            // aggro-tile-chase unit 2 — 프레임 변위 상한(<0.9타일) 아래의 dt 로 검증(0.5s → step 0.5).
            world.SetTime(new TimeData(world.Time.ElapsedTime + 0.5f, 0.5f));
            simGroup.Update();

            var pos = em.GetComponentData<LocalTransform>(e).Position;
            Assert.AreEqual(0.5f, pos.x, 1e-4f, "MoveSpeedMul 0.5 should halve this frame's step.");
            Assert.AreEqual(2f, em.GetComponentData<PathFollowState>(e).speed, 1e-5f,
                "Base speed field stays unchanged — Movement still owns it.");

            // Persistent NativeArray dispose (world.Dispose 가 entity 는 파괴하지만 NativeArray 는 leak).
            em.GetComponentData<FlowFieldSingleton>(fieldEntity).Dispose();
        }

        [Test]
        public void Combat_Applies_DamageMul_And_AttackSpeedMul_Via_ModifierStats()
        {
            using var world = new World("EffectIntegrationTests_Combat");
            var em = world.EntityManager;
            var simGroup = world.CreateSystemManaged<SimulationSystemGroup>();
            simGroup.AddSystemToUpdateList(world.CreateSystem<StatModifierTickSystem>());
            simGroup.AddSystemToUpdateList(world.CreateSystem<ModifierStatsAggregateSystem>());
            simGroup.AddSystemToUpdateList(world.CreateSystem<AttackSystem>());

            // Defender: cooldown ready, 10 damage base, range 5.
            var defender = em.CreateEntity();
            em.AddComponentData(defender, LocalTransform.FromPosition(new float3(0f, 0f, 0f)));
            em.AddComponent<DefenderUnitTag>(defender);
            em.AddComponentData(defender, new Health { value = 10f, max = 10f });
            em.AddComponentData(defender, new FactionTag { value = Faction.Defender });
            em.AddBuffer<IncomingDamage>(defender);
            em.AddComponentData(defender, new AttackState
            {
                range = 5f,
                cooldownDuration = 4f,
                cooldownRemaining = 0f,
                targetMask = (int)Faction.Enemy,
            });
            // Wire ModifierStats via StatModifierSlot: damageMul×2, attackSpeedMul×2 (halves cooldown).
            var outputs = em.AddBuffer<AttackOutputElement>(defender);
            outputs.Add(new AttackOutputElement
            {
                value = new AttackOutput
                {
                    kind = AttackOutputKind.Damage,
                    magnitude = 10f,
                }
            });
            em.AddComponentData(defender, new ModifierStats { damageMul = 1f, attackSpeedMul = 1f, dmgTakenMul = 1f, moveSpeedMul = 1f });
            em.AddComponent<ModifierStatsDirty>(defender);
            em.SetComponentEnabled<ModifierStatsDirty>(defender, true);
            var slots = em.AddBuffer<StatModifierSlot>(defender);
            slots.Add(new StatModifierSlot { header = new ModifierHeader { remaining = 10f }, stat = StatKind.DamageMul,      op = CombineOp.Multiplicative, magnitude = 2f });
            slots.Add(new StatModifierSlot { header = new ModifierHeader { remaining = 10f }, stat = StatKind.AttackSpeedMul, op = CombineOp.Multiplicative, magnitude = 2f });

            // Attacker in range at (1,0,0).
            var attacker = em.CreateEntity();
            em.AddComponentData(attacker, LocalTransform.FromPosition(new float3(1f, 0f, 0f)));
            em.AddComponent<AttackUnitTag>(attacker);
            em.AddComponentData(attacker, new Health { value = 10f, max = 10f });
            em.AddComponentData(attacker, new FactionTag { value = Faction.Enemy });
            em.AddBuffer<IncomingDamage>(attacker);

            world.SetTime(new TimeData(world.Time.ElapsedTime + 0.016f, 0.016f));
            simGroup.Update();

            // Emitted damage: 10 base × 2.0 damageMul = 20.
            var incoming = em.GetBuffer<IncomingDamage>(attacker);
            Assert.AreEqual(1, incoming.Length, "attacker should have received exactly one damage event");
            Assert.AreEqual(20f, incoming[0].amount, 1e-4f, "damageMul 2.0 from ModifierStats should double the emitted damage");

            // Reset cooldown: cooldown = base / attackSpeedMul = 4 / 2 = 2.
            var attackState = em.GetComponentData<AttackState>(defender);
            Assert.AreEqual(2f, attackState.cooldownRemaining, 1e-4f,
                "attackSpeedMul 2.0 should halve the cooldown reset value");
            Assert.AreEqual(4f, attackState.cooldownDuration, 1e-5f, "AttackState.cooldownDuration must remain unchanged.");
        }

        [Test]
        public void Combat_Applies_SynergyMul_Stacked_With_DamageMul_Via_ModifierStats()
        {
            using var world = new World("EffectIntegrationTests_Synergy");
            var em = world.EntityManager;
            var simGroup = world.CreateSystemManaged<SimulationSystemGroup>();
            simGroup.AddSystemToUpdateList(world.CreateSystem<StatModifierTickSystem>());
            simGroup.AddSystemToUpdateList(world.CreateSystem<ModifierStatsAggregateSystem>());
            simGroup.AddSystemToUpdateList(world.CreateSystem<AttackSystem>());

            var defender = em.CreateEntity();
            em.AddComponentData(defender, LocalTransform.FromPosition(new float3(0f, 0f, 0f)));
            em.AddComponent<DefenderUnitTag>(defender);
            em.AddComponentData(defender, new Health { value = 10f, max = 10f });
            em.AddComponentData(defender, new FactionTag { value = Faction.Defender });
            em.AddBuffer<IncomingDamage>(defender);
            em.AddComponentData(defender, new AttackState
            {
                range = 5f,
                cooldownDuration = 4f,
                cooldownRemaining = 0f,
                targetMask = (int)Faction.Enemy,
            });
            // modifier-additive-authoring — boost and synergy are increases, so
            // BattleBridge now authors them as additive deltas: +1.0 (×2 boost) and
            // +0.3 (×1.3 synergy). They SUM: 1 + 1.0 + 0.3 = 2.3 (not the old ×2.6 product).
            var outputs = em.AddBuffer<AttackOutputElement>(defender);
            outputs.Add(new AttackOutputElement
            {
                value = new AttackOutput
                {
                    kind = AttackOutputKind.Damage,
                    magnitude = 10f,
                }
            });
            em.AddComponentData(defender, new ModifierStats { damageMul = 1f, attackSpeedMul = 1f, dmgTakenMul = 1f, moveSpeedMul = 1f });
            em.AddComponent<ModifierStatsDirty>(defender);
            em.SetComponentEnabled<ModifierStatsDirty>(defender, true);
            var slots = em.AddBuffer<StatModifierSlot>(defender);
            slots.Add(new StatModifierSlot { header = new ModifierHeader { remaining = 10f }, stat = StatKind.DamageMul, op = CombineOp.Additive, magnitude = 1.0f });
            slots.Add(new StatModifierSlot { header = new ModifierHeader { remaining = 10f }, stat = StatKind.DamageMul, op = CombineOp.Additive, magnitude = 0.3f });

            var attacker = em.CreateEntity();
            em.AddComponentData(attacker, LocalTransform.FromPosition(new float3(1f, 0f, 0f)));
            em.AddComponent<AttackUnitTag>(attacker);
            em.AddComponentData(attacker, new Health { value = 10f, max = 10f });
            em.AddComponentData(attacker, new FactionTag { value = Faction.Enemy });
            em.AddBuffer<IncomingDamage>(attacker);

            world.SetTime(new TimeData(world.Time.ElapsedTime + 0.016f, 0.016f));
            simGroup.Update();

            // Expected: 10 base × (1 + 1.0 + 0.3) = 10 × 2.3 = 23.
            var incoming = em.GetBuffer<IncomingDamage>(attacker);
            Assert.AreEqual(1, incoming.Length);
            Assert.AreEqual(23f, incoming[0].amount, 1e-4f,
                "additive boost + synergy deltas sum into damageMul (base × (1 + Σadd))");
        }

        [Test]
        public void Combat_Attacker_With_AttackState_Damages_Defender_In_Range()
        {
            using var world = new World("EffectIntegrationTests_EnemyAttack");
            var em = world.EntityManager;
            var simGroup = world.CreateSystemManaged<SimulationSystemGroup>();
            simGroup.AddSystemToUpdateList(world.CreateSystem<AttackSystem>());

            // Attacker at origin, cooldown ready, 7 damage base, range 2.
            var attacker = em.CreateEntity();
            em.AddComponentData(attacker, LocalTransform.FromPosition(new float3(0f, 0f, 0f)));
            em.AddComponent<AttackUnitTag>(attacker);
            em.AddComponentData(attacker, new Health { value = 10f, max = 10f });
            em.AddComponentData(attacker, new FactionTag { value = Faction.Enemy });
            em.AddComponentData(attacker, new AttackState
            {
                range = 2f,
                cooldownDuration = 1f,
                cooldownRemaining = 0f,
                attackTargetCount = 1,
                targetMask = (int)(Faction.Defender | Faction.BlockingHazard),
            });
            var outputs = em.AddBuffer<AttackOutputElement>(attacker);
            outputs.Add(new AttackOutputElement
            {
                value = new AttackOutput
                {
                    kind = AttackOutputKind.Damage,
                    magnitude = 7f,
                }
            });
            em.AddBuffer<IncomingDamage>(attacker);

            // Defender in range at (1, 0, 0). No AttackState → defender side of the
            // loop is inactive; this test only verifies the attacker→defender path.
            var defender = em.CreateEntity();
            em.AddComponentData(defender, LocalTransform.FromPosition(new float3(1f, 0f, 0f)));
            em.AddComponent<DefenderUnitTag>(defender);
            em.AddComponentData(defender, new Health { value = 10f, max = 10f });
            em.AddComponentData(defender, new FactionTag { value = Faction.Defender });
            em.AddBuffer<IncomingDamage>(defender);

            world.SetTime(new TimeData(world.Time.ElapsedTime + 0.016f, 0.016f));
            simGroup.Update();

            var incoming = em.GetBuffer<IncomingDamage>(defender);
            Assert.AreEqual(1, incoming.Length, "defender should take one attack from the enemy");
            Assert.AreEqual(7f, incoming[0].amount, 1e-4f,
                "enemy→defender damage is the raw Damage output magnitude (no boost/synergy scaling)");
            // Attacker cooldown should be reset to its base duration (no CDR on enemies).
            var attackerState = em.GetComponentData<AttackState>(attacker);
            Assert.AreEqual(1f, attackerState.cooldownRemaining, 1e-4f);
        }
    }
}
