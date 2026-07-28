# 3 — 착지 프레임 스폰 연출 재배열

## 목적

스폰 시각 연출(배치 링 펄스·`PlayDeploy` 스폰 애니·placementVfx)이 유닛이 공중인 commit 프레임에 터지는 어긋남을 제거 — dismount 경로에서만 착지 프레임으로 옮긴다. **활성화 시계는 commit 기준 유지**(계약 4, 밸런스 무변경).

## 변경 대상

- `Assets/_Project/Scripts/UI/DefenderDragPlacementController.cs` — `RunDeployment` 재구성 + `RunDropDismount` 착지 훅 연결
- `Assets/_Project/Scripts/Bridge/BattleBridge.cs` — `PlayDeploymentPresentation` 분리(필요 시)

## 구현

- `RunDeployment(unitData, cell, entity, presentAtLanding: bool)` 로 확장:
  - `presentAtLanding == false`(탭/기존 경로): 현행 그대로 — 진입 즉시 `PlayDeploymentPresentation` → duration 대기 → skillDelay → `ActivateDeployedDefender`.
  - `presentAtLanding == true`(dismount 경로): **대기 시계는 즉시 시작**하되(`duration = unitData.deploymentDuration` 을 직접 읽음), `PlayDeploymentPresentation` 호출은 dismount 착지 신호에서 수행. 클램프(계약 3) 덕에 착지 ≤ duration 만료 — 활성화 프레임은 오늘과 동일.
  - 착지 신호 전달은 콜백조차 불필요(구현 중 단순화): dismount 코루틴 자신이 착지 프레임에 `PlayDeploymentPresentation` 을 직접 호출한다(`presentAtLanding` 플래그). abandon 되면(binding 붕괴) 자연히 미발화 — 엔티티가 이미 없거나 맵 teardown 이라 연출 대상도 없다.
- `PlayDeploymentPresentation` 내부는 무변경 — 호출 시점만 이동. 반환 duration 은 dismount 경로에서 무시(시계는 이미 돌고 있음).
- **시계 정합 (확인 완료 2026-07-28)**: 활성화 대기(`WaitForSeconds`)와 dismount(unscaled)는 같은 속도로 흐른다 — TimeManager 는 글로벌 `Time.timeScale` 을 절대 건드리지 않는다(항상 1, `TimeManager.cs:15` 계약). 따라서 "클램프 → 착지 ≤ 활성화" 정렬은 도메인 슬로우모(드림캐쳐 조준 등)가 겹쳐도 깨지지 않는다.
- facing 유닛(계약 8): aim 분기는 `RunDeployment` 를 타지 않는 현행 유지. 착지 훅은 `PulsePlacementHover` 만(구현 중 정정 — aim 확정 경로가 자체 연출을 갖고 있어 착지에서 PlayDeploy 까지 걸면 이중 재생 위험. `presentAtLanding=false`).

## 완료 기준

- compile 클린
- Play 육안: 드롭 착지 순간 링 펄스·스폰 애니 발화(공중에서 안 터짐), 탭 배치는 기존 타이밍 그대로
- 활성화 타이밍 무변경 단정은 unit 5 (`commit + deploymentDuration ± 2프레임`)
