using NUnit.Framework;
using Unity.Collections;
using Unity.Core;
using Unity.Entities;
using Wassup.Battle.Effects;

namespace Wassup.Tests.EditMode
{
    // effect-tiles unit 2 — 효과 타일 modifier 가 기존 파이프라인으로 정확히 부여/스택되는지 회귀.
    // BattleBridge.ApplyEffectTileIfAny 가 만드는 이벤트 shape 을 그대로 재현한다
    // (target=source=배치유닛, duration=∞, stackId=2).
    public class EffectTileModifierTests
    {
        private const ushort EffectTileStackId = 2; // BattleBridge.EffectTileStackId 와 동일 규약

        [Test]
        public void EffectTileModifier_AppliesAndStacksWithOnPlaceAndSynergy()
        {
            using var world = new World("EffectTileModifierTests_Stack");
            var em = world.EntityManager;
            var simGroup = world.CreateSystemManaged<SimulationSystemGroup>();
            simGroup.AddSystemToUpdateList(world.CreateSystem<ModifierApplySystem>());
            simGroup.AddSystemToUpdateList(world.CreateSystem<StatModifierTickSystem>());
            simGroup.AddSystemToUpdateList(world.CreateSystem<ModifierStatsAggregateSystem>());

            var statQ = new NativeQueue<StatModifierApplyEvent>(Allocator.Persistent);
            var stackQ = new NativeQueue<StackModifierApplyEvent>(Allocator.Persistent);
            try
            {
                var qe = em.CreateEntity();
                em.AddComponentData(qe, new StatModifierApplyEventsSingleton { queue = statQ });
                em.AddComponentData(qe, new StackModifierApplyEventsSingleton { queue = stackQ });

                var defender = em.CreateEntity();
                em.AddComponentData(defender, new ModifierStats());

                // 효과 타일(1.25, stackId=2, EffectTile 명시 op = Multiplicative 유지) +
                // on-place 류(+0.2, stackId=0) + 시너지 류(+0.1, stackId=1). modifier-additive-
                // authoring: on-place/synergy 는 증가라 이제 Additive 델타. 결합 = 곱×합.
                statQ.Enqueue(MakeEvent(defender, 1.25f, EffectTileStackId));
                statQ.Enqueue(MakeAdditiveEvent(defender, 0.2f, 0));
                statQ.Enqueue(MakeAdditiveEvent(defender, 0.1f, 1));

                world.SetTime(new TimeData(world.Time.ElapsedTime + 1f, 1f));
                simGroup.Update();

                Assert.AreEqual(3, em.GetBuffer<StatModifierSlot>(defender).Length,
                    "stackId 네임스페이스(0/1/2)로 3개 슬롯이 분리 공존해야 한다.");
                Assert.AreEqual(1.25f * (1f + 0.2f + 0.1f), em.GetComponentData<ModifierStats>(defender).damageMul, 1e-4f,
                    "effect-tile(mul) × (1 + Σadd of on-place+synergy).");
            }
            finally
            {
                statQ.Dispose();
                stackQ.Dispose();
            }
        }

        [Test]
        public void EffectTileModifier_IsIdempotentAndPermanent()
        {
            using var world = new World("EffectTileModifierTests_Permanent");
            var em = world.EntityManager;
            var simGroup = world.CreateSystemManaged<SimulationSystemGroup>();
            simGroup.AddSystemToUpdateList(world.CreateSystem<ModifierApplySystem>());
            simGroup.AddSystemToUpdateList(world.CreateSystem<StatModifierTickSystem>());
            simGroup.AddSystemToUpdateList(world.CreateSystem<ModifierStatsAggregateSystem>());

            var statQ = new NativeQueue<StatModifierApplyEvent>(Allocator.Persistent);
            var stackQ = new NativeQueue<StackModifierApplyEvent>(Allocator.Persistent);
            try
            {
                var qe = em.CreateEntity();
                em.AddComponentData(qe, new StatModifierApplyEventsSingleton { queue = statQ });
                em.AddComponentData(qe, new StackModifierApplyEventsSingleton { queue = stackQ });

                var defender = em.CreateEntity();
                em.AddComponentData(defender, new ModifierStats());

                // 같은 이벤트 2회 = merge-key refresh — 슬롯 1개 유지(즉시 적용 + 배치 훅 중복 시나리오).
                statQ.Enqueue(MakeEvent(defender, 1.25f, EffectTileStackId));
                statQ.Enqueue(MakeEvent(defender, 1.25f, EffectTileStackId));

                world.SetTime(new TimeData(world.Time.ElapsedTime + 1f, 1f));
                simGroup.Update();

                Assert.AreEqual(1, em.GetBuffer<StatModifierSlot>(defender).Length, "재적용은 refresh — 슬롯 증식 금지.");
                Assert.AreEqual(1.25f, em.GetComponentData<ModifierStats>(defender).damageMul, 1e-4f);

                // duration=∞ — 큰 dt 로 tick 해도 만료되지 않는다.
                world.SetTime(new TimeData(world.Time.ElapsedTime + 10000.0, 10000f));
                simGroup.Update();

                Assert.AreEqual(1, em.GetBuffer<StatModifierSlot>(defender).Length, "영구 지속 — tick 만료 금지.");
                Assert.AreEqual(1.25f, em.GetComponentData<ModifierStats>(defender).damageMul, 1e-4f);
            }
            finally
            {
                statQ.Dispose();
                stackQ.Dispose();
            }
        }

