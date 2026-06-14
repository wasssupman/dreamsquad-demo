# 2. BattleBridge 뷰 모드 분기

## 목적

BattleBridge 의 맵 오케스트레이션에 `BoardViewMode` 분기를 넣어, 인스펙터 값 1개로 Legacy3D / TilemapRect / TilemapIso 를 전환한다. ECS 렌더 헬스바를 Tilemap 모드에서 게이팅한다. Legacy3D 경로는 동작 무변경.

## 변경 대상

- `Assets/_Project/Scripts/Bridge/BattleBridge.cs` — 맵 빌드/teardown 구간 (현행 `mapView.Initialize(...)` 호출부, `_boardOrigin` 캡처부, 헬스바 엔티티 생성부 L2236-2268 인접)
- 씬: BattleBridge 인스펙터에 `boardViewMode` + `tilemapMapView` + `tileSet` 참조 wiring

## 구현

- SerializeField 추가: `BoardViewMode boardViewMode = Legacy3D`, `TilemapMapView tilemapMapView`, `TileSetData tileSet`.
- 맵 빌드 시퀀스 수정 (한 곳, 기존 순서 유지):
  - `Legacy3D`: 기존 그대로 `mapView.Initialize(map, tileSize, theme)`.
  - Tilemap 모드: `tilemapMapView.Initialize(map, tileSize, tileSet, boardViewMode)`. `mapView` 는 호출하지 않음 (GameObject 비활성 권장).
  - `_boardOrigin`: Legacy3D = `mapView.transform.position` (현행 유지), Tilemap 모드 = **무조건 `float3.zero`** — 비활성 `mapView` 의 transform 을 읽는 경로가 남지 않게 분기 순서 주의 (README 계약).
  - `BoardSpace.Configure(boardViewMode, BoardOrigin, tileSize, tilemapMapView?.Grid)` 호출을 `BuildFlowField()` 직전에 추가.
  - backdrop/prop 게이팅: `BackdropMounter.Mount` 와 `BackgroundPropPlacer`/`InstantiateObstacles` 분기는 `Legacy3D` 일 때만 수행.
  - **ECS 헬스바 게이팅**: Tilemap 모드에서는 헬스바 엔티티의 Entities Graphics 렌더 생성(`RenderMeshUtility.AddComponents` 경로)을 skip. `HealthBarSystem` 자체는 수정하지 않는다 (렌더 컴포넌트가 없으면 그릴 게 없을 뿐) — ECS 코드 불변 계약 유지. skip 시 1회 로그로 명시 (`[BattleBridge] HealthBar render gated: tilemap view mode`).
- teardown (`TeardownGeneratedMap` 인접 + `RebuildDraftMap` 재진입): Tilemap 모드면 `tilemapMapView.Clear()` 호출 추가. 기존 Unmount 순서 불변.
- `placementInput.Initialize(map, tileSize)` 는 모드 무관 항상 호출 (셀 판정은 sim 공간 — unit 3 에서 입력 평면만 모드 대응).
- FlowField/ECS 주입 경로는 한 줄도 바꾸지 않는다.

## 완료 기준

> 🟡 부분 완료 2026-06-14 (코드) — `BattleBridge.cs` 5곳: `boardViewMode`/`tilemapMapView`/`tileSet`
> SerializeField + `UseTilemapView` 분기, 맵빌드 분기(Tilemap=zero origin, `BoardSpace.Configure` BuildFlowField
> 직전), backdrop/prop Legacy 전용, 헬스바 렌더 게이팅(`HealthBarSystem` 불변·1회 로그), teardown `Clear()`.
> compile 0, 전체 EditMode **325/323 pass**(회귀 0). **Legacy3D Play 스모크 = 이전과 동일(사용자 확인)**. 커밋: cc62a71.
>
> ⏸ **잔여 Play 게이트 (씬 배선 대기)**: TilemapRect Play(보드 페인트 + sim 동일 + 헬스바 미표시·게이팅 로그)
> 와 RebuildDraftMap 2회 잔상 0 은 `_TilemapBoard` 씬 배선(unit 1 carve-out) + `BattleBridge.tilemapMapView/
> tileSet/boardViewMode` 할당 후 확인. dirty `BattleScene.unity`(무관 827줄) 정리 선행. 그때 코드+씬 함께 검증.

- Unity compile 0 errors.
- `boardViewMode = Legacy3D` Play smoke: 본 spec 이전과 시각/로그 동일 — 헬스바 정상 표시 포함. console error/warning 0. (회귀 기준)
- `boardViewMode = TilemapRect` Play 진입: Tilemap 보드가 칠해지고 전투 시뮬레이션(스폰/이동/사망 로그)이 Legacy3D 와 동일 seed 에서 동일하게 진행. 헬스바 미표시 + 게이팅 로그 1회 확인. ※ 이 시점에는 유닛 비주얼 위치가 보드와 어긋나 보이는 것이 정상 (unit 3 에서 정렬).
- `RebuildDraftMap` 경로 2회 반복에 잔상 없음.
