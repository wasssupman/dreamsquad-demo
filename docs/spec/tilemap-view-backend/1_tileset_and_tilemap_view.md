# 1. TileSetData SO + TilemapMapView

## 목적

`GeneratedMap` 을 Unity Tilemap 에 칠하는 뷰를 만들고, **BoardSpace ↔ Tilemap 셀 정합을 테스트로 고정**한다. 배치 피드백(hover/reject)도 이 뷰가 담당한다.

## 변경 대상

- 신규: `Assets/_Project/Scripts/Data/TileSetData.cs` (ScriptableObject)
- 신규: `Assets/_Project/Scripts/Core/TilemapMapView.cs` (MonoBehaviour)
- 신규: `Assets/_Project/Tests/EditMode/TilemapMapViewTests.cs`
- 신규 에셋: `Assets/_Project/Data/TileSets/TileSet_Placeholder.asset` + placeholder Tile (rect 단색 4종 + iso 마름모 4종)
- 씬: `_TilemapBoard` GameObject (Grid + ground/overlay Tilemap 자식) — unity-feature-wiring 스킬 절차 준수

## 구현

- `TileSetData`: `MapTileType → TileBase` 매핑 4슬롯 + `goalTile`/`spawnTile`/`hoverTile`/`rejectTile`(overlay 용) + `Vector3 isoCellSize`(Grid.cellSize 로 적용). 시즌/실험별로 에셋만 늘린다.
- `TilemapMapView`:
  - `Initialize(in GeneratedMap map, float tileSize, TileSetData tileSet, BoardViewMode mode)`:
    - Grid `cellLayout` 설정: `TilemapRect` → Rectangle (`cellSize=(tileSize,tileSize)`), `TilemapIso` → Isometric (`cellSize=tileSet.isoCellSize`). 타일 anchor 는 `(0.5, 0.5)` (셀 중심) 으로 고정 — 정합 테스트의 전제.
    - `Tilemap.SetTilesBlock` 로 전체 셀 일괄 페인트 (셀 단위 `SetTile` 루프 지양).
    - goal/spawn 마커는 overlay Tilemap 레이어에 페인트.
  - `Clear()` — `ClearAllTiles` + 상태 리셋. 재진입(`RebuildDraftMap`) 안전.
  - 배치 피드백 (기존 `MapView.SetPlacementHover`/`FlashTileReject`/`ClearPlacementHover` 와 대응하는 시그니처): overlay 레이어에 hover/reject 타일 set/clear. flash 는 코루틴 1개.
  - Tilemap 좌표 규약: `GeneratedMap` 셀 `(x, y)` → Tilemap cell `(x, y, 0)`. 변환 헬퍼 1개로 고정.
- 게임 로직은 이 클래스를 읽지 않는다 (write-only 뷰). 프로젝트 내 `GetTile` 호출 0건 유지 (정합 테스트의 `GetCellCenterWorld` 는 좌표 조회라 무관).

## 완료 기준

> ✅ 검증 2026-06-14 (코드+에셋) — `Data/TileSetData.cs` + `Core/TilemapMapView.cs` + `TilemapMapViewTests.cs`
> 신규, placeholder 타일셋 14에셋(`Data/TileSets/`). `TilemapMapViewTests` **3/3 passed**: Rect/Iso
> `GetCellCenterWorld≈BoardSpace.ToView` 정합 + Clear→Initialize 재진입 누수 0. 임시 보드 20×20 Rectangle
> 페인트 스크린샷으로 Walk/Place/Env/Deco 4종 + goal/spawn overlay 마커 시각 확인. compile 에러 0.
> 커밋: 371130b(1a 코드+테스트) · f4bfa8e(1b 에셋).
>
> ⏸ **carve-out — 씬 `_TilemapBoard` 영속화는 unit 2 로 이관**: `BattleScene.unity` 가 무관한 미커밋
> 변경(827줄)으로 dirty 라 `_TilemapBoard` 를 저장하면 커밋이 오염된다. 페인터 검증은 씬 미저장 임시 보드로
> 완료했고, 영속 씬 배선은 BattleBridge 뷰 모드 연결(unit 2)에서 dirty 씬 정리 후 함께 진행한다.

- **iso 정합 고정 테스트** (EditMode): Rect/Iso 각각에서 여러 셀 (코너·중앙·비대칭 좌표) 에 대해 `BoardSpace.ToView(GridMath.CellToWorldCenter(cell, tileSize)) ≈ tilemap.GetCellCenterWorld((x,y,0))`. 이 테스트가 unit 0 의 변환과 Tilemap 페인트 위치의 단일 권위를 못 박는다 — 이후 unit 에서 어긋나면 여기가 먼저 빨개져야 한다.
- 에디터에서 임시 호출로 20×20 맵이 Rectangle Tilemap 으로 칠해진 스크린샷 확인 — Walk/Place 구분 가시.
- `Clear → Initialize` 2회 반복에 잔상/누수 없음 (콘솔 0 errors).
- Unity compile 0 errors.
