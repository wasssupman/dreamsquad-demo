# Object Pipeline Map — 플레이 오브젝트 저작→렌더 정거장 체크표

> **대조용 문서다.** 플레이 오브젝트를 신설하거나 저작→렌더 경로를 바꾸는 spec 의 README 를 쓸 때, 아래에서 가장 가까운 아키타입 표를 복사해 `파이프라인 커버리지` 섹션으로 붙인다. 해당 없는 정거장은 빈 칸이 아니라 **`N/A + 이유`** 를 적는다(빈 칸은 「잊었음」과 「필요 없음」을 구분하지 못한다). 대조 중 표가 코드와 어긋나면 **그 자리에서 이 문서를 고친다.**
>
> 2026-09-25 전면 재작성(battle-core-rebuild unit 8c) — 전투는 순수 C# 전투 코어(`Scripts/BattleCore/`)와 새 Unity 층(`Scripts/BattleCoreUnity/`)이 돈다. 옛 정거장(ECS 컴포넌트 · 시스템 · 큐 싱글턴 · 브리지 드레인)은 각 표 끝의 **이력 한 줄**로만 남긴다. 앵커는 심볼이다 — 경로는 `Scripts/` 기준. 구현 상세의 정본은 코드이고, 이 문서는 정거장 유무만 답한다.

## 공통 정거장

| # | 정거장 | 어디 | 확인 포인트 |
|---|---|---|---|
| 1 | 저작 SO | `Data/**`(유닛·적·탄·해저드·카드 SO) · `Data/BattleView/**`(뷰 설정 SO 7종) | 수치는 SO 에서만 온다(제약 6). 뷰만 쓰는 값은 뷰 설정 SO 로, 판 규칙 값은 정의표로 |
| 2 | 정의표 행 | `MatchDefinitionBuilder.Build` → `MatchDefinition` 배열(`Units`·`Enemies`·`Projectiles`·`Patterns`·`Structures`·`Hazards`·`BlockingHazards`·`EffectTiles`·`Cards`·`Gimmicks`·…) | **빌더 매핑 누락은 조용히 죽는다**(인계 함정 8) — 새 SO 필드마다 빌더 매핑 테스트 + 열거 번호 핀 테스트(`BuilderEnumPinTests`) |
| 3 | 코어 스폰 · 사건 | `BattleWorld.Spawn*` · 담당자(`PlacementService`·`WaveScheduler`·`GimmickHost`·`HandDeck`) → `CoreEvent`(`CoreEventKind` 번호) | 사건은 **값 스냅샷**이다(`SimEntityId` 키 · 자리↔몸 짝, 절대 제약 4). 뷰는 사건으로 코어 상태를 되묻지 않는다 |
| 4 | 뷰 풀 | `BattleDriver.Subscribe(order, handler)` 구독자 — `Core*ViewPool` · `Core*Presenter` · `Core*Spawner` | 풀마다 자기 구독(통합 뷰 없음). 틱 뒤 `BattleDriver` 가 사건을 순서대로 흘린다 |
| 5 | 뷰 순서 | `ViewOrder`(Trace 0 → Leap 10 → Board 15 → Unit 20 → Projectile 30 → Effect 35 → Damage 40 → Status 45 → Overhead 50 → Hand 55 → Audio 60 → Outcome) | C# 이벤트 등록 순서(= 하이어라키 순서)에 기대지 않는다. 새 풀은 여기 상수 하나를 고른다 |
| 6 | 소멸 사건 회수 | `UnitDestroyed`(3) · `ProjectileDespawned`(11) · `HazardDestroyed`(45) · `FieldDespawned`(47) · `PickupTaken`(49)/`PickupExpired`(52) · `ResignationConsumed`(53) · `CardDetached`(61) + 판 경계 `MatchStarted`(1) | 스폰 사건과 **짝**이 있어야 한다. 판 경계 회수가 없으면 다음 판에 남는다 |
| 7 | 씬 배선 | `BattleCoreScene.unity` — 드라이버·뷰 풀 컴포넌트의 SerializeField · `MatchViewAssets`(정의표 번호 → 그림 SO) | UnityMCP 로 배선하고 Play 검증까지가 완료(CLAUDE.md 금지 행동). 폴백 `FindAnyObjectByType` + 경고는 배선 전 임시다 |

