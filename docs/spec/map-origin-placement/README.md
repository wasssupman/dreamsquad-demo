# Spec — map-origin-placement (맵 위치 기준 배치)

**상태: 완료 2026-06-10** — 작업 0~5 전부 구현·검증. handoff: `6_handoff_summary.md`

| 단위 | 커밋 |
|---|---|
| 0 grid_math_origin | 8362150 |
| 1 flowfield_origin_singleton | a852904 |
| 2 bridge_grid_conversions | fa69e34 |
| 3 ecs_systems_origin | 9312dbb |
| 4 placement_input_plane | 8b8ab6f |
| 5 backdrop_origin | (이 커밋) |

## 목표

전투 시뮬레이션 좌표계를 **MapView 의 실제 씬 위치**에 정렬한다. 현재 ECS 시뮬레이션과 배치 입력은 월드 절대 원점(0,0,0)을 기준으로 하지만, MapView 비주얼은 자식 `localPosition` 으로 그려져 MapView.transform 을 따라간다. MapView 를 옮기면 둘의 원점이 어긋나 **유닛을 배치해도 화면 밖(옮기기 전 원점)에 스폰**되고 클릭이 빈 셀로 매핑된다.

이 spec 은 단일 **board origin** 을 도입해 모든 grid↔world 변환에 일관되게 적용한다. 상수/하드코딩 없이 origin 은 `MapView.transform.position` 에서 나온다.

## 검증 질문

> MapView 의 Transform 위치를 임의로 바꾼 뒤 Play 했을 때, 클릭/드래그 배치한 유닛이 **옮겨진 타일 위에 정확히** 나타나고, 적·투사체·스킬·이동이 모두 같은 좌표계에서 동작하는가?

## 연결 문서

- `CLAUDE.md` — ECS 맥락 분리, BattleBridge 게이트웨이 규칙
- 근거 코드: `Assets/_Project/Scripts/Core/MapView.cs`(비주얼 local), `Battle/Movement/GridMath.cs`, `Battle/Effects/FlowFieldSingleton.cs`

## 확정된 아키텍처 결정 (2026-06-10)

1. **board origin 단일 소스 = `MapView.transform.position`**. BattleBridge 가 init 때 1회 읽어 캡처한다. (대안: 전용 board-root → 기각)
2. **변환 범위 = 이동(translation)만**. 회전/스케일은 비목표(후속 후보). origin 은 `float3` 위치만 의미.

## feature-wide 계약 (load-bearing)

- **단일 소스 of truth**: board origin 은 BattleBridge 가 `mapView.transform.position` 에서 캡처한 `_boardOrigin` (float3). 다른 어떤 곳도 origin 을 독자적으로 계산하지 않는다.
- **ECS 전파 경로**: origin 은 `FlowFieldSingleton.origin` 으로 모든 Burst 시스템에 전달된다. 시스템은 이미 이 싱글턴을 읽으므로 신규 싱글턴/채널을 만들지 않는다.
- **GridMath 계약**: `WorldToCell(worldPos, tileSize, gridSize, origin)` 은 `worldPos - origin` 을 셀화하고, `CellToWorldCenter(cell, tileSize, y, origin)` 은 `origin + cell*tileSize` 를 돌려준다. `origin` 은 **기본값 `default`(=zero) 파라미터** 로 추가해 매 커밋 컴파일 green 을 유지한다 (origin=0 이면 기존과 동일 동작).
- **MonoBehaviour 입력**: 배치 레이캐스트 평면은 `new Plane(Vector3.up, boardOrigin)` 을 쓰고, 셀 변환은 BattleBridge 헬퍼(`DebugWorldToCell`/`GridToWorldCenterVector`)를 경유한다. 입력 코드가 origin 산술을 직접 하지 않는다.
- **MapView 비주얼은 변경 없음**: MapView 는 이미 자식 local 좌표라 origin 을 자동 반영한다. MapView.cs 는 이 spec 에서 수정 대상 아님.
- **경계 준수**: origin 캡처/싱글턴 쓰기는 BattleBridge(게이트웨이)에서만. FlowFieldSingleton 은 Effects 맥락 소유 — origin 필드 추가도 Effects 맥락 정의에서.

## 구현 문서 목록

| 파일 | 작업 구분 | 목적 |
|---|---|---|
| `0_grid_math_origin.md` | foundation | `GridMath` / `MovementCellTrim` 에 `origin` 기본값 파라미터 추가 + EditMode 테스트 |
| `1_flowfield_origin_singleton.md` | contract | `FlowFieldSingleton.origin` 필드 추가, BattleBridge 가 `_boardOrigin` 캡처 후 주입 |
| `2_bridge_grid_conversions.md` | bridge | BattleBridge 의 모든 grid↔world 변환·스폰이 `_boardOrigin` 사용 |
| `3_ecs_systems_origin.md` | systems | Attack/Movement/Meteor/ZoneApply/HazardCast/EffectSpawner 가 `field.origin` 전파 |
| `4_placement_input_plane.md` | input | PlacementInput/DragController/SkillBar/디버그메뉴 레이캐스트 평면을 origin 기준으로 |
| `5_backdrop_origin.md` | visual | BackdropMounter board center 에 origin 반영 (엣지 프롭 정렬) |
| `6_handoff_summary.md` | handoff | 구현 종료 인계 (구현 시 작성) |

## 비목표 / 후속 후보

- **회전/스케일 지원** — 현재 translation 만. TRS 가 필요하면 별도 spec.
- **런타임 중 MapView 이동 추적** — origin 은 init 1회 캡처. 플레이 도중 MapView 를 움직이는 시나리오는 범위 밖.
- **카메라 자동 프레이밍 복원** — 최근 제거됨(`72c4022`). 본 spec 과 무관.
