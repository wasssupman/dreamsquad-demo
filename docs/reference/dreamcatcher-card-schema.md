# 드림캐쳐 카드 데이터 스키마

> `DreamcatcherCard` SO 하나가 **3가지 카드 타입(Squad / Unit / Active)** 을 담는 union 구조다.
> `type` 에 따라 서로 다른 효과 필드가 활성화된다. 이 문서는 **정의 계층(순수 데이터)** 의 스키마만 다룬다.
> 해석·실행(bake → unmanaged slot → 시뮬)은 `BattleBridge` / Combat·AttackSystem 소관이며 여기에 포함하지 않는다.
>
> 앵커 파일:
> - `Assets/_Project/Scripts/Data/Dreamcatcher/DreamcatcherCard.cs`
> - `Assets/_Project/Scripts/Data/Dreamcatcher/DcMechanic.cs`
> - `Assets/_Project/Scripts/Data/SkillData.cs`

---

## 1. 전체 구조도

```
DreamcatcherCard (ScriptableObject)
│
├─ 식별       id · displayName · art · description
│
├─ 분류 축
│   ├─ type      : CardType   { Squad, Unit, Active }   ← 카드 정체성 + 덱 캡 키(Squad ≤2)
│   ├─ binding   : CardBinding { Axis, Unit }           ← 결합 방식 (Squad↔Axis, Unit↔Unit)
│   ├─ axis      : CardTargetAxis { ClassRanger, ClassGuardian, Cost1, All }
│   └─ category  : CardCategory { Normal, Unique, Subconscious }  ← 은퇴(cosmetic, dormant)
│
└─ 효과 페이로드 (type 별로 활성 필드가 갈림)
    │
    ├─[Squad]──▶ effects[]           : CardEffect[]        (스탯% 버프, 보통 1개)
    │            placementWarmupSec  : float               (배치 시 강제 idle 초)
    │
    ├─[Unit ]──▶ mechanics[]         : DcMechanic[]        (트리거형 발현 효과)
    │            attackMods[]        : DcAttackModSpec[]   (상시 공격출력 변조)
    │
    └─[Active]─▶ skill               : SkillData           (매치당 발동 스킬 wrapper)
```

> **직렬화 규칙**: 모든 효과 필드는 **append-only**. 기존 에셋의 int 순서를 보존하려 항상 끝에 추가한다.
> zero-init 기본값 = `type=Squad` + `binding=Axis` → 구 스탯 카드는 에셋 무변경으로 유효.

---

## 2. 분류 축 (enum)

| enum | 값 | 의미 |
|---|---|---|
| `CardType` | `Squad` / `Unit` / `Active` | 카드 정체성. 덱 캡(Squad ≤2)이 이 필드에 걸림 |
| `CardBinding` | `Axis` / `Unit` | 축 매칭 버프 vs 개별 유닛 부착 |
| `CardTargetAxis` | `ClassRanger` / `ClassGuardian` / `Cost1` / `All` | 어떤 아군에 걸리나 |
| `CardCategory` | `Normal` / `Unique` / `Subconscious` | **은퇴** — 소비처 0, back-compat dormant |

---

## 3. 타입별 효과 스키마

### ⓐ Squad — 축 매칭 스탯 버프 (`binding=Axis`)

`axis` 에 매칭되는 **현재 + 미래** 아군 전체에 매치 영구 적용.

```
CardEffect { CardBuffKind kind; float percent }   // +10 = +10%, -50 = -50%

CardBuffKind { AttackDamage, AttackSpeed, EffectiveHealth, MoveSpeed, CostRate }
```

| kind | 실효 채널 | 비고 |
|---|---|---|
| `AttackDamage` | DamageMul | |
| `AttackSpeed` | AttackSpeedMul | |
| `EffectiveHealth` | **DmgTakenMul** | 받는 피해 감소 프록시 — max-HP 미변경 |
| `MoveSpeed` | MoveSpeedMul | |
| `CostRate` | (StatModifier 없음) | GameManager → `CostRuntime.SetRegenRateMultiplier` 가 직접 소비 |

+ `placementWarmupSec` : 배치 시 N초 idle 후 행동. 공속 버프와 조합 시 "N초 대기 후 강화"로 읽힘. 기본 0 = warmup 없음.

---

### ⓑ Unit — 개별 유닛 부착 메커니즘 (`binding=Unit`)

한 유닛에 결합. **트리거 → 페이로드** 2단 구조 + 상시 공격 변조.

