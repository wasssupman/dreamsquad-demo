# 장부 — 퇴역 집합 (unit 8c · retire-set.md)

> 생성 2026-09-25(8c). **unit 9 는 아래 `retire` 블록의 목록만 지운다** — 더 지우지도, 덜 지우지도 않는다. 목록은 손으로 고른 것이 아니라 **검사의 결과**다:
> 퇴역 후보를 지운 export 사본이 컴파일되고(코드 도달성) · 남는 뿌리에서 퇴역 스크립트에 닿지 않고(자산 도달성) · 남는 코드가 옛 경로를 문자열로 들지 않을 때(`--retire-assets`)만 이 목록이 성립한다. 목록을 고치면 아래 세 명령을 다시 돌린다.

총계: 퇴역 590 파일 · C# 574 파일 · 113228 줄

## 검증 명령

```
# ① 코드 도달성 — 퇴역 목록을 지운 트리가 컴파일되나(컴파일러에게 묻는다)
git -C /Users/sy/dev/wassup-core archive <sha> Assets/_Project/Scripts Assets/_Project/Tests Assets/_Project/Data \
    Assets/_Project/Editor Assets/_Project/Shaders Assets/Resources tools/battle-core-rebuild | tar -x -C <scratch>
python3 tools/battle-core-rebuild/check_ledgers.py --retire-prune <scratch>              # 워크트리의 스크립트 · 목록으로 사본을 지운다
dotnet build <scratch>/tools/battle-core-rebuild/headless/Retire.Check.csproj        # 오류 0
# ② 자산 도달성 + 옛 경로 문자열 + 이 파일의 총계 줄
python3 tools/battle-core-rebuild/check_ledgers.py --retire-assets                    # exit 0
```

⚠ `*.csproj` 는 `.gitignore` 대상이라 `Retire.Check.csproj` 는 강제 추가돼 있다. export 에는 들어간다.
⚠ `--retire-prune` 은 **`hold` 블록도** 지운다 — 보류 항목도 옛 브리지를 부르므로 지워야 컴파일 증명이 성립한다. 보류 항목이 남으려면 새 씬으로 옮겨야 하고(= 옛 브리지 의존 제거), 그 결정이 아래 「보류」다. 컴파일 증명은 「보류를 지워도 남는 코드가 그것을 부르지 않는다」까지 말한다 — 둘 다 잎(leaf)이다.

## 퇴역 목록

경로는 저장소 루트 기준. `/` 로 끝나면 폴더째. `.meta` 는 짝으로 같이 지운다.

