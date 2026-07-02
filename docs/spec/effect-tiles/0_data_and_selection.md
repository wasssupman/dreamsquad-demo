# 0 — 데이터 + 셀 선정 (순수)

## 목적

효과 타일의 데이터 정의(SO)와 seed 결정론 셀 선정 순수 함수 + 테스트. 비주얼/배선 없음(unit 1).

## 변경 대상

- 신규 `Assets/_Project/Scripts/Data/EffectTileData.cs` — SO: `id`, `displayName`, `overlayTile(TileBase)`, `stat(StatKind)`, `op(CombineOp)`, `magnitude(float)`
- 신규 `Assets/_Project/Scripts/Data/EffectTilePlacer.cs` — static: `SelectCells(in GeneratedMap map, int seed, int count) → List<int2>`
- 신규 `Assets/_Project/Tests/EditMode/EffectTilePlacerTests.cs`

## 구현

- `SelectCells`: Place 셀 수집 → `Unity.Mathematics.Random.CreateFromIndex((uint)math.max(1, seed ^ 0x9E3779B9))` 로 셔플/선정(0-seed panic 가드 + prop 배치(`BackgroundPropPlacer` 도 map seed 사용)와 decorrelate). 중복 없음, `count` 상한(Place 셀 수보다 크면 전부).
- `BackgroundPropPlacer` 미러 패턴(static class, Wassup.Data).

## 완료 기준

- compile 클린.
- EditMode(`BackgroundPropPlacerTests.Generate_IsDeterministicForSameSeed` 패턴): 같은 seed=같은 결과 / 다른 seed=다른 결과(대체로) / Place 셀만 / count 상한 / 중복 없음.
