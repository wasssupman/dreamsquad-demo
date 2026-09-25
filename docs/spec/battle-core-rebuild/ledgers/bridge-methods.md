# 장부 — 브리지 메서드 귀속표 (unit 0 · 항목 6 · bridge-methods.md)

> 생성/갱신: `python3 Tools/battle-core-rebuild/check_ledgers.py --generate`. 키 = `이름/인자수`. 「새 주인」 열은 사람이 고친다(재생성 시 보존). 「미정」은 조각 E 진입 전 0 이어야 한다. 접두사 휴리스틱 초안이므로 **틀린 귀속이 있을 수 있다** — 유닛별로 옮길 때 그 파일의 행을 확정한다.

총 367 선언 · 파일 7

## BattleBridge.BonusWave.cs (9)

| # | 메서드 | 새 주인 | 비고 |
|---|---|---|---|
| 1 | `SetBonusPullSuppressed/1` | WaveScheduler |  |
| 2 | `ResetBonusWaveState/0` | WaveScheduler |  |
| 3 | `TickBonusPullOffer/0` | WaveScheduler |  |
| 4 | `TryBonusPull/0` | WaveScheduler |  |
| 5 | `ForceBonusWave/0` | WaveScheduler |  |
| 6 | `TickBonusWave/1` | WaveScheduler |  |
| 7 | `SpawnBonusUnit/1` | WaveScheduler |  |
| 8 | `OpenBonusPortals/0` | WaveScheduler |  |
| 9 | `ClearBonusPortalViews/0` | `CoreBonusPortalPresenter.Clear` — WaveScheduler |  |

## BattleBridge.BossLeap.cs (7)

| # | 메서드 | 새 주인 | 비고 |
|---|---|---|---|
| 1 | `TryGetEnemyViewOverride/3` | `CoreLeapPresenter.TryGetFlightOverride` — 뷰 풀 |  |
| 2 | `CreateBossLeapChannel/0` | 삭제 (코어 스폰 = BattleWorld.Spawn*) |  |
| 3 | `DisposeBossLeapChannel/0` | 삭제 | 채널이 없다 — 도약은 `LeapAscend`/`LeapDescend` 사건이고 수명은 구독이다 |
| 4 | `DrainBossLeapVisualEvents/0` | `CoreLeapPresenter.OnCoreEvent` — 뷰 풀 / 담당자 구독 (이벤트로 접힘) |  |
| 5 | `RunBossLeap/1` | CoreLeapPresenter | 뷰 비행 코루틴. 슬램 발사는 **안 옮긴다** — 코어가 이미 낸다 |
| 6 | `ResolveLanding/2` | 삭제 | 착지 슬램은 코어(`CombatPhase.StepLeap`)의 것이다. 뷰가 투사체를 쏘던 자리 |
| 7 | `PlayLeapPuff/2` | 삭제 | 사건이 `dataIndex` 를 안 나른다. 슬램이 있으면 착탄 VFX 가 이미 그 자리를 그린다 — 5a 이식 제외 참조 |

## BattleBridge.Dreamcatcher.cs (22)

| # | 메서드 | 새 주인 | 비고 |
|---|---|---|---|
| 1 | `NotifyEnemyGoneIfMarked/1` | `HandDeck.Recover` — HandDeck (7b) | 표식 등록부가 사라졌다 — 표식 = 적에게 붙은 카드 규칙. 적 소멸(처치·유출) → `HandDeck.Recover`(`UnitDestroyed` 구독) → `BindingRegistry.DetachCard` → `CardDetached` |
| 2 | `IsEnemyMarked/1` | `CardBindings.IsMarked` — CardBindings (7b) | `CardBindings.IsMarked` — 적의 규칙 목록에 카드 표식이 있나(리티클 유효성은 7c 가 `HandDeck.WouldAttach` 로 묻는다) |
| 3 | `ApplyDreamcatcherCard/2` | HandDeck |  |
| 4 | `ApplyDreamcatcherCardHosted/1` | HandDeck |  |
| 5 | `ApplyDreamcatcherCardInternal/2` | HandDeck |  |
| 6 | `RevokeDreamcatcherEffects/1` | HandDeck |  |
| 7 | `ApplyActiveDcEffectsTo/2` | `BindingRegistry.AttachMatchRows` — HandDeck |  |
| 8 | `ApplyPlacementSleep/2` | `PlacementSleepSkill` — PlacementSleepSkill (7b) | 배치 서비스는 오라를 모른다(7b 구현 5) — 배치 오라의 수면 규칙(`OnPlace(Any)` × `PlacementSleepSkill`, revoke false) |
| 9 | `ApplyDreamcatcherCardToUnit/2` | HandDeck |  |
| 10 | `WouldDreamcatcherCardApply/2` | HandDeck |  |
| 11 | `ApplyBountyMark/2` | `CardBindings.Plan` — CardBindings · BountyMarkSkill (7b) | 판정 = `CardBindings.Plan`(적 전용 · 이중 표식 거절) · 실행 = `BountyMarkSkill`(원자 2효과) · 소비 = `HandDeck.OnSlain` × `UnitSlain.RewardMul` |
| 12 | `PassesAttachRequirement/2` | HandDeck |  |
| 13 | `LogAttachRequirementReject/2` | HandDeck |  |
| 14 | `BuildHostProfile/1` | `HostProfile.Of` — Applicability (7b) | `Trigger/Applicability.cs` `HostProfile.Of` — 정책(폭탄) → 자기 발사 명세 → 표준, 경로는 실제로 타는 길 |
| 15 | `TargetsEnemies/1` | `HostProfile.TargetsEnemies` — Applicability (7b) | `HostProfile.Of` 의 `TargetsEnemies`(공격 대상 마스크 ∩ 적) |
| 16 | `HasPositiveDamageOutput/1` | `HostProfile.Of` — Applicability (7b) | `HostProfile.Of` 의 `HasDamageOutput` |
| 17 | `RegisterPlacementAura/3` | `CardDefinitionBuilder.Fill` — CardDefinitionBuilder (7b) | 등록 영수증이 사라졌다 — 배치 오라 = 규칙 둘로 bake(공속 revoke true · 수면 false). 회수 = 숙주 소멸 시 슬롯 삭제 |
| 18 | `MapDcEffect/3` | HandDeck |  |
| 19 | `MapDcBuff/4` | `CardDefinitionBuilder` — HandDeck |  |
| 20 | `MapDcCc/1` | `BindingDefinitionBuilder` — HandDeck |  |
| 21 | `MapDcStack/1` | `BindingDefinitionBuilder` — HandDeck |  |
| 22 | `MatchesDcAxis/2` | `TriggerDispatcher.SubjectPasses` — HandDeck |  |

## BattleBridge.Relocation.cs (17)

