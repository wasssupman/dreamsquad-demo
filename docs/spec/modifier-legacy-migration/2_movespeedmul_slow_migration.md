# 2. MoveSpeedMul Slow Migration

## 목적

`CcEffect.Slow` 를 stat modifier 로 이전한다. 이동 속도 배율은 `ModifierStats.moveSpeedMul` 로 합성하고, `CcEffect` 는 impulse/stun/root 같은 movement-control 이벤트만 남긴다.

## 변경 대상

- `Assets/_Project/Scripts/Battle/Effects/Modifiers/ModifierTypes.cs`
- `Assets/_Project/Scripts/Battle/Effects/Modifiers/ModifierStats.cs`
- `Assets/_Project/Scripts/Battle/Effects/Modifiers/ModifierStatsAggregateSystem.cs`
- `Assets/_Project/Scripts/Battle/Movement/MovementSystem.cs`
- `Assets/_Project/Scripts/Battle/Effects/CcEffect.cs`
- `Assets/_Project/Scripts/Battle/Effects/CcApplySystem.cs`
- slow producer 호출부: hazard, projectile, on-place 등

## 구현

1. `StatKind.MoveSpeedMul` 과 `ModifierStats.moveSpeedMul` 을 추가한다. 기본값은 `1f`, 합성은 multiplicative.
2. `ModifierStatsAggregateSystem` 에 MoveSpeedMul 합성을 추가한다.
3. `MovementSystem` 은 `CcEffect.Slow` 대신 `ModifierStats.moveSpeedMul` 을 read-only 로 소비한다.
4. 기존 slow producer 는 `EnemyCcEvents` 대신 `StatModifierApplyEvents` 로 `MoveSpeedMul` 을 enqueue 한다.
5. `CcEffect` 에서 Slow kind/필드가 더 이상 필요 없으면 제거한다. 제거가 Stun/Impulse 와 얽히면 deprecated path 를 한 단계 유지하되 완료 기준 전까지 제거한다.

## 완료 기준

- Unity compile error 0.
- 기존 ice/slow hazard 또는 slow projectile 의 이동 속도 감소 체감이 유지된다.
- MovementSystem 이 raw modifier buffer 를 보지 않고 `ModifierStats` 캐시만 read 한다.
- `CcEffect` 에 stat multiplier 성격의 Slow 가 남지 않는다.
