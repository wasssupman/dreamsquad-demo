# 4 — 비용 부족 차단과 배치 거부 원인 피드백

## 목적

구매 불가능한 유닛을 전장까지 끌고 간 뒤 같은 빨간 실패로 거절되는 피로를 줄인다. 비용 부족은 슬롯에서 즉시 막고, 드래그 중 다른 거부 사유는 색 외 표식과 짧은 원인으로 구분한다. 선행: units 0~3.

## 변경 대상

- `Assets/_Project/Scripts/UI/DefenderDragSlot.cs`
- `Assets/_Project/Scripts/UI/DefenderSelector.cs`
- `Assets/_Project/Scripts/UI/DefenderDragPlacementController.cs`
- `Assets/_Project/Scripts/UI/CostDisplay.cs`
- `Assets/_Project/Scenes/BattleScene.unity`

## 구현

- `DefenderDragSlot.OnBeginDrag`에서 `CostRuntime.CanAfford(unit.cost)`를 확인하고 부족하면 controller drag session을 시작하지 않는다.
- 차단 시 `CostDisplay`의 짧은 rail pulse와 `코스트 N 부족` 피드백을 0.6초 내로 표시한다. 슬롯이 runtime 생성되므로 Selector가 직접 참조를 bind하며 새 전역 event를 만들지 않는다.
- drag가 시작된 뒤에는 `BattleBridge.CanPlaceDefenderAt(..., out reason)`의 reason을 버리지 않고 controller가 보관한다.
- `InsufficientCost`, `Occupied`, `NotBuildable/OutOfBounds`, 기타를 coral ×/amber lock/neutral 표식과 짧은 한글 label로 매핑한다.
- 배치 권한과 최종 비용 차감은 계속 `BattleBridge.TryBeginDefenderDeployment`에 남긴다. UI는 사전 피드백만 담당한다.

## 완료 기준

- [x] 비용 부족 슬롯에서 preview/slomo/drag session이 시작되지 않음.
- [x] 부족량과 rail pulse가 표시되고 연속 입력에도 coroutine/scale 상태가 누적되지 않음.
- [x] 점유/배치불가/범위밖이 색 외 표식과 원인 label로 구분됨.
- [x] drag 도중 cost가 변해도 최종 `BattleBridge` 검증이 권한을 유지함.
- [x] 유효 배치는 기존 키링 프리뷰·범위 표시·비용 차감과 동일하게 성공.

확인 2026-07-12 — 스크립트 배틀 검증: cost=2 에서 4코스트 슬롯 OnBeginDrag → 세션 미시작(reflection sessionActive=False) + "코스트 2 부족" 레일 펄스 캡처(warn 틴트+빨간 숫자). 드래그 hover 사유 라벨: NotBuildable="— 배치 불가"(neutral), Occupied="■ 점유됨"(amber) — 실측 캡처, 글리프+한글+색 3중 표기. 유효 셀 EndDrag → defender 0→1 생성(TryBeginDefenderDeployment 경로 무변경 = 권한/차감 보존). 펄스는 단일 코루틴 핸들 리셋 구조(누적 없음), 라벨은 ClearHover/CleanupSession 에서 숨김. 씬 배선 1줄(Selector.costDisplay). 콘솔 0.
