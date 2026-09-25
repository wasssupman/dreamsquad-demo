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
| 온보딩 | `BattleCoreUnity/Hud/CoreFirstRunGuide.cs` ← `UI/Tutorial/FirstRunTutorialController.cs`(908줄) — **사용자 결정 ①(2026-09-25: 옮긴다)** |
| 로비 교대 | `Core/SceneNames.cs:8` · `ProjectSettings/EditorBuildSettings.asset` · **`Editor/MobileBuild/DreamSquadMobileBuildCli.cs:33~37`(`ExpectedScenes`)·`:603~610`(불일치 거부) + `Tests/EditMode/MobileBuild/DreamSquadMobileBuildCliTests.cs:333`** — 「mobile-build 복구 커밋 보호」 대상이지만 **사용자 결정 ②(2026-09-25 허용)** 로 씬 목록(`OutgameScene` + `BattleCoreScene`)과 그 거부 문구만 바꾼다. 파일의 다른 부분은 무변 |
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
- **온보딩(결정 ① = (a) 옮긴다, 2026-09-25 확정).** 완료 플래그 `firstRunTutorialDone = true` 를 쓰는 곳은 옛 컨트롤러 **하나뿐**이다(`FirstRunTutorialController.cs:719`). 로비는 그 플래그가 거짓인 동안 참가 신청을 생략한다(`OutgameMenuController.cs:280~292` · `FirstRunTutorialConfig.cs:113~114`). 그러니 새 씬에 완료를 쓰는 주인이 없으면 **새 계정은 토너먼트에 영영 오르지 못하고**, 매 판 60초 저작 웨이브·첫 손패·보너스 억제가 걸린다. 옮길 때 가이드 문구·포커스·홀드는 `TutorialGuidanceView`·`OutgameTutorialOverlay`(도구 — 76038c26 이 사용자 결정으로 보존)를 그대로 쓰고, 컨트롤러만 읽기 모델·커맨드 receipt 로 다시 쓴다. **완료 기록은 그 컨트롤러가 갖는다.**
- **배틀 JSON 로그 파일**(`BattleLogger.cs:448`)은 새 씬에 두지 않는다(에이전트 판정). 그 파일의 유일한 소비 도구는 은퇴한 PRD 가설을 검증하는 스크립트(`tools/analyze_sessions.py:2`)다. 판별 로그는 코어 트레이스(`BattleCore/Harness/CoreTrace.cs`)가 맡는다. rules X28(「배틀 JSON 로그 미완」, `ledgers/rules.md:259`)은 이 판정으로 **제거**로 닫고 근거를 그 행에 적는다.
- 로비 교대는 **마지막 커밋**이다. 위가 전부 초록일 때 상수 한 줄 + 빌드 설정 + 모바일 빌드 CLI·테스트의 씬 목록(결정 ②)을 바꾼다.

## 고친 것 (2026-09-25 구현)

