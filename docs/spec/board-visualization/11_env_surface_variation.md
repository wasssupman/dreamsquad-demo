# 11. Env Surface Variation

## 목적

Env region 을 region-uniform 한 장 텍스처에서 **noise-driven sub-tile variation + region 간 blend** 로 교체. 큰 Env region 이 단조롭지 않고, region 끼리 맞닿는 경계가 hard cut 으로 보이지 않아야 한다.

## 전제

- `9` (cell `surfaceNoiseHash` 필드 채움) 완료.
- `10` (renderer 가 plan 만 소비) 완료.

## 변경 대상

- `Assets/_Project/Scripts/Core/MapView.cs` (`BuildEnvironmentSurfaces`, `BuildEnvironmentRegionSurface`)
- `Assets/_Project/Scripts/Data/TerrainSurfaceSelector.cs`
- `Assets/_Project/Scripts/Data/MapThemeData.cs` (env variation 슬롯 정비)
- `Assets/_Project/Map/Theme/forest/forest.asset`
- 테스트: `TerrainSurfaceSelectorTests` 확장

## 정의: region-level base vs cell variation

혼란 방지 계약:

- **region-level base** = `anchorCell` 위치에서 `TerrainSurfaceSelector.SelectRuleTexture` 로 결정한 dominant texture. region 간 blend 에서 "이 region 의 대표 텍스처".
- **cell variation** = 개별 셀에서 `SelectRuleTexture` 를 한 번 더 호출해 받은 local texture. region 내부 variation.

두 결과가 다를 수 있다 (noise hash 가 다름). renderer 는 아래 규칙으로 합친다.

## 구현 가이드

1. `TerrainSurfaceSelector.SelectRuleTexture` 를 **Env 경로에서도 셀 단위로** 호출. 현재 anchorCell 한 점만 사용하는 경로를 region 내부 모든 셀로 확장.
2. region 내부 렌더:
   - 셀 단위 sub-quad 로 돌지 않고, **같은 texture 가 연속되는 run** 을 묶어 tiled mesh 하나로 빌드.
   - 현재 `BuildEnvironmentRunSurface` 는 row-run. 이를 "같은 texture 로 묶는 run" 으로 확장.
3. Region 간 blend:
   - 두 Env region 의 **region-level base** 가 다르면 맞닿은 1 셀 폭에 fringe quad (half-opacity) 올림.
   - fringe 텍스처는 인접 region 의 region-level base (alpha 0.5 로 교차).
4. `surfaceNoiseHash` 활용:
   - selector 에 noise hash 인자 → deterministic 보장.
   - 동일 (seed, cell) → 동일 texture.
5. 테마 데이터 정리:
   - `envSurfaceRules` 를 variation 배열로 유지 (최소 2 종).
   - `envTileTexture`, `envTileVariants` 는 deprecated (`14` 에서 격리).

## 완료 기준

- Env region 하나 안에서 2 종 이상 surface texture 관찰 (screenshot).
- 인접 Env region 의 region-level base 가 다를 때 경계 1 셀에 blend 보임.
- 같은 seed 에서 동일 variation 패턴 재현.
- `TerrainSurfaceSelector` 가 Env 경로에서 호출됨 (grep).
- EditMode: 동일 `(seed, cell, ruleSet)` selector 결과 일관성.

## 주의

- noise scale, variation weight 는 theme data 에서 조절. 하드코딩 금지.
- blend band width 1 셀 고정 (v0). 더 넓으면 zone readability 훼손.
- Env detail scatter (풀/꽃) 는 이미 `BuildTerrainDetails` 가 anchor 기반으로 처리 중. 여기서는 **base surface 만** 수정.
- region 크기 1 셀이면 region-level base == cell variation 이므로 blend skip.

확인 일자: 2026-04-24 / 커밋 해시: 0978663
