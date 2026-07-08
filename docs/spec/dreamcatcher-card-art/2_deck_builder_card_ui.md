# 2 · UI — 카드 그리드 리디자인

## 목적

`DreamcatcherDeckBuilderView` 를 플랫 버튼에서 **아트 카드(이미지+효과텍스트 Column)** 그리드로 바꾼다. 보유=5열 세로 스크롤, 덱=5열 카드.

## 변경 대상

- `Assets/_Project/Scripts/UI/Outgame/DreamcatcherDeckBuilderView.cs`

## 구현

### 카드 아이템 (공통 헬퍼)
`CreateCardView(parent, card, onClick)`:
- 루트: `RectTransform + Image(프레임/배경) + Button`, `VerticalLayoutGroup`(spacing, padding, childForceExpandHeight=false).
- 자식1 **Art**: `Image`(sprite=`card.art`, `preserveAspect=true`) + `LayoutElement`(고정 height ≈ 셀폭×1.4). art null 이면 카테고리 색(Normal/Unique) 폴백.
- 자식2 **Effect**: `TextMeshProUGUI` + `LayoutElement`(고정 height ≈ 64). 텍스트 = axis 헤더 + 버프 라인.
- Unique 카드는 프레임색/테두리로 구분.

### 효과 텍스트
`Summary(card)`: axis(RANGER/GUARDIAN/COST-1/ALL) + 각 effect `KIND ±P%`(ATK/AS/HP/MOVE/COST). `DreamcatcherSelectionView.Summary` 규약과 동일 표기.

### 보유 = 5열 세로 스크롤
`EnsureScrollGrid(container, columns=5, cell)` 헬퍼:
- container 의 기존 `GridLayoutGroup` 제거(있으면).
- `ScrollRect`(horizontal=false, vertical=true) + Viewport(`RectMask2D`+투명 Image, stretch) + Content(`GridLayoutGroup` 5열 + `ContentSizeFitter` vertical=PreferredSize) 구성.
- 카드는 Content 에 parent. 반환값 = Content RectTransform.
- container 앵커/사이즈는 코드로 세팅(패널 하단 영역 stretch).

### 덱 = 5열 카드
덱 10슬롯도 동일 `CreateCardView`. 10개면 5×2. `deckContainer` GridLayoutGroup 5열로 코드 세팅(스크롤 불필요, 고정 2행). 슬롯 탭 = 제거.

### 레이아웃 코드 주도
`OnEnable`/build 시 `deckContainer`·`ownedContainer` 의 anchor/sizeDelta/position 을 코드로 배치(덱=상단 스트립, 보유=그 아래 스크롤). 씬 rect 는 무시하고 코드가 source. 셀 크기: 보유 ≈ 200×300, 덱 ≈ 150×230 (조정 가능).

### 기존 로직 보존
`_working`/`AddCard`/`RemoveAt`/`OnSave`/`Refresh`/`DeckRules` 검증·상태텍스트·세이브 버튼 흐름 불변. 카드 **생성 방식만** 교체.

## 완료 기준

- [x] 컴파일 통과.
- [x] 보유 컬렉션이 5열 카드 그리드로 표시, 10종 → 2행 세로 스크롤 동작.
- [x] 각 카드가 아트 이미지(위) + 효과 텍스트(아래) Column.
- [x] 덱 슬롯이 동일 카드 스타일(상단 10칸 단일 트레이), 추가/제거/저장 회귀 없음.
- [x] art 없는 카드는 색상 폴백으로 깨지지 않음.

Play 검증 2026-07-08: OutgameScene Play → 드림캐쳐 패널. 덱 10칸 트레이 + 보유 5열 스크롤, 아트/효과텍스트/Unique 금프레임 렌더, `10/10 · unique 2/2 · ok`. 덱 트레이 좌측 -46px 오프셋으로 우상단 메뉴 버튼 겹침 해소.
