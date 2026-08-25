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
    //       - SelfBlink (boss): **방어유닛 밀집도 최대 셀 → 링 스냅 → skip**
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
            state.RequireForUpdate<FlowFieldSingleton>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var threatLookup = SystemAPI.GetBufferLookup<ThreatEntry>(isReadOnly: false);
            // boss-jjangssen unit 2 — SelfTileAoe 캐리어의 피해 풀 진영을 host 에서 도출한다.
            // 기본값이 Enemy 라 그냥 두면 **보스의 폭발이 자기 진영(적)을 때린다**.
            // Units 소유 태그를 Combat 이 RO 로 읽는 것 — 이 시스템의 DefenderUnitTag 쿼리 선례와 동일.
            var factionLookup = SystemAPI.GetComponentLookup<FactionTag>(isReadOnly: true);

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

            // SelfStatBuff(디펜더 last_stand) 채널 — blink 부재와 무관하게 필요.
            bool hasStatQ = SystemAPI.TryGetSingletonRW<StatModifierApplyEventsSingleton>(out var statRW);
            // boss-mamemo unit 3 — 꿈의 장막(GrantShield) 쓰기 대상. Units 소유 이벤트 버퍼에
            // Combat 이 append 하는 것은 ShieldCastSystem(Effects)의 선례와 같은 모양이다.
            var incomingShieldLookup = SystemAPI.GetBufferLookup<Wassup.Battle.Units.IncomingShield>(isReadOnly: false);
            // boss-mamemo unit 4 — 실드 부여 원샷 연출은 **가디언과 같은 채널**을 쓴다
            // (ShieldGrantedEvents → VfxSpawner.SpawnShieldGranted, 이미 배선돼 있다).
            // 전용 ProjectileData 를 저작하지 않는 이유: "실드가 부여됐다" 는 같은 사건이면
            // 출처와 무관하게 같은 연출이어야 하고, 그러면 저작이 0 이 된다.
            // 대가(수용): 이 채널은 position 만 실어 **아군/적 실드 연출을 못 가른다**.
            // 가르려면 필드 1개 추가가 필요하고, 그건 이 spec 밖이다.
            bool hasShieldVfxQ = SystemAPI.TryGetSingletonRW<Wassup.Battle.Effects.ShieldGrantedEventsSingleton>(out var shieldVfxRW);
            // SelfBlink(보스) 채널 — 없으면 blink payload 만 skip(HealthThreshold
            // 평가 자체는 계속 — SelfStatBuff 가 blink 없이 돌아야 함).
            bool hasBlinkQ = SystemAPI.TryGetSingletonRW<BlinkRequestEventsSingleton>(out var blinkRW);
            var ff = SystemAPI.GetSingleton<FlowFieldSingleton>();

            // rev 3 (실플레이 피드백) — blink 연출: 출발/도착 퍼프를 기존 hit-VFX
            // 채널(Combat→Presentation)로 재생. 슬롯에 베이크된 퍼프 dataIndex 사용.
            // boss-jjangssen unit 6 — 퍼프는 이 시스템이 직접 쏘지 않는다. 비행 연출이 생기면서
            // 착지 퍼프가 뷰 도착보다 먼저 터지는 desync 가 되므로, 출발/착지 재생을 브리지의
            // 비행 코루틴이 소유한다. 여기서는 "언제·어디서·어디로" 만 신호한다.
            bool hasLeapQ = SystemAPI.TryGetSingletonRW<BossLeapVisualEventsSingleton>(out var leapRW);
            NativeQueue<BossLeapVisualEvent> leapQueue = hasLeapQ ? leapRW.ValueRW.queue : default;
            // ultimate-leap unit 3 — 이탈 연출 채널. 부재면 연출만 없고 sim 시퀀스는 그대로 돈다.
            bool hasUltLeapQ = SystemAPI.TryGetSingletonRW<UltimateLeapVisualEventsSingleton>(out var ultLeapRW);
            NativeQueue<UltimateLeapVisualEvent> ultLeapQueue = hasUltLeapQ ? ultLeapRW.ValueRW.queue : default;

            // 착지 앵커 풀 = 도약 계열(SelfBlink·UltimateLeap)이 «어디로 뛸까»를 고르는 소스.
            //
            // skill-layer-foundation unit 2b — **진영별로 둘**이다. 예전엔 defender 풀 하나였고
            // 그래서 방어유닛이 이 스킬을 장착하면 **자기 편 밀집 셀로 뛰었다**. 스킬이 host 를
            // 가리지 않으려면 앵커가 caster 의 상대 진영이어야 한다.
            // 오늘 라이브 경로는 전부 적(보스) 시전이라 결과가 같다 — 동작 무변경 리팩터다.
            //
            // 지연 생성은 유지한다: 디펜더-only 판(last_stand 만 있고 도약 슬롯 없음)에서 매
            // 프레임 쿼리+배열 할당을 피한다(ecs-review MEDIUM). BossPeriodic 풀 선례.
            var defQuery = SystemAPI.QueryBuilder().WithAll<DefenderUnitTag, LocalTransform>().Build();
            var enemyQuery = SystemAPI.QueryBuilder().WithAll<AttackUnitTag, LocalTransform>().Build();
            NativeArray<int2> defCells = default, enemyCells = default;
            bool defBuilt = false, enemyBuilt = false;
                                   // *Built 를 IsCreated 로 대체 금지 — 그 진영이 전멸하면 길이 0
                                   // 배열의 IsCreated 에 프레임당 재할당 여부가 걸린다.

            // caster 의 진영을 읽는 lookup. `FactionQuery` 가 이 질문의 단일 답이다.
            var enemyTagLookup = SystemAPI.GetComponentLookup<AttackUnitTag>(isReadOnly: true);
            var defTagLookup = SystemAPI.GetComponentLookup<DefenderUnitTag>(isReadOnly: true);

            // 진동갑주 (content-3 unit 4) — SelfTileAoe 캐리어 스테이징용 ECB.
            // Playback 은 이 시스템 끝(브리지 드레인 전 materialize — AttackSystem dcCarrier 선례).
            var ecb = new EntityCommandBuffer(Allocator.Temp);

            // 2. Threshold eval + blink resolve.
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
                        if (slot.payload == Wassup.Data.DcPayloadKind.SelfStatBuff)
                        {
                            // last_stand — self 에 StatModifier(배율=magnitude, TTL=duration).
                            // duration<=0 = 영구(float.PositiveInfinity, 기존 무한 컨벤션).
                            // 채널 부재-가드: 없으면 조용히 skip(k 는 이미 전진 — 재발동 없음).
                            if (hasStatQ)
                            {
                                float ttl = slot.duration > 0f ? slot.duration : float.PositiveInfinity;
                                // op/magnitude = FromMultiplier → +% 는 Additive 버킷(squad/on-place
                                // %-buff 와 동일 스택 규칙, modifier-additive-authoring 관례 일치).
                                ModifierAuthoring.FromMultiplier(slot.magnitude, out var buffOp, out var buffMag);
                                statRW.ValueRW.queue.Enqueue(new StatModifierApplyEvent
                                {
                                    target = entity,
                                    stat = slot.buffStat,
                                    op = buffOp,
                                    magnitude = buffMag,
                                    duration = ttl,
                                    source = entity,
                                    stackId = slot.statBuffStackId,
                                    // dreamcatcher-berserker unit 1 — 최대 중첩(slot.tileRange)이
                                    // 있으면 경계를 넘을 때마다 누적된다. 0 = 기존 덮어쓰기.
                                    magnitudeCap = ModifierAuthoring.StackCap(slot.magnitude, slot.tileRange),
                                    origin = ModifierOrigin.HealthThreshold,
                                });
                            }
                        }
                        else if (slot.payload == Wassup.Data.DcPayloadKind.GrantShield)
                        {
                            // boss-mamemo unit 3 — 꿈의 장막. 경계마다 **자기에게** 실드.
                            // 이 arm 은 self 만 배선한다(bake 가 tileRange>0 조합을 거절한다) —
                            // 반경 확산은 주기 arm 의 악몽의 가호가 소유한다.
                            //
                            // 쓰기는 Units 소유 IncomingShield 버퍼 append 하나다. 병합(같은 출처
                            // max)·흡수는 DamageApplicationSystem 이 하고, 가디언 전용 생산자
                            // (ShieldCastSystem)는 건드리지 않는다.
                            //
                            // ⚠ 실드는 이 프레임의 피해를 못 막는다 — 이 시스템이
                            // [UpdateAfter(DamageApplicationSystem)] 이라 append 는 **다음 프레임**
                            // 드레인에서 슬롯이 된다. 경계를 관통한 그 히트는 이미 지나간 뒤다.
                            // magnitude>0 은 bake 가 이미 거절하지만, 주기 arm 쪽이 런타임에도
                            // 확인하므로 두 arm 의 방어를 대칭으로 맞춘다(리뷰 L6).
                            if (slot.magnitude > 0f && incomingShieldLookup.HasBuffer(entity))
                            {
                                incomingShieldLookup[entity].Add(new Wassup.Battle.Units.IncomingShield
                                {
                                    source = entity,   // 같은 출처 = max 갱신 (누적 아님)
                                    amount = slot.magnitude,
                                });
                                // 실제로 부여된 발동만 연출한다(효과 없는 연출 금지 — whip 선례).
                                if (hasShieldVfxQ)
                                    shieldVfxRW.ValueRW.queue.Enqueue(
                                        new Wassup.Battle.Effects.ShieldGrantedEvent
                                        { position = transform.ValueRO.Position });
                            }
                        }
                        else if (slot.payload == Wassup.Data.DcPayloadKind.SelfBlink)
                        {
                            // 착지 앵커 = **caster 의 상대 진영** 밀집 셀. 첫 발동 때 1회 생성.
                            var blinkAnchors = ResolveAnchorPool(
                                entity, in ff, defQuery, enemyQuery,
                                in factionLookup, in enemyTagLookup, in defTagLookup,
                                ref defCells, ref defBuilt, ref enemyCells, ref enemyBuilt);
                            if (hasBlinkQ && TryResolveBlinkDest(slot.tileRange, (int)slot.magnitude,
                                     blinkAnchors, in ff, out float3 destWorld, out _))
                            {
                                blinkRW.ValueRW.queue.Enqueue(new BlinkRequestEvent { entity = entity, destWorld = destWorld });
                                // sim 은 이번 프레임에 destWorld 로 텔레포트한다. 뷰는 브리지가
                                // fromWorld → toWorld 아치로 날리고, 퍼프도 브리지가 비행
                                // 시작/종료에 맞춰 재생한다(dataIndex < 0 = 무연출).
                                if (hasLeapQ)
                                    leapQueue.Enqueue(new BossLeapVisualEvent
                                    {
                                        entity = entity,
                                        fromWorld = transform.ValueRO.Position,
                                        toWorld = destWorld,
                                        dataIndex = slot.projectileDataIndex,
                                        slamDamage = slot.slamDamage,
                                        slamTileRange = slot.slamTileRange,
                                    });
                            }
                            // 목적지 실패(방어유닛 전멸/링 상한 초과) = skip — k 는
                            // 이미 전진(발동 소모 유지, 재발동 없음).
                        }
                        else if (slot.payload == Wassup.Data.DcPayloadKind.UltimateLeap)
                        {
                            // ultimate-leap unit 1 — 이탈 개시. 여기서 하는 일은 **착지점 고정과
                            // 상태 부착**뿐이고, 카운트다운·착지는 UltimateLeapSystem 이 굴린다.
                            // 착지 셀을 지금 고정하는 것이 계약이다(README 4): 예고는 약속이라
                            // 착지 직전 재계산하면 빨간 타일을 보고 유닛을 빼는 회피가 거짓이 된다.
                            var ultAnchors = ResolveAnchorPool(
                                entity, in ff, defQuery, enemyQuery,
                                in factionLookup, in enemyTagLookup, in defTagLookup,
                                ref defCells, ref defBuilt, ref enemyCells, ref enemyBuilt);
                            if (TryResolveBlinkDest(slot.tileRange, (int)slot.magnitude,
                                    ultAnchors, in ff, out float3 ultDest, out int2 ultCell))
                            {
                                // 잠금(LeapFlight)과 무적(UltimateLeapState)은 **함께 붙는다** —
                                // 레이어는 갈리지만 수명은 하나다(README 6).
                                ecb.AddComponent(entity, new UltimateLeapState
                                {
                                    remaining = math.max(0.01f, slot.duration),
                                    landingCell = ultCell,
                                    landingWorld = ultDest,
                                    slamDamage = slot.slamDamage,
                                    slamTileRange = math.max(0, slot.slamTileRange),
                                    projectileDataIndex = slot.projectileDataIndex,
                                });
                                ecb.AddComponent<LeapFlight>(entity);
                                // ultimate-leap unit 3 — 이탈 상승 신호. 채널 부재면 연출만 없고
                                // sim 은 그대로 돈다(기존 채널 부재-가드 규약).
                                if (hasUltLeapQ)
                                {
                                    ultLeapQueue.Enqueue(new UltimateLeapVisualEvent
                                    {
                                        entity = entity,
                                        kind = UltimateLeapVisualKind.Ascend,
                                        world = transform.ValueRO.Position,
                                        dataIndex = -1,
                                    });
                                }
                            }
                            else
                            {
                                // 목적지 실패 = skip. k 는 이미 전진 — 생존당 1회라 **재시도가 없다.**
                                // 조용히 넘기면 "궁극기가 왜 안 나왔는지" 를 영영 알 수 없다(1회성이라
                                // 재현도 안 된다). 원인은 둘뿐이다: 방어유닛 0(밀집 셀 없음) 또는
                                // 링 6칸 안에 walkable·연결 셀 없음. Burst 라 문자열 리터럴만 쓴다.
                                UnityEngine.Debug.LogWarning(
                                    "[UltimateLeap] 착지점 해석 실패로 발동 skip — 방어유닛이 없거나 밀집 셀 주변 링 안에 갈 수 있는 칸이 없다. 임계는 소모됐고 재시도는 없다.");
                            }
                        }
                        else if (slot.payload == Wassup.Data.DcPayloadKind.SelfTileAoe)
                        {
                            // 진동갑주 (content-3 unit 4) — 자기 위치 즉발 TileAoe. 캐리어
                            // entity 로 ProjectileSpawnRequest 스테이징(AttackN ProjectileToTarget
                            // dcCarrier 선례) — 브리지 드레인이 스폰 후 캐리어를 파괴한다.
                            // owner=self 라 폭발 킬은 이 유닛에 귀속(OnKill 카드와 연쇄 가능).
                            var carrier = ecb.CreateEntity();
                            ecb.AddComponent(carrier, new Projectile.ProjectileSpawnRequest
                            {
                                movement = Projectile.MovementKind.SkyFall,
                                payload = Projectile.PayloadKind.TileAoe,
                                impact = transform.ValueRO.Position,
                                damage = slot.magnitude,
                                impactTileRange = slot.tileRange,
                                flightTime = 0f,
                                dataIndex = slot.projectileDataIndex,
                                visualScale = slot.visualScale > 0f ? slot.visualScale : 1f,
                                owner = entity,
                                // 피해 풀 진영을 host 에서 도출한다. 기본값이 Enemy 라 그냥 두면
                                // **보스의 폭발이 자기 진영을 때린다**(boss-jjangssen unit 2).
                                // unit 2b — 리터럴 비교를 `FactionRelation` 로 옮겼다. 진영이 늘거나
                                // 매핑이 바뀌면 그 순수 함수 하나만 고치면 된다(EditMode 가 고정).
                                // 진영 미상은 Enemy 유지 → 기존 디펜더 경로 byte-identical.
                                targetFaction =
                                    FactionQuery.OpponentsOf(entity, in factionLookup,
                                        in enemyTagLookup, in defTagLookup) == Faction.DefenderUnit
                                        ? Projectile.ProjectileTargetFaction.Defender
                                        : Projectile.ProjectileTargetFaction.Enemy,
                            });
                            ecb.AddComponent<Projectile.ProjectileRequestCarrier>(carrier);
                        }
                        else
                        {
                            UnityEngine.Debug.LogWarning("[HealthThreshold] HealthThreshold slot fired with unhandled payload kind.");
                        }
                    }
                    slots[si] = slot;
                }
            }

            if (defBuilt) defCells.Dispose();
            if (enemyBuilt) enemyCells.Dispose();

            ecb.Playback(state.EntityManager);
            ecb.Dispose();
        }

        // boss-jjangssen unit 4 — 착지 앵커 = **방어유닛 밀집도 최대 셀**.
        //
        // 구 정책(위협 리더 근처 → 최근접 폴백)을 **교체**했다. 필드로 정책을 고르게 하지
        // 않은 이유: 구 정책의 라이브 authoring 사용처가 0이었다(나이트메어 mechanic 3개는
        // 전부 PeriodicTimer). 쓰이지 않는 분기를 위해 셀렉터를 만드는 것은 제약 8 위반이다.
        // `ThreatEntry` 와 threat drain 은 별 책임이라 그대로 남아 있다.
        //
        // OffsetDest(리더를 지나쳐 1타일)는 쓰지 않는다 — 밀집을 응징하러 뛰는 것이므로
        // 밀집 셀 자체가 desired 이고, 그 셀이 배치칸(비-walkable)이면 TryFindLandingCell 의
        // 링 탐색이 인접 walkable·연결 셀로 스냅한다(고립 포켓 착지 방지는 기존 계약).
        // boss-jjangssen unit 4 — 밀집도 판정은 셀 단위. 위치→셀 변환을 한 번만 하고 순수 함수엔
        // 셀만 넘긴다. transforms 는 변환 재료일 뿐이라 수명을 늘리지 않는다.
        // 반환 배열의 Dispose 는 **호출측 책임**(OnUpdate 끝의 `if (*Built) *Cells.Dispose()`).
        // SelfBlink·UltimateLeap 두 arm 이 같은 지연 생성을 쓰므로 한 곳에 둔다 — 세 번째 arm 이
        // 복사본을 또 만들면 `defBuilt` 를 빠뜨려 프레임당 재할당이 조용히 시작된다.
        // skill-layer-foundation unit 2b — caster 의 **상대 진영** 셀 풀을 고른다.
        //
        // 두 도약 arm 이 같은 선택을 하므로 한 곳에 둔다. 세 번째 arm 이 사본을 만들면
        // `*Built` 플래그를 빠뜨려 프레임당 재할당이 조용히 시작된다(원래 주석의 경고 계승).
        // 진영 미상이면 defender 풀로 폴백한다 — 기존 동작이고, 앵커가 없는 것보다 낫다.
        private static NativeArray<int2> ResolveAnchorPool(
            Entity caster, in FlowFieldSingleton ff,
            EntityQuery defQuery, EntityQuery enemyQuery,
            in ComponentLookup<FactionTag> factions,
            in ComponentLookup<AttackUnitTag> enemyTags,
            in ComponentLookup<DefenderUnitTag> defTags,
            ref NativeArray<int2> defCells, ref bool defBuilt,
            ref NativeArray<int2> enemyCells, ref bool enemyBuilt)
        {
            var opponents = FactionQuery.OpponentsOf(caster, in factions, in enemyTags, in defTags);
            if (opponents == Faction.EnemyUnit)
            {
                if (!enemyBuilt) { enemyCells = BuildCells(enemyQuery, in ff); enemyBuilt = true; }
                return enemyCells;
            }
            if (!defBuilt) { defCells = BuildCells(defQuery, in ff); defBuilt = true; }
            return defCells;
        }

        private static NativeArray<int2> BuildCells(EntityQuery defQuery, in FlowFieldSingleton ff)
        {
            var xf = defQuery.ToComponentDataArray<LocalTransform>(Allocator.Temp);
            var cells = new NativeArray<int2>(xf.Length, Allocator.Temp);
            for (int i = 0; i < xf.Length; i++)
                cells[i] = GridMath.WorldToCell(xf[i].Position, ff.tileSize, ff.gridSize, origin: ff.origin);
            xf.Dispose();
            return cells;
        }

        // ultimate-leap unit 1 — 착지 **셀**도 함께 돌려준다.
        // (정정: 초판 주석은 "월드→셀 역변환이 반올림 경계에서 갈릴 수 있다" 고 적었으나 **거짓**이다.
        //  `CellToWorldCenter` = `origin + cell*tileSize`, `WorldToCell` = `floor(cell + 0.5)` 라
        //  왕복은 항상 정확하다. 실제 이유는 **의존 회피**다: 셀은 뷰(예고 타일)가, 월드는 sim 이
        //  쓰는데, 한쪽에서 다른 쪽을 파생하려면 그 시스템이 없는 의존을 새로 져야 한다 —
        //  sim 은 `FlowFieldSingleton`, 뷰는 격자 파라미터. 12바이트로 의존 하나를 산다.)
        // SelfBlink 호출부는 `_` 로 버린다.
        private static bool TryResolveBlinkDest(
            int maxRingRadius, int densityRadius,
            in NativeArray<int2> defCells,
            in FlowFieldSingleton ff, out float3 destWorld, out int2 landingCell)
        {
            destWorld = default;
            landingCell = default;

            if (!DefenderDensity.TryFindDensestCell(defCells, densityRadius, ff.gridSize, out int2 desiredCell, out _))
                return false; // 방어유닛 전멸 → skip
            if (!BlinkMath.TryFindLandingCell(desiredCell, ff.DistSlot(FlowFieldSingleton.PrimarySlot), ff.gridSize, math.max(0, maxRingRadius), out int2 landing))
                return false; // 링 상한 내 착지 불가 → skip
            landingCell = landing;
            destWorld = GridMath.CellToWorldCenter(landing, ff.tileSize, 0f, origin: ff.origin);
            return true;
        }
    }
}
