# 2 — 무의식 카드 저작 (2~3장)

## 목적

"림의 선물" 풀을 채운다. 현재 `CardCategory.Subconscious` 에셋은 1장(`slow_awakening`)뿐 → 최소 2~3장 신설해 림 선물이 서로 다른 2장을 뽑게 한다. **기존 효과 채널만 사용, 신규 메커닉/채널 없음**.

## 변경 대상

- (신규) `Assets/_Project/Data/Dreamcatcher/DC_Subconscious_*.asset` — 무의식 카드 2~3장(기존 1장 포함 총 3~4장).
- `Assets/_Project/Data/Dreamcatcher/*Catalog*.asset` — **카탈로그는 명시 직렬화 배열**(`DreamcatcherCardCatalog.cards`, `slow_awakening` guid 가 손으로 등록됨). 신규 카드를 이 **.asset 의 `cards` 배열에 추가**해야 함(폴더 스캔 아님, critic m1 — .cs 변경 아님).
- `Assets/_Project/Scripts/UI/Outgame/DreamcatcherDeckBuilderView.cs` — `IsSubconscious`(line 80), `RebuildOwnedCards`(line ~164, `catalog.AllIds()` 순회). 소유/선택 그리드 구성 필터에 **Subconscious 제외** 추가.
- `Assets/_Project/Scripts/Data/Dreamcatcher/DreamcatcherCard.cs` — `category` 필드(line 55)의 "RETIRED/dormant" 주석을 **다시 load-bearing** 으로 갱신(Rim 풀 + 덱빌더 제외의 근거, critic M3).

## 구현

1. 무의식 카드 2~3장 저작, 기존 필드로만:
   - `category = Subconscious`
   - `type` = `Squad` 또는 `Unit`(스킬은 Lucid 쪽 → 무의식은 스탯/부착 계열 권장)
   - `effects[]`/`attackMods[]`/`mechanics[]` 중 **이미 구현된 채널만**
   - `art` = 기존 아트 재사용/배정(신규 아트 생성은 스코프 밖; 없으면 fallback tint)
   - `displayName`/`description` = "무의식" 테마
   - 수치 = **placeholder 밸런스 초안**(사용자 후속 튜닝). 값은 .asset 에서(하드코딩 금지).
2. 카탈로그 `cards` 배열에 신규 guid 등록 → `ById`/`AllIds` 해석 확인.
3. 덱빌더 제외: `RebuildOwnedCards` 에서 `IsSubconscious` 인 id 를 소유목록에서 skip(선물 전용).
4. **마이그레이션(critic m2)**: 기존 `slow_awakening` 이 이미 저장 덱(cardIds)에 포함될 수 있음. 빌더 소유그리드에서 제외되면 **덱 트레이에선 제거만 가능·재추가 불가**, `DeckRules.Validate` 는 통과(Unit 무제한). 크래시/무효화 없음 — 별도 데이터 마이그레이션 불필요, 이 동작을 의도로 수용.

## 완료 기준

- [ ] `CardCategory.Subconscious` 카드 총 3장 이상(신규 2~3장).
- [ ] 카탈로그 `cards` 배열 등록 → 런타임 `ById`/`AllIds` 해석 성공.
- [ ] 덱빌더 소유그리드에 무의식 미노출.
- [ ] 기존 `slow_awakening` 포함 저장 덱 로드 시 오류 0(제거만 가능 동작 확인).
- [ ] `category` 주석 갱신, 컴파일/임포트 에러 0.
- [ ] (unit 1 연동) 림 선물이 서로 다른 무의식 2장 추출 가능.
