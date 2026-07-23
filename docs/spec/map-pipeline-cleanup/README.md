# Map Pipeline Cleanup — legacy 맵 생성 코드 정리

**상태: 구현 완료 2026-07-23** (유닛 0~4 EditMode 1248 green — 사용자 Play 확인 대기)

> **재점검 2026-07-23 (multi-goal-map·tournament-seed-map-select 반영)**: 스펙 작성 후 두 feature 가 keep-set 주변을 수정했다. 앵커 재검증 결과 —
> - **구조 유효**: `MapSettingsPanelView` 소비처(DraftView/SquadPrepView), legacy 스위치 arm, `ActiveDeck==null || map==null` 가드 3곳(라인 1140/1204/1502 → **1178/1242/1540** 이동) 전부 그대로. 각 유닛 구현 시 라인 앵커는 재확인하고 진행.
> - **keep-set 갱신**: 라이브 풀 선택은 이제 `fixedMapSeed(디버그) > 토너먼트 시드(SelectIndexFromTournamentSeed) > 폴백 0번` 3분기(tournament-seed-map-select). `BuildFallbackLinear` 는 goals 명시 세팅 포함(multi-goal unit 0), `MapConnectivity.AllSpawnsReachGoal` 은 멀티-소스 BFS 로 진화 — 모두 keep-set 그대로, 이 스펙이 건드리지 않는다.
> - **신규 keep 테스트**: `FlowFieldSingletonTests`·`MultiGoalPoolSeparationTests`·`MapConnectivityTests`(+2)·`MapDocumentRoundTripTests`(멀티골 케이스 추가됨) — 삭제 대상 아님. 유닛 4 의 `MapGridBattleAdapterTests` 재작성 시 멀티골 라운드트립 케이스 보존 확인.
> - **교차 참조 정밀 검증(grep, 2026-07-23)**: ① 풀 참조 GUID 에 TwinLane/hello 없음(유닛 0 안전) ② `MapPainterWindow`(멀티골 재작업분)는 삭제 체인 타입 0 참조(자체 BFS) ③ 신규 테스트 5종·멀티골 소비자 5종(TilemapMapView 등) 모두 삭제 대상 0 참조 ④ 씬 `mapDocument`=ArkFunnel 바인딩은 **inert**(풀이 항상 우선 — 코드 확인). manual-map-authoring 의 "document 최우선" 계약은 random-map-pool 이후 이미 stale — 맵 강제 노브는 `fixedMapSeed`(밸런싱 레퍼런스 문서화)로 대체됨 → 유닛 2/3 삭제 무영향 ⑤ `BuildFallbackLinear` 라이브 호출(:995 connectivity 폴백)은 keep — 유닛 2 의 gridSize/version 상수화가 이 호출 인자(options.spawnLaneCount 포함)를 커버해야 함.

## 목표

맵을 만드는 실 경로가 **하나**(`mapSource=MapGrid` + authored `MapDocumentPool`)로 굳었는데, 그 주변에 **두 세대 분량의 절차 생성 코드 + 참조 0 에셋**이 legacy 로 남아있다. 살아있는 경로는 건드리지 않고, 도달 불가/우회된 legacy 를 compile-safe 순서로 걷어낸다.

정리 대상은 조사(2026-07-23) + 팀리뷰 2인으로 확정:
- **구 pre-MapGrid 절차 경로** — `ProceduralMapGenerator`/`PathCarver`/`MapData` + BattleBridge legacy 스위치 arm(Manual/Fixture/Legacy) + 이를 런타임에 살려두던 디버그 UI(`MapSettingsPanelView`, `DraftView`/`SquadPrepView` 가 하드 필드로 물고 있음).
- **MapGrid 절차 생성 폴백 체인** — `MapGridGenerator` + 헬퍼 12종. authored 풀이 항상 usable doc 을 주므로 실게임에서 한 번도 안 돔.
- **참조 0 orphan 에셋** — 맵/타일셋 4종. (`desert.asset`/`TileSet_Desert` 는 리뷰에서 라이브 시즌 `season_S2_desert` 참조로 확인 → **제외**.)

## 살아있는 경로 (절대 불변 — 이 스펙이 건드리지 않는 keep-set)

```
GameManager.StartSquadMatch → BattleBridge.PrepareDraftMap → BuildMapForBattle
  → MapPoolSelect.SelectIndex → MapDocumentPool.Get → MapGridBattleAdapter.Build(authored doc)
  → MapDocumentBuilder.ToGeneratedMap → GeneratedMap
  → FlowFieldBuilder / TilemapMapView(Paint·Props) / BackgroundPropPlacer / EffectTilePlacer
```
데이터: `MapDocumentPool.asset`(Serpent/Coil/Twin/Spiral/Zig + WaveA/WaveB), `forest.asset`, `TileSet_AutoTileTest.asset`. `MapConnectivity.AllSpawnsReachGoal`, `ObstaclePlacer.DesignateDeco`(authored-Deco 없는 맵 대비 유지), `BattleMapBuilder.BuildFallbackLinear`(connectivity 실패 폴백) 도 keep.

