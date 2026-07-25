# 5 — reconcile 항상 락 해제 (사용자 모델 반영)

## 사용자 서버 모델 (확정)

- **응답을 받아야 서버가 락을 건다.** play 응답을 못 받으면(연결 실패/타임아웃) 서버는 락을 걸지 않는다 → 클라도 저장할 세션 정보가 없다.
- **응답을 받은 attempt** 는 서버가 락을 걸고, 클라는 attemptId 를 쥔다. 그 뒤 문제가 생겨 완주 못 해도, **0점 complete API 를 호출하면 그 매치의 락이 풀린다.**
- 서버는 오래 방치된 attempt 를 강제 0점 처리(느린 폴백)하지만, 클라가 즉시 complete(0) 하면 바로 풀린다.

## 규칙

1. **응답 없으면 세션관리 없음** — play 실패 경로는 pending 을 저장하지 않는다(현행 유지). 저장은 attemptId 를 받은 성공 콜백에서만.
2. **락은 스코어 제출로만 풀린다** — 클라가 응답받아 연 attempt 는 **반드시** 스코어를 올려 락을 푼다: 완주면 실점수(`ReportResult`), 이탈/문제면 0점(`AbandonMatch`/`ReconcilePending`).

## 변경 (이번 unit)

- `TournamentMatchReporter.ReconcilePending`: **TTL(600s) discard 제거 → 나이 무관 항상 complete(0)**.
  - 기존엔 경과>600s 면 complete 없이 버려서(`PendingMatchPolicy.DiscardOnly`), 아직 열린 락을 클라가 안 풀어 새 play 가 `500 "cannot wait"` 로 막혔다.
  - 이제 pending 에 남은 attempt 는 항상 complete(0) 로 마감 → 락 해제. 라운드가 이미 닫혔으면 서버가 무해하게 거부.
- `PendingMatchPolicy`(+테스트)는 이제 미사용(dead) — 후속 정리 대상.

## 완료 기준

- 정상 플레이에서 미완료/이탈 매치가 로비 복귀 시 항상 `reconcile/abandon complete ok score=0` 로 락 해제 → 다음 `시작` 이 500 없이 진행.
- 응답 실패(무락)는 저장·reconcile 없음.

## 주의 (현재 인스턴스 오염)

디버깅 중 **raw play 프로브**가 pending 저장 없이 서버 attempt 를 여러 개 만들어(= 클라가 attemptId 를 안 가진 락) 현재 에디터 세션이 그 락들로 막혀 있다. 이 락들은 pending 이 없어 reconcile 로 못 풀며 **서버 강제 0점**으로만 풀린다. 정상 플레이 경로에는 없는 오염이다.
