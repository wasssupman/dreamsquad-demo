# Product Learning Register

- 문서 상태: **Draft**
- 기준선: **2026-07-29 / `44c87885`**
- 대상 모드: 별도 표기가 없으면 로그인 계정으로 로비에서 시작하는 일반 토너먼트 흐름
- 목적: 데모에서 확인한 제품 사실·결정·가설을 분리하고 정규 프로젝트로 넘길 판단을 남긴다.

이 문서는 기능이 구현됐다는 사실과 재미가 검증됐다는 판단을 구분한다. 자동 테스트와 내부 Play는 동작 가능성을 보여주지만, 구조화된 플레이테스트 결과를 대신하지 않는다. 현재 저장소에는 설문·인터뷰 원문, 참가자별 반복 플레이 데이터셋, 분석 보고서가 없으므로 아래 재미 가설은 모두 미검증 상태다.

증거 등급과 상태 정의는 상위 [문서화 진입점](../README.md)의 공통 기록 계약을 따른다. 현재 데모의 실제 흐름은 [Demo Baseline](../demo-baseline.md), 출처의 최신성은 [Source Map](../source-map.md)에서 확인한다.

## LRN-PROD-001 — 과거 H1의 검증 대상은 현재 일반 흐름과 다르다

- `claim_kind`: `fact`
- `evidence_status`: `functional`
- `evidence_level`: `E1`
- `as_of`: 2026-07-29, 저장 스쿼드가 있는 일반 플레이. 드래프트 폴백·TestMode는 제외한다.
- 주장: 과거 PRD의 H1은 “10종 중 7종 드래프트 픽의 반복 개선”을 측정한다. 현재 일반 흐름은 저장한 최대 7개 스쿼드를 순서대로 반입하고, 전투 중에는 12장 순환형 드림캐쳐를 사용하므로 과거 H1을 그대로 측정하면 현재 핵심 의사결정을 대표하지 못한다.
- 이전 관계: superseded된 것은 과거 PRD의 10→7 draft 기반 H1이며, 현재 흐름의 새 검증 가설은 `LRN-PROD-002`로 재정의한다.
- 근거 경로:
  - [과거 PRD의 H1과 10→7 드래프트](../../PRD.md)
  - [현재 스쿼드 반입 계약](../../spec/squad-loadout/README.md)
  - [현재 드림캐쳐 순환 손패 계약](../../spec/dreamcatcher-awakening-hand/README.md)
- 관련 커밋: 기준선 `44c87885`; 스쿼드 결정화 `813ed7d`; 드림캐쳐 손패 완료 이력은 해당 spec의 상태·handoff 참조
- 관련 테스트:
  - `Assets/_Project/Tests/EditMode/SquadDrawTests.cs`
  - `Assets/_Project/Tests/EditMode/DreamcatcherCycleDeckTests.cs`
- 증거 산출물: 없음
- `redefined_as`: `LRN-PROD-002`
- `transfer_action`: `adapt`
- 정규 프로젝트 영향: PRD가 “드래프트 픽 수렴”을 핵심 가치로 전제하면 현재 데모에서 실제로 제공하는 준비·전투 의사결정과 어긋난다.
- 다음 검증·결정: 동일 콘텐츠 반복에서 스쿼드 편성, 드림캐쳐 덱·사용 순서, 배치와 웨이브 당기기가 어떻게 개선되는지를 하나의 새 H1로 확정한다.

## LRN-PROD-002 — 재정의 H1: 반복 플레이가 준비와 전투 판단을 개선한다

- `claim_kind`: `hypothesis`
- `evidence_status`: `untested`
- `evidence_level`: `E0`
- `as_of`: 2026-07-29, 같은 맵·웨이브 조건을 반복하는 정규 세션 후보
- 주장: 플레이어는 같은 전투 조건을 반복하면서 스쿼드 편성, 드림캐쳐 덱·사용, 배치, Next Wave 타이밍 중 적어도 하나를 의도적으로 바꾸고, 그 이유와 결과를 설명할 수 있으며 성과가 개선된다.
- 근거 경로:
  - [과거 H1 검증 의도](../../PRD.md)
  - [현재 스쿼드 구조](../../spec/squad-loadout/README.md)
  - [현재 드림캐쳐 구조](../../spec/dreamcatcher-awakening-hand/README.md)
  - [같은 맵·같은 웨이브 규칙](../../reference/map-wave-balancing.md)