정거장별 시공법 스킬: 씬 배선 = `unity-feature-wiring` · VFX 저작/통합 = `unity-vfx-authoring`/`unity-vfx-integration` · 프랍/타일 = `unity-prop-tile-authoring`.

---

## 방어 유닛 (Defender)

| 정거장 | 앵커 | 확인 포인트 |
|---|---|---|
| 저작 SO | `DefenderUnitData`(+`DefenderCatalog`) · 고유 능력 = `Data/Abilities/`(`DefenderAbilityData` 서브에셋) | 신규 유닛은 **`DefenderCatalog` 등록까지**(미등록 = 로스터 미노출). 편성은 로비 → `MatchEntry.ResolveSquadUnits` |
| 정의표 행 | `MatchDefinitionBuilder.ToUnitDef` → `UnitDef` · 공격 = `CombatDefinitionBuilder.BuildDefenderAttack` · 배치 스킬·실드 = `BindingDefinitionBuilder` | 배치 저작 7칸(코스트 등)이 정의표로 안 옮겨져 배치가 공짜였던 선례(함정 8) |
| 코어 스폰 · 사건 | 커맨드 `PlaceDefender` → `PlacementService.TryPlace` → `PlacementService.SpawnDefender` → `Placed`(25) · 비행 착지 커맨드 → `DefenderActivated`(28) · 퇴근 → `Retired`(26) · 거절 → `PlacementRejected`(27) | 「배치 중」은 코어가 소유한 페이즈다(`PlacementService.StepActivation`) — 길이 = 배치 모션 |
| 뷰 풀 | `CoreUnitViewPool`(`UnitSpawned`·`DefenderActivated`·`AttackResolved`·`Knockup`·`UnitSlain`) → `CoreSpineUnitView` / `CoreSpriteUnitView` / 폴백 `CoreQuadUnitView` · 배치 비행 `CoreDeployFlightPresenter` · 퇴근 비행 `CoreRetireFlightPresenter` · 드래그 `CoreDragPreviewPresenter` | ★백엔드 선택은 **`CoreUnitViewPool.TrySpawn` 한 곳**(스프라이트 모션이 있으면 스프라이트, 비면 Spine). 무기 궤적은 `CoreSpriteUnitView` 가 붙인다(`WeaponTrailRig`) |
| 뷰 순서 | `ViewOrder.Unit`(비행·퇴근 포함) · 체력 = `ViewOrder.Overhead` | |
| 체력 · 오버헤드 | `CoreUnitOverheadUiLayer`(+카드 아이콘 줄 `CoreUnitOverheadUiLayer.RebuildCardView`) | 폴링이 아니라 사건 구독 |
| 소멸 회수 | `UnitDestroyed`(3) — 사망 모션 뒤 반납 · 판 경계 `MatchStarted` | |
| 씬 배선 | `BattleDriver._defenders` · `CoreUnitViewPool` · `CoreUnitOverheadUiLayer` · 트레이 `CoreDefenderTray` | |

이력: 옛 정거장 = `BattleBridge.PlaceDefenderAs`/`CreateDefenderEntity` → ECS `DefenderUnitTag` → `DefenderDeathEventsSingleton` → `SpineUnitPool`/`SyncMonoUnitViews`(unit 9 에서 삭제).

## 순찰 아군 (Patrol)

방어유닛 표에서 **갈라지는 정거장만** 적는다.

| 정거장 | 앵커 | 방어유닛과 무엇이 다른가 |
|---|---|---|
| 저작 SO | `DefenderUnitData` 재사용 + 소환사 쪽 `SummonPatrolAbility` | 소환수는 `DefenderCatalog` 에 등록하지 않는다. 담당 구역 반경 = 소환사 사거리 |
| 코어 스폰 · 사건 | `CombatPhase.SpawnPatrol` → `UnitSpawned`(2) · 디버그 = 커맨드 `DebugSummonPatrol`(24, `CoreSummonDebugMenu`) | 배치 점유·각성치·사직서 드랍 대상이 아니다(배치로 서지 않았으므로) |
| 뷰 풀 | `CoreUnitViewPool` 그대로 | 이동하는 아군이라 걷기 모션을 쓴다 |
| 씬 배선 | N/A — 신규 SerializeField 0 | |

이력: 옛 `BattleBridge.CreatePatrolEntity` · `PatrolRequestCarrier` · `SyncPatrolViews`.

