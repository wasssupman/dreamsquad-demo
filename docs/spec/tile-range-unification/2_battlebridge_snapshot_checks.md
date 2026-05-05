# 2. BattleBridge 스냅샷 범위 체크 + 시너지 8방향

## 목적

BattleBridge 의 스냅샷 범위 체크 6종을 Chebyshev 로 교체하고,  
시너지 인접 판정을 4방향에서 8방향으로 확장한다.

## 변경 대상

- `Assets/_Project/Scripts/Bridge/BattleBridge.cs`

## 구현

### 공통 인라인 헬퍼 (BattleBridge private)

```csharp
private bool InTileRange(float3 worldPos, Vector2Int originTile, int range)
{
    var cell   = GridMath.WorldToCell(worldPos, tileSize,
                     new int2(_generatedMap.gridSize.x, _generatedMap.gridSize.y));
    var origin = new int2(originTile.x, originTile.y);
    return GridMath.ChebyshevDistance(cell, origin) <= range;
}
```

모든 아래 체크는 이 헬퍼를 사용하거나 동일 패턴 인라인.

---

### ① ApplySlow (line ~1241)

```csharp
int tileRange = GridMath.RangeToTiles(skill.range);
...
if (!InTileRange(pos, tile, tileRange)) continue;
```

`rangeWorld` / `rangeSq` 제거.

---

### ② ApplyOnPlaceEffect — SlowPulse (line ~1704)

```csharp
int tileRange = GridMath.RangeToTiles(unitData.onPlaceRange);
...
if (!InTileRange(pos, placedCell, tileRange)) continue;
```

---

### ③ ApplyOnPlaceEffect — BindNearby (line ~1722)

동일 패턴. `InTileRange` 적용.

---

### ④ ApplyOnPlaceEffect — MeleeBurst (line ~1740)

동일 패턴. `InTileRange` 적용.

---

### ⑤ BoostNearbyDefenders

`BoostNearbyDefenders` 내 Euclidean 체크 → `InTileRange` 로 교체.  
대상 인자는 `placedCell` (배치 타일 기준).

---

### ⑥ onPlacePushRadius (push-CC 효과)

push CC 루프의 `dx*dx + dz*dz > rangeSq` → `InTileRange` 적용.

---

### RecomputeSynergyFor — 4방향 → 8방향 (line ~1864)

기존 4개 `TryGetValue` 하드코딩 제거:

```csharp
int neighbors = 0;
for (int dx = -1; dx <= 1; dx++)
for (int dz = -1; dz <= 1; dz++)
{
    if (dx == 0 && dz == 0) continue;
    if (_defenderByTile.TryGetValue(c + new Vector2Int(dx, dz), out var n)
        && n.data == here.data
        && _em.Exists(n.entity)
        && !_em.HasComponent<PendingDeployment>(n.entity))
        neighbors++;
}
```

## 완료 기준

- [ ] compile error 0
- [ ] PlayMode: SlowPulse defender 배치 시 대각 1칸 적도 슬로우 적용
- [ ] PlayMode: 동종 defender 대각 배치 시 시너지 버프 적용 (기존 직교만 됐던 것 확장)
- [ ] PlayMode: BoostNearbyDefenders 대각 1칸 defender 에도 버프 적용
- [ ] 기존 직교 케이스 회귀 없음
