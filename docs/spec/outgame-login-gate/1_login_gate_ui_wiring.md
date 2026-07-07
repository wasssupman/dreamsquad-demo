# 1. 로그인 게이트 UI + 씬 wiring

## 목적

OutgameScene 에 로그인 패널을 추가하고, 인증 전에는 로비 메뉴를 숨긴다. 인증 성공 시 패널을 닫고 메뉴를 노출한다.

## 변경 대상

- `Assets/_Project/Scripts/UI/Outgame/LoginPanelView.cs` (신규) — 이름 InputField + LOGIN 버튼 + 상태 라벨 + 인증 로직(자동/수동, README 신원 정책 준수). 성공 시 `UserSession` 세팅 + `onSignedIn` 콜백. **패널 GameObject 의 활성/비활성은 소유하지 않음** (critic MINOR-A). apiKey/baseUrl 은 SerializeField (기본값 somnia-dev/실 dev URL)
- `Assets/_Project/Scripts/UI/Outgame/OutgameMenuController.cs` — **`menuRoot` SerializeField 신규** (critic MAJOR-2): 로비 버튼 전체를 감싸는 컨테이너. 게이팅 = `menuRoot.SetActive(signedIn)` + `loginPanel.SetActive(!signedIn)` — **가시성 전환은 이 컨트롤러가 단독 소유**. 기존 패널(squad 등) 로직 무변경
- OutgameScene — **`MenuButtons` 컨테이너 신규 생성** 후 기존 버튼 5개(Start/Squad/Dreamcatcher/TestMode/StatRefresh)와 StatRefreshResult 를 그 하위로 reparent (위치 유지). StatRefreshButtonView 의 자체 dev 게이트는 유지 — 로그인 게이트와 AND 조건. LoginPanel(배경+InputField+버튼+상태 라벨) 생성·배선. UnityMCP 자동화 + 저장

## 구현

- **시작 흐름**: PlayerPrefs 에 refreshToken 있음 → "SIGNING IN..." → refresh → sign-in → 성공: 패널 스킵 / 실패: 패널 표시 + 사유. 없음 → 패널 표시 (저장된 userName 있으면 미리 채움).
- **버튼 흐름**: 이름 trim → 빈 값 "ENTER YOUR NAME" → **저장 refreshToken 있으면 refresh 우선, 확정 무효일 때만 신규 signUp** (README 신원 정책) → sign-in(Bearer idToken, userName) → refreshToken/userName PlayerPrefs 저장 → `SIGNED IN AS {name}` 표시 후 메뉴 전환.
- 요청 중 버튼/입력 비활성 (중복 방지). 스탯 갱신 버튼(REFRESH STATS)도 게이트 뒤 — 메뉴 컨테이너와 함께 숨겨짐.
- UI 텍스트 영문 (한글 글리프 없음). 씬 배선은 unity-feature-wiring 절차 (YAML non-zero 검증 + Play 검증).

## 완료 기준

- [ ] compile 오류 없음
- [ ] 씬 YAML: LoginPanel/참조 필드 전부 non-zero
- [ ] 에디터 Play: 최초(저장값 없음) → 메뉴 숨김+패널 표시 → 이름 입력+LOGIN → 메뉴 노출, `UserSession.userId` 채워짐. 재시작 Play → 자동 로그인으로 패널 스킵. 빈 이름 시 안내. 콘솔 에러 0
- [ ] 실기기 Development Build 1회 (다음 빌드 때)
