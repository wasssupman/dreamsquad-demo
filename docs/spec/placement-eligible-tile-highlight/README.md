# placement-eligible-tile-highlight

상태: **구현·튜닝 완료 (브랜치, main 머지 대기)** · 작성 2026-07-18 · 브랜치 `feat/placeable-tile-highlight`(9커밋 `df75b984`~`7e9ed7eb`, main 미반영). 인계는 `4_handoff_summary.md`.
비판 리뷰 2건(구조/시각) + 사용자 방향 결정(밝게+표준 문법) 반영.
최종 룩: 배치영역 = `Tile_PlaceableSlab`(시안 림 슬랩) + `placeableColor` 시안 α0.5, **정적**. 사거리 = **주황 `(1,0.55,0.12)`**, 아웃라인 3px+1px soft, **펄스 제거(정적)**.
전 오버레이는 z-fight 방지 위해 카메라 쪽 미세 오프셋(ground depth 평면 분리). Play 검증: 배치가능 82/200셀(41%) 정확·경로/Deco 제외·사거리 공존·Hide 소거.
남은 것: 사용자 실드래그 체감 + main 머지(하단 handoff Follow-up).

## 목표

방어 유닛을 **D&D 드래그** 하거나 **탭 선택(arm)** 해 놓을 곳을 고르는 동안,
지금 배치 가능한 칸을 **밝게 강조**해 즉시 읽히게 한다(표준 TD 문법). 동시에 떠 있는
**공격 사거리 노란 링과 공존**하도록, 사거리엔 다크 라이너를 줘 밝은 칸 위에서도 살린다.

**검증 질문**: 유닛을 드래그/arm 한 순간, 배치 가능 칸이 밝게(가장자리 림으로 플랫폼처럼) 도드라지고,
그 위의 **노란 공격 사거리 링이 안 묻히고** 함께 읽히는가? 화면에서 움직이는 건 사거리 하나뿐인가?

## 전략 결정 이력 (load-bearing)

- 초안은 "배치 불가를 어둡게 죽이는 다크 마스크"였으나, 실제 배치 가능 영역이 판의 80%가 아니라
  **약 절반 + Deco 구멍**(라이브 테마 `mapGridBuildableKeepRatio=0.6`: 경로 뺀 Place 의 60%만 유지,
  40%는 잔디 Deco)임이 확인되며 반전. 절반 규모 + 산발 분포에선 밝게 칠해도 판을 뒤덮지 않아
  figure-ground 문제가 사라지고, 표준 문법(배치존=밝음)이 더 직관적.
- 밝은 강조가 노란 사거리를 명도로 죽이는 문제는 **마스크가 아니라 사거리 restyle 로 해결**:
  노란 아웃라인에 **다크 라이너**를 굽는다(밝은 바닥·어두운 경로 양쪽에서 링 생존). 이게 B의 핵심 열쇠.

## 핵심 계약 (feature-wide)

- **전용 레이어, owner enum 밖**: `TilemapMapView._placeableTilemap` 신설(`EnsureEffectTilemap`/
  `EnsureRangeTilemap` 패턴 미러). `_rangeTilemap` 재사용 금지 — range 는 드래그 중
  `RangeDisplayOwner=Placement` 로 그 타일맵을 점유하고, 배치 하이라이트는 **동시에** 떠야 한다.
  `RangeDisplayOwner` 는 상호배타 시분할 장치지 동시 합성이 아니므로 owner enum 에 넣지 않는다(직교 채널).
- **시각 = 은은한 fill + 밝은 림(플랫폼 느낌)**: 배치 영역이 절반 규모라 통짜 밝힘은 밋밋 → 안은 낮은
  알파 fill, **배치 가능 영역의 가장자리에 밝은 림/베벨**. 3D 융기 없이 스프라이트/타일 색만. 색은 **차갑고
  낮은 채도**(초록 금지 — 초록은 hover 전용). 형태·톤은 `placeableTile` 스프라이트 + `TileSetData` 값으로만.
- **정적(펄스 없음)**: 라이브 펄스는 사거리 **독점**. 배치 하이라이트는 상태(state)라 정적 — 집는 순간
  150~250ms 페이드인 juice 뒤 드롭까지 고정. alpha 는 `Update()` 가 `unscaledTime` 기준 소유(timeScale 무관).
- **sorting = 바닥(−13) + 드래그 중 상승 3티어**: 정적(arm) 시 ground −20 위·effect −15 위·range −12 아래
  = **−13**. 드래그 시작 시 range 가 유닛 위로 상승(`SetPlacementHighlightAboveUnits`, overlay 10002 /
  range 10000)하므로, **밝은 언더레이도 함께 상승해 9998**(range 10000 아래 / 유닛 위)로 편입 — 밀집 전투 중
  드래그(이 기능이 가장 필요한 순간)에 적 빌보드 밑에 깔리지 않게. `EnsureXxxTilemap` lazy 생성 시
  `_highlightAbove` 반영 함정을 range 와 동일하게 물려받는다.
- **사거리 다크 라이너(필수)**: `rangeTile`(+조준 화살표) 스프라이트에 어두운 라이너를 구워 밝은 배치칸
  위에서도 노랑이 안 묻히게. 코드 무변경(에셋 교체) — 단 tint 가 라이너 픽셀을 노랗게 물들이지 않게 스프라이트
  채널 구성 확인. **이건 옵션이 아니라 B의 성립 조건**(unit 3).
