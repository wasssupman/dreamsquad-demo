using System.Collections.Generic;

namespace Wassup.Sim.Effects
{
    /// <summary>
    /// battle-sim-extraction unit 18-C/6 — 캡처 #32 · <see cref="SimPhase.ModifierTick"/>(P7).
    /// 구 `Wassup.Battle.Effects.StackModifierTickSystem` 이식. **P7 의 마지막**이다.
    ///
    /// 스택 슬롯의 `remaining` 을 깎고, 넘어선 임계를 **전부** 발화하고, 만료분을 제거한다.
    ///
    /// 임계 규칙은 <see cref="SimConfig.StackThresholdsFor"/> 에서 온다 — 구 sim 의
    /// `StackThresholdRegistry`(sim 소유 static) 자리다. **미등록 kind 는 빈 배열**이고 그건
    /// "규칙 없음 = 임계 미발동" 이라는 **정상 상태**다. 배선 누락과의 구분은 이 조회가 아니라
    /// `SimWorld` 생성자가 진다(config 없이는 월드가 안 만들어진다 — 18-A/4).
    ///
    /// ⚠ 세 출력 채널의 **소비 시점이 서로 다르다**:
    /// 스탯(P2 `ModifierApplySystem`)은 다음 틱, CC·DoT 는 18-D 가 소비자를 옮길 때 정해진다.
    /// 이 시스템은 발행만 하고 지연을 선언하지 않는다 — 지연은 phase 배치의 결과다.
    /// </summary>
    public sealed class StackModifierTickSystem
    {
        /// <summary>
        /// 스택 임계가 만든 스탯 모디파이어의 `stackId` 네임스페이스 시작점(+ <see cref="StackKind"/>).
        ///
        /// ⚠ **kind 별로 갈라야 한다.** 이 파생은 `source = 피해자 자신`이라, 배치/스킬 감속
        /// (역시 `source = target`, `stackId = 0`)과 병합 키 4축이 **전부 겹쳤다** — 병합 규칙이
        /// magnitude 덮어쓰기라 강한 배치 감속이 약한 스택 감속으로 깎였다. base 100 은
        /// 저번호(0=일반, 1=Synergy)를 피하기 위한 것이다.
        /// </summary>
        internal const int StackDerivedStackIdBase = 100;

        private readonly SimChannel<EnemyCcEvent> _ccChannel;
        private readonly SimChannel<DotApplyEvent> _dotChannel;
        private readonly SimChannel<StatModifierApplyEvent> _statChannel;

        public StackModifierTickSystem(
            SimChannel<EnemyCcEvent> ccChannel,
            SimChannel<DotApplyEvent> dotChannel,
            SimChannel<StatModifierApplyEvent> statChannel)
        {
            _ccChannel = ccChannel;
            _dotChannel = dotChannel;
            _statChannel = statChannel;
        }

        public void Run(SimWorld world)
        {
            float dt = world.DeltaTime;

            // ⚠ 구 쿼리는 `SystemAPI.Query<DynamicBuffer<StackModifierSlot>>()` — **버퍼만** 본다.
            // `StatModifierTick`(#29)이 `RefRO<ModifierStats>` 를 함께 요구하는 것과 다르다.
            // `With<ModifierStats>()` 로 좁히면 스탯 캐시 없는 대상(적 일부)이 통째로 빠진다.
            foreach (SimEntityId e in world.WithBuffer<StackModifierSlot>())
            {
                List<StackModifierSlot> slots = world.GetBuffer<StackModifierSlot>(e);

                // 역순 — swap-back 제거가 원소를 건너뛰지 않게.
                for (int i = slots.Count - 1; i >= 0; i--)
                {
                    StackModifierSlot s = slots[i];

                    // 1. 틱
                    s.header.remaining -= dt;

                    // 2. 임계 엣지 검출 — 이번 프레임에 넘어선 임계를 **전부** 발화.
                    //    4→7 점프면 5·6·7 이 모두 터진다(다중 임계 계약).
                    if (s.stackCount > s.lastTriggeredStack)
                        DispatchThresholds(world, e, ref s);

                    // 3. 만료
                    if (s.header.remaining <= 0f) RemoveAtSwapBack(slots, i);
                    else slots[i] = s;
                }
            }
        }

