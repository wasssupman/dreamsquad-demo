# Data Schema Swap + Render Path Cutover

**작업 구분**: 2

## 목적

`ProjectileData` 의 mesh/material 필드를 prefab 기반 필드로 교체하고, BattleBridge 의 RenderMeshArray 경로를 제거하여 view 풀로 cutover 한다. 이번 task 가 본 spec 의 기능적 분기점.

## 변경 대상

- Modify: `Assets/_Project/Scripts/Data/ProjectileData.cs`
- Modify: `Assets/_Project/Scripts/Battle/Combat/Projectile/ProjectileSpawnRequest.cs`
- Modify: `Assets/_Project/Scripts/Battle/Combat/Projectile/ProjectileState.cs`
- Modify: `Assets/_Project/Scripts/Battle/Combat/Projectile/ProjectileRef.cs`
- Modify: `Assets/_Project/Scripts/Bridge/BattleBridge.cs` (캐시/Spawn 경로 50줄 안팎 교체)
- Modify: `Assets/_Project/Data/Projectiles/Projectile_Arrow.asset`
- Modify: `Assets/_Project/Data/Projectiles/Projectile_Bolt.asset`
- Modify: `Assets/_Project/Data/Projectiles/Projectile_CannonBall.asset`

## ProjectileData 신규 스키마 (이번 task 범위)

```csharp
public class ProjectileData : ScriptableObject
{
    public string id;
    public float speed = 10f;
    public float hitThreshold = 0.3f;
    public float visualScale = 0.3f;

    public GameObject projectilePrefab;   // 필수
    public GameObject hitPrefab;          // 선택

    // Splash (기존 유지)
    public OnHitEffectType onHitEffect = OnHitEffectType.None;
    public float onHitMagnitude;
    public float onHitDuration;
    public float splashRadius;
    public float splashDamageMul = 0.5f;
}
```

`visualMesh`, `visualMaterial` **제거**. 회전/배리에이션 필드는 task 3,4,6 에서 누적.

## ECS struct 명명 정리

- `ProjectileSpawnRequest.assetIndex` → **`dataIndex`**
- `ProjectileRef.assetIndex` → **`dataIndex`**
- `ProjectileState` 에 `int dataIndex` 추가 (hit event 에서 hit prefab 결정 위해)

## BattleBridge 변경

- 제거: `_projectileRenderIndex`, `_projectileRenderByIndex`, `GetOrCreateProjectileAssetIndex` (이름 변경 후 시그너처 정리).
- 신규: `Dictionary<ProjectileData, int> _projectileDataIndex`, `List<ProjectileData> _projectileDataByIndex`. lookup 함수 `GetOrCreateProjectileDataIndex(ProjectileData data)`.
- `SpawnProjectile(req)`:
  - 엔티티 생성 + `LocalTransform`/`ProjectileTag`/`ProjectileState` (dataIndex 포함) 부여까지는 동일.
  - `RenderMeshUtility.AddComponents` **제거**.
  - `_projectileViewPool?.Spawn(entity, data.projectilePrefab, req.visualScale)` 호출.
- `using` 정리 (RenderMesh 관련 import 제거).

## 자산 마이그레이션

- `Projectile_Arrow.asset` → `projectilePrefab` = WindBulletProjectile.prefab
- `Projectile_Bolt.asset` → `projectilePrefab` = StonebulletProjectile.prefab
- `Projectile_CannonBall.asset` → `projectilePrefab` = FireballProjectile.prefab, `hitPrefab` = FireballHit.prefab

UnityMCP 의 `manage_asset` 으로 ScriptableObject 직접 수정.

## 완료 기준

- compile: 모든 변경 파일이 에러 없이 통과.
- BattleScene Play: Archer/Bolt/Cannon 디펜더가 발사 시 새 prefab 비행체가 등장. 데미지/HitFlash/Splash 정상.
- 기존 RenderMeshArray 경로가 호출되지 않음 (Project Search 로 확인).
- hit prefab 은 이번 task 에서 아직 재생 안 함 (task 3 에서 연결). 큐는 enqueue 만 됨.
- read_console Error/Warning 0.
