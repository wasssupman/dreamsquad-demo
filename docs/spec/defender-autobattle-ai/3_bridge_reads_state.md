# 3 · 브리지 — 소환사 뷰 상태와 전이 트레이스

## 변경 대상
- `Bridge/BattleBridge.cs` `SyncSummonerAnimationState` — `IsPatrolAlive(patrol)` 폴링 → `DefenderAiStatus.value == Sustaining`. `IsPatrolAlive` 는 소비처가 남으면 유지.
- 같은 루프에서 상태 전이 트레이스: 직전 상태 딕셔너리(`_lastDefenderAiState`) 비교 → `LegacyTraceRecorder.Ev(TraceChannel.DefenderAiState = 22, a: simId, i: (int)state)`.
- `Core/Trace/LegacyTraceV0.cs` — 채널 22 append.

## 완료 기준
- 소환사 Play: 소환물 생존 중 `attack2` 루프, 상실 시 `attack3` 원샷 후 복귀 — 종전과 동일(`PatrolDefenderPlayTest`).
- 트레이스에 `DefenderAiState` 전이가 유닛당 «변할 때만» 찍힌다.
