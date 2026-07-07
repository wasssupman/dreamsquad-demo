# Outgame Login Gate — 데모 사용자 구분용 로그인

상태: **구현 완료 2026-07-07** — critic 리뷰(APPROVE-WITH-CHANGES) 반영 + 에디터 Play 4케이스 검증. 잔여: 실기기 Development Build 1회. 인계는 `2_handoff_summary.md`

## 목표

팀내 데모 공유 시 **테스트 기록의 사용자 구분**을 위해 Outgame(로비) 진입 시 인증을 요구한다. 인증 전에는 로그인 패널(이름 입력 + LOGIN)이 뜨고, 인증 성공 시 기존 로비 메뉴가 노출된다.

인증은 **Firebase Auth REST API 익명 로그인** (SDK 없음, UnityWebRequest 2엔드포인트):

```
1) POST identitytoolkit.googleapis.com/v1/accounts:signUp?key={apiKey}   body {}      → idToken, refreshToken, localId
2) POST securetoken.googleapis.com/v1/token?key={apiKey}   grant_type=refresh_token   → 새 idToken (재시작 시 같은 계정 유지)
3) POST {game}/user/sign/in   Authorization: Bearer {idToken}                          → User (userId/userName)
```

기기당 익명 계정 1개 + 사용자가 입력한 이름(`metadata.userName`)으로 식별한다. 토큰셋 사전 배포는 불필요해짐 (구 계획 폐기).

## 작업 단위 목록

| 번호 | 구분 | 문서 | 목적 |
|---|---|---|---|
| 0 | 구현 | `0_auth_clients_and_session.md` | `ApiEnvelope` 공통 파서 + `FirebaseAuthRestClient` + `UserSignApi` + `UserSession` + EditMode 테스트 |
| 1 | 구현+wiring | `1_login_gate_ui_wiring.md` | 로그인 패널(이름 입력+버튼+상태) + 로비 메뉴 게이팅 + 씬 배선 + Play 검증 |

## Feature-wide 계약

- **게임 서버 API**: `POST https://dev-api-somnia.cashroyale.games/user/sign/in` (contents 그룹) · `Authorization: Bearer {Firebase idToken}` + `X-SERVICE-APP-VERSION` 헤더 · body `{ "metadata": { "userName", "appVersion", "osType", "lang" } }` · 응답 envelope `{ success, data: User, errorDetail }`.
- **Firebase**: 프로젝트 `somnia-dev`, 웹 apiKey 사용 (Firebase 설계상 클라이언트 공개 식별자 — 하드코딩 허용, SerializeField 기본값). idToken 수명 1시간.
- **envelope 파서 일반화**: `{success, data, errorDetail}` 는 게임 서버 공통 포맷 — 단일 객체 data 용 `ApiEnvelope.Parse<T>` 를 신설하고, 기존 `SheetEnvelopeParser` 의 envelope 검증/errorDetail 조립은 이를 재사용 (포맷 정의 중복 금지). Firebase REST 응답은 이 envelope 이 **아님** (자체 JSON) — ApiEnvelope 적용 금지.
- **영속화는 PlayerPrefs 2개**: `refreshToken` (계정 유지의 핵심), `userName` (재입력 방지). idToken/UserSession 은 메모리 한정.
- **세션**: `UserSession` static 데이터 홀더 (userId/userName/idToken, `IsSignedIn`, `Clear`) — MonoBehaviour Manager 아님 (GameManager 유일 규칙 준수).
- **신원 정책 (critic MAJOR-1)**: 안정 키는 **userId** — 계정 재생성을 최소화한다. LOGIN 버튼도 저장된 refreshToken 이 있으면 **refresh 를 먼저 시도**하고, refresh 가 **확정 무효**(Firebase 에러: TOKEN_EXPIRED/USER_NOT_FOUND/INVALID_REFRESH_TOKEN 등)일 때만 저장 토큰을 버리고 신규 signUp 한다. **일시 실패**(네트워크)면 신규 계정을 만들지 않고 에러 표시 후 재시도. 에러 구분 계약: Firebase 응답 에러 = `firebase:` 접두, 전송 실패 = `network:` 접두.
- **시작 흐름**: 저장된 refreshToken 있음 → 토큰 갱신 → sign-in → 성공 시 패널 스킵. 실패 → 패널 표시 + 사유. 저장 토큰은 확정 무효일 때만 클리어(일시 실패는 유지).
- **로그인 흐름**: 이름 입력(공백 불가) → (저장 토큰 refresh 시도 →) 익명 signUp → idToken 으로 sign-in → refreshToken/userName 저장 → 메뉴 전환.
- **헤더 계약**: `X-SERVICE-APP-VERSION = Application.version` (Swagger 상 optional — body 의 `metadata.appVersion` 과 동일 값 전송, 서버 게이팅 키 여부는 실 프로브에서 확인).
- **실패 처리**: 각 단계 실패 시 사유를 패널에 영문으로 표시, 재시도 가능. 게임이 잠기지 않게 요청 중에도 강제 종료 가능(버튼만 비활성). 릴리즈 게이트 없음 (데모 앱 자체가 내부용).
- **UI 텍스트는 영문** (보유 TMP 폰트에 한글 글리프 없음).
- **스코프 제외**: 구글 로그인·계정 연동(`sign/link`), 세션 중 토큰 만료(1시간) 대응, 로그아웃 UI, 테스트 기록 전송(userId 사용처) — 후속.

## 후속 후보

- 세션 중 idToken 만료(1h) 시 자동 refresh 재시도 (401 감지) — 데모 세션이 길어지면 필요
- 테스트 기록 적재: 전투 결과에 `UserSession.userId` 첨부 — 별도 spec
- 계정 승격: 익명 → 구글 연동 (`accounts:signInWithIdp` + `/user/sign/link`)
- 로그아웃/계정 초기화 버튼 (PlayerPrefs 클리어) — 참고: 클리어/재설치 시 신규 익명 계정 생성(고아 계정 누적)은 내부 데모 특성상 허용
