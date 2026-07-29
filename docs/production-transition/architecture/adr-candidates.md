# ADR Candidates — 정규 프로젝트 결정 대기열

> 문서 상태: **Draft**
>
> 기준선: **2026-07-29 / `44c87885`**
>
> 후보 상태: 전 항목 **Candidate**
>
> 이 문서는 공식 ADR이 아니다. 승인 전 `docs/decisions/` 또는 공식 ADR 번호를 만들지 않는다.

## 운영 규칙

- 후보는 질문·동인·대안·의존성·결정에 필요한 증거를 관리한다. 이 문서의 대안 순서는 선호 순위가 아니다.
- 모든 후보의 `claim_kind`는 `decision`, `evidence_status`는 `untested`, `evidence_level`은 `E0`, `transfer_action`은 `decide`다.
- runtime·transport·database·hosting 제품명은 기술 spike와 운영 제약이 확보된 뒤 공식 ADR에서 비교한다.
- 승인된 ADR이 생기면 해당 후보 상태를 `Superseded`로 바꾸고 공식 문서를 `superseded_by`로 연결한다.
- 후보를 기각해도 삭제하지 않는다. 이유와 대체 후보를 기록한다.

## 후보 목록과 선행 관계

| 순서 | ID | 결정 주제 | 핵심 선행 입력 |
|---|---|---|---|
| 1 | `ADR-CAND-001` | Server host / runtime | 목표 동접, 지역, 세션 SLA, 팀 역량 |
| 2 | `ADR-CAND-002` | Non-ECS domain structure | 001의 process·concurrency 제약 |
| 3 | `ADR-CAND-003` | Tick / determinism / numeric policy | 제품 속도·정확도 요구 |
| 4 | `ADR-CAND-004` | Stable identity | 002의 수명 경계, 003의 ordering |
| 5 | `ADR-CAND-005` | Protocol / replication | 002~004 |
| 6 | `ADR-CAND-006` | Prediction / reconciliation | 003~005, latency 목표 |
| 7 | `ADR-CAND-007` | Content authority / version | 001·002·005 |
| 8 | `ADR-CAND-008` | Score authority / anti-cheat | 003·004·007 |
| 9 | `ADR-CAND-009` | Reconnect / terminal lifecycle | 001·004·005 |
| 10 | `ADR-CAND-010` | Online time control | 003·005·006 |
| 11 | `ADR-CAND-011` | Observability / replay | 003~005·007~009 |

이 순서는 문서 등록 순서다. 조사와 spike는 병렬로 진행할 수 있지만, 뒤 후보의 최종 승인은 선행 계약과 모순되지 않는지 확인해야 한다.

## ADR-CAND-001 — Server host / runtime

| field | value |
|---|---|
| `status` | `Candidate` |
| `claim_kind` | `decision` |
| `evidence_status` | `untested` |
| `evidence_level` | `E0` |
| `as_of` | `2026-07-29 / 44c87885`; 정규 프로젝트 서버 runtime·transport 미정 |
| `sources` | [demo baseline](../demo-baseline.md), [transition matrix](transition-matrix.md) |
| `related_commit_or_test` | 현 저장소에는 authoritative battle server의 부하·장애·비용 증거가 없음 |
| `transfer_action` | `decide` |
| `production_impact` | 배포 단위, session placement, 장애 격리, 확장 방식, 개발 언어와 운영 비용을 결정한다. |
| `next_validation_or_decision` | 제품 규모 가정과 1-session representative load spike를 확보한 뒤 공식 ADR 작성 |

**Question**

정규 프로젝트의 authoritative match를 어떤 host/runtime와 worker topology에서 실행할 것인가?

**Drivers**

- 목표 동시 match 수, 지역·지연 목표, match 최대 길이
- room/match 격리, crash 복구, rolling deployment와 version coexistence
- 팀의 개발·디버깅·운영 역량, 비용 예측 가능성
- game server orchestration, autoscaling, local integration test 난이도

**Options to compare**

- 전용 authoritative match process/worker + 별도 control plane
- 일반 application runtime의 actor/room worker
- managed game server hosting 또는 serverless/managed session 조합
- 위 선택들의 단계적 hybrid

**Dependencies**

- Product: 목표 동접, 지역, session SLA, 허용 대기시간
- Engineering: representative simulation CPU/memory profile, 배포·관측 요구
- 이 후보의 결론은 ADR-CAND-002·005·009·011의 제약이 된다.

