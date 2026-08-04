# 1. 골 엔티티 + Faction.Goal — 행동 변화 0 토대

## 목적

M>0 골을 전투 엔티티로 심에 존재시킨다. 이 unit 까지는 **어떤 행동도 바뀌지 않는다** — 아무 공격자의 targetMask 에도 Goal 비트가 없어 타겟팅되지 않고, 유출 게이트도 그대로다.

## 변경 대상

- `Assets/_Project/Scripts/Battle/Units/Faction.cs`
- `Assets/_Project/Scripts/Battle/Units/GoalPoint.cs` (신설)
- `Assets/_Project/Scripts/Bridge/BattleBridge.cs`

## 구현

1. `Faction` 에 `Goal = 1 << 3` 추가.
2. `GoalPoint : IComponentData { int2 cell; int goalIndex; }` — Units 소유. 골 엔티티 식별 + 셀 앵커.
3. `BattleBridge.BuildFlowField` 직후 골 스폰 (신설 private 메서드): `goals[i]` 의 M(unit 0 폴백 규칙) > 0 이면 엔티티 생성 —
   `GoalPoint{cell, i}` + `FactionTag{Faction.Goal}` + `Health{M, M}` + `IncomingDamage` 버퍼 + `LocalTransform`(셀 월드 좌표, blocking hazard 스폰과 같은 보드 변환).
4. teardown: `DestroyEntitiesByType<GoalPoint>()` 를 기존 hazard/유닛 정리 지점(매치 경계 + 브리지 파괴)에 추가.
5. `AttackState`/`PathFollowState` 는 부여하지 않는다(공격·이동 없음). `DefenderClassTag` 도 없음 — `EnemyTargetFilter` 는 클래스 없는 후보를 거르지 않으므로(cclass=-1) 이후 unit 에서 별도 처리 불필요.

주의: 골 엔티티는 `Health` 를 갖는 순간 `HealthDeathSystem`/`UnitLifecycleSystem` general-dead 루프의 잠재 대상이 된다. 이 unit 시점엔 피해 유입 경로가 없어 실행되지 않지만, 붕괴 이벤트(unit 4) 전에 피해 개통(unit 2)이 먼저 들어가므로 **unit 2~3 구간에서 붕괴 시 이벤트 없이 소멸**한다 — 의도된 중간 상태(현행 유출로 즉시 전환은 이미 성립).

## 완료 기준

- [ ] compile + 기존 EditMode/PlayMode 무회귀.
- [ ] M>0 테스트 맵 Play 진입 시 골 엔티티 존재(Entities 디버거 또는 로그), M=0 맵은 미스폰.
- [ ] 연속 2판 진행 시 골 엔티티 누수 없음(teardown 확인).
- [ ] 적/방어유닛 행동 현행과 동일(스모크).
