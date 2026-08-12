# Object Pipeline Map — 플레이 오브젝트 생성→렌더 정거장 체크표

> **대조용 문서다.** 플레이 오브젝트를 신설하거나 생성→렌더 경로를 바꾸는 spec 의 README 를 쓸 때, 아래에서 가장 가까운 아키타입 표를 복사해 `파이프라인 커버리지` 섹션으로 붙인다. 해당 없는 정거장은 빈 칸이 아니라 **`N/A + 이유`** 를 적는다 (빈 칸은 "잊었음"과 "필요 없음"을 구분하지 못한다). 대조 중 표가 실제 코드와 어긋나면 **그 자리에서 이 문서를 고친다.**
>
> 앵커 경로는 `Assets/_Project/Scripts/` 기준. 구현 상세의 source of truth 는 코드 — 이 문서는 정거장 유무만 답한다.

**공통 정거장 어휘**: 데이터 SO → 스폰 진입점 → ECS 컴포넌트(소유 맥락) → 시뮬 시스템 → 이벤트 큐(생성·drain·Dispose 3종 확인) → View/Pool → 씬 wiring(Play 검증까지).

정거장별 시공법 스킬: 씬 wiring = `unity-feature-wiring` · VFX 저작/통합 = `unity-vfx-authoring`/`unity-vfx-integration` · 프랍/타일 = `unity-prop-tile-authoring`.

---

## 방어 유닛 (Defender)

| 정거장 | 앵커 | 확인 포인트 |
|---|---|---|
| 데이터 SO | `Data/DefenderUnitData.cs` (+`DefenderCatalog.cs`) · 고유능력 = `Data/Abilities/`(`DefenderAbilityData` 서브에셋, 유닛 `abilities` 리스트로 참조) | 공통 스탯(체력/사거리/쿨다운/코스트)은 유닛 SO, **능력별 파라미터**(volley/hazard/shield/bomb)는 능력 서브에셋. bake = `CreateDefenderEntity` 가 `GetAbility<T>()` 로 해석(defender-ability-assets). 신규 유닛은 **DefenderCatalog 등록까지** (미등록 = 로스터 미노출) |
| 스폰 진입점 | `Bridge/BattleBridge.cs` `PlaceDefenderAs`→`CreateDefenderEntity` | 플레이어 배치 기반 |
| ECS 컴포넌트 (Units) | `Battle/Units/` DefenderUnitTag·Health·IncomingDamage·DefenderTile | 능력별 조건부: AttackState / HazardCastState / AggroProvider / DeployedFacing(방향 지정 배치 — 활성화 시 1회 기록) / VolleyFireState(Combat 소유, shotCount>1 만) |
| 시뮬 시스템 | `Battle/Combat/AttackSystem.cs` · `Battle/Units/DamageApplicationSystem.cs`·`HealthDeathSystem.cs` | **타일 배치 방어유닛은** 이동 없음(고정) — PathFollowState 미부여. ★이것은 더 이상 「방어 진영 전체」의 성질이 아니다 — 순찰병이 `PathFollowState` 를 갖고 walk 위를 걷는다(아래 **순찰 아군** 아키타입). 「방어유닛이면 안 움직인다」에 기대는 코드를 새로 쓰지 말 것 |
| 이벤트 큐 | `Battle/Units/DefenderDeathEventsSingleton.cs` + 공유 UnitAttackVisual/DamageNumber/HealApplied · 연출 전용 `Battle/Combat/KnockupVisualEvents.cs` | drain = `BattleBridge.DrainDefenderDeathEvents`. ★넉업 채널은 **연출 귀속용** — 심의 넉업은 Stun 이라 뷰가 `CcEffect.kind` 로는 일반 스턴과 구분 못 한다(knockup-fighter-defender unit 3) |
| View/Pool | `Presentation/SpineUnitPool.cs`+`SpineUnitView.cs`, 폴백 `QuadUnitViewPool.cs` · 지속 빔 = `Presentation/BeamPresenter.cs` | 위치/틴트 sync = `BattleBridge.SyncMonoUnitViews` 매 프레임. ★빔은 고속 틱 공격 사건을 TTL 세션으로 뭉친 결과이지 심 개념이 아니다. 빔 유닛 판별 = SO `beamVfxPrefab` 유무(beam-ranger-defender unit 1). ★**리그는 두 분기다** — `partSkins` 가 비면 고유 스켈레톤, 차 있으면 `Casual Character` 파츠 합성(`SpineCombinedSkinCache.ResolveSkin`). 같은 진입점이고 유닛 구조·로직은 어느 쪽도 모른다. 고유 리그 저작은 **코드 0** 이며 facing 규약이 반대인 리그는 `SkeletonFlipXModifier` 로 데이터에서 정규화한다(코드로 분기 금지) — summon-patrol-defender unit 8 |
| 체력 표시 | 기본: `Presentation/UnitOverheadUiLayer.cs`+`UnitOverheadView.cs` / Legacy: `TileHealthGaugeLayer.cs`+`TileHealthGaugeView.cs` | ★큐 아님 — `BattleBridge.SyncMonoUnitViews`가 매 프레임 Health read-only 폴링 |
| 씬 wiring | BattleBridge SerializeField: spineUnitPool·defenderFallbackViewPool·unitOverheadUiLayer·tileHealthGaugeLayer | Unified/Legacy 상호배타, Spine 실패 시 Quad 폴백 |

## 순찰 아군 (Patrol — summon-patrol-defender, 2026-08-12)

