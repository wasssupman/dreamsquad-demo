# Unit 14 — `goalEdgeOnly` Flag

## 목적

현재 6-section anchor zone 모델에서 goal cell 의 ~40 % 는 이미 map edge 위에 떨어진다. "엄밀히 edge 위에 강제" 하고 싶을 때 SO flag 1개로 후처리 필터 추가. 별도 placer 분기 / 별도 spec 불필요.

## 변경 대상

- 수정: `MapGridGenerationSettings.cs` — `goalEdgeOnly` SerializeField (default false) + 내부 setter.
- 수정: `GoalSpawnPlacer.cs` — goal pick 시 `settings.GoalEdgeOnly == true` 면 zone candidate 를 edge cell 로 필터.
- 수정: `BattleBridge.cs` / `DraftController.cs` — `SetGoalEdgeOnly(bool)` API (settings SO 의 PlayMode-only mutation).
- 수정: `MapSettingsPanelView.cs` — MapGrid 섹션에 "Goal Edge Only" 토글 1개.
- 신설: EditMode 테스트 `Pick_GoalEdgeOnly_GoalLandsOnMapEdge`.

## 정책

- **Default**: `false` — 기존 6-section 동작 그대로.
- `true` 일 때: goal candidate cell 셔플 후 `x==0 || x==W-1 || y==0 || y==H-1` 만 통과. anchor zone 안에 edge cell 이 없는 section 이 뽑히면 (이론상 모든 section 의 anchor 가 map boundary 에 닿으므로 발생 안 함) Pick 은 default 반환 → outer attempt 재시도.
- Spawn 은 영향 없음 (edge-only 강제 아님).
- SO 의 PlayMode 변경은 in-memory 전용. Inspector 에서 변경하면 영구.

## 완료 기준

- [ ] 컴파일 0 ERROR.
- [ ] EditMode 회귀 0 (단, 새 테스트 1개 추가).
- [ ] PlayMode: 패널 "Goal Edge Only" 토글 ON → 생성된 map 의 goal 이 항상 boundary 위. OFF → 기존 동작.
- [ ] 확인 일자 + 커밋 해시 (구현 후 채움):
