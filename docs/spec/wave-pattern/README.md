# Wave Pattern Spec

**작성일**: 2026-04-21  
**연결 문서**: `docs/spec/map-system/20_claude_handoff_summary.md`  
**목표**: 기존 정적 `AttackDeck.SpawnEntry` 타임라인을 seed 기반 wave 생성 구조로 전환한다. 3분 플레이 기준 10~15개 wave 를 생성하고, 각 wave 는 현재 구현된 공격 유닛 타입 중 2종을 골라 10~15마리를 스폰한다.
**상태**: **완료 2026-07-20** (units 0~7). 1차 구현 `0ec5f71`(리뷰 보정 완료). units 6~7 최신 인계는 `8_handoff_summary.md`, 1차 인계는 `5_handoff_summary.md`.
2026-07-20 추가: unit 6 고정 시드(`2d8c843e`, `deck.waveSeed` 비0 = 매판 동일 패턴) · unit 7 진행 수량 램프(`2c2ecacd`, min→max 선형 + `waveCountJitter` 지터).
2026-07-21 추가: unit 9 강제 호출 리스케줄 — `Next Wave` 가 남은 웨이브 전체를 함께 앞당긴다(아래 "시간 배정" 참조).
2026-07-26 추가: unit 11 스폰 리드인 — 웨이브 트리거와 첫 적 등장 사이에 균일 유예(`waveSpawnLeadInSec`, 전 덱 2초).

## 구현 문서 목록

| 작업 구분 | 문서 | 목적 |
|---|---|---|
| Phase 0 | `0_current_contract.md` | 현재 정적 AttackDeck 구조와 교체 경계 고정 |
| Phase 1 | `1_seeded_wave_plan.md` | seed 기반 WavePlan 생성 데이터 모델 |
| Phase 2 | `2_wave_scheduler_next_button.md` | wave 수 기반 자동 스케줄 + Next Wave 강제 호출 |
| Phase 3 | `3_briefing_wave_ui.md` | 공격 패턴 확인 UI 를 wave summary 스크롤로 전환 |
| Phase 4 | `4_logging_tests_validation.md` | 로그, 테스트, Play 검증 기준 |
| Phase 5 | `5_handoff_summary.md` | 구현 결과와 다음 작업 인계 요약 |
| 추가 6 (2026-07-20) | `6_fixed_wave_seed.md` | 테스트 버전용 고정 웨이브 시드 — `deck.waveSeed` 라이브 오버라이드 재활성 (**완료 `2d8c843e`**) |
| 추가 7 (2026-07-20) | `7_wave_difficulty_ramp.md` | 웨이브 진행 수량 램프 — min→max 선형 증가 + `waveCountJitter` 지터 (**완료 `2c2ecacd`**) |
| 추가 8 (2026-07-20) | `8_handoff_summary.md` | units 6~7 인계 요약 (고정 시드 + 수량 램프 + 밸런스 값) |
| 추가 9 (2026-07-21) | `9_force_wave_reschedule.md` | `Next Wave` 강제 호출이 남은 웨이브 스케줄을 함께 앞당기도록 수정 (계약-구현 불일치) (**완료 `dc80a3fc`**) |
| 추가 10 (2026-07-22) | `10_handoff_summary.md` | unit 9 인계 요약 |
| 추가 11 (2026-07-26) | `11_wave_spawn_lead_in.md` | 웨이브 스폰 리드인 — 트리거와 첫 적 등장 사이 유예(전 덱 2초) |

## 공통 원칙