**아군인데 walk 위를 이동하는 첫 유닛.** 위 방어유닛 아키타입에서 **갈라지는 정거장만** 적는다 — 나머지는 방어유닛 표를 그대로 따른다. 이동형 아군을 또 만들면 이 표를 복사한다.

| 정거장 | 앵커 | 방어유닛과 무엇이 다른가 |
|---|---|---|
| 데이터 SO | `Data/DefenderUnitData.cs` **재사용** + `Data/Abilities/SummonPatrolAbility.cs`(소환사 쪽) | **신규 SO 타입을 만들지 않았다** — `ISpineUnitVisualData` 구현체 3번째의 확장 비용이 근거. 갭은 필드 2개(`moveSpeed`·`SpineWalkAnimation`)를 **맨 뒤에 덧붙여** 메웠다. ★소환수는 `DefenderCatalog` 에 **등록하지 않는다**(미등록 = 로스터 미노출). 담당 구역 반경은 별도 필드가 아니라 소환사 `attackRange` |
| 스폰 진입점 | `BattleBridge.CreatePatrolEntity` (소환 발화 + 디버그 메뉴 2경로) | `CreateDefenderEntity` 를 **재사용하지 않는다** — 그쪽은 `_defenderByTile` 등록과 `DefenderTile` 부착을 한다. 요청 전달은 신규 채널이 아니라 `ProjectileRequestCarrier` 와 같은 **캐리어 엔티티** 관용구(`PatrolRequestCarrier`, 수명 1프레임) |
| ECS 컴포넌트 | 신규 4: `PatrolAnchor`(Movement) · `PatrolStep`(Effects) · `SummonerState`(Combat) · `SummonedBy`(Units) | ★**태그 조합이 계약이다.** `DefenderUnitTag`+`DefenderClassTag` 는 **붙이고**, `DefenderTile`·`AttackUnitTag` 는 **안 붙인다**. 각각의 귀결: 태그 부착 → 매치 경계 정리·힐/실드 편입·클래스 하드 타게팅 정상 / 미부착 → 배치 점유·각성치·**사직서 무한 드랍** 차단. 나중에 `DefenderTile` 을 붙이면 반복 사망 파밍이 조용히 열린다 |
| 시뮬 시스템 | 신규 2: `Battle/Effects/PatrolFieldSystem.cs` · `Battle/Units/PatrolLifecycleSystem.cs` · 순수 함수 `Battle/Effects/PatrolAreaMath.cs` | 이동은 **기존 BFS·하강을 재사용**한다 — 박스 제약을 walkMask 마스킹으로 표현해 `AggroChaseMath.BuildChaseField` 에 넘긴다. **8-이웃 그리디 금지**(`aggro-tile-chase` 가 벽 고착으로 폐기). 수정 3: `MovementSystem`(dir 분기 + **goal 게이트**) · `ZoneApplySystem`(진영 게이트) · `AttackSystem`(소환 발화) |
| 이벤트 큐 | **신규 채널 0** | 캐리어 엔티티 관용구라 싱글턴 배선도 CLAUDE.md 채널 목록 갱신도 불요. `DefenderDeathEventsSingleton` 은 `DefenderTile` 미부착으로 미발행 |
| View/Pool | 기존 `SpineUnitPool` 재사용 · **전용 sync 루프 `BattleBridge.SyncPatrolViews`** | ★없으면 **뷰가 스폰만 되고 영원히 제자리에 선다.** `SyncMonoUnitViews` 의 두 루프는 `AttackUnitTag` 쿼리(적)와 `_defenderByTile` 순회(방어유닛)인데 순찰병은 **둘 다 아니다**. walk 애니는 `SpineWalkAnimation` 을 채워 활성화(타일 방어유닛은 `""`). death 애니는 도달하지 않는다 — `Kill()` 구동원이 `_defenderByTile` 기반 |
| 체력 표시 | `UnitOverheadUiLayer`/`UnitOverheadView` 기존 폴링을 `SyncPatrolViews` 안에서 호출 | 숨기지 않는다 — 죽고 다시 나는 것이 이 유닛의 핵심 피드백 |
| 매치 경계 정리 | `BattleBridge.DestroyBattleEntities` — 순찰병 + 요청 캐리어 | `DefenderUnitTag` 부착으로 자동 포함되나 **회귀 방지로 명시 등재**했다. 타입 기반 파괴라 태그가 빠지면 앱 수명 default world 에 잔존한다 |
| 씬 wiring | **N/A — 신규 SerializeField 0** | 기존 `spineUnitPool`/`unitOverheadUiLayer` 를 그대로 쓴다 |
| 외력 예외 | — | 포털·토네이도·넉백은 faction 을 보지 않아 순찰병을 박스 밖으로 민다. 계약은 *«자기주도 이동은 박스를 안 벗어난다. 외력은 벗어날 수 있고 다음 틱에 복귀 경로가 잡힌다»* — 필드 계산이 **박스 밖 시작** 입력을 다뤄야 한다 |

## 적 (Enemy)

