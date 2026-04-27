# 5. Handoff Summary — Palette And Overlay Fix

**상태**: **완료 2026-04-27**. 사용자 시각 합격. board-visualization rev4 의 시각 미반영 문제와 격자감 잔존 문제가 본 spec 으로 실질 해소.

## Commit

본 spec 의 모든 변경을 한 docs+code 커밋으로 묶음. 정확한 해시는 본 handoff 작성 후 커밋 시점에 추가.

## Implemented

- **결정 실험 인프라**: `Assets/_Project/Scripts/Rendering/PaletteSanityProbe.cs` — `RuntimeMaterialFactory` + `Tile_Unlit` / URP Unlit shader path 를 격리된 새 씬에서 6 quad 로 단독 검증. 통과로 board-visualization rev4 의 두 의심선 (URP transparent surface mode 누락 / DOTS_INSTANCING 우회) 이 거짓임을 확정.
- **Bug A (캐시 키 결함) 폐기**: forest.asset `placeBaseTint = (1,0,0,1)` red-tint 로 본 게임 Play. 모든 Place 슬랩이 빨강. MapView 의 `_tileTextureMaterials` 텍스처-only 캐시는 forest.asset 에서 zone 별 텍스처가 분리돼있어 silent. 다른 테마 추가 시 폭발 가능성 잔존하지만 본 spec 직접 fix 대상 아님.
- **Bug C (overlay alpha 너무 낮음)**: `forest.asset` 의 `placeEdgeOpacity` 0.25 → 0.55, `placeOuterCornerOpacity` 0.42 → 0.7. `placeInnerCornerOpacity` 는 0.62 로 충분해 유지. fringe 가 시각 인지 가능 + V-004 (grid 강조) 회귀 없음.
- **Bug B (Place edge mask 가 Env 한정)**: `MapView.cs` line 242 `int edgeMask = visualCell.envNeighborMask;` → `int edgeMask = visualCell.transitionMask;`. Place ↔ Walk 경계에도 fringe.
- **격자감 root fix — per-region single mesh**: `MapView.CreateTiledSurfaceMesh` single-quad refactor 후 row 간 seam 잔존 발견. 따라서 `BuildRegionSurfaceMesh` 신규 메서드 — region bbox 의 shared vertex grid + region 셀에만 triangle + UV (0~1, 0~1) region 전체 stretch. `BuildPlaceRegionSurface` / `BuildEnvironmentRegionSurface` 의 row-run loop 제거. obsolete `BuildPlaceRunSurface` / `BuildEnvironmentRunSurface` 삭제. **부수 손실**: Env 의 per-cell texture variation 기각, region anchor 텍스처만 사용.

## Key Files

- `Assets/_Project/Scripts/Core/MapView.cs` — `BuildPlaceRegionSurface`, `BuildEnvironmentRegionSurface`, `BuildRegionSurfaceMesh` (신규), `CreateTiledSurfaceMesh` (single quad), `BuildPlaceEdgeOverlays` (transitionMask 사용)
- `Assets/_Project/Scripts/Rendering/PaletteSanityProbe.cs` — 진단용 MonoBehaviour (sanity 씬에 attach)
- `Assets/_Project/Map/Theme/forest/forest.asset` — `placeEdgeOpacity` / `placeOuterCornerOpacity` 갱신
- `docs/spec/palette-and-overlay-fix/0~4_*.md` — 작업 단위 4개 + 본 handoff

## Verified

- 컴파일 통과 (Unity console error 0).
- Play (Battle 씬) 시각 합격. Place 슬랩이 stone plate 처럼 연속 면으로 보임. 풀-경로-코블스톤이 zone 단위 transition 으로 자연스럽게 이어짐. 사용자 캡처:
  - 0 (sanity scene): 스크린샷 2026-04-27 오후 4.13.25.png — 6 quad 정상.
  - 1 (red-tint): 스크린샷 2026-04-27 오후 4.16.10.png — Place 빨강.
  - 2 (alpha): 스크린샷 2026-04-27 오후 4.20.53.png / 4.21.30.png — fringe 가시.
  - 3 (mask): 스크린샷 2026-04-27 오후 4.28.14.png — Place ↔ Walk fringe 들어옴.
  - 4 (per-region mesh): 스크린샷 2026-04-27 오후 4.55.30.png — region 격자선 사라짐, 연속 면.
