# Common Rules — Ordering, Resync and Versioning

## `PT-COM-010` — Authoritative ordering

- **책임 owner:** Game Server가 match-local authoritative sequence를 확정한다.
- **Invariant:** 같은 scope의 확정 fact는 비교 가능한 단조 증가 순서를 가지며 causal order를 뒤집지 않는다.
- **허용:** 여러 fact를 한 tick/packet/frame에 묶되 semantic order를 보존한다.
- **금지:** Client frame time, tween 완료 또는 delivery arrival order를 authoritative order로 사용한다.
- **Semantic input/outcome:** sequence, causal action/event ID와 terminal ordering.
- **Production 제약:** tick rate와 packet grouping은 이 규칙 밖이다.
- **미결 decision:** `PT-DEC-SERVER-001`.
- **Demo source pointer:** historical unit-lifecycle pilot의 spawn→action→damage→death→despawn ordering.

Lifecycle에 포함될 경우 최소 causal order는 다음과 같다.

1. Actor의 spawn fact가 그 actor를 참조하는 다른 event보다 먼저다.
2. 같은 action의 attack-started가 damage-applied보다 먼저다.
3. Lethal damage-applied가 unit-died보다 먼저다.
4. Unit-died가 death reason의 unit-despawned보다 먼저다.
5. Unit-died 뒤 그 actor를 source로 하는 새 authoritative action은 시작할 수 없다.

## `PT-COM-011` — Duplicate와 gap

- **책임 owner:** Server는 idempotent outcome identity를 제공하고 Client는 적용·cue 중복을 제거한다.
- **Invariant:** 같은 `event_id`는 같은 authoritative fact이며 두 번 적용되지 않는다.
- **허용:** Duplicate delivery를 조용히 무시하고 진단 정보를 남긴다.
- **금지:** Sequence gap이나 unknown identity를 Client 추측으로 확정한다.
- **Semantic input/outcome:** event identity, last applied sequence, gap detection과 resync request.
- **Production 제약:** Retry/backoff와 observability 방식은 production 구현이 정한다.
- **미결 decision:** `PT-DEC-COMMON-001`.
- **Demo source pointer:** `archive/legacy/client/cards/unit-lifecycle.md` historical correction rule.

## `PT-COM-012` — Snapshot, correction과 reconnect

- **책임 owner:** Game Server가 canonical resync state를 제공하고 Client가 projection을 수렴시킨다.
- **Invariant:** Snapshot/correction은 local pending/predicted/presentation history보다 우선한다.
- **허용:** 이미 표시한 cue의 재생 여부를 stable event identity와 Client policy로 판단한다.
- **금지:** Reconnect 뒤 animation 진행 상태로 actor/gameplay state를 복원한다.
- **Semantic input/outcome:** snapshot version, authoritative sequence, active identities와 pending intent disposition.
- **Production 제약:** Full/delta snapshot과 resume token 형식은 production ADR이 정한다.
- **미결 decision:** `PT-DEC-CLIENT-001`, `PT-DEC-SERVER-002`.
- **Demo source pointer:** Demo에는 authoritative reconnect가 없으므로 production-required 규칙으로 분류.

## `PT-COM-013` — Contract와 content version

- **책임 owner:** 양 tech owner가 semantic compatibility를 공동 관리하고 Server가 match ruleset을 pin한다.
- **Invariant:** 한 match의 canonical gameplay ruleset/content version은 도중에 암묵적으로 바뀌지 않는다.
- **허용:** Client presentation catalog가 stable gameplay/content ID를 local asset으로 매핑한다.
- **금지:** Client asset version이나 ScriptableObject를 canonical gameplay ruleset으로 취급한다.
- **Semantic input/outcome:** semantic contract version, ruleset/content version과 compatibility result.
- **Production 제약:** Version negotiation과 deployment policy는 production 저장소에서 결정한다.
- **미결 decision:** `PT-DEC-COMMON-001`.
- **Demo source pointer:** `docs/TRD.md`의 data-driven 원칙; Unity 저작 형태는 계승하지 않는다.
