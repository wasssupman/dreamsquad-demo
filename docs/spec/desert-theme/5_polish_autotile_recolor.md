# 5 — 폴리시: 사막 오토타일 엣지 recolor + 색조

## 목적

desert 의 teal 오토타일 엣지(grass blend)·salmon sand·녹색 프랍 moss 를 다듬는다.

## 변경 대상

- `Assets/_Project/Generated/Tiles/Desert/*.png(+meta)` (107, Test/ recolor 사본).
- `Assets/_Project/Data/TileSets/AutoTile_PlaceDirt_Desert.asset`, `AutoTile_PlaceStone_Desert.asset` (신규).
- `Assets/_Project/Data/TileSets/TileSet_Desert.asset` (place/walk → desert 오토타일).
- `Assets/_Project/Data/TileSets/Tile_Sand.asset` (color), `Assets/_Project/Map/Theme/desert/desert.asset` (propGlobalTint).

## 구현

- **teal→sand recolor (PIL)**: 오토타일 스프라이트는 dirt/stone 형상 + teal 배경(grass blend)이 베이크됨. HSV hue 범위(cyan-green, H 0.30~0.60, S≥0.12) → sand hue(0.083)로 remap, V 보존(텍스처 유지). dirt(brown)/stone(gray) 인테리어 불변.
- 각 Test/X.png → Desert/X.png + `.meta` 사본(guid만 새로) → 임포트 설정 동일 보존(PPU 130 등). 스프라이트 guid 매핑 후 AutoTile 사본의 sprite guid 텍스트 치환(dirt 31·stone 46).
- `TileSet_Desert` place=`AutoTile_PlaceDirt_Desert`, walk=`AutoTile_PlaceStone_Desert`.
- `Tile_Sand.color` (0.88,0.90,0.66) de-salmon. `desert.propGlobalTint` (1,0.78,0.52) 웜 강화 → moss khaki 화.

## 계약

- recolor 는 Test/ 원본 불변(사본만). forest 무영향.
- 메타 사본+신 guid 로 임포트 설정 일치(별도 설정 코드 불필요).

## 완료 기준

- 컴파일/콘솔 0, 미싱 스프라이트 0. Play(desert): 바닥/엣지 사막 통일(teal 제거), sand 골든탄, moss 완화.
- 사용자 육안 통과(현 상태 커밋). 잔여 통나무/보울더 녹색은 수용.

확인: 2026-06-30 사용자 통과(현 상태 커밋). 스샷 `desert_v4_polish.png`.
