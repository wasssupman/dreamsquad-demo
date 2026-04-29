# Unified Attacker Loop (회귀 게이트)

**작업 구분**: 0

## 목적

`AttackSystem.cs` 의 두 attacker loop (defender / enemy) 를 단일 loop 로 통합. attacker tag 분기를 `ComponentLookup.HasComponent` 기반 분기로 환원. 동작 변화 0 (회귀 게이트).

## 변경 대상

- Modify: `Assets/_Project/Scripts/Battle/Combat/AttackSystem.cs`
- Add: `Assets/_Project/Tests/EditMode/AttackSystemUnifiedLoopTests.cs` (회귀 검증)

## 구현 설계

### 두 loop 차이 분석 (통합 환원)

| 차이점 | Defender loop | Enemy loop | 통합 후 처리 |
|---|---|---|---|
| Attacker query | `WithAll<DefenderUnitTag> WithNone<PendingDeployment>` | `WithAll<AttackUnitTag>` | 단일 `WithAll<AttackState, LocalTransform> WithNone<PendingDeployment>` (적은 PendingDeployment 미부착이라 영향 0) |
| Cooldown tick | 동일 | 동일 | 변경 없음 |
| Target 선정 | mask 필터 + DistanceSqToTarget | 동일 | 변경 없음 |
| AttackEvent enqueue | DefenderAttackEvent | 없음 | `defenderTagLookup.HasComponent(attackerEntity)` 분기 |
| Damage scaling | DamageBoost / Synergy lookup | 1.0f (raw) | `damageBoostLookup.HasComponent` 등 lookup — 미보유 시 1.0f (이미 그렇게 되어 있음) |
| Cooldown reduction | CooldownReduction lookup | 1.0f | `cooldownReductionLookup.HasComponent` 분기 (이미) |
| Projectile vs melee | ProjectileRef lookup | 항상 melee | `projectileRefLookup.HasComponent` 분기 — 적은 ProjectileRef 미부착이라 자동 melee 분기 |
| AoE | attackTargetCount 처리 | 단일 target | 모든 attacker 가 attackTargetCount 보유 (적도 1 로 명시됨, 본 spec 의 destructible-blocking-hazards Unit 1) → desiredCount=1 fast path 가 적/멀티 hit 안 하는 attacker 모두 커버 |
| Knockback CC | DefenderCcData lookup | 없음 | `defenderCcLookup.HasComponent` 분기 — 적은 미부착이라 자동 skip |

→ **모든 분기가 이미 lookup 기반**. enemy loop 가 별도였던 유일한 이유 = `WithAll<AttackUnitTag>` 의 attacker tag 분리. tag 통합 시 자연 합류.

### 통합 코드 골격

