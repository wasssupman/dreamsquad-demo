# 3. 구 pre-MapGrid 절차 생성기 삭제

## 목적

유닛 2 로 무참조가 된 구세대 절차 생성 코드(시드→맵)를 실제로 제거한다. authored MapDocument 로 완전 대체된 세대다.

## 변경 대상

- 삭제(전체): `Assets/_Project/Scripts/Data/ProceduralMapGenerator.cs`, `PathCarver.cs`, `MapData.cs`(+ `TileType` enum), `ManualMapInput`(정의 파일 — 위치 확인 후)
- 수정(메서드만): `Assets/_Project/Scripts/Data/BattleMapBuilder.cs` — `BuildFromManual`/`BuildFromFixture` 제거, **`BuildFallbackLinear` 유지**. **동반 제거(리뷰)**: private `MapTile(TileType)`(:204, BuildFromFixture 전용 — TileType 삭제로 dangling), `MarkCells`(:162, BuildFromManual 전용 dead), `PrototypeMapPath` 상수/`MapData` using tail.
- 수정(메서드만): `Assets/_Project/Scripts/Data/ObstaclePlacer.cs` — `Place`(:9) 제거, **`DesignateDeco`/`TryKeep`/`FindUnkeptPlace`/`HasKeptNeighbor8` 유지**(Place 무의존 — 리뷰 CONFIRM). :25 stale 주석 정리.
- 삭제(에셋): `Data/Maps/MapDocument_ArkFunnel.asset`, `Scripts/Data/Maps/PrototypeMap.asset`(MapData — 실경로 주의), `MapGenerationSettings.asset`
- 삭제(설정 타입, 무참조 시): `Data/MapGenerationSettings.cs`, `MapGenerationOptions`/`MapPathShape`/`MapObstacleDensity`
- 테스트: 통삭제 `ProceduralMapGeneratorTests.cs`·`PathCarverTests.cs`·`ManualMapInputTests.cs`·**`ObstaclePlacerTests.cs`**(리뷰: 유일 테스트가 `Place` 호출 — DesignateDeco 직접 케이스 부재라 부분 유지 불가, 통삭제); **부분** `BattleMapBuilderTests.cs`(legacy `BuildFromFixture_…` 제거 / live `BuildFallbackLinear_…` :45/:65 유지)

## 구현

1. BattleMapBuilder 에서 manual/fixture 메서드 + 전용 private 헬퍼(MapTile/MarkCells) 제거 → `MapData`/`ManualMapInput`/`TileType` 참조 소멸 확인.
2. ObstaclePlacer.Place 제거(유일 호출처 ProceduralMapGenerator 도 삭제되므로 무참조). DesignateDeco 는 손대지 않음.
3. ProceduralMapGenerator/PathCarver/MapData/ManualMapInput `.cs` + meta 삭제.
4. legacy 에셋 3종 삭제 — GUID grep 으로 ArkFunnel(단일 mapDocument 필드, 유닛 2 로 소멸)·PrototypeMap·MapGenerationSettings.asset 이 BattleScene legacy 필드 외 무참조 재확인.
5. 설정 타입 무참조면 삭제. `MapGenerationOptions`/`MapPathShape`/`MapObstacleDensity` 가 GameManager/BattleBridge/DraftController 에서 완전히 빠졌는지 확인(유닛 1·2 반영분).
6. 테스트: legacy-only 파일(ObstaclePlacerTests 포함) 통삭, 혼재 BattleMapBuilderTests 는 FallbackLinear 케이스만 남김.

## 계약

- **DesignateDeco·BuildFallbackLinear 는 살아있는 keep-set** — 제거하지 않는다. 이 유닛은 `Place`·`BuildFromManual`·`BuildFromFixture` + 그 전용 헬퍼만 걷어낸다.
- `DesignateDeco` 는 원래부터 직접 테스트가 없다(pre-existing gap, 이 정리의 회귀 아님) → 슬림 테스트 신설은 **후속 후보**.

## 완료 기준

- [ ] ProceduralMapGenerator/PathCarver/MapData/ManualMapInput + meta 삭제, legacy 에셋 3종 삭제
- [ ] BattleMapBuilder=FallbackLinear only(MapTile/MarkCells 제거), ObstaclePlacer=DesignateDeco only(live 메서드 온전)
- [ ] MapGenerationOptions/PathShape/ObstacleDensity 무참조 확인 후 제거(잔존 시 사유 기록)
- [ ] compile 0 error, EditMode green(삭제 테스트 제외, DesignateDeco 간접·FallbackLinear 케이스 통과 유지)
- [ ] refresh 후 콘솔 missing-reference 0
