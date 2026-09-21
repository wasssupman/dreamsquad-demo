# 4 · 뷰 — 상태 하나를 입력으로

## 변경 대상
- `Presentation/UnitView.cs` — `public virtual void SetAiState(DefenderAiState state, string sustainLoop, string sustainLostOneShot)`
- `Presentation/SpineUnitView.cs`·`SpriteUnitView.cs` — Sustaining → 기존 `SetLoopOverride(loop, lost)` · 그 외 → `ClearLoopOverride`. 메커니즘(오버라이드·큐·원샷 게이트)은 그대로, **결정 입력만 상태**.
- 브리지 `SyncSummonerAnimationState` → `view.SetAiState(...)` 한 줄.

## 왜 여기까지만
원샷(Deploy·Attack·Death)은 사건이라 상태로 대체하면 프레임 유실이 생긴다(사건 채널 유지). 루프 선택(오버라이드/대기 순환)만 상태에서 나온다.
`SetLoopOverride` public API 는 남긴다(다른 소비자 0 이면 unit 6 에서 private 화).

## 완료 기준
- `PatrolDefenderPlayTest` 무수정 초록. idle-break 순환·배치 원샷 큐 동작 무변(Play 계측).
