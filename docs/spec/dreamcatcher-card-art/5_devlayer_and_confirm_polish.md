# 5 · 개발버튼 로비 레이어 + 확정영역 스타일

## 목적

(1) 개발용 버튼(TestMode/RefreshStats/ResetAccount)이 패널 위로 그려지지 않게 로비 레이어 전용으로. (2) 드림캐쳐 확정영역(덱 트레이)을 프레임 카드 UI 로 스타일 개선.

## 변경 대상

- `Assets/_Project/Scripts/UI/Outgame/OutgameMenuController.cs`
- `Assets/_Project/Scripts/UI/Outgame/DreamcatcherDeckBuilderView.cs`
- `Assets/_Project/Scenes/OutgameScene.unity` (재부모화 + CanvasGroup + 배선)

## 구현

### 개발버튼 로비 레이어 전용
- `MenuButtons` 밑 `TestModeButton` 을 기존 `DevButtons` 그룹(`DevOnlyGroup`)으로 재부모화 → 3개 개발버튼이 한 그룹.
- `DevButtons` 에 `CanvasGroup` 추가. `OutgameMenuController.devButtonsGroup` 로 참조.
- `RaiseExclusive`(패널 오픈) 시 `SetDevButtonsVisible(false)`, `ClosePanels` 시 `true`. alpha+interactable+blocksRaycasts 토글.
- **GameObject.active 대신 CanvasGroup** 을 쓰는 이유: `DevOnlyGroup.Awake` 가 비-dev 빌드에서 GO 를 SetActive(false) 하므로, active 토글 시 릴리스에서 재활성 충돌. CanvasGroup 은 GO 가 살아있는 dev 환경에서만 의미 → 게이트와 직교.

### 확정영역 스타일
- `DreamcatcherDeckBuilderView.BuildDeckFrame`: 덱 트레이 뒤에 프레임(네이비 Image) + 상단 골드 액센트 바 + "MY DECK" 골드 헤더. 덱 트레이 sibling index 앞에 삽입해 카드가 위로.
- `BuildSectionLabel`: 보유 스크롤 위 "COLLECTION" 뮤트 캡션.
- 덱 트레이는 프레임 헤더 아래로 재배치(`DeckFrameTopY`/`FrameHeaderH` 상수).

### 레이아웃 일관화 (동일 width·중앙정렬)
- 상단 덱 프레임과 하단 컬렉션이 **하나의 공유 콘텐츠 폭**(`contentWidth` = 5열 컬렉션 그리드 폭)에 정렬. 둘 다 x=0 중앙정렬.
- 개발버튼이 이제 패널에서 숨겨지므로 덱의 좌측 오프셋(-46) 제거 → 중앙정렬.
- 덱 10슬롯 셀 폭(`_deckCell`)은 `contentWidth - 2*FramePadX` 안에 맞도록 **런타임 계산**(고정 상수 제거). 프레임 외곽 = 컬렉션 카드블록 폭.

### 이미지 매핑 버그 수정 (unit 1 결함)
- `Card_GuardianAs8.art` 가 이미지가 아니라 카드 자신의 meta GUID 를 가리켜 빈 슬롯이었음 → `dreamcatcher_card_10`(img10) 로 교정. 10종 전부 서로 다른 이미지 참조 확인.

## 완료 기준

- [x] 컴파일 통과.
- [x] 패널 오픈 시 개발버튼(TEST MODE/REFRESH STATS/RESET ACCOUNT) 미표시, 네비(Squad/Dreamcatcher/StartGame) 유지.
- [x] 확정영역이 "MY DECK" 프레임(골드 액센트+헤더)로 구분, "COLLECTION" 라벨.
- [x] 씬 저장(재부모화/CanvasGroup/`devButtonsGroup` 배선 영속).

Play 검증 2026-07-08: OutgameScene Play → 드림캐쳐 패널 스크린샷. 우상단 개발버튼 사라짐, MY DECK 프레임/COLLECTION 라벨 렌더, `10/10·unique 2/2·ok`. DevOnlyGroup 빌드 게이트 불변.