| 무엇 | 옛 근거 | 새 자리 | 커밋 |
|---|---|---|---|
| 진입 해석 | `GameManager.Start`·`StartSquadMatch`·`StartTestModeMatch`(`:280~480`) | `MatchEntry.Resolve`(순수 static — 판정·상태 0, 테스트 문맥 1회 소비만). **「로비에서 왔나」 = `PlayerProfileSO.IsLoadedThisSession`**(옛 온보딩·기록 가드) — 에디터 직접 진입은 드라이버 저작 편성·개발용 덱 | `c899b6ab7` |
| 맵 풀 4갈래 | `BattleBridge.cs:1263~1300` | `MatchDefinitionBuilder.TrySelectEncounter` — 정적 상태(`DevMapOverride`·토너먼트 시드)는 드라이버가 값으로 넘긴다. 드라이버 `_mapPool` = 옛 씬 풀(0번 = 오늘의 `MapStage_Duel` + `Deck_Duel` — 에디터 판 무변) · `_stagePrefab` 은 풀이 빌 때만 | `c899b6ab7` |
| 웨이브 원천 7단 | `BattleBridge.cs:2114~2116` | `EntryAuthoring{ForcedPlan, EncounterPlan}` → `ResolveEntryPlan`/`ResolveWavePlan` | `c899b6ab7` |
| ⚠ **저작 플랜의 판 길이** | `BattleBridge.cs:1651`(저작 플랜이면 `plan.timerDurationSec`, 0 = 끝없음) | `ApplyEntryPlanClock` — 모드 **밖**에서 온 플랜(①②④)만 제 시계로(온보딩 60초 · 테스트 플랜 0 → `CountUp`). 모드 플랜(③)은 모드 시계. unit 4 의 「판 길이 = 모드 단독」은 덱 타이머의 결정이었고 저작 플랜은 이식이 빠져 있었다 — 안 옮기면 온보딩이 180초 판이 되고 문구 「튜토리얼 1분」이 거짓이 된다 | `c899b6ab7` |
| 온보딩 칸 둘 | G11·G12 | `MatchDefinition.PinnedHandFront`(→ `HandDeck.Begin` 의 `pinnedFront`) · `BonusPullSuppressed`(→ `WaveScheduler`, 켜기만 — 판 시작 전 직접 주입(X6) 을 끄지 않는다). 기본값이면 해시 입력 밖(골든 무변) | `c899b6ab7` |
| 첫 유닛 체력 낮추기 | `BattleBridge.TryQueueDeployedDefenderMaxHealthDamage`(`:7559`) | 코어 커맨드 `DamageMaxHealthRatio`(26) — 출처 없는 최대 × clamp01(비율), 인박스에만(옛 버퍼 적재) | `c899b6ab7` |
| 코스트 돌 배율 | `ResolveCostRateMultiplier:632` | **이미 7b 에 있었다** — `Build` 안에서 `costRateMultiplier × CostRateOf(dreamstones)`. 문서의 「`:290` 의 `1f` 를 교체」는 두 번 곱하게 된다 — 호출부 1 은 호출자 배율(항등)로 둔다. 남은 일은 프로필 돌을 넣는 것뿐이었다 | `c899b6ab7` |
| 덱 스냅샷 | `PersistTournamentDeckSnapshot:585` · `DreamcatcherHandController.cs:548` · `BattleLogger.DeckInfoJson:421` | `BattleDriver.DeckInfoJson` — 반입(유닛·돌 원시 id) → 덱 확정(+카드 = **고른 덱만**, 굴린 액티브 제외) 두 번 `PersistMatchDeck` · 제출·나가기가 같은 문자열 | `c899b6ab7`·`f4cefe7d4` |
| G21 참가 채택 | `GameManager.OnEnable` | `BattleDriver.Start` 첫 줄 `TournamentMatchReporter.BeginMatch` | `c899b6ab7` |
| 앱 훅 · 화면 초기화 | `GameManager.cs:176~204·285~291` | `Core/AppBootstrap.cs`(이동 — `GameManager` 에 `RuntimeInitializeOnLoadMethod` 0) · `View/CoreScreenSetup.cs`. G20 은 드라이버 수명이 대신한다(파일 헤더) | `c899b6ab7` |
| 온보딩 | `FirstRunTutorialController.cs`(908줄) | `Hud/CoreFirstRunGuide.cs` — 판별 = `Entry.Kind`, 홀드 = Battle 정지 리스(상한 자가 해제), 설명 = `CoreMapOverlay.ShowBriefing`, 신호 = 사건(`Placed`·`Retired`) + 읽기 창(`TryGetSlotRect`·`ActionRect`·`HitRect`·`TimerFocusRect`) | `847305ce9` |
| 철수 버튼 글자 | `DcInspectController.cs:477 RetireLabel = "철수"` | 5b 가 「퇴근」(기능 이름)으로 바꿔 놓았다 — 옛 라이브 글자로 복구(온보딩 문구가 그 말을 가리킨다) | `847305ce9` |
| 손패 뒤집기 경합 | — | 열기 뒤집기 중 닫히면 뒤집기를 끊는다(안 끊으면 칸 줄이 영영 접힌다 — 선택 직후 철수). 온보딩 자동 증언이 잡았다 | `847305ce9` |
| 결과·나가기·기록 | `MenuPopup.cs:113~161` · `GameManager.RecordMatchPlayed` | `CoreMatchOutcomePresenter.RecordMatchPlayed`(래치 1 · 이번 세션 프로필만 저장) · `AbandonAndLeave` · `CoreMenuPopup` 「나가기」↔「제출」 | `f4cefe7d4` |
| ⚠ **드라이버 시드 기본값 `1`→`0`** | `BattleDriver._seed`(G3 — 옛 `debugFixedMatchSeed`) · 씬 노브 20260923 | 0 = `MatchEntry.Resolve` 3단(`selectionSeed` → `FixedSeed` → `GenerateRandom`) 의 마지막 = **판마다 새 난수**(옛 라이브와 같다). 영향은 에디터 직접 진입의 반복 재현성뿐 — 골든·테스트는 하네스가 시드를 명시해 무변. 재현하려면 씬 노브에 비0 을 넣는다 (리뷰 M1 보충) | `c899b6ab7`·`f4cefe7d4` |
| 로비 교대 | `SceneNames.cs:8` · 빌드 설정 · CLI `:33~37·:603~610` | 상수 · 빌드 설정 · CLI 목록·문구(결정 ②). CLI 테스트는 같은 상수를 참조해 **변경 0** | `d101b9dab` |
| 옛 전투 PlayMode lane | 128곳이 `SceneNames.Battle` 로 옛 씬을 열었다 | `Tests/PlayMode/LegacyBattleScene.Load()`(경로로 연다 — 빌드 목록 밖) · unit 9 에서 파일째 삭제 | `d101b9dab` |
| 진입 테스트 6 | `Tests/PlayMode/` 6 파일 | 삭제 → `PlayModeCore/CoreMatchEntryTests` 14(왕복·전환·프리셋·못 찾는 id·확정 덱·덱 없음·스탯 돌·코스트 돌·테스트 모드·래치·메뉴 나가기·온보딩 판·다음 판·**온보딩 완주**) | `d101b9dab` |

