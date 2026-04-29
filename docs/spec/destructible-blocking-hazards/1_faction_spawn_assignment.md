# Faction Spawn Assignment (BattleBridge)

**작업 구분**: 1

## 목적

디펜더 / 적 spawn 코드에 `FactionTag` + `AttackState.targetMask` default 값을 부여한다. 이 단위 후 모든 attack-able entity 가 진영 식별 가능. **compile-only 게이트** — AttackSystem 은 아직 mask 안 보므로 동작 변화 0.

## 변경 대상

- Modify: `Assets/_Project/Scripts/Bridge/BattleBridge.cs`
  - 디펜더 spawn 경로 (line ~1949 부근, `new Health` add 직후)
  - 적 spawn 경로 (line ~2320 부근, `_em.AddComponent<AttackUnitTag>` 직후)

## 구현

### 디펜더 spawn (BattleBridge.cs:1949 부근)

```csharp
_em.AddComponentData(entity, new Health { value = unitData.health, max = unitData.health });
_em.AddComponentData(entity, new FactionTag { value = Faction.Defender });
// AttackState 추가 시 targetMask 도 함께
_em.AddComponentData(entity, new AttackState
{
    damage = unitData.attackDamage,
    range  = unitData.attackRange,
    cooldownDuration  = unitData.attackCooldown,
    cooldownRemaining = 0f,
    attackTargetCount = unitData.attackTargetCount,  // 기존 값 보존
    targetMask        = (int)Faction.Enemy,           // ← 추가
});
```

### 적 spawn (BattleBridge.cs:2320 부근)

```csharp
_em.AddComponent<AttackUnitTag>(entity);
_em.AddComponentData(entity, new Health { value = entry.unitType.health, max = entry.unitType.health });
_em.AddComponentData(entity, new FactionTag { value = Faction.Enemy });
if (entry.unitType.attackDamage > 0f)
{
    _em.AddComponentData(entity, new AttackState
    {
        damage = entry.unitType.attackDamage,
        range  = entry.unitType.attackRange,
        cooldownDuration  = entry.unitType.attackCooldown,
        cooldownRemaining = 0f,
        attackTargetCount = 1,                            // ← 명시 (struct default 0 의 의도된 hardening — math.max(1, …) 가 0/1 동치 처리하지만 명시화)
        targetMask        = (int)(Faction.Defender | Faction.BlockingHazard),  // ← 추가
    });
}
```

### Hazard spawn

본 unit 에서는 hazard spawn 코드 없음 (Unit 7 에서). hazard FactionTag 부여는 Unit 7 의 spawn API 안에서.

## 단위 테스트 (EditMode)

없음 — Bridge 의 spawn 경로 통합 검증은 Unit 2 회귀 게이트에서.

## 완료 기준

- 컴파일 성공.
- 기존 테스트 (133/133) 회귀 0.
- 동작 변화 0 — AttackSystem 이 아직 `targetMask` 안 봄.
- 모든 디펜더 entity 가 `FactionTag.Defender` + `AttackState.targetMask = (int)Faction.Enemy` 보유 (코드 리뷰).
- 모든 적 entity 가 `FactionTag.Enemy` 보유. attackDamage > 0 인 적은 `AttackState.targetMask = (int)(Defender | BlockingHazard)` 보유.
- **plays-test 금지** — Unit 2 와 합쳐야 회귀 검증 가능.
- 콘솔 에러/경고 0.

검증: 2026-04-29 — Unit 0~2 묶음으로 컴파일 성공, EditMode 142/142 통과, 콘솔 에러/경고 0. 커밋 `3f5ab31`.
