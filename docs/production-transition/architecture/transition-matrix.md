# Transition Matrix — Demo → Production

> 상태: **Historical · stale · preparatory**
>
> 기준선: **2026-07-29 / `44c87885`**
>
> 기본 전제: 정규 프로젝트는 **서버 권위 온라인 게임**, **non-ECS 전투 구조**다. 2026-07-30 책임 경계 결정에 따라 Product/Game Design은 규칙 의도·밸런스·콘텐츠 의미를 작성·승인하고, 서버는 결과에 영향을 주는 gameplay ruleset과 실행을, 클라이언트는 입력 UX와 presentation을 소유한다.
>
> 이 표의 `decide`는 선택 완료가 아니라 [ADR 후보](adr-candidates.md)에서 결정을 준비한다는 뜻이다.

> 관찰 사실과 `carry/rebuild/retire` 같은 production 선택은 새 registry에서 별도 ID와
> owner review를 갖는다. 이 표의 E1 근거가 production 결정을 승인하지 않는다.

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
| `TRN-001` | 클라이언트 전투 + 서버 인증·attempt·seed·결과 보고 | `adapt` | 명령 유효성, 비용·쿨다운, 타게팅, 효과·피해, 웨이브·스폰, gameplay clock·RNG, 승패·점수·보상을 포함한 모든 결과 영향 규칙과 상태 전이의 권위를 서버로 이동 | 규칙 의도·밸런스·공정성·응답성·허용 지연 기준 작성·승인 | 행동 의도 command 전송, 제한적 비권위 prediction과 presentation, 권위 결과 수용 | ruleset 정본, command 검증, simulation, 판정, 결과 확정 | ADR-CAND-001, 002, 005, 007, 008 |
| `TRN-002` | Units / Movement / Combat / Effects 책임과 단일 쓰기 소유권 | `carry` | non-ECS 서버 모듈에서도 명시적 state owner와 변경 경로 유지 | 규칙 의미와 충돌 시 제품 우선순위 확정 | replicated view state와 presentation만 로컬 소유 | authoritative domain state와 transition의 단일 쓰기 보장 | ADR-CAND-002 |
| `TRN-003` | 순수 계산과 test vector | `carry` | score·stat·targeting 등 서버 ruleset 의미론을 golden test로 고정 | 수식 의미·edge case 승인 | versioned read model 또는 prediction·preview용 최소 부분집합만 비권위로 사용 | authoritative 계산·ruleset·golden test 소유 | ADR-CAND-003, 006, 008 |
| `TRN-004` | 결정론적 순서·seeded 생성 | `adapt` | server tick, gameplay RNG stream, numeric policy까지 포함한 재현 계약 필요 | “같은 조건”과 공정성 범위 정의 | local sequence·presentation clock 사용, gameplay RNG와 격리된 cosmetic RNG | authoritative RNG·tick·ordering·rounding 소유 | ADR-CAND-003, 004 |
| `TRN-005` | `BattleBridge` 단일 gateway | `adapt` | intent command, semantic replication, presentation, ruleset/catalog mapping, lifecycle 포트로 분해 | 사용자 흐름·오류 노출 규칙 정의 | 포트별 adapter와 presentation 구성 | protocol endpoint와 match lifecycle 제공 | ADR-CAND-002, 005, 006, 007 |
| `TRN-006` | ECS Buffer / `NativeQueue` 이벤트 | `adapt` | server domain event와 stable ID·authoritative tick 기반 semantic protocol event를 분리하고 순서·중복 계약 추가 | 플레이어에게 전달할 사건 의미·피드백 기준 정의 | semantic outcome을 local asset으로 표현하고 event ID로 부작용 중복 방지; animation callback은 gameplay를 진행하지 않음 | domain outcome 생성, visibility·delivery 정책 적용; Unity asset·연출 지시는 전송하지 않음 | ADR-CAND-005, 006, 007 |
| `TRN-007` | ScriptableObject·Sheet 기반 콘텐츠 정본 | `adapt` | server canonical gameplay ruleset과 client presentation catalog를 stable gameplay ID·호환 version으로 분리 | Game Design 규칙·밸런스·콘텐츠 의미 작성·승인 | presentation 저작과 prefab·animation·VFX·SFX·UI·localization·camera·haptics mapping, catalog compatibility 확인 | canonical ruleset 검증·선택·match pinning·실행 | ADR-CAND-007 |
| `TRN-008` | 로컬 `BattleLogger`와 complete debug body | `adapt` | Authoritative Match Record, audit·metric·trace, Client telemetry·`as-seen presentation trace`를 목적별 artifact로 분리 | KPI·분쟁·진단 질문과 보존 목적 정의 | UX·네트워크 품질, presentation version, 표시 event correlation과 선택적 진단 trace 제공; 권위 근거로 사용하지 않음 | authoritative event·ruleset version을 기록하고 Client artifact와 correlation·보존·조회 소유 | ADR-CAND-011 |
| `TRN-009` | `Entity` / Component / `ISystem` / `SystemGroup` / ECB | `drop` | 정규 프로젝트 구현 의존성에서 제외 | 없음 | Entities 기반 전투 런타임을 전제하지 않음 | Entities 기반 서버 구조를 전제하지 않음 | ADR-CAND-002 |
| `TRN-010` | ECS world, native container, queue drain/dispose 규칙 | `drop` | ECS 전용 lifecycle은 제거하고 일반적인 match resource lifecycle을 새로 설계 | 중단·종료 사용자 규칙 정의 | 화면·cache·connection 자원 해제 | match actor/session 자원의 멱등 생성·종료 | ADR-CAND-002, 009 |
| `TRN-011` | 클라이언트가 계산한 score를 서버에 제출 | `drop` | score와 승패를 서버가 authoritative state에서 확정 | 산식·동률·무효 기준 정의 | 점수를 표시하되 확정값으로 서버 결과 사용 | 산식 실행, 무결성 확인, 결과 서명·저장 | ADR-CAND-008 |
| `TRN-012` | `Entity.Index` / `Entity.Version` 및 runtime-local identity | `drop` | protocol·snapshot·replay의 게임플레이 식별자로 사용하지 않음 | 없음 | runtime-local handle을 외부 identity로 노출하지 않음 | runtime-local handle을 외부 identity로 노출하지 않음 | ADR-CAND-004 |
| `TRN-013` | variable delta, 로컬 RNG와 부분 seed | `decide` | fixed tick, gameplay RNG stream, numeric/rounding, tie-break 정책 필요 | 체감 속도·허용 오차·공정성·telegraph 의미 정의 | render interpolation과 비권위 cosmetic RNG; 가시성·판정 시점·충돌·event 수·명령 가능 여부·대상·점수에 영향 금지 | tick scheduler, authoritative gameplay RNG·수치·telegraph state 실행 | ADR-CAND-003 |
| `TRN-014` | 네트워크 command·snapshot·delta·event 계약 | `decide` | intent-only command, server validation, semantic state/outcome, versioning과 delivery semantics 필요 | 체감 지연·정보 공개 범위 정의 | 행동 의도와 local sequence만 전송하고 ack·snapshot/delta 적용; damage·state delta·score·판정 시각을 제출하지 않음 | actor·state·ownership·permission·cost·cooldown·target·sequence·deadline·rate 검증 후 결과 계산·replicate | ADR-CAND-005 |
| `TRN-015` | prediction·interpolation·reconciliation·resync | `decide` | 제한적 prediction을 비권위·폐기 가능하게 유지하고 항상 서버 판정으로 수렴하며, 예측·reversal·network arrival history는 Authoritative Match Record와 canonical Replay에서 제외 | 허용 rollback·보정 시각 기준 정의 | predict/interpolate/reconcile, correction UX, authoritative event ID 기반 presentation side effect dedupe | authoritative snapshot·correction·resync 제공; Client 예측 결과를 수락·기록·Replay 근거로 사용하지 않음 | ADR-CAND-006, 012 |
| `TRN-016` | `PendingMatchStore` 기반 다음 로비 `complete(0)` | `decide` | 실제 reconnect, snapshot 복원, command dedupe, 멱등 종료로 재설계 | 재접속 유예·기권·보상 규칙 정의 | resume token 보관, 재접속 UI, 중복 command 방지 | 세션 보존, 재부착, timeout, idempotent terminal 처리 | ADR-CAND-009 |
| `TRN-017` | 로컬 pause·slow motion `TimeManager` | `decide` | server clock과 client presentation clock을 분리 | 온라인 pause·선택시간·deadline 규칙 정의 | 로컬 UI·연출 시간만 제어하고 server deadline 표시; presentation callback으로 gameplay 진행 금지 | tick 지속/정지 조건과 command window 강제 | ADR-CAND-010 |
| `TRN-018` | 서버 host/runtime·match process 모델 | `decide` | 배포, 확장, 장애 격리, 비용, 개발 workflow를 좌우 | 목표 동접·지역·세션 SLA 제공 | SDK/protocol 제약 확인 | 후보 runtime·hosting·worker topology 평가 | ADR-CAND-001 |
| `TRN-019` | non-ECS 서버 도메인 구조 | `decide` | aggregate/module/actor 등 상태·동시성 경계 확정 필요 | 도메인 규칙과 우선순위 제공 | 공유 schema 외 서버 내부 구조에 의존하지 않음 | module 경계, transaction, concurrency, lifecycle 구현 | ADR-CAND-002 |
| `TRN-020` | Authoritative Match Record·관측성 | `decide` | 경기 source of truth의 capture, 저장, checkpoint, progression signature·무결성, 보존과 Client presentation artifact correlation을 결정 | KPI·분쟁·분석·진단 질문과 개인정보·접근 제한 정의 | client quality signal, presentation version, 표시 event correlation 제공; canonical record를 생성·수정하지 않음 | authoritative tick·state transition·semantic event·stable ID·RNG 결과·승패·점수·ruleset/schema 기록과 조회 소유 | ADR-CAND-011 |
| `TRN-021` | stable gameplay ID 정책 | `decide` | match·player·unit·effect를 protocol·snapshot·replay 전 구간에서 안정적으로 식별 | 플레이어에게 보이는 개체 동일성 요구 정의 | stable ID로 view/prediction/cache 연결 | 발급 범위·수명·재사용 금지·lookup 소유 | ADR-CAND-004 |
| `TRN-022` | Replay와 조건부 Spectator viewer projection | `decide` | Replay 요구는 확정됐지만 projection·playback 구현은 새로 결정한다. Authoritative Match Record를 versioned role·관점·visibility·delay policy로 필터링한 semantic stream을 재생하며, 동일 match·policy의 Replay와 조건부 Spectator는 같은 authoritative progression에 수렴하되 Live player의 prediction·camera·VFX timing과는 다를 수 있다. | Replay 관점·공개 시점·핵심 단서 정의, Spectator 제공 여부와 접근·delay·anti-ghosting 정책 결정 | read-only projection을 playback clock·camera·UI·VFX/SFX로 표현하고 pause·seek·rewind·배속 부작용을 중복 제거; 판정 재실행 금지 | viewer 권한·visibility·delay를 전송 전에 집행하고 stable ID 기반 semantic projection 제공; Unity 연출 지시 금지 | ADR-CAND-005, 007, 011, 012 |