```retire
# 1. 옛 폴더 — 옛 전투 본체(ECS 시스템 · 브리지 · 옛 하네스 도구 — tools 1~5 는 코어판 완료, 4 `SimOrderDumpMenu` 은퇴)
Assets/_Project/Editor/Battle/
Assets/_Project/Scripts/Battle/
Assets/_Project/Scripts/Bridge/

# 2. 규칙 보유자 10(`rule-holders.md` — `TimeManager` 는 추가 제약의 의도된 예외라 남는다)
Assets/_Project/Scripts/Core/CostRuntime.cs
Assets/_Project/Scripts/Core/DraftController.cs
Assets/_Project/Scripts/Core/Dreamcatcher/DreamcatcherHandController.cs
Assets/_Project/Scripts/Core/GameManager.cs
Assets/_Project/Scripts/Core/MatchTally.cs
Assets/_Project/Scripts/Core/PlacementCooldownRuntime.cs
Assets/_Project/Scripts/Core/PlacementInput.cs
Assets/_Project/Scripts/Core/SkillLoadoutController.cs
Assets/_Project/Scripts/Core/SkillRuntime.cs
Assets/_Project/Scripts/Core/TilemapMapView.cs

# 3. 옛 뷰 · 옛 UI — 옛 씬(`BattleScene.unity`)에만 붙어 있고 새 층은 `Core*` 후계를 쓴다. `ReachDebugGizmos` 는 은퇴(에이전트 판정 — 8c 구현 6)
Assets/_Project/Scripts/Presentation/DcIconStripSpawner.cs
Assets/_Project/Scripts/Presentation/ProjectileViewPool.cs
Assets/_Project/Scripts/Presentation/QuadUnitView.cs
Assets/_Project/Scripts/Presentation/QuadUnitViewPool.cs
Assets/_Project/Scripts/Presentation/ReachDebugGizmos.cs
Assets/_Project/Scripts/Presentation/SpawnAlertPresenter.cs
Assets/_Project/Scripts/Presentation/SpineUnitPool.cs
Assets/_Project/Scripts/Presentation/SpineUnitView.cs
Assets/_Project/Scripts/Presentation/SpriteUnitView.cs
Assets/_Project/Scripts/Presentation/UnitLiftVisual.cs
Assets/_Project/Scripts/Presentation/UnitOverheadUiLayer.cs
Assets/_Project/Scripts/UI/BossWarningView.cs
Assets/_Project/Scripts/UI/CostDisplay.cs
Assets/_Project/Scripts/UI/DefenderDragPlacementController.cs
Assets/_Project/Scripts/UI/DefenderDragSlot.cs
Assets/_Project/Scripts/UI/DefenderRelocationController.cs
Assets/_Project/Scripts/UI/DefenderRetireFlight.cs
Assets/_Project/Scripts/UI/DefenderSelector.cs
Assets/_Project/Scripts/UI/Draft/DraftView.cs
Assets/_Project/Scripts/UI/Dreamcatcher/AwakeningGaugeView.cs
Assets/_Project/Scripts/UI/Dreamcatcher/DcInspectController.cs
Assets/_Project/Scripts/UI/Dreamcatcher/DreamcatcherCardDragSlot.cs
Assets/_Project/Scripts/UI/Dreamcatcher/DreamcatcherFluidBackdrop.cs
Assets/_Project/Scripts/UI/Dreamcatcher/DreamcatcherFocusPresenter.cs
Assets/_Project/Scripts/UI/Dreamcatcher/DreamcatcherHandView.cs
Assets/_Project/Scripts/UI/GimmickPhaseView.cs
Assets/_Project/Scripts/UI/NextWaveDock.cs
Assets/_Project/Scripts/UI/Outgame/ReturnToMenuButton.cs
Assets/_Project/Scripts/UI/Outgame/SquadPrepView.cs
Assets/_Project/Scripts/UI/PlacementPhaseView.cs
Assets/_Project/Scripts/UI/ScoreHudView.cs
Assets/_Project/Scripts/UI/Tutorial/FirstRunTutorialController.cs

# 4. 옛 씬 전용 입력(8c 가 떼어 둔 `*.OldBattle.cs` 부분 파일)과 잔여 이중화(옛 웨이브 생성기 · 보너스 스케줄 · 옛 판 설정 SO + 자산)
Assets/_Project/Data/Config/BattleConfig.asset
Assets/_Project/Scripts/Audio/SoundManager.OldBattle.cs
Assets/_Project/Scripts/Data/BattleConfig.cs
Assets/_Project/Scripts/Data/BonusWaveSchedule.cs
Assets/_Project/Scripts/Data/WavePatternGenerator.cs
Assets/_Project/Scripts/Presentation/CameraDirector.OldBattle.cs
Assets/_Project/Scripts/UI/Draft/WavePatternStripView.OldBattle.cs
Assets/_Project/Scripts/UI/ResultScreen.OldBattle.cs

# 5. 옛 테스트 — `Wassup.Tests.EditMode`(옛 ECS 시스템 · 옛 타입 · 옛 소스 텍스트 · 옛 코퍼스). 짝은 unit 9 구현 2 가 파일마다 적는다
Assets/_Project/Tests/EditMode/AgentCollisionTests.cs
Assets/_Project/Tests/EditMode/AggroAoeWidthTests.cs
Assets/_Project/Tests/EditMode/AggroChaseFreezeTests.cs
Assets/_Project/Tests/EditMode/AggroChaseMathTests.cs
Assets/_Project/Tests/EditMode/AggroPolicyTests.cs
Assets/_Project/Tests/EditMode/AggroStateSystemTests.cs
Assets/_Project/Tests/EditMode/AoeTargetCapTests.cs
Assets/_Project/Tests/EditMode/AttackCommitTests.cs
Assets/_Project/Tests/EditMode/AttackReachTests.cs
Assets/_Project/Tests/EditMode/AttackShapeGateTests.cs
Assets/_Project/Tests/EditMode/AttackShapeSelectionTests.cs
Assets/_Project/Tests/EditMode/AttackSystemMaskTests.cs
Assets/_Project/Tests/EditMode/AttackSystemStateGateTests.cs
Assets/_Project/Tests/EditMode/AttackSystemUnifiedLoopTests.cs
Assets/_Project/Tests/EditMode/AuraPulseTests.cs
Assets/_Project/Tests/EditMode/BallisticArcTests.cs
Assets/_Project/Tests/EditMode/BarrelExplosionTests.cs
Assets/_Project/Tests/EditMode/BattleBridgeDraftMapTests.cs
Assets/_Project/Tests/EditMode/BattleScaledRateManagerTests.cs
Assets/_Project/Tests/EditMode/Bezier3Tests.cs
Assets/_Project/Tests/EditMode/BlinkMathTests.cs
Assets/_Project/Tests/EditMode/BoardSpaceTests.cs
Assets/_Project/Tests/EditMode/BonusWaveScheduleTests.cs
Assets/_Project/Tests/EditMode/BoomerangBakeAndDrainTests.cs
Assets/_Project/Tests/EditMode/BoomerangTests.cs
Assets/_Project/Tests/EditMode/BossCcImmunityTests.cs
Assets/_Project/Tests/EditMode/BounceRetargetTests.cs
Assets/_Project/Tests/EditMode/CcActionLockTests.cs
Assets/_Project/Tests/EditMode/CcApplySystemTests.cs
Assets/_Project/Tests/EditMode/CcDecaySystemTests.cs
Assets/_Project/Tests/EditMode/CellLayersInstallTests.cs
Assets/_Project/Tests/EditMode/CostRuntimeTests.cs
Assets/_Project/Tests/EditMode/CostWellMathTests.cs
Assets/_Project/Tests/EditMode/DeadCasterFactionTests.cs
Assets/_Project/Tests/EditMode/DefenderAiStateSystemTests.cs
Assets/_Project/Tests/EditMode/DefenderBoardLimitTests.cs
Assets/_Project/Tests/EditMode/DefenderDensityTests.cs
Assets/_Project/Tests/EditMode/DefenderHunterGateTests.cs
Assets/_Project/Tests/EditMode/DefenderLockTests.cs
Assets/_Project/Tests/EditMode/DeploymentActivationSystemTests.cs
Assets/_Project/Tests/EditMode/DetectionChaseFieldTests.cs
Assets/_Project/Tests/EditMode/DetectionLeakProofTests.cs
Assets/_Project/Tests/EditMode/DetectionSystemTests.cs
Assets/_Project/Tests/EditMode/DistContractTests.cs
Assets/_Project/Tests/EditMode/DotApplySystemTests.cs
Assets/_Project/Tests/EditMode/DotEffectMergeTests.cs
Assets/_Project/Tests/EditMode/DotTickTests.cs
Assets/_Project/Tests/EditMode/EffectIntegrationTests.cs
Assets/_Project/Tests/EditMode/EffectTickSystemTests.cs
Assets/_Project/Tests/EditMode/EffectTileModifierTests.cs
Assets/_Project/Tests/EditMode/EmitterTickTests.cs
Assets/_Project/Tests/EditMode/EnemyAiStateSystemTests.cs
Assets/_Project/Tests/EditMode/EnemyBehaviorTests.cs
Assets/_Project/Tests/EditMode/EnemyTargetPriorityTests.cs
Assets/_Project/Tests/EditMode/EnemyTierBakeTests.cs
Assets/_Project/Tests/EditMode/FillWalkMaskTests.cs
Assets/_Project/Tests/EditMode/FlowFieldBuilderTests.cs
Assets/_Project/Tests/EditMode/FlowFieldRebuildTests.cs
Assets/_Project/Tests/EditMode/FlowFieldSingletonTests.cs
Assets/_Project/Tests/EditMode/FlowRecoveryTests.cs
Assets/_Project/Tests/EditMode/FootprintPlacementCheckTests.cs
Assets/_Project/Tests/EditMode/FrenzyStackingTests.cs
Assets/_Project/Tests/EditMode/FrontmostAttackLockTests.cs
Assets/_Project/Tests/EditMode/FrontmostTargetingTests.cs
Assets/_Project/Tests/EditMode/GameManagerMatchCountTests.cs
Assets/_Project/Tests/EditMode/GoalProjectileTests.cs
Assets/_Project/Tests/EditMode/GoalTargetingPriorityTests.cs
Assets/_Project/Tests/EditMode/GoalTauntGrantTests.cs
Assets/_Project/Tests/EditMode/GoalTowerArchetypeTests.cs
Assets/_Project/Tests/EditMode/GridMathTests.cs
Assets/_Project/Tests/EditMode/HandViewSelectionSignalTests.cs
Assets/_Project/Tests/EditMode/HazardCasterTests.cs
Assets/_Project/Tests/EditMode/HazardDestroyedEventTests.cs
Assets/_Project/Tests/EditMode/HazardShapeSamplerTests.cs
Assets/_Project/Tests/EditMode/HealAppliedEventTests.cs
Assets/_Project/Tests/EditMode/HealthRatioTests.cs
Assets/_Project/Tests/EditMode/HealthScaleMaxTests.cs
Assets/_Project/Tests/EditMode/HeatMathTests.cs
Assets/_Project/Tests/EditMode/HuntCloseInLockTests.cs
Assets/_Project/Tests/EditMode/KillAttributionTests.cs
Assets/_Project/Tests/EditMode/LegacyTraceV0Tests.cs
Assets/_Project/Tests/EditMode/LowestHealthTargetingTests.cs
Assets/_Project/Tests/EditMode/MatchTallyTests.cs
Assets/_Project/Tests/EditMode/ModifierAuraClassifierTests.cs
Assets/_Project/Tests/EditMode/ModifierAuthoringTests.cs
Assets/_Project/Tests/EditMode/ModifierFrameworkTests.cs
Assets/_Project/Tests/EditMode/ModifierMathTests.cs
Assets/_Project/Tests/EditMode/MovementCellTrimApplyTests.cs
Assets/_Project/Tests/EditMode/MovementCellTrimTests.cs
Assets/_Project/Tests/EditMode/MovementCompositionTests.cs
Assets/_Project/Tests/EditMode/MovementImpulseAcrossStatesTests.cs
Assets/_Project/Tests/EditMode/MovementSystemTests.cs
Assets/_Project/Tests/EditMode/NavGridTests.cs
Assets/_Project/Tests/EditMode/NearestLockTests.cs
Assets/_Project/Tests/EditMode/NearestTargetingTests.cs
Assets/_Project/Tests/EditMode/ObstacleLifetimeTests.cs
Assets/_Project/Tests/EditMode/ObstacleSignatureTests.cs
Assets/_Project/Tests/EditMode/OrbitTests.cs
Assets/_Project/Tests/EditMode/PathHitRehitCooldownTests.cs
Assets/_Project/Tests/EditMode/PathSmoothingTests.cs
Assets/_Project/Tests/EditMode/PatrolAreaMathTests.cs
Assets/_Project/Tests/EditMode/PatrolLayerRoutingTests.cs
Assets/_Project/Tests/EditMode/PatrolSystemIntegrationTests.cs
Assets/_Project/Tests/EditMode/PatternBakeTests.cs
Assets/_Project/Tests/EditMode/PatternDirectionTests.cs
Assets/_Project/Tests/EditMode/PatternScopeTests.cs
Assets/_Project/Tests/EditMode/PatternTargetingTests.cs
Assets/_Project/Tests/EditMode/PlacementCooldownRuntimeTests.cs
Assets/_Project/Tests/EditMode/PlacementLayerTests.cs
Assets/_Project/Tests/EditMode/PlacementMaskLivePathTests.cs
Assets/_Project/Tests/EditMode/ProjectileEmitterIntegrationTests.cs
Assets/_Project/Tests/EditMode/ProjectileOriginRadiusCarryTests.cs
Assets/_Project/Tests/EditMode/ProjectileRetargetAndBounceTests.cs
Assets/_Project/Tests/EditMode/ProjectileSystemTests.cs
Assets/_Project/Tests/EditMode/ProjectileVariationTests.cs
Assets/_Project/Tests/EditMode/RangeDisplayContractTests.cs
Assets/_Project/Tests/EditMode/RangePredicateInvariantsTests.cs
Assets/_Project/Tests/EditMode/RelocationCheckTests.cs
Assets/_Project/Tests/EditMode/SeparationTests.cs
Assets/_Project/Tests/EditMode/ShieldMathTests.cs
Assets/_Project/Tests/EditMode/SkillAdapterDirectWriteTests.cs
Assets/_Project/Tests/EditMode/SkillEntityIdPinTests.cs
Assets/_Project/Tests/EditMode/SkillLayerEndpointTests.cs
Assets/_Project/Tests/EditMode/SkillLoadoutControllerTests.cs
Assets/_Project/Tests/EditMode/SkillMathParityTests.cs
Assets/_Project/Tests/EditMode/SkillModifierKindPinTests.cs
Assets/_Project/Tests/EditMode/SkillRoutingCoverageTests.cs
Assets/_Project/Tests/EditMode/SkyFallTests.cs
Assets/_Project/Tests/EditMode/SpatialPlacementCheckTests.cs
Assets/_Project/Tests/EditMode/SpawnAlertForecastTests.cs
Assets/_Project/Tests/EditMode/SpawnBlockingHazardTests.cs
Assets/_Project/Tests/EditMode/SpawnSpreadTests.cs
Assets/_Project/Tests/EditMode/StackingModifierMergeTests.cs
Assets/_Project/Tests/EditMode/StagePoolDevEntriesTests.cs
Assets/_Project/Tests/EditMode/StructureDestinationTests.cs
Assets/_Project/Tests/EditMode/StructureFixtures.cs
Assets/_Project/Tests/EditMode/SweepHitMathTests.cs
Assets/_Project/Tests/EditMode/TargetPersistenceTests.cs
Assets/_Project/Tests/EditMode/ThreatTableTests.cs
Assets/_Project/Tests/EditMode/TileAoeTests.cs
Assets/_Project/Tests/EditMode/TileRangeTests.cs
Assets/_Project/Tests/EditMode/TilemapMapViewTests.cs
Assets/_Project/Tests/EditMode/TutorialDragGuidanceTests.cs
Assets/_Project/Tests/EditMode/TutorialGuidanceCopyTests.cs
Assets/_Project/Tests/EditMode/UnitLifecycleSystemTests.cs
Assets/_Project/Tests/EditMode/WaveConceptBossTests.cs
Assets/_Project/Tests/EditMode/WaveConceptGenerationTests.cs
Assets/_Project/Tests/EditMode/WaveConceptMathTests.cs
Assets/_Project/Tests/EditMode/WaveConceptVariantTests.cs
Assets/_Project/Tests/EditMode/WaveCountRampTests.cs
Assets/_Project/Tests/EditMode/WaveEligibilityGateTests.cs
Assets/_Project/Tests/EditMode/WaveForceRescheduleTests.cs
Assets/_Project/Tests/EditMode/WaveGroupsMatchSpawnTests.cs
Assets/_Project/Tests/EditMode/WaveNominalIntervalTests.cs
Assets/_Project/Tests/EditMode/WavePatternGeneratorBossTests.cs
Assets/_Project/Tests/EditMode/WavePatternGeneratorTests.cs
Assets/_Project/Tests/EditMode/WavePerTypeCapTests.cs
Assets/_Project/Tests/EditMode/WaveSpawnForecastTests.cs
Assets/_Project/Tests/EditMode/WaypointFlowFieldSlotTests.cs
Assets/_Project/Tests/EditMode/WaypointProgressTests.cs
Assets/_Project/Tests/EditMode/WhirlpotEngageRepro.cs
Assets/_Project/Tests/EditMode/ZoneApplyFactionGateTests.cs

# 6. 옛 테스트 — `Wassup.Tests.EditMode.Assets`(옛 ECS · 옛 생성기 · A/B 비교 `AttackReachParityTests` 는 짝 없이 은퇴)
Assets/_Project/Tests/EditModeAssets/AttackReachParityTests.cs
Assets/_Project/Tests/EditModeAssets/AuthoredTargetMaskTests.cs
Assets/_Project/Tests/EditModeAssets/DirectionalVolleyIntegrationTests.cs
Assets/_Project/Tests/EditModeAssets/FirstRunTutorialWavePlanTests.cs
Assets/_Project/Tests/EditModeAssets/WaveConceptAuthoringTests.cs
Assets/_Project/Tests/EditModeAssets/WaveKillBudgetPinTests.cs
Assets/_Project/Tests/EditModeAssets/WaveSpawnLeadInTests.cs

# 7. 옛 테스트 — `Wassup.Tests.PlayMode`(옛 씬을 부팅한다 · `LegacyBattleScene`). 남는 것 = 아웃게임 3(`AuthE2ETest`·`DeckInfoPresetApplyLiveE2ETest`·`PresetBarPopupLayerTest`)
Assets/_Project/Tests/PlayMode/AbilityAreaShieldTest.cs
Assets/_Project/Tests/PlayMode/AbilityBombManBarrelTest.cs
Assets/_Project/Tests/PlayMode/AbilityOnPlaceBlastTest.cs
Assets/_Project/Tests/PlayMode/ActionLockTest.cs
Assets/_Project/Tests/PlayMode/ActiveAllyZoneTest.cs
Assets/_Project/Tests/PlayMode/ActiveMeteorTest.cs
Assets/_Project/Tests/PlayMode/ActiveSlowFieldTest.cs
Assets/_Project/Tests/PlayMode/ActiveTileCastTest.cs
Assets/_Project/Tests/PlayMode/ActiveTornadoTest.cs
Assets/_Project/Tests/PlayMode/AttachRangePreviewTest.cs
Assets/_Project/Tests/PlayMode/BattleBridgeTestAccess.cs
Assets/_Project/Tests/PlayMode/BeamPresentationTest.cs
Assets/_Project/Tests/PlayMode/BoardLimitPlacementTest.cs
Assets/_Project/Tests/PlayMode/BoardLimitTrayStateTest.cs
Assets/_Project/Tests/PlayMode/BonusWavePullTest.cs
Assets/_Project/Tests/PlayMode/BossLullabyTest.cs
Assets/_Project/Tests/PlayMode/BossSelfBlinkTest.cs
Assets/_Project/Tests/PlayMode/BossShieldTest.cs
Assets/_Project/Tests/PlayMode/BossThresholdSelfAoeTest.cs
Assets/_Project/Tests/PlayMode/BossUltimateLeapTest.cs
Assets/_Project/Tests/PlayMode/BossWhipAuraTest.cs
Assets/_Project/Tests/PlayMode/BountyMarkTest.cs
Assets/_Project/Tests/PlayMode/DefenderApplyStackOutputTest.cs
Assets/_Project/Tests/PlayMode/DefenderRetireTest.cs
Assets/_Project/Tests/PlayMode/DioramaStagePlayTests.cs
Assets/_Project/Tests/PlayMode/DotAuraFromElementTest.cs
Assets/_Project/Tests/PlayMode/DotCoexistenceTest.cs
Assets/_Project/Tests/PlayMode/DraftFlowSmokeTest.cs
Assets/_Project/Tests/PlayMode/DragCancelZoneTest.cs
Assets/_Project/Tests/PlayMode/DragPlacementReachTest.cs
Assets/_Project/Tests/PlayMode/DragonBreathE2ETest.cs
Assets/_Project/Tests/PlayMode/DreamCocoonTest.cs
Assets/_Project/Tests/PlayMode/DreamcatcherAttachRequirementE2ETest.cs
Assets/_Project/Tests/PlayMode/DreamcatcherCombatDamageTest.cs
Assets/_Project/Tests/PlayMode/DreamcatcherCursedRelicTest.cs
Assets/_Project/Tests/PlayMode/DreamcatcherDamagedTriggerTest.cs
Assets/_Project/Tests/PlayMode/DreamcatcherEffectTest.cs
Assets/_Project/Tests/PlayMode/DreamcatcherGateE2ETest.cs
Assets/_Project/Tests/PlayMode/DreamcatcherKillThresholdTest.cs
Assets/_Project/Tests/PlayMode/DreamcatcherOnHitTest.cs
Assets/_Project/Tests/PlayMode/DreamcatcherSleepDamageTest.cs
Assets/_Project/Tests/PlayMode/DropDismountTest.cs
Assets/_Project/Tests/PlayMode/EffectTileBuffApplyTest.cs
Assets/_Project/Tests/PlayMode/EnemyShieldTest.cs
Assets/_Project/Tests/PlayMode/GoalStabilityTest.cs
Assets/_Project/Tests/PlayMode/HitscanDefenderTest.cs
Assets/_Project/Tests/PlayMode/IncubusPactTest.cs
Assets/_Project/Tests/PlayMode/KindlerFireStackE2ETest.cs
Assets/_Project/Tests/PlayMode/KnockupOnHitTest.cs
Assets/_Project/Tests/PlayMode/LegacyBattleScene.cs
Assets/_Project/Tests/PlayMode/MovementIntegritySmokeTest.cs
Assets/_Project/Tests/PlayMode/OnPlaceApplyStackNearbyTest.cs
Assets/_Project/Tests/PlayMode/OnPlaceBindNearbyTest.cs
Assets/_Project/Tests/PlayMode/OnPlaceBoostNearbyTest.cs
Assets/_Project/Tests/PlayMode/OnPlaceDotNearbyTest.cs
Assets/_Project/Tests/PlayMode/OnPlaceForwardProjectileTest.cs
Assets/_Project/Tests/PlayMode/OnPlaceGainCostTest.cs
Assets/_Project/Tests/PlayMode/OnPlaceMeleeBurstTest.cs
Assets/_Project/Tests/PlayMode/OnPlaceReduceSkillCooldownTest.cs
Assets/_Project/Tests/PlayMode/OnPlaceRuleTriggerTest.cs
Assets/_Project/Tests/PlayMode/OnPlaceSkyStrikeTest.cs
Assets/_Project/Tests/PlayMode/OnPlaceStunNearbyTest.cs
Assets/_Project/Tests/PlayMode/OnPlaceTauntNearbyTest.cs
Assets/_Project/Tests/PlayMode/PatrolDefenderPlayTest.cs
Assets/_Project/Tests/PlayMode/PlacementAuraTest.cs
Assets/_Project/Tests/PlayMode/ProjectileApplyStackAccumulatesTest.cs
Assets/_Project/Tests/PlayMode/ProjectileVisualSmokeTest.cs
Assets/_Project/Tests/PlayMode/RangePredicateMirrorTest.cs
Assets/_Project/Tests/PlayMode/RelocationMoveModeTest.cs
Assets/_Project/Tests/PlayMode/RelocationPlacementSessionTest.cs
Assets/_Project/Tests/PlayMode/RelocationSmokeTest.cs
Assets/_Project/Tests/PlayMode/ShieldBreakSkillLayerTest.cs
Assets/_Project/Tests/PlayMode/SkillLayerRemainingPayloadsTest.cs
Assets/_Project/Tests/PlayMode/SkillLayerRoutingTest.cs
Assets/_Project/Tests/PlayMode/SlimeSplitE2ETest.cs
Assets/_Project/Tests/PlayMode/SpawnGuideMatchesWalkTest.cs
Assets/_Project/Tests/PlayMode/SpriteUnitBackendPlayTest.cs
Assets/_Project/Tests/PlayMode/StructureLivePlayTest.cs
Assets/_Project/Tests/PlayMode/TallyFlowTest.cs
Assets/_Project/Tests/PlayMode/TestPlacement.cs
Assets/_Project/Tests/PlayMode/UnitOverheadUiLifecycleTest.cs
Assets/_Project/Tests/PlayMode/WavePullCapTest.cs
Assets/_Project/Tests/PlayMode/WaypointRoutingLiveTest.cs
Assets/_Project/Tests/PlayMode/WhirlpotLiveRepro.cs

# 8. 옛 씬 · 옛 코퍼스
Assets/_Project/Scenes/BattleScene.unity
Assets/_Project/Tests/Golden/

# 9. 설정 — 패키지 제거와 같이(unit 9 구현 4)
ProjectSettings/EntitiesClientSettings.asset
```

