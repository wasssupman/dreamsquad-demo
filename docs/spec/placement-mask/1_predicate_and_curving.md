# 1. 판정 교체 + 런타임 커빙 재해석

## 목적

배치 판정의 데이터 소스를 `tiles==Place` 에서 `placeMask` 로 교체한다. 판정 지점이 `SpatialPlacementCheck` 하나로 수렴돼 있으므로(placement-eligible-tile-highlight 계약) 하이라이트·D&D·재배치·탭 배치가 자동 추종한다. 런타임 커빙은 마스크와 동기가 깨지지 않게 재해석한다.

## 변경 대상

- `Assets/_Project/Scripts/Bridge/BattleBridge.cs` — `SpatialPlacementCheck`(4985~) · 커빙 블록(1113~1123)
- `Assets/_Project/Scripts/Data/EffectTilePlacer.cs`
- `Assets/_Project/Tests/EditMode/` — `SpatialPlacementCheckTests` · `EffectTilePlacerTests` 확장

## 구현

1. **SpatialPlacementCheck**: `if (map.TileAt(cell) != MapTileType.Place)` → `if (!map.PlaceableAt(cell))`. reason(`NotBuildable`)·순서·나머지 게이트 불변.
2. **커빙 skip 조건 확장** (BattleBridge 커빙 블록): 기존 `hasAuthoredDeco` 에 더해 **`hasAuthoredMaskIntent`** — **bool 비교** `(placeMask[i] != 0) != (tiles[i] == MapTileType.Place)` 인 셀이 하나라도 있으면 true — 일 때도 커빙을 skip 한다. 의미: "마스크가 파생값과 상이 = 저작자가 배치판을 손으로 지정" (authored-Deco 규칙과 동형). 파생 마스크(≡ tiles==Place)로만 저장된 재베이크 맵은 상이 셀이 없으므로 기존처럼 커빙된다 — 회귀 없음. (참고: 실맵 6종은 전부 authored Deco 보유라 이미 커빙 skip — 라이브 커빙 대상은 connectivity 폴백맵과 미래의 all-Place 문서뿐.)
3. **커빙 후 마스크 재파생**: `DesignateDeco` 실행 직후 `placeMask[i] = tiles[i]==Place ? 1 : 0` 로 갱신(커빙은 파생-마스크 맵에서만 도니 재파생이 정확히 동기). `ObstaclePlacer` 시그니처·내부 불변.
4. **EffectTilePlacer.SelectCells**: 셀 수집 조건 `map.TileAt(cell)==Place` → `map.PlaceableAt(cell)`. 파생 마스크에서 결과 불변(호출이 커빙 이후이므로).

## 완료 기준

- compile 클린.
- EditMode: `SpatialPlacementCheckTests` 에 마스크 픽스처 추가 — ① Walk 셀 mask=1 → `None`(배치 허용) ② Place 셀 mask=0 → `NotBuildable` ③ 마스크 미생성 → 기존 tiles 동작 그대로. `EffectTilePlacerTests` 에 mask 우선 케이스 1건.
- EditMode: **커빙 skip 조건 테스트** — intent 판정을 순수 함수로 추출해 ① 파생 동일 마스크 → intent=false ② 상이 셀 1개 → intent=true 검증. (`DesignateDeco` 는 현재 테스트 커버 0 — 구 `ObstaclePlacerTests` 는 map-pipeline-cleanup 에서 파일째 삭제됨. 이 유닛이 skip 경로에 첫 커버를 얹는다.)
- 기존 맵(파생 마스크) Play 무회귀: 하이라이트 셀·배치 가능 칸이 이 유닛 이전과 동일, 커빙(keepRatio) 동작 동일.
