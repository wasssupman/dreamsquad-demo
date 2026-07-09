# 5 — Handoff Summary

## Commit

- `c53ed605` feat(tournament): play/complete 서버 연동 + 결과 랭킹 조회 — tournament-play-report (코드 + 테스트)
- 문서(완료 기준·README 상태·이 handoff)는 후속 docs 커밋.

## Implemented

- `POST /tournament/play` — 배틀 씬 진입(`GameManager.OnEnable`) + RESTART(`BattleBridge.OnRestartRequested`) 마다 호출, `attemptId`/`entryId` 보관.
- `POST /tournament/complete/{attemptId}/{score}` — 결과 팝업(승/패 3경로) 확정 시, body `{"debug": <배틀로그 compact JSON>}`.
- `GET /tournament/result/tournament/{entryId}` — complete 성공 시에만 체인 호출, 결과 팝업 리더보드를 실 참가자로 교체.
- `TournamentMatchReporter`(static): epoch 가드로 stale 응답 폐기, `_completeSent` 로 attempt 당 complete 1회, 게스트(`idToken` 빈값)·실패는 게임 무영향.
- `BattleLogger.SnapshotJson()` — 세션 안 닫고 compact JSON. 파일 기록(`EndSession`)은 기존 그대로.
- `UserSession.GameServerBaseUrl` — sign-in/게스트 스킵 시 baseUrl 보관 (`Set` 3번째 인자 optional, 기존 호출자 무손상).
- ResultScreen: `maxEntryCount`(10) 슬롯 고정 렌더, 미배정 슬롯 `WAITING...` 회색, 본인 행 `#FFD54A` 강조. rank 는 클라 산출(서버 미제공).
- REDRAFT 버튼/이벤트/`OnRedraftRequested` + 유일 호출자였던 `RestartBattle()` 제거 → 재시작 경로 `OnRestartRequested` 단일화.

## Key Files

- `Assets/_Project/Scripts/Core/Api/TournamentApi.cs` — 3 엔드포인트 클라이언트 + 소비 DTO.
- `Assets/_Project/Scripts/Core/Api/TournamentMatchReporter.cs` — 호출 오케스트레이션 + 상태.
- `Assets/_Project/Scripts/UI/ResultScreen.cs` — `UpdateLeaderboard`.
- `Assets/_Project/Scripts/Bridge/BattleBridge.cs` — 결과 확정 3곳 `ReportMatchResult`, RESTART `BeginMatch`.
- `Assets/_Project/Scripts/Logging/BattleLogger.cs` — `SnapshotJson`.

## Verified

- EditMode: `TournamentApiTests`(7) + `BattleLoggerSnapshotTests`(2) + 기존 `UserAuthApiTests`(9) 통과.
- 실서버 curl 프로브: Firebase 익명 signUp → sign-in → play → complete(777) → result 왕복 success=true, 방금 점수 본인 userId 로 조회 확인.
- 에디터 Play: `play ok` / `complete ok — score=918` / `ranking ok — 3 entries`, 콘솔 에러 0.
- 무관 선재 실패 2건(`ObstaclePlacerTests`, `SkyFallTests`)은 본 작업과 접점 없음 — 손대지 않음.

## Notes

- **서버 `rank` 미제공**: dev 서버 `entries[]` 에 `rank` 없음(스키마엔 존재). 클라가 score 내림차순 위치로 산출, 서버 rank>0 이면 그 값 우선. 서버가 rank 를 채우기 시작하면 자동 반영.
- **UI 영문 고정**: TMP 폰트에 한글 글리프 없음(login spec 계약) → `WAITING...` 영문. 한글 필요 시 폰트 fallback 별도 작업.
- **epoch 규칙**: play race 자체는 현 흐름에서 사실상 불가능하지만, 결과 팝업 직후 RESTART 시 이전 판 complete/ranking 응답이 늦게 도착하는 창은 실재 → 모든 async 콜백에 단일 stale 규칙 적용(되돌리지 말 것).
- **미완료 attempt**: 결과 없이 끝난 판은 complete 미전송. 서버에 미완료 attempt 로 남는 것은 서버 정책에 위임.
- 워킹 트리의 `DefenderPortraits/`·probuilder Settings·`tmp/` 등은 본 작업과 무관 — 커밋에 포함하지 않음.

## Follow-up

- play 재호출 시 `status=IN_PROGRESS`(이전 미완료 attempt) 처리 정책 — 새 attempt 발급인지 기존 반환인지 실 프로브 후 문서화.
- 세션 중 idToken 만료(1h) 자동 refresh — outgame-login-gate 후속과 공통.
- 보상 수령 UI (`/tournament/claim`, `claimAll`) + 미수령/수령 목록 조회.
- 실기기(Android) Development Build 에서 API 왕복 1회 확인.
