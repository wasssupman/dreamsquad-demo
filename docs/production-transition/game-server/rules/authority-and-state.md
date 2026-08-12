# Game Server Rules — Authority and State

## `PT-SRV-001` — Canonical ruleset과 상태

- **책임 owner:** Game Server Simulation.
- **Invariant:** Gameplay 결과에 영향을 주는 ruleset, state와 transition은 Server가 검증·실행한다.
- **허용:** Client preview/prediction에 plain read model을 제공한다.
- **금지:** Client-reported damage/state/score를 canonical result로 채택한다.
- **Semantic input/outcome:** Validated intent + pinned ruleset + state → atomic state/result/event.
- **Production 제약:** Simulation은 Unity와 infrastructure에 의존하지 않는다.
- **미결 decision:** `PT-DEC-SERVER-001`.
- **Demo source pointer:** `CLAUDE.md`의 Units/Movement/Combat/Effects 단일 쓰기 교훈만 의미 수준에서 계승.

## `PT-SRV-002` — Command validation과 atomic outcome

- **책임 owner:** SessionRuntime ordering/idempotency, Simulation gameplay validation.
- **Invariant:** Actor, state, ownership, permission, cost, cooldown, target와 sequence를 검증한 뒤 하나의 atomic outcome을 만든다.
- **허용:** 명시적 reject reason과 retry-safe receipt를 제공한다.
- **금지:** Validation과 state mutation 사이의 관찰 가능한 반쪽 상태, duplicate outcome.
- **Semantic input/outcome:** Intent → accept/reject/correct + canonical transition/event.
- **Production 제약:** Transport, deadline과 rate limit adapter는 Simulation 밖이다.
- **미결 decision:** `PT-DEC-COMMON-001`.
- **Demo source pointer:** Client-only Demo input flow는 rule intent를 찾는 source일 뿐 authority model이 아니다.

## `PT-SRV-003` — Match와 actor lifecycle

- **책임 owner:** Game Server Simulation과 SessionRuntime.
- **Invariant:** Match/actor/action/event lifecycle은 stable identity와 명시적 terminal state를 가진다.
- **허용:** Spawn, active, dead/breached, removed와 terminal을 별도 semantic fact로 보존한다.
- **금지:** Removed identity 재사용, dead actor의 새 action, terminal result 중복 확정.
- **Semantic input/outcome:** Match start, spawn/action/damage/death/breach/removal, terminal result.
- **Production 제약:** Storage class와 process lifecycle은 production architecture가 정한다.
- **미결 decision:** `PT-DEC-SERVER-002`.
- **Demo source pointer:** historical lifecycle pilot과 current gameplay specs를 freeze 시 reconcile.

## `PT-SRV-004` — Domain state ownership

- **책임 owner:** Production Server domain design.
- **Invariant:** 각 canonical state에는 단일 writer와 명시적 mutation path가 있다.
- **허용:** Domain event/command로 모듈 경계를 통과하고 read-only projection을 공유한다.
- **금지:** Demo ECS type 또는 queue를 그대로 domain boundary로 복사하거나 다중 writer를 허용한다.
- **Semantic input/outcome:** Domain command/event와 owned state transition.
- **Production 제약:** 구체 module/type은 Game Server roadmap/ADR이 결정한다.
- **미결 decision:** 없음.
- **Demo source pointer:** `docs/TRD.md`의 context ownership 교훈.
