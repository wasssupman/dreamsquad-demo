# PRD Inputs

- 문서 상태: **Historical · stale · preparatory**
- 기준선: **2026-07-29 / `44c87885`**
- 대상 독자: Product, Client, Server
- 목적: 데모에서 정규 프로젝트 PRD로 넘길 플레이어 가치, 세션 루프, 요구사항, 성공 지표와 열린 질문을 정리한다.
- 비목적: 이 문서는 최종 PRD가 아니며 구현 구조, 서버 runtime, 전송 방식, 동기화·복제 해법을 결정하지 않는다.

> 이 문서는 2026-07-29 snapshot에서 출발한 입력 후보다. Product가 production-v1 범위를
> 결정하고 registry record를 current/reviewed로 만들기 전에는 공식 PRD input이 아니다.

제품 주장은 [Product Learning Register](learning-register.md), 검증 설계는 [Product Validation Backlog](validation-backlog.md), 현재 구현 사실은 [Demo Baseline](../demo-baseline.md)을 기준으로 한다. 기술 선택은 [ADR Candidates](../architecture/adr-candidates.md)에서 별도로 관리한다.

## 제품 전제

### PRD-IN-001 — 플레이어 가치 가설

- `claim_kind`: `hypothesis`
- `evidence_status`: `untested`
- `evidence_level`: `E0`
- `as_of`: 2026-07-29, 짧은 온라인 디펜스 세션의 정규 프로젝트 후보
- 입력: 플레이어는 전투 전 편성과 전투 중 타이밍 판단이 짧은 한 판 안에서 결과로 드러나고, 그 결과를 바탕으로 다음 판의 전략을 바꾸는 경험에서 가치를 느낀다.
- 근거 경로:
  - [과거 H1~H3](../../PRD.md)
  - [재정의된 현재 제품 학습](learning-register.md)
- 관련 커밋: 기준선 `44c87885`
- 관련 테스트: 없음 — 구현·자동 테스트는 플레이어 가치를 검증하지 않는다.
- 증거 산출물: 없음
- `transfer_action`: `retest`
- 정규 프로젝트 영향: 핵심 가치 문장, 우선순위, 반복 플레이 이유의 기준이 된다.
- 다음 검증·결정: `VAL-PROD-002`~`004` 결과로 가치 문장을 유지·축소·변경한다.

### PRD-IN-002 — 제품 수준 세션 루프

- `claim_kind`: `decision`
- `evidence_status`: `internal-observed`
- `evidence_level`: `E2`
- `as_of`: 2026-07-29, 로그인 계정의 데모 일반 흐름. 게스트·TestMode는 별도다.
- 입력: 정규 프로젝트의 PRD 초안은 아래 루프를 출발점으로 삼되, 각 단계의 체류시간과 실패·복귀 UX는 새로 결정한다.

```text
[로비·참가 준비]
  → [스쿼드·드림캐쳐 구성]
  → [경기 참가 확정·콘텐츠 확인]
  → [맵 확인·초기 배치]
  → [실시간 방어·자원 사용·웨이브 판단]
  → [승패·점수 분해·순위 확인]
  → [재도전 또는 편성 변경]
```

- 근거 경로:
  - [현재 outgame 흐름](../../spec/outgame-scene-and-flow/README.md)
  - [현재 스쿼드 반입](../../spec/squad-loadout/README.md)
  - [현재 토너먼트 참가·결과](../../spec/tournament-play-report/README.md)
  - [현재 점수와 결과](../../reference/score-formula.md)
- 관련 커밋: 기준선 `44c87885`
- 관련 테스트: 다수 기능 테스트가 있으나 전체 제품 퍼널을 검증하는 단일 테스트·플레이테스트 산출물은 없음
- 증거 산출물: 내부 spec의 Play 메모 외 구조화된 세션 관찰표 없음
- `transfer_action`: `adapt`
- 정규 프로젝트 영향: 화면·상태 목록이 아니라 플레이어가 경험하는 한 세션의 시작과 종료를 정의한다.
- 다음 검증·결정: `VAL-PROD-001`로 단계별 이탈과 시간을 측정하고, 목표 세션 길이와 재도전 지점을 PRD에서 확정한다.

