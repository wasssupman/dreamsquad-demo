# 데모 기준선

> 상태: **Historical · stale · preparatory** — 현재 freeze 후보가 아님
>
> 기준일: **2026-07-29**
>
> 기준 커밋: **`44c87885`**
>
> 대상: 일반 데모 플레이 경로. `TestMode`, 에디터의 `BattleScene` 직접 Play, `Endless`는 별도 조건으로 표시한다.

이 문서는 2026-07-29 당시 데모가 실제로 무엇을 했는지 기록한 역사적 snapshot이다.
현행 source와 대조해 registry에서 `current`로 재검토하기 전에는 공식 export할 수 없다.
구현 여부와 재미 검증 여부를 분리하며, 아래 `E2`는 내부 기능 Play를 뜻할 뿐 재미 가설의 지지를
뜻하지 않는다. 기록 필드와 판정 규칙은 [README](./README.md)의 공통 기록 계약을 따른다.

## 한눈에 보는 경계

| 영역 | 현재 권위/실행 위치 | 기준선 claim |
|---|---|---|
| Firebase 익명 인증과 게임 서버 사용자 식별 | Firebase + 게임 서버 | `BASE-004` |
| 토너먼트 attempt, seed, 결과 목록과 ranking | 게임 서버 | `BASE-004` |
| 미완료 attempt 표시·다음 로비의 0점 마감 | 클라이언트 로컬 저장 + 게임 서버 complete | `BASE-011` |
| 맵 선택 | 서버 seed를 받은 클라이언트 | `BASE-005` |
| 전투 상태, 명령 처리, 승패와 점수 계산 | 클라이언트 | `BASE-006` |
| 전투 로그 | 클라이언트 로컬 파일 + complete의 `debug` 문자열 | `BASE-009` |
| 전투 시뮬레이션 구현 | Unity Entities 기반 Hybrid ECS | `BASE-008` |

따라서 이 데모는 순수 오프라인 싱글 게임이 아니다. 정확한 표현은 **클라이언트 권위 전투 +
서버 인증·토너먼트 attempt·seed·결과 보고** 구조다.

## 현재 세션 흐름

| 구간 | 첫 판 | 첫 판 뒤 첫 로비 재방문/두 번째 판 | 일반 판 |
|---|---|---|---|
| 로비 | 로그인/guest 진입 뒤 Chapter A가 `START`를 지목 | Chapter B가 `SQUAD`와 `DREAMCATCHER`를 지목 | 저장된 loadout을 확인·편집하고 `START` |
| 입장 | 계정은 `/tournament/play` 성공과 attemptId+seed 수신 후 이동, guest는 즉시 이동 | 동일 | 동일 |
| Gift | 12장 덱은 만들지만 연출은 생략 | Gift 설명 홀드 2회가 최초 노출 | 일반 Gift 연출, 탭으로 단축 가능 |
| Placement | 첫 배치와 전투 시작을 행동으로 안내하고 클래스 설명을 노출 | 일반 배치 | 일반 배치 |
| Battle | Awakening 버튼·손패 표시를 막고 배치 중심으로 진행 | Awakening 3단계 힌트가 최초 노출될 수 있음 | 전체 전투 HUD와 Dreamcatcher 사용 |
| 종료 | `Battle → Tally → Result`; 계정은 client score를 complete로 보고 | 동일 | 동일 |

위 표는 `BASE-001`~`BASE-003`의 요약이다. 프로필 저장 실패나 참조 누락 시 튜토리얼은
플레이를 잠그지 않는 fail-open 경로를 갖기 때문에, 모든 사용자에게 반드시 같은 화면이 뜬다는
뜻은 아니다.

## Claim 기록

### BASE-001 — 공통 세션 골격

