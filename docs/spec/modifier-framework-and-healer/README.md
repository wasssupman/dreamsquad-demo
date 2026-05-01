# modifier-framework-and-healer

상태: 완료 2026-05-01 (handoff `12_handoff_summary.md` 참조)
설계 문서: `docs/plans/2026-04-30-modifier-framework-and-healer-design.md`

## 목표

1. 기존 ad-hoc 효과 컴포넌트(`DamageBoost`/`CooldownReduction`/`SynergyBuff`)를 통합 modifier framework 로 일반화한다. buff/debuff 는 magnitude 부호로 표현, 시스템 코드는 부호를 모른다.
2. 첫 사용 사례로 힐러 defender 를 도입한다 — N초마다 주변 3타일 이내 ally defender 회복. 다른 defender 와 동일한 attack 애니메이션 트리거를 사용.

## 공통 원칙 (feature-wide 계약)

- **Producer-agnostic framework**: `ModifierApplySystem` 은 누가 enqueue 했는지 모른다. `AttackOutput`, `OnPlaceOutput`, `ThresholdRule` 등 모든 producer 가 동일한 채널로 부착한다.
- **분리 buffer**: `StatModifierSlot` / `StackModifierSlot` 두 `IBufferElementData`. 공통 헤더는 임베딩 컨벤션(`ModifierHeader { remaining, source, stackId }`), C# 인터페이스 X.
- **인터페이스 미도입 이유**: `IModifier` 인터페이스를 두지 않는다. CLAUDE.md "인터페이스는 구현체 2개 이상일 때만" 원칙 적용. 현재 modifier 분류는 Stat/Stack 두 종류이지만 *데이터 형태가 본질적으로 달라* C# 추상으로 묶을 때 표현력 손해가 큼. struct 임베딩 컨벤션(`ModifierHeader`) 으로 공통성 표현하고 system 분리로 다형성 해소.
- **Stat 합성 정책**: `CombineOp` 를 SO 별 명시 강제. Aggregator 가 stat 별 Mul/Add/Override 슬롯을 분리 합산: `final = (base + Σadd) * Πmul * (override_max if any else 1)`.
- **StatModifier merge key**: 같은 `(target, source, stat, op, stackId)` 의 재적용은 같은 슬롯 refresh — `remaining = max(old, new)`, `magnitude = new`. 다른 `stackId` 는 새 슬롯. `stackId` 는 같은 source 의 다중 인스턴스 분리(매우 드문 케이스) 용도. **Producer 가 enqueue 시 부여**, 디폴트 0.
- **StackModifier merge key**: 같은 `(target, source, kind)` 의 재적용은 같은 슬롯 — `stackCount = min(maxStack, stackCount + countDelta)`, `remaining = perAppDuration` refresh.
- **Stack 감쇠 디폴트**: 단일 타이머 + 매 적용 시 `remaining = perAppDuration` refresh (S1). SO 의 `StackPolicy` enum 으로 향후 (per-stack/decay-tick) 확장 여지.
- **임계값 의미 (edge)**: `StackModifierTickSystem` 이 `lastTriggeredStack` 캐시로 edge 검출. SO 의 `ThresholdRule.mode` 로 `Edge` 또는 `Consume` 선택. **multi-threshold 통과 시 통과한 모든 threshold 가 1회씩 발화** (예: stack 4→7 점프 시 5/6/7 threshold 가 정의되어 있으면 셋 다 발화). 디폴트는 Edge.
- **Stack 임계 파생은 1프레임 지연**: `StackModifierTickSystem` 이 임계 도달 시 `StatModifierApplyEvents` / `EnemyCcEvents` 채널에 enqueue 하는 효과는 **다음 프레임에 적용**된다. 같은 프레임 처리를 위한 우회(2-pass ApplySystem 등) 는 두지 않는다 — 재진입 루프 위험 회피. 60fps 기준 ~16ms, 게임 체감 영향 무시 가능.
- **소비자는 ModifierStats 캐시만 read**: AttackSystem / DamageApplicationSystem 등은 raw modifier buffer 를 보지 않는다. 맥락 경계 유지.
- **ModifierStats write 권한**: `ModifierStatsAggregateSystem` 만이 `ModifierStats` 를 갱신한다. 다른 system 은 모두 read-only. Aggregate 는 dirty 한 entity 만 재계산.
- **dirty mark 메커니즘**: `ModifierStatsDirty` 는 `IEnableableComponent`. `ModifierApplySystem` / `StatModifierTickSystem` 이 enable, `ModifierStatsAggregateSystem` 이 처리 후 disable. Add/Remove 비용 없음, archetype 안정.
- **AttackOutput 은 producer 어댑터**: framework 일부가 아니다. attack hit 시점에 어떤 채널로 enqueue 할지 명세하는 데이터. AttackSystem 이 outputs 순회하며 kind 별 분기. **이번 spec 에서는 `DefenderUnitData.outputs[]` 만 도입** — `AttackUnitData` (적) 도입은 후속 spec.
- **힐 통로 두 개 — 별도 경로**: `IncomingHeal` Buffer 는 `AttackOutput.Heal` 의 즉시 펄스 전용. `RegenPerSec` StatModifier 는 `DamageApplicationSystem` 이 매 프레임 `ModifierStats.regenPerSec * dt` 를 직접 가산 — IncomingHeal 미경유. 두 경로는 합산되지만 거치는 데이터 흐름이 다름.
- **NativeQueue singleton 은 ECS singleton**: CLAUDE.md "Manager 싱글톤은 GameManager 1개만" 제약과 무관. ECS singleton component 는 별도 카테고리이며 기존 8개 채널(`EnemyCcEvents`, `DefenderAttackEvents`, `GoalReachedEvents`, `MeteorBurstEvents`, `ProjectileHitEvents`, `HazardRuntimeEvents`, `DefenderDeathEvents`, `HazardDestroyedEvents`) 과 동일 패턴.
- **legacy 이중 합성 기간**: 마이그레이션 동안 `ModifierStatsAggregateSystem` 이 신규 buffer + legacy 3개 컴포넌트 모두 read. `AttackSystem` 은 ModifierStats 만 read 로 전환. legacy 정의 제거가 마지막 단계.

