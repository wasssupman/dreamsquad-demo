// Spec unit 4 (modifier-framework-and-healer): tick StackModifierSlot remaining,
// detect edge threshold crossings (multi-threshold: all crossed thresholds fire),
// dispatch derived effects to EnemyCcEvents / StatModifierApplyEvents (1-frame delay),
// and remove expired slots.
// Not Burst-compiled: BattleBridge static SO registry uses a managed Dictionary.
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Wassup.Bridge;
using Wassup.Data;

namespace Wassup.Battle.Effects
{
    [UpdateInGroup(typeof(BattleSimGroup))]
    [UpdateAfter(typeof(ModifierStatsAggregateSystem))]
    public partial struct StackModifierTickSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<EnemyCcEventsSingleton>();
            state.RequireForUpdate<StatModifierApplyEventsSingleton>();
        }

        public void OnUpdate(ref SystemState state)
        {
            float dt = SystemAPI.Time.DeltaTime;
            var ccQ   = SystemAPI.GetSingleton<EnemyCcEventsSingleton>().queue;
            var statQ = SystemAPI.GetSingleton<StatModifierApplyEventsSingleton>().queue;

            foreach (var (_, entity) in
                     SystemAPI.Query<DynamicBuffer<StackModifierSlot>>().WithEntityAccess())
            {
                // Get a writable buffer reference (query result is read-only).
                var slots = SystemAPI.GetBuffer<StackModifierSlot>(entity);

                // Iterate backwards so RemoveAtSwapBack does not skip elements.
                for (int i = slots.Length - 1; i >= 0; i--)
                {
                    var s = slots[i];

                    // 1. Tick
                    s.header.remaining -= dt;

                    // 2. Threshold edge detection — fire all thresholds crossed this frame.
                    //    Multi-threshold: if stack jumped 4→7, thresholds at 5, 6, 7 all fire.
                    if (s.stackCount > s.lastTriggeredStack)
                    {
                        DispatchThresholds(
                            s.kind,
                            s.lastTriggeredStack,
                            ref s,
                            entity,
                            ccQ,
                            statQ);
                    }

                    // 3. Expiry
                    if (s.header.remaining <= 0f)
                    {
                        slots.RemoveAtSwapBack(i);
                    }
                    else
                    {
                        slots[i] = s;
                    }
                }
            }
        }

        // Dispatch all ThresholdRules where lastTriggeredStack < atStack <= stackCount.
        // ThresholdRule[] assumed ascending by atStack (SO authors must keep ascending order).
        // Consume mode: subtract atStack from stackCount after firing.
        // lastTriggeredStack is set to final stackCount after all consume adjustments.
        private static void DispatchThresholds(
            StackKind kind,
            byte prevStack,
            ref StackModifierSlot s,
            Entity entity,
            NativeQueue<EnemyCcEvent> ccQ,
            NativeQueue<StatModifierApplyEvent> statQ)
        {
            ThresholdRule[] rules = BattleBridge.GetStackThresholds(kind);
            if (rules == null || rules.Length == 0)
            {
                s.lastTriggeredStack = s.stackCount;
                return;
            }

            // Rules assumed ascending by atStack.
            foreach (var rule in rules)
            {
                if (rule.atStack <= prevStack || rule.atStack > s.stackCount)
                    continue;

                // Fire the rule.
                switch (rule.derivedKind)
                {
                    case DerivedEffectKind.ApplyDot:
                        ccQ.Enqueue(new EnemyCcEvent
                        {
                            target = entity,
                            effect = new CcEffect
                            {
                                kind          = CcKind.DoT,
                                scalar        = rule.magnitude, // dps
                                remainingTime = rule.duration,
                            }
                        });
                        break;

                    case DerivedEffectKind.ApplyStun:
                        ccQ.Enqueue(new EnemyCcEvent
                        {
                            target = entity,
                            effect = new CcEffect
                            {
                                kind          = CcKind.Stun,
                                remainingTime = rule.magnitude, // stun duration
                            }
                        });
                        break;

                    case DerivedEffectKind.ApplyStat:
                        statQ.Enqueue(new StatModifierApplyEvent
                        {
                            target    = entity,
                            stat      = rule.stat,
                            op        = rule.op,
                            magnitude = rule.magnitude,
                            duration  = rule.duration,
                            source    = entity,
                            stackId   = 0,
                            // 야근 번아웃만 전용 origin 승격 — 상태FX(BattleBridge)가 다른 Stack
                            // 파생과 안 섞이게(review #3). 범용 trigger→domain 통합은 파킹 문서.
                            origin    = kind == StackKind.Fatigue
                                        ? ModifierOrigin.Burnout
                                        : ModifierOrigin.Stack,
                        });
                        break;
                }

                // Consume mode: reduce stackCount by rule.atStack (clamped to 0).
                if (rule.mode == ThresholdMode.Consume)
                    s.stackCount = (byte)math.max(0, s.stackCount - rule.atStack);
            }

            // Update edge cache to current stackCount (after any consume adjustments).
            s.lastTriggeredStack = s.stackCount;
        }
    }
}