- `claim_kind`: `fact`
- `evidence_status`: `functional`
- `evidence_level`: `E2`
- `as_of`: `2026-07-29 @ 44c87885`
- 적용 모드·조건: 로비에서 유효한 7인 squad와 10장 Dreamcatcher deck으로 시작하는 일반 데모 경로.
  계정 세션은 서버 입장 게이트를 통과하고, guest는 서버 호출을 건너뛴다.
- 주장: 기본 흐름은 `Outgame → Gift → Placement → Battle → Tally → Result → Outgame`이다.
  저장 squad가 있으면 Draft를 건너뛰며, Gift에서 저장 deck 10장과 Lucid/Rim 선물 2장을 합친
  12장 cycle deck을 확정한다. `Tally`가 client 계산 점수를 연출한 뒤 결과 화면을 연다.
- 근거:
  - [GameManager.cs](../../Assets/_Project/Scripts/Core/GameManager.cs) — squad 우선 진입,
    `GamePhase`와 Draft fallback.
  - [Gift Phase spec](../spec/gift-phase/README.md) 및
    [GiftPhaseView.cs](../../Assets/_Project/Scripts/UI/Dreamcatcher/GiftPhaseView.cs) — Gift 삽입,
    10+2 구성과 Placement hand-off.
  - [BattleBridge.cs](../../Assets/_Project/Scripts/Bridge/BattleBridge.cs) — 종료 3종을 Tally와
    Result로 연결.
  - 테스트:
    [SquadCarryInSmokeTest.cs](../../Assets/_Project/Tests/PlayMode/SquadCarryInSmokeTest.cs),
    [TallyFlowTest.cs](../../Assets/_Project/Tests/PlayMode/TallyFlowTest.cs),
    [GiftDeckComposerTests.cs](../../Assets/_Project/Tests/EditMode/GiftDeckComposerTests.cs).
  - 관련 증거: Gift/튜토리얼/점수 spec의 내부 Play 기록. 구조화된 플레이테스트 산출물은 없음.
- `transfer_action`: `adapt`
- 정규 프로젝트 영향: Product의 세션 loop 후보로는 유지하되, 서버가 매치 상태를 소유하는
  입장·종료 상태 기계로 다시 정의해야 한다.
- 다음 검증·결정: 전체 세션 소요 시간과 단계별 이탈을 계측하고, Gift와 Tally가 짧은 세션의
  속도를 해치지 않는지 E3/E4로 검증한다.

### BASE-002 — 첫 판의 차별 흐름

- `claim_kind`: `fact`
- `evidence_status`: `functional`
- `evidence_level`: `E2`
- `as_of`: `2026-07-29 @ 44c87885`
- 적용 모드·조건: `PlayerProfileSO.IsLoadedThisSession=true`이고 core 및 lobby intro tutorial
  version이 미완료인 첫 로비/첫 매치. Skip 및 fail-open 경로는 제외.
- 주장: 첫 로비는 실제 `START` 버튼을 누르게 하는 Chapter A를 노출한다. 첫 매치는 Gift deck
  데이터 12장은 그대로 만들되 Gift 연출을 생략하고, `유닛 1회 배치 → 전투 시작`을 행동 신호로
  진행한다. 첫 매치 동안 Awakening 버튼을 숨겨 Dreamcatcher 사용을 봉인하며, 전투 진입 시
  core 완료를 저장한다.
- 근거:
  - [Outgame tutorial spec](../spec/outgame-tutorial/README.md) 및
    [OutgameTutorialController.cs](../../Assets/_Project/Scripts/UI/Outgame/Tutorial/OutgameTutorialController.cs).
  - [First-session tutorial spec](../spec/first-session-tutorial/README.md),
    [FirstSessionTutorialController.cs](../../Assets/_Project/Scripts/UI/Tutorial/FirstSessionTutorialController.cs),
    [GiftPhaseView.cs](../../Assets/_Project/Scripts/UI/Dreamcatcher/GiftPhaseView.cs).
  - 테스트:
    [TutorialProgressTests.cs](../../Assets/_Project/Tests/EditMode/TutorialProgressTests.cs),
    [FirstSessionTutorialSmokeTest.cs](../../Assets/_Project/Tests/PlayMode/FirstSessionTutorialSmokeTest.cs).
  - 관련 증거: spec에 사용자 Play 확인이 기록되어 있으나 신규 플레이어 이해도/재미를 측정한
    인터뷰·설문은 없음.
