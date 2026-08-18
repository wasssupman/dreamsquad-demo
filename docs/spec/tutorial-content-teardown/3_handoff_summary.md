# unit 3 — handoff summary

## Commit

- (units 0~2, 한 커밋 — 해시는 커밋 후 기재)

세 유닛을 한 커밋에 담았다. 유닛 경계가 「호출자 → 피호출자」라 중간 상태가 컴파일되지 않는다 — unit 0·1 이 호출자를 걷어야 unit 2 가 `TutorialProgress` 를 지울 수 있고, 반대로 unit 2 를 미루면 그 파일이 호출자 없이 남아 teardown 이 절반만 끝난 채 main 에 들어간다.

## Implemented

- **인게임 스텝 제거** — `FirstSessionTutorialController` 5파일(1,329줄). 첫 판 시퀀스(목표·픽·배치·조준·클래스·시작) · 각성 3단계 · 배틀 HUD · 효과 타일 · 기믹 리빌 홀드.
- **로비 스텝 제거** — `OutgameTutorialController`(531줄, 챕터 A~D 8스텝) + RESET TUTORIAL 버튼(씬 오브젝트 9블록) + `Editor/FirstSessionTutorialMenu.cs`.
- **소비처 훅 제거** — `PlacementPhaseView` 게이트 3 + 홀드 상태 2 + `profileSO` · `GimmickPhaseView` 홀드 이벤트 2 + `_tutorialMode`/`_holding` + `profileSO` · **각성 봉인 체인 4단**(게이지 `SetSuppressed`/`IsSuppressed`/`_suppressed` → 손패 릴레이 → 인스펙트 가드 **3곳**).
- **진행 저장 초기화** — `TutorialProgress.cs` 파일째 삭제(스텝 API 38개) · `PlayerProfile` 튜토리얼 필드 12개 · `ProfileStore.ResetTutorialProgressAt`/`ReplaceWithBackup`.
- **술어 축소** — `PlacementPhasePolicy.CanFinish(interactionBlocked)` · `UseAutoStart(placementPhaseEnabled)`. `match-intro-phase-toggles` 계약 6 은 **폐기**로 개정했다(그 spec README).
- **첫 판 토너먼트 우회 신호 교체** — `TutorialProgress.ShouldRunCore` → `OutgameMenuController.IsFirstMatch(profile)` = `matchesPlayed == 0`.
- **도구는 남았다** — `TutorialGuidanceView` · `TutorialGuidanceStyle` · `OutgameTutorialOverlay`/`DimLayout`/`TapZone` + 그 테스트.

## Key Files

- `Assets/_Project/Scripts/UI/PlacementPhaseView.cs` — `PlacementPhasePolicy`(맨 아래) · `TickAutoStart` 게이트
- `Assets/_Project/Scripts/UI/Outgame/OutgameMenuController.cs` — `IsFirstMatch` + `OnStartGame` 의 우회 분기
- `Assets/_Project/Scripts/Core/Profile/PlayerProfile.cs` — 남은 것은 `matchesPlayed` 하나
- `Assets/_Project/Tests/EditMode/FirstMatchTournamentBypassTests.cs` — 우회가 뒤집히지 않게 못 박는 그물

## Verified

- 컴파일 에러 0. 타입 실측: `TutorialProgress`·컨트롤러 2종 = **부재**, `TutorialGuidanceView`·`OutgameTutorialOverlay` = **생존**, `OutgameMenuController.IsFirstMatch` = 존재, `PlayerProfile.firstBattleTutorialVersion` = 부재 / `matchesPlayed` = 존재.
- EditMode **2,494 중 실패 0**(사전 skip 3). 삭제한 테스트만큼 총수가 2,549 → 2,494.
- 두 씬 로드: **미싱 스크립트 0**. `BattleScene` 의 도구는 `TutorialGuidance` 오브젝트에, `OutgameScene` 의 `Dim`(Overlay)·`Guidance` 는 `TutorialTools` **자식으로 제자리**. RESET TUTORIAL 버튼 부재.
- Play(`placementPhaseEnabled=false`): `3 → 2 → 1 → GO!`, `t=2.99` Battle 전이 · 차단막 동시 해제 · 트레이 생존.
- Play 중 안내 노출 0 — `TutorialOverlay·Message·Text·Skip·Label·FocusRing·Pointer` 전부 비활성(캔버스 루트만 활성).
- 콘솔 에러 0.

## 구현 중 잡은 결함 2건 (자가 리뷰)

1. **세션 가드 누락** — `IsFirstMatch` 로 신호를 옮기며 옛 술어의 `IsLoadedThisSession` 을 빠뜨렸다. 미로드 프로필은 빈 인메모리 인스턴스라 `matchesPlayed == 0` 으로 읽혀 **우회가 켜지고 정상 유저의 판이 토너먼트에서 빠진다**. 호출부에 가드를 복원했다.
2. **씬 계층 파손** — 컨트롤러 GameObject 를 통째로 지우면 **도구가 함께 죽거나 고아가 된다**. `BattleScene` 은 `TutorialGuidanceView` 가 **같은 오브젝트**에, `OutgameScene` 은 `Dim`·`Guidance` 가 **자식**으로 붙어 있었다. 둘 다 컴포넌트만 걷고 오브젝트는 이름만 바꿔 살렸다(`TutorialGuidance` · `TutorialTools`).

## Notes (되돌리면 안 되는 의도)

- **`IsFirstMatch` 를 지우거나 뒤집지 말 것.** 계정 첫 판을 토너먼트에 올리면 서버 `complete` 500 이 그대로 노출된다(`tutorial-offline-match`). 서버가 고쳐졌다는 확인 없이 걷지 않는다.
- **`matchesPlayed` 는 튜토리얼 필드가 아니다.** 그래서 이 teardown 에서 살아남았고 지금은 「첫 판」의 유일한 소유자다. `GameManager` 가 Result 전이와 나가기 두 경로에서 올린다.
- **도구 계층은 호출자 0 이어도 남긴다**(계약 1, 사용자 결정). 재설계가 이걸 다시 쓴다 — «미사용이니 지운다»로 되돌리지 말 것.
- **`BattleScene` 의 `TutorialGuidance` 오브젝트**는 이름만 바뀌었고 `TutorialGuidanceView` 가 그대로 붙어 있다. 튜토리얼 컨트롤러와 **같은 오브젝트를 공유**하고 있었으므로, 오브젝트째 지우면 도구까지 사라진다(실제로 한 번 그렇게 지웠다가 되돌렸다).
- `PlacementPhaseView.TickAutoStart` 의 `CanFinishPlacement()` 게이트는 튜토리얼과 무관하게 남는다 — 드래그/조준 중 종료 금지 + 종료 가드와의 술어 일치(`a1392b4d`).

## Follow-up

- **튜토리얼 재설계** — 이 spec 은 자리를 비우는 것까지다.
- `TutorialGuidanceStyle` · `GimmickRevealConfig.tutorialHoldFallbackSec` 등 **사라진 스텝 전용 SO 필드** 정리 — 재설계가 무엇을 쓸지 정해진 뒤.
- `docs/spec/README.md` Follow-up Backlog 의 `first-session-tutorial` · `outgame-tutorial` 항목들이 stale 해졌다. 재설계 spec 이 서면 그때 정리.
- 씬 YAML 에 남은 죽은 키(`profileSO:` on PlacementPhaseView/GimmickPhaseView, `outgameTutorial: {fileID: 0}`) — Unity 가 다음 씬 저장에서 스스로 떨어뜨린다.
- 온보딩 없는 상태의 첫 판 이탈률 관측 — 재설계의 기준선.
