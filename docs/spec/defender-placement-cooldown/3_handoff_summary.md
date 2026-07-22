# 3 — Handoff Summary

배치 쿨타임 feature 인계 지도. 최신 계약은 README / 번호 문서가 우선.

## Commit

- `76067285` unit 0 — SO 필드 + `PlacementCooldownRuntime` + GameManager/리셋 훅 + EditMode 테스트 + 씬 배선
- `e941fb25` unit 0 완료 스탬프(docs)
- `4b9caeeb` unit 1·2 — 시작·차단 + 액체 오버레이 + juice(3·4)

## Implemented

- `DefenderUnitData.placementCooldown`(초, 기본 0). **0 = 완전 inert**(분기 안 탐).
- `PlacementCooldownRuntime`(Mono, `GameManager.CooldownRuntime`): 유닛 타입 키드, `Battle` 도메인 self-tick(슬로모 감속·정지 동결), `_map` 비면 Update 조기반환. 리셋 = 배치페이즈 진입(`PlacementPhaseView`) + `BattleBridge` teardown(방어).
- 배치 성공(`DefenderDragPlacementController.PlacementCommitted`) → `StartCooldown(unit, placementCooldown)`. 슬롯 게이트(`DefenderDragSlot` 드래그/탭)로 쿨타임 중 배치 차단 — **ECS 미개입**(순수 Mono/UI).
- 트레이 셀 오버레이: 코스트 물통 셰이더(`Wassup/UI/CostWell`) **per-slot 재사용**, `_Fill=남은비율`로 탁한 슬레이트 액체가 아래로 빠지며 유닛이 떠오름 + **셀 전체 딤 스크림** + 중앙 카운트다운. 코스트와 **방향·색·위치·숫자** 4축 구분.
- juice(사용자 선택 3·4): 숫자 틱 스쿼시&스트레치 팝 + 종료 플러리시(스프링-아웃 · 잔물결 링 · 섬광), `EaseOutBack` 공유.

## Key Files

- `Assets/_Project/Scripts/Core/PlacementCooldownRuntime.cs` — 상태·tick
- `Assets/_Project/Scripts/UI/DefenderSelector.cs` — 오버레이 빌드/리페인트/juice, `PlacementCommitted` 구독
- `Assets/_Project/Scripts/UI/DefenderDragSlot.cs` — 배치 게이트
- `Assets/_Project/Scripts/Data/DefenderUnitData.cs`(필드) · `BattleHudTrayConfig.cs`(오버레이 튜닝값)
- `Assets/_Project/Scripts/Core/GameManager.cs` · `UI/PlacementPhaseView.cs` · `Bridge/BattleBridge.cs` — 노출/리셋
- `Assets/_Project/Tests/EditMode/PlacementCooldownRuntimeTests.cs`

## Verified

- 컴파일 클린(각 unit). EditMode **7/7**(런타임 로직: start/tick/expire/fraction/no-op/replace/independent/reset).
- 사용자 시각 확인 2026-07-22("일단 오케이"): 어두운 액체·셀 딤·카운트다운·juice 외형.
- 씬: `GameManager/PlacementCooldownRuntime` GO + `cooldownRuntime` 배선(BattleScene, commit `76067285`).

## Notes (되돌리면 안 되는 의도)

- **"0 = inert"** 3중 가드: `StartCooldown` no-op(≤0) · 오버레이/머티리얼 미생성(placementCooldown>0 슬롯만) · Update `_map.Count==0` 조기반환. 데모는 원하는 유닛만 값 넣어 opt-in.
- 쿨타임은 순수 Mono/UI — ECS/BattleBridge에 개념 없음(맥락 경계). 차단은 슬롯 사전 게이트, 최종 권한은 `TryBeginDefenderDeployment`.
- 액체는 코스트 물통과 셰이더 공유하지만 **per-slot 머티리얼 인스턴스**라 색/파라미터 독립. 인스턴스 수명은 `RebuildSlots`/`OnDestroy`에서 Destroy(누수 방지) — 지우지 말 것.
- shown 상태는 struct(`SlotVisual`) 필드가 아니라 `cooldownRoot.activeSelf`/`cooldownText.text`로 읽는다(value-copy 소실 회피 — critic M1). 되돌리면 만료 hide/pop·틱 팝이 깨진다.
- `RequiresFacing` 유닛은 aim-begin(코스트·엔티티 확정 지점)에 쿨타임 시작. 조준 취소 refund 경로가 없어 phantom 아님(critic 확인).

## Follow-up

- **전체 동작 Play 패스**(현재 외형 위주 확인): 재배치 차단 / 슬로모 감속 / 메뉴 정지 동결 / 0에서 재배치 가능 / 머티리얼 누수 없음 을 실제 배치 흐름으로 한 번 더.
- **밸런싱**: 어떤 유닛에 몇 초 줄지(현재 전부 0 = 미적용).
- 선택 juice(기포/림글로우)는 README 후속 후보.
- BattleScene.unity에 타 세션(dreamcatcher) 유래 미커밋 hunk 잔존 — 이 spec 커밋엔 미포함.
