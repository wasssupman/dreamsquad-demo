# 1 — 선택 → 손패 자동 오픈 + 모드 분기 + 선택 수명

> rev 2 (2026-07-29 critic 반영): pending-open 래치(H5) · 닫기 창구 단일화(H3) ·
> 오픈 모드 분기(H4, 사용자 결정 5) · 앵커 liveness(M3) · Pulse 생략(L1).

## 목적

유닛 선택이 드림캐쳐 손패를 **항상** 함께 연다(사용자 결정 1). 손패는 자기 오픈이 선택 기인인지
일반(항아리) 기인인지 알고 단순 분기한다(사용자 결정 5). 카드 슬롯이 즉발 대상(선택 유닛)을 읽을
seam 을 뷰에 만들고, 선택 수명(사망 커버)을 컨트롤러에 보강한다.

## 변경 대상

- `Assets/_Project/Scripts/UI/Dreamcatcher/DreamcatcherHandView.cs`
- `Assets/_Project/Scripts/UI/Dreamcatcher/DcInspectController.cs`

## 구현

### A. HandView — 선택 파트너 API + 모드 분기

- `public Entity SelectionTarget { get; }` + `SetSelectionTarget(Entity)` / `ClearSelectionTarget()`
  — 즉발 대상 저장소. 뷰는 이 Entity 를 판정하지 않는다(계약 1).
- **모드는 파생값**: `bool InSelectionMode => SelectionTarget != Entity.Null` — 별도 상태 저장
  금지(이중 상태 함정). 분기 지점:
  - **슬로모(계약 8)**: 선택 모드에선 선택 lease(0.3× 상시, `DcInspectController` 소유)가
    지배 — `TickSlomo(held)` 는 코드 불변(동일 priority/scale 라 잉여 lease 무해, 검증 완료).
    일반 모드 = use-flow 계약 1 그대로. **이 unit 에서 TimeManager/lease 코드를 만지지 않는다.**
  - **`gaugeView.Pulse()` 생략**: Pulse 는 "항아리를 눌렀다 → 열렸다" 인과 힌트 — 선택 기인
    오픈에선 발화하지 않는다(critic L1).
- `public void OpenForSelection()` — `Open()` 재사용(딜인·strip 플립·SetOpen 전부 기존 경로).
  이미 Hand 면 no-op. **`Transitioning` 이면 무시가 아니라 `_pendingSelectionOpen` 래치를
  세운다**(critic H5) — 침강(sink 0.26s+stagger)+strip fold(0.14s) 동안 닫힘 전이가 돌고 있어
  이 창에서 선택하면 "손패 없는 선택"이 되고 항아리로도 못 연다(`OnToggled` 도 같은 가드).
  `Update()` 에서 `!Transitioning && _pendingSelectionOpen && InSelectionMode` 첫 프레임에
  `Open()`. 래치는 `ClearSelectionTarget`/`ForceClose` 에서 해제.
- `public void CloseFromSelection()` — `Close()` 재사용(침강). 이미 UnitStrip 이면 no-op.
  **컨트롤러가 뷰를 닫는 유일한 공개 창구**(계약 7 — 뷰 `Close()` 는 private 유지, critic H3).

### B. DcInspectController — 선택 수명에 손패 결합

- `Select(entity)`: 기존 처리 후 `handView.SetSelectionTarget(entity)` + `handView.OpenForSelection()`.
  선택 전환(A→B)은 `SetSelectionTarget` 갱신만 — 손패는 이미 열려 있다(재딜 없음).
- `Close()`: `handView.ClearSelectionTarget()` + `handView.CloseFromSelection()`.
  호출 규칙(계약 7): **닫기 의도 탭 경유 Close(unit 2 수신부)는 선택 유무 무관** —
  항아리 단독 오픈의 바깥 탭 dismiss 를 보존한다. 그 외 경로(사망/페이즈/이동모드/트레이/
  OnDisable)는 `_selected != Entity.Null` 이었던 호출만 손패를 닫는다(무선택 no-op 유지).
- `OnMovePressed`(이동모드 진입): 기존 `Close()` 경유로 손패도 닫힌다.
- **앵커 liveness(critic M3)**: 부착 0장 유닛이 죽으면 `AttachmentsChanged` 가 발화하지 않는다
  (`DreamcatcherHandController.OnDefenderDied` 조기 return) → 사망 닫힘이 그 이벤트에만 걸려
  있으면 선택·lease·열린 손패·죽은 `SelectionTarget` 이 좀비로 남는다. `FeedZoomTarget` 의
  앵커 실패를 승격: **연속 N프레임(SerializeField, 기본 3) `TryGetUnitViewAnchor` 실패 →
  `Close()`**. (일시 실패 흔들림 방지로 1프레임 판정 금지.)

### C. 손패 단독 닫힘 = 선택 유지

- 항아리 토글 `Close()` / 자동 닫힘(`OnCardUsed` 0장) / `ForceClose` 는 건드리지 않는다 —
  선택은 살아 있고 보드 탭 소유권만 inspect 로 복귀. 리티클 소생은 unit 4 의 `FocusCleared` 가 담당.
- 손패가 닫힌 채 선택이 살아 있으면 항아리 탭으로 재오픈 가능 — `SelectionTarget` 이 살아
  있으므로 재오픈은 자동으로 선택 모드다(즉발 동작).

## 완료 기준

- [ ] compile 클린
- [ ] Play: 유닛 탭 → 줌·리티클과 함께 손패 딜인(게이지 0 이어도 dim 카드로 등장, Pulse 없음)
- [ ] Play: 재탭(선택 해제) → 손패 침강 + 리티클/줌 해제 동시
- [ ] Play: **빈 보드 탭으로 전부 닫고 침강 중(0.4초 내) 즉시 다른 유닛 탭** → 침강 완료 후
      손패가 자동으로 다시 열린다(래치 검증 — 결정 1 "항상")
- [ ] Play: 선택 중 항아리 탭 → 손패만 닫히고 선택 유지, 항아리 재탭 재오픈 시 즉발 정상
- [ ] Play: 커밋으로 사용 가능 0장 → 자동 닫힘(재딜인 생략) + 선택 유지
- [ ] Play: **부착 0장 유닛을 선택한 채 사망** → 선택·손패·슬로모 전부 해제(좀비 없음)
- [ ] Play: 이동모드 진입 → 손패·선택 동시 해제
