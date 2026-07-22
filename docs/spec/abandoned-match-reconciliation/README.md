# Abandoned Match Reconciliation — 기권/중단 판의 서버 마감 보장

상태: **작성 중 2026-07-22** — 설계 확정, 구현 전. units 0~2.

## 목표

시작된 모든 토너먼트 attempt 가 **결국 terminal `complete` 를 받게** 한다. 지금은 결과 팝업이 뜬 정상 종료에서만 `complete` 가 나가서, 메뉴 나가기·앱 강제종료로 끝난 판은 서버에 `in-progress` 로 매달린 채 결과처리가 안 된다.

종료 채널별 처리:

| 채널 | 앱 상태 | 처리 |
|---|---|---|
| 승/패 결과팝업 | 살아있음 | 실점수 `complete` — **기존, 무변경** |
| 메뉴 "나가기" | 살아있음 | 0점 `complete` 즉시 (신규) |
| 강제종료 / 크래시 | 코드 실행 불가 | 다음 세션 **로비에서 복구** (신규) |
| 백그라운드 후 복귀(안 죽음) | 배틀 유지 | **건드리지 않음** — 이어서 플레이 |

## 배경 / 연결

- 서버 연동 토대: `docs/spec/tournament-play-report/` (`TournamentApi.Play/Complete`, `TournamentMatchReporter`).
- 엔드포인트는 **현재 버전 그대로**만 쓴다: `POST /tournament/complete/{attemptId}/{score}` 하나로 기권 마감. 서버 TTL 추가·전용 abandon 엔드포인트·멱등성 요청 등 백엔드 변경은 스코프 밖.

## 작업 단위 목록

| 번호 | 구분 | 문서 | 목적 |
|---|---|---|---|
| 0 | 구현 | `0_pending_match_store.md` | `PendingMatchStore`(PlayerPrefs) + `PendingMatchPolicy`(순수) + EditMode 테스트 |
| 1 | 구현 | `1_reporter_extension.md` | `TournamentMatchReporter` 확장 — save / clear-at-send / `AbandonMatch` / `ReconcilePending` |
| 2 | 구현+wiring | `2_wiring_and_verification.md` | `MenuPopup`·`OutgameMenuController` 배선 + Play 검증 |

## Feature-wide 계약

- **store 의 의미** = "클라가 아직 어떤 terminal `complete` 도 **개시하지 않은** attempt 1건". 레코드 `{attemptId, userId, startedAtUnix}`. PlayerPrefs 단일 JSON 키.
- **clear-at-send (치명 구멍 차단)**: 정상/기권/복구 어느 경로든 terminal `complete` 를 **개시하는 순간 store 를 clear**. 이래야 느린 정상 `complete` 전송 중 앱이 죽어도 복구가 같은 attempt 에 0점을 덮어쓰지 않는다. 부수효과로 클라가 attempt 당 `complete` 최대 1회 → 서버 멱등성에 의존 안 함.
- **flush 필수**: `Save`/`Clear` 모두 `PlayerPrefs.Save()` 로 즉시 디스크 반영. (kill 생존이 목적이고, 미flush clear 는 좀비 레코드 부활 → 위 클로버 재발.)
- **complete 인자**: 마감은 **`attemptId`** 로 친다(`entryId` 아님). 복구 시 인증은 저장된 토큰이 아니라 **그 시점 살아있는 세션**(`UserSession.Credential` + `GameServerBaseUrl`) 으로 — 그래서 복구는 반드시 로그인/세션 복원 이후(로비)에 돈다.
- **계정 가드**: 레코드 `userId` 와 현재 세션 `UserSession.Current.userId` 가 같을 때만 복구 `complete`. 불일치면 폐기(clear). null-safe 비교. (firebase/username 모드 모두 userId 채워짐, guest 는 `""`+미플레이.)
- **grace window** = 클라 상수 **600초(10분)**. within → 0점 `complete`+clear / over → clear only. over-window 는 현재 서버가 라운드 종료로 정리한다는 전제(설계 결정). 상수 1곳(`PendingMatchPolicy`)에만 둔다.
- **게스트 스킵**: `HasAccount=false` → `Play` 자체가 스킵 → save/reconcile 전부 no-op.
- **실패 무시**: 기존 계약 유지 — 기권/복구 `complete` 실패는 `Debug.LogWarning` 만. `debug=""` 로 전송.
- **ECS 경계**: 전부 MonoBehaviour 계층(Core/Api, UI). ECS 접점 없음.

## 불변식 (문서화용, 구현이 지켜야 함)

- **레코드 수명 유한**: 매 계정보유 로비 방문에서 complete0+clear 또는 discard+clear → 무한 누적 불가.
- **단일 슬롯으로 충분**: 동시 미결 attempt ≤ 1 (한 번에 한 배틀 + kill 재실행은 START 전 로비 reconcile 경유, in-place RESTART 는 직전 attempt 가 clear-at-send 됨). 큐 불필요.
- **overwrite-leak 없음**: 위 경유 순서 덕에 `BeginMatch` 의 save 가 라이브 레코드를 덮지 않는다 → `BeginMatch` 에 reconcile 을 중복 삽입하지 않는다(과잉 방지).
- **원천 한계**: `Play` 응답(=attemptId) 수신 전 나가면 클라가 그 attempt 를 영영 못 닫는다 → 서버 정리에 위임. `AbandonMatch` 가 `_epoch++` 로 in-flight `Play` 콜백을 드롭해 로비 phantom 쓰기를 막는다.

## 파이프라인 커버리지

N/A — 플레이 오브젝트 신설/렌더 경로 변경 없음 (로컬 영속 + 네트워크 마감 + UI 배선).

## 후속 후보

- 기권 시 0점이 아니라 **이탈 시점 실 획득 점수** 제출 (배틀 중 점수 스냅샷 주기 write 필요 — 현재는 0점 몰수로 확정).
- window 값(10분)의 서버 라운드 실측 정합 — 현재 서버 동작 확인 후 상수 조정.
