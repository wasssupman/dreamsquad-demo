# Engineering Learnings — 데모에서 가져갈 공학적 교훈

> 상태: **Draft**
>
> 기준선: **2026-07-29 / `44c87885`**
>
> 정규 책임 경계 결정: **2026-07-30**
>
> 대상: Product, Client, Server
>
> 범위: 정규 프로젝트의 설계 입력. 구현 승인이나 공식 ADR이 아니다.

## 읽는 규칙

- `carry`는 데모 코드를 그대로 복사한다는 뜻이 아니다. **도메인 의미론, 책임 경계, 불변식, 테스트 계약**을 유지한다는 뜻이다.
- `Entity`, `IComponentData`, `ISystem`, `SystemGroup`, `EntityCommandBuffer`, `DynamicBuffer`, `NativeQueue`와 해당 수명주기 규칙은 정규 프로젝트로 `carry`하지 않는다.
- `functional` 또는 E1/E2는 구현과 기능 경로가 존재한다는 뜻일 뿐, 재미·공정성·운영 적합성을 지지하지 않는다.
- Product/Game Design은 규칙 의도·밸런스·콘텐츠 의미를 작성·승인한다. 런타임에서는 **게임 결과에 영향을 주는 규칙·설정·상태 전이의 정본과 실행 권위는 서버**, 입력 UX와 시각·청각·촉각 presentation은 클라이언트가 소유한다.
- 정규 프로젝트의 서버 runtime, 전송 기술, 수치 표현은 모두 [ADR 후보](adr-candidates.md)에서 결정한다.

## 학습 기록

### ENG-001 — 도메인 책임과 단일 쓰기 소유권은 유지한다

| field | value |
|---|---|
| `claim_kind` | `decision` |
| `evidence_status` | `functional` |
| `evidence_level` | `E1` |
| `as_of` | `2026-07-29 / 44c87885`, 데모 전투의 Units·Movement·Combat·Effects 맥락 |
| `sources` | [TRD §2.5, §5](../../TRD.md), [`CLAUDE.md`](../../../CLAUDE.md), [`Assets/_Project/Scripts/Battle/`](../../../Assets/_Project/Scripts/Battle/) |
| `related_commit_or_test` | 현재 구현 구조와 맥락별 EditMode 테스트. 구조화된 플레이테스트 증거는 없음 |
| `transfer_action` | `carry` |
| `production_impact` | 정규 프로젝트에서도 상태마다 명시적 소유 모듈을 두고, 다른 모듈은 명령 또는 이벤트를 통해 변경을 요청한다. 서버가 명령 유효성, 비용·쿨다운, 타게팅, 효과·피해, 웨이브·스폰, gameplay clock·RNG, 승패·점수·보상을 포함한 authoritative gameplay state와 transition의 최종 쓰기 소유자다. |
| `next_validation_or_decision` | ADR-CAND-002에서 non-ECS 모듈·transaction 경계를, ADR-CAND-005·007에서 protocol·ruleset 경계를 확정한다. |

데모에서 가치가 있었던 것은 ECS 타입이 아니라 “누가 상태를 쓸 수 있는가”를 명시한 점이다. 이 규칙은 동시 처리, 재접속, 재시뮬레이션에서도 상태 충돌을 줄이는 핵심 계약이다.

### ENG-002 — 순수 계산과 회귀 테스트의 형태를 유지한다

| field | value |
|---|---|
| `claim_kind` | `fact` |
| `evidence_status` | `functional` |
| `evidence_level` | `E1` |
| `as_of` | `2026-07-29 / 44c87885`, 데모 EditMode 계산 경로 |
| `sources` | [`ScoreMath.cs`](../../../Assets/_Project/Scripts/Core/ScoreMath.cs), [`MatchSeed.cs`](../../../Assets/_Project/Scripts/Core/MatchSeed.cs), [`DcTrigger.cs`](../../../Assets/_Project/Scripts/Battle/Combat/DcTrigger.cs), [`DotTick.cs`](../../../Assets/_Project/Scripts/Battle/Effects/DotTick.cs) |
| `related_commit_or_test` | [`ScoreMathTests.cs`](../../../Assets/_Project/Tests/EditMode/ScoreMathTests.cs), [`MatchSeedTests.cs`](../../../Assets/_Project/Tests/EditMode/MatchSeedTests.cs), [`DcTriggerTests.cs`](../../../Assets/_Project/Tests/EditMode/DcTriggerTests.cs), [`DotTickTests.cs`](../../../Assets/_Project/Tests/EditMode/DotTickTests.cs) |
| `transfer_action` | `carry` |
| `production_impact` | plain input → plain output 계산과 golden vector는 서버 authoritative ruleset의 의미론을 고정하는 계약으로 삼는다. 클라이언트는 versioned read model 또는 제한된 prediction·preview에 필요한 일부 계산만 비권위로 복제할 수 있으며, 그 결과는 폐기 가능하고 서버 correction이 항상 우선한다. 언어·runtime이 달라지면 코드가 아니라 의미론과 golden vector를 이식한다. |
| `next_validation_or_decision` | ADR-CAND-003에서 수치 타입·반올림을, ADR-CAND-006에서 클라이언트에 복제할 최소 계산 범위와 correction 계약을 결정한다. |

