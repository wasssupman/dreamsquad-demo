# 1 — play 게이팅 진입

## 목적

로비 `시작` 시 play 응답을 **대기**해서, attemptId 를 확보(성공)해야만 배틀씬으로 전환한다. 실패/무응답이면 전환하지 않고 `NoticePopup` 으로 알린다. 이렇게 하면 배틀에 들어온 판은 attemptId 가 보장돼 `ReportResult` 의 "no attemptId 스킵"(정상 완주인데 서버 락만 걸리고 강제 0점) 경로가 사라진다.

## 변경 대상

- `Assets/_Project/Scripts/Core/Api/TournamentMatchReporter.cs` — await 가능한 로비 진입 추가
- `Assets/_Project/Scripts/UI/Outgame/OutgameMenuController.cs` — `OnStartGame` 게이팅

## 구현

### TournamentMatchReporter

- `BeginMatch()` 은 `BeginMatchInternal(null, null)` 로 위임(비게이트, GameManager.OnEnable 용) — **동작 불변**.
- 신규 `BeginMatchFromLobby(Action onReady, Action<string> onFailed)`:
  - 계정 세션: play 발행 → 콜백에서 **attemptId 비어있지 않으면** seed 확보·pending 저장·`_lobbyIssued=true`·`onReady()`. 응답 실패(=API 10s 타임아웃 포함)/attempt 빈값이면 `onFailed(error)`.
  - 게스트(`!HasAccount`): attempt 없음 → 게이트 비대상, `_lobbyIssued=true` + `onReady()` 즉시.
  - **무응답 방어는 `TournamentApi.Play` 의 10s 타임아웃이 담당** — 타임아웃 시 콜백이 실패로 발화하므로 별도 타이머 불필요.
  - epoch 가드 유지(`if (epoch != _epoch) return;`). 로비 await 는 재진입 차단 상태라 대기 중 epoch 변화 없음.
- 기존 `BeginMatch` 시그니처·비게이트 동작 보존(계약 9). 성공 로그 `play ok — attemptId=…` 유지.

### OutgameMenuController.OnStartGame

- gate refs / LoadoutGate 체크는 현행 유지(먼저).
- 통과 후: `if (_starting) return; _starting = true;` 로 **재진입 차단** → `NoticePopup.ShowBusy("매칭 중")` → `BeginMatchFromLobby(onReady, onFailed)`.
  - `onReady`: `_starting=false; NoticePopup.Hide(); SceneTransition.Go(SceneNames.Battle);`
  - `onFailed`: `_starting=false;` + `NoticePopup.ShowAlert("입장 실패", "…다시 시도…", onRetry: OnStartGame)`.
- busy 딤(raycast 차단)이 대기 중 다른 로비 버튼 입력도 막는다(모달). `_starting` 이 실질 재진입 가드.

## 완료 기준

- compile 통과, 콘솔 에러 0.
- 성공 경로: play 정상 → "매칭 중" 잠깐 → 배틀 진입. 진입 후 `_attemptId` 보장(reflection 확인) → 완주 시 `complete ok — score=…` (미뤘던 "완주 매치가 서버에 실점수로 남는가" 를 unit 3 에서 종결).
- 실패 경로(강제): 잘못된 baseUrl/네트워크 → 배틀 **미진입** + "입장 실패" 팝업 + 다시시도 동작.
- 게스트: 게이트 없이 즉시 진입(현행).
- 비게이트 `BeginMatch`(GameManager.OnEnable) 동작 불변.