        /// <summary>
        /// `lastTriggeredStack &lt; atStack &lt;= stackCount` 인 규칙을 저작 순서대로 발화한다.
        /// Consume 모드는 발화 후 `atStack` 만큼 차감하고, 엣지 캐시는 **모든 차감이 끝난 뒤의**
        /// `stackCount` 로 전진한다.
        ///
        /// ⚠ **규칙이 없어도 엣지 캐시는 전진한다.** 안 그러면 매 프레임 재판정이 돌아
        /// `stackCount > lastTriggeredStack` 이 영원히 참이 된다.
        /// </summary>
        private void DispatchThresholds(SimWorld world, SimEntityId entity, ref StackModifierSlot s)
        {
            IReadOnlyList<StackThresholdRule> rules = world.Config.StackThresholdsFor(s.kind);
            if (rules == null || rules.Count == 0)
            {
                s.lastTriggeredStack = s.stackCount;
                return;
            }

            byte prevStack = s.lastTriggeredStack;
            for (int r = 0; r < rules.Count; r++)
            {
                StackThresholdRule rule = rules[r];
                if (rule.atStack <= prevStack || rule.atStack > s.stackCount) continue;

                switch (rule.derivedKind)
                {
                    case DerivedEffectKind.ApplyDot:
                        // 전용 도트 채널은 CC 적용을 안 거치므로 **보스 면역과 무관하게 통과**한다
                        // — "스택 임계 DoT 는 보스에게도 통한다"(boss-jjangssen unit 3) 유지.
                        _dotChannel.Enqueue(new DotApplyEvent
                        {
                            target = entity,
                            effect = new DotEffect
                            {
                                origin = DotOrigin.Stack,
                                element = DotElementMap.FromStack(s.kind),
                                scalar = rule.magnitude,
                                tickInterval = rule.tickInterval,
                                tickTimer = rule.tickInterval,   // 첫 틱 즉발(add-path 규약)
                                remainingTime = rule.duration,
                            },
                        });
                        break;

                    case DerivedEffectKind.ApplyStun:
                        // 출처 축이 은퇴해 보스는 스택 임계 스턴에도 면역이다(소비자가 거절).
                        _ccChannel.Enqueue(new EnemyCcEvent
                        {
                            target = entity,
                            effect = new CcEffect
                            {
                                kind = CcKind.Stun,
                                remainingTime = rule.magnitude,   // ApplyStun 은 magnitude 가 지속 시간
                            },
                        });
                        break;

                    case DerivedEffectKind.ApplyStat:
                        _statChannel.Enqueue(new StatModifierApplyEvent
                        {
                            target = entity,
                            stat = rule.stat,
                            op = rule.op,
                            magnitude = rule.magnitude,
                            duration = rule.duration,
                            source = entity,
                            stackId = (ushort)(StackDerivedStackIdBase + (int)s.kind),
                            // 야근 번아웃만 전용 origin 으로 승격 — 상태FX 표시가 다른 Stack
                            // 파생과 안 섞이게. 범용 통합은 별도 과제다.
                            origin = s.kind == StackKind.Fatigue
                                ? ModifierOrigin.Burnout
                                : ModifierOrigin.Stack,
                        });
                        break;
                }

                if (rule.mode == ThresholdMode.Consume)
                    s.stackCount = (byte)SimMath.Max(0, s.stackCount - rule.atStack);
            }

            s.lastTriggeredStack = s.stackCount;
        }

        private static void RemoveAtSwapBack<T>(List<T> list, int index)
        {
            int last = list.Count - 1;
            list[index] = list[last];
            list.RemoveAt(last);
        }
    }
}