`MatchSeed.GenerateRandom()`처럼 Unity나 로컬 시간에 의존하는 진입점은 이식 대상이 아니다. 파생 함수와 계산 계약만 후보로 삼는다. “공통 계약”은 Client가 판정 권위를 나눠 갖는다는 뜻이 아니다.

### ENG-003 — 결정론적 순서는 유효하지만 현재 데모는 재시뮬레이션 가능한 결정론이 아니다

| field | value |
|---|---|
| `claim_kind` | `fact` |
| `evidence_status` | `functional` |
| `evidence_level` | `E1` |
| `as_of` | `2026-07-29 / 44c87885`, 같은 맵의 고정 `waveSeed` 및 프레임 delta 기반 전투 |
| `sources` | [맵·웨이브 결정론 규칙](../../reference/map-wave-balancing.md), [전투 시뮬 설계 원칙](../../reference/lessons/04-sim-design.md), [`BattleScaledRateManager.cs`](../../../Assets/_Project/Scripts/Battle/BattleScaledRateManager.cs), [`MovementSystem.cs`](../../../Assets/_Project/Scripts/Battle/Movement/MovementSystem.cs), [`AttackSystem.cs`](../../../Assets/_Project/Scripts/Battle/Combat/AttackSystem.cs) |
| `related_commit_or_test` | [`WavePatternGeneratorTests.cs`](../../../Assets/_Project/Tests/EditMode/WavePatternGeneratorTests.cs), [`MapPoolSelectTests.cs`](../../../Assets/_Project/Tests/EditMode/MapPoolSelectTests.cs) |
| `transfer_action` | `adapt` |
| `production_impact` | 정규 프로젝트는 서버 fixed tick, 명시적 RNG stream, 안정적인 tie-break, 수치·반올림 정책을 새로 정의해야 한다. 데모의 `waveSeed`는 맵·웨이브 선택을 고정할 뿐 전체 전투의 byte-identical 재현을 보장하지 않는다. |
| `next_validation_or_decision` | ADR-CAND-003과 ADR-CAND-004를 함께 결정하고 cross-runtime determinism test를 만든다. |

결정론은 seed 하나를 공유하는 문제가 아니다. 입력 순서, tick, RNG 소비 순서, ID, 부동소수점과 정렬의 동률 처리까지 하나의 계약이어야 한다.

게임플레이 RNG는 서버 tick·ruleset에 귀속한다. 클라이언트의 cosmetic RNG는 telegraph의 의미·가시성, 판정·충돌 시점, event 수, command 가능 여부, 대상 선택, 점수에 영향을 줄 수 없고, 게임플레이에 필요한 telegraph는 서버 상태나 semantic event에서 파생해야 한다.

### ENG-004 — 데이터 주도 원칙은 유지하되 ScriptableObject를 정본으로 삼지 않는다

| field | value |
|---|---|
| `claim_kind` | `decision` |
| `evidence_status` | `functional` |
| `evidence_level` | `E1` |
| `as_of` | `2026-07-29 / 44c87885`, 데모의 ScriptableObject·Sheet import 기반 콘텐츠 |
| `sources` | [TRD §3.3](../../TRD.md), [맵·웨이브 밸런싱](../../reference/map-wave-balancing.md), [점수 산식](../../reference/score-formula.md), [`Assets/_Project/Scripts/Data/`](../../../Assets/_Project/Scripts/Data/) |
| `related_commit_or_test` | 에셋·import·계산 테스트가 구현 사실을 뒷받침함. 배포 간 버전 호환 증거는 없음 |
| `transfer_action` | `adapt` |
| `production_impact` | 서버가 해석·실행하는 versioned canonical gameplay ruleset과 hash를 정본으로 두고 match에 고정한다. 클라이언트는 stable gameplay ID를 prefab·animation·VFX·SFX·UI·localization·camera·haptics 등으로 매핑하는 별도 presentation catalog를 소유하며, 사용한 catalog version/hash를 기록하고 ruleset과의 호환성을 검증한다. catalog 배포·pinning 방식은 ADR-CAND-007에서 결정한다. |
| `next_validation_or_decision` | ADR-CAND-007에서 schema, version/hash, 배포·rollback·호환 정책을 정한다. |

