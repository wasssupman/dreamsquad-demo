# On-place Skill Contract

**작업 구분**: Phase 0

## 목적

Defender 배치 순간에 발동되는 고유 스킬의 공통 계약을 고정한다. 기존 `SlowPulse`, `BoostNearbyDefenders` 에 더해 배치 순간형 효과를 확장한다.

## 변경 대상

- Modify: `Assets/_Project/Scripts/Data/DefenderUnitData.cs`
- Modify: `Assets/_Project/Scripts/Bridge/BattleBridge.cs`

## Effect enum

`OnPlaceEffectType`:

```csharp
public enum OnPlaceEffectType
{
    None,
    SlowPulse,
    BoostNearbyDefenders,
    BindNearby,
    MeleeBurst,
    ForwardProjectile,
    GainCost,
    ReduceSkillCooldown,
}
```

## 데이터 필드

기존 필드 재사용:

```csharp
public OnPlaceEffectType onPlaceEffect;
public float onPlaceRange;
public float onPlaceMagnitude;
public float onPlaceDuration;
```

Deployment presentation 필드:

```csharp
public string dragAnimation = "idle";
public string deployAnimation = "deploy";
public GameObject placementVfxPrefab;
public float deploymentDuration = 0.45f;
public float placementSkillDelay = 0f;
```

## 실행 계약

- Drop 성공 시 tile 은 즉시 점유된다.
- `TryBeginDefenderDeployment` 는 cost 차감, entity 생성, `PendingDeployment` 부여까지 처리한다.
- 배치 presentation 후 `ActivateDeployedDefender` 가 on-place skill 을 트리거한다.
- on-place skill 은 `_onPlaceTriggeredEntities` 로 중복 방지한다.
- `PendingDeployment` 제거 후 일반 combat 대상이 된다.

## 완료 기준

- enum 8값이 컴파일된다.
- `DefenderUnitData` Inspector 에 on-place / deployment 필드가 노출된다.
- `BattleBridge.TriggerDeploymentOnPlaceSkill` 경로에서 중복 발동이 없다.
- 기존 click placement 경로는 즉시 배치 fallback 으로 유지된다.
