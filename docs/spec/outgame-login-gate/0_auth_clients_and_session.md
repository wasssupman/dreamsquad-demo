# 0. Auth 클라이언트 + 세션

## 목적

Firebase 익명 인증(REST)과 게임 서버 sign-in 을 호출하는 런타임 클라이언트, 그리고 인증 상태 홀더를 만든다. 게임 서버 공통 envelope 파싱을 일반화해 시트 파서와 정의를 공유한다.

## 변경 대상

- `Assets/_Project/Scripts/Core/Api/ApiEnvelope.cs` (신규) — 게임 서버 공통 `{success, data, errorDetail}` 파서: `Parse<T>(body, out error)` (data = 단일 객체) + errorDetail → 문자열 조립
- `Assets/_Project/Scripts/Data/StatImport/SheetEnvelopeParser.cs` — envelope 검증/errorDetail 조립을 `ApiEnvelope` 재사용으로 교체 (행 배열/빈 셀 strip 은 시트 전용 잔류). **동작 무변경 — 기존 테스트 무수정 통과로 증명**
- `Assets/_Project/Scripts/Core/Api/FirebaseAuthRestClient.cs` (신규) — `SignUpAnonymous(apiKey, cb)` / `RefreshIdToken(apiKey, refreshToken, cb)`. 응답 파싱(idToken/refreshToken/localId, 갱신 응답은 snake_case `id_token`/`refresh_token` 주의). 실패 시 Firebase 에러 JSON(`error.message`) 을 사유로
- `Assets/_Project/Scripts/Core/Api/UserSignApi.cs` (신규) — `SignIn(baseUrl, idToken, userName, cb)`: POST + `Authorization: Bearer` + `X-SERVICE-APP-VERSION` + metadata body. 성공 시 `SignedInUser`(userId/userName/provider), 실패 시 사유 (HTTP 실패여도 body 의 errorDetail 파싱 — 시트 API 와 동일한 "body 보존" 규칙)
- `Assets/_Project/Scripts/Core/Api/UserSession.cs` (신규) — static 홀더: `Current`(SignedInUser + idToken), `IsSignedIn`, `Clear()`
- EditMode 테스트 (신규 파일)

## 구현

- 네트워크와 파싱 분리: 각 클라이언트의 응답 파싱은 `internal static` 순수 함수 (`TryParseSignUp`, `TryParseRefresh`, `TryParseSignIn`) — EditMode 테스트는 이것만 대상, 라이브 콜 없음.
- sign-in DTO 는 쓰는 필드만 (`userId`/`userName`/`provider`) — User 스키마 전체 복제 금지.
- metadata: `userName`(입력값), `appVersion = Application.version`, `osType`(Android→ANDROID, iOS→IOS, 그 외 WEB), `lang = Application.systemLanguage.ToString()` — 채울 수 없는 필드는 생략.
- 콜백은 메인 스레드(UnityWebRequest completed) — 락 불필요. 타임아웃은 UnityWebRequest.timeout 10s.
- **에러 구분 계약** (README 신원 정책 지원): Firebase 응답이 에러 JSON 이면 `firebase: {message}` (확정 무효 판단용), 전송 실패(타임아웃/연결)면 `network: {사유}`. 네임스페이스는 `Wassup.Core.Api`.

## 완료 기준

- [x] compile 오류 없음, 기존 EditMode 스위트 회귀 없음 — 532개 통과 (SheetEnvelopeParser 의 ApiEnvelope 교체 포함, 2026-07-07)
- [x] 신규 테스트 9종: ApiEnvelope 성공/실패/비JSON, signUp(camelCase)·refresh(snake_case)·Firebase 에러(확정) 파싱, 확정/일시 에러 분류, metadata body, UserSession Set/Clear
- [x] 실 API 왕복 프로브 (2026-07-07): signUp 200 → sign-in `success:true` (userId uuid, provider GUEST — 서버 자동 가입 확인), refresh 200 snake_case 실증. 무효 토큰 → HTTP 500 + `INTERNAL_SERVER_ERROR` (예상했던 AUTHENTICATION_FAIL 이 아닌 500 래핑 — errorDetail 표시 동작엔 무영향). 부작용: 익명 계정 1개 생성됨 (`claude-probe`)
