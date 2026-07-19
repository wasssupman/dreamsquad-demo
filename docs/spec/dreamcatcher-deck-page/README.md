# dreamcatcher-deck-page — 드림캐쳐 덱을 캐릭터 페이지 레이아웃으로

> 상태: **완료 2026-07-18 · rev 2026-07-19 unit 6 완료** (units 0~6 구현·커밋·Play 확인. handoff `5_handoff_summary.md`. unit 6 = 덱-먼저 정렬 + 슬롯 선택 표시 — b86545ea)
> 선행: `squad-character-page`(레이아웃/패턴 원본, 완료) · `dreamcatcher-deck-builder`(DeckSave/DeckRules/카탈로그) · `dreamcatcher-card-art`(카드 art)
> 성격: 아웃게임 UI/UX 재설계 (MonoBehaviour 프레젠테이션). ECS 무관 — 플레이 오브젝트 아님, 파이프라인 커버리지 N/A.

## 검증 질문

OutgameScene 드림캐쳐 패널에서, **선택 카드의 art + 설명이 좌측 한 패널에 강조**되고, 우측 카드 그리드에서 **탭해 손쉽게 다른 카드로 바꾸며**, 상단 **10슬롯 덱을 규칙(정확히 10·유니크(중복 금지)) 지켜 편집·저장**한 뒤 게임을 시작하면 그 덱이 반입되는가? (모달 없이)

## 상위 목표

옛 `DreamcatcherDeckBuilderView`(슬롯+보유그리드+저장)를 **squad-character-page와 동일 레이아웃**(상세 1/3 + 덱 스트립 + 그리드 2/3)으로 재설계. 스쿼드보다 단순 — 이미지가 정적 art Sprite(라이브 Spine 아님), 스톤 모드 없음, 설명 포맷터(`DreamcatcherCardText.Body`) 이미 존재.

## 레이아웃

```
┌─────────┬─────────────────────────────┐
│  카드    │ ‹덱›▣▣▣▣▣▣▣▢▢▢  8/10 · ok  [저장]│ ← 10슬롯 + 유효성 + Save(게이트)
│  art    ├─────────────────────────────┤
│  백드롭  │  카드 그리드 (art + 편성중) │
├────────┤  ▣ ▣ ▣ ▣ ▣ ▣              │
│이름·카테고리                            │
│효과 설명(Body)                          │
│[덱에 추가]  (불가 시 사유 hint)          │
└────────┴─────────────────────────────┘
```

## 작업 단위

| 파일 | 작업 | 문서 | 목적 |
|---|---|---|---|
| 0 | UI | `0_card_detail_view.md` | 카드 상세 — art 백드롭 + 정보 카드(이름/카테고리/`Body` 설명/[덱 추가·제거]) + 추가 불가 사유 hint |
| 1 | UI | `1_card_browser.md` | 카드 그리드 — art + `CardCategoryStyle` 프레임/폴백 + 이름 + **편성중 뱃지** + 무의식 제외 (SquadRosterBrowser 재사용/확장) |
| 2 | UI | `2_deck_strip.md` | 덱 10슬롯 + 유효성 상태 라벨(`{n}/{size} · reason`) + **Save 버튼**(Validate 게이트) |
| 3 | 통합 | `3_orchestrator.md` | `DreamcatcherDeckPageController` — 추가(dedup·캡)/제거/무의식필터/저장(deck_1 생성·유효성 게이트), 브라우즈→상세 |
| 4 | 배선 | `4_builder_wiring_e2e.md` | 런타임 빌더 + dreamcatcherPanel 배선(옛 뷰 비활성) + Play e2e |
| 5 | 인계 | `5_handoff_summary.md` | handoff |
| 6 | UX | `6_pool_sort_and_slot_selected.md` | 카드 그리드 덱-먼저 정렬(라이브) + 덱 슬롯 선택 outline (스쿼드 unit 10과 쌍) |

순서: 0 → 1 → 2 → 3 → 4 → 5. 핵심 로직(3) 종료 시 code-review, 나머지는 feature 종료 시.

## Feature-wide 계약

