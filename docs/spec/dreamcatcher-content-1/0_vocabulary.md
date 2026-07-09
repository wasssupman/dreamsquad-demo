# 0 — 어휘 확장 (enum + 컴포넌트)

## 목적

3장이 쓸 신규 트리거/페이로드 enum 과 신규 런타임 컴포넌트를 append 한다. 전부 미사용 상태로 컴파일만 통과.

## 변경 대상

- 수정: `Assets/_Project/Scripts/Data/Dreamcatcher/DcMechanic.cs` (정의계층 enum, ECS 무참조)
- 신규: `Assets/_Project/Scripts/Battle/Combat/NextAttackDoubleFire.cs`
- 신규: `Assets/_Project/Scripts/Battle/Effects/LethalTimer.cs`

## 구현

`DcMechanic.cs` — **끝에 append** (기존 카드 직렬화 보존):

```csharp
public enum DcTriggerKind { None, AttackN, OnDamagedN, OnDeath }
public enum DcPayloadKind { None, ProjectileToTarget, SelfTileAoe, NextAttackDoubleFire, SelfBuffLethal }
```

- `DcTriggerSpec.period` 재사용: OnDamagedN=피격 횟수(N), OnDeath/즉발=미사용(0), AttackN=기존.
- `DcPayloadSpec` 재사용 + `int tileRange` **append**(SelfTileAoe 전용, 기본 0):
  - SelfTileAoe(②) → magnitude=폭발뎀, tileRange=반경, projectile=AOE 뷰 ProjectileData.
  - NextAttackDoubleFire(①) → 파라미터 없음(charge 1 고정).
  - SelfBuffLethal(③) → magnitude=공속%, `duration` 필드=지속/자폭 초. (`DcPayloadSpec` 에 `float duration` 도 append — 기본 0.)
- ③은 트리거 없는 **즉발**(부착 시 1회 적용)이라 mechanic 의 `trigger.kind=None`, payload=SelfBuffLethal 로 표현. DcTriggerSlot 미저장.

`NextAttackDoubleFire` (Combat) — charge 컴포넌트:

```csharp
public struct NextAttackDoubleFire : IComponentData { public int charges; }
```

`LethalTimer` (Effects) — 자폭 타이머:

```csharp
public struct LethalTimer : IComponentData { public float remaining; }
```

## 완료 기준

- [ ] 컴파일 통과 (신규 .cs refresh scope=all)
- [ ] 기존 카드/투사체 에셋 무변동 (append-only, 기본값 0)
- [ ] 정의계층(DcMechanic)에 ECS 참조 없음
