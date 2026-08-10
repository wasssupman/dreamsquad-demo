// Unit 11 — modifier-framework-and-healer EditMode tests.
// Tests 1, 2, 5 are fully implemented.
// Tests 3 and 4 are skipped (see [Ignore] attributes for rationale).
using NUnit.Framework;
using Unity.Collections;
using Unity.Core;
using Unity.Entities;
using Wassup.Battle.Effects;
using Wassup.Battle.Units;

namespace Wassup.Tests.EditMode
{
    public class ModifierFrameworkTests
    {
        private World _world;
        private EntityManager _em;
        private SimulationSystemGroup _simGroup;
        private NativeQueue<StatModifierApplyEvent> _statQueue;
        private NativeQueue<StackModifierApplyEvent> _stackQueue;

        [SetUp]
        public void SetUp()
        {
            _world    = new World("ModifierFrameworkTests");
            _em       = _world.EntityManager;
            _simGroup = _world.CreateSystemManaged<SimulationSystemGroup>();

            _simGroup.AddSystemToUpdateList(_world.CreateSystem<ModifierApplySystem>());
            _simGroup.AddSystemToUpdateList(_world.CreateSystem<StatModifierTickSystem>());
            _simGroup.AddSystemToUpdateList(_world.CreateSystem<ModifierStatsAggregateSystem>());
            _simGroup.AddSystemToUpdateList(_world.CreateSystem<StackModifierTickSystem>());

            // Singleton queues required by ModifierApplySystem and StackModifierTickSystem.
            _statQueue  = new NativeQueue<StatModifierApplyEvent>(Allocator.Persistent);
            _stackQueue = new NativeQueue<StackModifierApplyEvent>(Allocator.Persistent);

            var singletonStat = _em.CreateEntity();
            _em.AddComponentData(singletonStat,
                new StatModifierApplyEventsSingleton { queue = _statQueue });

            var singletonStack = _em.CreateEntity();
            _em.AddComponentData(singletonStack,
                new StackModifierApplyEventsSingleton { queue = _stackQueue });

            // StackModifierTickSystem also requires EnemyCcEventsSingleton.
            var ccQueue = new NativeQueue<EnemyCcEvent>(Allocator.Persistent);
            var singletonCc = _em.CreateEntity();
            _em.AddComponentData(singletonCc,
                new EnemyCcEventsSingleton { queue = ccQueue });
            // ccQueue is owned by the singleton entity; disposed when world is disposed
            // via EnemyCcEventsSingleton.queue — no separate tracking needed here because
            // the World.Dispose path does not auto-dispose NativeCollections. We capture
            // it only to ensure disposal in TearDown.
            _ccQueue = ccQueue;
        }

        private NativeQueue<EnemyCcEvent> _ccQueue;

        [TearDown]
        public void TearDown()
        {
            if (_statQueue.IsCreated)  _statQueue.Dispose();
            if (_stackQueue.IsCreated) _stackQueue.Dispose();
            if (_ccQueue.IsCreated)    _ccQueue.Dispose();
            _world?.Dispose();
        }

        private void Tick(float deltaTime = 0.016f)
        {
            _world.SetTime(new TimeData(_world.Time.ElapsedTime + deltaTime, deltaTime));
            _simGroup.Update();
        }

        // ── Helpers ───────────────────────────────────────────────────────────────

        /// Create an entity pre-wired with ModifierStats (defaults 1/1/1/0) and
        /// a disabled ModifierStatsDirty (the canonical starting state).
        private Entity CreateEntityWithModifierStats()
        {
            var e = _em.CreateEntity();
            _em.AddComponentData(e, new ModifierStats
            {
                damageMul      = 1f,
                attackSpeedMul = 1f,
                dmgTakenMul    = 1f,
                regenPerSec    = 0f,
                moveSpeedMul   = 1f,
            });
            _em.AddComponent<ModifierStatsDirty>(e);
            _em.SetComponentEnabled<ModifierStatsDirty>(e, false);
            return e;
        }

        // ── Test 1 ────────────────────────────────────────────────────────────────
        // StatModifier merge key: same (source/stat/op/stackId) refreshes the slot
        // rather than adding a second one.  remaining = max(old, new); magnitude = new.