```
DcMechanic { DcTriggerSpec trigger; DcPayloadSpec payload }

DcTriggerSpec {
    DcTriggerKind kind;   // { None, AttackN, OnDamagedN, OnDeath }
    int period;           // N (예: AttackN + period=5 → 5회 공격마다)
}

DcPayloadSpec {
    DcPayloadKind kind;
    float magnitude;          // 데미지 등 (attacker damageMul 미적용 → 값 예측성)
    ProjectileData projectile;// ProjectileToTarget 전용 (그 외 null)
    int   tileRange;          // SelfTileAoe: AOE 반경(타일)
    float duration;          // SelfBuffLethal: 지속/자폭 초 · PlacementAura: warmup 초
}

DcPayloadKind { None, ProjectileToTarget, SelfTileAoe, NextAttackDoubleFire,
                SelfBuffLethal, SelfWarmupBuff, PlacementAura }
```

| DcPayloadKind | 발현 내용 |
|---|---|
| `ProjectileToTarget` | 타겟에게 투사체 (flat damage) |
| `SelfTileAoe` | 자기 타일 중심 AOE 폭발 (사망 폭발 등) |
| `NextAttackDoubleFire` | 다음 공격 2연발 |
| `SelfBuffLethal` | 즉발 자기 공속버프 + 지속 후 자폭 |
| `SelfWarmupBuff` | **예약·미구현** (핸들러 유실, 어떤 카드도 미사용, append-only 잔존) |
| `PlacementAura` | host 부착 스폰 오라. host 생존 중 axis 매칭 **신규 배치 유닛** 에 magnitude% 공속(매치영구) + duration 초 warmup. host 사망 시 회수 |

**상시 공격 변조** (트리거 없음, 상시 적용):

```
DcAttackModSpec { DcAttackModKind kind; int count; int tileRange; float damageMul }

DcAttackModKind { None, ProjectileBounce }
```

| 필드 | ProjectileBounce 의미 |
|---|---|
| `count` | 튕김 횟수 |
| `tileRange` | 재타겟 검색 반경 (Chebyshev 타일) |
| `damageMul` | 튕김당 감쇠 (1 = 감쇠 없음) |

---

### ⓒ Active — 매치당 발동 스킬 (`type=Active`)

`skill` 필드가 `SkillData` 를 wrap. 기존 스킬 파이프라인으로 시전, 코스트는 awakening 에서 지불.
`type==Active` 일 때만 유효(다른 타입은 무시).

```
SkillData (ScriptableObject) {
    SkillEffectType effect;   // { SlowField, PowerSurge, RapidFire, Tornado, Meteor, Portal }
    SkillTargetType target;   // { TilePoint, DefenderUnit }
    float range;              // TilePoint: 효과 반경 / DefenderUnit: 미사용(0)
    float magnitude;          // 대상 속성 배율 (0.6=이동60%, 2.0=데미지2배)
    float durationSec;        // 효과 수명
    float cooldownSec;
    int   cost;
    float warningSec;         // 텔레그래프 표시 초 (Meteor)
    ProjectileData projectile;// 통합 투사체 파이프라인 탑승 시
    Color uiTint;
}
```

---

## 4. 정의 ↔ 실행 계층 경계

- 이 SO/enum 들은 **순수 데이터 + 에셋 참조**다. `Unity.Entities` · `Wassup.Battle` 를 **절대 참조하지 않는다**.
- 해석(unmanaged slot bake) + 실행은 전부 `BattleBridge`(bake: `MapDcEffect`, `RegisterPlacementAura` 등) 와 Combat / AttackSystem 에 있다.
- 따라서 아키텍처를 바꿔도 **translator 만 다시 쓰면** 되고, 이 정의들은 건드리지 않는다.
- `mechanics[]` · `attackMods[]` 는 **bake-time read only** — managed array 이므로 per-frame 순회 금지.

## 5. 관련 spec 계보

| spec | 도입 스키마 |
|---|---|
| `ingame-dreamcatcher` | `CardTargetAxis`, `CardBuffKind`, `CardEffect`, Squad 스탯 6종 |
| `dreamcatcher-unit-trigger` | `CardBinding`, `DcMechanic`(trigger/payload), `ProjectileToTarget` |
| `dreamcatcher-content-1` | `OnDamagedN`/`OnDeath` 트리거 + `SelfTileAoe`/`NextAttackDoubleFire`/`SelfBuffLethal` |
| `dreamcatcher-attack-mod-bounce` | `DcAttackModSpec`, `ProjectileBounce` |
| `dreamcatcher-placement-aura` | `PlacementAura` payload |
| `dreamcatcher-card-taxonomy` | `CardType { Squad, Unit }` 축 + 덱 캡 이전 |
| `dreamcatcher-awakening-hand` | `CardType.Active` + `skill` 필드 |
| `dreamcatcher-squad-warmup` | `placementWarmupSec`, `CardCategory.Subconscious` |
| `dreamcatcher-card-art` / `-card-description` | `art`, `description` |
| `dreamstone-loadout` | `CardTargetAxis.All`, `CardBuffKind.CostRate` (append) |
