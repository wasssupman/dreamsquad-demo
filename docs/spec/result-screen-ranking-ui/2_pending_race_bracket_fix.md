# 2 — Tally 연출 중 도착한 랭킹 드랍 픽스 + pending 칸수 5 통일

## 목적

결과 팝업이 로그인 상태에서 "참가자 10명 + 참가자 찾는 중"으로 굳는 버그를 없앤다.

**원인 (2026-07-29 라이브 재현으로 확정)**: 점수 제출(`ReportResult`)은 Tally 연출
**시작** 시점(`BeginTally`)에 나가는데, 결과 팝업은 연출(~4초)이 끝나야 열린다.
서버 왕복(complete→GetResult)은 대개 그보다 빨라 랭킹이 **팝업이 열리기 전에**
도착하고, `UpdateLeaderboard` 의 `!activeSelf` 가드(원래 "팝업 닫힌 뒤 늦은 응답"
드랍용)가 이 이른 응답까지 버린다. 재적용 기회가 없어 10칸 pending 이 영구히 남는다.
Tally 연출이 unit 4(tournament-play-report) **이후에** 끼면서 생긴 회귀다.

칸수 10도 낡았다: `AwaitingPendingSlots=10` 은 서버 통상 `maxEntryCount` 가 10이던
시절 값인데 현 서버는 5다 (2026-07-29 응답 실측 `maxEntryCount=5`).

## 변경 대상

- 수정 `Assets/_Project/Scripts/UI/ResultScreen.cs`
- 수정 `Assets/_Project/Scripts/Bridge/BattleBridge.cs`

## 구현

1. **랭킹 보관 후 재적용 (BattleBridge)**: `_arrivedRanking` 필드 신설.
   `ReportMatchResult` 의 onRanking 이 응답을 보관하고 기존처럼 `UpdateLeaderboard`
   도 호출한다(팝업이 이미 열려 있으면 그 자리에서 적용). `FinishTally` 가
   `ShowVictory/ShowDefeat` 로 팝업을 연 **직후** 보관값이 있으면 재적용한다.
   - stale 차단: `ReportMatchResult` 진입 시마다 `_arrivedRanking = null`.
     complete 왕복은 API 타임아웃 10초 안에 끝나므로 직전 판 응답이 다음 판
     `FinishTally` 까지 살아남을 수 없다.
   - `UpdateLeaderboard` 의 `!activeSelf` 가드는 유지 — "팝업 닫힌 뒤"(즉시
     RESTART) 드랍 역할은 그대로 필요하다 (README 패리티 3).
2. **pending 칸수 5 통일 (ResultScreen)**: `TerminalPendingSlots`/`AwaitingPendingSlots`
   와 `UserSession.HasAccount` 분기를 제거하고 `PendingSlots = 5` 단일 상수로.
   unit 1 의 "로그인 대기 = 10칸" 계약을 **대체**한다 (사용자 결정 2026-07-29:
   "디폴트로 5칸"). 서버 bracket 이 커져도 실데이터 행수는 응답의 `maxEntryCount`
   로 그려지므로(`BuildRows`) 여기 상수는 착지 전 placeholder 수만 정한다.

## 완료 기준

- [x] compile 통과, EditMode `ResultLeaderboardModelTests` 그린 (서버 경로 무변경) —
      2026-07-29, 전체 1571개 중 실패 2건은 무관 사전실패(MapDocument_Zig 병행 세션
      WIP · UnitKitSummary 인코딩)
- [x] 라이브 e2e (로그인 + 로비 게이트 입장 + 패배): Tally 연출을 스킵하지 않아도
      팝업이 열릴 때 실랭킹 5행이 이미 적용돼 있다 ("참가자 찾는 중" 없음) —
      2026-07-29 QA 계정(`cqa-popup-0729`)으로 픽스 전 10행 잔존 재현 → 픽스 후
      동일 조건에서 실랭킹 5행 + `순위 4 / 5` 자동 적용 확인
- [ ] 게스트: 5행 pending ("나" + "참가자 찾는 중" ×4) — 기존과 동일 (상수 경로
      동일이라 별도 e2e 미실행, 사용자 Play 확인 시 겸사 확인)
- [x] 콘솔에 `[TournamentReporter] ranking ok` 가 찍힌 판에서 pending 리스트가
      남아 있으면 실패 — 픽스 후 검증 판에서 미발생
