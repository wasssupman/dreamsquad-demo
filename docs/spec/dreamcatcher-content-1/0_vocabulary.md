# 0 — 어휘 확장 (enum + 컴포넌트 + 필드)

## 목적

3장이 쓸 신규 트리거/페이로드 enum, 신규 런타임 컴포넌트, 슬롯/스펙 필드를 append 한다. 전부 미사용 상태로 컴파일만 통과.

## 변경 대상

- 수정: `Assets/_Project/Scripts/Data/Dreamcatcher/DcMechanic.cs` (정의계층 enum + `DcPayloadSpec` 필드, ECS 무참조)
- 수정: `Assets/_Project/Scripts/Battle/Combat/DcTriggerSlot.cs` (`tileRange` 필드 append — SelfTileAoe 용)
- 신규: `Assets/_Project/Scripts/Battle/Units/DamagedCounter.cs` (Units — ① 피격 카운터)
- 신규: `Assets/_Project/Scripts/Battle/Combat/NextAttackDoubleFire.cs` (Combat 핸드오프 채널)
- 신규: `Assets/_Project/Scripts/Battle/Effects/LethalTimer.cs` (Effects — ③ 자폭)

## 구현

`DcMechanic.cs` — **끝에 append** (기존 카드 직렬화 보존):

```csharp
public enum DcTriggerKind { None, AttackN, OnDamagedN, OnDeath }
public enum DcPayloadKind { None, ProjectileToTarget, SelfTileAoe, NextAttackDoubleFire, SelfBuffLethal }
```

`DcPayloadSpec` 에 필드 append (기본 0, 기존 카드 inert):
```csharp
public int tileRange;   // SelfTileAoe: AOE 반경(타일)
public float duration;  // SelfBuffLethal: 지속/자폭 초
```

- period 재사용: OnDamagedN=피격 N, OnDeath/즉발=미사용.
- SelfTileAoe(②) → magnitude=폭발뎀, tileRange=반경, projectile=AOE 뷰.
- NextAttackDoubleFire(①) → 파라미터 없음(charge 1 고정).
- SelfBuffLethal(③) → magnitude=공속%, duration=초. trigger=None(즉발).

`DcTriggerSlot.cs` — `public int tileRange;` append (SelfTileAoe 베이크용; 기존 필드 뒤).

`DamagedCounter` (Units) — ① 피격 카운터. **Combat 의 DcTriggerSlot 과 분리**(맥락 경계: Units 가 쓸 상태는 Units 소유):
```csharp
public struct DamagedCounter : IComponentData
{
    public int instanceId; public ushort period; public ushort counter;
}
```

`NextAttackDoubleFire` (Combat) — Units→Combat 핸드오프 채널(IncomingDamage 역방향 선례). 생산=Units AddComponent, 소비=Combat read+Remove:
```csharp
public struct NextAttackDoubleFire : IComponentData { public int charges; }
```

`LethalTimer` (Effects) — 자폭 타이머:
```csharp
public struct LethalTimer : IComponentData { public float remaining; }
```

## 완료 기준

- [ ] 컴파일 통과 (신규 .cs refresh scope=all)
- [ ] 기존 카드(콕콕바늘/통통구슬)·투사체 에셋 로드 무변동 (append-only, zero-init)
- [ ] 정의계층(DcMechanic)에 ECS 참조 없음