```csharp
[BurstCompile]
public void OnUpdate(ref SystemState state)
{
    float dt = SystemAPI.Time.DeltaTime;

    // Target 후보 풀 (이미 통합됨, destructible-blocking-hazards Unit 2)
    var targetCandidatesQuery = SystemAPI.QueryBuilder()
        .WithAll<FactionTag, Health, LocalTransform>()
        .WithNone<PendingDeployment>()
        .WithNone<DeadTag>()
        .Build();
    var targetEntities    = targetCandidatesQuery.ToEntityArray(Allocator.Temp);
    var targetTransforms  = targetCandidatesQuery.ToComponentDataArray<LocalTransform>(Allocator.Temp);
    var targetFactions    = targetCandidatesQuery.ToComponentDataArray<FactionTag>(Allocator.Temp);

    var ecb = new EntityCommandBuffer(Allocator.Temp);

    // Lookup 들 (전부 read-only)
    var damageBoostLookup        = SystemAPI.GetComponentLookup<DamageBoost>(isReadOnly: true);
    var cooldownReductionLookup  = SystemAPI.GetComponentLookup<CooldownReduction>(isReadOnly: true);
    var synergyLookup            = SystemAPI.GetComponentLookup<SynergyBuff>(isReadOnly: true);
    var projectileRefLookup      = SystemAPI.GetComponentLookup<ProjectileRef>(isReadOnly: true);
    var defenderCcLookup         = SystemAPI.GetComponentLookup<DefenderCcData>(isReadOnly: true);
    var defenderTagLookup        = SystemAPI.GetComponentLookup<DefenderUnitTag>(isReadOnly: true);  // ← 추가
    var blockingHazardCellsLookup = SystemAPI.GetBufferLookup<BlockingHazardCellsBuffer>(isReadOnly: true);
    bool hasFlowField = SystemAPI.TryGetSingleton<FlowFieldSingleton>(out var flowField);

    // Singleton writers (조건부 — defender attack event 는 defender 만)
    NativeQueue<DefenderAttackEvent>.ParallelWriter? attackWriter = null;
    if (!_attackEventsQuery.IsEmpty)
        attackWriter = _attackEventsQuery.GetSingletonRW<DefenderAttackEventsSingleton>().ValueRW.queue.AsParallelWriter();

    NativeQueue<EnemyCcEvent>.ParallelWriter? ccWriter = null;
    if (!_ccEventsQuery.IsEmpty)
        ccWriter = _ccEventsQuery.GetSingletonRW<EnemyCcEventsSingleton>().ValueRW.queue.AsParallelWriter();

    // ─────────────────────────────────────────────────────
    // 단일 attacker loop
    // ─────────────────────────────────────────────────────
    foreach (var (attack, transform, attackerEntity) in
             SystemAPI.Query<RefRW<AttackState>, RefRO<LocalTransform>>()
                      .WithNone<PendingDeployment>()
                      .WithEntityAccess())
    {
        // Cooldown tick
        if (attack.ValueRO.cooldownRemaining > 0f)
            attack.ValueRW.cooldownRemaining = math.max(0f, attack.ValueRO.cooldownRemaining - dt);

        // Target 선정 (mask + 거리)
        float3 atkPos = transform.ValueRO.Position;
        float range = attack.ValueRO.range;
        float rangeSq = range * range;
        float bestSq = float.MaxValue;
        Entity bestTarget = Entity.Null;
        float3 bestTargetPos = default;
        int mask = attack.ValueRO.targetMask;
        for (int i = 0; i < targetEntities.Length; i++)
        {
            if (((int)targetFactions[i].value & mask) == 0) continue;
            if (targetEntities[i] == attackerEntity) continue;
            float d2 = DistanceSqToTarget(atkPos, targetEntities[i], targetTransforms[i].Position,
                                          blockingHazardCellsLookup, hasFlowField, flowField, out var nearestPos);
            if (d2 <= rangeSq && d2 < bestSq)
            {
                bestSq = d2;
                bestTarget = targetEntities[i];
                bestTargetPos = nearestPos;
            }
        }

        // Fire
        if (bestTarget != Entity.Null && attack.ValueRO.cooldownRemaining <= 0f)
        {
            // [Defender 분기] Attack event enqueue (Spine animation)
            if (attackWriter.HasValue && defenderTagLookup.HasComponent(attackerEntity))
            {
                attackWriter.Value.Enqueue(new DefenderAttackEvent
                {
                    defender    = attackerEntity,
                    targetWorld = bestTargetPos,
                });
            }

            // Buff scaling (보유 시만)
            float damageMul   = damageBoostLookup.HasComponent(attackerEntity)       ? damageBoostLookup[attackerEntity].multiplier   : 1f;
            float cooldownMul = cooldownReductionLookup.HasComponent(attackerEntity) ? cooldownReductionLookup[attackerEntity].multiplier : 1f;
            float synergyMul  = synergyLookup.HasComponent(attackerEntity)           ? synergyLookup[attackerEntity].damageMul        : 1f;
            float emittedDamage = attack.ValueRO.damage * damageMul * synergyMul;

            // Projectile vs melee 분기
            if (projectileRefLookup.HasComponent(attackerEntity))
            {
                var projRef = projectileRefLookup[attackerEntity];
                ecb.AddComponent(attackerEntity, new ProjectileSpawnRequest { /* 기존 필드 */ });
            }
            else
            {
                // Melee — AoE / single-target (기존 desiredCount fast path + hitMask)
                int desiredCount = math.max(1, attack.ValueRO.attackTargetCount);
                if (desiredCount == 1)
                {
                    ecb.AppendToBuffer(bestTarget, new IncomingDamage { amount = emittedDamage });
                }
                else
                {
                    // 기존 hitMask AoE 로직 그대로 — 단 inner-loop self-exclusion 의 변수명 변경 필요:
                    //   현재 AttackSystem.cs:201: `if (targetEntities[i] == defenderEntity) continue;`
                    //   통합 후:                  `if (targetEntities[i] == attackerEntity) continue;`
                    // mask 필터 (`(targetFactions[i].value & mask) == 0` continue) 도 그대로 보존.
                    var hitMask = new NativeArray<bool>(targetEntities.Length, Allocator.Temp);
                    int bestIdx = -1;
                    for (int i = 0; i < targetEntities.Length; i++)
                    {
                        if (targetEntities[i] == bestTarget) { bestIdx = i; break; }
                    }
                    if (bestIdx >= 0)
                    {
                        hitMask[bestIdx] = true;
                        ecb.AppendToBuffer(targetEntities[bestIdx], new IncomingDamage { amount = emittedDamage });
                    }
                    for (int pass = 1; pass < desiredCount; pass++)
                    {
                        float passSq = float.MaxValue;
                        int passIdx = -1;
                        for (int i = 0; i < targetEntities.Length; i++)
                        {
                            if (hitMask[i]) continue;
                            if (((int)targetFactions[i].value & mask) == 0) continue;
                            if (targetEntities[i] == attackerEntity) continue;        // ← 변수명 변경
                            float d2 = DistanceSqToTarget(atkPos, targetEntities[i], targetTransforms[i].Position,
                                                          blockingHazardCellsLookup, hasFlowField, flowField, out _);
                            if (d2 <= rangeSq && d2 < passSq) { passSq = d2; passIdx = i; }
                        }
                        if (passIdx < 0) break;
                        hitMask[passIdx] = true;
                        ecb.AppendToBuffer(targetEntities[passIdx], new IncomingDamage { amount = emittedDamage });
                    }
                    hitMask.Dispose();
                }
            }

            attack.ValueRW.cooldownRemaining = attack.ValueRO.cooldownDuration * cooldownMul;

            // [Defender 분기] Knockback CC
            if (ccWriter.HasValue && defenderCcLookup.HasComponent(attackerEntity))
            {
                // 기존 knockback 코드 그대로 (defenderEntity → attackerEntity 변수명만)
            }
        }
    }

    ecb.Playback(state.EntityManager);
    ecb.Dispose();
    targetEntities.Dispose();
    targetTransforms.Dispose();
    targetFactions.Dispose();
}
```