| # | 메서드 | 새 주인 | 비고 |
|---|---|---|---|
| 1 | `RelocationCheck/7` | 삭제 (7d · 재배치 미이식) | 재배치(유닛 이동)는 라이브에 없다 — `defender-clock-out/0` 이 진입구를 껐고(2026-08-13) 퇴근이 대신한다. 7d 「이식 제외」 · 판정 본체는 배치 판정(`PlacementService.Judge`)과 같은 자라 따로 둘 것이 없다 |
| 2 | `RelocationFootprintCheck/9` | PlacementService |  |
| 3 | `TryGetDefenderAt/4` | 삭제 (7d · 재배치 미이식) | 재배치(유닛 이동)는 라이브에 없다 — `defender-clock-out/0` 이 진입구를 껐고(2026-08-13) 퇴근이 대신한다. 7d 「이식 제외」 · 다른 소비자(배치 착지 가드·카드 드래그·검사 패널)는 새 층에서 코어 사건(`CoreDeployFlightPresenter`)과 화면 집기(`CoreCardTargets`·`SelectionInput`)가 대신한다 |
| 4 | `TryGetDefenderCell/2` | `MapRuntime.CellOf` — MapRuntime (코어) |  |
| 5 | `CanRelocateDefender/3` | PlacementService |  |
| 6 | `HasCostForRelocation/1` | CostLedger |  |
| 7 | `TryBeginDefenderRelocation/4` | PlacementService |  |
| 8 | `RelocatePatrolAnchorFor/2` | 삭제 (7d · 재배치 미이식) | 재배치(유닛 이동)는 라이브에 없다 — `defender-clock-out/0` 이 진입구를 껐고(2026-08-13) 퇴근이 대신한다. 7d 「이식 제외」 · 소환사가 안 움직이니 순찰 앵커를 옮길 일이 없다(앵커 = 소환 순간의 소환사 칸, `CombatPhase.SpawnPatrol`) |
| 9 | `SetDefenderViewOverride/4` | `CoreDeployFlightPresenter.Launch` — 뷰 풀 |  |
| 10 | `ClearDefenderViewOverride/1` | `CoreDeployFlightPresenter.Land` — 뷰 풀 |  |
| 11 | `PlayLandingSquash/3` | CoreUnitView.PlayLandingSquash | 착지 눌림은 뷰의 것 — 부르는 자는 `CoreDeployFlightPresenter`(배치 착지) · `CoreLeapPresenter`(도약·궁극기 착지). 재배치 착지 호출부는 재배치와 함께 사라졌다 |
| 12 | `TryGetDefenderViewOverride/4` | `CoreDeployFlightPresenter.TryGetFlightView` — 뷰 풀 |  |
| 13 | `TryGetRelocationAnchors/5` | 삭제 (7d · 재배치 미이식) | 재배치(유닛 이동)는 라이브에 없다 — `defender-clock-out/0` 이 진입구를 껐고(2026-08-13) 퇴근이 대신한다. 7d 「이식 제외」 |
| 14 | `ActivateRelocatedDefender/3` | 삭제 (7d · 재배치 미이식) | 재배치(유닛 이동)는 라이브에 없다 — `defender-clock-out/0` 이 진입구를 껐고(2026-08-13) 퇴근이 대신한다. 7d 「이식 제외」 |
| 15 | `ApplyRefitHeal/2` | 삭제 (7d · 재배치 미이식) | 재배치(유닛 이동)는 라이브에 없다 — `defender-clock-out/0` 이 진입구를 껐고(2026-08-13) 퇴근이 대신한다. 7d 「이식 제외」 · 재정비 회복은 재배치 전용 규칙이었다 |
| 16 | `FinishDefenderRelocation/2` | 삭제 (7d · 재배치 미이식) | 재배치(유닛 이동)는 라이브에 없다 — `defender-clock-out/0` 이 진입구를 껐고(2026-08-13) 퇴근이 대신한다. 7d 「이식 제외」 |
| 17 | `DebugRelocateFirstDefender/0` | 삭제 (재배치 은퇴 — `tools.md` 11행 · 7d 「이식 제외」) |  |

## BattleBridge.UltimateLeap.cs (6)

| # | 메서드 | 새 주인 | 비고 |
|---|---|---|---|
| 1 | `CreateUltimateLeapChannel/0` | 삭제 (코어 스폰 = BattleWorld.Spawn*) |  |
| 2 | `DisposeUltimateLeapChannel/0` | 삭제 | 위와 같다 — 채널이 없다 |
| 3 | `ShowLandingTelegraph/1` | `CoreMapOverlay.ShowLandingTelegraph` — 구동 `CoreLeapPresenter`(이탈 `LeapAscend.AreaTiles` · 색 `LeapVisualConfig.LandingTelegraphColor`) (8a2) | 착지 예고 링은 보드에 그리는 것이라 오버레이의 몫이다 |
| 4 | `DrainUltimateLeapVisualEvents/0` | `CoreLeapPresenter.OnCoreEvent` — 뷰 풀 / 담당자 구독 (이벤트로 접힘) |  |
| 5 | `RunUltimateLeapAscend/2` | CoreLeapPresenter | 이탈 — 올라가서 **머무른다**(예고 시간은 코어가 소유) |
| 6 | `RunUltimateLeapDescend/3` | CoreLeapPresenter | 강하 — `LeapDescend` 를 받아 내려온다 |

## BattleBridge.UnitStats.cs (1)

| # | 메서드 | 새 주인 | 비고 |
|---|---|---|---|
| 1 | `TryGetUnitStatReadout/2` | `EffectiveStats` — EffectiveStats (선택 패널 = 6c) | 실효 스탯 = `Unit.Modifiers.Effective`, 기본 = 정의표 줄. 델타 칩을 그리는 자는 5b 의 패널이고 배선은 6c |

## BattleBridge.cs (305)

