# 2 — aggro 타일 제약 (①)

## 목적

aggro 의 본질은 **이동목표 변경뿐**(goal→guardian). 현재 aggro 분기는 guardian 으로 직선 self-walk 후 `continue` 로 cell-trim 을 스킵 → 프랍/Place 타일 위로 이동하고 guardian 의 Place 타일에 적층한다. 이를 **다른 모든 이동과 같은 cell-trim 에 통과**시켜 walk 타일 위에 묶는다.

## 변경 대상

- `Assets/_Project/Scripts/Battle/Movement/MovementCellTrim.cs` — 인라인 cell-trim 을 `Apply(desired, currentCell, field, hasObstacles, obstacles)` 헬퍼로 추출.
- `Assets/_Project/Scripts/Battle/Movement/MovementSystem.cs` — flow 분기는 `Apply` 호출로 교체. **aggro 분기**: guardian 방향 step → `Apply` 통과(`continue` 의 bypass 제거).
- `Assets/_Project/Tests/EditMode/MovementCellTrimApplyTests.cs` (신규) — 5종.

## 구현

- `Apply`: `desired` 가 wall(zero-flow) 또는 obstacle 셀로 넘어가면 `currentCell` 경계로 clamp, 아니면 통과. flow·aggro 두 분기가 공유하는 단일 지점.
- aggro 분기: target=guardian 의 greedy step 을 `Apply` 로 클램프 → non-walk(Place 포함) 진입 0. 적은 guardian 인접 walk 타일 경계에 정착, 공격은 기존 `AttackSystem`(sticky-target)이 사거리에서 처리. **별도 사거리정지/stuck 코드 없음**(graceful degrade: 벽 뒤 guardian 은 greedy 가 벽 따라 sliding).
- **return 흡수**: aggro 가 타일 위에 머무므로 guardian 사망(target→goal) 시 현재 walk 셀에서 flow 재개 = 깨끗한 복귀. 별도 return 로직 불필요. unit 1+2 가 aggro 생애주기 전부 커버.

## 완료 기준

- compile 0 에러.
- EditMode `MovementCellTrimApplyTests` 5종(same-cell 불변 / walk 통과 / wall clamp / OOB clamp / obstacle clamp) + 기존 무회귀 green.
- Play 동적 검증(unit 3): aggro 시작 시 적이 guardian 으로 **타일 위** 접근(Place/프랍 진입 0), guardian 사망 시 **타일 위로** goal 경로 복귀.
