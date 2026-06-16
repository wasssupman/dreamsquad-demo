# Wave Authoring Test Mode Spec

**작성일**: 2026-06-16
**상태**: 초안 — 승인 대기. 구현 전.
**선행 spec**: `docs/spec/wave-pattern/` (seed 기반 wave 생성. 본 spec 은 그 위에 "직접 작성한 wave" 경로를 추가하며, seed 경로/결정론은 무변경)

## 상위 목표

"한 판의 재미"를 직접 설계할 수 있도록, seed 랜덤 생성에 의존하지 않고 **에디터에서 직접 작성한 웨이브 데이터**(웨이브당 N개 적 타입 + 각 수량 + 트리거 시각)를 배틀씬이 그대로 사용하는 **테스트 모드**를 만든다. 아웃게임에 "테스트 모드" 버튼을 추가해 작성된 플랜을 골라 진입한다.

## 검증 질문

에디터에서 `WavePlanAsset` 을 직접 작성하고, 아웃게임 "테스트 모드"에서 그 플랜을 골라 진입하면, 배틀씬이 **시드 생성이 아닌 작성된 웨이브(웨이브당 N타입·수량·트리거 시각)** 를 그대로 스폰하는가? 그리고 기존 seed(라이브) 경로의 스폰 순서·결정론은 **byte-identical 로 무변경**인가?

## 작업 단위

| # | 작업 구분 | 문서 | 목적 |
|---|---|---|---|
| 0 | 저작 데이터 | `0_wave_plan_asset.md` | `WavePlanAsset` SO + `AuthoredWave`/`AuthoredSpawnGroup` + 샘플 에셋 1개. `timerDurationSec=0` = endless. 순수 신규, 의존 없음. |
| 1 | 런타임 모델 일반화 | `1_generated_wave_nentry.md` | `GeneratedWave` 를 `(unit,count)[]` entries 로 일반화 + `ExpandWave`/`FormatSummary` entries 순회 + 소비처 전환(브리핑 UI `WavePatternStripView` N줄 가변, 로그 스키마 `WaveRecord` entries[]) + **결정론 회귀 테스트**. (구 1+2 병합 — 하나의 "모델 N-entry 화" cohesive 변경. 한 커밋에 compile-clean + 테스트 green.) |
| 2 | 변환기 + Bridge 경로 | `2_converter_and_bridge.md` | `WavePlanAsset → GeneratedWavePlan`(N-entry) 변환기 + `BattleBridge.SetAuthoredWavePlan` + `TryInitializeGeneratedWaves` 분기(작성 플랜 우선, 없으면 기존 seed) + **endless 종료**(`_timerDuration=0` → `CheckTimer` 비활성, `CheckVictory` 로만 종료) + 로깅 + EditMode 테스트. (구 3+4 병합.) unit 1 의존. |
| 3 | 테스트 모드 진입 | `3_testmode_entry.md` | `TestModeConfig` SO(디펜더 프리셋 + 플랜 카탈로그) + `TestModeContext`(static carry-in) + `GameManager` 테스트모드 최상위 분기(드래프트 스킵, `StartSquadMatch` 미러). unit 2 의존. |
| 4 | 아웃게임 UI + 배선 | `4_outgame_button_picker.md` | "테스트 모드" 버튼 + 플랜 피커 패널 + 씬 배선 + Play 검증(100-웨이브 endless 클리어/패배 포함). unit 3 의존. |
| 5 | Handoff | `5_handoff_summary.md` | 종료 요약. |

의존 순서: `0 → 1 → 2 → 3 → 4`. (0 은 1 과 독립이라 먼저 또는 병행 가능하나 번호 순서대로 진행한다. 구 8유닛 → 6유닛 통합: 모델 일반화+소비처를 1로, 변환기+Bridge+endless 를 2로 묶음.)

## Feature-wide 계약

