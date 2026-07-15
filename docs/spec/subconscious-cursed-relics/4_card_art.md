# 4 — 저주 유물과 placeholder 카드 아트

## 목적

새 저주 유물 2장에 전용 아트를 연결하고, 문자 placeholder가 남아 있던 기존 드림캐쳐 3장을 현재 카드 아트 스타일의 완성 이미지로 교체한다.

## 변경 대상

- `Assets/_Project/Art/DreamcatcherCards/dreamcatcher_card_21.png` — 악몽의 여운
- `Assets/_Project/Art/DreamcatcherCards/dreamcatcher_card_22.png` — 끝을 보는 눈
- `Assets/_Project/Art/DreamcatcherCards/dreamcatcher_card_23.png` — 응축된 일격
- `Assets/_Project/Art/DreamcatcherCards/dreamcatcher_card_24.png(.meta)` — 재앙의 심장
- `Assets/_Project/Art/DreamcatcherCards/dreamcatcher_card_25.png(.meta)` — 금이 간 성배
- `Card_CalamityHeart.asset`, `Card_CrackedGrail.asset`

## 구현

모든 이미지는 기존 카드와 같은 `1024×1536` 세로 2:3 비율, Single Sprite, mipmap off로 import한다. 카드명이나 설명 텍스트는 이미지에 넣지 않고, 중앙 상징과 장식 프레임만으로 작은 손패에서도 효과를 구분한다.

- 21~23은 PNG만 교체해 기존 GUID와 카드 참조를 유지한다.
- 24~25는 신규 Sprite로 추가하고 각각 재앙의 심장과 금이 간 성배 SO에 연결한다.
- 기존 씬·카탈로그 배열·카드 UI는 변경하지 않는다.

## 완료 기준

- [ ] 5장 모두 1024×1536, Single Sprite, mipmap off다.
- [ ] 21~23의 meta GUID는 기존 값을 유지한다.
- [ ] 재앙의 심장과 금이 간 성배가 서로 다른 신규 Sprite를 참조한다.
- [ ] 5장에 placeholder 문구·제목·워터마크가 없다.
- [ ] Gift·손패·Inspect에서 잘림이나 빈 이미지 없이 표시된다.
