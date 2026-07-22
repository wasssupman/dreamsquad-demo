# demo-username-recovery — 재설치 후 닉네임 기반 계정 복구 (데모 전용)

> 상태: 계획 (승인 대기) · 2026-07-22

## 문제

Firebase 익명 인증을 쓰므로 계정의 재접근 경로는 refresh token 뿐이다. 앱을 삭제하면
token 이 사라지고, 같은 닉네임으로 다시 로그인해도 서버는 **새 익명 계정을 발급**한다.
결과: 이름만 같은 별개 계정이 중복 생성된다 (실사례: "wassup" 2개).

서버는 데모 단계라 이를 개선하지 않는다. 서버에는 검증된 우회로 `X-AUTH-USERNAME` 헤더가
있다 — 이 헤더를 실으면 Bearer 없이(있어도 무시하고) 해당 닉네임 유저로 인증된다.
(프로브 확인: `GET /user` 에 헤더만 실으면 200+유저정보, 헤더 없으면 403, 헤더가 Bearer 를 이김.)

## 목표

로그인 시 **신규 발급 직전에 서버에 그 닉네임이 이미 있는지 조회**하고, 있으면 새 계정을
만들지 않고 그 유저로 인증(username 모드)한다. 없으면 기존 firebase 발급 경로 그대로.

```
이름 입력 → GET /user (X-AUTH-USERNAME: 이름)
  200 (존재)   → username 모드로 adopt (firebase 안 탐, idToken 없음)
  403 (없음)   → firebase 발급 (SignUpAnonymous → /user/sign/in, 기존 그대로)
  네트워크 실패 → 발급 안 함, 에러 표시 후 정지

이후 인증 호출:
  username 모드 → X-AUTH-USERNAME 헤더
  firebase 모드 → Bearer {idToken}  (기존 그대로)
```

## 작업 단위

| # | 문서 | 작업 | 목적 |
|---|---|---|---|
| 0 | `0_usersession_auth_model.md` | `UserSession` + `AuthCredential` | 두 인증 모드 상태 + 실계정 술어 + 크리덴셜 선택 |
| 1 | `1_user_lookup_api.md` | `UserLookupApi.GetUser` | `GET /user` 3분기 조회 (found / 403=없음 / network) |
| 2 | `2_login_panel_branch.md` | `LoginPanelView` | 발급 직전 조회 삽입 + adopt + 재실행 시 silent 재-adopt |
| 3 | `3_api_caller_auth_branch.md` | `TournamentApi.Send` + 호출부 | 헤더 부착 단일 seam 에서 모드 분기, guest 게이트 교체 |
| 4 | `4_gate_predicate_migration.md` | `ResultScreen`, `OutgameMenuController` | 실계정 술어를 `IdToken` → `HasAccount` 로 |
| 5 | `5_handoff_summary.md` | 인계 요약 | (구현 종료 시) |

## feature-wide 계약

1. **두 인증 모드는 상호 배타**: firebase 모드는 `IdToken` 을, username 모드는 `AuthUserName` 을
   채운다. 한 세션에 하나만 유효.
2. **실계정 술어 = `UserSession.HasAccount`** = `IdToken` 비어있지 않음 **OR** `AuthUserName`
   비어있지 않음. 게스트(SKIP)는 둘 다 비어 `false`. 기존 `IsNullOrEmpty(IdToken)` 게이트를
   이 술어로 대체한다 (게스트 배제 의도는 유지, username 계정만 게스트에서 빠짐).
3. **계정 발급은 확정 403 에만**: `GET /user` 가 응답 body 를 동반한 실패(=없는 유저)를 줬을
   때만 firebase 신규 발급으로 넘어간다. 네트워크 실패(body 없음)는 발급하지 않는다 —
   기존 identity policy(`network:` 는 새 계정 안 만듦)를 그대로 계승.
4. **헤더 부착은 단일 seam**: `TournamentApi.Send` 한 곳에서 `AuthCredential.Apply` 로
   Bearer/`X-AUTH-USERNAME` 를 고른다. 호출부마다 분기 금지 (한 곳 누락 시 조용히 다른 계정에
   기록되는 사고를 구조적으로 차단).
5. **`UserSignApi.SignIn` 은 항상 firebase Bearer**: 이 호출은 firebase 발급 경로에서만
   불린다. username 모드에서는 호출되지 않으므로 손대지 않는다.
6. **데모 전용 · 제거 가능**: username 모드는 서버 데모 우회로에 의존한다. 서버가 헤더를
   막으면 이 경로만 죽고 firebase 는 무사해야 한다. 근본 해결은 서버측 custom token 또는
   구글 연동(`/user/sign/link`, 후속 후보).

## 미검증 (런타임 확인 대상)

- `X-AUTH-USERNAME` 가 `/tournament/*` 등 다른 인증 엔드포인트에서도 동작하는지는
  미검증 (`GET /user` 에서만 실증). 구조는 지원하나 서버가 그 필터에서 헤더를 존중하는지는
  라이브 확인. 실패해도 firebase 경로는 영향 없음.

## 파이프라인 커버리지

N/A — 플레이 오브젝트(유닛/적/투사체/해저드/VFX)를 신설·변경하지 않는다. 인증/API 계층 전용.

## 후속 후보 (현 스코프 밖)

- 익명 → 구글/애플 IDP 연동 (`accounts:signInWithIdp` + `POST /user/sign/link`) — 재설치를
  넘어 살아남는 정식 계정 복구. 이게 근본 해결이며 username 모드를 은퇴시킨다.
- 서버측 custom token 발급 엔드포인트 (userId → 진짜 Firebase token). 서버 작업 필요.
- 세션 중 idToken 만료(1시간) 시 refresh 후 재시도 — 본 스펙과 독립된 firebase 모드 갭.
