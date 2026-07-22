# 2 — 배선 + Play 검증

## 목적

두 마감 진입점을 실제 UI 흐름에 연결한다. 신규 GameObject 0개 — 기존 콜백에 한 줄씩.

## 변경 대상

- `Assets/_Project/Scripts/UI/MenuPopup.cs`
- `Assets/_Project/Scripts/UI/Outgame/OutgameMenuController.cs`

## 구현

### 메뉴 나가기 → AbandonMatch

`MenuPopup.OnExit()` 에서 `SceneTransition.Go(SceneNames.Outgame)` **직전**:

```csharp
Wassup.Core.Api.TournamentMatchReporter.AbandonMatch();
```

- MENU 팝업은 배틀 씬 전용이라 나가기 = 라이브 판 기권. 드래프트/배치 중 나가도 attempt 는 이미 `BeginMatch`(GameManager.OnEnable)로 열렸으므로 동일하게 0점 마감된다.

### 로비 진입 → ReconcilePending

`OutgameMenuController.ApplyAuthGate()` 안, `bool signedIn = ...` 이후:

```csharp
if (UserSession.HasAccount)
    Wassup.Core.Api.TournamentMatchReporter.ReconcilePending();
```

- `ApplyAuthGate` 는 로비 Awake + `onSignedIn`(자동 리프레시·명시 로그인 모두) + 로그아웃 전이에서 발화 → 정확히 "post-auth, 로비" 지점. `HasAccount` 가드로 게스트/로그아웃은 no-op.
- 배틀→로비 복귀 시에도 발화하지만 store 는 이미 clear 되어 no-op(무해).

## 완료 기준

- 컴파일 통과, 콘솔 무에러.
- **Play 검증** (실계정 로그인 + dev 서버):
  1. **메뉴 나가기 0점**: 배틀 진입(play 로그 확인) → MENU → 나가기 → 콘솔에 기권 `complete score=0` 전송 로그, 서버 결과에 해당 attempt 0점 마감.
  2. **kill 재실행 복구**: 배틀 중 앱 강제종료 → 재실행 → 로그인/자동복원 후 로비에서 `ReconcilePending` 이 0점 `complete` 전송 + store clear 로그.
  3. **over-window discard**: startedAt 을 10분 이상 과거로 조작(또는 상수 임시 축소) 후 재실행 → `complete` 없이 store clear(discard-only) 로그.
  4. **정상 종료 무회귀**: 승/패 결과팝업 → 실점수 `complete` 정상, 이후 로비 복귀 시 `ReconcilePending` 이 no-op(store 이미 clear).
  5. **게스트 무영향**: 게스트로 배틀→나가기/재실행 시 save/complete 호출 없음.

## 완료 스탬프

- 배선 구현: 2026-07-22 `8b5fdaaf` — 컴파일 0에러. PlayMode 스모크 4건(OutgameFlow/SceneTransition/TallyFlow×2) 통과.
- **실서버 왕복 검증 완료: 2026-07-22** — 일회용 Editor 프로브(익명 계정 발급 → 실 `TournamentApi.Play` → 기능 메서드)로 dev 서버 왕복:
  - 복구: `play ok(IN_PROGRESS, attemptId)` → `ReconcilePending` → **`reconcile complete ok — score=0`** 서버 수락, store 삭제.
  - 기권: `BeginMatch`→play 저장 → `AbandonMatch` → **`abandon complete ok — score=0`** 서버 수락, store 삭제.
  - 초과 폐기: startedAt TTL+1h 과거 → `ReconcilePending` → **`pending attempt discarded`**, complete 없음, store 삭제.
  - 프로브는 검증 후 삭제(미커밋).
- **선행 발견/수정**: `TournamentApi.Play` 응답 스키마 중첩(`data.userTournamentState`) — `tournament-play-report` 커밋에서 수정. 이게 없으면 attemptId 미수신으로 전 경로 무동작.
- 잔여(수동, 실기기): 실제 배틀 플레이→메뉴 나가기 UI 경로 / 실제 앱 강제종료→재실행 로비 복구는 기기에서 최종 확인 권장(로직·서버 왕복은 위에서 검증됨).