## 이식 제외

| 안 옮긴 것 | 이유 | 등급 |
|---|---|---|
| 편성 없음 → 뽑기 폴백(G6·G8) | 계약 9 | 제거(선행) |
| 조준 모드 배타(G16) | 스킬 탭 조준은 7b·7c 의 카드 입력이 대신한다 | 확인 — 새 입력에 같은 배타가 있는지 테스트로 못박는다. ⚠ **8b 에서 못박지 않았다**(배치 무장·드래그가 선택을 닫는 것 `SelectionInput.Update` 까지만 코드로 확인) — core-reviewer·플레이 4차 몫 |
| 판 안 재시작(`OnRestartRequested` dormant) | 사용자 결정 2026-09-23 「판 안 재시작 없음」 | 제거(결정) |
| 배틀 JSON 로그 파일 | 위 판정 | 제거(에이전트 판정) — rules X28 **제거**로 닫음 |
| 에디터 직접 진입의 프로필 편성 | 옛 편성 반입은 세션 가드 없이 SO 메모리 사본을 읽었다(에디터에서 옛 씬을 열어도 개발자 편성). 새 씬 직접 진입은 드라이버 저작 편성·개발용 덱 — 8b 표 G5 행 | 차이(의도) |
| 온보딩 B1 의 「가능 칸」을 말파이트의 **층**으로 칠하기 | 새 가이드는 칸의 상태(`CellStateAt` — 층 무관)만 안다. 말파이트는 지상 유닛이라 오늘 판에서 그림은 같다 | 차이(경미) |
| 테스트 모드 판의 끝없는 시계 | 옛 테스트 플랜(`timerDurationSec 0`)은 끝없는 판이었다 — 그대로 옮겼다(`CountUp`). 테스트 모드 SO 가 강제되면 그 모드의 시계가 아니라 플랜 시계다 | 이식(옛 규칙) |

