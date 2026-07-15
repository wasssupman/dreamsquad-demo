# 0 — "로비로" 버튼 + 재시작 배선 해제

## 목적

결과창 하단 버튼을 "다시하기" 에서 "로비로" 로 바꾸고, 누르면 OutgameScene 으로 나가게 한다. 재시작 이벤트 배선을 끊되 `BattleBridge` 의 재시작 메서드는 남긴다.

## 변경 대상

- `Assets/_Project/Scripts/UI/ResultScreen.cs`
- `Assets/_Project/Scripts/Bridge/BattleBridge.cs`
- `Assets/_Project/Scripts/Core/GameManager.cs` — 낡은 주석 1줄

## 구현

### ResultScreen

`MenuPopup.OnExit()` 과 같은 idiom — 뷰가 직접 나간다. 이벤트를 경유하지 않는다.

- `public event Action RestartRequested` **삭제**. `using System;` 이 이 이벤트 때문에만 필요했다면 함께 정리.
- 필드 `restartButton` → `lobbyButton`.
- `Awake`/`OnDestroy` 의 `onClick` 등록/해제 대상 이름만 따라 변경.
- `OnRestartClicked()` → `OnLobbyClicked() => SceneTransition.Go(SceneNames.Outgame);`
  - `SceneNames`/`SceneTransition` 은 `Wassup.Core` — `ResultScreen.cs:6` 에 이미 `using Wassup.Core;` 있음.
- `BuildFooter()`: GameObject 이름 `"RestartButton"` → `"LobbyButton"`, 라벨 `"다시하기"` → `"로비로"`.
- 클래스 상단 주석(`ResultScreen.cs:12-13`)의 "Emits RestartRequested when the player taps Restart; BattleBridge subscribes to tear down and restart the match." 를 실제 동작으로 갱신.

`Hide()` 를 먼저 부를 필요 없다 — 씬이 통째로 언로드된다. `MenuPopup.OnExit` 이 pause lease 를 먼저 놓는 것과 달리 결과창은 lease 를 들고 있지 않다.

### BattleBridge

- `Start()` (`BattleBridge.cs:329-335`) — 본문이 구독 하나뿐이므로 **메서드째 제거**.
- `OnDestroy()` (`BattleBridge.cs:4313-4318`) — `resultScreen.RestartRequested -= OnRestartRequested;` 와 그 `if (resultScreen != null)` 블록 제거. `TeardownCurrentBattle()` 이하는 그대로.
- `OnRestartRequested` / `ReLogSkillLoadoutForNewSession` / `EnterPlacementOrGift` 는 **남긴다** (사용자 결정). `OnRestartRequested` 위에 왜 호출처가 없는지 한 줄 적는다 — 끊긴 배선이 아니라 의도라는 표시:

  ```csharp
  // result-screen-lobby-exit unit 0 — 결과창 버튼이 "로비로" 가 되면서 호출처가
  // 없다. 재시작을 되살릴 때 다시 구독하면 되도록 로직은 남겨둔다.
  ```

- `[SerializeField] private ResultScreen resultScreen;` 는 **유지** — `ShowVictory`/`ShowDefeat`/`UpdateLeaderboard` 가 계속 쓴다.

### GameManager

`GameManager.cs:143` 의 `// entry; restarts issue their own via BattleBridge.OnRestartRequested.` 는 사실이 아니게 된다. 전투 진입이 유일 발급처가 됐다는 내용으로 갱신.

## 완료 기준

- [x] compile clean, 콘솔 에러 0.
- [x] `RestartRequested` grep 결과 0건 (주석 포함).
- [x] 결과창 하단 버튼 라벨이 **"로비로"**, GameObject 이름 `LobbyButton` (Play 실측).
- [x] Play: `ShowVictory` 후 `LobbyButton.onClick.Invoke()` → **`activeScene == OutgameScene`**, `OutgameMenuController` 기동, `BattleBridge` 언로드, `SceneTransition.Instance` 정상 경유. 콘솔 에러 0.
- [x] EditMode 854 중 852 passed / **0 failed** / 2 skipped(기존부터 문서화된 Ignore, 무관).
- [ ] 로비 진입 후 START → 전투 재진입 — **미실행**. `game-start-loadout-gate` 가 검증한 경로 그대로이고 이 unit 이 건드리지 않았다.

확인 2026-07-16 — 로비 전환. **되돌리면 안 되는 것**: 뷰가 직접 `SceneTransition.Go` 를 부르는 형태(=`MenuPopup.OnExit` 선례). `BattleBridge` 를 경유시키면 ECS 게이트웨이가 씬 네비게이션을 소유하게 된다.