## 적 (Enemy)

| 정거장 | 앵커 | 확인 포인트 |
|---|---|---|
| 저작 SO | `AttackUnitData` · 웨이브 = `AttackDeck`/`WavePlanAsset`/`WaveConceptData` | 새 적·등장 조건은 `enemy-wave-integration` 스킬 필수 |
| 정의표 행 | `MatchDefinitionBuilder.CollectEnemies` → `EnemyDef` · 공격 = `CombatDefinitionBuilder.BuildEnemyAttack` · 웨이브 = `MatchDefinitionBuilder.ToDeckDef`/`ToPlanDef`/`ToBonusDef` | 적 목록 순서가 정의표 번호다 — 재현(modeId + seed)이 여기에 기댄다 |
| 코어 스폰 · 사건 | `WaveScheduler` → `EnemySpawn.At` → `UnitSpawned`(2) · 웨이브 `WaveQueued`(20)/`WaveStarted`(21) · 분열 `EnemySplit` · 골 도달 `GoalReached`(5) · 감지 `Detected`(6) | 보스는 `UnitSpawned` 의 정의표 번호로 판별한다(`CoreBossWarning`) |
| 뷰 풀 | `CoreUnitViewPool` · 히트바 `CoreEnemyHitBarSpawner`(`DamageApplied`) · 감지 표식 `CoreVfxSpawner`(`Detected`) · 스폰 예고선 `CoreSpawnAlertPresenter`(`WaveScheduler.CollectForecast` 폴링) · 보스 경보 `CoreBossWarning` | 예고선은 사건 구독이 아니라 매 프레임 읽기다 — 예고는 「아직 안 일어난 일」이라 사건이 없다 |
| 뷰 순서 | `ViewOrder.Unit` · 히트바 `ViewOrder.Damage` · 경보 `ViewOrder.Overhead` | |
| 소멸 회수 | `UnitDestroyed`(3)(처치·유출 모두) | |
| 씬 배선 | `BattleDriver` 의 덱·플랜·보너스 필드 · `CoreSpawnAlertPresenter` · `CoreBossWarning` | |

이력: 옛 `BattleBridge.SpawnUnit`/`QueueDueWaves` · `SyncMonoUnitViews` · `SpawnAlertPresenter`.

## 투사체 (Projectile)

| 정거장 | 앵커 | 확인 포인트 |
|---|---|---|
| 저작 SO | `ProjectileData` · 발사 명세 `ProjectilePatternData` | 착탄 효과(부여·스택)는 탄 SO |
| 정의표 행 | `CombatDefinitionBuilder.ToDef` → `ProjectileDef` · `PatternDef` | 선정 규칙 열거 번호 어긋남(12 중 11 오독) 선례 — `CombatDefinitionBuilder.ToCoreSelection` 핀 테스트 |
| 코어 스폰 · 사건 | `BattleWorld.SpawnProjectile` → `ProjectileSpawned`(10) · `ProjectileHit`(12) · `ProjectileDespawned`(11) · 착탄 예고 = `ProjectileSpawned` 의 비행 시간·반경 | 즉발 폭발도 탄 파이프라인을 탈 수 있다 — 판정 원점의 몸은 사건이 실어 온다(제약 13) |
| 뷰 풀 | `CoreProjectileViewPool` · 총구·착탄 VFX `CoreVfxSpawner` · 착탄 예고 링 `CoreMapOverlay.ShowTelegraph` | |
| 뷰 순서 | `ViewOrder.Projectile` · VFX `ViewOrder.Effect` | 유닛 뷰가 선 뒤라야 총구 앵커를 묻는다 |
| 소멸 회수 | `ProjectileDespawned`(11) | |
| 씬 배선 | `CoreProjectileViewPool` · `MatchViewAssets` | |

이력: 옛 `ProjectileSystem` · `ProjectileHitEventsSingleton` · `ProjectileViewPool`.

## 거점 — 골 타워 · 본능 · 적 마음 (Structure)