## 파이프라인 커버리지

N/A — 판 오브젝트의 생성→렌더 경로는 바뀌지 않는다(입력 값만 바뀐다). 맵 풀 선택은 **어느 스테이지 프리팹**이냐를 바꿀 뿐이고, 정거장은 「맵 스테이지/프랍」 표 그대로다.

## 완료 기준

- [ ] 로비 START → `BattleCoreScene` → 3분 → 결과 화면에 **실제 랭킹**(「참가자 찾는 중」 5칸이 아님) → 로비.
      **Play 스모크(2026-09-25, 에디터 · 게스트)**: `OutgameScene` Play → `OutgameMenuController.OnStartGame` → `BattleCoreScene`(entry=Squad · 편성 7 · 풀 0번 `MapStage_Duel` · 카드 12 · 덱 스냅샷 세 필드 채움) → 배치 3기 → 60초 해금 뒤 메뉴 「제출」 → 결과 화면(10기 · 결과 1회 · 제출 게이트 통과 · 기록 1) → 「로비로」 → `OutgameScene`(드라이버 0 · `targetFrameRate` 60 · vSync 0). 스크린샷 6장. ⚠ **실제 랭킹은 미확인** — 이 머신 세션은 게스트라 참가 신청이 없고(`ReportResult` 가 게스트에서 생략) 「참가자 찾는 중」 5칸이 정상이다. 로그인 계정 + 서버로 확인할 몫이다(사용자 플레이 4차).
- [x] 제출 payload 의 `deckInfo` **세 필드 내용** = 그 판의 편성 유닛 id · 장착 돌 id · 확정 카드 덱 id(반입 시점 기록 → 덱 확정 뒤 갱신, 단조 증가).
      `CoreMatchEntryTests`(못 찾는 유닛 id 도 id 로 · 돌 4 · 코스트 돌 + 못 찾는 돌 · 카드 10 = 고른 덱) + Play 스모크의 실문자열.
- [x] 나가기 → 0점 제출 1회(덱 포함) · `matchesPlayed` +1 · 로비. 결과 경로와 겹쳐도 +1(래치). `CoreMatchEntryTests` 래치·메뉴 나가기(로비 도착 · 씬 전환 앞 기록). 0점 제출의 서버 왕복은 게스트라 미확인.
- [x] 같은 토너먼트 시드 두 판 = 같은 맵·같은 덱(옛 `tournament-seed-map-select` 결정론). dev 강제 인덱스가 이긴다. `MatchEntryBuildTests`(라이브 풀 · 시드 6개 · 네 갈래 서열 · dev 슬롯). ⚠ 이 머신은 `dev_forceMapIndex = 0` 이 PlayerPrefs 에 박혀 있어 모든 판이 source=dev 로 뜬다(0번이라 판은 같다).
- [x] 테스트 모드 패널·에디터 「Test this plan」 → 저작 플랜 판(1회 소비 — 다음 판은 일반 판). `CoreMatchEntryTests`(문맥 → `TestMode` · 원천 = 그 플랜 · `CountUp` · 다음 판 일반). 에디터 런처는 새 씬 경로로 바꿨고 버튼 육안은 미실행.
- [x] 온보딩(결정 ①): 새 계정 → 온보딩 판(저작 웨이브·첫 손패·보너스 억제·가이드 순서) → **완주 → `firstRunTutorialDone` 저장 → 다음 판 참가 신청 발행**(결과 화면 랭킹이 뜬다).
      `CoreMatchEntryTests.온보딩을_완주하면_…` — 새 계정(`ProfileStore.CreateDefault`) → 온보딩 판(60초 · 억제 · 첫 손패 고정) → 사람 박자로 B1→B2b→B3a(접근 대기)→B3b(철수)→B3c→B4(재선택·부착)→B5 → **완료 기록** → `ShouldRun` 거짓 → 다음 판 `Squad`. 「참가 신청 발행」 자체는 로비 코드(`OutgameMenuController.cs:280~292`, 무변)의 몫이라 술어로만 확인했다. 실제 손가락 육안은 사용자 플레이 4차.
