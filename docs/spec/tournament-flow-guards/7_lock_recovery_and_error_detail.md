# 7 — 락 자동 복구(tracked 한정) + 입장 실패 상세 에러

## 목적

play 실패로 입장이 막힐 때 (1) 팝업에 **실제 에러 상세**를 함께 노출해 원인 구분이 가능하게 하고, (2) **락 유형**(500 "cannot wait") 실패면 클라가 열린 attempt 의 attemptId 를 쥔 경우(pending)에 한해 **complete(0) → play 1회 재시도**로 사용자를 로비에 가두지 않는다. unit 4 의 결론(orphan 락은 클라 복구 불가)은 유지 — 이 unit 은 **tracked 락**(pending 보유)만 복구한다.

## 배경

- unit 4 라이브 프로브 결론: pending 에 attemptId 가 없는 orphan 락은 클라가 풀 수 없다(서버 이관). 단 **pending 이 있는 락**은 complete(0) 가 가능한데, 기존엔 로비 진입 시 `ReconcilePending` 1회에만 의존 — reconcile 이 실패했거나 in-flight 인 채 `시작` 을 누르면 play 가 500 으로 막히고 사용자는 원인 모를 "입장 실패"만 본다.
- unit 4 의 "남는 클라측 액션"(락 유형 메시지 분기)도 이 unit 에서 함께 구현한다.

## 변경 대상

- `Assets/_Project/Scripts/Core/Api/TournamentMatchReporter.cs`
  - `IsLockError(string)` — "cannot wait" 포함 여부로 락 유형 판정 (internal, 테스트 대상)
  - `ReconcilePending(Action<bool> onDone)` 오버로드 — complete(0) 실제 성공 시에만 `onDone(true)`
  - `BeginMatchFromLobby` — play 실패가 락 유형이면 `ReconcilePending` → 성공 시 play **1회** 재시도
- `Assets/_Project/Scripts/UI/Outgame/OutgameMenuController.cs`
  - `OnStartGame.onFailed` — 락 유형이면 "이미 진행 중인 게임이 있어요" 메시지 분기 + 모든 실패에 상세 에러를 작은 글씨로 덧붙임
- `Assets/_Project/Tests/EditMode/Api/TournamentMatchReporterTests.cs` — IsLockError 판정 + onDone(false) 분기 테스트

## 구현

- **락 판정**: 에러 문자열에 `"cannot wait"` 포함(대소문자 무시). ApiEnvelope 포맷(`errorCode — errorMessage / detailMessage`)의 필드 위치에 의존하지 않는다.
- **복구 흐름**: `BeginMatchFromLobby` 의 실패 콜백에서 락 유형이면 `ReconcilePending(released)` 호출 → `released=true`(pending attemptId 로 complete(0) 성공)일 때만 `BeginMatchInternal` 재시도. 재시도는 **1회 바운드** — 재실패는 그대로 onFailed 표면화(무한 루프 금지). orphan(무 pending)/reconcile 실패/in-flight 는 즉시 표면화.
- **pending 계약 유지**: unit 6 의 "complete 성공 후에만 pending clear" 그대로. 복구도 같은 `ReconcilePending` 코드를 지나므로 계약 분기 없음.
- **팝업**: busy 딤은 복구·재시도 동안 유지("매칭 중"). 최종 실패 시 락이면 `"이미 진행 중인 게임이 있어요.\n잠시 후 '시작'을 다시 눌러 주세요."`, 그 외 기존 문구. 두 경우 모두 하단에 `<size=60%>` 로 raw 에러 상세를 덧붙인다(진단용 — 데모 단계라 사용자 노출 허용).

## 완료 기준

- EditMode: `IsLockError` 판정(포함/미포함/null/대소문자) + `ReconcilePending(onDone)` 의 클라측 분기(no-record / no-account / mismatch → `false`) green. 기존 1275 무회귀.
- 컴파일 클린.
- 라이브: 락 상태(서버에 열린 attempt + pending 보유)에서 `시작` → 자동 complete(0) → play 재시도 → 입장. orphan 락에선 "이미 진행 중인 게임이 있어요" + 상세 에러 팝업.
- 일반 실패(서버 다운 등)에서 팝업에 상세 에러 문자열이 표시된다.

확인: 2026-07-27 — EditMode 1371 (1369 pass / 0 fail / 2 기존 skip) + 에디터 라이브 강제 재현 통과(Awake reconcile 임시 차단 → 배틀 진입 후 강제 정지로 tracked 락 생성 → `시작` 한 번에 `reconcile complete ok(구 attemptId)` → `play ok(신규 attemptId)` → 입장). 사용자 승인 후 커밋.
