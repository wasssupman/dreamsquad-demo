# ADR Candidates — 정규 프로젝트 결정 대기열

> 문서 상태: **Historical · stale · preparatory**
>
> 기준선: **2026-07-29 / `44c87885`**
>
> 후보 상태: 전 항목 **Candidate**
>
> 이 문서는 공식 ADR이 아니다. 승인 전 `docs/decisions/` 또는 공식 ADR 번호를 만들지 않는다.

> 후보는 미래 freeze의 reference로 이동할 수 있지만 production ADR 승인이나 live protocol
> 정본이 아니다. 각 후보는 current source와 owner review를 다시 거쳐야 한다.

## 운영 규칙

- 후보는 질문·동인·대안·의존성·결정에 필요한 증거를 관리한다. 이 문서의 대안 순서는 선호 순위가 아니다.
- 모든 후보의 `claim_kind`는 `decision`, `evidence_status`는 `untested`, `evidence_level`은 `E0`, `transfer_action`은 `decide`다.
- runtime·transport·database·hosting 제품명은 기술 spike와 운영 제약이 확보된 뒤 공식 ADR에서 비교한다.
- 승인된 ADR이 생기면 해당 후보 상태를 `Superseded`로 바꾸고 공식 문서를 `superseded_by`로 연결한다.
- 후보를 기각해도 삭제하지 않는다. 이유와 대체 후보를 기록한다.

## 후보가 변경하지 않는 고정 책임 경계

이 경계는 2026-07-30 정규 프로젝트 방향 결정이며, 아래 후보가 다시 선택하거나 완화할 대상이 아니다.

- Product/Game Design은 규칙 의도·밸런스·콘텐츠 의미를 작성·승인하지만 런타임 판정 권위를 갖지 않는다.
- 서버는 게임 결과에 영향을 주는 gameplay ruleset·상태·상태 전이의 정본과 실행 권위를 갖는다.
- 클라이언트는 입력 UX와 시각·청각·촉각 presentation을 소유하고, server semantic outcome을 local asset으로 표현한다.
- protocol은 intent-only command와 stable ID·authoritative tick 기반 semantic state/outcome을 교환하며 Unity asset·연출 지시를 전달하지 않는다.
- 제한적 Client prediction은 비권위·폐기 가능해야 하고 server correction·resync가 항상 우선한다.
- Authoritative Match Record는 서버가 확정한 progression의 유일한 경기 source of truth인 논리적 기록 계약이며 Client prediction·delivery timing·presentation history를 포함하지 않는다.
- Replay와 조건부 Spectator는 Authoritative Match Record에서 서버가 viewer role·관점·visibility·delay policy를 적용해 만든 semantic projection을 소비한다. Client는 숨은 정보를 받은 뒤 표시만 억제하지 않는다.
- canonical Replay는 authoritative progression과 viewer별 관찰 의미론을 재현하며 당시 Live player 화면의 pixel/frame 동일성을 보장하지 않는다.

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
| 11 | `ADR-CAND-011` | Authoritative Match Record / observability | 003~005·007~009 |
| 12 | `ADR-CAND-012` | Replay playback / viewer projection | 003~007·011, Product viewer policy |

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
| `production_impact` | authoritative gameplay ruleset과 state의 모듈·aggregate·transaction·concurrency·test seam을 결정하되 Client presentation과의 의존은 차단한다. |
| `next_validation_or_decision` | representative combat slice를 2개 이하 후보 구조로 구현·비교 후 공식 ADR 작성 |

**Question**

ECS 없이 Units·Movement·Combat·Effects의 책임과 단일 쓰기 소유권을 어떤 domain 구조로 구현할 것인가?

**Fixed constraints**

- 서버 domain은 명령 유효성, 비용·쿨다운, 타게팅, 효과·피해, 웨이브·스폰, gameplay clock·RNG, 승패·점수·보상을 포함한 결과 영향 규칙과 상태 전이를 단독으로 실행한다.
- Product/Game Design의 authoring·approval과 서버 runtime authority를 구분한다.
- 서버 domain은 Unity, prefab, animation, VFX, SFX, camera, haptics 등 Client presentation 타입과 callback에 의존하지 않는다.

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
- server domain이 Client presentation assembly·schema 없이 실행되는 dependency test
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
| `production_impact` | authoritative simulation cadence, progression signature 재현 가능성, cross-runtime 결과, 성능과 수치 안정성을 결정한다. |
| `next_validation_or_decision` | 목표 tick rate별 profile과 cross-run signature test 후 공식 ADR 작성 |

**Question**

