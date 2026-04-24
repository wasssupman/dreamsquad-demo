# 24. Enemy View Mono Migration

## 목적

Enemy 렌더 경로를 ECS `RenderMeshUtility` → MonoBehaviour view 로 이관한다. **Spine asset 이 아직 준비되지 않았으므로** 내부는 기존 `AttackUnitData.visualMesh + visualMaterial` 로 placeholder quad view 를 유지. 이후 Spine asset 이 준비되면 `27` 에서 view 내부만 `SkeletonAnimation` 으로 교체.

본 단계의 결과는 **시각적으로 기존과 동일**하되 렌더 경로가 MonoBehaviour 로 바뀌어 `SpriteRenderer`/`MeshRenderer` 기반 sortingOrder 체계에 진입한다. 26 번 sort 통일의 전제.

## 전제

- `17`, `17b` 완료.
- `17c` 철회 상태 확인.

## 변경 대상

- `Assets/_Project/Scripts/Bridge/BattleBridge.cs` — `:1925~1955` Enemy RenderMesh 경로 제거, Mono view 생성으로 교체
- 신규 `Assets/_Project/Scripts/Presentation/EnemyView.cs` (`SpineDefenderView` 를 참고한 Mono view)
- 신규 `Assets/_Project/Scripts/Presentation/EnemyViewPool.cs` (`SpineDefenderPool` 와 유사한 구조)
- `Assets/_Project/Scripts/Data/AttackUnitData.cs` — 필요 시 `visualMaterial`/`visualMesh` 접근 방식 확인
- BattleBridge Update 루프 — ECS LocalTransform → EnemyView transform sync 추가

## 구현 가이드

### Step 1. `EnemyView` MonoBehaviour

```csharp
public class EnemyView : MonoBehaviour
{
    private MeshRenderer _renderer;
    private MeshFilter _filter;
    private Entity _entity;
    private AttackUnitData _data;

    public void Configure(AttackUnitData data, Entity entity, Mesh mesh, Material material)
    {
        _data = data;
        _entity = entity;
        // Quad + material 세팅 (현재 RenderMeshArray 에 넣던 것 동일)
    }

    public void UpdatePosition(Vector3 world) => transform.position = world;
    public void SetSortingOrder(int order) => _renderer.sortingOrder = order; // Renderer.sortingOrder 는 SRP Sprite 지원. 필요 시 MaterialPropertyBlock / depth.
    public Entity Entity => _entity;
}
```

주의: `MeshRenderer.sortingOrder` 는 Unity 의 SRP 2D Renderer / Transparency Sort Axis 환경에서 `SpriteRenderer` 와 섞일 때 동작 여부 확인 필요. 만약 섞이지 않으면 **EnemyView 의 렌더러를 `SpriteRenderer` 로 바꾸고** quad texture 를 sprite 로 래핑. 이 결정은 26 번에서 정리.

### Step 2. `EnemyViewPool` MonoBehaviour

- `Dictionary<Entity, EnemyView> _byEntity`
- `TrySpawn(AttackUnitData, Entity, Vector3 world)` / `TryGet(Entity, out EnemyView)` / `Despawn(Entity)` / `DisposeAll()`
- GameObject 생성은 prefab 없이 `new GameObject("EnemyView")` + AddComponent 로 충분. 추후 prefab 필요 시 rev.

### Step 3. `BattleBridge` enemy 생성 경로 교체

기존 `:1925~1937`:
```csharp
var renderArray = GetOrCreateRenderMeshArray(entry.unitType);
var desc = new RenderMeshDescription(...);
RenderMeshUtility.AddComponents(entity, _em, desc, renderArray, ...);
```

→ 변경:
```csharp
var mesh = entry.unitType.visualMesh ?? Resources.GetBuiltinResource<Mesh>("Quad.fbx");
var material = CreateEnemyRuntimeMaterial(entry.unitType.visualMaterial);
enemyViewPool.TrySpawn(entry.unitType, entity, spawnWorldPos, mesh, material);
```

RenderMesh 관련 `GetOrCreateRenderMeshArray(AttackUnitData)` + `_renderCache` 는 enemy 용도 제거. projectile / healthBar 용 캐시는 유지.

### Step 4. View sync 루프

BattleBridge 의 ECS → view sync 시점에 (예: `Update` 또는 전용 system) 각 살아있는 enemy entity 에 대해:

```csharp
if (enemyViewPool.TryGet(e, out var view) && _em.HasComponent<LocalTransform>(e))
{
    var p = _em.GetComponentData<LocalTransform>(e).Position;
    view.UpdatePosition(new Vector3(p.x, p.y, p.z));
    // sortingOrder 는 26 에서 붙인다. 이 단계에서는 position 만.
}
```

Enemy 사망 이벤트 처리 시점에 `enemyViewPool.Despawn(entity)` 호출 추가.

### Step 5. Health bar 처리

현재 enemy 에는 `CreateHealthBar` 가 `RenderMeshUtility` 로 붙는다. 이 경로는 **이번 단계에서 건드리지 않는다**. Spine defender 도 같은 health bar 경로 사용 중이면 일관성 유지. 26 번 완료 후 별도 health bar spec 에서 정리.

## 완료 기준

- Enemy 엔티티가 `RenderMeshUtility` 를 쓰지 않음 (grep 0, enemy 관련 경로 한정).
- `EnemyView` + `EnemyViewPool` 이 존재하고 BattleBridge 에서 생성/파괴됨.
- Enemy 이동이 Mono view 의 transform 으로 반영됨 (ECS LocalTransform → view.transform sync).
- 시각적으로 기존과 동일한 quad + material.
- `_renderCache` 와 `GetOrCreateRenderMeshArray(AttackUnitData)` 가 enemy 에 사용되지 않음 (제거 가능 시 제거).
- Unity console error 0.

## 주의

- **Spine 은 이 단계에서 도입하지 않는다.** 현재 asset 이 없어 placeholder. 추후 `27` 에서 교체 (SkeletonAnimation 를 EnemyView 에 붙이는 것만 추가).
- projectile RenderMesh 경로 (`:1143~1147`) 는 그대로. Enemy 범위만 이관.
- ECS 의 enemy entity 는 그대로 유지 (LocalTransform, Health, Targeting 등). 렌더만 Mono.
- 초기 spawn 폭주 시 GameObject Instantiate 비용 발생 가능. 필요 시 pool 의 pre-warm 추가 (후속).

확인 일자: 2026-04-24 / 커밋 해시: PENDING
