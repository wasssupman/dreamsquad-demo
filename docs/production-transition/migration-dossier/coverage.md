# 이식 준비 문서 범위 현황

> 상태: **축적 중**
>
> 근거 기준: `2d35df0680ce97d29b78101120cb9fae63c5a8ad`
>
> Freeze 후보: **아님**

이 표는 gameplay 의미를 얼마나 문서화했는지와 실제 migration 준비도를 분리한다.
`coverage: complete`는 정해진 범위를 빠짐없이 다뤘다는 뜻일 뿐, 담당자 검토나
이식 포함 승인을 뜻하지 않는다. 현재 disposition은 모두 후보 상태이며,
freeze 범위를 정할 때만 `include`, `defer`, `exclude`로 바꾼다.

허용 값은 다음과 같다.

- `disposition`: `candidate | include | defer | exclude`
- `coverage`: `none | partial | complete`
- `review_status`: `draft | review_requested | reviewed | stale`
- `migration_readiness`: `blocked | conditional | ready`

| area_id | gameplay_area | disposition | card | coverage | review_status | migration_readiness | depends_on | blocking_decisions | as_of_commit | next_trigger |
|---|---|---|---|---|---|---|---|---|---|---|
| `MIG-AREA-001` | Match lifecycle와 terminal | `candidate` | [core-combat-lifecycle](cards/core-combat-lifecycle.md) | `partial` | `draft` | `blocked` | `MIG-AREA-004`, `MIG-AREA-005`, `MIG-AREA-012` | `MIG-DEC-CORE-006`, `MIG-DEC-CORE-007`, `MIG-DEC-CORE-008`, `MIG-DEC-CORE-012` | `2d35df0680ce97d29b78101120cb9fae63c5a8ad` | Core terminal 질문을 owner가 검토하거나 관련 gameplay spec이 완료될 때 |
| `MIG-AREA-002` | Time, ordering, numeric, identity와 randomness 의미 | `candidate` | [core-combat-lifecycle](cards/core-combat-lifecycle.md) | `partial` | `draft` | `blocked` | `none` | `MIG-DEC-CORE-002`, `MIG-DEC-CORE-009`, `MIG-DEC-CORE-010`, `MIG-DEC-CORE-011`, `MIG-DEC-CORE-012` | `2d35df0680ce97d29b78101120cb9fae63c5a8ad` | 시간·순서·수치·식별·무작위성 중 하나의 gameplay 의미가 바뀌거나 freeze 검토를 시작할 때 |
| `MIG-AREA-003` | Map, path와 occupancy | `candidate` | `none` | `none` | `draft` | `blocked` | `none` | `none` | `2d35df0680ce97d29b78101120cb9fae63c5a8ad` | 관련 gameplay spec이 완료되거나 freeze 검토를 시작할 때 |
| `MIG-AREA-004` | Spawn과 wave | `candidate` | [core-combat-lifecycle](cards/core-combat-lifecycle.md) | `partial` | `draft` | `blocked` | `MIG-AREA-002`, `MIG-AREA-003` | `MIG-DEC-CORE-005`, `MIG-DEC-CORE-009` | `2d35df0680ce97d29b78101120cb9fae63c5a8ad` | Spawn·wave 의미가 바뀌거나 첫 행동 시점 질문을 검토할 때 |
| `MIG-AREA-005` | Unit movement와 breach | `candidate` | [core-combat-lifecycle](cards/core-combat-lifecycle.md) | `partial` | `draft` | `blocked` | `MIG-AREA-002`, `MIG-AREA-003` | `MIG-DEC-CORE-004`, `MIG-DEC-CORE-010` | `2d35df0680ce97d29b78101120cb9fae63c5a8ad` | Movement·breach 의미가 바뀌거나 동시 사건 우선순위를 검토할 때 |
| `MIG-AREA-006` | Targeting과 attack | `candidate` | [core-combat-lifecycle](cards/core-combat-lifecycle.md) | `partial` | `draft` | `blocked` | `MIG-AREA-002`, `MIG-AREA-003`, `MIG-AREA-005` | `MIG-DEC-CORE-001`, `MIG-DEC-CORE-002`, `MIG-DEC-CORE-003`, `MIG-DEC-CORE-011` | `2d35df0680ce97d29b78101120cb9fae63c5a8ad` | Targeting·attack 의미가 바뀌거나 초기 readiness를 검토할 때 |
| `MIG-AREA-007` | Damage, heal과 death | `candidate` | [core-combat-lifecycle](cards/core-combat-lifecycle.md) | `partial` | `draft` | `blocked` | `MIG-AREA-002`, `MIG-AREA-006` | `MIG-DEC-CORE-004`, `MIG-DEC-CORE-010` | `2d35df0680ce97d29b78101120cb9fae63c5a8ad` | Damage·heal·death 의미가 바뀌거나 lethal 우선순위를 검토할 때 |
| `MIG-AREA-008` | Projectile, effect, status와 hazard | `candidate` | `none` | `none` | `draft` | `blocked` | `MIG-AREA-002`, `MIG-AREA-003`, `MIG-AREA-006`, `MIG-AREA-007` | `none` | `2d35df0680ce97d29b78101120cb9fae63c5a8ad` | 관련 gameplay spec이 완료되거나 freeze 검토를 시작할 때 |
| `MIG-AREA-009` | Placement, relocation과 facing | `candidate` | `none` | `none` | `draft` | `blocked` | `MIG-AREA-002`, `MIG-AREA-003`, `MIG-AREA-010` | `none` | `2d35df0680ce97d29b78101120cb9fae63c5a8ad` | 관련 gameplay spec이 완료되거나 freeze 검토를 시작할 때 |
| `MIG-AREA-010` | Resource, cost와 cooldown | `candidate` | `none` | `none` | `draft` | `blocked` | `MIG-AREA-002` | `none` | `2d35df0680ce97d29b78101120cb9fae63c5a8ad` | 관련 gameplay spec이 완료되거나 freeze 검토를 시작할 때 |
| `MIG-AREA-011` | Card와 skill | `candidate` | `none` | `none` | `draft` | `blocked` | `MIG-AREA-006`, `MIG-AREA-008`, `MIG-AREA-009`, `MIG-AREA-010`, `MIG-AREA-012` | `none` | `2d35df0680ce97d29b78101120cb9fae63c5a8ad` | 관련 gameplay spec이 완료되거나 freeze 검토를 시작할 때 |
| `MIG-AREA-012` | Mode와 content rule | `candidate` | [core-combat-lifecycle](cards/core-combat-lifecycle.md) | `partial` | `draft` | `blocked` | `none` | `MIG-DEC-CORE-006`, `MIG-DEC-CORE-008` | `2d35df0680ce97d29b78101120cb9fae63c5a8ad` | Mode별 terminal 의도를 검토하거나 content rule이 바뀔 때 |
| `MIG-AREA-013` | Score와 result | `candidate` | [core-combat-lifecycle](cards/core-combat-lifecycle.md) | `partial` | `draft` | `blocked` | `MIG-AREA-001`, `MIG-AREA-002`, `MIG-AREA-004`, `MIG-AREA-007`, `MIG-AREA-012` | `MIG-DEC-CORE-004`, `MIG-DEC-CORE-006`, `MIG-DEC-CORE-007`, `MIG-DEC-CORE-010`, `MIG-DEC-CORE-012` | `2d35df0680ce97d29b78101120cb9fae63c5a8ad` | Score·result 의미가 바뀌거나 전용 카드를 작성할 때 |

Freeze 후보가 되려면 `include` 영역의 카드가 모두 `coverage: complete`,
`review_status: reviewed`여야 한다. Readiness는 `ready`이거나 owner가 가정과 gap을
승인한 `conditional`이어야 한다. `stale` card가 하나라도 있거나 미결
`blocking_decisions`가 accepted gap으로 승인되지 않으면 freeze할 수 없다.
