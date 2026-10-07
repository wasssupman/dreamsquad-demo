# 0 — `Tile` 껍데기 → `Sprite`

## 목적

오버레이가 `Tile` 에셋에서 `.sprite` 만 꺼내 쓰므로, 저작 필드를 `Sprite` 로 바꾸고 `Tile` 에셋 13개를 지운다. 그림은 같은 png 를 가리킨다.

## 변경 대상

1. **`Scripts/Data/TileSetData.cs` → `BoardOverlayStyle.cs`**(git mv, GUID 유지 — 씬의 `CoreMapOverlay._tileSet` 참조가 산다). 클래스명 `BoardOverlayStyle`, `CreateAssetMenu` 이름 `Wassup/Board Overlay Style`.
   - 삭제: `TileBase goalTile · spawnTile · hoverTile · rejectTile`(「Overlay markers」 헤더째) · `rangeTile` · `aimRangeTile` · `telegraphTile` · `placeableTile` — 소비처 0.
   - 교체: `TileBase blockedTile` → `Sprite blockedSprite`(툴팁 유지).
   - `using UnityEngine.Tilemaps` 제거. `RangeRingStyle` 등 색·알파 knob 는 그대로.
2. **`Scripts/Data/EffectTileData.cs`**: `TileBase overlayTile` → `Sprite overlaySprite`. `using UnityEngine.Tilemaps` 제거.
3. **`BattleCoreUnity/View/CoreMapOverlay.cs`**: `TileOf()`·`as UnityEngine.Tilemaps.Tile` 분기 제거, `_tileSet.blockedTile` → `_tileSet.blockedSprite`, 효과 타일 `data.overlayTile.sprite` → `data.overlaySprite`. 필드 `_tileSet` 타입 `BoardOverlayStyle`. 「`TileBase` 는 스프라이트를 직접 노출하지 않는다」 주석 삭제.
4. **`BattleCoreUnity/MatchViewAssets.cs:57`** 주석의 `overlayTile` 언급.
5. **에셋 YAML**(열린 씬이 아니라 직접 편집 가능):
   - `Data/TileSets/TileSet_Overlay.asset` → `Data/BoardOverlayStyle.asset`(git mv): 삭제된 8 필드 줄 제거 · `blockedTile: {… Tile_LandingTelegraph}` → `blockedSprite: {fileID: 21300000, guid: <tile_range_solid.png>, type: 3}` · `m_EditorClassIdentifier` 갱신.
   - `Data/EffectTiles/effect_tile_*.asset` 5: `overlayTile: {… ET_X.asset}` → `overlaySprite: {fileID: 21300000, guid: <Art/EffectTiles/ET_X.png>, type: 3}`.
6. **삭제**: `Data/TileSets/{PH_Goal,PH_Spawn,PH_Hover,PH_Reject,tile_grid_outline,tile_range_solid,Tile_LandingTelegraph,Tile_PlaceableSlab}.asset` 8 · `Data/EffectTiles/ET_*.asset` 5 · 참조가 끊기는 png `tile_grid_outline.png` · `placeable_slab.png`(격자선과 배치 칸은 `CellSprite()`·`LineRenderer` 로 **생성**하므로 안 쓴다). **남기는 png**: `tile_range_solid.png`(blocked) · `Art/EffectTiles/ET_*.png` 5.
   - `Data/TileSets/` 에 남는 건 `PlacementLiquidTile.mat` · `PlacementRangeRing.mat` · `tile_range_solid.png` → 폴더를 `Data/BoardOverlay/` 로 옮기고 스타일 에셋도 거기에.
7. **테스트**: `PlayModeCore/CorePlacementHighlightTests.cs:36`(`set.blockedTile as Tile` → `set.blockedSprite`) · `PlayModeCore/CoreViewRemainderTests.cs:77`(`data.overlayTile as Tile` → `data.overlaySprite`). `EditModeCore/EffectTileTests` 는 코어 선택 로직이라 무관(확인만).

## 구현

- 순서: 코드(1~4) → 에셋 YAML(5) → 삭제(6) → 테스트(7). 에디터가 열려 있으면 코드 저장 직후 재컴파일, YAML 은 refresh 때 반영.
- `git mv` 로 옮기는 `.cs`·`.asset` 은 `.meta` 를 같이 — GUID 가 곧 씬·SO 의 참조다.
- 삭제 전 GUID 역참조(세션 스크래치 `guidrefs.py query`)로 13 에셋·png 2 의 바깥 참조가 위 YAML 편집 후 0 임을 확인한다.

## 구현 결과 (2026-10-07)

- `TileSetData` → **`BoardOverlayStyle`**(git mv, GUID 유지). `TileBase` 9 필드 중 8 삭제, `blockedTile` → `Sprite blockedSprite`. `EffectTileData.overlayTile` → `Sprite overlaySprite`. 두 파일에서 `using UnityEngine.Tilemaps` 소멸.
- `CoreMapOverlay`: 필드 `_tileSet` → `_style`(`FormerlySerializedAs("_tileSet")` 로 씬 참조 유지 — 씬이 열려 있어 YAML 은 손대지 않음) · 프로퍼티 `TileSet` → `Style` · `GuideTile()` → `GuideSprite()` · 타일 색 곱(`tile.color`) 제거(모든 타일이 흰색이라 픽셀 동일). 효과 타일은 `overlaySprite` 직접.
- 에셋: `Data/TileSets/` → **`Data/BoardOverlay/`**(스타일 SO `BoardOverlayStyle.asset` + 머티리얼 2 + `tile_range_solid.png`). `Tile` 에셋 13(`PH_*` 4 · `tile_grid_outline` · `tile_range_solid` · `Tile_LandingTelegraph` · `Tile_PlaceableSlab` · `ET_*` 5)과 png 2(`tile_grid_outline` · `placeable_slab`) 삭제. 효과 타일 SO 5 는 `Art/EffectTiles/ET_*.png` 스프라이트를 직접 가리킨다.
- 테스트 3(`CorePlacementHighlight` · `CoreViewRemainder` · `CoreShapeGuide`) 를 스프라이트 기준으로.
- GUID 역참조: 삭제 후 새로 끊긴 참조 0.

## 완료 기준

- [x] `rg "UnityEngine.Tilemaps|TileBase" Assets/_Project/Scripts/Data Assets/_Project/Scripts/BattleCoreUnity Assets/_Project/Tests` → 0
- [ ] 에디터 컴파일 0 · missing reference 0 · Play: 놓을 수 없는 칸(점유·지형 2색) · 효과 타일 5종 · 격자선 · 사거리 링 · 착지 예고가 전과 같다
- [x] 커밋(경로 지정)
