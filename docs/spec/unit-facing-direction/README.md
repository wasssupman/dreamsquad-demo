# unit-facing-direction — Spine 유닛 이동/공격 방향 보정

> 상태: 완료 2026-07-02 — Unity compile PASS, MCP Play runtime probe PASS.

## 배경 / 문제

Spine 유닛은 공격 시작 이벤트에서만 `FaceToward(target)` 로 좌우 방향을 갱신한다. 이후 이동 재개 시 `BattleBridge.SyncMonoUnitViews()` 는 위치만 갱신하므로, 왼쪽을 보고 공격한 적이 오른쪽으로 이동하면서도 계속 왼쪽을 보는 문제가 생긴다.

## 목표

Spine 적 유닛이 공격할 때는 타겟을 바라보고, 이동할 때는 실제 이동 방향을 바라보게 한다.

## 작업 단위

| # | 문서 | 목적 |
|---|---|---|
| 0 | `0_spine_movement_facing.md` | `SpineUnitView` 에 이동 방향 facing 보정 추가 |

## 공통 원칙

- ECS 시뮬레이션은 변경하지 않는다. 위치 동기화를 이미 담당하는 Presentation 계층에서 처리한다.
- 공격 방향이 우선이다. 공격 애니메이션이 재생 중일 때 이동 방향 갱신이 공격 facing 을 덮지 않는다.
- 기존 Spine 좌우 convention(`ScaleX=+abs` 왼쪽, `ScaleX=-abs` 오른쪽)을 유지한다.
- Quad fallback 방향 전환은 이번 범위 밖이다.

## 후속 후보

- Quad fallback directional sprite/mesh 가 생기면 `QuadUnitView` 에도 동일한 movement-facing 정책 적용.
