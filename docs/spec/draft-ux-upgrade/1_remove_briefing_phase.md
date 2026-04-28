# 1. Briefing 페이즈 제거

## 목적

`GamePhase.Briefing` 을 enum 에서 제거하고, GameManager 의 시작 분기를 단순화하여 곧장 `Draft` 페이즈로 진입한다. **`TimelineBriefingView.cs` 는 이 task 에서 삭제하지 않는다** — task 3 가 그 안의 MAP SETTINGS UI 빌드 코드를 새 컴포넌트로 옮길 때까지 보존한다. 본 task 는 게임 흐름과 enum 만 정리.

## 변경 대상

- `Assets/_Project/Scripts/Core/GameManager.cs`
- (참조 정리만) `Assets/_Project/Scripts/UI/TimelineBriefingView.cs` — **파일 삭제하지 않음**. 미사용 상태로만 둔다.

## 구현

1. `GamePhase` enum 에서 `Briefing` 항목 제거. 결과: `GamePhase: None, Draft, Placement, Battle, Result`.
2. GameManager 의 `[SerializeField] private TimelineBriefingView timelineBriefing;` 필드 제거.
3. Start (또는 라운드 시작 진입점) 의 briefing 분기:
   ```
   if (timelineBriefing != null && draftController != null) { SetPhase(Briefing); timelineBriefing.Show(); ... }
   else { draftController.BeginDraft(); }
   ```
   를 다음으로 단순화:
   ```
   SetPhase(GamePhase.Draft);
   draftController.BeginDraft();
   ```
4. `OnBriefingConfirmed` 콜백 메서드 제거.
5. 씬에 남아있는 TimelineBriefing GameObject 는 task 3 가 MAP SETTINGS 만 추출한 뒤 task 3 완료 기준으로 삭제한다. 본 task 에서는 GameObject 자체는 살아있되 `SetActive(false)` 로만 둬서 게임 흐름이 직진하도록 한다 (또는 GameManager 가 더 이상 Show 를 호출하지 않으므로 자연스럽게 비활성).
6. **map 옵션 임시값**: TimelineBriefingView.Show 가 사라졌으므로 사용자가 라운드 시작 전 맵 옵션을 조정할 UI 가 일시적으로 사라진다. task 3 가 좌상단 토글로 복원할 때까지 `DraftController.SelectedMapGenerationOptions` 는 `MapGenerationOptions.Default` 로 시작. 이 윈도우는 task 1 → task 3 사이에만 존재.
7. `BattleBridge.OnRedraftRequested` 의 `BeginDraft()` 호출은 그대로 둔다 (이미 briefing 을 거치지 않는다).

## 완료 기준

- `grep -r "GamePhase.Briefing\|timelineBriefing\b" Assets/_Project/Scripts` 가 enum 정의 외엔 0건.
- 게임 시작 → 곧장 (현재 비주얼의) DraftView 가 뜨고 Placement → Battle 로 진행 가능.
- `TimelineBriefingView.cs` 파일 자체는 보존 (task 3 가 추출 후 삭제).
- 씬에 Missing Script 경고 없음. (TimelineBriefing GameObject 가 남아있어도 컴포넌트는 살아있다.)
- Console 에 컴파일 에러 / NullReferenceException 없음.
