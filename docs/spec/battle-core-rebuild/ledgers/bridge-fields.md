# 장부 — 브리지 직렬화 필드 귀속표 (unit 0 · 항목 6 · bridge-fields.md)

> 생성/갱신: 같은 스크립트 `--generate`. 코드 `[SerializeField]` 선언과 `BattleScene.unity` 브리지 블록 키를 대조한다.

**unit 5a 에서 91행 전부 닫혔다.** 분류는 넷이다:

⚠ **5a 후속 정정(리드 판정)**: 처음에 「코어(상수)」로 둔 5행(24·39~42)은 잘못된 분류였다.
spec 은 「코어로 가는 값은 `MatchDefinitionBuilder` 입력 → 정의표」라고 적었는데 이 다섯은
`EnemySpawn` 안 리터럴로 굳어 있었고, 그중 셋은 **옛 씬 값과 달랐다**(3·0.2·0.5 → 5·0.4·1).
제약 6 + 계약 2 위반이라 `MovementTuningDef` 로 올리고 옛 씬 값을 복원했다.

> **8c(2026-09-25)**: 「삭제」가 아닌 행은 비고의 **첫 심볼**(또는 「새 주인 = `…`」)이 남는 코드의 심볼로 해석돼야 한다(`check_ledgers.py --owners`). 심볼이 없던 15행에 새 주인을 코드로 대조해 적었고, 28 `draftController` 는 드래프트 은퇴(계약 9)라 「삭제」로 정정했다.

| 분류 | 수 | 뜻 |
|---|---|---|
| 코어 정의표 | 12 | `MatchDefinitionBuilder` 의 입력. 값이 판 안으로 들어간다 |
| 뷰 설정 SO | 53 | 새 자산 7종(`Data/BattleView/`). 값은 옛 씬 블록에서 그대로 복사했다. 그중 한 행(59 `tileSet`)은 **5b 의 맵 뷰가 가져간다** |
| 씬 배선 참조 | 20 | **값이 아니다** — 프리팹·컴포넌트 슬롯이라 정의표에 실을 것이 없다. 각 행이 그 이유를 한 줄로 댄다 |
| 삭제 | 6 | 소비처가 0 이거나(26·73) 새 층에 자리가 없다(34·35) · 축이 값 안으로 접혔다(39) · 드래프트 은퇴(28 — 8c 정정) |

값 대조: 7종 자산의 모든 수치는 `BattleScene.unity` 의 브리지 블록에서 복사했다. 91행 중
**씬 값이 C# 선언 기본값과 다른 행은 다섯**이고, 전부 **씬 쪽을 정본으로** 삼았다 — 그것이
사람이 눈으로 튜닝한 값이다:

| # | 필드 | 코드 기본값 | 씬 값 | 새 주인 |
|---|---|---|---|---|
| 6 | `bossLeapArcMinHeight` | 3.5 | **6** | `LeapVisualConfig` |
| 20 | `fixedMapSeed` | 20260719 | **0** | 코어 정의표(드라이버 `_seed`) |
| 51 | `unitHealthPresentationMode` | `Legacy` | **`UnifiedOverhead`** | `CharacterViewConfig` |
| 60 | `tilemapCharacterScale` | 0.42 | **0.504** | `CharacterViewConfig` |
| 66 | `blobShadowColor` | (0,0,0,**0.45**) | (0,0,**0.08**,**0.75**) | `BlobShadowConfig` |

⚠ 20·51 은 **자산으로 옮기면서 뜻이 바뀐다**: `fixedMapSeed` 0 은 옛 전투에서 「고정 안 함」
이었고 새 드라이버의 `_seed` 는 **언제나 쓰이는 값**이라 0 을 그대로 옮기면 안 된다(씬에는
20260923 을 넣었다). `unitHealthPresentationMode` 는 씬이 이미 `UnifiedOverhead` 라 SO 기본값도
그쪽으로 맞췄다 — 코드 기본값(`Legacy`)을 따랐으면 새 씬에서 오버헤드 바가 통째로 안 떴다.

코드 선언 91 · 씬 블록 키 91

