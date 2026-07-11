# 2 — MovementSystem 보스 분기

## 목적

보스가 `Marching` 일 때 defender field 가 유효하면 goal flow 대신 그 flow 를 따르게 한다. 이동 메커니즘(속도·CC·recenter·cell-trim)은 전부 기존 코드 공유 — 바뀌는 건 "방향을 어느 필드에서 읽나"뿐.

## 변경 대상

- `Assets/_Project/Scripts/Battle/Movement/MovementSystem.cs`

## 구현

1. `BossTag` RO lookup + `TryGetSingleton<DefenderFieldSingleton>` 추가. 싱글톤 부재(테스트/티어다운 중) → 전원 기존 경로 (RequireForUpdate 에 추가하지 않는다).
2. flow step 직전에 사냥 판정: `hunting = isBoss && huntField.dist[idx] != int.MaxValue` (idx = 현재 셀, 두 필드는 같은 그리드).
   - `hunting` → `dir = huntField.flow[idx]`, zero-flow recovery 도 `huntField.dist` 로 이웃 탐색.
   - (rev, ecs-review M3) recovery 이웃 탐색은 `FlowRecovery.RecoveryDir(cell, dist, gridSize)` 순수함수로 추출 — dist 배열 스왑(goal↔defender)이 유일한 신규 회귀면이라 EditMode 4종(`FlowRecoveryTests`)으로 고정.
   - 아니면 → 기존 goal field 그대로 (계약 5 fallback: 방어유닛 0 = 전 셀 MaxValue = 자동 마칭).
3. goal-leak 가드: `cell == goalCell → PastGoalTag` 판정을 `hunting` 이면 skip — 사냥 중 goal 셀을 지나쳐도 누수 안 함 (leak-proof 요구).
4. **불변**: `Standoff`/`Chasing`(aggro)/`Engaging` 분기, portal/tornado/impulse, `LateralRecenter`, `MovementCellTrim`(goal field 사용, 계약 7) 전부 무수정.

## 완료 기준

- compile 클린, 기존 EditMode 전체 무회귀 (특히 FlowFieldBuilder/Movement 관련).
- 비-보스 적: diff 전후 이동 동일 (BossTag 없음 → hunting 항상 false → 코드 경로 동일).
- 보스 + 방어유닛 존재: Play 에서 보스가 방어유닛 방향(뒤 포함)으로 걷는 것 확인 (정식 e2e 는 unit 3).

확인 2026-07-11 · 커밋 `dc298ceb` (EditMode 653 무회귀 + FlowRecovery 4종, Play 트레이스 unit 3 참조)
