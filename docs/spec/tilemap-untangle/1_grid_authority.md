# 1 — 셀↔월드 권위를 `GridLayout` 에서 보드 평면 Transform + `tileSize` 로

## 목적

`BoardSpace` 가 `GridLayout.CellToLocalInterpolated/LocalToCellInterpolated` 로 하던 일은 Rectangle · 간격 0 그리드에서 `local = (cx·t, cy·t, 0)` 과 그 역산이다. `Grid` 컴포넌트 없이 **평면 Transform 과 `tileSize`** 로 같은 값을 내고, 씬에서 `Grid` 를 뗀다. 권위가 하나(`MapStageMath` 의 셀 정의 + 이 평면)가 된다.

## 변경 대상

1. **`Scripts/Core/BoardSpace.cs`**
   - `Configure(float3 simOrigin, float tileSize, GridLayout grid)` → `Configure(float3 simOrigin, float tileSize, Transform boardPlane)`. 에러 문구 「Tilemap mode requires a GridLayout」 → 「보드 평면 Transform 이 필요하다」.
   - `ToView`: `boardPlane.TransformPoint(new Vector3(cx * t, cy * t, 0))`. `ToSim`: `boardPlane.InverseTransformPoint` 뒤 `/ t`. `RaycastPlane`: `boardPlane.forward / position`(지금과 동일). `using UnityEngine.Tilemaps` 가 없어도 `GridLayout` 은 `UnityEngine` 네임스페이스 — 참조만 사라진다.
   - 머리 주석의 「managed GridLayout 의존 · 셀 크기 수식을 하드코딩하지 않는다」 → 「평면 Transform 과 tileSize 만 안다 — 셀 정의는 `MapStageMath` 와 같은 식」.
2. **`BattleCoreUnity/View/CoreBoardPlane.cs`**: `Grid _grid` 필드 · `EnsureGrid()` · `cellLayout/cellSize` 설정 제거. `Declare(tileSize, …)` 는 `TileSize` 를 보관하고 `transform` 을 평면으로 노출(`public Transform Plane => transform; public float TileSize`). 주석 「격자(`Grid`)이고, 격자를 세우는 자리」 → 「보드 평면이고, 평면을 세우는 자리」.
3. **`BattleCoreUnity/BattleDriver.cs:162`**: `GridLayout BoardGrid` → `Transform BoardPlane`(+ `TileSize` 는 이미 있다). `BoardSpace.Configure(…, _boardPlane.transform)`.
4. **`BattleCoreUnity/View/CorePhaseFeed.cs:69-`**: `plane.CellToWorld(new Vector3Int(x,y,0))` → `BoardSpace.ToView(셀 모서리 sim 좌표)` 또는 `plane.TransformPoint(new Vector3(x*t, y*t, 0))`. 카메라 bounds 산출값이 전과 같아야 한다(단언 추가).
5. **씬 `BattleCoreScene` — `BoardPlane` 오브젝트(fileID 500)의 `Grid` 컴포넌트(&502) 제거.** 씬이 열려 있으므로 일회용 `MenuItem`(`Editor/BattleCore/` 에 두고 실행 뒤 삭제)으로 `DestroyImmediate(grid)` + `SaveAssetIfDirty`. 닫혀 있으면 YAML 블록 `--- !u!156049354 &502` 와 GO 의 `component: {fileID: 502}` 줄 삭제.
6. **테스트**
   - `EditMode/BoardSpaceAuthorityTests.cs`: 계약 문장을 「권위는 주입된 평면 Transform + tileSize」로. `CreateGrid(cellSize, pos, euler)` → `CreatePlane(tileSize, pos, euler)`(빈 GameObject). 「grid 로컬 공간에서 비교」 케이스는 `plane.InverseTransformPoint` 로 같은 단언. 비균등 cellSize(2,3) 케이스는 **삭제** — tileSize 는 스칼라다(지금도 `Declare` 가 `(t,t,1)` 만 세운다).
   - `EditMode/CoreProjectileVariationTests.cs:122-125`: `Grid` 생성 → 빈 GameObject Transform 으로 `Configure`.
   - `PlayModeCore/CoreViewYardstickTests.cs:135`: `driver.BoardGrid` → `driver.BoardPlane`.
7. **동치 고정 테스트(먼저 쓴다)**: 바꾸기 전 `BoardSpaceAuthorityTests` 에 「임의 셀 20개 · 회전 3종에서 `Grid` 식과 `plane × t` 식이 1e-5 안」을 추가해 초록을 본 뒤 구현을 교체하고, 교체 후 그 테스트는 새 식만 남긴다.

## 구현

- `BoardSpace` 는 static 이라 PlayMode 테스트 픽스처(`CoreSceneFixture`)가 `BattleDriver` 경유로 다시 `Configure` 한다 — 픽스처 수정 없음(드라이버 내부 교체).
- `CoreBoardPlane` 에 `Grid` 가 `[SerializeField]` 로 직렬화돼 있다 — 필드 삭제 뒤 씬 YAML 의 `_grid:` 줄은 5 의 MenuItem 저장이 함께 걷어낸다.

## 완료 기준

- [ ] `rg "GridLayout|<Grid>|\.Grid\b|BoardGrid" Assets/_Project/Scripts Assets/_Project/Tests` → 0
- [ ] 씬에 `Grid:` 블록 0 · 에디터 컴파일 0 · Play: 유닛 배치 위치 · 드래그 고스트 · 카메라 프레이밍이 전과 같다(동치 테스트 초록)
- [ ] 커밋(경로 지정) — 씬 변경은 「git」 제약대로 남의 헝크 없이
