# 8b — 판 진입·퇴장 + 로비를 새 씬으로 (조각 E · 2/3)

> 5c 는 「로비 진입이 새 씬으로 옮겨질 때(unit 9)」라고 적었다. 이 unit 은 그 교대를 **8b 로 당기는 안**이다. unit 9 는 지우는 unit 이고, 지우기 전에 로비가 새 씬으로 한 번 이상 돌아 봐야 한다(옛 씬을 지운 뒤 진입 결함이 나오면 돌아갈 씬이 없다). ⚠ 당기면 **8c 의 main 머지 때 동료·GitLab 이 새 전투를 받는다** — 시점은 사용자 확인 대상이다(README 결정 ③).

## 목적

**로비의 「시작」이 `BattleCoreScene` 을 열고, 그 판이 옛 판과 같은 입력(편성·돌·시드·맵·웨이브 원천·테스트 플랜·토너먼트 참가)으로 지어지고, 같은 방식으로 끝난다(결과·나가기·기록).** 지금 새 드라이버는 이 입력을 거의 받지 않는다. 편성은 드라이버 저작이다(`BattleDriver.cs:37 _defenders`). 맵은 프리팹 직접 지정이다(`:43 _stagePrefab`). 코스트 배율은 상수다(`:290 costRateMultiplier: 1f`). `TestModeContext`·`TournamentMatchReporter.BeginMatch`·`matchesPlayed` 는 `BattleCoreUnity/` 에서 grep 0 이다(`ReportResult` 호출은 이미 있다, `CoreMatchOutcomePresenter.cs:110`). 이 규칙들은 전부 `GameManager`(rule-holders G1~G24)와 `BattleBridge.BuildMapForBattle` 안에 있고, unit 9 에 함께 지워진다.

## 변경 대상

| 항목 | 경로 |
|---|---|
| 진입 해석 | `BattleCoreUnity/MatchEntry.cs`(신설, 순수 static — 판정·상태 0. 이름에 Manager·Controller 금지) |
| carry-in | **테스트 플랜의 carry-in 은 `TestModeContext` 하나로 둔다** — 패널(`TestModePanelView.cs:95`)과 에디터 런처(`WavePlanTestLauncher.cs:37` → `TestModeContext.cs:32` BeforeSceneLoad 훅)가 이미 쓴다. `MatchEntry` 가 1회 소비한다. `MatchEntryContext`(5c)는 **모드 선택**만 나른다 |
| 모드 | 로비 경로는 모드를 고르지 않는다 — 씬 드라이버의 `_mode`(`BattleDriver.cs:35`, `MatchMode_KillScore3Min`)가 3단 서열의 셋째 칸이다. 모드 선택 UI 는 범위 밖(`match-mode-design.md` 「흐름」) |
| 드라이버 | `BattleDriver.cs` — `Begin` 이 `MatchEntry` 결과를 `MatchDefinitionBuilder.Build` 에 넘긴다 |
| 맵 풀 | `MatchDefinitionBuilder` — 모드 `mapPool`(null = 기본 풀) → 풀 엔트리(스테이지·덱·플랜 한 몸) · `match-mode-design.md:22` 「맵」 행을 4갈래로 갱신 |
| 앱 전역 훅 | `Core/AppBootstrap.cs`(신설) ← `GameManager.cs:176~189`(`[RuntimeInitializeOnLoadMethod]` 2개 — **이동**, 두 곳에 두지 않는다) |
| 씬 초기화 | 새 씬 컴포넌트 ← `GameManager.cs:195~204`(세로 1080 캡) · `:285~291`(탭/드래그 임계 DPI) · `:645`(씬 꺼짐) |
| 퇴장 | `Hud/CoreMenuPopup.cs`(「나가기」·「성적 확정」 — 파일 헤더 `:18` 이 5c 로 미뤘고 5c 가 안 만들었다) |
| 덱 스냅샷 | `TournamentDeckInfo.Serialize(unitIds, stoneIds, cardIds)`(`Core/Api/TournamentDeckInfo.cs:46`, 순수 static)를 **직접** 부른다. `BattleLogger` 는 새 씬에 들이지 않는다 — `CoreMatchOutcomePresenter.cs:107~109` 가 「그 로거를 새 씬이 들면 두 번째 매니저」라고 기록했고, 로거 자체가 옛 타입을 든다(`BattleLogger.cs:159 SetWavePattern(GeneratedWavePlan)`) |
| 온보딩 | `BattleCoreUnity/Hud/CoreFirstRunGuide.cs` ← `UI/Tutorial/FirstRunTutorialController.cs`(908줄) — **사용자 결정 ①** |
| 로비 교대 | `Core/SceneNames.cs:8` · `ProjectSettings/EditorBuildSettings.asset` · **`Editor/MobileBuild/DreamSquadMobileBuildCli.cs:33~37`(`ExpectedScenes`)·`:603~610`(불일치 거부) + `Tests/EditMode/MobileBuild/DreamSquadMobileBuildCliTests.cs:333`** — 이 둘은 「mobile-build 복구 커밋 보호」 대상이라 **사용자 결정 ②** 없이 손대지 않는다 |
| 에디터 도구 | `Editor/WavePlanTestLauncher.cs:15·38`(옛 씬 경로) · `Editor/WavePlanAssetEditor.cs:21`(「Test this plan (Play BattleScene)」) → 새 씬 |
| 테스트 | 진입 테스트 **재작성**(이동이 아니다 — `GameManager`·옛 씬을 직접 부른다): `OutgameFlowSmokeTest`·`PresetCarryInTest`·`SquadCarryInSmokeTest`·`SceneTransitionSmokeTest`·`DreamcatcherDeckCarryInTest`(프로필 선택 덱 → 판 덱, 짝 = `CoreDeckComposition`)·`DreamstoneCarryInSmokeTest` → `Tests/PlayModeCore/` |

