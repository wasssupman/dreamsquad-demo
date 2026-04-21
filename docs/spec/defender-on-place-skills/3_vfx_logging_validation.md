# VFX Logging Validation

**작업 구분**: Phase 3

## 목적

On-place skill 의 시각 피드백, 로그, 검증 기준을 고정한다.

## VFX

| Effect | VFX |
|---|---|
| `BindNearby` | placement ring + cyan/blue pulse |
| `MeleeBurst` | placement ring + short radial burst |
| `ForwardProjectile` | line/beam or fast projectile streak |
| `GainCost` | small gold pulse, 없으면 placement ring fallback |
| `ReduceSkillCooldown` | blue pulse, 없으면 placement ring fallback |
| `SlowPulse` | 기존 slow field pulse |
| `BoostNearbyDefenders` | 기존 boost pulse |

VFX 는 gameplay 판정과 분리한다. VFX 실패가 배치/스킬 실패로 이어지면 안 된다.

## Logging

기존 placement 로그에 on-place 결과를 추가하거나 별도 event 로 기록한다.

필수 정보:

- defender id/name
- tile
- effect type
- affected count
- magnitude
- duration

효과별 affected 의미:

| Effect | affected |
|---|---|
| `BindNearby` | 속박 적용 enemy 수 |
| `MeleeBurst` | 피해 적용 enemy 수 |
| `ForwardProjectile` | 피해 적용 enemy 수 |
| `GainCost` | 실제 증가 cost 양 |
| `ReduceSkillCooldown` | 감소 적용 skill 수 |

## Validation

Play smoke:

1. Archer 배치 시 주변 적이 N초간 속박.
2. Bruiser/Bastion/Cannon 배치 시 주변 적이 즉시 피해.
3. Marksman/Sniper/Piercer 배치 시 전방 피해.
4. Scout 배치 시 cost 증가.
5. Ranger 배치 시 skill cooldown 감소.
6. PendingDeployment 중 공격/피격 없음.
7. Deployment 완료 후 일반 combat 참여.

EditMode:

1. `GainCost` 가 max cost 를 넘지 않는지 테스트.
2. `ReduceSkillCooldown` 이 남은 쿨타임을 0 아래로 만들지 않는지 테스트.
3. PendingDeployment 필터가 Attack/Damage query 에 적용되는지 확인.

## 완료 기준

- Unity compile 0 errors.
- Console error/warning 0.
- 10종 배치 smoke 통과.
- log 에 effect type 과 affected count 가 남는다.