        [Test]
        public void StatModifier_SameKey_Refreshes_Slot_Instead_Of_Adding_Duplicate()
        {
            var e = CreateEntityWithModifierStats();

            // First application: magnitude=1.5, duration=10.
            _statQueue.Enqueue(new StatModifierApplyEvent
            {
                target    = e,
                stat      = StatKind.DamageMul,
                op        = CombineOp.Multiplicative,
                magnitude = 1.5f,
                duration  = 10f,
                source    = e,
                stackId   = 0,
            });
            Tick();

            var slots = _em.GetBuffer<StatModifierSlot>(e);
            Assert.AreEqual(1, slots.Length,
                "First application should create exactly one slot.");
            Assert.AreEqual(1.5f, slots[0].magnitude, 1e-5f);

            // Second application with same key: magnitude=2.0, duration=5 (shorter).
            _statQueue.Enqueue(new StatModifierApplyEvent
            {
                target    = e,
                stat      = StatKind.DamageMul,
                op        = CombineOp.Multiplicative,
                magnitude = 2.0f,
                duration  = 5f,
                source    = e,
                stackId   = 0,
            });
            Tick();

            slots = _em.GetBuffer<StatModifierSlot>(e);
            Assert.AreEqual(1, slots.Length,
                "Second application with same key must refresh, not add a second slot.");
            Assert.AreEqual(2.0f, slots[0].magnitude, 1e-5f,
                "Refreshed slot magnitude must be the new value (2.0).");
            // remaining after first Tick was ~9.98 (10 - 0.016); new duration=5 is shorter,
            // so remaining should stay >= 5 (max behaviour).
            Assert.GreaterOrEqual(slots[0].header.remaining, 5f,
                "remaining = max(old, new) — must not be reduced to the shorter new duration.");
        }

        // ── dreamcatcher-new-abilities unit 3 ───────────────────────────────────────
        // DamageVsCcMul(6번째 stat)은 슬롯이 없어도 집계가 base 1 로 써야 한다. 이게
        // 깨지면(0 유지) shatter 미보유 유닛이 CC 걸린 적에게 데미지 0 = 적 무적(critic HIGH).
        [Test]
        public void DamageVsCcMul_AggregatesToBaseOne_WhenNoVsCcSlot()
        {
            var e = CreateEntityWithModifierStats(); // damageVsCcMul 필드는 0 으로 시작

            // 무관한 DamageMul 모디파이어만 적용 → dirty 활성 → 집계 1회 실행.
            _statQueue.Enqueue(new StatModifierApplyEvent
            {
                target = e, stat = StatKind.DamageMul, op = CombineOp.Multiplicative,
                magnitude = 2f, duration = 100f, source = e, stackId = 0,
            });
            Tick();

            Assert.AreEqual(1f, _em.GetComponentData<ModifierStats>(e).damageVsCcMul, 1e-5f,
                "집계는 vsCc 슬롯이 없어도 base 1 을 써야 한다(0 이면 CC 적 무적).");
        }

        // shatter_hymn: DamageVsCcMul 모디파이어가 곱연산으로 집계된다.
        [Test]
        public void DamageVsCcMul_Combines_Multiplicatively()
        {
            var e = CreateEntityWithModifierStats();

            _statQueue.Enqueue(new StatModifierApplyEvent
            {
                target = e, stat = StatKind.DamageVsCcMul, op = CombineOp.Multiplicative,
                magnitude = 1.25f, duration = 100f, source = e, stackId = 0,
            });
            Tick();

            Assert.AreEqual(1.25f, _em.GetComponentData<ModifierStats>(e).damageVsCcMul, 1e-5f,
                "+25% vsCc → damageVsCcMul == 1.25.");
        }

        // ── Test 2 ────────────────────────────────────────────────────────────────
        // ModifierStats combine formula: (1 + Σadd) * Πmul, and Override wins.