## 채널 (NativeQueue singletons)

| 채널 | 신규/기존 | Producer 군 | Consumer | Payload |
|---|---|---|---|---|
| `StatModifierApplyEventsSingleton` | 신규 | Attack hit(`ApplyStat` output), OnPlace, Zone, Projectile, StackTick(Stat 파생) | `ModifierApplySystem` | `{ Entity target, StatKind stat, CombineOp op, float magnitude, float duration, Entity source, ushort stackId }` |
| `StackModifierApplyEventsSingleton` | 신규 | 위와 동일 | `ModifierApplySystem` | `{ Entity target, StackKind kind, byte countDelta, float perAppDuration, Entity source }` |
| `EnemyCcEventsSingleton` | 기존 재사용 | StackTick(DOT/Stun 파생) + 기존 producer | `CcApplySystem` | (무변경) |

신규 두 채널은 기존 8개 채널과 동일 lifecycle 패턴 답습: `BattleBridge` field 보유 + `StartBattle()` 에서 `new NativeQueue<T>(Allocator.Persistent)` create + singleton entity 생성 + `EndBattle()`/`CleanupBattle()` 에서 entity destroy + queue dispose.

## StatKind 1차 셋

| Kind | 의미 | 기본 합성 | 소비처 (적용 시점) |
|---|---|---|---|
| `DamageMul` | 발사 데미지 곱 | Multiplicative | `AttackSystem` (attacker side, hit 시 `ModifierStats` read) |
| `AttackSpeedMul` | 공격 속도 (`cooldown = base / mul`) | Multiplicative | `AttackSystem` (attacker side, cooldown reset 시) |
| `DmgTakenMul` | 받는 데미지 곱 | Multiplicative | `DamageApplicationSystem` (target side, IncomingDamage drain 시 `total *= dmgTakenMul`) |
| `RegenPerSec` | 초당 회복 | Additive | `DamageApplicationSystem` (매 프레임 `Health += ModifierStats.regenPerSec * dt`, IncomingHeal 미경유) |

`MoveSpeedMul` 은 후속 spec.

## AttackOutput 모델 (producer 어댑터)

```csharp
public enum AttackOutputKind { Damage, Heal, ApplyStat, ApplyStack }

[Serializable] public struct AttackOutput {
    public AttackOutputKind kind;
    public float magnitude;        // Damage/Heal 양 / Stat magnitude / Stack countDelta
    public float duration;         // ApplyStat / ApplyStack 만 의미
    public StatKind stat;          // ApplyStat 만
    public CombineOp op;           // ApplyStat 만
    public StackKind stackKind;    // ApplyStack 만
}
```

`DefenderUnitData.outputs[]` 가 hit 한 target 마다 적용. target 결정은 기존 `attackRange`/`attackTargetCount`/`targetMask` 가 담당. 자기 자신은 `AttackSystem` 의 self-skip 분기 (`targetEntities[i] == attackerEntity`) 로 자동 제외.

## ECS 시스템 구성 (Effects 맥락)

