using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using Wassup.Battle.Effects;
using Wassup.Battle.Movement;
using Wassup.Battle.Units;

namespace Wassup.Battle.Combat
{
    // nightmare-catcher unit 3 — HealthThreshold arm + the threat channel drain.
    // dreamcatcher-kill-and-threshold unit 1 — 개명(BossHealthThresholdSystem→
    // HealthThresholdSystem): 디펜더 last_stand(HealthThreshold×SelfStatBuff)를
    // 함께 처리하므로 더 이상 보스 전용이 아니다. faction-neutral 쿼리(BossTag/
    // DefenderUnitTag 게이트 없음)는 그대로.
    //
    // Two responsibilities, both Combat-owned:
    //  1. Drain ThreatHitEvents into the victims' ThreatEntry tables (the
    //     accumulation write — unit 1 staged the channel, this closes it).
    //     TryGetSingletonRW + HasBuffer 독립 가드라 ThreatEntry 없어도 무손상.
    //  2. Evaluate HealthThreshold slots against current Health (Units, RO) and
    //     resolve the payload:
    //       - SelfStatBuff (last_stand): self 에 StatModifier enqueue(Effects 채널).
    //         duration<=0 = 영구(float.PositiveInfinity). 디펜더는 flowfield 만
    //         있으면 되므로 blink 채널 부재와 무관하게 발동.
    //       - SelfBlink: **상대 진영 밀집도 최대 셀 → 링 스냅 → skip**
    //         (boss-jjangssen unit 4 에서 구 "위협 리더 근처" 정책을 교체했다 — 그 정책은
    //         라이브 authoring 사용처가 0이었다). position write 는 Movement 소유라
    //         BlinkRequestEventsSingleton 로 나가고, 뷰 비행 신호는 BossLeapVisualEvents 로 나간다.
    //         ⚠ ThreatEntry 는 이제 blink 목적지 계산에 쓰이지 않는다 — 아래 threat drain 은
    //         현재 소비자가 없다(spec 후속 후보에 기재).
    //
    // Runs after DamageApplicationSystem so same-tick damage is visible to the
    // threshold, and after the same-tick threat hits have been enqueued
    // (AttackSystem → DamageApplication chain).
    [BurstCompile]
    [UpdateInGroup(typeof(BattleSimGroup))]
    [UpdateAfter(typeof(DamageApplicationSystem))]
    public partial struct HealthThresholdSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            // unit 1 — ThreatEntry 게이팅 제거: 보스 없이 디펜더만 있어도 last_stand
            // 이 돌아야 한다. threat-drain 은 아래 TryGet/HasBuffer 로 독립 가드됨.
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var threatLookup = SystemAPI.GetBufferLookup<ThreatEntry>(isReadOnly: false);
            // boss-jjangssen unit 2 — SelfTileAoe 캐리어의 피해 풀 진영을 host 에서 도출한다.
            // 기본값이 Enemy 라 그냥 두면 **보스의 폭발이 자기 진영(적)을 때린다**.
            // Units 소유 태그를 Combat 이 RO 로 읽는 것 — 이 시스템의 DefenderUnitTag 쿼리 선례와 동일.

            // skill-layer-migration — 이전된 스킬은 arm 을 안 돌고 여기 실린다.
            bool hasSkillQ = SystemAPI.TryGetSingletonRW<Wassup.Battle.Skills.SkillFiredEventsSingleton>(
                out var skillFiredRW);

            // 1. Threat drain — accumulate this frame's attributed hits. A victim
            // destroyed since enqueue simply drops its events (HasBuffer guard).
            if (SystemAPI.TryGetSingletonRW<ThreatHitEventsSingleton>(out var threatEventsRW))
            {
                var queue = threatEventsRW.ValueRW.queue;
                while (queue.TryDequeue(out var evt))
                {
                    if (!threatLookup.HasBuffer(evt.victim)) continue;
                    ThreatTable.Accumulate(threatLookup[evt.victim], evt.attacker, evt.amount);
                }
            }


            // skill-layer-migration unit 3g — **착지 앵커 풀·진영 lookup·캐리어 ECB 가 여기서
            // 사라졌다.** 도약과 자기 자리 폭발이 concrete 로 갔고, 그 셋은 그 arm 들만 쓰던
            // 부속이었다. arm 만 지우고 부속을 남기면 「아무도 안 쓰는데 매 프레임 도는 코드」가
            // 되므로 같이 걷는다.
            //
            // 이 시스템에 남은 일은 이제 **경계를 넘었는지 판정하고 알리는 것**뿐이다.

