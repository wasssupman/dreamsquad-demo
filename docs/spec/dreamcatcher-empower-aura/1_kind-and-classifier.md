# 1 — StatusFxKind.Empowered + 순수 ModifierAuraClassifier

## 목적

드림캐쳐 강화 오라의 판정 토대: 상태 kind `Empowered`(append-only) + `StatModifierSlot` 버퍼에서
드림캐쳐 출처(unit 0 의 `origin==Dreamcatcher`) 모디파이어가 활성인지 판정하는 순수 함수 + EditMode.

## 변경 대상

- `Assets/_Project/Scripts/Data/StatusFxKind.cs` — `Empowered = 2` append
- `Assets/_Project/Scripts/Battle/Effects/Modifiers/ModifierAuraClassifier.cs` — 순수 static
- `Assets/_Project/Tests/EditMode/ModifierAuraClassifierTests.cs` — EditMode

## 구현

1. **enum**: `StatusFxKind { Aggro=0, Sleep=1, Empowered=2 }` (append-only 계약).
2. **순수 classifier** — `HasActiveDreamcatcherModifier(NativeArray<StatModifierSlot>) → bool`:
   - `header.origin == Dreamcatcher` 슬롯만 골라 stat별 net 재집계(Additive/Multiplicative/Override).
   - net 이 base 에서 벗어나면 true. 없거나(드림캐쳐 슬롯 0) net=identity(revoke 중립화)면 false.
   - mul 스탯 base 1, `regenPerSec` base 0. `DamageVsCcMul`/`MaxHealthMul` 은 제외(조건부·비체감).
   - epsilon `1e-4`. 방향 무관(감속이든 증뎀이든 "강화" 취급 — 단일 kind).
   - 아키텍처-blind: NativeArray<StatModifierSlot>(POD)만, EntityManager/View 불요(CLAUDE.md 제약 10).
3. **테스트**(13 케이스): 드림캐쳐 버프 true / 드림스톤·시너지·on-place 제외 false / revoke(additive0·mult1) false /
   방향무관(감속) true / dmgTaken 감소 true / regen true / 드림스톤+드림캐쳐 공존 true / 드림스톤+revoke드림캐쳐 false /
   DamageVsCc 제외 false / empty false.

## 완료 기준

- [x] 컴파일 클린, 기존 Aggro/Sleep 무손실(append-only)
- [x] `ModifierAuraClassifierTests` EditMode **13/13 green**
- [x] 시각 변화는 unit 2(reconcile)~3(저작)에서
