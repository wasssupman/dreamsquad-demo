# 2. LoginPanelView — 발급 직전 조회 + adopt

## 목적

"새 firebase 계정을 만들기 직전"에 unit 1 의 `UserLookupApi.GetUser` 를 끼워, 닉네임이
이미 서버에 있으면 새로 만들지 않고 그 계정을 username 모드로 adopt 한다. 없으면 기존
firebase 발급 그대로. 네트워크 실패면 발급하지 않는다.

## 변경 대상

- `Assets/_Project/Scripts/UI/Outgame/LoginPanelView.cs`

## 구현

### 새 PlayerPrefs 표식

- `UsernameModePrefsKey = "Wassup.Auth.UsernameMode"` — username 모드로 복구된 세션
  표식(int 1). 다음 실행 때 silent 재-adopt 의 근거. firebase 모드와 상호 배타로 유지.

### 발급 지점에 lookup 삽입

`OnLoginClicked` 의 두 "mint" 진입(저장 토큰 없음 / refresh 확정실패 후)에서 `SignUpFresh`
직접 호출 → `MintOrAdopt(userName, epoch)` 로 교체:

```
MintOrAdopt → UserLookupApi.GetUser(name):
  Found        → AdoptExistingUser (username 모드)
  NotFound     → SignUpFresh (기존 firebase 발급)
  NetworkError → HandleFailure (발급 안 함)
```

refresh 성공 경로(저장 토큰이 살아있는 같은 기기)는 **손대지 않는다** — 이미 유효한
firebase 계정이므로 바로 `SignInToGameServer`.

### AdoptExistingUser

- prefs: `UserName` 저장 + `UsernameMode=1` + `RefreshToken` 삭제(스테일 방지) + Save.
- `UserSession.Set(user, idToken:"", baseUrl, authUserName: userName)` — 헤더에 쓸 값은
  방금 200 을 만든 **입력 이름**(검증된 값). `Current` 는 서버 user 객체.
- firebase 성공과 동일한 linger → `onSignedIn`.

### 모드 배타 유지

- `SignInToGameServer`(firebase 성공): `UsernameMode` 키 삭제 추가.
- `ResetAccount`: `UsernameMode` 키 삭제 추가.

### Start() silent 재진입

`IsSignedIn` 아니면:
1. 저장 토큰+이름 있음 → firebase silent (기존).
2. 아니고 `UsernameMode==1`+이름 있음 → `UserLookupApi.GetUser` silent → Found 면
   `AdoptExistingUser`, 아니면 조용히 패널로(발급 안 함, 표식 유지).
3. 아무것도 없음 → 로그인 패널.

## 완료 기준

- [ ] compile 성공 (`read_console` clean)
- [ ] 전체 EditMode 회귀 없음 (LoginPanelView 는 순수 단위테스트 대상 아님 — 아래 참조)
- [ ] 사용자 Play 검증: dev "RESET ACCOUNT" → 이름 "wassup" 입력 → 새 계정 생성 없이
  기존 계정으로 로그인(중복 미생성). 없는 이름 → 정상 가입.

## 검증 경계 (정직하게)

LoginPanelView 는 MonoBehaviour + UI + 네트워크 콜백 오케스트레이션이라 EditMode 순수
단위테스트 대상이 아니다. 분류 로직(`Classify`)은 unit 1 에서 테스트됨. 서버측 전제(존재
닉네임이 안정된 userId 를 돌려줌)는 curl 프로브로 실증됨(`wassup` →
`019f8019-…`). 따라서 unit 2 는 **compile + 회귀 없음 + 사용자 Play** 로 검증한다.

username 모드에서 tournament 리포트가 실제로 붙는 것은 unit 3(헤더 seam)이 완성해야 동작
— unit 2 시점에는 로그인/게이트까지만.

---

완료(코드) 2026-07-22 — compile clean, EditMode 1175 passed / 0 failed (회귀 없음).
사용자 Play 확인(RESET → "wassup" → 중복 미생성)은 대기.
