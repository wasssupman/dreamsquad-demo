# 1 — TournamentMatchReporter 확장

## 목적

리포터에 pending 레코드의 저장/개시-시-삭제(clear-at-send)와, 두 마감 진입점 `AbandonMatch`(메뉴 나가기)·`ReconcilePending`(로비 복구)을 추가한다.

## 변경 대상

- `Assets/_Project/Scripts/Core/Api/TournamentMatchReporter.cs`

## 구현

기존 in-memory 상태(`_epoch`, `_attemptId`, `_entryId`, `_completeSent`) 유지. `PendingMatchStore`/`PendingMatchPolicy`(unit 0) 소비.

### Play 성공 콜백 (기존 `BeginMatch` 내부)

- `_attemptId = state.tournamentEntryAttemptId;` 직후:
  `PendingMatchStore.Save(_attemptId, UserSession.Current?.userId ?? "", DateTimeOffset.UtcNow.ToUnixTimeSeconds());`
- 이 콜백은 이미 `epoch != _epoch` 로 stale-drop 된다 → `AbandonMatch` 의 `_epoch++` 가 in-flight save 를 자동으로 막는다(README 원천-한계 처리).

### ReportResult (정상 종료) — clear-at-send

- 실제 `TournamentApi.Complete` 를 **개시하기 직전**(guest/attemptId 없음/`_completeSent` 조기 return 을 모두 통과한 뒤, `_completeSent = true` 부근)에서 `PendingMatchStore.Clear();`.
- 성공 콜백이 아니라 **전송 개시 시점**에 clear 하는 것이 계약(README). 나머지 로직 무변경.

### AbandonMatch() — 메뉴 나가기용 (신규)

순서:
1. 로컬 캡처: `attemptId=_attemptId`, `baseUrl=UserSession.GameServerBaseUrl`, `cred=UserSession.Credential`.
2. `_epoch++;` — in-flight `Play` 콜백 드롭(로비 phantom save 차단).
3. `if (!UserSession.HasAccount) return;` (게스트).
4. `if (string.IsNullOrEmpty(attemptId) || _completeSent) return;` — 아직 attempt 없음/이미 전송.
5. `_completeSent = true;`
6. `PendingMatchStore.Clear();` (clear-at-send)
7. `TournamentApi.Complete(baseUrl, cred, attemptId, 0, "", (ok,err)=>{ if(!ok) Debug.LogWarning(...); });`

### ReconcilePending() — 로비 복구용 (신규)

라이브 in-memory 상태를 건드리지 않고 **persisted 레코드 + 현재 세션**으로만 동작:
1. `if (!PendingMatchStore.TryLoad(out var rec)) return;`
2. `if (!UserSession.HasAccount) { PendingMatchStore.Clear(); return; }`
3. 계정 가드: `if ((UserSession.Current?.userId ?? "") != rec.userId) { PendingMatchStore.Clear(); return; }`
4. `long elapsed = DateTimeOffset.UtcNow.ToUnixTimeSeconds() - rec.startedAtUnix;`
5. `var action = PendingMatchPolicy.Decide(elapsed, PendingMatchPolicy.DefaultTtlSeconds);`
6. `PendingMatchStore.Clear();` — **낙관적, 전송 전**(이중 발화·재진입 시 중복 complete 차단).
7. `if (action == Complete0)` → `baseUrl`/`cred` 유효성 확인 후 `TournamentApi.Complete(baseUrl, cred, rec.attemptId, 0, "", 실패 시 warn)`. `DiscardOnly` 면 종료.

## 완료 기준

- 컴파일 통과, 콘솔 무에러.
- 코드 리뷰로 계약 확인: clear-at-send 가 전송 개시 시점인지, `AbandonMatch` 가 `_epoch++` 하는지, `ReconcilePending` 이 라이브 `_attemptId`/`_completeSent` 를 안 건드리는지, 복구가 현재 세션 cred 를 쓰는지.
- 기존 `TournamentApi`/reporter EditMode 테스트 무회귀.
- 실제 서버 왕복은 unit 2 Play 검증에서 확인.

완료: 2026-07-22 `c8178a1c` — 컴파일 0에러, 전체 EditMode 무회귀(guest 경로 inert). `ReconcilePending` 가드/폐기 분기(no-record/no-account/account-mismatch/over-window)는 `TournamentMatchReporterTests` 로 회귀 고정. within-window Complete0 + AbandonMatch 서버 왕복은 unit 2 실서버 프로브로 검증 완료.
