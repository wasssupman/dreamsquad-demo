# 0 — SimulateDragTo 드라이버 (월드 공간 비행)

**작업 구분**: feature (토대) · 의존: placement-cell-snap(드래그 파이프라인)

## 목적

기존 드래그 파이프라인을 스크립트로 구동해 "진짜 드래그처럼" 배치를 재생하는 코어 코루틴.
탭 선택(unit 1)·보드 탭(unit 2)이 호출한다.

> **스크린 역산 방식 폐기 이력**: 처음엔 목표 손가락 화면좌표(toScreen)를 역산해 `UpdateDrag(스크린 트윈)` 으로
> 구동했으나 반복 오배치 — ① unit 5 스큐 보정이 셀 중심 조준을 "레이 히트 열"로 되밀고, ② 비행(수 초) 중
> `SetDragFocus` 로 카메라가 dolly 이동해 시작 시 계산한 화면 목표가 stale. 최종 = **월드 공간 직접 구동**
> ("유닛 위치를 시뮬하면 키링이 config 대로 따라온다"). 스크린 역산 재도입 금지.

## 변경 대상

- `Assets/_Project/Scripts/UI/DefenderDragPlacementController.cs` — `SimulateDragTo` / `RunSimulatedDrag` /
  `CommitPlacementAt` / `ScreenToBoardFeet` / `_sessionGen`
- `Assets/_Project/Scripts/Data/DragSwaySettings.cs` — `tapTravelDuration`(3s), `tapTravelScaleMin`(0.25), `tapTravelScaleMax`(1.5)

## 구현

- **API**: `SimulateDragTo(DefenderUnitData unit, Vector2 fromScreen, Vector2Int targetCell)` —
  가드(`unit/bridge` null, `_session.active` 중복) 후 `StartCoroutine(RunSimulatedDrag)`.
- **코루틴 `RunSimulatedDrag`**:
  1. `BeginDrag(unit, fromScreen, simulated:true)` — 세션/프리뷰/슬로우모/컷신 셋업. `_simulatedDrag` 는 BeginDrag 가
     CleanupSession 직후·첫 UpdateDrag 전에 세팅(범위 억제가 첫 프레임부터 적용).
  2. **세대 캡처**: `int gen = _sessionGen;` — CleanupSession 마다 증가하는 토큰. 루프 조건과 종료 가드에서
     `_sessionGen == gen` 을 확인해, 비행 중 새 드래그(BeginDrag→CleanupSession→새 세션 active=true)가 시작되면
     **커밋 없이 물러난다**. `_session.active` 만 보면 새 세션을 하이재킹한다(리뷰 확정 버그).
  3. **좌표**: `endFeet = bridge.GridCellToViewCenter(targetCell)`(보드 평면 위 셀 중심, view world).
     `startFeet = ScreenToBoardFeet(fromScreen, fallback: endFeet)`(트레이 탭 레이의 보드 평면 히트; 미스 시 폴백 —
     그 경우 비행이 무동작 팝이 되는 건 수용된 degenerate).
     `boardN` 은 카메라 쪽으로 뒤집은 보드 노멀. `totalDrop = unitHeight + ropeLength × visualScale`.
  4. **비행 시간**: `dur = tapTravelDuration × clamp(|WorldToScreen(startFeet)−WorldToScreen(endFeet)| / Screen.height, tapTravelScaleMin, tapTravelScaleMax)`.
  5. **월드 트윈**(OutCubic `e = 1−(1−t)³`, `Time.unscaledDeltaTime`): 시작 전 `_onBoard=true; _posInit=true;
     _unitPosWorld=start; _unitVelWorld=0` 로 렌더 활성. 매 프레임:
     `feet = Lerp(startFeet, endFeet, e)`; `_unitTargetWorld = feet + boardN×previewHeight`(스프링 타깃);
     `_ringWorld = feet + camUp×totalDrop`(고리); `_lastScreenPos = WorldToScreen(_ringWorld)`(카메라 포커스 피드).
     Update() 가 이 값으로 스프링/줄/고리/스윙/hover 를 config 대로 렌더 — 시뮬 전용 렌더 코드 없음.
  6. **종료**: 세대/active 재확인 → `_unitTargetWorld/_ringWorld` 를 endFeet 기준으로 고정, `_debounce = default`
     (throttle 스킵 — 탭은 명시적 의도) → `CommitPlacementAt(targetCell)`.
- **`CommitPlacementAt(cell)` (드롭과 공용 커밋 꼬리)**: 검증은 `bridge.TryBeginDefenderDeployment` 내부
  (CanPlaceDefenderAt)가 **단일 담당**(사전 중복 검증 금지). 성공 = `CleanupSession` + `RunDeployment` 코루틴,
  실패 = `FlashPlacementReject(cell)` + `CleanupSession`. `EndDrag` 도 이 헬퍼를 사용(경로 간 동작 단일화).

## 완료 기준

- 컴파일 클린. 세대 토큰: 비행 중 실드래그 시작 → 시뮬이 즉시 물러나고 실드래그 정상(하이재킹 없음).
- Play: 탭한 칸에 정확히 안착(오버슛/텔레포트 없음), 키링 스윙·줄이 드래그와 동일.
- 비행 시간이 거리 비례(가까운 칸 짧게, 먼 칸 길게).
