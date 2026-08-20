# 3 — Handoff Summary

**상태**: 완료 2026-07-20 (units 0~2 Play 확인)

## Commit

- `bc30446d` feat unit 0 — 보드 제스처 상태기계 + 드래그-릴리즈 커밋 (+ `7643beaf` 확인 기록)
- `e88fb071` feat unit 1 — 범위-only 스카우트 (+ `167dea87` 확인 기록)
- `5b1c575f` feat unit 2 — 탭 배치 + 비행 중 공격범위 노출

## Implemented

- arm(트레이 슬롯 탭)된 유닛으로 **보드 프레스-드래그-릴리즈** 상호작용을 도입. 기존 `HandleArmedBoardTap`(탭=즉시 배치) 대체.
- **탭/드래그 구분 = 이동량**(`boardDragThreshold` px, 시간 delta 아님).
- **드래그**: 프레스부터 손가락 셀의 공격범위+hover 를 range-only 스카우트(키링 유닛 없음, 유닛은 트레이에 잔류). 릴리즈(유효셀)=배치.
- **탭**: 기존 클릭과 동일하게 즉시 비행 배치 + 공격범위를 **비행 중에만** 노출(배치=착지 시 소거, linger 없음).
- 배치 커밋은 tap·drag 모두 기존 시뮬 비행(`SimulateDragTo`, tray→cell) 재사용 → `CommitPlacementAt`→`TryBeginDefenderDeployment` 공용 꼬리(directional/일반 분기 불변).
- 무효셀 탭/드래그 릴리즈 = `FlashPlacementReject` + arm 유지. 성공 배치는 arm 해제.
  - **⚠ unit 4(2026-08-20)가 뒤집음**: 무효셀 = reject + **Disarm**, 맵 밖 = 취소음 + **Disarm**.
    셀 판정도 `TryResolveArmedCell`(D&D 와 같은 격자 밖 관용)로 이동. 최신은 `4_invalid_release_disarm.md`.
- `DcInspectController.Blocked()` 에 `drag.HasArmedUnit` 추가 — armed 동안 보드 press 는 배치 제스처 단독 소유(계약 11 aim-mode race 재생산 방지).

## Key Files

- `Assets/_Project/Scripts/UI/DefenderDragPlacementController.cs` — `UpdateBoardGesture`/`CommitBoardDrag`/`HandleBoardTap`/`ResetBoardGesture`/`UpdateBoardScout`/`ClearBoardScout`/`StartTapPlaceRangePeek`·`RunTapPlaceRangePeek`·`CancelTapPlaceRangePeek`, `HasArmedUnit` seam. 제스처 상태 필드 `_boardGestureActive/_boardDragging/_boardDownScreen/_boardScoutCell/_tapPlaceRangeRoutine`.
- `Assets/_Project/Scripts/Data/DragSwaySettings.cs` — 그룹 ⑨ `boardDragThreshold`.
- `Assets/_Project/Scripts/UI/Dreamcatcher/DcInspectController.cs` — `Blocked()` seam 확장.

## Verified

- `dotnet build Wassup.Runtime.csproj` 오류 0 (각 unit).
- 사용자 Play(에디터): 드래그 배치 / 탭 배치 / 무효셀 reject / 스카우트 범위 추종 / 비행 중에만 범위 / armed 중 인스펙트 양보 / 트레이 D&D 무회귀 — 전부 확인.

## Notes (되돌리면 안 되는 의도)

- **탭 범위 flourish 는 비행 세션 수명에 묶인다**(`_session.active && _simulatedDrag` 동안만 재확인, 배치 시 소거). "착지 후 linger" 는 사용자 정정으로 폐기 — 다른 배치 동작과 동일하게 "날아가는 동안만". 되살리지 말 것.
- **`_tapPlaceRangeRoutine` 은 별도 소유**: 자기 flight 의 `Disarm`/`ResetBoardGesture` 에 죽으면 안 된다. 취소는 새 press·`BeginDrag(!simulated)` 뿐. (매 프레임 `SetPlacementRange` 재확인은 비행 sim 경로의 `CleanupSession` clear 를 덮어쓰기 위함 — 줄이지 말 것.)
- **DcInspect 양보 = `HasArmedUnit`**(제스처 활성 여부 아님): press 프레임 실행순서(-50 DcInspect → 0 컨트롤러) race 를 피하려면 arm 은 직전 프레임에 확정된 상태여야 한다. `IsArmedBoardGesture` 류로 좁히면 press 프레임 race 재생산.
- **range-only 스카우트**(키링 유닛 없음)는 사용자 결정. 커밋은 시뮬 비행 재사용이라 tap-to-place 비행 자산 보존.

## Follow-up (후속 후보)

- 연속 배치(커밋 후 arm 유지) · 스카우트 중 키링 유닛 프리뷰 · 범위 per-cell alpha 페이드(현재 `TilemapMapView` 펄스가 alpha 단독 소유) · 스카우트 중 유효 타일 프리하이라이트(`placement-eligible-tile-highlight` 재사용).
