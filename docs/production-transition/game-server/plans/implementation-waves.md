# Production Game Server Implementation Waves

> Official frozen input 검증과 별도 Game Server implementation activation 뒤에만 실행한다.

| Wave | Outcome | 핵심 작업 | Exit gate |
|---|---|---|---|
| `S0` Intake와 authority foundation | Imported rules를 Server roadmap/ADR에 매핑 | Manifest/receipt, included domain, stable IDs, ruleset/content decision | 현지 execution plan 승인 |
| `S1` Deterministic headless slice | Spawn-to-terminal 최소 authoritative Simulation | Logical step, actor lifecycle, attack/damage/death/breach, repeatability oracle | 같은 input의 exact repeated result |
| `S2` Command와 gameplay domain | Included player action과 전체 core rules | Validation/atomic outcome, map/wave, resources, effects/hazards/special | Domain coverage included row contract tests |
| `S3` Session과 replication | Ordered online session과 recovery | Dedup/order, semantic state/event, snapshot/resync/reconnect | Client와 authoritative vertical slice |
| `S4` Result와 integrity | 신뢰 가능한 경쟁 result | Score/terminal, persistence, rewards boundary, anti-cheat/audit | Result finality와 failure acceptance |
| `S5` Replay와 release readiness | 운영 가능한 production service | Authoritative record/replay, observability, load/fault/security/release | Production SLO와 release gates |

Phase 번호, public types, transport와 infrastructure dependency는 Somnia Game Server master roadmap과
각 wave의 별도 plan에서 결정한다. Transition 문서는 미래 phase 구현 권한이 아니다.