canonical gameplay ruleset에는 수치·산식, 비용·쿨다운, 타게팅·효과 의미, 웨이브·스폰, timer, gameplay RNG parameter, 승패·점수·보상 입력이 포함된다. presentation catalog의 누락이나 변경은 결과를 바꿀 수 없다.

### ENG-005 — “정의와 해석” 분리는 유지하되 Unity 참조를 제거한다

| field | value |
|---|---|
| `claim_kind` | `decision` |
| `evidence_status` | `functional` |
| `evidence_level` | `E1` |
| `as_of` | `2026-07-29 / 44c87885`, Dreamcatcher trigger × payload × modifier 구현 |
| `sources` | [Dreamcatcher 이식 가이드](../../reference/dreamcatcher-portability.md), [`DcMechanic.cs`](../../../Assets/_Project/Scripts/Data/Dreamcatcher/DcMechanic.cs), [`DreamcatcherCard.cs`](../../../Assets/_Project/Scripts/Data/Dreamcatcher/DreamcatcherCard.cs) |
| `related_commit_or_test` | [`DcTriggerTests.cs`](../../../Assets/_Project/Tests/EditMode/DcTriggerTests.cs) 및 관련 카드 규칙 테스트 |
| `transfer_action` | `adapt` |
| `production_impact` | trigger·payload·modifier 의미론과 미지원 조합의 명시적 거절은 유지한다. 실제 정의 schema는 prefab·ScriptableObject 참조가 없는 서버 해석 가능 형태로 다시 만든다. |
| `next_validation_or_decision` | ADR-CAND-002와 ADR-CAND-007에서 domain schema와 content schema의 경계를 정한다. |

현재 `DcMechanic`이 ECS를 직접 참조하지 않는다는 사실은 서버 독립성을 뜻하지 않는다. Unity 직렬화와 에셋 참조가 남아 있으므로 코드 복사보다 schema 재정의가 안전하다.

### ENG-006 — 수명주기와 완료 확인을 상태 전이의 일부로 취급한다

| field | value |
|---|---|
| `claim_kind` | `fact` |
| `evidence_status` | `functional` |
| `evidence_level` | `E1` |
| `as_of` | `2026-07-29 / 44c87885`, ECS queue drain/reset과 tournament attempt 마감 |
| `sources` | [tournament-flow-guards](../../spec/tournament-flow-guards/README.md), [`TournamentMatchReporter.cs`](../../../Assets/_Project/Scripts/Core/Api/TournamentMatchReporter.cs), [`PendingMatchStore.cs`](../../../Assets/_Project/Scripts/Core/Api/PendingMatchStore.cs), [TRD §2.5](../../TRD.md) |
| `related_commit_or_test` | [`TournamentMatchReporterTests.cs`](../../../Assets/_Project/Tests/EditMode/Api/TournamentMatchReporterTests.cs), [`PendingMatchStoreTests.cs`](../../../Assets/_Project/Tests/EditMode/Api/PendingMatchStoreTests.cs) |
| `transfer_action` | `carry` |
| `production_impact` | 요청 발행이 아니라 성공 확인을 기준으로 상태를 닫고, compare-and-clear·idempotency key·소유권 검증을 기본 계약으로 둔다. ECS native container dispose 규칙 자체는 이식하지 않는다. |
| `next_validation_or_decision` | ADR-CAND-009에서 match 종료 상태기계, 재시도, 멱등 종료와 복구 보존 기간을 결정한다. |

과거 `clear-at-send`는 전송 실패 시 복구 근거를 잃었다. 최신 `tournament-flow-guards` unit 9의 clear-on-success와 compare-and-clear가 이를 supersede한다. 이 실패 경험은 서버 권위 종료 설계의 직접 입력이다.

### ENG-007 — `BattleBridge`의 경계 역할은 포트로 분해한다

