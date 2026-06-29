# 0 — 필드 plumbing (hitDelaySec)

## 목적

hit-delay config/runtime 필드를 추가하고 baking 까지 배선. **기본 0 → 동작 무변경**(`AttackSystem` 미수정). unit 1 의 토대.

## 변경 대상

- `Assets/_Project/Scripts/Battle/Combat/AttackState.cs` — `hitDelaySec`(config) + `hitDelayRemaining`(runtime).
- `Assets/_Project/Scripts/Data/AttackUnitData.cs` / `DefenderUnitData.cs` — `hitDelaySec = 0f`.
- `Assets/_Project/Scripts/Bridge/BattleBridge.cs` — enemy(`:3578`)/defender(`:2949`) `AttackState` 생성에 `hitDelaySec` 세팅.

## 구현

- `AttackState` 신규 필드는 C# object initializer 에서 미세팅 시 default 0 → hazard caster(`:2986`)/taunt(`TauntAttackGrantSystem:46`) 는 **0 유지**(변경 불필요).
- enemy ← `entry.unitType.hitDelaySec`, defender ← `unitData.hitDelaySec`.
- `hitDelayRemaining` 은 항상 0 으로 시작(런타임이 세팅).

## 완료 기준

- compile 0 에러.
- EditMode 무회귀(기존 26).
- **동작 변화 0** — `hitDelaySec` 를 아무도 안 읽으므로(unit 1 전) 공격 타이밍 현행 유지.
