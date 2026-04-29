# AttackSystem Mask-Based Target Query (회귀 게이트)

**작업 구분**: 2

## 목적

`AttackSystem.cs` 의 두 loop 가 타겟 query 시 진영 tag (`AttackUnitTag`/`DefenderUnitTag`) 대신 `FactionTag` + `targetMask` 를 보도록 전환. **회귀 게이트**: 디펜더 ↔ 적 공격 동작 동일 검증.

## 변경 대상

- Modify: `Assets/_Project/Scripts/Battle/Combat/AttackSystem.cs`
- Add: `Assets/_Project/Scripts/Tests/EditMode/AttackSystemMaskTests.cs` (EditMode unit test)

## 구현

### Snapshot query 변경 (line 41, 47)

**현재**:
```csharp
var attackerQuery = SystemAPI.QueryBuilder().WithAll<AttackUnitTag, LocalTransform>().Build();
// ...
var defenderQuery = SystemAPI.QueryBuilder()
    .WithAll<DefenderUnitTag, LocalTransform>()
    .WithNone<PendingDeployment>()
    .Build();
```

**변경 후** — 양쪽 모두 단일 "공격 가능 후보 풀" 로:
```csharp
// 공격 가능한 모든 entity (Faction + Health + LocalTransform 보유, 배치 대기 제외)
var targetCandidatesQuery = SystemAPI.QueryBuilder()
    .WithAll<FactionTag, Health, LocalTransform>()
    .WithNone<PendingDeployment>()
    .WithNone<DeadTag>()
    .Build();
var targetEntities    = targetCandidatesQuery.ToEntityArray(Allocator.Temp);
var targetTransforms  = targetCandidatesQuery.ToComponentDataArray<LocalTransform>(Allocator.Temp);
var targetFactions    = targetCandidatesQuery.ToComponentDataArray<FactionTag>(Allocator.Temp);
```

→ **양 loop 가 동일 풀 사용**. 분기는 attacker 의 `targetMask` 로.

### Defender → target loop (line 80~265)

```csharp
foreach (var (attack, transform, defenderEntity) in
         SystemAPI.Query<RefRW<AttackState>, RefRO<LocalTransform>>()
                  .WithAll<DefenderUnitTag>()    // ← attacker tag 유지 (buff/projectile 분기 보존)
                  .WithNone<PendingDeployment>()
                  .WithEntityAccess())
{
    // ... cooldown tick ...
    int mask = attack.ValueRO.targetMask;
    // 타겟 후보 순회 시 mask 필터:
    for (int i = 0; i < targetEntities.Length; i++)
    {
        if (((int)targetFactions[i].value & mask) == 0) continue;
        if (targetEntities[i] == defenderEntity) continue;  // self-target 방지
        // ... 거리 계산 + bestTarget 선정 (기존 로직)
    }
    // ... fire, buff/projectile 분기 (기존 그대로) ...
}
```

### Enemy → target loop (line 268~300)

동일 패턴:
```csharp
foreach (var (attack, transform, attackerEntity) in
         SystemAPI.Query<RefRW<AttackState>, RefRO<LocalTransform>>()
                  .WithAll<AttackUnitTag>()       // ← attacker tag 유지
                  .WithEntityAccess())
{
    int mask = attack.ValueRO.targetMask;
    for (int i = 0; i < targetEntities.Length; i++)
    {
        if (((int)targetFactions[i].value & mask) == 0) continue;
        if (targetEntities[i] == attackerEntity) continue;
        // ... 거리 계산 + 데미지 (기존 로직)
    }
}
```

### 핵심 결정

- **Attacker foreach 의 `WithAll<DefenderUnitTag>` / `WithAll<AttackUnitTag>` 유지** — 두 loop 의 buff/projectile/CC 분기가 attacker 진영별 다름 (defender 만 DamageBoost/CooldownReduction/Synergy/Projectile/CC). 통합 시 회귀 위험 ↑.
- **Target snapshot 만 통합** — 단일 풀 + mask 필터.
- 자기 진영 self-target 방지를 위해 `attackerEntity` 제외 (사실 mask 가 자기 진영 제외하면 자연 처리되지만 이중 보장).

## 단위 테스트 (EditMode)

`AttackSystemMaskTests`:
- mask filter 산술: `(int)Faction.Enemy & (int)Faction.Defender == 0`, `(int)Faction.Enemy & (int)(Faction.Defender | Faction.BlockingHazard) == 0`, `(int)Faction.BlockingHazard & (int)(Faction.Defender | Faction.BlockingHazard) != 0` 등.
- 디펜더가 mask=Enemy 일 때 적만 타겟. hazard 없으면 hazard 무시.
- 적 mask=Defender|BlockingHazard 일 때 디펜더와 hazard 둘 다 후보.

## 완료 기준

- 컴파일 + Burst 활성.
- EditMode 신규 mask 테스트 + 기존 133/133 회귀 0.
- **PlayMode 사용자 확인 (회귀 게이트)**:
  - 디펜더가 적 공격 → 사망 동작 동일.
  - 적이 디펜더 공격 (attackDamage > 0 인 적 SO 사용) → 디펜더 사망 동작 동일.
  - knockback / projectile / synergy 동작 동일.
- 콘솔 에러/경고 0.
- LocalTransform writer 단독 = MovementSystem (불변 검증).

검증: 2026-04-29 — 컴파일 성공, `AttackSystemMaskTests` 추가, EditMode 142/142 통과, 콘솔 에러/경고 0. PlayMode 회귀 검증은 사용자 확인 대기. 커밋 `3f5ab31`.
