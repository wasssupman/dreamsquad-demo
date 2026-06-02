# 4 — 선택 UI (3카드 모달)

## 목적

3장 노출 → 1장 탭 선택하는 인게임 모달. 런타임 빌드(씬 wiring 최소).

## 변경 대상

- 신규 `Assets/_Project/Scripts/UI/Dreamcatcher/DreamcatcherSelectionView.cs`
- 수정 `Assets/_Project/Scripts/Core/Dreamcatcher/DreamcatcherController.cs` — UI 연동
- 수정 `Assets/_Project/Scenes/BattleScene.unity` — DreamcatcherController + View GameObject 배치 (UnityMCP)

## 구현

`DreamcatcherSelectionView` (MonoBehaviour):
- `BuildCanvas()`(PlacementPhaseView 패턴): ScreenSpaceOverlay Canvas(sortingOrder 50) + 어두운 배경 + 가로 3 카드 슬롯 + 제목 "DREAMCATCHER".
- `Show(IReadOnlyList<DreamcatcherCard> three, Action<DreamcatcherCard> onPick)`: 카드별 버튼에 displayName + 효과 요약(예: "Ranger ATK +10%") 라벨, onClick → onPick(card) 후 Hide.
- `Hide()`: 패널 비활성.
- 라벨 영문(한글 폰트 후속). 효과 요약은 axis+effects 로 생성.

`DreamcatcherController`:
- `[SerializeField] DreamcatcherSelectionView view;`
- 트리거 → `view.Show(Draw3(), Pick)`; view 없으면 폴백 자동선택(Unit 3).
- Show 시 `Time.timeScale=0`, Pick/닫기 후 `1`.

씬: BattleScene 에 `Dreamcatcher` GameObject(또는 기존 UI root 하위) — DreamcatcherController + DreamcatcherSelectionView, bridge/deck/view 참조 wiring. deck = `DreamcatcherDeck_Default.asset`.

## 완료 기준

- BattleScene Play(스쿼드 모드): 첫 배치 → 3카드 모달, 1장 선택 → 효과 적용 + 모달 닫힘 + 게임 재개.
- 5웨이브 도달 → 모달 재등장, 선택 누적(스택).
- 선택 중 적군 진행 정지(timeScale 0) 확인.
- read_console clean (기존 DraftView 누락 제외).
- 컨트롤러/뷰 미배치 시 무영향(비파괴).

> 완료 확인 2026-06-02 — Play(MCP): 첫 배치 → 모달 패널 활성 + 카드 3장 + timeScale=0(일시정지), 카드 탭 → registry 0→1(효과 적용) + 패널 숨김 + timeScale=1(재개). 에러 0(기존 DraftView 제외).
> 메모: BattleScene 에 `Dreamcatcher`(DreamcatcherController) + 자식 `DreamcatcherSelectionView` 배치, deck=DreamcatcherDeck_Default. 유닛 배치하는 PlayMode 테스트는 씬 컨트롤러를 Destroy + TearDown 에서 timeScale=1 리셋(오염 방지). PlayMode 6/6.
