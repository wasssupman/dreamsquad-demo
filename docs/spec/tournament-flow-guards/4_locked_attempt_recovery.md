# 4 — 락/실패 자동 복구 (열린 attempt 정리 후 재시도)

## 목적

play 가 **이미 열린(진행중) attempt 때문에 실패**(서버 500 "cannot wait")할 때, 그 열린 attempt 를 `complete(0)` 으로 닫아 락을 풀고 play 를 재시도해 사용자가 로비에 갇히지 않게 한다. unit 1 의 실패 처리(팝업)를 확장한다.

## 배경

- 안 끝낸 attempt 가 서버에 열려 있으면 새 play 가 500 `"cannot wait"` 로 막힌다(raw 요청도 동일 → 서버측 확정).
- 클라의 기존 `ReconcilePending` 은 **로비 진입 시 + 600s 이내 + pending 에 attemptId 있을 때만** complete(0) 로 푼다. **다시 시도 루프 중엔 재실행 안 하고**, pending 이 비면(응답 유실 orphan) 못 푼다.
- entryId 로 complete 는 서버가 거부(`"tournamentEntryAttemptId not matched"`) → **attemptId 가 반드시 필요**.

## Linchpin (구현 전 확정 필요)

**열린 attempt 의 attemptId 를 클라가 얻을 수 있는가?**
- pending 레코드에 있으면 그것으로. (tracked 케이스)
- 없으면(응답 유실) 서버에서 얻어야 함 — `unclaimed` 는 entryId 만 줌. play-500 응답/다른 엔드포인트가 열린 attemptId 를 노출하는지 **프로브로 확정**.
- **얻으면** → 아래 구현. **못 얻으면** → 이 orphan 은 클라 복구 불가 → 서버측(play-while-locked 를 열린 attempt 재발급/409, 혹은 락 자동 만료)로 이관. 이 문서는 그 결론을 기록한다.

## 구현 (attemptId 확보 가능일 때)

- `OnStartGame`/재시도 경로에서 play 실패가 **락 유형**(500/"cannot wait")이면:
  1. 열린 attemptId 확보(pending 우선, 없으면 서버 조회).
  2. `TournamentApi.Complete(openAttemptId, 0, "")` 로 닫음(락 해제).
  3. play **1회** 재시도. 성공 → 입장. 여전히 실패 → unit 1 팝업.
- **바운드**: 자동 복구는 1회만(무한 루프 금지).
- **안전**: 단일 플레이 데모라 열린 attempt 는 항상 재개 불가한 orphan → complete(0) 로 버려도 무손실.

## 완료 기준

- 락 상태에서 `시작` → 자동으로 열린 attempt 정리 → play 재시도 → 입장(또는 여전히 실패 시 팝업).
- 무한 재시도 없음(1회 바운드).
- tracked/orphan 케이스 각각 확인. attemptId 확보 불가로 판명되면 그 사실 + 서버 이관 결론을 문서에 남김.

## 결론 (2026-07-25 — 라이브 프로브)

**auto-recovery 구현 불가 — 서버 이관.** 열린 attempt 의 attemptId 를 클라가 얻을 방법이 없음을 라이브 확인:

- `complete/{entryId}/0` → 서버 거부 `"tournamentEntryAttemptId not matched"` (complete 는 attemptId 필수, entryId 불가).
- `result/entry/unclaimed` → entryId 만, attemptId 없음.
- `result/tournament/{entryId}` → 200 이나 랭킹 스키마라 attempt 필드 없음(`hasAttemptField=false`).
- 응답 유실 orphan 은 pending 에도 없음(`<absent>`).

→ **pending 에 attemptId 가 없는 락(=락-500 이 실제로 나는 경우)은 클라가 못 푼다.** 사용자 아이디어(complete 0 + 재시도)는 attemptId 가 있어야만 성립하는데 그 경우는 이미 `ReconcilePending` 이 로비 진입 시 처리한다.

**락은 시간제로 자동 만료**됨을 확인(`play NOW code=200`, 새 라운드 롤오버). 그래서 실질 복구 = 대기 or 서버 수정.

### 남는 클라측 액션 (achievable) — unit 7 에서 구현됨
- **락 유형 실패 메시지 구분**: play 500/`cannot wait` 면 "입장 실패" 대신 `"이미 진행 중인 게임이 있어요. 잠시 후 다시 시도해 주세요"` — 무의미한 즉시 재시도 방지. (unit 1 의 onFailed 메시지 분기)
- tracked 락(pending 에 attemptId 보유)의 complete(0)+재시도 1회도 unit 7 이 커버 — 이 문서의 "구현 불가" 결론은 **orphan 락에 한정**해 유지된다.

### 서버 이관 (클라 범위 밖)
- play-while-locked 를 500 대신 **열린 attempt 재발급/409** 로 주거나, 락 TTL 단축, 혹은 userId 기준 취소 엔드포인트 제공. 이게 있어야 클라가 즉시 복구 가능.