- `transfer_action`: `retest`
- 정규 프로젝트 영향: 첫 판이 데모의 핵심 차별점인 Dreamcatcher를 숨기므로, 첫 경험의 단순함과
  제품 정체성 노출 사이의 trade-off를 PRD 입력으로 다뤄야 한다.
- 다음 검증·결정: 신규 사용자 관찰에서 첫 판 종료 후 “무엇이 이 게임만의 선택이었는가”를
  회상하게 하고, 봉인 유지/부분 노출/즉시 노출 변형을 비교한다.

### BASE-003 — 첫 재방문과 일반 판의 차이

- `claim_kind`: `fact`
- `evidence_status`: `functional`
- `evidence_level`: `E2`
- `as_of`: `2026-07-29 @ 44c87885`
- 적용 모드·조건: core tutorial 완료 후 lobby loadout/gift/awakening tutorial version 중
  해당 항목이 미완료인 프로필. 모든 version이 완료된 뒤에는 일반 판.
- 주장: 첫 판 뒤 로비는 Chapter B에서 `SQUAD`와 `DREAMCATCHER` 버튼을 함께 지목하고 둘 중
  실제 버튼 클릭으로 완료한다. 그 다음 Gift가 처음 보이는 판에는 reveal과 shuffle 두 지점의
  설명 홀드가 있고, Battle에서는 Awakening 버튼 소개→사용 가능 알림→손패 사용 안내의 단계가
  최초 1회 진행될 수 있다. 이후 일반 판은 이 tutorial hold/lockout 없이 같은
  Gift→Placement→Battle loop를 반복한다.
- 근거:
  - [TutorialProgress.cs](../../Assets/_Project/Scripts/Core/Profile/TutorialProgress.cs) — chapter
    선행 조건과 version 완료 정책.
  - [OutgameTutorialController.cs](../../Assets/_Project/Scripts/UI/Outgame/Tutorial/OutgameTutorialController.cs),
    [GiftPhaseView.cs](../../Assets/_Project/Scripts/UI/Dreamcatcher/GiftPhaseView.cs),
    [FirstSessionTutorialController.cs](../../Assets/_Project/Scripts/UI/Tutorial/FirstSessionTutorialController.cs).
  - [First-session tutorial spec](../spec/first-session-tutorial/README.md) 및
    [Outgame tutorial spec](../spec/outgame-tutorial/README.md).
  - 테스트:
    [TutorialProgressTests.cs](../../Assets/_Project/Tests/EditMode/TutorialProgressTests.cs),
    [GiftDeckComposerTests.cs](../../Assets/_Project/Tests/EditMode/GiftDeckComposerTests.cs).
  - 관련 증거: 노출/동작 확인만 존재. 다음 판 무안내 수행률과 이해도 데이터 없음.
- `transfer_action`: `retest`
- 정규 프로젝트 영향: “첫 판”, “첫 복귀”, “일반 판”을 한 세션 평균으로 섞지 않고 서로 다른
  cohort/퍼널로 분석해야 한다.
- 다음 검증·결정: 두 번째 판 시작률, Chapter B 이후 loadout 편집률, 무안내 배치·Awakening
  사용 성공률을 별도 측정한다.

### BASE-004 — 인증과 토너먼트 attempt 수명주기

- `claim_kind`: `fact`
- `evidence_status`: `functional`
- `evidence_level`: `E2`
- `as_of`: `2026-07-29 @ 44c87885`
- 적용 모드·조건: Firebase 익명 계정 또는 복구 가능한 account session. guest는 모든 tournament
  API와 history를 건너뛴다.
