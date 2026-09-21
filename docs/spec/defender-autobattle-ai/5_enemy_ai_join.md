# 5 · 적 편입 — `AiState` enum + `Evaluate` 를 `Wassup.UnitAi` 로

## 변경 대상
- `Battle/Combat/EnemyAiState.cs` 의 `enum AiState` → `UnitAi/EnemyAi.cs`(namespace `Wassup.UnitAi`) + `EnemyAi.Evaluate(aggroed, guardianInRange, hasFireTarget)`
- `EnemyAiStateSystem.Evaluate` → `EnemyAi.Evaluate` 위임(기존 정적 메서드는 호환 유지 후 제거)
- `AiState` 를 쓰는 28 파일에 `using Wassup.UnitAi;` (Scripts 7 · Tests 21) — 이름·값 무변
- `EnemyAiStateTransitionTests` → `EnemyAiTests`(UnitAi 대상)

## 완료 기준
- 컴파일 0 · 28 파일 수정이 `using` 한 줄뿐(diff 로 확인) · 관련 EditMode/PlayMode 무수정 초록.
