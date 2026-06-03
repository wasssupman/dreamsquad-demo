# 2 — 덱 빌더 UI

## 목적

DreamcatcherPanel placeholder 를 10장 덱 빌더로 채운다. 보유 카드 → 덱에 추가/제거 → 규칙 충족 시 저장.

## 변경 대상

- 신규 `Assets/_Project/Scripts/UI/Outgame/DreamcatcherDeckBuilderView.cs`
- 수정 `Assets/_Project/Scenes/OutgameScene.unity` — DreamcatcherPanel 내용 (UnityMCP)

## 구현

`DreamcatcherDeckBuilderView` (MonoBehaviour, 씬 로컬, SquadBuilderView 패턴):
- 참조: `DreamcatcherCardCatalog catalog`, `PlayerProfileSO profileSO`.
- 작업 덱: 메모리 `List<string> _working`(편집 중인 cardIds). OnEnable 시 선택 덱 복사(없으면 빈 리스트).
- 상단: **현재 덱** (10 슬롯 가로/그리드, 카드명+카테고리색; 탭하면 제거). 카운트 "N/10".
- 하단: **보유 카드 그리드**(catalog.AllIds → ById). 카드 탭 → `_working` 에 추가(10 미만일 때; 고유는 2 초과 시 거부). 일반 중복 허용.
- 규칙 라벨: `DeckRules.Validate(_working, catalog)` 결과 표시(영문 사유). 
- **SAVE** 버튼: 유효할 때만 활성. 저장 시 `deck_1`(없으면 생성) upsert: `cardIds=_working`, `selectedDeckId="deck_1"`, `ProfileStore.Save`.
- 카드 카테고리 색: Normal=파랑, Unique=주황(테두리/배경 톤).
- 라벨 영문, 가로(1920x1080) 캔버스(부모 MenuCanvas 따름), anchor 기반.

UnityMCP 로 DreamcatcherPanel 하위에 덱 슬롯행 + 보유 그리드 + 카운트/규칙 라벨 + SAVE 버튼 생성, `DreamcatcherDeckBuilderView` 부착·참조 wiring.

## 완료 기준

- DreamcatcherPanel 열기 → 보유 카드 6 + 덱 슬롯 표시, 콘솔 에러 0.
- 카드 탭 → 덱 추가(10 상한·고유 2 상한 enforce), 슬롯 탭 → 제거. 카운트/규칙 라벨 갱신.
- 10장 유효 구성 → SAVE 활성 → 저장 후 Play 재시작 시 유지(profile.json 반영, selectedDeckId=deck_1).
- 9장/고유3 등 무효 시 SAVE 비활성 + 사유 표시.
