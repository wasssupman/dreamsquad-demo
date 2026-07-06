# 유닛 정적 스탯 구조 스펙

> MonoBehaviour 기반 · ECS 비의존 · 기준 문서: 기획 PART B
> 자매 문서: [스탯 모디파이어 시스템](./2-stat-modifier-system.md)

이 문서는 **정적 스탯 구조**의 단일 source of truth다. 어떤 스탯이 존재하고, 어떤 레이어로 다뤄지며, 어디에 저장되고, 내구·카운터가 어떻게 구성되는지를 정의한다. 버프/디버프의 **적용·연산·결정성**은 자매 문서로 위임한다.

특정 실행 프레임워크(ECS/DOTS 등)에 의존하지 않으며, 순수 C# + ScriptableObject로 표현한다.

---

## 0. 개요 & 범위

### 0.1 정의한다
- 스탯 **3+1 레이어** 모델 (Prototype / Modifier / Final / Runtime)
- 스탯 **카탈로그** (공용 / 특공대 / 악몽 / 매치)
- **StatId** 식별자 체계 (효과 레이어와의 공통 어휘)
- **내구 모델** (방어력 없음, 실드 → 체력)
- **런타임 카운터** 구성 (효과 레이어가 읽을 누산값)
- 정적 데이터를 담는 **Mono 클래스 구조**

### 0.2 위임한다
- 모디파이어 적용·만료·중첩·Final 연산·결정성 → **자매 문서 「스탯 모디파이어 시스템」**
- 구체 수치·확률·계수·캡 값 → 밸런싱 spec
- 효과·시너지·드림캐쳐의 의미 → 드림캐쳐 spec
- 타겟팅·이동·사망 등 전투 엔진 → 전투 시뮬 spec
- 유닛 비주얼·연출(Spine/VFX/배치 애니) → 프레젠테이션 레이어 (스탯 SO에 섞지 않는다)

### 0.3 불변 원칙 (PART B 승계, 정적 구조 관련)
| 원칙 | 규칙 |
|---|---|
| 수 표현 | 고정소수점 정수, 스케일 `1000` (표시할 때만 `/1000`) |
| 내구 | 방어력 스탯 없음. 실드 → 체력 순 차감 |
| 확률 | 확률 발동 스탯을 모델에 두지 않음 |
| 프레젠테이션 분리 | 비주얼/연출은 스탯 데이터와 분리(god-object 방지) |

---

## 1. 스탯 3+1 레이어

모든 스탯은 아래 레이어로 다룬다.

| 레이어 | 정의 | 저장 위치 (Mono) | 가변성 |
|---|---|---|---|
| **Prototype** | 원본 기준값. 불변. | `UnitStatBlock` (ScriptableObject) | 읽기 전용 |
| **Modifier** | 버프/디버프가 얹는 변경분 (Flat 상수 / Percent 비율). | `UnitStats.modifiers : List<StatModifier>` | 효과 레이어가 추가/만료 (자매 문서) |
| **Final** | Prototype + Modifier 적용된 런타임 최종값. 캐시. | `UnitStats.effective : EffectiveStats` | dirty 시 재계산 (자매 문서) |
| **Runtime** | 현재 체력·누산 카운터 등 인스턴스 종속 가변 상태. | `UnitStats.runtime` + `Durability` | 시뮬이 갱신 |

> **Final ≠ Runtime** — "최종 공격력"은 Final(파생 캐시)이고 "현재 체력"은 Runtime(독립 상태)이다. 체력은 Prototype·Modifier로 **최대치**가 정해지지만, 현재 체력은 데미지/회복으로 따로 움직인다. 둘을 한 곳에 섞지 않는다.

---

## 2. 데이터 흐름 (정적 관점)

```
UnitStatBlock        UnitStats (인스턴스 보유자)         EffectiveStats        소비자
(ScriptableObject) → Prototype 참조 + List<StatModifier> → StatId별 Final  →  공격/이동/내구
불변 Prototype        + Final 캐시 + Runtime
```

- 한 유닛 인스턴스는 `UnitStatBlock`(공유 불변 자산)을 참조하고, 자신만의 모디파이어 목록·Final 캐시·Runtime 상태를 가진다.
- 모디파이어를 **생산**하는 쪽(효과/시너지/특성/드림캐쳐)과 그 **적용·집계** 로직은 자매 문서가 다룬다.

---

## 3. 스탯 카탈로그