## 보류 — 사용자 결정 대기

옛 spec 에서 은퇴 결정을 찾지 못했다(8c 구현 6). 답이 오기 전에는 unit 9 가 지우지 않는다.

| 항목 | 하는 일 | 찾은 것 | 선택지 |
|---|---|---|---|
| `Scripts/Presentation/IngameCharacterTest.cs` | 그림자 하이브리드 실험대 — 옛 씬 루트 `CharacterTest` 오브젝트에 붙어 진짜 그림자·블롭 그림자를 실시간 토글한다. 블롭 값은 `BattleBridge` static 미러에서 읽는다 | `camera-direction/15_handoff_summary.md` 「`cf8cec74` 디버그용 `CharacterTest` 오브젝트 끔」 · `distance-based-range/20_shadow_body_parity.md:47` 「겹쳐 렌더되는 것은 이 실험대가 이미 증명해 둔 상태」 — **끔·목적 달성 기록이지 은퇴 결정은 아니다** | (a) 옛 씬과 함께 은퇴 → `retire` 블록으로 옮긴다 · (b) 새 씬으로 옮긴다(블롭 값을 `BlobShadowConfig` 에서 읽게) |
| `Scripts/UI/MenuPopup.cs` 의 dev 토글 「캐릭터/포스트」(`:172~178`) | 메뉴 우하단에서 옛 씬 루트 `CharacterTest`·`Post` 를 켜고 끈다. 파일 나머지(재개·나가기)는 옛 메뉴이고 새 씬은 `CoreMenuPopup` + `CoreMatchOutcomePresenter` 가 이었다(8a) | 없음 | (a) 토글째 은퇴 → `MenuPopup.cs` 를 `retire` 로 · (b) 토글만 `CoreMenuPopup` 으로 옮기고 `MenuPopup.cs` 를 `retire` 로 |

