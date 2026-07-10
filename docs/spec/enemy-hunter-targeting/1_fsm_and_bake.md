# 1 — FSM 헌터 판정 + 스폰 베이크

## 목적

`EnemyAiStateSystem` 이 보스(헌터)에 대해 최근접 방어유닛을 잡아 `HuntTarget` 에 쓰고 `Chasing` 으로 전이시킨다. 보스 스폰 시 `HuntTarget` 을 사전 부착한다.

## 변경 대상

- `Assets/_Project/Scripts/Battle/Combat/EnemyAiStateSystem.cs`
- `Assets/_Project/Scripts/Bridge/BattleBridge.cs` (`BakeNightmareMechanics` 인근 — 보스 = BossTag 경로)

## 구현

### EnemyAiStateSystem

- `BossTag` lookup 추가(RO). 엔티티가 `isHunter = bossTagLookup.HasComponent(enemyEntity)`.
- 비-aggro && `hasAttack` 경로에서 `hasFireTarget` 계산 후:
  - `isHunter && !hasFireTarget` 면 `SelectNearestTarget`(unit 0)로 최근접 방어유닛 → `HuntTarget.value` write, `hasHuntTarget = idx >= 0`.
  - 사거리 내 타겟 있으면(`hasFireTarget`) HuntTarget 은 굳이 안 씀(Engaging 이 우선). 안전하게 `HuntTarget.value = Null` 로 클리어(추격 잔상 방지).
- `Evaluate(aggroed, guardianInRange, hasFireTarget, isHunter, hasHuntTarget)` 로 상태 set.
- **HuntTarget write 는 컴포넌트 존재 시에만**(HasComponent 가드) — 비-보스엔 미부착이라 무시.
- ⚠ `HasFireTarget` 미러 주석(AttackSystem 동기화)은 그대로. 최근접 선정은 fire 후보와 같은 mask/필터 풀을 쓰되, **사거리 조건만 뺀 전체 거리 최소**.

### BattleBridge 베이크

- `BakeNightmareMechanics`(보스 분기, unit 5)에서 `HuntTarget { value = Entity.Null }` 부착. BossTag 와 같은 자리.
- 일반 적은 미부착 → FSM 가드가 무시.

## 완료 기준

- [ ] 보스 스폰 시 `HuntTarget` 부착 확인(reflection/Play).
- [ ] 비-aggro 보스가 사거리 밖 방어유닛 존재 시 `Chasing` + HuntTarget=최근접 set.
- [ ] 방어유닛 0 시 `Marching`. 사거리 내면 `Engaging`(HuntTarget 클리어).
- [ ] 일반 적 FSM 무변경(BossTag 없으면 기존 경로) — 무회귀.
- [ ] (렌즈 B) BossTag lookup·HuntTarget write 맥락 경계·Burst.
