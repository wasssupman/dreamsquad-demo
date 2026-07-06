# 스탯 모디파이어 시스템 스펙

> MonoBehaviour 기반 · ECS 비의존 · 기준 문서: 기획 PART B
> 자매 문서: [유닛 정적 스탯 구조](./1-static-stat-structure.md)

이 문서는 **버프/디버프 모디파이어 로직 + Final 연산 + 결정성**의 단일 source of truth다. 정적 스탯 구조(레이어·카탈로그·StatId·내구)는 자매 문서를 전제로 한다.

특정 실행 프레임워크(ECS/DOTS 등)에 의존하지 않으며, 순수 C# + 고정 틱 시뮬으로 표현한다.

---

## 0. 개요 & 범위

### 0.1 정의한다
- **모디파이어 데이터 모델** (Flat/Percent, source, duration, stack)
- **적용·갱신·만료·중첩** 규칙
- **Final 집계 규약** (고정소수점, 가산비율 1회 승산, 소스 ID 정렬, 단일 나눗셈 헬퍼, 캡)
- **상태이상 스택** 선택 모듈
- **드림캐쳐 경계** (단방향 의존)
- **결정성 체크리스트**

### 0.2 위임한다 / 전제한다
- 스탯 식별자·카탈로그·내구 → **자매 문서 「정적 스탯 구조」** 전제
- 구체 수치·확률·계수·캡 값 → 밸런싱 spec
- 효과·시너지·조건 슬롯의 의미 → 드림캐쳐 spec

### 0.3 불변 원칙 (PART B 승계)
| 원칙 | 규칙 |
|---|---|
| 수 표현 | 고정소수점 정수, 스케일 `1000` (소수점 3자리) |
| 비율 합성 | **가산 후 1회 승산.** 10% + 10% = 20% (1.1×1.1=21% 금지) |
| 합산 순서 | 모디파이어를 **소스 ID로 정렬** 후 합산 |
| 나눗셈 | 단일 헬퍼(`FixedMath.Div`) + truncate toward zero 하나로 통일. 직접 `/` 금지 |
| 시간 | 고정 틱(예 1/30s). `deltaTime` 가변 틱 금지 |
| 확률 발동 | **전면 배제.** PRNG는 맵/웨이브 시드성 요소 한정, 스탯 레이어 미사용 |

---

## 1. 아키텍처 (모디파이어 흐름)

모든 박스는 **일반 C# 객체**이고, 화살표는 메서드 호출이다 — 큐·시스템·월드 없음.

```
효과 레이어              UnitStats.AddModifier()      dirty=true      고정 틱마다
(드림캐쳐·특성·유닛효과) →  Flat/Percent, source,    →            →  dirty 시 Recalculate() 1회
모디파이어 생산자          duration                                   → EffectiveStats 갱신
```

모든 적용은 효과 레이어가 `AddModifier()`를 직접 호출하는 것으로 끝난다. 적용 시점(프레임)과 집계 시점(고정 틱 `Recalculate`)이 분리돼 있어, 같은 틱에 들어온 다중 모디파이어가 일관되게 합산된다.

---

## 2. 모디파이어 모델

### 2.1 데이터
```csharp
public enum ModifierMode : byte { Flat, Percent }   // Override는 §2.5 선택 확장

public struct StatModifier
{
    public StatId  stat;          // 대상 스탯 (자매 문서 §4)
    public ModifierMode mode;     // Flat(상수) / Percent(비율, BASE=1000 단위)
    public int     value;         // 고정소수점. Flat=정수×1000, Percent=10%→100
    public int     remainingTicks;// 만료까지 남은 틱. INFINITE = 영구
    public int     sourceId;      // 결정적 정렬 키 (효과/시너지/특성 고유 ID)
    public ushort  stackId;       // 동일 소스의 다중 인스턴스 구분(기본 0)
}
```

### 2.2 적용 — refresh-or-add
병합 키 = `(sourceId, stat, mode, stackId)`. 같은 키가 있으면 **갱신**, 없으면 추가.
```csharp
void AddModifier(StatModifier m)
{
    int i = FindByKey(m.sourceId, m.stat, m.mode, m.stackId);
    if (i >= 0) {
        var e = modifiers[i];
        e.remainingTicks = Max(e.remainingTicks, m.remainingTicks); // 긴 쪽 유지
        e.value = m.value;                                          // 최신 크기로
        modifiers[i] = e;
    } else {
        modifiers.Add(m);
    }
    dirty = true;
}
```

### 2.3 만료 — 고정 틱
```csharp
void Tick()   // 고정 틱마다 1회 (예: 1/30s)
{
    bool changed = false;
    for (int i = modifiers.Count - 1; i >= 0; i--) {
        var m = modifiers[i];
        if (m.remainingTicks == INFINITE) continue;
        if (--m.remainingTicks <= 0) { modifiers.RemoveAt(i); changed = true; }
        else modifiers[i] = m;
    }
    if (changed) dirty = true;
    if (dirty) { Recalculate(); dirty = false; }
}
```

