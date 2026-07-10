# 0 — SelfWarmupBuff payload + Unit-path 적용

## 목적

Unit 부착 경로가 "warmup idle + 영구 공속 버프"를 한 유닛에 적용하도록 새 payload 를 추가한다.

## 변경 대상

- `Assets/_Project/Scripts/Data/Dreamcatcher/DcMechanic.cs` — `DcPayloadKind` 에 `SelfWarmupBuff` append
- `Assets/_Project/Scripts/Bridge/BattleBridge.cs` — `ApplyDreamcatcherCardToUnit` 에 처리 분기

## 구현

`DcMechanic.cs`:
```csharp
public enum DcPayloadKind { None, ProjectileToTarget, SelfTileAoe, NextAttackDoubleFire, SelfBuffLethal, SelfWarmupBuff }
```

`BattleBridge.ApplyDreamcatcherCardToUnit` — SelfBuffLethal 분기 옆(트리거 가드 이전, 즉발류):
```csharp
// dreamcatcher-subconscious-unit — SelfWarmupBuff (느린 각성): trigger=None(즉발).
// 부착 즉시 공속 +magnitude% 매치 영구(DcDuration) + duration 초 warmup idle. 자폭 없음.
if (m.payload.kind == Wassup.Data.DcPayloadKind.SelfWarmupBuff)
{
    if (m.payload.magnitude <= 0f)
    {
        Debug.LogWarning($"[BattleBridge] Card '{card.id}' mechanic {i}: SelfWarmupBuff non-positive magnitude — skipped.");
        continue;
    }
    EnqueueAttackSpeedMul(defender, 1f + m.payload.magnitude / 100f, DcDuration);
    if (m.payload.duration > 0f) ApplyPlacementWarmup(defender, m.payload.duration);
    attached++;
    continue;
}
```

- `EnqueueAttackSpeedMul(target, mult, duration)` 재사용, `DcDuration=1e9f`(매치 영구).
- `ApplyPlacementWarmup` 재사용(cooldownRemaining = max(현재, sec)) — squad-warmup 에서 도입한 헬퍼.
- SelfBuffLethal 과 달리 LethalTimer 미부착(자폭 없음).

## 완료 기준

- [ ] 컴파일 클린(`read_console`).
- [ ] 기존 카드(마지막 불꽃 등) payload int 값 보존(append-only, 회귀 없음).
