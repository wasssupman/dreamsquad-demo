# 2. BattleBridge legacy 브랜치·필드·API 제거

## 목적

`BuildMapForBattle` 의 switch 는 `mapSource=MapGrid` 하나만 실제로 돈다. Manual/Fixture/Procedural_Legacy/Legacy arm 과 그 입력 필드·public setter 를 제거해 브리지를 authored-pool 경로만 남긴다. (유닛 1 이 유일한 런타임 진입을 이미 제거해 이 arm 들은 도달 불가.)

## 변경 대상

- `Assets/_Project/Scripts/Bridge/BattleBridge.cs`
- `Assets/_Project/Scripts/Core/GameManager.cs` — `SetMapGenerationOptions(Default)` 호출(:261/:330)이 무의미해지면 제거

## 구현 (BattleBridge)

1. **switch 축소**: `BuildMapForBattle` 의 `case Manual/Fixture/Procedural_Legacy/Legacy` arm 전부 제거. `case MapGrid`(913–924)만 남긴다.
2. **필드 제거**: `map`(MapData, :32), `mapSettings`(MapGenerationSettings, :33), `useProcedural`(:35), `mapDocument`(단일 폴백, :39), `_manualMapInput`(:173). **추가(리뷰)**: `mapPathShape`(:47), `mapGenerationOptions`(:48), `_mapGridGridSizeOverride` 백킹필드.
   - `mapGridSettings`(:38) 와 그에 의존하는 `SetGoalEdgeOnly`/`CurrentGoalEdgeOnly` 는 **유닛 4** 에서 제거(컴파일 순서 — adapter 가 아직 참조).
3. **line 869 재작업(리뷰)**: `MapDocument activeDoc = mapDocument;`(:869) → `mapDocument` 필드 제거와 함께 `activeDoc` 초기값을 `null` 로. 라이브 풀이 항상 usable doc 을 주므로(871–879) 런타임 무회귀.
4. **null-가드 대체**: `ActiveDeck==null || map==null` (:1140/:1204/:1502) → `ActiveDeck==null || mapPool==null || mapPool.Count==0`. **≈4093 `SpatialPlacementCheck(GeneratedMap map, …)` 의 `map` 은 파라미터 shadow — 절대 불변**(리뷰 CONFIRM).
5. **GeneratorVersion/gridSize 폴백**: `GeneratorVersion`(:853, mapSettings 의존)·PickGridSize(:847–848) 는 legacy + `BuildFallbackLinear` 만 소비. authored doc 은 `ToGeneratedMap` 이 `doc.GeneratorVersion` 을 실어옴(리뷰 CONFIRM) → `GeneratorVersion` 을 상수화(FallbackLinear 용)하고 mapSettings 참조 제거.
6. **public map-config API + Current* getter 제거(리뷰 — 클러스터 전량)**: `SetMapSource`(:...)·`SetMapGridGridSizeOverride`(:1779)·`SetMapPathShape`(:1790)·`SetMapGenerationOptions` + getter `CurrentMapSource`(:1777)·`CurrentMapGridGridSizeOverride`(:1780)·`CurrentMapGenerationOptions`(:1804). 유닛 1 후 호출처는 GameManager(SetMapGenerationOptions) 뿐 → 그 호출도 제거. **`SetGoalEdgeOnly`(:1783)/`CurrentGoalEdgeOnly`(:1789) 는 `mapGridSettings` 참조라 유닛 4 로 미룸.**
7. **로그 정리**: :1095 의 `shape=…density=…` 는 legacy options 산물 → doc 기반으로 축소 또는 제거.

## 계약

- authored-pool 빌드 경로(913–924 이후 전부)·flow field·prop·effect-tile·connectivity 는 불변.
- 이 유닛 후 `BattleMapBuilder.BuildFromManual/Fixture`·`ProceduralMapGenerator`·`MapData`·`MapPathShape`/`MapObstacleDensity`/`MapGenerationOptions` 는 런타임서 무참조(테스트만) → 유닛 3 삭제 대상.

## 완료 기준

- [x] switch 자체 제거(MapGrid 본문 인라인 — 1-arm switch 무의미), legacy 필드 9종(map/mapSettings/useProcedural/**mapSource**/mapDocument/_manualMapInput/mapPathShape/mapGenerationOptions/_mapGridGridSizeOverride) 제거, 가드 3곳 pool 화(shadow `map` :4040 불변 확인)
- [x] Current*/Set* 맵-config 클러스터 제거(GoalEdgeOnly 쌍은 유닛 4 유지), activeDoc=null 초기화, GridSize/GeneratorVersion → Fallback 상수(라이브 값 20×10/ver1/lane2 와 동일), `RebuildDraftMapCallCount` 카운터 제거(소비 테스트 삭제됨 — `RebuildDraftMap` 본체는 라이프사이클 API 로 유지)
- [x] `BattleBridgeDraftMapTests` 픽스처를 legacy MapData → **라이브 풀 경로**로 재작성(5케이스: 빌드/문서사용/무재빌드/재빌드/폴백. adapter settings-null 검사 탓 settings 인스턴스 주입 — 유닛 4 에서 제거)
- [x] BattleScene 재저장으로 고아 직렬 필드 7종 드롭(diff 검증: ArkFunnel·PrototypeMap·MapGenerationSettings 씬 참조 소멸 — 유닛 3 에셋 삭제 사전 정리)
- [x] compile 0 error, EditMode green(삭제 대상 테스트 제외)
- [ ] (사용자) 스쿼드 Play — 매판 맵 로테이션·배치·pathing·점수 예산 정상(회귀 0)

확인 2026-07-23 — EditMode 1299 중 1297 green(0 fail, 픽스처 재작성 +1케이스). deco-designate 게이트의 `mapSource==MapGrid` 조건도 함께 제거(상시 참). MapSource enum 파일은 무참조화 — 유닛 3 에서 삭제 판정.