**Decision evidence required**

- 대표 전투 slice 부하 측정, scale-out·crash injection, 배포·rollback rehearsal
- 월간 비용 모델과 on-call/운영 복잡도 비교
- 선택 runtime에서 fixed tick·reconnect·version pinning을 구현할 수 있다는 spike

## ADR-CAND-002 — Non-ECS domain structure

| field | value |
|---|---|
| `status` | `Candidate` |
| `claim_kind` | `decision` |
| `evidence_status` | `untested` |
| `evidence_level` | `E0` |
| `as_of` | `2026-07-29 / 44c87885`; 정규 프로젝트는 ECS를 사용하지 않음 |
| `sources` | [engineering learnings](engineering-learnings.md), [TRD](../../TRD.md), [`Scripts/Battle/`](../../../Assets/_Project/Scripts/Battle/) |
| `related_commit_or_test` | 데모의 맥락 분리·순수 계산 테스트만 존재하며 non-ECS server slice는 없음 |
| `transfer_action` | `decide` |
| `production_impact` | authoritative state의 모듈·aggregate·transaction·concurrency·test seam을 결정한다. |
| `next_validation_or_decision` | representative combat slice를 2개 이하 후보 구조로 구현·비교 후 공식 ADR 작성 |

**Question**

ECS 없이 Units·Movement·Combat·Effects의 책임과 단일 쓰기 소유권을 어떤 domain 구조로 구현할 것인가?

**Drivers**

- 명시적 state ownership과 cross-domain mutation 방지
- fixed tick 내 업데이트 순서와 transaction 경계
- match 단위 격리, 단위·통합 테스트 용이성
- 프로토콜·저장소·presentation으로부터 domain logic 격리
- 거대 `BattleBridge`와 거대 manager의 재발 방지

**Options to compare**

- match aggregate 내부의 명시적 domain modules
- actor/room + 내부 service/module 구성
- functional core + imperative shell
- object-oriented domain model + application command handlers

**Dependencies**

- ADR-CAND-001의 runtime·concurrency model
- ADR-CAND-003의 tick phase와 ADR-CAND-005의 protocol boundary

**Decision evidence required**

- spawn→move→target→damage→death의 representative slice
- cross-module write violation을 검출하는 test
- tick phase trace와 allocation/profile
- match 생성·종료·오류 rollback test

## ADR-CAND-003 — Tick / determinism / numeric policy

| field | value |
|---|---|
| `status` | `Candidate` |
| `claim_kind` | `decision` |
| `evidence_status` | `untested` |
| `evidence_level` | `E0` |
| `as_of` | `2026-07-29 / 44c87885`; 데모는 고정 wave seed와 일부 순수 계산을 갖지만 variable delta 전투 |
| `sources` | [map-wave-balancing](../../reference/map-wave-balancing.md), [sim-design](../../reference/lessons/04-sim-design.md), [`BattleScaledRateManager.cs`](../../../Assets/_Project/Scripts/Battle/BattleScaledRateManager.cs) |
| `related_commit_or_test` | [`MatchSeedTests.cs`](../../../Assets/_Project/Tests/EditMode/MatchSeedTests.cs), [`WavePatternGeneratorTests.cs`](../../../Assets/_Project/Tests/EditMode/WavePatternGeneratorTests.cs); 전체 battle replay test 없음 |
| `transfer_action` | `decide` |
| `production_impact` | authoritative simulation cadence, replay 가능성, cross-runtime 결과, 성능과 수치 안정성을 결정한다. |
| `next_validation_or_decision` | 목표 tick rate별 profile과 cross-run signature test 후 공식 ADR 작성 |

**Question**

서버 전투의 tick rate, update phase, RNG stream, tie-break, numeric type와 rounding을 어떤 결정론 계약으로 묶을 것인가?

**Drivers**

- 같은 command·content version·seed에서 같은 authoritative outcome
- 서버 비용과 조작 반응성의 균형
- replay·분쟁 조사·score 검증 가능성
- float divergence, iteration order, RNG 소비 순서 변화 방지

**Options to compare**

- fixed tick + integer/fixed-point simulation-critical values
- fixed tick + 엄격한 float/rounding·platform policy
- authoritative 결과만 보장하고 byte-identical replay는 제한하는 모델

**Dependencies**

- 제품이 요구하는 replay 수준과 허용 오차
- ADR-CAND-001 runtime, ADR-CAND-002 update 구조
- ADR-CAND-004 stable ordering key

