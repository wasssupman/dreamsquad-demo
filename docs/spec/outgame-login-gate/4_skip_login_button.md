# 4. 로그인 스킵 버튼 (그냥 들어가기)

## 목적

로그인 실패(네트워크/서버 장애) 시 게임 진입 자체가 막히는 문제를 푼다. 게임은 API 없이도 동작하므로, 로그인 패널 **우하단**에 스킵 버튼을 두어 인증 없이 로비로 진입할 수 있게 한다. 데모 사용자 구분(userId 기록)은 포기하는 대신 진행이 잠기지 않는다.

## 변경 대상

- `Assets/_Project/Scripts/UI/Outgame/LoginPanelView.cs` — `skipButton` SerializeField + 스킵 흐름
- OutgameScene — LoginPanel 하위 `SkipButton` 생성(우하단 앵커) + 참조 배선. UnityMCP 자동화 + 저장

## 구현

- **스킵 = 게스트 세션**: `UserSession.Set(new SignedInUser { userId="", userName=(입력값 or "GUEST"), provider="guest" }, idToken:"")` 후 `onSignedIn` 즉시 발화(링거 없음). 기존 게이트(`OutgameMenuController.ApplyAuthGate`)가 그대로 메뉴를 연다 — 게이트 로직 무변경.
- **영속화 없음**: PlayerPrefs 에 아무것도 쓰지 않는다. 게스트는 앱 재시작 시 다시 로그인 패널을 본다 (스킵은 세션 한정 escape hatch).
- **busy 중에도 동작**: `SetBusy` 가 skipButton 은 건드리지 않는다 — 요청이 hang 이어도 탈출 가능해야 하므로.
- **in-flight 안전**: 스킵으로 패널이 비활성화된 뒤 진행 중이던 sign-in 이 성공하면 실 세션이 게스트를 덮어쓴다(정상 승격). 비활성 상태에서 `StartCoroutine` 이 던지지 않도록 링거는 `isActiveAndEnabled` 일 때만 사용, 아니면 `onSignedIn` 직접 발화.
- **UI 텍스트 영문** (한글 글리프 없음): 버튼 라벨 `ENTER WITHOUT LOGIN >`. LOGIN 대비 시각적으로 보조적(작은 텍스트 버튼).

## 완료 기준

- [x] compile 오류 없음 (2026-07-07)
- [x] 씬 YAML: `SkipButton` GameObject + `skipButton: {fileID: 1927007712}` non-zero, diff 순수 추가 260줄 (2026-07-07)
- [x] 에디터 Play (2026-07-07): ① 스킵 클릭 → 패널 숨김+메뉴 노출, `UserSession` provider=guest/userName=GUEST, 콘솔 에러 0 ② 세션 클리어 → 로그인 패널 복귀 (PlayerPrefs 계정 보존한 채 리셋 경로 등가 검증) ③ 정상 로그인 경로는 코드 무변경(링거 가드만 추가) — 사용자 Play 에서 최종 확인
- [x] 스크린샷: 패널 우하단 `ENTER WITHOUT LOGIN >` 배치 확인 (우 16px·하 8px 마진)
