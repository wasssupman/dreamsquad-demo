# Unit 1 — TournamentApi.Send 인증실패(403/401) 단발 재시도

## 목적

`TournamentApi.Send` 가 인증 실패 응답을 만나면 `UserSession.TryRefreshBearer` 로 idToken 을 재발급하고 **그 요청을 1회만** 새 Bearer 로 재시도한다. 만료 토큰이 자가치유돼 히스토리/리포트 실패가 사라진다.

**실측 계약(2026-07-22 curl)**: 이 서버의 인증 거부 = **HTTP 403** + `errorCode:HANDLE_ACCESS_DENIED/C006`. **401 이 아님** — 반드시 403 을 트리거에 포함(401 은 안전빵). 타임아웃은 `responseCode==0` 이라 트리거를 비껴간다(refresh 로 못 고치는 별개 원인 — 의도된 제외).

## 변경 대상

- `Assets/_Project/Scripts/Core/Api/TournamentApi.cs`

## 구현

1. **request 팩토리 전환**: UnityWebRequest 는 1회용이라 재시도엔 새 요청이 필요하다. `Send(UnityWebRequest request, ...)` → `Send(Func<UnityWebRequest> requestFactory, AuthCredential credential, Action<string,string> onResponse)`.
   - 4개 호출부(`Play`/`Complete`/`GetResult`/`GetUnclaimedEntries`)를 팩토리 람다로 변환. `Complete` 은 팩토리 안에서 uploadHandler + `Content-Type` 까지 구성(재시도 시 body 재생성).
2. **내부 `Attempt(factory, credential, bool allowRefresh, onResponse)`**:
   - `request = factory()`; downloadHandler/credential.Apply/`X-SERVICE-APP-VERSION`/timeout 세팅(기존 Send 본문 이동).
   - 완료 콜백에서 `long code = request.responseCode` 캡처, body/transportError 계산, `request.Dispose()`.
   - **재시도 게이트**: `(code == 403 || code == 401) && allowRefresh && !string.IsNullOrEmpty(credential.idToken)` 이면
     - `UserSession.TryRefreshBearer(ok => { if (ok) Attempt(factory, UserSession.Credential, allowRefresh:false, onResponse); else onResponse(body, transportError); })` 후 return.
     - 재시도는 **갱신된** `UserSession.Credential`(새 Bearer)로. `allowRefresh:false` 로 단발 보장.
   - 그 외: `onResponse(body, transportError)` (기존과 동일).
   - `Send` 는 `Attempt(requestFactory, credential, allowRefresh:true, onResponse)` 한 줄.
3. username 세션은 `credential.idToken` 이 비어 재시도 게이트를 통과하지 못함 → 403 이어도 그대로 실패(계약대로).

## 완료 기준

- 컴파일 오류 0. 4개 엔드포인트 정상 호출 형태 유지(반환 계약 `onDone(value/ok, error)` 불변).
- 정상(200) 경로는 재발급 미발동(왕복 1회) — `read_console` 에 refresh 로그 없음 확인.
- 403 자가치유 동작 검증은 unit 2 라이브(강제 무효 토큰)에서.
- 기존 EditMode(파싱 테스트 `TryParseUnclaimed`/`TryParseResult` 등) 회귀 없음.

완료: 2026-07-22 — 컴파일 0, EditMode 회귀 0, unit 2 런타임에서 403→retry→200 실증.