- **seed 경로 무변경**: 기존 `WavePatternGenerator.Generate(deck, seed)` 의 출력(스폰 순서/시간/lane)은 byte-identical 유지. unit 1 의 일반화는 seed 웨이브를 정확히 2-entry 로 생성하며, round-robin 인터리브가 기존 `A,B,A,B` 와 동일함을 회귀 테스트로 못박는다.
- **단일 런타임 모델**: 작성 웨이브와 seed 웨이브는 모두 `GeneratedWavePlan`/`GeneratedWave`(N-entry) 라는 하나의 런타임 표현을 통과한다. 스케줄러(`BattleBridge`)는 출처를 구분하지 않는다.
- **작성 데이터는 SO source-of-truth**: `WavePlanAsset` 이 작성 웨이브의 source of truth. 적 타입은 기존 `AttackUnitData` SO 를 드래그해 참조. 하드코딩 수치 금지(수량/시각/스케일 모두 SO 필드).
- **웨이브 규모 무제한**: 웨이브 개수에 상한 없음(작성자 자유). 3분 라이브 감각용 샘플은 약 15개로 제공하되 **강제하지 않는다**. 테스트 모드에서 100개도 유효.
- **endless 종료 조건 (테스트 모드)**: 시간 제한 없음. `WavePlanAsset.timerDurationSec=0` → `BattleBridge._timerDuration=0` → `CheckTimer` 가 early-return(타임아웃 승리 비활성). 전투는 **`CheckVictory` 로만 종료** = 모든 작성 웨이브 dispatch 완료 + `_pending` 비고 + 생존 공격유닛 0(전멸). 패배(goal-reached 횟수 도달)는 기존대로 동작. 즉 100개 웨이브면 100번째 웨이브 몬스터까지 전부 죽어야 승리. (새 종료 로직 추가 없음 — 기존 `_timerDuration<=0` 시맨틱 재사용.)
- **테스트 모드 = wave 소스 + 디펜더 + 종료조건만 오버라이드**: 테스트 모드는 (1) seed 대신 작성 플랜 사용, (2) 드래프트 스킵 후 `TestModeConfig.defenderPreset` 반입, (3) endless 종료(위). 맵은 `MapGenerationOptions.Default`, 스킬 로드아웃은 기존 roll/기본값 경로 재사용. 그 외 전투 규칙 무변경.
- **carry-in 은 static 1회 소비**: `TestModeContext`(Active/Plan)는 아웃게임 버튼이 set, `GameManager.Start` 가 읽고 즉시 clear. GameManager 는 비영속(씬 전환 시 teardown)이므로 SO 가 아닌 static 으로 씬 경계를 넘긴다.
- **비파괴 분기**: `TestModeContext.Active == false` 면 기존 squad → draft → fallback 분기 그대로. 테스트 모드는 최상위 우선 분기로만 추가.
- **로그 포맷 변경 명시**: `WaveRecord` 가 `unitA/B/countA/B` → `entries[]` 로 바뀐다. 디버그 로그라 save 마이그레이션 부담은 없으나 과거 로그와 포맷 불일치는 의도된 변경.

## 비목표 / 후속 후보 (본 spec 범위 밖)

- 그룹별 개별 오프셋 초(웨이브 내 항목마다 별도 시각) — 현재는 웨이브 트리거 시각 + intraWaveSpacing 균등.
- 엘리트/보스 웨이브, lane/formation 지정 작성.
- 작성 플랜 밸런싱·난이도 곡선 자동화.
- 작성 플랜 import/export, 런타임(인게임) 작성 UI.
- 커스텀 인스펙터/EditorWindow 로 저작 UX 고도화(현재는 기본 인스펙터 드래그).
- 테스트 모드의 맵/스킬도 작성·고정하는 확장.
- endless(시간무제한) 모드의 타이머 HUD 표시 처리(현재 `TimerRemaining` 은 `_timerDuration=0` 에서 0 반환). 본 spec 에선 기능 동작 우선, HUD 표시 정리는 후속.

## 참고

- 현재 모델 분석: `GeneratedWave` 2타입 고정이 생성기·스케줄러·브리핑 UI·로그 4곳에 박혀 있음(이 spec 의 unit 1·2 가 일반화).
- 진입 분기 모델: `GameManager.StartSquadMatch`(드래프트 스킵 + `SetDefenderPool` + `PrepareDraftMap` + placement)를 테스트 모드가 미러한다.
