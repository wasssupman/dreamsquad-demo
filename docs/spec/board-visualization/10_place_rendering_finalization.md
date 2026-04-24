# 10. Place Rendering Finalization + Renderer 해석 단일화

## 목적

Place edge/corner 렌더를 `shapeClass` + `innerCornerMask` 기반으로 재구성. `MapView` 의 `map.TileAt` 직접 참조를 제거해 renderer 가 `BoardVisualPlan` 만 읽도록 단일화. legacy `TerrainTileShape*` / `TerrainTileRuleResolver` 정리.

## 전제

- `9` (shape mask + cell 필드 확장) 완료.

## 변경 대상

- `Assets/_Project/Scripts/Core/MapView.cs`
- `Assets/_Project/Scripts/Data/TerrainTileRuleResolver.cs` (축소 or 제거)
- `Assets/_Project/Scripts/Data/TerrainTileShape.cs` / `TerrainTileShapeUtility.cs` (제거)
- `Assets/_Project/Scripts/Data/MapThemeData.cs` (inner corner sprite slot 추가)
- `Assets/_Project/Map/Theme/forest/forest.asset`

## 구현 가이드

1. `TerrainTileShape` / `TerrainTileShapeUtility` 파일 삭제. 모든 consumer 는 `BoardShape*` 또는 `plan.CellAt.shapeClass` 로.
2. `TerrainTileRuleResolver` 처리 (택 1):
   - `Resolve(BoardVisualPlan, cell)` 시그니처로 재작성
   - 통째 제거하고 `MapView` 가 plan 을 직접 소비
3. `MapView.BuildTiles`:
   - Walk 셀: `visualCell.shapeClass` → theme `walkShapeSet` sprite + yaw
   - Place 셀: base slab (또는 variant) → `OuterCorner*` 면 corner sprite → `innerCornerMask` 비트마다 inner corner overlay quad
4. `BuildPlaceEdgeOverlays` 는 mask 스트립에서 **shape + innerCornerMask 기반 sprite** 로 교체:
   - outer corner 구간: `placeOuterCornerTexture`
   - 직선 구간: `placeEdgeTexture`
   - inner corner: `placeInnerCornerTexture` overlay
5. **한 셀 최대 4 inner corner overlay 케이스 처리**:
   - X 모양 1×1 셀 (모든 cardinal 동일 zone + 모든 diagonal 다른 zone) 이 이론상 가능
   - z-order: overlay 는 `baseHeight + 0.003 × (bitIndex + 1)` 로 계층 분리. z-fighting 방지
   - 4 overlay 동시 배치 허용 (4 quad 추가만)
6. `map.TileAt` 호출 완전 제거. 모든 판정은 `_visualPlan.CellAt`.
7. `MapThemeData` 슬롯 추가:
   - `placeOuterCornerTexture`
   - `placeInnerCornerTexture`
   - `placeEdgeTexture` (기존 `placeBackgroundEdgeTexture` 재활용 가능)
8. **null fallback 정책**:
   - `placeInnerCornerTexture == null` → inner corner overlay **skip**. `placeOuterCornerTexture` 로 회귀 fallback 금지.
   - `placeOuterCornerTexture == null` → base slab 만, outer corner sprite skip.
   - `placeEdgeTexture == null` → edge fringe skip.
   - 모든 null 케이스에서 렌더 에러 없이 동작.

## 완료 기준

- L자 Place region 의 inner corner 에 overlay 가 보임 (screenshot).
- X 모양 1×1 Place 셀에서 4 overlay 가 z-fighting 없이 그려짐.
- `MapView` 내부 `map.TileAt` / `MapTileType` 직접 참조 grep 0.
- `TerrainTileShape` / `TerrainTileShapeUtility` 파일 삭제 확인.
- `TerrainTileRuleResolverTests` 제거 또는 plan 입력 기반으로 재작성.
- theme 의 inner corner 슬롯이 null 일 때 렌더 에러 없이 overlay skip.

## 주의

- Env 렌더 변경은 이 단계 밖 (`11`).
- prop instantiate 는 변경 없음 (`13` 에서 rotation/scale).
- inner corner overlay 가 outer corner 와 시각 충돌 심하면 band width theme param 으로 조절. rev3 기본값 outer=0.12, inner=0.09.

확인 일자: 2026-04-24 / 커밋 해시: 194263b