| field | value |
|---|---|
| `claim_kind` | `fact` |
| `evidence_status` | `functional` |
| `evidence_level` | `E1` |
| `as_of` | `2026-07-29 / 44c87885`, 데모의 유일 MonoBehaviour↔ECS gateway |
| `sources` | [TRD §2.1, §5.3](../../TRD.md), [`BattleBridge.cs`](../../../Assets/_Project/Scripts/Bridge/BattleBridge.cs), [`BattleBridge.Dreamcatcher.cs`](../../../Assets/_Project/Scripts/Bridge/BattleBridge.Dreamcatcher.cs) |
| `related_commit_or_test` | bridge 변환·흐름 관련 EditMode/PlayMode 테스트. 단일 클래스의 정규 온라인 확장성 증거는 없음 |
| `transfer_action` | `adapt` |
| `production_impact` | 하나의 `NetworkBattleBridge`로 치환하지 않는다. intent-only input command, replicated semantic state, presentation event, ruleset/presentation mapping, match lifecycle을 별도 포트로 나눈다. |
| `next_validation_or_decision` | ADR-CAND-002, ADR-CAND-005, ADR-CAND-006에서 각 포트의 소유자와 방향을 확정한다. |

데모에서는 하나의 gateway가 무분별한 ECS 접근을 막았다. 그러나 전투 생성, 상태 변환, 화면 이벤트, 점수, 로깅까지 모인 클래스의 크기와 책임은 정규 프로젝트에서 분리해야 할 신호다.

### ENG-008 — 데모 이벤트 채널은 도메인 이벤트와 프로토콜 이벤트로 재해석한다

| field | value |
|---|---|
| `claim_kind` | `decision` |
| `evidence_status` | `functional` |
| `evidence_level` | `E1` |
| `as_of` | `2026-07-29 / 44c87885`, ECS buffer·NativeQueue 기반 맥락 간 통신 |
| `sources` | [`CLAUDE.md` ECS 맥락 분리](../../../CLAUDE.md), [TRD §2.5](../../TRD.md), [`DefenderDeathEventsSingleton.cs`](../../../Assets/_Project/Scripts/Battle/Units/DefenderDeathEventsSingleton.cs), [`ProjectileHitEventsSingleton.cs`](../../../Assets/_Project/Scripts/Battle/Combat/Projectile/ProjectileHitEventsSingleton.cs) |
| `related_commit_or_test` | producer/consumer 구현과 기능 테스트. 네트워크 순서·중복·손실 계약은 없음 |
| `transfer_action` | `adapt` |
| `production_impact` | 서버 내부 domain event와 외부 replication/presentation event를 구분한다. 외부 event는 event ID·authoritative tick·stable gameplay ID·의미론적 결과를 전달하고 ordering, deduplication, reliability, visibility를 명시한다. prefab·animation·VFX·tween 같은 Unity 표현 타입이나 연출 지시는 protocol에 넣지 않는다. |
| `next_validation_or_decision` | ADR-CAND-005에서 command/event/snapshot schema와 전송 보장을 결정한다. |

클라이언트는 semantic outcome을 presentation catalog로 해석한다. animation·VFX callback은 authoritative gameplay state를 진행하거나 판정 시점을 결정할 수 없으며, 예측 연출과 서버 확정 연출의 부작용은 authoritative event ID로 중복 제거한다.

### ENG-009 — 클라이언트 로그는 진단 자료이지 권위 로그나 replay가 아니다

| field | value |
|---|---|
| `claim_kind` | `fact` |
| `evidence_status` | `instrumented` |
| `evidence_level` | `E1` |
| `as_of` | `2026-07-29 / 44c87885`, 클라이언트 `BattleLogger` snapshot을 complete debug body에 첨부하는 경로 |
| `sources` | [tournament-play-report](../../spec/tournament-play-report/README.md), [`BattleLogSchema.cs`](../../../Assets/_Project/Scripts/Logging/BattleLogSchema.cs), [`TournamentApi.cs`](../../../Assets/_Project/Scripts/Core/Api/TournamentApi.cs) |
| `related_commit_or_test` | API body·snapshot 자동 테스트와 기능 Play 기록. 서버 authoritative audit/replay 검증은 없음 |
| `transfer_action` | `adapt` |
| `production_impact` | server tick·match ID·actor ID·ruleset version/hash를 포함하는 authoritative audit event를 만들고, 클라이언트의 presentation version/hash와 input/command → authoritative outcome → 표시 event correlation을 연결한다. metric·trace·replay 입력은 목적별로 분리하며 Client telemetry를 권위 근거로 사용하지 않는다. |
| `next_validation_or_decision` | ADR-CAND-011에서 관측 보존·샘플링·개인정보·replay 범위를 결정한다. |

