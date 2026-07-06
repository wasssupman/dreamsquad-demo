# placement-attack-range-preview

상태: 완료 2026-07-04 (units 0~2 · `eded85d`/`81d1d79`/`b3cd345` + docs `1625de5`) + **확장 unit 3 (2026-07-06)**: TilePoint 스킬 aim/텔레그래프도 같은 격자 표시로 통일(빨간 쿼드 텔레그래프 삭제). 사용자 요청으로 승격.

## 목표

방어 유닛을 드래그 배치하는 동안, 기존 배치 타일 하이라이트(중심 셀 초록/빨강)에 더해
그 유닛의 **공격 범위**를 노란색 전체-동기 펄스 하이라이트로 함께 보여준다.

**검증 질문**: 드래그 중, 유닛의 실제 공격 사거리(`attackRange`)와 일치하는 범위가
노란색 전체-동기 펄스(글로우 트위닝)로 보이는가?

## 연결 문서

- 드래그 프리뷰 각도/흔들림(빌보드 버그 + sway): `docs/spec/placement-drag-preview-polish/`
- 범위 판정 규칙 출처(Chebyshev / RangeToTiles): `docs/spec/tile-range-unification/`

## 작업 단위 목록

| # | 파일 | 작업 | 목적 |
|---|---|---|---|
| 0 | `0_tileset_range_fields.md` | data | `TileSetData` 에 rangeTile + 색/펄스 파라미터 |
| 1 | `1_tilemap_range_layer.md` | view | `_rangeTilemap` 생성 + Set/Clear + 동기 펄스 |
| 2 | `2_bridge_and_drag_wiring.md` | wiring | BattleBridge 포워딩 + DragController 배선 |
| 3 | `3_skill_aim_range.md` | 확장 | 스킬 aim 커서 추종 + 캐스트 후 텔레그래프 격자 통일 (owner 게이트, 빨간 쿼드 삭제) |

의존: `0 → 1 → 2` (순차) · `3` 은 2 완료 후 독립.

## Feature-wide 계약

- **범위 소스**: `DefenderUnitData.attackRange`(float). `aggroRange`(Guardian taunt 자석)는
  공격 범위가 아니므로 **사용 안 함**.
- **셀 집합**: `tileRange = GridMath.RangeToTiles(attackRange)`. 범위 = 중심에서
  `ChebyshevDistance` `1..tileRange` (정사각 링). **중심 셀 제외**(초록/빨강 hover 가 그 자리).
  AttackSystem 과 동일 함수 → 보이는 범위 = 실제 사거리.
- **보드 경계 clip**: 범위 셀은 `[0, gridSize)` 안으로만 그린다. `TilemapMapView` 가
  `Initialize` 시 `map.gridSize` 를 필드(`_gridSize`)로 보관.
- **표시 조건**: 배치 가능/불가와 **무관하게 항상** 노란 범위 표시. 중심 타일만 초록/빨강.
- **전용 타일맵**: `_rangeTilemap` — `EnsureEffectTilemap` 선례를 **정확히 미러**
  (런타임 생성, `grid.transform` 자식, guard-and-reuse, `Clear()` 는 `ClearAllTiles()` 만 —
  GameObject 파괴/누수 금지). sortingOrder **-12** (ground -20 · effect -15 위 / hover overlay -10 아래).
  **머티리얼은 overlayTilemap 의 sharedMaterial 을 복사**(검증된 반투명 tint 경로 재사용).
- **펄스**: 전체 동기. alpha 는 `TilemapMapView.Update()` 가 **단독 소유** —
  `_rangeTilemap.color` RGB = rangeColor, alpha = `Lerp(min, max, 0.5+0.5*sin(unscaledTime*speed))`.
  전 타일 동일 위상. 범위 활성 시에만. `Time.unscaledTime`(배치 페이즈 timeScale 무관 UI 효과).
- **tint 검증 게이트**: `Tilemap.color` 전역 tint 는 이 코드베이스 **미사용 경로**다(모든 기존 tint 는 per-cell).
  unit 1 에서 **에디터 렌더로 노란색+alpha 펄스가 실제로 보이는지 최우선 확인**. 안 보이면
  per-cell 폴백(`SetTileFlags(cell, TileFlags.None)` + `SetColor(cell, pulseColor)`, PaintSurroundRing
  과 동일 패턴, 매 펄스 프레임 재적용 ~48콜)으로 전환. 폴백도 전 셀 동일 alpha 라 동기 유지.
- **시각 형태 = 격자 outline (map 가시성)**: `rangeTile` 스프라이트는 **셀 테두리만 그리는 격자 outline**
  (중앙 투명). solid fill 은 맵을 과하게 가려 폐기(사용자 결정 2026-07-04). 현재 에셋
  `Assets/_Project/Data/TileSets/tile_grid_outline.png`(64px, **2px** 흰 테두리, PPU 64, Bilinear/Uncompressed).
  **형태 조정은 rangeTile 에셋 교체로만** — 페인트/펄스 코드는 스프라이트-agnostic(코드 무관).
- **데이터 주도(하드코딩 금지)**: rangeTile / rangeColor / pulseMinAlpha / pulseMaxAlpha / pulseSpeed
  전부 `TileSetData`. (-12 등 sorting 정수는 기존 -10/-15/-20 과 동류의 구조 상수 — 게임플레이 수치 아님.)
- **BattleBridge 게이트웨이**: DragController 는 `bridge.SetPlacementRange/ClearPlacementRange` 만 호출
  (hover 페어와 대칭). `attackRange→tileRange` 변환은 bridge 에서(뷰는 int tileRange 만 받음,
  DefenderUnitData-agnostic). 뷰 전용, ECS 쓰기 0.
- **맥락 경계**: 순수 Presentation. ECS Component 읽기/쓰기 없음.

## 비목표 / 후속 후보

- **배치 스킬 범위 표시** [M] · `onPlaceRange` / `hazardCastRange` 를 **다른 색 채널**로.
  웜(공격)/쿨(스킬) 색코드 + 채널별 펄스 위상차로 겹침 분리, 필요 시 스킬 채널만 border 타일.
  **2번째 색 채널 등장 시점에** `EnsureRangeTilemap`/펄스 로직을 `(tilemap, color, phase)` 파라미터로
  추출(그 전엔 concrete 유지 — effect-tile 코드 스타일과 일치). 별도 spec.
- **Guardian 어그로 반경 시각화** [S] · `aggroRange` 를 또 다른 표기로. 공격 범위와 별개 성격.
- **이미 배치된 유닛 선택/탭 시 범위 표시** [S] · 현재는 드래그 중만.
- **버프로 사거리 변동 실시간 반영** [S].
- **비-Chebyshev 형상**(splash / cone) 범위 표기.
