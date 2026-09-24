# 8b — 판 진입·퇴장 + 로비를 새 씬으로 (조각 E · 2/3)

> 5c 는 「로비 진입이 새 씬으로 옮겨질 때(unit 9)」라고 적었다. 그 교대를 **여기로 당긴다** — unit 9 는 지우는 unit 이고, 지우기 전에 로비가 새 씬으로 한 번 이상 돌아 봐야 한다(옛 씬을 지운 뒤 진입 결함이 나오면 돌아갈 씬이 없다).

## 목적

**로비의 「시작」이 `BattleCoreScene` 을 열고, 그 판이 옛 판과 같은 입력(편성·돌·시드·맵·테스트 플랜·토너먼트 참가)으로 지어지고, 같은 방식으로 끝난다(결과·나가기·기록).** 지금 새 드라이버는 이 입력을 **하나도** 받지 않는다 — 편성은 드라이버 저작(`BattleDriver.cs:37 _defenders`), 맵은 프리팹 직접 지정(`:43 _stagePrefab`), 코스트 배율은 상수(`:290 costRateMultiplier: 1f`), `TestModeContext`·`TournamentMatchReporter.BeginMatch`·`matchesPlayed` 는 `BattleCoreUnity/` 에서 grep 0 이다. 그 규칙들은 전부 `GameManager`(rule-holders G1~G24)와 `BattleBridge.BuildMapForBattle` 안에 있고 unit 9 에 함께 지워진다.

## 변경 대상

| 항목 | 경로 |
|---|---|
| 진입 해석 | `BattleCoreUnity/MatchEntry.cs`(신설, 순수 static — 판정·상태 0. 이름에 Manager·Controller 금지) · `ModeSelection.cs` 의 `MatchEntryContext` 를 넓힌다(1회 소비 유지) |
| 드라이버 | `BattleDriver.cs` — `Begin` 이 `MatchEntry` 결과를 `MatchDefinitionBuilder.Build` 에 넘긴다 |
| 맵 풀 | `MatchDefinitionBuilder` — 모드 `mapPool`(null = 기본 풀, `match-mode-design.md` 「맵」 행) → 풀 엔트리(스테이지·덱·플랜 한 몸) |
| 앱 전역 훅 | `Core/AppBootstrap.cs`(신설) ← `GameManager.cs:176~189`(`[RuntimeInitializeOnLoadMethod]` 2개) |
| 씬 초기화 | 새 씬 컴포넌트 ← `GameManager.cs:195~204`(세로 1080 캡) · `:285~291`(탭/드래그 임계 DPI) |
| 퇴장 | `Hud/CoreMenuPopup.cs`(「나가기」·「성적 확정」 — 파일 헤더 `:18` 이 5c 로 미뤘고 5c 가 안 만들었다) |
| 기록 | `Logging/BattleLogger.cs`(재사용 — 덱 스냅샷 `DeckInfoJson:421`) |
| 온보딩 | `BattleCoreUnity/Hud/CoreFirstRunGuide.cs` ← `UI/Tutorial/FirstRunTutorialController.cs`(908줄) — **사용자 결정 ①** |
| 로비 전환 | `Core/SceneNames.cs:8` `Battle = "BattleScene"` → `"BattleCoreScene"` · `ProjectSettings/EditorBuildSettings.asset`(옛 씬을 빌드 목록에서 빼고 새 씬을 넣는다 — 파일은 unit 9 까지 존치) |
| 테스트 | `Tests/PlayMode/` 의 진입 4건(`OutgameFlowSmokeTest`·`PresetCarryInTest`·`SquadCarryInSmokeTest`·`SceneTransitionSmokeTest`) → 새 씬 대상으로 `Tests/PlayModeCore/` 로 |

## 구현 — 옛 규칙과 그 새 자리