### 핵심 결정

- **단일 attacker query 의 `WithNone<PendingDeployment>`** — defender 만 영향. 적은 PendingDeployment 미부착이라 동작 동일.
- **AttackUnitTag / DefenderUnitTag 는 attacker query 에서 제거** — `AttackState` 보유 자체가 attacker 정체성. 두 tag 는 Movement / lifecycle / 배치 식별 등 *다른 용도* 만.
- **DefenderAttackEvent enqueue 는 `defenderTagLookup.HasComponent` 로 분기** — defender 만 enqueue. Spine animation 트리거 동작 보존. 미래 적도 attack event 가 필요해지면 별도 lookup 또는 FactionTag 분기.
- **버프/projectile/CC 분기 = 기존 lookup HasComponent 패턴 그대로** — refactor 하지 않음. variable 이름만 `defenderEntity` → `attackerEntity` 일괄 변경.
- **회귀 0 보장 = `defenderTagLookup` 분기** — defender 만 enqueue/buff/projectile/CC. 적은 적 동작 그대로 (raw damage, melee, single target).

### 잠재 회귀 벡터 (+ 방어)

| 위험 | 방어 |
|---|---|
| 적이 PendingDeployment 가질 일 발생 (현재 0, 미래 변경 시) | spec 후속 후보로 명시 — pending enemy 도입 시 enemy spawn 코드 검토 |
| 적의 attackTargetCount 가 0 default 로 남아있을 때 | destructible-blocking-hazards Unit 1 이 1 로 명시. `math.max(1, …)` fallback 유지 |
| 적이 ProjectileRef 가지면 projectile spawn → 기존엔 적 projectile 없어 무의미 | 적 spawn 코드에 ProjectileRef 부착 안 함 (기존 동작). 향후 적 projectile 도입 시 정책 결정 |
| DefenderUnitTag 가 attacker query 에서 빠짐 → defender 식별 어떻게? | attacker tag query 는 `defenderTagLookup.HasComponent(attackerEntity)` 로 대체 |
| AoE (attackTargetCount > 1) 적용 — 적이 melee AoE 보유 시 | 적 attackTargetCount = 1 명시. 적이 AoE 가지면 그건 의도된 변화 (mask + count 조합) |
| Entity 순회 순서 변경 (defender 먼저 → archetype 혼합) | 모든 mutation 이 ECB 지연 (`ecb.AppendToBuffer` / `ecb.AddComponent`) + target snapshot array 가 loop 안 불변 → frame-internal 순서 무관. `IncomingDamage` 는 buffer append (다음 시스템 `DamageApplicationSystem` 합산), entity 사망 / DeadTag 추가는 ECB.Playback 시점 = 두 loop 끝난 후 — 순서 회귀 없음 |

