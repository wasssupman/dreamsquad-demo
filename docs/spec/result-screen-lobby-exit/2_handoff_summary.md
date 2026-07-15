# 2 — 인계 요약

## Commit

- unit 0 (코드: 로비로 버튼 + 재시작 배선 해제) — 본 문서와 같은 브랜치
- unit 1 (씬: 레거시 자식 3개 제거) — 별도 커밋

## Implemented

- 결과창 하단 버튼이 **"로비로"**(`LobbyButton`). 클릭 → `SceneTransition.Go(SceneNames.Outgame)`.
- 뷰가 직접 나간다 — `MenuPopup.OnExit()` 과 같은 idiom. `RestartRequested` 이벤트와 `BattleBridge` 경유가 사라졌다.
- 재시작 로직(`OnRestartRequested` / `ReLogSkillLoadoutForNewSession` / `EnterPlacementOrGift`)은 **삭제하지 않고 남겼다**(사용자 결정). 이벤트 구독만 끊겨 호출처가 0.
- `BattleBridge.Start()` 제거(본문이 구독 하나뿐), `OnDestroy` 의 구독 해제 제거.
- `BattleScene` 의 `ResultScreen` 밑 레거시 3개(`ResultLabel` / `RestartButton` / `RedraftButton`) 삭제 → `childCount` 5 → 2.

## Key Files

- `Assets/_Project/Scripts/UI/ResultScreen.cs` — `OnLobbyClicked`, `BuildFooter`
- `Assets/_Project/Scripts/Bridge/BattleBridge.cs` — 휴면 상태가 된 재시작 경로 (주석 참조)
- `Assets/_Project/Scripts/Core/GameManager.cs:142-144` — `BeginMatch` 유일 발급처
- `Assets/_Project/Scenes/BattleScene.unity` — `ResultScreen` 자식 3개 삭제

## Verified

- Play 실측: `ShowVictory` → `LobbyButton.onClick.Invoke()` → `activeScene == OutgameScene`, `OutgameMenuController` 기동, `BattleBridge` 언로드. 콘솔 에러 0.
- EditMode 854 중 852 passed / **0 failed** / 2 skipped(기존 Ignore, 무관).
- 씬 diff: 오브젝트 3개 + `Text` 자식 2개 + 딸린 MonoBehaviour 7개 삭제뿐. **추가 0건**, `GameManager` 블록 md5 동일.
- `RestartRequested` grep 0건.

## Notes (되돌리면 안 되는 의도)

- **뷰가 직접 `SceneTransition.Go` 를 부른다.** `BattleBridge` 를 경유시키면 ECS 게이트웨이가 씬 네비게이션을 소유한다. 선례는 `MenuPopup.OnExit`. (`game-start-loadout-gate` 의 "팝업은 네비게이션을 모른다" 는 **패널 가시성** 규칙이라 여기 적용되지 않는다.)
- **`TournamentMatchReporter.BeginMatch()` 는 `GameManager.OnEnable` 이 유일 발급처가 됐다.** 재시작 경로의 `BeginMatch`(`BattleBridge.cs:~362`)는 휴면. 전투 진입이 유일 경로이므로 시도 집계는 그대로다.
- **`BattleBridge` 의 재시작 3인방은 죽은 코드가 아니라 휴면이다** — 사용자 결정으로 남겼고 이유가 주석에 있다. 지우려면 사용자 확인 필요.
- **`ResultCanvas.sortingOrder`(0)를 만지지 말 것.** 중첩 캔버스의 `overrideSorting=true` 가 결과창을 전역 오버레이 정렬에 2000 으로 올린다 — 루트가 0 이어도 이미 `MenuReturnCanvas`(1000) 위다. 상세와 반증 데이터는 `1_scene_legacy_cleanup.md` 하단 "오진 기록".

## 이번 세션의 오진 (같은 함정 반복 방지)

정렬 버그를 보고했다가 **실측으로 자기 반증**했다. 경위:

1. `ShowVictory` 를 직접 호출해 HUD 를 띄운 채 결과창을 캡처했다. 스크린샷에서 HUD 가 "선명해 보여" 딤이 안 덮는다고 판단했다.
2. 근거를 캔버스 정렬에서 찾았다 — 루트 `ResultCanvas` 가 0 이니 중첩 2000 은 무의미하다고 추론.
3. **픽셀을 재보니 반대였다.** 그 스크린샷에서 이미 점수 라벨 `(2,12,10)`, 각성 `(2,7,10)`, 덱 도크 `(0,0,0)`, MENU `(4,8,21)` — 전부 딤 아래.
4. `0 → 2000` 으로 바꿔도 MENU 픽셀은 `(4,8,21)` 로 **완전 동일**. 변화 0.

교훈 두 가지:

- **렌더된 PNG 를 눈으로 보고 밝기를 판정하지 말 것.** 고대비 UI 는 8% 투과에서도 밝아 보인다. 밝기 주장은 픽셀 샘플로만.
- **"코드 주석이 틀렸다" 는 결론은 반증을 먼저 시도할 것.** `ResultScreen.cs:296-300` 의 주석은 정확했고, 이를 "고치는" 변경은 맞는 문서를 틀리게 만들 뻔했다.

같은 방식으로 "레거시 유령 텍스트가 선명하다" 도 과장이었다 — 실제 영향은 1~2/255. 레거시 제거는 유효하지만 근거는 **참조 없는 죽은 오브젝트**이지 시각 결함이 아니다.

## Follow-up

- **미실행**: 로비 진입 후 START → 전투 재진입. `game-start-loadout-gate` 가 검증한 경로 그대로라 건드리지 않았다.
- **미실행**: 사용자 Play 육안 확인.
- 결과창이 떠도 전투 시뮬이 계속 돈다(실측: 결과창 표시 중 전투가 진행돼 패배까지 감). 씬 언로드로 정리되므로 무해하나 결과 확정 후 정지가 맞는지는 별도 판단 — README 후속 후보.
- 나머지 후속 후보(`ResultCanvas` 의 베이크된 빈 roots, MENU/MAP SETTINGS 겹침, `BattleLogger.StartReplacementSession` 호출처 0)는 README 하단 참조.
