# 0 — 튜토리얼 판은 참가 신청을 발행하지 않는다

## 목적

첫 인게임 튜토리얼이 뜨는 판에 한해, 로비 `시작` 이 서버 참가 신청(`play`)을 건너뛰고 곧장 배틀로 보낸다. 그 판은 attempt 를 갖지 않으므로 점수 제출·나가기 마감도 일어나지 않는다(하위 경로는 무변경 — attemptId 부재로 이미 스킵된다).

## 변경 대상

- `Assets/_Project/Scripts/UI/Outgame/OutgameMenuController.cs` — `OnStartGame`
- `Assets/_Project/Scripts/Core/GameManager.cs` — `RecordMatchPlayed` 주석(세는 기준에 튜토리얼 판을 명시)

테스트는 **새로 만들지 않는다.** 이 unit 이 더한 것은 MonoBehaviour 안의 라우팅 분기 한 개이고, 그 분기가 묻는 술어(`ShouldRunCore`)는 `TutorialProgressTests` 가 이미 네 방향(pending / 완료 / 미로드 홀더 / null 프로필)으로 덮고 있다. 판정을 순수 함수로 따로 빼서 테스트를 붙이는 것은 호출처 하나짜리 한 줄 술어에 대한 과잉 추상화다(CLAUDE.md 제약 8 · 원칙 10 단서).

## 구현

`OnStartGame` 의 로드아웃 게이트 **뒤**, 참가 신청 게이트 **앞**에 분기 하나를 둔다.

- 판정: `TutorialProgress.ShouldRunCore(profileSO)` (README 계약 1 — 배틀 씬과 같은 호출).
- 참이면 `NoticePopup.ShowBusy` 도 `BeginMatchFromLobby` 도 부르지 않고 `SceneTransition.Go(SceneNames.Battle)`.
- 재진입 가드(`_starting`)는 이 경로에 **걸지 않는다**: 대기할 왕복이 없어 풀어 줄 지점이 없고(게이트 경로는 콜백에서 푼다), 연타 이중 로드는 `SceneTransition` 자신의 `_transitioning` 가드가 막는다.

순서가 계약이다. 로드아웃 게이트가 먼저여야 미충족 상태로 튜토리얼 판에 들어가지 않는다.

배틀 씬 쪽은 손대지 않는다. `GameManager.OnEnable → BeginMatch()` 가 adopt 할 것(`_lobbyIssued`)이 없어 리포터 상태를 리셋하므로, **직전 판의 attemptId 가 튜토리얼 판으로 새지 않는다** — TestMode 진입이 이미 이 경로다(`tournament-flow-guards` unit 8).

## 완료 기준

- 컴파일 클린.
- EditMode 코어 레인 무회귀(`TutorialProgressTests` 포함).
- 라이브(실계정 로그인): 튜토리얼 판 진입 시 콘솔에 `[TournamentReporter] play ok` 가 **없다**. 그 판을 나가기로 끝내고 로비 `시작` → 정상 입장하며 그때는 `play ok` 가 찍힌다.
- 튜토리얼을 마친 계정의 다음 판은 종전대로 참가 신청·점수 제출이 일어난다.
- 콘솔에 `[BattleBridge] map pool index=0 ... (source=fallback0)` — 계약 5 의 의도된 결과(단, 라이브 풀이 1장인 지금은 종전과 같은 맵이다).

확인: 2026-08-18 — EditMode 코어 2355 (통과 2352 / 실패 0 / 스킵 3 = 기존 ignore) · 컴파일 클린 · **사용자 라이브 확인 완료**. 코드리뷰에서 나온 3건(문서 계약 5 정정 · `tournament-flow-guards` 계약 드리프트 · `no attemptId` 경고 문구)도 같은 커밋에 포함.
