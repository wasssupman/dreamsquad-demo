# Game Server Rules — Time, Ordering, Numeric and RNG

## `PT-SRV-010` — Authoritative logical time

- **책임 owner:** SessionRuntime scheduler와 Simulation logical step.
- **Invariant:** Gameplay outcome은 Server logical time으로 결정하고 wall clock/render delta에 의존하지 않는다.
- **허용:** Host가 logical step을 schedule하되 Simulation에는 명시적 time input만 전달한다.
- **금지:** `Time.deltaTime`, wall clock, ambient timer 또는 Client timestamp로 결과 판정.
- **Semantic input/outcome:** Match time/tick, ordered intent와 timed state transition.
- **Production 제약:** Tick rate와 catch-up/overload policy는 production ADR에서 결정한다.
- **미결 decision:** `PT-DEC-SERVER-001`.
- **Demo source pointer:** Demo time domains는 관찰 대상이며 production clock으로 복사하지 않는다.

## `PT-SRV-011` — Total ordering과 tie-break

- **책임 owner:** SessionRuntime과 Simulation.
- **Invariant:** 같은 logical step의 command/event/terminal arbitration은 재현 가능한 total order를 가진다.
- **허용:** 명시적 sequence와 stable tie-break key를 사용한다.
- **금지:** Container iteration, thread scheduling, arrival race 또는 Client frame order에 의존한다.
- **Semantic input/outcome:** Ordered command batch → deterministic transition/event sequence.
- **Production 제약:** Parallelism은 observable order를 바꾸지 않아야 한다.
- **미결 decision:** `PT-DEC-SERVER-001`, `PT-DEC-SERVER-002`.
- **Demo source pointer:** Active gameplay specs의 update semantics를 freeze 시 규칙으로 재해석.

## `PT-SRV-012` — Numeric policy

- **책임 owner:** Game Server Simulation.
- **Invariant:** Damage, stat, movement, targeting, score의 representation/rounding/clamp가 명시적이고 반복 가능하다.
- **허용:** Product가 승인한 numeric test vector를 authoritative tests로 사용한다.
- **금지:** 플랫폼별 부동소수 차이나 암묵적 cast/rounding을 gameplay 규칙으로 둔다.
- **Semantic input/outcome:** Plain values + versioned rule → exact result and boundary behavior.
- **Production 제약:** 구체 representation은 production ADR 전까지 미정이다.
- **미결 decision:** `PT-DEC-SERVER-001`.
- **Demo source pointer:** Demo 순수 계산과 test contract를 의미 수준에서 계승.

## `PT-SRV-013` — Gameplay RNG

- **책임 owner:** Game Server Simulation.
- **Invariant:** Gameplay RNG stream, seed, draw order와 scope가 explicit하고 replay/audit 가능하다.
- **허용:** Presentation-only cosmetic RNG를 Client에서 별도 사용한다.
- **금지:** `Random.Shared`, ambient/global RNG, Client RNG 또는 cosmetic draw가 gameplay outcome에 영향.
- **Semantic input/outcome:** Pinned seed/stream state + ordered draw request → authoritative random result.
- **Production 제약:** Algorithm과 stream partition은 versioned production decision이다.
- **미결 decision:** `PT-DEC-SERVER-001`.
- **Demo source pointer:** Demo seeded generation은 완전한 server determinism 증거가 아니다.
