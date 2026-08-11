# Shared Card — Unit Lifecycle Semantic Vocabulary

> `card_id`: `PT-SHARED-UL-CARD-001`
>
> 상태: **draft · partial · blocked · preparatory**
>
> 소비자: Client + Game Server

## Scope

`spawn → attack-started → damage → death → despawn` 한 경로에서 양쪽 소비자가 같은 사건을
식별하고 같은 순서로 해석하기 위한 의미만 정의한다. Wire DTO, transport, serializer,
Unity/ECS 타입과 frame timing은 정의하지 않는다.

## Stable identity

- `match_id`: 한 authoritative match를 식별한다.
- `actor_id`: match 안에서 actor를 안정적으로 식별하며 despawn 뒤 재사용하지 않는다.
- `action_id`: 하나의 accepted authoritative action과 그 결과를 연결한다.
- `event_id`: semantic event의 exactly-once 소비와 중복 제거 키다.
- `authoritative_sequence`: 한 match에서 확정 event의 전순서를 비교하는 단조 증가 값이다.

구체적인 비트 폭, 생성기와 wire 표현은 production ADR이 결정한다.

## Semantic events

| event | 필수 의미 | 제외 |
|---|---|---|
| `unit-spawned` | `actor_id`가 authoritative world에서 alive actor가 됨 | prefab 생성 시각 |
| `attack-started` | `action_id`의 공격이 권위 있게 시작됐고 source/target이 확정됨 | 애니메이션 clip/길이 |
| `damage-applied` | `action_id` 결과가 target 상태에 원자적으로 적용됨 | floating text, hit VFX |
| `unit-died` | lethal 결과로 actor가 더 이상 alive gameplay action을 할 수 없음 | ragdoll/죽음 연출 길이 |
| `unit-despawned` | actor의 authoritative runtime presence가 제거됨 | 화면 object pool 반환 시각 |

Result 의미는 event에 담되 numeric representation, packet grouping과 delivery 횟수는 정하지
않는다. Delivery duplicate가 있어도 같은 `event_id`는 같은 authoritative fact다.

## Ordering invariants

1. 한 actor의 `unit-spawned`는 그 actor를 참조하는 다른 pilot event보다 앞선다.
2. 한 action의 `attack-started`는 그 action의 `damage-applied`보다 앞선다.
3. Lethal `damage-applied`는 같은 결과의 `unit-died`보다 앞선다.
4. `unit-died`는 death reason의 `unit-despawned`보다 앞선다.
5. `unit-died` 뒤 해당 actor가 source인 새 authoritative action은 시작할 수 없다.

이 ordering은 semantic 순서다. 한 tick/packet/frame에 묶이는지는 정하지 않는다.
Pilot fixture의 공격 source는 slice 시작 전에 alive인 precondition이며, 나열된 sequence는
target actor 하나의 spawn부터 despawn까지를 검증한다.

## Resync와 replay

- Snapshot/resync는 현재 lifecycle state와 마지막 적용 sequence를 제공할 수 있어야 한다.
- Replay가 같은 event stream을 쓰든 compact record를 쓰든 위 fact와 ordering을 보존한다.
- Client가 gap을 발견하면 presentation을 추측으로 확정하지 않고 resync를 요청할 수 있어야 한다.

## Dependencies와 blockers

- Game Server authority coordination record: `PT-GS-UL-CARD-001` (Server-only, Shared
  delivery dependency가 아님)
- Client projection coordination record: `PT-CLIENT-UL-CARD-001` (Client-only, Shared
  delivery dependency가 아님)
- Fixture: [`../fixtures/unit-lifecycle/semantic-events.json`](../fixtures/unit-lifecycle/semantic-events.json)
- `PT-DEC-UL-001`: spawn 뒤 첫 action eligibility
- `PT-DEC-UL-002`: attack start와 damage resolution의 intended timing 의미
- `PT-DEC-UL-003`: lethal damage, death와 removal 사이에 허용되는 state change

Blocker가 `decided`되고 양쪽 tech owner가 이 exact revision/source를 area별로 검토하기 전에는
official include가 아니다.
