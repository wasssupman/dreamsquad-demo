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

- [ ] 3장 모두 `art` 배정, category 색 폴백이 아닌 실아트 렌더
- [ ] 확인 지점: 손패(HandView) · Gift 리빌 연출 · 유닛 인스펙트 패널 · (덱빌더는 Subconscious 제외라 비대상)
- [ ] 임포트 세팅이 기존 카드 아트와 동일(압축/맥스사이즈), 모바일 메모리 이상 없음