| 정거장 | 앵커 | 확인 포인트 |
|---|---|---|
| 데이터 SO | `Data/AttackUnitData.cs` (+`EnemyCatalog.cs`) | ★적 스탯 SO 이름이 **AttackUnitData** — "EnemyData" 는 없음. 신규 적은 **EnemyCatalog + AttackDeck/웨이브 pool 노출까지**. ⚠ **풀에 1종을 더하면 그 덱의 웨이브가 전부 재추첨된다** — `WavePatternGenerator` 가 `rng.NextInt(0, pool.Count)` 로 뽑아 `waveSeed` 고정이어도 웨이브 1부터 구성이 바뀐다(시드를 갱신해 새 baseline 을 diff 에 드러낼 것). 삽입은 **풀 중간에** — 맨 뒤면 `ResolveWaveEligibleIndex` 의 전방 순환이 초반 웨이브를 `pool[0]` 로 쏠리게 한다. **라이브 덱은 7종**(`Serpent·Coil·Twin·Spiral·Zig·Hook·Endless`)이고 열거의 정본은 `WaveKillBudgetPinTests`. 수량 상한은 `maxPerWave`(0=무제한) — 없으면 한 종류가 일반 웨이브 최대 24기 / 보스 호위 3~4기로 나온다 |
| 스폰 진입점 | `Bridge/BattleBridge.cs` `SpawnUnit` | 웨이브 스케줄러가 `Data/AttackDeck.cs`·`WavePlanAsset.cs` 소비 |
| ECS 컴포넌트 | Units: AttackUnitTag·Health·IncomingDamage·CcEffect·DotEffect·**ShieldSlot·IncomingShield** · Movement: `PathFollowState` · Combat: AttackState·EnemyBehavior·EnemyAiState | ~~이동은 적 전용~~ — **순찰 아군이 깼다**(summon-patrol-defender, 2026-08-12). `PathFollowState`·`EnemyAiState`·`EnemyBehavior` 는 이름만 «Enemy» 이고 진영 중립이다. ★**실드 버퍼는 적 전원**(보스만이 아니다 — `boss-mamemo` unit 2, 마메모가 호위에게 실드를 준다). **쌍으로** 붙여야 한다: `IncomingShield` 드레인이 `ShieldSlot` 존재로 게이팅돼 있어 한쪽만 붙이면 부여가 영영 안 빠지고 버퍼가 무한 성장한다. 따름정리 — `DamageApplicationSystem` 의 실드 파열 감지가 **적에서도 참이 되므로** `OnShieldBreak` 를 적에 여는 것은 실행기 진영 파라미터화가 선행이다(`DcTrigger.EnemyTriggerArmed` 가 막고 `DcTriggerTests` 가 고정) |
| 시뮬 시스템 | `Battle/Movement/MovementSystem.cs`(flow-field) · `Battle/Combat/AttackSystem.cs`·`EnemyAiStateSystem.cs` | |
| 이벤트 큐 | `Battle/Units/EnemyKilledEventsSingleton.cs`·`GoalReachedEventsSingleton.cs` · `Battle/Effects/EnemyCcEvents.cs` | + 공유 UnitAttackVisual/DamageNumber |
| View/Pool | `Presentation/SpineUnitPool.cs`(공유) / `QuadUnitViewPool.cs`(enemyViewPool 인스턴스) | 저체력 틴트 = SyncMonoUnitViews 내 |
| 체력 표시 | 기본: `Presentation/UnitOverheadUiLayer.cs`+`UnitOverheadView.cs` / Legacy: `EnemyHitBarSpawner.cs`+`EnemyHitBarView.cs` | Unified는 매 프레임 Health read-only 폴링, Legacy 피격바는 **DamageNumberEventsSingleton 공유** drain |
| 씬 wiring | BattleBridge SerializeField: spineUnitPool·enemyViewPool·unitOverheadUiLayer·enemyHitBarSpawner·deck | Unified/Legacy 상호배타 |

## 투사체 (Projectile)

| 정거장 | 앵커 | 확인 포인트 |
|---|---|---|
| 데이터 SO | `Data/ProjectileData.cs`(탄 1발) + `Data/ProjectilePatternData.cs`(발사 명세) | 궤적(MovementKind) × 페이로드(PayloadKind) 2축. flightMode 가 이 2축으로 번역됨(`ResolveProjectileAxes`). **패턴 SO 는 "누구를·몇 발·어떤 간격" 만 소유하고 탄의 성질을 복제하지 않는다** — 새 효과는 `ProjectileData` 에 추가 |
| 스폰 진입점 | `Battle/Combat/AttackSystem.cs` 가 `ProjectileSpawnRequest` stage → `BattleBridge.DrainProjectileSpawnRequests`→`SpawnProjectile` | ★2단계 — ECS 는 request 만, 엔티티+뷰 생성은 Bridge. **stage 지점은 4곳**: RESOLVE(기본 공격·dc 니들) / 폭탄 발사 성사 / 캐스트 사건 드레인 / **`ProjectileEmitterSystem`(발사 명세)** — 전부 `ProjectileRequestCarrier` 캐리어를 공유한다(drain 이 스폰 후 파괴) |
| ECS 컴포넌트 (Combat) | `Battle/Combat/Projectile/` ProjectileState·ProjectileTag·ProjectileSpawnRequest + `Projectile/Emission/` EmitterInstance·PatternSlot | 페이로드별 조건부: PathHitRecord 버퍼(PathHit — 대상당 1회 스윕, drain 이 부착). Emission 버퍼 2개는 **패턴 host 전용**(패턴 없는 유닛엔 미부착) |
| 시뮬 시스템 | `ProjectileMoveSystem.cs`(궤적) · `ProjectileHitSystem.cs`(페이로드 — IncomingDamage/IncomingHeal 기입) · `Projectile/Emission/ProjectileEmitterSystem.cs`(발사 스케줄→요청) | emitter 는 개별 MovementKind 가 아니라 **바인딩 클래스**(`MovementBinding.Of` → Entity/Cell/Direction)로 분기 — 새 이동 수학이 기존 바인딩이면 emitter 무변경 |
| 이벤트 큐 | `Battle/Combat/Projectile/ProjectileHitEventsSingleton.cs` | drain = `DrainProjectileHitEvents` → PlayHit |
| View/Pool | `Presentation/ProjectileViewPool.cs` | 매 프레임 `SyncTransforms`; muzzle/cast VFX 도 이 풀 (PlayHit/PlayCast, UnitAttackVisualEvents drain) |
| 씬 wiring | BattleBridge `_projectileViewPool` | |

