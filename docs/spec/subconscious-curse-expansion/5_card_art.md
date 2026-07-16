# 5. 카드 아트 3종

## 목적

신규 무의식 3장에 타로 스타일 카드 아트를 저작해 배정한다. `subconscious-cursed-relics/4_card_art.md` 선례(저주 카드 아트 작업 절차·톤)를 따른다.

## 변경 대상

- `Assets/_Project/Art/DreamcatcherCards/` — 신규 아트 3종 (임포트 세팅은 기존 카드 아트와 동일)
- `Card_ButterflyDream.asset` / `Card_IncubusPact.asset` / `Card_FattenedOffering.asset` — `art` 필드 배정

## 구현

**모티프 가이드** (§6 저주 톤 — 어둡고 달콤한 유혹):

| 카드 | 모티프 |
|---|---|
| 호접몽 | 잠든 실루엣을 감싼 고치 + 빠져나오는 나비(장자 호접몽). 남보라 바탕, 나비 날개에 금빛 |
| 몽마의 계약 | 계약서/인장 위에 앉은 몽마의 그림자, 촛불 하나. 핏빛 인장 포인트 |
| 살찌운 제물 | 제단 위에 살찐 악몽, 그 위로 드리운 표식 문양. 탐욕스러운 금-적 대비 |

- 기존 무의식 카드(재앙의 심장·금이 간 성배)와 같은 프레임/비율 규격 유지 — 덱빌더의 Subconscious 프레임 색과 조화.
- `.meta` 짝 add 필수 (경로 지정 add 시 GUID 파괴 주의 — lessons 참조).

## 완료 기준

- [x] 3장 모두 `art` 배정, category 색 폴백이 아닌 실아트 렌더 — `dreamcatcher_card_26/27/28.png` (코덱스 생성, 시리즈 프레임 일치 확인)
- [ ] 확인 지점: 손패(HandView) · Gift 리빌 연출 · 유닛 인스펙트 패널 — 사용자 Play 육안 대기
- [x] 임포트 세팅이 기존 카드 아트와 동일 — card_25 meta 복제(Sprite/Single/mipmap off, guid 보존), `CompletedCardArt_HasExpectedSpriteImportContract` 에 3항목 확장으로 잠금(1024×1536 실측 포함)

확인 2026-07-16 — EditMode 카탈로그 suite 10/10 (아트 계약 테스트 신규 3항목 포함). 아트 소싱: 세션에서 작성한 코덱스 프롬프트(스타일 레퍼런스 card_24/25 첨부)로 사용자가 생성.
