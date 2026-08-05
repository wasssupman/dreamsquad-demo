using System.Collections.Generic;

namespace Wassup.Sim.Effects
{
    /// <summary>
    /// battle-sim-extraction unit 18-C/3 — 캡처 #9 · <see cref="SimPhase.Intake"/>(P2).
    /// 구 `Wassup.Battle.Effects.ModifierApplySystem` 이식.
    ///
    /// 두 채널을 드레인해 슬롯 버퍼를 갱신한다. **스탯 먼저, 그 다음 스택** — 순서가 계약이다
    /// (스탯 파생이 같은 틱의 스택 처리에 섞이지 않는다).
    ///
    /// **채널 fan-in 최대 지점**이다 — `StatModifierApply` 10 생산자 + `StackModifierApply` 3.
    /// 26쌍 중 13이 여기로 들어온다. 그중 같은틱/1틱-지연의 갈림은 이 시스템이 아니라
    /// **생산자의 phase 가 P2 보다 앞이냐 뒤냐**가 정한다(<see cref="SimChannel{T}"/> 주석).
    ///
    /// ⚠ **구 sim 의 ECB 우회를 그대로 옮겼다.** 구 코드는 버퍼 신설을 ECB 가 아니라
    /// `EntityManager` 로 **즉시** 했다 — ECB 로 하면 같은 드레인 안의 두 번째 이벤트가
    /// AddBuffer 를 또 기록해 playback 이 첫 슬롯을 덮어썼다(마지막만 생존). 신 sim 의
    /// <see cref="SimWorld.AddBuffer{T}"/> 도 즉시이고 기존 리스트를 돌려주므로 그 함정이
    /// 구조적으로 사라지지만, **분기 모양은 보존**한다 — 되살아나면 조용히 슬롯이 사라진다.
    /// </summary>
    public sealed class ModifierApplySystem
    {
        private readonly SimChannel<StatModifierApplyEvent> _statChannel;
        private readonly SimChannel<StackModifierApplyEvent> _stackChannel;

        public ModifierApplySystem(
            SimChannel<StatModifierApplyEvent> statChannel,
            SimChannel<StackModifierApplyEvent> stackChannel)
        {
            _statChannel = statChannel;
            _stackChannel = stackChannel;
        }

        public void Run(SimWorld world)
        {
            // 두 채널은 서로 다른 인스턴스라 Drain 의 재사용 버퍼가 겹치지 않는다.
            // (같은 채널을 두 번 드레인하면 두 번째가 첫 리스트를 무효화한다 — 그럴 일은 없다.)
            List<StatModifierApplyEvent> stats = _statChannel.Drain();
            for (int i = 0; i < stats.Count; i++) ApplyStat(world, stats[i]);

            List<StackModifierApplyEvent> stacks = _stackChannel.Drain();
            for (int i = 0; i < stacks.Count; i++) ApplyStack(world, stacks[i]);
        }

        /// <summary>
        /// 병합 키 = **`(source, stat, op, stackId)` 4축**.
        /// refresh 시 `remaining = max(old, new)` · `magnitude = 새 값` · `origin = 새 값`.
        /// 일치가 없으면 새 슬롯.
        ///
        /// ⚠ `remaining` 이 `max` 인 것은 **짧은 재적용이 긴 버프를 깎지 않게** 하려는 것이다
        /// (`ModifierFrameworkTests` Test 1 이 박제). 스택 쪽은 반대로 덮어쓴다 — 비대칭이 계약이다.
        /// </summary>
        private static void ApplyStat(SimWorld world, in StatModifierApplyEvent ev)
        {
            SimEntityId target = ev.target;
            if (!world.Exists(target)) return;   // 발행 후 파괴된 대상 — 조용히 버린다

            List<StatModifierSlot> buf = world.GetBuffer<StatModifierSlot>(target);
            if (buf != null)
            {
                for (int i = 0; i < buf.Count; i++)
                {
                    StatModifierSlot slot = buf[i];
                    if (slot.header.source == ev.source &&
                        slot.stat == ev.stat &&
                        slot.op == ev.op &&
                        slot.header.stackId == ev.stackId)
                    {
                        buf[i] = new StatModifierSlot
                        {
                            header = new ModifierHeader
                            {
                                remaining = SimMath.Max(slot.header.remaining, ev.duration),
                                source = ev.source,
                                stackId = ev.stackId,
                                origin = ev.origin,
                            },
                            stat = ev.stat,
                            op = ev.op,
                            magnitude = ev.magnitude,
                        };
                        MarkDirty(world, target);
                        return;
                    }
                }
                buf.Add(NewStatSlot(ev));
            }
            else
            {
                // 버퍼 신설 — 즉시(위 ⚠ 참조). 같은 드레인의 다음 이벤트는 위 분기를 탄다.
                world.AddBuffer<StatModifierSlot>(target).Add(NewStatSlot(ev));
            }

            MarkDirty(world, target);
        }

