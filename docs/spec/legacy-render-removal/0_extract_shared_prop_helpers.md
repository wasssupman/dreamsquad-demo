# 0. 공용 프랍 헬퍼 추출 — MapView → PropInstanceUtil

## 목적

ACTIVE 렌더 경로(`TilemapMapView`)가 Legacy `MapView`의 static 헬퍼 3종을 호출하고 있어 `MapView.cs` 통삭제(unit 2)가 막혀 있다. 이 헬퍼들을 중립 static 클래스로 추출해 얽힘 1번을 해소한다. **동작 변경 0** — 코드 이동만.

## 변경 대상

- **신규**: `Assets/_Project/Scripts/Core/PropInstanceUtil.cs`
- `Assets/_Project/Scripts/Core/MapView.cs` — 헬퍼 3종 본문 제거, 호출부는 `PropInstanceUtil.*` 로 전환
- `Assets/_Project/Scripts/Core/TilemapMapView.cs` — `MapView.*` 호출 4곳을 `PropInstanceUtil.*` 로 전환

## 구현

1. `PropInstanceUtil` (namespace `Wassup.Core`, `internal static class`) 생성. `MapView.cs:805~836` 의 3개 메서드를 **verbatim 이동**:
   - `DisablePropDebugMarkers(GameObject instance)` — marker/footprint/debug/bounds 렌더러 비활성
   - `ApplyPropSorting(GameObject, PropData, Wassup.Data.PropPlacement, BoardVisualPlan)` — `prop.sortingOrder + BoardSortOrder.Compute(...)` 를 SpriteRenderer 에 적용
   - `ApplyPropGlobalTint(GameObject, Color)` — SpriteRenderer color 곱연산 tint
   - using 은 필요분만: `UnityEngine`, `Wassup.Data`, `Wassup.Presentation`(BoardSortOrder/BoardVisualPlan 위치 확인 후).
2. 호출부 전환 (기계적 치환):
   - `TilemapMapView.cs:324, 325, 328, 457` — `MapView.X(...)` → `PropInstanceUtil.X(...)`
   - `MapView.cs:798, 799, 801` — 자기 내부 호출도 `PropInstanceUtil.X(...)` 로 (unit 2 삭제 준비)
3. `MapView.cs` 에서 헬퍼 3종 본문 삭제. 다른 멤버/로직은 건드리지 않는다 (삭제는 unit 2).

**주의**: 로직 수정·시그니처 변경·개선 금지. `internal` 접근성 유지 (동일 어셈블리 내 사용).

## 완료 기준

- [x] compile 통과 (`read_console` 에러 0)
- [x] `rg "MapView\.(ApplyPropSorting|ApplyPropGlobalTint|DisablePropDebugMarkers)" Assets` → 0건
- [x] Tilemap Play 스크린샷: 프랍 sorting/tint/마커 숨김이 기존과 동일 (배경/프랍 변경 = 육안 검증 필수)

확인 2026-07-03 — compile 0 에러 · grep 0건 · Play 스크린샷(`legacy_removal_u0_props_verify.png`, 근경 41/링 123/구조물 3) 사용자 통과.
