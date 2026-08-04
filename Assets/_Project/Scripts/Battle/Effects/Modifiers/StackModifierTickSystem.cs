// Spec unit 4 (modifier-framework-and-healer): tick StackModifierSlot remaining,
// detect edge threshold crossings (multi-threshold: all crossed thresholds fire),
// dispatch derived effects to EnemyCcEvents / StatModifierApplyEvents (1-frame delay),
// and remove expired slots.
// Not Burst-compiled: StackThresholdRegistry uses a managed Dictionary.
// (battle-sim-extraction unit 11 머지 2 — 규칙 조회가 BattleBridge 에서 sim 소유 레지스트리로
//  뒤집혔다. Bridge 는 저작 SO 를 등록만 하고, sim 은 Bridge 를 모른다.)
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Wassup.Data;

namespace Wassup.Battle.Effects
{
    [UpdateInGroup(typeof(BattleSimGroup))]
    [UpdateAfter(typeof(ModifierStatsAggregateSystem))]
    public partial struct StackModifierTickSystem : ISystem
    {
        // 스택 임계가 만든 스탯 모디파이어의 stackId 네임스페이스 시작점(+ StackKind).
        internal const int StackDerivedStackIdBase = 100;


        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<EnemyCcEventsSingleton>();
            state.RequireForUpdate<DotApplyEventsSingleton>();
            state.RequireForUpdate<StatModifierApplyEventsSingleton>();
        }

        public void OnUpdate(ref SystemState state)
        {
            float dt = SystemAPI.Time.DeltaTime;
            var ccQ   = SystemAPI.GetSingleton<EnemyCcEventsSingleton>().queue;
            var dotQ  = SystemAPI.GetSingleton<DotApplyEventsSingleton>().queue;
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
                            dotQ,
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
            NativeQueue<DotApplyEvent> dotQ,
            NativeQueue<StatModifierApplyEvent> statQ)
        {
            ThresholdRule[] rules = StackThresholdRegistry.Get(kind);
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
                        // dot-effect-extraction unit 0 — 지속 피해는 전용 채널·전용 버퍼로 간다.
                        dotQ.Enqueue(new DotApplyEvent
                        {
                            target = entity,
                            // 전용 도트 채널은 CcApplySystem 을 안 거치므로 보스 면역과 무관하게
                            // 통과한다 — boss-jjangssen unit 3 의 "스택 임계 DoT 는 통한다" 유지.
                            effect = new DotEffect
                            {
                                // 병합 키(origin·element) 와 오라 소스(element)를 실어 보낸다.
                                origin        = DotOrigin.Stack,
                                element       = DotElementMap.FromStack(kind),
                                // tickInterval 0 = 연속(scalar 는 DPS) / >0 = 이산(scalar 는 틱당 피해).
                                // 0 이면 매 프레임 지급이라 데미지 숫자가 초당 수십 번 튄다.
                                scalar        = rule.magnitude,
                                tickInterval  = rule.tickInterval,
                                tickTimer     = rule.tickInterval, // 첫 틱 즉발(add-path 규약)
                                remainingTime = rule.duration,
                            }
                        });
                        break;

                    case DerivedEffectKind.ApplyStun:
                        ccQ.Enqueue(new EnemyCcEvent
                        {
                            target = entity,
                            // boss-jjangssen unit 8 — 여기가 출처 축의 유일한 생산자였다. 축이
                            // 은퇴하면서 보스는 스택 임계 스턴에도 면역이다(CcApplySystem 이 거절).
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
                            // dreamcatcher-content-3 unit 6 — 슬롯 분리. 이 파생은 source=피해자
                            // 자신이라 BattleBridge.EnqueueMoveSpeedMul(배치/스킬 감속: source=target,
                            // stackId=0)과 (source,stat,op,stackId) 4개가 전부 겹쳤다 — 병합 규칙이
                            // magnitude 덮어쓰기라 강한 배치 감속이 약한 스택 감속으로 깎였다.
                            // kind 별 전용 id 로 갈라 파생끼리도 원소별 독립. base 100 은 저번호
                            // (0=일반, 1=Synergy)를 피하기 위한 것.
                            stackId   = (ushort)(StackDerivedStackIdBase + (int)kind),
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
