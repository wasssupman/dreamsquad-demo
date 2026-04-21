# Drop Pending Deployment

**작업 구분**: Phase 5

## 목적

Valid drop 시 즉시 전투 가능한 defender 를 만들지 않고, `PendingDeployment` 상태의 defender entity 를 만든다.

## API

```csharp
public bool TryBeginDefenderDeployment(
    int tileX,
    int tileY,
    DefenderUnitData unitData,
    out Entity entity);
```

## 규칙

- valid drop 이 아니면 cost 차감 없음.
- valid drop 이면 cost 차감 + tile 점유 + entity 생성.
- 생성된 entity 는 `PendingDeployment` component 를 가진다.
- Pending entity 는 공격하지 않고, 피격되지 않고, synergy 계산에서 제외된다.

## 완료 기준

- valid drop 시 PendingDeployment entity 생성.
- invalid drop 시 reject flash 만 발생.
- PendingDeployment 필터가 AttackSystem / DamageApplicationSystem 에 적용된다.
