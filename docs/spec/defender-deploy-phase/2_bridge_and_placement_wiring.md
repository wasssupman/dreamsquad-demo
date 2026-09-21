# 2 · 진입 3경로 통일 · 착지 신호 · 활성화 드레인 · 저작 초 은퇴 (한 커밋)

## 목적

「시작 → 착지 → 활성화」를 브리지 API 두 개(`Begin*` · `Land*`)와 드레인 하나로 접고, UI 코루틴에서 시계를 걷고, 그 시계가 읽던 저작 초 3개를
같은 커밋에서 은퇴시킨다(critic H-4 — 따로 지우면 중간 커밋이 깨진다).

## 변경 대상

- `Assets/_Project/Scripts/Bridge/BattleBridge.cs`
  - `CreateDefenderEntity(..., pendingDeployment:)` 파라미터 삭제 — **항상** `PendingDeployment{InFlight}` 부착. `cooldownRemaining = 0f`(:8424) · 순찰병(:8711)도 0f
  - `PlaceDefenderAs`(:7718) — `TriggerDeploymentOnPlaceSkill(:7738)` **삭제**(critic M-6) · 생성 직후 `LandDeployedDefender(entity)`(비행 없음 → Deploying)
  - 신규 `public void LandDeployedDefender(Entity)` — `stage = Deploying`, `remaining = binding.data.DeployMotionSeconds`. 이미 Deploying/부재/Dead 면 no-op. 첫 줄 트레이스
  - `ActivateDeployedDefender(cell, entity)` — **이름 유지**, 뜻은 동기 진입점(sim 시스템의 세 줄을 EntityManager 로: Pending 제거 · `MarkJustDeployedForRules` · `OnDefenderActivated`). 테스트 6곳·재배치(`Relocation.cs:311`)는 무수정
  - 신규 `private void OnDefenderActivated(Entity)` — 활성화 장부(배치 스킬 1회 가드 `TriggerDeploymentOnPlaceSkill` · 취소 유예 종료 · 로그). 드레인과 동기 진입점이 둘 다 여기로
  - `BeginPlacement` — `DefenderActivatedEventsSingleton` 생성. `TickBattleFrame` — `if (!_running) return;` **앞**에 `DrainDefenderActivatedEvents()`(첫 줄 `LegacyTraceRecorder.Ev`)
  - `PlayDeploymentPresentation` — 반환값 삭제. 내부 `duration`(VFX 수명·링 펄스·폴백 펄스 게이트 :8292/8298/8305)의 소스를 `unitData.DeployMotionSeconds` 로(critic L-12 — 0 인 유닛은 폴백 펄스가 안 뜬다, 고지). `LandDeployedDefender` 를 **부르지 않는다**(연출과 전이는 호출자가 나란히)
- `Assets/_Project/Scripts/UI/DefenderDragPlacementController.cs`
  - `RunDeployment` 코루틴 은퇴 → `FinishDeploymentEntry`(비-dismount 경로: `PlayDeploymentPresentation` + `LandDeployedDefender` 즉시)
  - `RunDropDismount` 착지 프레임(:1680 부근) — `PlayDeploymentPresentation` 옆에 `bridge.LandDeployedDefender(entity)`
  - **`AbandonDismount`(:1632 바인딩 붕괴) · `FinishDismountsInstant`(:1628 OnDisable)** — 둘 다 `LandDeployedDefender`(critic H-3: 안 부르면 영구 InFlight = 공격·피격·퇴근 전부 불가, 코스트만 잃는다)
  - `StartDropDismount`(:1512) — `deploymentDuration` 클램프 삭제(비행 = `dropTotalSeconds`) · `BeginUndoWindow` 코루틴 인자 삭제(창 종료 = `DefenderActivated` 이벤트)
- `Assets/_Project/Scripts/Data/DefenderUnitData.cs` — `deploymentDuration`·`deployDelaySec`·`placementSkillDelay` 삭제
- `Assets/_Project/Scripts/Data/StatImport/UnitStatImportDto.cs` — `deployDelaySec` 삭제
- `Assets/_Project/Scripts/Data/DragSwaySettings.cs:214-217` · 컨트롤러 `:1511` — 「비행 ⊆ pending 창」 주석을 새 계약으로(critic L-13)
- PlayMode: `ActivateDeployedDefender` 호출 6곳은 무수정(동기 진입점 유지). `DropDismountTest.cs:113-125` 계약 4 단언 → 「활성화 = commit + 비행(`dropTotalSeconds`) + `DeployMotionSeconds`(±0.25s)」. 재배치 테스트 무수정
- `CLAUDE.md` 채널 목록 30 → 31 · `docs/reference/battle-core-architecture.md` §4.2·§4.4·§6

## 완료 기준

- 세 경로(트레이 D&D · 탭 · `PlaceDefenderAs`)가 같은 트레이스 열: `Begin → Land(remaining=N) → Activated`. 배치 단계(StartBattle 전)에도 Activated 가 그 프레임에 드레인된다.
- `PlaceDefenderAs` 로 놓은 실드셔틀이 0.67s 동안 공격·피격 없이 `skill` 완주 후 첫 공격 → unit 4 계측.
- 비행 중 OnDisable / 바인딩 붕괴 시 pending 이 남지 않는다(`AbandonDismount`·`FinishDismountsInstant` 가 Land — 코드 리뷰 + Play 계측).
- grep: `deploymentDuration|deployDelaySec|placementSkillDelay` 0건. 시트 push 1회(컬럼 탈락).
- PlayMode 위 목록 초록(리터럴 0.45 잔존 0).
