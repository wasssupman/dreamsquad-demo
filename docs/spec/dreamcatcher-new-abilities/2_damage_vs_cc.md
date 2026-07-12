# 2 — DamageVsCc (shatter_hymn)

## 목적

CC(Stun/Sleep/Impulse/DoT) 걸린 적에게 아군 데미지 +% 를 주는 축(axis) 스탯을 신설·소비. frost_arrow/ember_bite 가 건 CC 와 시너지. critic HIGH 2건(base-1 무적 방지, 투사체 경로 포함) 반영.

## 변경 대상 / 구현

- `ModifierStatsAggregateSystem.cs`: 6번째 stat `damageVsCcMul` 집계(base 1, MulStat clamp). 슬롯 없으면 `vMul=1` → CombineMul 1.
- `BattleBridge.cs` (ModifierStats add-site ×2, :3358/:4167): `damageVsCcMul = 1f` 명시. **필수** — `ModifierStatsDirty` 가 disabled 로 추가돼(무-모디파이어 유닛은 집계가 영영 안 돎) add-site 값이 그대로 고정. 0 이면 CC 적 데미지 0 무적 버그.
- `BattleBridge.Dreamcatcher.cs` `MapDcEffect`: `CardBuffKind.DamageVsCc → StatKind.DamageVsCcMul`, mult=1+percent/100.
- `AttackSystem.cs`:
  - RESOLVE 초입: `attackerVsCc = HasComponent ? damageVsCcMul : 1f`.
  - 투사체 bake 경로(Damage output): 발사 시점 `bestTarget` 활성 CC 시 `amount *= attackerVsCc` — **궁수 콤보 살림**(critic HIGH).
  - 멜리/직접 output(Damage): `hitTarget` 별 활성 CC 시 `dmg *= attackerVsCc` (IncomingDamage·TryCredit·log 동일 값).
  - `AnyActiveCc(buf)` 정적 헬퍼: remaining>0 CcEffect 존재 판정(`ccActionLookup` = BufferLookup<CcEffect> 재사용).

## 완료 기준

- [x] 4개 어셈블리 `dotnet build` 오류 0개.
- [x] base-1 보장 코드: add-site `damageVsCcMul=1f` + 집계 CombineMul(부재→1) + read `HasComponent?…:1f`. 무적 회귀 경로 차단.
- [x] shatter 활성 시 CC 적 공격 데미지 ×attackerVsCc — 투사체 bake·멜리 양 경로 배선.
- [x] 비-CC 적/무-shatter 는 배율 미적용(gate: `attackerVsCc!=1 && AnyActiveCc`).
- [ ] PlayMode/EditMode assertion — unit 3 통합(궁수 투사체 경로 포함).

확인: 2026-07-13 — dotnet build 컴파일 검증. (Unity PlayMode 미실시.)
