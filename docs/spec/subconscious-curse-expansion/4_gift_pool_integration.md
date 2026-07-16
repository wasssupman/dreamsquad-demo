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

- [x] 카탈로그에 신규 3장 등록 완료 (`sub_butterfly_dream`, `sub_incubus_pact`, `sub_fattened_offering`) — 로스터 테스트가 6장 잠금
- [x] Rim 경로에서 신규 카드 등장 — `RimGift_LiveCatalogPool_PicksTwoDistinctSubconscious`(EditMode): 실 카탈로그 풀 6장에서 시드 64종 × 서로 다른 2장 + 신규 3장 전부 추출 가능(시드 유니온) 결정론 검증. Gift 연출 육안(리빌에서 category 폴백색 표시)은 사용자 Play 확인 대기
- [x] 덱빌더에 신규 3장 비노출 — `DreamcatcherDeckBuilderView.cs:168` category==Subconscious skip(카탈로그 구동 자동)
- [x] EditMode + PlayMode 전체 테스트 — EditMode 856/856(skip 2 = 기존 Testability 건). PlayMode 30/34: **실패 4건은 전부 main 기존 실패로 실측 확정** — 내 커밋 이전(`ea155e65`) detached 재실행에서 동일 실패 재현. 내역: DeckCarryIn(덱 10장 기대 vs 라이브 규칙 8장, `56cf7380` 이후 stale + 제거된 fallback 덱 기대), Squad/DreamstoneCarryIn(`RequestPlacement`→Placement 기대 vs gift-phase 의 Gift 삽입 이후 stale), MovementIntegrity(어그로 스모크 — 본 spec 무관 도메인). → `docs/spec/README.md` Follow-up Backlog "PlayMode 스모크 위생" 이관

확인 2026-07-16 — 신규 저주 3장 관련 suite 전부 green(카탈로그 10 + 컴포저 9, PlayMode Bounty/Cocoon/Pact 7). 기존 실패 4건은 위 실측 근거로 본 spec 과 무관.
