# 0 — Spine movement facing

## 목적

Spine 유닛의 이동 방향과 좌우 facing 을 동기화한다. 공격 시에는 기존처럼 타겟을 바라보고, 공격 애니메이션이 끝난 뒤 이동이 재개되면 이동 방향으로 다시 전환한다.

## 변경 대상

- `Assets/_Project/Scripts/Presentation/SpineUnitView.cs`

## 구현

- `UpdatePosition(world)` 에서 이전 sim 위치와 새 sim 위치를 `BoardSpace.ToView` 기준으로 비교한다.
- view-space X 이동량이 epsilon 보다 크면 기존 Spine convention 에 맞춰 `Skeleton.ScaleX` 를 갱신한다.
- `FaceToward(worldPoint)` 의 좌우 반전 로직은 공통 helper 로 빼서 공격/이동이 같은 규칙을 쓰게 한다.
- 현재 track 0 에 비루프 attack animation 이 재생 중이면 movement-facing 갱신을 스킵한다.
- 적 Spine 은 defender Spine 과 좌우 sign 규칙이 반대라 `_defenderExtras == null` 경로에서 enemy sign 을 사용한다.

## 완료 기준

- compile 통과.
- 적이 타겟을 향해 공격한다.
- 공격 후 반대 방향으로 이동할 때 이동 방향으로 몸을 돌린다.
- stationary defender / idle 상태에서 미세 떨림이 없다.

---

✅ **완료 2026-07-02** — Unity refresh 후 compile PASS(콘솔 error 0). MCP Play runtime probe PASS: 적 이동 오른쪽 `ScaleX=+1`, 왼쪽 공격 타겟 `ScaleX=-1`, 공격 중 이동은 공격 facing 유지, 공격 종료 후 오른쪽 이동에서 `ScaleX=+1` 복귀.