## 거점 — 골 타워·본능·적 마음 (battle-structures, 2026-08-10 현행화)

| 정거장 | 앵커 | 확인 포인트 |
|---|---|---|
| 데이터 SO | 방어 마음(골 타워): `Data/AttackDeck.cs` `goalStabilityMax`(HP)+`goals[]`(셀) · 본능·적 마음: `Data/StructureData.cs` + `Data/MapGrid/MapDocument.cs` `structures[]`(셀×편×SO) | **HP 소스가 스폰 소스로 갈린다** — goals[]=덱, structures[]=SO. 진영은 (편×종류) 파생(`StructurePlacements.DeriveFaction`) — 거점 아닌 비트가 나올 수 없다. 저작 규칙(모드·겹침·(Defender,Core) 금지·중립 금지·아군사격)은 `StructureAuthoringRules` 가 단일 소유 — 페인터와 `MapDocument.OnValidate` 가 같은 함수 호출 |
| 스폰 진입점 | `Bridge/BattleBridge.cs` `SpawnStructureEntities`(StartBattle) | 판 시작 1회 — 요청 큐 없음. `_resolvedMapDoc`(빌드가 보관, teardown/fallback 에서 null)에서 SO 스탯을 읽는다. `(Defender, Core)` 는 스폰에서도 거부(골 두 벌 최후 방어선) |
| ECS 컴포넌트 (Units) | `StructureTag`(cell+faction — 전 거점) + FactionTag(`DefenderCore`/`EnemyCore`/`*Instinct`)·Health·IncomingDamage·LocalTransform · 방어 마음은 `GoalTowerTag` 추가(패배 판정용) · 본능은 `BlockingHazardCellsBuffer` 3×3 + (공격 저작 시) AttackState·출력·ProjectileRef | 체력은 **거점 단위**(공유 풀 아님 — 계약 7). CC·모디파이어 버퍼 미부여(계약 8). ★**버퍼 보유 = 다중셀 점유 선언** — `ObstacleLifetimeSystem` 이 `BlockingHazard` 컴포넌트가 아니라 버퍼로 blockedCells 를 만든다(리뷰 C-1 정정) |
| 시뮬 시스템 | 전용 시스템 **0** — 피해는 표준 경로(`DamageApplicationSystem`→DeadTag→`UnitLifecycleSystem` 일반 사망 루프), 본능 공격은 `AttackSystem` 통합 루프(계약 10), 통행은 `ObstacleLifetimeSystem`+`FlowFieldRebuildSystem` 기존 소비 | 마음은 공격·이동 없음(AttackState/PathFollowState 미부여). 거점은 **거리순 일반 후보**(타입 우선순위 없음, 계약 4 폐기) |
| 타겟 후보 진입 | 양쪽 다 **저작 마스크**다 — 적: `EnemyTargetFilter.factionMask`+`EnemyTargetDefaults`(unit 1) · 방어: `DefenderUnitData.targetFactions`+`DefenderTargetDefaults`(unit 8, 기본 `AnyEnemy`) | `AttackSystem` 후보 쿼리(FactionTag+Health+LocalTransform)는 **거점을 이미 담고 있다** — 막던 것은 마스크 리터럴이었다. 아군 타게팅(`targetAllies`)은 `DefenderUnit` **단독** — 넓히면 `IncomingHeal` 버퍼 없는 거점이 후보에 들어 ECB playback 에서 던진다 |
| 광역 피해자 진입 | `ProjectileHitSystem` TileAoe — 피해자 풀 **한 벌** + `FactionTag` 진영 비트 필터(`AnyDefender`/`AnyEnemy`) | ★진영 대칭(unit 9). `GoalTowerTag` 특례 은퇴 — 거점은 `StructureTag`+진영 비트로 걸리고 미래의 방어 본능도 코드 변경 0. splash·bounce·경로 스윕은 **적 유닛 풀만**(기존 의도 유지). `BlockingHazard` 는 두 그룹 어디에도 없어 광역 피해자가 아니다 |
| 붕괴 관측 | 브리지 `SyncGoalStability` — `_structureRegistry`(entity·cell·faction) 폴링으로 «사라진 엔티티의 셀» 특정 | **셀 단위**(ⓐ): 방어 마음 붕괴 → `_breachedCells` + 그 셀만 유출 전환(`OpenGoalCellAfterBreach`). **본능**은 연출·로그만. ★열기는 미러 갱신 **뒤**(리뷰 A-M1 — 제출값 순서). 붕괴 프레임 미러=0, 다음 프레임부터 생존 골 최저 |
| 승패 축 | 같은 순회가 적 마음 잔여도 모은다 → `_enemyCoreCurrent`. 판정은 `CheckEnemyCoreDestroyed`(Update 에서 Sync **다음**) · 만료 비교는 `CheckTimer` | ★**모드 분기 없음**(계약 15). 축 활성 = 「저작된 상한 > 0」 — `_timerDuration`/`StressLimit`/`_goalStabilityMax` 와 같은 형태. 만료 판정 `_goalStability >= _enemyCoreCurrent` **한 줄**이 침략(적 잔여 0 → 항상 승리 = 기존 동치)과 공성을 통합. 두 마음 HP 는 저작으로 맞추고 `MapDocumentPool.OnValidate` 가 어긋남을 경고. 읽기 창구 `EnemyCoreCurrent/Max` |
| 이벤트 큐 | **N/A** — 붕괴 감지는 등록부 폴링. `GoalCollapsedEventsSingleton` 은 생산자 0 존치(페이로드가 골 인덱스 기준이라 거점 체계와 불일치 — 후속에서 재정의) | |
| View/Pool | 게이지: `SyncGoalOverheadGauges` 가 등록부 순회(defender 색 = 진영 파생) + HUD 바 `SyncGoalStabilityBars`(가장 위험한 골 미러) · 프랍: 골=`theme.goalStructureProp`, 거점=SO `viewPrefab` 을 브리지 Instantiate(Pickup 선례, `ClearStructureViews` 로 teardown) · 붕괴 원샷 `VfxSpawner.SpawnGoalCollapse` | Pool N/A(맵 수명) |
| 배치·통행 배제 | 맵 빌드 시 `CloseCellLayers` — 본능 footprint(**적대적** 본능은 +`HostileInstinctPlacementPadding` = 9×9, 술어 `StructurePlacements.IsHostileInstinct`) · 연결성: `MapConnectivity`+페인터 BFS 가 본능 footprint 를 벽으로(마음은 비차단, 계약 12) | 공성 모드는 파생 — 적 마음 셀 = spawns[](`ToGeneratedMap` 투영 1곳, 소비처 8곳 무변경). ⚠ 배제 여유 ≥ 본능 사거리면 **본능이 아무도 못 쏜다**(실측: 여유 4 = 사거리 4 → 최근접 합법 칸 거리 5). 마음은 본체 1칸만 닫혀 **인접 배치로 공성**이 성립한다 |
| 씬 wiring | 신규 SerializeField 0 — 기존 게이지/VFX 배선 재사용 | |