- 주장: client는 Firebase Auth REST로 익명 계정의 `idToken`/`refreshToken`을 얻고 게임 서버
  `/user/sign/in`으로 사용자 세션을 만든다. 로비 `START`는 `/tournament/play`가
  `attemptId+seed`를 반환해야 BattleScene으로 이동한다. 정상 종료는 client score와 battle log를
  `/tournament/complete/{attemptId}/{score}`에 보내고, 성공 뒤 tournament result를 조회해
  ranking을 표시한다. 401/403에는 Firebase token refresh 후 요청을 1회 재시도한다.
- 근거:
  - [Outgame login gate spec](../spec/outgame-login-gate/README.md),
    [Session token refresh spec](../spec/session-token-refresh/README.md),
    [Tournament play/report spec](../spec/tournament-play-report/README.md),
    [Tournament flow guards spec](../spec/tournament-flow-guards/README.md).
  - [FirebaseAuthRestClient.cs](../../Assets/_Project/Scripts/Core/Api/FirebaseAuthRestClient.cs),
    [LoginPanelView.cs](../../Assets/_Project/Scripts/UI/Outgame/LoginPanelView.cs),
    [TournamentApi.cs](../../Assets/_Project/Scripts/Core/Api/TournamentApi.cs),
    [TournamentMatchReporter.cs](../../Assets/_Project/Scripts/Core/Api/TournamentMatchReporter.cs).
  - 테스트:
    [AuthE2ETest.cs](../../Assets/_Project/Tests/PlayMode/AuthE2ETest.cs),
    [TournamentApiTests.cs](../../Assets/_Project/Tests/EditMode/Api/TournamentApiTests.cs),
    [TournamentMatchReporterTests.cs](../../Assets/_Project/Tests/EditMode/Api/TournamentMatchReporterTests.cs),
    [UserSessionRefreshTests.cs](../../Assets/_Project/Tests/EditMode/Api/UserSessionRefreshTests.cs).
  - 관련 증거: spec에 dev server 왕복과 Unity Play 확인이 기록됨. 운영 부하·장기 안정성 자료 없음.
- `transfer_action`: `adapt`
- 정규 프로젝트 영향: 인증과 match lifecycle 경험은 재사용 가능하지만, 정규 프로젝트에서는
  server-authoritative match session과 client connection state를 분리해 모델링해야 한다.
- 다음 검증·결정: server runtime/host, session hand-off, 장애 재시도, idempotency와 만료 정책을
  ADR 후보에서 결정한다.

### BASE-005 — 서버 seed가 보장하는 범위

- `claim_kind`: `fact`
- `evidence_status`: `functional`
- `evidence_level`: `E2`
- `as_of`: `2026-07-29 @ 44c87885`
- 적용 모드·조건: 계정 사용자가 로비 게이트를 통과하고 `tournament.seed`를 받은 일반 매치.
  guest, 직접 BattleScene Play, TestMode, seed 미수신은 map pool 0번으로 fallback한다.
- 주장: server seed는 `seed % mapPool.Count`로 6개 map/deck pair 중 하나를 고른다. 각 deck의
  고정 non-zero `waveSeed` 때문에 같은 map은 같은 wave 구성이다. 이는 **맵과 웨이브 배정의
  결정론**이지, client 전투 전체의 fixed-tick 재시뮬레이션이나 byte-identical determinism을
  의미하지 않는다.
- 근거:
  - [Tournament seed map select spec](../spec/tournament-seed-map-select/README.md),
    [Map/wave balancing reference](../reference/map-wave-balancing.md).
  - [MapPoolSelect.cs](../../Assets/_Project/Scripts/Data/MapGrid/MapPoolSelect.cs),
    [MapDocumentPool.asset](../../Assets/_Project/Data/Maps/MapDocumentPool.asset),
    [Deck_Serpent.asset](../../Assets/_Project/Scripts/Data/Decks/Deck_Serpent.asset) 등
    `Deck_{Serpent,Coil,Twin,Spiral,Zig,Hook}.asset`.
  - 테스트:
    [MapPoolSelectTests.cs](../../Assets/_Project/Tests/EditMode/MapPoolSelectTests.cs),
    [WavePatternGeneratorTests.cs](../../Assets/_Project/Tests/EditMode/WavePatternGeneratorTests.cs).
  - 관련 증거: seed 기반 map 선택의 사용자 Play 확인. full battle replay 대조 자료 없음.