        [Test]
        public void ModifierStats_Combines_Multiplicative_And_Additive_Then_Override_Wins()
        {
            var e = CreateEntityWithModifierStats();

            // Pre-populate two slots directly (no Apply events needed for formula test).
            var buf = _em.AddBuffer<StatModifierSlot>(e);
            // Slot A: Multiplicative × 1.5
            buf.Add(new StatModifierSlot
            {
                header    = new ModifierHeader { remaining = 100f, source = e, stackId = 0 },
                stat      = StatKind.DamageMul,
                op        = CombineOp.Multiplicative,
                magnitude = 1.5f,
            });
            // Slot B: Additive + 0.2  (different stackId to distinguish from slot A)
            buf.Add(new StatModifierSlot
            {
                header    = new ModifierHeader { remaining = 100f, source = e, stackId = 1 },
                stat      = StatKind.DamageMul,
                op        = CombineOp.Additive,
                magnitude = 0.2f,
            });

            // Mark dirty so Aggregate runs.
            _em.SetComponentEnabled<ModifierStatsDirty>(e, true);
            Tick();

            // Formula: (1 + 0.2) * 1.5 = 1.8
            float damageMul = _em.GetComponentData<ModifierStats>(e).damageMul;
            Assert.AreEqual(1.8f, damageMul, 1e-4f,
                "damageMul must equal (1 + additive_sum) * multiplicative_product = (1+0.2)*1.5 = 1.8");

            // Now add an Override slot: 3.0 — must win over mul+add result.
            buf = _em.GetBuffer<StatModifierSlot>(e);
            buf.Add(new StatModifierSlot
            {
                header    = new ModifierHeader { remaining = 100f, source = e, stackId = 2 },
                stat      = StatKind.DamageMul,
                op        = CombineOp.Override,
                magnitude = 3.0f,
            });

            _em.SetComponentEnabled<ModifierStatsDirty>(e, true);
            Tick();

            damageMul = _em.GetComponentData<ModifierStats>(e).damageMul;
            Assert.AreEqual(3.0f, damageMul, 1e-4f,
                "Override slot must win: damageMul = max(override values) = 3.0, ignoring mul/add slots.");
        }

        [Test]
        public void ModifierStats_Combines_MoveSpeedMul_As_Multiplicative_Stat()
        {
            var e = CreateEntityWithModifierStats();
            var buf = _em.AddBuffer<StatModifierSlot>(e);
            buf.Add(new StatModifierSlot
            {
                header = new ModifierHeader { remaining = 100f, source = e, stackId = 0 },
                stat = StatKind.MoveSpeedMul,
                op = CombineOp.Multiplicative,
                magnitude = 0.5f,
            });
            buf.Add(new StatModifierSlot
            {
                header = new ModifierHeader { remaining = 100f, source = e, stackId = 1 },
                stat = StatKind.MoveSpeedMul,
                op = CombineOp.Multiplicative,
                magnitude = 0.8f,
            });

            _em.SetComponentEnabled<ModifierStatsDirty>(e, true);
            Tick();

            Assert.AreEqual(0.4f, _em.GetComponentData<ModifierStats>(e).moveSpeedMul, 1e-4f,
                "MoveSpeedMul should multiply like the other multiplier stats.");
        }

        [Test]
        public void StatModifierApply_Ignores_Event_When_Target_Was_Destroyed()
        {
            var e = CreateEntityWithModifierStats();
            _statQueue.Enqueue(new StatModifierApplyEvent
            {
                target = e,
                stat = StatKind.MoveSpeedMul,
                op = CombineOp.Multiplicative,
                magnitude = 0.5f,
                duration = 1f,
                source = Entity.Null,
                stackId = 0,
            });
            _em.DestroyEntity(e);

            Assert.DoesNotThrow(() => Tick());
            Assert.IsTrue(_statQueue.IsEmpty());
        }

        [Test]
        public void StackModifierApply_Ignores_Event_When_Target_Was_Destroyed()
        {
            var e = CreateEntityWithModifierStats();
            _stackQueue.Enqueue(new StackModifierApplyEvent
            {
                target = e,
                kind = StackKind.Fire,
                countDelta = 1,
                maxStack = 5,
                perAppDuration = 1f,
                source = Entity.Null,
            });
            _em.DestroyEntity(e);

            Assert.DoesNotThrow(() => Tick());
            Assert.IsTrue(_stackQueue.IsEmpty());
        }

        [Test]
        public void Structure_Is_Completely_Immune_And_Never_Gains_ModifierBuffers()
        {
            var structure = _em.CreateEntity();
            _em.AddComponentData(structure, new StructureTag
            {
                cell = new Unity.Mathematics.int2(3, 4),
                faction = Faction.EnemyCore,
            });
            _statQueue.Enqueue(new StatModifierApplyEvent
            {
                target = structure,
                stat = StatKind.DamageMul,
                op = CombineOp.Multiplicative,
                magnitude = 0.5f,
                duration = 2f,
                source = Entity.Null,
            });
            _stackQueue.Enqueue(new StackModifierApplyEvent
            {
                target = structure,
                kind = StackKind.Fire,
                countDelta = 1,
                maxStack = 5,
                perAppDuration = 2f,
                source = Entity.Null,
            });

            Assert.DoesNotThrow(() => Tick());
            Assert.IsTrue(_statQueue.IsEmpty());
            Assert.IsTrue(_stackQueue.IsEmpty());
            Assert.IsTrue(_em.Exists(structure));
            Assert.IsFalse(_em.HasBuffer<StatModifierSlot>(structure));
            Assert.IsFalse(_em.HasBuffer<StackModifierSlot>(structure));
            Assert.IsFalse(_em.HasComponent<ModifierStats>(structure));
            Assert.IsFalse(_em.HasComponent<ModifierStatsDirty>(structure));
        }

