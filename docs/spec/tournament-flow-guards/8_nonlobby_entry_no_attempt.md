# 8 — 비게이트 진입은 attempt 를 만들지 않는다 (결함 A)

## 목적

로비 `시작` 게이트를 거치지 않은 배틀씬 진입(TestMode, 에디터 직접 Play)이 **실서버 토너먼트 엔트리/락을 만들지 않게** 한다. 개발·테스트 행위가 히스토리 오염(0점 엔트리)과 실점수 제출 사고를 일으키는 결함 A 의 해소.

## 배경

- `GameManager.OnEnable` 이 배틀씬 진입마다 무조건 `BeginMatch()` 를 호출하고, 이것이 로비 미발행 시 **play 를 재발행**했다. 배틀씬 스크립트 진입은 로비 `OnStartGame` 한 곳뿐이므로(전수 확인), 재발행이 실제로 발동하는 경우는 전부 비정규 진입이다:
  - **TestMode** — `TestModePanelView.StartPlan` 이 `TestModeContext.Set` 후 게이트 우회 직행
  - **에디터에서 BattleScene 직접 Play**
- 이런 판이 완주하면 `ReportResult` 가 실점수를 진짜 토너먼트에 제출하고, 중단하면 0점 엔트리/락이 남는다.

## 변경 대상

- `Assets/_Project/Scripts/Core/Api/TournamentMatchReporter.cs` — `BeginMatch()` 를 adopt-or-reset 으로 (발행 제거). `BeginMatchInternal` 의 adopt 분기는 데드가 되므로 제거.
- `Assets/_Project/Scripts/Core/GameManager.cs` — `OnEnable` 주석 갱신 (sole issuer → adopt-only).
- `Assets/_Project/Tests/EditMode/Api/TournamentMatchReporterTests.cs` — 비게이트 리셋/adopt 분기 테스트.

## 구현

- `BeginMatch()`: `_lobbyIssued` 면 adopt(플래그만 소거, 상태 보존 — 기존과 동일). 아니면 **상태 리셋만**(epoch++·attemptId/entryId/completeSent/seed 클리어) 하고 **play 를 부르지 않는다**. 리셋은 유지해야 한다 — 직전 매치의 stale attemptId 에 테스트 판 점수가 제출되는 사고 방지.
- attempt 부재의 파급은 기존 가드가 그대로 흡수: `ReportResult`/`AbandonMatch` 는 attemptId 없으면 스킵, pending 저장 없음.
- **부수 효과(의도)**: 비게이트 진입은 `HasTournamentSeed=false` → 맵풀 **index 0 폴백**. 개발 중 맵 강제는 기존 스테퍼(PlayerPrefs override > fixedMapSeed)로 가능.
- 로비 경로는 무변경: `BeginMatchFromLobby` 가 유일한 발행 창구가 된다 (게스트 즉시입장·락 복구 포함).

## 완료 기준

- EditMode: 비게이트 `BeginMatch()` 가 (a) stale attempt/seed 를 리셋하고 (b) 네트워크 발행 없이 pending 을 만들지 않으며 (c) `_lobbyIssued` adopt 시 상태를 보존함. 기존 테스트 무회귀.
- 라이브: 로그인 상태에서 TestMode 진입 → 콘솔에 `play ok` 없음 + pending 없음 + 완주해도 `complete` 미전송. 로비 `시작` 정상 경로는 기존대로 play ok → adopt → 입장.

확인: 2026-07-27 — 배치 EditMode 1377 (1375 pass / 0 fail / 2 기존 skip, UnityMCP 브리지 다운으로 testrig worktree 배치 실행) + 라이브: TestMode 진입 시 play 미발행(attemptId null·pending 없음·맵 fallback0), 로비 경로는 play ok→adopt(attemptId 보존·pending 저장·seed 확보) 무회귀. 스모크 attempt 는 abandon complete(0) 로 즉시 마감.
