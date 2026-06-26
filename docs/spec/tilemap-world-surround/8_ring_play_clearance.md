# 8 — 원경 링 플레이 클리어런스 (하단 한정)

## 목적

틸트(7) 적용 후, 원경 링의 큰 나무가 `+y`(보드 안쪽)로 누워 플레이 영역을 덮는다. 단순 버퍼로 해소:
원경 링 프랍을 플레이 셀(Walk/Place) Chebyshev `clearance` 이내엔 배치하지 않는다(기본 3).

**rev(unit 10 맥락) — 하단(`-y`)만 적용**: 틸트가 `+y`로 눕는다는 걸 반영해, 클리어런스를 **링 셀의
`+y` 방향에 플레이가 있는 경우(=링이 플레이 영역 하단)** 로 한정한다. 플레이의 상/좌/우 원경 링은 `+y`로
누워도 플레이를 안 가리므로 허용 → 빽빽한 숲이 보드를 감싸되 하단(화면 앞)만 트인다.

**근경엔 적용 안 함**(사용자 지정). 근경 큰 프랍 가림은 unit 10(visual footprint)에서 해소.

## 변경 대상

- `Assets/_Project/Scripts/Data/MapThemeData.cs` — `int ringPlayClearanceCells = 3` (0=off)
- `Assets/_Project/Scripts/Core/TilemapMapView.cs` — `InstantiateRingProps` 링 셀 루프에 `NearPlayCell` skip + static 헬퍼

## 구현

- `InstantiateRingProps` 는 시그니처 무변경. 내부에서 자기 `_visualPlan` 을 직접 참조(BattleBridge 무변경).
- 링 셀 `(x,y)`(보드 밖) 루프에서, `clearance>0 && WouldOccludePlay(_visualPlan, x, y, clearance)` 면 `continue`.
- `WouldOccludePlay(plan, cx, cy, r)`: **`dy∈[1,r]`(+y 방향만)**, `dx∈[-r,r]` 중 보드 내부이고
  `zoneType ∈ {Walk, Place}` 인 셀이 있으면 true. = 링이 플레이 하단(-y)에 있어 `+y` 누움으로 가리는 경우.
  보드 밖 좌표=비-플레이. `plan==null`→false.
- 거리값은 `theme.ringPlayClearanceCells`(하드코딩 금지). 0 이면 기존 동작.

## 완료 기준

- Play 게임뷰 스크린샷: 플레이 영역 **하단(화면 앞)** 원경 나무만 떨어져 트이고, **상/좌/우는 숲이 보드를
  가까이 감싼다**. 보드 내부 근경 프랍은 유지.
- `read_console` CS 에러 0. Legacy3D·근경 BackgroundPropPlacer 무영향.
