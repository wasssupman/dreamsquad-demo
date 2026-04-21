# Unit Assignment

**작업 구분**: Phase 2

## 목적

현재 구현된 10종 Defender 에 on-place skill 을 임시 배정한다. 이번 목표는 최종 밸런스가 아니라, 배치 순간 효과의 차이를 플레이 중 체감할 수 있게 하는 것이다.

## 임시 배정표

| Defender | 역할 | onPlaceEffect | range | magnitude | duration |
|---|---|---:|---:|---:|---:|
| Scout | 저비용 유틸 | `GainCost` | 0 | 1 | 0 |
| Archer | 기본 원거리 | `BindNearby` | 2.5 | 0.1 | 1.5 |
| Ranger | 빠른 연사 | `ReduceSkillCooldown` | 0 | 2 | 0 |
| Marksman | 정밀 원거리 | `ForwardProjectile` | 6 | 70 | 0 |
| Sniper | 고비용 폭딜 | `ForwardProjectile` | 8 | 120 | 0 |
| Piercer | 관통형 | `ForwardProjectile` | 5 | 90 | 0 |
| Bruiser | 근접 공격형 | `MeleeBurst` | 1.5 | 70 | 0 |
| Bastion | 탱커 / 근접 장악 | `MeleeBurst` | 1.25 | 50 | 0 |
| Guardian | 방어형 지원 | `BoostNearbyDefenders` | 1.5 | 0.2 | 0 |
| Cannon | 고피해 포격 | `MeleeBurst` | 2 | 80 | 0 |

## Cannon 메모

Cannon 은 원거리 포격 유닛이므로 최종적으로는 `ForwardProjectile` 보다 "지점 폭발" 계열이 더 자연스럽다. 하지만 이번 범위에 지점 폭발 신규 타입은 없으므로 임시로 `MeleeBurst` 를 배정해 배치 주변 폭발처럼 사용한다.

후속 선택지:

- Cannon 을 `ForwardProjectile` 로 바꾸고 `range=4`, `magnitude=100` 으로 둔다.
- `OnPlaceEffectType.PointBlast` 를 Phase 11+ 에 추가한다.

## 완료 기준

- 10개 Defender asset 전부 on-place effect 가 설정된다.
- `Guardian` 포함 10종 전체가 Inspector 에서 유효한 deployment 필드를 가진다.
- Archer `BindNearby` 는 배치 순간 1회만 발동하고 N초간 속박한다.
- `None` 으로 남은 Defender 가 없다.
