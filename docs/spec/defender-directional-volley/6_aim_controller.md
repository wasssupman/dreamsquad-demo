# 6. Mono 컨트롤러 — DirectionAimController + 드래그 핸드오프

## 목적

드롭 성공 시 공격방향 페이즈를 실제로 돌린다: 슬로우모션 유지(lease 이관), 줌인(CameraDirector 포커스 피드), 4방향 가이드 UI, 스와이프 하이라이트, 확정 시 배치 연출→활성화(facing 기록).

## 변경 대상

- `Assets/_Project/Scripts/UI/DirectionAimController.cs` (신규)
- `Assets/_Project/Scripts/Data/DirectionAimSettings.cs` + `Assets/_Project/Data/Config/DirectionAimSettings.asset` (신규 SO — 데드존 px·가이드 크기/색·하이라이트 색)
- `Assets/_Project/Scripts/UI/DefenderDragPlacementController.cs` (`CommitPlacementAt` 핸드오프 · `BeginDrag` 잠금 · `Configure` 주입)
- `Assets/_Project/Scripts/UI/DefenderSelector.cs` (`aimSettings` 직렬화 필드 → Configure 주입, DragSwaySettings 선례)

## 구현

**핸드오프** (`DefenderDragPlacementController.CommitPlacementAt` 성공 분기):
- `unitData.directionalAttack == false`: 현행 그대로 (`CleanupSession` → `RunDeployment`).
- true: `_aimController.Begin(...)` → `CleanupSession()` 순서로 호출. 이 시점 엔티티는 `PendingDeployment`(전투 미참여)로 이미 스폰돼 있고, 방향이 확정돼야 활성화된다.
- **rev1 (구현 시 확정) — lease 이관 대신 선점**: 초안은 "`CleanupSession` 이 dispose 를 건너뛰는 변형 정리"였으나, 그러면 lease 소유권이 두 컨트롤러에 걸쳐 흐려진다. 대신 **`Begin` 이 자기 lease 를 먼저 Request(priority 60) 한 뒤 `CleanupSession` 이 드래그 lease 를 놓는다** — 겹치는 순간이 있을 뿐 틈이 없어 전투가 정속으로 튀지 않고, 각 컨트롤러가 자기 lease 만 소유한다. 순서 의존이므로 뒤집지 말 것. priority 60 = 일시정지(100) 아래 · 드림캐쳐 인스펙트(50) 위.
- **조준 중 트레이 잠금**(`BeginDrag` 가드): 조준 중 새 드래그를 허용하면 그 한 번의 제스처가 배치와 조준 양쪽에서 소비되어, 앞 유닛이 플레이어가 고르지 않은 방향으로 고정된다.

**DirectionAimController** (단일 세션, 페이즈 해석은 전부 DirectionAimLogic 위임):
- 입력: 드롭=손가락 up 이후라 UGUI 드래그 핸들러가 없다 — `Pointer.current` 폴링 Update(`DcInspectController` 선례, unscaled 시간). **UI 위에서 시작한 press 는 조준 제스처가 아니다** — 판정은 `EventSystem.RaycastAll` 즉석 레이캐스트로 한다. `IsPointerOverGameObject()` 는 지난 프레임 pointer 상태를 읽는데 터치는 hover 가 없어 press 프레임에 상태 자체가 없다 → 실기기에서만 UI 관통(에디터는 마우스 hover 잔상이 가림, DcInspectController 가 같은 이유로 레이캐스트를 씀).
- 줌인: 배치 셀 월드좌표를 CameraDirector 인스펙트 포커스 채널에 매 프레임 피드(직접 조작 금지, staleness 자동 해제 계약 준수).
- 가이드 UI: 자체 ScreenSpaceOverlay 캔버스(드래그 컨트롤러 오버레이 패턴, sortingOrder 20001)에 유닛 주위 4방향 화살표. 스와이프 중 `DirectionAimLogic.Evaluate` 결과로 해당 방향만 하이라이트.
- 확정(`OnRelease.confirmed`): 가이드 파괴 → 포커스 피드 중단 → lease dispose → `bridge.PlayDeploymentPresentation` → duration 대기 → `bridge.ActivateDeployedDefender(entity, facing)` (unit 1 API — DeployedFacing 기록). 기존 placementSkillDelay 시퀀스와 동일 간격 유지.
- 미확정 릴리즈: 가이드 유지, 다음 스와이프 대기(계약 9). 취소 없음.
- 화면 cardinal → 보드 cardinal 변환은 컨트롤러 책임(카메라 yaw 고정 구도면 항등 매핑으로 시작, Play 검증에서 어긋나면 보정) — 로직 레이어는 카메라를 모른다(unit 5 계약).

