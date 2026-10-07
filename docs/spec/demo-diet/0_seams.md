# 0 — 전투 입구·출구 seam

## 목적

전투 Unity 층이 아웃게임을 **직접 읽거나 부르는 곳 6군데**를 값/사건으로 바꾼다. 이 단위가 끝나면 단위 1 의 삭제가 KEEP 쪽을 하나도 깨뜨리지 않는다. 아웃게임 코드는 아직 남겨 두고 새 seam 에 맞춰 호출만 바꾼다(단위 1 에서 통째로 사라진다).

## 변경 대상

1. **`BattleCoreUnity/MatchEntry.cs`** (`:51,71-75,91-92,134,178`)
   - 입력 타입 `MatchEntryInput` 신설(값 타입): `defenderIds[]`(또는 `DefenderUnitData[]`) · `dreamstoneIds[]` · `deckCardIds[]` · `WavePlanAsset planOverride` · `DefenderUnitData[] rosterOverride` · `int mapIndexOverride(-1=off)` · `bool hasSeed, int seed`.
   - `PlayerProfileSO` · `CommittedSquad()` · `SquadDraw.Resolve` · `TestModeContext.*` · `Core.Api.TournamentDeckInfo.Serialize` 읽기 제거. `MatchEntryPlan` 은 `MatchEntryInput` 에서만 만든다.
   - 덱 스냅샷 JSON 은 전투 몫이 아니다 — `deckCardIds` 값만 보관, 직렬화는 호출자.
2. **`BattleCoreUnity/BattleDriver.cs`** (`:270,316,324-326,400`)
   - `TournamentMatchReporter.BeginMatch/PersistMatchDeck` 호출 → 사건 `MatchStarted` · `DeckLocked(deckCardIds)` 발행.
   - `TournamentMatchReporter.HasTournamentSeed/TournamentSeed` · `DevMapOverride.Index` → `MatchEntryInput.hasSeed/seed` · `mapIndexOverride`.
   - 기본 입력 제공처: 직렬화 `[SerializeField] MatchEntryConfig _defaultEntry`(SO: 기본 스쿼드 7 · 드림스톤 · 덱 · 맵 인덱스 · 고정 시드) — Demo 에서 BattleCoreScene 을 바로 Play 할 때 쓴다. 외부 호출자(테스트·에디터 런처·훗날 somnia App)는 `BattleDriver.Begin(MatchEntryInput)` 으로 덮어쓴다.
3. **`BattleCoreUnity/CoreMatchOutcomePresenter.cs`** — 「성적 받기」만 남긴다: `MatchOutcome` 을 사건 `MatchFinished(MatchOutcome)` 으로 발행하고 `TimeManager` 홀드(`:182`)만 수행. `ResultScreen`·프로필 저장·`ReportResult/AbandonMatch`·`UserSession`·`NoticePopup`·`SceneTransition.Go` 호출 제거. (파일 자체는 단위 1 에서 `ResultScreen` 과 함께 삭제되거나, 사건 발행자로 개명 — 구현 시 판단.)
4. **`Hud/CoreMenuPopup.cs:163`** — `SceneTransition.Go(SceneNames.Outgame)` → `[SerializeField] UnityEngine.Events` 금지 규칙에 따라 **C# 콜백/인터페이스 1개**(`IMatchExitHandler` 또는 `Action onExit`) 주입. 핸들러가 없으면 버튼 비활성.
5. **`UI/KeyringSim.cs`** 수학(`SpringStep` · `FlightTimeRemap` · `DismountPoint`) → `Presentation/SpringMath.cs`(가칭) 로 이동. 호출처 `CameraDirector:610,704,724` · `CoreHandView:350` · `CoreDeployFlightPresenter:147` 갱신. 아웃게임 키링은 단위 1 에서 사라지므로 옛 파일은 그때 삭제.
6. **`Core/StressMath`(옛 사본)** 소비처 `Data/AttackDeck` · `BonusWaveData` · `BonusPullTrigger` · `UI/ResultScreen` → 코어 `Owners/StressMath` 로 using 교체. 옛 파일 삭제.
7. **개발 statics 접기 (Q4)**: `TestModeContext` → `MatchEntryInput.planOverride/rosterOverride`; `DevMapOverride` → `mapIndexOverride`. writer 교체: 에디터 `WavePlanTestLauncher`(SessionState GUID → 입력으로) · PlayMode.Core 테스트 3(`CoreMatchEntryTests` · `RetiredBeamPortTest` · `RetiredSpriteBackendPortTest`: `TestModeContext.Set(null, roster)` → 입력 값) · 아웃게임 `TestModePanelView`/`DevMapOverridePanel` 은 단위 1 삭제 대상이라 이 단위에선 컴파일만 유지(입력 경유로 최소 교체). `Core/TestModeContext.cs` · `Core/DevMapOverride.cs` · `Data/TestModeConfig.cs` + `Data/Config/TestModeConfig.asset` 삭제.

## 구현