**Decision evidence required**

- 장시간 동일 입력 반복 signature 비교
- 경계 tick·large delta·RNG stream 독립성 test
- target runtime 간 golden vector와 performance profile

## ADR-CAND-004 — Stable identity

| field | value |
|---|---|
| `status` | `Candidate` |
| `claim_kind` | `decision` |
| `evidence_status` | `untested` |
| `evidence_level` | `E0` |
| `as_of` | `2026-07-29 / 44c87885`; 데모 일부 tie-break는 `Entity.Index`·`Entity.Version` 사용 |
| `sources` | [`FrontmostTargeting.cs`](../../../Assets/_Project/Scripts/Battle/Combat/FrontmostTargeting.cs), [`LowestHealthTargeting.cs`](../../../Assets/_Project/Scripts/Battle/Combat/LowestHealthTargeting.cs), [transition matrix](transition-matrix.md) |
| `related_commit_or_test` | targeting EditMode tests는 있으나 protocol·reconnect·replay identity test는 없음 |
| `transfer_action` | `decide` |
| `production_impact` | entity 참조, tie-break, snapshot/delta, command target, replay와 관측 correlation의 기반이 된다. |
| `next_validation_or_decision` | 수명·범위·재사용 정책과 serialization test를 확정한 뒤 공식 ADR 작성 |

**Question**

match, player, squad slot, unit, projectile/effect 등 simulation object에 어떤 stable ID를 어떤 범위와 수명으로 발급할 것인가?

**Drivers**

- runtime-local handle에 의존하지 않는 total ordering
- snapshot/delta와 reconnect 후 동일 개체 매핑
- replay·audit log의 장기 참조 가능성
- payload 크기, 생성 비용, 보안상 추측 가능성

**Options to compare**

- match-local monotonic integer + match ID
- globally unique time/random ID
- object kind별 compound ID
- 네트워크 stable ID와 내부 handle을 분리하는 이중 체계

**Dependencies**

- ADR-CAND-002의 object lifecycle
- ADR-CAND-003의 ordering
- ADR-CAND-005·011의 wire/log schema

**Decision evidence required**

- create/destroy/reuse, late snapshot, reconnect, replay serialization test
- ID collision·wrap·spoofed target command test

## ADR-CAND-005 — Protocol / replication

| field | value |
|---|---|
| `status` | `Candidate` |
| `claim_kind` | `decision` |
| `evidence_status` | `untested` |
| `evidence_level` | `E0` |
| `as_of` | `2026-07-29 / 44c87885`; 데모 API는 match 전후 REST성 play/complete 중심, 전투 replication 없음 |
| `sources` | [tournament-play-report](../../spec/tournament-play-report/README.md), [tournament-flow-guards](../../spec/tournament-flow-guards/README.md), [`TournamentApi.cs`](../../../Assets/_Project/Scripts/Core/Api/TournamentApi.cs) |
| `related_commit_or_test` | API URL·DTO·pending guard 테스트는 있으나 실시간 protocol test는 없음 |
| `transfer_action` | `decide` |
| `production_impact` | command validation, snapshot/delta/event 배달, bandwidth, version compatibility와 정보 공개를 결정한다. |
| `next_validation_or_decision` | packet loss·duplicate·reorder·late join simulator를 포함한 protocol spike 후 공식 ADR 작성 |

**Question**

클라이언트 command와 서버의 snapshot/delta/event를 어떤 schema, cadence, reliability, ordering, versioning으로 교환할 것인가?

**Drivers**

- 서버 권위와 최소 지연
- command validation·rate limit·deduplication
- full snapshot 대비 delta bandwidth와 복구 복잡도
- protocol schema evolution, content version mismatch, 보안상 visibility

**Options to compare**

- 주기 snapshot + event/command
- baseline snapshot + state delta + reliable critical events
- event stream + 필요 시 snapshot
- transport별 reliability channel을 조합하는 모델

**Dependencies**

- ADR-CAND-001 runtime/transport 제약
- ADR-CAND-002 state model, 003 tick, 004 ID

**Decision evidence required**

- 대표 match bandwidth·serialization CPU 측정
- loss/reorder/duplicate/version mismatch/resync test
- 숨겨야 하는 state가 클라이언트에 노출되지 않는지 검증

## ADR-CAND-006 — Prediction / interpolation / reconciliation

