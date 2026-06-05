# 1 — enqueue (적 사망 시)

## 목적

`DamageApplicationSystem` 에서 적(`AttackUnitTag`)이 데미지로 사망(HP≤0 전이)할 때 `EnemyKilledEvent` 를 enqueue. 골 도달 제거는 `UnitLifecycleSystem` 별도 경로라 이 분기에 안 들어옴 → 자연히 "디펜더가 처치한 적"만 집계.

## 변경 대상

- `Assets/_Project/Scripts/Battle/Units/DamageApplicationSystem.cs`

## 구현 (완료)

- `damage-number-popup` unit 1 에서 추가한 `_attackTagLookup` + `_transformLookup` 재사용.
- OnUpdate 상단: `hasEnemyKilledQueue = TryGetSingletonRW<EnemyKilledEventsSingleton>(...)`.
- 사망 분기(`newHp <= 0f`)에서 `AddComponent<DeadTag>` 직후, `AttackUnitTag` 보유 시 `position = LocalTransform.Position` 으로 enqueue.

## 계약/주의

- enqueue 는 이 한 곳만. Burst 호환(룩업 + NativeQueue.Enqueue). `[BurstCompile]` 유지.
- 프레임당 같은 적은 1회만 죽으므로 중복 없음(DeadTag 부여 후 다음 프레임 쿼리에서 제외).
- 점수 가치(pointsPerKill)는 여기서 모름 — 뷰가 결정(표시 전용).

## 완료 기준

- ✅ compile: CS 에러 0, Burst 에러 0 (force refresh + read_console).
- 코드 검토: `AttackUnitTag` + HP≤0 일 때만 enqueue.
- 런타임 표시는 unit 3 후.