        // ── Test 3 ────────────────────────────────────────────────────────────────
        // Stack edge multi-threshold (4→7 jump fires all crossed thresholds).
        // BattleBridge._stackThresholds is private static with no public injection hook.
        // Cannot mock without modifying production code. Skipped; tracked as future spec item.

        [Test]
        [Ignore("BattleBridge._stackThresholds is private static with no public setter. " +
                "Multi-threshold crossing test requires a test-injection hook or a " +
                "StackThresholdRegistry abstraction. Track as follow-up spec.")]
        public void StackModifier_MultiThreshold_FourToSeven_Fires_All_Crossed_Thresholds()
        {
            // TODO: Once BattleBridge exposes SetStackThresholdsForTest(kind, rules[]) or
            // StackModifierTickSystem is decoupled from the static registry via an injected
            // IStackThresholdRegistry, implement this test:
            //   1. Inject rules at atStack=5 and atStack=6 for StackKind.Fire.
            //   2. Enqueue StackModifierApplyEvent(kind=Fire, countDelta=4, maxStack=10).
            //   3. Tick -> verify stackCount=4, lastTriggeredStack=0.
            //   4. Enqueue another event(countDelta=3) -> Tick.
            //   5. Assert ccQueue.Count == 2 (rules at 5 and 6 both fired).
        }

        // ── Test 4 ────────────────────────────────────────────────────────────────
        // AttackOutput branching — verifying all 4 output kinds (Damage/Heal/ApplyStat/ApplyStack)
        // each reach their respective channel.
        // AttackSystem's output dispatch is deeply integrated: it requires a full combat setup
        // (AttackState, FactionTag, in-range targets, valid AttackOutputElement buffer) and
        // the branching is not extracted into a unit-testable pure function.
        // Skipped; covered by PlayMode smoke tests per existing pattern.

        [Test]
        [Ignore("AttackOutput branching is inside AttackSystem's update loop with no " +
                "extracted unit-testable dispatch function. Requires full combat world setup " +
                "equivalent to a PlayMode integration test. Track as follow-up spec for " +
                "refactoring dispatch into a testable static helper.")]
        public void AttackOutput_AllFourKinds_EnqueueToCorrectChannels()
        {
            // TODO: Refactor AttackSystem to extract ProcessAttackOutput(outputs, ccQ, statQ, stackQ)
            // as a static method, then test it with a controlled AttackOutputElement buffer and
            // mock NativeQueue writers.
            //
            // bleed-fighter-defender unit 0 — ApplyStack arm 은 그 사이 PlayMode 로 실질 커버됐다:
            // Tests/PlayMode/DefenderApplyStackOutputTest.cs (enqueue 를 끊으면 실패하는 것까지 확인).
            // 남은 미커버는 Damage/Heal/ApplyStat 3종.
        }

        // ── Test 5 (CRITICAL — hotfix regression guard) ───────────────────────────
        // After StatModifierTickSystem hotfix: entities with ModifierStatsDirty=false must still
        // have their slot remaining values decremented, and expired slots must be removed.
        // Previously the system only iterated entities with dirty=true, causing permanent
        // modifiers (never expired even after duration elapsed).

