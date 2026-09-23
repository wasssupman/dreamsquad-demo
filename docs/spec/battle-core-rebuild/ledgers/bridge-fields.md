# 장부 — 브리지 직렬화 필드 귀속표 (unit 0 · 항목 6 · bridge-fields.md)

> 생성/갱신: 같은 스크립트 `--generate`. 코드 `[SerializeField]` 선언과 `BattleScene.unity` 브리지 블록 키를 대조한다.

**unit 5a 에서 91행 전부 닫혔다.** 분류는 넷이다:

| 분류 | 수 | 뜻 |
|---|---|---|
| 코어 정의표 | 7 | `MatchDefinitionBuilder` 의 입력. 값이 판 안으로 들어간다 |
| 코어(상수) | 5 | 옮기는 과정에서 **코드 상수가 된 것**. ⚠ 셋은 씬 값과 다르다(40·41·42) — unit 2 가 고른 값이고 되볼 자리는 플레이다 |
| 뷰 설정 SO | 53 | 새 자산 7종(`Data/BattleView/`). 값은 옛 씬 블록에서 그대로 복사했다. 그중 한 행(59 `tileSet`)은 **5b 의 맵 뷰가 가져간다** |
| 씬 배선 참조 | 21 | **값이 아니다** — 프리팹·컴포넌트 슬롯이라 정의표에 실을 것이 없다. 각 행이 그 이유를 한 줄로 댄다 |
| 삭제 | 5 | 소비처가 0 이거나(26·73) 새 층에 자리가 없다(21·34·35) |

값 대조: 7종 자산의 모든 수치는 `BattleScene.unity` 의 브리지 블록에서 복사했고, **코드 기본값과 다른 행**(`tilemapCharacterScale` 0.42→0.504 · `bossLeapArcMinHeight` 3.5→6 · `blobShadowColor` 0.45→0.75 알파 · `propDistanceTiltFactor` 미설정→0.78)은 **씬 쪽을 정본으로 삼았다** — 그것이 사람이 눈으로 튜닝한 값이다.

코드 선언 91 · 씬 블록 키 91

