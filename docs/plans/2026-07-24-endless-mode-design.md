# Endless Mode — Design (thin)

> 브레인스토밍 결과물. 구현 상세는 `docs/spec/endless-mode/`. 이 문서는 목표·아키텍처 요약·포인터.
> critic 리뷰(REVISE) 반영 완료 — 2026-07-24.

## 목표

기존 토너먼트 배틀(메인)과 별개인 **스코어어택 "무한 모드"**. 3분 고정 창에서
**당기기(`ForceNextWave`)로 웨이브를 더 욱여넣어 킬 극대화**하는 리스크/리워드 모드.

- 웨이브: 최대 30, **고정 10초 간격**, 타이머 180초. 당기기 없으면 ~18웨이브만 스폰.
- 점수 = **킬 + 스트레스(누수 페널티)**, **시간축 0**. v1 은 **무제한(누수로 안 죽음)**.

## 아키텍처 요약 (critic 반영 최종)

**모드 seam = `AttackDeck.battleMode` enum `{ Main, Endless }` 단일.** BattleBridge(unit 2 한 곳)가 분기:

1. **진입** — 공용 풀 아님. 전용 `[SerializeField] endlessEncounter` + dev 토글. `mapPool.Count` 불변
   → `MapPoolSelect` 무손 → 토너먼트 회귀 0.
2. **간격** — `fixedWaveIntervalSec`(>0) vs `duration/waveCount` (데이터 구동, unit 1 생성기).
3. **누수/패배** — `defeatEnabled = !IsEndless` (엔드리스 안 죽음, 신규 필드 없음).
4. **점수** — `remainingMs = IsEndless ? 0` 한 줄. 메인 `scoreRules`·`ScoreMath` 그대로. 스트레스 예산 =
   `defeatGoalReachedCount` 재사용(높게 — saturation 방지).
5. **토너먼트 리포트** — 엔드리스는 스킵.

**재활용(신규 코드 0)**: `ForceNextWave`+리스케줄, `SpawnAlertPresenter`, `QueueDueWaves`,
`_killScoreTotal`, 순수함수 `ScoreMath.Evaluate`, 결과 팝업.

**ECS 경계**: 변경 없음. 스폰은 기존 `QueueWave→SpawnUnit`(Units).

## 결정 로그

- 웨이브: 고정 10초 × 180초, 최대 30. 당기기가 핵심 스킬 레버.
- 패배: **v1 무제한만**(구현 최단). 개수 기반은 후속.
- 점수: 시간축 제거. 킬 주력, 누수 페널티. 예산 높게(saturation 방지).
- 진입: 공용 풀 미사용 — 전용 encounter + dev 토글 (토너먼트 회귀 0).
- **스코어 인터페이스 3분할 기각** — 차이가 로직 아닌 값. 순수함수 + enum + 데이터로 충분(제약 8·10).

## critic 리뷰 반영 (REVISE → 적용)

1. 스펙 문서를 컷에 동기화(구 unit 0/3/4·계약 stale 제거).
2. **엔드리스를 공용 풀에서 빼고 전용 encounter 로** → 선택 제외 필터(구 unit 5) **삭제**, 토너먼트 회귀 0.
3. 누수 페널티 **saturation** 방지 — 예산 높게 + unit 4 어서션.
4. 스모크에 **리스크/리워드 어서션** 추가(배관만 검증 금지).
5. 세부: 시간0 은 CheckVictory 비활성 대신 **한 줄**; 무제한-only 라 `endlessNoDefeat` 필드 제거(`!IsEndless`).

## 포인터

- 구현 스펙: `docs/spec/endless-mode/README.md`
- 밸런싱: `docs/reference/map-wave-balancing.md` · 점수: `docs/reference/score-formula.md`