- 관련 커밋: 기준선 `44c87885`
- 관련 테스트: 없음 — 위 테스트들은 결정적 입력과 순환 규칙만 확인하며 학습을 검증하지 않는다.
- 증거 산출물: 없음
- `transfer_action`: `retest`
- 정규 프로젝트 영향: 이 가설은 짧은 세션을 반복할 이유와 준비 화면의 제품 가치를 결정한다.
- 다음 검증·결정: `VAL-PROD-002`로 반복 플레이 실험을 수행하고, 단일 “정답 덱” 수렴이 아니라 의도 있는 판단 변화와 성과·이해도의 동반 개선을 평가한다.

## LRN-PROD-003 — 현재 자원 구조가 실시간 긴장감을 만든다는 근거는 없다

- `claim_kind`: `hypothesis`
- `evidence_status`: `untested`
- `evidence_level`: `E0`
- `as_of`: 2026-07-29, 배치 코스트와 각성 수치를 함께 사용하는 일반 전투
- 주장: 제한된 배치 코스트와 각성 수치의 획득·지출 타이밍이 “지금 쓸지 아낄지” 고민하게 만들고, 그 선택이 지루한 대기나 정답 행동이 아니라 의미 있는 긴장으로 인식된다.
- 근거 경로:
  - [과거 H2](../../PRD.md)
  - [현재 드림캐쳐 비용·순환 계약](../../spec/dreamcatcher-awakening-hand/README.md)
  - `Assets/_Project/Scripts/Core/CostRuntime.cs`
  - `Assets/_Project/Scripts/Core/Dreamcatcher/DreamcatcherHandController.cs`
- 관련 커밋: 기준선 `44c87885`
- 관련 테스트:
  - `Assets/_Project/Tests/EditMode/CostRuntimeTests.cs`
  - `Assets/_Project/Tests/EditMode/DreamcatcherCycleDeckTests.cs`
- 증거 산출물: 없음
- `transfer_action`: `retest`
- 정규 프로젝트 영향: 실시간 루프의 핵심 긴장과 자원 UI 우선순위, 밸런스 방향을 좌우한다.
- 다음 검증·결정: `VAL-PROD-003`에서 자원 포화 시간, 지출·보류 시점, 지불 불가 시도와 직후 설문을 함께 수집한다.

## LRN-PROD-004 — 패배 원인 귀인 가설은 결과 분해 구현만으로 검증되지 않는다

- `claim_kind`: `hypothesis`
- `evidence_status`: `untested`
- `evidence_level`: `E0`
- `as_of`: 2026-07-29, 패배 직후 결과 화면이 표시되는 일반 전투
- 주장: 플레이어는 패배 직후 운이나 막연한 난이도 탓에 머물지 않고, 준비 또는 전투 중 자신의 구체적 판단을 원인으로 지목할 수 있다.
- 근거 경로:
  - [과거 H3와 인터뷰 제안](../../PRD.md)
  - [현재 점수와 원시 상태 표시](../../reference/score-formula.md)
  - [결과 화면 시각 구조](../../spec/result-screen-visual-upgrade/README.md)
- 관련 커밋: 기준선 `44c87885`
- 관련 테스트: `Assets/_Project/Tests/EditMode/ScoreMathTests.cs`
- 증거 산출물: 없음
- `transfer_action`: `retest`
- 정규 프로젝트 영향: 패배 후 재도전 동기, 결과 화면 정보 구조, 복잡도 상한을 결정한다.
- 다음 검증·결정: `VAL-PROD-004`에서 결과 해설로 답을 유도하기 전에 오픈 질문을 먼저 하고, 답변과 실제 로그의 일치 여부를 별도로 코딩한다.