- `transfer_action`: `adapt`
- 정규 프로젝트 영향: 안정 ID, server fixed tick, RNG stream, numeric policy와 canonical content
  version 없이는 authoritative replay/re-simulation 근거로 사용할 수 없다.
- 다음 검증·결정: seed를 gameplay RNG root로 확장할지, 각 RNG stream을 어떻게 분리·기록할지,
  server snapshot에 어떤 config hash를 포함할지 결정한다.

### BASE-006 — 전투·승패·점수는 client 권위

- `claim_kind`: `fact`
- `evidence_status`: `functional`
- `evidence_level`: `E1`
- `as_of`: `2026-07-29 @ 44c87885`
- 적용 모드·조건: 일반 비-Endless 전투. 서버에 제출되는 계정 매치와 로컬 guest 매치 모두
  동일한 client simulation/score path를 사용한다.
- 주장: 적 이동·공격·상태·승패는 client ECS world에서 계산되고, 최종 점수는 client의
  `ScoreMath.Evaluate`가 산출한다. client는 계산 결과와 client가 만든 battle log를 complete에
  제출한다. 서버가 명령을 검증하거나 battle state를 재현해 점수를 재검산하는 구현은 저장소에
  없다. 따라서 ranking에 쓰이는 제출값의 출처도 현재는 client다.
- 근거:
  - [BattleBridge.cs](../../Assets/_Project/Scripts/Bridge/BattleBridge.cs) — client 승패 판정,
    score 산출, `ReportMatchResult`.
  - [ScoreMath.cs](../../Assets/_Project/Scripts/Core/ScoreMath.cs) 및
    [TournamentMatchReporter.cs](../../Assets/_Project/Scripts/Core/Api/TournamentMatchReporter.cs).
  - [Battle score formula spec](../spec/battle-score-formula/README.md) — “점수 재검증/무효 플래그”가
    후속 후보이며 fixed timestep을 선결 조건으로 기록.
  - 테스트:
    [ScoreMathTests.cs](../../Assets/_Project/Tests/EditMode/ScoreMathTests.cs),
    [TallyFlowTest.cs](../../Assets/_Project/Tests/PlayMode/TallyFlowTest.cs).
  - 관련 커밋/증거: 기준 커밋 `44c87885`; server-side simulation/revalidation 산출물 없음.
- `transfer_action`: `drop`
- 정규 프로젝트 영향: client-authoritative battle/score를 유지하면 공정성, 치트 방지,
  재접속 복구와 dispute audit 요구를 충족할 수 없다. 정규 프로젝트에서는 Server가 gameplay
  rule·canonical config·battle state·상태 전이·판정·score를 소유하고, Client는 행동 의도만
  제출한 뒤 Server의 stable ID 기반 semantic outcome을 presentation으로 해석해야 한다.
- 다음 검증·결정: client command validation, server scoring, 결과 서명, audit/replay의 최소
  보존 범위를 ADR로 결정한다.

### BASE-007 — 현재 콘텐츠·밸런스 기준값

- `claim_kind`: `fact`
- `evidence_status`: `functional`
- `evidence_level`: `E1`
- `as_of`: `2026-07-29 @ 44c87885`
- 적용 모드·조건: 6개 일반 map pool deck. `Deck_Endless`, legacy `WaveA/WaveB`,
  dev override는 제외.
