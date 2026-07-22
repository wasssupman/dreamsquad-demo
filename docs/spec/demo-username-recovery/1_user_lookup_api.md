# 1. UserLookupApi — GET /user 닉네임 조회

## 목적

로그인 시 "이 닉네임이 서버에 이미 있는가"를 판정한다. `GET /user` 에
`X-AUTH-USERNAME` 헤더만 실어 호출하고, 응답을 **3분기**(존재 / 없음 / 네트워크실패)로
분류한다. 이 분류가 unit 2 의 "새 계정 발급 여부" 결정의 근거다.

## 변경 대상

- `Assets/_Project/Scripts/Core/Api/UserLookupApi.cs` (신규)
- `Assets/_Project/Tests/EditMode/Api/UserAuthApiTests.cs` (테스트 추가)

## 구현

### 실증된 서버 동작 (프로브 2026-07-22)

- `GET /user` + `X-AUTH-USERNAME: {이름}` → 존재하면 `200` + User envelope
- 없는 이름 → `403` `HANDLE_ACCESS_DENIED` (body 있는 실패)
- 헤더 없으면 `403` (동일) — 그래서 존재/미존재를 body 유무가 아니라 **HTTP 결과**로 가른다

### 분류 규칙 (`Classify`, 순수 함수 — 테스트 대상)

`onDone(Result)` 3-state. `(body, transportError)` 입력:

| 조건 | Outcome | 의미 |
|---|---|---|
| success envelope 파싱됨 | `Found(user)` | 그 유저로 adopt (unit 2) |
| body 있고 success=false | `NotFound` | firebase 신규 발급 (unit 2) |
| body 비어있고 transportError 있음 | `NetworkError` | 발급 안 함, 정지 |

- `403`(존재X)은 UnityWebRequest 상 ProtocolError + **body 동반** → `NotFound`.
- 연결 실패는 body 없음 + transportError → `NetworkError`. 이 둘의 분리가 핵심
  (계약 #3: 네트워크 실패로는 절대 새 계정 안 만듦).
- User 바인딩은 기존 `UserSignApi.SignedInUser` 재사용(userId/userName/provider만).

### GetUser

`GET {base}/user`, 헤더 `X-AUTH-USERNAME` + `X-SERVICE-APP-VERSION`(기존 관례), 10초
타임아웃. 완료 콜백에서 body/transportError 를 `Classify` 에 넘겨 `onDone(Result)`.

## 완료 기준

- [ ] compile 성공 (`read_console` clean)
- [ ] EditMode 테스트 통과:
  - success body → `Found`, `user.userId` 바인딩
  - success=false body(403 HANDLE_ACCESS_DENIED) → `NotFound`
  - 빈 body + transportError → `NetworkError`
  - null body + transportError → `NetworkError`

## 주의 (알려진 한계)

- `GET /user` 는 "없는 유저"와 "다른 이유의 거부(500/MAINTENANCE 등 body 동반 실패)"를
  errorCode 없이 구분 못 한다 — 후자도 `NotFound` 로 분류돼 발급될 수 있다. 사용자가
  `/user` 경로를 택했고(검증된 API), 서버가 데모라 감수. 필요 시 errorCode 화이트리스트로
  좁히는 건 후속.

---

완료 2026-07-22 — compile clean, EditMode 1175 passed / 0 failed (신규 4 테스트 포함).