**멱등/정리**: 컨트롤러 세션 종료(확정) 시 캔버스·피드·lease 를 전부 해제. 매치 종료 등 외부 정리 경로에서 세션이 살아 있으면 안전 dispose(드래그 컨트롤러 CleanupSession 멱등 선례).

**조준 페이즈는 모달이다 — 보드 탭 소비자 전원을 막아야 한다**: 이 화면은 "드래그 세션은 끝났지만 여전히 배치 조작 중"이라는 어중간한 상태라, 기존 게이트(`IsDragging`)에 걸리지 않는다. 전역 포인터를 폴링하는 컨트롤러가 조준 스와이프를 자기 제스처로 오해하면 **한 제스처가 두 곳에서 소비**된다. 실측된 사례:
- `DcInspectController` — 가이드 중앙이 곧 유닛이라 스와이프 시작점이 유닛을 맞고, 인스펙트가 선택돼 slomo(우선도 50)와 줌을 계속 붙잡는다. **방향 확정 후에도 남아 닫는 클릭이 한 번 더 필요**해진다(사용자 Play 실측 2026-07-17). → `Blocked()` 에 `drag.IsAiming` 추가.
- 트레이 드래그 / tap-to-place 보드 탭 → `DefenderDragPlacementController.BeginDrag` 잠금이 차단(시뮬 경로도 BeginDrag 를 타므로 함께 막힘).

신규 보드 탭 소비자를 추가할 때 `DefenderDragPlacementController.IsAiming` 을 게이트에 포함할 것.

**Cancel 의 두 얼굴** (`activatePending`): 조준을 못 끝낸 채 세션이 무너질 때 —
- **재진입**(다음 배치가 조준을 덮음, 트레이 잠금 이후 정상 흐름에선 미도달): 코스트를 이미 낸 유닛이므로 기본 방향(+Y)으로 활성화. **계약 9 의 명시적 예외** — "방향 확정 없이는 활성화 없음"의 유일한 구멍이고, PendingDeployment 로 굳는 것보다 낫다는 판단.
- **teardown**(OnDisable/OnDestroy): ECS 를 건드리지 않는다. 파괴 순서가 비결정적이라 World 가 먼저 사라졌으면 EntityManager 접근이 던진다.

**병행 변경 주의 (2026-07-17 placement-cell-snap units 0~5 반영)**: 드래그 컨트롤러에 히스테리시스+throttle 셀 확정(`ResolveFocusAndTarget`)·확정 팝(`SetHover`)이 들어갔다 — EndDrag 의 확정 셀은 이 산출물을 그대로 쓰면 되고 aim 페이즈는 드롭 후라 스냅 로직과 무관. 단 **고스트 셀 스냅 재도입 금지**(키링 스윙 파괴, placement-cell-snap handoff Notes) 결정과 충돌하는 UI 를 만들지 말 것. 착수 시점에 DefenderDragPlacementController·DefenderDragSlot·DragSwaySettings 는 병행 수정 이력이 있으므로 반드시 재독.

## 완료 기준

- [ ] compile + 기존 유닛 D&D 배치 무변화 (non-directional 경로 회귀 없음)
- [ ] Play 검증(에디터): directionalAttack 유닛 드롭 → 슬로우모션 유지 + 줌인 + 4방향 가이드 노출 → 스와이프에 방향 하이라이트 추종 → 릴리즈로 확정 → 배치 연출 후 지정 방향 발사. 게임뷰 스크린샷 첨부
- [ ] 데드존 릴리즈 시 가이드가 유지되고 재스와이프로 확정 가능
- [ ] 확정 후 슬로우모션·줌이 정상 복귀(잔류 lease 없음 — TimeManager 상태 확인)