| # | 필드 | 파일 | 씬에 있음 | 새 주인 | 비고 |
|---|---|---|---|---|---|
| 1 | `bonusPortalPrefab` | BattleBridge.BonusWave.cs | ○ | 씬 배선 참조 | 보너스 포탈 **프리팹**이다 — 값이 아니라 그릴 물건이라 정의표에 실을 것이 없다. 새 주인 = `CoreBonusPortalPresenter._portalPrefab`(**8a** 실현 — 초판 「unit 6 의 보너스 뷰」는 배정만 되고 코드에 없었다) |
| 2 | `bossLeapTotalSeconds` | BattleBridge.BossLeap.cs | ○ | 뷰 설정 SO | `LeapVisualConfig.bossTotalSeconds` |
| 3 | `bossLeapRecoilSeconds` | BattleBridge.BossLeap.cs | ○ | 뷰 설정 SO | `LeapVisualConfig.bossRecoilSeconds` |
| 4 | `bossLeapRecoilDip` | BattleBridge.BossLeap.cs | ○ | 뷰 설정 SO | `LeapVisualConfig.bossRecoilDip` |
| 5 | `bossLeapArcHeightFactor` | BattleBridge.BossLeap.cs | ○ | 뷰 설정 SO | `LeapVisualConfig.bossArcHeightFactor` |
| 6 | `bossLeapArcMinHeight` | BattleBridge.BossLeap.cs | ○ | 뷰 설정 SO | `LeapVisualConfig.bossArcMinHeight` |
| 7 | `bossLeapLaunchControl` | BattleBridge.BossLeap.cs | ○ | 뷰 설정 SO | `LeapVisualConfig.bossLaunchControl` |
| 8 | `bossLeapLandingHeight` | BattleBridge.BossLeap.cs | ○ | 뷰 설정 SO | `LeapVisualConfig.bossLandingHeight` |
| 9 | `bossLeapHangPower` | BattleBridge.BossLeap.cs | ○ | 뷰 설정 SO | `LeapVisualConfig.bossHangPower` |
| 10 | `bossLeapLandingSquash` | BattleBridge.BossLeap.cs | ○ | 뷰 설정 SO | `LeapVisualConfig.bossLandingSquash` |
| 11 | `bossLeapLandingSquashSeconds` | BattleBridge.BossLeap.cs | ○ | 뷰 설정 SO | `LeapVisualConfig.bossLandingSquashSeconds` |
| 12 | `ultimateLeapAscendSeconds` | BattleBridge.UltimateLeap.cs | ○ | 뷰 설정 SO | `LeapVisualConfig.ultimateAscendSeconds` |
| 13 | `ultimateLeapDescendSeconds` | BattleBridge.UltimateLeap.cs | ○ | 뷰 설정 SO | `LeapVisualConfig.ultimateDescendSeconds` |
| 14 | `ultimateLeapHeight` | BattleBridge.UltimateLeap.cs | ○ | 뷰 설정 SO | `LeapVisualConfig.ultimateHeight` |
| 15 | `ultimateLeapLandingSquash` | BattleBridge.UltimateLeap.cs | ○ | 뷰 설정 SO | `LeapVisualConfig.ultimateLandingSquash` |
| 16 | `ultimateLeapLandingSquashSeconds` | BattleBridge.UltimateLeap.cs | ○ | 뷰 설정 SO | `LeapVisualConfig.ultimateLandingSquashSeconds` |
| 17 | `deck` | BattleBridge.cs | ○ | 코어 정의표 | `MatchDefinitionBuilder.Build(deck:)` → `WaveDeckDef`. 드라이버가 직렬화로 든다 |
| 18 | `bonusWaveData` | BattleBridge.cs | ○ | 코어 정의표 | 새 주인 = `BattleDriver._bonus` (8c 확인) — `Build(bonus:)` → `BonusWaveDef` |
| 19 | `mapPool` | BattleBridge.cs | ○ | 코어 정의표 | 새 주인 = `BattleDriver._mapPool` (8c 확인) — 맵 선택. **5a 는 스테이지 프리팹 직접 지정**이고 풀 선택(서버 시드 %)은 5c 에서 — 「이식 제외」 참조 |
| 20 | `fixedMapSeed` | BattleBridge.cs | ○ | 코어 정의표 | **배선이 아니라 규칙 값이다** — 재현의 두 축 중 하나(`MatchDefinition.Seed`). 드라이버의 `_seed` |
| 21 | `seasonRegistry` | BattleBridge.cs | ○ | 코어 정의표 | ⚠ **기믹 풀만** `MatchModeData.gimmickPool` 로 옮겨졌다(계약 5) — 시즌 자산 전체가 은퇴한 것이 아니다. 살아 있는 소비가 둘 더 있다: `SeasonRuntime.Bind` 가 싣는 **맵 테마**가 `effectTiles`·`effectTileCount`(코어 `MatchDefinition.EffectTileCount`)와 `tileSet` 오버라이드(5b 의 타일 오버레이)를 준다. 5a 의 드라이버는 아직 안 읽는다 |
| 22 | `tileSize` | BattleBridge.cs | ○ | 코어 정의표 | 새 주인 = `BattleDriver._tileSize` (8c 확인) — `Build(tileSize:)` → `MapSnapshot.TileSize`. 뷰의 타일↔월드 환산도 여기서 **파생**한다(저작 2벌 금지) |
| 23 | `spawnHeight` | BattleBridge.cs | ○ | 뷰 설정 SO | `UnitLiftKnobs.spawnHeight` |
| 24 | `agentRadiusTiles` | BattleBridge.cs | ○ | 코어 정의표 | `MovementTuningDef.AgentRadiusTiles`(`MovementTuningConfig.asset` → 드라이버 → 빌더). **군집 통과로 검산한 값**이라 단독 통과는 검산이 아니다 |
| 25 | `resultScreen` | BattleBridge.cs | ○ | 씬 배선 참조 | 결과 **화면 컴포넌트**다 — 값이 아니라 띄울 UI. 새 주인 = `CoreMatchOutcomePresenter._resultScreen`(5c, `BattleCoreScene` 배선) |
| 26 | `scoreRules` | BattleBridge.cs | ○ | 삭제 | 소비처 0. 선언만 있고 읽는 줄이 없다(브리지 60행). 점수는 처치당 `killScore` 합이고 그 값은 적 SO 에 있다. **5c 확인** — 새 층의 점수는 `ScoreLedger` → `MatchOutcome` 이고 이 자산을 읽는 줄이 없다 |
| 27 | `defenderPool` | BattleBridge.cs | ○ | 코어 정의표 | 새 주인 = `BattleDriver._defenders` (8c 확인) — `Build(defenders:)` → `UnitDef[]`. 드라이버의 `_defenders` |
| 28 | `draftController` | BattleBridge.cs | ○ | 삭제 | 드래프트 은퇴(계약 9) — 새 층에 드래프트 진입이 없다(8c 확인). 옛 비고: 드래프트 **컨트롤러**다 — 판 밖 흐름이라 정의표에 실을 값이 없다. 새 주인은 5c 의 모드 진입 |
| 29 | `skillRuntime` | BattleBridge.cs | ○ | 씬 배선 참조 | 새 주인 = `HandDeck` (8c 확인) — 스킬 런타임 **컴포넌트**. 발동은 unit 7 의 것이고 여기엔 값이 없다 |
| 30 | `_placementPhaseView` | BattleBridge.cs | ○ | 씬 배선 참조 | 새 주인 = `CorePlacementPhaseView` (8c 확인) — 배치 페이즈 **뷰**. 값이 아니라 창을 그리는 물건 — 5b |
| 31 | `_gimmickPhaseView` | BattleBridge.cs | ○ | 씬 배선 참조 | 기믹 페이즈 **뷰**. 새 주인 = `CoreGimmickReveal`(**8a** 실현 — 초판 「5b」는 배정만 됐다). 구동 = 코어 `GimmickAssigned` 사건 |
| 32 | `spineUnitPool` | BattleBridge.cs | ○ | 씬 배선 참조 | 유닛 뷰 **풀 컴포넌트**. 새 주인 = `CoreUnitViewPool`(이 unit 에서 씬에 선다) |
| 33 | `retireFlight` | BattleBridge.cs | ○ | 씬 배선 참조 | 퇴근 비행 **연출 컴포넌트**. 값이 아니라 코루틴을 도는 물건. 새 주인 = `CoreRetireFlightPresenter`(**8a** 실현 — 대조표 재측정이 찾았다. 초판 「5c」는 배정만 됐다). 구동 = 코어 `Retired` 사건 → `CoreUnitViewPool.TryDetach` |
| 34 | `enemyViewPool` | BattleBridge.cs | ○ | 삭제 | 쿼드 폴백 풀이 **둘**이던 시절의 반쪽. 백엔드 선택이 `CoreUnitViewPool.TrySpawn` 한 곳으로 합쳐져 자리가 없다(씬 값도 이미 비어 있다) |
| 35 | `defenderFallbackViewPool` | BattleBridge.cs | ○ | 삭제 | 위와 같은 반쪽. 씬 값이 비어 있고 새 층에는 자리가 없다 |
| 36 | `enemyDragDimAlpha` | BattleBridge.cs | ○ | 뷰 설정 SO | `CharacterViewConfig.enemyDragDimAlpha` |
| 37 | `enemyDragDimFadeSpeed` | BattleBridge.cs | ○ | 뷰 설정 SO | `CharacterViewConfig.enemyDragDimFadeSpeed` |
| 38 | `spineDefenderYOffset` | BattleBridge.cs | ○ | 뷰 설정 SO | `UnitLiftKnobs.spineDefenderYOffset` |
| 39 | `spawnSpreadEnabled` | BattleBridge.cs | ○ | 삭제 | `spawnSpreadEnabled` bool 축은 안 옮긴다 — **분산 폭 0 이 곧 「끔」**이라 끄는 방법이 이미 값 안에 있다. 축이 둘이면 「켜져 있는데 폭이 0」과 「꺼져 있는데 폭이 0.2」가 표현 가능해진다 |
| 40 | `spawnSpreadFraction` | BattleBridge.cs | ○ | 코어 정의표 | `MovementTuningDef.SpawnSpreadFraction` |
| 41 | `spawnSpreadTopScale` | BattleBridge.cs | ○ | 코어 정의표 | `MovementTuningDef.SpawnSpreadTopScale` |
| 42 | `spawnSubLaneCount` | BattleBridge.cs | ○ | 코어 정의표 | `MovementTuningDef.SpawnSubLaneCount` |
| 43 | `vfxSpawner` | BattleBridge.cs | ○ | 씬 배선 참조 | 새 주인 = `CoreVfxSpawner` (8c 확인) — VFX **스포너 컴포넌트**. 구독할 사건이 unit 6·7 에서 열린다 |
| 44 | `damageNumberSpawner` | BattleBridge.cs | ○ | 씬 배선 참조 | 피해 숫자 **스포너 컴포넌트**. 새 주인 = `CoreDamageNumberSpawner` |
| 45 | `healthDisplayStyle` | BattleBridge.cs | ○ | 뷰 설정 SO | `CharacterViewConfig.healthDisplayStyle`(SO 참조를 SO 가 든다) |
| 46 | `walkAnimSpeedStyle` | BattleBridge.cs | ○ | 뷰 설정 SO | `CharacterViewConfig.walkAnimSpeedStyle` |
| 47 | `enemyHitBarSpawner` | BattleBridge.cs | ○ | 씬 배선 참조 | 히트바 **스포너 컴포넌트**. 새 주인 = `CoreEnemyHitBarSpawner` |
| 48 | `statusFxSpawner` | BattleBridge.cs | ○ | 씬 배선 참조 | 새 주인 = `CoreStatusFxSpawner` (8c 확인) — 상태 FX **스포너 컴포넌트**. 사건(CC·DoT)이 unit 6 에서 열린다 — 빈 풀을 먼저 만들지 않는다 |
| 49 | `tileHealthGaugeLayer` | BattleBridge.cs | ○ | 씬 배선 참조 | 새 주인 = `CoreUnitOverheadUiLayer` (8c 확인) — 타일 게이지 **레이어 컴포넌트**. 레거시 표시 모드의 것이라 5b 에서 처분 |
| 50 | `dcIconStripSpawner` | BattleBridge.cs | ○ | 씬 배선 참조 | 새 주인 = `CoreUnitOverheadUiLayer.RebuildCardView` (8c 확인) — 드림캐쳐 아이콘 **스포너 컴포넌트**. 부착 사건이 unit 7 에서 열린다 |
| 51 | `unitHealthPresentationMode` | BattleBridge.cs | ○ | 뷰 설정 SO | `CharacterViewConfig.healthPresentationMode` |
| 52 | `unitOverheadUiLayer` | BattleBridge.cs | ○ | 씬 배선 참조 | 오버헤드 **레이어 컴포넌트**. 새 주인 = `CoreUnitOverheadUiLayer` |
| 53 | `beamPresenter` | BattleBridge.cs | ○ | 씬 배선 참조 | 새 주인 = `CoreBeamPresenter` (8c 확인) — 빔 **프리젠터 컴포넌트**. 사건이 unit 6 에서 열린다(씬 값도 비어 있다) |
| 54 | `scoreHud` | BattleBridge.cs | ○ | 씬 배선 참조 | 새 주인 = `CoreScoreHud` (8c 확인) — 점수 **HUD 컴포넌트**. 5b |
| 55 | `_bossWarning` | BattleBridge.cs | ○ | 씬 배선 참조 | 보스 경보 **뷰 컴포넌트**. 값이 아니라 띄울 UI — 구동 신호는 **보스 스폰 순간**(`BattleBridge.cs:10116~10121`, `tier == Boss`)이다 — 2026-09-25 정정(초판 「`WaveStarted`」는 오기). 새 주인 = `CoreBossWarning`(**8a** 실현 — `UnitSpawned` 의 `DefIndex` → `tier == Boss`) |
| 56 | `_projectileViewPool` | BattleBridge.cs | ○ | 씬 배선 참조 | 투사체 뷰 **풀 컴포넌트**. 새 주인 = `CoreProjectileViewPool` |
| 57 | `placementInput` | BattleBridge.cs | ○ | 씬 배선 참조 | 새 주인 = `DragPlacementInput` (8c 확인) — 배치 입력 **컴포넌트**. 커맨드로 바뀐다 — 5b |
| 58 | `tilemapMapView` | BattleBridge.cs | ○ | 씬 배선 참조 | 맵 뷰 **컴포넌트**. 5a 는 그중 **평면 선언만** 갖는다(`CoreBoardPlane`), 오버레이·범위 타일은 5b |
| 59 | `tileSet` | BattleBridge.cs | ○ | 뷰 설정 SO(5b) | 새 주인 = `CoreMapOverlay._tileSet` (8c 확인) — **배선이 아니라 콘텐츠 값이다** — 타일 그림 세트. 다만 그 소비자(오버레이 타일맵)가 5b 라 자산도 그때 선다 |
| 60 | `tilemapCharacterScale` | BattleBridge.cs | ○ | 뷰 설정 SO | `CharacterViewConfig.characterScale` |
| 61 | `tilemapBillboardTilt` | BattleBridge.cs | ○ | 뷰 설정 SO | `CharacterViewConfig.billboardTilt` |
| 62 | `propDistanceTiltFactor` | BattleBridge.cs | ○ | 뷰 설정 SO | `CharacterViewConfig.propDistanceTiltFactor` |
| 63 | `propDistanceTiltMin` | BattleBridge.cs | ○ | 뷰 설정 SO | `CharacterViewConfig.propDistanceTiltMin` |
| 64 | `propDistanceTiltMax` | BattleBridge.cs | ○ | 뷰 설정 SO | `CharacterViewConfig.propDistanceTiltMax` |
| 65 | `blobShadowSprite` | BattleBridge.cs | ○ | 뷰 설정 SO | `BlobShadowConfig.sprite` |
| 66 | `blobShadowColor` | BattleBridge.cs | ○ | 뷰 설정 SO | `BlobShadowConfig.color` |
| 67 | `blobShadowLift` | BattleBridge.cs | ○ | 뷰 설정 SO | `BlobShadowConfig.lift` |
| 68 | `liftScalePerHeight` | BattleBridge.cs | ○ | 뷰 설정 SO | `UnitLiftKnobs.liftScalePerHeight` |
| 69 | `liftScaleMax` | BattleBridge.cs | ○ | 뷰 설정 SO | `UnitLiftKnobs.liftScaleMax` |
| 70 | `liftShadowFullHeight` | BattleBridge.cs | ○ | 뷰 설정 SO | `UnitLiftKnobs.liftShadowFullHeight` |
| 71 | `liftShadowMinAlpha` | BattleBridge.cs | ○ | 뷰 설정 SO | `UnitLiftKnobs.liftShadowMinAlpha` |
| 72 | `useRealShadows` | BattleBridge.cs | ○ | 뷰 설정 SO | `BlobShadowConfig.useRealShadows`(모바일 게이트 포함) |
| 73 | `tilemapHiddenEnvironment` | BattleBridge.cs | ○ | 삭제 | 타일맵 모드로 전환할 때 **끌 옛 환경 오브젝트** 목록이다. 씬 값이 비어 있고, 새 씬에는 끌 옛 환경이 없다 |
| 74 | `stackModifierAuthoring` | BattleBridge.cs | ○ | 코어 정의표 | 새 주인 = `BattleDriver._stackModifiers` (8c 확인) — 스택 임계 저작. 읽는 쪽이 unit 6(효과·스탯)이라 빌더 입력은 그때 열린다 |
| 75 | `pickupViewPrefab` | BattleBridge.cs | ○ | 뷰 설정 SO | `PickupViewConfig.pickupPrefab` |
| 76 | `pickupViewHeight` | BattleBridge.cs | ○ | 뷰 설정 SO | `PickupViewConfig.pickupHeight` |
| 77 | `pickupModelScale` | BattleBridge.cs | ○ | 뷰 설정 SO | `PickupViewConfig.pickupModelScale` |
| 78 | `pickupModelBaseY` | BattleBridge.cs | ○ | 뷰 설정 SO | `PickupViewConfig.pickupModelBaseY` |
| 79 | `pickupOverrideMaterial` | BattleBridge.cs | ○ | 뷰 설정 SO | `PickupViewConfig.pickupOverrideMaterial` |
| 80 | `resignationViewPrefab` | BattleBridge.cs | ○ | 뷰 설정 SO | `PickupViewConfig.resignationPrefab` |
| 81 | `resignationViewHeight` | BattleBridge.cs | ○ | 뷰 설정 SO | `PickupViewConfig.resignationHeight` |
| 82 | `heartRestBpm` | BattleBridge.cs | ○ | 뷰 설정 SO | `HeartHudConfig.restBpm` |
| 83 | `heartMaxBpm` | BattleBridge.cs | ○ | 뷰 설정 SO | `HeartHudConfig.maxBpm` |
| 84 | `heartBeatDepth` | BattleBridge.cs | ○ | 뷰 설정 SO | `HeartHudConfig.beatDepth` |
| 85 | `heartBarPunchDepth` | BattleBridge.cs | ○ | 뷰 설정 SO | `HeartHudConfig.barPunchDepth` |
| 86 | `heartBarPunchFullRise` | BattleBridge.cs | ○ | 뷰 설정 SO | `HeartHudConfig.barPunchFullRise` |
| 87 | `heartBarPunchDecayPerSec` | BattleBridge.cs | ○ | 뷰 설정 SO | `HeartHudConfig.barPunchDecayPerSec` |
| 88 | `coreBurstHoldSec` | BattleBridge.cs | ○ | 뷰 설정 SO | `HeartHudConfig.coreBurstHoldSec` |
| 89 | `coreBurstTimeScale` | BattleBridge.cs | ○ | 뷰 설정 SO | `HeartHudConfig.coreBurstTimeScale` |
| 90 | `goalOverheadHeight` | BattleBridge.cs | ○ | 뷰 설정 SO | `HeartHudConfig.goalOverheadHeight` |
| 91 | `dcProcImpactMinIntervalSec` | BattleBridge.cs | ○ | 뷰 설정 SO | `DcVisualConfig.procImpactMinIntervalSec` |