- 공격 패턴은 같은 seed 에서 항상 같은 wave 목록을 만든다.
- **시드 권한(unit 6 갱신)**: 라이브는 `deck.waveSeed` 비0 = 고정, 0 = `MatchSeed.DeriveWaveSeed(matchSeed)` 파생. `ResolveWaveSeed()` 의 `0→1` 폴백은 레거시 `Generate(deck)` 오버로드(프리뷰/테스트) 전용 — 0 판별이 필요한 라이브 분기는 필드를 직접 본다.
- briefing preview 와 battle runtime 은 같은 `WavePatternGenerator.Generate(deck)` 경로를 사용한다.
- 한 wave 는 정확히 2종의 공격 유닛 타입을 포함한다.
- `unitsPerWave` 총량은 웨이브 진행에 따라 `minUnitsPerWave`(6, 첫 웨이브)→`maxUnitsPerWave`(10, 마지막)로 **선형 증가** + `±waveCountJitter`(1) 지터, `[min,max]` 클램프 (unit 7). min/max 는 이제 "균등 랜덤 범위"가 아니라 "램프 양끝"이다. (2026-07-20: 10~15 → −50% → +20% → 램프. `WaveA.asset` 값)
- `wavesPerRun` 은 10~15개다.
- 자동 wave 시간은 `timerDurationSec / wavesPerRun` 으로 배정한다.
- Wave 1 은 0초에 호출하고, 마지막 wave 는 `timerDurationSec` 보다 앞에 예약한다.
- `Next Wave` 버튼은 다음 wave 를 즉시 호출하되, 이미 호출된 wave 를 중복 호출하지 않는다.
- `Next Wave` 연타는 허용한다. 남은 wave 들을 순서대로 앞당기며, 추가 wave 를 생성하지 않는다.
- wave 생성은 map 생성 seed 와 독립적으로 재현 가능해야 한다. 같은 map seed 를 재사용할 수는 있지만 로그에는 wave seed 를 별도로 기록한다.
- 기존 `AttackUnitData` SO 가 공격 유닛 풀의 source of truth 다.
- unit pool 이 2종 미만이면 generated wave 생성은 실패하고 legacy `spawns` fallback 을 사용한다.
- generated wave 의 lane 배정은 **`deckIndex % laneCount`** 다(`deckIndex = waveIndex × 1000 + 엔트리순번`,
  `WavePatternGenerator.EffectiveSpawnIndex`). `localIndex % laneCount` 는 `ExpandWave` 가 채우는
  authored 후보값이고, **lane 이 3개 이상이면 deckIndex 규약이 이를 덮는다** — `1000 % 3 = 1` 이라
  웨이브마다 lane 이 한 칸씩 회전한다. lane ≤ 2 에서만 authored 값을 clamp 해서 쓴다.
  (2026-07-26 정정: 기존 서술은 3+ lane 실제 동작과 달랐다. 실제 규약은 `spawn-point-alert/0_*` 참조.)
- wave 내부 스폰 순서는 deterministic interleave 다. `A,B,A,B...` 순서로 펼치고 한쪽 수량이 먼저 끝나면 남은 타입을 이어서 스폰한다.
- `intraWaveSpacingSec` 는 round-robin 펼침에서 스폰지점 간 첫 적 간격(= 지점별 텀)이자 같은 지점 내 간격은 `spacing × laneCount` 다. 값이 작으면 모든 스폰지점이 거의 동시에 활성화된다. (2026-07-20 밸런스: 0.35 → 1.0, 스폰지점 순차 출현. `WaveA.asset` 값)

## 시간 배정

3분 플레이 기준으로 생성된 wave 수에 따라 자동 호출 시간을 균등 배정한다.

- seed 는 총 10~15개 wave 를 생성한다.
- `waveIntervalSec = timerDurationSec / waveCount`.
- Wave 1 은 0초에 호출한다.
- 마지막 wave 는 `timerDurationSec - waveIntervalSec` 에 예약된다.
- 예: 10 wave 는 18초 간격으로 0~162초, 15 wave 는 12초 간격으로 0~168초에 호출된다.
- **첫 적 등장 = 트리거 + `waveSpawnLeadInSec`** (unit 11, 전 덱 2초). 리드인은 `QueueWave` 의
  스폰 base 에만 더해진다 — 트리거 그리드·`_waveTimeShift`·플랜 `triggerTimeSec`·브리핑 표기·
  `wave_started` 로그 시각은 전부 불변이다. 작성 플랜은 그룹 상대 시각으로 직접 표현하므로 미적용.
- `Next Wave` 는 다음 예정 wave 를 현재 시점으로 앞당긴다(첫 적은 그 시점 + 리드인).
- **앞당긴 만큼 남은 wave 전체가 함께 이동한다** (unit 9). 웨이브 간 간격이 보존되므로
  강제 호출 뒤 다음 wave 는 `호출 시점 + 원래 간격` 에 나온다. 플랜의 `triggerTimeSec` 은
  불변이고(브리핑·로그의 source of truth) 런타임 오프셋 `_waveTimeShift` 만 이동한다.
- 전투 타이머(`timerDurationSec`)는 함께 당기지 않는다. 몰아 부르면 그만큼 일찍 끝난다.

## 비목표

- 난이도 곡선 정교화
- wave 내부 hp/damage 분포 밸런싱
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
- 180초 기준 10~15개 wave 가 생성되고 마지막 wave 는 180초보다 앞에 호출된다.
- 각 wave 는 2종 유닛, 총 10~15마리 조건을 만족한다.
- wave 수 기반 자동 스폰과 `Next Wave` 강제 호출이 모두 동작한다.
- briefing UI 에 wave별 요약이 큰 카드/행 형태로 스크롤 표시된다.
