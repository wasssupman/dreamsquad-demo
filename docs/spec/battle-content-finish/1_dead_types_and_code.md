# 1 — 죽은 SO 타입 · 코드 · enum 잔재

## 목적

참조가 0 인 타입과, 소비처가 없는 SO 에셋을 지운다. 판정은 **선언 줄을 뺀 전 파일**(같은 파일의 파생·호출 포함) 에서 코드 참조 0 이거나, 테스트만 남은 경우(그 테스트도 함께).

## 검증 (2026-10-07 재검증에서 걸러진 거짓 양성 — 지우지 않는다)

「참조가 있다」와 「효과를 낸다」를 갈라 적는다. 실효성 = 라이브 콘텐츠(카탈로그 · 맵 풀 · 씬)가 실제로 그 길을 지나는가.

| 후보였던 것 | 왜 산 것인가 | 실효성 |
|---|---|---|
| `StatAuraSkill` | 같은 파일의 `AllySpeedAuraSkill` · `AllyStatAuraSkill` · `OpponentStatAuraSkill` 의 abstract 베이스 | **있음** — Guardian 의 `Effect_ally_damage_aura_guardian`(kind 26) · Archer 의 `Effect_slow_aura_archer`(kind 27) · `AllyMoveSpeedAura`(kind 9) 가 이 셋으로 라우팅된다 |
| `EffectSlots.LegacyPayload` · `ToLegacy` · `Of` · `EffectSlot` | `CardDefinitionBuilder:195`(액티브 카드 굽기 — 매 판) · `BindingSpec.ToMechanic`(카드 문안)이 `ToLegacy` 를 부르고, `ToLegacy` 가 `Of`/`EffectSlot` 을 쓴다 | **있음** — 라이브 굽기의 hot path. 테스트만 쓰는 건 `FromLegacy` 하나. 이 번역 층의 제거는 백로그 「옛 메커닉 번역 층 제거」(skill-data-table) |
| `Card_IncubusPact.asset` | 2026-08-08 사용자 결정으로 **의도적 비활성** — `DreamcatcherCatalogSyncTests.IntentionallyDisabled` 가 사유와 함께 지킨다 | **없음** — 카탈로그 밖이라 뽑히지 않는다 → **D8** |
| 덱 5(`Deck_Twin/Spiral/Hook/Ford/Isle`) · `WaveA` · `WavePlan_BossTest` | 테스트 4 가 경로로 연다 | **없음** — 맵 풀은 `Deck_Duel/Serpent/Zig/Coil` 4 뿐. Twin/Spiral/Hook 은 사라진 맵의 덱이고 `LiveDeckBossAuthoringTests` 의 「6맵 ÷ 보스 3종 = 각 2맵」 은 옛 맵 수를 전제한다. Ford/Isle 은 Siege 스테이지(없음)의 덱. `WaveA`(킬 예산 기준 덱) · `WavePlan_BossTest` 는 순수 **테스트 픽스처** → **D4** |
| `LayerLabPresetImporter` | `UnitVisualDataValidator:130` 이 `DefenderUnitData` 인스펙터에 `LayerLabImportSection` 을 그린다 | **있음** — Layer Lab 프리셋 → `partSkins` · `slotColors` 를 쓰고, 런타임 `SpineCombinedSkinCache` 가 그 둘을 스켈레톤에 적용한다(Layer Lab 유닛 42 의 외형 저작 경로) |
| `GaProjectileStripper` | `lessons/03:129` 가 GA 탄 반입 절차의 도구로 든다 | **런타임 없음 · 저작 있음** — 산출물 50 중 10 이 라이브(Marksman/Piercer/Sniper 배치탄 · Artillery · Meteor · NightmareBarrage · ShotgunPellet · Boomerang). 다음 GA 반입 때만 필요. 유지(1 파일) — 지우려면 lessons/03 그 줄과 함께 |
| `com.unity.timeline` | PixPlays `PlayableVfx` + `.playable` 3 | **있음** — `EarthSlamSpikesAoeVFX` 는 Guardian · Scout · ShieldShuttle · `Projectile_JjangssenLeap`, `WaterBlast` 는 Bastion 이 쓴다 |
| `RouteMarker` | `MapStageScanner` · `SpawnMarker.routeIndex` · 커스텀 인스펙터가 읽고, 코어 `Move/WaypointProgress` · `SpawnPathPreview` 가 웨이포인트를 돈다 | **없음(오늘)** — 라이브 스테이지 4 에 `RouteMarker` 0 · `routeIndex` 전부 -1(골 직행). 웨이포인트는 코어 이동 기능인데 저작한 맵이 없다 → **D9**(기능째 둘지) |
| `MapMode` | `StructureAuthoringRules` 안에서만 쓴다 — 그 클래스와 운명을 같이한다(아래) | — |
| `TraceEvent`(LegacyTraceV0.cs 안) | `BattleWorld:540` 이 상태 해시에 `Quantize` 를 쓴다 | **있음** — 골든 결정론 |

## 삭제 — SO 타입 + 에셋 (참조 0)

