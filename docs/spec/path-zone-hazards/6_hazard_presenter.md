# HazardPresenter (Visual Layer)

**작업 구분**: 6

## 목적

Hazard ECS entity 의 visual prefab 을 world 에 인스턴스화 + entity 라이프사이클 동기. 기존 `TornadoFieldPresenter` / `MeteorWarningPresenter` 패턴 미러. ECS 는 visual 을 모름 — Presentation 계층의 일방향 listening.

## 변경 대상

- Add: `Assets/_Project/Scripts/Presentation/HazardPresenter.cs` — MonoBehaviour
- Modify: `Assets/_Project/Scripts/Bridge/BattleBridge.cs` — hazard entity 추적 (spawn/destroy 이벤트) + HazardPresenter 호출 + visual prefab dictionary

## HazardPresenter

```csharp
public class HazardPresenter : MonoBehaviour
{
    private readonly System.Collections.Generic.Dictionary<Entity, GameObject> _instances = new();

    public void OnHazardSpawned(Entity hazardEntity, GameObject visualPrefab, Vector3 worldOrigin, int radius, HazardShape shape)
    {
        if (visualPrefab == null) return;
        var go = Instantiate(visualPrefab, worldOrigin, Quaternion.identity, transform);
        // shape 따라 scale (SingleCell=1, Square3x3=3, RadiusCircle=2*radius+1)
        float side = shape switch
        {
            HazardShape.SingleCell => 1f,
            HazardShape.Square3x3 => 3f,
            HazardShape.RadiusCircle => 2f * radius + 1f,
            _ => 1f,
        };
        go.transform.localScale = new Vector3(side, go.transform.localScale.y, side);
        _instances[hazardEntity] = go;
    }

    public void OnHazardDespawned(Entity hazardEntity)
    {
        if (_instances.TryGetValue(hazardEntity, out var go) && go != null)
            Destroy(go);
        _instances.Remove(hazardEntity);
    }

    private void OnDestroy()
    {
        foreach (var kv in _instances)
            if (kv.Value != null) Destroy(kv.Value);
        _instances.Clear();
    }
}
```

## BattleBridge 통합

```csharp
// 신규 필드
[SerializeField] private HazardPresenter _hazardPresenter;
private readonly Dictionary<Entity, (GameObject prefab, int radius, HazardShape shape, Vector3 worldOrigin)> _hazardVisualMeta = new();
private readonly HashSet<Entity> _liveHazards = new();

// 매 프레임 frame loop 에서 (예: 기존 TornadoPresenter sync 위치):
//   - Query<Hazard> → 현재 살아있는 entity set (currentSet)
//   - currentSet \ _liveHazards = new entries → meta lookup → presenter.OnHazardSpawned
//   - _liveHazards \ currentSet = removed entries → presenter.OnHazardDespawned + meta dictionary 정리
//   - _liveHazards = currentSet
```

기존 ECS-MB sync 패턴을 그대로 따름. 정확한 위치는 `TornadoFieldPresenter` 통합 코드 참고하여 동일 hook point 에 추가.

## visualPrefab ref 흐름

`Hazard` ECS 컴포넌트는 prefab ref 보유 안 함 (Burst/blittable 제약). 대안:

- BattleBridge 가 `SpawnHazard` 를 wrap 하는 `SpawnHazardWithVisual(HazardSO so, int2 cell)` 메서드 노출. 이 wrapper 가 `EffectSpawner.SpawnHazard` 호출 + `_hazardVisualMeta[entity]` 등록 + `OnHazardSpawned` 즉시 호출.
- Unit 7 의 디버그 진입점은 이 wrapper 를 호출.
- 미래 producer (스킬/배치/장비) 도 `SpawnHazardWithVisual` 호출 또는 직접 `EffectSpawner.SpawnHazard` + 자체 visual 등록.

```csharp
// BattleBridge:
public Entity SpawnHazardWithVisual(HazardSO so, int2 cell)
{
    if (so == null || _em == null) return Entity.Null;
    var e = EffectSpawner.SpawnHazard(_em, so, cell);
    if (e == Entity.Null) return e;

    Vector3 worldOrigin = GridToWorldXZ(cell);  // 기존 grid math
    _hazardVisualMeta[e] = (so.visualPrefab, so.radius, so.shape, worldOrigin);
    _liveHazards.Add(e);
    _hazardPresenter?.OnHazardSpawned(e, so.visualPrefab, worldOrigin, so.radius, so.shape);
    return e;
}
```

## 단위 테스트

Presenter 자체는 MonoBehaviour 이라 EditMode 단위테스트 어려움. PlayMode smoke 검증은 Unit 7 에서.

## 완료 기준

- 컴파일.
- Presentation 계층만 추가. ECS 동작 변화 0.
- BattleBridge frame sync 코드가 Hazard entity 라이프사이클을 정확히 추적 (코드 리뷰).
- 콘솔 에러/경고 0.
- 디버그 spawn 시 (Unit 7) visual prefab 가 world 에 등장 + 만료 시 사라짐 — Unit 7 의 PlayMode 에서 검증.
