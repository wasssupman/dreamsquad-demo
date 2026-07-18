# 5 — Handoff (dreamcatcher-deck-page)

> feature 종료 인계. 최신 계약은 README(패리티 체크리스트 포함)·번호 문서·코드 우선.

## Commit

- `2d48f61c` 스펙 (패리티 체크리스트 포함)
- `30d882cf` units 0~4 — 컴포넌트 6종 + DreamcatcherPanel 배선 + Play e2e

## Implemented

- 옛 `DreamcatcherDeckBuilderView`(슬롯+보유그리드+모달) → **squad-character-page와 동일 레이아웃**(좌 카드 art 백드롭+정보카드 / 우 덱 10슬롯+그리드)로 재설계. 모달 카드 상세 → 좌측 상시 패널.
- 좌 상세: 타로 art + 이름 + 카테고리 배지 + `DreamcatcherCardText.Body` 효과/설명 + [덱 추가/제거] + 추가 불가 hint.
- 우 덱 스트립: `EffectiveDeckSize` 슬롯 + status(`{n}/{size} · reason`) + Save(Validate 게이트).
- 우 그리드: 전 카드 art + 프레임색 + **"편성중" 불리언 뱃지**(유니크). 무의식 제외.
- 편집: 추가(deckSize 상한 + type 캡 + **dedup, 유니크**) / 제거. 편성됨→[덱에서 제거]만·미편성→[덱에 추가]만. **편집 in-memory, Save 버튼만 영속**(정확히-N 덱 → auto-save 부적합).

## Key Files

- `Assets/_Project/Scripts/UI/Outgame/`: `DreamcatcherDeckPage.cs`(런타임 빌더·씬 facing), `DreamcatcherDeckPageController.cs`(오케스트레이터), `DreamcatcherCardDetailView.cs`, `DreamcatcherCardBrowser.cs`, `DreamcatcherDeckStrip.cs`, `CardCategoryStyle.cs`
- `Assets/_Project/Scenes/OutgameScene.unity`(DreamcatcherPanel/DreamPage + 옛 뷰 비활성)
- 재사용: `DreamcatcherCardCatalog`/`DeckRules`/`DreamcatcherCardText`/`PlayerProfile.SelectedDeck`/`ProfileStore`

## Verified

- 컴파일 클린(에러 0). Play e2e: 로비 드림캐쳐 열기 → 실화면 렌더(타로 art 상세 + 카테고리 배지 + Body 효과 + 편성중 뱃지 + 덱 스트립 + 유효성 + Save), 콘솔 에러 0, 브라우즈→상세 비파괴('guardian_fortress'→가디언 풀존버).
- 기능 패리티(README 체크리스트): 프레임색·무의식 제외·추가 캡/dedup·명시적 Save+게이트·deck_1 생성·status 포맷 — 전부 커버.

## Notes (되돌리면 안 됨)

- **편집은 in-memory, Save 버튼만 영속**(Validate 게이트). auto-save 금지 — 정확히-deckSize 덱이라 중간 상태(8/10)가 무효. 스쿼드(auto-save)와 의도적으로 다름.
- **무의식(Subconscious)**: 컬렉션 풀 제외(gift 전용). 덱에 이미 있으면 슬롯에서 제거만 가능.
- **옛 DreamcatcherDeckBuilderView 비파괴 보존**(enabled=false + 옛 자식 비활성). 되돌리기 = 역순.
- 컨트롤러 GO inactive 생성→주입→활성. 정적 art라 SkeletonGraphic/머티리얼 불요.
- deckSize/type캡은 `DeckRules`(ruleConfig, 현재 deckSize=8) 라이브 — 하드코딩 금지.

## Follow-up

- 사용자 실기기/에디터 hands-on(로그인→드림캐쳐 추가/제거·저장 지속·무효 방지).
- 관찰: 기존 저장 덱 10장 vs 현재 deckSize 8 → 초과 무효 표시(데이터 상황). 사용자 정리 필요.
- 덱 스트립 상단 status/Save 여백 튜닝, 상세 art 크기 미세조정 여지.
- (후속 후보) 카드 보유/언락·정렬/필터·여러 덱 전환.
