# 2 — 고스트 타일 중심 스냅 (C) — **되돌림(REVERTED)**

> ⚠️ **이 unit 은 되돌렸다.** 고스트 스프링 타깃을 셀 중심으로 스냅하니 유닛이 셀에 얼어붙어
> 키링 줄/스윙(스프링·댐핑·길이)이 사라졌다(사용자 피드백). 고스트는 손가락을 연속 추종(키링 유지)으로
> 복원했고, "스냅 느낌"은 unit 4(하이라이트 확정 팝)가 대신한다. 아래 내용은 시도 기록으로 보존.
> 현재 코드: `ResolveFocusAndTarget` 은 셀만 확정(히스테리시스+디바운스), 스프링 타깃은 raw feet.

**작업 구분**: feature (C) · 의존: unit 1

## 목적

유닛 고스트의 스프링 rest 타깃을 포커스 셀(unit 1 의 `_focusedCell`) **중심**으로 스냅해,
배치될 타일을 시각적으로 확정한다. 링=손가락, 줄=링→유닛 머리를 유지해 키링 시인성을 살린다.

## 변경 대상

- Modify: `Assets/_Project/Scripts/UI/DefenderDragPlacementController.cs`
- (필요 시) Modify: `Assets/_Project/Scripts/Bridge/BattleBridge.cs` — 셀 중심 view-world read 헬퍼

## 구현 (실제)

- **원시 발점 / 스냅 타깃 분리**: 새 필드 `_feetRawWorld`(원시 손가락 발점, 셀 해석 입력) vs
  `_unitTargetWorld`(이제 스냅된 스프링 타깃). `UpdateDrag` 는 `_feetRawWorld` 만 저장.
- **`UpdateHoverAtTarget` → `ResolveFocusAndTarget` 로 개명·확장**: 입력을 `_feetRawWorld` 로 바꾸고,
  셀 확정(unit 1 히스테리시스)+hover+거부라벨 뒤에 스냅 타깃을 계산:
  `viewCenter = bridge.GridCellToViewCenter(cell)`(기존 card-fly 헬퍼 재사용, sim→view 중심)
  + 보드 노멀 방향 `previewHeight` 가산(`SnapNormalToward`, TryComputeRingUnit 과 동일 규약) → `_unitTargetWorld`.
- **Update 순서**: `ResolveFocusAndTarget()` 를 스프링 스텝(150행) **직전**으로 이동, 말미의 hover 호출 제거.
  링(`_ringWorld`)/줄/머리 계산은 손가락·`_unitPosWorld` 기준 그대로.
- **부드러움 유지**: 스프링 타깃만 셀 중심으로. `KeyringSim.SpringStep`(spring/damping/maxSpeed=SO 기존값) 재사용
  → 이동 중 스윙 살고 멈추면 중심 안착. 새 SO 필드 미추가.
- **무효/오프보드**: 오프보드는 기존대로 프리뷰 숨김·`ClearHover`. 무효 셀도 그 칸 중심으로 스냅(빨강 hover).
- **커밋 정합**: `EndDrag` 는 `hoverTile`(=스냅된 셀) 로 배치 → 고스트가 앉은 칸과 실제 배치 칸 일치.
- 실제 배치된 유닛에는 스냅/스윙 없음(뷰 프리뷰 전용).

## 완료 기준

- 컴파일 통과, EditMode 회귀 없음.
- Play/오프스크린 스크린샷: 유닛 고스트가 포커스 타일 중심에 앉고, 손가락이 타일 사이일 때 줄이 손가락~유닛으로 자연스럽게 늘어남(키링 또렷).
- 유닛이 손가락 밑으로 끌려 올라가 가려지지 않음(화면상 `totalDrop` 아래 유지).
- 셀 전환 시 순간이동이 아니라 부드럽게 안착(딱딱하면 spring/damping 튜닝, 필요 시 SO 필드).
- 사용자 Play 체감 확인 일자 + 커밋 해시 추가 후 커밋. 이후 `3_handoff_summary.md` 작성.
