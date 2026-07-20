# 1. Effects — 획득 게이트 + per-enemy chase field 부착/해제

## 목적

어그로 획득 시점에 (a) 전투수단 없는 적 거부, (b) 목적지 후보/도달가능 판정, (c) 통과 시 per-enemy chase dist field 를 부착한다. 좀비 클래스(도달 불가 Chasing)가 **생성 자체가 안 되게** 만든다.

## 변경 대상

- `Assets/_Project/Scripts/Battle/Effects/AggroChaseCell.cs` (신규 — dist 버퍼 요소)
- `Assets/_Project/Scripts/Battle/Effects/AggroStateSystem.cs` — Pass 1(해제 시 버퍼 제거) + Pass 3(게이트+부착)
- `Assets/_Project/Tests/EditMode/AggroStateSystemTests.cs` — MakeEnemy 에 기본 프로파일(실데이터 반영) + 게이트 테스트 추가

## 구현

- `AggroChaseCell { int dist }` DynamicBuffer — **AggroStateSystem(Effects)만 쓴다**. Aggroed 와 수명 동기: 획득 시 부착, 해제(가디언 사망/orphan) 시 제거. 적 사망은 엔티티 소멸로 자연 정리.
- Pass 3 드레인 게이트 (순서 = 비용순):
  1. `ResolveTileRange` = NoAttack → 거부 (AttackState·AggroAttackProfile 둘 다 없음 — 구 M5 고착의 원천 차단)
  2. 기존 capacity/선점 게이트 (무변경)
  3. FlowFieldSingleton 존재 시: walkable mask(`MovementCellTrim.IsWallCell` + ObstacleSingleton — 계약 4) → `BuildChaseField`. 소스 0 **또는** 적 셀 dist=MaxValue → 거부. 통과 → Aggroed + 버퍼 부착.
- **flow field 부재(합성 테스트 월드) = 기하 게이트/버퍼 생략, 부착만** — 기존 capacity/선점/해제 테스트는 정책 하네스로 유지. 실전(배틀)은 항상 flow field 가 있다.
- mask/tmp 배열은 드레인 중 기하 게이트가 처음 필요할 때 1회 lazy 할당(Temp).

## 완료 기준

- compile 0 · EditMode 전체 green (기존 aggro 테스트는 MakeEnemy 프로파일 추가로 유지).
- 신규 테스트: 전투수단 없음 → 미부착 / 도달 불가(고립) → 미부착 / 도달 가능 → Aggroed+버퍼 부착, dist[적 셀] 유한 / 가디언 사망 해제 시 버퍼도 제거.
