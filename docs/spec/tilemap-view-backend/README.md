# Tilemap View Backend Spec

**작성일**: 2026-06-12 (rev1 — critic 리뷰 반영)
**연결 문서**: `docs/plans/2026-06-12-tilemap-view-backend-design.md`
**상태**: 진행 중 — unit 0~2 검증 완료 (2026-06-14). 0: `4bd8cff` · 1a: `371130b` · 1b: `f4bfa8e` · 2: `cc62a71`. Legacy3D Play 회귀 0(사용자 확인) + TilemapRect Play(메모리 배선) 보드 페인트·헬스바 게이팅·RebuildDraftMap 잔상0 검증. **남은 것 = `_TilemapBoard`+`BattleBridge` 필드의 영속 씬 저장** — dirty `BattleScene.unity`(무관 827줄) 정리 후. 다음 코드 작업: unit 3(프레젠테이션 경계).
**목표**: Unity Tilemap 을 뷰 백엔드로 도입해 ① 타일 에셋 자유 교체 ② Rectangle / Isometric 레이아웃 토글 실험이 가능한 프레임웍을 만든다. 시뮬레이션 계층(`GeneratedMap`/`FlowFieldSingleton`/`GridMath`/생성기)은 변경하지 않는다.

## 검증 질문

1. **타일 교체**: TileSetData SO 하나를 swap 하면 보드 타일 비주얼이 통째로 바뀌는가?
2. **sim 결정론**: 뷰 모드 3종 전환과 무관하게 같은 matchSeed 가 같은 맵·같은 판 결과(킬/도달 로그)를 내는가?
3. **visual parity**: Tilemap 모드에서 유닛/투사체/VFX/배치가 보드 셀과 정렬되어 그려지는가? (단, ECS 렌더 헬스바는 본 spec 범위에서 제외 — 계약 참조)
4. **Legacy3D 회귀**: Legacy3D 모드는 본 spec 적용 전과 시각·동작이 동일한가?

## 작업 단위

| # | 작업 구분 | 문서 | 목적 |
|---|---|---|---|
| 0 | Mapping | `0_board_space_mapping.md` | `BoardSpace` sim↔view 변환 헬퍼 (위치+방향) + `BoardViewMode` enum + EditMode 테스트 |
| 1 | View | `1_tileset_and_tilemap_view.md` | `TileSetData` SO + `TilemapMapView` 페인터 + 배치 피드백 overlay + iso 정합 고정 테스트 |
| 2 | Bridge | `2_battlebridge_view_mode.md` | BattleBridge 뷰 모드 분기 + ECS 헬스바 게이팅 + backdrop/prop 게이팅 |
| 3 | Boundary | `3_presentation_boundary.md` | 프레젠테이션 write 전수(위치 16+방향 4) `ToView` + 입력 2곳 `ToSim` + sorting sim 좌표 보존 |
| 4 | Camera | `4_camera_and_sorting.md` | 모드별 카메라 프리셋 + 레이어 sorting. TilemapRect Play 검증 |
| 5 | Iso | `5_iso_layout.md` | Isometric 시각 검증 + 모드 3종 sim 결정론 확인 |
| 6 | Handoff | `6_handoff_summary.md` | 인계 요약 (종료 시 작성 — 의존 체인 밖) |

의존 순서: `0 → 1 → 2 → 3 → 4 → 5`.

## Feature-wide 계약

