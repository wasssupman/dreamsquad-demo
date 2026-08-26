using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using Wassup.Battle.Effects;
using Wassup.Battle.Movement;
using Wassup.Battle.Units;
using Wassup.Battle.Combat.Projectile;

namespace Wassup.Battle.Combat
{
    // nightmare-catcher unit 2 — PeriodicTimer × AreaBarrage arm. Gated on
    // DcTriggerSlot buffer presence only (faction-neutral by construction —
    // no BossTag/DefenderUnitTag in the gate, spec unit 4): any slot carrier
    // with a PeriodicTimer slot ticks here.
    //
    // ⚠ dreamcatcher-content-4 unit 0 — **방어유닛 카드 슬롯도 여기서 돈다.** 예전 주석은
    // "디펜더 카드는 periodSeconds 가 0 이라 건너뛴다" 였는데, 그건 카드 bake 가 그 값을
    // 안 실어 보내서 생긴 **우연**이었다(조용한 무발동). 이제 bake 가 싣고 <=0 은 loud
    // 거절한다 — 이 시스템의 진영 중립성이 «설계» 에서 «실제» 가 된 지점이다.
    //
    // Fire = one SkyFall×TileAoe carrier request into the existing projectile
    // drain (dc-trigger contract 6: the slot owner's own attack may stage a
    // request the same frame, so a dedicated carrier entity is required).
    // Orthogonal to the basic attack by construction: nothing here touches
    // AttackState / AiState / movement (계약 4).
    //
    // nightmare-whip-aura unit 1 — second payload arm on the same tick:
    // AllyMoveSpeedAura pulses a MoveSpeedMul modifier (TTL) onto same-faction
    // units in range via StatModifierApplyEvents; release is TTL expiry only.
    // on-place-skill-rework unit 0 — 두 번째 트리거 축: `OnPlace`(방어유닛 배치). 브리지가
    // 배치 확정 시 `JustDeployed` 태그를 붙이고 여기서 그 프레임에 슬롯을 발화·태그 제거한다.
    // payload arm 은 PeriodicTimer 와 **공유**한다 — 그게 이 시스템에 얹은 이유다(브리지에
    // 실행부를 두면 `EmitProjectilePattern` arm 의 세 번째 사본이 된다).
    //
    // ⚠ 순서 계약: `ProjectileEmitterSystem` 이 `[UpdateAfter(this)]` 라 패턴은 같은 프레임에
    // 나가고, `AreaTaunt`(unit 4)가 같은 틱에 어그로를 붙이려면 `AggroStateSystem` 보다 앞이어야
    // 한다. 속성이 없으면 1프레임 지연이 빌드마다 달라진다.
    [BurstCompile]
    [UpdateInGroup(typeof(BattleSimGroup))]
    // battle-sim-extraction M0 unit 0 — 순서 박제. **현행 유효 순서를 고정할 뿐 고치지 않는다**
    //   (재배치 판단은 M1 설계의 몫). 근거: docs/spec/battle-sim-extraction/order-capture.md
    //   모디파이어 이벤트 생산자 중 소비자보다 **앞**에 있는 셋 중 하나(같은 프레임 반영).
    [UpdateBefore(typeof(Wassup.Battle.Effects.ModifierApplySystem))]
    [UpdateBefore(typeof(Wassup.Battle.Effects.AggroStateSystem))]
    public partial struct BossPeriodicTriggerSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<DcTriggerSlot>();
            state.RequireForUpdate<FlowFieldSingleton>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            // BattleSimGroup dt — TimeManager Battle-domain scaled (slomo 포함).
            float dt = SystemAPI.Time.DeltaTime;
            // dreamcatcher-content-4 unit 3 — 궤도 화염구가 캐리어 entity 를 만든다(구조 변경).
            // 이 시스템의 다른 arm 들은 큐/버퍼만 만져서 여태 ECB 가 없었다. 슬롯 루프 도중
            // 구조 변경을 즉시 하면 순회 중인 버퍼 뷰가 무효화되므로 ECB 로 미룬다.
            var ecb = new EntityCommandBuffer(Allocator.Temp);

            // skill-layer-foundation unit 5 — 이전된 스킬은 arm 을 안 돌고 여기 실린다.
            bool hasSkillQ = SystemAPI.TryGetSingletonRW<Wassup.Battle.Skills.SkillFiredEventsSingleton>(
                out var skillFiredRW);

            // skill-layer-migration unit 3g — **채널 참조·풀·lookup 이 여기서 사라졌다.**
            // 스탯 이벤트·히트 VFX·CC·실드 버퍼·도발 큐·진영 lookup 3종·어그로 용량·경로 상태 —
            // 전부 은퇴한 arm 들만 쓰던 부속이다. arm 만 지우고 남기면 「아무도 안 쓰는데 매
            // 프레임 조회되는 싱글턴/lookup」이 되므로 같이 걷는다.
            //
            // 이 시스템에 남은 일은 **주기가 찼는지 세고 알리는 것**뿐이다.