## LRN-PROD-005 — 비교 가능한 맵·웨이브 입력은 구현돼 있다

- `claim_kind`: `fact`
- `evidence_status`: `functional`
- `evidence_level`: `E1`
- `as_of`: 2026-07-29, 로그인 토너먼트 흐름에서 서버 seed를 받은 경우. 게스트·seed 미도착 폴백은 제외한다.
- 주장: 토너먼트 seed가 맵 풀 인덱스를 결정하고, 각 맵의 비영(非零) `waveSeed`가 해당 맵의 웨이브 구성을 고정한다. 이는 반복·비교 실험의 입력 통제 기반이지만, 전투 전체가 동일하게 재현되거나 공정성이 체감된다는 뜻은 아니다.
- 근거 경로:
  - [토너먼트 seed 맵 선택](../../spec/tournament-seed-map-select/README.md)
  - [맵·웨이브 결정론 규칙](../../reference/map-wave-balancing.md)
  - `Assets/_Project/Scripts/Data/MapGrid/MapPoolSelect.cs`
- 관련 커밋: 기준선 `44c87885`
- 관련 테스트:
  - `Assets/_Project/Tests/EditMode/MapPoolSelectTests.cs`
  - `Assets/_Project/Tests/EditMode/WavePatternGeneratorTests.cs`
  - `Assets/_Project/Tests/EditMode/Api/TournamentApiTests.cs`
- 증거 산출물: 없음
- `transfer_action`: `carry`
- 정규 프로젝트 영향: H1과 점수 비교 실험은 콘텐츠 입력을 고정한 상태에서 설계할 수 있다.
- 다음 검증·결정: 실험 데이터에 콘텐츠 버전·맵·웨이브 식별자를 반드시 포함하고, 전투 전체 재현성은 별도 아키텍처 결정으로 다룬다.

## LRN-PROD-006 — 점수 산식과 로그는 구현됐지만 배점의 타당성은 미검증이다

- `claim_kind`: `fact`
- `evidence_status`: `instrumented`
- `evidence_level`: `E1`
- `as_of`: 2026-07-29, 180초 일반 전투의 데모 기준값
- 주장: 데모 총점은 시간점수, 스트레스점수, 킬점수의 합으로 계산되고 결과 화면과 `score_events[]` 로그에 연결돼 있다. 현재 기준값은 초당 100, 스트레스 점당 900, 스트레스 한계 10이며, 이 숫자는 구현 기준값이지 정규 프로젝트의 균형 요구사항이 아니다.
- 근거 경로:
  - [점수 산식 레퍼런스](../../reference/score-formula.md)
  - [점수 설계와 열린 항목](../../spec/battle-score-formula/README.md)
  - `Assets/_Project/Scripts/Core/ScoreMath.cs`
  - `Assets/_Project/Scripts/Logging/BattleLogSchema.cs`
- 관련 커밋: 기준선 `44c87885`
- 관련 테스트:
  - `Assets/_Project/Tests/EditMode/ScoreMathTests.cs`
  - `Assets/_Project/Tests/EditMode/BattleLoggerSnapshotTests.cs`
  - `Assets/_Project/Tests/PlayMode/TallyFlowTest.cs`
- 증거 산출물: 구조화된 플레이테스트 데이터셋 없음
- `transfer_action`: `retest`
- 정규 프로젝트 영향: 점수가 지배전략과 토너먼트 행동을 유도하므로 산식·표시·콘텐츠 예산을 함께 검증해야 한다.
- 다음 검증·결정: `VAL-PROD-006`에서 점수 축 비중과 Next Wave 사용의 관계를 측정한 뒤 기준값을 새로 결정한다.

## LRN-PROD-007 — 행동형 튜토리얼은 기능 확인됐지만 학습 효과는 미검증이다