## 작업 단위 목록

| # | 작업 구분 | 문서 | 목적 |
|---|---|---|---|
| 0 | 에셋 | `0_orphan_assets.md` | 참조 0 orphan 에셋 4종 삭제 (무위험, 코드 무관) |
| 1 | UI/Core | `1_debug_map_config_ui.md` | `MapSettingsPanelView` 삭제 + `DraftView`/`SquadPrepView`/`DraftController` 맵-config 표면 + 씬 와이어링 + 관련 draft 테스트 제거 |
| 2 | Bridge | `2_battlebridge_legacy_branches.md` | BattleBridge legacy 스위치 arm·필드(`map`/`mapSettings`/`mapDocument`/`useProcedural`/`_manualMapInput`)·public map-config API 제거, null-가드 pool 화 |
| 3 | Data | `3_pre_mapgrid_generators.md` | `ProceduralMapGenerator`/`PathCarver`/`MapData`/`ManualMapInput` + `BuildFromManual/Fixture`·`ObstaclePlacer.Place` + 관련 에셋·테스트 |
| 4 | Data/MapGrid | `4_mapgrid_procedural_fallback.md` | adapter hard-fail 화 + MapGrid 생성 체인 13종 + 테스트·디버그창 삭제 |
| 5 | Handoff | `5_handoff_summary.md` | 인계 (종료 시) |

## Feature-wide 계약

- **실 경로 불변**: keep-set(위) 은 절대 수정하지 않는다. 이 정리로 authored-pool 맵 빌드 동작·결정론·예산이 1비트도 바뀌면 안 된다.
- **compile-safe 순서**: 각 유닛은 소비처(consumer) 먼저 → 생산처(producer) 나중. 유닛 종료 시 항상 compile 0 error + EditMode green(그 유닛에서 삭제한 테스트 제외).
- **하드 페일 전환**: authored doc 이 unusable 하면 이제 조용한 절차 폴백 대신 **명확한 예외**로 실패한다. painter 가 bake 시 usable 을 강제하므로 unusable = authoring 버그 → 표면화가 맞다.
- **파일 통삭제 금지(메서드 혼재)**: `BattleMapBuilder`(FallbackLinear live), `ObstaclePlacer`(DesignateDeco live) 는 파일이 아니라 **메서드 단위** 제거(전용 private 헬퍼 동반). 단 테스트는 파일별로 판단 — `BattleMapBuilderTests` 는 FallbackLinear 케이스 유지(부분), `ObstaclePlacerTests` 는 DesignateDeco 직접 케이스가 애초에 없어 **통삭제**(리뷰), MapGrid 폴더는 `MapDocumentRoundTripTests` 유지·`MapGridBattleAdapterTests` 재작성.
- **`map`(MapData) 가드 대체**: `ActiveDeck==null || map==null` (1140/1204/1502) → `ActiveDeck==null || mapPool==null || mapPool.Count==0`. 단 placement 검사부(≈4095) 의 `map` 은 **GeneratedMap local shadow** 이니 절대 건드리지 않는다.
- **씬 편집 격리**: BattleScene 의 `MapSettingsPanelView`/legacy asset 참조 제거는 사용자 미저장 WIP 를 베이크할 위험 → surgical YAML edit 또는 스냅샷→checkout HEAD→delta 재적용으로 격리(`feedback_scene_save_bakes_wip`).
- **DesignateDeco/keepRatio 유지**: 절차 생성은 지우지만 `DesignateDeco` 는 authored-Deco 를 칠하지 않은 맵의 배치칸 커빙에 여전히 쓰인다 → 제거 대상 아님. `ObstaclePlacer.Place`(ProceduralMapGenerator 전용) 만 제거.
- **git 위생**: 삭제는 `dangerouslyDisableSandbox:true` 필요. 유닛당 1커밋, 사용자 확인 후 진행.

## 파이프라인 커버리지

N/A — 플레이 오브젝트 신설/파이프라인 정거장 변경 없음. **소비 경로 축소(폴백/legacy 제거)만** 하며 authored-pool 생산 경로는 불변. (`docs/reference/object-pipeline-map.md` 의 맵 아키타입 정거장은 그대로.)

## 후속 후보

- `MapGenerationOptions`/`MapPathShape`/`MapObstacleDensity` enum 이 정리 후 완전 무참조면 함께 제거(유닛 2/3 에서 판정).
- **`season_S2_desert` 시즌 폐기 여부** — `desert.asset` 을 참조하는 등록 시즌. 폐기하면 desert 테마·TileSet 도 함께 정리 가능하나 이는 맵 정리가 아닌 **시즌 콘텐츠 product 결정** → 별도.
- `DesignateDeco` 슬림 EditMode 테스트 신설(현재 직접 테스트 부재 — pre-existing gap).
- 즉시-반복 방지(직전 맵 재선택 억제), 아웃게임 맵 프리뷰 (random-map-pool 후속에서 이관).