### ENG-010 — 데모의 시간 제어를 온라인 authoritative time과 분리한다

| field | value |
|---|---|
| `claim_kind` | `fact` |
| `evidence_status` | `functional` |
| `evidence_level` | `E1` |
| `as_of` | `2026-07-29 / 44c87885`, 로컬 `TimeManager`의 Battle-domain pause·slow motion |
| `sources` | [전투 시뮬 설계 원칙](../../reference/lessons/04-sim-design.md), [`TimeManager.cs`](../../../Assets/_Project/Scripts/Core/TimeControl/TimeManager.cs), [`BattleScaledRateManager.cs`](../../../Assets/_Project/Scripts/Battle/BattleScaledRateManager.cs) |
| `related_commit_or_test` | time-manager 관련 자동·기능 테스트. 다중 사용자 온라인 의미론은 미검증 |
| `transfer_action` | `adapt` |
| `production_impact` | server simulation clock은 클라이언트 UI pause와 분리한다. slow motion·선택 UI 중 서버 tick을 멈출지, command deadline을 둘지, 연출만 늦출지를 제품 규칙으로 확정해야 한다. |
| `next_validation_or_decision` | ADR-CAND-010에서 온라인 시간 제어 정책을 결정한다. |

## 버릴 구현과 보존할 계약

| 데모 구현 | 판정 | 보존할 것 | 버릴 것 |
|---|---|---|---|
| Units / Movement / Combat / Effects | `carry` 의미론 + `adapt` 구조 | 책임 분리, 단일 쓰기 소유권, 경계 테스트 | ECS 폴더·Component 소유 규칙의 문법적 복제 |
| `BattleBridge` | `adapt` | intent command와 semantic outcome 사이의 명시적 경계 | 단일 거대 gateway, 직접 ECS 변환, presentation callback에 의한 상태 진행 |
| `DynamicBuffer` / `NativeQueue` 이벤트 | `adapt` | producer/consumer, drain, ordering, lifecycle 계약 | 컨테이너 타입과 ECS singleton |
| ScriptableObject / Sheet import | `adapt` | 데이터 주도와 검증, stable gameplay ID | 클라이언트 로컬 에셋을 canonical truth로 취급, gameplay ruleset과 presentation catalog 혼합 |
| `Entity.Index` / `Entity.Version` tie-break | `drop` | 안정적인 total ordering 필요성 | runtime-local entity identity |
| `ISystem` / `SystemGroup` / `ECB` | `drop` | 명시적 phase와 structural transition 테스트 | Entities update·structural-change 구현 |
| ECS world·native container dispose | `drop` | idempotent teardown과 자원 소유권 | ECS 전용 생성·dispose 절차 |
| 클라이언트 점수 계산·제출 | `drop` | 점수 산식의 제품 의미와 test vector | 클라이언트 결과 권위 |

## 정규 프로젝트의 최소 공학 원칙

1. 게임 결과에 영향을 주는 규칙·설정·상태 전이와 점수의 정본·최종 쓰기는 서버가 소유한다.
2. 클라이언트 command는 행동 의도만 전달한다. 서버가 actor·state·ownership·permission·cost·cooldown·target·sequence·deadline·rate를 검증하고 결과를 계산한다.
3. protocol은 안정 ID·server tick·semantic state/outcome을 전달하며 Unity asset·animation·VFX 지시를 전달하지 않는다.
4. 클라이언트 prediction은 제한적·비권위·폐기 가능해야 하고, 서버 correction·resync가 항상 우선한다.
5. 모든 simulation-critical state에는 안정 ID, server tick, ruleset version이 붙고, 클라이언트 표시에는 호환되는 presentation version과 correlation이 붙는다.
6. 계산은 가능한 한 순수하게 분리하고, runtime이 다르면 동일 test vector로 서버 ruleset 의미론을 고정한다.
7. reconnect는 로컬 pending 마감이 아니라 snapshot 복구와 command deduplication 문제로 다룬다.
8. 로그는 진단 문자열이 아니라 audit·metric·trace·replay 목적별 schema로 관리한다.
9. 위 원칙의 구체 기술은 후보 상태이며, 승인 전에는 정규 프로젝트 구현을 허가하지 않는다.
