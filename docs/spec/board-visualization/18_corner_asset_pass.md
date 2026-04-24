# 18. Corner Asset Pass (inner / outer corner 마감)

## 목적

audit V-003 에서 확증된 결함:

- inner corner overlay 가 **45° 회전된 사각 패치**로 떠 보임. sprite 가 "꺾인 경계" 로 읽히지 않고 타일 위에 이물질처럼 얹혀 있음.

rev3 `10_place_rendering_finalization.md` 이 inner corner overlay 의 배치 좌표와 yaw 를 고정하고 sprite 는 프로토타입 1 장으로 두었다. 본 spec 은 **corner sprite 자체의 재제작** + **overlay quad 파라미터 재튜닝** 으로 V-003 을 해소한다.

## 전제

- `10` (place rendering finalization) 완료.
- audit V-003 이 `16` 에 기록되어 있음.

## 변경 대상

### Asset
- `Assets/_Project/Art/Theme/forest/tile_forest_place_inner_corner.png` (신규 또는 재제작)
- `Assets/_Project/Art/Theme/forest/tile_forest_place_outer_corner.png` (품질 점검)
- import 설정: mipmap on, trilinear, aniso 4, alpha 보존

### Code
- `Assets/_Project/Scripts/Core/MapView.cs` (`BuildPlaceEdgeOverlays` 의 inner corner branch 좌표/스케일/opacity 조정)
- `Assets/_Project/Scripts/Data/MapThemeData.cs` (band width / opacity param 노출)

### Theme
- `Assets/_Project/Map/Theme/forest/forest.asset` (새 asset + param 값 반영)

## 구현 가이드

### Step 1. Sprite 재설계 요구사항

inner corner sprite 의 시각 요건:

- 45° 회전된 **사각형이 아닌**, 모서리에 들어가는 **호(arc) / fillet 형태**의 투명 배경 sprite
- 안쪽 (place 방향) 은 place base 색감과 자연 연결되는 톤
- 바깥쪽 (Env 방향) 은 soft alpha falloff
- 해상도는 기존 tile 과 동일 (예: 128×128 또는 256×256)
- pivot: 기본 중앙. renderer 가 yaw 45/135/225/315 로 회전해 4방향 사용

outer corner sprite 는 이미 작동 중이면 품질만 점검. 재제작 없을 수 있음.

### Step 2. Overlay quad 파라미터 튜닝

현재 `MapView.BuildPlaceEdgeOverlays` inner corner branch (`10_place_rendering_finalization.md:65` 참조):

```
pos = (±0.28 * tileSize, 0.025~0.034, ±0.28 * tileSize)
yaw = 45/135/225/315
scale = 0.22 * tileSize
```

문제: scale 이 작아 sprite 가 "작은 사각" 으로 보임. 배치 지점이 모서리에 붙지 않고 살짝 안쪽으로 들어와 있음.

튜닝 방향:
- scale 을 `0.32 ~ 0.40 × tileSize` 범위로 확장 (sprite 가 모서리를 덮도록)
- position 을 셀 모서리 지점 `±(0.5 - scale*0.5)` 로 재계산해 overlay 가 정확히 코너를 차지
- opacity (material 의 tint alpha) 를 theme param 으로 노출 → 프로토 품질에서 튜닝 가능

### Step 3. Theme 파라미터 노출

`MapThemeData` 에 추가:

- `float placeInnerCornerScale` (기본 0.36, 0.2~0.5 range)
- `float placeInnerCornerOpacity` (기본 0.6, 0~1)
- `float placeOuterCornerScale` (outer 쪽도 튜닝 필요하면)
- `float placeOuterCornerOpacity`

MapView 는 `_placeEdgeInnerOverlayMaterial` 생성 시 opacity 를 적용한다 (현재 `new Color(0.92f, 1f, 0.92f, 0.45f)` 하드코딩).

### Step 4. null fallback 유지

`placeInnerCornerTexture == null` → overlay skip (기존 유지). outer corner 로 회귀 fallback 금지 (`10`, `14` 계약 유지).

### Step 5. 검증

- forest theme 에 새 inner corner asset 연결
- 동일 audit seed 로 재캡처
- V-003 재평가: inner corner 가 sprite 로 읽히는지
- band 폭 / opacity 가 theme 에서 조절 가능한지 Inspector 에서 확인

## 완료 기준

- audit 재캡처에서 inner corner overlay 가 "회전 사각 패치" 가 아닌 **꺾인 경계 sprite** 로 읽힘
- `placeInnerCornerScale`, `placeInnerCornerOpacity` theme 에서 조절 가능
- forest `placeInnerCornerTexture` 가 재제작된 sprite asset 으로 교체됨
- null fallback 동작 유지 (빈 slot 으로 테스트 시 overlay skip)
- Unity console error 0

## 주의

- Sprite 제작은 디자인 판단 영역. 본 spec 은 요구사항만 정의. 프로토 품질로 먼저 시도 후 audit 재평가 → 재제작 iterate 허용.
- overlay quad 4 방향 동시 배치 케이스 (X 모양 1×1 Place) 에서 sprite scale 이 커지면 중앙 겹침이 발생할 수 있음. 겹침 심하면 scale 또는 opacity 를 축소.
- `19` (place edge finish) 와 같은 시점에 작업하는 걸 권장. inner corner 와 outer edge 는 시각적으로 한 묶음이라 분리 튜닝 시 서로 상쇄될 수 있음.

확인 일자: 2026-04-24 / 커밋 해시: a1c7c98
