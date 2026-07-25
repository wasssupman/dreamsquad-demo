# 3 — 배선 + Play 검증

## 목적

units 0~2 의 end-to-end 동작을 Play 로 확정한다. 특히 미뤘던 근본원인 질문 **"정상 완주 매치가 서버에 실점수로 남는가"** 를 여기서 닫는다.

## 배선

**없음.** `NoticePopup` 은 `RuntimeInitializeOnLoadMethod(BeforeSceneLoad)` 자기 부트스트랩 + DontDestroyOnLoad 라 씬에 배치할 오브젝트/SerializeField 가 없다. 리포터·`OnStartGame`·`BattleBridge` 변경도 전부 코드 경로. SceneTransition·EventSystem 은 기존 씬 것을 사용.

## 검증 프로토콜 (사용자 Play)

콘솔을 비운 뒤 로그인 상태로 진행. 세션은 데모 서버(`dev-api-somnia`).

1. **성공 경로**: 로비 `시작` → "매칭 중" 잠깐 → 배틀 진입 → **완주(승/패 결과 화면)** → `로비로`.
   - 콘솔 기대: `play ok — attemptId=X` → `VICTORY/DEFEAT triggered` → **`complete ok — score=Y`** (Y = 결과 화면 점수).
   - 서버 기대: 엔트리 X 의 score = Y (0 아님). ← **R1 종결**.
   - 로비 복귀 시 `reconcile … score=0` **안 떠야** 함(정상 complete 로 pending 정리됨).
2. **입장 실패 경로**(선택, wifi off 등): 로비 `시작` → play 실패 → 배틀 **미진입** + "입장 실패" 팝업 + `다시 시도` 동작.
3. **게스트**: 로그아웃(SKIP) 상태로 `시작` → 게이트 없이 즉시 진입(현행 유지).

## 완료 기준

- 성공 경로에서 완주 매치가 서버에 실점수로 기록(연속 2~3판 확인). 히스토리 0-엔트리 양산 멈춤.
- 실패 경로에서 배틀 미진입 + 알림.
- 비게이트 진입(TestMode/에디터 직접 Play)·게스트 동작 무회귀.
- (확인일/커밋 해시 기록)

## 남는 후속 (범위 밖 — README 후속 후보)

- 결함 A(비게이트 진입이 엔트리 생성), C/D(reconcile/abandon 0 정책). 입장 게이팅으로 정상 경로는 잡히나, TestMode·이탈 0-엔트리는 별도 결정.
