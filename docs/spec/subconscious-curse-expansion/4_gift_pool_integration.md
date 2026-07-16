# 4. 선물 풀 통합 · 전체 검증

## 목적

신규 3장이 무의식 풀(총 6장)의 일원으로 림의 선물에 정상 편입되는지, 기존 계약(덱빌더 제외·카탈로그 sync·기존 저주 2장)이 깨지지 않았는지 통합 검증한다. 신규 코드는 원칙적으로 0 — 검증과 등록 잔손질만.

## 변경 대상

- `Assets/_Project/Data/Dreamcatcher/DreamcatcherCardCatalog.asset` — units 0~2 에서 개별 등록했다면 검증만, 누락 시 등록
- `Assets/_Project/Tests/EditMode/DreamcatcherCatalogSyncTests.cs` — 신규 카드로 인한 기대치 영향 확인(시트 export JSON 은 stale 이므로 테스트가 JSON 과 대조한다면 스킵/기대치 조정 판단)
- Play smoke

## 구현

**확인 항목** (코드 변경이 아니라 계약 검증):
1. **림 풀 6장** — `ResolveRimGift` 는 `category==Subconscious` 필터라 카탈로그 등록만으로 자동 편입. `GiftDeckComposer.PickRim` 무변경으로 서로 다른 2장 추출 확인.
2. **덱빌더 제외** — Subconscious 는 덱빌딩 밖(기존 계약). 신규 3장이 덱빌더 목록에 나타나지 않는지 확인.
3. **Gift 연출** — Rim(림의 선물) 리빌에서 신규 카드가 표시되는지. 아트 미저작 상태(unit 5 이전)에서는 category 색 폴백이 정상 동작해야 한다.
4. **기존 무회귀** — `DreamcatcherCursedRelicTest` 등 기존 PlayMode/EditMode 전체 green. 재앙의 심장·금이 간 성배·느린 각성이 풀에서 계속 추출되는지.
5. **로그** — `LogDeck` 이 신규 카드 id 를 정상 기록.

**시트 sync 유의** — `dreamcatcher-sheet-sync/7_full_dreamcatcher_export.json` 은 cursed-relics 이후 이미 stale. 본 spec 은 JSON 을 갱신하지 않는다(후속 후보 — 일괄 재export). sync 테스트가 이 JSON 을 SoT 로 대조한다면 이 unit 에서 그 대조 범위를 실측하고 판단을 기록한다.

## 완료 기준

- [ ] 카탈로그에 신규 3장 등록 완료 (`sub_butterfly_dream`, `sub_incubus_pact`, `sub_fattened_offering`)
- [ ] Play: Gift 페이즈 Rim 경로에서 신규 카드 등장 확인 — 결정 시드(`GameManager.MatchSeed`) 주입으로 Rim 을 강제한다(비결정 반복 진입 지양, critic 권고)
- [ ] 덱빌더에 신규 3장 비노출 확인
- [ ] EditMode + PlayMode 전체 테스트 green (기존 저주 회귀 없음)