서버 전투의 tick rate, update phase, RNG stream, tie-break, numeric type와 rounding을 어떤 결정론 계약으로 묶을 것인가?

**Fixed constraints**

- server tick과 gameplay RNG stream이 authoritative하며 Client clock·seed·RNG 결과를 판정 입력으로 신뢰하지 않는다.
- Client cosmetic RNG는 telegraph 의미·가시성, 판정·충돌 시점, event 수, command 가능 여부, 대상 선택, 점수에 영향을 줄 수 없다.
- gameplay-relevant telegraph는 서버 semantic state/event에서 파생하며 presentation clock·callback은 gameplay state를 진행하지 않는다.
- authoritative progression signature는 accepted command 순서, authoritative tick·stable ID·상태 전이·gameplay RNG 결과·승패·점수를 검증한다. network delivery history, Client prediction·correction, camera와 frame/pixel 결과는 signature에서 제외한다.
- `simulation_fidelity`는 고정 요구지만 byte-identical record 저장 또는 cross-runtime command 재시뮬레이션 여부는 ADR-CAND-011에서 비교한다.

**Drivers**

- 같은 command·ruleset version·seed에서 같은 authoritative outcome
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
- cosmetic RNG seed·presentation frame rate를 바꿔도 authoritative signature가 변하지 않는 격리 test
- 동일 authoritative progression이 서로 다른 network arrival·Client presentation 조건에서도 같은 signature로 수렴하는 test
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
| `production_impact` | intent-only command validation, semantic snapshot/delta/event 배달, bandwidth, version compatibility와 정보 공개를 결정한다. |
| `next_validation_or_decision` | packet loss·duplicate·reorder·late join simulator를 포함한 protocol spike 후 공식 ADR 작성 |

**Question**

클라이언트 command와 서버의 snapshot/delta/event를 어떤 schema, cadence, reliability, ordering, versioning으로 교환할 것인가?

**Fixed constraints**

- Client command는 행동 의도, stable target/reference와 전송·dedupe에 필요한 식별자만 보낸다. damage, state delta, score, authoritative outcome이나 판정 시각 override를 제출하지 않는다.
- 서버는 actor·state·ownership·permission·cost·cooldown·target·sequence·deadline·rate를 검증한 뒤 ruleset으로 결과를 계산한다.
- snapshot/delta/event는 stable gameplay ID·authoritative tick·semantic state/outcome을 표현한다. prefab·animation·VFX·tween 같은 Unity asset 또는 연출 지시를 protocol에 넣지 않는다.
- viewer용 protocol envelope는 canonical authoritative event를 참조하고 viewer role·viewpoint·visibility·delay policy의 version을 식별한다. 서버가 policy를 적용한 뒤 전송하며 Client-side display suppression을 정보 보호 경계로 사용하지 않는다.

**Drivers**

- 서버 권위와 최소 지연
- command validation·rate limit·deduplication
- full snapshot 대비 delta bandwidth와 복구 복잡도
- protocol schema evolution, ruleset/presentation version mismatch, 보안상 visibility

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
- 변조된 damage·state delta·score·timing 필드를 거절하거나 무시하고 서버 결과만 사용하는 test
- loss/reorder/duplicate/version mismatch/resync test
- wire schema가 Client presentation 타입·asset reference를 포함하지 않는 자동 검증
- viewer role·visibility·delay policy별로 숨겨야 하는 state가 클라이언트에 노출되지 않는지 검증

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
| `production_impact` | 서버 권위를 바꾸지 않는 범위에서 입력 반응성, 시각 안정성, correction 빈도, 구현 복잡도와 치트 표면을 결정한다. |
| `next_validation_or_decision` | 목표 지역 RTT·loss profile에서 조작감 prototype과 correction telemetry 확보 |

**Question**

어떤 상태와 행동을 예측하고, 무엇을 보간하며, 서버 결과와 불일치할 때 어떻게 reconciliation·resync할 것인가?

**Fixed constraints**

- prediction은 제한적·비권위·폐기 가능하며 accepted damage·state transition·score 등 authoritative outcome을 만들 수 없다.
- 서버 snapshot·correction·resync가 항상 우선하고 Client predicted state는 확정 근거로 사용하지 않는다.
- 예측 연출은 취소·병합 가능해야 하며 authoritative semantic event ID를 기준으로 VFX·SFX·UI·analytics 부작용을 중복 제거한다.
- predicted state/event와 reversal·network arrival history는 Authoritative Match Record와 canonical Replay에 포함하지 않는다. 당시 사용자 화면 조사가 필요하면 권위 기록과 분리된 `as-seen presentation trace`로만 취급한다.

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
- 예측 결과를 변조해도 서버 상태·점수에 영향이 없고 확정 event 부작용이 한 번만 발생하는 test
- prediction 성공·정정·거절 모두에서 predicted/reversal event가 canonical record·Replay에 섞이지 않고 최종 authoritative signature가 일치하는 test
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
| `production_impact` | server canonical gameplay ruleset과 Client presentation catalog의 고정 소유권 분리 위에서 schema, version pinning, 배포·rollback과 compatibility 정책을 결정한다. |
| `next_validation_or_decision` | canonical schema와 version pinning pipeline prototype 후 공식 ADR 작성 |

