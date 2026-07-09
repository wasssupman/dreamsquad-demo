# 3 — TournamentMatchReporter 배선

## 목적

play/complete 호출을 실제 게임 흐름에 연결한다. 배틀 진입·RESTART 시 play, 결과 팝업 시 complete(점수 + 배틀 로그 JSON), complete 성공 시 결과 조회(`GetResult`) 체인까지. 게스트/실패는 게임을 막지 않는다.

선행: unit 0 (TournamentApi), unit 1 (SnapshotJson), unit 2 (REDRAFT 제거 — 재시작 경로가 `OnRestartRequested` 하나로 수렴하는 전제).

## 변경 대상

- `Assets/_Project/Scripts/Core/Api/TournamentMatchReporter.cs` (신설, static — `UserSession` 선례. MonoBehaviour 불필요: `UnityWebRequest.SendWebRequest().completed` 콜백은 코루틴 없이 동작, `UserSignApi` 검증됨)
- `Assets/_Project/Scripts/Core/GameManager.cs` — 배틀 진입 시 `BeginMatch()` (logger.StartSession 옆)
- `Assets/_Project/Scripts/Bridge/BattleBridge.cs` — `OnRestartRequested` 에서 `BeginMatch()`, 결과 확정 3곳에서 `ReportResult(...)`

## 구현

- 상태: `_epoch`(int), `_attemptId`, `_entryId`, `_completeSent`.
  - `BeginMatch()`: `UserSession.IdToken` 비어 있으면 스킵(게스트 포함). **`_epoch++`, `_attemptId = _entryId = null`, `_completeSent = false` 를 Play 발사 전에 리셋** (critic M2 — 리셋 없이는 두 번째 판부터 complete 가 조용히 막힌다). 이전 판의 미완료 attempt 는 폐기(complete 미전송 계약). **모든 비동기 응답 콜백(play/complete/GetResult)은 발사 시점의 epoch 를 캡처해 현재 `_epoch` 와 불일치하면 폐기** — 단일 stale-응답 규칙 (`LoginPanelView` 의 auth epoch 선례). play race 자체는 현 흐름에서 사실상 불가능하지만(RESTART 는 결과 팝업에서만, timeout 10초), 결과 팝업 직후 RESTART 시 이전 판의 complete/랭킹 응답이 몇 초 뒤 도착하는 창은 실재한다 — 콜백별로 위험도를 따로 추론하지 않고 한 규칙으로 통일 (2026-07-08 리뷰 후 사용자 논의 반영). 일치 시 `_attemptId`/`_entryId` 저장 + 로그.
  - `ReportResult(int score, string battleLogJson, Action<TournamentApi.ResultData> onRanking)`: `_attemptId` 없음(게스트/미로그인/play 실패/응답 미도착) 또는 `_completeSent == true` 면 LogWarning 후 스킵. play 응답 대기 중 결과가 먼저 뜨는 판은 데모 특성상 버린다 — 큐잉하지 않는다. `_completeSent = true` 로 마킹 후 `Complete` 호출. **성공 시** `_entryId` 로 `TournamentApi.GetResult` 체인 → 응답을 epoch 검사 후 `onRanking` 으로 전달 (UI 소비는 unit 4). complete/조회 실패는 로그만 남기고 무영향.
- 호출 지점:
  - `GameManager.OnEnable` — `logger.StartSession()` 직후 `BeginMatch()` (배틀 씬 진입 1회).
  - `BattleBridge.OnRestartRequested` — `StartReplacementSession("restart", ...)` 직후 `BeginMatch()`. unit 2 이후 이곳이 유일한 재시작 경로다 (`RestartBattle` 은 unit 2 에서 제거됨).
  - 결과 확정 3곳 (BattleBridge ~L2549 defeat / ~L2572 victory_timeout / ~L2590 victory): `SetResult`/`SetScore` **이후** `logger.SnapshotJson()` → `ReportResult(playerScore, snapshot, onRanking)`. 3곳 공통 헬퍼로 묶는다. `onRanking` 은 unit 3 시점에서는 수신 로그만 (UI 배선은 unit 4).
- 씬 배선 없음 (static + 기존 컴포넌트 수정만). unity-feature-wiring 스킬의 씬 오브젝트 단계 N/A.

## 완료 기준

- [ ] compile 통과
- [ ] 에디터 Play (로그인 상태): 배틀 진입 시 play 성공 로그 + attemptId/entryId, 결과 팝업 시 complete 성공 로그 + 결과 조회 응답 로그 (entries 수)
- [ ] RESTART: 새 attemptId 발급 로그, 이전 판(결과 전 재시작)은 complete 미전송, 결과 팝업 → RESTART → 두 번째 판 결과도 complete 전송됨 (`_completeSent` 리셋 확인)
- [ ] 게스트 스킵 진입: play/complete 호출 로그 없음, 게임 정상 진행
- [ ] 네트워크 차단 상태에서도 게임 진행 무영향 (경고 로그만)

확인: 2026-07-08 · `c53ed605` — 에디터 Play 로그 `play ok`/`complete ok — score=918`/`ranking ok — 3 entries` 확인, 콘솔 에러 0.
