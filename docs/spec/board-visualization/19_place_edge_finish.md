# 19. Place Edge Finish

## 목적

audit V-004 에서 확증된 결함:

- Place tile 외곽 fringe 가 너무 밝고 모든 방향에서 동일 두께로 반복되어 **grid outline 을 강조**. 배치 구역 분리감을 만드는 게 아니라 오히려 격자감을 드러냄.

본 spec 은 edge sprite 와 배치 파라미터를 분리/마감해 Place 외곽이 배경과 자연스럽게 연결되도록 한다. 18 (corner asset) 과 쌍으로 작업하는 것을 권장.

## 전제

- `10` (place rendering finalization) 완료.
- `18` (corner asset pass) 와 병행 권장.
- audit V-004 가 `16` 에 기록.

## 변경 대상

### Asset
- `Assets/_Project/Art/Theme/forest/tile_forest_place_edge.png` (straight edge 재제작)
- 필요 시 `tile_forest_place_edge_shoulder.png` (Walk 와 맞닿는 shoulder)

### Code
- `Assets/_Project/Scripts/Core/MapView.cs` (`BuildPlaceEdgeOverlays` straight edge branch, corner 연결부 중복 제거)
- `Assets/_Project/Scripts/Data/MapThemeData.cs` (edge opacity / band width param 노출)

### Theme
- `Assets/_Project/Map/Theme/forest/forest.asset`

## 구현 가이드

### Step 1. Edge sprite 재제작

현재 edge sprite 가 "밝은 흰색 스트립" 톤. Place base 와 대비가 너무 강함.

요건:
- sprite 는 **alpha gradient fringe** (center ~0.4, 양 끝 ~0.0 opacity)
- 색감은 Place base 색보다 약간 밝지만 Env 쪽으로 갈수록 fade
- 직선 구간에서 반복 배치 시 seam 이 보이지 않도록 가로 방향 tiling 지원
- 해상도 기존과 동일 또는 upscale

optional: Walk 와 맞닿는 변은 shoulder sprite 로 분리 (색감/두께가 다를 수 있음).

### Step 2. Overlay 배치 파라미터 튜닝

현재 (짐작):
```
outer edge: scale (tileSize * 0.66, tileSize * 0.14, 1), pos ±0.385 * tileSize
inner edge: scale (tileSize * 0.54, tileSize * 0.10, 1)
```

튜닝:
- outer edge 두께를 `tileSize * 0.10` 이하로 얇게
- opacity 는 theme param (기본 0.35~0.45)
- 직선 edge 4 방향을 동일 파라미터로 두되, **outer corner 셀에서는 2 방향 straight edge 를 skip** 하고 corner sprite 가 담당 (중복 제거)

### Step 3. Corner 연결부 처리

현재 outer corner shape 인 셀에서는 두 변이 만나는 지점에 직선 edge 와 corner sprite 가 겹쳐 배치될 수 있음. 중복 방지:

- `shapeClass == OuterCorner*` 인 셀은 **corner sprite 만** 사용, 직선 edge overlay 는 skip
- `shapeClass != OuterCorner*` 이지만 `transitionMask` 비트가 있는 셀만 straight edge overlay

즉 edge 와 corner 를 셀 단위로 **둘 중 하나만** 선택.

### Step 4. Theme 파라미터

`MapThemeData` 에 추가:
- `float placeEdgeOpacity` (기본 0.4)
- `float placeEdgeThickness` (기본 0.10, tileSize 비율)
- `float placeEdgeFalloff` (기본 0.5, sprite 자체 alpha 와 곱)

### Step 5. null fallback 유지

`placeEdgeTexture == null` → edge overlay skip. corner 로 회귀 fallback 금지.

### Step 6. 검증

- 동일 audit seed 로 screenshot 재캡처
- V-004 재평가: Place 외곽이 grid outline 이 아니라 배경과 연결된 fringe 로 읽히는지
- corner 연결부 중복 artifact 없음 (outer corner 셀에서 edge 와 corner 가 겹치지 않음)
- theme param 으로 opacity / thickness 조절 가능 확인

## 완료 기준

- audit 재캡처에서 Place 외곽이 "grid outline" 이 아닌 자연스러운 fringe 로 읽힘 (V-004 심각도 Mid 이하로 강등)
- outer corner 셀과 straight edge 가 중복 배치되지 않음 (코드 path grep)
- `placeEdgeOpacity`, `placeEdgeThickness` theme 에서 조절 가능
- forest `placeEdgeTexture` 재제작 asset 으로 교체
- null fallback 동작 유지
- Unity console error 0

## 주의

- `18` (corner asset) 과 시각 점검을 같이 해야 서로 상쇄 판단 가능.
- edge 와 corner sprite 가 서로 다른 "재료감" 으로 보이면 보드 일관성이 깨짐. palette/톤은 18, 19 간 조율.
- Walk 와 맞닿는 변에 shoulder sprite 를 도입하는 건 optional. 본 spec 스코프에서는 straight edge 재제작이 우선, shoulder 는 22 (theme palette pass) 에서 재검토.
- Place 내부 seam 문제 (slab variant 경계) 는 본 spec 범위 아님. 20 또는 별도 spec.

확인 일자: 2026-04-24 / 커밋 해시: 818712b
