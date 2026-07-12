# 2 — 무의식 카드 저작 (2~3장)

## 목적

"림의 선물"이 지급할 무의식(Subconscious) 카드 풀을 채운다. 현재 `CardCategory.Subconscious` 에셋은 1장뿐 → 최소 2~3장으로 늘려 림 선물이 서로 다른 2장을 보여줄 수 있게 한다. **기존 효과 채널만 사용, 신규 메커닉/채널 없음**(드림캐쳐 구조 변경 0 원칙).

## 변경 대상

- (신규) `Assets/_Project/Data/Dreamcatcher/DC_Subconscious_*.asset` — 무의식 카드 2~3장(기존 1장 포함 총 3~4장 되도록).
- `Assets/_Project/Scripts/Data/Dreamcatcher/DreamcatcherCardCatalog.cs` — 신규 카드 등록(카탈로그가 명시 배열이면 추가, 폴더 스캔이면 자동).
- `Assets/_Project/Scripts/UI/Outgame/DreamcatcherDeckBuilderView.cs` — `IsSubconscious`(line 80) 기반으로 **덱빌더 선택 풀에서 Subconscious 제외**(선물 전용). 현재 프레임 색만 칠하고 있으므로 소유/선택 목록 필터에 제외 조건 추가.

## 구현

1. 무의식 카드 2~3장 저작. 각 카드는 기존 필드로만 구성:
   - `category = Subconscious`
   - `type` = `Squad` 또는 `Unit`(기존 축 재사용 — Active/스킬은 Lucid 쪽이므로 무의식은 스탯/부착 계열 권장)
   - `effects[]` / `attackMods[]` / `mechanics[]` 중 **이미 구현된 채널**만 사용
   - `art` = 기존 카드 아트 재사용 또는 dreamcatcher-card-art 풀에서 배정(신규 아트 에셋 생성은 스코프 밖; 없으면 fallback tint)
   - `displayName` / `description` = "무의식" 테마 네이밍
   - 수치 = **placeholder 밸런스 초안**(사용자 후속 튜닝 전제). 하드코딩 금지 — 값은 .asset 에서.
2. 카탈로그 등록 방식 확인 후 신규 id 반영. `DeckRules`(exactly 10, Squad≤2)와 충돌하지 않는지 — 무의식은 덱빌더 밖이므로 룰 카운트에 안 들어가야 함(3번과 연동).
3. 덱빌더 제외: 무의식 카드는 outgame 소유/선택 그리드에 노출하지 않는다. `IsSubconscious` 판정을 소유목록 구성 필터로 승격.

## 완료 기준

- [ ] 무의식 `CardCategory.Subconscious` 카드 총 3장 이상 존재(신규 2~3장 저작).
- [ ] 카탈로그가 신규 카드를 `ById`/`AllIds` 로 해석(런타임 조회 성공).
- [ ] 덱빌더 UI 에 무의식 카드 미노출(선택 풀 제외) 확인.
- [ ] 컴파일/임포트 에러 0, 에셋 임포트 정상.
- [ ] (unit 1 연동) 림 선물이 서로 다른 무의식 2장을 뽑을 수 있음.
