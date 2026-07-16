# 6. Mono 컨트롤러 — DirectionAimController + 드래그 핸드오프

## 목적

드롭 성공 시 공격방향 페이즈를 실제로 돌린다: 슬로우모션 유지(lease 이관), 줌인(CameraDirector 포커스 피드), 4방향 가이드 UI, 스와이프 하이라이트, 확정 시 배치 연출→활성화(facing 기록).

## 변경 대상

- `Assets/_Project/Scripts/UI/DirectionAimController.cs` (신규)
- `Assets/_Project/Scripts/UI/DirectionAimSettings.cs` + `Assets/_Project/Settings/DirectionAimSettings.asset` (신규 SO — 데드존 px·가이드 크기/색·하이라이트 색)
- `Assets/_Project/Scripts/UI/DefenderDragPlacementController.cs` (`EndDrag`·`CleanupSession`·`RunDeployment` 분기)

## 구현

**핸드오프** (`DefenderDragPlacementController.EndDrag` valid-drop 성공 분기):
- `unitData.directionalAttack == false`: 현행 그대로 (`CleanupSession` → `RunDeployment`).
- true: `CleanupSession` 에서 슬로우모 lease dispose 를 건너뛰는 변형 정리를 수행하고, lease·엔티티·배치 셀·unitData 를 `DirectionAimController` 에 넘긴다(런타임 AddComponent — `DefenderSelector.EnsureDragController` 선례). 이 시점 엔티티는 `PendingDeployment` 상태(전투 미참여)로 이미 스폰돼 있음.

**DirectionAimController** (단일 세션, 페이즈 해석은 전부 DirectionAimLogic 위임):
- 입력: 드롭=손가락 up 이후라 UGUI 드래그 핸들러가 없다 — `Pointer.current` 폴링 Update(`DcInspectController` 선례, unscaled 시간).
- 줌인: 배치 셀 월드좌표를 CameraDirector 인스펙트 포커스 채널에 매 프레임 피드(직접 조작 금지, staleness 자동 해제 계약 준수).
- 가이드 UI: 자체 ScreenSpaceOverlay 캔버스(드래그 컨트롤러 오버레이 패턴, sortingOrder 20001)에 유닛 주위 4방향 화살표. 스와이프 중 `DirectionAimLogic.Evaluate` 결과로 해당 방향만 하이라이트.
- 확정(`OnRelease.confirmed`): 가이드 파괴 → 포커스 피드 중단 → lease dispose → `bridge.PlayDeploymentPresentation` → duration 대기 → `bridge.ActivateDeployedDefender(entity, facing)` (unit 1 API — DeployedFacing 기록). 기존 placementSkillDelay 시퀀스와 동일 간격 유지.
- 미확정 릴리즈: 가이드 유지, 다음 스와이프 대기(계약 9). 취소 없음.
- 화면 cardinal → 보드 cardinal 변환은 컨트롤러 책임(카메라 yaw 고정 구도면 항등 매핑으로 시작, Play 검증에서 어긋나면 보정) — 로직 레이어는 카메라를 모른다(unit 5 계약).

**멱등/정리**: 컨트롤러 세션 종료(확정) 시 캔버스·피드·lease 를 전부 해제. 매치 종료 등 외부 정리 경로에서 세션이 살아 있으면 안전 dispose(드래그 컨트롤러 CleanupSession 멱등 선례).

## 완료 기준

- [ ] compile + 기존 유닛 D&D 배치 무변화 (non-directional 경로 회귀 없음)
- [ ] Play 검증(에디터): directionalAttack 유닛 드롭 → 슬로우모션 유지 + 줌인 + 4방향 가이드 노출 → 스와이프에 방향 하이라이트 추종 → 릴리즈로 확정 → 배치 연출 후 지정 방향 발사. 게임뷰 스크린샷 첨부
- [ ] 데드존 릴리즈 시 가이드가 유지되고 재스와이프로 확정 가능
- [ ] 확정 후 슬로우모션·줌이 정상 복귀(잔류 lease 없음 — TimeManager 상태 확인)
