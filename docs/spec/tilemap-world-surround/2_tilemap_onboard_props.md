# 2 — Tilemap 모드 보드 내부 프랍 (Deco 셀)

## 목적

단위 1 이 만든 `Deco` 셀에 배경 프랍을 프리팹으로 배치한다. 좌표는 **grid 권위**(BoardSpace.ToView 수식)
경유 — Legacy 의 raw `(x,y)*tileSize` 금지. 근경(보드) 프랍은 그림자 CAST. `PropsTilemap` GO 비활성.

## 변경 대상

- `Assets/_Project/Scenes/BattleScene.unity` — `PropsTilemap` SetActive(false) (씬 저장 1회)
- `Assets/_Project/Scripts/Core/TilemapMapView.cs` — VisualPlan 빌드/노출 + `InstantiateBackgroundProps` + `CellCenterToWorld`
- `Assets/_Project/Scripts/Core/MapView.cs` — prop 데코 헬퍼 3개 `private static`→`internal static` (재사용, 동작 불변)
- `Assets/_Project/Scripts/Bridge/BattleBridge.cs` — 669 gate 를 Tilemap 모드까지 확장

## 구현

- `TilemapMapView`: `_visualPlan` 필드 + `Initialize` 에서 `BoardVisualPlanBuilder.Build(map, map.seed)` + `public BoardVisualPlan VisualPlan`.
  `Clear()`/teardown 에서 `_backgroundPropsRoot` 정리.
- `CellCenterToWorld(float cx, float cy)` = `grid.transform.TransformPoint(grid.CellToLocalInterpolated(new Vector3(cx+0.5f, cy+0.5f, 0f)))`
  (BoardSpace.ToView 와 동일 셀중심 수식). z-fight 회피용 미세 world +Y lift 상수.
- `InstantiateBackgroundProps(plan, theme, placements, castShadows)`: Legacy 루프 미러 + 위치만 grid.
  프랍은 standalone root(child of transform) 에, `instance.transform.position`(world)/`rotation`(world Y yaw) 설정 →
  부모 90° 회전 비상속. `PropBillboard` 가 LateUpdate 로 facing override. 정렬/마커/틴트는 `MapView.*` 헬퍼 재사용.
  `castShadows` 면 렌더러 `shadowCastingMode=TwoSided`.
- `BattleBridge` 669: 조건 재구성 — Tilemap+MapGrid 도 프랍 배치.
  `if (theme?.tileProps?.Length>0 && mapSource!=MapGrid_빈손조건)`: UseTilemapView 면
  `var plan=tilemapMapView.VisualPlan; var p=BackgroundPropPlacer.Generate(plan,theme,seed); tilemapMapView.InstantiateBackgroundProps(plan,theme,p,UseRealShadows);`
  Legacy 분기는 기존 그대로.

## 완료 기준

- compile 0 에러. Play(Tilemap) 시 Deco 셀에 프랍 등장, 위치가 셀 중심과 정합(공중/어긋남 없음), 정렬 자연.
- `UseRealShadows` 면 프랍이 바닥에 실루엣 그림자 CAST. 배치/이동/타겟팅 회귀 없음.
- 게임뷰 스크린샷(`screenshot_super_size=10`)로 육안 확인.
