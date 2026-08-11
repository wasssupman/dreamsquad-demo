# Product Validation Backlog

> **DORMANT · OWNER-GATED · NOT A DEMO BACKLOG.** 아래 항목은 실행 지시가 아니다. Project owner의 명시적 transition 활성화 전에는 계측·실험·UX 변경을 시작하거나 Demo 완료를 차단하지 않는다.

- 문서 상태: **Historical · stale · preparatory**
- 기준선: **2026-07-29 / `44c87885`**
- 목적: Demo pre-freeze와 production 구현 wave에서 다시 검증할 제품 가설, 필요한 데이터,
  판정 절차를 구분해 관리한다.

모든 항목은 실행 전 상태다. 자동 테스트나 내부 기능 Play는 실험 결과로 세지 않는다. 실험을 시작하기 전에 모집 조건, 표본, 성공 문턱, 제외 기준을 고정하고, 종료 후 익명화한 산출물을 [Evidence 규칙](../evidence/README.md)에 따라 연결한다.

우선순위는 실행 stage가 아니라 제품 위험도다. `P0`라도 production vertical slice가 필요한
항목은 Demo freeze 결과가 될 수 없다. Freeze 전에 고정하는 것은 실험 요구, protocol과 목표
문턱이며, 실행 결과는 지정된 production-side gate에 축적한다.

| validation_id | execution_stage | blocks_gate | required_artifact |
|---|---|---|---|
| `VAL-PROD-001` | `demo-pre-freeze` | `global-freeze` | 익명 세션 퍼널 dataset과 판정보고 |
| `VAL-PROD-002` | `demo-pre-freeze` | `global-freeze` | 반복 플레이 dataset·인터뷰 코딩 |
| `VAL-PROD-003` | `demo-pre-freeze` | `global-freeze` | 자원 시계열·관찰 보고 |
| `VAL-PROD-004` | `demo-pre-freeze` | `global-freeze` | 귀인 인터뷰·평가자 합의 |
| `VAL-PROD-005` | `demo-pre-freeze` | `global-freeze` | onboarding 비교 결과 |
| `VAL-PROD-006` | `demo-pre-freeze` | `global-freeze` | Next Wave·점수 민감도 분석 |
| `VAL-PROD-007` | `demo-pre-freeze` | `none` | 무안내 재현 관찰표 |
| `VAL-PROD-008` | `production-client-wave` | `client-stage-authoritative-feedback` | 첫 Server-authoritative vertical slice UX evidence |
| `VAL-PROD-009` | `production-release` | `release` | Replay progression parity와 제품 신뢰 evidence |

`global-freeze` 표시는 Product가 production-v1에 해당 가설을 포함할 때의 gate다. Product가
기능 자체를 scope에서 제외하면 그 결정을 manifest에 남기며, 나중에 Demo를 두 번째로
import하지 않는다.

우선순위는 다음과 같다.

- `P0`: 핵심 루프 또는 PRD의 성립 여부에 큰 위험이 있어 배정된 execution stage의 첫
  blocking gate 전에 실행한다.
- `P1`: 핵심 루프를 폐기할 정도는 아니지만 온보딩·밸런스·결과 UX의 방향을 정하므로 첫 제품 검증 빌드에서 실행한다.

## VAL-PROD-001 — 전체 세션 퍼널과 이탈

- 우선순위: `P0`
- 연결 학습: `LRN-PROD-010`
- `claim_kind`: `hypothesis`
- `evidence_status`: `untested`
- `evidence_level`: `E0`
- `as_of`: 2026-07-29, 로그인 계정의 로비 진입부터 결과 확인·로비 복귀까지. 신규/재방문, 정상/중단 세션을 분리한다.
- 가설: 플레이어는 인증, 준비, 매칭 진입, 배치, 전투, 정산·랭킹의 전체 흐름을 과도한 대기나 이해 불가 상태 없이 마치고 다음 판 선택 지점까지 도달한다.
- 근거 경로:
  - [현재 로그인 게이트](../../spec/outgame-login-gate/README.md)
  - [현재 play/complete·랭킹 흐름](../../spec/tournament-play-report/README.md)
  - [현재 입장·완료 실패 가드](../../spec/tournament-flow-guards/README.md)
  - [Demo Baseline](../demo-baseline.md)
