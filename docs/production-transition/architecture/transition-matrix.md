# Transition Matrix — Demo → Production

> 상태: **Draft**
>
> 기준선: **2026-07-29 / `44c87885`**
>
> 기본 전제: 정규 프로젝트는 **서버 권위 온라인 게임**, **non-ECS 전투 구조**다.
>
> 이 표의 `decide`는 선택 완료가 아니라 [ADR 후보](adr-candidates.md)에서 결정을 준비한다는 뜻이다.

## 판정 기준

| `transfer_action` | 의미 |
|---|---|
| `carry` | 도메인 의미론·불변식·test contract를 유지한다. 구현 복사를 뜻하지 않는다. |
| `adapt` | 목적은 유지하되 서버 권위·온라인 경계에 맞게 다시 설계한다. |
| `drop` | 데모 구현 또는 권위 모델을 정규 프로젝트에 넣지 않는다. |
| `decide` | 정규 프로젝트 시작 전에 ADR로 대안과 trade-off를 확정한다. |

## 판정 및 책임 Matrix

| ID | 전환 대상 | `transfer_action` | 정규 프로젝트 영향 | Product 책임 | Client 책임 | Server 책임 | 연결 후보 |
|---|---|---|---|---|---|---|---|
| `TRN-001` | 클라이언트 전투 + 서버 인증·attempt·seed·결과 보고 | `adapt` | 전투 상태·판정·점수의 최종 소유자를 서버로 이동 | 공정성·응답성·허용 지연 기준 정의 | input command 전송, 예측 표현, 권위 결과 수용 | command 검증, simulation, 판정, 결과 확정 | ADR-CAND-001, 002, 005 |
| `TRN-002` | Units / Movement / Combat / Effects 책임과 단일 쓰기 소유권 | `carry` | non-ECS 모듈에서도 명시적 state owner와 변경 경로 유지 | 규칙 충돌 시 제품 우선순위 확정 | replicated view state의 로컬 소유 범위 준수 | authoritative domain state의 단일 쓰기 보장 | ADR-CAND-002 |
| `TRN-003` | 순수 계산과 test vector | `carry` | score·stat·targeting 등 runtime-neutral 계약을 회귀 테스트로 고정 | 수식 의미·edge case 승인 | 예측·표시에 필요한 동일 규칙 사용 | authoritative 계산과 golden test 소유 | ADR-CAND-003, 008 |
| `TRN-004` | 결정론적 순서·seeded 생성 | `adapt` | server tick, RNG stream, numeric policy까지 포함한 재현 계약 필요 | “같은 조건”과 공정성 범위 정의 | command에 tick/sequence 부여, 연출 RNG 분리 | RNG·tick·ordering·rounding 권위 소유 | ADR-CAND-003, 004 |
| `TRN-005` | `BattleBridge` 단일 gateway | `adapt` | input, replication, presentation, content, lifecycle 포트로 분해 | 사용자 흐름·오류 노출 규칙 정의 | 포트별 adapter와 presentation 구성 | protocol endpoint와 match lifecycle 제공 | ADR-CAND-002, 005, 006 |
| `TRN-006` | ECS Buffer / `NativeQueue` 이벤트 | `adapt` | domain event와 protocol/presentation event를 분리하고 순서·중복 계약 추가 | 관측·피드백에 필요한 사건 정의 | presentation event 소비와 중복 방지 | domain event 생성, visibility·delivery 정책 적용 | ADR-CAND-005 |
| `TRN-007` | ScriptableObject·Sheet 기반 콘텐츠 정본 | `adapt` | versioned canonical config/hash를 서버 권위로 전환 | 밸런스 schema·배포 단위 승인 | 버전에 맞는 표현 에셋과 compatibility 확인 | canonical config 검증·선택·match pinning | ADR-CAND-007 |
| `TRN-008` | 로컬 `BattleLogger`와 complete debug body | `adapt` | server audit·metric·trace·replay 입력을 목적별 분리 | KPI·분석 이벤트와 보존 목적 정의 | UX·네트워크 품질 진단 이벤트 전송 | authoritative event·trace·보존·조회 소유 | ADR-CAND-011 |
| `TRN-009` | `Entity` / Component / `ISystem` / `SystemGroup` / ECB | `drop` | 정규 프로젝트 구현 의존성에서 제외 | 없음 | Entities 기반 전투 런타임을 전제하지 않음 | Entities 기반 서버 구조를 전제하지 않음 | ADR-CAND-002 |
| `TRN-010` | ECS world, native container, queue drain/dispose 규칙 | `drop` | ECS 전용 lifecycle은 제거하고 일반적인 match resource lifecycle을 새로 설계 | 중단·종료 사용자 규칙 정의 | 화면·cache·connection 자원 해제 | match actor/session 자원의 멱등 생성·종료 | ADR-CAND-002, 009 |
| `TRN-011` | 클라이언트가 계산한 score를 서버에 제출 | `drop` | score와 승패를 서버가 authoritative state에서 확정 | 산식·동률·무효 기준 정의 | 점수를 표시하되 확정값으로 서버 결과 사용 | 산식 실행, 무결성 확인, 결과 서명·저장 | ADR-CAND-008 |
| `TRN-012` | `Entity.Index` / `Entity.Version` 및 runtime-local identity | `drop` | protocol·snapshot·replay의 게임플레이 식별자로 사용하지 않음 | 없음 | runtime-local handle을 외부 identity로 노출하지 않음 | runtime-local handle을 외부 identity로 노출하지 않음 | ADR-CAND-004 |
| `TRN-013` | variable delta, 로컬 RNG와 부분 seed | `decide` | fixed tick, RNG stream, numeric/rounding, tie-break 정책 필요 | 체감 속도·허용 오차·공정성 정의 | render interpolation과 비권위 cosmetic RNG | tick scheduler, authoritative RNG·수치 실행 | ADR-CAND-003 |
| `TRN-014` | 네트워크 command·snapshot·delta·event 계약 | `decide` | command validation, state replication, versioning과 delivery semantics 필요 | 체감 지연·정보 공개 범위 정의 | serialize, ack, buffer, snapshot/delta 적용 | validate, sequence, visibility filter, replicate | ADR-CAND-005 |
| `TRN-015` | prediction·interpolation·reconciliation·resync | `decide` | 지연 중 조작감과 서버 판정 수렴 정책 필요 | 허용 rollback·보정 시각 기준 정의 | predict/interpolate/reconcile 및 correction UX | authoritative snapshot·correction·resync 제공 | ADR-CAND-006 |
| `TRN-016` | `PendingMatchStore` 기반 다음 로비 `complete(0)` | `decide` | 실제 reconnect, snapshot 복원, command dedupe, 멱등 종료로 재설계 | 재접속 유예·기권·보상 규칙 정의 | resume token 보관, 재접속 UI, 중복 command 방지 | 세션 보존, 재부착, timeout, idempotent terminal 처리 | ADR-CAND-009 |
| `TRN-017` | 로컬 pause·slow motion `TimeManager` | `decide` | server clock과 client presentation clock을 분리 | 온라인 pause·선택시간·deadline 규칙 정의 | 로컬 UI·연출 시간만 제어, server deadline 표시 | tick 지속/정지 조건과 command window 강제 | ADR-CAND-010 |
| `TRN-018` | 서버 host/runtime·match process 모델 | `decide` | 배포, 확장, 장애 격리, 비용, 개발 workflow를 좌우 | 목표 동접·지역·세션 SLA 제공 | SDK/protocol 제약 확인 | 후보 runtime·hosting·worker topology 평가 | ADR-CAND-001 |
| `TRN-019` | non-ECS 서버 도메인 구조 | `decide` | aggregate/module/actor 등 상태·동시성 경계 확정 필요 | 도메인 규칙과 우선순위 제공 | 공유 schema 외 서버 내부 구조에 의존하지 않음 | module 경계, transaction, concurrency, lifecycle 구현 | ADR-CAND-002 |
| `TRN-020` | 관측성·감사·replay | `decide` | 운영 진단, 분쟁 조사, 밸런스 분석, 재현 수준을 결정 | KPI·분쟁·분석 질문과 개인정보 제한 정의 | client quality signals와 correlation ID 제공 | authoritative log, metric, trace, replay 보존·도구 제공 | ADR-CAND-011 |
| `TRN-021` | stable gameplay ID 정책 | `decide` | match·player·unit·effect를 protocol·snapshot·replay 전 구간에서 안정적으로 식별 | 플레이어에게 보이는 개체 동일성 요구 정의 | stable ID로 view/prediction/cache 연결 | 발급 범위·수명·재사용 금지·lookup 소유 | ADR-CAND-004 |