## 해저드 — Zone/Blocking (방어 유닛 HazardCast 능력)

| 정거장 | 앵커 | 확인 포인트 |
|---|---|---|
| 데이터 SO | `Data/HazardSO.cs`(Zone) / `Battle/Effects/BlockingHazardSO.cs` | visualPrefab·lifetime·파괴 VFX |
| 스폰 진입점 | `Battle/Effects/HazardCastSystem.cs` → HazardSpawnRequests 큐 → `BattleBridge.DrainHazardSpawnRequests` | staged-request drain (투사체와 동형) |
| ECS 컴포넌트 (Effects) | `Battle/Effects/` Hazard·HazardEffect·BlockingHazard·BlockingHazardCellsBuffer (`EffectSpawner.cs`) | |
| 시뮬 시스템 | `HazardLifetimeSystem.cs`·`ZoneApplySystem.cs`·`DotApplySystem.cs`·`CcApplySystem.cs` | |
| 이벤트 큐 | HazardSpawnRequests·HazardDestroyed(Blocking 파괴)·HazardRuntime Singleton + **CastEvents**(Effects→Combat) | ★HazardRuntimeEvents 는 **텔레메트리 로깅 전용** — VFX 트리거 아님. ★CastEvents 는 해저드 스폰과 무관 — 캐스트 성사를 **그 host 의 공격 사건**으로 Combat 에 넘기는 채널(캐스터는 `attackRange 0` 이라 RESOLVE 에 못 간다). `HazardCastSystem [UpdateBefore(AttackSystem)]` 로 같은 프레임 소비 |
| View | Zone: `Presentation/HazardVisualLifetime.cs`(self-destroy) / Blocking: `Battle/Effects/BlockingHazardPresenter.cs`(엔티티 추적) | 계열별 뷰 백엔드 다름 |
| 씬 wiring | BattleBridge (EffectSpawner·vfxSpawner 경유) | |

## ~~목표지점 — 안정도 골 (goal-stability)~~ — 은퇴 (2026-08-10)

이 아키타입의 잠자는 경로(`GoalPoint`/`SpawnGoalEntities`/`goalMaxStability` — 전 맵 미저작이라 런타임에 한 번도 태어나지 않았다)는 **battle-structures unit 0 이 걷어냈다.** «엔티티 존재 = 그 셀의 골이 살아있다» 라는 원설계는 위 **거점(battle-structures)** 아키타입의 셀 단위 붕괴(ⓐ)로 승계됐다. 설계 이력은 `docs/spec/goal-stability/`(문서 보존 — battle-structures 의 근거).

## 스킬 해저드 — Tornado/Meteor/Portal (플레이어 스킬 탭)

