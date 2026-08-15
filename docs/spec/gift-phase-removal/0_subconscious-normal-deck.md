# 0. 무의식 카드 일반 덱 승격

## 목적

림의 선물이 사라지면 무의식 카드 6장은 진입로를 잃는다. 덱빌더 제외 필터를 풀어 **플레이어가 직접 고르는 일반 덱 카드**로 승격한다. 선물 페이즈 제거와 독립이라 먼저 넣어도 무해하고, 덱 페이지에서 즉시 눈으로 검증된다.

## 변경 대상

- `Assets/_Project/Scripts/UI/Outgame/DreamcatcherDeckPageController.cs` (L106 부근)
- `Assets/_Project/Scripts/Core/Profile/PresetApply.cs` (L184 부근)
- `Assets/_Project/Scripts/Data/Dreamcatcher/DreamcatcherCard.cs` (`category` 주석)
- `Assets/_Project/Scripts/UI/Outgame/DreamcatcherCardBrowser.cs` (클래스 주석)
- `Assets/_Project/Tests/EditMode/Profile/PresetApplyTests.cs` (`FilterCards_DropsHidden_*` 케이스)

## 구현

1. **덱 페이지 풀 필터 제거** — `DreamcatcherDeckPageController` 의 `if (c.category == CardCategory.Subconscious) continue;` 한 줄 삭제. `_pool` 이 곧 그리드 소스이자 추가 가능 목록이라 이 한 줄이 "보이지도, 넣을 수도 없다"를 동시에 걸고 있었다. `visible == 0` 필터는 **그대로 둔다**(시트가 숨긴 카드는 여전히 숨김).

2. **프리셋 적용 필터 제거** — `PresetApply` 의 같은 조건 한 줄 삭제. 이 함수의 판정 기준은 "페이지의 `CanAdd` 와 일치" 이므로 1번과 반드시 같이 바꾼다. 어긋나면 프리셋으로만 넣을 수 있거나 그 반대인 카드가 생긴다.

3. **`category` 주석 갱신** — `DreamcatcherCard.category` 의 "gift-phase 가 Rim 풀 필터 + 덱빌더 제외에 쓴다" 서술을 폐기하고, 이제 **시각 라벨 전용**(`CardCategoryStyle` 의 보라 프레임 + "무의식" 칩)임을 적는다. enum 값과 에셋의 `category: 2` 는 **건드리지 않는다**.

덱 규칙은 손대지 않는다. `DeckRules`(덱 10장 · Squad ≤2 · Unit 무제한)가 그대로 적용되어:

| 카드 | id | type | 규칙상 위치 |
|---|---|---|---|
| 금 간 성배 | `cracked_grail` | Squad | Squad ≤2 캡에 편입 |
| 희생계약 | `sub_incubus_pact` | Squad | **카탈로그 미등록 — 덱 페이지에 뜨지 않는다** (아래 참조) |
| 나비꿈 | `sub_butterfly_dream` | Unit | 무제한 |

| 재앙의 심장 | `calamity_heart` | Unit | 무제한 |
| 살찌운 제물 | `sub_fattened_offering` | Unit | 무제한. 적 표식 카드 — 드래그 라우팅 기존 그대로 |
| 느린 각성 | `slow_awakening` | Unit | 무제한. 기존 "제거만 가능·재추가 불가" 마이그레이션 제약 자연 해소 |

4. **테스트 갱신** — `PresetApplyTests.FilterCards_DropsHidden_Subconscious_Duplicates_Unresolved` 가 무의식 제외를 단언한다. `FilterCards_DropsHidden_Duplicates_Unresolved` 로 개명하고 무의식 카드가 **살아남는지**를 단언해 승격의 회귀 가드로 뒤집는다(계약 10 — 테스트 갱신은 같은 커밋).

**실제 노출은 6장이 아니라 5장이다.** 덱 페이지 풀은 카탈로그에서 만들어지는데
`Card_IncubusPact`(희생계약)는 2026-08-08 사용자 결정으로 카탈로그에서 **의도적으로 빠져
있다** — 유출 허용치를 선불로 내는 카드인데 goal-tower-siege 이후 유출이 골 파괴 뒤에만
생겨 지불이 사실상 무비용이 됐기 때문이다. `DreamcatcherCatalogSyncTests` 의
`IntentionallyDisabled` 가 그 사유와 함께 이 제외를 지킨다. 이 unit 은 그 결정을 뒤집지
않는다 — 재등록 여부는 유출/스트레스 규칙이 안정된 뒤의 별도 판단이다.

밸런스 재조정은 이 unit 의 스코프가 아니다(README 후속 후보).

## 완료 기준

- [ ] compile 성공, 콘솔 에러 0
- [ ] 덱 페이지 그리드에 무의식 **5장**이 보이고 추가 가능 (희생계약은 카탈로그 미등록이라 **안 뜨는 것이 정상**)
- [ ] Squad 카드 3장째 추가 시 캡 거절(`DeckRules` 메시지) — 금 간 성배/희생계약이 그 캡을 정상적으로 먹는다
- [ ] `visible == 0` 으로 숨긴 카드는 여전히 그리드에 없다
- [ ] 무의식 카드를 포함한 10장 덱을 저장 → 전투 진입 시 그 덱이 손패에 반영
- [ ] EditMode `DeckRulesTests` · `ProfileStoreDefaultDeckTests` 그린
- [ ] `DreamcatcherCatalogSyncTests.SubconsciousPool_MatchesCursedGiftRoster`(6장 로스터) 여전히 그린 — 이 unit 은 `category` 값을 바꾸지 않는다는 가드
