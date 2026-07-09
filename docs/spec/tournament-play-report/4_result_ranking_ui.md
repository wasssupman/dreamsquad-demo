# 4 — 결과 팝업 랭킹 실데이터 구성

## 목적

결과 팝업의 랭킹 화면을 봇 점수 대신 같은 토너먼트 참가자들의 실제 점수(`GET /tournament/result/tournament/{entryId}`)로 구성한다. 데이터는 unit 3 의 `ReportResult` → `onRanking` 콜백으로 도착한다.

선행: unit 3 (reporter 체인).

## 변경 대상

- `Assets/_Project/Scripts/UI/ResultScreen.cs` — `UpdateLeaderboard(...)` 추가
- `Assets/_Project/Scripts/Bridge/BattleBridge.cs` — `onRanking` 콜백에서 ResultScreen 갱신 배선

## 구현

- `ResultScreen.UpdateLeaderboard(TournamentApi.ResultData data, string ownUserId)`
  - **rank 는 클라 산출**: dev 서버 실측(2026-07-08 curl 프로브) 결과 `entries[]` 에 `rank` 필드가 내려오지 않는다 (Swagger 스키마엔 존재). score 내림차순 정렬 후 위치로 표시 rank 를 산출하고, 서버 rank 가 양수로 오면 그 값을 우선한다.
  - **슬롯 고정 표시** (사용자 결정 2026-07-08): 토너먼트는 `maxEntryCount`(현재 10)명이 배정되는 구조 — 항상 `maxEntryCount` 행을 그리고, 아직 배정되지 않은 슬롯은 `WAITING...` 회색 행으로 표기한다 (UI 영문 계약 — TMP 폰트에 한글 글리프 없음). 참가자 0명이어도 동작. 리더보드 패널 높이 360→440, 폰트 32→28 로 11행 수용.
  - `RANK NAME SCORE` 기존 mspace 포맷 유지. `userId == ownUserId` 행은 기존 YOU 강조색(`#FFD54A`) 재사용 — 이름은 서버 `userName` 그대로 표시.
  - 팝업이 비활성(`gameObject.activeSelf == false`)이면 무시 — 늦게 도착한 응답이 닫힌 팝업을 되살리지 않게.
- 표시 순서: `ShowVictory/ShowDefeat` 는 지금처럼 **봇 목록으로 즉시 표시** (로딩 대기 없음), 랭킹 응답이 도착하면 `UpdateLeaderboard` 가 교체. 게스트·complete 실패·조회 실패 시 콜백이 안 오므로 봇 목록이 그대로 남는다 (README fallback 계약).
- BattleBridge: unit 3 의 결과 확정 공통 헬퍼에서 `onRanking: data => resultScreen?.UpdateLeaderboard(data, UserSession.Current?.userId)` 배선. RESTART 로 새 판이 시작된 뒤 도착하는 이전 판 응답은 unit 3 의 epoch 검사 + 팝업 비활성 가드가 이중으로 걸러낸다.
- 서버 `userName` 이 비어 있으면 `"?"` 로 표시, 10자 초과는 잘라서 mspace 컬럼 유지.

## 완료 기준

- [ ] compile 통과
- [ ] 에디터 Play (로그인 상태): 결과 팝업이 봇 목록으로 뜬 뒤 서버 참가자 목록으로 교체, 본인 행 강조 + 서버 점수 일치
- [ ] 게스트 스킵: 봇 목록 유지 (교체 없음)
- [ ] 결과 팝업 → RESTART 직후 이전 판 랭킹 응답이 도착해도 UI 오동작 없음

확인: 2026-07-08 · `c53ed605` — 에디터 Play 에서 랭킹 3 entries 로 리더보드 교체 확인. 미배정 슬롯 WAITING 표기는 서버 참가자 < maxEntryCount 케이스에서 렌더 경로 검증됨.
