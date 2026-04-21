# Deploy Animation

**작업 구분**: Phase 6

## 목적

Drop 후 배치 VFX/애니메이션을 재생하고, 완료 시점에 일반 전투 활성화를 허용한다.

## API

```csharp
public float PlayDeploymentPresentation(
    DefenderUnitData unitData,
    Vector2Int cell,
    Entity entity);
```

## 규칙

- `placementVfxPrefab` 이 있으면 해당 prefab 을 spawn 한다.
- 없으면 placement ring fallback 을 사용한다.
- Spine view 가 있으면 `SpineDefenderView.PlayDeploy()` 를 호출한다.
- duration 은 `DefenderUnitData.deploymentDuration`.

## 완료 기준

- deployment presentation 이 재생된다.
- duration 이 0보다 크면 activation 이 지연된다.
- presentation 실패 시에도 entity 는 activation 가능하다.
