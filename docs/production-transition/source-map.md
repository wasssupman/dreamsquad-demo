# Source Map

> 상태: **Draft**
>
> 확인 기준: **2026-07-29 / `44c87885`**
>
> 목적: 각 출처가 증명할 수 있는 범위와 드리프트를 명시한다.

이 문서는 출처를 “최신 문서 하나”로 합치지 않는다. 구현 사실은 코드·에셋·테스트와 활성 spec을 우선하고, PRD·TRD·prototype·milestone은 의도와 역사 근거로 사용한다. 제품 효과는 별도 증거 산출물이 없으면 확인되지 않은 것으로 남긴다.

## 출처 레지스터

### 운영·역사 문서

| ID | 출처 | 역할과 신뢰 범위 | 기준일·상태 | 드리프트·주의 |
|---|---|---|---|---|
| SRC-GOV-001 | [`CLAUDE.md`](../../CLAUDE.md) | 이 데모 저장소의 현재 작업·ECS 경계 규칙 | 2026-07-29 확인 / active | “네트워크 코드 금지”는 데모 구현 규칙이다. 정규 프로젝트 목표 구조를 설명하지 않는다. |
| SRC-GOV-002 | [`docs/spec/README.md`](../spec/README.md) | 활성 spec 인덱스와 후속 후보 | 2026-07-29 확인 / active | 개별 기능의 상세 진실원은 각 spec README·작업 단위와 코드다. |
| SRC-HIST-001 | [`docs/PRD.md`](../PRD.md) | H1~H4의 원래 가설, 초기 검증 프로토콜과 의도 | 2026-04-15 계열 / Draft | 구 10→7 draft 흐름과 “네트워크 비목표” 등 현재 구현과 다른 부분이 있다. 정규 프로젝트 PRD가 아니다. |
| SRC-HIST-002 | [`docs/TRD.md`](../TRD.md) | 데모의 Hybrid ECS 선택과 prototype 제약의 역사 | prototype 문서 / legacy-active | 현재 데모 ECS 이해에는 유효하지만 정규 프로젝트에서 ECS를 선택하는 근거가 아니다. 일부 네트워크 금지 서술은 실제 API 연동보다 오래됐다. |
| SRC-HIST-003 | [`docs/prototype/`](../prototype/) | Phase 0~10 구현 목표·의사결정 아카이브 | prototype 종료 / archive | 완료 보고와 재미 검증 결과를 동일시하지 않는다. 외부 플레이테스트 게이트가 남은 기록이 있다. |
| SRC-HIST-004 | [`gameplay-design-summary.md`](../milestone/gameplay-design-summary.md) | 2026-05-08 기획·구현 스냅샷 | 2026-05-08 / historical | draft/redraft, 점수, Goal 등 후속 spec에 의해 바뀐 내용을 포함한다. |
| SRC-HIST-005 | [`gameplay-design-summary-quick.md`](../milestone/gameplay-design-summary-quick.md) | 위 스냅샷의 빠른 요약 | 2026-05-08 / historical | 현재 세션 흐름의 진실원으로 사용하지 않는다. |

### 현재 제품·세션 흐름 spec

| ID | 출처 | 증명 범위 | 기준일·신뢰 수준 | 한계 |
|---|---|---|---|---|
| SRC-FLOW-001 | [`outgame-login-gate`](../spec/outgame-login-gate/README.md) | Firebase 인증, 로그인 게이트, dev 우회 UI의 데모 계약 | 2026-07-29 확인 / E1 | 인증 서비스의 서버 내부 구현·보안 수준은 증명하지 않는다. |
| SRC-FLOW-002 | [`game-start-loadout-gate`](../spec/game-start-loadout-gate/README.md) | 저장 스쿼드·덱 유효성 확인과 전투 진입 게이트 | 2026-07-29 확인 / E1 | 선택이 재미있거나 이해된다는 증거가 아니다. |
| SRC-FLOW-003 | [`ingame-dreamcatcher`](../spec/ingame-dreamcatcher/README.md) | 인게임 드림캐쳐 선택·적용의 현재 토대 | 2026-07-29 확인 / E1 | 후속 Gift·튜토리얼 변경을 함께 읽어야 한다. |
| SRC-FLOW-004 | [`gift-phase`](../spec/gift-phase/README.md), [`gift-phase-presentation`](../spec/gift-phase-presentation/README.md) | 배치 전 Gift 구성과 표현 흐름 | 2026-07-29 확인 / E1·일부 E2 | 기능 Play는 서사·재미·이탈 개선의 근거가 아니다. |
| SRC-FLOW-005 | [`first-session-tutorial`](../spec/first-session-tutorial/README.md) | 첫 판 조건, 각성 lockout, 행동 기반 안내 | 2026-07-29 확인 / E1·일부 E2 | 인지·학습·재방문 효과는 구조화 검증되지 않았다. |
| SRC-FLOW-006 | [`wave-pattern`](../spec/wave-pattern/README.md) | seed 기반 웨이브 계획, briefing, Next Wave | 2026-07-29 확인 / E1 | 전체 전투의 byte-identical 결정론을 증명하지 않는다. |
| SRC-FLOW-007 | [`battle-score-formula`](../spec/battle-score-formula/README.md), [`score-formula.md`](../reference/score-formula.md) | 데모 점수 산식·표시와 현재 값 | 2026-07-29 확인 / E1 | 정규 프로젝트의 목표 배점이나 공정성 근거가 아니다. |
| SRC-FLOW-008 | [`time-manager`](../spec/time-manager/README.md) | 데모 전투·표현 시간 배율 계약 | 2026-07-29 확인 / E1 | 온라인에서 pause·slow motion이 가능하다는 근거가 아니다. |
| SRC-FLOW-009 | [`outgame-scene-and-flow`](../spec/outgame-scene-and-flow/README.md), [`squad-loadout`](../spec/squad-loadout/README.md) | 2-scene 로비와 저장 squad 기반 일반 진입 흐름 | 2026-07-29 확인 / E1 | prototype의 10→7 draft를 현재 일반 흐름으로 해석하지 않는다. |
| SRC-FLOW-010 | [`dreamcatcher-awakening-hand`](../spec/dreamcatcher-awakening-hand/README.md) | 10장 저장 deck+2장 Gift, 순환 hand와 Awakening 사용 | 2026-07-29 확인 / E1·일부 E2 | 내부 “플레이 감각” 메모는 구조화된 재미 근거가 아니다. |
| SRC-FLOW-011 | [`outgame-tutorial`](../spec/outgame-tutorial/README.md) | 첫 로비 Chapter A와 첫 복귀 Chapter B의 실제 행동 게이트 | 2026-07-29 확인 / E1·일부 E2 | 튜토리얼 완료가 이해·기억·두 번째 판 도달을 증명하지 않는다. |