- 주장:
  - loadout은 squad 7명과 저장 Dreamcatcher deck 정확히 10장을 요구한다.
  - 일반 map pool은 6개 map/deck pair이고 각 live deck은 wave 15개, wave당 적 6→10,
    boss interval 5, spawn lead-in 2초, 적 간격 1초, 제한시간 180초, stress limit 10이다.
  - cost는 시작 10/최대 10/초당 재생 0.35이다.
  - 점수는 `time + stress + kill`; 시간 초당 100, 남은 stress point당 900, 일반 적 kill 100,
    boss kill 2,000이다. 현재 값은 **데모 기준값**이며 정규 프로젝트 요구사항이 아니다.
- 근거:
  - [LoadoutGate.cs](../../Assets/_Project/Scripts/Core/Profile/LoadoutGate.cs),
    [SquadDraw.cs](../../Assets/_Project/Scripts/Core/Squad/SquadDraw.cs),
    [DeckRuleConfig_Default.asset](../../Assets/_Project/Data/Dreamcatcher/DeckRuleConfig_Default.asset).
  - [MapDocumentPool.asset](../../Assets/_Project/Data/Maps/MapDocumentPool.asset),
    `Assets/_Project/Scripts/Data/Decks/Deck_*.asset`,
    [DefaultCostConfig.asset](../../Assets/_Project/Data/Config/DefaultCostConfig.asset),
    [ScoreRules.asset](../../Assets/_Project/Data/Config/ScoreRules.asset).
  - [Score formula reference](../reference/score-formula.md) 및
    [Map/wave balancing reference](../reference/map-wave-balancing.md).
  - 테스트:
    [ScoreMathTests.cs](../../Assets/_Project/Tests/EditMode/ScoreMathTests.cs),
    [WaveKillBudgetPinTests.cs](../../Assets/_Project/Tests/EditMode/WaveKillBudgetPinTests.cs).
  - 관련 증거: 구현·자동 검증만 존재. 수치의 재미/난이도를 지지하는 E3/E4 자료 없음.
- `transfer_action`: `retest`
- 정규 프로젝트 영향: 산식의 축과 튜닝 관계는 실험 출발점으로 쓸 수 있으나 숫자 자체를 이관하면
  안 된다. 특히 stress limit과 point value는 함께 움직이는 예산이다.
- 다음 검증·결정: session completion, leak 분포, resource occupancy, Next Wave 사용과 score
  분포를 수집한 뒤 목표 구간을 정한다.

### BASE-008 — Hybrid ECS 구현 기준선

- `claim_kind`: `fact`
- `evidence_status`: `functional`
- `evidence_level`: `E1`
- `as_of`: `2026-07-29 @ 44c87885`
- 적용 모드·조건: Unity `6000.4.3f1`, Entities `6.4.0`의 현재 BattleScene runtime.
- 주장: 전투 simulation은 `Units / Movement / Combat / Effects` 네 context의 Hybrid ECS로
  구현되어 있고, MonoBehaviour와 ECS의 통로는 `BattleBridge`다. `BattleSimGroup` 아래 `ISystem`
  순서, ECS Component/Buffer, ECB와 NativeQueue event channel 및 명시적 native lifecycle 규칙이
  현재 구현을 지탱한다. 이것들은 데모 구현 수단이며 정규 non-ECS 프로젝트의 carry 대상이 아니다.
- 근거:
  - [CLAUDE.md](../../CLAUDE.md) 및 [TRD](../TRD.md) — 현재 저장소의 ECS 경계와 context 소유권.
  - [`Battle` 디렉터리](../../Assets/_Project/Scripts/Battle/),
    [BattleSimGroup.cs](../../Assets/_Project/Scripts/Battle/BattleSimGroup.cs),
    [BattleBridge.cs](../../Assets/_Project/Scripts/Bridge/BattleBridge.cs).
  - package 근거:
    [manifest.json](../../Packages/manifest.json),
    [ProjectVersion.txt](../../ProjectSettings/ProjectVersion.txt).
  - 테스트 예:
    [MovementIntegritySmokeTest.cs](../../Assets/_Project/Tests/PlayMode/MovementIntegritySmokeTest.cs),
    [DreamcatcherEffectTest.cs](../../Assets/_Project/Tests/PlayMode/DreamcatcherEffectTest.cs).
  - 관련 증거: 구현과 자동 회귀 검증. non-ECS port 결과물은 아직 없음.
