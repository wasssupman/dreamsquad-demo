// Spec unit 2 (modifier-framework-and-healer): drain StatModifierApplyEvents + StackModifierApplyEvents,
// update DynamicBuffers, and mark ModifierStatsDirty.
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Wassup.Battle.Units;

namespace Wassup.Battle.Effects
{
    [UpdateInGroup(typeof(BattleSimGroup))]
    [UpdateBefore(typeof(StatModifierTickSystem))]
    // battle-sim-extraction M0 unit 0 — 순서 박제. **현행 유효 순서를 고정할 뿐 고치지 않는다**
    //   (재배치 판단은 M1 설계의 몫). 근거: docs/spec/battle-sim-extraction/order-capture.md
    //   ⚠ **이 핀이 모디파이어 클러스터 전체의 1프레임 지연을 고정한다.** 생산자 11개 중
    //   8개가 이 시스템보다 **뒤**에 있어(공격·피해·착탄·임계 등) 그들의 모디파이어는 다음
    //   프레임에 반영된다. 이 시스템이 뒤로 밀리면 그 8개가 조용히 같은 프레임 반영으로
    //   바뀐다 — MovementSystem 앞에 묶어 이동 이후 블록 전체와의 관계를 고정한다.
    [UpdateBefore(typeof(Wassup.Battle.Movement.MovementSystem))]
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
        //
        // dreamcatcher-berserker unit 0 — ev.magnitudeCap > 0 이면 magnitude 가 **덮어쓰기가
        // 아니라 누적**이 된다(min(cap, 기존 + 새 값)). 광란(공격마다 공속이 쌓임)이 서는 축.
        // 상한은 magnitude 만 막고 **remaining 은 막지 않는다** — 최대 중첩에 도달해도 매
        // 발동이 지속을 갱신해야 한다. 이 둘을 같이 막으면 「가장 뜨거운 지점에서 버프가 스스로
        // 꺼지는」 결함이 생긴다(스택 임계 방식을 안 쓴 이유와 같은 함정).
        //
        // 누적을 여기(병합)에 두고 생산자에 두지 않는 이유: 생산자가 현재값을 읽으려면 Effects
        // 소유 버퍼를 Combat/Units 이 들여다봐야 하고, 읽은 시점과 병합 시점이 벌어진다.
        private static void ApplyStat(EntityManager em, EntityCommandBuffer ecb, StatModifierApplyEvent ev)
        {
            var target = ev.target;
            if (!em.Exists(target))
                return;
            if (em.HasComponent<StructureTag>(target))
                return;

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
                                origin    = ev.origin,
                            },
                            stat      = ev.stat,
                            op        = ev.op,
                            // 상한이 있을 때만 누적. 슬롯을 새로 만드는 아래 두 경로는 기존값이
                            // 0 이고 상한은 항상 1회분 이상이라(상한 = 1회분 × 최대 중첩) 클램프가
                            // 무의미해 여기 한 곳에만 둔다.
                            magnitude = ev.magnitudeCap > 0f
                                ? math.min(ev.magnitudeCap, slot.magnitude + ev.magnitude)
                                : ev.magnitude,
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
                        origin    = ev.origin,
                    },
                    stat      = ev.stat,
                    op        = ev.op,
                    magnitude = ev.magnitude,
                });
            }
            else
            {
                // Buffer does not exist yet — create it via EntityManager (immediate),
                // not ECB. With ECB, a second event for the same bufferless target in
                // the same drain loop would AddBuffer again and the playback would
                // overwrite the first slot (only the last survives). Immediate creation
                // means subsequent events take the HasBuffer path and append correctly.
                // Same rationale as MarkDirty using em directly (see note below).
                var buf = em.AddBuffer<StatModifierSlot>(target);
                buf.Add(new StatModifierSlot
                {
                    header = new ModifierHeader
                    {
                        remaining = ev.duration,
                        source    = ev.source,
                        stackId   = ev.stackId,
                        origin    = ev.origin,
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
        // NOTE(dreamcatcher-empower-aura): StackModifierApplyEvent 에는 origin 이 없어 stack 슬롯의
        // header.origin 은 Unspecified 로 둔다(의도). 스택은 스탯 오라 판정 대상이 아니다.
        private static void ApplyStack(EntityManager em, EntityCommandBuffer ecb, StackModifierApplyEvent ev)
        {
            var target = ev.target;
            if (!em.Exists(target))
                return;
            if (em.HasComponent<StructureTag>(target))
                return;

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
                        // Stack buffer does not directly affect ModifierStats — no MarkDirty.
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
                // Immediate creation (not ECB) — same overwrite-avoidance rationale as
                // ApplyStat's bufferless path.
                var buf = em.AddBuffer<StackModifierSlot>(target);
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
            // Stack buffer does not directly affect ModifierStats — no MarkDirty.
        }

        // Using EntityManager directly (not ECB) so that two ApplyStat calls in the same
        // frame to the same target are both correctly handled: em.HasComponent reflects the
        // actual state after the first add, whereas ECB would double-record AddComponent.
        // Safe here because ApplyStat is called from TryDequeue loops, not inside a query iteration.
        private static void MarkDirty(EntityManager em, Entity target)
        {
            if (!em.HasComponent<ModifierStatsDirty>(target))
                em.AddComponent<ModifierStatsDirty>(target); // added disabled by default (IEnableableComponent)
            em.SetComponentEnabled<ModifierStatsDirty>(target, true);
        }
    }
}