### 인증·토너먼트·결과 spec

| ID | 출처 | 증명 범위 | 기준일·신뢰 수준 | 한계 |
|---|---|---|---|---|
| SRC-NET-001 | [`session-token-refresh`](../spec/session-token-refresh/README.md) | 세션 token 갱신과 401 retry의 클라이언트 계약 | 2026-07-29 확인 / E1 | 서버 session 정책 전체는 범위 밖이다. |
| SRC-NET-002 | [`tournament-play-report`](../spec/tournament-play-report/README.md) | `play` 시도 발급, `complete` 점수·battle log 보고, 결과·랭킹 UI | 2026-07-29 확인 / E1 | 클라이언트가 계산해 보낸 점수를 서버가 재시뮬레이션한다는 근거가 없다. |
| SRC-NET-003 | [`tournament-seed-map-select`](../spec/tournament-seed-map-select/README.md) | 서버 발급 seed의 map pool 선택 사용 | 2026-07-29 확인 / E1 | seed가 모든 gameplay RNG stream을 통제하지 않는다. |
| SRC-NET-004 | [`tournament-flow-guards`](../spec/tournament-flow-guards/README.md) | 진입 잠금, 오류 표시, pending attempt 정리 규칙 | 2026-07-29 확인 / E1 | 재접속·authoritative state resume 계약이 아니다. |
| SRC-NET-005 | [`abandoned-match-reconciliation`](../spec/abandoned-match-reconciliation/README.md) | 다음 lobby에서 미완료 attempt를 0점 종료하는 초기 복구 | 2026-07-29 확인 / E1 | unit 0의 clear-at-send 수명주기는 후속 flow guards unit 9가 대체했다. 온라인 재접속으로 해석하지 않는다. |
| SRC-NET-006 | [`tournament-history`](../spec/tournament-history/README.md) | 완료 토너먼트 목록·상세 ranking 조회 UI | 2026-07-29 확인 / E1 | ranking 계산과 부정행위 방지의 서버 내부 계약은 증명하지 않는다. |

### 참조·교훈

| ID | 출처 | 증명 범위 | 기준일·신뢰 수준 | 드리프트·주의 |
|---|---|---|---|---|
| SRC-REF-001 | [`map-wave-balancing.md`](../reference/map-wave-balancing.md) | 데모의 map·wave 조정 지점과 nonzero `waveSeed` 규칙 | 2026-07-29 확인 / E1 | “같은 map=같은 wave”이지 전체 매치 결정론이 아니다. 표의 `10/15` wave 표기는 현재 live `Deck_{6종}.asset`의 `15/15`와 드리프트가 있어 asset을 우선한다. |
| SRC-REF-002 | [`dreamcatcher-portability.md`](../reference/dreamcatcher-portability.md) | definition/interpreter 분리와 trigger×payload×modifier 의미론 | 2026-07-29 확인 / E1 | 현재 `DcMechanic`은 Unity asset·prefab 참조를 포함하므로 서버 공유 모델과 동일하지 않다. |
| SRC-REF-003 | [`lessons/04-sim-design.md`](../reference/lessons/04-sim-design.md) | 구조적 결정론·TimeManager에서 얻은 데모 교훈 | 2026-07-29 확인 / E0 (경험 기록) | 서버 tick·수치 정책의 승인된 결정이 아니다. |
| SRC-REF-004 | [`ingame-flow.md`](../reference/ingame-flow.md) | 2026-07-06 당시 스쿼드 기반 흐름 요약 | 2026-07-06 / historical | Gift, 최신 첫 판 tutorial과 일부 tournament guard를 반영하지 않는다. |