스탯은 4개 블록으로 분리한다. 공용 블록은 특공대·악몽이 **공유**하고, 전용 블록은 합성으로 덧붙인다(god-object 분해).
레이어 표기: P=Prototype, M=Modifier 대상, R=Runtime.

### 3.1 공용 스탯 (모든 유닛)
| StatId | 레이어 | 단위/의미 | 비고 |
|---|---|---|---|
| `Cost` | P | 예산 | 유닛 파워 총량의 기준 |
| `Health` | P·M | 최대 체력 | Runtime 현재체력은 별도(§5) |
| `AttackPower` | P·M | 공격력 | 실제 데미지의 기준값 |
| `AttackSpeed` | P·M | 초당 N회 | 틱 단위 정수로 환산해 누적 |
| `MoveSpeed` | P·M | 초당 N타일 | 특공대는 0(고정) |
| `ProjectileSpeed` | P | 초당 N타일 | 원거리만 의미 |
| `ProjectileCount` | P·M | 정수 | 증가 = 데미지 뻥튀기, 밸런싱 검증 대상 |
| `AttackRange` | P | 타일 거리 | 1 = 배치 타일 기준 1타일. 확장 시 Shape 지정(마름모·별 등) |
| `AttackTargetCount` | P·M | 정수 | 근접 다수 타겟 / 원거리 단일발사+스플래시 |
| `Shield` | M | 실드량 | Prototype 기본 0, 버프로 부여(§5) |

### 3.2 특공대 전용 스탯
| StatId | 레이어 | 의미 |
|---|---|---|
| `Class` | P | 가디언 / 레인저 / 파이터 |
| `PostPlaceDelay` | P | 배치 후 첫 공격까지 대기(배치 스킬 고려) |
| `AggroCount` | P | 어그로 끌 수 있는 악몽 최대 수(0 = 어그로 없음) |
| `AggroRange` | P | 가디언 어그로 범위(= 공격 범위) |
| `AggroFilter` | P | 추후 논의 |

### 3.3 악몽 전용 스탯
| StatId | 레이어 | 의미 |
|---|---|---|
| `NightmareClass` | P | 탱커 / 러너 등 |
| `AttackFilter` | P | 어떤 클래스 특공대를 먼저 칠지. 어그로 우선 → 사거리 내 → 타일거리 → 잔여체력 → 생성순 |

### 3.4 매치 스탯
| StatId | 레이어 | 의미 |
|---|---|---|
| `GimmickType` / `ThemeType` / `MapSize` | P | 매치 환경 식별 |
| `CostProductionRate` | P·M | 코스트 생산 속도(상수/비율 모디파이어 가능) |
| `DeployedUnitCount` | R | 증감하는 **현재값(게이지)** — 상태형 조건 평가용 |

> **속성/스택은 미래 확장** — 기획 메모의 "나중에 추가할 만한 스탯: 속성, 속성별 스택"은 자매 문서의 선택 모듈로 분리한다. 코어 스탯 카탈로그에 지금 박지 않는다.

---

## 4. StatId 식별자 체계

모든 수치 스탯은 단일 `StatId` enum으로 식별한다. 모디파이어는 이 식별자로 대상 스탯을 가리킨다 — 효과 레이어와 스탯 레이어의 **공통 어휘**다(드림캐쳐가 단방향으로 따른다).

```csharp
public enum StatId : byte
{
    // 공용
    Cost, Health, AttackPower, AttackSpeed, MoveSpeed,
    ProjectileSpeed, ProjectileCount, AttackRange, AttackTargetCount, Shield,
    // 특공대
    PostPlaceDelay, AggroCount, AggroRange,
    // 매치
    CostProductionRate,
}
```

> **비-수치 식별자는 분리** — `Class`, `NightmareClass`, `AttackFilter`, `GimmickType` 등 enum/카테고리 값은 `StatId`에 넣지 않는다(Flat/Percent 산술 대상이 아님). 별도 enum으로 두고 모디파이어 대상에서 제외한다.

---

## 5. 내구 모델 (실드 단일)

> **방어력 스탯 없음.** 내구는 실드 하나로 표현한다 — 데미지 감산(% 경감) 메커니즘이 없으므로 결정성·직관성이 단순해진다.

```csharp
// 모든 데미지(일반 공격 + 드캐 독립 데미지)는 동일 규칙
void ApplyDamage(int amount)   // amount: 고정소수점
{
    int absorbed = Min(shield, amount);
    shield -= absorbed;
    int spill = amount - absorbed;
    currentHealth = Max(0, currentHealth - spill);
}
```