## Claim Registry

아래 표는 위 Matrix의 각 행에 공통 기록 계약을 연결한다. `sources`는 구현 사실의 근거이며, 정규 프로젝트 선택을 승인하는 근거가 아니다.

| ID | `claim_kind` | `evidence_status` | `evidence_level` | `as_of`와 적용 조건 | `sources` / 관련 테스트·커밋 | 다음 검증·결정 |
|---|---|---|---|---|---|---|
| `TRN-001` | `decision` | `untested` | `E0` | `2026-07-30`; 데모 근거 기준 `2026-07-29 / 44c87885`, 정규 온라인 전투 | [demo baseline](../demo-baseline.md), [tournament-play-report](../../spec/tournament-play-report/README.md) | 고정 authority boundary를 ADR-CAND-002·005·007에 제약으로 반영하고 latency prototype 검증 |
| `TRN-002` | `decision` | `functional` | `E1` | 동일 기준선; 데모 ECS 맥락 | [TRD](../../TRD.md), [`Scripts/Battle/`](../../../Assets/_Project/Scripts/Battle/) | non-ECS 경계와 cross-module write test 설계 |
| `TRN-003` | `fact` | `functional` | `E1` | 동일 기준선; 순수 계산 | [`ScoreMathTests`](../../../Assets/_Project/Tests/EditMode/ScoreMathTests.cs), [`ModifierMathTests`](../../../Assets/_Project/Tests/EditMode/ModifierMathTests.cs), [`DcTriggerTests`](../../../Assets/_Project/Tests/EditMode/DcTriggerTests.cs) | 서버 runtime에서 동일 vector·경계값을 통과하고 Client prediction subset이 서버 판정으로 수렴 |
| `TRN-004` | `fact` | `functional` | `E1` | 동일 기준선; 맵·웨이브 선택까지만 | [map-wave-balancing](../../reference/map-wave-balancing.md), [`MatchSeedTests`](../../../Assets/_Project/Tests/EditMode/MatchSeedTests.cs) | fixed tick 재시뮬 signature와 cross-runtime 결과 비교 |
| `TRN-005` | `fact` | `functional` | `E1` | 동일 기준선; 하이브리드 ECS 데모 | [`BattleBridge.cs`](../../../Assets/_Project/Scripts/Bridge/BattleBridge.cs), [TRD](../../TRD.md) | 포트별 책임·dependency test 정의 |
| `TRN-006` | `decision` | `functional` | `E1` | 동일 기준선; 로컬 프로세스 내 이벤트 | [`CLAUDE.md`](../../../CLAUDE.md), [`DefenderDeathEventsSingleton.cs`](../../../Assets/_Project/Scripts/Battle/Units/DefenderDeathEventsSingleton.cs) | semantic event ordering·dedupe·delivery와 presentation callback 비권위 계약 test |
| `TRN-007` | `fact` | `functional` | `E1` | 동일 기준선; 로컬 에셋·Sheet import | [map-wave-balancing](../../reference/map-wave-balancing.md), [score-formula](../../reference/score-formula.md) | ruleset match pinning, catalog version 기록·호환성, mismatch·rollback rehearsal |
| `TRN-008` | `fact` | `instrumented` | `E1` | 동일 기준선; client debug log 첨부 | [tournament-play-report](../../spec/tournament-play-report/README.md), [`BattleLogSchema.cs`](../../../Assets/_Project/Scripts/Logging/BattleLogSchema.cs) | Authoritative Match Record와 Client telemetry·`as-seen presentation trace`의 분리 및 end-to-end correlation 검증 |
| `TRN-009` | `decision` | `untested` | `E0` | 동일 기준선; 정규 프로젝트 non-ECS 전제 | [engineering learnings](engineering-learnings.md), [TRD](../../TRD.md) | ADR-CAND-002 승인 시 제거 범위 고정 |
| `TRN-010` | `decision` | `untested` | `E0` | 동일 기준선; 정규 프로젝트 non-ECS 전제 | [engineering learnings](engineering-learnings.md), [TRD](../../TRD.md) | match lifecycle failure-injection test 정의 |
| `TRN-011` | `fact` | `functional` | `E1` | 동일 기준선; 데모 complete 경로 | [`ScoreMath.cs`](../../../Assets/_Project/Scripts/Core/ScoreMath.cs), [`TournamentApi.cs`](../../../Assets/_Project/Scripts/Core/Api/TournamentApi.cs), [score-formula](../../reference/score-formula.md) | 서버 score 계산·저장·client mismatch test |
| `TRN-012` | `decision` | `functional` | `E1` | 동일 기준선; 데모 targeting tie-break의 runtime-local ID 사용 | [`FrontmostTargeting.cs`](../../../Assets/_Project/Scripts/Battle/Combat/FrontmostTargeting.cs), [`LowestHealthTargeting.cs`](../../../Assets/_Project/Scripts/Battle/Combat/LowestHealthTargeting.cs) | 정규 protocol·log schema에 runtime-local ID가 새지 않는지 검증 |
| `TRN-013` | `decision` | `untested` | `E0` | 동일 기준선; 정규 server simulation | [`BattleScaledRateManager.cs`](../../../Assets/_Project/Scripts/Battle/BattleScaledRateManager.cs), [sim-design](../../reference/lessons/04-sim-design.md) | tick stress, RNG stream audit, cosmetic RNG 격리, numeric golden test |
| `TRN-014` | `decision` | `untested` | `E0` | 동일 기준선; 정규 client-server protocol | [tournament-flow-guards](../../spec/tournament-flow-guards/README.md), [engineering learnings](engineering-learnings.md) | tampered outcome field, invalid intent, loss·duplicate·reorder·version mismatch test |
| `TRN-015` | `decision` | `untested` | `E0` | 동일 기준선; 지연·packet loss 조건 | 현 데모에 해당 구현 없음 | network simulation으로 correction rate·체감·presentation side effect dedupe를 검증하고 predicted/reversal event가 canonical record·Replay에 포함되지 않는지 확인 |
| `TRN-016` | `fact` | `functional` | `E1` | 동일 기준선; 다음 로비에서 미완료 attempt를 0점 마감 | [`PendingMatchStore.cs`](../../../Assets/_Project/Scripts/Core/Api/PendingMatchStore.cs), [tournament-flow-guards](../../spec/tournament-flow-guards/README.md), [`PendingMatchStoreTests`](../../../Assets/_Project/Tests/EditMode/Api/PendingMatchStoreTests.cs) | 실제 disconnect/reconnect/resume/duplicate terminal test |
| `TRN-017` | `fact` | `functional` | `E1` | 동일 기준선; 단일 클라이언트 로컬 시간 | [`TimeManager.cs`](../../../Assets/_Project/Scripts/Core/TimeControl/TimeManager.cs), [sim-design](../../reference/lessons/04-sim-design.md) | 온라인 선택 UI·deadline·pause 사용자 테스트 |
| `TRN-018` | `decision` | `untested` | `E0` | 동일 기준선; runtime 미정 | 사용자 승인 계획, 현 저장소에 서버 runtime 근거 없음 | 부하·배포·비용 spike 후 ADR-CAND-001 |
| `TRN-019` | `decision` | `untested` | `E0` | 동일 기준선; non-ECS 구조 미정 | [engineering learnings](engineering-learnings.md), [TRD](../../TRD.md) | 대표 전투 slice로 concurrency·testability 비교 |
| `TRN-020` | `decision` | `untested` | `E0` | 동일 기준선; Authoritative Match Record·server observability 미구현 | [`BattleLogSchema.cs`](../../../Assets/_Project/Scripts/Logging/BattleLogSchema.cs), [tournament-play-report](../../spec/tournament-play-report/README.md) | capture 방식별 progression signature·schema migration·누락·중복·변조 탐지와 incident reconstruction 검증 |
| `TRN-021` | `decision` | `untested` | `E0` | 동일 기준선; 정규 protocol 전체의 stable ID 정책 미정 | [engineering learnings](engineering-learnings.md), [`FrontmostTargeting.cs`](../../../Assets/_Project/Scripts/Battle/Combat/FrontmostTargeting.cs) | ID 발급·수명·재사용·serialization·replay 참조 무결성 test |
| `TRN-022` | `decision` | `untested` | `E0` | `2026-07-30`; 정규 Replay는 확정, Spectator 제공 여부와 viewer policy는 미정 | [ENG-011](engineering-learnings.md), [demo baseline](../demo-baseline.md) | 동일 record·policy의 Replay/Spectator semantic sequence 수렴, hidden information 비노출, playback seek 부작용, ruleset/catalog 호환 검증 |

