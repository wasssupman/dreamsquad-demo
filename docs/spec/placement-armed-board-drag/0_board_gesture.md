# 0 — 보드 제스처 상태기계 + 드래그-릴리즈 커밋

**작업 구분**: feature (선행 없음, 이 spec 의 토대)

## 목적

arm 된 유닛으로 보드를 조작하는 입력을, "탭=즉시 시뮬 비행 배치"(`HandleArmedBoardTap`)에서
**press → 이동량 기반 tap/drag 판정 → release 분기** 상태기계로 바꾼다. 드래그-릴리즈는 유효셀에서
기존 시뮬 비행(`SimulateDragTo`)을 재사용해 배치한다. 탭은 이 unit 에선 no-op(범위 피크는 unit 2,
스카우트 비주얼은 unit 1).

## 변경 대상

- `Assets/_Project/Scripts/Data/DragSwaySettings.cs` — 그룹 ⑨ `boardDragThreshold`(스크린 px) 추가.
- `Assets/_Project/Scripts/UI/DefenderDragPlacementController.cs` — `HandleArmedBoardTap` 제거,
  `UpdateBoardGesture`/`CommitBoardDrag`/`ResetBoardGesture` 신설, 제스처 상태 필드, `HasArmedUnit` seam,
  `Update()` 진입 교체, `Disarm()` 에서 제스처 리셋.
- `Assets/_Project/Scripts/UI/Dreamcatcher/DcInspectController.cs` — `Blocked()` 에 `drag.HasArmedUnit` 추가.

## 구현

### 상태기계 (`UpdateBoardGesture`)

`Update()` 진입: `if (_armedUnit != null && !_session.active) UpdateBoardGesture();`

- **press 다운**(`wasPressedThisFrame`, `!_boardGestureActive`): 가드 3종 통과 시 제스처 개시.
  같은 프레임에 이어서 이동/릴리즈도 평가한다(early-return 금지 — 순간 탭이 stuck 되는 회귀 방지).
  - 가드: `GameManager.IsAiming` 무시 · `PointerOverUi()`(터치 touchId) 제외 · `bridge.TryScreenToCell` 실패(보드 밖) 무시.
  - `_boardDownScreen = down; _boardGestureActive = true; _boardDragging = false;`
- **이동 → 드래그 승격**: `Vector2.Distance(cur, _boardDownScreen) >= max(1, Cfg.boardDragThreshold)` 이면 `_boardDragging = true`.
- **release**(`wasReleasedThisFrame`): `_boardDragging` 이면 `CommitBoardDrag(cur)`, 아니면 탭(현재 no-op). 이후 `ResetBoardGesture()`.

### 커밋 (`CommitBoardDrag`)

```
if (!bridge.TryScreenToCell(cam, screen, out cell)) return;   // 보드 밖 릴리즈 = 취소(arm 유지)
if (bridge.CanPlaceDefenderAt(cell.x, cell.y, _armedUnit, out _))
    SimulateDragTo(_armedUnit, _armedFromScreen, cell);        // 기존 tray→cell 비행 재사용(내부 BeginDrag 가 Disarm)
else
    bridge.FlashPlacementReject(cell);                         // arm 유지(재시도)
```

`SimulateDragTo` 인자는 호출 전에 `_armedUnit/_armedFromScreen` 로 평가되므로, 내부 `BeginDrag→Disarm` 이
arm 을 비워도 값은 안전하다. 커밋 성공 시 arm 은 자연히 해제(비행 = 배치 확정).

### DcInspect 양보 seam

`HasArmedUnit => _armedUnit != null` 노출. `DcInspectController.Blocked()` 에
`|| drag.HasArmedUnit` 추가 → **armed 인 동안 보드 press 는 배치 제스처가 단독 소유**(인스펙트 양보).
arm 은 직전 프레임에 확정돼 있어 press 프레임 실행순서(-50 DcInspect → 0 컨트롤러)와 무관 —
계약 11 의 두-소비자 aim-mode race 를 재생산하지 않는다. 세컨드 탭 핸들러를 새로 만들지 않는다.

## 완료 기준

- 컴파일 통과(Unity 없이 검증 시 `dotnet build`).
- Play(에디터): 유닛 arm 후
  - 보드를 **눌러 드래그**하다 유효셀에서 손을 떼면 → 유닛이 트레이에서 그 칸으로 날아와 배치된다.
  - 보드를 **짧게 탭**(안 움직이고 다운→업)하면 → 아무 일도 없다(배치 X, arm 유지).
  - 무효셀(점유/코스트부족)에서 드래그 릴리즈하면 → reject 플래시 + arm 유지.
  - armed 상태에서 보드 유닛을 탭해도 **인스펙트 패널이 열리지 않는다**(양보).
- 탭/드래그 구분이 **누른 시간과 무관**하게 이동량으로만 갈린다(길게 눌렀다 안 움직이고 떼면 탭).
- 트레이 D&D(슬롯에서 끌어 배치)는 그대로 동작.
- 드래그 중 범위/hover 시각 피드백은 **unit 1** 에서 추가(이 unit 은 게이팅 로직만).

사용자 Play 확인: **통과 2026-07-20** · 구현 커밋 `bc30446d`