- `transfer_action`: `drop`
- 정규 프로젝트 영향: `Entity`, `IComponentData`, `ISystem`, `SystemGroup`, `ECB`, `NativeQueue`,
  ECS world 생성·dispose 규칙은 제거한다. domain responsibility, single write ownership,
  pure calculation과 test contract는 Server gameplay 의미론으로 추출하고, Client presentation은
  stable ID 기반 semantic state·outcome을 별도 catalog로 해석한다.
- 다음 검증·결정: non-ECS Server domain module 경계와 Client의 비권위 prediction·preview에
  복제할 최소 pure model 범위를 ADR로 결정하고, ECS type이 새 interface나 protocol에 새지
  않는지 review한다.

### BASE-009 — 로그는 계측 기반이지 플레이테스트 결과가 아님

- `claim_kind`: `fact`
- `evidence_status`: `instrumented`
- `evidence_level`: `E1`
- `as_of`: `2026-07-29 @ 44c87885`
- 적용 모드·조건: `BattleLogger`가 배선된 match. Editor는 `<project>/GameLogs`, build는
  `Application.persistentDataPath/GameLogs`; account complete는 compact snapshot을 `debug`에 첨부.
- 주장: session id, entry mode, seed, wave, placement, kill, Dreamcatcher/skill 사용,
  score event와 결과를 남길 수 있는 client logger가 구현되어 있다. 그러나 저장소에는
  익명화된 structured playtest, survey, interview나 반복 분석 dataset이 추적되어 있지 않다.
  logger의 존재는 재미 가설을 지지하지 않는다.
- 근거:
  - [BattleLogger.cs](../../Assets/_Project/Scripts/Logging/BattleLogger.cs),
    [Tournament play/report spec](../spec/tournament-play-report/README.md).
  - 저장소 추적 파일 점검: 기준 커밋 `44c87885`에서 `GameLogs/`, playtest, survey, interview,
    telemetry dataset 없음.
  - 관련 테스트/증거: logger 기능은 여러 PlayMode flow에서 사용되나, E3/E4 evidence artifact 없음.
- `transfer_action`: `adapt`
- 정규 프로젝트 영향: client local JSON 대신 Server의 Authoritative Match Record·authoritative
  audit와 product telemetry를 분리해야 한다. Client의 `as-seen presentation trace`는 당시 표시를
  조사하는 진단용 비권위 산출물로만 보존하고 canonical Replay의 정본으로 사용하지 않는다. 각
  산출물에는 build/ruleset/presentation/protocol version과 pseudonymous participant key를
  포함해야 한다.
- 다음 검증·결정: [Evidence guide](./evidence/README.md)의 manifest로 첫 구조화 세션을 등록하고,
  raw data 저장 위치·retention·접근권한을 정한다.

### BASE-010 — 재미 가설의 현재 상태

- `claim_kind`: `hypothesis`
- `evidence_status`: `untested`
- `evidence_level`: `E0`
- `as_of`: `2026-07-29 @ 44c87885`
- 적용 모드·조건: 현재 squad + Dreamcatcher + Gift + Next Wave + 3축 score로 구성된 짧은 일반 세션.
- 주장: “이 loop가 한 세션으로 재미있고, 반복할수록 squad/Dreamcatcher 선택이 나아지며,
  cost와 Next Wave가 의미 있는 긴장을 만들고, 패배 원인을 이해할 수 있다”는 것은 아직 검증할
  제품 가설이다. H1~H3를 시험할 기능과 일부 계측은 있지만 재미 검증 완료로 분류할 근거는 없다.