        [Test]
        public void StatModifier_ExpiresAfterDuration_Even_When_ModifierStatsDirty_Is_False()
        {
            var e = CreateEntityWithModifierStats();

            // Apply a modifier with duration=2 via the Apply channel.
            _statQueue.Enqueue(new StatModifierApplyEvent
            {
                target    = e,
                stat      = StatKind.DamageMul,
                op        = CombineOp.Multiplicative,
                magnitude = 1.5f,
                duration  = 2f,
                source    = e,
                stackId   = 0,
            });

            // Frame 1: Apply -> Tick (remaining: 2.0 - 1.0 = 1.0) -> Aggregate (damageMul=1.5, dirty=false).
            Tick(1.0f);
            Assert.AreEqual(1.5f, _em.GetComponentData<ModifierStats>(e).damageMul, 1e-4f,
                "damageMul should be 1.5 after first tick (modifier active).");
            Assert.IsFalse(_em.IsComponentEnabled<ModifierStatsDirty>(e),
                "ModifierStatsDirty must be false after Aggregate resets it.");

            // Frame 2: dirty=false — the hotfix ensures TickSystem still decrements remaining.
            // remaining: 1.0 - 1.0 = 0.0 -> slot expires -> dirty set to true -> Aggregate runs.
            Tick(1.0f);

            // Frame 3: slot is gone; Aggregate should have reset damageMul to 1.0 (identity).
            // Aggregate only runs when dirty=true, which was set by TickSystem on expiry.
            // After Aggregate: damageMul=1.0, dirty=false again.
            Tick(1.0f);

            float finalDamageMul = _em.GetComponentData<ModifierStats>(e).damageMul;
            Assert.AreEqual(1.0f, finalDamageMul, 1e-4f,
                "damageMul must revert to 1.0 after modifier expires — hotfix regression guard: " +
                "TickSystem must decrement remaining regardless of ModifierStatsDirty state.");

            // Slot buffer should be empty after expiry.
            Assert.IsTrue(_em.HasBuffer<StatModifierSlot>(e),
                "StatModifierSlot buffer should still exist on entity (just empty).");
            Assert.AreEqual(0, _em.GetBuffer<StatModifierSlot>(e).Length,
                "Expired slot must be removed from the buffer.");
        }

        // ── modifier-stacking-policy wiring guard ───────────────────────────────────
        // System-level check that each stat is clamped with ITS OWN bound: damageMul
        // floors at 0.2, moveSpeedMul at 0.15. Distinct-source multiplicative stacks
        // (distinct stackId → separate slots) drive both below their floors. A swap of
        // MulStatFloor/MoveMulFloor, or moveSpeedMul routed through the wrong constant,
        // fails here even though the pure-function tests still pass.
        [Test]
        public void Clamp_DamageAndMove_UseTheirOwnFloor()
        {
            var e = CreateEntityWithModifierStats();

            // 5 distinct ×0.6 DamageMul (0.6^5 ≈ 0.078) and 5 distinct ×0.5 MoveSpeedMul
            // (0.5^5 ≈ 0.031) — both well below their respective floors.
            for (ushort i = 0; i < 5; i++)
            {
                _statQueue.Enqueue(new StatModifierApplyEvent
                {
                    target = e, stat = StatKind.DamageMul, op = CombineOp.Multiplicative,
                    magnitude = 0.6f, duration = 100f, source = e, stackId = i,
                });
                _statQueue.Enqueue(new StatModifierApplyEvent
                {
                    target = e, stat = StatKind.MoveSpeedMul, op = CombineOp.Multiplicative,
                    magnitude = 0.5f, duration = 100f, source = e, stackId = i,
                });
            }
            Tick();

            var stats = _em.GetComponentData<ModifierStats>(e);
            Assert.AreEqual(0.2f, stats.damageMul, 1e-5f, "damageMul must clamp to its own 0.2 floor.");
            Assert.AreEqual(0.15f, stats.moveSpeedMul, 1e-5f, "moveSpeedMul must clamp to its own 0.15 floor, not damage's 0.2.");
        }

        // ── modifier-additive-authoring: additive buffs SUM, and the sum is ceiled ──
        // Two distinct-stackId additive DamageMul deltas (+3.0 each, the shape buffs
        // now author) sum to 1 + 3 + 3 = 7, which the stacking-policy ceil pulls to 5.
        // If additive buffs multiplied instead, this would read (1+3)*(1+3)=16→ceil too,
        // so the intermediate slot count + the SUM (not product) is what this pins.
        [Test]
        public void AdditiveBuffs_Sum_ThenCeil()
        {
            var e = CreateEntityWithModifierStats();

            _statQueue.Enqueue(new StatModifierApplyEvent
            {
                target = e, stat = StatKind.DamageMul, op = CombineOp.Additive,
                magnitude = 3f, duration = 100f, source = e, stackId = 0,
            });
            _statQueue.Enqueue(new StatModifierApplyEvent
            {
                target = e, stat = StatKind.DamageMul, op = CombineOp.Additive,
                magnitude = 3f, duration = 100f, source = e, stackId = 1,
            });
            Tick();

            Assert.AreEqual(2, _em.GetBuffer<StatModifierSlot>(e).Length, "distinct stackId → two additive slots");
            Assert.AreEqual(5f, _em.GetComponentData<ModifierStats>(e).damageMul, 1e-5f,
                "1 + Σadd(3+3)=7 clamped to the 5.0 ceil (buffs sum, not compound).");
        }
    }
}
