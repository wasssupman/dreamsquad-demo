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

- [x] 보스 스폰 시 `HuntTarget` 부착(BakeNightmareMechanics). — reflection/Play 확인은 unit 3.
- [x] 비-aggro 보스가 사거리 밖 방어유닛 존재 시 `Chasing` + HuntTarget=최근접 set (FSM 블록).
- [x] 방어유닛 0 시 `Marching`. 사거리 내/aggro 면 HuntTarget 클리어(Null).
- [x] 일반 적 FSM 무변경(BossTag 없으면 블록 no-op) — EditMode 648/650 무회귀.
- [ ] (렌즈 B) BossTag lookup·HuntTarget write 맥락 경계·Burst — unit 2 와 묶어 실행.

확인 2026-07-11 — 컴파일 클린 + EditMode 648/650 그린 + code-review(medium): FocusUntilDead 락 미러 미적용은 **의도적 분기**(추격≠조준)로 코드 주석+후속 기록, 나머지 findings 0.

## review 메모 (의도적 결정)

- `SelectNearestTarget` 는 `HasFireTarget` 의 FocusUntilDead 락을 미러하지 않는다. 추격은 거리 좁히기 목적이라 최근접이 맞고, 사거리 진입 후 조준 대상은 `HasFireTarget` 이 락 규칙대로 재결정한다. FocusUntilDead 헌터가 실제로 생기면 재평가(후속).
