# 2. Handoff Summary

## Commit

- `e7a43c86` feat(auth): unit 0 — Firebase 익명 REST + sign-in 클라이언트 + 세션 (critic 리뷰 반영 포함)
- (unit 1 커밋 — 로그인 게이트 UI + 씬 배선)

## Implemented

- 로비 진입 게이트: 미인증 시 메뉴(`MenuButtons` 컨테이너) 숨김 + `LoginPanel` 표시, 인증 시 반대 — 가시성은 `OutgameMenuController.ApplyAuthGate` 단독 소유
- 인증 플로우 (SDK 없음): Firebase 익명 signUp / refresh (REST) → 게임 서버 `POST /user/sign/in` (Bearer idToken) → `UserSession`(userId/userName/idToken)
- 신원 정책: 저장 refreshToken 이 있으면 항상 refresh 우선, **확정 무효**(`firebase:` 접두 에러)일 때만 신규 계정. 일시(`network:`) 실패는 재시도만
- 영속화: PlayerPrefs `Wassup.Auth.RefreshToken`/`Wassup.Auth.UserName` — 재시작 시 자동 로그인 (검증: 같은 userId 유지)
- `ApiEnvelope`: 게임 서버 공통 `{success,data,errorDetail}` 단일 정의 — `SheetEnvelopeParser` 도 이걸 재사용하도록 교체
- 에러 표시: 서버 한글 메시지는 폰트에 글리프가 없어 errorCode 부분만 표시 (`DisplayableError`)

## Key Files

- `Assets/_Project/Scripts/Core/Api/` — ApiEnvelope / FirebaseAuthRestClient / UserSignApi / UserSession
- `Assets/_Project/Scripts/UI/Outgame/LoginPanelView.cs` — 플로우 전담 (패널 가시성은 소유 안 함)
- `Assets/_Project/Scripts/UI/Outgame/OutgameMenuController.cs` — `menuRoot`/`loginPanel` 게이팅
- OutgameScene — `MenuButtons`(기존 버튼 6개 reparent) + `LoginPanel`(SIGN IN/입력/LOGIN/상태)

## Verified

- EditMode 532개 통과 (무관 상시 실패 1건 제외), compile 0 error
- 실 API 왕복 프로브: signUp→sign-in(`userId` uuid, provider GUEST)→refresh(snake_case 실증). 무효 토큰 → HTTP 500 `INTERNAL_SERVER_ERROR` (AUTHENTICATION_FAIL 아님 — 서버가 500 래핑)
- 에디터 Play 4케이스: 게이트/로그인/자동 재로그인(같은 userId)/빈 이름 안내 — 콘솔 에러 0, 스크린샷 시각 확인

## Notes

- Firebase 웹 apiKey 는 공개 클라이언트 식별자 — SerializeField 로 씬에 포함 (의도됨)
- 이 에디터는 Enter Play Mode 도메인 리로드 off — static `UserSession` 이 Play 재진입을 생존. 자동 재로그인 경로를 에디터에서 테스트하려면 세션을 명시 클리어해야 함 (검증 시 임시 메뉴로 수행)
- idToken 수명 1시간 — 세션 중 만료 대응은 후속 (시작 시 refresh 로 항상 신선하게 시작)
- 프로브/검증 과정에서 익명 계정 2개 생성됨 (`claude-probe`, `sj-editor`) — 내부 dev, 정리 불필요

## Follow-up

- 실기기 Development Build 1회 (unit 1 체크박스 잔여)
- README 후속 후보: 세션 중 401 대응, 테스트 기록 적재(userId 사용), 계정 승격(sign/link), 로그아웃 버튼
