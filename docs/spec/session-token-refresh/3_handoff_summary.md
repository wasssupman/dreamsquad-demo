# Handoff — session-token-refresh

## Commit

`feat(session-token-refresh): 403 만료 idToken 세션 중 자가치유` (2026-07-22)

## Implemented

- 게임서버 인증 = **firebase idToken 그 자체를 Bearer**(별도 게임 토큰 없음). 1h 만료 후 세션 중 미갱신 → 403 → 히스토리/토너먼트 리포트 조용히 실패하던 것을 자가치유.
- `UserSession.TryRefreshBearer(Action<bool>)` — firebase refreshToken 으로 idToken **인메모리** 재발급. 코얼레스(동시 403 → 단일 refresh), Clear 경합 방어, definitive 실패(`IsDefinitiveAuthError`) 시 소스 정리(반복 방지). PlayerPrefs 미기록(영속은 로그인 뷰 소유).
- `TournamentApi.Send` → **request 팩토리** 전환(UnityWebRequest 1회용) + `Attempt` 내부에 **403/401 단발 재시도**. 재시도는 갱신된 `UserSession.Credential`(새 Bearer)로 1회만.
- `LoginPanelView.SignInToGameServer` 가 `refreshToken` + `firebaseApiKey` 를 `UserSession.Set` 에 전달(1줄). guest/username Set 은 미전달(만료 없음).

## Key Files

- `Assets/_Project/Scripts/Core/Api/UserSession.cs` — RefreshToken/FirebaseApiKey + TryRefreshBearer
- `Assets/_Project/Scripts/Core/Api/TournamentApi.cs` — Send 팩토리 + Attempt 403/401 retry
- `Assets/_Project/Scripts/UI/Outgame/LoginPanelView.cs` — refresh 소스 배선
- `Assets/_Project/Tests/EditMode/Api/UserSessionRefreshTests.cs` — 가드/배선 5개

## Verified

- 컴파일 오류 0. EditMode **1229 통과 / 0 실패** (신규 5 + 2 pre-existing skip).
- **curl HTTP 체인**: 무효토큰→403 `HANDLE_ACCESS_DENIED/C006`; refresh→새 idToken; 재발급 토큰→200 수용.
- **런타임(Unity Play)**: 인메모리 firebase 세션 세워 idToken 을 garbage 로 무효화 → 히스토리 API → 403→refresh→retry→**200, idToken 21→834, healed=True**.

## Notes

- **트리거는 403(||401)** — 이 서버는 인증 거부를 401 이 아니라 **403** 으로 준다(실측). 401 만 걸면 무용지물. 되돌리지 말 것.
- **타임아웃/5xx 는 대상 아님**(responseCode 0 → 트리거 비껴감). 이 수정은 **인증 만료만** 치유. 사용자가 겪는 "간혹"이 네트워크면 별개 대응 필요 — 콘솔 `list fetch failed:` 문자열로 판별(`HANDLE_ACCESS_DENIED`=인증 / `HTTP timeout`=네트워크).
- **username-복구 세션은 무영향**(X-AUTH-USERNAME, 만료 없음). idToken 이 비어 트리거 게이트를 통과 못 함.
- 재발급은 **인메모리 전용** — firebase refresh 토큰은 보통 옛 값도 유효해 다음 실행 `Start()` 가 정상 부트스트랩.

## Follow-up

- 회전 refresh 토큰 PlayerPrefs 영속(firebase 가 실제 회전·구식화 시). `UserSession.onBearerRefreshed` 훅 → 로그인 뷰가 저장.
- 선제 갱신(`expiresIn` 추적, 첫 요청 지연 제거) — 현재 reactive 로 충분.
- 401/403 재시도를 `UserSignApi`/`SheetHttp` 등 다른 게임서버 호출에도 필요 시 `Send` 공용화.
- (별개) 네트워크 타임아웃 실패 모드 대응이 필요하면 timeout 재시도/backoff 스펙 신설.
