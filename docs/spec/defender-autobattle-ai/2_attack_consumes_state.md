# 2 · AttackSystem 이 상태를 읽는다 (동작 무변)

## 변경 대상
- `Battle/Combat/AttackSystem.cs` — 세 START 경로(폭탄 :324 · 소환 :436 · 일반 :956)의 `canStart && cooldownRemaining <= 0` →
  `DefenderAi.CanStartAttack(aiState, cooldownRemaining <= 0)`(방어유닛) / 적은 `UnitActionPhase` 그대로.
  소환 블록의 `alivePatrol` 3중 술어 삭제 → `aiState == Sustaining`. `actionLocked` 계산은 적용(RESOLVE 완료 규약)에 남는다.

## 구현 메모
- 방어유닛만 `DefenderAiState` 를 갖는다 — `aiStateLookup.HasComponent` 로 분기. 적은 `EnemyAiState` 게이트(`stateAllowsFire`) 현행.
- 소환 블록: `Sustaining` 이면 스폰 skip + `gateOpen` 이면 쿨 리셋 — 오늘과 같은 흐름.

## 완료 기준
- `AttackSystemUnifiedLoopTests` · `AttackSystemStateGateTests` · `CcActionLockTests` · `PatrolDefender*` EditMode 무수정 초록.
- 골든 코퍼스 값 무변(unit 6).