| # | 메서드 | 새 주인 | 비고 |
|---|---|---|---|
| 1 | `SetEnemiesDimmed/1` | `CoreUnitViewPool.SetEnemiesDimmed` — 켜기/끄기 `DragPlacementInput`(승격 · `EndDrag`) · 페이드·fan-out `CoreUnitViewPool.SyncViews` (8a2) | 유닛 뷰 `SetDimmed` fan-out. 구동은 5b 의 드래그 입력 |
| 2 | `SetPlacementHighlightAboveUnits/1` | `CoreMapOverlay.ShowPlacement` — PlacementService |  |
| 3 | `CreateAliveAttackerQueries/0` | 삭제 (코어 스폰 = BattleWorld.Spawn*) |  |
| 4 | `MirrorLiftKnobs/0` | `CoreViewKnobs.ResolveLift` — 뷰 풀 |  |
| 5 | `SetMatchSeed/1` | `ModeSelection.Seed` → BattleDriver | 재현의 둘째 축. 씬 경계를 넘어오는 값이라 진입 선택이 나른다(0 = 저작 시드) |
| 6 | `BuildBriefingWavePlan/0` | WaveScheduler |  |
| 7 | `SetAssignedGimmick/1` | GimmickHost |  |
| 8 | `Awake/0` | 삭제 (7d · 계약 1) | MonoBehaviour 수명. 코어의 수명은 `BattleMatch` 조립이고 드라이버(`BattleDriver`)가 든다 |
| 9 | `OnValidate/0` | 삭제 (7d · 계약 1) | 인스펙터 값 보정 — 값의 정본이 SO → `MatchDefinitionBuilder` 로 옮겨 빌더가 거절한다(계약 6) |
| 10 | `ApplyUnitHealthPresentationMode/0` | CharacterViewConfig | 표시 모드는 저작 값이 됐다 — 런타임에 미는 함수가 없다 |
| 11 | `EnterPlacementOrIntro/0` | PlacementService |  |
| 12 | `OnRestartRequested/0` | 삭제 (7d · 사용자 결정) | 판 안 재시작 없음(사용자 확정 2026-09-23) — 새 판은 `BattleMatch` 를 새로 조립한다 |
| 13 | `ReLogSkillLoadoutForNewSession/1` | 삭제 (배틀 JSON 로그 전용 — rules X28 제거 · README 조각 E 에이전트 결정 ⑷) |  |
| 14 | `TeardownCurrentBattle/0` | MatchClock |  |
| 15 | `HasLiveEntityManager/0` | 삭제 (코어 스폰 = BattleWorld.Spawn*) |  |
| 16 | `AttachSimEntityId/1` | HandDeck |  |
| 17 | `DestroyBattleEntities/0` | 삭제 (7d · 계약 1) | ECS 월드 정리. 코어 월드는 판과 함께 버려진다 — 개별 파괴 경로가 없다 |
| 18 | `DestroyEcsInfrastructureEntities/0` | 삭제 (7d · 계약 1) | 싱글턴·채널 엔티티 정리 — 코어에 큐·싱글턴이 없다 |
| 19 | `DisposeEcsInfrastructureNativeContainers/0` | 삭제 (7d · 계약 1) | NativeContainer 해제 — 코어는 관리 배열만 쓴다(계약 4) |
| 20 | `DisposeCachedQueries/0` | 삭제 (7d · 계약 1) | EntityQuery 캐시 — 코어에 쿼리가 없다(순회 = `SimEntityId` 오름차순 목록) |
| 21 | `BuildFlowField/0` | `FlowFieldSet.Rebuild` — MapRuntime (코어) |  |
| 22 | `AddTraversalMask/2` | `BattleMatch.CollectTraversalMasks` — BattleMatch (7a 확인) | 통행 층 목록은 조립 지점이 정의표에서 한 번 모은다(`CollectTraversalMasks`) — 규칙이 만드는 개체(장판·탄)는 새 층을 안 연다 |
| 23 | `BuildPickupSpawnState/0` | GimmickHost |  |
| 24 | `TeardownPickupSpawnState/0` | MatchClock |  |
| 25 | `ComputeSpawnLateralOffset/1` | `SpawnSpread.LaneFraction` — EnemySpawn (코어) | 스폰 칸 흐름 수직 이산 N-레인 분산 = `Move/SpawnSpread` + `World/EnemySpawn` · 값은 `MovementTuningDef.SpawnSpread*`(bridge-fields 40~42). 가변 순번은 X25 보류(후속 후보) |
| 26 | `BuildStageMarkerRegistry/0` | BattleDriver | 스테이지 스캔 → 거점 목록. `Build(…, structures:)` 의 입력 |
| 27 | `TryGetGoalVisualAnchor/1` | `CoreFirstRunGuide.TryGetGoalAnchor` — HeartMeter |  |
| 28 | `TryGetSpawnVisualAnchor/2` | 삭제 (옛 호출처 0 — 선언만 있던 휴면 코드 · 8c 확인) |  |
| 29 | `CellCenterView/1` | `CoreCardTargets.CellViewCenter` — MapRuntime (코어) |  |
| 30 | `TeardownGeneratedMap/0` | MatchClock |  |
| 31 | `BuildMapForBattle/0` | `MatchDefinitionBuilder.TrySelectEncounter` — MapRuntime (코어) |  |
| 32 | `TeardownFlowField/0` | MatchClock |  |
| 33 | `BeginPlacement/0` | PlacementService |  |
| 34 | `StartBattle/0` | MatchClock |  |
| 35 | `EnsureQueriesAndQueues/0` | 삭제 (7d · 계약 1) | 쿼리·큐 지연 생성 — 둘 다 코어에 없다 |
| 36 | `StopBattle/0` | MatchClock |  |
| 37 | `PrepareDraftMap/0` | `BattleDriver.Begin` — MapRuntime (코어) |  |
| 38 | `DeferredPrepareDraftMap/0` | `BattleDriver.Start` — MapRuntime (코어) |  |
| 39 | `CleanupDraftMapBeforeRebuild/0` | `BattleDriver.TeardownStage` — MapRuntime (코어) |  |
| 40 | `DestroyEntitiesByType/0` | 삭제 (7d · 계약 1) | 판 경계 타입별 엔티티 파괴 헬퍼 — 판 경계 = 조립 교체라 소비처가 없다 |
| 41 | `RebuildDraftMap/0` | 삭제 (드래프트 은퇴 — 계약 9 · 런타임 호출처 0) |  |
| 42 | `SetAuthoredWavePlan/1` | WaveScheduler |  |
| 43 | `TryInitializeGeneratedWaves/0` | WaveScheduler |  |
| 44 | `ScheduledWaveTime/1` | WaveScheduler |  |
| 45 | `QueueDueWaves/1` | WaveScheduler |  |
| 46 | `RefreshTimerHud/0` | MatchClock |  |
| 47 | `TryGetSpawnGuideForecast/2` | WaveScheduler | `CollectForecast` — 대기열이 이미 정본이라 예보를 따로 굽지 않는다(구우면 당김·보너스 뒤에 옛 값이 남는다) |
| 48 | `LastSpawnSec/1` | 삭제 | 구운 배열의 마지막 시각을 재던 보조 — 예보를 안 굽는다 |
| 49 | `TryGetSpawnPathSim/4` | `SpawnPathPreview.Build` — SpawnPathPreview (코어) | 이동과 **같은** 평활화·NavGrid·슬롯. 뷰에 두면 이동이 바뀌는 날 라인만 옛 규칙으로 남는다 |
| 50 | `TryResolveFirstStructureDestination/3` | AiMovePhase.TryPickStructure | M18 — 고르는 자는 하나다. 후보를 다시 모으던 것이 「예고선은 마음, 적은 본능」의 원인 |
| 51 | `AppendSpawnPathSegment/8` | `SpawnPathPreview.Append` — SpawnPathPreview.Append (코어, private) |  |
| 52 | `TryPullNextWave/0` | WaveScheduler |  |
| 53 | `ForceNextWave/0` | WaveScheduler |  |
| 54 | `QueueWave/4` | WaveScheduler |  |
| 55 | `SetDefenderPool/1` | 삭제 | 놓을 수 있는 목록은 정의표의 `Roster` 다(빌더가 판 밖에서 정한다) — 런타임에 미는 함수가 없다 |
| 56 | `SetSkillLoadout/1` | `HandDeck.Begin` — BindingRegistry / TriggerDispatcher |  |
| 57 | `CastSkillAtTile/3` | `HandDeck.TryCast` — MapRuntime (코어) |  |
| 58 | `CastPortal/4` | `HandDeck.TryCast` — BindingRegistry / TriggerDispatcher |  |
| 59 | `CollectAlliesInRange/3` | 삭제 (배틀 JSON 로그 전용 — rules X28 제거 · README 조각 E 에이전트 결정 ⑷) |  |
| 60 | `GridToWorldCenter/2` | MapRuntime.CenterOf | 코어가 이미 갖고 있다 — 뷰는 그것을 부르고 `BoardSpace.ToView` 로 옮긴다 |
| 61 | `GridToWorldCenterVector/2` | MapRuntime.CenterOf | 위와 같은 함수의 Vector3 오버로드 |
| 62 | `InTileRange/3` | 삭제 (배틀 JSON 로그 전용 — rules X28 제거 · README 조각 E 에이전트 결정 ⑷) |  |
| 63 | `DebugWorldToCell/1` | `MapRuntime.CellOf` — MapRuntime (코어) |  |
| 64 | `DebugWorldToCellFractional/1` | `DragPlacementInput.TryResolveCell` — MapRuntime (코어) |  |
| 65 | `WorldToFractionalCell/1` | `DragPlacementInput.TryResolveCell` — MapRuntime (코어) |  |
| 66 | `DebugCollectReachSpheres/1` | 삭제 (유일 소비처 `ReachDebugGizmos` 은퇴 — 8c 구현 6 에이전트 판정) |  |
| 67 | `TryGetNearestWalkCell/2` | `CoreHazardDebugMenu.NearestPathCell` — MapRuntime (코어) |  |
| 68 | `TryFindValidBlockingHazardCell/4` | `BlockerSpawn.TrySpawn` — MapRuntime (코어) |  |
| 69 | `IsInGeneratedMapBounds/1` | `MapRuntime.InBounds` — MapRuntime (코어) |  |
| 70 | `CastActiveSkillAtTile/9` | `HandDeck.Cast` — MapRuntime (코어) |  |
| 71 | `CountAlliesInTileRange/2` | 삭제 (배틀 JSON 로그 전용 — rules X28 제거 · README 조각 E 에이전트 결정 ⑷) |  |
| 72 | `CountEnemiesInTileRange/2` | 삭제 (배틀 JSON 로그 전용 — rules X28 제거 · README 조각 E 에이전트 결정 ⑷) |  |
| 73 | `Update/0` | BattleDriver | 누산 + 틱 발행 |
| 74 | `TickBattleFrame/0` | BattleDriver | 같은 자리 |
| 75 | `ReadFinalTally/3` | ScoreLedger |  |
| 76 | `SimIdOf/1` | 삭제 | `SimEntityId` 가 곧 그 id 다 — 변환할 것이 없다 |
| 77 | `CollectMatchConfig/0` | MatchDefinitionBuilder |  |
| 78 | `StepOneTick/0` | BattleDriver | `BattleMatch.Tick()` 한 번 |
| 79 | `ResolveBattleSimGroup/0` | 삭제 | ECS 시스템 그룹이 없다 — 순서는 `TickPipeline` 나열이다 |
| 80 | `LateUpdate/0` | `CoreUnitViewPool.LateUpdate` — 뷰 풀(각자) | 뷰 동기는 풀마다 자기 `LateUpdate` 다(계약 12 — 통합 뷰 없음) |
| 81 | `SyncProjectileViews/0` | `CoreProjectileViewPool.SyncTransform` — 뷰 풀 |  |
| 82 | `ReconcileStatusFx/0` | `CoreStatusFxSpawner.OnCoreEvent` — 뷰 풀 / 담당자 구독 (이벤트로 접힘) | 6c — `CoreStatusFxSpawner`(군중 제어·지속 피해·번아웃·라스트런) + `CoreDcAuraVisualPool`(강화 오라). 매 프레임 월드 폴링 → 걸림/풀림/숙주 소멸 사건 |
| 83 | `ReconcilePickupViews/0` | `CorePickupViewPool.OnCoreEvent` — 뷰 풀 | 6c 주인 정정 — `CorePickupViewPool`(`PickupSpawned`/`PickupTaken`/`PickupExpired`). 셈판은 `GimmickHost`, 그림은 풀이다 |
| 84 | `ClearPickupVisuals/0` | `CorePickupViewPool.Clear` — 뷰 풀 | 6c — `CorePickupViewPool.Clear`(판 경계 `MatchStarted`) |
| 85 | `ReconcileResignationViews/0` | `CoreResignationViewPool.OnCoreEvent` — 뷰 풀 | 6c 주인 정정 — `CoreResignationViewPool`(`ResignationDropped`/`ResignationConsumed`) |
| 86 | `ClearResignationVisuals/0` | `CoreResignationViewPool.Clear` — 뷰 풀 | 6c — `CoreResignationViewPool.Clear` |
| 87 | `PushBattleTimeScaleToEcs/0` | 삭제 | 코어는 배율을 모른다 — 느려지는 것은 **틱 발행률**이다(계약 5) |
| 88 | `SyncMonoUnitViews/0` | `CoreUnitViewPool.SyncViews` — 뷰 풀 |  |
| 89 | `SyncPatrolViews/3` | `CoreUnitViewPool.SyncViews` — 뷰 풀 |  |
| 90 | `ShieldRatioOf/2` | `ShieldMath.Sum` — ShieldMath.Sum (뷰 = 6c) | `ShieldMath.Sum(u.Shield.Slots) / u.MaxHealth`. 오버헤드 바가 읽는다 |
| 91 | `GatherOverheadStacks/1` | `StackSet.CountOf` — StackSet.CountOf (뷰 = 6c) | 스택 아이콘 행. 열기(`HeatAccrual`)는 6b2 가 같은 자리에 합류한다 |
| 92 | `TryMapOverheadStackKind/2` | `CoreUnitOverheadUiLayer.GatherStacks` — MapRuntime (코어) |  |
| 93 | `EvaluateEnemyHealthTint/1` | `CoreEnemyHealthTint.Resolve` — 통합 머리 위면 흰색(라이브) · 호출 `CoreUnitViewPool.SyncViews` (8a2) | 저체력 틴트. 값은 `CharacterViewConfig.healthDisplayStyle` |
| 94 | `SyncSummonerAnimationState/3` | `CoreUnitViewPool.SyncViews` — 소환 정책 Spine 뷰에 매 프레임 `CoreUnitView.SetAiState`(읽기 창 `Unit.Ai` · 이름 `SummonPatrolAbility`) (8a2) |  |
| 95 | `TraceDefenderAiTransition/1` | `CoreEvent.DefenderAiChanged` — 발행 `CombatPhase`(변할 때만) · 트레이스 `CoreTraceChannel.DefenderAiChanged`(61 · 골든 하네스 비구독) (8a2 — 새 트레이스에 같은 사건이 없어 흡수 불가 → 신설) |  |
| 96 | `FindSummonPatrolAbility/1` | `CombatDefinitionBuilder.BuildDefenderAttack` — CombatDefinitionBuilder (7d 확인) | `GetAbility<SummonPatrolAbility>()` → `AttackDef.SummonPatrolDefIndex` · 순찰 유닛을 정의표 줄에 편입 |
| 97 | `DrainDefenderDeathEvents/0` | `PlacementService.OnDestroyed` — 뷰 풀 / 담당자 구독 (이벤트로 접힘) |  |
| 98 | `OccupyDefenderFootprint/2` | PlacementService |  |
| 99 | `ReleaseDefenderFootprint/1` | PlacementService |  |
| 100 | `TryResolveDefenderKey/2` | 삭제 | 키는 `SimEntityId` 하나다 — 뷰·입력이 자기 등록부를 들지 않는다 |
| 101 | `TryCancelPendingDeployment/1` | 삭제 | 배치 뒤 되돌리기는 없다 — 보드 밖 드롭은 커맨드를 **안 보내고**, 이미 선 유닛의 복구는 퇴근이다 |
| 102 | `IsDefenderPendingDeployment/1` | PlacementService | `PendingActivations` · `Unit.Deploying` |
| 103 | `RetireDefender/1` | PlacementService.Retire | 커맨드 `Retire`. 입력은 RetireInput(길게 누르기) |
| 104 | `DrainDcTriggerFiredEvents/0` | `CoreVfxSpawner.OnTriggerFired` — 뷰 풀 / 담당자 구독 (이벤트로 접힘) |  |
| 105 | `ResolveBeamViewPos/3` | `CoreBeamPresenter.TryPlace` — 뷰 풀 |  |
| 106 | `EnsureBeamPresenter/0` | `CoreBeamPresenter` — 뷰 풀 |  |
| 107 | `DrainKnockupVisualEvents/0` | `CoreUnitViewPool.OnCoreEvent` — 뷰 풀 / 담당자 구독 (이벤트로 접힘) |  |
| 108 | `DrainShieldBreakEvents/0` | `TriggerDispatcher.RaiseShieldBreak` — 뷰 풀 / 담당자 구독 (이벤트로 접힘) |  |
| 109 | `FactionOfEntity/1` | 삭제 (코어 스폰 = BattleWorld.Spawn*) |  |
| 110 | `HostBodyRadiusOf/1` | `TriggerDispatcher.RaiseShieldBreak` — 실드 파열 규칙 (7a/7b 확정) | 6c 는 「뷰」로 적었으나 유일 소비처가 **실드 파열 대상 수집**(`BattleBridge.cs:4620`·`:4635`)이라 판정이다. 새 코어에서 숙주 몸은 감지자가 사건에 값으로 싣는다(`TriggerDispatcher.RaiseShieldBreak` — `SiteBody = victim.HitRadius`) → `OnShieldBreak` 규칙(`SelfAreaBlastSkill`·`AreaSleepSkill`)이 소비 |
| 111 | `DrainUnitAttackVisualEvents/0` | `CoreVfxSpawner.OnAttackResolved` — 뷰 풀 / 담당자 구독 (이벤트로 접힘) | 이 드레인 안의 공격 SFX 는 `CoreBattleAudio`(`AttackResolved`) — 구독자가 둘이다 |
| 112 | `TickPendingHitVfx/1` | `CoreVfxSpawner.OnAttackResolved` — 뷰 풀 |  |
| 113 | `DotAuraKind/1` | `DotSlot.Element` — DotSlot.Element (뷰 = 6c) | 오라가 읽는 축은 **원소**다(출처가 아니다) — `DotSet` 이 그 값을 슬롯에 들고 있다 |
| 114 | `FindDefenderData/1` | BattleDriver.DefenderAssets | 엔티티→SO 조회가 **줄 번호 되찾기**로 바뀌었다. 사건이 `DefIndex` 를 값으로 나른다(5a·5c) |
| 115 | `DrainAttackOutputLogEvents/0` | `CoreTrace.Record` — 뷰 풀 / 담당자 구독 (이벤트로 접힘) |  |
| 116 | `TrySpawnCastVfx/2` | `CoreVfxSpawner.OnProjectileSpawned` — 뷰 풀 | 6c 주인 정정 — `CoreVfxSpawner`(`ProjectileSpawned` → 탄 저작 `castPrefab`). 규칙이 아니라 총구 그림이다 |
| 117 | `PushStagePostVolume/0` | `CorePhaseFeed.PushBoard` — 5b(스테이지 뷰) | 스테이지 포스트 볼륨은 카메라 쪽 배선이다 |
| 118 | `EnsureCameraDirector/0` | `CorePhaseFeed.EnsureDirector` — 5b(카메라) | `CameraDirector` 는 재사용한다 — 배선 지점만 옮긴다 |
| 119 | `ImpactSocketHeightOf/1` | `CoreProjectileViewPool.TryGetImpactSocketHeight` — CoreUnitViewPool | 뷰 앵커 조회. 5a 는 소비처가 없어 열지 않았다(이식 제외) |
| 120 | `DrainProjectileHitEvents/0` | `CoreProjectileViewPool.PlayHitFromEvent` — 뷰 풀 / 담당자 구독 (이벤트로 접힘) |  |
| 121 | `DrainHealAppliedEvents/0` | `CoreVfxSpawner.OnCoreEvent` — 뷰 풀 / 담당자 구독 (이벤트로 접힘) |  |
| 122 | `DrainShieldGrantedEvents/0` | `CoreVfxSpawner.OnCoreEvent` — 뷰 풀 / 담당자 구독 (이벤트로 접힘) |  |
| 123 | `DrainDetectionEvents/0` | `CoreVfxSpawner.OnCoreEvent` — 뷰 풀 / 담당자 구독 (이벤트로 접힘) |  |
| 124 | `DrainDamageNumberEvents/0` | `CoreDamageNumberSpawner.OnCoreEvent` — 뷰 풀 / 담당자 구독 (이벤트로 접힘) |  |
| 125 | `ResolveUnitViewTransform/1` | `CoreStatusFxSpawner.AnchorOf` — 뷰 풀 |  |
| 126 | `TryGetUnitScreenAnchor/3` | CoreUnitOverheadUiLayer | 화면 앵커는 오버헤드가 직접 뷰에 묻는다 |
| 127 | `ProjectTileScreenWidth/1` | `CoreUnitOverheadUiLayer.ProjectTileScreenWidth` — MapRuntime (코어) |  |
| 128 | `TryGetGoalViewAnchor/1` | `CoreFirstRunGuide.TryGetGoalAnchor` — HeartMeter |  |
| 129 | `TryGetUnitViewAnchor/2` | `CoreCardTargets.TryGetUnitViewPosition` — 뷰 풀 |  |
| 130 | `TryGetUnitView/2` | `CoreCardTargets.TryGetUnitView` — 뷰 풀 |  |
| 131 | `SpawnCardAbsorbVfx/1` | `CoreVfxSpawner.SpawnCardAbsorb` — HandDeck |  |
| 132 | `GridCellToViewCenter/1` | `CoreCardTargets.CellViewCenter` — MapRuntime (코어) |  |
| 133 | `TryGetDefenderRestViewPos/2` | `CoreDeployFlightPresenter.RestViewPos` — 뷰 풀 |  |
| 134 | `FootprintAnchorToFoot/1` | PlacementService |  |
| 135 | `GridAnchorToViewCenter/2` | CoreDragPreviewPresenter | 드래그 실루엣의 자리(옛 `DefenderDragPlacementController.cs:1216`). 규칙(발밑 = 하단 행 가로 중앙)은 코어 `Footprint.FootPosition` 을 **호출만** 하고 `BoardSpace.ToView` 로 옮긴다 — 브리지의 `FootprintAnchorToFoot` 복제는 안 옮겼다 |
| 136 | `DrainEnemyKilledEvents/0` | `ScoreLedger.OnSlain` — 뷰 풀 / 담당자 구독 (이벤트로 접힘) |  |
| 137 | `DrainProjectileSpawnRequests/0` | `CoreProjectileViewPool.SpawnFromEvent` — 뷰 풀 / 담당자 구독 (이벤트로 접힘) | 안의 발사 SFX 는 `CoreBattleAudio`(`ProjectileSpawned`, 방어유닛 탄만) |
| 138 | `DrainMeteorBarrageRequests/0` | `ResignationBarrage.OnThreshold` — 뷰 풀 / 담당자 구독 (이벤트로 접힘) |  |
| 139 | `SpawnProjectile/2` | `IntentApplier.SpawnProjectile` — IntentApplier (7a) | 스킬 탄은 `IntentApplier.SpawnProjectile` → 요청 줄 → 착탄 관문(6a2). 퇴근 운석의 옛 직접 발사도 `DeathSiteBlast × OnRetire` 규칙으로 접혔다 |
| 140 | `CanDefenderTargetMover/2` | `CoreSkillContext.Collect` — CoreSkillContext (7a) | `CandidateFilter.MatchTraversalLayers` = `LayerBits.CanTarget(시전자 공격 층, 후보 통행 층)` · 스킬 패턴 층도 같은 술어 |
| 141 | `RegisteredFootprintRect/2` | PlacementService |  |
| 142 | `EnqueueStatModifier/6` | 삭제 (코어 내부 호출) |  |
| 143 | `EnqueueStatModifierRaw/7` | 삭제 (코어 내부 호출) |  |
| 144 | `EnqueueDamageMul/4` | 삭제 (코어 내부 호출) |  |
| 145 | `EnqueueMoveSpeedMul/4` | 삭제 (코어 내부 호출) |  |
| 146 | `TryScreenToCell/3` | `CoreCardTargets.TryScreenToCellStrict` — MapRuntime (코어) |  |
| 147 | `TryScreenToCellStrict/3` | `CoreCardTargets.TryScreenToCellStrict` — MapRuntime (코어) |  |
| 148 | `TryScreenToBoardFrac/3` | DragPlacementInput.TryResolveCell | `BoardSpace.ToSim` + `PlacementCellSnap`(순수 재사용) |
| 149 | `TryPickNearestEnemy/4` | `CoreCardTargets.TryPickNearestEnemy` — 입력 층 (7c 타겟 화살 — 7a 귀속 확정) | 화면 좌표 → 보드 평면 → **판 위 최근접 적** 픽(표식 카드 조준). 판정이 아니라 입력이라 코어에 두지 않는다 — 입력이 `BoardSpace` 레이 + `BattleWorld.Units` 읽기로 고르고 결과는 커맨드 대상이 된다 |
| 150 | `TryGetDefenderAt/2` | PlacementOccupancy.OwnerAt | 칸의 주인은 배치 담당자가 점유와 **쌍으로** 관리한다(옛 `_defenderByTile` 을 안 옮긴 자리) |
| 151 | `SetDefenderHoverHighlight/3` | `CoreCardFocusPresenter.SetHover` — CoreUnitViewPool | 유닛 뷰 `SetHoverHighlight` fan-out. 구동은 5b |
| 152 | `TryPickDefenderAtScreen/7` | `SelectionInput.TryPickDefender` — RetireInput.TryPickDefender | 화면 → 칸 → 점유 주인. 판정 없음 |
| 153 | `ScreenDistanceToRect/2` | 삭제 | 화면 사각까지의 거리로 집던 보조 — 칸 점유로 집으면 필요 없다 |
| 154 | `TryGetUnitScreenRect/3` | CoreUnitView.TryGetScreenRect | 뷰가 이미 갖고 있다 — 중개가 필요 없다 |
| 155 | `TryGetDefenderData/2` | BattleDriver.DefenderAssets | 정의표 줄 번호 → 저작 에셋. 트레이 초상·이름이 읽는다 |
| 156 | `SetDreamstones/1` | `MatchDefinitionBuilder.Build` — MatchDefinitionBuilder (7b) | 판 진입 반입 — `Build(…, dreamstones)` → `CardDefinitionBuilder`(스탯 돌 = `MatchDefinition.MatchBindings` · 코스트 돌 = `CostRateMultiplier`) |
| 157 | `ApplyPendingDreamstones/0` | `BindingRegistry.AttachMatchRows` — BindingRegistry (7b) | `BattleMatch.Begin` → `AttachMatchRows` — 판 호스트 `OnPlace(Any)` 규칙이 배치 유닛에 상속(`DreamstoneStatSkill`, 출처 Dreamstone) |
| 158 | `KnockbackOn/1` | 삭제 | 저작 술어(`거리>0 && 지속>0`)일 뿐이다. 넉백의 실체는 `CcState` 의 `Impulse` 슬롯이고, 「값이 있나」 판정은 부여 호출부에 이미 인라인돼 있다 |
| 159 | `GetOrCreateSkillVfxIndex/1` | `MatchViewAssets.RegisterSkillVfx` — BindingRegistry / TriggerDispatcher |  |
| 160 | `GetOrCreateProjectileDataIndex/1` | 삭제 (코어 스폰 = BattleWorld.Spawn*) |  |
| 161 | `EffectiveLeakLimit/0` | 삭제 (7d · 계약 9) | 유출 한도 제거 확정(X17) |
| 162 | `ResetGoalStability/0` | HeartMeter |  |
| 163 | `BakeProjectileRef/2` | 삭제 (코어 스폰 = BattleWorld.Spawn*) |  |
| 164 | `SpawnStructureEntities/0` | FieldPrepPhase.Begin | 코어가 저작 거점을 세운다 — 드라이버는 목록만 넘긴다 |
| 165 | `SpawnStructureViews/0` | `CoreStructurePropLayer.Rebuild` — 뷰 풀 |  |
| 166 | `ClearStructureViews/0` | `CoreStructurePropLayer.Clear` — 뷰 풀 |  |
| 167 | `DestroyStructureEntities/0` | 삭제 | 판이 끝나면 월드가 통째로 사라진다 — 개별 파괴 경로가 없다 |
| 168 | `RemainingLeakAllowance/0` | 삭제 (7d · 계약 9) | 유출 한도 제거 확정(X17) |
| 169 | `TryPayLeakAllowance/1` | 삭제 (7d · 계약 9) | 유출 한도 제거 확정(X17) |
| 170 | `DrainGoalEvents/0` | `HeartMeter.OnGoalReached` — 뷰 풀 / 담당자 구독 (이벤트로 접힘) |  |
| 171 | `NearestGoalCell/1` | HeartMeter |  |
| 172 | `EnqueueGoalHeal/1` | 삭제 (코어 내부 호출) |  |
| 173 | `EnqueueGoalTowerDamage/2` | 삭제 (코어 내부 호출) |  |
| 174 | `PushGoalCrack/2` | HeartMeter |  |
| 175 | `SyncGoalStability/0` | HeartMeter |  |
| 176 | `OpenBreachedCellsForLeak/1` | 삭제 (유출 칸 개방 휴면 코드 — rules X21 제거) |  |
| 177 | `OpenGoalCellAfterBreach/1` | 삭제 (유출 칸 개방 휴면 코드 — rules X21 제거 · 붕괴 연출은 `PlayCoreBurst` 행) |  |
| 178 | `LeakSiegingEnemy/1` | 삭제 (7d · 휴면 코드) | 골 붕괴 셀의 공성 적 → 유출 전환. 옛 코드도 **도달 불가**(heart-stress-axis 0 — 첫 붕괴가 판을 끝낸다 `BattleBridge.cs:7072`). 코어는 `HeartMeter.Damage` 가 첫 붕괴에 판을 닫는다 |
| 179 | `SubmitMatch/0` | MatchClock |  |
| 180 | `CheckTimer/0` | MatchClock |  |
| 181 | `NoQueuedAttackersRemain/0` | WaveScheduler.FieldClear | 전멸 술어(보너스 적 제외 · 자기 술어 X12) · 목표는 `LastWaveDispatchedAndFieldClear` 를 읽는다 |
| 182 | `ReportMatchResult/1` | CoreMatchOutcomePresenter | 게이트 = `submitsReport && allowSubmit`. `ReportResult` 시그니처 무변(계약 13) |
| 183 | `EndMatch/1` | MatchClock |  |
| 184 | `ShowResult/1` | CoreMatchOutcomePresenter | `MatchOutcome` → `MatchTally` 어댑터 한 줄 + `ResultScreen.Show` |
| 185 | `HoldThenShowResult/1` | CoreMatchOutcomePresenter | 박자 판정은 코어(`MatchClock.EndHasPresentationBeat`)가 이미 한다 |
| 186 | `ReleaseCoreBurstHold/1` | CoreMatchOutcomePresenter | 접두사 휴리스틱 오귀속 정정(5c) — 배치와 무관한 **결과 박자**의 리스 정리다 |
| 187 | `PlayCoreBurst/1` | `CoreVfxSpawner.OnHeartCollapsed` — 골 칸 붕괴 원샷 + `GoalMarker.MarkCollapsed` · 슬로모 = `CoreMatchOutcomePresenter`(5c 도메인 리스) (8a2) |  |
| 188 | `BuildTally/1` | IMatchGoal.BuildOutcome | 정정(5c) — 조립 지점은 목표다. `ScoreLedger` 는 그 재료 하나(점수)만 갖는다 |
| 189 | `PlaceDefender/2` | PlacementService.TryPlace | 커맨드 `PlaceDefender` → receipt |
| 190 | `SpatialPlacementCheck/4` | PlacementService |  |
| 191 | `SpatialFootprintCheck/7` | PlacementService |  |
| 192 | `GetPlacementCellReasons/4` | PlacementService |  |
| 193 | `TryFindNearestPlaceableAnchor/4` | 삭제 (자석 스냅 은퇴 — 사용자 결정 2026-09-23 · `DragPlacementInput` 머리말) | 자석도 코어의 것이다 — 프리뷰가 자기 자를 가지면 「초록인데 거절」 |
| 194 | `SetPlacementGhostCells/2` | `CoreMapOverlay.ShowPlacement` — PlacementService |  |
| 195 | `IsPlacementRangeCell/1` | `CoreMapOverlay.ShowPlacement` — PlacementService |  |
| 196 | `ClearPlacementGhostCells/0` | `CoreMapOverlay.ShowPlacement` — PlacementService |  |
| 197 | `CanPlaceDefenderAt/4` | PlacementService |  |
| 198 | `DeployedCountOf/1` | PlacementService.OnBoard |  |
| 199 | `TryGetDeployedEntity/2` | 삭제 (코어 스폰 = BattleWorld.Spawn*) |  |
| 200 | `TryQueueDeployedDefenderMaxHealthDamage/2` | 삭제 | 첫 판 튜토리얼의 저체력 연출 훅. 튜토리얼 콘텐츠는 2026-09 에 전량 제거됐고(76038c26) 새 코어에 자리가 없다 |
| 201 | `CloseCellLayers/1` | `MapSnapshot.CloseReservedPlacement` — MapRuntime (코어) |  |
| 202 | `ShowPlacementHighlight/2` | `CoreMapOverlay.ShowPlacement` — PlacementService |  |
| 203 | `HidePlacementHighlight/0` | `CoreMapOverlay.ShowPlacement` — PlacementService |  |
| 204 | `AnyEnemyWithinTilesOfGoal/1` | `CoreFirstRunGuide.AnyEnemyWithinTilesOfGoal` — HeartMeter |  |
| 205 | `NearestGoalDistance/1` | HeartMeter |  |
| 206 | `ShowBlockedHighlight/1` | CoreMapOverlay.ShowPlacement | 고스트가 빨강으로 답한다 |
| 207 | `HideBlockedHighlight/0` | CoreMapOverlay.HidePlacement |  |
| 208 | `RefreshPlacementHighlightIfShown/0` | `CoreMapOverlay.ShowPlacement` — PlacementService |  |
| 209 | `RepaintPlacementHighlight/0` | `CoreMapOverlay.ShowPlacement` — PlacementService |  |
| 210 | `PlaceDefenderAs/3` | 삭제 | 유닛을 인자로 받던 두 번째 진입 — 커맨드 하나로 접힌다 |
| 211 | `TryBeginDefenderDeployment/4` | PlacementService | 안의 배치 보이스는 `CoreBattleAudio`(`Placed`) |
| 212 | `LandDeployedDefender/1` | `PlacementService.Land` — 커맨드 `LandDefender` (DragPlacementInput) | 비행은 프레젠테이션 시간이라 코어가 길이를 모른다 |
| 213 | `ActivateDeployedDefender/2` | PlacementService.StepActivation |  |
| 214 | `OnDefenderActivated/1` | `CoreUnitViewPool.OnCoreEvent` — `DefenderActivated` 사건 구독 | 뷰 풀이 배치 모션을 재생한다 |
| 215 | `DrainDefenderActivatedEvents/0` | `CoreUnitViewPool.OnCoreEvent` — 뷰 풀 / 담당자 구독 (이벤트로 접힘) |  |
| 216 | `TriggerDeploymentOnPlaceSkill/2` | `TriggerDispatcher.OnActivated` — BindingRegistry / TriggerDispatcher |  |
| 217 | `ApplyEnvironmentGating/0` | 삭제 (7d) | 타일맵 모드에서 끌 옛 환경 오브젝트 — 필드 `tilemapHiddenEnvironment` 가 bridge-fields 73 에서 이미 삭제(새 씬에 끌 환경이 없다) |
| 218 | `SetPlacementHover/2` | `CoreMapOverlay.ShowPlacement` — PlacementService |  |
| 219 | `SetPlacementHover/3` | `CoreMapOverlay.ShowPlacement` — PlacementService |  |
| 220 | `PulsePlacementHover/2` | `CoreMapOverlay.ShowPlacement` — PlacementService |  |
| 221 | `PulsePlacementHover/3` | `CoreMapOverlay.ShowPlacement` — PlacementService |  |
| 222 | `SetPlacementStretch/4` | `CoreMapOverlay.ShowPlacement` — PlacementService |  |
| 223 | `ClearPlacementStretch/0` | `CoreMapOverlay.ShowPlacement` — PlacementService |  |
| 224 | `ClearPlacementHover/1` | `CoreMapOverlay.ShowPlacement` — PlacementService |  |
| 225 | `ClearPlacementHover/0` | `CoreMapOverlay.ShowPlacement` — PlacementService |  |
| 226 | `SetRangeOwner/1` | CoreMapOverlay.ShowPlacement | 링의 주인 = 지금 끌고 있는 유닛 |
| 227 | `BakeAttackShape/2` | 삭제 (코어 스폰 = BattleWorld.Spawn*) |  |
| 228 | `SetPlacementRange/2` | `CoreMapOverlay.ShowPlacement` — PlacementService |  |
| 229 | `RefreshRangeTargetMarks/3` | CoreMapOverlay.PaintRange | 표식 판정은 `AttackReach.InReach` **호출만**(제약 13). 같은 루프의 도형 가이드(`SetShapeGuide` 호출, 옛 `:8176-8183`)는 `CoreMapOverlay.PaintShapeGuide`(6c 후속) — 최근접은 코어 `NearestTargeting.RanksBefore` |
| 230 | `ClearPlacementRange/0` | `CoreMapOverlay.ShowPlacement` — PlacementService |  |
| 231 | `SetSkillAimRange/2` | `CoreMapOverlay.ShowAimRing` — BindingRegistry / TriggerDispatcher |  |
| 232 | `ClearSkillAimRange/0` | `CoreMapOverlay.HideAim` — BindingRegistry / TriggerDispatcher |  |
| 233 | `TryGetTileScreenCenter/3` | `CoreCardTargets.TryGetCellScreenCenter` — MapRuntime (코어) |  |
| 234 | `SetSkillAimCells/1` | `CoreMapOverlay.ShowAimCells` — MapRuntime (코어) |  |
| 235 | `PinSkillTelegraph/2` | `CoreMapOverlay.ShowTelegraph` — BindingRegistry / TriggerDispatcher |  |
| 236 | `CenteredRingRadius/1` | CoreMapOverlay.PaintRange | 반지름 = `사거리 + 내 몸`. **판정이 아니라 그 판정을 그리는 치수**다 |
| 237 | `PinCenteredRange/3` | CoreMapOverlay.PaintRange |  |
| 238 | `ClearSkillTelegraph/0` | `CoreMapOverlay.HideTelegraph` — BindingRegistry / TriggerDispatcher |  |
| 239 | `ClearRange/1` | CoreMapOverlay.HidePlacement |  |
| 240 | `SetAttachPreview/3` | `CoreMapOverlay.ShowAttachRange` — HandDeck |  |
| 241 | `CanDrawAttachPreviewFor/1` | `CoreMapOverlay.PaintCardArea` — HandDeck |  |
| 242 | `ClearAttachPreview/0` | `CoreMapOverlay.HideAttachRange` — HandDeck |  |
| 243 | `RedrawAttachPreview/0` | `CoreMapOverlay.PaintCardArea` — HandDeck |  |
| 244 | `SetPlacementRangeValidity/1` | `CoreMapOverlay.ShowPlacement` — PlacementService |  |
| 245 | `FlashPlacementReject/1` | PlacementService |  |
| 246 | `PlayDeploymentPresentation/3` | `CoreUnitViewPool.OnCoreEvent` — CoreUnitViewPool (`DefenderActivated` → PlayDeploy) | 모션만 옮겼다. 컷신 프레임은 저작 자산이고 그 소비처는 unit 6 의 VFX 풀 |
| 247 | `PlayFallbackDeploymentPulse/3` | `CoreVfxSpawner.PlayDeploymentLanding` — unit 6 VFX 풀 | 배치 펄스는 VFX 사건이 열리는 unit 6 의 것이다 |
| 248 | `PlayDeploymentRingPulse/2` | `CoreVfxSpawner.DeploymentRingPulse` — 뷰 풀 | 6c — `CoreVfxSpawner`(`Placed` → 착지 = 비행 키 소멸 프레임) |
| 249 | `CreateDefenderEntity/3` | 삭제 (코어 스폰 = BattleWorld.Spawn*) |  |
| 250 | `CreatePatrolEntity/5` | 삭제 (코어 스폰 = BattleWorld.Spawn*) |  |
| 251 | `TryGetPatrolHomeCell/4` | `CombatPhase.SpawnPatrol` — MapRuntime (코어) |  |
| 252 | `DebugSpawnPatrolAt/3` | `CoreSummonDebugMenu.Summon` — 디버그/로그 (도구 처분표) |  |
| 253 | `DebugTryGetPatrolAnchorCell/2` | `CoreSummonDebugMenu.AnchorCell` — MapRuntime (코어) |  |
| 254 | `RegisterPatrolUnitSO/1` | `CombatDefinitionBuilder.Fill` — CombatDefinitionBuilder (7d 확인) | 순찰 SO → `Units` 줄 인덱스. 옛 런타임 등록부의 후계는 bake 한 번이다 — 실행은 `CombatPhase.SpawnPatrol` |
| 255 | `DrainPatrolSpawnRequests/0` | `CombatPhase.SpawnPatrol` — 뷰 풀 / 담당자 구독 (이벤트로 접힘) |  |
| 256 | `AddEffectTile/2` | PlacementService | unit 6b 정정 — 칸 목록의 주인은 **뽑는 자**다(`Begin` 이 칸·종류를 함께 뽑고 판 내내 불변). 맵은 그 칸을 모른다 |
| 257 | `ApplyEffectTileIfAny/2` | PlacementService | unit 6b 정정 — `ApplyArmedTile`(활성화 엣지 · 저작 연산자 그대로 · 칸 `SlotKind.Tile`) |
| 258 | `ApplyEffectTileOnce/2` | PlacementService | unit 6b 정정 — **개체당** 1회: `ArmTileFor`(앵커 칸 하나 · 칸 소비 없음) → 활성화 엣지 `ApplyArmedTile`, 회수는 `RevokeTile`(퇴근 · F33). 배치 스킬 표식과 비공유(F19) |
| 259 | `FireOnPlaceCameraShake/1` | CameraDirector.Shake | 호출부 = `CoreVfxSpawner`(`DefenderActivated`, 6c). 세기·길이 = `DefenderUnitData.onPlaceShake*` |
| 260 | `MarkJustDeployedForRules/1` | `TriggerDispatcher.OnActivated` — `DefenderActivated` 사건 | 표식 컴포넌트를 남기지 않는다 — 남으면 다음 배치 사건과 섞인다(E6) |
| 261 | `DebugSpawnObstacleAt/2` | `CoreObstacleDebugMenu.Set` — 디버그/로그 (도구 처분표) |  |
| 262 | `SpawnHazardWithVisual/3` | `CoreHazardViewPool.SpawnZone` — 뷰 풀 |  |
| 263 | `DebugSpawnHazardAt/2` | `CoreHazardDebugMenu.SpawnZone` — 디버그/로그 (도구 처분표) |  |
| 264 | `SpawnBlockingHazardWithVisual/2` | `CoreHazardViewPool.SpawnBlocker` — 뷰 풀 |  |
| 265 | `DebugSpawnBlockingHazardAt/2` | `CoreHazardDebugMenu.SpawnBlocker` — 디버그/로그 (도구 처분표) |  |
| 266 | `DebugLogFatigueStacks/0` | `CoreGimmickDebugMenu.DumpGimmick` — 디버그/로그 (도구 처분표) |  |
| 267 | `DebugLogPickups/0` | `CoreGimmickDebugMenu.DumpGimmick` — GimmickHost |  |
| 268 | `RegisterBlockingHazardSO/1` | 삭제 (BoardEffectDefinitionBuilder.ToBlockingHazardDefs) | 런타임 등록부가 **판 밖 정의표**(`MatchDefinition.BlockingHazards`)로 바뀐다. 탄→길막 참조는 줄의 역참조(`SpawnedByProjectile`) |
| 269 | `RegisterZoneHazardSO/1` | 삭제 (BoardEffectDefinitionBuilder.ToHazardDefs) | 같은 이유 — `MatchDefinition.Hazards` 줄 번호가 곧 참조다 |
| 270 | `EnsureBlockingHazardVisualRoot/0` | `CoreHazardViewPool` — 뷰 풀 |  |
| 271 | `ClearBlockingHazardVisuals/0` | `CoreHazardViewPool.Clear` — 뷰 풀 |  |
| 272 | `RecordHazardSpawn/2` | 삭제 (`HazardSpawned` 사건 · CoreHarness 트레이스 구독) | 기록은 사건 구독이다(계약 7). 채널 42 |
| 273 | `DrainHazardRuntimeEvents/0` | `CoreTrace.Record` — 뷰 풀 / 담당자 구독 (이벤트로 접힘) |  |
| 274 | `DrainHazardSpawnRequests/0` | `HazardSpawn.Spawn` — 뷰 풀 / 담당자 구독 (이벤트로 접힘) |  |
| 275 | `SyncBlockingHazardOverheadGauges/1` | `CoreUnitOverheadUiLayer.SetBlocker` — 뷰 풀 |  |
| 276 | `DrainHazardDestroyedEvents/0` | `CoreHazardViewPool.OnCoreEvent` — 뷰 풀 / 담당자 구독 (이벤트로 접힘) |  |
| 277 | `DrainGoalCollapsedEvents/0` | `CoreVfxSpawner.OnHeartCollapsed` — `HeartCollapsed` 구독(트레이스는 코어 채널 29) (8a2) |  |
| 278 | `SyncGoalOverheadGauges/1` | `GoalMarker.SetStressTint` — 소비자(옛과 같다) · 구동 `CoreScoreHud.PaintMarkers`(마음 바와 같은 위상 · 마커 사상 `CoreGoalMarkers`) · 마음 바 = `CoreScoreHud.PaintHeart`(5b) · 체력 정본 `HeartMeter` (8a2 행 9) | 8c 는 `HeartMeter` 로 해석해 뷰 몫(월드 틴트)이 새는 것을 못 잡았다(8a2 행 9) |
| 279 | `RecordBlockingHazard/4` | 삭제 (`UnitSpawned` 사건 — `UnitKind.BlockingHazard`) | 길막은 유닛이라 스폰 사건이 이미 있다 |
| 280 | `RecordBlockingHazardDestroyed/2` | 삭제 (`UnitDestroyed` 사건 — `UnitKind.BlockingHazard`) | 같은 이유 — 문은 「부서짐」 하나 |
| 281 | `WorldToLogCell/1` | 삭제 (배틀 JSON 로그 전용 — rules X28 제거 · README 조각 E 에이전트 결정 ⑷) |  |
| 282 | `BlockingHazardLogSide/1` | 삭제 (배틀 JSON 로그 전용 — rules X28 제거 · README 조각 E 에이전트 결정 ⑷) |  |
| 283 | `BuildStackThresholdRegistry/0` | 삭제 (MatchDefinitionBuilder.ToStackRuleDefs) | 전역 사전(`StackKind` → 규칙)이 **자산당 한 줄**인 정의표로 바뀐다(F31). 등록 시점도 판 밖이다 |
| 284 | `CreateGimmickConfigIfActive/0` | GimmickHost |  |
| 285 | `GetStackThresholds/1` | 삭제 (StackRules.Resolve) | 종류로 전역 한 벌을 찾던 조회가 **줄 번호 해석**으로 바뀐다 — 미지정이면 그 종류의 첫 줄 |
| 286 | `ShapeToHazardVisualScale/3` | `CoreHazardViewPool.SpawnZone` — 뷰 풀 |  |
| 287 | `DebugSpawnObstacleContext/0` | `CoreObstacleDebugMenu.TryGetDriver` — 디버그/로그 (도구 처분표) |  |
| 288 | `LogPlacementReject/3` | PlacementService |  |
| 289 | `OnDestroy/0` | 삭제 (7d · 계약 1) | MonoBehaviour 수명 — 판이 끝나면 `BattleMatch` 가 통째로 버려진다 |
| 290 | `EnsureMonoViewPools/0` | `CoreUnitViewPool` — 뷰 풀 |  |
| 291 | `CreateViewPool/1` | 삭제 (코어 스폰 = BattleWorld.Spawn*) |  |
| 292 | `ResolveUnitMaterial/2` | CoreUnitViewPool | 쿼드 폴백 머티리얼. `RuntimeMaterialFactory` 경유로 바뀌었다 |
| 293 | `InstallSkillLayer/0` | `TriggerDispatcher.Install` — BindingRegistry / TriggerDispatcher |  |
| 294 | `RunImmediateSkills/0` | `CommandPhase.Execute` — BindingRegistry / TriggerDispatcher |  |
| 295 | `RoutingProbe/2` | `SkillRouting.SkillIdFor` — SkillRouting (7a) | 그물용 창은 필요 없다 — 표 자체가 코어 공개 함수(`Trigger/SkillRouting.SkillIdFor`)이고 `SkillRoutingTests` 가 전수로 친다 |
| 296 | `BakeNightmareMechanics/2` | 삭제 (코어 스폰 = BattleWorld.Spawn*) |  |
| 297 | `BakeUnitMechanics/6` | 삭제 (코어 스폰 = BattleWorld.Spawn*) |  |
| 298 | `BakeDefenderDirectionalPattern/3` | 삭제 (코어 스폰 = BattleWorld.Spawn*) |  |
| 299 | `TryBuildPatternSlot/5` | `BindingDefinitionBuilder.BindPattern` — BindingDefinitionBuilder (7a) | `BindPattern` — 거절 규칙(FanOut 범위 0 · FanOut 비개체 조준 · 방향 사거리 0 · TryToSpec) 이식. 슬롯은 규칙(`Binding.Emitters`)이 든다 |
| 300 | `BuildPatternTemplate/4` | `CombatPhase.EmitPatternShot` — CombatPhase (7a) | 템플릿 조립은 발사 시점 `EmitPatternShot`/`FanOut` 이 한다 — 진영(상대 진영 유닛)·층(시전자 공격 층)은 `EmitterInstance.FromSkill` 이 가른다 |
| 301 | `SpawnUnit/1` | WaveScheduler |  |
| 302 | `CreateEnemyEntity/4` | 삭제 (코어 스폰 = BattleWorld.Spawn*) |  |
| 303 | `ConeCosSq/1` | `BindingDefinitionBuilder.Bake` — BindingDefinitionBuilder (7a) | bake 1회 변환(도 → cos²). 그림용 반각은 `BindingDef.ConeHalfAngleDeg` 로 함께 싣는다(브레스 `TriggerFired`) |
| 304 | `SpawnSplitChildren/2` | `EnemySplit.Run` — EnemySplit (코어 · 7d) | 사망 seam `OnSlain` — 부모 칸 중심 + `2π·c/count` · 첫 슬롯 · 자기순환 거절 · 전멸 판정 앞(`d4cded945`) |
| 305 | `CreateAttackUnitRuntimeMaterial/1` | 삭제 (코어 스폰 = BattleWorld.Spawn*) |  |
