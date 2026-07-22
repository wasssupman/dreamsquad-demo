# Unit 0 — UserSession 재발급 능력

## 목적

`UserSession` 이 firebase Bearer 세션의 idToken 을 **자체적으로** 재발급할 수 있게 한다. 재발급 소스(refreshToken + firebaseApiKey)를 보관하고, 코얼레스된 `TryRefreshBearer` 를 노출한다. (아직 아무도 호출하지 않음 — unit 1 이 소비.)

## 변경 대상

- `Assets/_Project/Scripts/Core/Api/UserSession.cs`
- `Assets/_Project/Tests/EditMode/` — 신규 `UserSessionRefreshTests.cs` (신규 .cs → `refresh_unity` scope=all, meta 회수 주의)

## 구현

1. **필드 추가** (private set): `RefreshToken`, `FirebaseApiKey`. `Set` 시그니처에 `string refreshToken = null, string firebaseApiKey = null` 추가 후 대입. `Clear()` 에서 둘 다 null.
   - 기존 `Set` 호출부(guest/username)는 인자 미전달 → null 로 세팅(정상: 만료 없는 세션).
2. **`TryRefreshBearer(Action<bool> done)`**:
   - `RefreshToken`/`FirebaseApiKey` 중 하나라도 비면 즉시 `done(false)` (firebase 세션 아님).
   - 대기열(`List<Action<bool>>`)에 `done` 추가. 재발급 in-flight(`_refreshing`)면 return(코얼레스).
   - `_refreshing=true`, `FirebaseAuthRestClient.RefreshIdToken(FirebaseApiKey, RefreshToken, ...)`:
     - 성공(`tokens != null`): `IdToken = tokens.Value.idToken`; refreshToken 회전값 있으면 `RefreshToken` 갱신.
     - 실패이고 `FirebaseAuthRestClient.IsDefinitiveAuthError(error)`: in-memory 소스 정리(`RefreshToken=null; FirebaseApiKey=null`) — 죽은 토큰 반복 재시도 방지. network 성 실패는 소스 유지.
     - `_refreshing=false`; 대기열 스냅샷 후 clear, 각 waiter 에 `done(ok)`.
   - 메인스레드 단일 실행(UnityWebRequest 콜백) → 락 불필요.
   - `using System.Collections.Generic;`, `using UnityEngine;`(Debug) 추가.
3. **Clear 중 경합 방어**(경미): refresh 콜백에서 성공 적용 전 `RefreshToken`/`FirebaseApiKey` 가 비었으면(로그아웃 발생) `ok=false` 로 처리해 부분 세션 부활 방지.

## 완료 기준

- 컴파일 오류 0 (`read_console`).
- EditMode 테스트 통과:
  - username/guest 세션(`Set` 에 refresh 소스 미전달)에서 `TryRefreshBearer` 가 **동기적으로 `done(false)`** (네트워크 미접촉).
  - `Set` 이 RefreshToken/FirebaseApiKey 채우고 `Clear()` 가 비움.
  - `Set(..., authUserName)` (username 모드)은 refresh 소스 null 유지 → `Credential` 은 Username 모드.
- 실제 네트워크 재발급 경로는 unit 2 라이브 검증에서.

완료: 2026-07-22 — 컴파일 0, EditMode 5/5 통과.