- `claim_kind`: `decision`
- `evidence_status`: `internal-observed`
- `evidence_level`: `E2`
- `as_of`: 2026-07-29, 신규 프로필의 첫 전투와 첫 복귀 흐름
- 주장: 데모는 긴 기능 투어보다 실제 배치 성공과 전투 시작을 진행 신호로 쓰고, 화면에는 한 번에 제한된 안내와 포커스를 보여주는 행동형 온보딩을 채택했다. 내부 Play에서 흐름 동작은 확인됐지만 신규 사용자의 이해·기억·이탈에는 근거가 없다.
- 근거 경로:
  - [첫 판 튜토리얼 계약](../../spec/first-session-tutorial/README.md)
  - [units 10~12 Play 확인과 테스트 공백](../../spec/first-session-tutorial/13_handoff_summary.md)
  - [아웃게임 튜토리얼 계약](../../spec/outgame-tutorial/README.md)
- 관련 커밋: `7a704a20`, `649991bb`, `815b38c4`; 기준선 `44c87885`
- 관련 테스트:
  - `Assets/_Project/Tests/PlayMode/FirstSessionTutorialSmokeTest.cs`
  - `Assets/_Project/Tests/EditMode/TutorialProgressTests.cs`
  - `Assets/_Project/Tests/EditMode/TutorialDragGuidanceTests.cs`
- 증거 산출물: 내부 Play 확인 기록은 spec handoff에만 존재하며 참가자별 관찰표는 없음
- `transfer_action`: `adapt`
- 정규 프로젝트 영향: “행동 → 반응 → 이해” 원칙은 유지 후보지만, 안내 단계·문구·노출 시점은 다시 검증해야 한다.
- 다음 검증·결정: `VAL-PROD-007`에서 안내 직후 성공뿐 아니라 다음 판의 무안내 행동 재현과 개념 회상을 확인한다.

## LRN-PROD-008 — 첫 판은 핵심 차별점의 일부를 의도적으로 숨긴다

- `claim_kind`: `fact`
- `evidence_status`: `functional`
- `evidence_level`: `E1`
- `as_of`: 2026-07-29, 신규 프로필의 첫 판과 두 번째 판
- 주장: 첫 판에는 각성 버튼과 드림캐쳐 사용이 숨겨지고 선물 연출도 억제된다. 두 번째 판부터 선물 덱 구성과 각성 손패가 단계적으로 노출된다.
- 근거 경로:
  - [첫 판 각성 봉인과 두 번째 판 선물 안내](../../spec/first-session-tutorial/README.md)
  - `Assets/_Project/Scripts/UI/Tutorial/FirstSessionTutorialController.cs`
  - `Assets/_Project/Scripts/UI/Dreamcatcher/AwakeningGaugeView.cs`
  - `Assets/_Project/Scripts/UI/Dreamcatcher/GiftPhaseView.cs`
- 관련 커밋: `9e75c0ae`, `7a704a20`; 기준선 `44c87885`
- 관련 테스트:
  - `Assets/_Project/Tests/EditMode/TutorialProgressTests.cs`
  - `Assets/_Project/Tests/PlayMode/FirstSessionTutorialSmokeTest.cs`
- 증거 산출물: 없음
- `transfer_action`: `retest`
- 정규 프로젝트 영향: 첫 판 이탈자가 드림캐쳐라는 차별점을 경험하지 못할 수 있으며, 반대로 조기 노출은 학습 부담을 키울 수 있다.
- 다음 검증·결정: `VAL-PROD-005`에서 현재 단계 노출과 제한적 첫 판 미리보기를 비교한다.

## LRN-PROD-009 — 드림캐쳐 순환 구조는 동작하지만 재미 판단은 내부 감상 수준이다