| 타입 | 에셋 | 근거 |
|---|---|---|
| `Data/RelocationSettings.cs` | `Data/Config/RelocationSettings.asset` | 「이동모드」 기능은 은퇴(판 안 동사는 배치·퇴근·드림캐쳐·당김). 코드 참조 0 |
| `Data/ScoreRulesData.cs` | `Data/Config/ScoreRules.asset` | 클래스 본문이 「에디터에서 지울 때까지 빈 SO 로 둔다」— 지금이 그때 |
| `Data/MapGrid` 의 옛 `MapDocument` | `Data/Maps/MapDocument_MovementStress.asset` | 스크립트가 이미 없다(missing script 에셋) |
| `Data/Dreamcatcher/DreamcatcherDeck.cs` | `DreamcatcherDeck_Default.asset` | **D2 에 따라** 단위 0 이 기본 덱 그릇으로 되살리거나 여기서 지운다 |

`RelocationSettings` 의 소비자로 주석이 드는 `DefenderRelocationController` 는 존재하지 않는다(주석 3곳뿐) — 재배치 기능은 은퇴했고 SO 는 그 잔재다.

## 삭제 — 코드 (선언 줄 제외 코드 참조 0, 또는 테스트만)

| 대상 | 같이 지우는 테스트 | 근거 |
|---|---|---|
| `UnitAi/DeployPhaseClock.cs` | `EditMode/DeployPhaseClockTests.cs` | 코드 참조 = 그 테스트 5줄뿐. 배치 중 판정은 코어 `CombatPhase:156` 의 `u.Deploying` |
| `Data/BonusPullTrigger.cs` | `EditMode/BonusPullTriggerTests.cs` | 코드 참조 = 그 테스트뿐. 당김 크레딧은 코어 `WaveScheduler` 가 센다 |
| `BattleCore/Trace/LegacyTraceV0.cs` 의 `LegacyTraceV0` 클래스 · `TraceChannel` enum | `EditModeCore/DeterminismTests.cs:217` 케이스 1 | 코드 참조 = 그 테스트 + 자기 파일. `TraceEvent` 는 남긴다(`Quantize` 를 코어 둘이 쓴다) — 파일명은 `TraceEvent.cs` 로. 코어 산식 무변 |
| `Presentation/TileHealthGaugeLayer.cs` · `TileHealthGaugeView.cs` | `EditMode/HealthDisplayBarGaugeTests.cs` 의 `EdgeFill` 단언 2 | 둘 다 MonoBehaviour 인데 씬·프리팹·`AddComponent` 0. 체력 표시 축(`UnitHealthPresentationMode` = Legacy/UnifiedOverhead)의 어느 쪽도 이 둘을 쓰지 않는다 |
| `UI/Layout/UiFullBleedModalDim.cs` | — | 참조 0 · 씬 0 |
| `Presentation/Fluid/FluidPaintView.cs` | — | 참조 0(`FluidPaintSim` 은 손패 배경이 쓴다 — 유지) |
| `Data/StructurePlacement.cs` 의 `StructureAuthoringRules` + `MapMode` | — | 호출 0. 주석의 호출자 「페인터 · `MapDocument.OnValidate`」가 둘 다 사라졌다 |
| `BattleCore/Combat/AggroTargeting.cs` 의 `AggroPolicy` | — | 참조 0(코어 — 산식 아님, 정적 클래스 삭제만) |
| `Data/Dreamcatcher/DcMechanic.cs` 의 `DcPayloadKinds` | — | 주석 1곳만 든다 |

## `GamePhase` 잔재 값

`GamePhase { None, Draft, Placement, Battle, Result, Tally, Gimmick }` — `Draft`(드래프트 픽 은퇴) · `Tally`(결과 집계 연출 — 결과 화면 삭제) 는 발행처 0. `Result` 는 `CorePhaseFeed:104` 가 종료 때 민다(유지).

- enum 은 `CameraDirectionConfig.asset` 에 **int 로 직렬화**된다(`GamePhase.cs` 머리 주석). 값을 빼면 같은 커밋에서 그 에셋의 phase 정수를 옮긴다 — 에디터 안에서 일회용 MenuItem 으로(열린 씬 아님, 에셋이라 YAML 직접 편집도 가능).
- `CameraDirectionConfig.cs:199` 의 `Draft` 참조 · `CoreGamePhaseTests` 의 핀 값 갱신.

## 완료 기준

- [x] 위 표의 파일 0 · `rg "RelocationSettings|ScoreRulesData|MapDocument|DeployPhaseClock|BonusPullTrigger|LegacyTraceV0|TraceChannel|TileHealthGauge|UiFullBleedModalDim|FluidPaintView|StructureAuthoringRules|AggroPolicy|DcPayloadKinds" Assets` → `CoreTrace` 의 거절 메시지 문자열 1 외 0
- [x] `GamePhase` 값 5 · `CameraDirectionConfig.asset` 의 phase 정수 재대응 · 카메라 프리셋이 Play 에서 전과 같다
- [x] 에디터 컴파일 0 · 커밋 1(경로 지정)

## 구현 결과 (2026-10-07) — `85e48a310`

- 삭제: 표의 전부(`RouteMarker` 제외 — 저작 선택지로 유지). `LegacyTraceV0.cs` 는 **파일째** — `TraceEvent.Quantize` 는 `CoreTraceEvent.Quantize` 로 자립(같은 1e-3 격자 · `BattleWorld` 해시 식 무변). `BoardSortOrder.TileGaugeOrder` 도 함께.
- `GamePhase { None, Placement, Battle, Result, Gimmick }` — 에셋 `breathPhases` 가 비어 있어 정수 이동 없음. `CameraDirectionConfig` 기본값 · `CoreGamePhaseTests` 핀 갱신.
- 헤드리스 `BattleCore.csproj` 빌드 0 에러(코어 변경분 확인) · 에디터 전체 재컴파일 0.
