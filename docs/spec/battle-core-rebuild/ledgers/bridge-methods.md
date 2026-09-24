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
| 9 | `ClearBonusPortalViews/0` | WaveScheduler |  |

## BattleBridge.BossLeap.cs (7)

| # | 메서드 | 새 주인 | 비고 |
|---|---|---|---|
| 1 | `TryGetEnemyViewOverride/3` | 뷰 풀 |  |
| 2 | `CreateBossLeapChannel/0` | 삭제 (코어 스폰 = BattleWorld.Spawn*) |  |
| 3 | `DisposeBossLeapChannel/0` | 삭제 | 채널이 없다 — 도약은 `LeapAscend`/`LeapDescend` 사건이고 수명은 구독이다 |
| 4 | `DrainBossLeapVisualEvents/0` | 뷰 풀 / 담당자 구독 (이벤트로 접힘) |  |
| 5 | `RunBossLeap/1` | CoreLeapPresenter | 뷰 비행 코루틴. 슬램 발사는 **안 옮긴다** — 코어가 이미 낸다 |
| 6 | `ResolveLanding/2` | 삭제 | 착지 슬램은 코어(`CombatPhase.StepLeap`)의 것이다. 뷰가 투사체를 쏘던 자리 |
| 7 | `PlayLeapPuff/2` | 삭제 | 사건이 `dataIndex` 를 안 나른다. 슬램이 있으면 착탄 VFX 가 이미 그 자리를 그린다 — 5a 이식 제외 참조 |

## BattleBridge.Dreamcatcher.cs (22)

| # | 메서드 | 새 주인 | 비고 |
|---|---|---|---|
| 1 | `NotifyEnemyGoneIfMarked/1` | 미정 |  |
| 2 | `IsEnemyMarked/1` | 미정 |  |
| 3 | `ApplyDreamcatcherCard/2` | HandDeck |  |
| 4 | `ApplyDreamcatcherCardHosted/1` | HandDeck |  |
| 5 | `ApplyDreamcatcherCardInternal/2` | HandDeck |  |
| 6 | `RevokeDreamcatcherEffects/1` | HandDeck |  |
| 7 | `ApplyActiveDcEffectsTo/2` | HandDeck |  |
| 8 | `ApplyPlacementSleep/2` | PlacementService |  |
| 9 | `ApplyDreamcatcherCardToUnit/2` | HandDeck |  |
| 10 | `WouldDreamcatcherCardApply/2` | HandDeck |  |
| 11 | `ApplyBountyMark/2` | 미정 |  |
| 12 | `PassesAttachRequirement/2` | HandDeck |  |
| 13 | `LogAttachRequirementReject/2` | HandDeck |  |
| 14 | `BuildHostProfile/1` | 미정 |  |
| 15 | `TargetsEnemies/1` | 미정 |  |
| 16 | `HasPositiveDamageOutput/1` | 미정 |  |
| 17 | `RegisterPlacementAura/3` | PlacementService |  |
| 18 | `MapDcEffect/3` | HandDeck |  |
| 19 | `MapDcBuff/4` | HandDeck |  |
| 20 | `MapDcCc/1` | HandDeck |  |
| 21 | `MapDcStack/1` | HandDeck |  |
| 22 | `MatchesDcAxis/2` | HandDeck |  |

## BattleBridge.Relocation.cs (17)

| # | 메서드 | 새 주인 | 비고 |
|---|---|---|---|
| 1 | `RelocationCheck/7` | 미정 |  |
| 2 | `RelocationFootprintCheck/9` | PlacementService |  |
| 3 | `TryGetDefenderAt/4` | 미정 |  |
| 4 | `TryGetDefenderCell/2` | MapRuntime (코어) |  |
| 5 | `CanRelocateDefender/3` | PlacementService |  |
| 6 | `HasCostForRelocation/1` | CostLedger |  |
| 7 | `TryBeginDefenderRelocation/4` | PlacementService |  |
| 8 | `RelocatePatrolAnchorFor/2` | 미정 |  |
| 9 | `SetDefenderViewOverride/4` | 뷰 풀 |  |
| 10 | `ClearDefenderViewOverride/1` | 뷰 풀 |  |
| 11 | `PlayLandingSquash/3` | 미정 |  |
| 12 | `TryGetDefenderViewOverride/4` | 뷰 풀 |  |
| 13 | `TryGetRelocationAnchors/5` | 미정 |  |
| 14 | `ActivateRelocatedDefender/3` | 미정 |  |
| 15 | `ApplyRefitHeal/2` | 미정 |  |
| 16 | `FinishDefenderRelocation/2` | 미정 |  |
| 17 | `DebugRelocateFirstDefender/0` | 디버그/로그 (도구 처분표) |  |

## BattleBridge.UltimateLeap.cs (6)

