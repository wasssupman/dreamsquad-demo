# 3 — Handoff Summary

## Commit

- `b4ef4434` unit 0 — PendingMatchStore + PendingMatchPolicy + EditMode 9
- `c8178a1c` unit 1 — reporter save / clear-at-send / AbandonMatch / ReconcilePending
- `8b5fdaaf` unit 2 — MenuPopup + OutgameMenuController 배선
- `58fd4014` docs — 스펙 4파일

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
- `Assets/_Project/Tests/EditMode/Api/PendingMatch*Tests.cs`

## Verified

- 컴파일 0에러(read_console).
- EditMode **1213/1213**(신규 9 포함), 스킵 2는 기존 known-ignored.
- PlayMode 스모크 **4/4**(OutgameFlow/SceneTransition/TallyFlow×2) — 로비 reconcile·결과 clear-at-send 런타임 무예외.

## Notes

- **되돌리지 말 것**: clear-at-send 는 성공 콜백이 아니라 **전송 개시 시점**에 clear 해야 한다. 성공 시 clear 로 바꾸면 치명 구멍(느린 complete + kill → 0점 클로버) 재발.
- `AbandonMatch` 의 `_epoch++` 는 in-flight play 콜백이 로비에서 phantom Save 하는 걸 막는다 — 제거 금지.
- `ReconcilePending` 은 라이브 `_attemptId`/`_completeSent` 를 건드리지 않고 persisted 레코드 + 현재 세션으로만 동작. Clear 를 전송 **전**에(낙관적) 한다 = 이중 발화 방지.
- Save/Clear 는 반드시 `PlayerPrefs.Save()` flush — kill 생존/좀비 방지의 핵심.
- 원천 한계: play 응답 전 이탈한 attempt 는 클라가 attemptId 를 모르므로 못 닫는다 → 서버 정리 위임(설계 수용).

## Follow-up

- **실계정 + dev 서버 Play 검증 5종**(unit 2): 메뉴나가기-0 / kill-재실행 복구 / over-window discard / 정상종료 무회귀 / 게스트 무영향. 서버 tournament 상태를 실제 변경하므로 수동/사용자 주도.
- window 10분의 서버 라운드 실측 정합, 기권=0점 대신 실 획득 점수 제출 — README "후속 후보".