- 관련 커밋: 기준선 `44c87885`
- 관련 테스트: 없음 — API·씬 흐름 테스트는 제품 퍼널과 체감 대기를 검증하지 않는다.
- 증거 산출물: 없음. 실행 후 `evidence/playtests/val-prod-001-<date>/` 등록
- `transfer_action`: `retest`
- 정규 프로젝트 영향: 전체 세션 이탈이 크면 전투 재미 데이터가 도달 사용자에 편향되고 “짧은 한 판” 가치도 성립하지 않는다.
- 다음 검증·결정: Product가 목표 완료율과 허용 대기시간을 사전 등록한 뒤 파일럿을 실행한다.

### 실험 설계

- 코호트: 첫 설치 사용자와 2회 이상 재방문 사용자를 분리한다. 게스트·개발자 직접 진입은 별도 진단 코호트로 둔다.
- 최소 이벤트: `lobby_visible`, `auth_complete`, `start_pressed`, `match_ready`, `battle_entered`, `first_placement`, `battle_started`, `battle_ended`, `result_visible`, `result_acknowledged`, `ranking_visible`, `lobby_returned`.
- 중단 사유: 사용자 종료, 앱 suspend/kill, 연결 실패, 인증 실패, 준비 중 포기, 전투 중 포기, 결과 전 이탈을 구분한다.
- 지표: 단계별 전환율, 단계별 중앙값·상위 지연, 완주율, 결과 확인율, 재도전 선택률, 중단 후 복귀 성공률.
- 관찰: 정량 이벤트와 함께 “지금 무엇을 기다리는가”, “다음에 무엇을 할 수 있는가”를 각 주요 단계에서 짧게 확인한다.
- 판정: 가장 큰 이탈 구간과 원인을 특정할 수 있어야 하며, 목표 미달이면 전투 밸런스 결론보다 퍼널 개선을 먼저 수행한다.

## VAL-PROD-002 — 재정의 H1: 반복 전략 학습