**Question**

분리된 server gameplay ruleset과 Client presentation catalog의 schema version, artifact hash, 배포·rollback, compatibility와 match pinning을 어떻게 관리할 것인가?

**Fixed constraints**

- server canonical gameplay ruleset은 stable gameplay ID, 수치·산식, 비용·쿨다운, 타게팅·효과 의미, 웨이브·스폰, timer, gameplay RNG parameter, 승패·점수·보상 입력의 정본이다.
- Client presentation catalog는 stable gameplay ID를 prefab·animation·VFX·SFX·UI style/layout·localization·camera·haptics·accessibility·cosmetic asset에 매핑하며 gameplay outcome을 바꿀 수 없다.
- 두 artifact는 stable ID와 명시적 호환 version/hash로 연결하고 match에서 사용한 ruleset version을 고정한다.
- Authoritative Match Record에는 recording schema와 match-pinned ruleset version/hash를, Replay presentation에는 사용한 presentation catalog version/hash를 각각 기록한다. 과거 catalog를 보존할지 호환 catalog로 재표현할지는 선택 대상이지만 semantic outcome을 변경할 수 없다.

**Drivers**

- 서버 semantic outcome과 클라이언트 표현 mapping의 호환성
- 진행 중 match가 배포 중간에 규칙을 바꾸지 않음
- 과거 replay·분쟁 조사 시 원래 config 복원
- schema migration과 구버전 client 정책

**Options to compare**

- versioned ruleset을 서버 배포와 함께 고정하고 presentation catalog는 Client build에 포함
- 별도 content service가 signed ruleset/catalog artifact 제공
- build-time generated stable ID/schema + 환경별 compatibility manifest
- ruleset과 presentation catalog를 독립 배포하고 호환 manifest로 조합

**Dependencies**

- ADR-CAND-001 배포 모델, 002 domain schema, 005 protocol version
- Product의 밸런스 배포 빈도, Client presentation 배포 주기와 긴급 rollback 요구

**Decision evidence required**

- version mismatch, mid-match deploy, rollback, old replay load test
- schema compatibility policy와 artifact 서명/hash 검증
- presentation catalog를 바꾸거나 누락시켜도 server authoritative outcome signature가 변하지 않는 test
- stable gameplay ID mapping의 completeness와 incompatible version 차단 test
- 과거 Replay를 다른 catalog로 열 때 `normal`·`degraded`·`rejected` 호환 상태를 구분하고 핵심 semantic cue의 보존 여부를 검증

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
- 산식·ruleset version·match seed의 감사 가능성
- invalid command, impossible action rate, desync 처리
- 결과 저장의 멱등성과 동률·기권·무효 정책

**Options to compare**

- 서버 simulation이 종료 시 score를 직접 확정
- 서버 event ledger에서 score projection
- 서버 직접 확정 + 별도 비동기 fraud analysis

**Dependencies**

- ADR-CAND-003 determinism/numeric, 004 identity, 007 ruleset version
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

## ADR-CAND-011 — Authoritative Match Record / observability

| field | value |
|---|---|
| `status` | `Candidate` |
| `claim_kind` | `decision` |
| `evidence_status` | `untested` |
| `evidence_level` | `E0` |
| `as_of` | `2026-07-30`; 데모 기준선은 `2026-07-29 / 44c87885`이며 client `BattleLogger` JSON을 debug body로 전송 |
| `sources` | [tournament-play-report](../../spec/tournament-play-report/README.md), [`BattleLogSchema.cs`](../../../Assets/_Project/Scripts/Logging/BattleLogSchema.cs), [evidence policy](../evidence/README.md) |
| `related_commit_or_test` | client snapshot·API body 기능 증거만 있으며 Authoritative Match Record·운영 incident reconstruction 증거 없음 |
| `transfer_action` | `decide` |
| `production_impact` | 경기 source of truth인 Authoritative Match Record의 capture·저장·checkpoint·무결성·보존과 audit·metric·trace·Client presentation artifact의 correlation을 결정한다. |
| `next_validation_or_decision` | record 후보별 progression signature, incident reconstruction, 저장·조회 비용과 개인정보 검증 후 공식 ADR 작성 |

