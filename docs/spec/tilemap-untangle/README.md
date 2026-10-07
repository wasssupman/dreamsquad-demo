# tilemap-untangle — 디오라마 맵에서 Tilemap 잔재 떼어내기

상태: 초안 2026-10-07 (승인 대기)

## 목표

바닥은 디오라마 스테이지 프리팹(`MapStage`)이 그리고 셀은 `MapStageMath`(`gridOriginLocal` + `tileSize`)가 정하는데, 옛 Tilemap 전투(`TilemapMapView` 1,608줄)에서 남은 세 가지가 아직 그 구조에 얽혀 있다. 셋을 떼어내 **`UnityEngine.Tilemaps` 의존 0** 으로 만든다. 사용자 요청(2026-10-07): 「타일이 현재의 디오라마 방식 맵 구조에 불필요하게 얽힌 것들을 탐색해 제거할 수 있는 건 제거」.

## 탐색 결과 (2026-10-07)

### 얽혀 있는 것 셋

| # | 무엇 | 어디 | 왜 불필요한가 |
|---|---|---|---|
| A | **`Tile` 에셋을 스프라이트 껍데기로 쓴다** | `TileSetData` 의 `TileBase` 필드 9 · `EffectTileData.overlayTile` · `Data/TileSets/*.asset` 8 · `Data/EffectTiles/ET_*.asset` 5 · `CoreMapOverlay` 의 `as Tile`·`.sprite` | 오버레이는 런타임에 **Tilemap 컴포넌트를 쓰지 않는다** — `SpriteRenderer` 풀과 `LineRenderer` 로 그린다(씬에 `Tilemap`/`TilemapRenderer` 0). `Tile` 은 `.sprite` 를 꺼내는 데만 쓰인다. 게다가 9 필드 중 코드가 읽는 건 `blockedTile` 하나(→ `tile_range_solid.png`)뿐이고, 나머지 8(`goal/spawn/hover/reject/range/aimRange/telegraph/placeableTile`)은 소비처 0 이다. `PH_*` 4개는 스프라이트가 끊긴 placeholder. |
| B | **`GridLayout` 이 셀↔월드 변환의 권위** | `BoardSpace.Configure(…, GridLayout)` · `CoreBoardPlane` 이 `Grid` 컴포넌트를 세움 · `BattleDriver.BoardGrid` · `CorePhaseFeed.PushBoard`(`CellToWorld`) · 씬 `BoardPlane` 오브젝트의 `Grid` 컴포넌트 · 테스트 3 | 셀의 정본은 이미 `MapStageMath`(스테이지 로컬 → 셀)다. `Grid` 는 Rectangle · cellSize `(t,t,1)` · 간격 0 이라 `CellToLocalInterpolated(v) = v × cellSize` — **곱셈 하나**를 컴포넌트가 대신하고 있다. 입력 레이캐스트 평면도 `grid.transform` 에서 유도하므로 평면 Transform 만 있으면 같다. 권위가 둘(`Grid` · `MapStageMath`)이면 언젠가 갈린다. |
| C | **패키지·이름** | `com.unity.2d.tilemap.extras` 9.0.1(RuleTile 류 — 사용 0, 데저트 AutoTile 은 이미 삭제) · `Data/TerrainTileRenderInfo.cs`(소비처 0) · `CoreSpineUnitView.ApplyTilemapShadow` · 「Tilemap 모드」 주석들(`DefenderUnitData:130` 등) · `CameraPreset_TilemapRect` 이름 | A·B 가 끝나면 `UnityEngine.Tilemaps` 를 쓰는 파일이 0 이라 빌트인 모듈 `com.unity.modules.tilemap` 도 끌 수 있다(벤더·플러그인 사용 0 확인). |

### 얽히지 않은 것 — 손대지 않는다

- **논리 격자**: `MapTileType`(Walk · Place · Env · Deco) · `GeneratedMap` · 코어 `MapSnapshot.MapTile` · `DioramaMapBuilder` · `MapConnectivity`. 「타일」이란 말을 쓰지만 Unity Tilemap 과 무관한 sim 의 칸 모델이다.
- **효과 타일(게임 기능)**: `EffectTileData` 의 효과 파라미터 · `EffectTilePlacer` · `BoardEffectDefinitionBuilder` · `Art/EffectTiles/ET_*.png` · `BoardSortOrder.EffectTileOrder`. 바뀌는 건 그림을 담는 그릇(A)뿐이다.
- `BoardSpace` 의 **공개 API**(`ToView` · `ToSim` · `ToViewVector` · `RaycastPlane` · `IsConfigured`) — 소비처 33 파일. 내부만 바뀐다.
- 격자선 · 배치 가이드 · 고스트 · 사거리 링 · 공격 도형 · 착지 예고 · 효과 타일 칸 — 오버레이의 그림 자체는 그대로.

## 작업 단위

| # | 문서 | 목적 | 손대는 것 |
|---|---|---|---|
| 0 | `0_tile_assets.md` | `Tile` 껍데기 → `Sprite` | `TileSetData`(→ `BoardOverlayStyle`) · `EffectTileData` · `CoreMapOverlay` · SO 6 · Tile 에셋 13 삭제 · 테스트 2 |
| 1 | `1_grid_authority.md` | `GridLayout` → 평면 Transform + `tileSize` | `BoardSpace` · `CoreBoardPlane` · `BattleDriver` · `CorePhaseFeed` · 씬 `Grid` 컴포넌트 1 · 테스트 3 |
| 2 | `2_packages_and_names.md` | 패키지 2 · 죽은 파일 · 이름 · 문서 | manifest · `TerrainTileRenderInfo.cs` · 개명 3 · 문서 4 |

## 공통 원칙

- **그림과 판정은 바뀌지 않는다.** 단위 1 은 수식이 동치(`v × cellSize` ↔ `v × tileSize`)임을 테스트로 먼저 고정하고 바꾼다. 단위 0 은 스프라이트 참조만 갈아 끼운다.
- 검증은 사용자 정책대로 에디터 컴파일 0 + Play 육안(격자선 · 놓을 수 없는 칸 2색 · 사거리 링 · 착지 예고 · 효과 타일 5종이 전과 같이 보인다). EditMode 는 `BoardSpaceAuthorityTests` · `EffectTileTests` 를 Test Runner 에서(사용자) — 배치는 돌리지 않는다.
- **씬은 사용자 에디터에 열려 있다**(2026-10-07 현재 `BattleCoreScene` Play 중). 단위 1 의 `Grid` 컴포넌트 제거는 YAML 직접 편집이 아니라 **일회용 MenuItem 스크립트**로 하거나, 씬을 닫은 뒤 한다.
- 커밋은 단위마다 경로 지정. 모듈 끄기(`com.unity.modules.tilemap`)는 단위 2 마지막에 따로 — 컴파일이 깨지면 그 커밋만 되돌린다.

## 이 spec 밖

- `MapStage.previewTileSize` 기즈모 · `MapStageMath` — 현행 정본, 그대로.
- 옛 Tilemap 시절 교훈(`lessons/03` 「Tilemap 채움 타일 격자선」 「Tile 캐시」)은 이력으로 남긴다(머리에 「옛 Tilemap 전투 기준」 표시만).
- `GamePhase.Draft` enum(직렬화 순서) — somnia 이식 때.