| 정거장 | 앵커 | 확인 포인트 |
|---|---|---|
| 저작 SO | 스테이지 프리팹의 마커(`StructureMarker`·`GoalMarker`) + `Data/Structures/*` | 스테이지 프리팹이 곧 정본(bake 없음) |
| 정의표 행 | `CombatDefinitionBuilder.FillStructures` → `StructureDef` · 마음 = `MatchDefinitionBuilder.ToHeartConfig` → `HeartDef` | |
| 코어 스폰 · 사건 | `BattleWorld.SpawnStructure` → `UnitSpawned`(2) · 마음 `HeartChanged`(29)/`HeartCollapsed`(30) | |
| 뷰 풀 | `CoreStructurePropLayer` · 마음 게이지 = `CoreScoreHud`/`HeartHudConfig` | ⚠ `HeartCollapsed` 의 붕괴 연출 구독자는 **없다**(8c 발견 · `bridge-methods` 「미실현」) |
| 소멸 회수 | `UnitDestroyed`(3) | |
| 씬 배선 | 스테이지 프리팹(`BattleDriver` 가 `MapStagePool` 에서 고른다) | |

이력: 옛 `BattleBridge` 골 드레인 · `GoalCollapsedEventsSingleton` · `PlayCoreBurst`.

## 존 해저드 · 길막 (Zone / Blocking hazard)

| 정거장 | 앵커 | 확인 포인트 |
|---|---|---|
| 저작 SO | `HazardSO` · `BlockingHazardSO`(8c 에 `Data/Authoring/` 으로 이사) · 캐스트 능력 `HazardCastAbility` | 길막 프리팹 2 에 `BlockingHazardPresenter` 가 붙어 있다(새 층은 부르지 않는다 — Missing Script 방지로 남긴 것) |
| 정의표 행 | `BoardEffectDefinitionBuilder.ToHazardDefs` → `HazardDef` · `BoardEffectDefinitionBuilder.ToBlockingHazardDefs` → `BlockingHazardDef` | 모양 = `BoardEffectDefinitionBuilder.ToCoreShape` · 원소 = `BoardEffectDefinitionBuilder.ToCoreDotElement` |
| 코어 스폰 · 사건 | 존 = `BattleWorld.SpawnHazard` → `HazardSpawned`(44)/`HazardDestroyed`(45) · 길막 = `BlockerSpawn` → `UnitSpawned`(2, 길막 종류) · 디버그 커맨드 17·18(`CoreHazardDebugMenu`) | 길막은 **유닛**이다(부술 수 있는 벽) — 그래서 스폰·소멸이 유닛 사건이다 |
| 뷰 풀 | `CoreHazardViewPool`(장판 그림 + 길막 프리팹 `Instantiate` · 스폰/파괴 VFX) | 스폰 VFX 는 SO 의 것이다(프리젠터에 안 넘겨 죽은 저작이 됐던 선례) |
| 뷰 순서 | `ViewOrder.Board` | 바닥은 유닛보다 먼저 선다 |
| 소멸 회수 | `HazardDestroyed`(45) · 길막 `UnitDestroyed`(3) · `MatchStarted` | |
| 씬 배선 | `BattleDriver._hazards` · `MatchViewAssets` · `CoreHazardViewPool` | |

이력: 옛 `EffectSpawner` · `HazardRuntimeEventsSingleton` · `BattleBridge` 길막 비주얼 맵.

## 장 캐리어 — 회오리 · 포탈 · 아군 버프 장 (Field)

| 정거장 | 앵커 | 확인 포인트 |
|---|---|---|
| 저작 SO | 카드·스킬 SO(`SkillData` · `DreamcatcherCard`) | |
| 정의표 행 | `CardDefinitionBuilder.Fill` · 스킬 = `BindingDefinitionBuilder` → `HazardDef` 공유 | |
| 코어 스폰 · 사건 | `BattleWorld.SpawnField` → `FieldSpawned`(46)/`FieldDespawned`(47) | 사건이 출구(`SiteTarget`)·반경 칸·지속을 싣는다 |
| 뷰 풀 | `CoreFieldPresenter`(반경 = `CoreDrawRadius.AreaTiles`) | 뷰는 반경을 재지 않는다 |
| 뷰 순서 | `ViewOrder.Effect` | |
| 소멸 회수 | `FieldDespawned`(47) · `MatchEnded`/`MatchStarted` | |
| 씬 배선 | `CoreFieldPresenter` | |

이력: 옛 `TornadoField`/`PortalLink`/`AllyBuffField` 캐리어 + 브리지 `CastSkillAtTile`/`CastPortal` 이 그렸다.

## 효과 타일 (Effect tile)

