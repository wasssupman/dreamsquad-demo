# 2 — score 전송 실패 알림

## 목적

정상 완료 매치의 complete(점수) 전송이 **실제로 실패**하면 `NoticePopup` 으로 알린다. 점수 전송 로직 자체는 손대지 않는다(현행 그대로). 논블로킹 — 결과 화면/로컬 점수/씬 전환에 무영향, **재시도 없음**(단순 안내).

## 변경 대상

- `Assets/_Project/Scripts/Core/Api/TournamentMatchReporter.cs` — `ReportResult` 에 `onError` 콜백 추가
- `Assets/_Project/Scripts/Bridge/BattleBridge.cs` — `ReportMatchResult` 가 실패 시 팝업 배선

## 구현

- **레이어링**: 리포터(`Core.Api`)는 UI 를 참조하지 않는다. `ReportResult` 에 `Action<string> onError = null` 추가하고 **Complete 콜백의 실제 실패(`!ok`) 분기에서만** `onError?.Invoke(error)`. 전제 미스(게스트/`_attemptId` 없음/`_completeSent`)의 조기 return 은 API 호출 실패가 아니므로 **onError 발화 안 함**(사용자 표현 "실제로 api 호출 실패 시" 그대로).
  - 403/401 토큰 갱신 재시도는 `TournamentApi.Attempt` 가 이미 처리 → `!ok` 는 갱신 후에도 실패한 진짜 실패.
  - epoch 가드 유지 → 새 매치 시작 후 도착한 stale 실패는 드롭(엉뚱한 시점 팝업 방지).
- **팝업 배선**: `BattleBridge.ReportMatchResult` 가 `onError: _ => NoticePopup.ShowAlert("점수 전송 실패", "…", null)`. Bridge 는 이미 UI(`ScoreTallyView`/`ScoreHudView`) 참조 → 레이어 위반 없음. 재시도 없음(닫기 단독 = gold).
- 팝업은 DontDestroyOnLoad(sorting 3000) 라 결과 화면(2000) 위에 뜨고, 닫으면 결과가 그대로 남는다(논블로킹).

## 완료 기준

- compile 통과, 콘솔 에러 0.
- `ReportResult` 의 정상/게이트 동작 불변(onRanking 경로 유지). onError 는 optional 이라 기존 호출 안전.
- 강제 실패 재현(complete 응답 실패): "점수 전송 실패" 팝업 + 결과 화면 잔존(닫기 시 결과 보임) — unit 3 에서 종결.
- 정상 전송 시 팝업 안 뜸(`complete ok — score=…` 만).