- 근거:
  - [PRD](../PRD.md) — H1~H3와 계획된 5~10명 관찰/설문/인터뷰. Draft 의도 근거.
  - `BASE-001`~`BASE-009` — 구현 및 기능 확인 기준선.
  - 관련 증거: E3/E4 산출물 없음. `supported`/`refuted` 판정 없음.
- `transfer_action`: `retest`
- 정규 프로젝트 영향: PRD는 구현 목록이 아니라 이 가설들을 검증할 player value, metric과
  실험 조건을 명시해야 한다.
- 다음 검증·결정: `product/validation-backlog.md` 순서대로 session 완주, H1 반복 판단,
  cost 긴장, 패배 귀인, 첫 판 차별점 노출, Next Wave 전략을 검증한다.

### BASE-011 — pending attempt 복구는 전투 재접속이 아니다

- `claim_kind`: `fact`
- `evidence_status`: `functional`
- `evidence_level`: `E1`
- `as_of`: `2026-07-29 @ 44c87885`
- 적용 모드·조건: 계정 사용자가 `/tournament/play`로 attempt를 발급받은 뒤 정상
  `complete` 전에 앱·scene을 이탈하고, 이후 로비에 다시 진입하는 경우.
- 주장: client는 발급된 attempt를 로컬 `PendingMatchStore`에 기록한다. 정상 결과 보고은 성공
  응답 뒤 현재 attempt와 일치할 때만 compare-and-clear하고, 다음 로비에서 pending attempt가
  남아 있으면 `complete(0)`으로 마감해 참가 잠금을 복구한다. 이 흐름은 중단된 전투 상태를
  보존하거나 이어 하는 재접속이 아니라 **abandoned match의 terminal reconciliation**이다.
- 근거:
  - [PendingMatchStore.cs](../../Assets/_Project/Scripts/Core/Api/PendingMatchStore.cs),
    [TournamentMatchReporter.cs](../../Assets/_Project/Scripts/Core/Api/TournamentMatchReporter.cs).
  - [Abandoned match reconciliation spec](../spec/abandoned-match-reconciliation/README.md),
    [Tournament flow guards spec](../spec/tournament-flow-guards/README.md),
    [clear-on-success 후속 계약](../spec/tournament-flow-guards/9_clear_on_success.md).
  - 테스트:
    [PendingMatchStoreTests.cs](../../Assets/_Project/Tests/EditMode/Api/PendingMatchStoreTests.cs),
    [TournamentMatchReporterTests.cs](../../Assets/_Project/Tests/EditMode/Api/TournamentMatchReporterTests.cs).
  - 관련 커밋/증거: 기준 커밋 `44c87885`; 중단 전투 snapshot·resume token·command
    deduplication 구현과 실제 재접속 Play 산출물은 없음.
- `transfer_action`: `adapt`
- 정규 프로젝트 영향: 성공 확인 뒤 상태를 지우는 수명주기와 멱등 종료 교훈은 유지하되,
  PlayerPrefs 기반 0점 마감은 server-authoritative reconnect·timeout·forfeit 정책으로
  대체해야 한다.
- 다음 검증·결정: snapshot 보존 기간, resume 자격, command deduplication, timeout·forfeit,
  중복 terminal 요청을 `ADR-CAND-009`에서 결정하고 fault-injection으로 검증한다.

## 역사적 기준선의 재검토 조건

이 문서는 Product·Client·Server 세 역할이 사실관계와 누락을 검토하기 전까지 `Draft`다.
이 문서 하나를 `Frozen`으로 승격하지 않는다. 영향 claim을 새 source commit에서 개별
registry record로 갱신하고 전역 strict gate를 통과시킨다. 공식 freeze는
[전역 transition 정본](README.md)에 따라 모든 package에 대해 한 번만 수행한다.
코드·asset이 바뀌면 기존 기준선을 조용히 덮어쓰지 않고 새 `as_of`를 만들거나 변경 claim을
`superseded` 처리한다.