| 정거장 | 앵커 | 확인 포인트 |
|---|---|---|
| 저작 SO | `EffectTileData` · 시즌 맵 테마(`effectTiles`·`effectTileCount`) | |
| 정의표 행 | `BoardEffectDefinitionBuilder.FillEffectTiles` → `EffectTileDef` | 스테이지 `suppressEffectTiles` 존중 |
| 코어 스폰 · 사건 | `PlacementService.ArmedEffectTiles`(판 시작에 뽑고 판 내내 불변) · 적용은 배치 활성화 엣지 · 퇴근 회수 | 판정은 앵커 칸 하나 |
| 뷰 풀 | ⚠ **없다** — 규칙은 돌지만 판 위에 어느 칸인지 그리지 않는다(8c 발견 · `rule-holders` T15 「미실현」) | |
| 씬 배선 | N/A — 뷰가 없다 | |

이력: 옛 `TilemapMapView.SetEffectTile`(미러) ↔ `BattleBridge._effectTilesByCell`(소유).

## 픽업 — 레드불 / 사직서 (Pickup · Resignation)

| 정거장 | 앵커 | 확인 포인트 |
|---|---|---|
| 저작 SO | 시즌 기믹 SO · 뷰 = `PickupViewConfig` | 기믹이 뽑힌 판에서만 산다(기본 모드는 기믹 0) |
| 정의표 행 | `MatchDefinitionBuilder.ToGimmickDefs` → `GimmickDef` | |
| 코어 스폰 · 사건 | `GimmickHost` → `BattleWorld.SpawnPickup` → `PickupSpawned`(48)/`PickupTaken`(49)/`PickupExpired`(52) · 사직서 `BattleWorld.DropResignation` → `ResignationDropped`(50)/`ResignationThreshold`(51)/`ResignationConsumed`(53) · 디버그 커맨드 19~21(`CoreGimmickDebugMenu`) | 픽업 판정은 「칸 반폭 + 내 몸」 자(제약 13) |
| 뷰 풀 | `CorePickupViewPool`(+`CorePickupPresenter`) · `CoreResignationViewPool`(+`CoreResignationPresenter`) | |
| 뷰 순서 | `ViewOrder.Board` | |
| 소멸 회수 | `PickupTaken`·`PickupExpired`·`ResignationConsumed` · `MatchStarted` | |
| 씬 배선 | 두 풀 컴포넌트 · `PickupViewConfig` | |

이력: 옛 `ReconcilePickupViews`·`ReconcileResignationViews` 폴링.

## 드림캐쳐 카드 — 부착 · 시전 (Card)

| 정거장 | 앵커 | 확인 포인트 |
|---|---|---|
| 저작 SO | `DreamcatcherCard` · `DreamcatcherCardCatalog` · 드림스톤 | 문안은 `DreamcatcherCardText`(로비·새 층 공유) |
| 정의표 행 | `CardDefinitionBuilder.Fill` → `CardDef` · 덱 = `CoreDeckComposition.Compose`(확정 덱 + 판 시드 액티브 롤) | 52장 자동 증언 = `CardProbe`(7e) |
| 코어 스폰 · 사건 | `HandDeck.TryAttach`/`HandDeck.TryCast` → `CardAttached`(60)/`CardDetached`(61)/`CardCast`(62) · 규칙 = `BindingRegistry` → `BindingAttached`(57)/`TriggerFired`(56)/`SkillVisual`(59) | 트랜잭션 = ① 적용 → ② 차감 → ③ 순환 |
| 뷰 풀 | 손패 `CoreHandView`·`CoreCardDragSlot`·`CoreCardFocusPresenter` · 각성 항아리 `CoreAwakeningGaugeView` · 선택 패널 `CoreSelectionPanel` · 부착 범위 링 `CoreMapOverlay.ShowAttachRange` · 표식·오라 `CoreStatusFxSpawner`·`CoreDcAuraVisualPool` · 발동 임팩트·빔 `CoreVfxSpawner`·`CoreBeamPresenter` | 부착 범위 링은 사건 구독자가 아니다 — 손패 드래그가 오버레이에 민다 |
| 뷰 순서 | `ViewOrder.Hand` · 표식 `ViewOrder.Status` · 오버헤드 카드 줄 `ViewOrder.Overhead` | 카드 사건 한 건이 몸에 붙는 것을 먼저 세운 뒤 손패가 창을 다시 읽는다 |
| 소멸 회수 | `CardDetached`(61) · 숙주 `UnitDestroyed` → `HandDeck.Recover` | |
| 씬 배선 | `BattleDriver._cards`(dev 덱 — 비우면 프로필 경로) · 손패 캔버스 · `CoreSelectionPanel._defenderCatalog` | |