## 구현 — 옛 규칙과 그 새 자리

| 옛 규칙(rule-holders) | 옛 코드 | 새 자리 |
|---|---|---|
| G3 판 시드는 판당 한 번 — 고정 노브 ≠ 0 이면 그 값, 아니면 새 난수 | `GameManager.cs:248~254` | `MatchEntry` → `ModeSelection.Seed` |
| G5·G7 테스트 모드 > 저장 편성 · 랜덤 채움 없음 · 못 찾는 id 는 그 슬롯만 빠진다 | `:305~316` · `StartSquadMatch` | `MatchEntry` → `Build(defenders:)`. 드라이버 `_defenders` 는 에디터 직접 진입 폴백으로만 |
| G9 코스트 계열이 아닌 돌만 유닛 버프 | `ResolveEquippedStones:613` | **입력 배선만 남았다** — 드라이버 `_dreamstones`(`:82`)가 이미 `Build(dreamstones:)`(`:304`)로 넘긴다. 프로필 편성의 돌을 거기 넣는다 |
| G10 코스트 돌 = 충전 배율, 판 진입 때만 | `ResolveCostRateMultiplier:632` | 새 층에 이미 있다: `CardDefinitionBuilder.CostRateOf`(`:59`). `:290` 의 `1f` 를 그것으로 바꾼다(자를 새로 만들지 않는다) |
| G11 온보딩 판 = 저작 웨이브 + 첫 손패 고정 + 보너스 억제(무조건 설정) | `:393~409` | `MatchEntry` → 플랜 · `pinnedFront`(7c 의 칸) · `WaveScheduler.BonusPullSuppressed`. **결정 ① 과 한 몸**이다(아래) |
| G13 테스트 모드 = 저작 플랜 + 저장 편성(비면 프리셋), 1회 소비 | `:447~480` | `TestModeContext` 를 `MatchEntry` 가 소비 |
| 맵 풀 4갈래: dev 강제 > 디버그 고정 시드 > 서버 토너먼트 시드 > 0번 · 맵·덱·플랜은 같은 인덱스로 잠긴다 | `BattleBridge.cs:1263~1300` | `MatchDefinitionBuilder`(`MapPoolSelect` 재사용) · dev 슬롯(rules M17) 포함 |
| G21 토너먼트 참가는 로비가 발행한 것만 채택 | `GameManager.cs:240~243` | 새 씬 진입 1회 `TournamentMatchReporter.BeginMatch` |
| G22·G23 반입 편성·돌을 미리 기록(못 찾는 id 도 id 로) · 카드는 덱 확정 때 **같은 통로로 갱신**(payload 단조 증가) | `:585 PersistTournamentDeckSnapshot` · `:535 LogDreamstoneCarryIn` · 주석 `:581~583` · `DreamcatcherHandController.cs:548` | `PersistMatchDeck(Serialize(…))` 를 **두 시점**에: 반입(유닛·돌) · 덱 확정(+카드, 7c `CoreDeckComposition`). `ReportResult`·`AbandonMatch` 는 같은 문자열을 싣는다 — 5c 「제출 payload 의 덱 스냅샷」 해소 |
| G15 「한 판 해봤다」 = 판당 1회, 결과·나가기 두 통로 | `:110·146 RecordMatchPlayed` · `MenuPopup.cs:160` | 결과 사건 + 나가기(래치 1) |
| 나가기 = 참가 포기 0점 제출(덱 포함) → 기록 → 로비 · 제출 열린 뒤엔 「성적 확정」 | `MenuPopup.cs:136~161` | `CoreMenuPopup`(제출 = 커맨드 `Submit`) |
| G17 60프레임·수직동기 끔·트윈 풀 400 — 앱 시작 훅 | `GameManager.cs:176~189` | `AppBootstrap`(이동) |
| 세로 1080 캡 + 기기 가로비 · G18 DPI 임계(낮추지 않는다) · G20 씬 꺼짐 | `:195~204` · `:285~291` · `:645` | 새 씬 초기화 |

