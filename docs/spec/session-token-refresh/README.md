# Session Token Refresh — 세션 중 idToken 만료 자가치유

상태: **완료 2026-07-22** — units 0~2 구현 + 컴파일 오류 0 + EditMode 1229 통과 / 0 실패(신규 `UserSessionRefreshTests` 5개 포함) + **HTTP 체인 실측(curl)**(403 만료 + refresh 새토큰 + 재발급 토큰 200 수용) + **런타임 자가치유 실증(Unity Play)**: garbage idToken → 403 → 자동 refresh → retry → 200 (idToken 21→834, healed=True). 인계는 `3_handoff_summary.md`.

## 목표

로비 히스토리("기록 조회에 실패했습니다.") + 토너먼트 리포트(play/complete/랭킹)가 **실행 세션 1시간 후 조용히 실패**하는 근본 원인 제거.

원인: 게임서버 인증은 **firebase idToken 을 Bearer 로 그대로** 검증한다(`UserSignApi.cs:48`, `TournamentApi` credential — 별도 게임 토큰 없음, `SignedInUser` 에 토큰 필드 없음). Firebase `idToken` 수명은 1시간인데, `UserSession.IdToken` 은 **앱 시작(`LoginPanelView.Start`) 또는 수동 로그인(`OnLoginClicked`) 에서만** `RefreshIdToken` 으로 채워지고, **실행 중 세션에서는 절대 재갱신되지 않는다**. 만료 토큰을 계속 보내 서버가 **403 HANDLE_ACCESS_DENIED** → `ApiEnvelope` 실패 → 리스트/결과 null. 토큰이 idToken 그 자체이므로 **firebase 만 갱신하면 인증이 회복**되고 re-SignIn 은 불필요(서버는 stateless JWT 검증, 같은 uid 의 새 idToken 을 그대로 수용).

해결(사용자 결정 2026-07-22): **reactive** — 요청이 인증 실패(403/401)로 떨어진 그 순간 `refreshToken` 으로 idToken 을 재발급하고 그 요청을 **1회만** 재시도한다. 만료 시각 추적/선제 갱신은 하지 않는다(자가치유·시계오차 무관). 재발급 자체가 실패하면 **현행 유지**(기존 실패 문구, 다음 앱 실행의 `Start()` 자동 재로그인이 회복).

## 작업 단위 목록

| 번호 | 구분 | 문서 | 목적 |
|---|---|---|---|
| 0 | 구현 | `0_usersession_refresh.md` | `UserSession` 에 refreshToken/firebaseApiKey 보관 + `TryRefreshBearer`(코얼레스) + EditMode 테스트 |
| 1 | 구현 | `1_send_401_retry.md` | `TournamentApi.Send` 를 request 팩토리로 전환 + 401 단발 재시도(`TryRefreshBearer`) |
| 2 | wiring | `2_login_wiring_and_verify.md` | `LoginPanelView.SignInToGameServer` 가 refreshToken+apiKey 를 `UserSession.Set` 에 전달 + 라이브 검증 |

세 유닛은 **함께 착지해야 기능**한다(0→1 컴파일 의존, 2 없으면 RefreshToken null 이라 재발급이 no-op). 각 유닛은 독립 컴파일 가능. 순서 0→1→2.

## Feature-wide 계약

- **트리거**: HTTP `responseCode == 401 || responseCode == 403` **이고** 해당 요청 credential 이 Bearer(`idToken` 비어있지 않음)일 때만 재발급. username-복구(`X-AUTH-USERNAME`) 세션은 만료가 없으므로 재발급 대상 아님(idToken 비어 게이트 통과 못 함).
  - **실서버 실측(2026-07-22 curl)**: 무효/만료/누락 토큰 → **HTTP 403** + `{success:false, errorDetail.errorCode:"HANDLE_ACCESS_DENIED", code:"C006", errorMessage:"권한이 없습니다."}`. 이 서버는 인증 거부를 **401 이 아니라 403** 으로 준다(`UserLookupApi.cs:12` 주석의 "absent name → 403 HANDLE_ACCESS_DENIED" 와 동일 미들웨어). **401 만 걸면 트리거가 절대 안 걸려 무용지물** — 반드시 403 포함. (401 도 표준 서버 대비 안전빵으로 함께.)
  - errorCode 기준이 아니라 **HTTP code 기준**으로 판정한다: 무효·만료·누락이 모두 동일한 `HANDLE_ACCESS_DENIED/C006` 로 와서 errorCode 로는 "만료(refresh 로 회복 가능)" 와 "영구 거부" 를 구별할 수 없다. 그래서 "일단 재발급 1회 재시도 → 그래도 실패면 surface" 전략이 맞다(오탐 재시도는 왕복 1회 낭비뿐, 무해).
