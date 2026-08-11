# Game Server Card — Authoritative Unit Lifecycle

> `card_id`: `PT-GS-UL-CARD-001`
>
> 상태: **draft · partial · blocked · preparatory**
>
> 소비자: Game Server

## Scope

Server가 `spawn → attack-started → damage → death → despawn` 결과를 단일 authoritative
progression으로 만들 때 보존해야 할 규칙 후보와 공백을 기록한다. Demo ECS system order,
component, queue와 float representation을 production 설계로 복사하지 않는다.

## Canonical state 후보

```text
absent -> alive -> dead -> removed
```

- Spawn acceptance는 stable `actor_id`를 할당하고 `alive`를 만든다.
- Accepted attack는 stable `action_id`, source와 target을 확정한다.
- Damage result는 검증과 상태 변경을 하나의 authoritative outcome으로 기록한다.
- Lethal result는 actor를 `dead`로 만든다. `dead` actor는 새 gameplay action source가 아니다.
- Despawn은 runtime presence를 `removed`로 만들지만 death fact를 지우지 않는다.

이 상태 이름은 기술 중립 후보이며 storage class나 domain object 계층을 승인하지 않는다.

## Invariants

- 같은 match에서 `actor_id`, `action_id`, `event_id`는 재사용하지 않는다.
- Attack 결과는 accepted action 하나에 귀속되며 중복 command/delivery가 결과를 두 번 만들지 않는다.
- Damage validation과 applied result는 관찰 가능한 반쪽 상태 없이 원자적으로 확정된다.
- Death가 확정된 actor는 새 attack을 시작하거나 새 damage source가 될 수 없다.
- Despawn 뒤에도 match result/replay/audit가 death와 causal action을 식별할 수 있다.
- Server semantic emission은 Shared ordering을 만족한다.

## Logical validation 후보

| 단계 | 검증해야 할 의미 | 원자 결과 |
|---|---|---|
| spawn | actor definition, match scope, duplicate ID 없음 | alive actor + `unit-spawned` |
| attack start | source/target eligibility, action 중복 없음 | accepted action + `attack-started` |
| damage | action/result 귀속, target eligibility | state change + `damage-applied` |
| death | lethal 판정과 terminal action 금지 | dead state + `unit-died` |
| despawn | removal reason과 lifecycle state | removed state + `unit-despawned` |

구체적인 target/range/cooldown/damage formula는 이 pilot 범위가 아니며 승인된 gameplay card에
의존한다.

## Dependencies와 blockers

- Shared semantics: [`../../shared/cards/unit-lifecycle.md`](../../shared/cards/unit-lifecycle.md)
- Historical source pointer: Demo record `PT-LEGACY-GS-CORE-001` (provenance only;
  Server delivery dependency가 아님)
- `PT-DEC-UL-001`: 새 actor의 첫 action eligibility
- `PT-DEC-UL-002`: action start와 resolution timing
- `PT-DEC-UL-003`: death 확정 뒤 despawn 전 허용 상태 변경

이 card는 Demo가 관찰된 방식과 production 선택을 분리한다. 위 질문에 실제 Product owner
결정과 Game Server tech owner의 exact area review가 없으므로 현재 readiness는 blocked다.
