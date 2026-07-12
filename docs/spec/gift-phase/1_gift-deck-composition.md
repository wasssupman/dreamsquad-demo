# 1 — 선물 덱 조합 seam

## 목적

선물 이벤트(Lucid/Rim)를 **매치 시드로 결정**하고, 저장 10 + 선물 2 = 12장을 조합한다. `DreamcatcherHandController` 가 **Gift 진입 시** 실제 `DreamcatcherCycleDeck` 을 1회 생성·캐시하고, **배치에서 같은 인스턴스를 재사용**한다 → 이중 셔플 없이 **연출 순서 == 런타임 순서**를 보장. `DreamcatcherCycleDeck` 은 **무변경**.

## 변경 대상

- (신규) `Assets/_Project/Scripts/Core/Dreamcatcher/GiftDeckComposer.cs` — 순수 static: plain 입력(저장 10, Lucid/Rim 후보, 시드) → plain 출력(선물 2장 + `GiftKind`). 아키텍처 중립(제약 10) → EditMode 대상. **셔플은 안 한다**(CycleDeck 생성자에 위임).
- `Assets/_Project/Scripts/Core/Dreamcatcher/DreamcatcherHandController.cs` — `OnPhaseChanged`(85), `ResolveAttachDeck`(102), `AppendActiveCards`(124), `FindActiveCard`(136). Gift 진입 시 조합→`new DreamcatcherCycleDeck(12장, seed)` 생성·캐시(+`GiftKind`), Placement 는 캐시 인스턴스 재사용.
- `Assets/_Project/Scripts/Core/Dreamcatcher/DreamcatcherCycleDeck.cs` — **변경 없음**(`Hand(12)` 로 확정 순서 읽기만).
- `Assets/_Project/Scripts/Data/Dreamcatcher/DreamcatcherCardCatalog.cs` — Subconscious 풀 조회(`AllIds`/`ById`).
- `Assets/_Project/Scripts/Core/SkillLoadoutController.cs` — `Picked` 재사용(Lucid, 변경 없음).
- (신규) `Assets/_Project/Tests/EditMode/GiftDeckComposerTests.cs`.

## 구현

1. **이벤트 선택**: `GiftKind Pick(int seed, GiftConfig)` — 매치 시드 파생 결정론 값으로 `lucidWeight:rimWeight` 가중 선택. `MatchSeed` 는 재시작 간 고정이므로 **재시작 시 동일 결과**(계약 4, restartIndex 미사용).
2. **선물 2장**:
   - **Lucid**: `SkillLoadoutController.Picked` 를 `FindActiveCard(skill)` 로 Active 카드 매핑 — 현행 `AppendActiveCards` 로직 재사용.
   - **Rim**: 카탈로그 `card.category == CardCategory.Subconscious` 목록을 시드 셔플 후 앞 2장. **풀<2 면 부족분은 임의 폴백**(카탈로그 non-Active 카드에서 시드로, 중복 허용). unit 2 저작 후 폴백 거의 미발동.
   - **Rim 이면 롤된 Active 2장은 append 하지 않는다**(스킬↔무의식 교환, 계약 5/m3). `SetSkillLoadout` 자체는 계속 실행되나 인핸드에 안 붙음 — 이를 전제로 다른 코드가 Picked 인핸드 존재를 하드 가정하지 않는지 확인.
3. **확정 순서 (이중 셔플 금지, critic B2 — CycleDeck 무변경 해법)**:
   - HandController 가 `저장10 + 선물2`(unshuffle 상태) 로 **`new DreamcatcherCycleDeck(12장, MatchSeed)`** 를 Gift 진입 시 1회 생성 → 생성자 내부 FY 셔플이 **딱 한 번** 적용됨.
   - `deck.Hand(12)`(handSize=12 = 전체 큐 순서, `DreamcatcherCycleDeck.cs:51`) 로 **확정 순서**를 읽어 연출(unit 4)에 넘긴다.
   - **같은 `deck` 인스턴스를 배치에서 재사용** → 재생성/재셔플 없음 → 인게임 핸드 순서 == 연출이 보여준 `Hand(12)`. Unit 카드 out-of-pool 은 사용 시점에 발생하며 초기 순서와 무관.
4. **HandController 전환**:
   - Gift 진입(unit 3 라우팅이 호출하는 조합 API 또는 `OnPhaseChanged(Gift)`): 선물 2장 조합 → CycleDeck 생성 → `(GiftKind, DreamcatcherCycleDeck deck)` 캐시. GiftPhaseView 가 `deck.Hand(12)` + `GiftKind` 를 조회하는 public 접근자 제공. 연출용으로 "저장10 / 선물2" 분리 목록도 노출(4-2 등장 그룹핑).
   - Placement 진입: 기존 `AppendActiveCards`→재생성 대신 **캐시 `deck` 재사용**. **캐시 없으면 기존 경로 폴백**(Gift 우회/구경로 안전).
5. **재시작**: Gift 재진입마다 재조합하나 동일 시드 → 동일 결과(계약 4).

## 완료 기준

- [ ] 컴파일 통과, `read_console` 에러 0.
- [ ] EditMode: **결정론**(같은 시드→동일 `GiftKind`+동일 12장 순서), **Lucid 분기**(Picked 2 Active 포함), **Rim 분기**(Subconscious 2장), **Rim 폴백**(풀<2 시 12장·크래시 없음).
- [ ] 런타임 assert/로그: **연출이 읽은 `deck.Hand(12)` == 배치 인게임 핸드 순서**(동일 인스턴스 재사용, 이중 셔플 없음). `DreamcatcherCycleDeck` 코드 무변경 확인.
- [ ] Lucid 경로 인게임 스킬 카드 동작 무회귀(현행 append 와 동일 결과).
- [ ] 캐시 부재 시 기존 경로 폴백 정상.