## 교차 점검

| 필수 전환 주제 | Matrix | ADR 후보 |
|---|---|---|
| 서버 권위 | `TRN-001` | ADR-CAND-001, 002, 005, 007, 008 |
| 게임 규칙 ↔ Client presentation 소유권 | `TRN-001`, `TRN-006`, `TRN-007` | ADR-CAND-002, 005, 007 |
| ECS 제거 | `TRN-009`, `TRN-010`, `TRN-019` | ADR-CAND-002 |
| 콘텐츠 버전 | `TRN-007` | ADR-CAND-007 |
| semantic protocol | `TRN-006`, `TRN-014` | ADR-CAND-005 |
| prediction·cosmetic RNG | `TRN-013`, `TRN-015` | ADR-CAND-003, 006 |
| 점수 권위·치트 방지 | `TRN-011` | ADR-CAND-008 |
| 안정 ID | `TRN-012`, `TRN-021` | ADR-CAND-004 |
| 재접속·멱등 종료 | `TRN-016` | ADR-CAND-009 |
| Authoritative Match Record·관측성 | `TRN-008`, `TRN-020` | ADR-CAND-011 |
| Replay·조건부 Spectator projection | `TRN-015`, `TRN-022` | ADR-CAND-005, 007, 012 |

이 Matrix는 정규 프로젝트의 구현 백로그가 아니다. 후보가 승인되어 공식 ADR과 PRD 요구사항으로 연결되기 전에는 데모 저장소의 ECS 규칙을 바꾸거나 네트워크 구현을 추가하는 근거로 사용할 수 없다.
