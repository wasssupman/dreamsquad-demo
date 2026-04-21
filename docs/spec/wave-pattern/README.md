# Wave Pattern Spec

**작성일**: 2026-04-21  
**연결 문서**: `docs/spec/map-system/20_claude_handoff_summary.md`  
**목표**: 기존 정적 `AttackDeck.SpawnEntry` 타임라인을 seed 기반 wave 생성 구조로 전환한다. 3분 플레이 기준 10~15개 wave 를 생성하고, 각 wave 는 현재 구현된 공격 유닛 타입 중 2종을 골라 10~15마리를 스폰한다.

## 구현 문서 목록

| 작업 구분 | 문서 | 목적 |
|---|---|---|
| Phase 0 | `0_current_contract.md` | 현재 정적 AttackDeck 구조와 교체 경계 고정 |
| Phase 1 | `1_seeded_wave_plan.md` | seed 기반 WavePlan 생성 데이터 모델 |
| Phase 2 | `2_wave_scheduler_next_button.md` | wave 수 기반 자동 스케줄 + Next Wave 강제 호출 |
| Phase 3 | `3_briefing_wave_ui.md` | 공격 패턴 확인 UI 를 wave summary 스크롤로 전환 |
| Phase 4 | `4_logging_tests_validation.md` | 로그, 테스트, Play 검증 기준 |

## 공통 원칙

- 공격 패턴은 같은 seed 에서 항상 같은 wave 목록을 만든다.
- 한 wave 는 정확히 2종의 공격 유닛 타입을 포함한다.
- 한 wave 의 총 스폰 수는 10~15마리다.
- 3분 기준 wave 수는 10~15개다.
- 자동 wave 시간은 3분을 생성된 wave 수로 나눠 배정한다.
- `Next Wave` 버튼은 다음 wave 를 즉시 호출하되, 이미 호출된 wave 를 중복 호출하지 않는다.
- wave 생성은 map 생성 seed 와 독립적으로 재현 가능해야 한다. 같은 map seed 를 재사용할 수는 있지만 로그에는 wave seed 를 별도로 기록한다.
- 기존 `AttackUnitData` SO 가 공격 유닛 풀의 source of truth 다.

## 시간 배정

3분 플레이 기준으로 생성된 wave 수에 따라 자동 호출 시간을 균등 배정한다.

- seed 는 총 10~15개 wave 를 생성한다.
- `waveIntervalSec = timerDurationSec / (waveCount - 1)`.
- Wave 1 은 0초에 호출한다.
- 마지막 wave 는 180초에 예약된다.
- 예: 10 wave 는 20초 간격, 15 wave 는 약 12.86초 간격이다.
- `Next Wave` 는 다음 예정 wave 를 현재 시점으로 앞당긴다.

## 비목표

- 난이도 곡선 정교화
- elite/boss wave
- spawn formation/동시성 연출 고도화
- wave reward/economy 재밸런싱
- 플레이어별 adaptive director

## 기본 출력 예시

```text
Wave 1 - Basic 5, Swift 10
Wave 2 - Tanker 4, Basic 8
Wave 3 - Swift 7, Tanker 6
```

## 완료 기준

- 같은 seed 로 Play 재진입 시 wave 목록이 동일하다.
- 다른 seed 로 Play 재진입 시 wave 목록이 달라질 수 있다.
- 180초 기준 10~15개 wave 가 생성된다.
- 각 wave 는 2종 유닛, 총 10~15마리 조건을 만족한다.
- wave 수 기반 자동 스폰과 `Next Wave` 강제 호출이 모두 동작한다.
- briefing UI 에 wave별 요약이 큰 카드/행 형태로 스크롤 표시된다.
