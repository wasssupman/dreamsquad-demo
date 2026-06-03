# dreamcatcher-deck-builder (D)

> 상태: 초안 (작성 2026-06-03)
> 선행: A `outgame-scene-and-flow`, C `ingame-dreamcatcher` (완료).

## 검증 질문

OutgameScene 드림캐쳐 패널에서 **10장 세이브덱을 구성·저장**(정확히 10장, 고유≤2)하고 선택하면, 인게임 드림캐쳐 3중1이 **그 저장 덱**에서 뽑히는가? 저장 덱이 없으면 기존 고정 덱으로 폴백하는가?

## 상위 목표

C가 고정 기본 덱을 쓰던 것을, 플레이어가 OutgameScene에서 짠 세이브덱으로 대체한다. 스코프는 **10장 빌더 + 저장 + 인게임 반입 MVP**. 보유/언락/가챠/무의식은 후속.

## 작업 단위

| 파일 | 작업 | 문서 | 목적 |
|---|---|---|---|
| 0 | 데이터 | `0_card_category_and_catalog.md` | `DreamcatcherCard.category`(Normal/Unique)+6 백필, `DreamcatcherCardCatalog`, `DeckSave` 확장, `SelectedDeck()` |
| 1 | 규칙 | `1_deck_rules.md` | 순수 `DeckRules.Validate`(정확히 10·고유≤2) + EditMode 테스트 |
| 2 | UI | `2_deck_builder_ui.md` | DreamcatcherPanel: 보유 카드 + 10슬롯 추가/제거 + 규칙 피드백 + 저장 |
| 3 | 통합 | `3_ingame_deck_carry_in.md` | DreamcatcherController가 선택 저장덱(catalog 해석) 사용, 없으면 고정 덱 폴백 |
| 4 | 인계 | `4_handoff_summary.md` | (종료 시) |

## Feature-wide 계약

- **덱 규칙**: 정확히 **10장**, **고유(Unique) ≤ 2**, 일반(Normal) 중복 허용. `DeckRules.Validate(cardIds, catalog)`가 단일 source.
- **카드 카테고리**: `DreamcatcherCard.category` (Normal/Unique). 6종 = Normal 5 + Unique 1(fortress). axis/effects 와 독립.
- **카드 풀**: 6종 전부 사용 가능(보유 시스템 없음, MVP). `DreamcatcherCardCatalog`(id→card).
- **저장**: `PlayerProfile.dreamcatcherDecks`(List<DeckSave>) + `selectedDeckId`. `DeckSave { id, name, List<string> cardIds }`.
- **신규 프로필 = 덱 0개**(스쿼드와 달리 기본 덱 미생성). `selectedDeckId` null. 빌더에서 첫 덱 생성.
- **인게임 폴백(비파괴)**: 선택 저장덱이 있고 유효하면 그 덱으로 3중1; 없으면 `DreamcatcherController`의 serialized 고정 덱(`DreamcatcherDeck_Default`). C 동작 유지.
- **선택 덱 → 인게임**: `DreamcatcherController`가 `PlayerProfileSO` + `DreamcatcherCardCatalog` 참조로 런타임 덱 빌드(스쿼드의 catalog 해석 패턴과 동일).
- **라벨 영문**(한글 폰트 후속).

## 후속 후보 (D 범위 밖)

- 카드 보유/언락(ownedCardIds) + 가챠/꿈런 파밍
- 카드 콘텐츠 확장(기획 일반10+고유3+무의식2, 신규 메커닉 채널)
- 다중 덱 수집/전환 UI, 덱 이름 편집, 무의식 편입
- 카드 효과 아이콘/비주얼