| # | 메서드 | 새 주인 | 비고 |
|---|---|---|---|
| 1 | `CreateUltimateLeapChannel/0` | 삭제 (코어 스폰 = BattleWorld.Spawn*) |  |
| 2 | `DisposeUltimateLeapChannel/0` | 삭제 | 위와 같다 — 채널이 없다 |
| 3 | `ShowLandingTelegraph/1` | 5b(맵 오버레이) | 착지 예고 링은 보드에 그리는 것이라 오버레이의 몫이다 |
| 4 | `DrainUltimateLeapVisualEvents/0` | 뷰 풀 / 담당자 구독 (이벤트로 접힘) |  |
| 5 | `RunUltimateLeapAscend/2` | CoreLeapPresenter | 이탈 — 올라가서 **머무른다**(예고 시간은 코어가 소유) |
| 6 | `RunUltimateLeapDescend/3` | CoreLeapPresenter | 강하 — `LeapDescend` 를 받아 내려온다 |

## BattleBridge.UnitStats.cs (1)

| # | 메서드 | 새 주인 | 비고 |
|---|---|---|---|
| 1 | `TryGetUnitStatReadout/2` | EffectiveStats (선택 패널 = 6c) | 실효 스탯 = `Unit.Modifiers.Effective`, 기본 = 정의표 줄. 델타 칩을 그리는 자는 5b 의 패널이고 배선은 6c |

## BattleBridge.cs (305)