- `MatchEntryInput` 은 **값 스냅샷**이다(코어 「커맨드 ≠ 사건」 원칙의 Unity 층 판). 프로필·static·PlayerPrefs 를 모른다.
- 사건 채널은 `BattleDriver.Subscribe` 선례를 따른다 — 코어 사건이 아니라 **Unity 층 사건**이므로 `CoreEventKind` 를 늘리지 않는다(골든 무영향).
- 기존 테스트 `MatchEntryBuildTests` · `LiveDefinitionSmokeTests` · `LobbyEntryAfterDecision4` 가 프로필 경로를 단언하면 입력 값으로 바꾼다. 아웃게임 전용 단언은 단위 1 에서 테스트와 함께 삭제.
- 세 작업 — `KeyringSim` 이동 · `StressMath` 접기 · statics 접기 — 는 순수 이동/치환이라 먼저 하고, `MatchEntry`/`BattleDriver` seam 을 그 다음에.

## 구현 결과 (2026-10-07)

- `MatchEntryInput`(새 파일): `Kind` · `UnitIds/StoneIds/DeckCardIds` · 직접 에셋 `Defenders/Stones` · `PlanOverride` · `MapIndexOverride` · `HasMapSeed/MapSeed`. `MatchEntry.Resolve(input, 카탈로그 2, fixedSeed, selectionSeed)` 가 푼다. `SquadDraw` 의 중복 제거·7칸 상한은 `MatchEntry.ResolveUnitIds`/`FieldCount` 로 들어왔다. 덱 스냅샷 직렬화(`DeckInfoJson`)는 사라지고 `BattleDriver.LockedDeckCardIds` 만 남는다.
- `MatchEntryContext` 가 선택 + 입력을 한 칸에 싣는다(`Set(selection, input)` · `Consume(out input)`). 에디터 「Test this plan」 캐리(SessionState GUID)는 `TestModeContext` 에서 여기로 옮겼다. `DevMapOverride`·`TestModeContext`·`TestModeConfig` 삭제.
- `BattleDriver`: `Begin(ModeSelection, MatchEntryInput)` · 사건 `MatchStarted` · `DeckLocked` · `MatchFinished(MatchOutcome)` · `MatchAbandoned` · `Abandon()`(래치). `_profile` 필드 제거 — 씬 YAML 의 `_profile:` 줄은 고아로 남는다(단위 1 씬 정리 때 함께).
- `CoreDeckComposition.Compose/ResolveAttachDeck` 는 프로필 대신 `IReadOnlyList<string>` 카드 id 를 받는다.
- `CoreMatchOutcomePresenter` → **`CoreMatchEndBeat`**(`git mv`, GUID 유지 → 씬 컴포넌트 생존): 붕괴 박자(시간 리스)만. 통보·기록·표시·씬 복귀 제거. `CoreHudGate` 는 바깥이 `Hide(bool)` 로 부르고 `MatchStarted` 에서 자동 복구(결과 화면이 없는 판은 HUD 가 남아 마지막 점수를 보여 준다).
- `CoreMenuPopup` 나가기 → `_driver.Abandon()`. `_outcome` 필드 제거(씬 YAML 고아 줄).
- `UI/KeyringSim` → `Presentation/MotionMath`(SpringStep 3종 · CubicBezier · DismountPoint · FlightTimeRemap; 로비 전용 `FallStep`·`LeanAngle`·`ThrowArcControls` 삭제). 호출처 5 파일 갱신.
- 테스트: `CoreMatchEntryTests`(로비 씬 기반) → `CoreMatchEntryCarryTests`(입력 기반 8케이스) · `SquadDrawTests` → `MatchEntryUnitIdsTests` · `KeyringSimTests` → `MotionMathTests` · `CoreMatchOutcomeTests`/`CoreScreenTransferTests` 사건·게이트 기준으로 수정 · `Retired*PortTest` 픽스처는 `driver.Begin(ModeSelection.None, input)` 직접 호출.

## 완료 기준

- [x] `rg 'PlayerProfileSO|TournamentMatchReporter|TournamentDeckInfo|UserSession|TestModeContext|DevMapOverride|SceneTransition|SceneNames' Assets/_Project/Scripts/BattleCoreUnity Assets/_Project/Scripts/Presentation` → 0건
- [x] BattleCoreScene 이 입력 없이(드라이버 저작) 판을 시작한다 — `MatchEntryConfig` SO 는 **만들지 않았다**: 드라이버 저작 필드(`_defenders`·`_dreamstones`·`_cards`·`_seed`)가 이미 기본 입력이다
- [x] 검증 워크트리 `wt47`(4.7) 배치: 컴파일 0 에러 · EditMode 2,240(선행 3 + CRLF 9 외 초록, 새 빨강 0) · `PlayMode.Core` 94 중 87 초록 — 빨강 7 은 **패치 없는 HEAD 에서도 같은 7**(배치 환경의 드래그 미리보기 테스트, `test-procedure.md` 「PlayMode 판정은 에디터 실행으로」) · 헤드리스 Core 1,038/0/4 · Check 빌드 통과
- [x] 커밋(경로 지정) — 단위 1 과 한 커밋

확인 2026-10-07 — 커밋 해시는 단위 1(에셋) 커밋 때 README 상태 줄에 함께 기록.