**Question**

서버가 확정한 경기 progression을 어떤 Authoritative Match Record로 capture·저장·검증하고, 운영 telemetry와 어떤 correlation·보존 정책으로 연결할 것인가?

**Fixed constraints**

- Authoritative Match Record는 match·server build·recording schema·ruleset version/hash와 서버가 확정한 tick·상태 전이·semantic event·stable ID·gameplay RNG 결과·승패·점수를 직접 저장하거나 결정론적으로 재구성·검증할 수 있는 유일한 논리적 경기 source of truth다.
- Client prediction·correction·network arrival timing·camera·UI·VFX/SFX와 `as-seen presentation trace`는 Authoritative Match Record에 포함하지 않는다. invalid/rejected command audit도 canonical progression과 구분한다.
- command/RNG 재시뮬레이션, checkpoint+event/delta playback, hybrid 중 저장 방식은 미정이다. 어느 방식이든 동일 progression signature를 산출하고 누락·중복·변조와 첫 divergent authoritative event를 식별해야 한다.
- Client telemetry에는 presentation version/hash와 선택적 input/command → authoritative event/tick → viewer projection event → presentation event correlation을 남기되 권위 판정 근거로 사용하지 않는다. timer·AI·RNG·spawn처럼 Client command가 없는 event도 독립적으로 식별한다.
- 개인정보·인증 token 원문을 기록하지 않으며 Authoritative Match Record, audit, metric, trace, Product telemetry와 Client diagnostic trace의 목적·접근 권한·보존을 구분한다.

**Drivers**

- authoritative progression의 재현 가능성과 무결성
- match·player·ruleset version·presentation version·server build의 end-to-end correlation
- desync, invalid command, disconnect, score dispute 조사
- Product KPI와 gameplay telemetry 분리
- 개인정보 최소화, 접근 통제, 보존 비용
- 전 match 기본 기록과 상세 진단 artifact의 비용 차이

**Options to compare**

- accepted command·RNG 입력을 저장하고 deterministic server simulation으로 재구성
- 주기 authoritative checkpoint + semantic event/delta tail을 저장하고 직접 playback
- command/RNG와 checkpoint/event를 함께 보존하는 hybrid
- 전 match canonical record + 표본·신고·오류 match에만 상세 audit/trace를 추가하는 tiered retention

**Dependencies**

- ADR-CAND-003 determinism, 004 identity, 005 schema
- ADR-CAND-007 content artifact 보존, 008 score audit, 009 lifecycle
- Product/Data/보안의 보존·익명화 요구

**Decision evidence required**

- 각 저장 후보가 동일 authoritative progression signature를 재현하는 cross-run·cross-version test
- schema migration, checkpoint 경계, 누락·중복·변조·순서 오류와 첫 divergence 탐지 test
- 대표 장애를 문서 없이 record·audit·trace만으로 재구성하는 drill
- 선택적 input/command부터 authoritative event/tick, viewer projection event와 실제 presentation event까지 correlation completeness test
- match당 저장량·query 비용·보존 기간 모델
- 개인정보·token이 기록되지 않는 자동 검증

## ADR-CAND-012 — Replay playback / viewer projection

| field | value |
|---|---|
| `status` | `Candidate` |
| `claim_kind` | `decision` |
| `evidence_status` | `untested` |
| `evidence_level` | `E0` |
| `as_of` | `2026-07-30`; 정규 프로젝트 Replay는 확정 요구이며 Spectator 제공 여부는 미정 |
| `sources` | [ENG-011](engineering-learnings.md), [transition matrix](transition-matrix.md), [PRD inputs](../product/prd-inputs.md), [validation backlog](../product/validation-backlog.md) |
| `related_commit_or_test` | 현 저장소에 Authoritative Match Record·Replay·Spectator projection 구현 및 테스트 없음 |
| `transfer_action` | `decide` |
| `production_impact` | Authoritative Match Record를 viewer role·관점·visibility·delay policy에 맞는 read-only semantic stream으로 투영하고 Replay playback 및 조건부 Spectator에서 표현하는 계약을 결정한다. |
| `next_validation_or_decision` | 첫 server-authoritative slice에서 Live player·Replay semantic parity와 viewer 이해를 검증하고, Spectator 채택 시 live/delayed projection test 후 공식 ADR 작성 |

**Question**

