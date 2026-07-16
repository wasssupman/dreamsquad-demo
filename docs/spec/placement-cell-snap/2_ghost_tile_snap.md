# 2 — 고스트 타일 중심 스냅 (C)

**작업 구분**: feature (C) · 의존: unit 1

## 목적

유닛 고스트의 스프링 rest 타깃을 포커스 셀(unit 1 의 `_focusedCell`) **중심**으로 스냅해,
배치될 타일을 시각적으로 확정한다. 링=손가락, 줄=링→유닛 머리를 유지해 키링 시인성을 살린다.

## 변경 대상

- Modify: `Assets/_Project/Scripts/UI/DefenderDragPlacementController.cs`
- (필요 시) Modify: `Assets/_Project/Scripts/Bridge/BattleBridge.cs` — 셀 중심 view-world read 헬퍼

## 구현

- **셀 중심 → 유닛 타깃(view world)**:
  - sim 중심 = `GridMath.CellToWorldCenter(cell, tileSize, _boardOrigin.y, _boardOrigin)` (bridge 1657행 헬퍼 재사용/승격).
  - `viewCenter = BoardSpace.ToView(simCenter)`; 발 띄움은 기존과 동일하게 보드 노멀 방향 `previewHeight` 가산.
  - 이 값을 **스프링 타깃**으로 사용: `_unitTargetWorld = snappedFeetWorld`.
- **Update 순서**: 스프링 스텝(현 150행) **이전**에 포커스 셀이 확정돼야 하므로,
  `UpdateHoverAtTarget()`(셀 확정)을 스프링 스텝 앞으로 옮기거나, 셀 확정→타깃 계산을 `UpdateDrag`/전용 헬퍼로 끌어올린다.
  링(`_ringWorld`)/줄 계산은 손가락 기준 그대로(변경 없음).
- **부드러움 유지**: 스프링 타깃만 셀 중심으로 바꾸고 `KeyringSim.SpringStep` 파라미터(spring/damping/maxSpeed)는 기존 값 재사용 → 이동 중 스윙 살고 멈추면 중심에 안착. 새 SO 필드는 튜닝 결과 부족할 때만 추가.
- **무효/오프보드**: 오프보드는 기존대로 프리뷰 숨김. 무효 셀(점유 등)도 포커스 셀 중심으로 스냅(빨강 hover 로 "이 칸이지만 불가" 표시) — valid/invalid 로 스냅 여부를 가르지 않는다.
- 실제 배치된 유닛(`ActivateDeployedDefender` 결과)에는 스냅/스윙 없음(뷰 프리뷰 전용).

## 완료 기준

- 컴파일 통과, EditMode 회귀 없음.
- Play/오프스크린 스크린샷: 유닛 고스트가 포커스 타일 중심에 앉고, 손가락이 타일 사이일 때 줄이 손가락~유닛으로 자연스럽게 늘어남(키링 또렷).
- 유닛이 손가락 밑으로 끌려 올라가 가려지지 않음(화면상 `totalDrop` 아래 유지).
- 셀 전환 시 순간이동이 아니라 부드럽게 안착(딱딱하면 spring/damping 튜닝, 필요 시 SO 필드).
- 사용자 Play 체감 확인 일자 + 커밋 해시 추가 후 커밋. 이후 `3_handoff_summary.md` 작성.
