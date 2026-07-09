# 1 — 카드 에셋 type 지정

## 목적

기존 카드 15장에 `type` 을 지정한다. 스탯 카드 = Squad, 이번 세션 메커니즘 5장 = Unit.

## 변경 대상

- 수정: `Assets/_Project/Data/Dreamcatcher/Card_*.asset` (유닛 5장만 실제 변경; 스쿼드 10장은 zero-init=Squad 라 무변동)

## 구현

- **Unit 지정(5장)**: `Card_BouncyBead`, `Card_Farewell`, `Card_LastFlame`, `Card_PokeNeedle`, `Card_Thornmail` → `type = Unit`. (전부 binding=Unit)
- **Squad(10장)**: 나머지 stat 카드는 zero-init 로 자동 Squad — 명시 불필요.
- 판정 근거: `type = (binding == Unit) ? Unit : Squad` 와 일치. AssetDatabase 로 유닛 5장만 SetDirty + Save.

## 완료 기준

- [x] 유닛 5장 type=Unit(BouncyBead/Farewell/LastFlame/PokeNeedle/Thornmail), 스쿼드 10장 type=Squad — 전체 집계 Squad=10 / Unit=5
- [x] `DeckRules.SquadCount` 가 기본 카탈로그에서 스쿼드 10 / 유닛 5 로 집계
- [x] 기본 덱 점검 결과: **`DreamcatcherDeck_Default` 는 새 규칙에 무효**(스쿼드 10/2). 규칙이 올바로 작동함을 실증. **콘텐츠 재구성은 게임 디자인 결정이라 후속(README) 으로 이관** — 이 unit 은 코드/타입만.

완료 확인: 2026-07-09 — 유닛 5장 지정, 집계 정확, 기본 덱 무효 확인(콘텐츠 후속). 이 문서와 동일 커밋.