| field | value |
|---|---|
| `status` | `Candidate` |
| `claim_kind` | `decision` |
| `evidence_status` | `untested` |
| `evidence_level` | `E0` |
| `as_of` | `2026-07-29 / 44c87885`; 데모는 로컬 권위라 network correction 경험 없음 |
| `sources` | [transition matrix](transition-matrix.md), [demo baseline](../demo-baseline.md) |
| `related_commit_or_test` | 현 저장소에 prediction·reconciliation 구현 및 테스트 없음 |
| `transfer_action` | `decide` |
| `production_impact` | 입력 반응성, 시각 안정성, correction 빈도, 구현 복잡도와 치트 표면을 결정한다. |
| `next_validation_or_decision` | 목표 지역 RTT·loss profile에서 조작감 prototype과 correction telemetry 확보 |

**Question**

어떤 상태와 행동을 예측하고, 무엇을 보간하며, 서버 결과와 불일치할 때 어떻게 reconciliation·resync할 것인가?

**Drivers**

- 배치·skill 입력의 즉각적 피드백
- 다수 유닛 이동·투사체의 bandwidth와 시각 안정성
- rollback 시 UX와 부작용 중복 방지
- hidden information과 치트 표면

**Options to compare**

- command acknowledgement만 예측하고 simulation은 보간
- 제한된 client prediction + server reconciliation
- 주요 local actor만 예측하고 나머지 interpolation
- 장르 특성상 prediction을 최소화하고 빠른 server response에 집중

**Dependencies**

- Product latency·correction 허용 기준
- ADR-CAND-003 tick, 004 ID, 005 replication

**Decision evidence required**

- 20/80/150ms RTT와 loss/jitter 시나리오 사용자 테스트
- correction rate·거리·중복 VFX/score event 검증
- forced resync 중 command 유실·중복 test

## ADR-CAND-007 — Content authority / version

| field | value |
|---|---|
| `status` | `Candidate` |
| `claim_kind` | `decision` |
| `evidence_status` | `untested` |
| `evidence_level` | `E0` |
| `as_of` | `2026-07-29 / 44c87885`; 데모는 ScriptableObject·Sheet import가 런타임 데이터 |
| `sources` | [map-wave-balancing](../../reference/map-wave-balancing.md), [score-formula](../../reference/score-formula.md), [dreamcatcher-portability](../../reference/dreamcatcher-portability.md) |
| `related_commit_or_test` | 에셋·생성기 테스트는 있으나 server/client version coexistence·rollback test는 없음 |
| `transfer_action` | `decide` |
| `production_impact` | 밸런스 정본, match 재현, client compatibility, 배포·rollback과 live operation의 안전성을 결정한다. |
| `next_validation_or_decision` | canonical schema와 version pinning pipeline prototype 후 공식 ADR 작성 |

**Question**

전투 콘텐츠의 canonical source, schema version, artifact hash, 배포·rollback과 match pinning을 어떻게 관리할 것인가?

**Drivers**

- 서버 판정과 클라이언트 표현의 동일 의미
- 진행 중 match가 배포 중간에 규칙을 바꾸지 않음
- 과거 replay·분쟁 조사 시 원래 config 복원
- schema migration과 구버전 client 정책

**Options to compare**

- versioned config artifact를 서버 배포와 함께 고정
- 별도 content service가 signed artifact 제공
- build-time generated shared schema + 환경별 manifest
- 서버 canonical config와 client presentation mapping을 분리

**Dependencies**

- ADR-CAND-001 배포 모델, 002 domain schema, 005 protocol version
- Product의 밸런스 배포 빈도와 긴급 rollback 요구

**Decision evidence required**

- version mismatch, mid-match deploy, rollback, old replay load test
- schema compatibility policy와 artifact 서명/hash 검증

## ADR-CAND-008 — Score authority / anti-cheat

| field | value |
|---|---|
| `status` | `Candidate` |
| `claim_kind` | `decision` |
| `evidence_status` | `untested` |
| `evidence_level` | `E0` |
| `as_of` | `2026-07-29 / 44c87885`; 데모는 클라이언트 `ScoreMath` 결과를 complete path parameter로 제출 |
| `sources` | [score-formula](../../reference/score-formula.md), [tournament-play-report](../../spec/tournament-play-report/README.md), [`ScoreMath.cs`](../../../Assets/_Project/Scripts/Core/ScoreMath.cs), [`TournamentApi.cs`](../../../Assets/_Project/Scripts/Core/Api/TournamentApi.cs) |
| `related_commit_or_test` | [`ScoreMathTests.cs`](../../../Assets/_Project/Tests/EditMode/ScoreMathTests.cs), API 전송 테스트. 서버 재계산·치트 대응 증거 없음 |
| `transfer_action` | `decide` |
| `production_impact` | 승패·랭킹 신뢰, score 산식 실행 위치, 무효 처리, 감사와 이의제기 절차를 결정한다. |
| `next_validation_or_decision` | server-owned result prototype과 tampered command/client payload test 후 공식 ADR 작성 |

