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

- [ ] 유닛 5장 type=Unit, 스쿼드 10장 type=Squad (인스펙터/로드 확인)
- [ ] `DeckRules.SquadCount` 가 기본 카탈로그에서 스쿼드 10 / 유닛 5 로 집계
- [ ] 기본 덱(`DreamcatcherDeck_Default`)이 새 규칙(스쿼드≤2)에 유효한지 점검 — 무효면 후속 콘텐츠 재구성으로 flag(이 unit 스코프 아님)
