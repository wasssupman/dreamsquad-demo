# 2 — 드롭 하마 비행 구동

## 목적

실드래그 릴리스 커밋 직후, 실제 유닛의 뷰를 고스트 위치에서 확정 타일까지 `DismountPoint` 궤적으로 날린다. 순간이동(고스트 파괴→타일 팝업) 제거의 본체.

## 변경 대상

- `Assets/_Project/Scripts/UI/DefenderDragPlacementController.cs` — `CommitPlacementAt` 연결 + `RunDropDismount` 코루틴 신설

## 구현

**커밋 연결** (`CommitPlacementAt`, `TryBeginDefenderDeployment` 성공 분기):

1. 게이트: `!_simulatedDrag` (계약 1 — 탭/armed 보드드래그 제외).
2. **CleanupSession 전에** plain 값 캡처. **팝 0 은 변환이 아니라 양끝 실좌표 캡처로 보장** (구현 중 정정 —
   previewHeight 0.35 ≠ spawnHeight 0.5 라 "previewHeight 제거" 변환만으론 0.15 world 팝 잔존):
   - 시작 = `_unitPosWorld` **그대로**(고스트 실루엣 발 실좌표)
   - 끝 = `bridge.TryGetDefenderRestViewPos(cell)` — SyncMonoUnitViews defender 피드 공식
     `(p.x, p.y + spineDefenderYOffset, p.z)` 을 미러하는 신설 헬퍼. clear 직후 프레임과 동일 좌표.
   - `startVel = _unitVelWorld × (반동초)`(F-2, DismountPoint 접선 규약)
   - 시작 오버라이드는 **동기 등록** — 같은 프레임 LateUpdate 피드가 소비(스폰 위치 1프레임 팝 방지)
3. `StartCoroutine(RunDropDismount(...))` — facing 분기(`RequiresFacing`)에서도 aim.Begin 앞에 시작(계약 8 병행). 이후 기존 흐름(CleanupSession → PlacementCommitted → RunDeployment/aim) 그대로.

**RunDropDismount(entity, cell, startFeet, startVel, endFeet, ...)**:

- 자체 세대 토큰 `_dropGen`(세션 `_sessionGen` 과 무관 — 계약 7). 진행 시계 `Time.unscaledDeltaTime`.
- `duration = Mathf.Min(cfg.dropTotalSeconds, unitData.deploymentDuration > 0 ? unitData.deploymentDuration : cfg.dropTotalSeconds)` (계약 3 클램프).
- 매 프레임: binding check — `bridge.TryGetDefenderAt(cell, out e, ...) && e == entity` 실패 시 abandon(`ClearDefenderViewOverride` 후 종료, 계약 9). 통과 시 `p = KeyringSim.DismountPoint(..., t)` → `bridge.SetDefenderViewOverride(entity, p)`. **시간 이징 없음(선형)** (구현 중 정정 — Out* 이징은 끝속도를 0 으로 죽여 스틱 착지가 물러진다. 착지 임팩트 속도는 기하(끝접선 3·(end−c2))가, 도약 속도는 launch 제어점이 만든다).
- 반동 종료 프레임에 잔류 고리·줄 분리 신호(unit 4 훅 — 이 unit 에서는 no-op 자리만).
- 종료: `ClearDefenderViewOverride` → 착지 연출 훅 호출(unit 3 — 이 unit 에서는 `PulsePlacementHover(cell, true)` 만).
- `OnDisable`/`OnDestroy`: 진행 중 dismount 전부 즉시 완결(override clear) — `FinishFlightInstant` 미러. 기존 `CleanupSession` 은 건드리지 않는다(dismount 는 세션 밖).

**확인 완료 (2026-07-28)**: pending 유닛 뷰의 오버라이드 소비는 이미 동작한다 — `SyncMonoUnitViews` defender 피드의 최우선 분기(`BattleBridge.cs:2559` 부근)가 오버라이드를 소비하며, 재배치 비행 자체가 pending 유닛을 날린다(주석 명시: "비행은 PendingDeployment(비전투)"). bridge 쪽 추가 변경 불필요.

## 완료 기준

- compile 클린 · Play 육안: 릴리스 → 유닛이 고스트 자리에서 반동 후 타일로 도약, 팝 프레임 없음
- 탭 배치·재배치 육안 무변화
- 비행 중 새 드래그 시작 → 이전 유닛 비행 지속(순간이동 없음)
- 정식 단정은 unit 5

- 확인: 2026-07-28 · Play 육안(사용자) + DropDismountTest · 커밋 `ccd59103` (+착지 앵커 sim/view 수정 `35bb5642`)