- 우선순위: `P0`
- 연결 학습: `LRN-PROD-001`, `LRN-PROD-002`, `LRN-PROD-005`, `LRN-PROD-009`
- `claim_kind`: `hypothesis`
- `evidence_status`: `untested`
- `evidence_level`: `E0`
- `as_of`: 2026-07-29, 참가자별 동일 맵·웨이브·콘텐츠 버전을 반복하는 세션
- 가설: 반복 플레이를 거치며 플레이어가 스쿼드, 드림캐쳐, 배치, Next Wave 중 하나 이상의 판단을 의도적으로 개선하고 그 변화가 성과 또는 설명 가능한 전략 이해의 개선으로 이어진다.
- 근거 경로:
  - [재정의된 H1](learning-register.md#lrn-prod-002--재정의-h1-반복-플레이가-준비와-전투-판단을-개선한다)
  - [과거 반복 측정 프로토콜](../../PRD.md)
  - [같은 맵·웨이브 규칙](../../reference/map-wave-balancing.md)
- 관련 커밋: 기준선 `44c87885`
- 관련 테스트: 없음 — `SquadDrawTests`와 `DreamcatcherCycleDeckTests`는 입력 규칙만 확인한다.
- 증거 산출물: 없음. 실행 후 `evidence/playtests/val-prod-002-<date>/` 등록
- `transfer_action`: `retest`
- 정규 프로젝트 영향: 반복 학습이 없으면 편성·덱·점수 경쟁이 장기 동기가 되기 어렵고 핵심 루프의 축소 또는 재설계가 필요하다.
- 다음 검증·결정: Product가 개선의 최소 크기와 “설명 가능한 변화” 코딩 규칙을 사전 확정한다.

### 실험 설계

- 시작안: 참가자 5명 이상, 권장 8~10명. 참가자마다 같은 조건 10회. 이는 과거 PRD의 출발점을 재사용한 것이며 실행 전 모집 편향과 피로도를 검토한다.
- 비교 단위: 첫 3회와 마지막 3회, 그리고 판단을 바꾼 직후의 국소 변화를 함께 본다.
- 수집:
  - 콘텐츠·규칙 버전, 맵·웨이브 ID
  - 스쿼드와 드림캐쳐 덱 스냅샷
  - 배치·재배치, 각성 획득·지출, 손패·사용·회수, Next Wave 시점
  - 결과, 점수 3축, 유출, 클리어 시각
  - 매 판 “무엇을 바꿨고 왜 바꿨는가” 한 문장
- 분석: 성과 추세, 판단 변화 전후 차이, 전략 다양성, 같은 실패 반복률을 본다. 특정 조합으로의 수렴만을 학습의 정의로 삼지 않는다.
- 판정:
  - `Go`: 다수 참가자에게 의도 있는 판단 변화와 성과·설명 개선이 함께 나타난다.
  - `Revise`: 성과는 오르지만 이유를 설명하지 못하거나, 설명은 개선되지만 게임 결과에 반영되지 않는다.
  - `Stop`: 반복해도 판단 변화가 없고 결과가 우연으로 인식된다.

## VAL-PROD-003 — 코스트·각성 지출의 긴장감

- 우선순위: `P0`
- 연결 학습: `LRN-PROD-003`, `LRN-PROD-010`
- `claim_kind`: `hypothesis`
- `evidence_status`: `untested`
- `evidence_level`: `E0`
- `as_of`: 2026-07-29, 배치 코스트와 각성 수치가 모두 활성화된 전투
- 가설: 플레이어는 지출과 보류 사이에서 의미 있는 고민을 하며, 자원 부족을 무작정 기다리는 시간이나 UI 오해로 경험하지 않는다.
- 근거 경로:
  - [과거 H2](../../PRD.md)
  - [현재 각성 비용과 수급 의도](../../spec/dreamcatcher-awakening-hand/README.md)
  - `Assets/_Project/Scripts/Core/CostRuntime.cs`
  - `Assets/_Project/Scripts/Logging/BattleLogSchema.cs`
- 관련 커밋: 기준선 `44c87885`
- 관련 테스트: `Assets/_Project/Tests/EditMode/CostRuntimeTests.cs` — 산술·상태 규칙만 확인
- 증거 산출물: 없음. 실행 후 `evidence/playtests/val-prod-003-<date>/` 등록
- `transfer_action`: `retest`
- 정규 프로젝트 영향: 자원 경험이 실패하면 전투의 실시간 선택 밀도와 배치·드림캐쳐 가치가 함께 약해진다.
- 다음 검증·결정: 지불 불가 시도와 자원 상한 체류를 관측할 수 있도록 데이터 계약을 보완한 뒤 테스트한다.

### 필요한 데이터와 질문

- 배치 코스트·각성 수치의 시계열, 획득·지출·취소·실패 사유
- 지불 가능한 선택지 수, 지불 불가 시도, 자원 상한 체류 시간, 손패에 사용 가능한 카드가 없는 시간
- 배치와 카드 사용 직전·직후의 전장 압력 대리값
- 직후 설문:
  - 언제 쓸지 고민했는가
  - 기다림과 선택 중 어느 쪽으로 느꼈는가
  - 쓰지 않은 이유가 계획, 불확실성, UI 인지 실패 중 무엇인가
- 관찰 인터뷰에서 “자원이 하나 더 있었다면 무엇을 했을지”를 물어 실제 선택지 인식을 확인한다.
- 판정은 설문 평균 하나로 끝내지 않고, 고민 응답과 실제 보류·지출 패턴이 일치하는지 본다.

## VAL-PROD-004 — 패배 귀인과 결과 설명력

- 우선순위: `P0`
- 연결 학습: `LRN-PROD-004`, `LRN-PROD-006`
- `claim_kind`: `hypothesis`
- `evidence_status`: `untested`
- `evidence_level`: `E0`
- `as_of`: 2026-07-29, 패배한 일반 전투. 첫 판과 재방문 판을 분리한다.
- 가설: 플레이어는 패배 원인을 자신의 구체적인 준비·배치·자원·웨이브 판단으로 지목하고, 그 설명은 실제 로그와 대체로 부합한다.
- 근거 경로:
  - [과거 H3](../../PRD.md)
  - [현재 결과의 원시 상태·점수 분해](../../reference/score-formula.md)
  - [현재 결과 화면](../../spec/result-screen-visual-upgrade/README.md)
- 관련 커밋: 기준선 `44c87885`
- 관련 테스트: 없음 — 점수 계산·표시 테스트는 귀인을 검증하지 않는다.
- 증거 산출물: 없음. 실행 후 `evidence/interviews/val-prod-004-<date>/` 등록
- `transfer_action`: `retest`
- 정규 프로젝트 영향: 귀인이 어려우면 재도전보다 운·불공정 인식이 커지고 시스템 복잡도를 줄이거나 피드백을 재구성해야 한다.
- 다음 검증·결정: 코딩 기준과 두 명 이상 평가자의 합의 절차, 목표 비율을 사전 등록한다.

### 인터뷰 순서

1. 결과 세부 설명을 읽기 전에 “방금 왜 졌다고 생각하나요?”라고 묻는다.
2. 추가 질문 없이 첫 답변을 `구체적 자기 판단`, `구체적 외부 요인`, `모호함`, `운·불공정`, `이해 불가`로 코딩한다.
3. 결과 분해를 본 뒤 무엇을 다음 판에 바꿀지 묻는다.
4. 실제 로그에서 지목한 시점·행동·유출과 부합하는지 별도 코딩한다.
5. 첫 답변과 결과 확인 후 답변의 차이를 기록해 결과 UI가 설명을 돕는지, 답을 주입하는지 구분한다.

과거 PRD의 “구체적 지목 60%”는 역사적 제안일 뿐 현재 판정선이 아니다. 현재 콘텐츠와 참가자 조건을 정한 뒤 새 문턱을 고정한다.

## VAL-PROD-005 — 첫 판 차별점 노출과 두 번째 판 도달

- 우선순위: `P0`
- 연결 학습: `LRN-PROD-008`, `LRN-PROD-009`
- `claim_kind`: `hypothesis`
- `evidence_status`: `untested`
- `evidence_level`: `E0`
- `as_of`: 2026-07-29, 신규 프로필의 첫 판부터 두 번째 판 각성·선물 노출까지
- 가설: 현재의 단계적 노출은 첫 판 인지 부담을 낮추면서도 플레이어가 두 번째 판까지 갈 충분한 기대를 만들고, 드림캐쳐를 게임의 차별점으로 이해하게 한다.
- 근거 경로:
  - [첫 판 각성 봉인·선물 연출 억제](../../spec/first-session-tutorial/README.md)
  - [현재 드림캐쳐 루프](../../spec/dreamcatcher-awakening-hand/README.md)
  - [아웃게임의 스쿼드·드림캐쳐 포커스](../../spec/outgame-tutorial/README.md)
- 관련 커밋: `9e75c0ae`, `7a704a20`, `815b38c4`; 기준선 `44c87885`
- 관련 테스트: `Assets/_Project/Tests/EditMode/TutorialProgressTests.cs` — 노출 조건만 확인
- 증거 산출물: 없음. 실행 후 `evidence/playtests/val-prod-005-<date>/` 등록
- `transfer_action`: `retest`
- 정규 프로젝트 영향: 첫 판만 플레이한 사용자가 핵심 차별점을 못 보면 획득·첫 세션 지표가 전투 기본기만 평가하게 된다.
- 다음 검증·결정: 현재 흐름과 첫 판에 제한적 드림캐쳐 예고를 주는 흐름의 비교안을 Product가 승인한다.

### 비교안과 측정

- A안: 현재처럼 첫 판에는 각성 사용과 선물 연출을 숨기고 두 번째 판에서 공개한다.
- B안: 첫 판의 배치 집중은 유지하되 드림캐쳐의 존재와 다음 판 기대만 짧게 예고한다. 강제 사용은 하지 않는다.
- 공통 측정:
  - 첫 판 종료 직후 게임의 고유한 요소 자유 회상
  - 스쿼드와 드림캐쳐의 역할에 대한 비유도 설명
  - 첫 판 완료율, 두 번째 판 시작·완료율
  - 두 번째 판 첫 카드 사용까지 시간과 오류
  - 안내가 많았다/핵심이 숨겨졌다라는 정성 코딩
- 판정: B안이 이해를 높여도 첫 판 완료·행동 자율성을 크게 해치면 채택하지 않는다.

## VAL-PROD-006 — Next Wave·점수 지배전략과 콘텐츠 체감

- 우선순위: `P0`
- 연결 학습: `LRN-PROD-006`, `LRN-PROD-011`
- `claim_kind`: `hypothesis`
- `evidence_status`: `untested`
- `evidence_level`: `E0`
- `as_of`: 2026-07-29, 현재 점수식과 제한 없는 Next Wave가 적용된 일반 전투
- 가설: Next Wave는 감당 가능한 위험을 스스로 올리는 실력 표현으로 쓰이며, 최고 점수를 위해 무조건 연타해야 하는 단일 지배전략이나 보스·맵 경험을 건너뛰는 수단이 되지 않는다.
- 근거 경로:
  - [시간점수 도달 범위와 현행 유지 결정](../../spec/battle-score-formula/README.md)
  - [점수 산식 레퍼런스](../../reference/score-formula.md)
  - [맵·웨이브 밸런싱](../../reference/map-wave-balancing.md)
- 관련 커밋: 기준선 `44c87885`
- 관련 테스트:
  - `Assets/_Project/Tests/EditMode/WaveForceRescheduleTests.cs`
  - `Assets/_Project/Tests/EditMode/ScoreMathTests.cs`
- 증거 산출물: 없음. 실행 후 `evidence/playtests/val-prod-006-<date>/` 등록
- `transfer_action`: `retest`
- 정규 프로젝트 영향: 점수 최적화가 전략 폭을 줄이면 순위 경쟁이 핵심 콘텐츠를 소비하는 방식을 왜곡한다.
- 다음 검증·결정: 콘텐츠별 점수 예산과 허용할 Next Wave 기여 상한을 데이터 확인 후 결정한다.

### 분석 계획

- 기록: Next Wave 횟수·시점·연속 입력, 동시에 살아 있는 적, 유출·사망, 보스 노출·생존 시간, 클리어 시각, 점수 3축.
- 비교: 동일 콘텐츠에서 상위 점수 세션과 중간 점수 세션의 Next Wave 패턴, 스쿼드·드림캐쳐 다양성을 비교한다.
- 민감도: Next Wave를 쓰지 않은 기준, 적당히 쓴 기준, 최대 압축 시나리오를 분리한다.
- 정성 질문: “왜 지금 눌렀는가”, “누르지 않을 이유가 있었는가”, “보스나 웨이브 차이를 기억하는가”.
- 경고 신호: 상위 점수 대부분이 초반 연속 입력 하나로 설명되거나, 다른 판단의 분산이 점수에 거의 반영되지 않는 경우.

## VAL-PROD-007 — 튜토리얼 이후의 무안내 재현

- 우선순위: `P1`
- 연결 학습: `LRN-PROD-007`, `LRN-PROD-008`
- `claim_kind`: `hypothesis`
- `evidence_status`: `untested`
- `evidence_level`: `E0`
- `as_of`: 2026-07-29, 신규 프로필의 첫 판 안내와 이후 무안내 판
- 가설: 플레이어는 행동형 안내 직후뿐 아니라 다음 판에서도 도움 없이 배치·전투 시작·각성 손패 열기·카드 사용을 재현하고, 각 행동의 목적을 설명할 수 있다.
- 근거 경로:
  - [첫 판 행동형 튜토리얼](../../spec/first-session-tutorial/README.md)
  - [첫 판 튜토리얼의 테스트 공백](../../spec/first-session-tutorial/13_handoff_summary.md)
  - [아웃게임 튜토리얼](../../spec/outgame-tutorial/README.md)
- 관련 커밋: `7a704a20`, `649991bb`, `815b38c4`; 기준선 `44c87885`
- 관련 테스트: `Assets/_Project/Tests/PlayMode/FirstSessionTutorialSmokeTest.cs` — 진행 상태와 게이트만 확인
- 증거 산출물: 없음. 실행 후 `evidence/playtests/val-prod-007-<date>/` 등록
- `transfer_action`: `retest`
- 정규 프로젝트 영향: 즉시 완료만 좋고 기억이 남지 않으면 튜토리얼이 진행 잠금 해제 절차에 머물며 재방문 마찰을 줄이지 못한다.
- 다음 검증·결정: 첫 판 직후와 24시간 내 재방문의 무안내 과제를 같은 정의로 측정한다.

### 과제와 지표

- 과제: 유닛 1회 배치, 전투 시작, 드림캐쳐 손패 열기, 사용 가능한 카드 1장 사용, 결과 후 로비에서 편성 화면 찾기.
- 지표: 첫 성공까지 시간, 잘못된 탭·취소·Skip, 도움 요청, 과제 성공률, 행동 목적 자유 설명.
- 관찰 시점: 안내 직후, 다음 판, 가능하면 24시간 내 재방문.
- 문구 평가는 선호도가 아니라 행동과 개념 재현에 미친 영향으로 판단한다.

## VAL-PROD-008 — 온라인 권위 전환의 반응성과 정정 이해

- 우선순위: `P0`
- `execution_stage`: `production-client-wave`
- `blocks_gate`: `client-stage-authoritative-feedback`
- 선행 조건: Game Server authoritative vertical slice와 통제 가능한 network test harness
- 연결 학습: `LRN-PROD-010`, `LRN-PROD-012`
- `claim_kind`: `hypothesis`
- `evidence_status`: `untested`
- `evidence_level`: `E0`
- `as_of`: 2026-07-30, 정규 프로젝트의 첫 Server-authoritative 전투 vertical slice. 정상 연결과 통제된 지연·손실·짧은 연결 중단 후 복귀 조건을 분리한다.
- 가설: 플레이어는 연결 상태가 나빠져도 행동이 접수됐는지 빠르게 이해하고, 화면에서 본 결과와 최종 경기 결과를 일관된 것으로 받아들인다. 표시가 정정되거나 되돌려질 때는 무엇이 바뀌었는지 이해하며 입력 무시나 불공정한 번복으로 해석하지 않는다.
- 근거 경로:
  - [온라인 권위 경험 학습](learning-register.md#lrn-prod-012--온라인-권위-전환의-반응성과-결과-이해는-미검증이다)
  - [반응성과 결과 정합성 요구사항 입력](prd-inputs.md#prd-in-011--행동은-빠르게-반응하고-최종-결과와-일관돼야-한다)
  - [중단·복귀 요구사항 입력](prd-inputs.md#prd-in-008--중단재접속중복-입력은-플레이어-관점에서-일관되게-끝나야-한다)
- 관련 커밋: 기준선 `44c87885`
- 관련 테스트: 없음 — 실행 환경의 자동 정합성 검사는 필요하지만 플레이어가 느끼는 반응성·정정 이해의 E3 근거를 대신하지 않는다.
- 증거 산출물: 없음. 실행 후 `evidence/playtests/val-prod-008-<date>/` 등록
- `transfer_action`: `retest`
- 정규 프로젝트 영향: 실패하면 온라인 전투의 조작감과 경쟁 결과 신뢰가 성립하지 않으므로, 콘텐츠·밸런스 결론보다 피드백과 복구 경험을 먼저 수정해야 한다.
- 다음 검증·결정: Product가 행동 유형·연결 조건별 허용 지연, 정정·반전, 표시 불일치와 이해도 문턱을 사전 등록한 뒤 첫 vertical slice에서 실행한다.

### 조건과 측정

- 조건: 정상 연결을 기준으로 지연, 패킷 손실, 짧은 연결 중단과 복귀를 각각 통제한다. 구체적인 구간과 반복 횟수는 실험 실행 전에 고정한다.
- 행동: 배치, 자원 지출, 드림캐쳐 사용, Next Wave처럼 결과와 비용을 즉시 이해해야 하는 대표 행동을 포함하고 성공·거절·정정 사례를 분리한다.
- 지표: 입력부터 첫 가시 피드백까지 시간, 정정·반전 비율, 최종 경기 결과와 표시 결과의 불일치율·지속시간, 복귀 후 현재 상태를 이해하기까지 시간.
- 관찰: 각 조건 직후 “입력이 처리됐다고 느꼈는가”, “화면이 왜 바뀌었는가”, “최종 결과를 믿을 수 있는가”를 비유도 질문으로 확인한다.
- 분석: 참가자와 행동 유형별로 정상 조건 대비 악화 폭을 비교하고, 빠른 피드백이 오히려 잦은 번복이나 결과 불신으로 이어지는지 함께 본다.
- 판정: 모든 목표 문턱은 실행 전에 확정한다. 자동 기록이 일치해도 참가자가 정정을 이해하지 못하면 통과로 보지 않는다.

## VAL-PROD-009 — Live player와 Replay의 동일 경기 인지와 신뢰

- 우선순위: `P1`
- `execution_stage`: `production-release`
- `blocks_gate`: `release`
- 선행 조건: production Authoritative Match Record와 Replay vertical slice
- 연결 학습: `LRN-PROD-013`
- `claim_kind`: `hypothesis`
- `evidence_status`: `untested`
- `evidence_level`: `E0`
- `as_of`: 2026-07-30, 동일한 완료 경기를 Live player 경험과 정규 프로젝트의 첫 Replay vertical slice에서 비교한다. Spectator는 제품 범위에 포함된 경우에만 추가한다.
- 가설: Live player와 Replay의 camera·UI·피드백 timing이 일부 달라도 참가자와 시청자는 확정된 결과, 핵심 사건과 인과를 같은 경기로 이해하고 Replay를 신뢰한다. 선택한 관점에서 공개되지 않아야 할 정보는 허용 시점 전에 드러나지 않는다.
- 근거 경로:
  - [Replay 경험 학습](learning-register.md#lrn-prod-013--live-player와-replay의-presentation-차이를-같은-경기로-이해하는지는-미검증이다)
  - [Replay 요구사항 입력](prd-inputs.md#prd-in-012--replay는-확정된-경기-진행과-핵심-인과를-신뢰할-수-있게-전달해야-한다)
  - [Replay 성공 지표 후보](prd-inputs.md#성공-지표-후보)
- 관련 커밋: 기준선 `44c87885`
- 관련 테스트: 없음 — 자동 비교는 경기 진행의 기능 정합성을 확인할 수 있지만 동일 경기 인지·인과 이해·신뢰의 E3 근거를 대신하지 않는다.
- 증거 산출물: 없음. 실행 후 `evidence/playtests/val-prod-009-<date>/` 등록
- `transfer_action`: `retest`
- 정규 프로젝트 영향: 실패하면 Replay를 경기 검토·공유·학습 경험으로 신뢰하기 어렵고, Spectator 도입 시에도 참가자와 시청자가 서로 다른 경기를 봤다고 느낄 수 있다.
- 다음 검증·결정: Product가 Replay 관점, 정보 공개 시점, 반드시 보존할 핵심 사건과 목표 문턱을 사전 등록한 뒤 실행한다. Spectator 제공 여부와 별도 정책은 `PRD-Q-012`에서 결정한다.

### 비교 조건과 측정

- 비교 단위: 동일한 완료 경기에서 Live player가 확인한 최종 결과·핵심 사건과 Replay에서 관찰한 내용을 대응시킨다. 당시 화면의 pixel/frame 동일성은 성공 기준으로 삼지 않는다.
- 조건: 정상 진행뿐 아니라 행동의 임시 표시가 확정·정정·거절된 사례, 주요 상태 변화, Replay의 pause·seek·rewind·배속 이후를 포함한다.
- 선행 기술 gate: Server progression signature와 Replay의 최종 상태·승패·점수는 `100%` 일치해야 한다. 같은 viewer role·policy에서 허용된 authoritative semantic event의 누락·추가·순서 오류와 숨은 정보 조기 노출은 `0`이어야 한다. 이는 조정 가능한 제품 목표나 E3 재미 근거가 아니다.
- 사전 등록: 비교할 핵심 사건·단서 목록, Replay 관점과 정보 공개 시점, 참가자·시청자 코호트와 제품 이해·신뢰 목표 문턱을 실행 전에 고정한다.
- 제품 지표: 핵심 단서 인지율, 같은 경기 인지율, 핵심 인과 설명 정확도와 Replay 신뢰 응답. 기술 gate 결과는 별도 표로 함께 보고하되 이 제품 가설과 합산하지 않는다.
- 관찰: “두 경험이 같은 경기라고 판단한 근거”, “달라 보인 부분”, “결과를 바꾼 사건”, “보면 안 된 정보를 보았는가”를 비유도 질문으로 확인한다.
- Spectator 조건: 기능 도입이 결정된 경우에만 동일 경기의 허용된 live/delayed 관전을 추가하고, 같은 관점·공개 정책의 완료 Replay와 핵심 사건 인지가 수렴하는지 비교한다.
- 판정: 경기 진행·결과가 다르면 제품 인식과 무관하게 실패다. 진행이 일치해도 핵심 인과를 이해하지 못하거나 Replay를 신뢰하지 않으면 제품 검증을 통과하지 않는다.

## 공통 데이터 공백

| 데이터 | 현재 데모 상태 | 실험 전 필요한 계약 |
|---|---|---|
| 전체 세션 퍼널 | 전투 완료 로그 중심 | 결과 전 이탈도 남는 단계 이벤트와 중단 사유 |
| 참가자·빌드·콘텐츠 버전 | 일부 seed·entry 정보 존재 | 익명 참가자 ID, 빌드, 규칙·콘텐츠 버전 고정 |
| 현재 드림캐쳐 순환 | 구 offer/pick 스키마와 현재 손패가 불완전하게 맞음 | 손패 스냅샷, 획득·사용·취소·회수·실패 사유 |
| 자원 고민 | 성공한 배치 비용은 기록 | 자원 시계열, 상한 체류, 지불 불가 시도, 보류 이유 |
| Next Wave | 웨이브 이벤트에 강제 여부 존재 | 입력 시점의 전장 압력, 연속 입력, 보스 노출 시간 |
| 튜토리얼 이해 | 진행 버전과 기능 테스트 존재 | 관찰표, 무안내 과제, 자유 회상, 재방문 결과 |
| 귀인 | 결과와 행동 로그 존재 | 비유도 인터뷰 원문, 코딩표, 평가자 합의 기록 |
| 반응성·정정 정합성 | 온라인 권위 전환 이후의 측정 자료 없음 | 입력 시각, 첫 가시 피드백, 최종 결과, 정정·복귀 시각을 같은 행동 단위로 연결 |
| Replay·조건부 Spectator 정합성 | Server-authoritative Replay의 비교 자료 없음 | 익명 경기 식별자, 관찰 모드·관점·공개 정책, 확정 사건과 표시된 사건의 연결, 규칙·연출 버전 |

실험별 원시 로그에 사용자 이름, 인증 토큰, 이메일, 기기 고유 식별자를 넣지 않는다. 저장소에는 익명화된 집계·코딩 결과와 재현에 필요한 메타데이터만 커밋한다.
