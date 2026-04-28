# Facing Policy + Hit Prefab Lifecycle

**작업 구분**: 3

## 목적

비행체에 회전 정책을 부여하고, hit prefab 재생/자동 despawn 을 view 풀에 연결한다.

## 변경 대상

- Modify: `Assets/_Project/Scripts/Data/ProjectileData.cs` (신규 필드)
- Modify: `Assets/_Project/Scripts/Presentation/ProjectileViewPool.cs` (회전 + hit lifecycle)
- Modify: `Assets/_Project/Scripts/Bridge/BattleBridge.cs` (Drain 채움)
- Modify: `Assets/_Project/Data/Projectiles/Projectile_*.asset` (facing 값 설정)

## ProjectileData 신규 필드

```csharp
public enum ProjectileFacing
{
    AlongVelocity,   // 매 프레임 이동 방향 향함
    FixedUp,          // 회전 안 함
    SpinAroundUp,     // Y축 자전 (spinSpeed deg/s)
}

public ProjectileFacing facing = ProjectileFacing.AlongVelocity;
public float spinSpeed = 0f;
```

자산 권장값:
- Arrow / Bolt: `AlongVelocity`
- CannonBall (Fireball): `SpinAroundUp`, spinSpeed = 360.

## ProjectileViewPool 확장

- `Spawn(entity, prefab, scale, facing, spinSpeed)` 시그너처 확장.
- `SyncTransforms`:
  - 이전 frame position 캐시 → 현재 position 차이로 velocity 추정.
  - `AlongVelocity` 일 때 `Quaternion.LookRotation(velocity, Vector3.up)`. velocity≈0 이면 회전 유지.
  - `SpinAroundUp` 일 때 매 frame `transform.Rotate(0, spinSpeed * Time.deltaTime, 0)`.
  - `FixedUp` 은 prefab 기본 rotation 유지.
- 내부 자료구조: 기존 `Dictionary<Entity, GameObject>` 를 `Dictionary<Entity, ProjectileViewState>` 로 격상.

```csharp
private struct ProjectileViewState
{
    public GameObject view;
    public GameObject prefab;            // 풀 반환 키
    public ProjectileFacing facing;
    public float spinSpeed;
    public float3 lastPosition;
}
```

## Hit Prefab 재생

- `ProjectileViewPool.PlayHit(GameObject hitPrefab, float3 position)`:
  - 풀에서 꺼내거나 Instantiate. `SetActive(true)`. position 적용.
  - 자동 despawn: `ParticleSystem.main.duration` + maxStartLifetime 의 max 값을 lifetime 로 사용. fallback 1.5초.
  - Coroutine 으로 lifetime 경과 시 풀 반환.
- BattleBridge `DrainProjectileHitEvents`:
  ```csharp
  while (_projectileHitEventQueue.TryDequeue(out var evt)) {
      var data = _projectileDataByIndex[evt.dataIndex];
      if (data.hitPrefab != null)
          _projectileViewPool?.PlayHit(data.hitPrefab, evt.position);
  }
  ```
  호출 위치는 `DrainGoalEvents`/`DrainDefenderAttackEvents` 와 같은 흐름.

## 완료 기준

- compile + Play smoke: Arrow/Bolt 가 이동 방향 향해 회전. CannonBall(Fireball) 가 자전.
- CannonBall 적중 시 FireballHit prefab 이 1회 재생되고 자체 lifetime 후 사라짐. 동일 prefab 으로 N회 발사해도 GameObject 누수 없음 (`_active`/`_pool` 카운트로 검증).
- Splash 보조 타겟에는 hit VFX 가 안 뜸 (직접 타겟 1점만).
- read_console Error/Warning 0.

확인 2026-04-28 / 커밋: 9a6b8d2
