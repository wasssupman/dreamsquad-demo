# 0 — Frontmost 정의 계층 (contract, compile-only)

## 목적

`끝을 보는 눈`이 딛고 설 **타입 토대**를 세운다. 이 unit은 로직 없이 enum·컴포넌트·필드만 추가하며, 미사용 기본값은 전부 inert(기존 동작 무회귀)여야 한다. 실제 선택/피해 로직은 unit 1~3.

## 변경 대상

- `Assets/_Project/Scripts/Data/Dreamcatcher/DcMechanic.cs` — `DcAttackModKind`에 `FrontmostTarget` append.
- `Assets/_Project/Scripts/Battle/Combat/FrontmostAttackLock.cs` — **신규** Combat-owned 컴포넌트.
- `Assets/_Project/Scripts/Battle/Combat/Projectile/ProjectileSpawnRequest.cs` — `priorityTarget`/`priorityDamageMul` 필드.
- `Assets/_Project/Scripts/Battle/Combat/Projectile/ProjectileState.cs` — 동일 필드.

## 구현

### 1. enum append

```csharp
// DcAttackModKind — append at the end (기존: None, ProjectileBounce)
public enum DcAttackModKind { None, ProjectileBounce, FrontmostTarget }
```

`DcAttackModSpec`의 `count`/`tileRange` 필드 주석에 "FrontmostTarget kind에서는 미사용"을 명시(값 추가 없음).

### 2. FrontmostAttackLock (신규 컴포넌트)

Combat 맥락 소유. `AttackSystem`만 RW. 매 공격 add/remove 금지(값만 갱신). 수명 = defender entity.

```csharp
using Unity.Entities;

namespace Wassup.Battle.Combat
{
    // 끝을 보는 눈: 공격 단위 최전방 잠금. Bridge가 mod 최초 부착 시 1회 추가, AttackSystem만 RW.
    public struct FrontmostAttackLock : IComponentData
    {
        public bool active;             // START에서 최전방 후보를 잠갔는가
        public Entity target;           // 잠긴 대상 (Entity.Null = 없음)
        public float damageMulSnapshot; // START 시 유효 FrontmostTarget slot damageMul 곱 (기본 1)
        public bool targetIsPriority;   // 잠긴 target이 +20% 대상인가 (fallback이면 false)
    }
}
```

### 3. projectile inert 필드

`ProjectileSpawnRequest`, `ProjectileState` 양쪽에 동일 추가(기존 `bounce*` 필드 옆):

```csharp
public Entity priorityTarget;   // Entity.Null = 비활성
public float  priorityDamageMul; // 0 = 비활성; 소비 시 (priorityDamageMul > 0 ? priorityDamageMul : 1)
```

- zero-init 기본값 `Null/0`이 곧 "보너스 없음"이다. 기존 request 생산자·spawn 경로를 전수 수정하지 않아도 모든 기존 투사체가 inert.
- 이 unit에서는 **필드 선언만**. drain 전달(BattleBridge)과 소비(ProjectileHitSystem)는 unit 3.

## 완료 기준

- [ ] 프로젝트 compile green (`read_console` error 0). 신규 스크립트는 `refresh_unity` scope=all 후 확인.
- [ ] `DcAttackModKind.FrontmostTarget` 존재, 기존 `None=0, ProjectileBounce=1` 정수값 불변(append라 `FrontmostTarget=2`).
- [ ] `FrontmostAttackLock`이 Combat 폴더에 `IComponentData`로 존재, 4필드.
- [ ] `ProjectileSpawnRequest`/`ProjectileState`에 `priorityTarget`/`priorityDamageMul` 존재.
- [ ] 기존 투사체/공격 무회귀: 신규 필드 참조처 0(선언만)이므로 런타임 동작 불변. 기존 EditMode/PlayMode 회귀 0.
- [ ] 기존 `DcAttackModKind` 소비처(bake switch 등)가 새 enum 값에 대해 컴파일 경고 없이 default 처리되는지 확인.