| 정거장 | 앵커 | 확인 포인트 |
|---|---|---|
| 데이터 SO | `Data/SkillData.cs` | SkillEffectType 분기 |
| 스폰 진입점 | `BattleBridge.CastSkillAtTile` → ApplyTornado/ApplyMeteor/ApplyPortal | ★Mono 주도 — ECS request 왕복 없음 |
| ECS 캐리어 (Effects) | `Battle/Effects/` TornadoField·MeteorPending·PortalLink (`EffectSpawner.cs`) | |
| 시뮬 consumer | `Battle/Movement/MovementSystem.cs`(pull·텔레포트) · `Battle/Combat/MeteorResolutionSystem.cs` | 캐리어=Effects, 데미지 쓰기=Combat 의도적 분리 |
| 이벤트 큐 | `Battle/Combat/MeteorBurstEventsSingleton.cs` (Meteor 전용) | Tornado/Portal 은 큐 없음 — 캐스트타임 즉시 시각 |
| View | `Presentation/VfxSpawner.cs` (SpawnTornado/SpawnMeteorFall/SpawnPortal/SpawnMeteorBurst) + `MeteorFall.cs` | 경고링은 BattleBridge 인라인 쿼드 |
| 씬 wiring | BattleBridge.vfxSpawner + VfxSpawner 프리팹 슬롯 | |

## 스킬 아군 장판 — 공격폭증/속사 (active-ally-zone)

| 정거장 | 앵커 | 확인 포인트 |
|---|---|---|
| 데이터 SO | `Data/SkillData.cs` (range/magnitude/durationSec) | 정본은 **DcSkills 시트** — 에셋만 고치면 런타임 갱신이 덮는다 |
| 스폰 진입점 | `BattleBridge.CastSkillAtTile` → `SpawnAllyBuffZone` | Mono 주도. 대상 0기여도 성공(카운트는 로그용) |
| ECS 캐리어 (Effects) | `Battle/Effects/AllyBuffField.cs` (`EffectSpawner.SpawnAllyBuffField`) | 중심=셀(int2), `StackId=3` |
| 시뮬 consumer | `Battle/Effects/AllyBuffFieldSystem.cs` | 매 프레임 재발행(ZoneApplySystem 관용구). duration = `AllyBuffApplySec` 고정 |
| 이벤트 큐 | `StatModifierApplyEventsSingleton` (기존) | 신규 큐 없음 |
| 수명/정리 | `Battle/Effects/EffectTickSystem.cs` + `BattleBridge.DestroyBattleEntities` | 만료 파괴 + 매치 경계 정리 |
| View | `Core/TilemapMapView.cs` 전용 zone 타일맵(`AddZoneCells`/`RemoveZoneCells`, 칸별 refcount) + `BattleBridge.PaintAllyBuffZone`/`DrainAllyBuffZoneVisuals`/`ClearAllyBuffZonePaint` | 조준 프리뷰(`SkillAim`)·맵 효과 타일 채널과 분리 |
| 씬 wiring | `TilemapMapView.allyZoneColor` | 유닛별 신호는 미구현(StatusFx 후속) |

## 픽업 — 레드불 (season-gimmick-overwork, 시즌 기믹)

| 정거장 | 앵커 | 확인 포인트 |
|---|---|---|
| 데이터 SO | `Data/Gimmick/OverworkGimmickData.cs` (스폰 주기·수명·동시상한·효과 수치) | 시즌 SO `SeasonData.gimmick` 로 활성. gimmick=null=전면 비활성 |
| 스폰 진입점 | `Battle/Effects/PickupSpawnSystem.cs` (Effects 내부 ECB) | ★staged-request 아님 — 순수 ECS 스폰. `OverworkGimmickConfig`+`PickupSpawnState` self-gate |
| ECS 컴포넌트 (Effects) | `Battle/Effects/` Pickup(cell·kind·remainingLife) + PickupSpawnState 싱글턴(후보 셀 Walk∪Place·rng·cadence, **BattleBridge 소유**) | 후보 셀은 `BuildPickupSpawnState`(FlowField 동형 lifecycle)가 `_generatedMap` 에서 구축 |
| 시뮬 시스템 | `PickupSpawnSystem`(스폰+만료) · `PickupConsumeSystem`(co-location 소비) · `LastRunSystem`(지연 crash) | Consume/LastRun 은 telemetry 로그 위해 non-Burst |
| 이벤트 큐 | N/A — 신규 채널 0. 라스트런 효과는 기존 `StatModifierApplyEvents` 재사용(AS 버프 + MaxHealthMul 컷) | |
| View | `Battle/Effects/PickupPresenter.cs`(절차적 플레이스홀더) + `BattleBridge.ReconcilePickupViews` poll-reconcile(엔티티↔GameObject) | ★이벤트 아님 — 매 프레임 poll. 정식 아트/소비 VFX 후속 |
| 씬 wiring | BattleBridge.pickupViewPrefab(옵션)·pickupViewHeight + SeasonRegistry.defaultSeason=season_overwork | 상태 아이콘은 unit-buff-debuff-aura 오라에 위임 |

## 사직서 → 메테오 — 집에 가도 되나요 (season-gimmick-clockout, 시즌 기믹)

