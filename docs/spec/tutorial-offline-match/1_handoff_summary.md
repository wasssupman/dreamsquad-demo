# 1 — Handoff Summary

## Commit

- `1ed41336` — feat(tutorial-offline-match): unit 0 — 첫 튜토리얼 판은 토너먼트에 올리지 않는다

## Implemented

- 로비 `시작` 이 **첫 인게임 튜토리얼이 뜨는 판**에 한해 참가 신청(`play`)을 발행하지 않고 곧장 배틀로 보낸다. 판정은 `TutorialProgress.ShouldRunCore(profileSO)`.
- 그 판은 attempt 를 갖지 않으므로 점수 제출·나가기 마감·덱 기록이 **attemptId 부재로 스스로 스킵**된다. 하위 경로는 한 줄도 안 고쳤다.
- 결과: 튜토리얼 판이 **게스트 판과 같은 모양**이 된다(결과 화면의 `BuildPendingRows` 폴백이 이미 그 상태를 그린다).
- `tournament-flow-guards` README 계약 1·2 에 rev 한 줄 — 비게이트 진입이 둘(게스트 + 튜토리얼 판)이 됐다.
- `ReportResult` 의 `no attemptId` 경고 문구를 세 원인(참가 신청 미발행 / play 실패 / 왕복 중)으로 정정.
- `GameManager.RecordMatchPlayed` 주석 — 튜토리얼 판이 "히스토리엔 없는데 세는" 쪽에 합류함을 명시.

## Key Files

- `Assets/_Project/Scripts/UI/Outgame/OutgameMenuController.cs` — `OnStartGame` 의 분기(로드아웃 게이트 뒤, 참가 신청 게이트 앞)
- `Assets/_Project/Scripts/Core/Api/TournamentMatchReporter.cs` — 발행/마감 창구. 이번엔 경고 문구만 변경
- `Assets/_Project/Scripts/Core/Profile/TutorialProgress.cs` — `ShouldRunCore` (로비·배틀이 공유하는 유일 술어)
- `Assets/_Project/Scripts/Core/GameManager.cs` — `OnEnable → BeginMatch()` (attemptId 누수 차단 지점) · `RecordMatchPlayed`

## Verified

- EditMode 코어 `Wassup.Tests.EditMode` 2355 — 통과 2352 / 실패 0 / 스킵 3(전부 기존 ignore). 52초.
- 컴파일 클린(콘솔 에러 0).
- 사용자 라이브 확인 완료 (2026-08-18).
- 통로 전수: `OnStartGame` 은 씬 바인딩 1개·코드 호출부 0개(로비 튜토리얼 오버레이는 버튼을 대신 누르지 않는다). 배틀 씬 안에서 리포터를 만지는 5개 지점 전부 attemptId 부재에 안전. `OnStartGame`/`BeginMatch` 를 부르는 PlayMode 테스트는 없다.

## Notes

- **되돌리지 말 것 — 술어를 공유한다.** 로비의 "서버에 안 올릴 판"과 배틀의 "튜토리얼 띄울 판"은 같은 `ShouldRunCore` 호출이어야 한다. 별도 플래그를 만들면 두 값이 언젠가 갈리고, 그때 "튜토리얼은 떴는데 점수는 올라간" 판이 생긴다.
- **새 상태를 만들지 않았다.** 발행 창구는 여전히 `BeginMatchFromLobby` 하나(`tournament-flow-guards` unit 8). 튜토리얼 판은 그 창구를 안 부를 뿐이고, 배틀 진입의 `BeginMatch()` 가 adopt 할 게 없어 리셋하므로 직전 판 attemptId 가 새지 않는다 — TestMode 와 완전히 같은 경로다.
- `_starting` 재진입 가드를 이 분기에 걸지 않았다. 기다릴 왕복이 없어 풀 지점이 없고, 연타 이중 로드는 `SceneTransition` 자신의 `_transitioning` 가드가 막는다.
- **맵 고정은 오늘 기준 무의미하다.** 시드 부재 → `fallback0` 이지만 라이브 풀(`MapDocumentPool.entries`)이 1장이라 종전에도 모두 0번이었다. 풀이 2장 이상이 되면 그때부터 "튜토리얼 판만 항상 0번"이 실동작이 된다.
- 튜토리얼이 **fail-open** 된 첫 판(참조 누락·affordable 슬롯 부재)도 오프라인이다. 배틀 쪽이 이미 "그 경로도 첫 판은 소비된 것으로 본다"는 같은 결정을 내려 뒀다.

## Follow-up

- **서버 `complete` 500 (클라 범위 밖).** 이 변경은 벽을 한 판 뒤로 밀 뿐이다 — 튜토리얼 다음 실전 판을 끝내면 다시 락이 걸린다. 서버팀 이관: `POST /tournament/complete/{attemptId}/{score}` 가 유효한 attempt 에 대해 결정적으로 500(traceId `KYvxTlDQ`·`NP60pt5b`·`S4d09grq`·`nqM2tGvJ`, 2026-08-17). 잘못된 attemptId 에는 정상 400 을 주므로 검증부가 아니라 마감 로직이다.
- **락 팝업 문구.** 자동 복구까지 실패한 락에 "잠시 후 '시작'을 다시" 는 거짓 안내다(실제 복구는 라운드 롤오버). 튜토리얼 밖에서는 여전히 이 팝업을 만난다.
- **나가기/reconcile 의 complete 실패가 무성이다.** `Debug.LogWarning` 만 남고 사용자에겐 완주 제출 실패만 알린다.
- **`no attemptId` 경고 레벨.** 지금은 정상 경로(TestMode·직접 Play·튜토리얼 판)에서도 Warning 이다. 낮추려면 "이 판이 로비 게이트를 거쳤나"를 리포터가 들어야 해서, 새 상태를 감수할 값이 있는지 별도 판단이 필요하다.
