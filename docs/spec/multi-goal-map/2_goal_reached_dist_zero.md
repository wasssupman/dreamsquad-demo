# 2. 골 판정 → IsGoalCell (도달 + wall 예외 + 해저드 검증)

## 목적

멀티골의 **행동 핵심**. 골 관련 셀 판정을 단일 `goalCell` 동등 비교에서 **`FlowFieldSingleton.IsGoalCell(cell)`** 로 전환 → 어느 골이든 도달/보호가 걸린다.

> **구현 중 접근 변경(2026-07-23)**: 초안은 `dist[idx]==0` 을 쓰려 했으나 **다수 EditMode 픽스처가 `dist` 를 all-zero NativeArray 로 두고 `goalCell` 만 세팅**한다(dist 는 안 쓰던 값). `dist==0` 으로 바꾸면 그 픽스처에서 **모든 셀이 dist==0 → 전부 골로 오판** → MovementCellTrim/Hazard/MovementSystem 픽스처 8개+가 깨진다(리뷰 2 의 "대부분 무해" 는 오판 — dist 가 실제로는 all-zero). 대신 **`FlowFieldSingleton` 에 `goals` 집합 + `IsGoalCell` 헬퍼**(goals 멤버십, **미설정 시 goalCell 폴백**)를 둔다: goals 를 안 채우는 픽스처는 goalCell 폴백으로 **무변경 통과**, 프로덕션은 goals 로 멀티골. dist 의미(FrontmostTargeting 등)도 안 건드린다.

## 변경 대상 (goalCell 동등 비교 4곳 + 싱글턴 헬퍼)

- `Assets/_Project/Scripts/Battle/Effects/FlowFieldSingleton.cs` — `NativeArray<int2> goals` 필드 + `bool IsGoalCell(int2)` (goals 멤버십/goalCell 폴백) + Dispose goals. `BattleBridge.BuildFlowField` 가 Persistent goals 를 싱글턴에 저장(유닛 1 블록 확장, 소유권→싱글턴, TeardownFlowField dispose).
- `MovementSystem.cs:127` — **골 도달**: `!hunting && cell==goalCell` → `!hunting && field.IsGoalCell(cell)`
- `MovementCellTrim.cs:20` — **wall 예외**: `cell.Equals(field.goalCell)` → `field.IsGoalCell(cell)`
- `EffectSpawner.cs:180` — **해저드 배치 검증**: `cell.Equals(ff.goalCell)` → `ff.IsGoalCell(cell)`
- `MovementIntegritySmokeTest.cs`(PlayMode) `:117,145` — walkability proxy: `cell==goalCell` → `field.IsGoalCell(cell)`

## 구현

1. `IsGoalCell`: goals.IsCreated && Length>0 이면 1~4 소량 루프 멤버십, 아니면 `cell.Equals(goalCell)`. Burst 호환(NativeArray 인덱싱 + int2.Equals).
2. `BuildFlowField`: goals 를 Persistent 로 만들어 BFS 소스 겸 싱글턴 저장. 성공 시 소유권 싱글턴 이관, 예외 시 catch 에서 dispose(이중 dispose 없음).
3. `MovementSystem` 의 `!hunting` 가드 보존.

## 계약

- **단일골/픽스처 회귀 0**: goals 미설정 → goalCell 폴백 → 기존 `cell==goalCell` 과 동일. 프로덕션 goals=[g] → 그 한 칸.
- 판정이 **골 개수·위치에 무관**해짐(멀티골 핵심 불변식). dist 의미 불변.
- Movement/Effects 는 FlowFieldSingleton(Effects 소유)을 **읽기만**.

## 완료 기준

- [x] FlowFieldSingleton.goals+IsGoalCell+Dispose, BuildFlowField Persistent goals 저장, 4개 사이트 IsGoalCell
- [x] 단일골 맵·기존 픽스처 회귀 0(EditMode green — dist all-zero 픽스처 무변경 통과)
- [x] 2골 멤버십: IsGoalCell 이 goals 전체에 true(ecs-review CONFIRM). 실전 2골 도달 e2e 는 유닛 6(실 멀티골 맵)에서
- [x] compile 0 error, EditMode green
- [x] **ecs-reviewer** 통과(Movement 도달 + Effects goals lifecycle)

확인 2026-07-23 — 접근 변경(dist==0 → IsGoalCell goals 멤버십/goalCell 폴백)으로 dist all-zero 픽스처 무변경 통과. 검증: 병행 세션 WIP 로 메인 컴파일 일시 차단 → wassup-testrig 격리 배치로 EditMode 1274 green(compile 0) 실증, 이후 메인 컴파일 회복돼 유닛 3 포함 EditMode 1276 green 재확인. ecs-reviewer: goalsField 소유권 이관·double-dispose·Burst·경계 전부 SOUND, 지적 0(LOW 스타일뿐).