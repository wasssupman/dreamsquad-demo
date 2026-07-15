# 0 — ModifierOrigin 프레임워크 (모디파이어 출처 1급 태깅)

## 목적

스탯 모디파이어가 **어디서 왔는지**(드림캐쳐/드림스톤/시너지/…)를 슬롯 단위 데이터로 지니게 한다.
크기·stat 이 같은 모디파이어도 출처로 구분 가능해진다. 이번 오라가 첫 소비자이고, 향후 dispel/UI/로깅이
재사용한다. stackId/handle 로 출처를 **추측**하던 방식(드림캐쳐·드림스톤이 stackId 100+ 공유라 구분 불가)을
대체하는 정공법.

## 변경 대상

- `Assets/_Project/Scripts/Battle/Effects/Modifiers/ModifierTypes.cs` — `ModifierOrigin` enum + `ModifierHeader.origin`
- `Assets/_Project/Scripts/Battle/Effects/Modifiers/StatModifierApplyEvents.cs` — `StatModifierApplyEvent.origin`
- `Assets/_Project/Scripts/Battle/Effects/Modifiers/ModifierApplySystem.cs` — event.origin → slot.header.origin 전파
- `Assets/_Project/Scripts/Bridge/BattleBridge.cs` — `EnqueueStatModifier(...origin)` + 브리지 생산자 태깅
- 생산자 ECS 시스템 7곳: `AttackSystem`(OnHit) · `ProjectileHitSystem`(OnHit) · `BossPeriodicTriggerSystem`(Boss) ·
  `HealthThresholdSystem`(HealthThreshold) · `DamageApplicationSystem`(Dreamcatcher OnKill 트리거) ·
  `ZoneApplySystem`(Zone) · `StackModifierTickSystem`(Stack)

## 구현

1. **enum** (byte, append-only, 실존 생산자 매핑 — 임의 추가 금지):
   `Unspecified, OnPlace, Skill, Synergy, Dreamcatcher, Dreamstone, Tile, Zone, Boss, HealthThreshold, OnHit, Stack`
2. **저장 위치 = `ModifierHeader.origin`** (stat/stack 슬롯 공용 메타 struct). `StatModifierApplyEvent.origin` 이
   운반하고 `ModifierApplySystem.ApplyStat` 의 슬롯 생성/머지 3곳이 `header.origin = ev.origin` 전파.
3. **origin 은 머지 키 아님.** 머지 키는 `(source, stat, op, stackId)` 유지. origin 은 순수 메타데이터 —
   같은 슬롯이 origin 만 달라 중복되지 않도록.
4. **생산자 전수 태깅.** `EnqueueStatModifier`/wrapper 에 origin 파라미터(기본값 없음 → 누락 시 컴파일 에러).
   브리지: on-place=OnPlace, skill=Skill, synergy=Synergy, dreamstone=Dreamstone, effect tile=Tile,
   드림캐쳐 카드/placement-aura=Dreamcatcher. ECS: 위 목록.
5. **StackModifierSlot** 의 header.origin 은 Unspecified 로 둔다(스택은 스탯 오라 비대상, StackModifierApplyEvent
   에 origin 미추가 — 스코프 최소).

## 완료 기준

- [x] 컴파일 클린 — 19개 생산자 전부 origin 지정(누락 시 컴파일 에러로 강제)
- [x] 머지 키에 origin 미포함 확인(`ModifierApplySystem`)
- [x] 전체 EditMode 회귀 무손실 (779 pass / 2 기존 skip)
- 계약: origin 은 슬롯 단위로만 유효(집계 `ModifierStats` 에선 소실) — 소비처는 슬롯 버퍼를 읽는다.

사용자 확인 2026-07-15(실플레이: 드림스톤-only 유닛 오라 미표시 = origin 분리 실증).
