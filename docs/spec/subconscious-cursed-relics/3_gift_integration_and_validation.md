# 3 — 림 선물 통합과 최종 검증

## 목적

교체한 두 카드가 기존 카탈로그·림 선물·순환 손패·상세 UI에서 별도 분기 없이 소비되는지 확인한다.

## 변경 대상

- `Assets/_Project/Data/Dreamcatcher/DreamcatcherCardCatalog.asset` (참조 확인)
- `Assets/_Project/Tests/EditMode/DreamcatcherCatalogSyncTests.cs`
- `docs/spec/README.md`

## 구현

1. GUID 보존 rename으로 기존 카탈로그 슬롯이 새 에셋을 계속 가리키게 한다.
2. Subconscious 풀을 다음 3장으로 유지한다.
   - `slow_awakening`
   - `calamity_heart`
   - `cracked_grail`
3. `DreamcatcherCatalogSyncTests`에서 실제 에셋 기준 풀 구성을 검증한다.
4. 기존 `GiftDeckComposer.PickRim`의 중복 없는 2장 선택과 시드 결정론은 변경하지 않는다.

씬, Gift view/controller, Hand controller, DeckRules, 카드 스키마는 변경하지 않는다. 기존 `category=Subconscious` 필터와 표시 경로를 그대로 사용한다.

## 완료 기준

- [ ] 카탈로그에 미등록·null·중복 ID가 없다.
- [ ] Subconscious 풀은 정확히 위 3장이고 림은 서로 다른 2장을 뽑는다.
- [ ] 두 카드가 덱빌더에는 노출되지 않고 Gift·손패·부착·Inspect에는 표시된다.
- [ ] 실패한 부착은 무차감이고 성공한 부착은 기존 비용·순환 계약을 따른다.
- [ ] compile clean, 관련 EditMode·PlayMode 테스트 green.
- [ ] 신규 ECS 타입·시스템·채널 0, 씬 diff 0.
