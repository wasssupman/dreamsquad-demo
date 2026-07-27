# 0 — 카드 상세 뷰 (DreamcatcherCardDetailView)

## 목적

좌 1/3 상세 — 카드 art 백드롭 + 통합 카드(이름/카테고리 배지/`DreamcatcherCardText.Body` 설명/[덱에 추가·제거] + 추가 불가 hint). 모달 대체(기존은 팝업). 정적 Sprite라 Spine 없음.

## 변경 대상

- 신규 `Assets/_Project/Scripts/UI/Outgame/DreamcatcherCardDetailView.cs` (`Wassup.UI`)
- 신규 `Assets/_Project/Scripts/UI/Outgame/CardCategoryStyle.cs` — Frame/ArtFallback/Label (기존 FrameColorOf/ArtFallbackOf 재현, unit 1과 공용)

## 구현

- `ShowCard(card, deckSlotMode, canAdd, hint)`: art(있으면 sprite/없으면 카테고리 폴백색) + 이름 + 카테고리 배지(색+라벨) + 효과(`Body`). 버튼: deckSlot이면 "덱에서 제거"(빨강, 활성), 아니면 "덱에 추가"(canAdd면 초록/아니면 회색 비활성 + hint).
- `event ActionClicked` — orchestrator가 add/remove 해석. `Clear()` 선택 없을 때.
- SerializeField `artImage`(백드롭) / `cardRoot`(절차적 카드) / `font`. 빌더가 주입.

## 완료 기준

- [x] 컴파일 클린. `Show`가 art/이름/카테고리/효과/버튼/hint 예외 없이 채움.
- [x] Play 실화면: 타로 art + 이름 + "스쿼드 버프" 배지 + Body 효과 + hint + 버튼 렌더 확인(2026-07-18).

> 구현 2026-07-18 · 커밋 `30d882cf`.
> rev 2026-07-27 · 커밋 `50bdbd2f` — 설명 본문 폰트 26 고정 → **26~32 오토사이즈** + `LayoutElement.flexibleHeight=1`(카드 영역 잉여 높이 흡수, 박스 150→198). 카탈로그 38장 실측: before 전부 26·넘침 2장 → after 전부 32·넘침 0. Play 실화면 확인은 미완.
