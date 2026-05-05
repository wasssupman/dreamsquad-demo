# 4. MeteorPending Chebyshev 전환

## 목적

`MeteorPending` AoE 범위 판정을 Chebyshev 타일 거리로 교체한다.  
VFX(경고 링, 버스트) 크기는 `tileRange * tileSize` 로 환산해서 넘김.

## 변경 대상

- `Assets/_Project/Scripts/Battle/Effects/MeteorPending.cs`
- `Assets/_Project/Scripts/Battle/Effects/EffectSpawner.cs`
- `Assets/_Project/Scripts/Battle/Combat/MeteorResolutionSystem.cs`
- `Assets/_Project/Scripts/Bridge/BattleBridge.cs`

## 구현

### MeteorPending.cs

```csharp
public struct MeteorPending : IComponentData
{
    public float3 centerWorld;
    public int    tileRange;    // Chebyshev 범위
    public float  damage;
    public float  warningRemaining;
}
```

### EffectSpawner.cs — SpawnMeteor 시그니처

```csharp
public static Entity SpawnMeteor(EntityManager em, float3 centerWorld,
    int tileRange, float damage, float warningDuration)
```

### BattleBridge.cs — ApplyMeteor

```csharp
float3 centerWorld = GridToWorldCenter(tile);
int    tileRange   = GridMath.RangeToTiles(skill.range);
float  radiusWorld = tileRange * tileSize;  // VFX 전용

EffectSpawner.SpawnMeteor(_em, centerWorld, tileRange, skill.magnitude, warn);
SpawnMeteorWarningVisual(centerWorld, radiusWorld, warn);

// preview count
if (!InTileRange(pos, tile, tileRange)) continue;
```

### MeteorResolutionSystem.cs — Chebyshev 체크

기존:
```csharp
float dx = pos.x - meteor.centerWorld.x;
float dz = pos.z - meteor.centerWorld.z;
if (dx * dx + dz * dz > meteor.radius * meteor.radius) continue;
...
burstWriter.Enqueue(new MeteorBurstEvent { ... radius = meteor.radius ... });
```

변경:
```csharp
// FlowFieldSingleton 은 이미 읽고 있음
int2 entityCell = GridMath.WorldToCell(pos, flowField.tileSize, flowField.gridSize);
int2 centerCell = GridMath.WorldToCell(meteor.centerWorld, flowField.tileSize, flowField.gridSize);
if (GridMath.ChebyshevDistance(entityCell, centerCell) > meteor.tileRange) continue;
...
// MeteorBurstEvent.radius 는 world 단위 (VFX 소비) — 환산해서 넘김
burstWriter.Enqueue(new MeteorBurstEvent { ... radius = meteor.tileRange * flowField.tileSize ... });
```

`MeteorBurstEvent.radius` 타입은 float 유지 — BattleBridge VFX 쪽이 world 단위로 소비.

## 완료 기준

- [ ] compile error 0
- [ ] PlayMode: Meteor AoE 가 정사각형 범위로 피해 적용 (대각 포함)
- [ ] PlayMode: 경고 링 VFX 크기 이상 없음
- [ ] PlayMode: 버스트 VFX 크기 이상 없음
- [ ] 기존 Meteor 데미지/타이밍 회귀 없음