- 실드는 체력보다 **먼저** 까인다. 데미지는 실드 → 체력 순.
- 드림캐쳐 파생 데미지(반사·폭격·스플래시)도 동일 규칙. 출처 무관 단일 경로.
- 실드량은 `Shield` StatId의 Final이 상한, 현재 실드는 Runtime 상태.

---

## 6. 런타임 카운터

효과 레이어가 조건 평가에 쓰는 누산값. 스탯 엔진은 **세기만** 하고 의미는 부여하지 않는다.

| 스코프 | 카운터 | 리셋 정책 |
|---|---|---|
| 유닛 | 총 공격 횟수, 생존 시간(틱), 피격 횟수, 인접 유닛 수(All/클래스/코스트별, 공격범위 내) | 인스턴스 종속 → 사망 시 소멸, 별도 리셋 없음 |
| 매치 | 배치/사망 횟수(All/코스트/클래스), 배치된 유닛 수(게이지), 코스트 사용량, 스킬 사용 횟수, 악몽 퇴치 수 | 매치 내내 누적, 별도 리셋 없음 |
| 드캐 전용 | "N회마다"용 누산기 | **별개** 누산기 — 발동 시 그것만 리셋. 유닛/매치 총계는 불변 |

> **경계** — "공격 2회마다 발동" 같은 트리거는 드캐 전용 누산기로 카운트하고 발동 시 리셋한다. 유닛의 "총 공격 횟수"는 그와 무관하게 계속 증가한다. 두 카운터를 섞지 않는 것이 재현성의 핵심.

---

## 7. Mono 데이터 클래스 설계

| 타입 | 종류 | 책임 | 의존 |
|---|---|---|---|
| `StatId` | enum | 식별자 어휘 | 없음 |
| `UnitStatBlock` | ScriptableObject | Prototype 값. 공용 블록 + 특공대/악몽 전용 블록 합성 | `StatId` |
| `EffectiveStats` | struct/class | StatId→Final 배열 캐시 | `StatId` |
| `RuntimeCounters` | class | 유닛 스코프 누산값(§6) | 없음 |
| `Durability` | struct | 현재 체력 + 실드, `ApplyDamage`(§5) | `FixedMath`* |
| `UnitStats` | MonoBehaviour 또는 plain holder | Prototype 참조 + `List<StatModifier>` + Final 캐시 + Durability + Counters | 위 전부 |

\* `FixedMath`(고정소수점 산술 헬퍼)의 **연산 규약**은 자매 문서가 소유한다. 정적 구조 관점에서는 "모든 스탯 값이 스케일 1000 정수"라는 표현 규칙만 전제한다.

```csharp
// 정적 구조 — 합성 블록 예시
[CreateAssetMenu(menuName = "Units/StatBlock")]
public class UnitStatBlock : ScriptableObject
{
    public CommonStats   common;     // 공용(§3.1)
    public DefenderStats defender;   // 특공대 전용(§3.2), 악몽이면 null/미사용
    public NightmareStats nightmare; // 악몽 전용(§3.3), 특공대면 null/미사용
}

// 소비자 — 정적 Prototype 읽기(모디파이어 미적용 시)
int baseAtk = block.common.attackPower;   // 고정소수점
```

---

## 8. 설계 목표 & 효과

- **경계 명확** — 스탯 데이터와 효과 의미를 분리하고, 단일 god-object를 공용/전용 블록으로 분해해 확장 시 충돌 면적을 줄인다.
- **엔진 비의존** — 순수 C# + ScriptableObject. 어떤 Mono 프로젝트에도 그대로 이식되며, 특정 실행 프레임워크에 묶이지 않는다.
- **단일 표현** — 공격값 등 스탯은 하나의 식별자(`StatId`)·하나의 저장처(Prototype)로만 표현해 authoring 이중표현·혼란을 제거한다.

> 이 스펙이 기존 ECS 구현에서 무엇을 바꾸는지에 대한 비교는 별도 문서 [기존 구현 → Mono 마이그레이션 노트](./3-migration-notes.md) 참조.

---

**미결(위임):** 캡 구체 수치, `AggroFilter` 정의, 속성/스택 도입 시점 — 밸런싱/후속 spec.
**연산·결정성:** [스탯 모디파이어 시스템](./2-stat-modifier-system.md) 참조.