이력: 옛 `DreamcatcherHandController` · `DreamcatcherHandView` · `DcInspectController` · `DcIconStripSpawner`.

## 상태 표식 · 오라 (Status FX)

| 정거장 | 앵커 | 확인 포인트 |
|---|---|---|
| 저작 SO | `StatusFxRegistry` · `StackModifierSO` · `DcVisualConfig` | |
| 정의표 행 | 스택 규칙 = `BattleDriver._stackModifiers` → `StackRuleDef` | |
| 코어 사건 | `ModifierApplied`(34)/`ModifierRevoked`(35) · `StackChanged`(36)/`StackThreshold`(37) · `CcApplied`(38)/`CcCleared`(39) · `DotApplied`(40)/`DotCleared`(42) · `ShieldGranted`(41)/`ShieldBroken`(15) · `AggroAcquired`(7)/`AggroReleased`(54) · `LastRunEnded`(55) | 한 몸에 상태가 여럿일 때 무엇이 이겨 보이나는 데이터(6c) |
| 뷰 풀 | `CoreStatusFxSpawner`(+`CoreStatusFxView`) · `CoreDcAuraVisualPool` | |
| 뷰 순서 | `ViewOrder.Status` | 유닛 뒤 — 같은 틱에 태어난 유닛의 앵커가 선 뒤 |
| 소멸 회수 | 각 `…Revoked`/`…Cleared` 짝 · 숙주 `UnitDestroyed`/`UnitSlain` · `MatchStarted` | |
| 씬 배선 | 두 컴포넌트 | |

이력: 옛 `ReconcileStatusFx` 폴링 · `StatModifierApplyEventsSingleton`.

## 도약 — 보스 도약 · 궁극기 강습 (Leap)

| 정거장 | 앵커 | 확인 포인트 |
|---|---|---|
| 저작 SO | 보스·궁극기 능력 SO · 뷰 = `LeapVisualConfig` | |
| 정의표 행 | `BindingDefinitionBuilder`(궁극기 fireCap 1) | |
| 코어 사건 | `CombatPhase` 도약 단계 → `LeapAscend`(18)/`LeapDescend`(19) · 순간이동 `Blinked`(8) | 판정(착지 슬램 · 순간이동)은 코어가 이미 끝냈다 — 뷰는 비행만 |
| 뷰 풀 | `CoreLeapPresenter`(`CoreLeapPresenter.TryGetFlightOverride` 로 유닛 뷰 위치를 덮어쓴다) | ⚠ 궁극기 **착지 예고 칸**은 그리지 않는다(8c 발견 · T16 「미실현」) |
| 뷰 순서 | `ViewOrder.Leap` — 유닛 동기보다 **먼저**(X3 · 1프레임 팝 방지) | |
| 씬 배선 | `CoreLeapPresenter` · `LeapVisualConfig` | |

이력: 옛 `BossLeapVisualEventsSingleton`·`UltimateLeapVisualEventsSingleton` · `BattleBridge.RunBossLeap`.

## 보너스 웨이브 포탈 (Bonus portal)

| 정거장 | 앵커 | 확인 포인트 |
|---|---|---|
| 저작 SO | `BonusWaveData` | |
| 정의표 행 | `MatchDefinitionBuilder.ToBonusDef` → `BonusWaveDef` | |
| 코어 사건 | `WaveScheduler` → `BonusOffered`(22)/`BonusPulled`(23) | 온보딩 판은 당김 억제(`WaveScheduler.BonusPullSuppressed`) |
| 뷰 풀 | `CoreBonusPortalPresenter`(`CoreBonusPortalPresenter._portalPrefab`) · 당김 UI `CoreNextWaveDock` | 8a 실현(초판 배정 「unit 6 의 보너스 뷰」는 실체가 없었다) |
| 뷰 순서 | `ViewOrder.Board` | |
| 소멸 회수 | `MatchStarted` · `CoreBonusPortalPresenter.Clear` | |
| 씬 배선 | `CoreBonusPortalPresenter` | |

