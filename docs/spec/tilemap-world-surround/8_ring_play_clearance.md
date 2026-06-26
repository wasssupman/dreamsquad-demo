# 8 — 원경 링 플레이 클리어런스

## 목적

틸트(7) 적용 후, 보드 앞쪽(작은 y) 원경 링의 큰 나무가 `+y`(보드 안쪽)로 누워 플레이 영역을 덮는다.
정밀 occlusion 모델(handoff 후속)은 보류하고, **단순 버퍼**로 해소: 원경 링 프랍을 플레이 셀
(Walk/Place)로부터 Chebyshev `clearance` 타일 이내엔 배치하지 않는다(기본 3).

**근경엔 적용 안 함**(사용자 지정). 실측상 현재 보드(20×10)는 Env 77셀이 전부 플레이 3타일 이내라,
근경에 버퍼를 걸면 보드 내부 프랍이 전멸한다. 근경 큰 프랍 가림은 별도(occlusion 후속).

## 변경 대상

- `Assets/_Project/Scripts/Data/MapThemeData.cs` — `int ringPlayClearanceCells = 3` (0=off)
- `Assets/_Project/Scripts/Core/TilemapMapView.cs` — `InstantiateRingProps` 링 셀 루프에 `NearPlayCell` skip + static 헬퍼

## 구현

- `InstantiateRingProps` 는 시그니처 무변경. 내부에서 자기 `_visualPlan` 을 직접 참조(BattleBridge 무변경).
- 링 셀 `(x,y)`(보드 밖) 루프에서, `clearance>0 && NearPlayCell(_visualPlan, x, y, clearance)` 면 `continue`(skip).
- `NearPlayCell(plan, cx, cy, r)`: `[-r,r]²` 중 보드 내부이고 `zoneType ∈ {Walk, Place}` 인 셀이 있으면 true.
  보드 밖 좌표는 plan 무효 → 비-플레이 취급. `plan==null` → false(=클리어런스 off).
- 거리값은 `theme.ringPlayClearanceCells`(하드코딩 금지). 0 이면 기존 동작.

## 완료 기준

- Play 게임뷰 스크린샷: 플레이 영역 가장자리를 덮던 원경 나무가 사라져 보드 주변이 숨 트임.
  보드 내부 근경 프랍(꽃/돌/나무)은 그대로 유지. 원경 침엽수림은 더 바깥에서 유지.
- `read_console` CS 에러 0. Legacy3D·근경 BackgroundPropPlacer 무영향.
