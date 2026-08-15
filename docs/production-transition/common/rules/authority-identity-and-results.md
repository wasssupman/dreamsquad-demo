# Common Rules — Authority, Identity and Results

## `PT-COM-001` — 결과 권위

- **책임 owner:** Game Server가 gameplay 결과를 확정하고 Client는 그 결과를 투영한다.
- **Invariant:** Client 입력·prediction·animation·local state는 canonical gameplay state가 아니다.
- **허용:** Client가 즉시 비권위 feedback을 표시하고 Server 결과로 수렴한다.
- **금지:** Client가 damage, death, score, terminal, cost 또는 cooldown 결과를 확정해 제출한다.
- **Semantic input/outcome:** intent → accepted/rejected/corrected result → authoritative state/event.
- **Production 제약:** 실제 command/API/wire는 production ADR이 정한다.
- **미결 decision:** `PT-DEC-COMMON-001`.
- **Demo source pointer:** `CLAUDE.md`, `docs/TRD.md`; single-player authority shape는 계승하지 않는다.

## `PT-COM-002` — Stable identity

- **책임 owner:** Game Server가 match scope의 canonical identity를 발급·보존한다.
- **Invariant:** `match_id`, `actor_id`, `action_id`, `event_id`는 정의된 scope 안에서 재사용하지 않는다.
- **허용:** Client가 stable ID를 local view/catalog handle에 매핑한다.
- **금지:** Unity `Entity`, instance ID, array index, pointer 또는 pool slot을 외부 identity로 사용한다.
- **Semantic input/outcome:** identity는 command, result, snapshot, event와 audit 상관관계를 연결한다.
- **Production 제약:** bit width, generator와 wire representation은 production 결정이다.
- **미결 decision:** 없음.
- **Demo source pointer:** `archive/legacy/shared/cards/unit-lifecycle.md`의 historical pilot 의미.

## `PT-COM-003` — Intent와 receipt

- **책임 owner:** Client는 행동 의도만 만들고 Game Server는 actor/state/ownership/permission과
  gameplay precondition을 검증한다.
- **Invariant:** 같은 logical intent의 retry/duplicate가 authoritative outcome을 중복 생성하지 않는다.
- **허용:** accepted, rejected와 corrected outcome을 명시적으로 구분한다.
- **금지:** Client가 canonical state delta나 계산된 최종 결과를 command로 보낸다.
- **Semantic input/outcome:** intent ID, actor/target reference, accept/reject/correct reason, causal outcome ID.
- **Production 제약:** deadline, rate limit과 idempotency window는 production protocol에서 결정한다.
- **미결 decision:** `PT-DEC-COMMON-001`.
- **Demo source pointer:** 관찰 가능한 Demo 입력은 `client/demo-experience-map.md`에서 freeze 시 reconcile.

## `PT-COM-004` — Authoritative state와 semantic event

- **책임 owner:** Game Server가 canonical state와 gameplay semantic event를 생성한다.
- **Invariant:** Event는 확정된 gameplay fact이며 asset/VFX/animation 명령이 아니다.
- **허용:** Client가 하나의 event를 여러 presentation cue로 해석한다.
- **금지:** Presentation 완료 callback이 다음 authoritative state transition을 발생시킨다.
- **Semantic input/outcome:** versioned snapshot/state delta와 causal semantic event/result.
- **Production 제약:** Snapshot/delta/event transport 선택은 production ADR이 정한다.
- **미결 decision:** `PT-DEC-COMMON-001`.
- **Demo source pointer:** `archive/legacy/architecture/transition-matrix.md`의 historical authority seam.
