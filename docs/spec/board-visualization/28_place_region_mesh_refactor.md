# 28. Place Region Mesh Refactor

**상태**: **착수** (2026-04-25). `27` 수행 결과 V-007 Place slab seam 강조가 경량 조정으로 해소되지 않음이 audit screenshot 에서 확인됨.

## 착수 근거

- `27` 커밋 `26b7b5d` 이후 Play screenshot 비교: tileTopScale 0.86→0.95, edgeOpacity 0.36→0.25 로 조정했으나 Place 셀 사이 녹색 seam 이 여전히 뚜렷.
- 근본 원인: Place 가 **셀 단위 개별 quad** 로 렌더됨 (`MapView.BuildTiles` 내 각 Place 셀마다 `Primitive.Quad` 생성). top quad 를 키워도 1.0 에 도달하지 못하는 한 Env base mesh 의 녹색 틈이 셀 경계로 드러남.
- 1.0 에 붙이면 인접 quad 끼리 z-fighting 위험 → 0.95~0.98 이 경량 경로 상한.
- 해결은 **region 내 인접 Place 셀을 하나의 mesh 로 묶음** 뿐.

## 목적

Place 를 `BuildEnvironmentRegionSurface` 와 같은 구조로 재설계해:
- region 내부 Place 셀들이 하나의 tiled mesh 로 렌더됨 → 내부 seam 제거
- Env 와 동일한 run-based mesh 패턴 사용 → 코드 일관성
- outer corner / inner corner overlay / edge fringe 는 region mesh 위의 별도 quad 로 유지 (기존 구조 유지)
- FlashTileReject / SetPlacementHover 는 셀 단위 hover overlay layer 로 분리 → 인터랙션 회귀 방지

## 전제

- `24`, `25`, `26`, `27` 완료.
- `BoardVisualPlan` 이 Place region 도 이미 cardinal 4-이웃 grouping 으로 생성 (재활용 가능).

## 변경 대상

- `Assets/_Project/Scripts/Core/MapView.cs`
  - `BuildTiles` 의 Place 셀 분기 제거 → `BuildPlaceSurfaces()` / `BuildPlaceRegionSurface(region)` 로 이동
  - `BuildPlaceRegionSurface` 는 `BuildEnvironmentRegionSurface` 패턴 차용 (row run 묶기 + tiled mesh 생성)
  - outer corner / inner corner / edge overlay 는 기존 `BuildPlaceEdgeOverlays` 를 **region 외곽 셀에만** 호출하도록 유지
  - 신규 `BuildPlaceHoverOverlays()` — 각 Place 셀에 투명 placeholder quad 를 배치 (hover/flash 대상)
  - `_tileRenderers` / `_buildableRenderers` 의 키는 hover overlay 의 renderer 로 교체
- `Assets/_Project/Scripts/Presentation/` — 변경 없음
- `Assets/_Project/Tests/EditMode/` — MapView 직접 테스트가 없으면 변경 불필요. Plan 측 테스트 회귀만 확인
- audit: `docs/spec/board-visualization/audit/VISUAL_AUDIT.md` V-007 재갱신

## 구현 가이드

### Step 1. `BuildPlaceSurfaces` (Env 패턴 복제)

현재 `BuildTiles` 의 Place 분기:
```csharp
// 셀별 loop 안에서:
var top = GameObject.CreatePrimitive(PrimitiveType.Quad);
top.transform.localScale = Vector3.one * (_tileSize * renderInfo.baseScale);
...
```

→ 이동:
```csharp
BuildPlaceSurfaces();

private void BuildPlaceSurfaces()
{
    if (_visualPlan == null) return;
    for (int i = 0; i < _visualPlan.Regions.Count; i++)
    {
        var region = _visualPlan.Regions[i];
        if (region.zoneType != BoardZoneType.Place) continue;
        BuildPlaceRegionSurface(region);
    }
}

private void BuildPlaceRegionSurface(BoardVisualRegion region)
{
    // Env 의 run-based mesh 로직을 참고.
    // 각 row 에서 region 에 속한 연속 셀 묶음을 run 으로 만들고
    // CreateTiledSurfaceMesh 로 tiled mesh 하나 생성.
    // texture 는 anchorCell 위치의 SelectTexture 결과 (또는 variant 셀 단위 — 이번 단계에서는 region-uniform 허용).
}
```

**variant per-cell 표현**: 본 리팩터 1차 범위에서는 **region-uniform texture** 로 시작. 셀 단위 variant 가 사라지는 대신 seam 이 제거됨. 결과가 너무 단조로우면 후속 spec 에서 UV 단위 variant 또는 noise-driven blend 로 복원.

### Step 2. Outer corner / inner corner overlay

