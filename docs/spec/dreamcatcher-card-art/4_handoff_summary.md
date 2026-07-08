# 4 · Handoff — dreamcatcher-card-art

## Commit

`63d44050` feat(dreamcatcher): 카드 아트 그리드 + 상세 팝업 + 10종 확장 (branch `feature/dreamcatcher-card-art`, 47 files)

## Implemented

- `DreamcatcherCard` SO 에 `Sprite art` 필드(effects 뒤 append, nullable).
- 카드 풀 6 → **10종**: 신규 `Card_AllAtk8`/`AllMove10`/`RangerHp12`/`GuardianAs8`(Normal, 기존 효과 채널). 카탈로그 등록.
- 루트 테스트 PNG 01~10 → `Art/DreamcatcherCards/dreamcatcher_card_01~10.png` Sprite 승격(textureType 8, mipmap off, max 2048). 카탈로그 순서대로 10 카드에 `art` 배정.
- `DreamcatcherDeckBuilderView` 전면 리디자인: 플랫 버튼 → **아트 카드**(이미지+효과텍스트 세로 Column). 보유=5열 세로 스크롤(ScrollRect+RectMask2D+GridLayout+ContentSizeFitter), 덱=상단 10칸 단일 트레이. 레이아웃 코드 주도(씬 YAML 무편집).
- Unique(fortress) 금색 프레임, art null 시 카테고리 색 폴백. 효과표기 = `DreamcatcherSelectionView.Summary` 규약(axis + KIND ±%). CostRate → "COST".
- (unit 5) 개발버튼(TestMode/Refresh/Reset)을 `DevButtons` 그룹으로 묶고 CanvasGroup 토글 → 패널 오픈 시 숨김(로비 레이어 전용). 확정영역을 "MY DECK" 프레임(네이비+골드 액센트+헤더)로, 보유 위 "COLLECTION" 라벨. 덱 프레임과 컬렉션을 동일 콘텐츠 폭·중앙정렬로 정렬(덱 셀폭 런타임 계산).
- (fix) `Card_GuardianAs8.art` 가 카드 자신의 GUID 를 가리키던 배정 버그를 img10 으로 교정(빈 슬롯 해소).
- (unit 6) 카드 아이템을 **이미지 전용**으로, 탭 시 **모달 팝업**(큰 아트 + 스타일 효과 텍스트 + ADD/REMOVE 액션 + X + 바깥클릭 닫힘). 안쪽 클릭 버블링 차단(패널 no-op Button), 버튼 `transition=None`. 컬렉션에서 **덱 장착 카드는 뒤로 정렬 + 딤 + "IN DECK" 뱃지**(`RebuildOwnedCards`, 매 Refresh 동기화).

## Key Files

- `Assets/_Project/Scripts/Data/Dreamcatcher/DreamcatcherCard.cs` (art 필드)
- `Assets/_Project/Data/Dreamcatcher/Card_*.asset` (10종 + 카탈로그)
- `Assets/_Project/Art/DreamcatcherCards/dreamcatcher_card_01~10.png(.meta)`
- `Assets/_Project/Scripts/UI/Outgame/DreamcatcherDeckBuilderView.cs` (뷰 + 확정영역 프레임)
- `Assets/_Project/Scripts/UI/Outgame/OutgameMenuController.cs` (devButtonsGroup 토글)
- `Assets/_Project/Scenes/OutgameScene.unity` (TestModeButton→DevButtons 재부모화, CanvasGroup, devButtonsGroup 배선)

## Verified

- 컴파일 CS 에러 0 (Unity 6000.4.7f1 refresh+compile). 잔여 콘솔은 무관한 Burst JIT 캐시 경고.
- OutgameScene Play(MCP) → `OutgameMenuController.OnOpenDreamcatcher()` 로 패널 오픈 → 전체화면 스크린샷 검증. 덱 10칸 + 보유 5열 스크롤, 아트/효과텍스트/Unique 금프레임/`10/10·unique 2/2·ok` 정상.
- 검증용 one-off MenuItem 스크립트 + 스크린샷은 삭제 후 재컴파일.

## Notes

- **레이아웃 코드 주도**: 뷰가 `deckContainer`/`ownedContainer` 의 anchor/size/scroll 을 런타임 세팅. 씬 rect 는 앵커로만 사용 — 씬에서 컨테이너 위치 바꿔도 코드가 덮어씀. 튜닝은 뷰 상수(`OwnedCell`/`DeckCell`/anchoredPosition)에서.
- 덱 트레이 좌측 -46px 오프셋 = 우상단 메뉴 버튼(Test/Refresh/Reset) 겹침 회피. 이 값 되돌리면 10번 슬롯이 버튼 뒤로 감춰짐.
- 이미지↔카드 매핑은 **카탈로그 순서 자동 배정**(임시). 각 카드 SO `art` 필드에서 자유 재조정 가능(사용자 계약).
- 이미지 11~20 은 미사용(향후 드림캐쳐용). 신규 드림캐쳐 = SO 생성 → art 배정 → 카탈로그 추가로 자동 렌더.
- **개발버튼 숨김은 CanvasGroup 토글**(GameObject.active 아님). `DevOnlyGroup` 이 비-dev 빌드에서 GO 를 끄므로 active 토글로 되돌리면 릴리스에서 개발버튼이 되살아난다 — 절대 active 토글로 바꾸지 말 것.

## Follow-up

- 인게임 3중1 모달(`DreamcatcherSelectionView`)도 아트 카드화(현재 텍스트).
- 아트 압축/아틀라스(모바일 메모리) — 1024×1536 10장.
- Android 실기 렌더/스크롤 터치 확인.
- 카드 콘텐츠 확장(신규 메커닉/무의식) — 별도 spec.
