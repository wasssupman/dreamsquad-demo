# 1 — 선택 → 손패 자동 오픈 + 선택 타겟 전달 seam

## 목적

유닛 선택이 드림캐쳐 손패를 **항상** 함께 연다(사용자 결정 1). 선택 해제는 손패도 닫는다
(계약 7 비대칭 — 손패만 닫히는 경로는 선택 유지). 카드 슬롯이 즉발 대상(선택 유닛)을 읽을
seam 을 뷰에 만든다.

## 변경 대상

- `Assets/_Project/Scripts/UI/Dreamcatcher/DreamcatcherHandView.cs`
- `Assets/_Project/Scripts/UI/Dreamcatcher/DcInspectController.cs`

## 구현

### A. HandView — 선택 파트너 API

- `public void OpenForSelection()` — `Open()` 재사용(딜인·strip 플립·gaugeView.SetOpen 전부
  기존 경로). 이미 Hand 면 no-op. `Transitioning` 중이면 무시(OnToggled 의 mash guard 와 동일).
- `public void CloseFromSelection()` — `Close()` 재사용(침강). 이미 UnitStrip 이면 no-op.
- `public Entity SelectionTarget { get; }` + `SetSelectionTarget(Entity)` / `ClearSelectionTarget()`
  — 즉발 대상 저장소. **뷰는 이 Entity 를 판정하지 않는다**(계약 1) — 슬롯(unit 3)이 읽기만.

### B. DcInspectController — 선택 수명에 손패 결합

- `Select(entity)`: 기존 처리 후 `handView.SetSelectionTarget(entity)` + `handView.OpenForSelection()`.
  선택 전환(A→B)은 `SetSelectionTarget` 갱신만 — 손패는 이미 열려 있다(재딜 없음).
- `Close()`: `handView.ClearSelectionTarget()` + `handView.CloseFromSelection()`.
  - 단 `Close()` 는 여러 경로에서 불린다 — **손패 닫기는 "선택이 실제로 있었던" 경우만**
    (`_selected != Entity.Null` 이었던 호출만). 미선택 no-op 계약 유지.
- `OnMovePressed`(이동모드 진입): 기존 `Close()` 경유로 손패도 닫힌다(계약 7, catcher 가
  목적지 탭을 먹는 조합 차단).

### C. 손패 단독 닫힘 = 선택 유지

- 항아리 토글 `Close()` / 자동 닫힘(`OnCardUsed` 0장) / `ForceClose` 는 **건드리지 않는다** —
  선택은 살아 있고, 보드 탭 소유권만 inspect 로 복귀(unit 2 매트릭스).
- 손패가 닫힌 채 선택이 살아 있으면 다시 항아리 탭으로 열 수 있다(기존 토글) —
  `SelectionTarget` 은 선택이 살아 있는 동안 유지되므로 즉발도 다시 동작.

## 완료 기준

- [ ] compile 클린
- [ ] Play: 유닛 탭 → 줌·리티클과 함께 손패 딜인(게이지 0 이어도 dim 카드로 등장)
- [ ] Play: 재탭(선택 해제) → 손패 침강 + 리티클/줌 해제 동시
- [ ] Play: 선택 중 항아리 탭 → 손패만 닫히고 선택 유지, 재탭으로 재오픈 시 즉발 정상
- [ ] Play: 커밋으로 사용 가능 0장 → 자동 닫힘(재딜인 생략) + 선택 유지
- [ ] Play: 이동모드 진입 → 손패·선택 동시 해제