- **시뮬레이션 불변**: `Assets/_Project/Scripts/Battle/**`, `Data/MapGrid/**`, `GeneratedMap`, `FlowFieldSingleton`, `GridMath` 는 본 spec 에서 수정하지 않는다. sim 공간 = 현행 rect XZ 월드 (`origin + cell * tileSize`).
- **단일 변환 지점**: `BoardSpace` 가 sim↔view 변환의 유일한 코드 경로다. 위치뿐 아니라 **방향/회전 벡터도 경계를 넘는다** — facing/LookRotation/cast 방향 계산은 반드시 같은 공간 좌표끼리만 수행하고, 공간을 섞는 빼기 연산(예: sim 타겟 − view transform.position) 금지.
- **공간 규약**: ECS 이벤트/스냅샷이 운반하는 좌표는 항상 sim 공간이다. 뷰 클래스 내부(`transform.position` 이후)의 모든 비교·방향·보간은 view 공간끼리만. 변환은 각 소비 지점의 진입부 1회.
- **ECS 렌더 비주얼 carve-out**: Entities Graphics 가 `LocalTransform` 에서 직접 렌더하는 비주얼(현재 헬스바 유일)은 `BoardSpace` 로 가로챌 수 없다. Tilemap 모드에서는 **생성을 게이팅(비활성)** 하고, Mono 오버레이 헬스바는 후속 후보로 이관. ECS 시스템에 뷰 모드 지식을 주입하는 방식은 금지 (맥락 경계 위반).
- **Legacy3D = identity**: `BoardViewMode.Legacy3D` 에서 `BoardSpace` 는 입력을 그대로 반환한다. 기존 동작과의 동등성이 모든 단계의 회귀 기준.
- **iso 변환의 권위는 Unity Grid**: Tilemap 모드의 셀↔월드 정합 기준은 `GridLayout.CellToLocalInterpolated`/`Tilemap.GetCellCenterWorld` 다. `BoardSpace` 에 iso 수식을 하드코딩하지 않고 주입된 `GridLayout` 에 위임한다. 정합은 unit 1 의 고정 테스트로 못 박는다.
- **Tilemap 모드 sim origin**: 무조건 `float3.zero`. 비활성 `mapView` 의 transform 을 읽지 않는다. view 원점 오프셋은 `BoardSpace` 의 `viewOrigin` 에만 존재한다.
- **Tilemap 은 뷰 전용**: source of truth 는 `MapDocument`/`GeneratedMap`. `Tilemap.GetTile` 류로 게임 로직(배치 가능, walkable, 사거리)을 판정하는 코드 금지. Tilemap API 는 MonoBehaviour 계층 전용 — ECS/Burst 코드에서 참조 금지 (절대 제약 1번 유지).
- **타일 교체 단위**: `TileSetData` SO (`MapTileType → TileBase` 매핑). 본 spec 은 placeholder 단색 타일로 동작 검증까지만. 아트 폴리시/RuleTile 은 후속.
- **모드 3종**: `{Legacy3D, TilemapRect, TilemapIso}`. Hexagonal 은 인접성·경로 계약(90° 직선 세그먼트, Chebyshev)이 달라 범위 밖. 모드 전환은 Play 재시작(씬 리로드) 전제 — 런타임 중 전환 비지원.
- **Tilemap 모드의 외곽**: 시즌 backdrop(`BackdropMounter`)과 `BackgroundPropPlacer` 는 Tilemap 모드에서 비활성. 2D 외곽 연출은 후속 theming spec.
- **하드코딩 금지 준수**: 카메라 프리셋(투영/각도/거리), 타일 매핑, 셀 크기는 전부 SO 또는 SerializeField.
- **RNG/결정론**: 본 spec 은 RNG 를 일절 추가하지 않는다. 맵 생성·웨이브 경로(matchSeed) 무변경.

## 후속 후보

- Tilemap 모드용 Mono 헬스바 오버레이 (ECS 헬스바 carve-out 의 대체재)
- Hexagonal 레이아웃 (생성기/거리 함수 추상화 필요 — 별도 spec)
- Tilemap → MapDocument 임포터 (에디터 브러시 맵 저작, 맵툴)
- RuleTile / 시즌별 타일 아트 (theming)
- Tilemap 모드용 2D 외곽 연출 (backdrop 대체)
- 실험 종료 후 MapView(region mesh) 제거 여부 결정
- 3D VFX(파티클/스핀 축/`Vector3.up` 관성)의 rect·iso 정합 튜닝 — 본 spec 은 위치 정렬까지만 보장
