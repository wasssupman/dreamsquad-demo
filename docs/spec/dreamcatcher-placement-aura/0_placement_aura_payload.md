# 0 — DcPayloadKind.PlacementAura

## 목적

스폰 오라 payload 정의. host 생존 중 axis 매칭 신규 배치 유닛에 warmup+공속버프를 부여하는 오라.

## 변경 대상
- `Assets/_Project/Scripts/Data/Dreamcatcher/DcMechanic.cs`

## 구현

`DcPayloadKind` 끝에 **append (kind 6)**. SelfWarmupBuff(5)는 reserved 로 그대로 둔다(append-only, H4):
```csharp
public enum DcPayloadKind { None, ProjectileToTarget, SelfTileAoe, NextAttackDoubleFire, SelfBuffLethal, SelfWarmupBuff, PlacementAura }
```

의미 (주석으로 명기):
- **PlacementAura**: host 부착. host·기존 유닛엔 미적용. host 생존 중 axis 매칭 **신규 배치 유닛**에
  배치 시점 `magnitude`% 공속(매치영구 DcDuration) + `duration`초 warmup idle 부여. host 사망 시 회수.
- `magnitude` = 공속 %, `duration` = warmup 초. 다른 필드(projectile/tileRange) 미사용.

## 완료 기준
- [ ] 컴파일 클린. grep 으로 enum 에 PlacementAura 실제 존재 확인.
- [ ] 기존 카드 payload int 보존(append-only).