```hold
Assets/_Project/Scripts/Presentation/IngameCharacterTest.cs
Assets/_Project/Scripts/UI/MenuPopup.cs
```

## 자산 도달성의 뿌리

빌드 설정의 켜진 씬(`EditorBuildSettings` — 8b 뒤 `OutgameScene` · `BattleCoreScene`) + `Resources/**`(Editor 밖) + `ProjectSettings/*.asset` 이 부르는 자산 + 아래 dev 씬.
⚠ 「자산이 부르는 스크립트 전부」를 뿌리로 잡지 않는다 — 그러면 옛 전용 프리팹도 뿌리가 돼 퇴역 집합이 비지 않는다.

dev 씬 두 개는 **남긴다**(에이전트 판정 — 퇴역 스크립트를 하나도 안 부른다: `MapTest` = `PropFootprint`·`BlobShadow`·`CameraDirector`, `FluidScratch` = `FluidPaintSim`·`FluidPaintView`).

```roots
Assets/_Project/Scenes/MapTest.unity
Assets/_Project/Scenes/FluidScratch.unity
```

## 판정 메모

- **옛 씬 전용 입력은 부분 파일로 떼었다**(4번 묶음) — `CameraDirector`·`SoundManager` 의 `GameManager.PhaseChanged` 구독, `ResultScreen.Show(MatchTally)`, `WavePatternStripView.RebuildFromDeck`. 본 파일은 남고 `*.OldBattle.cs` 만 지운다. `partial void` 선언은 구현이 사라지면 호출째 빠진다. unit 9 의 「이중 입력을 걷는다」(구현 8)는 이 파일들을 지우는 것으로 끝난다.
- **옛 웨이브 생성기는 퇴역한다**(unit 9 「잔여 이중화」). 마지막 소비처였던 스트립의 덱 경로를 부분 파일로 떼자 남는 코드의 소비처가 0 이 됐다. `GeneratedWavePlan` 은 스트립 입력이라 남는다. 같은 커밋에서 `enemy-wave-integration` 스킬을 코어 `WaveGenerator` 로 옮기는 것은 unit 9 구현 7 몫이다.
- **테스트 짝**(unit 9 구현 2)은 이 목록에 아직 적지 않았다 — 파일마다 「같은 규칙을 증언하는 코어 테스트」를 적는 일은 삭제 커밋의 것이다. 짝 확인이 특히 필요한 것: `EditModeAssets` 의 `FirstRunTutorialWavePlanTests`·`WaveConceptAuthoringTests`·`WaveKillBudgetPinTests`(저작 덱을 옛 생성기로 굴린다) · `EditMode/SkillLoadoutControllerTests`(그중 `FilterHiddenSkills` 단언은 본문이 `CoreDeckComposition` 으로 이사했다).
- 이사해서 **남는** 것(목록에 없다): `Scripts/Data/Authoring/**`(8c 구현 3·4) · 볼륨 프로필 4(`Art/Theme/<맵>/`).
