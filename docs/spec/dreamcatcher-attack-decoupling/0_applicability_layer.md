# 0 — 적용성 판정 계층 (지원 행렬)

## 목적

"이 payload/mod 가 이 host 에서 **발동할 수 있는가**"를 판정하는 **순수 계층**을 만든다. 지금 이 지식은 세 곳에 흩어져 있다 — `DreamcatcherAttachEval.WouldApply`(UI preflight), `ApplyDreamcatcherCardToUnit` 의 자체 preflight 체인(커밋), `BattleBridge.cs:5596` 의 적 베이크 가드. 셋은 **손으로 미러링**되고 있고(`DreamcatcherAttachEval.cs:18` 의 "★ 동기화 계약"), 그 부채가 곧 이 spec 이 고치려는 병의 원인이다.

이 단위는 **판정만** 만든다. 소비자 교체는 unit 1, 새 사건 지점은 unit 3·4. 컴파일과 EditMode 만으로 완결된다.

**범위 경계**: host **종속** 조건만 다룬다. `magnitude <= 0`, `projectile == null`, `duration <= 0` 같은 **카드 데이터 검증**은 어느 host 에서든 결과가 같으므로 이 계층 밖이다(기존 위치 유지). 이 구분은 `DreamcatcherAttachEval.cs:12~17` 의 기존 판단을 그대로 잇는다.

## 변경 대상

- 신규 `Assets/_Project/Scripts/Core/Dreamcatcher/DcApplicability.cs`
- 신규 `Assets/_Project/Tests/EditMode/DcApplicabilityTests.cs`

`Wassup.Core` 에 두는 이유: 정의 계층(`Wassup.Data`)은 순수 데이터여야 하고, 판정은 `DreamcatcherCard` 를 읽되 ECS 타입은 모른다 — 기존 `DreamcatcherAttachEval` 과 같은 자리다.

## 구현

### host 프로필 (plain 값)

ECS 조회 결과를 담는 입력 struct. 브리지가 채우고 순수 함수가 소비한다 — 이 계층은 `Entity` 를 모른다.

```csharp
public enum DcHostArchetype { Standard, FacingVolley, BombThrow, HazardCast }
public enum DcProjectileRoute { None, Homing, Ballistic, Directional, Grenade }

public struct DcHostProfile
{
    public DcHostArchetype archetype;
    public DcProjectileRoute route;   // 실제 발사 경로 (SO flightMode 아님 — 계약 6)
    public bool targetsEnemies;       // AttackState.targetMask 의 Enemy 비트
    public bool hasDamageOutput;
    public bool hasLethalTimer;       // 이중 상태 거부용
    public bool hasDreamCocoon;
}
```

`archetype` 은 **host 의 실제 공격 모델**이다: `BombLauncherState` 보유 → `BombThrow`, `HazardCastAbility` → `HazardCast`, `DeployedFacing`+`VolleyFireState` → `FacingVolley`, 그 외 → `Standard`. `route` 도 같은 원칙 — `BombThrow` 는 `ProjectileRef` 가 뭐라 선언하든 `Grenade` 다(`Projectile_Bomb.asset` 은 `flightMode: 0`(Homing)이라 SO 만 보면 오판한다).

### 판정

```csharp
public enum DcRejectReason
{
    None, NoEventPoint, NeedsEnemyTargeting, NeedsDamageOutput,
    NeedsHomingRoute, NeedsTargetContext, DuplicateState,
}
public static DcRejectReason EvaluateMechanic(DcPayloadKind, DcTriggerKind, in DcHostProfile);
public static DcRejectReason EvaluateAttackMod(DcAttackModKind, in DcHostProfile);
```

행렬은 이 두 함수의 `switch` **한 곳**이 유일한 source of truth 다. 별도 테이블 자료구조를 만들지 않는다(제약 8 — 소비처가 여기뿐이다).

판정 규칙(대표):

| 대상 | 요구 | 거절 사유 |
|---|---|---|
| `ProjectileToTarget` | 적을 타겟하거나(→ host 대상 사용) 자체 탐색 가능 = **`targetsEnemies`** | `NeedsEnemyTargeting` |
| `ApplyCcToTarget` · `ApplyStackToTarget` | *그 공격의 대상*이 필요 → `archetype` 이 대상을 확정하는 부류(`Standard`/`FacingVolley`) | `NeedsTargetContext` |
| `HeavyStrike` · `FrontmostTarget` | `hasDamageOutput` | `NeedsDamageOutput` |
| `ProjectileBounce` | `route == Homing` | `NeedsHomingRoute` |
| `SelfStatBuff`·`SelfTileAoe`·`SelfBlink` 등 self 계열 | host 무관 | — |
| `AttackN` 트리거 전체 | host 에 사건 지점이 있는가(**unit 3·4 전까지 `BombThrow`/`HazardCast` 는 없다**) | `NoEventPoint` |

`NoEventPoint` 가 이 spec 의 잠금/해제 축이다 — unit 3 이 `BombThrow` 를, unit 4 가 `HazardCast` 를 사건 지점 보유로 바꾼다. 그때 이 함수의 한 줄만 바뀐다.

### total 보장

`switch` 의 `default` 는 **거절**이다(fail-closed). 새 `DcPayloadKind` 를 추가하고 여기를 잊으면 조용히 통과하는 대신 붙지 않는다 — 기존 `DreamcatcherAttachEval` 의 `default: return false` 와 같은 태도.

## 완료 기준

- [ ] 컴파일 클린. 이 단위는 **소비자가 없다**(unit 1 이 붙인다) — 기존 동작 변화 0.
- [ ] `DcApplicabilityTests`:
  - **total 어서션** — `DcPayloadKind` × `DcHostArchetype` 전 조합(현 17 × 4)과 `DcAttackModKind` × `DcProjectileRoute` 전 조합이 `EvaluateMechanic`/`EvaluateAttackMod` 에서 **미분류 없이** 값을 낸다. `Enum.GetValues` 로 순회해 새 kind 추가 시 자동으로 실패한다.
  - 현행 동작 미러 — `ProjectileToTarget`×힐러 = `NeedsEnemyTargeting`, `ProjectileBounce`×`Directional` = `NeedsHomingRoute`, `HeavyStrike`×outputs 없음 = `NeedsDamageOutput`.
  - 잠금 상태 고정 — `AttackN`×`BombThrow`/`HazardCast` = `NoEventPoint` (unit 3·4 가 이 기대값을 뒤집는다).
  - self 계열은 전 archetype 통과.
- [ ] `DreamcatcherAttachEval` · `ApplyDreamcatcherCardToUnit` **미변경** — 이 단위는 추가만 한다.

---

확인 일자 / 커밋: (미완)