**Question**

서버가 authoritative battle state에서 score를 어떻게 계산·확정·저장하고, 비정상 입력·client 변조·중복 종료를 어떻게 처리할 것인가?

**Drivers**

- 랭킹 공정성과 client payload 불신
- 산식·content version·match seed의 감사 가능성
- invalid command, impossible action rate, desync 처리
- 결과 저장의 멱등성과 동률·기권·무효 정책

**Options to compare**

- 서버 simulation이 종료 시 score를 직접 확정
- 서버 event ledger에서 score projection
- 서버 직접 확정 + 별도 비동기 fraud analysis

**Dependencies**

- ADR-CAND-003 determinism/numeric, 004 identity, 007 content version
- Product의 score·동률·기권·무효 규칙

**Decision evidence required**

- client score field를 무시해도 동일 UX가 성립하는 e2e
- tampered·duplicate·late terminal request와 impossible command test
- leaderboard write의 idempotency·audit query 검증

## ADR-CAND-009 — Reconnect / terminal lifecycle

| field | value |
|---|---|
| `status` | `Candidate` |
| `claim_kind` | `decision` |
| `evidence_status` | `untested` |
| `evidence_level` | `E0` |
| `as_of` | `2026-07-29 / 44c87885`; 데모 pending attempt 복구는 다음 로비에서 `complete(0)` 마감이며 battle resume이 아님 |
| `sources` | [tournament-flow-guards](../../spec/tournament-flow-guards/README.md), [`PendingMatchStore.cs`](../../../Assets/_Project/Scripts/Core/Api/PendingMatchStore.cs), [`TournamentMatchReporter.cs`](../../../Assets/_Project/Scripts/Core/Api/TournamentMatchReporter.cs) |
| `related_commit_or_test` | [`PendingMatchStoreTests.cs`](../../../Assets/_Project/Tests/EditMode/Api/PendingMatchStoreTests.cs), [`TournamentMatchReporterTests.cs`](../../../Assets/_Project/Tests/EditMode/Api/TournamentMatchReporterTests.cs); 실제 resume test 없음 |
| `transfer_action` | `decide` |
| `production_impact` | disconnect 허용 시간, 상태 보존, resume, abandon, terminal idempotency와 사용자 보상을 결정한다. |
| `next_validation_or_decision` | disconnect 위치별 fault-injection과 resume/timeout/duplicate terminal e2e 후 공식 ADR 작성 |

**Question**

연결이 끊긴 match를 얼마나 유지하고, 클라이언트가 어떤 token·snapshot으로 재부착하며, 종료·기권을 어떻게 멱등 처리할 것인가?

**Drivers**

- 모바일 background·네트워크 전환·앱 kill
- command ack 경계와 중복 전송
- server worker crash·migration 가능성
- 재접속 유예, 기권, 보상·악용 방지

**Options to compare**

- 짧은 grace 동안 동일 worker에 재부착
- durable snapshot/checkpoint에서 match 복구
- 단기 재부착 + 일정 시간 이후 authoritative abandon
- 장르·세션 길이에 따라 resume 없이 명시적 terminal 처리

**Dependencies**

- ADR-CAND-001 worker lifecycle
- ADR-CAND-004 identity, 005 protocol, 008 score terminal

**Decision evidence required**

- command 전송 전/후, snapshot 전/후, 결과 저장 전/후 disconnect matrix
- duplicate reconnect·terminal request·stale token·계정 변경 test
- worker crash와 rolling deploy 중 사용자 결과 검증

## ADR-CAND-010 — Online time control

