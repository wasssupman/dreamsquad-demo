# 0 — Sand 타일 import

## 목적

사막 그라운드용 스타일라이즈드 sand 텍스처를 프로젝트 소유 에셋으로 확보한다.

## 변경 대상

- `Assets/_Project/Art/Theme/desert/tile_desert_sand.jpg` (신규).

## 구현

- 출처: `Assets/Plugins/PrimeTween/Demo/Stylized Sand by Joao Paulo/Stylized_Sand_001_basecolor.jpg` (스타일라이즈드 심리스 sand, 잔물결+자갈) → `Art/Theme/desert/` 로 복사(플러그인 데모 의존 회피).
- import: `textureType=Sprite`, `spriteImportMode=Single`, `pixelsPerUnit=1024`(=1셀), pivot Center, FullRect, `filterMode=Bilinear`, `compression=Uncompressed`(격자선 방지), `wrap=Repeat`.

## 계약

- 타일 import 규칙(Bilinear+Uncompressed) 준수. [[project_tilemap_grid_lines_cause]]
- 1024² → PPU 1024 → 1 world unit = 1 cell.

## 완료 기준

- import 성공, Sprite 로드 가능(1×1 world). → unit 2 의 `Tile_Sand` 가 이 스프라이트를 사용.

확인: 2026-06-30 import OK (guid 8dc6dc33…, 1024², Bilinear/Uncompressed). 커밋 대기.