            // 2. Threshold eval.
            // boss-jjangssen unit 2 — 죽은 유닛은 새 발동을 시작하지 않는다. DeadTag 는
            // DamageApplicationSystem 이 자기 OnUpdate 끝에 playback 하므로 **죽는 프레임에 이미
            // 붙어 있고**, 오버킬로 여러 경계를 한 번에 관통하면 시체가 마지막 경계에서 폭발/도약한다.
            // BossPeriodicTriggerSystem 이 같은 이유로 같은 제외를 쓴다.
            foreach (var (slotsRef, health, transform, entity) in
                     SystemAPI.Query<DynamicBuffer<DcTriggerSlot>, RefRO<Health>, RefRO<LocalTransform>>()
                              .WithNone<Wassup.Battle.Units.DeadTag>()
                              .WithEntityAccess())
            {
                var slots = slotsRef; // CS1654 회피 — 뷰 struct 로컬 복사
                for (int si = 0; si < slots.Length; si++)
                {
                    var slot = slots[si];
                    if (slot.trigger != Wassup.Data.DcTriggerKind.HealthThreshold) continue;

                    int k = slot.nextBoundaryIndex;
                    bool fired = DcTrigger.HealthThresholdEval(health.ValueRO.value, slot.maxHpRef, slot.fraction, ref k);
                    slot.nextBoundaryIndex = k;
                    if (fired)
                    {
                        if (slot.skillId != Wassup.Skills.SkillRegistry.LegacyArmId)
                        {
                            // skill-layer-migration — 이전된 스킬은 여기서 갈린다.
                            // 값 스냅샷을 실어 보내고 seam 의 디스패처가 concrete 를 부른다.
                            if (hasSkillQ)
                            {
                                skillFiredRW.ValueRW.queue.Enqueue(new Wassup.Battle.Skills.SkillFiredEvent
                                {
                                    Seam = Wassup.Battle.Skills.SkillSeam.Threshold,   // 이 드레인 지점이 실행한다
                                    Caster = entity,
                                    SkillId = slot.skillId,
                                    SlotIndex = si,
                                    FiredPosition = transform.ValueRO.Position,
                                    Target = Entity.Null,
                                    Magnitude = slot.magnitude,
                                    Duration = slot.duration,
                                    TileRange = slot.tileRange,
                                    Period = slot.period,
                                    DataIndex = slot.projectileDataIndex,
                                    Selector = (int)slot.ccKind,
                                    Speed = slot.speed,
                                    HitThreshold = slot.hitThreshold,
                                    SlamDamage = slot.slamDamage,
                                    SlamTileRange = slot.slamTileRange,
                                    StackId = slot.statBuffStackId,
                                    VisualScale = slot.visualScale,
                                    PatternIndex = slot.patternIndex,
                                    StatSelector = (int)slot.buffStat,
                                    StackSelector = (int)slot.stackKind,
                                    ProjectileMovement = (int)slot.projectileMovement,
                                    ProjectilePayload = (int)slot.projectilePayload,
                                    HazardDataIndex = slot.hazardDataIndex,
                                    // ⚠ **경계 arm 들은 층을 안 실었다**(= 무제한). 경계 자폭이
                                    // 그렇고, 여기서 host 의 공격 층을 실으면 지상만 때리는
                                    // 유닛의 자폭이 비행 적을 더는 못 때린다 — 사양 변경이다.
                                    // (불꽃 팽이가 층을 **싣는** 것과 갈린다. 그쪽 레거시가
                                    //  실었기 때문이고, 그 판단은 감지자별로 다르다.)
                                    TargetTraversalLayers = 0,
                                });
                            }
                        }
                        else
                        {
                            // skill-layer-migration unit 3g — **경계 arm 은 전부 은퇴했다.**
                            // 이 트리거로 저작되는 payload(자기 버프·실드·순간이동·궁극기
                            // 도약·자기 자리 폭발)는 예외 없이 concrete 를 갖고, 그래서
                            // 위 라우팅이 전부 가져간다. 여기 오는 것은 **concrete 가 없는
                            // payload 를 이 트리거로 저작한 경우**뿐이다 — 조용히 넘기면
                            // 「경계를 넘었는데 아무 일도 안 일어난다」가 된다.
                            UnityEngine.Debug.LogWarning(
                                "[HealthThreshold] 이 payload 는 스킬 레이어에 없다 — 경계는 소모됐고 재시도는 없다. 저작을 확인하라.");
                        }
                    }
                    slots[si] = slot;
                }
            }


        }

    }
}