| 옛 규칙(rule-holders) | 옛 코드 | 새 자리 |
|---|---|---|
| G3 판 시드는 판당 한 번 — 고정 노브 ≠ 0 이면 그 값, 아니면 새 난수 | `GameManager.cs:248~254` | `MatchEntry` → `ModeSelection.Seed` |
| G5·G7 테스트 모드 > 저장 편성, 랜덤 채움 없음 · 못 찾는 id 는 그 슬롯만 빠진다 | `:305~316` · `StartSquadMatch` | `MatchEntry` → `Build(defenders:)`. 드라이버 `_defenders` 는 **에디터 직접 진입 폴백**으로만 남는다 |
| G9 코스트 계열이 아닌 돌만 유닛 버프 · G10 코스트 돌 = 충전 배율, 판 진입 때만 | `:416·422` · `ResolveEquippedStones:613`·`ResolveCostRateMultiplier:632` | `Build(… costRateMultiplier:)` 실값 + 돌 버프 입력 |
| G11 온보딩 판 = 저작 웨이브 + 첫 손패 고정 + 보너스 억제(무조건 설정) | `:393~409` | `MatchEntry` → 플랜·`pinnedFront`(7c 의 칸)·`WaveScheduler.BonusPullSuppressed` |
| G13 테스트 모드 = 저작 플랜 + 저장 편성(비면 프리셋), 1회 소비 | `:447~480` | `TestModeContext` 를 `MatchEntry` 가 읽는다 |
| 맵 풀 4갈래: dev 강제 > 디버그 고정 시드 > 서버 토너먼트 시드 > 0번 · 맵·덱·플랜은 같은 인덱스로 잠긴다 | `BattleBridge.cs:1263~1300` | `MatchDefinitionBuilder`(순수 선택 `MapPoolSelect` 재사용) · dev 슬롯(rules M17) 포함 |
| G21 토너먼트 참가는 로비가 발행한 것만 채택 | `GameManager.cs:240~243` → `TournamentMatchReporter.BeginMatch` | 새 씬 진입 1회 |
| G22·G23 반입 편성·돌을 미리 기록(못 찾는 id 도 id 로) | `:585 PersistTournamentDeckSnapshot` · `:535 LogDreamstoneCarryIn` | `BattleLogger` 재사용 — 5c 「제출 payload 의 덱 스냅샷」 해소(rules X28) |
| G15 「한 판 해봤다」 = 판당 1회, 결과·나가기 두 통로 | `:110·146 RecordMatchPlayed` · `MenuPopup.cs:160` | 결과 사건 + 나가기 버튼(래치 1) |
| 나가기 = 참가 포기 0점 제출(덱 포함) → 기록 → 로비 · 제출 열린 뒤엔 「성적 확정」 | `MenuPopup.cs:136~161` | `CoreMenuPopup`(제출 = 커맨드 `Submit`) |
| G17 60프레임·수직동기 끔·트윈 풀 400 — **앱 시작 훅** | `GameManager.cs:176~189` | `AppBootstrap` — `GameManager` 파일이 지워지면 **로비까지 30fps 로 떨어진다**(주석 `:171`) |
| 세로 1080 캡 + 기기 가로비 | `:195~204` | 새 씬 초기화 |
| G18 탭/드래그 임계를 DPI 로 올린다(낮추지 않는다) | `:285~291` | 새 씬 초기화 |
| G20 씬이 꺼지면 기록 세션을 닫는다 | `:645 OnDisable` | 새 씬 초기화의 짝 |

- 순서는 옛 것 그대로다: **시드 → 기믹 → 맵**(G3·G4 주석), 반입 기록은 배치 **전**(앱이 죽어도 그 판이 편성을 갖는다, G22).
- 온보딩(결정 ① 기본값 = 옮긴다): 가이드 문구·포커스·홀드는 `TutorialGuidanceView`(도구 — 76038c26 이 사용자 결정으로 보존)를 그대로 쓰고, 컨트롤러만 읽기 모델·커맨드 receipt 로 다시 쓴다. 로비 쪽 첫 판 우회(`OutgameMenuController.cs:254~293`)는 무변.
- 로비 전환은 **마지막 커밋**이다 — 위가 전부 초록일 때 상수 한 줄 + 빌드 설정.

## 이식 제외

| 안 옮긴 것 | 이유 | 등급 |
|---|---|---|
| 편성 없음 → 뽑기 폴백(G6·G8) | 계약 9 | 제거(선행) |
| 조준 모드 배타(G16) | 스킬 탭 조준은 7b·7c 의 카드 입력이 대신했다 | 확인 — 새 입력에 같은 배타가 있는지 테스트로 못박는다 |
| 판 안 재시작(`OnRestartRequested` dormant) | 사용자 결정 2026-09-23 「판 안 재시작 없음」 | 제거(결정) |

## 파이프라인 커버리지

N/A — 판 오브젝트의 생성→렌더 경로는 바뀌지 않는다(입력 값만 바뀐다). 맵 풀 선택이 바꾸는 것은 **어느 스테이지 프리팹**이냐이고 정거장은 `맵 스테이지/프랍` 표 그대로다.

## 완료 기준

- [ ] 로비 START → `BattleCoreScene` → 3분 → 결과 화면에 **실제 랭킹**(참가자 찾는 중 5칸이 아님) → 로비. 서버 제출 payload 에 덱이 실린다(`deckInfo` 키 존재).
- [ ] 나가기 → 0점 제출 1회 · `matchesPlayed` +1 · 로비. 결과 경로와 겹쳐도 +1(래치).
- [ ] 같은 토너먼트 시드 두 판 = 같은 맵·같은 덱(옛 `tournament-seed-map-select` 결정론). dev 강제 인덱스가 이긴다.
- [ ] 테스트 모드 패널 → 저작 플랜 판. 온보딩 계정 → 저작 웨이브·첫 손패·보너스 억제 + 가이드 순서(결정 ① 에 따라).
- [ ] 로비 콜드 스타트 `Application.targetFrameRate == 60`(옛 씬 한 번도 안 연 상태).
- [ ] `grep -rn "SceneNames.Battle\b" Assets/_Project/Scripts` 의 목적지 = 새 씬 · 빌드 설정에 `BattleScene` 없음.
- [ ] 옮긴 PlayMode 진입 4건 + 신규 진입 테스트 초록 · EditMode 선행 2 외 빨강 0 · 헤드리스 3종.
- [ ] **Android QA 빌드**(`Wassup.Editor.MobileBuild.DreamSquadMobileBuildCli.BuildAndroidQa`) 성공 + 실기기 1판(로비 → 판 → 결과 → 로비). Entities 는 아직 있다 — 두 번째 빌드는 unit 9.
- [ ] `core-reviewer` APPROVE → **사용자 플레이 4차**(조각 D 의 3차 뒤 · 질문 = 「로비에서 들어간 판이 옛 판과 같은 판인가」).
