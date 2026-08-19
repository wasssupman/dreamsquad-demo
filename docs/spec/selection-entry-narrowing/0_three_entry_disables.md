# 0 — 스위치 3개로 진입구 끄기

## 목적

항아리 탭 · 보드 유닛 탭 · 선택 줌을 각각 스위치 하나로 끈다. 기능 코드는 전부 남긴다.

## 변경 대상

- `Assets/_Project/Scripts/UI/Dreamcatcher/AwakeningGaugeView.cs` — `JarTapEnabled`
- `Assets/_Project/Scripts/UI/Dreamcatcher/DcInspectController.cs` — `BoardTapSelectEnabled`, `InspectZoomEnabled`

## 구현

### A. 항아리 탭 (`JarTapEnabled = false`)

독 패널을 만드는 자리에서 `onClick` 배선을 **아예 걸지 않고**, `button.interactable` 과
`hitGraphic.raycastTarget` 을 같은 값으로 내린다. `Toggled` 이벤트·구독자
(`DreamcatcherHandView.OnToggled`)·`SetOpen`/`Pulse`/`HitRect` 는 손대지 않는다 — 발화원만
사라진다. 손패는 `OpenForSelection` 하나로만 열린다.

히트를 함께 놓는 것이 요점이다. 놓지 않으면 손패가 열린 동안 항아리 위 탭이
`HandDismissTapCatcher` 에 닿지 않아 그 자리만 «닫히지 않는 구역» 이 된다.

### B. 보드 유닛 탭 (`BoardTapSelectEnabled = false`)

선택을 만드는 지점 둘만 게이트한다.

- `HandleTap` (손패 닫힘 · raw 포인터 경로) → 게이트가 닫히면 `Close()` 후 반환.
- `OnBoardTapped` (손패 열림 · `HandDismissTapCatcher` 경로) → `TryPick` 분기를 건너뛰고
  `CloseByIntent()`.

`TryPick` · `Select` · `SelectDeployed` 는 그대로 산다. 트레이 경로
(`DefenderDragSlot.GoToDeployedUnit`)는 `SelectDeployed` 를 직접 부르므로 이 게이트를 지나지
않는다 — **입구가 갈라져 있어서 한쪽만 끌 수 있다**는 것이 이 변경이 성립하는 이유다.

### C. 선택 줌 (`InspectZoomEnabled = false`)

`TickSelectionAnchor` 의 `cameraDirector.SetInspectFocus(anchor.position)` 피드만 막는다.
같은 메서드가 하는 나머지 둘(앵커 소실 = 사망 감지, 실효 스탯/액션 버튼 갱신)은 그대로 돈다 —
**줌만 끄고 선택의 수명 관리는 살려야** 부착 0장 유닛이 죽었을 때 좀비 선택이 남지 않는다.

## 완료 기준

- [x] compile 클린 (2026-08-19 — `dotnet build Wassup.Runtime.csproj` 오류 0, Unity 콘솔 0)
- [x] Play: 항아리를 눌러도 손패가 열리지 않는다. 손패가 열린 상태에서 항아리 위를 누르면 닫힌다.
- [x] Play: 판 위 유닛을 탭해도 선택되지 않는다(손패가 열려 있었으면 닫힌다).
- [x] Play: 하단 트레이 셀(배치 완료된 유닛)을 탭하면 그 유닛이 선택되고 손패가 뜬다.
- [x] Play: 선택해도 카메라가 당겨지지 않는다. 방향 지정 배치의 셀 포커스는 **그대로 동작**한다.
- [x] Play: 선택 중 부착(탭·드래그)과 액티브 카드 드래그가 종전대로 동작한다.

> 사용자 Play 확인 2026-08-19 · 커밋 `df1d6f9d`
