# 9. Shape Mask Extension + Cell Field Ownership

## 목적

`BoardVisualCell` 을 rev3 계약의 완전한 형태로 확장:
- `sameZoneMask` 4-bit → **8-bit**
- 신규 필드: `innerCornerMask`, `surfaceNoiseHash`, `decorBudgetBias`
- `BoardShapeType` 의 `Corner*` 를 `OuterCorner*` 로 rename (**inner corner enum 추가 금지** — overlay-only)
- `TerrainSurfaceSelector` 의 legacy `TerrainTileShape*` 의존 제거

모든 시각 확장 (inner corner overlay, Env variation, anchor 세분화, prop rewrite) 은 이 필드 위에서 돈다.

## 전제

- `7` (Deco resolution) 완료.
- `8` (Placer → Plan) 완료.

## 변경 대상

- `Assets/_Project/Scripts/Data/BoardVisualCell.cs`
- `Assets/_Project/Scripts/Data/BoardShapeType.cs`
- `Assets/_Project/Scripts/Data/BoardShapeUtility.cs`
- `Assets/_Project/Scripts/Data/BoardVisualPlanBuilder.cs`
- `Assets/_Project/Scripts/Data/TerrainSurfaceSelector.cs`
- `Assets/_Project/Tests/EditMode/BoardVisualPlanBuilderTests.cs`

## 구현 가이드

1. `BoardVisualCell` 필드 추가:
   - `byte sameZoneMask` (8-bit)
   - `byte innerCornerMask` (4-bit, NE=1, SE=2, SW=4, NW=8)
   - `uint surfaceNoiseHash`
   - `float decorBudgetBias` (0~1)
2. `BoardShapeType`:
   - 기존 `Corner*` → `OuterCorner*` rename
   - `Isolated`, `End*`, `Straight*`, `TJunction*`, `Cross` 유지
   - **inner corner 항목 추가 금지.** 총 16 개
3. `BoardShapeUtility`:
   - `GetShapeForMask(byte mask8)` → cardinal 4-bit 만으로 shape 결정
   - `GetInnerCornerMask(byte mask8)` 신규: 각 대각 비트가 0 이고 그 대각에 인접한 두 cardinal 비트가 1 이면 코너 bit 세움
4. `BoardVisualPlanBuilder`:
   - 8-bit mask 계산 (cardinal 4-bit + diagonal 4-bit concat, bit 순서 `1_board_visual_plan.md` 와 일치)
   - 셀마다 `innerCornerMask`, `surfaceNoiseHash`, `decorBudgetBias` 채움
   - `surfaceNoiseHash` 공식:
     ```
     h = (uint)visualSeed
     h ^= (uint)(x * 374761393)
     h ^= (uint)(y * 668265263)
     h = (h ^ (h >> 13)) * 1274126177u
     ```
   - `decorBudgetBias` 공식 (초안):
     ```
     bias = saturate(1.0 - pathProximity/8.0 - (borderProximity == 0 ? 0.3 : 0.0))
     ```
     (path 에서 멀수록 bias 높음, 맵 외곽 셀은 감점). 튜닝 발생 시 본 문서 갱신.
5. `TerrainSurfaceSelector`:
   - `TerrainTileShapeUtility.GetWalkShape` 호출 제거
   - `BoardShapeUtility` 또는 `plan.CellAt(cell).shapeClass` 경유
   - `TerrainTileShape` enum import 제거
6. 테스트:
   - L자 `Place` 4 셀 구성에서 안쪽 셀의 `innerCornerMask` NE bit 세워짐
   - 8-bit mask 비트 순서
   - `decorBudgetBias` deterministic (동일 seed/cell → 동일 값)
   - `OuterCornerNE` 분류가 기존 `CornerNE` 케이스와 동등
   - 기존 cardinal shape 테스트 유지

## 완료 기준

- `sameZoneMask` 8-bit, bit 순서가 `1_board_visual_plan.md` 와 일치
- `BoardShapeType` 16 항목, inner corner 항목 없음
- 신규 셀 필드 3개 채워짐 (`innerCornerMask`, `surfaceNoiseHash`, `decorBudgetBias`)
- `TerrainSurfaceSelector` 가 `TerrainTileShape*` 참조하지 않음 (grep 0)
- `BoardVisualPlanBuilderTests` 신규/기존 전원 통과
- `MapView` compile OK (inner corner 렌더링은 `10`)

## 주의

- `TerrainTileShape.cs` / `TerrainTileShapeUtility.cs` 파일 삭제는 `10`. 여기서는 의존만 끊는다.
- `decorBudgetBias` 공식은 초안이므로 `13` 에서 사용 시 튜닝 여지 있음. 공식 변경은 본 문서 갱신 + 커밋 메시지에 한 줄.

확인 일자: 2026-04-24 / 커밋 해시: c3b61db
