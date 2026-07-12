# 1 — 선물 덱 조합 seam

## 목적

선물 이벤트(Lucid/Rim)를 **매치 시드로 결정**하고, 저장 덱 10 + 선물 2 = 확정 12장(순서 포함)을 만드는 조합 로직을 둔다. `DreamcatcherHandController` 가 배치 진입 시 기존 `AppendActiveCards` 대신 이 확정 덱을 소비하도록 전환한다. 연출(unit 4)이 보여줄 데이터의 source of truth.

## 변경 대상

- `Assets/_Project/Scripts/Core/Dreamcatcher/DreamcatcherHandController.cs` — `OnPhaseChanged`(line 85), `ResolveAttachDeck`(102), `AppendActiveCards`(124). Gift 진입 시 조합·캐시, Placement 진입 시 캐시 소비.
- (신규 또는 인접) 조합 로직: `GiftDeckComposer` static/헬퍼 — plain 입력(저장 10 카드, 선물 후보 풀, 시드) → plain 출력(확정 12 + `GiftKind`). 아키텍처 중립 → 순수 함수(제약 10, EditMode 대상).
- `Assets/_Project/Scripts/Data/Dreamcatcher/DreamcatcherCardCatalog.cs` — `AllIds`/`ById` 로 Subconscious 풀 조회.
- `Assets/_Project/Scripts/Core/SkillLoadoutController.cs` — `Picked` 재사용(Lucid).
- (신규) `Assets/_Project/Tests/EditMode/GiftDeckComposerTests.cs`.

## 구현

1. **이벤트 선택**: `GiftKind Pick(seed, GiftConfig)` — 매치 시드에서 파생한 결정론 값으로 `lucidWeight:rimWeight` 가중 선택. 같은 시드 → 같은 결과.
2. **선물 2장 해석**:
   - **Lucid**: `SkillLoadoutController.Picked`(이미 시드 롤된 2 Active) 를 `FindActiveCard(skill)` 로 Active `DreamcatcherCard` 매핑 — **현행 `AppendActiveCards` 경로 그대로 재사용**.
   - **Rim**: 카탈로그에서 `card.category == CardCategory.Subconscious` 인 카드 목록을 모아 시드 셔플 후 앞 2장. **풀 < 2 면 부족분은 임의 폴백**(카탈로그 non-Active 카드에서 시드로 채움, 중복 허용). 폴백은 안전장치이며 unit 2 저작 후엔 거의 미발동.
3. **확정 12장 순서**: `저장 10 + 선물 2` 를 매치 시드 Fisher-Yates 로 셔플 → `DreamcatcherCycleDeck` 이 실제로 쓰는 초기 큐 순서와 **동일한 시드·동일한 알고리즘**을 사용해야 한다(연출 노티 = 런타임 덱). 가능하면 `DreamcatcherCycleDeck` 의 셔플을 재사용하거나, 조합 단계에서 확정 순서를 만들어 `DreamcatcherCycleDeck(orderedCards, seed=NoShuffle)` 로 넘기는 방식 중 하나로 **이중 셔플/불일치를 방지**한다.
4. **HandController 전환**:
   - Gift 진입(`OnPhaseChanged(Gift)` 또는 컨트롤러 콜백): `ComposeGiftDeck` 실행 → `(GiftKind kind, IReadOnlyList<DreamcatcherCard> ordered12)` 캐시. 연출/View 가 이 캐시를 읽는다.
   - Placement 진입: 기존 `ResolveAttachDeck + AppendActiveCards` 대신 **캐시된 `ordered12`** 로 `DreamcatcherCycleDeck` 생성. 캐시가 없으면(Gift 우회 경로) 기존 경로 폴백.
5. **재시작**: Gift 재진입마다 재조합. 재시작 시드 정책은 기존 Restart(스킬픽 보존) vs Redraft(재롤)와 **모순되지 않게** — Restart 도 이벤트를 재추첨하되(계약 8), 사용하는 시드 소스는 `EnsureMatchSeed`/기존 관례를 따른다.

## 완료 기준

- [ ] 컴파일 통과, `read_console` 에러 0.
- [ ] EditMode: **결정론**(같은 시드 → 동일 `GiftKind` + 동일 12장 순서), **Lucid 분기**(Picked 2 Active 포함), **Rim 분기**(Subconscious 2장), **Rim 폴백**(풀<2 시 12장 채워짐·크래시 없음).
- [ ] 연출이 보여줄 12장 순서 == 실제 `DreamcatcherCycleDeck` 초기 큐 순서(수동/테스트로 대조).
- [ ] Lucid 경로에서 기존 인게임 스킬 카드 동작 무회귀(현행 append 와 동일 결과).