| field | value |
|---|---|
| `status` | `Candidate` |
| `claim_kind` | `decision` |
| `evidence_status` | `untested` |
| `evidence_level` | `E0` |
| `as_of` | `2026-07-29 / 44c87885`; 데모 `TimeManager`는 로컬 Battle domain을 pause·slow motion |
| `sources` | [sim-design](../../reference/lessons/04-sim-design.md), [`TimeManager.cs`](../../../Assets/_Project/Scripts/Core/TimeControl/TimeManager.cs), [`BattleScaledRateManager.cs`](../../../Assets/_Project/Scripts/Battle/BattleScaledRateManager.cs) |
| `related_commit_or_test` | 로컬 시간 제어 기능 증거만 있으며 다중 사용자·server deadline 테스트 없음 |
| `transfer_action` | `decide` |
| `production_impact` | pause, drag slow motion, 선택 UI, deadline과 reconnect 중 simulation 진행 규칙을 결정한다. |
| `next_validation_or_decision` | 각 UX 흐름을 server clock에서 재정의하고 latency/reconnect 사용자 테스트 후 공식 ADR 작성 |

**Question**

로컬 pause·slow motion·선택 UI를 서버 authoritative clock과 어떤 규칙으로 조화시킬 것인가?

**Drivers**

- 한 플레이어의 UI가 공용/경쟁 simulation을 멈추지 않음
- 배치·skill 선택 중 조작 가능 시간과 접근성
- disconnect·background 악용 방지
- client 연출 clock과 server deadline의 일관된 표시

**Options to compare**

- 서버 tick 지속 + client presentation만 pause/slow
- 제한된 decision window를 서버가 부여
- 모든 command를 즉시 선택형으로 재설계
- game mode별 server pause 정책

**Dependencies**

- Product의 온라인 상호작용 모델
- ADR-CAND-003 tick, 005 protocol, 006 prediction, 009 reconnect

**Decision evidence required**

- drag/선택/튜토리얼/앱 background별 server timeline
- deadline edge와 late command 처리 test
- 지연 환경에서 남은 시간 표시와 사용자 이해 검증

## ADR-CAND-011 — Observability / replay

| field | value |
|---|---|
| `status` | `Candidate` |
| `claim_kind` | `decision` |
| `evidence_status` | `untested` |
| `evidence_level` | `E0` |
| `as_of` | `2026-07-29 / 44c87885`; 데모는 client `BattleLogger` JSON을 debug body로 전송 |
| `sources` | [tournament-play-report](../../spec/tournament-play-report/README.md), [`BattleLogSchema.cs`](../../../Assets/_Project/Scripts/Logging/BattleLogSchema.cs), [evidence policy](../evidence/README.md) |
| `related_commit_or_test` | client snapshot·API body 기능 증거만 있으며 authoritative replay·운영 incident 증거 없음 |
| `transfer_action` | `decide` |
| `production_impact` | 운영 탐지, 분쟁 조사, 밸런스 분석, 재현 능력, 저장 비용과 개인정보 위험을 결정한다. |
| `next_validation_or_decision` | incident reconstruction drill과 replay fidelity/cost 측정 후 공식 ADR 작성 |

**Question**

어떤 authoritative event·metric·trace를 어떤 correlation key와 보존 정책으로 남기며, replay를 어느 수준까지 보장할 것인가?

**Drivers**

- match·player·content version·server build의 end-to-end correlation
- desync, invalid command, disconnect, score dispute 조사
- Product KPI와 gameplay telemetry 분리
- 개인정보 최소화, 접근 통제, 보존 비용
- exact replay와 diagnostic reconstruction의 비용 차이

**Options to compare**

- authoritative command/event log + deterministic replay
- 주기 snapshot + command/event tail
- diagnostic audit trail만 유지하고 exact replay는 제한
- 전 match 기본 telemetry + 표본/신고 match 상세 기록

**Dependencies**

- ADR-CAND-003 determinism, 004 identity, 005 schema
- ADR-CAND-007 content artifact 보존, 008 score audit, 009 lifecycle
- Product/Data/보안의 보존·익명화 요구

**Decision evidence required**

- 대표 장애를 문서 없이 log만으로 재구성하는 drill
- replay outcome signature, schema migration, 누락·중복 event test
- match당 저장량·query 비용·보존 기간 모델
- 개인정보·token이 기록되지 않는 자동 검증

## 후보 상태 전이

```text
Candidate
  ├─> Accepted ADR ──> 이 후보는 Superseded + 공식 ADR 링크
  ├─> Rejected     ──> 사유와 대체 후보 기록
  └─> Deferred     ──> 재개 조건과 필요한 증거 기록
```

현재는 11개 모두 `Candidate`다. 선택된 runtime, transport, database, tick rate 또는 구현 패턴은 없다.