### 2.4 중첩 규칙
- 동일 효과 중첩은 **기본 누산**(Flat은 합, Percent는 합) — §3 공식이 자연히 처리.
- 서로 다른 소스의 같은 스탯 버프는 각각 별도 슬롯으로 공존.
- 중첩 한도·우선순위 수치는 밸런싱 위임. 결과는 항상 §3의 캡으로 보호.

### 2.5 Override (선택 확장)
기획 PART B는 Flat/Percent만 요구한다. 기존 ECS 모델에 있던 `Override`(강제 설정)는 **지금 도입하지 않는다.** "강제 고정값" 요구가 실제로 생기면 `ModifierMode.Override` 추가 + "Override 존재 시 Flat/Percent 무시하고 max(override) 채택" 규칙으로 확장한다. YAGNI — 기본 모델에서 제외.

---

## 3. Final 계산 규약

### 3.1 핵심 공식
```
Final = clamp(
    FixedMath.Div( (Base + ΣFlat) * (BASE + ΣPercent), BASE ),
    cap.min, cap.max
)

// BASE = 1000 (= 100%)
// Flat 먼저 가산 → Percent 합산 후 1회 승산 → 클램프
```

> **가산 후 1회 승산 (승산 누적 금지)** — 10% + 10% 버프는 `(BASE + 100 + 100) = 1200` → 20% 증가. `1.1 × 1.1 = 21%`가 아니다. 추적·디버깅·밸런싱이 압도적으로 쉬워지고 곱셈 순서 의존성이 사라진다.

### 3.2 결정적 합산 — 소스 ID 정렬
```csharp
void Recalculate()
{
    foreach (StatId s in modifiableStats) {
        long flat = 0, pct = 0;
        // 소스 ID(그다음 stackId)로 정렬한 뷰에서 합산 → 환경 무관 동일 결과
        foreach (var m in ModifiersFor(s).OrderBy(m => m.sourceId).ThenBy(m => m.stackId)) {
            if (m.mode == ModifierMode.Flat) flat += m.value;
            else                             pct  += m.value;   // Percent
        }
        long baseVal = prototype[s];
        long final = FixedMath.Div((baseVal + flat) * (BASE + pct), BASE);
        effective[s] = (int)FixedMath.Clamp(final, cap[s].min, cap[s].max);
    }
}
```

> **나눗셈이 결정성의 진짜 함정** — 깨지는 건 곱셈이 아니라 정수 나눗셈의 나머지 처리다. 직접 `/` 금지, 전 코드가 `FixedMath.Div`(truncate toward zero) **하나**만 경유한다.

### 3.3 정수 스탯 처리
`ProjectileCount`, `AttackTargetCount` 같은 정수 스탯도 동일 고정소수점으로 계산하되, **소비 시점에 `/1000` 후 truncate**해 정수로 환산한다. 내부 누적은 항상 스케일된 정수로 유지.

### 3.4 캡 테이블
| StatId | min | max | 근거 |
|---|---|---|---|
| `Cost` | 0 | TBD | 코스트 ≥ 0 (배치 코스트 감소 효과의 하한) |
| `AttackSpeed` | TBD | TBD | 공속 상한 (틱 분해능 보호) |
| `Health` / `AttackPower` | 0 | TBD | 음수 방지 |
| *특성 합산 천장* | — | **+15%** | 스쿼드 특성 P2W 하드캡(PART A.6.3). 스탯 시스템이 강제할 글로벌 상한 |

구체 수치는 밸런싱 spec. 이 시스템은 **캡을 적용하는 메커니즘**만 보장한다.

---

## 4. 상태이상 스택 (선택 모듈)

기획상 "속성/속성별 스택"은 미래 확장이다. 코어 모디파이어 엔진과 **분리된 모듈**로 둔다 — 도입 전까지 없어도 코어가 성립한다. 기존 ECS의 스택/임계값 설계를 Mono로 옮긴 형태:

```csharp
public enum StackKind : byte { Fire, Ice, Bleed, Poison }

public struct StatusStack {            // 유닛별 List<StatusStack>
    public StackKind kind;
    public byte count, maxStack;       // SO 캡 (기본 5)
    public int  remainingTicks;        // 갱신 정책: 적용 시 perAppDuration로 리셋
    public byte lastTriggered;         // 임계값 엣지 감지 캐시
}

// StackThresholdSO: count 오름차순 ThresholdRule[]
//   atStack, mode(Edge|Consume), derived(ApplyDot|ApplyStun|ApplyStat), magnitude, duration
//   → 임계 통과 시 DoT/Stun을 큐잉하거나 StatModifier를 생성(§2로 환류)
```