| 정거장 | 앵커 | 확인 포인트 |
|---|---|---|
| 데이터 SO | `Data/Gimmick/ClockOutGimmickData.cs` (사직서 임계·메테오 수치) | `BattleConfig.gimmickPool` 배정(GameManager). 픽업과 달리 **death-스폰·비소비** |
| 스폰 진입점 | `Battle/Effects/ResignationDropSystem.cs` — **death-트리거**: defender 사망 시(원인 불문) 배치 타일에 Resignation 스폰 | 주기 스폰 아님. `ClockOutGimmickConfig` self-gate. UnitLifecycle 파괴 직전 관찰(UpdateAfter Damage/Health, UpdateBefore Lifecycle) |
| ECS 컴포넌트 (Effects) | `Resignation(cell)` | 사직서는 유닛이 안 줍는다 — 전역 임계로만 소멸 |
| 시뮬 시스템 | `ResignationDropSystem`(DeadTag defender → 사직서 스폰) · `ResignationThresholdSystem`(사직서 ≥ threshold 소모 → barrage 요청) | 사망 = 기존 death 경로(DeadTag→UnitLifecycle→DefenderDeathEvent) 그대로. 강제 퇴근/코스트 환급은 unit 8 재설계로 폐기 |
| 이벤트 큐 | **신규 1**: `MeteorBarrageRequestsSingleton` (Effects→Bridge). 메테오 자체는 기존 `ProjectileSpawnRequest`(SkyFall×TileAoe) 재사용 | 메테오 cast = `BattleBridge.SpawnProjectile(...,Entity.Null)`(bridge-cast, targetFaction=Enemy) |
| View | `Battle/Effects/ResignationPresenter.cs`(절차적 흰 종이) + `BattleBridge.ReconcileResignationViews` poll-reconcile. 메테오 뷰는 기존 투사체 파이프라인 | ★poll-reconcile(Pickup 동형). 정식 아트/VFX 후속 |
| 씬 wiring | BattleBridge.resignationViewPrefab(옵션)·resignationViewHeight + `BattleConfig.gimmickPool` 에 `Gimmick_ClockOut` 등록 | — |

## 힐 (Heal)

| 정거장 | 앵커 | 확인 포인트 |
|---|---|---|
| 데이터 SO | N/A — 전용 SO 없음. `Data/AttackOutput.cs` 의 AttackOutputKind.Heal + 유닛 스탯 | |
| 스폰 진입점 | `Battle/Combat/AttackSystem.cs`·`ProjectileHitSystem.cs` → IncomingHeal 버퍼 append | 캐리어 엔티티 없음 |
| ECS 컴포넌트 (Units) | IncomingHeal 버퍼 — 배치 시 사전 부착 (`BattleBridge`) | ECB 구조변경 없이 append 하기 위함 |
| 시뮬 시스템 | `Battle/Units/DamageApplicationSystem.cs` | pulse>0 만 이벤트 — RegenPerSec 는 VFX 스팸 방지로 의도적 제외 |
| 이벤트 큐 | `Battle/Units/HealAppliedEventsSingleton.cs` | drain → `VfxSpawner.SpawnHealApplied` |
| View | VfxSpawner one-shot (healAppliedPrefab, 풀 없음) | |
| 씬 wiring | VfxSpawner.healAppliedPrefab 슬롯 | |

## VFX (one-shot)

| 정거장 | 앵커 | 확인 포인트 |
|---|---|---|
| 프리팹 소스 | `Presentation/VfxSpawner.cs` SerializeField 슬롯 (SO 아님) | 슬롯 null 이면 LogError — 코드 폴백 없음 |
| 트리거 | ★혼합 — 큐 drain(MeteorBurst·HealApplied) + BattleBridge 직접 호출(배치링·캐스트타임 시각) | 단일 큐 아님. 새 VFX 는 어느 경로인지 먼저 결정 |
| 공격 히트/캐스트 VFX | `Presentation/ProjectileViewPool.cs` PlayHit/PlayCast (UnitAttackVisualEvents drain) | ★VfxSpawner 를 거치지 않음 |
| View | 프리팹 내부 Shuriken PS / `MeteorFall.cs` | 풀링 없음, 타이머 Destroy |
| 씬 wiring | BattleBridge.vfxSpawner + 슬롯별 프리팹 할당 | |

## 본 부착 VFX — 무기 궤적 (spine-weapon-trail)

one-shot VFX 와 달리 **유닛의 자식으로 붙어 수명을 함께하고, Spine 본을 따라간다.**
스포너도 큐도 새로 만들지 않는다 — 유닛 SO 가 리그 프리팹을 들고, 뷰가 공격 사건에 재생을 건다.
새 "본 부착" 계열(꼬리·오라 트레일 등)은 이 표를 기준으로 삼는다.

> 실제로 켜고·바꾸고·새 호스트에 붙이는 **레시피와 증상→원인 표**는
> [`weapon-trail-authoring.md`](weapon-trail-authoring.md).

| 정거장 | 앵커 | 확인 포인트 |
|---|---|---|
| 데이터 SO | `Data/ISpineUnitVisualData.cs` — `SpineWeaponTrailPrefab` / `…EndNormalized` | ★디펜더·적 **공용** 인터페이스. 범위는 타입이 아니라 **프리팹 할당**이 정한다 |
| 룩 SO | `_Project/VFX/WeaponTrailPreset_*.asset` (벤더 `HS_SwordTrailPreset` 복사본 7종) | 벤더 프리셋 직접 참조 불가 — sortingOrder/recalcOnAwake/startActive 3개를 반드시 덮어야 한다 |
| 프리팹 소스 | `_Project/VFX/WeaponTrail_Slash.prefab`(base) + 룩별 **Prefab Variant** | 리그 = 빈 Animator + BoneFollower + `HS_SwordMeshTrail` + `WeaponTrailRig` + Point A/B. Animator 빠지면 상시 방출 |
| ECS | **N/A — 시뮬 무관.** 궤적은 판정에 기여하지 않는다 | |
| 트리거 | 기존 `UnitAttackVisualEvents` drain → `SpineUnitView.PlayAttack` | **신규 큐 0** |
| View | `Presentation/WeaponTrailRig.cs` — `Bind(SkeletonRenderer)` / `Play(sec)` | 호스트는 이 둘만 안다. `Bind(null)` = 본 없는 호스트(구조물) 경로 |
| Pool | **N/A — 유닛당 1개, 유닛 수명과 동일** | 생성 메시 레이어는 **씬 루트** 오브젝트(부모 없음) |
| 정렬 | `BoardSortOrder.WeaponTrailOrder` + 프리셋 layer sortingOrder | ★실제 적용값은 **프리셋**(HS 가 매 LateUpdate 되쓴다). 파티클은 리그가 소유하고 호스트 스윕이 `IsChildOf` 로 제외 |
| 씬 wiring | **N/A — 씬 오브젝트 신설 없음** | 프리팹 참조는 유닛 SO 가 들고 있다 |

