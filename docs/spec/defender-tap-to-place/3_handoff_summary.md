# 3 — Handoff Summary

## Commit

- `dedde0f6` feat(placement): tap-to-place 탭 배치 + cell-snap unit 7 rev 끈적 액체 하이라이트
  (두 spec 이 같은 파일들(controller/bridge/view)에 얽혀 한 커밋. 병행 세션(directional-volley) hunk 는
  hash-object 분리 스테이징으로 제외 — 그 쪽 작업은 워크트리에 dirty 로 남아 있음)

## Implemented

- 트레이 슬롯 **탭 = arm 토글**(`IPointerClickHandler`, 드래그 임계 미만만 발화). 비용 부족 시 arm 하지 않고 pulse.
- arm 상태에서 **보드 탭 → D&D 시뮬 배치**: `SimulateDragTo` 가 BeginDrag→월드 트윈→확정을 스크립트로 재생
  → 키링(고리/줄/스프링)·hover·throttle·확정 팝·deploy 컷신 전부 실제 드래그와 동일 경로 재사용.
- 비행 = **월드 공간 트윈**(OutCubic): 발점을 tray→셀중심으로 직접 이동, 링=발점+camUp·totalDrop.
  화면 역산 없음 → 스큐/카메라 dolly 무영향(스크린 역산 방식의 오배치 원인 제거).
- 비행시간 = 기준(`tapTravelDuration`) × 화면거리 비례(min~max clamp).
- 시뮬 경로는 **공격 범위 프리뷰 억제**(`_simulatedDrag`, BeginDrag 파라미터로 첫 프레임부터).
- **세션 세대 토큰**(`_sessionGen`): 비행 중 새 드래그 시작 시 코루틴이 자진 종료(하이재킹 방지).
- `GameManager.CalibrateDragThreshold`: DPI 기반 `pixelDragThreshold` 보정(고DPI 탭→드래그 오인식 수정).
- 가드: 터치 touchId UI 판정(`PointerOverUi`), 스킬 조준 중 무시, 슬롯 파괴 시 자가 disarm(Unity `==`).

## Key Files

- `Assets/_Project/Scripts/UI/DefenderDragPlacementController.cs` — `ToggleArm`/`HandleArmedBoardTap`/`SimulateDragTo`/`RunSimulatedDrag`/`CommitPlacementAt`
- `Assets/_Project/Scripts/UI/DefenderDragSlot.cs` — `OnPointerClick`/`SetArmed`
- `Assets/_Project/Scripts/Core/GameManager.cs` — `CalibrateDragThreshold`
- spec: `docs/spec/defender-tap-to-place/{README,0,1,2}.md`

## Verified

- 컴파일 클린 · EditMode 20/20(snap+debounce) · 사용자 Play: arm→탭→비행→배치, 범위 억제, 오배치 해소 확인.

## Notes (되돌리면 안 되는 의도)

- **비행은 월드 트윈**. 화면 역산으로 되돌리면 비행 중 카메라 dolly(`SetDragFocus`)에 목적지가 밀린다(3회 실패 이력).
- `_simulatedDrag` 는 `BeginDrag(simulated:)` 파라미터로 세팅 — BeginDrag 내부 첫 `UpdateDrag` 가 SetHover 를
  부르기 전에 세팅돼야 범위가 한 프레임도 안 새어 나온다(사후 세팅은 리그레션).
- 검증은 `TryBeginDefenderDeployment` 내부 단일 담당 — `CommitPlacementAt` 앞에 사전 중복 검증 재도입 금지.

## Follow-up

- Android 실기기에서 DPI 보정·touchId UI 가드 체감 확인(에디터에선 검증 불가).
- arm 상태 시각(테두리 하이라이트)의 최종 아트 패스.
