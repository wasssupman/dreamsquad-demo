# 9 — pending 은 complete 성공 후에만 제거 (전 경로 통일)

## 목적

미완료 attempt 의 서버 락을 클라가 스스로 풀 수 있게 만든 안전망(`PendingMatchStore`)이,
**정작 그 안전망이 필요한 유일한 경우 = complete 전송 실패** 에서 비어 있던 구멍을 막는다.

unit 6 이 `ReconcilePending` 에 대해 이미 고친 규칙(clear-on-success)을 **주 발신 경로 2개
(`ReportResult`/`AbandonMatch`)로 확장**한다. 그 둘은 여전히 clear-at-send 였다.

## 배경 (구멍)

`ReportResult`/`AbandonMatch` 는 complete 를 **보내기 직전** 에 pending 을 지웠다. 그래서:

1. 전투 종료 → complete 전송 중 네트워크 끊김/타임아웃/앱 백그라운드
2. "점수 전송 실패" 팝업 (재시도 없음) — 이 시점에 attemptId 는 디스크에서 이미 소멸
3. 로비 `ReconcilePending` → 레코드 없음 → no-op
4. `시작` → play 500 `"cannot wait"` → 락 자동복구도 pending 이 없어 실패
5. **서버 배치까지 대기** — 이 spec 이 막으려던 바로 그 상태

즉 "complete 를 보냈다"를 "complete 가 성공했다"로 취급하고 있었다.

clear-at-send 의 원래 사유는 "느린 complete + 앱 킬 시 남은 레코드가 reconcile 을 통해
실점수를 0으로 덮는다"였다. 그 시나리오는 **첫 complete 가 서버에서 성공한 경우**에만
성립하고(그러면 attempt 는 닫혀 있어 두 번째 complete(0) 은 거부될 가능성이 높다), 반면
지금 방식의 손실(영구 락)은 전송이 실패하면 **확정적으로** 발생한다. 위험이 비대칭이다.

> 미확인: **이미 닫힌 attempt 에 complete(0) 재전송 시 서버 응답.** 거부면 무손실이고,
> 덮어쓰면 실점수가 0 이 될 수 있다. unit 4 프로브는 entryId-로-complete 만 확인했다.
> 이 확인 전까지는 "0 으로 덮일 잔여 위험"이 남아 있는 것으로 취급한다.

## 변경 대상

- `Assets/_Project/Scripts/Core/Api/PendingMatchStore.cs`
- `Assets/_Project/Scripts/Core/Api/TournamentMatchReporter.cs`
- `Assets/_Project/Tests/EditMode/Api/PendingMatchStoreTests.cs`
- `Assets/_Project/Tests/EditMode/Api/TournamentMatchReporterTests.cs`

## 구현

1. **`PendingMatchStore.ClearIfMatches(attemptId)`** — compare-and-clear. complete 왕복 동안
   새 매치가 자기 attemptId 를 저장했을 수 있으므로, 무조건 `Clear()` 하면 **다음 판의
   안전망을 지운다**. 방금 마감한 그 attempt 의 레코드일 때만 제거한다.
2. **세 발신 경로 모두 clear-on-success** — `ReportResult` / `AbandonMatch` / `ReconcilePending`
   가 성공 콜백에서 `ClearIfMatches` 를 호출한다. 실패면 레코드를 남겨 다음 로비 reconcile
   이 complete(0) 로 마감한다.
3. **`_reconciling` → `_completesInFlight` 카운터로 확장** — 응답 전까지 레코드가 남으므로,
   그 창 동안 다른 경로가 같은 attempt 를 또 마감하는 것을 막아야 한다. reconcile 중복 발화
   (Awake+onSignedIn) 뿐 아니라 **나가기 직후 로비 진입 reconcile** 이 상시 겹친다(씬 전환
   수백 ms < complete 왕복). `ReconcilePending` 만 이 카운터에 게이트된다 — `ReportResult`/
   `AbandonMatch` 는 `_completeSent` 가 이미 1회성을 보장한다.
   - **bool 이 아니라 카운터인 이유**(리뷰 반영): 서로 다른 attempt 의 complete 두 개가
     겹칠 수 있다 — 지연된 reconcile(A) 가 아직 나는 중에 새 매치의 abandon(B) 이 출발하면,
     먼저 돌아온 A 응답이 bool 가드를 조기 해제해 로비 reconcile 이 B 를 재마감한다.
   - **Play 진입 리셋 + 0 클램프**(리뷰 반영): DisableDomainReload 라 static 이 Play 세션을
     넘어 잔존한다. in-flight 인 채 Play 가 끝나 콜백이 안 오면 카운터가 박혀 reconcile 이
     영구 skip → `[RuntimeInitializeOnLoadMethod(SubsystemRegistration)]` 로 초기화
     (LobbyReactionLock 선례). 고아 콜백의 뒤늦은 감소는 0 클램프로 흡수.
4. **`ReportResult` 의 pending 정리는 epoch 가드보다 앞** — 락 해제 성사 여부는 그 매치의
   생사와 무관하다. RESTART 로 epoch 가 바뀐 뒤 도착한 성공 응답도 레코드를 정리해야 한다.

## 완료 기준

- EditMode: `ClearIfMatches` 3종(일치/불일치/무효 입력) + `ReconcilePending` in-flight 스킵 통과.
- 정상 완주 → complete 성공 → pending 제거, 로비 재진입 시 reconcile 무발신.
- **complete 강제 실패**(기내모드 등) → "점수 전송 실패" 팝업 + **pending 유지** → 다음 로비
  진입에서 `reconcile complete ok — score=0` 로그 → 이어지는 `시작` 이 500 없이 진행.
- 나가기(AbandonMatch) 직후 로비 진입에서 같은 attempt 로 complete 가 **두 번** 나가지 않는다.

## 남는 구멍 (이 unit 범위 밖)

- **orphan 락**: play 응답 유실 시 attemptId 자체가 없어 저장도 복구도 불가. 서버가
  play-while-locked 에 열린 attemptId 를 돌려줘야 해결(unit 4 결론과 동일).
- **복구 시점이 로비 진입뿐**: 앱 resume 훅 없음. 배틀 중 백그라운드 복귀로는 재시도 안 함.
- **계정 전환**: `userId` 불일치 레코드는 complete 없이 폐기 → 그 락은 배치 대기.
- **락 판정이 문자열 매칭**(`"cannot wait"`): 서버 문구 변경 시 자동복구가 조용히 죽는다.
