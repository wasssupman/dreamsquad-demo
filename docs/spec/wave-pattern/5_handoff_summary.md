# Wave Pattern Handoff Summary

**작업 구분**: Phase 5 handoff
**상태**: 1차 구현 완료, 리뷰 보정 반영
**작성일**: 2026-04-22

## Commit

- `0ec5f71 feat(waves): add seeded wave pattern flow`
- 리뷰 보정 커밋은 별도 예정

## Implemented

- `AttackDeck` 에 generated wave 설정 필드 추가
- `GeneratedWavePlan`, `GeneratedWave`, `WavePatternGenerator` 추가
- 같은 seed + 같은 attack unit pool 에서 deterministic wave plan 생성
- 3분 기준 `wavesPerRun=10~15`, `unitsPerWave=10~15`
- `BattleBridge` 가 generated wave plan 을 만들고 기존 `SpawnUnit` 경로로 펼쳐서 스폰
- `Next Wave` 버튼을 `BattleBridge` 소유 UI 로 생성
- briefing UI 를 `ATTACK WAVES` 스크롤 row 로 전환
- `BattleLogger` 에 wave pattern summary 와 wave event 기록 추가
- `Placement/Portal` VFX prefab 의 ParticleSystem velocity curve mode mismatch 수정

## Key Files

- `Assets/_Project/Scripts/Data/AttackDeck.cs`
- `Assets/_Project/Scripts/Data/GeneratedWavePlan.cs`
- `Assets/_Project/Scripts/Data/WavePatternGenerator.cs`
- `Assets/_Project/Scripts/Bridge/BattleBridge.cs`
- `Assets/_Project/Scripts/UI/TimelineBriefingView.cs`
- `Assets/_Project/Scripts/Logging/BattleLogSchema.cs`
- `Assets/_Project/Scripts/Logging/BattleLogger.cs`
- `Assets/_Project/Tests/EditMode/WavePatternGeneratorTests.cs`

## Verified

- Unity console: 0 errors / 0 warnings after VFX curve mode fix
- EditMode tests after initial implementation: 73/73 passed
- Review 보정 후 EditMode tests: 74/74 passed
- Review 보정 후 Unity console: 0 errors / 0 warnings

## Notes

- `waveSeed == 0` 은 현재 `1` 로 resolve 한다. session seed 파생은 아직 도입하지 않았다.
- briefing preview 와 battle runtime 은 둘 다 `WavePatternGenerator.Generate(deck)` 를 사용한다.
- `waveIntervalSec = timerDurationSec / waveCount` 가 canonical rule 이다.
- 마지막 wave 는 `timerDurationSec` 보다 앞에 호출되어야 한다.
- generated wave 내부 spawn entry 순서는 deterministic interleave 다.
- generated wave lane 배정은 `localIndex % laneCount` 다.
- `Next Wave` 연타는 현재 허용한다. 중복 wave 는 만들지 않고 다음 wave 들을 순서대로 앞당긴다.
- unit pool 이 2종 미만이면 generated path 는 실패하고 legacy `spawns` fallback 을 사용한다.
- `generatorVersion` 은 현재 로그 메타데이터다. 과거 로그 replay 분기는 아직 없다.

## Follow-up

- Playtest 로 briefing wave rows 와 runtime wave order 일치 확인
- `Next Wave` 연타가 실제 게임 템포에 과한지 확인
- session seed owner 가 생기면 `waveSeed == 0` 정책을 explicit resolved seed 공유 방식으로 교체