## 단위 테스트 (EditMode)

`AttackSystemUnifiedLoopTests` — `_session_handoff.md` 의 PlayMode 시나리오 U1~U8 와 1:1 매핑되는 EditMode 자동화:

| EditMode 케이스 | 매핑 PlayMode 시나리오 | 검증 |
|---|---|---|
| 디펜더 ProjectileRef 보유 | U1 | ProjectileSpawnRequest 추가 — IncomingDamage 직접 X |
| 디펜더 melee AoE (attackTargetCount=2) | U2 | 두 target 에 IncomingDamage 추가 (hitMask 로직) |
| 디펜더 DefenderCcData 보유 | U3 | EnemyCcEvent enqueue (knockback) |
| 디펜더 DamageBoost / Synergy / CooldownReduction | U4 | 데미지 / 쿨다운 multiplier 적용 |
| 적 (AttackUnitTag) 디펜더 공격 | U5 | IncomingDamage 직접, projectile/CC/AoE 없음 |
| 적이 hazard 공격 | U6 | hazard target FactionTag.BlockingHazard 매칭 + IncomingDamage |
| 디펜더 fire 시 DefenderAttackEvent enqueue | U7 | queue.Count == 1 |
| 적 fire 시 DefenderAttackEvent enqueue 0 | U8 | queue.Count == 0 (회귀 검증) |

추가:
- mask 자기 진영 self-target 가드 (자기 entity 제외) 동작 검증.
- target 풀에 PendingDeployment 있는 디펜더 제외 검증.
- target 풀에 DeadTag entity 제외 검증.

## 완료 기준

- 컴파일 + Burst 활성.
- EditMode 신규 unified loop 테스트 + 기존 149/149 회귀 0.
- **PlayMode 사용자 확인 (회귀 게이트)**:
  - 디펜더가 적 공격 (projectile / melee 둘 다) → 사망 동작 동일.
  - 적이 디펜더 공격 (`Enemy_Debug_Melee_Attacker.asset` 사용) → 디펜더 사망 동작 동일.
  - 적이 hazard 공격 → HP 감소 + 부서짐 동일 (destructible-blocking-hazards V1 시나리오).
  - knockback / synergy / DamageBoost / AoE 동작 동일.
  - 콘솔 에러/경고 0.
- AttackSystem.cs 의 코드 줄 수 감소 확인 (300 → ~170 줄 추정).
- LocalTransform writer 단독 = MovementSystem (불변).

검증: 2026-04-29 — 컴파일 성공, EditMode 155/155 통과 (unified loop 테스트 11/11), 콘솔 에러/경고 0. PlayMode 회귀 게이트 (U1~U8) 사용자 확인 통과. 커밋 `ccc2873`.