```
SimulationSystemGroup:
  ModifierApplySystem            (드레인 → 두 buffer 갱신, ModifierStatsDirty enable)
  StatModifierTickSystem         (tick + 만료 시 슬롯 제거 + ModifierStatsDirty enable)
  StackModifierTickSystem        (tick + 만료 + 임계 → EnemyCcEvents/ModifierApply enqueue, 효과는 1프레임 지연)
  ModifierStatsAggregateSystem       (ModifierStatsDirty enabled entity 만 재계산, 처리 후 disable)
  CcApplySystem (기존)           EnemyCcEvents 드레인 → CcEffect Buffer 갱신
  AttackSystem (수정)            outputs[] 순회 + ModifierStats.damageMul/.attackSpeedMul 곱
  DotApplySystem (기존, 무변경)
  DamageApplicationSystem (수정) IncomingDamage drain (× ModifierStats.dmgTakenMul) + IncomingHeal drain + RegenPerSec*dt 가산
  EffectTickSystem (마이그레이션 단계 동안 유지, 9번 단위에서 정리)
```

## 작업 단위

| # | 파일 | 목적 |
|---|---|---|
| 0 | `0_data_model.md` | struct/enum 정의: `Slot`, `Header`, `ModifierStats`, `ModifierStatsDirty`(`IEnableableComponent`), `StatKind`, `StackKind`, `CombineOp`, `AttackOutputKind` 등 + 빈 system 골격 stub (있으면 컴파일 통과까지) |
| 1 | `1_apply_channels_and_lifecycle.md` | 두 신규 NativeQueue singleton 정의 + `BattleBridge` 의 lifecycle (기존 8개 채널 패턴 답습: field + `StartBattle` 에서 `Allocator.Persistent` create + singleton entity + `EndBattle`/`CleanupBattle` 에서 entity destroy + queue dispose) |
| 2 | `2_modifier_apply_system.md` | `ModifierApplySystem` — 두 채널 드레인. **StatModifierSlot merge key**: `(source, stat, op)` 동일 시 같은 슬롯 refresh — `remaining = max(old, new)`, `magnitude = new`. 다른 `stackId` 면 새 슬롯. **StackModifierSlot merge key**: `(source, kind)` 동일 시 같은 슬롯, `stackCount = min(maxStack, stackCount + countDelta)`, `remaining = perAppDuration`. 각 갱신 시 `ModifierStatsDirty` enable |
| 3 | `3_stat_tick_and_aggregate.md` | `StatModifierTickSystem` (tick + 만료 시 슬롯 제거 + dirty enable) + `ModifierStatsAggregateSystem` (dirty enabled entity 만 재계산, 처리 후 disable, write 권한 단독) |
| 4 | `4_stack_tick_and_thresholds.md` | `StackModifierTickSystem` + `StackModifierSO` + `ThresholdRule` + edge 검출(`lastTriggeredStack`). **multi-threshold 통과 발화 정책**: stack 이 4→7 점프 시 정의된 모든 통과 threshold 1회씩 발화. **파생 효과 1프레임 지연 명시** (다음 프레임 ApplySystem/CcApplySystem 이 처리). EditMode 테스트: 같은 프레임 stack 6 도달 시 효과는 다음 프레임에 관찰됨 |
| 5 | `5_attack_output_and_attack_system.md` | `AttackOutput[]` 모델, **`DefenderUnitData.outputs[]` 만 도입** (`AttackUnitData` 는 후속 spec). `AttackSystem` outputs 순회 분기 (Damage→IncomingDamage, Heal→IncomingHeal, ApplyStat→StatModifierApply, ApplyStack→StackModifierApply) + `ModifierStats.damageMul`/`attackSpeedMul` 곱. 기존 단일 `attackDamage` 는 `outputs = [{Damage, attackDamage}]` 자동 변환으로 호환 유지 |
| 6 | `6_incoming_heal_and_damage_application.md` | `IncomingHeal` Buffer 신설. `DamageApplicationSystem` 수정: ① IncomingDamage drain 시 `total *= ModifierStats.dmgTakenMul` (target side), ② IncomingHeal drain 후 `Clear()` 호출 보장 (Heal pulse), ③ 매 프레임 `Health += ModifierStats.regenPerSec * dt` 직접 가산 (RegenPerSec, IncomingHeal 미경유). EditMode: 2 프레임 연속 실행 시 IncomingHeal 1회만 적용 (누적 방지) |
| 7 | `7_aggregate_legacy_adapter.md` | **단일 커밋 원자성**: ① `ModifierStatsAggregateSystem` 가 legacy 3개 컴포넌트(`DamageBoost`/`CooldownReduction`/`SynergyBuff`) 도 read → `ModifierStats` 에 합성, ② `AttackSystem` 의 `damageBoostLookup`/`cooldownReductionLookup`/`synergyLookup` 제거 + `ModifierStats.damageMul`/`attackSpeedMul` 으로 대체. **이 두 변경은 한 커밋**. 중간 상태 커밋 금지. PlayMode 회귀 기준: ① `DamageBoost` 가진 defender 의 발사 데미지 = base × multiplier, ② `CooldownReduction` 적용 시 cooldown 단축 동작, ③ 인접 동족 SynergyBuff 데미지 곱 동작 — 셋 다 변경 전 동등 |
| 8 | `8_migrate_legacy_producers.md` | producer 측 마이그레이션. **(a)** `BattleBridge.RecomputeSynergyFor()` + `EffectSpawner.SetSynergy/RemoveSynergy` → 인접 동족 카운트 결과를 `SynergyBuff` 컴포넌트 add/remove 대신 `StatModifierApplyEvents` enqueue (`DamageMul, Multiplicative, source=defenderEntity, duration=∞ 또는 인접 갱신마다 재발행`) 로 변경. **(b)** OnPlace 효과 (`BoostNearby`/`ReduceSkillCooldown` 등의 `EffectSpawner.ApplyDamageBoost`/`ApplyCooldownReduction` 호출) → 동일 채널로 변경. **dead 메서드는 9번에서 제거**. EditMode: 마이그레이션 후에도 ModifierStats 결과가 변경 전과 동일 |
| 9 | `9_remove_legacy_components.md` | **단일 커밋**: ① `DamageBoost`/`CooldownReduction`/`SynergyBuff` 컴포넌트 정의 제거, ② `EffectTickSystem` 의 해당 분기 제거, ③ `ModifierStatsAggregateSystem` 의 legacy read 제거 (7번에서 추가한 부분), ④ `EffectSpawner` 의 dead 메서드(`SetSynergy`/`RemoveSynergy`/`ApplyDamageBoost`/`ApplyCooldownReduction`) 제거. EditMode: legacy 3개 없이 ModifierStats 정확히 계산. PlayMode 회귀: 모든 OnPlace 효과 + 시너지 + 기존 buff 동작이 마이그레이션 전과 동일 |
| 10 | `10_healer_authoring.md` | 힐러 SO/프리팹/Spine + `outputs = [{Heal, magnitude=15}]` + **`targetMask = (int)Faction.Defender`** (`Faction.AllyDefender` 는 존재하지 않음). 자가 힐 방지는 별도 처리 불필요 — `AttackSystem` 의 self-skip 분기 (`targetEntities[i] == attackerEntity`) 가 자동 처리. 힐러가 다른 힐러를 회복하는 것은 의도된 동작. 시각 검증: 공격 애니메이션 트리거가 다른 defender 와 동일 (`UnitAttackVisualEvents` 큐 발화) + Heal pulse 시점에 ally HP 즉시 증가 |
| 11 | `11_tests_and_handoff_summary.md` | EditMode 단위 테스트: ① StatModifier merge key (같은 source/stat/op refresh, 다른 source 새 슬롯), ② Stack edge multi-threshold 발화 (4→7 점프 시 5/6/7 모두), ③ ModifierStats 합성 식 (`(base + Σadd) * Πmul`), ④ AttackOutput 분기 (4 종 kind 별 정확한 채널 enqueue). PlayMode 검증: Stack 임계 파생은 **1프레임 지연 전제** 기대값. handoff |

