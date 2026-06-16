# 4 — 아웃게임 테스트 모드 버튼 + 플랜 피커 + 배선

## 목적

아웃게임 메인 메뉴에 "테스트 모드" 버튼을 추가하고, 누르면 `TestModeConfig.planCatalog` 의 작성 플랜을 나열하는 피커 패널을 연다. 플랜 선택 시 `TestModeContext.Set(plan, preset)` 후 BattleScene 로드 → unit 3 분기가 작성 웨이브로 진입. 실제 Play 로 endless 동작을 검증한다.

## 변경 대상

- 신규: `Assets/_Project/Scripts/UI/Outgame/TestModePanelView.cs` — 피커(자체 UI 빌드).
- `Assets/_Project/Scripts/UI/Outgame/OutgameMenuController.cs` — `testModePanel` 필드 + `OnOpenTestMode` + ClosePanels 포함.
- `Assets/_Project/Scenes/OutgameScene.unity` — TestModeButton(메뉴) + TestModePanel(MenuCanvas 자식, 비활성) + 참조 배선.

## 구현

### TestModePanelView (자체 UI 빌드)
- SerializeField: `TestModeConfig config`, `TMP_FontAsset font`, `OutgameMenuController menu`.
- `OnEnable` → 1회 빌드: 배경 + 타이틀 + planCatalog 버튼 목록(라벨 `{displayName} ({n} waves)`) + 닫기.
- 플랜 클릭 → `TestModeContext.Set(plan, config.defenderPreset)` + `SceneManager.LoadScene(SceneNames.Battle)`.
- planCatalog 비면 안내 텍스트.

### OutgameMenuController
- `[SerializeField] private GameObject testModePanel;` + `OnOpenTestMode() => RaiseExclusive(testModePanel);` + ClosePanels 에 testModePanel 추가.

### 씬 배선
- TestModeButton: DreamcatcherButton 복제, 라벨 "TEST MODE", y=-180, onClick → `OnOpenTestMode`(persistent listener, UnityEventTools).
- TestModePanel: MenuCanvas 자식, full-rect, TestModePanelView 부착, 비활성 시작. config=TestModeConfig.asset, font, menu=OutgameMenu 배선.
- OutgameMenu.testModePanel → TestModePanel.

## 완료 기준

- 컴파일 0, 씬 저장.
- Play: 메뉴 "TEST MODE" → 피커에 "Sample Test Plan (8 waves)" 표시 → 선택 → BattleScene 진입.
- 드래프트 없이 **기존 저장 스쿼드**로 배치 단계 진입(스쿼드 없으면 프리셋 폴백), 작성 8웨이브가 트리거 시각대로 스폰.
- endless: 타이머 만료 승리 없음. 전 웨이브 dispatch + 전멸 시에만 승리(또는 goal-reached 패배).
- 기존 메뉴(Start/Squad/Dreamcatcher) 무변경.

---

*완료 확인*: 2026-06-16 — 컴파일 0, OutgameScene 저장. Play 전체 체인 검증: TEST MODE 버튼 → 영어 피커("Sample Test Plan (8 waves)") → 선택 → BattleScene 진입(Context 소비, phase=Placement, 드래프트 스킵), 디펜더=**기존 저장 스쿼드 7유닛**(요청 반영), StartBattle 시 `_usingAuthoredPlan=True`/`_timerDuration=0`(endless)/waves=8, wave0 스폰 확인. UI 텍스트 전부 영어. 커밋 `__PENDING__`.
