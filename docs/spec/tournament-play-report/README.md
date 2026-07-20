# Tournament Play Report — 토너먼트 play/complete 서버 연동

상태: **완료 2026-07-08** — units 0~4 구현 + EditMode 테스트 통과 + 실서버 API 왕복 프로브 통과 + 에디터 Play 확인(play/complete/ranking 로그 정상). 인계는 `5_handoff_summary.md`

## 목표

배틀 씬을 플레이하면 게임 서버에 토너먼트 참가를 기록한다.

1. **게임 시작** (배틀 씬 진입 + RESTART 재시작마다) → `POST /tournament/play` 호출, 응답의 `tournamentEntryAttemptId` + `tournamentEntryId` 보관
2. **게임 종료** (결과 팝업 표시 시점) → `POST /tournament/complete/{attemptId}/{score}` 호출, body 의 `debug` 필드에 배틀 로그 JSON 문자열 첨부
3. **랭킹 표시** — complete 성공 응답을 받으면 `GET /tournament/result/tournament/{tournamentEntryId}` 로 같은 토너먼트 참가자들의 점수를 조회해 결과 팝업의 랭킹 화면을 실데이터로 구성 (사용자 추가 요구 2026-07-08)

부수 결정: 결과 팝업의 **REDRAFT 버튼은 제거**한다 (사용자 결정 2026-07-08 — 재시작 경로를 RESTART 하나로 단순화).

## 작업 단위 목록

| 번호 | 구분 | 문서 | 목적 |
|---|---|---|---|
| 0 | 구현 | `0_tournament_api_client.md` | `TournamentApi` (play/complete) + `UserSession` baseUrl 보관 + EditMode 테스트 |
| 1 | 구현 | `1_battlelog_json_snapshot.md` | `BattleLogger.SnapshotJson()` — 세션을 닫지 않고 현재 로그를 JSON 문자열로 뽑기 |
| 2 | 구현 | `2_redraft_button_removal.md` | ResultScreen REDRAFT 버튼/이벤트 + BattleBridge 핸들러 제거 |
| 3 | 구현+wiring | `3_reporter_wiring.md` | `TournamentMatchReporter` — play/complete 호출 지점 배선 + complete 성공 시 결과 조회 체인 |
| 4 | 구현+wiring | `4_result_ranking_ui.md` | ResultScreen 랭킹을 실 토너먼트 참가자 데이터로 구성 (게스트/실패 시 봇 목록 fallback) + Play 검증 |

## Feature-wide 계약

- **엔드포인트**: `POST {base}/tournament/play` (무파라미터 변형, body 없음) · `POST {base}/tournament/complete/{tournamentEntryAttemptId}/{score}` body `{ "debug": "<battle log JSON 문자열>" }`. base = `https://dev-api-somnia.cashroyale.games` (LoginPanelView 의 `gameApiBaseUrl` → sign-in 시 `UserSession` 에 보관).
- **인증 헤더**: `Authorization: Bearer {UserSession.IdToken}` + `X-SERVICE-APP-VERSION` — `UserSignApi` 와 동일 패턴. 응답은 공통 envelope → `ApiEnvelope.Parse<T>` 재사용.
- **play 응답에서 소비하는 필드**: `tournamentEntryAttemptId` (complete 경로 파라미터), `tournamentEntryId` (결과 조회 경로 파라미터), `status` — 나머지는 파싱하지 않는다 (`UserSignApi.SignedInUser` 선례).
- **결과 조회**: complete 성공 시에만 `GET /tournament/result/tournament/{tournamentEntryId}` 호출. 응답 `TournamentResult.entries[]` 에서 `userName`/`score` 를, 루트에서 `maxEntryCount` 를 소비한다 (dev 서버는 `rank` 미제공 — 클라가 score 내림차순 위치로 산출, 2026-07-08 실측). 랭킹은 항상 `maxEntryCount` 슬롯(현재 10)을 그리고 미배정 슬롯은 `WAITING...` 표기. 본인 행은 `UserSession.Current.userId` 매칭으로 강조. complete 응답 `data` 도 동일한 `TournamentResult` 스키마 — 파싱 DTO 를 공유한다.
- **랭킹 fallback**: 게스트·API 실패·조회 전 로딩 중에는 대기 상태 목록("참가자 찾는 중", 점수 `-`)을 보여준다. 실데이터는 도착하면 교체.
  (당초 `BotScoreGenerator` 더미 점수를 썼으나 없는 순위를 지어내는 문제로 `result-screen-ranking-ui` unit 1 에서 교체·삭제했다.)
- **호출 시점**: play = 배틀 씬 진입 1회 + RESTART 로 새 판이 시작될 때마다. complete = 결과 팝업(승/패) 확정 시 1회. 판당 attemptId 1개, complete 는 attemptId 당 최대 1회.
- **미완료 판**: 결과 팝업 없이 끝난 판(결과 전 재시작·앱 종료)은 complete 를 보내지 않는다. 서버에 미완료 attempt 가 남는 것은 서버 정책에 위임 (사용자 결정 2026-07-08).
- **게스트 스킵**: `UserSession.IdToken` 이 비어 있으면 (게스트 = `idToken=""`) play/complete 호출 자체를 스킵. `IsSignedIn` 만으로는 게스트를 걸러낼 수 없음에 주의.
- **실패 무시**: play/complete 실패(네트워크·서버 에러)는 게임 진행을 막지 않는다. `Debug.LogWarning` 만 남기고 계속.
- **배틀 로그 JSON**: `BattleLogger` 가 유일한 JSON 작성자 원칙 유지 — 리포터는 `SnapshotJson()` 이 돌려준 문자열을 그대로 body 에 담는다 (compact, prettyPrint 없음). 파일 기록(`EndSession`)은 기존 그대로.
- **ECS 경계**: 전부 MonoBehaviour 계층 (Core/Api, UI, Bridge). ECS 접점 없음.

## 파이프라인 커버리지

N/A — 플레이 오브젝트 신설/렌더 경로 변경 없음 (네트워크 클라이언트 + UI 버튼 제거).

## 후속 후보

- 보상 수령 UI (`/tournament/claim`, `claimAll`) 및 미수령 목록 조회
- 세션 중 idToken 만료(1h) 시 자동 refresh — outgame-login-gate 후속 후보와 공통
- play 응답 `status` 가 IN_PROGRESS(이전 미완료 attempt) 일 때의 서버 정책 확인 — 새 attempt 발급인지 기존 반환인지 실 프로브 후 문서화