## 의존 그래프

```
0 → 1 → 2 → {3, 4 병렬}
{3, 4} → 5 → 6 → 7 → 8 → 9 → 10 → 11
```

## 후속 후보 (이번 spec 범위 밖)

1. **cc-effect-consolidation** — `EnemyAttackMovePause` → `CcEffect.Stun` 흡수. `EnemyCcEventsSingleton` → `EntityCcEvents` rename. defender 도 `CcEffect` 받기. `AttackSystem` 의 `enemyPauseLookup`/`!isDefender` 분기 제거.
2. **MoveSpeedMul + CcEffect.Slow 정리** — defender 이동 도입 시점 또는 cc-effect-consolidation 과 함께.
3. **Aura defender** — 지속 영역 effect producer (`AuraOutput[]` + `AuraApplySystem`). framework 변경 0줄.
4. **Projectile on-hit modifier** — `ProjectileResolveSystem` 이 ModifierApply 채널 enqueue. 화염 화살, 빙결 화살, 둔화 투사체.
5. **Modifier UI 시각화** — defender HUD 아이콘 + 적 머리 위 디버프 표시. `ModifierStats`/buffer read-only view.
6. **Dispel/Cleanse** — ModifierBuffer 슬롯 제거 채널, CombineOp 별 면역 정책.
7. **AttackUnitData.outputs[]** — 적도 outputs 모델 도입. defender 에 디버프 거는 적 도입 시점.