| # | 메서드 | 새 주인 | 비고 |
|---|---|---|---|
| 1 | `SetEnemiesDimmed/1` | CoreUnitViewPool | 유닛 뷰 `SetDimmed` fan-out. 구동은 5b 의 드래그 입력 |
| 2 | `SetPlacementHighlightAboveUnits/1` | PlacementService |  |
| 3 | `CreateAliveAttackerQueries/0` | 삭제 (코어 스폰 = BattleWorld.Spawn*) |  |
| 4 | `MirrorLiftKnobs/0` | 뷰 풀 |  |
| 5 | `SetMatchSeed/1` | `ModeSelection.Seed` → BattleDriver | 재현의 둘째 축. 씬 경계를 넘어오는 값이라 진입 선택이 나른다(0 = 저작 시드) |
| 6 | `BuildBriefingWavePlan/0` | WaveScheduler |  |
| 7 | `SetAssignedGimmick/1` | GimmickHost |  |
| 8 | `Awake/0` | 미정 |  |
| 9 | `OnValidate/0` | 미정 |  |
| 10 | `ApplyUnitHealthPresentationMode/0` | CharacterViewConfig | 표시 모드는 저작 값이 됐다 — 런타임에 미는 함수가 없다 |
| 11 | `EnterPlacementOrIntro/0` | PlacementService |  |
| 12 | `OnRestartRequested/0` | 미정 |  |
| 13 | `ReLogSkillLoadoutForNewSession/1` | BindingRegistry / TriggerDispatcher |  |
| 14 | `TeardownCurrentBattle/0` | MatchClock |  |
| 15 | `HasLiveEntityManager/0` | 삭제 (코어 스폰 = BattleWorld.Spawn*) |  |
| 16 | `AttachSimEntityId/1` | HandDeck |  |
| 17 | `DestroyBattleEntities/0` | 미정 |  |
| 18 | `DestroyEcsInfrastructureEntities/0` | 미정 |  |
| 19 | `DisposeEcsInfrastructureNativeContainers/0` | 미정 |  |
| 20 | `DisposeCachedQueries/0` | 미정 |  |
| 21 | `BuildFlowField/0` | MapRuntime (코어) |  |
| 22 | `AddTraversalMask/2` | 미정 |  |
| 23 | `BuildPickupSpawnState/0` | GimmickHost |  |
| 24 | `TeardownPickupSpawnState/0` | MatchClock |  |
| 25 | `ComputeSpawnLateralOffset/1` | 미정 |  |
| 26 | `BuildStageMarkerRegistry/0` | BattleDriver | 스테이지 스캔 → 거점 목록. `Build(…, structures:)` 의 입력 |
| 27 | `TryGetGoalVisualAnchor/1` | HeartMeter |  |
| 28 | `TryGetSpawnVisualAnchor/2` | 뷰 풀 |  |
| 29 | `CellCenterView/1` | MapRuntime (코어) |  |
| 30 | `TeardownGeneratedMap/0` | MatchClock |  |
| 31 | `BuildMapForBattle/0` | MapRuntime (코어) |  |
| 32 | `TeardownFlowField/0` | MatchClock |  |
| 33 | `BeginPlacement/0` | PlacementService |  |
| 34 | `StartBattle/0` | MatchClock |  |
| 35 | `EnsureQueriesAndQueues/0` | 미정 |  |
| 36 | `StopBattle/0` | MatchClock |  |
| 37 | `PrepareDraftMap/0` | MapRuntime (코어) |  |
| 38 | `DeferredPrepareDraftMap/0` | MapRuntime (코어) |  |
| 39 | `CleanupDraftMapBeforeRebuild/0` | MapRuntime (코어) |  |
| 40 | `DestroyEntitiesByType/0` | 미정 |  |
| 41 | `RebuildDraftMap/0` | MapRuntime (코어) |  |
| 42 | `SetAuthoredWavePlan/1` | WaveScheduler |  |
| 43 | `TryInitializeGeneratedWaves/0` | WaveScheduler |  |
| 44 | `ScheduledWaveTime/1` | WaveScheduler |  |
| 45 | `QueueDueWaves/1` | WaveScheduler |  |
| 46 | `RefreshTimerHud/0` | MatchClock |  |
| 47 | `TryGetSpawnGuideForecast/2` | WaveScheduler | `CollectForecast` — 대기열이 이미 정본이라 예보를 따로 굽지 않는다(구우면 당김·보너스 뒤에 옛 값이 남는다) |
| 48 | `LastSpawnSec/1` | 삭제 | 구운 배열의 마지막 시각을 재던 보조 — 예보를 안 굽는다 |
| 49 | `TryGetSpawnPathSim/4` | SpawnPathPreview (코어) | 이동과 **같은** 평활화·NavGrid·슬롯. 뷰에 두면 이동이 바뀌는 날 라인만 옛 규칙으로 남는다 |
| 50 | `TryResolveFirstStructureDestination/3` | AiMovePhase.TryPickStructure | M18 — 고르는 자는 하나다. 후보를 다시 모으던 것이 「예고선은 마음, 적은 본능」의 원인 |
| 51 | `AppendSpawnPathSegment/8` | SpawnPathPreview.Append (코어, private) |  |
| 52 | `TryPullNextWave/0` | WaveScheduler |  |
| 53 | `ForceNextWave/0` | WaveScheduler |  |
| 54 | `QueueWave/4` | WaveScheduler |  |
| 55 | `SetDefenderPool/1` | 삭제 | 놓을 수 있는 목록은 정의표의 `Roster` 다(빌더가 판 밖에서 정한다) — 런타임에 미는 함수가 없다 |
| 56 | `SetSkillLoadout/1` | BindingRegistry / TriggerDispatcher |  |
| 57 | `CastSkillAtTile/3` | MapRuntime (코어) |  |
| 58 | `CastPortal/4` | BindingRegistry / TriggerDispatcher |  |
| 59 | `CollectAlliesInRange/3` | MatchDefinitionBuilder |  |
| 60 | `GridToWorldCenter/2` | MapRuntime.CenterOf | 코어가 이미 갖고 있다 — 뷰는 그것을 부르고 `BoardSpace.ToView` 로 옮긴다 |
| 61 | `GridToWorldCenterVector/2` | MapRuntime.CenterOf | 위와 같은 함수의 Vector3 오버로드 |
| 62 | `InTileRange/3` | MapRuntime (코어) |  |
| 63 | `DebugWorldToCell/1` | MapRuntime (코어) |  |
| 64 | `DebugWorldToCellFractional/1` | MapRuntime (코어) |  |
| 65 | `WorldToFractionalCell/1` | MapRuntime (코어) |  |
| 66 | `DebugCollectReachSpheres/1` | MatchDefinitionBuilder |  |
| 67 | `TryGetNearestWalkCell/2` | MapRuntime (코어) |  |
| 68 | `TryFindValidBlockingHazardCell/4` | MapRuntime (코어) |  |
| 69 | `IsInGeneratedMapBounds/1` | MapRuntime (코어) |  |
| 70 | `CastActiveSkillAtTile/9` | MapRuntime (코어) |  |
| 71 | `CountAlliesInTileRange/2` | MapRuntime (코어) |  |
| 72 | `CountEnemiesInTileRange/2` | MapRuntime (코어) |  |
| 73 | `Update/0` | BattleDriver | 누산 + 틱 발행 |
| 74 | `TickBattleFrame/0` | BattleDriver | 같은 자리 |
| 75 | `ReadFinalTally/3` | ScoreLedger |  |
| 76 | `SimIdOf/1` | 삭제 | `SimEntityId` 가 곧 그 id 다 — 변환할 것이 없다 |
| 77 | `CollectMatchConfig/0` | MatchDefinitionBuilder |  |
| 78 | `StepOneTick/0` | BattleDriver | `BattleMatch.Tick()` 한 번 |
| 79 | `ResolveBattleSimGroup/0` | 삭제 | ECS 시스템 그룹이 없다 — 순서는 `TickPipeline` 나열이다 |
| 80 | `LateUpdate/0` | 뷰 풀(각자) | 뷰 동기는 풀마다 자기 `LateUpdate` 다(계약 12 — 통합 뷰 없음) |
| 81 | `SyncProjectileViews/0` | 뷰 풀 |  |
| 82 | `ReconcileStatusFx/0` | 미정 |  |
| 83 | `ReconcilePickupViews/0` | GimmickHost |  |
| 84 | `ClearPickupVisuals/0` | GimmickHost |  |
| 85 | `ReconcileResignationViews/0` | GimmickHost |  |
| 86 | `ClearResignationVisuals/0` | GimmickHost |  |
| 87 | `PushBattleTimeScaleToEcs/0` | 삭제 | 코어는 배율을 모른다 — 느려지는 것은 **틱 발행률**이다(계약 5) |
| 88 | `SyncMonoUnitViews/0` | 뷰 풀 |  |
| 89 | `SyncPatrolViews/3` | 뷰 풀 |  |
| 90 | `ShieldRatioOf/2` | ShieldMath.Sum (뷰 = 6c) | `ShieldMath.Sum(u.Shield.Slots) / u.MaxHealth`. 오버헤드 바가 읽는다 |
| 91 | `GatherOverheadStacks/1` | StackSet.CountOf (뷰 = 6c) | 스택 아이콘 행. 열기(`HeatAccrual`)는 6b2 가 같은 자리에 합류한다 |
| 92 | `TryMapOverheadStackKind/2` | MapRuntime (코어) |  |
| 93 | `EvaluateEnemyHealthTint/1` | CoreUnitViewPool | 저체력 틴트. 값은 `CharacterViewConfig.healthDisplayStyle` |
| 94 | `SyncSummonerAnimationState/3` | 뷰 풀 |  |
| 95 | `TraceDefenderAiTransition/1` | 디버그/로그 (도구 처분표) |  |
| 96 | `FindSummonPatrolAbility/1` | 미정 |  |
| 97 | `DrainDefenderDeathEvents/0` | 뷰 풀 / 담당자 구독 (이벤트로 접힘) |  |
| 98 | `OccupyDefenderFootprint/2` | PlacementService |  |
| 99 | `ReleaseDefenderFootprint/1` | PlacementService |  |
| 100 | `TryResolveDefenderKey/2` | 삭제 | 키는 `SimEntityId` 하나다 — 뷰·입력이 자기 등록부를 들지 않는다 |
| 101 | `TryCancelPendingDeployment/1` | 삭제 | 배치 뒤 되돌리기는 없다 — 보드 밖 드롭은 커맨드를 **안 보내고**, 이미 선 유닛의 복구는 퇴근이다 |
| 102 | `IsDefenderPendingDeployment/1` | PlacementService | `PendingActivations` · `Unit.Deploying` |
| 103 | `RetireDefender/1` | PlacementService.Retire | 커맨드 `Retire`. 입력은 RetireInput(길게 누르기) |
| 104 | `DrainDcTriggerFiredEvents/0` | 뷰 풀 / 담당자 구독 (이벤트로 접힘) |  |
| 105 | `ResolveBeamViewPos/3` | 뷰 풀 |  |
| 106 | `EnsureBeamPresenter/0` | 뷰 풀 |  |
| 107 | `DrainKnockupVisualEvents/0` | 뷰 풀 / 담당자 구독 (이벤트로 접힘) |  |
| 108 | `DrainShieldBreakEvents/0` | 뷰 풀 / 담당자 구독 (이벤트로 접힘) |  |
| 109 | `FactionOfEntity/1` | 삭제 (코어 스폰 = BattleWorld.Spawn*) |  |
| 110 | `HostBodyRadiusOf/1` | 미정 |  |
| 111 | `DrainUnitAttackVisualEvents/0` | 뷰 풀 / 담당자 구독 (이벤트로 접힘) | 이 드레인 안의 공격 SFX 는 `CoreBattleAudio`(`AttackResolved`) — 구독자가 둘이다 |
| 112 | `TickPendingHitVfx/1` | 뷰 풀 |  |
| 113 | `DotAuraKind/1` | DotSlot.Element (뷰 = 6c) | 오라가 읽는 축은 **원소**다(출처가 아니다) — `DotSet` 이 그 값을 슬롯에 들고 있다 |
| 114 | `FindDefenderData/1` | BattleDriver.DefenderAssets | 엔티티→SO 조회가 **줄 번호 되찾기**로 바뀌었다. 사건이 `DefIndex` 를 값으로 나른다(5a·5c) |
| 115 | `DrainAttackOutputLogEvents/0` | 뷰 풀 / 담당자 구독 (이벤트로 접힘) |  |
| 116 | `TrySpawnCastVfx/2` | BindingRegistry / TriggerDispatcher |  |
| 117 | `PushStagePostVolume/0` | 5b(스테이지 뷰) | 스테이지 포스트 볼륨은 카메라 쪽 배선이다 |
| 118 | `EnsureCameraDirector/0` | 5b(카메라) | `CameraDirector` 는 재사용한다 — 배선 지점만 옮긴다 |
| 119 | `ImpactSocketHeightOf/1` | CoreUnitViewPool | 뷰 앵커 조회. 5a 는 소비처가 없어 열지 않았다(이식 제외) |
| 120 | `DrainProjectileHitEvents/0` | 뷰 풀 / 담당자 구독 (이벤트로 접힘) |  |
| 121 | `DrainHealAppliedEvents/0` | 뷰 풀 / 담당자 구독 (이벤트로 접힘) |  |
| 122 | `DrainShieldGrantedEvents/0` | 뷰 풀 / 담당자 구독 (이벤트로 접힘) |  |
| 123 | `DrainDetectionEvents/0` | 뷰 풀 / 담당자 구독 (이벤트로 접힘) |  |
| 124 | `DrainDamageNumberEvents/0` | 뷰 풀 / 담당자 구독 (이벤트로 접힘) |  |
| 125 | `ResolveUnitViewTransform/1` | 뷰 풀 |  |
| 126 | `TryGetUnitScreenAnchor/3` | CoreUnitOverheadUiLayer | 화면 앵커는 오버헤드가 직접 뷰에 묻는다 |
| 127 | `ProjectTileScreenWidth/1` | MapRuntime (코어) |  |
| 128 | `TryGetGoalViewAnchor/1` | HeartMeter |  |
| 129 | `TryGetUnitViewAnchor/2` | 뷰 풀 |  |
| 130 | `TryGetUnitView/2` | 뷰 풀 |  |
| 131 | `SpawnCardAbsorbVfx/1` | HandDeck |  |
| 132 | `GridCellToViewCenter/1` | MapRuntime (코어) |  |
| 133 | `TryGetDefenderRestViewPos/2` | 뷰 풀 |  |
| 134 | `FootprintAnchorToFoot/1` | PlacementService |  |
| 135 | `GridAnchorToViewCenter/2` | 뷰 풀 |  |
| 136 | `DrainEnemyKilledEvents/0` | 뷰 풀 / 담당자 구독 (이벤트로 접힘) |  |
| 137 | `DrainProjectileSpawnRequests/0` | 뷰 풀 / 담당자 구독 (이벤트로 접힘) | 안의 발사 SFX 는 `CoreBattleAudio`(`ProjectileSpawned`, 방어유닛 탄만) |
| 138 | `DrainMeteorBarrageRequests/0` | 뷰 풀 / 담당자 구독 (이벤트로 접힘) |  |
| 139 | `SpawnProjectile/2` | 미정 |  |
| 140 | `CanDefenderTargetMover/2` | 미정 |  |
| 141 | `RegisteredFootprintRect/2` | PlacementService |  |
| 142 | `EnqueueStatModifier/6` | 삭제 (코어 내부 호출) |  |
| 143 | `EnqueueStatModifierRaw/7` | 삭제 (코어 내부 호출) |  |
| 144 | `EnqueueDamageMul/4` | 삭제 (코어 내부 호출) |  |
| 145 | `EnqueueMoveSpeedMul/4` | 삭제 (코어 내부 호출) |  |
| 146 | `TryScreenToCell/3` | MapRuntime (코어) |  |
| 147 | `TryScreenToCellStrict/3` | MapRuntime (코어) |  |
| 148 | `TryScreenToBoardFrac/3` | DragPlacementInput.TryResolveCell | `BoardSpace.ToSim` + `PlacementCellSnap`(순수 재사용) |
| 149 | `TryPickNearestEnemy/4` | 미정 |  |
| 150 | `TryGetDefenderAt/2` | PlacementOccupancy.OwnerAt | 칸의 주인은 배치 담당자가 점유와 **쌍으로** 관리한다(옛 `_defenderByTile` 을 안 옮긴 자리) |
| 151 | `SetDefenderHoverHighlight/3` | CoreUnitViewPool | 유닛 뷰 `SetHoverHighlight` fan-out. 구동은 5b |
| 152 | `TryPickDefenderAtScreen/7` | RetireInput.TryPickDefender | 화면 → 칸 → 점유 주인. 판정 없음 |
| 153 | `ScreenDistanceToRect/2` | 삭제 | 화면 사각까지의 거리로 집던 보조 — 칸 점유로 집으면 필요 없다 |
| 154 | `TryGetUnitScreenRect/3` | CoreUnitView.TryGetScreenRect | 뷰가 이미 갖고 있다 — 중개가 필요 없다 |
| 155 | `TryGetDefenderData/2` | BattleDriver.DefenderAssets | 정의표 줄 번호 → 저작 에셋. 트레이 초상·이름이 읽는다 |
| 156 | `SetDreamstones/1` | 미정 |  |
| 157 | `ApplyPendingDreamstones/0` | 미정 |  |
| 158 | `KnockbackOn/1` | 삭제 | 저작 술어(`거리>0 && 지속>0`)일 뿐이다. 넉백의 실체는 `CcState` 의 `Impulse` 슬롯이고, 「값이 있나」 판정은 부여 호출부에 이미 인라인돼 있다 |
| 159 | `GetOrCreateSkillVfxIndex/1` | BindingRegistry / TriggerDispatcher |  |
| 160 | `GetOrCreateProjectileDataIndex/1` | 삭제 (코어 스폰 = BattleWorld.Spawn*) |  |
| 161 | `EffectiveLeakLimit/0` | 미정 |  |
| 162 | `ResetGoalStability/0` | HeartMeter |  |
| 163 | `BakeProjectileRef/2` | 삭제 (코어 스폰 = BattleWorld.Spawn*) |  |
| 164 | `SpawnStructureEntities/0` | FieldPrepPhase.Begin | 코어가 저작 거점을 세운다 — 드라이버는 목록만 넘긴다 |
| 165 | `SpawnStructureViews/0` | 뷰 풀 |  |
| 166 | `ClearStructureViews/0` | 뷰 풀 |  |
| 167 | `DestroyStructureEntities/0` | 삭제 | 판이 끝나면 월드가 통째로 사라진다 — 개별 파괴 경로가 없다 |
| 168 | `RemainingLeakAllowance/0` | 미정 |  |
| 169 | `TryPayLeakAllowance/1` | 미정 |  |
| 170 | `DrainGoalEvents/0` | 뷰 풀 / 담당자 구독 (이벤트로 접힘) |  |
| 171 | `NearestGoalCell/1` | HeartMeter |  |
| 172 | `EnqueueGoalHeal/1` | 삭제 (코어 내부 호출) |  |
| 173 | `EnqueueGoalTowerDamage/2` | 삭제 (코어 내부 호출) |  |
| 174 | `PushGoalCrack/2` | HeartMeter |  |
| 175 | `SyncGoalStability/0` | HeartMeter |  |
| 176 | `OpenBreachedCellsForLeak/1` | MapRuntime (코어) |  |
| 177 | `OpenGoalCellAfterBreach/1` | HeartMeter |  |
| 178 | `LeakSiegingEnemy/1` | 미정 |  |
| 179 | `SubmitMatch/0` | MatchClock |  |
| 180 | `CheckTimer/0` | MatchClock |  |
| 181 | `NoQueuedAttackersRemain/0` | 미정 |  |
| 182 | `ReportMatchResult/1` | CoreMatchOutcomePresenter | 게이트 = `submitsReport && allowSubmit`. `ReportResult` 시그니처 무변(계약 13) |
| 183 | `EndMatch/1` | MatchClock |  |
| 184 | `ShowResult/1` | CoreMatchOutcomePresenter | `MatchOutcome` → `MatchTally` 어댑터 한 줄 + `ResultScreen.Show` |
| 185 | `HoldThenShowResult/1` | CoreMatchOutcomePresenter | 박자 판정은 코어(`MatchClock.EndHasPresentationBeat`)가 이미 한다 |
| 186 | `ReleaseCoreBurstHold/1` | CoreMatchOutcomePresenter | 접두사 휴리스틱 오귀속 정정(5c) — 배치와 무관한 **결과 박자**의 리스 정리다 |
| 187 | `PlayCoreBurst/1` | HeartMeter |  |
| 188 | `BuildTally/1` | IMatchGoal.BuildOutcome | 정정(5c) — 조립 지점은 목표다. `ScoreLedger` 는 그 재료 하나(점수)만 갖는다 |
| 189 | `PlaceDefender/2` | PlacementService.TryPlace | 커맨드 `PlaceDefender` → receipt |
| 190 | `SpatialPlacementCheck/4` | PlacementService |  |
| 191 | `SpatialFootprintCheck/7` | PlacementService |  |
| 192 | `GetPlacementCellReasons/4` | PlacementService |  |
| 193 | `TryFindNearestPlaceableAnchor/4` | PlacementService.TrySnapAnchor | 자석도 코어의 것이다 — 프리뷰가 자기 자를 가지면 「초록인데 거절」 |
| 194 | `SetPlacementGhostCells/2` | PlacementService |  |
| 195 | `IsPlacementRangeCell/1` | PlacementService |  |
| 196 | `ClearPlacementGhostCells/0` | PlacementService |  |
| 197 | `CanPlaceDefenderAt/4` | PlacementService |  |
| 198 | `DeployedCountOf/1` | PlacementService.OnBoard |  |
| 199 | `TryGetDeployedEntity/2` | 삭제 (코어 스폰 = BattleWorld.Spawn*) |  |
| 200 | `TryQueueDeployedDefenderMaxHealthDamage/2` | 삭제 | 첫 판 튜토리얼의 저체력 연출 훅. 튜토리얼 콘텐츠는 2026-09 에 전량 제거됐고(76038c26) 새 코어에 자리가 없다 |
| 201 | `CloseCellLayers/1` | MapRuntime (코어) |  |
| 202 | `ShowPlacementHighlight/2` | PlacementService |  |
| 203 | `HidePlacementHighlight/0` | PlacementService |  |
| 204 | `AnyEnemyWithinTilesOfGoal/1` | HeartMeter |  |
| 205 | `NearestGoalDistance/1` | HeartMeter |  |
| 206 | `ShowBlockedHighlight/1` | CoreMapOverlay.ShowPlacement | 고스트가 빨강으로 답한다 |
| 207 | `HideBlockedHighlight/0` | CoreMapOverlay.HidePlacement |  |
| 208 | `RefreshPlacementHighlightIfShown/0` | PlacementService |  |
| 209 | `RepaintPlacementHighlight/0` | PlacementService |  |
| 210 | `PlaceDefenderAs/3` | 삭제 | 유닛을 인자로 받던 두 번째 진입 — 커맨드 하나로 접힌다 |
| 211 | `TryBeginDefenderDeployment/4` | PlacementService | 안의 배치 보이스는 `CoreBattleAudio`(`Placed`) |
| 212 | `LandDeployedDefender/1` | 커맨드 `LandDefender` (DragPlacementInput) | 비행은 프레젠테이션 시간이라 코어가 길이를 모른다 |
| 213 | `ActivateDeployedDefender/2` | PlacementService.StepActivation |  |
| 214 | `OnDefenderActivated/1` | `DefenderActivated` 사건 구독 | 뷰 풀이 배치 모션을 재생한다 |
| 215 | `DrainDefenderActivatedEvents/0` | 뷰 풀 / 담당자 구독 (이벤트로 접힘) |  |
| 216 | `TriggerDeploymentOnPlaceSkill/2` | BindingRegistry / TriggerDispatcher |  |
| 217 | `ApplyEnvironmentGating/0` | 미정 |  |
| 218 | `SetPlacementHover/2` | PlacementService |  |
| 219 | `SetPlacementHover/3` | PlacementService |  |
| 220 | `PulsePlacementHover/2` | PlacementService |  |
| 221 | `PulsePlacementHover/3` | PlacementService |  |
| 222 | `SetPlacementStretch/4` | PlacementService |  |
| 223 | `ClearPlacementStretch/0` | PlacementService |  |
| 224 | `ClearPlacementHover/1` | PlacementService |  |
| 225 | `ClearPlacementHover/0` | PlacementService |  |
| 226 | `SetRangeOwner/1` | CoreMapOverlay.ShowPlacement | 링의 주인 = 지금 끌고 있는 유닛 |
| 227 | `BakeAttackShape/2` | 삭제 (코어 스폰 = BattleWorld.Spawn*) |  |
| 228 | `SetPlacementRange/2` | PlacementService |  |
| 229 | `RefreshRangeTargetMarks/3` | CoreMapOverlay.PaintRange | 표식 판정은 `AttackReach.InReach` **호출만**(제약 13) |
| 230 | `ClearPlacementRange/0` | PlacementService |  |
| 231 | `SetSkillAimRange/2` | BindingRegistry / TriggerDispatcher |  |
| 232 | `ClearSkillAimRange/0` | BindingRegistry / TriggerDispatcher |  |
| 233 | `TryGetTileScreenCenter/3` | MapRuntime (코어) |  |
| 234 | `SetSkillAimCells/1` | MapRuntime (코어) |  |
| 235 | `PinSkillTelegraph/2` | BindingRegistry / TriggerDispatcher |  |
| 236 | `CenteredRingRadius/1` | CoreMapOverlay.PaintRange | 반지름 = `사거리 + 내 몸`. **판정이 아니라 그 판정을 그리는 치수**다 |
| 237 | `PinCenteredRange/3` | CoreMapOverlay.PaintRange |  |
| 238 | `ClearSkillTelegraph/0` | BindingRegistry / TriggerDispatcher |  |
| 239 | `ClearRange/1` | CoreMapOverlay.HidePlacement |  |
| 240 | `SetAttachPreview/3` | HandDeck |  |
| 241 | `CanDrawAttachPreviewFor/1` | HandDeck |  |
| 242 | `ClearAttachPreview/0` | HandDeck |  |
| 243 | `RedrawAttachPreview/0` | HandDeck |  |
| 244 | `SetPlacementRangeValidity/1` | PlacementService |  |
| 245 | `FlashPlacementReject/1` | PlacementService |  |
| 246 | `PlayDeploymentPresentation/3` | CoreUnitViewPool (`DefenderActivated` → PlayDeploy) | 모션만 옮겼다. 컷신 프레임은 저작 자산이고 그 소비처는 unit 6 의 VFX 풀 |
| 247 | `PlayFallbackDeploymentPulse/3` | unit 6 VFX 풀 | 배치 펄스는 VFX 사건이 열리는 unit 6 의 것이다 |
| 248 | `PlayDeploymentRingPulse/2` | unit 6 VFX 풀 | 같은 이유 |
| 249 | `CreateDefenderEntity/3` | 삭제 (코어 스폰 = BattleWorld.Spawn*) |  |
| 250 | `CreatePatrolEntity/5` | 삭제 (코어 스폰 = BattleWorld.Spawn*) |  |
| 251 | `TryGetPatrolHomeCell/4` | MapRuntime (코어) |  |
| 252 | `DebugSpawnPatrolAt/3` | 디버그/로그 (도구 처분표) |  |
| 253 | `DebugTryGetPatrolAnchorCell/2` | MapRuntime (코어) |  |
| 254 | `RegisterPatrolUnitSO/1` | 미정 |  |
| 255 | `DrainPatrolSpawnRequests/0` | 뷰 풀 / 담당자 구독 (이벤트로 접힘) |  |
| 256 | `AddEffectTile/2` | PlacementService | unit 6b 정정 — 칸 목록의 주인은 **뽑고 소비하는 자**다(`Begin` 이 칸·종류를 함께 뽑는다). 맵은 그 칸을 모른다 |
| 257 | `ApplyEffectTileIfAny/2` | PlacementService | unit 6b 정정 — `ApplyArmedTile`(활성화 엣지 · 저작 연산자 그대로 · 칸 `SlotKind.Tile`) |
| 258 | `ApplyEffectTileOnce/2` | PlacementService | unit 6b 정정 — 1회 가드는 `ConsumeEffectTile`(unit 4), 회수는 `RevokeTile`(퇴근 · F33). 배치 스킬 표식과 비공유(F19) |
| 259 | `FireOnPlaceCameraShake/1` | CameraDirector.Shake | 호출부는 unit 6(배치 VFX) — 세기는 유닛 저작값이라 5b 가 지어낼 수 없다 |
| 260 | `MarkJustDeployedForRules/1` | `DefenderActivated` 사건 | 표식 컴포넌트를 남기지 않는다 — 남으면 다음 배치 사건과 섞인다(E6) |
| 261 | `DebugSpawnObstacleAt/2` | 디버그/로그 (도구 처분표) |  |
| 262 | `SpawnHazardWithVisual/3` | 뷰 풀 |  |
| 263 | `DebugSpawnHazardAt/2` | 디버그/로그 (도구 처분표) |  |
| 264 | `SpawnBlockingHazardWithVisual/2` | 뷰 풀 |  |
| 265 | `DebugSpawnBlockingHazardAt/2` | 디버그/로그 (도구 처분표) |  |
| 266 | `DebugLogFatigueStacks/0` | 디버그/로그 (도구 처분표) |  |
| 267 | `DebugLogPickups/0` | GimmickHost |  |
| 268 | `RegisterBlockingHazardSO/1` | 삭제 (BoardEffectDefinitionBuilder.ToBlockingHazardDefs) | 런타임 등록부가 **판 밖 정의표**(`MatchDefinition.BlockingHazards`)로 바뀐다. 탄→길막 참조는 줄의 역참조(`SpawnedByProjectile`) |
| 269 | `RegisterZoneHazardSO/1` | 삭제 (BoardEffectDefinitionBuilder.ToHazardDefs) | 같은 이유 — `MatchDefinition.Hazards` 줄 번호가 곧 참조다 |
| 270 | `EnsureBlockingHazardVisualRoot/0` | 뷰 풀 |  |
| 271 | `ClearBlockingHazardVisuals/0` | 뷰 풀 |  |
| 272 | `RecordHazardSpawn/2` | 삭제 (`HazardSpawned` 사건 · CoreHarness 트레이스 구독) | 기록은 사건 구독이다(계약 7). 채널 42 |
| 273 | `DrainHazardRuntimeEvents/0` | 뷰 풀 / 담당자 구독 (이벤트로 접힘) |  |
| 274 | `DrainHazardSpawnRequests/0` | 뷰 풀 / 담당자 구독 (이벤트로 접힘) |  |
| 275 | `SyncBlockingHazardOverheadGauges/1` | 뷰 풀 |  |
| 276 | `DrainHazardDestroyedEvents/0` | 뷰 풀 / 담당자 구독 (이벤트로 접힘) |  |
| 277 | `DrainGoalCollapsedEvents/0` | 뷰 풀 / 담당자 구독 (이벤트로 접힘) |  |
| 278 | `SyncGoalOverheadGauges/1` | HeartMeter |  |
| 279 | `RecordBlockingHazard/4` | 삭제 (`UnitSpawned` 사건 — `UnitKind.BlockingHazard`) | 길막은 유닛이라 스폰 사건이 이미 있다 |
| 280 | `RecordBlockingHazardDestroyed/2` | 삭제 (`UnitDestroyed` 사건 — `UnitKind.BlockingHazard`) | 같은 이유 — 문은 「부서짐」 하나 |
| 281 | `WorldToLogCell/1` | MapRuntime (코어) |  |
| 282 | `BlockingHazardLogSide/1` | 디버그/로그 (도구 처분표) |  |
| 283 | `BuildStackThresholdRegistry/0` | 삭제 (MatchDefinitionBuilder.ToStackRuleDefs) | 전역 사전(`StackKind` → 규칙)이 **자산당 한 줄**인 정의표로 바뀐다(F31). 등록 시점도 판 밖이다 |
| 284 | `CreateGimmickConfigIfActive/0` | GimmickHost |  |
| 285 | `GetStackThresholds/1` | 삭제 (StackRules.Resolve) | 종류로 전역 한 벌을 찾던 조회가 **줄 번호 해석**으로 바뀐다 — 미지정이면 그 종류의 첫 줄 |
| 286 | `ShapeToHazardVisualScale/3` | 뷰 풀 |  |
| 287 | `DebugSpawnObstacleContext/0` | 디버그/로그 (도구 처분표) |  |
| 288 | `LogPlacementReject/3` | PlacementService |  |
| 289 | `OnDestroy/0` | 미정 |  |
| 290 | `EnsureMonoViewPools/0` | 뷰 풀 |  |
| 291 | `CreateViewPool/1` | 삭제 (코어 스폰 = BattleWorld.Spawn*) |  |
| 292 | `ResolveUnitMaterial/2` | CoreUnitViewPool | 쿼드 폴백 머티리얼. `RuntimeMaterialFactory` 경유로 바뀌었다 |
| 293 | `InstallSkillLayer/0` | BindingRegistry / TriggerDispatcher |  |
| 294 | `RunImmediateSkills/0` | BindingRegistry / TriggerDispatcher |  |
| 295 | `RoutingProbe/2` | 미정 |  |
| 296 | `BakeNightmareMechanics/2` | 삭제 (코어 스폰 = BattleWorld.Spawn*) |  |
| 297 | `BakeUnitMechanics/6` | 삭제 (코어 스폰 = BattleWorld.Spawn*) |  |
| 298 | `BakeDefenderDirectionalPattern/3` | 삭제 (코어 스폰 = BattleWorld.Spawn*) |  |
| 299 | `TryBuildPatternSlot/5` | 미정 |  |
| 300 | `BuildPatternTemplate/4` | 미정 |  |
| 301 | `SpawnUnit/1` | WaveScheduler |  |
| 302 | `CreateEnemyEntity/4` | 삭제 (코어 스폰 = BattleWorld.Spawn*) |  |
| 303 | `ConeCosSq/1` | 미정 |  |
| 304 | `SpawnSplitChildren/2` | 미정 |  |
| 305 | `CreateAttackUnitRuntimeMaterial/1` | 삭제 (코어 스폰 = BattleWorld.Spawn*) |  |
