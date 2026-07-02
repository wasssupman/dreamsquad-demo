# 1 — 비주얼 + 맵빌드 배선

## 목적

런타임 효과 타일맵과 `AddEffectTile` 진입점을 만들고, 맵 빌드 시 unit 0 의 `SelectCells` 로 효과 타일을 배치·표시한다. 효과 부여는 unit 2.

## 변경 대상

- `Assets/_Project/Scripts/Core/TilemapMapView.cs` — 런타임 효과 타일맵(lazy 생성: grid 하위 GO + Tilemap + TilemapRenderer, anchor 0.5/0.5, sorting −15, cast off) + `SetEffectTile(cell, TileBase)`/`ClearEffectTile(cell)` + `Clear()` 에서 ClearAllTiles
- `Assets/_Project/Scripts/Bridge/BattleBridge.cs` — `[SerializeField] EffectTileData[] effectTiles` + `[SerializeField] int effectTileCount = 3` + `Dictionary<Vector2Int, EffectTileData> _effectTilesByCell` + `AddEffectTile(cell, data)` + 맵 빌드 배선
- authoring: `EffectTileData` 에셋 3종(공격력/공속/받는피해) + placeholder overlayTile(기존 PH 스프라이트 + 틴트: 버프=초록/파랑, 디버프=빨강) + BattleBridge 배열 지정

## 구현

- `AddEffectTile(cell, data)`: dict 등록(셀당 1개, 덮어쓰기) + `tilemapMapView` null 가드(Legacy3D 미동작) 후 페인트. 점유 셀 즉시 적용 스텁은 unit 2 에서 채움(주석: "현재 후속 런타임 생성 루트에서만 도달").
- 맵 빌드 배선: `BuildMapForBattle` 의 `InstantiateStructureProps` 호출 근처(= `Initialize`/Clear **이후** — 페인트 순서 계약) — dict clear → `SelectCells(_generatedMap, _generatedMap.seed, effectTileCount)` → 셀마다 seed 스트림으로 effectTiles 중 종류 배정 → `AddEffectTile`.
- 효과 타일맵은 `overlayTilemap` 과 별개 — hover/reject 와 시각 충돌 없음.

## 완료 기준

- compile 클린.
- Play: Place 셀 위 효과 타일 3개 표시, hover 지나가도 효과 타일 유지, draft→placement 유지. 콘솔 클린. 스크린샷 육안.