Authoritative Match Record에서 어떤 viewer projection을 만들고, Replay의 관점·공개 시점·playback 동작·과거 version 호환과 조건부 Spectator 정책을 어떻게 정의할 것인가?

**Fixed constraints**

- canonical Replay는 confirmed Server progression을 재생한다. 당시 Live player의 prediction·reversal·network arrival timing·camera·cosmetic timing을 재연하지 않으며, 실제 사용자 화면이 필요하면 영상이나 별도 `as-seen presentation trace`를 사용한다.
- “Player POV replay” 대신 `player-visible authoritative perspective`를 사용한다. 이는 해당 role·관점에서 볼 수 있었던 authoritative semantic progression이지 실제 화면의 pixel/frame 복제가 아니다.
- `simulation_fidelity`는 authoritative tick 순서·상태 전이·stable ID·gameplay RNG 결과·승패·점수의 일치를 요구한다.
- `observation_fidelity`는 같은 viewer role과 policy에서 관찰 가능한 event의 누락·추가·순서 오류가 없고 숨은 정보가 노출되지 않을 것을 요구한다.
- `presentation_fidelity`는 핵심 단서와 인과관계를 이해할 수 있어야 하지만 pixel/frame 동일성은 요구하지 않는다.
- 서버는 viewer 권한·visibility·delay를 projection 전에 적용한다. projection은 stable ID 기반 semantic state/event만 전달하며 prefab·animation·VFX·tween 지시를 포함하지 않는다.
- Replay pause·speed·seek·rewind는 Client playback/presentation clock과 비권위 replay cursor/read model을 변경할 수 있지만 Server authoritative state나 원 경기 결과를 변경하지 않는다. 원 경기의 score·reward·achievement·gameplay analytics 같은 비가역 부작용은 재발행하지 않고, Replay 조작 telemetry는 별도 관찰 event로 기록한다.
- Spectator가 채택되면 Replay와 같은 projection 계약의 live/delayed tail을 read-only로 소비한다. 접근 권한·delay·late join·anti-ghosting은 서버가 집행하며, 동일 match·policy의 완료 stream은 Replay와 같은 authoritative semantic sequence에 수렴한다.

**Drivers**

- 같은 경기와 결과라는 신뢰를 유지하면서 Live prediction과 Replay presentation의 차이를 설명할 수 있음
- viewer 관점별 hidden information·공정성·privacy 보호
- pause·seek·rewind·배속과 event 부작용 중복 방지
- old ruleset·recording schema·presentation catalog의 장기 호환
- Spectator 도입 시 stream-sniping·late join·비용·지연 trade-off

**Options to compare**

- player-visible authoritative perspective를 기본 Replay로 제공
- match 종료 후 승인된 omniscient perspective를 제공
- Product가 정한 복수의 versioned viewpoint/visibility policy 중 권한에 맞는 projection 제공
- Spectator를 제공하지 않거나, 동일 projection의 live tail 또는 server-delayed tail로 제공

**Dependencies**

- Product의 Replay 핵심 단서·관점·공개 시점과 Spectator 제공 여부
- ADR-CAND-003 progression signature, 004 stable identity, 005 projection protocol, 006 prediction boundary
- ADR-CAND-007 ruleset/catalog compatibility, 011 Authoritative Match Record

**Decision evidence required**

- prediction 성공·정정·거절 모두에서 Replay 최종 상태·승패·점수가 Authoritative Match Record signature와 일치하는 test
- 같은 viewer role·policy의 event 누락·추가·순서와 hidden information 비노출 test
- pause·seek·rewind·배속 반복 시 VFX 누적과 원 경기 score·reward·achievement·gameplay
  analytics 재발행이 없고, Replay-control telemetry는 조작 단위로 별도 기록되는 test
- 과거 ruleset·recording schema와 다른 presentation catalog 조합의 `normal`·`degraded`·`rejected` 호환 test
- Spectator 채택 시 loss·reconnect·late join 후 완료 stream이 같은 policy의 Replay semantic sequence에 수렴하는 test
- Live player와 Replay의 presentation 차이에도 핵심 인과와 동일 경기로 인지되는지 사용자 검증

## 후보 상태 전이

```text
Candidate
  ├─> Accepted ADR ──> 이 후보는 Superseded + 공식 ADR 링크
  ├─> Rejected     ──> 사유와 대체 후보 기록
  └─> Deferred     ──> 재개 조건과 필요한 증거 기록
```

현재는 12개 모두 `Candidate`다. 선택된 runtime, transport, database, tick rate, Authoritative Match Record 저장 방식, viewer projection 구현 패턴 또는 Spectator 제공 여부는 없다.
