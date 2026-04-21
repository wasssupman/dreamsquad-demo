# On-place Effect Implementation

**작업 구분**: Phase 1

## 목적

신규 on-place 효과 5종을 `BattleBridge.ApplyOnPlaceEffect` 안에서 구현한다. 효과는 배치 순간 1회만 적용되며, 지속 효과가 필요한 경우 기존 ECS effect component 를 재사용한다.

## BindNearby

주변 적을 N초간 속박한다.

- range: `onPlaceRange`
- duration: `onPlaceDuration`
- magnitude: 속박 강도 또는 최소 slow 계수. 현재 구현에서는 이동을 사실상 멈추는 강한 slow/bind 로 처리한다.
- 대상: 배치 cell 중심 반경 안의 살아있는 공격 유닛
- 주의: 배치 순간에만 대상 검색. 이후 새로 진입한 적에게는 영향 없음.
- 약간의 시간 버퍼 느낌은 `placementSkillDelay` 또는 presentation timing 으로 처리한다.

## MeleeBurst

배치 위치 주변에 강한 근접 범위 피해를 준다.

- range: `onPlaceRange`
- damage: `onPlaceMagnitude`
- 대상: 배치 cell 중심 반경 안의 공격 유닛
- 적용: `IncomingDamage` append

## ForwardProjectile

배치 위치에서 가장 가까운 path 방향으로 전방 투사체형 타격을 적용한다.

- range: `onPlaceRange`
- damage: `onPlaceMagnitude`
- 방향: `FindNearestPathDirection(placedCell)`
- v1 구현은 즉시 판정 line/segment damage 허용. 실제 projectile visual 은 VFX 후속.

## GainCost

배치 순간 cost 를 추가한다.

- amount: `Mathf.RoundToInt(onPlaceMagnitude)`
- `CostRuntime.AddCost(int amount)` 사용
- max cost 를 넘지 않도록 `CostRuntime` 내부에서 clamp

## ReduceSkillCooldown

현재 skill cooldown 을 감소시킨다.

- seconds: `onPlaceMagnitude`
- `SkillRuntime.ReduceAllCooldowns(float seconds)` 사용
- 0 아래로 내려가지 않도록 clamp

## 기존 효과 유지

`SlowPulse`, `BoostNearbyDefenders` 는 기존 Phase 효과를 유지한다. 단, `PendingDeployment` 상태의 defender 는 synergy/targeting 대상에서 제외한다.

## 완료 기준

- 각 effect branch 가 null-safe 하다.
- target 없음은 실패가 아니라 affected count 0 으로 처리한다.
- cost/cooldown API 가 별도 단위 테스트 가능하다.
- 배치 순간 이후 새 적에게 반복 적용되지 않는다.
