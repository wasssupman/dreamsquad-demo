# MapView 4-Tile Materials

**작업 구분**: Phase 10A

## 목적

`MapView` 를 GeneratedMap 기반으로 전환. 4 타일 타입 각각 Material 구분 + cube primitive 렌더링 유지. Phase 10B 에서 obstacle prefab 이 얹히기 전의 임시 시각.

## 변경 대상

- Modify: `Assets/_Project/Scripts/Core/MapView.cs`

## 구현

### Initialize 시그니처

기존 Phase 9:
```csharp
public void Initialize(MapData map, float tileSize)
{
    _map = map;
    _tileSize = tileSize;
    BuildSharedMaterials();
    BuildTiles();
}
```

신규:
```csharp
private GeneratedMap _map;
private float _tileSize;

public void Initialize(GeneratedMap map, float tileSize)
{
    _map = map;
    _tileSize = tileSize;
    BuildSharedMaterials();
    BuildTiles();
}
```

### BuildSharedMaterials — 4 타입 각 색상

```csharp
private readonly Dictionary<MapTileType, Material> _tileMaterials = new();

private void BuildSharedMaterials()
{
    var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
    _tileMaterials[MapTileType.Walk]  = new Material(shader) { color = new Color(0.95f, 0.75f, 0.40f) }; // 기존 path 색
    _tileMaterials[MapTileType.Place] = new Material(shader) { color = new Color(0.85f, 0.85f, 0.85f) }; // 기존 buildable 색
    _tileMaterials[MapTileType.Env]   = new Material(shader) { color = new Color(0.55f, 0.70f, 0.35f) }; // 초록 (환경)
    _tileMaterials[MapTileType.Deco]  = new Material(shader) { color = new Color(0.25f, 0.25f, 0.30f) }; // 기존 obstacle 색
}
```

### BuildTiles

```csharp
private void BuildTiles()
{
    var tilesRoot = new GameObject("Tiles");
    tilesRoot.transform.SetParent(transform, false);
    int w = _map.gridSize.x, h = _map.gridSize.y;
    for (int y = 0; y < h; y++)
    for (int x = 0; x < w; x++)
    {
        var type = _map.TileAt(new int2(x, y));
        var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
        cube.name = $"Tile_{x}_{y}_{type}";
        cube.transform.SetParent(tilesRoot.transform, false);
        cube.transform.localPosition = new Vector3(x * _tileSize, 0f, y * _tileSize);
        cube.transform.localScale = new Vector3(_tileSize * 0.95f, 0.1f, _tileSize * 0.95f);
        var r = cube.GetComponent<Renderer>();
        r.sharedMaterial = _tileMaterials[type];
        if (type == MapTileType.Place) _buildableRenderers[new Vector2Int(x, y)] = r;
        var col = cube.GetComponent<Collider>();
        if (col != null) Destroy(col);
    }
}
```

### Flash API (Phase 9 유지)

`FlashTileReject(Vector2Int cell)` 메서드 + FlashCoroutine + `_buildableRenderers` dict + `_activeFlashes` dict 그대로 유지. 단 `MapTileType.Place` 만 dict 에 등록.

### OnDestroy

기존 `_lineMaterial` 해제 (Phase 9 P9-07 에서 제거됨) 유지. 4 타일 material dict 해제 추가:
```csharp
private void OnDestroy()
{
    foreach (var m in _tileMaterials.Values) SafeDestroy(m);
    _tileMaterials.Clear();
    if (obstaclesRoot != null) Destroy(obstaclesRoot);  // Phase 10B obstacle root 예약
}
```

## 미래 확장 (Phase 10B task 13/14)

- 4-color cube 는 Phase 10B 에서 theme prefab Instantiate 로 확장 예정
- `obstaclesRoot` 부분은 Phase 10B `ObstaclePlacer` 가 사용

## 완료 기준

- 컴파일 0 errors.
- PlayMode smoke: PrototypeMap 진입 → 4 종 색상이 구분되어 보임 (Walk 주황, Place 회색, Env 초록, Deco 짙은회색).
- `FlashTileReject` 가 Place 타일에서 정상 동작 (Phase 9 기능 회귀 없음).
