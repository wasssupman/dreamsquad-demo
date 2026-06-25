# 1 — MapGrid 내부 Deco designation

## 목적

MapGrid 맵은 내부 셀이 전부 `Walk`/`Place` 라 '빈 타일'이 없다. 배경 프랍이 앉을 **`Deco` 셀을
생성 후 데이터로 만든다**. 옛 `ProceduralMapGenerator` 의 자연 분포(buildable dirt 블롭 + 나머지
decorative)를 MapGrid 에 도입. 시드 결정적, `Walk` 경로 불변, buildable 솔리드 블롭 보존.

## 변경 대상

- `Assets/_Project/Scripts/Data/ObstaclePlacer.cs` — `DesignateDeco(...)` 추출(재사용)
- `Assets/_Project/Scripts/Data/MapThemeData.cs` — `mapGridBuildableKeepRatio` 필드
- `Assets/_Project/Scripts/Bridge/BattleBridge.cs` — MapGrid 빌드 후 designate 호출

## 구현

- `ObstaclePlacer` 리팩터: 기존 `Place()` 의 블롭 코어를 `public static void DesignateDeco(ref Random rng,
  NativeArray<MapTileType> tiles, int2 gridSize, float keepFraction)` 로 추출. `keepFraction` = 남길 Place 비율.
  `Place()` 는 `keepFraction = clamp(ratio,0.2,0.8)*0.85` 로 위임 → **옛 경로 동작 불변**(동일 keepTarget 수식).
- `MapThemeData` 에 `[Range(0,1)] public float mapGridBuildableKeepRatio = 1f;` 추가. **기본 1 = off**
  (keepTarget≥placeCount → DesignateDeco early-return). 시즌 `MapThemeData` 에셋에서 <1 로 켠다.
- `BattleBridge.BuildMapForBattle`: MapGrid 빌드 + connectivity 직후, `tilemapMapView.Initialize`(페인트/VisualPlan) **이전**에:
  `if (mapSource == MapSource.MapGrid && theme != null && theme.mapGridBuildableKeepRatio < 1f)` →
  시드 파생 `Random` 으로 `ObstaclePlacer.DesignateDeco(ref rng, _generatedMap.tiles, _generatedMap.gridSize, theme.mapGridBuildableKeepRatio)`.
- 결정성: `var rng = Random.CreateFromIndex((uint)(seed ^ 0x5A5A5A) | 1u);` (생성 rng 와 분리).
- `Deco` 는 `Walk` 미변경 → 경로/connectivity 불변. `mergeDegree`/`chokepoint`/`propLayerId` 는 path 기반이라 영향 없음.

## 비포함 (다음 단위)

- Deco 셀에 실제 프랍 인스턴스화 → 단위 2.
- decoTile 아트 튜닝(현재 `decoTile == envTile` 로 렌더). 시즌별 grass 차별화는 후속.

## 완료 기준

- compile 0 에러. 옛 ProceduralMapGenerator/Legacy 의 ObstaclePlacer 경로 동작 불변(동일 keepTarget).
- 시즌 mapTheme `mapGridBuildableKeepRatio<1` 설정 시, Tilemap 보드 내부에 grass(Deco) 패치가 시드 결정적으로 등장.
- 배치 가능 셀은 솔리드 블롭으로 보존(swiss-cheese 아님), `Walk` 경로 무변화, 배치/이동 정상.