- **적용 범위(정직한 한계)**: 이 수정은 **인증 실패(401/403)** 만 치유한다. **네트워크 타임아웃(10s)·서버 5xx·연결 blip 은 403 이 아니므로 재시도 대상이 아니다**(refresh 로 못 고침 — 별개 원인). 타임아웃은 `responseCode==0` 이라 트리거를 정확히 비껴간다(의도된 동작). 사용자가 겪는 "간혹" 이 인증 만료인지 네트워크인지는 콘솔 `[TournamentHistoryPanel] list fetch failed: {error}` 로 확정 가능: `HANDLE_ACCESS_DENIED — 권한이 없습니다` → 이 수정이 고침 / `(HTTP: ...timeout...)`·`empty response body` → 네트워크(이 spec 밖).
- **재시도 1회 한정**: 재발급 후 재요청은 딱 한 번. 그 재요청이 또 401 이어도 더는 재발급하지 않는다(무한루프 방지 플래그).
- **재발급 소스**: `refreshToken`(firebase refresh 응답 회전값) + `firebaseApiKey`. 둘 다 firebase 로그인 시 `UserSession` 에 심는다. guest/username `Set` 은 null 로 둔다.
- **in-memory 전용**: `TryRefreshBearer` 는 `UserSession.IdToken`/`RefreshToken` 을 **메모리에서만** 갱신한다. PlayerPrefs 저장은 하지 않는다(영속은 로그인 뷰 소유라는 기존 분리 유지 — `UserSession.cs` 주석). Firebase refresh 토큰은 보통 회전돼도 옛 토큰이 유효하므로 다음 실행 `Start()` 는 저장된 값으로 정상 동작. (회전 토큰 영속이 필요해지면 후속 후보.)
- **코얼레스**: 동시 다발 401(히스토리+리포트 겹침 등)이 N중 재발급을 트리거하지 않도록, 재발급 in-flight 중 들어온 요청은 대기열에 붙였다가 완료 시 일괄 통지. 메인스레드 단일 실행이라 락 불필요.
- **definitive 실패 정리**: refresh 가 `IsDefinitiveAuthError`(firebase 확정 거부)면 in-memory refresh 소스를 비워, 이후 401 이 재발급을 반복 시도하지 않게 한다. network 성 실패는 소스 유지(나중에 성공 가능).
- **실패 UX 불변**: 재발급 실패 시 호출부(패널/리포터)에는 **원래의 401 응답**이 그대로 전달돼 기존 문구/폴백이 뜬다. 새 UX·씬 전이 없음.
- **ECS 경계**: 전부 MonoBehaviour/Core 계층(Core/Api, UI). ECS 접점 없음.

## 파이프라인 커버리지

N/A — 플레이 오브젝트 신설/렌더 경로 변경 없음 (네트워크 인증 라이프사이클).

## 후속 후보

- **회전 refresh 토큰 영속**: firebase 가 refresh 토큰을 실제 회전·구식화하면 in-memory 갱신 후 PlayerPrefs 재저장 필요. `UserSession.onBearerRefreshed` 훅 → 로그인 뷰가 영속하는 형태(계층 유지).
- **선제 갱신**: `expiresIn` 추적해 만료 임박 시 요청 전 refresh (첫 요청 왕복 지연 제거). 현재는 reactive 로 충분.
- **공용 API 클라이언트**: 401-재시도가 `SheetHttp`/`UserSignApi` 등 다른 게임서버 호출에도 필요해지면 `Send` seam 을 공용화.