            foreach (var (slotsRef, entity) in
                     SystemAPI.Query<DynamicBuffer<DcTriggerSlot>>()
                              .WithNone<Wassup.Battle.Units.DeadTag>()
                              .WithEntityAccess())
            {
                // 죽은 유닛은 새 발동을 시작하지 않는다. DeadTag 는 DamageApplicationSystem 이
                // 붙이고 UnitLifecycleSystem 이 같은 프레임에 파괴하지만, 그 사이에 이 시스템이
                // 끼면 시체가 한 번 더 스킬을 쓴다. 시스템 순서(UpdateAfter)로 가리는 대신
                // 규칙으로 표현한다 — 이미 시작된 버스트는 emitter 가 완주시킨다
                // (combat-action-lock 의 "START 는 막고 RESOLVE 는 완료" 와 같은 결).
                // foreach 변수는 readonly — DynamicBuffer 는 뷰 struct 라 로컬
                // 복사가 같은 버퍼 메모리를 가리킨다(CS1654 회피 관용구).
                var slots = slotsRef;
                // on-place-skill-rework unit 0 — 배치 사건은 엔티티 단위라 슬롯 루프 밖에서
                // 1회만 묻는다. 태그 제거는 아래 별도 패스가 ECB 로 한다(버퍼 순회 중 구조 변경 금지).
                bool justDeployed = SystemAPI.HasComponent<JustDeployed>(entity);
                for (int si = 0; si < slots.Length; si++)
                {
                    var slot = slots[si];

                    // 트리거별 발화 판정. **payload arm 은 아래에서 공유**한다 — 트리거는
                    // "언제" 만 답하고 "무엇을" 은 payload 소유다.
                    bool fired;
                    if (slot.trigger == Wassup.Data.DcTriggerKind.PeriodicTimer)
                    {
                        float elapsed = slot.elapsed;
                        fired = DcTrigger.PeriodicTick(ref elapsed, dt, slot.periodSeconds);
                        slot.elapsed = elapsed;
                    }
                    else if (slot.trigger == Wassup.Data.DcTriggerKind.OnPlace)
                    {
                        // 1회성. 재무장은 브리지가 태그를 다시 붙일 때만(재배치).
                        fired = justDeployed;
                    }
                    else continue;

                    if (fired)
                    {
                        if (slot.skillId != Wassup.Skills.SkillRegistry.LegacyArmId)
                        {
                            if (hasSkillQ && SystemAPI.HasComponent<LocalTransform>(entity))
                            {
                                skillFiredRW.ValueRW.queue.Enqueue(new Wassup.Battle.Skills.SkillFiredEvent
                                {
                                    Seam = Wassup.Battle.Skills.SkillSeam.Periodic,   // 이 드레인 지점이 실행한다
                                    Caster = entity,
                                    SkillId = slot.skillId,
                                    SlotIndex = si,
                                    FiredPosition = SystemAPI.GetComponent<LocalTransform>(entity).Position,
                                    Target = Entity.Null,
                                    Magnitude = slot.magnitude,
                                    Duration = slot.duration,
                                    TileRange = slot.tileRange,
                                    Period = slot.period,
                                    DataIndex = slot.projectileDataIndex,
                                    Selector = (int)slot.ccKind,
                                    // unit 5b — 실드 캐스트의 세 축(다른 payload 는 0 이라 무해).
                                    // ⚠ 필터는 `Selector`(=ccKind)와 **다른 축**이다(리뷰 M-3).
                                    Count = slot.shieldTargetCount,
                                    IncludesSelf = slot.shieldIncludesSelf,
                                    Selector2 = slot.shieldFilter,
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
                                    // killer 사양 스냅샷 — 어댑터가 재질의하지 않는다.
                                    TargetTraversalLayers = SystemAPI.HasComponent<AttackState>(entity)
                                        ? SystemAPI.GetComponent<AttackState>(entity).targetTraversalLayers
                                        : (byte)0,
                                });
                            }
                        }
                        else
                        {
                            // skill-layer-migration unit 3g — **주기 arm 은 전부 은퇴했다.**
                            // 이동속도 오라·자장가·실드·집단 도발·궤도 화염구가 concrete 로 갔고,
                            // 그 다섯이 이 시스템이 갖고 있던 payload 전부였다.
                            // 여기 오는 것은 **concrete 가 없는 payload 를 주기로 저작한 경우**뿐
                            // 이다 — 조용히 넘기면 「주기는 도는데 아무 일도 안 일어난다」가 된다.
                            UnityEngine.Debug.LogWarning(
                                "[BossPeriodicTrigger] 이 payload 는 스킬 레이어에 없다 — 이번 pulse 는 소모됐다. 저작을 확인하라.");
                        }
                    }
                    slots[si] = slot;
                }
            }

            // on-place-skill-rework unit 0 — 배치 태그는 **1프레임**이다. 슬롯 유무와 무관하게
            // 이번 업데이트에서 전부 걷는다(슬롯이 없는 유닛에 남으면 다음 배치 사건과 섞인다).
            // 브리지는 `DcTriggerSlot` 버퍼가 있는 유닛에만 태그를 붙이므로, 태그가 존재하는
            // 프레임엔 `RequireForUpdate<DcTriggerSlot>` 이 항상 만족돼 이 패스가 반드시 돈다.
            // ⚠ ECB 인 이유: 위 foreach 가 아직 살아 있는 쿼리 이터레이션이다.
            // (태그는 zero-size 라 `RefRO<JustDeployed>` 로 순회할 수 없다 — 쿼리 일괄 제거.)
            var justDeployedQuery = SystemAPI.QueryBuilder().WithAll<JustDeployed>().Build();
            if (!justDeployedQuery.IsEmpty)
                ecb.RemoveComponent<JustDeployed>(justDeployedQuery, EntityQueryCaptureMode.AtPlayback);

            ecb.Playback(state.EntityManager);
            ecb.Dispose();

        }

    }
}
