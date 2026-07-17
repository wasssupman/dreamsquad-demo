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
| 데이터 SO | `Data/DefenderUnitData.cs` (+`DefenderCatalog.cs`) | 스탯/투사체/스킬 값 전부 SO 에서. 신규 유닛은 **DefenderCatalog 등록까지** (미등록 = 로스터 미노출) |
| 스폰 진입점 | `Bridge/BattleBridge.cs` `PlaceDefenderAs`→`CreateDefenderEntity` | 플레이어 배치 기반 |
| ECS 컴포넌트 (Units) | `Battle/Units/` DefenderUnitTag·Health·IncomingDamage·DefenderTile | 능력별 조건부: AttackState / HazardCastState / AggroProvider / DeployedFacing(방향 지정 배치 — 활성화 시 1회 기록) / VolleyFireState(Combat 소유, shotCount>1 만) |
| 시뮬 시스템 | `Battle/Combat/AttackSystem.cs` · `Battle/Units/DamageApplicationSystem.cs`·`HealthDeathSystem.cs` | 이동 없음(고정) — PathFollowState 미부여 |
| 이벤트 큐 | `Battle/Units/DefenderDeathEventsSingleton.cs` + 공유 UnitAttackVisual/DamageNumber/HealApplied | drain = `BattleBridge.DrainDefenderDeathEvents` |
| View/Pool | `Presentation/SpineUnitPool.cs`+`SpineUnitView.cs`, 폴백 `QuadUnitViewPool.cs` | 위치/틴트 sync = `BattleBridge.SyncMonoUnitViews` 매 프레임 |
| 체력 표시 | `Presentation/TileHealthGaugeLayer.cs`+`TileHealthGaugeView.cs` | ★큐 아님 — 매 프레임 Health **폴링** |
| 씬 wiring | BattleBridge SerializeField: spineUnitPool·defenderFallbackViewPool·tileHealthGaugeLayer | Spine 실패 시 Quad 폴백 |

## 적 (Enemy)

| 정거장 | 앵커 | 확인 포인트 |
|---|---|---|
| 데이터 SO | `Data/AttackUnitData.cs` (+`EnemyCatalog.cs`) | ★적 스탯 SO 이름이 **AttackUnitData** — "EnemyData" 는 없음. 신규 적은 **EnemyCatalog + AttackDeck/웨이브 pool 노출까지** |
| 스폰 진입점 | `Bridge/BattleBridge.cs` `SpawnUnit` | 웨이브 스케줄러가 `Data/AttackDeck.cs`·`WavePlanAsset.cs` 소비 |
| ECS 컴포넌트 | Units: AttackUnitTag·Health·IncomingDamage · Movement: `PathFollowState` · Combat: AttackState·EnemyBehavior·EnemyAiState | 이동은 적 전용 |
| 시뮬 시스템 | `Battle/Movement/MovementSystem.cs`(flow-field) · `Battle/Combat/AttackSystem.cs`·`EnemyAiStateSystem.cs` | |
| 이벤트 큐 | `Battle/Units/EnemyKilledEventsSingleton.cs`·`GoalReachedEventsSingleton.cs` · `Battle/Effects/EnemyCcEvents.cs` | + 공유 UnitAttackVisual/DamageNumber |
| View/Pool | `Presentation/SpineUnitPool.cs`(공유) / `QuadUnitViewPool.cs`(enemyViewPool 인스턴스) | 저체력 틴트 = SyncMonoUnitViews 내 |
| 피격바 | `Presentation/EnemyHitBarSpawner.cs`+`EnemyHitBarView.cs` | ★전용 큐 아님 — **DamageNumberEventsSingleton 공유** drain |
| 씬 wiring | BattleBridge SerializeField: spineUnitPool·enemyViewPool·enemyHitBarSpawner·deck | |

## 투사체 (Projectile)

| 정거장 | 앵커 | 확인 포인트 |
|---|---|---|
| 데이터 SO | `Data/ProjectileData.cs` | 궤적(MovementKind) × 페이로드(PayloadKind) 2축. flightMode 가 이 2축으로 번역됨(`ResolveProjectileAxes`) |
| 스폰 진입점 | `Battle/Combat/AttackSystem.cs` 가 `ProjectileSpawnRequest` stage → `BattleBridge.DrainProjectileSpawnRequests`→`SpawnProjectile` | ★2단계 — ECS 는 request 만, 엔티티+뷰 생성은 Bridge |
| ECS 컴포넌트 (Combat) | `Battle/Combat/Projectile/` ProjectileState·ProjectileTag·ProjectileSpawnRequest | 페이로드별 조건부: PathHitRecord 버퍼(PathHit — 대상당 1회 스윕, drain 이 부착) |
| 시뮬 시스템 | `ProjectileMoveSystem.cs`(궤적) · `ProjectileHitSystem.cs`(페이로드 — IncomingDamage/IncomingHeal 기입) | |
| 이벤트 큐 | `Battle/Combat/Projectile/ProjectileHitEventsSingleton.cs` | drain = `DrainProjectileHitEvents` → PlayHit |
| View/Pool | `Presentation/ProjectileViewPool.cs` | 매 프레임 `SyncTransforms`; muzzle/cast VFX 도 이 풀 (PlayHit/PlayCast, UnitAttackVisualEvents drain) |
| 씬 wiring | BattleBridge `_projectileViewPool` | |

## 해저드 — Zone/Blocking (방어 유닛 HazardCast 능력)

| 정거장 | 앵커 | 확인 포인트 |
|---|---|---|
| 데이터 SO | `Data/HazardSO.cs`(Zone) / `Battle/Effects/BlockingHazardSO.cs` | visualPrefab·lifetime·파괴 VFX |
| 스폰 진입점 | `Battle/Effects/HazardCastSystem.cs` → HazardSpawnRequests 큐 → `BattleBridge.DrainHazardSpawnRequests` | staged-request drain (투사체와 동형) |
| ECS 컴포넌트 (Effects) | `Battle/Effects/` Hazard·HazardEffect·BlockingHazard·BlockingHazardCellsBuffer (`EffectSpawner.cs`) | |
| 시뮬 시스템 | `HazardLifetimeSystem.cs`·`ZoneApplySystem.cs`·`DotApplySystem.cs`·`CcApplySystem.cs` | |
| 이벤트 큐 | HazardSpawnRequests·HazardDestroyed(Blocking 파괴)·HazardRuntime Singleton | ★HazardRuntimeEvents 는 **텔레메트리 로깅 전용** — VFX 트리거 아님 |
| View | Zone: `Presentation/HazardVisualLifetime.cs`(self-destroy) / Blocking: `Battle/Effects/BlockingHazardPresenter.cs`(엔티티 추적) | 계열별 뷰 백엔드 다름 |
| 씬 wiring | BattleBridge (EffectSpawner·vfxSpawner 경유) | |

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

## 데미지 넘버

| 정거장 | 앵커 | 확인 포인트 |
|---|---|---|
| 데이터 | `Presentation/DamageNumberStyle.cs` (Spawner 직렬화 번들, SO 아님) | |
| 트리거 | `Battle/Units/DamageNumberEventsSingleton.cs` → `BattleBridge.DrainDamageNumberEvents` | 같은 drain 이 EnemyHitBar 도 구동 |
| Spawner/Pool/View | `Presentation/DamageNumberSpawner.cs` / `DamageNumberPool.cs` / `DamageNumberView.cs` | plain C# Queue 풀 |
| 씬 wiring | BattleBridge.damageNumberSpawner | |

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