기존 `BuildPlaceEdgeOverlays(root, mask, innerCornerMask, shapeClass)` 는 셀 단위 호출 구조. region 구조 전환 후에는:

- region 외곽 셀을 따로 순회 (`transitionMask != 0` 또는 `innerCornerMask != 0` 인 셀) → 해당 셀의 world 위치에 `_placeEdgeOverlayMaterial` / `_placeEdgeInnerOverlayMaterial` / outer corner sprite 를 올림
- overlay quad 는 region mesh 상단 별도 GameObject 로 유지 (sortingOrder 는 prop 과 같은 공식)

### Step 3. Hover / Flash 인터랙션 분리

기존:
```csharp
_tileRenderers[cell] = r; // Place top quad 의 Renderer
_buildableRenderers[cell] = r;
```

이 renderer 가 사라지므로 회귀 방지용 hover overlay 생성:

```csharp
private void BuildPlaceHoverOverlays()
{
    // 모든 Place 셀마다 투명 quad 를 생성하고 _tileRenderers/_buildableRenderers 에 등록.
    // 이 quad 는 평소 invisible (alpha=0), hover / flash 시 material 교체로 색을 드러낸다.
}
```

또는 더 단순하게: 기존 `SetPlacementHover` / `FlashTileReject` 가 renderer 의 sharedMaterial 을 교체하던 방식을 유지하되, 교체 대상이 region mesh 가 아닌 **셀 단위 hover overlay quad** 가 되도록 한다.

### Step 4. sortingOrder

- region mesh: 단일 order. anchorCell 기준 `BoardSortOrder.Compute(gridSize, anchor.x, anchor.y)`.
- outer/inner corner overlay: overlay 가 속한 셀 기준 `BoardSortOrder.Compute(gridSize, cell.x, cell.y)`.
- hover overlay: 셀 기준. 프랍 order 와 동일 공식.

프랍이 region 중간에 있는 경우 (예: 큰 Place region 한가운데 프랍이 배치되지는 않지만 경로상) region 전체가 같은 order 라 특정 y 의 프랍이 region 뒤로 들어가는 현상이 생길 수 있음. 첫 캡처 후 확인 → 문제 있으면 region mesh 를 row 당 분할해 row 별 sortingOrder 부여.

### Step 5. 테스트

- `BoardVisualPlanBuilderTests` 는 그대로 통과해야 함.
- MapView 렌더 테스트는 없음 — Play 수동.
- Hover/Flash 회귀 수동 체크: 배치 시도 시 hover 색 변화, 비용 부족 시 flash red 동작 확인.

### Step 6. audit 재캡처

Unity MCP 가능하면 `Assets/Screenshots/audit/20260425_28/seed12345_*.png` 저장. 불가하면 제약 명시.

## 완료 기준

- `MapView.BuildTiles` 의 Place 셀별 primitive Quad 생성 분기 제거됨.
- `BuildPlaceRegionSurface` 가 region 단위 tiled mesh 를 생성.
- 시각적으로 Place 영역이 **하나의 묶인 plate** 로 읽힘. 내부 seam 없음.
- outer corner / inner corner overlay 가 정상 렌더 (audit V-003 퇴행 없음).
- FlashTileReject / SetPlacementHover 인터랙션 회귀 없음 (수동 검증 필수).
- `Renderer.sortingOrder` 체계 유지. 캐릭터-프랍-타일 간 sorting 회귀 없음.
- `VISUAL_AUDIT.md` V-007 업데이트 (Low 또는 해소 기대).
- Unity console error 0.

## 리스크 & 주의

- **hover/flash 회귀가 가장 큰 리스크**. 셀 단위 Renderer 가 사라지는 데서 옴. Step 3 의 hover overlay 분리를 건너뛰면 배치 UI 가 깨진다. 구현 시 먼저 hover overlay 를 만들고 그 후 region mesh 로 교체.
- region 당 단일 sortingOrder 가 프랍 depth 에 미치는 영향은 착수 후 screenshot 확인. 문제면 row 단위 분할.
- region-uniform texture 로 시작하는 탓에 variant 다양성이 줄어듦. 필요하면 후속 spec 에서 UV-based variant 복원.
- `MapView.cs` 는 현재 파일이 크고 복잡. 변경 전 Place 관련 분기를 모두 파악 후 진행. 새 method 는 기존 Env 분기 바로 아래에 배치.
- `_placeEdgeOverlayMaterial`, `_placeEdgeInnerOverlayMaterial`, outer corner material 의 생성/해제 경로는 유지.
- 테스트가 걸려있지 않은 영역이므로 커밋 전 Play 수동 확인 필수.

확인 일자: 2026-04-25 (코드 레벨 완료. Play 육안 확인은 사용자 위임. Unity MCP unavailable 로 재캡처 불가. 커밋 해시: 1bc73f9)
