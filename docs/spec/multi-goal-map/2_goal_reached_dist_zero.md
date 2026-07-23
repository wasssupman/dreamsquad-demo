# 2. 골 판정 → dist==0 (도달 + wall 예외 + 해저드 검증)

## 목적

멀티골의 **행동 핵심**. 골 관련 셀 판정을 단일 `goalCell` 동등 비교에서 `dist[idx]==0`(모든 골 셀에서 참)로 전환 → 어느 골이든 도달/보호가 걸린다.

## 변경 대상 (goalCell 동등 비교 3곳 + 라이브 스모크 1곳)

> **리뷰 M1 정정**: 3곳은 성격이 다르다 — 각각의 실제 목적을 알고 바꿔야 오해 없음. 셋 다 `dist==0` 전환이 멀티골에 옳다.

- `MovementSystem.cs:127` — **진짜 골 도달**: `!hunting && cell==goalCell` → PastGoalTag 부착(누수). → `!hunting && field.dist[idx]==0`
- `MovementCellTrim.cs:20` — **wall 예외**(도달 아님): 골 셀은 flow=0 이라 "zero-flow=wall" 규칙에 걸려 적이 골 밖으로 clamp 되는 걸 막는 예외. `cell==goalCell` → `field.dist[idx]==0` → **모든 골 셀이 wall 예외**(멀티골에 정확히 필요)
- `EffectSpawner.cs:180` — **해저드 배치 검증**(도달 아님): blocking hazard 가 골 셀을 덮지 못하게. `cell==ff.goalCell` → `ff.dist[idx]==0` → **모든 골 보호**
- `MovementIntegritySmokeTest.cs:117,145` — **라이브 스모크**(리뷰 M1): `walk = flow!=0 || cell==goalCell` walkability proxy. 멀티골 풀에서 2차 골은 flow=0·非primary → blocked 오판 → red. → `... || field.dist[idx]==0`

## 구현

1. 각 사이트의 셀→idx 변환 확인(기존 flow/dist 인덱싱과 동일 함수 재사용 — MovementCellTrim 은 `:22` 에서 이미 idx 계산). `dist` 는 flow 와 동반 Persistent 할당·시스템이 FlowFieldSingleton 강제 → `dist[idx]` 는 기존 `flow[idx]` 와 동일하게 안전(리뷰 CONFIRM).
2. `dist[idx]==0` 로 교체. `MovementSystem` 의 `!hunting` 가드 보존(사냥 중엔 골 지나쳐도 누수 안 함).
3. 스모크 proxy 도 dist==0 로.

## 계약

- **단일골 회귀 0**: 단일 골 맵은 dist==0 이 그 한 칸뿐 → 기존 `cell==goalCell` 과 동일 결과.
- 판정이 **골 개수·위치에 무관**해짐(멀티골 핵심 불변식).
- Movement/Effects 는 dist(Effects 소유)를 **읽기만**(맥락 경계 준수).

## 완료 기준

- [ ] 3개 사이트 + 스모크 proxy dist==0 전환, !hunting 가드 보존
- [ ] 단일골 맵: 누수/trim/해저드검증 타이밍 기존과 동일(회귀). `MovementSystemTests.cs:115`(goalCell 도달 태깅)의 dist 세팅이 골 셀=0 인지 점검(리뷰)
- [ ] 2골 맵: 두 골 어느 쪽 도달해도 누수, 두 골 다 wall 예외·해저드 보호(PlayMode/scripted e2e)
- [ ] compile 0 error, EditMode green
- [ ] **ecs-reviewer** 통과(Movement 도달 + Effects 소비 변경)
