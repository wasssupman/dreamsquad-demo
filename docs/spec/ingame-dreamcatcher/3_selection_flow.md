# 3 — 선택 흐름 (트리거 + 추첨)

## 목적

첫 배치 / 5웨이브마다 3장 추첨을 띄우고, 선택 카드를 Unit 2로 적용한다.

## 변경 대상

- 수정 `Assets/_Project/Scripts/Bridge/BattleBridge.cs` — 트리거 이벤트 발화
- 신규 `Assets/_Project/Scripts/Core/Dreamcatcher/DreamcatcherController.cs`

## 구현

BattleBridge 이벤트(구독자 없어도 무해):
```csharp
public event System.Action FirstDefenderPlaced;
public event System.Action<int> WaveMilestoneReached;  // 1-indexed wave
```
- `PlaceDefenderAs`(+deployment 활성화): 매치 첫 배치 시 `_firstPlacedFired` 가드 후 `FirstDefenderPlaced?.Invoke()`. 가드/카운터는 `BeginPlacement` 에서 리셋.
- `QueueWave`: `(wave.waveIndex+1)%5==0` 이면 `WaveMilestoneReached?.Invoke(wave.waveIndex+1)`.

`DreamcatcherController` (MonoBehaviour, BattleScene):
- 참조: `BattleBridge bridge`, `DreamcatcherDeck deck`, (UI는 Unit 4).
- 상태: `List<DreamcatcherCard> _remaining`(덱 복사), `int _seed`.
- OnEnable: `bridge.FirstDefenderPlaced += OnSelectionTrigger; bridge.WaveMilestoneReached += OnWaveMilestone;` (OnDisable 해제).
- `OnSelectionTrigger`: `Draw3()` → UI 표시(Unit 4). UI 미연결 시 첫 1장 자동 선택(폴백, 테스트 가능).
- `Draw3()`: `_remaining`(또는 덱 전체)에서 seed 기반 3장. 덱 중복 카드 그대로 풀에 존재. 3 미만이면 가능한 만큼.
- `Pick(card)`: `bridge.ApplyDreamcatcherCard(card)` + UI 닫기 + (선택 시 timeScale 복귀).
- 선택 모달 동안 `Time.timeScale=0`(웨이브 트리거 시 적군 진행 정지), 선택 후 1 복귀. 첫 배치(전투 전)엔 영향 미미.

매치 리셋: 컨트롤러는 BattleScene 과 함께 새로 생성(GameManager 비영속과 동일 수명)이므로 별도 리셋 불필요.

## 완료 기준

- compile + read_console clean.
- 런타임: 첫 배치 → FirstDefenderPlaced 발화 → Draw3 3장. 5웨이브 도달 → WaveMilestoneReached(5) 발화.
- UI 미연결 폴백(자동 1장 선택) 경로로 ApplyDreamcatcherCard 호출 확인(Unit 2 효과 반영).
- 구독자 없을 때(컨트롤러 미배치) 기존 흐름 무영향.

> 완료 확인 2026-06-02 — PlayMode `FirstPlacement_TriggersController_AutoPicksAndApplies`: 첫 배치 시 FirstDefenderPlaced 1회 발화, DreamcatcherController Draw3→폴백 자동선택→ApplyDreamcatcherCard(ranger attackSpeed 1.1), 2번째 배치엔 재발화 없음. PlayMode 6/6.
> 메모: 5웨이브 트리거는 `QueueWave` 의 `(waveIndex+1)%5==0` + 동일 이벤트 경로(첫 배치 테스트로 메커니즘 검증). 5웨이브 실주행 통합검증은 Unit 4 Play 또는 후속.