| # | 필드 | 파일 | 씬에 있음 | 새 주인 | 비고 |
|---|---|---|---|---|---|
| 1 | `bonusPortalPrefab` | BattleBridge.BonusWave.cs | ○ | 씬 배선 참조 | 보너스 포탈 **프리팹**이다 — 값이 아니라 그릴 물건이라 정의표에 실을 것이 없다. 새 주인은 unit 6 의 보너스 뷰 |
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
| 18 | `bonusWaveData` | BattleBridge.cs | ○ | 코어 정의표 | `Build(bonus:)` → `BonusWaveDef` |
| 19 | `mapPool` | BattleBridge.cs | ○ | 코어 정의표 | 맵 선택. **5a 는 스테이지 프리팹 직접 지정**이고 풀 선택(서버 시드 %)은 5c 에서 — 「이식 제외」 참조 |
| 20 | `fixedMapSeed` | BattleBridge.cs | ○ | 코어 정의표 | **배선이 아니라 규칙 값이다** — 재현의 두 축 중 하나(`MatchDefinition.Seed`). 드라이버의 `_seed` |
| 21 | `seasonRegistry` | BattleBridge.cs | ○ | 삭제 | 기믹 풀의 새 주인은 `MatchModeData.gimmickPool` 이다(계약 5 — 모드가 «어느 저작 자산을 쓸지» 고른다). 시즌 레지스트리는 그 축의 전신이라 남길 것이 없다 |
| 22 | `tileSize` | BattleBridge.cs | ○ | 코어 정의표 | `Build(tileSize:)` → `MapSnapshot.TileSize`. 뷰의 타일↔월드 환산도 여기서 **파생**한다(저작 2벌 금지) |
| 23 | `spawnHeight` | BattleBridge.cs | ○ | 뷰 설정 SO | `UnitLiftKnobs.spawnHeight` |
| 24 | `agentRadiusTiles` | BattleBridge.cs | ○ | 코어(상수) | `EnemySpawn.AgentRadiusTiles = 0.25` — 씬 값과 같다(드리프트 0). 저작값으로 되돌리는 것은 unit 2 의 결정이라 여기서 바꾸지 않는다 |
| 25 | `resultScreen` | BattleBridge.cs | ○ | 씬 배선 참조 | 결과 **화면 컴포넌트**다 — 값이 아니라 띄울 UI. 새 주인은 5c |
| 26 | `scoreRules` | BattleBridge.cs | ○ | 삭제 | 소비처 0. 선언만 있고 읽는 줄이 없다(브리지 60행). 점수는 처치당 `killScore` 합이고 그 값은 적 SO 에 있다 |
| 27 | `defenderPool` | BattleBridge.cs | ○ | 코어 정의표 | `Build(defenders:)` → `UnitDef[]`. 드라이버의 `_defenders` |
| 28 | `draftController` | BattleBridge.cs | ○ | 씬 배선 참조 | 드래프트 **컨트롤러**다 — 판 밖 흐름이라 정의표에 실을 값이 없다. 새 주인은 5c 의 모드 진입 |
| 29 | `skillRuntime` | BattleBridge.cs | ○ | 씬 배선 참조 | 스킬 런타임 **컴포넌트**. 발동은 unit 7 의 것이고 여기엔 값이 없다 |
| 30 | `_placementPhaseView` | BattleBridge.cs | ○ | 씬 배선 참조 | 배치 페이즈 **뷰**. 값이 아니라 창을 그리는 물건 — 5b |
| 31 | `_gimmickPhaseView` | BattleBridge.cs | ○ | 씬 배선 참조 | 기믹 페이즈 **뷰**. 위와 같다 — 5b |
| 32 | `spineUnitPool` | BattleBridge.cs | ○ | 씬 배선 참조 | 유닛 뷰 **풀 컴포넌트**. 새 주인 = `CoreUnitViewPool`(이 unit 에서 씬에 선다) |
| 33 | `retireFlight` | BattleBridge.cs | ○ | 씬 배선 참조 | 퇴근 비행 **연출 컴포넌트**. 값이 아니라 코루틴을 도는 물건 — 5c |
| 34 | `enemyViewPool` | BattleBridge.cs | ○ | 삭제 | 쿼드 폴백 풀이 **둘**이던 시절의 반쪽. 백엔드 선택이 `CoreUnitViewPool.TrySpawn` 한 곳으로 합쳐져 자리가 없다(씬 값도 이미 비어 있다) |
| 35 | `defenderFallbackViewPool` | BattleBridge.cs | ○ | 삭제 | 위와 같은 반쪽. 씬 값이 비어 있고 새 층에는 자리가 없다 |
| 36 | `enemyDragDimAlpha` | BattleBridge.cs | ○ | 뷰 설정 SO | `CharacterViewConfig.enemyDragDimAlpha` |
| 37 | `enemyDragDimFadeSpeed` | BattleBridge.cs | ○ | 뷰 설정 SO | `CharacterViewConfig.enemyDragDimFadeSpeed` |
| 38 | `spineDefenderYOffset` | BattleBridge.cs | ○ | 뷰 설정 SO | `UnitLiftKnobs.spineDefenderYOffset` |
| 39 | `spawnSpreadEnabled` | BattleBridge.cs | ○ | 코어(상수) | `EnemySpawn` 의 레인 분산. 씬은 켬(1)이고 코어는 항상 켜져 있다 — 끄는 축이 없어졌다 |
| 40 | `spawnSpreadFraction` | BattleBridge.cs | ○ | 코어(상수) | `SpawnSpread.LaneFraction(..., 0.4f, ...)` — 씬 0.2 와 **다르다**. unit 2 가 고른 값이고 이 unit 의 범위 밖이다(플레이로 되본다) |
| 41 | `spawnSpreadTopScale` | BattleBridge.cs | ○ | 코어(상수) | `SpawnSpread.LaneFraction(..., 1f)` — 씬 0.5 와 다르다. 위와 같은 사유 |
| 42 | `spawnSubLaneCount` | BattleBridge.cs | ○ | 코어(상수) | `SpawnSpread.LaneFraction(_, 5, _, _)` — 씬 3 과 다르다. 위와 같은 사유 |
| 43 | `vfxSpawner` | BattleBridge.cs | ○ | 씬 배선 참조 | VFX **스포너 컴포넌트**. 구독할 사건이 unit 6·7 에서 열린다 |
| 44 | `damageNumberSpawner` | BattleBridge.cs | ○ | 씬 배선 참조 | 피해 숫자 **스포너 컴포넌트**. 새 주인 = `CoreDamageNumberSpawner` |
| 45 | `healthDisplayStyle` | BattleBridge.cs | ○ | 뷰 설정 SO | `CharacterViewConfig.healthDisplayStyle`(SO 참조를 SO 가 든다) |
| 46 | `walkAnimSpeedStyle` | BattleBridge.cs | ○ | 뷰 설정 SO | `CharacterViewConfig.walkAnimSpeedStyle` |
| 47 | `enemyHitBarSpawner` | BattleBridge.cs | ○ | 씬 배선 참조 | 히트바 **스포너 컴포넌트**. 새 주인 = `CoreEnemyHitBarSpawner` |
| 48 | `statusFxSpawner` | BattleBridge.cs | ○ | 씬 배선 참조 | 상태 FX **스포너 컴포넌트**. 사건(CC·DoT)이 unit 6 에서 열린다 — 빈 풀을 먼저 만들지 않는다 |
| 49 | `tileHealthGaugeLayer` | BattleBridge.cs | ○ | 씬 배선 참조 | 타일 게이지 **레이어 컴포넌트**. 레거시 표시 모드의 것이라 5b 에서 처분 |
| 50 | `dcIconStripSpawner` | BattleBridge.cs | ○ | 씬 배선 참조 | 드림캐쳐 아이콘 **스포너 컴포넌트**. 부착 사건이 unit 7 에서 열린다 |
| 51 | `unitHealthPresentationMode` | BattleBridge.cs | ○ | 뷰 설정 SO | `CharacterViewConfig.healthPresentationMode` |
| 52 | `unitOverheadUiLayer` | BattleBridge.cs | ○ | 씬 배선 참조 | 오버헤드 **레이어 컴포넌트**. 새 주인 = `CoreUnitOverheadUiLayer` |
| 53 | `beamPresenter` | BattleBridge.cs | ○ | 씬 배선 참조 | 빔 **프리젠터 컴포넌트**. 사건이 unit 6 에서 열린다(씬 값도 비어 있다) |
| 54 | `scoreHud` | BattleBridge.cs | ○ | 씬 배선 참조 | 점수 **HUD 컴포넌트**. 5b |
| 55 | `_bossWarning` | BattleBridge.cs | ○ | 씬 배선 참조 | 보스 경보 **뷰 컴포넌트**. 값이 아니라 띄울 UI — 구동 신호는 `WaveStarted` 이고 5b 가 잇는다 |
| 56 | `_projectileViewPool` | BattleBridge.cs | ○ | 씬 배선 참조 | 투사체 뷰 **풀 컴포넌트**. 새 주인 = `CoreProjectileViewPool` |
| 57 | `placementInput` | BattleBridge.cs | ○ | 씬 배선 참조 | 배치 입력 **컴포넌트**. 커맨드로 바뀐다 — 5b |
| 58 | `tilemapMapView` | BattleBridge.cs | ○ | 씬 배선 참조 | 맵 뷰 **컴포넌트**. 5a 는 그중 **평면 선언만** 갖는다(`CoreBoardPlane`), 오버레이·범위 타일은 5b |
| 59 | `tileSet` | BattleBridge.cs | ○ | 뷰 설정 SO(5b) | **배선이 아니라 콘텐츠 값이다** — 타일 그림 세트. 다만 그 소비자(오버레이 타일맵)가 5b 라 자산도 그때 선다 |
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
| 74 | `stackModifierAuthoring` | BattleBridge.cs | ○ | 코어 정의표 | 스택 임계 저작. 읽는 쪽이 unit 6(효과·스탯)이라 빌더 입력은 그때 열린다 |
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
