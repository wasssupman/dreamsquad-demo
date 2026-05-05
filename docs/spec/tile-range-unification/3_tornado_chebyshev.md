# 3. TornadoField Chebyshev 전환

## 목적

`TornadoField` containment 체크를 Chebyshev 타일 거리로 교체한다.  
풀 방향 계산은 `centerWorld` (world-space) 유지. 풀링 지속.  
타일 경계 진동(boundary jitter)은 허용 범위로 수용.

## 변경 대상

- `Assets/_Project/Scripts/Battle/Effects/TornadoField.cs`
- `Assets/_Project/Scripts/Battle/Effects/EffectSpawner.cs`
- `Assets/_Project/Scripts/Battle/Movement/MovementSystem.cs`
- `Assets/_Project/Scripts/Bridge/BattleBridge.cs`

## 구현

### TornadoField.cs

```csharp
public struct TornadoField : IComponentData
{
    public float3 centerWorld;  // 풀 방향 계산용
    public int    tileRange;    // Chebyshev containment 범위
    public float  pullSpeed;
    public float  remaining;
}
```

### EffectSpawner.cs — SpawnTornadoField 시그니처

```csharp
public static Entity SpawnTornadoField(EntityManager em, float3 centerWorld,
    int tileRange, float pullSpeed, float duration)
```

### BattleBridge.cs — ApplyTornado

```csharp
float3 targetWorld = GridToWorldCenter(tile);
int    tileRange   = GridMath.RangeToTiles(skill.range);
float  rangeWorld  = tileRange * tileSize;  // VFX 전용

EffectSpawner.SpawnTornadoField(_em, targetWorld, tileRange, skill.magnitude, skill.durationSec);

if (vfxSpawner != null)
    vfxSpawner.SpawnTornado(new Vector3(targetWorld.x, 0f, targetWorld.z), rangeWorld, skill.durationSec);

// preview count
int preview = 0;
...
if (!InTileRange(pos, tile, tileRange)) continue;
preview++;
```

`InTileRange` 는 Unit 2 에서 추가된 BattleBridge private 헬퍼.

### MovementSystem.cs — containment 체크

`FlowFieldSingleton` 은 이미 읽고 있음. 동일 `flowField.tileSize` / `flowField.gridSize` 활용:

기존:
```csharp
float dx = pos.x - field.centerWorld.x;
float dz = pos.z - field.centerWorld.z;
if (math.lengthsq(new float2(dx, dz)) > field.radius * field.radius) continue;
```

변경:
```csharp
int2 entityCell = GridMath.WorldToCell(pos, flowField.tileSize, flowField.gridSize);
int2 centerCell = GridMath.WorldToCell(field.centerWorld, flowField.tileSize, flowField.gridSize);
if (GridMath.ChebyshevDistance(entityCell, centerCell) > field.tileRange) continue;

// 풀 방향은 world-space 유지
float3 toCenter = field.centerWorld - pos;
...
```

## 완료 기준

- [ ] compile error 0
- [ ] PlayMode: Tornado 대각 범위 적도 풀림
- [ ] PlayMode: 필드 밖 적은 풀리지 않음
- [ ] VFX 링 크기 시각적으로 이상 없음
- [ ] 풀링 지속 동작 회귀 없음
