# 5 · Handoff Summary — defender-deploy-phase

## Commit

- (이 문서와 같은 커밋) `feat(defender-deploy-phase): 「배치 중」을 sim 페이즈로 — PendingDeployment{InFlight,Deploying} · DeploymentActivationSystem · 저작 초 3개 은퇴` — 해시는 사용자 Play 확인 뒤 README 상태 라인에 기록.
- 선행 커밋 `ccec4a1d`(deployDelaySec 을 애니 길이로) 는 이 spec 이 **대체**했다 — 그 필드 자체가 사라졌다.

## Implemented

- `DefenderUnitData.DeployMotionSeconds` — 배치 페이즈 길이 = 배치 모션 길이(시트 deploy / Spine deployAnimation, **명시 슬롯만**). 저작 초 `deploymentDuration`·`deployDelaySec`·`placementSkillDelay` 은퇴(필드·DTO 컬럼·에셋 키).
- `PendingDeployment{stage, remaining}` — InFlight(기본, 시계 없음) / Deploying(배틀 시간). 14곳 `WithNone` 쿼리 무수정.
- `DeploymentActivationSystem`(Units, `UpdateAfter(BossPeriodicTrigger)`) — remaining 틱 → 같은 ECB 에서 태그 제거 + `JustDeployed` + `DefenderActivatedEvent`. 사망한 pending 은 태그만 걷음.
- 31번째 채널 `DefenderActivatedEventsSingleton`(EnsureQueriesAndQueues 생성) · 드레인 `DrainDefenderActivatedEvents` 는 `TickBattleFrame` 의 **`_running` 앞** · 트레이스 채널 21.
- 브리지: `CreateDefenderEntity` 는 항상 InFlight · `LandDeployedDefender`(착지 신호 — 하마 착지·즉시 배치·비행 중단·OnDisable 전부) · `ActivateDeployedDefender` 는 동기 진입점으로 이름 유지(테스트·재배치) · `OnDefenderActivated` 장부 · `PlaceDefenderAs` 는 즉시 배치 스킬 대신 연출+착지.
- 컨트롤러: `RunDeployment` 코루틴 은퇴(시계는 sim) · 되돌리기 창은 pending 폴링 · 드롭 비행 클램프 제거.
- `UnitActionPhase`(Combat, 순수) — Attack·Movement 의 lock 식이 이걸 읽는다(동작 무변). 뷰 원샷 순서 Death > Deploy > Attack(배치 원샷 중 공격은 큐) · `PlayDeploy` 명시 슬롯만(폴백 체인·`ResolveDeploy` 은퇴).

## Key Files

- `Battle/Units/PendingDeployment.cs` · `DeploymentActivationSystem.cs` · `DefenderActivatedEventsSingleton.cs` · `Battle/Combat/UnitActionPhase.cs`
- `Bridge/BattleBridge.cs`(`LandDeployedDefender`·`ActivateDeployedDefender`·`OnDefenderActivated`·`DrainDefenderActivatedEvents`·`PlaceDefenderAs`·`PlayDeploymentPresentation`)
- `UI/DefenderDragPlacementController.cs`(`CommitPlacementAt`·`FinishDeploymentEntry`·`RunDropDismount` 착지·`AbandonDismount`·`FinishDismountsInstant`)
- `Data/DefenderUnitData.cs`(`DeployMotionSeconds`) · `Presentation/SpineUnitView.cs`·`SpriteUnitView.cs`
- Tests: `EditMode/DeployMotionSecondsTests` · `DeploymentActivationSystemTests` · `UnitActionPhaseTests` · `EditModeAssets/DeployMotionSecondsAssetTests` · `PlayMode/DropDismountTest`(계약 4 rev)

## Verified

- 컴파일 0 · EditMode 두 lane 2884건 — 기지 실패 2건(bomb_man·boomerang 문안, 시트 소관) 외 초록.
- MCP Play(적이 사거리 안인 전투 중 `PlaceDefenderAs`): 실드셔틀 `skill(once) pending(Deploying)` 0.64s 완주 → `idle1 pending` → 활성화 → 첫 `attack4`. 캐논 `Hit(once)` 0.78s → 활성화(0.97s) → `Attack3`. 스나이퍼 `drop` 1.67s 뒤 활성화 → `attack`. 배치 모션이 잘리는 일 없음(제보 증상 소멸).
- 미실행: PlayMode lane(8분 · `DropDismountTest`·on-place 테스트·`ActionLockTest` — 사용자 승인 후) · D&D 경로 육안 · 골든 코퍼스 재베이크(활성화 타이밍 + 트레이스 채널 21 추가로 전건 갈림이 정상).

## Notes

- **비행을 끝내는 모든 출구는 Land 다.** `AbandonDismount`·`FinishDismountsInstant` 에서 빼면 영구 InFlight(공격·피격·퇴근 전부 불가, 코스트만 소실).
- **드레인은 `_running` 앞.** 배치는 StartBattle 전에 일어난다. 뒤로 옮기면 배치 단계 활성화가 판 시작에 몰린다.
- `PendingDeployment` 기본값 = InFlight 가 안전장치인 이유: 기존 `AddComponent<PendingDeployment>()`(재배치·테스트)가 그대로 «배제»를 뜻한다. 재배치는 자기 시계(`redeploySeconds`) 뒤 동기 `ActivateDeployedDefender` — 이 spec 밖.
- `DeploymentActivationSystem` 의 `remaining ≤ 0` 는 부동소수 누적에 기대지 않는다(테스트가 0.45/0.1 을 쓰는 이유).
- 콘솔 "Bone not found: Gear" 는 파츠형 리그의 기존 로그(이 spec 무관).
- 시트: `deployDelaySec` 컬럼은 DTO 에서 빠져 시트가 보내도 무시된다 — push 불필요. 시트에서 컬럼을 지울지는 시트 소유자 몫.

## Follow-up

- 사용자 Play 육안(D&D 배치·적 있는 배치·되돌리기 창) → 커밋 해시 기록.
- PlayMode lane · 골든 재베이크(남의 WIP 와 격리).
- README 후속 후보: `DefenderAiState` 승격 · HazardCast CC 락 · Attack 루프 Dead 게이트 · 재배치 모션 길이 적용.
