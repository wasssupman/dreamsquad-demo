# 25. Fallback Defender View Mono

## 목적

Defender 가 Spine asset 을 갖지 못한 경우 (`spineDefenderPool.TrySpawn` 실패 시) 현재 `RenderMeshUtility` 로 fallback 한다 (`BattleBridge.cs:1762~1768`). 이 경로를 Mono view 로 수렴해 **모든 Defender 렌더가 MonoBehaviour 체계**에 들어오도록 한다.

## 전제

- `24` 완료 (Enemy 가 먼저 Mono 로 이관되어 있어야 패턴이 일관).
- `17c` 철회 상태.

## 변경 대상

- `Assets/_Project/Scripts/Bridge/BattleBridge.cs` — `:1755~1768` fallback RenderMesh 경로 제거
- `Assets/_Project/Scripts/Presentation/SpineDefenderPool.cs` — fallback 분기 추가 또는 신규 `DefenderFallbackPool`
- (선택) `24` 의 `EnemyView` / `EnemyViewPool` 를 재사용해 generic quad view 공용화

## 구현 가이드

### Step 1. Fallback 분기 지점

현재:
```csharp
bool spineSpawned = false;
if (spineDefenderPool != null)
{
    spineSpawned = spineDefenderPool.TrySpawn(unitData, entity, spineWorld, out _);
}
if (!spineSpawned)
{
    var renderArray = GetOrCreateDefenderRenderMeshArray(unitData);
    ...
    RenderMeshUtility.AddComponents(...);
}
```

→ 변경:
```csharp
if (spineDefenderPool == null || !spineDefenderPool.TrySpawn(unitData, entity, spineWorld, out _))
{
    // Spine 없는 유닛도 Mono quad view 로 fallback
    defenderFallbackPool.Spawn(unitData, entity, spineWorld);
}
```

### Step 2. Fallback pool 구현 선택지

- **(a) 24 의 `EnemyViewPool` 재사용**: generic `QuadUnitView` / `QuadUnitViewPool` 로 이름 일반화. `EnemyView` 를 rename 하고 defender/enemy 둘 다 여기 얹음. 가장 깔끔.
- **(b) `DefenderFallbackViewPool` 신규**: 독립 pool. 코드 중복 있지만 defender/enemy 분리 유지.

권장: **(a) generic 화**. `QuadUnitView` + `QuadUnitViewPool` 로. `AttackUnitData` / `DefenderUnitData` 공통 필드 (`visualMesh`, `visualMaterial`) 만 사용.

단, `SpineDefenderPool` 과 `QuadUnitViewPool` 이 둘 다 Entity dict 를 가지므로 **entity 가 둘 중 한 pool 에만 존재** 하도록 보장 필요. `TryGetAnyView(Entity) -> IUnitView` 인터페이스로 통합 접근 허용.

### Step 3. `GetOrCreateDefenderRenderMeshArray` 및 `_defenderRenderCache` 제거

fallback 경로가 사라지면 더 이상 필요 없음. 관련 필드/메서드 제거.

### Step 4. view sync 루프

Defender sync 에 fallback view 도 포함. 위치 갱신:
```csharp
if (spineDefenderPool.TryGet(e, out var spineView)) spineView.UpdatePosition(...);
else if (defenderFallbackPool.TryGet(e, out var quadView)) quadView.UpdatePosition(...);
```

또는 통합 인터페이스:
```csharp
if (unitViewRegistry.TryGet(e, out var view)) view.UpdatePosition(...);
```

### Step 5. SpineDefenderPool.TrySpawn 실패 조건 정리

현재 `TrySpawn` 은 skeleton data 없으면 false 반환. 이 동작은 그대로. fallback pool 이 받아주는 구조.

## 완료 기준

- `GetOrCreateDefenderRenderMeshArray` / `_defenderRenderCache` 제거.
- RenderMesh 를 쓰는 defender 코드 path 가 존재하지 않음 (projectile/healthBar 제외).
- Spine 없는 defender 도 Mono quad view 로 렌더됨.
- 기존 Spine defender 경로는 regression 없음.
- Unity console error 0.

## 주의

- 24 와 같은 generic pool 을 쓸지 독립 pool 로 갈지는 구현자 선택. 24 와 묶어 커밋하는 것이 정합.
- DefenderUnitData 의 `visualMesh` / `visualMaterial` 이 없는 유닛이 있는지 확인 필요. 없으면 사전 fallback (Quad + 기본 material).
- Spine asset 이 준비되는 유닛은 점진적으로 Spine 경로로 흡수. fallback 은 점진적으로 비어간다.

확인 일자: 2026-04-24 / 커밋 해시: 4f8684f