- `claim_kind`: `fact`
- `evidence_status`: `internal-observed`
- `evidence_level`: `E2`
- `as_of`: 2026-07-29, 각성이 열린 일반 전투
- 주장: 10장 세이브 덱과 2장 선물 카드, 앞 5장 손패, 사용·호스트 사망에 따른 순환, 각성 수치 비용과 슬로모가 구현돼 있다. spec에 “플레이 감각 좋음”이라는 내부 Play 메모가 있으나 구조화된 비교·관찰 결과는 아니다.
- 근거 경로:
  - [드림캐쳐 각성 손패 spec](../../spec/dreamcatcher-awakening-hand/README.md)
  - `Assets/_Project/Scripts/Core/Dreamcatcher/DreamcatcherCycleDeck.cs`
  - `Assets/_Project/Scripts/Core/Dreamcatcher/DreamcatcherHandController.cs`
- 관련 커밋: 해당 spec handoff 참조; 기준선 `44c87885`
- 관련 테스트:
  - `Assets/_Project/Tests/EditMode/DreamcatcherCycleDeckTests.cs`
  - `Assets/_Project/Tests/PlayMode/DreamcatcherAttachRequirementE2ETest.cs`
- 증거 산출물: 참가자·조건·관찰표가 있는 플레이테스트 산출물 없음
- `transfer_action`: `adapt`
- 정규 프로젝트 영향: 순환형 손패는 핵심 전투 가치 후보지만 사용 빈도, 손패 유효성, 자원 압박과 인지 부담을 제품 데이터로 다시 판단해야 한다.
- 다음 검증·결정: `VAL-PROD-002`와 `VAL-PROD-003`에서 구성 변화·사용 순서·보류 이유·무효 손패 시간을 수집한다.

## LRN-PROD-010 — 계측 기반은 있으나 현재 가설에 필요한 관측이 완전하지 않다

- `claim_kind`: `fact`
- `evidence_status`: `instrumented`
- `evidence_level`: `E1`
- `as_of`: 2026-07-29, 로컬 전투 로그와 완료 시점의 debug snapshot
- 주장: 전투 로그는 seed, 맵·웨이브, 스쿼드, 배치, 킬, 점수, 결과 등을 기록한다. 그러나 중도 이탈한 전체 세션 퍼널, 지불 불가 시도, 현재 순환 손패의 제안·보유·사용 이력, 결과를 보지 못한 판은 제품 가설 분석에 충분히 남지 않는다.
- 근거 경로:
  - `Assets/_Project/Scripts/Logging/BattleLogSchema.cs`
  - `Assets/_Project/Scripts/Logging/BattleLogger.cs`
  - [완료 시 debug snapshot 전송](../../spec/tournament-play-report/README.md)
  - [현재 손패 로그 통합이 후속 후보임을 명시](../../spec/dreamcatcher-awakening-hand/README.md)
- 관련 커밋: 기준선 `44c87885`
- 관련 테스트: `Assets/_Project/Tests/EditMode/BattleLoggerSnapshotTests.cs`
- 증거 산출물: 저장소에 커밋된 익명화 데이터셋·분석 결과 없음
- `transfer_action`: `adapt`
- 정규 프로젝트 영향: 구현 완료율과 실제 플레이 이탈·판단 품질을 혼동하지 않도록 제품 이벤트 계약을 PRD 성공 지표와 함께 정의해야 한다.
- 다음 검증·결정: 각 실험의 최소 이벤트와 누락 허용치를 `validation-backlog.md`에서 확정하고, 원시 개인정보 없이 실험별 evidence package로 보존한다.

## LRN-PROD-011 — Next Wave가 점수 지배전략이 될 가능성은 열려 있다

- `claim_kind`: `hypothesis`
- `evidence_status`: `untested`
- `evidence_level`: `E0`
- `as_of`: 2026-07-29, Next Wave를 제한 없이 사용할 수 있는 180초 일반 전투
- 주장: 남은 시간 점수가 최대 압축 여부에 따라 크게 달라지므로, 숙련 플레이가 다양한 준비·전투 판단보다 Next Wave 연타와 이를 버티는 단일 전략에 과도하게 수렴할 수 있다.
- 근거 경로:
  - [Next Wave에 따른 시간점수 도달 범위](../../spec/battle-score-formula/README.md)
  - [현재 점수 산식](../../reference/score-formula.md)
  - `Assets/_Project/Scripts/UI/NextWaveDock.cs`