## Claim Registry

아래 표는 위 Matrix의 각 행에 공통 기록 계약을 연결한다. `sources`는 구현 사실의 근거이며, 정규 프로젝트 선택을 승인하는 근거가 아니다.

| ID | `claim_kind` | `evidence_status` | `evidence_level` | `as_of`와 적용 조건 | `sources` / 관련 테스트·커밋 | 다음 검증·결정 |
|---|---|---|---|---|---|---|
| `TRN-001` | `decision` | `untested` | `E0` | `2026-07-29 / 44c87885`; 정규 온라인 전투 | [demo baseline](../demo-baseline.md), [tournament-play-report](../../spec/tournament-play-report/README.md) | authority boundary를 ADR-CAND-001·002·005에서 확정하고 latency prototype 검증 |
| `TRN-002` | `decision` | `functional` | `E1` | 동일 기준선; 데모 ECS 맥락 | [TRD](../../TRD.md), [`Scripts/Battle/`](../../../Assets/_Project/Scripts/Battle/) | non-ECS 경계와 cross-module write test 설계 |
| `TRN-003` | `fact` | `functional` | `E1` | 동일 기준선; 순수 계산 | [`ScoreMathTests`](../../../Assets/_Project/Tests/EditMode/ScoreMathTests.cs), [`ModifierMathTests`](../../../Assets/_Project/Tests/EditMode/ModifierMathTests.cs), [`DcTriggerTests`](../../../Assets/_Project/Tests/EditMode/DcTriggerTests.cs) | 서버 runtime에서 동일 vector·경계값 통과 |
| `TRN-004` | `fact` | `functional` | `E1` | 동일 기준선; 맵·웨이브 선택까지만 | [map-wave-balancing](../../reference/map-wave-balancing.md), [`MatchSeedTests`](../../../Assets/_Project/Tests/EditMode/MatchSeedTests.cs) | fixed tick 재시뮬 signature와 cross-runtime 결과 비교 |
| `TRN-005` | `fact` | `functional` | `E1` | 동일 기준선; 하이브리드 ECS 데모 | [`BattleBridge.cs`](../../../Assets/_Project/Scripts/Bridge/BattleBridge.cs), [TRD](../../TRD.md) | 포트별 책임·dependency test 정의 |
| `TRN-006` | `decision` | `functional` | `E1` | 동일 기준선; 로컬 프로세스 내 이벤트 | [`CLAUDE.md`](../../../CLAUDE.md), [`DefenderDeathEventsSingleton.cs`](../../../Assets/_Project/Scripts/Battle/Units/DefenderDeathEventsSingleton.cs) | event ordering·dedupe·delivery contract test |
| `TRN-007` | `fact` | `functional` | `E1` | 동일 기준선; 로컬 에셋·Sheet import | [map-wave-balancing](../../reference/map-wave-balancing.md), [score-formula](../../reference/score-formula.md) | content version pinning·mismatch·rollback rehearsal |
| `TRN-008` | `fact` | `instrumented` | `E1` | 동일 기준선; client debug log 첨부 | [tournament-play-report](../../spec/tournament-play-report/README.md), [`BattleLogSchema.cs`](../../../Assets/_Project/Scripts/Logging/BattleLogSchema.cs) | server event schema와 trace correlation 검증 |
| `TRN-009` | `decision` | `untested` | `E0` | 동일 기준선; 정규 프로젝트 non-ECS 전제 | [engineering learnings](engineering-learnings.md), [TRD](../../TRD.md) | ADR-CAND-002 승인 시 제거 범위 고정 |
| `TRN-010` | `decision` | `untested` | `E0` | 동일 기준선; 정규 프로젝트 non-ECS 전제 | [engineering learnings](engineering-learnings.md), [TRD](../../TRD.md) | match lifecycle failure-injection test 정의 |
| `TRN-011` | `fact` | `functional` | `E1` | 동일 기준선; 데모 complete 경로 | [`ScoreMath.cs`](../../../Assets/_Project/Scripts/Core/ScoreMath.cs), [`TournamentApi.cs`](../../../Assets/_Project/Scripts/Core/Api/TournamentApi.cs), [score-formula](../../reference/score-formula.md) | 서버 score 계산·저장·client mismatch test |
| `TRN-012` | `decision` | `functional` | `E1` | 동일 기준선; 데모 targeting tie-break의 runtime-local ID 사용 | [`FrontmostTargeting.cs`](../../../Assets/_Project/Scripts/Battle/Combat/FrontmostTargeting.cs), [`LowestHealthTargeting.cs`](../../../Assets/_Project/Scripts/Battle/Combat/LowestHealthTargeting.cs) | 정규 protocol·log schema에 runtime-local ID가 새지 않는지 검증 |
| `TRN-013` | `decision` | `untested` | `E0` | 동일 기준선; 정규 server simulation | [`BattleScaledRateManager.cs`](../../../Assets/_Project/Scripts/Battle/BattleScaledRateManager.cs), [sim-design](../../reference/lessons/04-sim-design.md) | tick stress test, RNG stream audit, numeric golden test |
| `TRN-014` | `decision` | `untested` | `E0` | 동일 기준선; 정규 client-server protocol | [tournament-flow-guards](../../spec/tournament-flow-guards/README.md), [engineering learnings](engineering-learnings.md) | loss·duplicate·reorder·version mismatch test |
| `TRN-015` | `decision` | `untested` | `E0` | 동일 기준선; 지연·packet loss 조건 | 현 데모에 해당 구현 없음 | network simulation으로 correction rate·체감 검증 |
| `TRN-016` | `fact` | `functional` | `E1` | 동일 기준선; 다음 로비에서 미완료 attempt를 0점 마감 | [`PendingMatchStore.cs`](../../../Assets/_Project/Scripts/Core/Api/PendingMatchStore.cs), [tournament-flow-guards](../../spec/tournament-flow-guards/README.md), [`PendingMatchStoreTests`](../../../Assets/_Project/Tests/EditMode/Api/PendingMatchStoreTests.cs) | 실제 disconnect/reconnect/resume/duplicate terminal test |
| `TRN-017` | `fact` | `functional` | `E1` | 동일 기준선; 단일 클라이언트 로컬 시간 | [`TimeManager.cs`](../../../Assets/_Project/Scripts/Core/TimeControl/TimeManager.cs), [sim-design](../../reference/lessons/04-sim-design.md) | 온라인 선택 UI·deadline·pause 사용자 테스트 |
| `TRN-018` | `decision` | `untested` | `E0` | 동일 기준선; runtime 미정 | 사용자 승인 계획, 현 저장소에 서버 runtime 근거 없음 | 부하·배포·비용 spike 후 ADR-CAND-001 |
| `TRN-019` | `decision` | `untested` | `E0` | 동일 기준선; non-ECS 구조 미정 | [engineering learnings](engineering-learnings.md), [TRD](../../TRD.md) | 대표 전투 slice로 concurrency·testability 비교 |
| `TRN-020` | `decision` | `untested` | `E0` | 동일 기준선; authoritative observability 미구현 | [`BattleLogSchema.cs`](../../../Assets/_Project/Scripts/Logging/BattleLogSchema.cs), [tournament-play-report](../../spec/tournament-play-report/README.md) | audit query·incident reconstruction·replay fidelity 검증 |
| `TRN-021` | `decision` | `untested` | `E0` | 동일 기준선; 정규 protocol 전체의 stable ID 정책 미정 | [engineering learnings](engineering-learnings.md), [`FrontmostTargeting.cs`](../../../Assets/_Project/Scripts/Battle/Combat/FrontmostTargeting.cs) | ID 발급·수명·재사용·serialization·replay 참조 무결성 test |

## 교차 점검

| 필수 전환 주제 | Matrix | ADR 후보 |
|---|---|---|
| 서버 권위 | `TRN-001` | ADR-CAND-001, 002, 005 |
| ECS 제거 | `TRN-009`, `TRN-010`, `TRN-019` | ADR-CAND-002 |
| 콘텐츠 버전 | `TRN-007` | ADR-CAND-007 |
| 점수 권위·치트 방지 | `TRN-011` | ADR-CAND-008 |
| 안정 ID | `TRN-012`, `TRN-021` | ADR-CAND-004 |
| 재접속·멱등 종료 | `TRN-016` | ADR-CAND-009 |
| 관측성·replay | `TRN-008`, `TRN-020` | ADR-CAND-011 |

이 Matrix는 정규 프로젝트의 구현 백로그가 아니다. 후보가 승인되어 공식 ADR과 PRD 요구사항으로 연결되기 전에는 데모 저장소의 ECS 규칙을 바꾸거나 네트워크 구현을 추가하는 근거로 사용할 수 없다.
