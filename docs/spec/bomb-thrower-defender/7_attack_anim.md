# 7 — 폭탄 발사 공격 애니메이션 (Play 후속)

## 목적

폭탄맨이 폭탄을 던질 때 공격 애니메이션이 재생되지 않는 문제 수정. 정상 공격 경로는
`UnitAttackVisualEvent` 를 enqueue 해 SpineUnitPool 이 애니+facing 을 재생하지만, 폭탄
분기(unit 4)는 그 앞에서 `continue` 라 이벤트를 건너뛴다.

## 변경 대상

- `Assets/_Project/Scripts/Battle/Combat/AttackSystem.cs` — 폭탄 분기 발사 성사 시 `UnitAttackVisualEvent` enqueue

## 구현

- 폭탄 발사 성사(`landValid` → request enqueue) 직후, 정상 경로(AttackSystem:503-511)와
  동형으로 enqueue:
  - `attacker = attackerEntity`
  - `targetWorld = landWorld` (착지 셀 → 던지는 방향을 바라봄, facing 재사용)
  - `attackAnimPeriod = attack.ValueRO.cooldownDuration` (투척 주기 = 애니 compress-to-fit)
  - `if (attackWriter.HasValue)` 가드 (기존과 동일).
- 애니 클립명은 SO `attackAnimation`(현재 `Attack3`) — 던지기 모션 적합성은 Play 데이터
  튜닝(3s 주기로 늘어지면 클립/주기 조정). sim 무변경.

## 완료 기준

- [ ] compile 0.
- [ ] (Play) 폭탄맨이 던질 때 공격 애니 재생 + 착지 방향을 바라봄.
- [ ] 기존 유닛 애니 회귀 없음(분기 국소 추가).
