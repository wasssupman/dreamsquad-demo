# Client Card — Unit Lifecycle Projection and Cues

> `card_id`: `PT-CLIENT-UL-CARD-001`
>
> 상태: **draft · partial · blocked · preparatory**
>
> 소비자: Client

## Scope

Shared lifecycle fact를 화면 object, animation과 cue로 투영하는 Client 책임을 정의한다.
Animation 완료나 local object 존재를 gameplay truth로 되먹이지 않는다. 실제 Unity 구현과
asset mapping은 이번 foundation 범위가 아니다.

## Projection contract

| authoritative fact | Client projection 책임 | 소유하지 않는 것 |
|---|---|---|
| `unit-spawned` | `actor_id` projection 생성/재사용, catalog lookup, spawn cue | spawn 승인과 ID 할당 |
| `attack-started` | action keyed playback, source/target cue 예약 | range/cooldown/target 판정 |
| `damage-applied` | 상태 projection 갱신, hit/number cue | damage formula와 lethal 판정 |
| `unit-died` | alive interaction 중단, death cue exactly once | death fact와 score/result |
| `unit-despawned` | projection 제거 또는 pool 반환 | authoritative removal timing |

Presentation은 event 사이를 interpolation할 수 있지만 새로운 authoritative fact를 만들거나
semantic ordering을 뒤집지 않는다.

## Duplicate, gap와 correction

- 같은 `event_id`의 재전달은 state와 cue를 두 번 적용하지 않는다.
- `authoritative_sequence` gap이나 알 수 없는 `actor_id/action_id`는 추측으로 메우지 않고
  resync 흐름으로 보낸다.
- Snapshot correction은 projection을 canonical lifecycle state로 수렴시킨다. 이미 보여준
  cue를 무조건 재생하지 않고 event identity/policy로 판정한다.
- Reconnect 뒤 `dead/removed` actor를 animation 진행 상태만 보고 alive로 복원하지 않는다.
- Local pending feedback가 있다면 accepted/rejected/corrected 상태를 authoritative result와
  분리해 표시한다.

## Cue acceptance 후보

- 정상 stream에서 각 semantic cue는 policy상 정확히 한 번 보인다.
- duplicate delivery는 cue count를 늘리지 않는다.
- gap/resync 뒤 최종 projection은 authoritative lifecycle state와 일치한다.
- pause/speed/frame rate 변화는 semantic ordering을 바꾸지 않는다.
- asset 누락은 진단 가능한 fallback presentation으로 끝나며 gameplay state를 바꾸지 않는다.

## Dependencies와 blockers

- Shared semantics: [`../../shared/cards/unit-lifecycle.md`](../../shared/cards/unit-lifecycle.md)
- Server authority coordination record: `PT-GS-UL-CARD-001` (Client delivery dependency가 아님)
- Shared fixture: [`../../shared/fixtures/unit-lifecycle/semantic-events.json`](../../shared/fixtures/unit-lifecycle/semantic-events.json)
- Client expectation: [`../fixtures/unit-lifecycle/expected-projection.json`](../fixtures/unit-lifecycle/expected-projection.json)
- `PT-DEC-UL-002`: attack/damage 의미 timing이 cue mapping에 미치는 영향
- `PT-DEC-UL-003`: death와 despawn 사이의 authoritative state

Client tech owner와 Shared 공동 review가 없으므로 현재 readiness는 blocked다. 이 문서는
Somnia Client runtime adoption이나 official input을 만들지 않는다.
