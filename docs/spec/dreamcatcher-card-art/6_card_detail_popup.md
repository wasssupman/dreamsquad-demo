# 6 · 카드 상세 팝업

## 목적

카드 아이템을 이미지 전용으로 바꾸고, 카드 탭 시 효과를 **모달 팝업**으로 스타일하게 표기한다. 팝업에서 덱 추가/제거를 수행한다.

## 변경 대상

- `Assets/_Project/Scripts/UI/Outgame/DreamcatcherDeckBuilderView.cs`

## 구현

### 카드 아이템 = 이미지 전용
- `CreateCardView` 에서 하단 효과 텍스트 밴드 제거 → 프레임 안에 아트만. Unique 금색 프레임 유지. 아트 `raycastTarget=false` 로 루트 버튼이 탭 수신.
- 탭 콜백: 보유 카드 → `ShowCardPopup(card, false, -1)`, 덱 슬롯 → `ShowCardPopup(card, true, index)`.

### 컬렉션 장착 카드 정렬/딤
- `RebuildOwnedCards()`(매 `Refresh` 호출): 덱에 있는(`_working.Contains(id)`) 카드는 **뒤로 정렬**(available → equipped, 각 그룹 내 카탈로그 순서 유지) + **딤 오버레이 + "IN DECK" 골드 뱃지**.
- 보유 카드 populate 는 `BuildLayoutOnce` 가 아니라 `Refresh` 가 담당(덱 변경 시 재정렬/재딤 동기화). 추가하면 뒤+딤, 제거하면 앞+복원.
- `CreateCardView(..., bool dimmed)` 로 딤 오버레이 제어.

### 모달 팝업 (`EnsurePopup`/`ShowCardPopup`/`HidePopup`)
- 뷰(=DreamcatcherPanel) 자식으로 1회 생성, 매 탭 재populate. `SetAsLastSibling` 로 최상단.
- 레이아웃: 풀스크린 dim 오버레이 + 골드 테두리 + 네이비 패널 + 골드 상단 액센트. 큰 아트(380×540) + 제목(displayName) + 효과 본문(axis·category 골드/뮤트, 버프 라인 색상) + 액션 버튼 + X 닫기.
- **액션 버튼**: 보유 → "ADD TO DECK"(덱 full/unique 초과 시 비활성+힌트), 덱 → "REMOVE FROM DECK". 실행 후 `AddCard`/`RemoveAt` + `HidePopup`.
- **닫기**: 오버레이 Button.onClick=HidePopup(바깥 클릭) + X 버튼. 패널에 no-op Button 을 둬 **안쪽 클릭이 오버레이로 버블링돼 닫히는 것 방지**(uGUI 는 클릭을 최근접 조상 핸들러로 라우팅).
- 버튼은 `transition=None` 으로 `image.color` 가 ColorTint 에 덮이지 않게.

## 완료 기준

- [x] 컴파일 통과.
- [x] 카드 = 이미지 전용, 탭 시 팝업.
- [x] 팝업에 아트+효과 텍스트 스타일 표기, 액션 버튼(상태 반영: Deck full 10/10 시 비활성)+X.
- [x] 바깥 클릭 닫힘 / 안쪽 클릭 유지 (시뮬레이트 클릭 검증: inside→handledBy Panel 유지, outside→handledBy CardPopup 닫힘).

Play 검증 2026-07-08: 보유 카드 탭 → 팝업(Guardian AS+8% img10, GUARDIAN·NORMAL, Attack Speed +8%, Deck full 힌트+비활성 ADD, X). 클릭 라우팅 로그로 안/밖 동작 확인.