- **공유 술어(병렬 스캔 금지)**: "Place ∖ 점유" 를 따로 재구현하지 않는다. `CanPlaceDefenderAt` 의 공간
  게이트(bounds + `TileAt==Place` + `!_occupiedTiles`)를 `SpatialPlacementCheck(int2)` 로 추출해
  **판정과 하이라이트가 같은 함수** 사용(`PaintLanes` 선례). 배치 규칙이 자라도 어긋나지 않는다.
- **변경 구동 리프레시**: 슬로우모(0.2×) 중 전투가 돌아 `_occupiedTiles` 가 변한다. show-1회캐시도
  매프레임재스캔도 오답. bridge 가 show 플래그를 들고 `_occupiedTiles` 변이 지점(수비 사망 해제 / pending
  점유 Add ×2 / clear)에서 `RefreshPlacementHighlightIfShown()`. 폴링·이벤트 불요.
- **파생 상태 1함수 토글**: 지점마다 show/hide 산탄 금지. `desired = (_session.active && !_simulatedDrag)
  || _armedUnit != null` 를 `DefenderDragPlacementController` 가 파생해 idempotent Show/Hide. → 탭 비행 중
  자동 OFF, BeginDrag Disarm→재Show 순서의존 제거, `_sessionGen` 하이재킹 무관. 사용자 요청 "D&D·탭 선택
  두 상태" 가 이 조건식에 정확히 담긴다.
- **2-state(가능/불가)**: 점유칸은 3번째 상태 만들지 않는다 — **그냥 안 밝힘**(= 배치 불가), 그 위 유닛
  스프라이트가 곧 점유 마커. 유닛 몸체 틴트 절대 금지(드림캐쳐 몸체 상태색과 채널 충돌).
- **의미 계약**: 하이라이트 = **공간 조건만**(bounds/Place/점유). hover 는 비용·풀·`_placementAllowed`
  까지 본다. "밝은(=공간상 배치가능) 칸인데 비용 부족이라 hover 는 invalid" 가 정상. 하이라이트에 비용을
  끼우지 않는다 — 끼우면 코스트 리젠 경계마다 보드 전체가 깜빡인다.
- **순수 Presentation**: ECS Component 읽기/쓰기 0. bridge 게이트웨이 경유.

## 작업 단위 목록

| # | 파일 | 작업 | 목적 |
|---|---|---|---|
| 0 | `0_placeable_tileset_fields.md` | data | `TileSetData` 에 placeableTile + fill/rim 색·알파 + 페이드 시간 |
| 1 | `1_placeable_tilemap_layer.md` | view | `_placeableTilemap`(−13 / 드래그 상승 9998) Ensure + Set/Clear + 페이드인 + 상승 + `Clear()` teardown |
| 2 | `2_bridge_predicate_and_wiring.md` | wiring | `SpatialPlacementCheck` 추출 + Show/Hide/RefreshIfShown(변이 지점) + 컨트롤러 파생상태 토글 |
| 3 | `3_range_dark_liner.md` | polish(필수) | rangeTile·조준화살표 스프라이트 다크 라이너 (asset swap) — B 성립 조건 |
| 4 | `4_handoff_summary.md` | doc | Play 확인 후 인계 |

의존: `0 → 1 → 2`. `3` 은 독립이나 B 완성엔 필수. 선행: `placement-attack-range-preview`
(레이어·sorting·상승 선례), `defender-tap-to-place`(arm 훅·`_simulatedDrag`·`_sessionGen`).

## 파이프라인 커버리지

**N/A** — 신규 플레이 오브젝트/생성→렌더 경로 없음. board overlay 타일맵 레이어 1개 추가
(`placement-attack-range-preview` 의 `_rangeTilemap` 과 동류).

## 리뷰 반영 기록

- 구조 리뷰: `_rangeTilemap` 재사용 불가(owner=시분할) → 전용 레이어. **밝은 언더레이라 상승 3티어 필수**
  (마스크였다면 불필요했던 부분 — B 선택의 비용). 변경구동 리프레시. `PlaceableCellScan` 병렬 스캔 →
  `CanPlaceDefenderAt` 공유 술어. 훅 산탄 → 파생 상태 1함수. 비용은 하이라이트에서 제외.
- 시각 리뷰: 밝은 강조가 사거리를 죽이는 문제는 **사거리 다크라이너로 해결**(마스크 반전 대신). 움직임은
  사거리 독점(정적 하이라이트). 점유 3-state 금지. 실제 배치영역 ~절반이라 밝힘이 판을 안 뒤덮음.

## 후속 후보

- 탭 arm 중 스킬 조준 진입 시 하이라이트 억제(SkillAim 과 동시 소음) — 파생상태 조건 추가.
- 연속 배치 시 하이라이트 유지 — arm 이 배치 시 유지되면 리프레시로 다음 빈 칸 즉시 반영.
- 배치영역 실제 비율 정밀 측정(에디터) — "약 절반" 을 seed·프리셋별 수치로 확정.

## 비목표

- 배치 불가를 어둡게 죽이는 다크 마스크(반전 전 초안, 폐기) · 진짜 3D 융기 플랫폼 · 점유 전용 3번째 상태 ·
  사각 테두리 렌더(실제 Place 는 경로 파고든 비사각+구멍) · 하이라이트에 비용/풀 판정 · 유닛 몸체 틴트.
