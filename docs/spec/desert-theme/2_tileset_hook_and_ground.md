# 2 — 테마-구동 tileSet 훅 + 사막 그라운드

## 목적

Tilemap 모드 바닥이 테마 무관(scene `BattleBridge.tileSet`)이던 문제 해결. 테마가 자기 TileSetData 를 지정하면 그걸로 바닥을 칠하게 하고, 사막 바닥용 `TileSet_Desert` + `Tile_Sand` 를 만든다.

## 변경 대상

- `Assets/_Project/Scripts/Data/MapThemeData.cs` — `public TileSetData tileSet;` 필드 추가(Header "Tilemap Ground").
- `Assets/_Project/Scripts/Bridge/BattleBridge.cs` — `tilemapMapView.Initialize` 의 tileSet 인자를 `theme.tileSet != null ? theme.tileSet : tileSet` 로.
- `Assets/_Project/Data/TileSets/Tile_Sand.asset` (신규 Tile), `TileSet_Desert.asset` (신규 TileSetData).
- `Assets/_Project/Art/Theme/desert/tile_desert_sand.jpg` (Sprite, PPU 1024, Bilinear/Uncompressed).

## 구현

- **코드 훅**: theme 지정 시 그 TileSetData, 아니면 scene fallback. forest 는 `tileSet=null` → fallback(무영향). ~5줄.
- **Tile_Sand**: `UnityEngine.Tilemaps.Tile`, sprite=sand(1×1 world, center), color 살짝 de-salmon, collider None.
- **TileSet_Desert**: `TileSet_AutoTileTest` 복제 → `envTile`/`decoTile`/`terrainTile` = Tile_Sand, `surroundFarColor` 샌디. walk(AutoTile_PlaceStone)·place(AutoTile_PlaceDirt)·overlay 타일은 공유 유지.
- `desert.asset.tileSet = TileSet_Desert`.

## 계약

- 테마-구동 tileSet 은 fallback 패턴 — 기존 forest/씬 동작 불변(`tileSet` 미지정 시 scene 필드 그대로).
- 바닥 변경은 코드+타일 에셋. 씬 변경 없음(theme 이 tileSet 운반).

## 완료 기준

- compile 0. Play(desert 활성) 에서 바닥이 sand 로 렌더(`tile_desert_sand` 가 Ground Tilemap 에 페인트됨 — 확인).
- forest 활성 시 기존 바닥 불변.

확인: 2026-06-30 컴파일 0, desert 바닥 sand 렌더 확인(스샷 `desert_v2_ground.png`/`desert_v3_final.png`). 커밋 5ebe315.
