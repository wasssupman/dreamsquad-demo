# Client Rules — Authority and Projection

## `PT-CLI-001` — Input feedback state

- **책임 owner:** Client.
- **Invariant:** 사용자 입력의 `pending`, `accepted`, `rejected`, `corrected` 표현을 canonical state와 분리한다.
- **허용:** 접수 즉시 reversible feedback을 제공하고 Server result로 확정·취소·정정한다.
- **금지:** Local feedback을 성공 결과로 고정하거나 rejected intent의 비용·효과를 유지한다.
- **Semantic input/outcome:** Intent ID, pending presentation, authoritative receipt와 correction reason.
- **Production 제약:** 행동별 latency/rollback UX는 Client Product gate에서 결정한다.
- **미결 decision:** `PT-DEC-CLIENT-001`.
- **Demo source pointer:** `demo-experience-map.md`의 input surface.

## `PT-CLI-002` — Projection 단일 진입점

- **책임 owner:** Client application/presentation boundary.
- **Invariant:** 모든 view는 authoritative projection 또는 Client-only cue를 소비하며 Server 결과를 직접 계산하지 않는다.
- **허용:** Snapshot, ordered event와 local presentation clock을 하나의 projector/controller에서 조정한다.
- **금지:** View별 network/state mutation, source별 gameplay branch와 animation callback 기반 판정.
- **Semantic input/outcome:** Common state/event/result → Client projection + presentation cue.
- **Production 제약:** 구체 class/interface는 Somnia Client architecture에 맞춰 구현한다.
- **미결 decision:** 없음.
- **Demo source pointer:** Demo `BattleBridge`는 관찰 seam일 뿐 production 타입으로 복사하지 않는다.

## `PT-CLI-003` — Duplicate, gap와 correction

- **책임 owner:** Client projection layer.
- **Invariant:** State와 one-shot cue 모두 stable event identity로 dedupe한다.
- **허용:** Gap/unknown identity에서 incremental 적용을 멈추고 resync를 요청한다.
- **금지:** 누락 sequence, HP, death, score나 actor lifecycle을 시각 상태로 추측한다.
- **Semantic input/outcome:** Last sequence, duplicate/gap classification, corrected snapshot과 pending disposition.
- **Production 제약:** Retry/reconnect 구현은 현지 infrastructure 경계를 따른다.
- **미결 decision:** `PT-DEC-CLIENT-001`, `PT-DEC-COMMON-001`.
- **Demo source pointer:** `archive/legacy/client/cards/unit-lifecycle.md` historical pilot.

## `PT-CLI-004` — Reconnect와 playback

- **책임 owner:** Client session/application layer.
- **Invariant:** Reconnect/resume/replay source도 같은 common semantic input으로 projection한다.
- **허용:** Live와 replay의 camera/UI timing이 달라도 authoritative result와 causal order는 보존한다.
- **금지:** 이전 view/pool/animation 상태를 canonical reconnect state로 사용한다.
- **Semantic input/outcome:** Session state, snapshot/resume boundary, replay sequence와 viewer policy.
- **Production 제약:** Replay 제품 범위와 visibility는 Product/Server decision을 따른다.
- **미결 decision:** `PT-DEC-SERVER-002`.
- **Demo source pointer:** Demo에는 online reconnect/replay 정본이 없으므로 production-required 규칙.
