# 3 — Handoff Summary

## Commit

- `b4ef4434` unit 0 — PendingMatchStore + PendingMatchPolicy + EditMode 9
- `c8178a1c` unit 1 — reporter save / clear-at-send / AbandonMatch / ReconcilePending
- `8b5fdaaf` unit 2 — MenuPopup + OutgameMenuController 배선
- `58fd4014` docs — 스펙 4파일
- `fd186884` fix(tournament-play-report) — **선행 의존**: play 응답 스키마 중첩 대응(아래 Notes)

## Implemented

- 시작된 토너먼트 attempt 를 `PendingMatchStore`(PlayerPrefs 단일 키 `Wassup.PendingMatch`)에 영속 — play 성공 콜백에서 `{attemptId, userId, startedAtUnix}` Save(+flush).
- **메뉴 나가기** = 라이브 판 기권 → `AbandonMatch()` 가 인메모리 attemptId 로 즉시 0점 complete + `_epoch++`(in-flight play 콜백 드롭).
- **앱 킬/크래시** → 다음 로비 `OutgameMenuController.ApplyAuthGate` 에서 `ReconcilePending()`: 계정 가드 → grace window(600s) 내면 현재 세션 cred 로 0점 complete, 초과면 discard. 둘 다 Clear.
- **clear-at-send**: 정상 종료(`ReportResult`)도 complete 개시 순간 store Clear → 느린 정상 complete + kill 되어도 복구가 실점수를 0으로 덮지 않음. 부수효과로 서버 멱등성 비의존.
- 게스트(`HasAccount=false`)는 play 자체가 스킵이라 save/reconcile 전부 no-op.

## Key Files

- `Assets/_Project/Scripts/Core/Api/PendingMatchStore.cs` — 영속(Save/TryLoad/Clear, 매 mutation flush)
- `Assets/_Project/Scripts/Core/Api/PendingMatchPolicy.cs` — 순수 판정 + `DefaultTtlSeconds=600` 단일 소유
- `Assets/_Project/Scripts/Core/Api/TournamentMatchReporter.cs` — save/clear-at-send/`AbandonMatch`/`ReconcilePending`
- `Assets/_Project/Scripts/UI/MenuPopup.cs` (`OnExit`) · `Assets/_Project/Scripts/UI/Outgame/OutgameMenuController.cs` (`ApplyAuthGate`)
- `Assets/_Project/Tests/EditMode/Api/PendingMatch*Tests.cs` · `TournamentMatchReporterTests.cs`(ReconcilePending 가드/폐기 분기)

## Verified

- 컴파일 0에러(read_console).
- EditMode 전체 **1228 pass / 0 fail / 2 known-ignored skip**. 신규 13 = store 5 · policy 4 · `ReconcilePending` 가드 4(no-record/no-account/account-mismatch/over-window, 전부 네트워크 미도달) + 갱신 `TryParsePlay`.
- PlayMode 스모크 **4/4**(OutgameFlow/SceneTransition/TallyFlow×2) — 로비 reconcile·결과 clear-at-send 런타임 무예외.
- **실 dev 서버 왕복**(일회용 Editor 프로브, 익명 계정): reconcile→`reconcile complete ok score=0`, abandon→`abandon complete ok score=0`, over-window→`pending attempt discarded`(complete 없음). 모두 store 삭제 확인. 프로브는 삭제(미커밋).

## Notes

- **되돌리지 말 것**: clear-at-send 는 성공 콜백이 아니라 **전송 개시 시점**에 clear 해야 한다. 성공 시 clear 로 바꾸면 치명 구멍(느린 complete + kill → 0점 클로버) 재발.
- `AbandonMatch` 의 `_epoch++` 는 in-flight play 콜백이 로비에서 phantom Save 하는 걸 막는다 — 제거 금지.
- `ReconcilePending` 은 라이브 `_attemptId`/`_completeSent` 를 건드리지 않고 persisted 레코드 + 현재 세션으로만 동작. Clear 를 전송 **전**에(낙관적) 한다 = 이중 발화 방지.
- Save/Clear 는 반드시 `PlayerPrefs.Save()` flush — kill 생존/좀비 방지의 핵심.
- 원천 한계: play 응답 전 이탈한 attempt 는 클라가 attemptId 를 모르므로 못 닫는다 → 서버 정리 위임(설계 수용).

## Follow-up

- **실기기 UI 경로 최종 확인**(수동): 실제 배틀 플레이→메뉴 나가기, 실제 앱 강제종료→재실행 로비 복구. 로직·서버 왕복은 프로브로 검증됨 — 남은 건 기기 UI 흐름뿐.
- **`123` 계정 서버측 stuck attempt**: play 가 500 `cannot wait` 반환(미완료 attempt 잔존). attemptId 를 못 받아 클라가 못 닫는다 → 백엔드 정리 또는 RESET ACCOUNT 필요. **이 기능이 애초에 방지하려던 상태의 실사례** — 앞으로는 abandon/reconcile 이 attempt 를 닫아 이런 stuck 이 안 생긴다.
- window 10분의 서버 라운드 실측 정합, 기권=0점 대신 실 획득 점수 제출 — README "후속 후보".