- **모달 폐기**: 카드 열람·덱 편집 모두 이 split-view 단일 면에서. 스톤 모드 없음(단일 차원). (기존은 카드 상세가 모달 팝업 → 좌측 상시 패널로 이전.)
- **상세 = 선택 카드**: 그리드 셀 탭이 상세 결정(art + 이름 + 카테고리 배지 + `DreamcatcherCardText.Body` 설명). 편집은 [덱에 추가]/[제거]·슬롯 탭만.
- **이미지 = 정적 art Sprite**: 라이브 Spine 아님. `DreamcatcherCard.art` 없으면 **art 폴백색**(`CardCategoryStyle`가 기존 `ArtFallbackOf` 재현).
- **프레임 색 = 타입/카테고리** (기존 `FrameColorOf`): Subconscious(보라) 우선 > Unit(금) > Normal/Squad(파랑). `CardCategoryStyle`로 공용화(프레임+art폴백).
- **덱 편집 규칙 (유니크, 2026-07-18 사용자 결정)**: 카드는 덱에 **0/1장**(중복 금지). [덱에 추가]=한 장 추가(`EffectiveDeckSize` 상한 + `EffectiveMax(type)` 캡 + **이미 있으면 dedup**). 슬롯 탭/[제거]=제거. 그리드 셀 **"편성중" 불리언 뱃지**(카운트 아님). 상세는 편성됨→[덱에서 제거]만/미편성→[덱에 추가]만(상호배타). 추가 불가 시 사유 표기.
- **무의식(Subconscious) 제외** (기존 gift-phase 규칙): 컬렉션 풀에서 Subconscious 카드 제외. 단 이미 덱에 있으면 슬롯에서 **제거만** 가능(추가는 불가).
- **저장 = 명시적 Save 버튼 + 유효성 게이트** (기존 동일, ⚠ auto-save 아님): `DeckRules.Validate` 통과 시만 Save 활성. 저장 시 `PlayerProfile` 의 deck(id `deck_1`) `cardIds` 갱신 + `selectedDeckId` + `ProfileStore.Save`. 덱은 정확히-10 이 유효라 중간 상태 auto-save 부적합 — 스쿼드와 다른 점.
- **유효성/상태 표기**: `DeckRules.Validate(cardIds, catalog, out reason)`. 덱 스트립에 `{count}/{deckSize} · {reason}`. (레거시 `squad {n}/{max}` 표기는 제거 — Squad 타입 캡 은퇴로 무의미했음.)
- **덱 반입 폴백**: 저장 덱 없음/무효 시 기존 고정 덱 폴백 — 기존 계약 불변(이 spec 은 편집 UI만).
- **옛 뷰 비파괴 보존**: `DreamcatcherDeckBuilderView` enabled=false + 옛 자식 비활성. 되돌리기 = 역순.
- **아키텍처**: 전부 MonoBehaviour 프레젠테이션(Outgame). ECS/BattleBridge 변경 없음. `DreamcatcherCardCatalog`/`PlayerProfileSO`/`DeckRules`/`DreamcatcherCardText` 재사용.

## 기능 패리티 체크리스트 (기존 DreamcatcherDeckBuilderView 대비)

재설계가 기존 기능을 빠뜨리지 않도록 대조 — 각 항목을 담당 unit 에서 검증:

| 기존 기능 | 신 페이지 커버 | unit |
|---|---|---|
| 덱 10슬롯 트레이 + "MY DECK" 프레임 | 덱 스트립 10슬롯 + 유효성 | 2 |
| 컬렉션 그리드(art 카드, 5열 스크롤) | 카드 그리드(art + 편성중) | 1 |
| 프레임색 타입/카테고리 + art 폴백 | `CardCategoryStyle` | 1 |
| 무의식 컬렉션 제외(덱 있으면 제거만) | 브라우저 풀 필터 | 1·3 |
| 카드 상세(art+제목+효과 Body+추가/제거) | 좌측 상세 패널(모달 대체) | 0 |
| 추가: deckSize 상한 + type 캡 | `AddCard` 로직 | 3 |
| 추가 불가 사유(full/type limit) | 상세 버튼 hint | 0·3 |
| 제거 | id occurrence 제거(슬롯/그리드 공용) | 3 |
| 덱 내 카드 표시 | "편성중" 불리언 뱃지 | 1 |
| status 표기 | 덱 스트립 `{n}/{size} · reason` (레거시 squad 제거) | 2 |
| Save 버튼(유효성 게이트) + 저장 | Save 버튼 + `ProfileStore.Save` | 2·3 |
| deck_1 없으면 생성 | orchestrator 저장 로직 | 3 |

## 재사용 (squad-character-page에서)

- `SquadRosterBrowser` 그리드/스크롤/셀 기계 — 카드용으로 확장(편성중 뱃지) 또는 형제. 구현 시 결정(2회째 사용 → 공용화 검토).
- `SquadCharacterPage` 런타임 빌더 패턴(inactive→주입→활성), `UiLayer`, 카테고리 색 헬퍼(신설 `CardCategoryStyle`).

## 후속 후보 (범위 밖)

- 카드 보유/언락/가챠, 여러 덱 수집·전환 UI
- 정렬·필터(카테고리/타입/효과별)
- 무의식(Subconscious)·gift 카드 통합 표기, Unit-type 카드 mechanics 상세
- 상세 art 확대/줌, 카드 뒤집기 연출