- 관련 커밋: 기준선 `44c87885`
- 관련 테스트: `Assets/_Project/Tests/EditMode/WaveForceRescheduleTests.cs` — 스케줄 동작만 검증
- 증거 산출물: 없음
- `transfer_action`: `retest`
- 정규 프로젝트 영향: 지배전략이면 점수 경쟁이 전략 폭을 축소하고 맵·보스 연출의 체감 시간을 훼손할 수 있다.
- 다음 검증·결정: `VAL-PROD-006`에서 상위 점수와 Next Wave 횟수·시점의 상관, 전략 다양성, 보스 인지 여부를 함께 측정한다.

## LRN-PROD-012 — 온라인 권위 전환의 반응성과 결과 이해는 미검증이다

- `claim_kind`: `hypothesis`
- `evidence_status`: `untested`
- `evidence_level`: `E0`
- `as_of`: 2026-07-30, 정규 프로젝트의 첫 Server-authoritative 전투 vertical slice 후보. 정상 연결과 지연·손실·짧은 연결 중단 후 복귀 조건을 포함한다.
- 주장: 플레이어의 행동은 입력이 접수됐는지 알 수 있을 만큼 빠르게 화면에 반응하고, 이후 표시되는 결과는 최종 경기 결과와 일관돼야 한다. 임시로 보인 상태가 정정될 때도 플레이어는 무엇이 바뀌었는지 이해하며 이를 입력 무시나 부당한 결과 번복으로 받아들이지 않는다.
- 근거 경로:
  - [현재 Client 권위 기준선](../demo-baseline.md)
  - [Server-authoritative 이전 판정](../architecture/transition-matrix.md)
  - [제품 검증 항목](validation-backlog.md#val-prod-008--온라인-권위-전환의-반응성과-정정-이해)
- 관련 커밋: 기준선 `44c87885`
- 관련 테스트: 없음 — 데모의 기능 테스트와 내부 Play는 온라인 권위 전환 이후의 반응성·정정 이해를 검증하지 않는다.
- 증거 산출물: 없음
- `transfer_action`: `retest`
- 정규 프로젝트 영향: 이 가설이 실패하면 전투 조작감, 결과 신뢰, 중단 후 복귀 경험이 함께 훼손돼 핵심 루프 검증 결과도 왜곡될 수 있다.
- 다음 검증·결정: `VAL-PROD-008`에서 연결 조건과 행동 유형별 반응 시간, 정정·반전, 표시 불일치와 사용자 이해를 함께 측정한다.

## 이전 판단 요약

| 영역 | 현재 기록 | 정규 프로젝트 처리 |
|---|---|---|
| H1 | 과거 10→7 드래프트 가설은 현재 일반 흐름에 대해 `superseded` | 스쿼드·드림캐쳐·배치·웨이브 판단의 반복 개선으로 재정의 후 검증 |
| H2 | 자원 시스템은 구현됐으나 긴장감은 미검증 | 행동 로그와 직후 설문을 결합해 재검증 |
| H3 | 결과 분해 UI는 구현됐으나 패배 귀인은 미검증 | 비유도 오픈 인터뷰와 로그 대조 |
| 튜토리얼 | 행동형 진행과 단계 노출은 기능 확인 | 첫 판 이해, 다음 판 재현, 차별점 인지를 재검증 |
| 점수 | 산식·표시·로그 구현 | 현 수치를 요구사항으로 승격하지 않고 지배전략까지 재검증 |
| 관측 | 전투 로그 기반은 존재 | 전체 세션 퍼널과 현재 의사결정 로그를 보강 |
| 온라인 권위 경험 | Server-authoritative 전환 이후 반응성·정정 이해 근거 없음 | 첫 vertical slice에서 지연·손실·재접속 조건을 포함해 P0 재검증 |