- inner / outer corner overlay / edge fringe / hover / flash 인터랙션 회귀 없음 (사용자 시각 + 코드 트레이스 기반).

## Notes (되돌리면 안 되는 의도)

- `MapView` 의 region surface 는 region 단위 single mesh. row-run 으로 회귀 금지 (mesh seam 으로 격자감 부활).
- Env 의 per-cell texture variation 은 본 spec 에서 기각. 다양성이 다시 필요하면 region 내부에 detail layer 또는 noise overlay 로 추가하되 base mesh 는 single-mesh-per-region 유지.
- `_tileTextureMaterials` 의 텍스처-only 캐시 키는 Bug A 잠재 위험. 새 테마 추가 시 zone 별 텍스처가 겹치면 첫 zone tint 만 반영. 새 테마 도입 시점에 `(BoardZoneType, Texture2D)` 키로 전환 필요. 본 spec 에서는 silent 라 미수정.
- `RuntimeMaterialFactory.CreateTransparentTexture` 는 URP transparent surface mode 를 이미 다 설정 (line 72-83). board-visualization rev4 종료 시점에 적었던 의심선은 거짓. **다시 의심하지 말 것**.
- `Tile_Unlit.shader` 의 `DOTS_INSTANCING_ON` variant 는 ECS RenderMesh 경로 전용. Mono `MeshRenderer.sharedMaterial` 인 본 프로젝트 타일에는 무관. 다시 의심하지 말 것.
- `BoardSortOrder` 를 overlay quad 에 부여하지 않은 상태가 본 spec 종료 시점. occlusion 위험 없음 확인됨 (region mesh y=0.002 / hover 0.006 / edge 0.022 + transparent ZWrite off). 미래 회귀 보험으로 추가 가능하지만 본 spec 에서는 미적용.

## Follow-up (선택)

- **`_tileTextureMaterials` 캐시 키 zone-aware 화** — Bug A 잠재 위험 제거. 새 테마 도입 전에 작업.
- **Env per-cell texture variation 복구** — region anchor 텍스처만 쓰는 현재의 단순화가 단조롭게 느껴지면 detail layer / noise overlay 로 추가. 본 spec 의 single-mesh-per-region 구조 유지하면서.
- **Outer corner overlay 가시성 점검** — 사용자 캡처에서 outer corner 가 시각적으로 안 두드러진 인상. mask 발화 여부 확인 + 필요 시 sprite 보강 또는 alpha 추가 상향.
- **`BoardSortOrder` overlay 부여 보험** — 회귀 방지 차원. 우선순위 낮음.
- **22 palette pass 의 톤 값 재평가** — 현재 베이지/녹색 한 계열인데 더 어둡게 / 더 따뜻하게 가고 싶으면 forest.asset Inspector 만 만져서 시도.
- **volcano theme 채움** — `docs/spec/board-visualization/` 의 23 후보 그대로.
- **`BattleBridge.StartBattle` 반복 시 Persistent leak** — 별도 spec.

## 다음 spec 진입 가이드

본 spec 의 후속 작업 중 하나로 진입한다면:
- 우선순위 1 = `_tileTextureMaterials` zone-aware 캐시 키 (silent bug 의 사전 차단)
- 우선순위 2 = Env detail layer (단조로움 보강)
- 우선순위 3 = outer corner 가시성 점검

이외 작업은 사용자가 새 우선순위 이슈 발견 시 별도 spec.

## board-visualization rev4 와의 관계

- board-visualization rev4 의 22 palette pass 가 화면에 안 보였던 진짜 원인 = Bug C alpha + Bug B mask. rev4 종료 시 추측한 두 의심선 (transparent surface mode / DOTS_INSTANCING) 은 거짓.
- rev4 의 컨셉 (보드게임 타일, 격자 수용) 은 본 spec 의 per-region single mesh 로 결과적으로 **컨셉 스펙트럼 안에서 더 연속적 / 조화로운 형태**로 안착. "보드게임 타일" 이라기보다 "zone 단위 plate 가 보드 위에 놓인" 형태에 가까움. 사용자 합격.
- board-visualization spec 폴더 (`docs/spec/board-visualization/`) 는 여전히 종료 상태. 본 spec 이 그 후속 fix 다.
