# 기존 구현 → Mono 마이그레이션 노트

> 참고용 부록. 본 스펙: [정적 스탯 구조](./1-static-stat-structure.md) · [스탯 모디파이어 시스템](./2-stat-modifier-system.md)

이 문서는 **이 프로젝트의 기존 ECS/DOTS 구현**을 Mono 기반 재설계 스펙으로 옮길 때의 변경점만 담는다. 두 본 스펙은 엔진 비의존 구현 스펙이므로 이 비교를 포함하지 않는다 — ECS 맥락이 필요한 사람만 여기를 본다.

---

## 1. 개념 대응표 (ECS → Mono)

| ECS (기존 프로젝트) | Mono 스펙 표현 |
|---|---|
| `DefenderUnitData` / `AttackUnitData` (ScriptableObject) | `UnitStatBlock` (공용/전용 블록 합성) |
| `ModifierStats` (effective 캐시 Component) | `EffectiveStats` (StatId→Final 캐시) |
| `DynamicBuffer<StatModifierSlot>` | `List<StatModifier>` |
| `ModifierStatsAggregateSystem` | `Recalculate()` 메서드 |
| `StatModifierTickSystem` (만료) | `Tick()` |
| `NativeQueue<StatModifierApplyEvent>` + `ModifierApplySystem` | 직접 `AddModifier()` 호출 |
| `StackModifierSlot` + `ThresholdRule` SO | `StatusStackController` + `StackThresholdSO` |
| `BattleBridge` 게이트웨이 | 불필요 (직접 참조/메서드 호출) |
| `IComponentData` / `ISystem` / Burst | 순수 C# 클래스/struct + 고정 틱 루프 |

---

## 2. 정적 구조 변경점

| 항목 | 기존 (ECS) | 재설계 (Mono) |
|---|---|---|
| 레거시 공격값 | `attackDamage` + `outputs[]` 이중 표현 | `AttackPower` 단일 — 이중표현 제거 |
| 정적 구조 | 스탯+hazard+aggro+Spine+연출 혼재(god SO) | 공용/전용 블록 합성 + 프레젠테이션 분리 |
| 수 표현 | float | 고정소수점 정수(scale 1000) |
| 스탯 식별 | 고정 5필드 `ModifierStats` | 일반화된 `StatId` enum(임의 스탯 확장) |
| 내구 | 실드 → 체력 | 동일 유지 — 방어력 없음 |

---

## 3. 모디파이어 로직 변경점

| 항목 | 기존 (ECS) | 재설계 (Mono) |
|---|---|---|
| 비율 합성 | `Πmul` 승산 누적 — `(1+Σadd)·Πmul` | **가산 후 1회 승산** — `(Base+ΣFlat)×(BASE+ΣPercent)`. 10%+10%=20% |
| 모디파이어 대상 | 고정 5필드 `ModifierStats{damageMul,...}` | 일반화된 **StatId별 Flat/Percent** |
| 수 표현 | float | **고정소수점 정수**(scale 1000) — 크로스플랫폼 불일치 차단 |
| 합산 순서 | 버퍼 삽입 순서 의존 가능 | **소스 ID 정렬** 후 합산 — 환경 무관 동일 |
| 적용 경로 | NativeQueue 채널 + Apply 시스템(1프레임 지연) | 직접 `AddModifier()` 메서드 호출 |
| 실행 기반 | ISystem/IComponentData/Burst/BattleBridge | 순수 C# + 고정 틱 `Tick()` |

> **가장 큰 변경**은 비율 합성 규약이다. ECS는 `Πmul`(승산 누적)이라 10%+10% 버프가 21% 증가였지만, PART B 규약은 가산 후 1회 승산이라 20% 증가다. 결정성·디버깅·밸런싱을 위해 후자를 채택한다.
