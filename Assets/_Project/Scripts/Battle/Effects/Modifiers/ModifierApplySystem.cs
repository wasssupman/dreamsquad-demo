// Spec unit 2 (modifier-framework-and-healer): drain StatModifierApplyEvents + StackModifierApplyEvents,
// update DynamicBuffers, and mark BuffStatsDirty.
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace Wassup.Battle.Effects
{
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateBefore(typeof(StatModifierTickSystem))]
    public partial struct ModifierApplySystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<StatModifierApplyEventsSingleton>();
            state.RequireForUpdate<StackModifierApplyEventsSingleton>();
        }

        // OnUpdate is not Burst-compiled: EntityManager.HasBuffer / GetBuffer / AddComponent /
        // SetComponentEnabled are structural-change-adjacent APIs not eligible under Burst.
        public void OnUpdate(ref SystemState state)
        {
            var statQ  = SystemAPI.GetSingleton<StatModifierApplyEventsSingleton>().queue;
            var stackQ = SystemAPI.GetSingleton<StackModifierApplyEventsSingleton>().queue;
            var em     = state.EntityManager;
            var ecb    = new EntityCommandBuffer(Allocator.Temp);

            while (statQ.TryDequeue(out var ev))
                ApplyStat(em, ecb, ev);

            while (stackQ.TryDequeue(out var ev))
                ApplyStack(em, ecb, ev);

            // ECB is still used for AddBuffer<StatModifierSlot> / AddBuffer<StackModifierSlot>
            // when the buffer does not yet exist on a freshly spawned entity.
            ecb.Playback(em);
            ecb.Dispose();
        }

        // merge key: (source, stat, op, stackId)
        // refresh: remaining = max(old, new), magnitude = new
        // new slot when no match
        private static void ApplyStat(EntityManager em, EntityCommandBuffer ecb, StatModifierApplyEvent ev)
        {
            var target = ev.target;

            if (em.HasBuffer<StatModifierSlot>(target))
            {
                var buf = em.GetBuffer<StatModifierSlot>(target);
                for (int i = 0; i < buf.Length; i++)
                {
                    var slot = buf[i];
                    if (slot.header.source == ev.source &&
                        slot.stat          == ev.stat  &&
                        slot.op            == ev.op    &&
                        slot.header.stackId == ev.stackId)
                    {
                        buf[i] = new StatModifierSlot
                        {
                            header = new ModifierHeader
                            {
                                remaining = math.max(slot.header.remaining, ev.duration),
                                source    = ev.source,
                                stackId   = ev.stackId,
                            },
                            stat      = ev.stat,
                            op        = ev.op,
                            magnitude = ev.magnitude,
                        };
                        MarkDirty(em, target);
                        return;
                    }
                }
                // no match — add new slot directly to existing buffer
                buf.Add(new StatModifierSlot
                {
                    header = new ModifierHeader
                    {
                        remaining = ev.duration,
                        source    = ev.source,
                        stackId   = ev.stackId,
                    },
                    stat      = ev.stat,
                    op        = ev.op,
                    magnitude = ev.magnitude,
                });
            }
            else
            {
                // buffer does not exist yet — create via ECB and append the first slot
                var buf = ecb.AddBuffer<StatModifierSlot>(target);
                buf.Add(new StatModifierSlot
                {
                    header = new ModifierHeader
                    {
                        remaining = ev.duration,
                        source    = ev.source,
                        stackId   = ev.stackId,
                    },
                    stat      = ev.stat,
                    op        = ev.op,
                    magnitude = ev.magnitude,
                });
            }

            MarkDirty(em, target);
        }

        // merge key: (source, kind)
        // refresh: stackCount = min(maxStack, stackCount + countDelta), remaining = perAppDuration
        // new slot when no match
        private static void ApplyStack(EntityManager em, EntityCommandBuffer ecb, StackModifierApplyEvent ev)
        {
            var target = ev.target;

            if (em.HasBuffer<StackModifierSlot>(target))
            {
                var buf = em.GetBuffer<StackModifierSlot>(target);
                for (int i = 0; i < buf.Length; i++)
                {
                    var slot = buf[i];
                    if (slot.header.source == ev.source && slot.kind == ev.kind)
                    {
                        buf[i] = new StackModifierSlot
                        {
                            header = new ModifierHeader
                            {
                                remaining = ev.perAppDuration,
                                source    = ev.source,
                                stackId   = slot.header.stackId,
                            },
                            kind                = ev.kind,
                            stackCount          = (byte)math.min((int)slot.maxStack, (int)(slot.stackCount + ev.countDelta)),
                            maxStack            = slot.maxStack,
                            lastTriggeredStack  = slot.lastTriggeredStack,
                        };
                        // Stack buffer does not directly affect BuffStats — no MarkDirty.
                        return;
                    }
                }
                // no match — new slot
                buf.Add(new StackModifierSlot
                {
                    header = new ModifierHeader
                    {
                        remaining = ev.perAppDuration,
                        source    = ev.source,
                        stackId   = 0,
                    },
                    kind               = ev.kind,
                    stackCount         = (byte)math.min((int)ev.maxStack, (int)ev.countDelta),
                    maxStack           = ev.maxStack,
                    lastTriggeredStack = 0,
                });
            }
            else
            {
                var buf = ecb.AddBuffer<StackModifierSlot>(target);
                buf.Add(new StackModifierSlot
                {
                    header = new ModifierHeader
                    {
                        remaining = ev.perAppDuration,
                        source    = ev.source,
                        stackId   = 0,
                    },
                    kind               = ev.kind,
                    stackCount         = (byte)math.min((int)ev.maxStack, (int)ev.countDelta),
                    maxStack           = ev.maxStack,
                    lastTriggeredStack = 0,
                });
            }
            // Stack buffer does not directly affect BuffStats — no MarkDirty.
        }

        // Using EntityManager directly (not ECB) so that two ApplyStat calls in the same
        // frame to the same target are both correctly handled: em.HasComponent reflects the
        // actual state after the first add, whereas ECB would double-record AddComponent.
        // Safe here because ApplyStat is called from TryDequeue loops, not inside a query iteration.
        private static void MarkDirty(EntityManager em, Entity target)
        {
            if (!em.HasComponent<BuffStatsDirty>(target))
                em.AddComponent<BuffStatsDirty>(target); // added disabled by default (IEnableableComponent)
            em.SetComponentEnabled<BuffStatsDirty>(target, true);
        }
    }
}