        [Test]
        public void EffectTileModifier_MultiStatEntriesApplyDistinctSlots()
        {
            using var world = new World("EffectTileModifierTests_MultiStat");
            var em = world.EntityManager;
            var simGroup = world.CreateSystemManaged<SimulationSystemGroup>();
            simGroup.AddSystemToUpdateList(world.CreateSystem<ModifierApplySystem>());
            simGroup.AddSystemToUpdateList(world.CreateSystem<StatModifierTickSystem>());
            simGroup.AddSystemToUpdateList(world.CreateSystem<ModifierStatsAggregateSystem>());

            var statQ = new NativeQueue<StatModifierApplyEvent>(Allocator.Persistent);
            var stackQ = new NativeQueue<StackModifierApplyEvent>(Allocator.Persistent);
            try
            {
                var qe = em.CreateEntity();
                em.AddComponentData(qe, new StatModifierApplyEventsSingleton { queue = statQ });
                em.AddComponentData(qe, new StackModifierApplyEventsSingleton { queue = stackQ });

                var defender = em.CreateEntity();
                em.AddComponentData(defender, new ModifierStats());

                // 글래스캐논 타일 shape: 한 타일이 stat 2개 entry — 같은 stackId=2, stat 만 다름.
                statQ.Enqueue(MakeEvent(defender, 1.4f, EffectTileStackId)); // DamageMul
                statQ.Enqueue(new StatModifierApplyEvent
                {
                    target = defender, stat = StatKind.DmgTakenMul, op = CombineOp.Multiplicative,
                    magnitude = 1.4f, duration = float.PositiveInfinity, source = defender,
                    stackId = EffectTileStackId,
                });

                world.SetTime(new TimeData(world.Time.ElapsedTime + 1f, 1f));
                simGroup.Update();

                Assert.AreEqual(2, em.GetBuffer<StatModifierSlot>(defender).Length,
                    "stat 이 다르면 같은 stackId 여도 슬롯 분리.");
                var ms = em.GetComponentData<ModifierStats>(defender);
                Assert.AreEqual(1.4f, ms.damageMul, 1e-4f);
                Assert.AreEqual(1.4f, ms.dmgTakenMul, 1e-4f);
            }
            finally
            {
                statQ.Dispose();
                stackQ.Dispose();
            }
        }

        // BattleBridge.ApplyEffectTileIfAny 의 이벤트 shape 재현 (source=target, duration=∞).
        // EffectTile 은 명시 op 저작을 존중하므로 Multiplicative 로 남는다(additive-authoring 범위 밖).
        private static StatModifierApplyEvent MakeEvent(Entity target, float magnitude, ushort stackId)
            => new StatModifierApplyEvent
            {
                target    = target,
                stat      = StatKind.DamageMul,
                op        = CombineOp.Multiplicative,
                magnitude = magnitude,
                duration  = float.PositiveInfinity,
                source    = target,
                stackId   = stackId,
            };

        // on-place/synergy 등 증가형 버프의 새 shape — BattleBridge 헬퍼가 Additive 델타로 저작.
        private static StatModifierApplyEvent MakeAdditiveEvent(Entity target, float delta, ushort stackId)
            => new StatModifierApplyEvent
            {
                target    = target,
                stat      = StatKind.DamageMul,
                op        = CombineOp.Additive,
                magnitude = delta,
                duration  = float.PositiveInfinity,
                source    = target,
                stackId   = stackId,
            };
    }
}