- 스택 누적 → 임계값 통과 시 파생 효과(DoT/Stun/Stat). `ApplyStat` 파생은 §2 모디파이어로 환류된다.
- 이 모듈은 `StatusStackController` 하나로 캡슐화. 코어 `UnitStats`는 이를 몰라도 동작.
- 확률 없음 — 스택은 **결정적** 누적이다.

---

## 5. 드림캐쳐 경계 (단방향 의존)

| 스탯/모디파이어 시스템이 소유 | 드림캐쳐가 소유 (이 문서 밖) |
|---|---|
| 스탯 식별자(`StatId`)와 어휘 | 효과의 *의미*·시너지·조건 슬롯 조합 |
| 모디파이어 적용 API(`AddModifier`/만료) | 배치효과/특수효과의 발동 트리거 |
| Final 산출·캡 | "N타일 이내" 등 공간 조건 평가 |
| 읽기용 런타임 카운터 (자매 문서 §6) | 파생 데미지/실드의 양과 타이밍 |

> **단방향** — 유닛 문서가 어휘의 출처이고 드림캐쳐는 그것을 따른다. 드림캐쳐는 스탯 시스템에 대해 `AddModifier(stat, mode, value, duration, sourceId)` 호출 + 카운터 읽기만 한다. 스탯 시스템은 드림캐쳐를 **전혀 모른다**. 이 경계가 중복과 순환 의존을 막는다.

---

## 6. Mono 클래스 설계 (모디파이어)

| 타입 | 종류 | 책임 | 의존 |
|---|---|---|---|
| `FixedMath` | static class | Scale/BASE=1000, `Mul`/`Div`(truncate)/`Clamp`/`FromPercent`/`ToDisplay`. 모든 산술의 단일 관문 | 없음 |
| `ModifierMode` | enum | Flat / Percent (/ Override 확장) | 없음 |
| `StatModifier` | struct | 순수 데이터(§2.1) | `StatId` |
| `UnitStats`(모디파이어 부분) | MonoBehaviour/holder | `AddModifier` / `Tick` / `Recalculate` / `Get(StatId)` | `FixedMath`, `StatModifier`, `EffectiveStats` |
| `StatusStackController` | 선택 모듈 | 상태이상 스택(§4). 코어와 분리 | `UnitStats`, `StackThresholdSO` |

```csharp
// 소비자 — 모디파이어 적용된 Final 읽기
int dmg  = stats.Get(StatId.AttackPower);    // Final (고정소수점)
int rate = stats.Get(StatId.AttackSpeed);    // 초당 N회 → 틱 간격으로 환산
int step = stats.Get(StatId.MoveSpeed);      // 초당 N타일 → 틱당 누적
```

---

## 7. 결정성 체크리스트

| 축 | 규칙 | 코드 강제 지점 |
|---|---|---|
| 수 표현 | 고정소수점 정수, 스케일 1000 | `FixedMath` 외부에서 raw float 스탯 금지 |
| 스탯 연산 | `(Base+ΣFlat)×(BASE+ΣPercent)`, 가산비율·1회 승산 | `Recalculate()` 단일 경로 |
| 나눗셈 | truncate toward zero, 단일 헬퍼 | 직접 `/` 금지 → `FixedMath.Div` |
| 합산 순서 | 소스 ID(→stackId) 정렬 후 합산 | `OrderBy(sourceId).ThenBy(stackId)` |
| 시간 | 고정 틱(1/30s 등), 공속·이동 전부 틱 정수 환산 | `Tick()`만 시뮬, 렌더는 가변 허용 |
| 난수 | 결정적 PRNG(xorshift/PCG), 시드 파생·소비 순서 고정 | 스탯 레이어 미사용. 맵/웨이브 시드 한정 |
| 확률 발동 | 전면 배제 | 크리티컬·확률 추가발동 스탯 자체를 모델에 두지 않음 |

---

## 8. 설계 목표 & 효과

- **비동기 토너먼트 결정성** — 고정소수점 + 가산비율 1회 승산 + 소스 ID 정렬 + 고정 틱으로 "동일 시드 → 동일 결과"를 보장한다.
- **디버깅 용이** — 승산 누적·순서 의존을 제거해 임의 스탯값을 손으로 재현할 수 있다.
- **확장 용이** — 모디파이어 대상이 `StatId`로 일반화돼 있어, 새 스탯을 추가해도 집계 코드가 그대로 작동한다.
- **엔진 비의존** — 순수 C# + 고정 틱. 어떤 Mono 프로젝트에도 그대로 이식된다.

> 이 모델이 기존 ECS 구현에서 무엇을 바꾸는지에 대한 비교는 별도 문서 [기존 구현 → Mono 마이그레이션 노트](./3-migration-notes.md) 참조.

---

**미결(위임):** 캡 구체 수치, 특성 +15% 하드캡 적용 지점, 속성/스택 모듈 도입 시점 — 밸런싱/후속 spec.
**정적 구조:** [유닛 정적 스탯 구조](./1-static-stat-structure.md) 참조.
