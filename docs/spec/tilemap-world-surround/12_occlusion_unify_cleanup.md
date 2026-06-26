# 12 — occlusion 통합 + dead shadow 정리

## 목적

코드 리뷰(2026-06-26) 중복/dead 지적 정리. 발 피벗(11)으로 근본을 고친 뒤, 같은 맥락에서:
- **occlusion 2벌 통합**: `WouldOccludePlay`(원경, TilemapMapView)와 `VisualFootprintHitsPlay`(근경,
  BackgroundPropPlacer)가 같은 "+y 틸트 가림" 개념을 두 곳에 구현 → 1 공유 함수로.
- **dead code 제거**: `castShadows`/`SetPropCastShadows`(RingProps 가 항상 false 전달 → 도달 불가).

## 변경 대상

- `BackgroundPropPlacer.cs` — `public static OccludesPlay(plan, originX, originY, width, depth)` 신설.
  `VisualFootprintHitsPlay` 를 그 호출로 축약.
- `TilemapMapView.cs` — `WouldOccludePlay` 제거, 링 루프에서 `BackgroundPropPlacer.OccludesPlay(...)` 호출.
  `InstantiateRingProps` 의 `castShadows` 파라미터 + `SetPropCastShadows` 메서드 제거.
- `BattleBridge.cs` — RingProps 호출에서 `castShadows`(false) 인자 제거.

## 구현

- **`OccludesPlay`**: origin 셀에서 `+y`(틸트 누운 방향, BoardSpace XZ + 카메라 고정)로 `depth` 셀,
  폭 `width`(중심 정렬) 안에 플레이 셀(Walk/Place) 있으면 true. 보드 밖=비-플레이.
  - 근경: `OccludesPlay(footX, footY, visualFootprint.x, visualFootprint.y - 1)` (발 셀 제외 depth).
  - 원경: `OccludesPlay(x, y, 2*clearance+1, clearance)` (하단 한정 = +y 검사, 폭 2r+1).
- 두 호출 모두 기존 동작과 동일(리팩터). `+y` 방향은 한 곳(OccludesPlay)에만 하드코딩 → 카메라 회전 시
  변경 지점 단일화.
- `SetPropCastShadows` 는 9rev 이후 BackgroundProps 가 blob-only 라 RingProps(항상 false)만 참조하던 dead.

## 완료 기준 (충족)

- compile 0 에러.
- Play(tilemap): onboard 42 tree +y 위반 0, ring 184 +y 위반 0, avg sprite.min.y=-0.07 접지. 룩 보존. ✓
- 후속: `OccludesPlay` 가 public static 이 되어 EditMode 단위 테스트 가능(리뷰 권장, 미작성 — 별도).