**웨이브 원천 우선순위**(옛 `BattleBridge.cs:2114~2116` + 모드 슬롯): ① 테스트 모드 플랜 > ② 온보딩 플랜(G11) > ③ 모드 `plan`(`waveSourceKind == AuthoredPlan`) > ④ 맵 풀 엔트리의 플랜 > ⑤ 모드 `deck` > ⑥ 맵 풀 엔트리의 덱 > ⑦ 드라이버 저작 덱. 옛 게임에는 ③·⑤ 가 없었다(모드가 새로 생긴 칸). 라이브 모드는 둘 다 비어 있다(`plan`·`deck` null) — 그래서 **라이브 판의 원천은 옛 게임과 같다**. `MatchDefinitionBuilder.ResolveDeck/ResolvePlan`(`:88·95`)이 이 순서를 담는 유일한 자리다.

- 순서는 옛 것 그대로다: **시드 → 기믹 → 맵**(G3·G4 주석). 반입 기록은 배치 **전**이다(앱이 죽어도 그 판이 편성을 갖는다, G22).
- **온보딩(결정 ①).** 완료 플래그 `firstRunTutorialDone = true` 를 쓰는 곳은 옛 컨트롤러 **하나뿐**이다(`FirstRunTutorialController.cs:719`). 로비는 그 플래그가 거짓인 동안 참가 신청을 생략한다(`OutgameMenuController.cs:280~292` · `FirstRunTutorialConfig.cs:113~114`). 그러니 새 씬에 완료를 쓰는 주인이 없으면 **새 계정은 토너먼트에 영영 오르지 못하고**, 매 판 60초 저작 웨이브·첫 손패·보너스 억제가 걸린다. (a) 옮긴다면 가이드 문구·포커스·홀드는 `TutorialGuidanceView`·`OutgameTutorialOverlay`(도구 — 76038c26 이 사용자 결정으로 보존)를 그대로 쓰고, 컨트롤러만 읽기 모델·커맨드 receipt 로 다시 쓴다. **완료 기록은 그 컨트롤러가 갖는다.** (b)·(c) 는 README 결정 ①.
- **배틀 JSON 로그 파일**(`BattleLogger.cs:448`)은 새 씬에 두지 않는다(에이전트 판정). 그 파일의 유일한 소비 도구는 은퇴한 PRD 가설을 검증하는 스크립트(`tools/analyze_sessions.py:2`)다. 판별 로그는 코어 트레이스(`BattleCore/Harness/CoreTrace.cs`)가 맡는다. rules X28(「배틀 JSON 로그 미완」, `ledgers/rules.md:259`)은 이 판정으로 **제거**로 닫고 근거를 그 행에 적는다.
- 로비 교대는 **마지막 커밋**이다. 위가 전부 초록일 때 상수 한 줄 + 빌드 설정 + (결정 ② 에 따라) CLI 목록을 바꾼다.