## 데미지 넘버

| 정거장 | 앵커 | 확인 포인트 |
|---|---|---|
| 데이터 | `Presentation/DamageNumberStyle.cs` (Spawner 직렬화 번들, SO 아님) | |
| 트리거 | `Battle/Units/DamageNumberEventsSingleton.cs` → `BattleBridge.DrainDamageNumberEvents` | 같은 drain 이 EnemyHitBar 도 구동 |
| Spawner/Pool/View | `Presentation/DamageNumberSpawner.cs` / `DamageNumberPool.cs` / `DamageNumberView.cs` | plain C# Queue 풀 |
| 씬 wiring | BattleBridge.damageNumberSpawner | |

## 예고 오버레이 (스폰 라인 — 폴링 구동 월드 오버레이)

이벤트가 아니라 **이미 확정된 다음 스폰 시각을 읽어** 그리는 계열이라 큐/풀/프리팹이 전부 없다.
(2026-07-26 정정: 초기 구현은 "다음 웨이브 예측"이었으나 `spawn-point-alert/3` 에서 **큐잉된
웨이브의 사실**로 바뀌었다 — 예측 로직·캐시가 사라졌다.)
다른 아키타입과 트리거 성격이 다르므로 새 예고류는 이 표를 기준으로 삼는다.

| 정거장 | 앵커 | 확인 포인트 |
|---|---|---|
| 데이터 | `Presentation/SpawnAlertPresenter.cs` SerializeField (SO 아님) | 색·폭·타이밍 전부 인스펙터. 프리팹/SO 소스 없음 |
| ECS | N/A — 시뮬 무관 순수 Mono | 예고는 시뮬을 바꾸지 않는다 |
| 트리거 | ★큐 아님 — `BattleBridge.TryGetSpawnAlertForecast` **read-only 폴링** | NextWaveDock 과 같은 폴링 계열. 이벤트 drain 아님 |
| 예보 산식 | `Data/WavePatternGenerator.FirstSpawnTimesPerLane` (순수) — **`BattleBridge.QueueWave` 가 큐잉 시점에 1회 호출** | 실스폰 엔트리와 **같은 인자**로 호출(+`EffectiveSpawnIndex`·`DeckIndexStride` 공유)가 정확도 보증. 창은 `waveSpawnLeadInSec`(wave-pattern 11)가 만든다 |
| 경로 소스 | `BattleBridge.TryGetSpawnPathSim` (goal flow field 추적) | 유닛 이동과 같은 필드 → 표시 루트 = 실제 루트 |
| View | `Presentation/SpawnAlertPresenter.cs` — lane 당 LineRenderer 3 + SpriteRenderer 1 | 풀 없음(lane 수만큼 생성 후 재사용). 텍스처 절차 생성 |
| 정렬 | `BoardSortOrder.SpawnAlertOrder = -9` (−9~−6) | ★바닥 데칼 대역. 유닛(양수) 아래 — 양수로 두면 유닛을 덮는다 |
| 씬 wiring | `SpawnAlertPresenter` GameObject + `bridge` 참조 | |

## 프랍/타일 (맵 데코)

| 정거장 | 앵커 | 확인 포인트 |
|---|---|---|
| 데이터 SO | `Data/MapThemeData.cs` + `Data/PropData.cs` + `Data/TileSetData.cs` | |
| ECS | N/A — 배틀 런타임 무관, 맵 빌드 1회 생성 | seed 결정론(비동기 토너먼트 양측 동일) |
| 배치 계산 | `Data/BackgroundPropPlacer.cs` (순수 static) | |
| 인스턴스화 | `BattleBridge`(맵 빌드) → `Core/TilemapMapView.cs` InstantiateProp | 모바일 prop budget 솎음 |
| View | `Presentation/PropBillboard.cs`(프리팹 authored) / `TilemapPropScatter.cs`(독립 tilemap 데코 — Bridge/ECS 무관) | |
| 씬 wiring | 씬 theme SO + tilemap GameObject | `unity-prop-tile-authoring` 스킬 |

---

## 유지 규칙

- 갱신 트리거는 **구조 변경만**: 새 아키타입, 정거장 추가/제거(새 큐·새 pool), 앵커 파일 이동/개명. 수치·필드·시스템 내부 로직 변경은 대상 아님.
- 강제 지점: feature 종료 handoff 작성 시 구조 변경 여부 확인(CLAUDE.md 워크플로우 5번) + spec 작성 시점 대조 중 어긋남 발견 시 즉시 수정.
- 이 문서에 동작 설명·이벤트 필드·코드 흐름 산문을 추가하지 않는다.
