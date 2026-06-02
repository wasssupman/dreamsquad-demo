# 3 — 씬 전환 + GameManager 비영속화 + smoke

## 목적

Outgame ↔ Battle 왕복을 완성한다. GameManager 를 전투 전용·비영속으로 바꿔 재진입을 깨끗하게.

## 변경 대상

- 수정 `Assets/_Project/Scripts/UI/Outgame/OutgameMenuController.cs` — `OnStartGame` 채움
- 수정 `Assets/_Project/Scripts/Core/GameManager.cs` — `DontDestroyOnLoad` 제거
- 신규 `Assets/_Project/Scripts/UI/Outgame/ReturnToMenuButton.cs` (또는 기존 Result UI 에 핸들러)
- 빌드 세팅: Scenes In Build = [OutgameScene(0), BattleScene(1)]
- 신규 `Assets/_Project/Tests/PlayMode/OutgameFlowSmokeTest.cs`

## 구현

전환 (씬 이름 상수로):
- `OnStartGame()` → `SceneManager.LoadScene("BattleScene")`.
- BattleScene 전투 종료/일시정지 UI 에 **메인으로** 버튼 → `SceneManager.LoadScene("OutgameScene")`.

`GameManager.Awake`:
- `DontDestroyOnLoad(gameObject);` **제거**. 싱글톤 가드(`Instance` 중복 파기)는 유지 — 단, 비영속이므로 매 씬 1개만 존재. BattleScene 떠날 때 자연 파기.
- `Start()` 폴백 유지: `draftController != null` → 기존 드래프트 진행(A 비파괴). 추후 C 가 `PlayerProfileSO.selectedSquad` 분기 추가.
- 회귀 점검: `GameManager.Instance` 참조처가 BattleScene 내부에서만 쓰이는지 확인(이미 그러함). Outgame 에서 `GameManager.Instance` 접근 금지.

빌드 세팅: UnityMCP `manage_editor`/`EditorBuildSettings` 로 두 씬 등록, OutgameScene 을 index 0.

## 완료 기준

- Play(OutgameScene) → 게임 시작 → BattleScene 로드 → 드래프트/배치/전투 정상.
- 전투 화면 **메인으로** → OutgameScene 복귀, 에러/중복 GameManager 없음.
- 재진입(메인→전투→메인→전투) 2회 반복 시 상태 누수·중복 싱글톤 경고 없음.
- PlayMode smoke (`OutgameFlowSmokeTest`): OutgameScene 로드 → `OnStartGame` → BattleScene 활성 씬 확인 → (가능하면) 복귀 확인.
- 기존 `DraftFlowSmokeTest` 등 PlayMode 테스트 여전히 통과.
- read_console clean.

## 주의

- `DontDestroyOnLoad` 제거가 다른 영속 가정(예: 세션 로거)을 깨지 않는지 확인. `BattleLogger.StartSession` 은 씬마다 새로 시작되어도 무방한지 점검.
