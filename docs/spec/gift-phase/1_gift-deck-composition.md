# 1 — 선물 덱 조합 seam

## 목적

선물 이벤트(Lucid/Rim)를 **매치 시드로 결정**하고, 저장 10 + 선물 2 = 확정 12장(순서 포함)을 만든다. `DreamcatcherCycleDeck` 이 이 순서를 **재셔플 없이** 소비하도록 no-shuffle 경로를 추가하고, `DreamcatcherHandController` 가 배치 진입 시 캐시된 확정 덱을 소비하도록 전환한다. 연출(unit 4)이 보여줄 데이터의 source of truth이며 **연출 순서 == 런타임 순서**를 코드로 보장한다.

## 변경 대상

- (신규) `Assets/_Project/Scripts/Core/Dreamcatcher/GiftDeckComposer.cs` — 순수 static: plain 입력(저장 10, Lucid/Rim 후보, 시드) → plain 출력(확정 순서 12 + `GiftKind`). 아키텍처 중립(제약 10) → EditMode 대상.
- `Assets/_Project/Scripts/Core/Dreamcatcher/DreamcatcherCycleDeck.cs` — **no-shuffle 수용 경로 추가**. 현재 생성자(line 40-46)는 항상 Fisher-Yates. pre-ordered 리스트를 그대로 큐에 넣는 오버로드/플래그 추가. 내부 FY 로직은 `GiftDeckComposer` 와 **공유 헬퍼**로 추출해 알고리즘 1벌 유지(이중 구현 방지).
- `Assets/_Project/Scripts/Core/Dreamcatcher/DreamcatcherHandController.cs` — `OnPhaseChanged`(85), `ResolveAttachDeck`(102), `AppendActiveCards`(124), `FindActiveCard`(136). Gift 조합·캐시 + Placement 캐시 소비.
- `Assets/_Project/Scripts/Data/Dreamcatcher/DreamcatcherCardCatalog.cs` — Subconscious 풀 조회(`AllIds`/`ById`).
- `Assets/_Project/Scripts/Core/SkillLoadoutController.cs` — `Picked` 재사용(Lucid, 변경 없음).
- (신규) `Assets/_Project/Tests/EditMode/GiftDeckComposerTests.cs`.

## 구현

1. **이벤트 선택**: `GiftKind Pick(int seed, GiftConfig)` — 매치 시드 파생 결정론 값으로 `lucidWeight:rimWeight` 가중 선택. `MatchSeed` 는 재시작 간 고정이므로 **재시작 시 동일 결과**(계약 4, restartIndex 미사용).
2. **선물 2장**:
   - **Lucid**: `SkillLoadoutController.Picked` 를 `FindActiveCard(skill)` 로 Active 카드 매핑 — 현행 `AppendActiveCards` 로직 재사용.
   - **Rim**: 카탈로그 `card.category == CardCategory.Subconscious` 목록을 시드 셔플 후 앞 2장. **풀<2 면 부족분은 임의 폴백**(카탈로그 non-Active 카드에서 시드로, 중복 허용). unit 2 저작 후 폴백 거의 미발동.
   - **Rim 이면 롤된 Active 2장은 append 하지 않는다**(스킬↔무의식 교환, 계약 5/m3). `SetSkillLoadout` 자체는 계속 실행되나 인핸드에 안 붙음 — 이를 전제로 다른 코드가 Picked 인핸드 존재를 하드 가정하지 않는지 확인.
3. **확정 순서 (이중 셔플 금지, critic B2)**:
   - `GiftDeckComposer` 가 `저장10 + 선물2` 를 공유 FY 헬퍼(시드 동일)로 셔플해 **확정 순서 12** 를 만든다.
   - HandController 는 이 확정 순서를 `DreamcatcherCycleDeck` 의 **no-shuffle 경로**로 넘긴다 → CycleDeck 내부 재셔플 없음 → 큐 초기 순서 == 확정 순서.
   - Unit 타입 카드는 사용 시 out-of-pool 로 빠지는 큐 동작이 있으나, **초기 큐 순서(연출이 보여줄 대상)** 는 확정 순서와 일치. 연출은 "초기 확정 순서" 를 보여주는 것이며 이후 인게임 사이클 동작과 모순 없음.
4. **HandController 전환**:
   - Gift 진입(unit 3 라우팅이 호출하는 조합 API 또는 `OnPhaseChanged(Gift)`): `GiftDeckComposer.Compose(...)` → `(GiftKind, IReadOnlyList<DreamcatcherCard> ordered12)` 캐시. GiftPhaseView 가 조회하는 public 접근자 제공.
   - Placement 진입: 기존 `AppendActiveCards` 대신 **캐시 ordered12** 로 no-shuffle CycleDeck 생성. **캐시 없으면 기존 경로 폴백**(Gift 우회/구경로 안전).
5. **재시작**: Gift 재진입마다 재조합하나 동일 시드 → 동일 결과(계약 4).

## 완료 기준

- [ ] 컴파일 통과, `read_console` 에러 0.
- [ ] EditMode: **결정론**(같은 시드→동일 `GiftKind`+동일 12장 순서), **Lucid 분기**(Picked 2 Active 포함), **Rim 분기**(Subconscious 2장), **Rim 폴백**(풀<2 시 12장·크래시 없음).
- [ ] EditMode 또는 런타임 assert: **CycleDeck 초기 큐 순서 == 캐시 확정 순서**(no-shuffle 경로 검증, 이중 셔플 없음).
- [ ] Lucid 경로 인게임 스킬 카드 동작 무회귀(현행 append 와 동일 결과).
- [ ] 캐시 부재 시 기존 경로 폴백 정상.
