# Phase 10A EditMode Tests

**작업 구분**: Phase 10A

## 목적

Phase 10A task 0~9 결과의 결정성 + 회귀를 EditMode 로 검증. Phase 9 52/52 에 신규 테스트 추가.

## 변경 대상

- 새 파일들:
  - `Assets/_Project/Tests/EditMode/MapTileTypeTests.cs`
  - `Assets/_Project/Tests/EditMode/GeneratedMapTests.cs`
  - `Assets/_Project/Tests/EditMode/BattleMapBuilderTests.cs`
  - `Assets/_Project/Tests/EditMode/MapConnectivityTests.cs`

## 테스트 목록

### MapTileTypeTests

```csharp
[Test] public void EnumValues_AreOrderedZeroToThree()
{
    Assert.AreEqual(0, (byte)MapTileType.Walk);
    Assert.AreEqual(1, (byte)MapTileType.Place);
    Assert.AreEqual(2, (byte)MapTileType.Env);
    Assert.AreEqual(3, (byte)MapTileType.Deco);
}
```

### GeneratedMapTests

```csharp
[Test] public void CellIndex_IsRowMajor()
{
    // 20x10 grid, cell(3,2) → 2*20+3 = 43
    var map = new GeneratedMap { gridSize = new int2(20, 10) };
    Assert.AreEqual(43, map.CellIndex(new int2(3, 2)));
}

[Test] public void Dispose_Idempotent()
{
    var tiles = new NativeArray<MapTileType>(4, Allocator.Persistent);
    var spawns = new NativeArray<int2>(1, Allocator.Persistent);
    var map = new GeneratedMap { tiles = tiles, spawns = spawns };
    Assert.IsTrue(map.IsCreated);
    map.Dispose();
    Assert.IsFalse(map.IsCreated);
    map.Dispose();  // 두 번째 호출 throw 없음
}
```

### BattleMapBuilderTests

- try/finally 로 GeneratedMap dispose 보장

```csharp
[Test] public void BuildFromFixture_PreservesTileCounts()
{
    var proto = AssetDatabase.LoadAssetAtPath<MapData>("Assets/_Project/Scripts/Data/Maps/PrototypeMap.asset");
    var gm = BattleMapBuilder.BuildFromFixture(proto);
    try
    {
        Assert.AreEqual(200, gm.tiles.Length, "20x10");
        Assert.AreEqual(1, gm.spawns.Length, "single spawn in fixture");
        Assert.AreEqual(new int2(19, 5), gm.goal);
    }
    finally { gm.Dispose(); }
}

[Test] public void BuildFromFixture_MapsLegacyTypes()
{
    // Path → Walk, Buildable → Place, Obstacle → Deco
    var proto = AssetDatabase.LoadAssetAtPath<MapData>("Assets/_Project/Scripts/Data/Maps/PrototypeMap.asset");
    var gm = BattleMapBuilder.BuildFromFixture(proto);
    try
    {
        int walkCount = 0, placeCount = 0, decoCount = 0;
        for (int i = 0; i < gm.tiles.Length; i++)
        {
            if (gm.tiles[i] == MapTileType.Walk)  walkCount++;
            else if (gm.tiles[i] == MapTileType.Place) placeCount++;
            else if (gm.tiles[i] == MapTileType.Deco)  decoCount++;
        }
        Assert.Greater(walkCount, 0);
        Assert.Greater(placeCount, 0);
        // Deco 는 PrototypeMap Obstacle 타일이 있으면 양수
    }
    finally { gm.Dispose(); }
}

[Test] public void BuildFallbackLinear_MidRowIsWalkable()
{
    var gm = BattleMapBuilder.BuildFallbackLinear(new int2(20, 20));
    try
    {
        int midY = 10;
        for (int x = 0; x < 20; x++)
            Assert.AreEqual(MapTileType.Walk, gm.TileAt(new int2(x, midY)));
        Assert.AreEqual(new int2(0, 10), gm.spawns[0]);
        Assert.AreEqual(new int2(19, 10), gm.goal);
    }
    finally { gm.Dispose(); }
}
```

### MapConnectivityTests

```csharp
[Test] public void AllSpawnsReachGoal_PrototypeMap_True()
{
    var proto = AssetDatabase.LoadAssetAtPath<MapData>(...);
    var gm = BattleMapBuilder.BuildFromFixture(proto);
    try { Assert.IsTrue(MapConnectivity.AllSpawnsReachGoal(gm)); }
    finally { gm.Dispose(); }
}

[Test] public void AllSpawnsReachGoal_DisconnectedMap_False()
{
    var gm = BattleMapBuilder.BuildFallbackLinear(new int2(10, 10));
    try
    {
        // Walk 타일 전부 Deco 로 교체 → goal 도달 불가
        for (int i = 0; i < gm.tiles.Length; i++) gm.tiles[i] = MapTileType.Deco;
        Assert.IsFalse(MapConnectivity.AllSpawnsReachGoal(gm));
    }
    finally { gm.Dispose(); }
}
```

### 20×20 크기 회귀

- `BuildFlowField` 이 `gridSize = (20, 20)` 에서 flow.Length == 400 인지 확인하는 통합 테스트 1건 (기존 `FlowFieldBuilderTests` 에 추가 가능)

## 완료 기준

- 총 신규 EditMode 테스트 7~10개 모두 PASS.
- 기존 Phase 9 EditMode 52/52 여전히 PASS.
- 모든 NativeArray 사용처 try/finally dispose 적용.
