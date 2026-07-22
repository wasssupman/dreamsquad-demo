# 0. UserSession 인증 모드 + AuthCredential

## 목적

세션이 두 인증 모드(firebase Bearer / demo username 헤더) 중 무엇인지를 `UserSession` 이
들고 있게 하고, "이 요청을 어떻게 인증하나"를 값 하나(`AuthCredential`)로 캡슐화한다.
이후 unit 들이 이 위에 얹힌다.

## 변경 대상

- `Assets/_Project/Scripts/Core/Api/AuthCredential.cs` (신규)
- `Assets/_Project/Scripts/Core/Api/UserSession.cs`
- `Assets/_Project/Tests/EditMode/Api/UserAuthApiTests.cs` (테스트 추가)

## 구현

### AuthCredential (신규)

읽기 전용 struct. `idToken`(firebase) 또는 `userName`(username) 중 하나를 담고, 단일
seam(unit 3)에서 `Apply(UnityWebRequest)` 로 정확히 한 헤더만 붙인다.

- `Bearer(idToken)` / `Username(userName)` / `None` 팩토리.
- `IsValid` = 둘 중 하나라도 비어있지 않음.
- `Apply`: userName 있으면 `X-AUTH-USERNAME`, 아니면 idToken 있으면 `Authorization: Bearer`.
  username 이 Bearer 를 이긴다(서버 동작과 일치). `UnityEngine.Networking` 의존이라 별도 파일.

### UserSession (확장)

- `AuthUserName` 프로퍼티 추가 (username 모드 표식).
- `Set(user, idToken, gameServerBaseUrl = null, authUserName = null)` — 4번째 optional
  파라미터 추가. 기존 호출부(firebase·guest·테스트)는 authUserName=null 로 무변경 컴파일.
- `HasAccount` = `IdToken` 비어있지 않음 **OR** `AuthUserName` 비어있지 않음.
  게스트(둘 다 빈 값)는 false. `IsSignedIn`(Current != null, 게스트 true)과 공존한다.
- `Credential` = AuthUserName 우선 → Bearer(IdToken) → None.
- `Clear()` 에 `AuthUserName = null` 추가.

## 완료 기준

- [ ] compile 성공 (`read_console` clean)
- [ ] EditMode 테스트 통과:
  - firebase Set → `HasAccount` true, `Credential` 이 Bearer(idToken)
  - authUserName Set(idToken 빈 값) → `HasAccount` true, `Credential` 이 Username
  - guest Set(idToken 빈 값, authUserName 없음) → `HasAccount` false, `Credential.IsValid` false, 단 `IsSignedIn` true
  - `Clear()` → `HasAccount`/`IsSignedIn` false, `AuthUserName` null
  - `AuthCredential.Apply`: username 모드는 `X-AUTH-USERNAME`, bearer 모드는 `Authorization` 헤더 세팅 (`GetRequestHeader` 확인)

---

완료 2026-07-22 — compile clean, EditMode 1171 passed / 0 failed (신규 6 테스트 포함, 스킵 2는 기존 무관).