이력: 옛 `BattleBridge.OpenBonusPortals`.

## VFX 원샷 · 빔 · 피해 숫자

| 정거장 | 앵커 | 확인 포인트 |
|---|---|---|
| 저작 SO | 유닛 SO 의 VFX 프리팹 칸 · `DcVisualConfig` · `DamageNumberStyle` | 벤더 VFX 는 `Assets/_Project` 사본을 쓴다(리포에 없는 팩 선례) |
| 코어 사건 | `AttackResolved`(9) · `ProjectileHit`(12) · `HealApplied`(14) · `Placed`(25)/`DefenderActivated`(28) · `SkillVisual`(59) · `TriggerFired`(56) · `DamageApplied`(13) | 빔은 고속 틱 공격 사건을 TTL 세션으로 뭉친 **뷰의 개념**이다 |
| 뷰 풀 | `CoreVfxSpawner` · `CoreBeamPresenter` · `CoreDamageNumberSpawner` | 무기 궤적은 유닛 뷰가 붙인다(`CoreSpriteUnitView`) |
| 뷰 순서 | `ViewOrder.Effect` · 숫자 `ViewOrder.Damage` | |
| 소멸 회수 | 원샷은 자기 수명(파티클 길이 상한) · 빔은 `UnitDestroyed`/`UnitSlain` · `MatchStarted` | 자기소멸 없는 벤더 VFX 가 판에 쌓인 선례 — 수명 상한 필수 |
| 씬 배선 | 세 컴포넌트 | |

이력: 옛 `VfxSpawner` · `BeamPresenter` · `DamageNumberSpawner`(드레인 구동).

## 맵 오버레이 — 격자 · 배치 가이드 · 사거리 · 예고 (Overlay)

| 정거장 | 앵커 | 확인 포인트 |
|---|---|---|
| 저작 SO | 타일 세트(`CoreMapOverlay._tileSet`) · 스테이지 프리팹 | |
| 정의표 행 | `MatchDefinitionBuilder.BuildMap` → `MapSnapshot` | |
| 코어 읽기 | `PlacementService`(칸의 상태) · `MapRuntime` | 사건 구독이 아니라 **입력이 민다** — 드래그 중에만 그린다 |
| 뷰 | `CoreMapOverlay`(`ShowPlacement`·`PaintRange`·`ShowAimRing`·`ShowTelegraph`·`ShowBriefing`) · 평면 `CoreBoardPlane` · 판 경계 `CorePhaseFeed` | 도달 판정은 `AttackReach.InReach` **호출만**(제약 13) — 뷰가 자를 새로 만들지 않는다 |
| 씬 배선 | `CoreMapOverlay` · `CoreBoardPlane` | |

이력: 옛 `TilemapMapView`(1,608줄).

## 맵 스테이지 · 프랍 (Stage)

| 정거장 | 앵커 | 확인 포인트 |
|---|---|---|
| 저작 | `MapStage` 프리팹(`Art/Theme/<맵>/`) · 풀 `MapStagePool` · 볼륨 프로필은 프리팹 옆(8c 이사) | bake 없음 — 프리팹이 정본(`map-stage-authoring.md`) |
| 정의표 행 | `MatchDefinitionBuilder.BuildMap`(마커 스캔 → 칸·레인·거점) | |
| 생성 | `BattleDriver.Begin` → 스테이지 `Instantiate` · 회수 `BattleDriver.TeardownStage` | |
| 뷰 | 마커 프랍 `MarkerPropInstaller` · 거점 `CoreStructurePropLayer` | |
| 씬 배선 | `BattleDriver._mapPool` · `MarkerPropInstaller.style` | |

## 유지 규칙

- 이 표의 심볼이 코드에서 사라지면 같은 커밋에서 표를 고친다. unit 9 의 옛 전투 삭제 뒤에는 「모든 심볼이 남아 있다」를 grep 으로 다시 확인한다.
- 새 사건 종류를 열면 `CoreEventKind` 번호와 트레이스 정거장(`CoreTrace`)을 같이 연다(추가 제약 「로깅은 첫 축」).
- 「⚠ 없다」로 적힌 정거장(효과 타일 그림 · 착지 예고 · 붕괴 연출)은 8c 가 찾은 옛 기능의 공백이다. 처분이 정해지면 이 표를 고친다.
