# 6 — handoff summary

토너먼트 서버 왕복(play/complete)의 성공·실패를 플레이어에게 드러내고, 락을 스코어 제출로 확실히 푸는 작업. 구현·정리 완료, 컴파일/EditMode green. 라이브 e2e 만 서버 락 해제 후 대기.

## Commit

- `820de3c2` unit 0 — 공용 알림 팝업 NoticePopup
- `5d6c84a0` unit 1 — play 응답 게이트 진입
- `bedda40e` unit 2 — score 전송 실패 알림
- `f496889f` 입장 실패 팝업 재시도 버튼 제거
- `98f2cd55` unit 5 — reconcile 나이 무관 항상 complete(0)
- `30989502` unit 6 — pending 은 complete 성공 후에만 제거
- `44d24c01` 크리틱 리뷰 정리 — churn/데드코드 제거

## Implemented

- **입장 게이팅**: 로비 `시작` → play 응답 대기 → **attemptId 확보(성공)**해야만 `SceneTransition.Go(Battle)`. 실패/무응답/attempt 빈값이면 입장 취소 + `NoticePopup` 안내. 대기 중 `_starting` + busy 딤으로 재진입(더블탭) 차단.
- **게스트**: 계정 없으면 play 없음 → 게이트 비대상, 즉시 입장.
- **score 실패 알림**: `ReportResult` 에 `onError` 추가, complete 실제 실패(`!ok`)에서만 발화 → BattleBridge 가 `NoticePopup` 로 안내(논블로킹, 재시도 없음).
- **락 해제 신뢰성**(핵심): reconcile 가 나이(TTL) 무관 **항상 complete(0)** 로 열린 attempt 를 마감하고, pending 은 **complete 성공 후에만** 제거(실패면 유지 → 다음 로비 재시도). `_reconciling` 로 중복 방지.
- **NoticePopup**: DontDestroyOnLoad 자기부트스트랩 + 정적 `ShowBusy`/`ShowAlert`/`Hide`. 인스턴스 부재 시 no-op degrade. 닫기 단독(재시도 제거).

## Key Files

- `Scripts/Core/Api/TournamentMatchReporter.cs` — BeginMatchFromLobby(await)/BeginMatchInternal/ReportResult(onError)/ReconcilePending
- `Scripts/UI/NoticePopup.cs` — 공용 팝업
- `Scripts/UI/Outgame/OutgameMenuController.cs` — OnStartGame 게이팅
- `Scripts/Bridge/BattleBridge.cs:~3976` — ReportMatchResult onError 배선
- `Tests/EditMode/Api/TournamentMatchReporterTests.cs` — reconcile 클라측 분기

## Verified

- 컴파일 클린(CS 에러 0). EditMode 1277: **1275 pass / 0 fail / 2 skip**(기존 ModifierFramework [Ignore], 무관).
- NoticePopup 3상태 오프스크린 렌더 확인(busy/alert). 실제 클라 play 경로가 서버 500 "cannot wait"(락) 를 정확히 실패로 처리함을 라이브 확인.

## Notes (되돌리면 안 됨)

- **응답 없으면 클라는 아무것도 저장 안 함**(사용자 모델). pending 저장은 attemptId 받은 성공 콜백에서만.
- **락은 스코어 제출로만 풀린다**: 완주=실점수(ReportResult), 미완주=0점(Abandon/Reconcile). reconcile 은 **성공 확인 후에만 pending clear** — 전송 전 optimistic clear 로 되돌리면 실패 시 attemptId 유실 → 영구 락(영구 500) 재발.
- 성공 판정은 HTTP 200 이 아니라 **attemptId 비어있지 않음**.
- 진단용 raw play 프로브는 금지 — pending 없이 서버 락만 만들어(클라가 못 푸는 orphan) 세션 오염.

## Follow-up

- **라이브 e2e**(로비 시작→배틀→완주→로비, 완주 매치 서버 실점수 기록) — 현재 세션은 진단 프로브가 만든 서버 락으로 막혀 있어 락 해제(라운드 롤오버) 후 확인.
- **서버 이관**(unit 4): play-while-locked 를 500 대신 열린 attempt 재발급/409 로, 또는 락 TTL 단축 — 있어야 attemptId 유실 orphan 도 즉시 복구.
- **결함 A**(범위 밖): `GameManager.OnEnable.BeginMatch` 가 TestMode/직접Play 진입에도 play(엔트리) 발행.
- reconcile 영구실패 attempt 는 로비마다 1회 재시도 잔존 — "already closed" 감지로 정리 가능(미세).