- [x] `grep -rn "RuntimeInitializeOnLoadMethod" Assets/_Project/Scripts/Core/GameManager.cs` = 0 · `AppBootstrap` 에 둘 존재(두 곳에서 설정하지 않는다). 옛 씬을 한 번도 안 연 로비 콜드 스타트에서 `targetFrameRate == 60` — **unit 9 삭제 뒤 한 번 더** 잰다(지금은 옛 훅이 씬과 무관하게 돌아 판별력이 없다). grep 0 · `AppBootstrap` 2 · Play 스모크 로비 복귀 뒤 60/0.
- [x] `SceneNames.Battle` 목적지 = 새 씬 · 빌드 설정 = `OutgameScene` + `BattleCoreScene` · CLI `ExpectedScenes` 와 그 테스트가 같은 목록(결정 ②). `DreamSquadMobileBuildCliTests` 63/63.
- [x] 재작성한 진입 테스트 6 + 신규 진입 테스트 초록 · EditMode 선행 2 외 빨강 0(`DreamSquadMobileBuildCliTests` 포함) · 헤드리스 3종.
      헤드리스(커밋 4개 각각 클린 export): build 0 · test 685 · Check 0. Unity EditMode 코어+Assets 1005/1007(선행 2 `bomb_man`·`boomerang` · 골든 무변) · PlayMode 코어 **85/85**(71 + 신규 14) · 옛 PlayMode 부분집합 38/38(8a 의 40 중 재작성 2 를 뺀 6 파일 — ⚠ 첫 실행은 코어 lane 직후라 `BonusWavePullTest` 3 빨강, 단독 13/13 · 재실행 38/38 로 재현 안 됨) · `check_ledgers.py` exit 0.
- [ ] **Android QA 빌드**(`DreamSquadMobileBuildCli.BuildAndroidQa`) 성공 + 실기기 1판(로비 → 판 → 결과 → 로비). ⚠ Entities 가 아직 있어 기본 월드가 옛 시스템을 만든다 — 이 빌드의 성능 수치는 unit 9 뒤 빌드와 바로 비교하지 않는다.
      **미시도**: 저장소 CLI 는 keystore 비밀번호를 **숨김 입력**으로 받고(에이전트가 가진 값이 아니다) · 작업 트리 clean 을 요구하며(무관한 dirty 파일이 있다) · 같은 프로젝트를 연 에디터와 배치 Unity 가 공존할 수 없다. SDK/OpenJDK/NDK 는 Hub 에 있다. 사용자가 실행할 몫.
- [x] `core-reviewer` **APPROVE**(2026-09-25 — CRITICAL·HIGH 0 · MEDIUM 1 = 시드 기본값 문서 보충(위 표) · LOW 2 = 후속 후보) → [ ] **사용자 플레이 4차**(조각 D 의 3차 뒤 · 질문 = 「로비에서 들어간 판이 옛 판과 같은 판인가」).

리드 재검증 2026-09-25 — HEAD `7d5d99722` 클린 export: build 0 · test 685/685 · Check 0 · `check_ledgers.py` exit 0. Unity EditMode 코어+Assets 1005/1007(선행 2 `bomb_man`·`boomerang`) · PlayMode 코어 85/85 · 옛 씬 PlayMode 부분집합 38/38(`BonusWavePullTest` 빨강 재현 안 됨) · `DreamSquadMobileBuildCliTests` 63/63. 스모크 부수 효과: 이 머신 프로필 `matchesPlayed` 410→411(게스트 세션 기록 1회). 실제 랭킹·Android 빌드·플레이 4차는 8c 머지 게이트에서 사용자 몫.