### 코드·자동 검증

| ID | 출처 | 증명 범위 | 기준일·신뢰 수준 | 한계 |
|---|---|---|---|---|
| SRC-CODE-001 | [`TournamentApi.cs`](../../Assets/_Project/Scripts/Core/Api/TournamentApi.cs), [`PendingMatchStore.cs`](../../Assets/_Project/Scripts/Core/Api/PendingMatchStore.cs) | client가 호출하는 tournament API와 로컬 pending attempt 저장 | `44c87885` / E1 | 서버 내부 저장·검증 구현은 이 저장소에 없다. |
| SRC-CODE-002 | [`BattleBridge.cs`](../../Assets/_Project/Scripts/Bridge/BattleBridge.cs) | Mono↔ECS 연결, 전투 lifecycle, client 결과 산출·보고 경로 | `44c87885` / E1 | 거대한 데모 gateway를 정규 구조로 이식하지 않는다. |
| SRC-CODE-003 | [`ScoreMath.cs`](../../Assets/_Project/Scripts/Core/ScoreMath.cs), [`ScoreMathTests.cs`](../../Assets/_Project/Tests/EditMode/ScoreMathTests.cs) | 데모 점수 계산 계약과 회귀 검증 | `44c87885` / E1 | 서버 권위·공정성 또는 재미를 증명하지 않는다. |
| SRC-CODE-004 | [`MatchSeed.cs`](../../Assets/_Project/Scripts/Core/MatchSeed.cs), [`MatchSeedTests.cs`](../../Assets/_Project/Tests/EditMode/MatchSeedTests.cs) | seed 파생 함수의 동작 | `44c87885` / E1 | `GenerateRandom()`과 별도 local RNG, 가변 delta까지 포함한 전체 결정론은 아니다. |
| SRC-CODE-005 | [`BattleLogger.cs`](../../Assets/_Project/Scripts/Logging/BattleLogger.cs), [`BattleLogSchema.cs`](../../Assets/_Project/Scripts/Logging/BattleLogSchema.cs) | 데모 로그 event·JSON 산출 경로 | `44c87885` / E1, `instrumented` | 저장소에 분석 가능한 구조화 결과셋이나 authoritative replay가 없다. |
| SRC-TEST-001 | [`DcTriggerTests.cs`](../../Assets/_Project/Tests/EditMode/DcTriggerTests.cs), [`ModifierMathTests.cs`](../../Assets/_Project/Tests/EditMode/ModifierMathTests.cs), [`WavePatternGeneratorTests.cs`](../../Assets/_Project/Tests/EditMode/WavePatternGeneratorTests.cs) | 이식 가능한 의미론과 순수 계산의 회귀 사례 | `44c87885` / E1 | ECS type이나 server runtime 선택을 지지하지 않는다. |

## 명시적 supersession

| 과거 기록 | `superseded_by` | 해석 |
|---|---|---|
| `docs/PRD.md`의 10→7 draft 기반 H1 | [`squad-loadout`](../spec/squad-loadout/README.md), [`ingame-dreamcatcher`](../spec/ingame-dreamcatcher/README.md), [`gift-phase`](../spec/gift-phase/README.md) | H1의 “반복 판단 학습” 의도는 남지만 현재 선택 구조로 다시 정의해야 한다. |
| 2026-05-08 milestone의 Draft/Redraft·구 점수·Goal 흐름 | 현재 flow·score·tournament spec | 역사 스냅샷으로만 유지한다. |
| `docs/reference/ingame-flow.md`의 pre-Gift 흐름 | Gift와 first-session tutorial spec | 최신 baseline은 여러 활성 spec을 조합해 재구성한다. |
| abandoned reconciliation unit 0의 전송 시 pending clear | [`tournament-flow-guards/9_clear_on_success.md`](../spec/tournament-flow-guards/9_clear_on_success.md) | 성공 응답 뒤 compare-and-clear가 현재 계약이다. |
| tournament 결과 화면의 Redraft 경로 | [`tournament-play-report/2_redraft_button_removal.md`](../spec/tournament-play-report/2_redraft_button_removal.md) | 현재 결과 흐름에서 Redraft는 제거됐다. |

## 근거 공백

- 저장소에서 구조화된 플레이테스트 보고서, 익명화 설문 결과, 인터뷰 coding table, 반복 플레이 분석 dataset을 확인하지 못했다.
- E3/E4 증거와 `supported`/`refuted` 상태의 주장은 이 1차 문서에 없다.
- 서버 코드 저장소가 범위에 없으므로 인증·tournament·ranking의 내부 구현, 점수 재검증, anti-cheat는 확인할 수 없다.
- 내부 “사용자 Play 확인” 기록은 기능·배선·시각 확인으로만 사용하며 제품 효과로 일반화하지 않는다.
- 이후 증거는 [`evidence/README.md`](evidence/README.md)의 익명화·보안 규칙을 따라야 한다.
