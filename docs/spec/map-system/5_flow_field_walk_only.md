# FlowFieldBuilder Walk-Only

**작업 구분**: Phase 10A

## 목적

Phase 9 P9-12 회귀 fix (`walkable = TileType.Path only`) 를 새 `MapTileType.Walk` 기반으로 전환. `BattleBridge.BuildFlowField` 가 GeneratedMap 을 받아 walkmask 생성하도록 교체.

## 변경 대상

- Modify: `Assets/_Project/Scripts/Bridge/BattleBridge.cs` (`BuildFlowField` 메서드)

## 구현

### 기존 (Phase 9 P9-12 fix)

```csharp
private void BuildFlowField()
{
    if (map == null || _em == null) return;
    TeardownFlowField();
    int w = MapData.Width, h = MapData.Height, n = w * h;
    var walk = new NativeArray<byte>(n, Allocator.Temp);
    try
    {
        for (int y = 0; y < h; y++)
        for (int x = 0; x < w; x++)
        {
            var t = map.GetTile(x, y);
            walk[y * w + x] = (byte)(t == TileType.Path ? 1 : 0);
        }
        // ... FlowFieldBuilder.Build + singleton 등록
    }
    finally { if (walk.IsCreated) walk.Dispose(); }
}
```

### Phase 10A 교체

```csharp
private void BuildFlowField()
{
    if (!_generatedMap.IsCreated || _em == null) return;
    TeardownFlowField();

    int w = _generatedMap.gridSize.x, h = _generatedMap.gridSize.y, n = w * h;
    var walk = new NativeArray<byte>(n, Allocator.Temp);
    try
    {
        for (int i = 0; i < n; i++)
            walk[i] = (byte)(_generatedMap.tiles[i] == MapTileType.Walk ? 1 : 0);

        var flow = new NativeArray<float2>(n, Allocator.Persistent);
        var dist = new NativeArray<int>(n, Allocator.Persistent);
        try
        {
            var gridSize = _generatedMap.gridSize;
            var goal = _generatedMap.goal;
            FlowFieldBuilder.Build(walk, gridSize, goal, flow, dist);

            var data = new FlowFieldSingleton
            {
                flow = flow, dist = dist,
                gridSize = gridSize,
                goalCell = goal,
                tileSize = tileSize,
                version = _generatedMap.generatorVersion,
            };
            _flowFieldSingleton = _em.CreateEntity();
            _em.AddComponentData(_flowFieldSingleton, data);
        }
        catch
        {
            if (flow.IsCreated) flow.Dispose();
            if (dist.IsCreated) dist.Dispose();
            throw;
        }
    }
    finally { if (walk.IsCreated) walk.Dispose(); }
}
```

### 주요 변경

- `MapData.Width/Height` 하드코딩 → `_generatedMap.gridSize` (X×Y 가변 지원)
- 2차원 loop → 1차원 loop (`_generatedMap.tiles` 가 이미 flat NativeArray)
- `map.GetTile` → `_generatedMap.tiles[i]`
- walk 판정: `TileType.Path` → `MapTileType.Walk`
- `FlowFieldSingleton.version` 에 `generatorVersion` 저장 (Phase 10B seed 로그에서 재활용)

## FlowFieldBuilder 자체

`Assets/_Project/Scripts/Battle/Effects/FlowFieldBuilder.cs` 는 수정 없음. 이미 `NativeArray<byte> walkMask, int2 gridSize, int2 goal` 시그니처로 가변 크기 지원.

## 완료 기준

- 컴파일 0 errors.
- EditMode `FlowFieldBuilderTests` 3종 전부 PASS.
- PlayMode smoke: PrototypeMap fixture → 기존 flow field 와 동일 결과 (Walk 셀만 통과).
- 20×20 크기로 변경 시 FlowFieldSingleton.flow.Length == 400 확인 (9_multispawn_connectivity 테스트에서).

## Subtask 분할 (OVERRUN 대응, 30분 예상)

- **5A** — `BuildFlowField` 시그니처 변경 (map → _generatedMap) + walkmask = Walk only
- **5B** — try/catch leak 보호 재확인 (Phase 9 패턴 유지)
- **5C** — 20×20 gridSize 회귀 테스트 추가 (FlowFieldBuilderTests 확장)
