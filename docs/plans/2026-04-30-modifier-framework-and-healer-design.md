# Modifier Framework + Healer — Design (2026-04-30)

> 얇은 브레인스토밍 결과물. 구체 작업 단위와 계약은 `docs/spec/modifier-framework-and-healer/` 참조.

## 목표

ad-hoc 효과 컴포넌트(`DamageBoost`/`CooldownReduction`/`SynergyBuff`)를 producer-agnostic modifier framework 로 통합한다. 그 위에 힐러 defender 를 올려 framework 의 첫 사용 사례로 검증한다. buff/debuff 는 magnitude 부호로 표현 — 시스템 코드는 부호를 모른다.

## 핵심 결정

| 축 | 선택 | 비고 |
|---|---|---|
| 범위 | Stat 변조 + 적 Stat 디버프 통합 | CcEffect/DOT/필드 carrier 는 별도 레인 유지 |
| 데이터 모델 | 분리 buffer + `ModifierHeader` 임베딩 | C# 인터페이스 X (구현체가 데이터 형태 본질적으로 달라 추상화 표현력 손해. CLAUDE.md "구현체 2개 이상" 원칙) |
| Stack 감쇠 | 단일 타이머 + 매 적용 시 refresh | SO 별 `StackPolicy` enum 으로 향후 (per-stack/decay-tick) 확장 |
| 디스패치 | Producer 가 기존 채널에 직접 enqueue | dispatcher hop 없음 |
| 임계 의미 | Edge-triggered (`lastTriggeredStack`), multi-threshold 모두 발화 | SO 별 Consume 모드 선택. **파생 효과는 1프레임 지연** |
| StatKind 1차 | `DamageMul`, `AttackSpeedMul`, `DmgTakenMul`, `RegenPerSec` | `CooldownReduction` → `AttackSpeedMul` 통일. `DmgTakenMul` 은 target side 적용 |
| CombineOp | SO 별 명시 강제 | Mul/Add/Override 슬롯을 별도 합성 |
| StatModifier merge | key=`(source, stat, op)`, `remaining=max`, `magnitude=new` | 다른 stackId 는 새 슬롯 |
| dirty mark | `BuffStatsDirty: IEnableableComponent` | Add/Remove 비용 없음, archetype 안정 |
| 힐 메커니즘 | `IncomingHeal` pulse + `RegenPerSec` StatModifier | 별도 경로. RegenPerSec 는 BuffStats 직접 read (IncomingHeal 미경유) |
| AttackOutput | 다중 output 배열 (Damage/Heal/ApplyStat/ApplyStack) | producer 어댑터, framework 외부. 이번 spec 은 `DefenderUnitData` 만 |
| 힐러 targetMask | `Faction.Defender` 재사용 | `AllyDefender` 는 존재하지 않음. `AttackSystem` 의 self-skip 분기로 자가 힐 자동 방지 |
| 마이그레이션 | Adapter — Aggregate 가 legacy read → producer 이전 → legacy 제거 | 작업 단위 7/9 는 단일 커밋 원자성 |

## 시스템 흐름

```
Producer (AttackOutput / OnPlace / Zone / Projectile / StackThreshold)
   ↓ enqueue
Channel (NativeQueue singletons — Allocator.Persistent, BattleBridge lifecycle)
   ├─ StatModifierApplyEvents  (신규)
   ├─ StackModifierApplyEvents (신규)
   └─ EnemyCcEvents            (기존 재사용 — DOT/Stun 파생)
   ↓
ModifierApplySystem (드레인 → buffer 갱신, BuffStatsDirty enable)
   ├─ StatModifierSlot Buffer  ─→ StatModifierTickSystem ──┐
   └─ StackModifierSlot Buffer ─→ StackModifierTickSystem  │ (임계 도달 → 채널 enqueue)
                                          ↑                │  ※ 효과는 다음 프레임에 적용
                                          └────── 1프레임 지연 ───┘
                                                            ↓
                                  BuffStatsAggregateSystem (Dirty enabled 만 재계산)
                                                            ↓
                                  BuffStats 캐시 (write: Aggregate 만 / read: 모든 consumer)
                                                            ↓
                                  Consumer
                                  ├─ AttackSystem            (damageMul, attackSpeedMul)
                                  └─ DamageApplicationSystem (× dmgTakenMul, IncomingHeal drain, regenPerSec×dt)
```

## 핵심 책임 경계

- **Modifier Framework**: 부착·수명·합성·만료·캐시. Producer 가 누구인지 모른다. `BuffStats` 는 `BuffStatsAggregateSystem` 만 write, 다른 system 은 read-only.
- **AttackOutput**: 공격 hit 시점에 어떤 효과를 발화할지 데이터 명세. framework 외부의 producer 어댑터.
- **Consumer**: BuffStats 캐시만 read. raw buffer 보지 않는다 — 맥락 경계 유지.

## Spec 폴더

구체 작업 단위는 `docs/spec/modifier-framework-and-healer/README.md` (12 units).

## 후속 후보 (이번 spec 범위 밖)

1. `cc-effect-consolidation` — `EnemyAttackMovePause` → `CcEffect.Stun` 흡수, `EnemyCcEventsSingleton` → `EntityCcEvents` rename
2. `MoveSpeedMul` 도입 + `CcEffect.Slow` 정리
3. Aura defender (지속 영역 effect producer)
4. Projectile on-hit modifier 부착
5. Modifier UI 시각화
6. Dispel/Cleanse 채널
7. `AttackUnitData.outputs[]` — 적도 outputs 모델 도입
