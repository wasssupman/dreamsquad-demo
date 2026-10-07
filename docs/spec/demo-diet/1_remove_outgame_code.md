# 1 — 아웃게임 코드·테스트 삭제

## 목적

단위 0 으로 결합이 끊긴 아웃게임 코드(≈150 .cs)를 지운다. 컴파일 초록 유지.

## 변경 대상 (삭제)

| 묶음 | 경로 | .cs |
|---|---|---|
| 로그인·서버·프로필 | `Scripts/Core/Api/` · `Core/Profile/` · `Core/Squad/SquadDraw.cs` · `Core/MobileScreenOrientation.cs` · `Core/GimmickSelection.cs`(코어 `GimmickSelection` 정본과 중복, 참조 0 확인 후) | 21 |
| 시트 런타임 | `Core/AllRuntimeRefresher.cs` · `CostConfigRuntimeRefresher.cs` · `UnitStatRuntimeRefresher.cs` · `Core/Dreamcatcher/DcSheetRuntimeRefresher.cs` · `IRuntimeRefresher.cs` · `UI/Outgame/LoginAutoImport.cs`. **`Data/StatImport/` DTO · `SheetSync/` · `Editor/UnitStatImport/` 는 Q2 로 유지** — 단 런타임 적용기(`*Applier`)가 에디터 임포터에서만 쓰이는지 확인하고, 런타임 전용이면 함께 삭제 | 6 |
| 아웃게임 UI | `UI/Outgame/`(나머지 35) · `UI/DevMapOverridePanel.cs` · `UI/DevOnlyGroup.cs` · `UI/DevTray*.cs` · `UI/CostWellMath.cs`(참조 0) · `UI/KeyringSim.cs`(단위 0 에서 수학 이전 완료) | 40 |
| 결과 화면 (Q3) | `UI/ResultScreen.cs` · `UI/LeaderboardList.cs` · `UI/NoticePopup.cs` · `UI/UiOverlay.cs` · `BattleCoreUnity/CoreMatchOutcomePresenter.cs`(단위 0 에서 사건 발행으로 대체됐으면 그 잔여) | 5 |
| 씬 전환 | `Core/SceneTransition.cs` · `Core/SceneNames.cs` · `Core/AppBootstrap.cs` — 전투 참조는 단위 0 에서 콜백으로 끊김. `AppBootstrap`(60fps 캡 · PrimeTween 용량)은 **전투 씬 부트에 필요한 설정만** `BattleDriver` 또는 씬 오브젝트로 옮긴 뒤 삭제 | 3 |
| 에디터 | `Editor/MobileBuild/`(3) · `Editor/LayerLabPresetImporter.cs` · `Editor/Ralph*.cs`(2, 에이전트 하네스) | 6 |
| 테스트 | `Tests/EditMode/{Api,Profile,MobileBuild}/` · `Tests/EditMode/` 루트의 아웃게임 테스트(DeckInfo* · DreamcatcherDeckSave · FirstMatchTournamentBypass · HistoryPresetApplyRouting · LoadoutGate · ProfileStore* · ResultLeaderboardModel · ResultScreenStatText · SquadDraw · TournamentHistory* · DevTrayToggle · BattleLogger* · CardsTabCooldown · LobbyEntryAfterDecision4) · `Tests/PlayMode/`(3 + asmdef) · `Tests/EditMode/UnitStatImport/`(13)는 **Q2 유지** | ~40 |
| 사망 확인 후 | `Logging/`(2 — `BattleLogger` 참조 0 이면) · `Rendering/PaletteSanityProbe.cs` · `Core/Dreamcatcher/{DreamcatcherCycleDeck,DreamcatcherAttachEval}.cs`(실참조 0 이면) | ≤5 |

함께: `Scripts/AssemblyInfo.cs` 의 `InternalsVisibleTo("Wassup.Tests.PlayMode")` 제거 · `.claude/hooks/guardrails.mjs` 의 `Wassup.Tests.PlayMode` 거절 규칙(어셈블리가 사라지니 무해하지만 정리) · `docs/reference/test-procedure.md` lane 표.

## 구현

- 삭제 전 각 묶음에 대해 `rg` 로 KEEP 쪽 참조 0 을 확인한다. 1건이라도 있으면 그 참조를 단위 0 의 seam 으로 되돌려 보낸다(이 단위에서 새 seam 을 만들지 않는다).
- 어셈블리 단위로 커밋을 나눠도 된다(런타임 / 에디터 / 테스트). 각 커밋 컴파일 초록.
- 씬 `OutgameScene.unity` 는 이 단위에서 **컴포넌트 참조가 missing 이 된다** — 씬 자체는 단위 2 에서 삭제하므로 여기선 건드리지 않는다(에디터 콘솔의 missing script 경고는 단위 2 까지 허용).

## 구현 결과 (2026-10-07 — 단위 0 과 한 커밋)

- 삭제 272 파일(.meta 포함). 디렉터리: `Core/Api`(`ApiEnvelope` 는 `Data/StatImport` 로 이동) · `Core/Profile` · `Core/Squad` · `Logging` · `Editor/MobileBuild` · `UI/Outgame`(`CardCategoryStyle` 은 `UI/Dreamcatcher` 로 이동) · `Tests/EditMode/{Api,Profile,MobileBuild}` · `Tests/PlayMode`.
- `UI/UiOverlay` · `Editor/LayerLabPresetImporter` 는 KEEP 쪽 참조가 있어 **남겼다**(후자는 단위 2 에서 Layer Lab 과 함께).
- `Tests/EditMode/UnitStatImport/` 는 런타임 갱신기 테스트 5(`AllRuntimeRefresh` · `DcSheetRuntimeRefresh` · `LoginAutoImport` · `UnitStatRuntimeRefresh` · `CostConfigSheet`)만 삭제, 임포터·적용기 테스트는 유지. `ApiEnvelopeFailureShapeTests` 는 여기로 이동(네임스페이스 `Wassup.Tests.EditMode.UnitStatImport`).
- 삭제된 타입을 참조하던 KEEP 테스트 수정: `BonusPullTriggerTests`(코어 `StressMath`) · `GimmickSelectionTests`(코어) · `UnitRosterInvariantTests`(`MatchEntry.FieldCount`) · `CardViewAssetTests`(카드 id 목록). 추가 삭제: `DeckInfoDisplayTests` · `DcAttachRequirementWiringTests`(아웃게임 씬 배선 검사).
- 타입이 사라진 SO 2개를 함께 삭제: `Data/Config/TestModeConfig.asset` · `LobbyKeyringSettings.asset`.
- `Scripts/AssemblyInfo.cs` 의 `InternalsVisibleTo("Wassup.Tests.PlayMode")` 제거.

## 완료 기준

- [x] `Assets/_Project/Scripts` 아래 `Wassup.Core.Api` · `Wassup.Core.Profile` 네임스페이스 0건(`Wassup.UI` 는 전투 UI 가 쓰는 네임스페이스라 남는다)
- [x] 검증 워크트리 배치: 컴파일 0 에러 · EditMode 2,240(아웃게임 테스트 410 감소) · `PlayMode.Core` 94(−15 +12, 실패 집합 HEAD 와 동일) · 헤드리스 통과
- [x] 커밋(경로 지정) — 단위 0 과 한 커밋

확인 2026-10-07.