## PRD 요구사항 입력

### PRD-IN-003 — 준비 선택과 전투 선택이 모두 결과에 의미 있게 기여해야 한다

- `claim_kind`: `hypothesis`
- `evidence_status`: `untested`
- `evidence_level`: `E0`
- `as_of`: 2026-07-29, 동일 콘텐츠 반복 세션
- 요구사항 입력: 플레이어는 스쿼드·드림캐쳐 구성과 배치·카드·Next Wave 판단 중 무엇이 결과를 바꿨는지 인식할 수 있어야 한다. 한 축이 나머지를 무의미하게 만드는 단일 지배전략이 되어서는 안 된다.
- 근거 경로:
  - [재정의 H1](learning-register.md#lrn-prod-002--재정의-h1-반복-플레이가-준비와-전투-판단을-개선한다)
  - [Next Wave 위험](learning-register.md#lrn-prod-011--next-wave가-점수-지배전략이-될-가능성은-열려-있다)
- 관련 커밋: 기준선 `44c87885`
- 관련 테스트: 없음
- 증거 산출물: 없음
- `transfer_action`: `retest`
- 정규 프로젝트 영향: 핵심 루프의 전략 폭과 편성 화면의 존재 이유를 결정한다.
- 다음 검증·결정: `VAL-PROD-002`, `VAL-PROD-006` 결과로 허용할 기여도와 전략 다양성 기준을 정한다.

### PRD-IN-004 — 실시간 자원 선택은 기다림이 아니라 긴장으로 읽혀야 한다

- `claim_kind`: `hypothesis`
- `evidence_status`: `untested`
- `evidence_level`: `E0`
- `as_of`: 2026-07-29, 배치 코스트와 각성 수치를 쓰는 전투
- 요구사항 입력: 플레이어는 자원을 지금 지출할지 보류할지 선택할 수 있고, 지출 결과와 다음 기회를 예측할 수 있어야 한다. 자원이 없어 아무것도 못 하는 시간과 정보 부족에 의한 보류는 의도한 긴장으로 간주하지 않는다.
- 근거 경로:
  - [자원 긴장감 학습](learning-register.md#lrn-prod-003--현재-자원-구조가-실시간-긴장감을-만든다는-근거는-없다)
  - [현재 드림캐쳐 비용 의도](../../spec/dreamcatcher-awakening-hand/README.md)
- 관련 커밋: 기준선 `44c87885`
- 관련 테스트: `Assets/_Project/Tests/EditMode/CostRuntimeTests.cs` — 기능 규칙만 확인
- 증거 산출물: 없음
- `transfer_action`: `retest`
- 정규 프로젝트 영향: 전투의 행동 밀도, HUD 정보 우선순위, 자원 수급·비용 밸런스에 영향을 준다.
- 다음 검증·결정: `VAL-PROD-003`의 행동 데이터와 직후 설문을 함께 사용해 목표 상태를 구체화한다.

### PRD-IN-005 — 결과는 다음 행동으로 이어질 만큼 설명 가능해야 한다

- `claim_kind`: `hypothesis`
- `evidence_status`: `untested`
- `evidence_level`: `E0`
- `as_of`: 2026-07-29, 승리·패배·시간 종료 결과
- 요구사항 입력: 결과 화면은 승패와 점수만 알리는 데 그치지 않고, 플레이어가 다음 판에 바꿀 준비·배치·자원·웨이브 판단을 하나 이상 고를 수 있게 해야 한다. 설명은 실제 경기 기록과 모순되지 않아야 한다.
- 근거 경로:
  - [패배 귀인 학습](learning-register.md#lrn-prod-004--패배-원인-귀인-가설은-결과-분해-구현만으로-검증되지-않는다)
  - [현재 점수 분해](../../reference/score-formula.md)
- 관련 커밋: 기준선 `44c87885`
- 관련 테스트: `Assets/_Project/Tests/EditMode/ScoreMathTests.cs` — 계산만 확인
- 증거 산출물: 없음
- `transfer_action`: `retest`
- 정규 프로젝트 영향: 패배의 공정성 인식과 재도전 의향, 결과 정보 구조를 결정한다.
- 다음 검증·결정: `VAL-PROD-004`에서 비유도 귀인과 로그 일치를 확인한 후 필요한 결과 설명의 깊이를 결정한다.

### PRD-IN-006 — 첫 세션은 자립 행동과 핵심 차별점 기대를 함께 만들어야 한다

- `claim_kind`: `decision`
- `evidence_status`: `untested`
- `evidence_level`: `E0`
- `as_of`: 2026-07-29, 신규 사용자의 첫 로비·첫 판·첫 복귀
- 요구사항 입력: 첫 세션은 최소한의 안내로 배치와 전투 시작을 직접 수행하게 하고, 사용자가 이 게임의 편성·드림캐쳐 차별점을 첫 세션 안에 인지하거나 다음 판 기대를 형성하게 해야 한다.
- 근거 경로:
  - [행동형 튜토리얼 학습](learning-register.md#lrn-prod-007--행동형-튜토리얼은-기능-확인됐지만-학습-효과는-미검증이다)
  - [첫 판 차별점 노출 공백](learning-register.md#lrn-prod-008--첫-판은-핵심-차별점의-일부를-의도적으로-숨긴다)
- 관련 커밋: `7a704a20`, `815b38c4`; 기준선 `44c87885`
- 관련 테스트: 튜토리얼 진행 기능 테스트는 있으나 이해·기억 테스트는 없음
- 증거 산출물: 없음
- `transfer_action`: `decide`
- 정규 프로젝트 영향: 첫 판 복잡도와 차별점 전달 사이의 균형, 단계 해금·튜토리얼 범위를 정한다.
- 다음 검증·결정: `VAL-PROD-005`, `VAL-PROD-007`로 단계 노출과 무안내 재현을 비교한 뒤 PRD 요구사항으로 승인한다.

### PRD-IN-007 — 경쟁 결과는 비교 가능하고 신뢰할 수 있어야 한다

- `claim_kind`: `decision`
- `evidence_status`: `untested`
- `evidence_level`: `E0`
- `as_of`: 2026-07-29, 로그인 토너먼트 흐름의 데모 기능 기준
- 요구사항 입력: 같은 경쟁 단위의 참가자는 비교 가능한 콘텐츠 조건을 받아야 하며, 표시된 결과·점수·순위가 최종 경기 결과와 일치해야 한다. 사용자에게 보인 성공과 실제 기록 상태가 다르면 명확히 알리고 복구 경로를 제공해야 한다.
- 근거 경로:
  - [현재 seed 기반 맵 배정](../../spec/tournament-seed-map-select/README.md)
  - [현재 play/complete와 랭킹](../../spec/tournament-play-report/README.md)
  - [현재 실패 가드](../../spec/tournament-flow-guards/README.md)
- 관련 커밋: 기준선 `44c87885`
- 관련 테스트(데모 기능 근거일 뿐 제품 신뢰 검증은 아님):
  - `Assets/_Project/Tests/EditMode/MapPoolSelectTests.cs`
  - `Assets/_Project/Tests/EditMode/Api/TournamentApiTests.cs`
  - `Assets/_Project/Tests/EditMode/Api/TournamentMatchReporterTests.cs`
- 증거 산출물: 기능 검증 기록은 각 spec에 있으나 참가자의 신뢰·공정성 인식 자료는 없음
- `transfer_action`: `adapt`
- 정규 프로젝트 영향: 경쟁의 신뢰, 고객지원 기준, 점수·순위 UX의 최소 품질을 정의한다.
- 다음 검증·결정: 정규 경쟁 단위, 동점 정책, 결과 확정 시점, 사용자에게 보여줄 오류·복구 상태를 PRD에서 확정한다.

### PRD-IN-008 — 중단·재접속·중복 입력은 플레이어 관점에서 일관되게 끝나야 한다

- `claim_kind`: `decision`
- `evidence_status`: `untested`
- `evidence_level`: `E0`
- `as_of`: 2026-07-29, 데모의 중단 판 정리와 전송 실패 처리 기준
- 요구사항 입력: 앱 중단, 연결 끊김, 재진입, 결과 전송 실패가 발생해도 플레이어는 현재 경기 상태, 재개 가능 여부, 최종 결과를 이해할 수 있어야 한다. 같은 경기가 중복 생성·종료되거나 성공처럼 보인 뒤 기록이 사라져서는 안 된다.
- 근거 경로:
  - [현재 pending attempt 정리](../../spec/abandoned-match-reconciliation/README.md)
  - [최신 clear-on-success 계약](../../spec/tournament-flow-guards/README.md)
  - [세션 토큰 갱신](../../spec/session-token-refresh/README.md)
- 관련 커밋: `44d24c01`, `eb67d5c5`; 기준선 `44c87885`
- 관련 테스트(데모 기능 근거일 뿐 제품 복구 경험 검증은 아님): `Assets/_Project/Tests/EditMode/Api/TournamentMatchReporterTests.cs`
- 증거 산출물: 실제 네트워크 중단 코호트의 제품 관찰 자료 없음
- `transfer_action`: `adapt`
- 정규 프로젝트 영향: 정상 완료율, 경쟁 신뢰, 재접속 UX와 이탈 후 복귀 정책을 좌우한다.
- 다음 검증·결정: 어떤 중단까지 재개하고 언제 기권·무효·확정 처리할지 제품 정책을 먼저 정한 뒤 세부 해법을 결정한다.

### PRD-IN-009 — 한 판의 짧음은 전체 세션과 재도전까지 포함해 정의해야 한다

- `claim_kind`: `hypothesis`
- `evidence_status`: `untested`
- `evidence_level`: `E0`
- `as_of`: 2026-07-29, 180초 전투를 포함한 로비→결과→다음 선택 전체
- 요구사항 입력: “짧은 세션”은 전투 제한시간만이 아니라 인증·준비·연출·정산·랭킹을 포함한 실제 체류시간으로 정의해야 한다. 첫 판과 재도전의 목표 시간을 분리한다.
- 근거 경로:
  - [현재 180초 점수·전투 기준](../../reference/score-formula.md)
  - [전체 세션 검증 항목](validation-backlog.md#val-prod-001--전체-세션-퍼널과-이탈)
- 관련 커밋: 기준선 `44c87885`
- 관련 테스트: 없음
- 증거 산출물: 없음
- `transfer_action`: `decide`
- 정규 프로젝트 영향: 핵심 가치 문구, 매칭·연출 허용 시간, 일일 이용 패턴과 성공 지표를 정한다.
- 다음 검증·결정: `VAL-PROD-001` 데이터로 첫 판·재도전 각각의 목표 중앙값과 상한을 PRD에 넣는다.

### PRD-IN-010 — 성공 지표에 필요한 관측 가능성이 제품 범위에 포함돼야 한다

- `claim_kind`: `decision`
- `evidence_status`: `instrumented`
- `evidence_level`: `E1`
- `as_of`: 2026-07-29, 데모의 로컬 전투 로그와 완료 snapshot
- 요구사항 입력: PRD의 핵심 성공 지표는 구현 후 수집 가능한 이벤트·속성·제외 기준까지 함께 정의해야 한다. 중도 이탈을 제외하거나 구현 완료를 재미 지표로 대체해서는 안 된다.
- 근거 경로:
  - [현재 계측과 공백](learning-register.md#lrn-prod-010--계측-기반은-있으나-현재-가설에-필요한-관측이-완전하지-않다)
  - [Validation Backlog의 공통 데이터 공백](validation-backlog.md#공통-데이터-공백)
  - `Assets/_Project/Scripts/Logging/BattleLogSchema.cs`
- 관련 커밋: 기준선 `44c87885`
- 관련 테스트: `Assets/_Project/Tests/EditMode/BattleLoggerSnapshotTests.cs`
- 증거 산출물: 저장소에 익명화 데이터셋·분석 보고서 없음
- `transfer_action`: `adapt`
- 정규 프로젝트 영향: PRD 승인 후 “측정할 수 없는 성공 기준”이 생기는 것을 막고, 제품·클라이언트·서버가 동일한 지표 정의를 공유하게 한다.
- 다음 검증·결정: 아래 지표마다 owner, 이벤트 정의, 보존 기간, 개인정보 제외 규칙, 목표값을 PRD 승인 전에 채운다.

### PRD-IN-011 — 행동은 빠르게 반응하고 최종 결과와 일관돼야 한다

- `claim_kind`: `decision`
- `evidence_status`: `untested`
- `evidence_level`: `E0`
- `as_of`: 2026-07-30, 연결 상태 변화와 중단 후 복귀를 포함하는 정규 온라인 전투
- 요구사항 입력: 플레이어의 조작은 행동이 접수됐는지 알 수 있을 만큼 빠르게 가시적 반응을 보여야 하고, 화면에 표시된 결과는 최종 경기 결과와 일관돼야 한다. 임시로 보인 상태가 정정될 때는 무엇이 바뀌었는지 이해할 수 있어야 하며, 입력 무시나 부당한 결과 번복으로 느껴져서는 안 된다.
- 근거 경로:
  - [온라인 권위 경험 학습](learning-register.md#lrn-prod-012--온라인-권위-전환의-반응성과-결과-이해는-미검증이다)
  - [온라인 전투 검증 항목](validation-backlog.md#val-prod-008--온라인-권위-전환의-반응성과-정정-이해)
- 관련 커밋: 기준선 `44c87885`
- 관련 테스트: 없음 — 자동 기능·정합성 테스트는 플레이어가 느끼는 반응성과 정정 이해를 검증하지 않는다.
- 증거 산출물: 없음
- `transfer_action`: `decide`
- 정규 프로젝트 영향: 전투 피드백 우선순위, 연결 상태가 나쁠 때의 안내, 결과 신뢰와 복귀 경험의 제품 품질 기준을 정한다.
- 다음 검증·결정: `VAL-PROD-008`과 `PRD-MET-010`을 사용해 행동별 허용 지연, 정정·반전, 표시 불일치와 사용자 안내 기준을 확정한다.

### PRD-IN-012 — Replay는 확정된 경기 진행과 핵심 인과를 신뢰할 수 있게 전달해야 한다

- `claim_kind`: `decision`
- `evidence_status`: `untested`
- `evidence_level`: `E0`
- `as_of`: 2026-07-30, 정규 프로젝트의 Replay. Spectator는 제공이 결정된 경우에만 같은 경험 원칙을 적용한다.
- 요구사항 입력: Replay 시청자는 최종 승패·점수, 주요 상태 변화와 결과를 만든 핵심 사건을 실제 확정된 경기와 일관되게 이해할 수 있어야 한다. 당시 Live player의 camera·UI·즉시 반응 화면을 그대로 재현하지 않아도 되지만, 연출 차이 때문에 다른 결과·사건 순서·인과로 오해하게 해서는 안 된다. 선택한 관점에서 공개되지 않아야 할 정보는 허용 시점 전에 보여서는 안 된다.
- 근거 경로:
  - [Replay 경험 학습](learning-register.md#lrn-prod-013--live-player와-replay의-presentation-차이를-같은-경기로-이해하는지는-미검증이다)
  - [Replay 경험 검증 항목](validation-backlog.md#val-prod-009--live-player와-replay의-동일-경기-인지와-신뢰)
- 관련 커밋: 기준선 `44c87885`
- 관련 테스트: 없음 — 경기 진행의 기능 정합성 테스트는 필요하지만 시청자의 동일 경기 인지·인과 이해·신뢰를 검증하지 않는다.
- 증거 산출물: 없음
- `transfer_action`: `decide`
- 정규 프로젝트 영향: Replay의 경기 검토·공유·학습 가치, 결과 신뢰와 핵심 사건의 연출 우선순위를 정한다. Spectator를 제공한다면 같은 경기 진행을 본다는 기대에도 적용된다.
- 다음 검증·결정: `VAL-PROD-009`와 `PRD-MET-011`로 이해와 신뢰를 검증하고, `PRD-Q-011`, `PRD-Q-012`에서 Replay 관점·정보 공개와 Spectator 범위를 확정한다.

## 성공 지표 후보

아래 목표값은 모두 `TBD`다. 데모 기준값이나 과거 PRD의 통과선을 자동 승계하지 않는다.

| ID | 지표 정의 | 기록 계약 | 근거·영향 | 다음 결정 |
|---|---|---|---|---|
| `PRD-MET-001` | 로비 노출부터 결과 확인까지의 전체 세션 완료율과 단계별 이탈률 | `claim_kind: hypothesis` · `evidence_status: untested` · `evidence_level: E0` · `as_of: 2026-07-29, 신규/재방문·정상/복구 분리` · 근거 커밋 `44c87885` · 테스트/산출물 없음 · `transfer_action: decide` | `VAL-PROD-001`; 전투에 도달하지 못한 사용자를 포함해야 핵심 루프 지표가 왜곡되지 않는다. | 목표 완료율, 단계별 허용 이탈, 제외 기준 |
| `PRD-MET-002` | 첫 판 전체 소요시간, 재도전 소요시간, 각 단계의 중앙값과 상위 지연 | `claim_kind: hypothesis` · `evidence_status: untested` · `evidence_level: E0` · `as_of: 2026-07-29, 전투 밖 시간 포함` · 근거 커밋 `44c87885` · 테스트/산출물 없음 · `transfer_action: decide` | `PRD-IN-009`; “3분 전투”와 실제 짧은 세션을 구분한다. | 첫 판·재도전 목표 시간과 허용 상한 |
| `PRD-MET-003` | 동일 콘텐츠 반복에서 의도 있는 전략 변경과 성과·설명 개선을 함께 보인 참가자 비율 | `claim_kind: hypothesis` · `evidence_status: untested` · `evidence_level: E0` · `as_of: 2026-07-29, 콘텐츠 버전 고정` · 근거 커밋 `44c87885` · 테스트/산출물 없음 · `transfer_action: retest` | `VAL-PROD-002`; 재정의 H1의 핵심 지표다. | 개선 최소치, 코딩 규칙, 반복 횟수 |
| `PRD-MET-004` | 자원 지출 고민 설문과 실제 보류·지출 패턴의 일치도, 자원 때문에 행동 불가한 시간 | `claim_kind: hypothesis` · `evidence_status: untested` · `evidence_level: E0` · `as_of: 2026-07-29, 배치·각성 자원 분리` · 근거 커밋 `44c87885` · 테스트/산출물 없음 · `transfer_action: retest` | `VAL-PROD-003`; 긴장과 기다림을 구분한다. | 설문 문항, 행동 분류, 허용 무행동 시간 |
| `PRD-MET-005` | 패배 직후 구체적 자기 판단을 지목하고 실제 로그와 부합한 답변 비율 | `claim_kind: hypothesis` · `evidence_status: untested` · `evidence_level: E0` · `as_of: 2026-07-29, 결과 설명 전 첫 답변` · 근거 경로 `docs/PRD.md`, 커밋 `44c87885` · 테스트/산출물 없음 · `transfer_action: retest` | `VAL-PROD-004`; 귀인 가능성과 결과 설명력을 함께 본다. | 목표 비율, 평가자 합의 기준 |
| `PRD-MET-006` | 첫 판 후 차별점 자유 회상, 두 번째 판 시작·완료, 첫 드림캐쳐 사용 성공 | `claim_kind: hypothesis` · `evidence_status: untested` · `evidence_level: E0` · `as_of: 2026-07-29, 신규 프로필` · 근거 커밋 `7a704a20`, `815b38c4` · 테스트/산출물 없음 · `transfer_action: retest` | `VAL-PROD-005`; 단계 노출이 이해와 이탈에 미치는 영향을 본다. | 첫 세션 안에 요구할 인지·행동 수준 |
| `PRD-MET-007` | Next Wave 사용 집중도, 점수 축 기여도, 상위 점수 세션의 전략 다양성 | `claim_kind: hypothesis` · `evidence_status: untested` · `evidence_level: E0` · `as_of: 2026-07-29, 동일 맵·웨이브` · 근거 커밋 `44c87885` · 기능 테스트만 존재 · 산출물 없음 · `transfer_action: retest` | `VAL-PROD-006`; 점수 지배전략과 콘텐츠 건너뛰기를 탐지한다. | 허용 기여 상한과 경고 기준 |
| `PRD-MET-008` | 안내 직후·다음 판·재방문에서 핵심 행동을 도움 없이 재현한 비율 | `claim_kind: hypothesis` · `evidence_status: untested` · `evidence_level: E0` · `as_of: 2026-07-29, 신규 사용자` · 근거 커밋 `7a704a20`, `815b38c4` · 기능 테스트만 존재 · 산출물 없음 · `transfer_action: retest` | `VAL-PROD-007`; 튜토리얼 완료와 학습을 구분한다. | 과제별 목표 성공률과 재측정 간격 |
| `PRD-MET-009` | 경기 참가 후 최종 결과 확인 성공률, 중복·고립 경기 비율, 중단 후 복귀 성공률 | `claim_kind: decision` · `evidence_status: untested` · `evidence_level: E0` · `as_of: 2026-07-29, 정상·연결중단 분리` · 근거 커밋 `44d24c01`, `eb67d5c5` · 기능 테스트만 존재 · 산출물 없음 · `transfer_action: decide` | `PRD-IN-007`, `PRD-IN-008`; 경쟁 신뢰의 제품 품질 지표다. | 결과 확정 SLA, 복구 목표, 사용자 고지 기준 |
| `PRD-MET-010` | 행동 입력부터 첫 가시 피드백까지 시간, 정정·반전 비율, 최종 경기 결과와 표시 결과의 불일치율·지속시간 | `claim_kind: hypothesis` · `evidence_status: untested` · `evidence_level: E0` · `as_of: 2026-07-30, 정상·지연·손실·재접속 및 행동 유형 분리` · 근거 커밋 `44c87885` · 테스트/산출물 없음 · `transfer_action: decide` | `PRD-IN-011`, `VAL-PROD-008`; 반응 속도만 줄이고 번복·불신을 늘리는 결과를 함께 탐지한다. | 행동·연결 조건별 목표 시간, 허용 정정·불일치와 이해도 문턱 |
| `PRD-MET-011` | Replay의 핵심 단서 인지율, 핵심 인과 설명 정확도, Live player와 같은 경기라는 인지율과 Replay 신뢰 응답 | `claim_kind: hypothesis` · `evidence_status: untested` · `evidence_level: E0` · `as_of: 2026-07-30, Replay 관점·공개 정책별 측정; Spectator는 도입 시 별도 분리` · 근거 커밋 `44c87885` · 테스트/산출물 없음 · `transfer_action: decide` | `PRD-IN-012`, `VAL-PROD-009`; 연출의 pixel/frame 동일성이 아니라 확정 진행과 핵심 인과를 이해·신뢰하는지 판단한다. 최종 결과·권위 사건 순서 일치와 숨은 정보 비노출은 목표값을 조정할 제품 가설이 아니라 선행 기술 gate다. | 핵심 사건·단서 목록, 관점별 공개 기준, 인지·이해·신뢰 목표 |

## 미검증 질문

다음 항목은 주장이 아니라 PRD 작성 전에 답해야 할 질문이다. 답이 정해지면 별도 `decision` 기록으로 승격한다.

| ID | 질문 | 주 책임 | 선행 증거·결정 |
|---|---|---|---|
| `PRD-Q-001` | 정규 프로젝트의 1차 플레이어와 주 이용 상황은 누구·언제인가? | Product | 타깃 인터뷰와 시장 가정 |
| `PRD-Q-002` | “짧은 세션”의 목표는 첫 판과 재도전 각각 몇 분인가? | Product | `VAL-PROD-001`, `PRD-MET-002` |
| `PRD-Q-003` | 반복 플레이의 1차 동기는 생존, 점수 경쟁, 편성 개선 중 무엇인가? | Product | `VAL-PROD-002`, `VAL-PROD-006` |
| `PRD-Q-004` | 경쟁 단위와 순위 확정 시점, 동점·중도 이탈 정책은 무엇인가? | Product | `PRD-IN-007`, `PRD-IN-008` |
| `PRD-Q-005` | Next Wave는 필수 실력 표현인가, 선택적 위험 조절인가, 제거 후보인가? | Product | `VAL-PROD-006` |
| `PRD-Q-006` | 드림캐쳐를 첫 판에 어디까지 노출해야 핵심 차별점과 학습 부담이 균형을 이루는가? | Product | `VAL-PROD-005`, `VAL-PROD-007` |
| `PRD-Q-007` | 중단된 경기는 어디까지 재개하고 언제 기권·무효·확정하는가? | Product | `PRD-IN-008` |
| `PRD-Q-008` | 정규 프로젝트의 스쿼드·드림캐쳐 획득·성장 범위가 한 세션 판단에 어떤 제약을 주는가? | Product | 정규 메타 진행 범위 |
| `PRD-Q-009` | 각 성공 지표의 목표값, 표본, 관측 기간, 제외 조건은 무엇인가? | Product + Data | `validation-backlog.md` |
| `PRD-Q-010` | 행동별로 허용할 가시 피드백 지연과 결과 정정·반전 기준은 무엇이며, 어떤 상태에서 어떤 안내가 필요한가? | Product + UX | `VAL-PROD-008`, `PRD-MET-010` |
| `PRD-Q-011` | Replay는 누구의 관점으로 어떤 정보를 언제 공개하며, 같은 경기로 이해시키기 위해 반드시 보존할 핵심 단서는 무엇인가? | Product + UX | `PRD-IN-012`, `VAL-PROD-009`, `PRD-MET-011` |
| `PRD-Q-012` | Spectator를 제공할 것인가? 제공한다면 누가 접근할 수 있고 어느 관점·지연·정보 공개 정책을 적용할 것인가? | Product | `PRD-IN-012`, `PRD-Q-011` |

## PRD에서 제외하고 별도로 결정할 것

- 서버 host/runtime과 배포 단위
- 전송 protocol, snapshot/delta, 예측·보간·재동기화 방식
- 고정 tick, 수치 표현, RNG stream과 결정론 정책
- non-ECS 전제에서의 Client·Server 내부 모듈 구조
- 콘텐츠 배포·서명·version 저장 형식
- 로그 수집·저장 기술, Replay 저장·재생과 조건부 Spectator 전달·지연 구현 방식

이 항목들은 제품 요구를 만족시키는 해법이며, 제품 요구 자체가 아니다. PRD에는 플레이어가 관찰할 결과와 성공 기준만 남기고 기술 선택은 ADR 후보에서 결정한다.

## PRD 작성 전 체크

- [ ] `PRD-Q-001`~`012` 중 1차 출시 범위를 바꾸는 질문에 owner와 기한이 있다.
- [ ] 핵심 가설마다 실행 가능한 validation 항목과 필요한 이벤트가 연결돼 있다.
- [ ] 데모의 수치·문구·화면 흐름이 근거 없이 정규 요구사항으로 승격되지 않았다.
- [ ] 기능 테스트나 내부 Play를 재미 검증으로 표현하지 않았다.
- [ ] 성공 지표마다 분모, 코호트, 제외 기준, 콘텐츠 버전, 목표값이 정의돼 있다.
- [ ] 경쟁 결과·중단·복구의 사용자 정책이 기술 해법보다 먼저 정의돼 있다.
- [ ] 반응성·정정 요구가 구현 방식이 아니라 플레이어가 관찰할 결과와 목표 문턱으로 정의돼 있다.
- [ ] Replay의 확정 진행·핵심 인과 요구와 당시 화면의 완전 재현 비목표가 구분돼 있고, Spectator는 조건부 범위로 남아 있다.