        private static StatModifierSlot NewStatSlot(in StatModifierApplyEvent ev) => new StatModifierSlot
        {
            header = new ModifierHeader
            {
                remaining = ev.duration,
                source = ev.source,
                stackId = ev.stackId,
                origin = ev.origin,
            },
            stat = ev.stat,
            op = ev.op,
            magnitude = ev.magnitude,
        };

        /// <summary>
        /// 병합 키 = **`(source, kind)` 2축**(스탯의 4축과 다르다).
        /// refresh 시 `stackCount = min(슬롯의 maxStack, 현재 + countDelta)` ·
        /// `remaining = perAppDuration`(**덮어쓰기** — `max` 아님).
        ///
        /// ⚠ 세 가지 보존 항목이 눈에 안 띈다:
        /// ① cap 은 **슬롯의 `maxStack`** 을 쓴다(이벤트의 값이 아니다) — 생산자가 다른 cap 을
        ///    보내도 기존 슬롯이 이긴다. ② `stackId` 는 슬롯 것을 **유지**한다.
        /// ③ `lastTriggeredStack` 도 유지한다 — 리셋하면 임계가 매 부착마다 재발화한다.
        ///
        /// **MarkDirty 를 부르지 않는다** — 스택 버퍼는 `ModifierStats` 에 직접 기여하지 않는다.
        /// 스택이 스탯에 영향을 주는 경로는 임계 파생(P7 `StackModifierTick` → 스탯 채널)뿐이다.
        /// </summary>
        private static void ApplyStack(SimWorld world, in StackModifierApplyEvent ev)
        {
            SimEntityId target = ev.target;
            if (!world.Exists(target)) return;

            List<StackModifierSlot> buf = world.GetBuffer<StackModifierSlot>(target);
            if (buf != null)
            {
                for (int i = 0; i < buf.Count; i++)
                {
                    StackModifierSlot slot = buf[i];
                    if (slot.header.source == ev.source && slot.kind == ev.kind)
                    {
                        buf[i] = new StackModifierSlot
                        {
                            header = new ModifierHeader
                            {
                                remaining = ev.perAppDuration,
                                source = ev.source,
                                stackId = slot.header.stackId,
                                // origin 미설정 = Unspecified. 구 sim 과 동일(의도).
                            },
                            kind = ev.kind,
                            stackCount = (byte)SimMath.Min(slot.maxStack, slot.stackCount + ev.countDelta),
                            maxStack = slot.maxStack,
                            lastTriggeredStack = slot.lastTriggeredStack,
                        };
                        return;
                    }
                }
                buf.Add(NewStackSlot(ev));
            }
            else
            {
                world.AddBuffer<StackModifierSlot>(target).Add(NewStackSlot(ev));
            }
        }

        private static StackModifierSlot NewStackSlot(in StackModifierApplyEvent ev) => new StackModifierSlot
        {
            header = new ModifierHeader
            {
                remaining = ev.perAppDuration,
                source = ev.source,
                stackId = 0,
            },
            kind = ev.kind,
            stackCount = (byte)SimMath.Min(ev.maxStack, ev.countDelta),
            maxStack = ev.maxStack,
            lastTriggeredStack = 0,
        };

        /// <summary>
        /// 구 sim 의 `ModifierStatsDirty` 는 `IEnableableComponent` 라 **3상태**였다
        /// (부재 / 존재+비활성 / 존재+활성). 신 sim 은 **존재 = dirty** 2상태로 접는다(18-A 결정).
        ///
        /// 여기서는 손실이 없다 — 구 코드도 `AddComponent` 직후 무조건 `SetComponentEnabled(true)`
        /// 라 "부착과 동시에 활성" 이 유일한 결과였다. 접힘이 실제로 눈에 띄는 곳은
        /// `StatModifierTickSystem` 의 만료 경로이고, 그 논증은 거기 주석이 진다.
        /// </summary>
        private static void MarkDirty(SimWorld world, SimEntityId target)
            => world.Set(target, default(ModifierStatsDirty));
    }
}
