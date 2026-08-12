# Game Server Rules — Content, Result and Replay

## `PT-SRV-020` — Ruleset/content version

- **책임 owner:** Game Server canonical content pipeline.
- **Invariant:** Match는 검증된 gameplay ruleset/content version을 pin하고 끝까지 유지한다.
- **허용:** Stable ID로 Client presentation catalog와 느슨하게 결합한다.
- **금지:** Client ScriptableObject/prefab/catalog 값을 canonical gameplay data로 신뢰한다.
- **Semantic input/outcome:** Validated ruleset version + stable IDs → match configuration.
- **Production 제약:** Authoring/import/deploy storage는 production 결정이다.
- **미결 decision:** `PT-DEC-COMMON-001`.
- **Demo source pointer:** Demo data-driven authoring 의미만 계승.

## `PT-SRV-021` — Score와 terminal result

- **책임 owner:** Game Server Simulation/result domain.
- **Invariant:** Victory/defeat, terminal finality, score와 tie-break를 authoritative state에서 한 번 확정한다.
- **허용:** Client가 확정 result와 causal breakdown을 표시한다.
- **금지:** Client-computed score 제출, terminal 중복 확정 또는 확정 뒤 무규칙 state mutation.
- **Semantic input/outcome:** Final authoritative progression → signed/stored result and semantic breakdown.
- **Production 제약:** Persistence/reward/anti-cheat는 production scope에서 분리 설계한다.
- **미결 decision:** `PT-DEC-SERVER-002`.
- **Demo source pointer:** Current score/result specs를 freeze 시 포함 범위에 맞춰 reconcile.

## `PT-SRV-022` — Reconnect와 authoritative record

- **책임 owner:** Game Server SessionRuntime/result/observability boundary.
- **Invariant:** Reconnect, audit와 canonical replay가 Server 확정 progression을 기준으로 한다.
- **허용:** Snapshot/resume, compact authoritative record와 viewer-specific projection을 분리한다.
- **금지:** Client presentation trace를 match source of truth로 사용하거나 숨은 정보를 먼저 전달한다.
- **Semantic input/outcome:** Authoritative progression + viewer/visibility policy → resume/replay projection.
- **Production 제약:** Storage, retention, privacy와 spectator 제공 여부는 production 결정이다.
- **미결 decision:** `PT-DEC-SERVER-002`.
- **Demo source pointer:** Demo battle log는 진단 자료이지 authoritative record가 아니다.

## `PT-SRV-023` — Observability와 failure

- **책임 owner:** SessionRuntime/Host/operations; Simulation은 plain diagnostic fact만 노출한다.
- **Invariant:** Match/ruleset/command/event/result를 stable IDs로 상관관계화하고 failure가 canonical state를 모호하게 만들지 않는다.
- **허용:** Metrics, trace, audit와 Client telemetry를 목적별 artifact로 분리한다.
- **금지:** Logging side effect가 Simulation 결과·순서에 영향하거나 Client log를 분쟁 정본으로 사용한다.
- **Semantic input/outcome:** Correlated diagnostic facts, terminal reason와 receipt.
- **Production 제약:** Backend/tooling/retention은 operations ADR이 정한다.
- **미결 decision:** `PT-DEC-SERVER-002`.
- **Demo source pointer:** Demo logger의 계측 교훈만 계승.