## 이식 제외

| 안 옮긴 것 | 이유 | 등급 |
|---|---|---|
| 편성 없음 → 뽑기 폴백(G6·G8) | 계약 9 | 제거(선행) |
| 조준 모드 배타(G16) | 스킬 탭 조준은 7b·7c 의 카드 입력이 대신한다 | 확인 — 새 입력에 같은 배타가 있는지 테스트로 못박는다 |
| 판 안 재시작(`OnRestartRequested` dormant) | 사용자 결정 2026-09-23 「판 안 재시작 없음」 | 제거(결정) |
| 배틀 JSON 로그 파일 | 위 판정 | 제거(에이전트 판정) |

## 파이프라인 커버리지

N/A — 판 오브젝트의 생성→렌더 경로는 바뀌지 않는다(입력 값만 바뀐다). 맵 풀 선택은 **어느 스테이지 프리팹**이냐를 바꿀 뿐이고, 정거장은 「맵 스테이지/프랍」 표 그대로다.

## 완료 기준

- [ ] 로비 START → `BattleCoreScene` → 3분 → 결과 화면에 **실제 랭킹**(「참가자 찾는 중」 5칸이 아님) → 로비.
- [ ] 제출 payload 의 `deckInfo` **세 필드 내용** = 그 판의 편성 유닛 id · 장착 돌 id · 확정 카드 덱 id(반입 시점 기록 → 덱 확정 뒤 갱신, 단조 증가).
- [ ] 나가기 → 0점 제출 1회(덱 포함) · `matchesPlayed` +1 · 로비. 결과 경로와 겹쳐도 +1(래치).
- [ ] 같은 토너먼트 시드 두 판 = 같은 맵·같은 덱(옛 `tournament-seed-map-select` 결정론). dev 강제 인덱스가 이긴다.
- [ ] 테스트 모드 패널·에디터 「Test this plan」 → 저작 플랜 판(1회 소비 — 다음 판은 일반 판).
- [ ] 결정 ①(a) 의 경우: 새 계정 → 온보딩 판(저작 웨이브·첫 손패·보너스 억제·가이드 순서) → **완주 → `firstRunTutorialDone` 저장 → 다음 판 참가 신청 발행**(결과 화면 랭킹이 뜬다). (b)·(c) 의 경우 README 결정 ① 의 결과 문장대로.
- [ ] `grep -rn "RuntimeInitializeOnLoadMethod" Assets/_Project/Scripts/Core/GameManager.cs` = 0 · `AppBootstrap` 에 둘 존재(두 곳에서 설정하지 않는다). 옛 씬을 한 번도 안 연 로비 콜드 스타트에서 `targetFrameRate == 60` — **unit 9 삭제 뒤 한 번 더** 잰다(지금은 옛 훅이 씬과 무관하게 돌아 판별력이 없다).
- [ ] `SceneNames.Battle` 목적지 = 새 씬 · 빌드 설정 = (결정 ② 의 답).
- [ ] 재작성한 진입 테스트 6 + 신규 진입 테스트 초록 · EditMode 선행 2 외 빨강 0(결정 ② 가 CLI 테스트를 바꾸면 그 테스트 포함) · 헤드리스 3종.
- [ ] **Android QA 빌드**(`DreamSquadMobileBuildCli.BuildAndroidQa`) 성공 + 실기기 1판(로비 → 판 → 결과 → 로비). ⚠ Entities 가 아직 있어 기본 월드가 옛 시스템을 만든다 — 이 빌드의 성능 수치는 unit 9 뒤 빌드와 바로 비교하지 않는다.
- [ ] `core-reviewer` APPROVE → **사용자 플레이 4차**(조각 D 의 3차 뒤 · 질문 = 「로비에서 들어간 판이 옛 판과 같은 판인가」).
