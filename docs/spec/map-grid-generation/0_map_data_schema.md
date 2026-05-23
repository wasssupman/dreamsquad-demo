# Unit 0 — MapData 스키마

## 목적

절차적 생성 결과와 손수 작성한 맵이 **완전히 동일한 SO 스키마** 를 가지도록 새 authoring SO (`MapDocument`) 를 도입하고, 런타임 `GeneratedMap` 에 셀 메타데이터 (`mergeDegree`, `chokepoint`, `propLayerId`) 평행 NativeArray 를 추가한다. authoring ↔ runtime 라운드트립이 EditMode 테스트로 결정성을 보장해야 한다.

기존 `Wassup.Data.MapData` (SO, `TileType` 3종) 은 **본 spec 범위 밖** — 단위 6 에서 어댑터로 어떻게 다룰지 결정하고, 본 unit 에서는 신규 SO 와 공존 가능하게만 둔다.

## 변경 대상

- 신설: `Assets/_Project/Scripts/Data/MapGrid/MapDocument.cs` (ScriptableObject) — `namespace Wassup.Data.MapGrid` (현재 트리에 이 namespace 0건, 충돌 없음 — `grep -r "namespace Wassup.Data.MapGrid"` 확인).
- 신설: `Assets/_Project/Scripts/Data/MapGrid/MapDocumentBuilder.cs` (static, `MapDocument` ↔ `GeneratedMap` 변환) — **반드시 동일 namespace, 동일 asmdef (`Wassup.Runtime`).** Editor 전용 asmdef 분리 금지 (internal setter 필요).
- 수정: `Assets/_Project/Scripts/Data/GeneratedMap.cs` — 메타 NativeArray 3개 + Dispose 확장.
- 신설: `Assets/_Project/Tests/EditMode/MapGrid/MapDocumentRoundTripTests.cs`.

`MapDocument.SetFrom` 은 `internal` — 같은 asmdef 안에서만 호출. `Wassup.Tests.EditMode` asmdef 의 InternalsVisibleTo 또는 `[InternalsVisibleTo]` 어트리뷰트 필요.

## 구현

### 1. `MapDocument` SO

```csharp
namespace Wassup.Data.MapGrid
{
    [CreateAssetMenu(fileName = "MapDocument", menuName = "Wassup/Map/MapDocument", order = 1)]
    public class MapDocument : ScriptableObject
    {
        [SerializeField] private int width = 20;
        [SerializeField] private int height = 10;
        [SerializeField] private MapTileType[] tiles;        // length = width * height, row-major (y * width + x)
        [SerializeField] private byte[] mergeDegree;          // length = width * height. path 외 셀은 0.
        [SerializeField] private bool[] chokepoint;           // length = width * height. degree >= 3 셀만 true.
        [SerializeField] private byte[] propLayerId;          // length = width * height. 0 = none.
        [SerializeField] private Vector2Int goal;
        [SerializeField] private Vector2Int[] spawns;
        // -1 = 수동 입력, 그 외 값 = 절차적 결과 캐시. cleanup spec 까지 의미 보존.
        [SerializeField] private int authoringSeed = -1;
        [SerializeField] private int generatorVersion;        // 절차적 생성기 버전. 수동 입력은 0.

        public int Width => width;
        public int Height => height;
        public IReadOnlyList<MapTileType> Tiles => tiles;
        public IReadOnlyList<byte> MergeDegree => mergeDegree;
        public IReadOnlyList<bool> Chokepoint => chokepoint;
        public IReadOnlyList<byte> PropLayerId => propLayerId;
        public Vector2Int Goal => goal;
        public IReadOnlyList<Vector2Int> Spawns => spawns;
        public int AuthoringSeed => authoringSeed;
        public int GeneratorVersion => generatorVersion;

#if UNITY_EDITOR
        private void OnValidate()
        {
            // 1. width/height ≥ 1
            // 2. tiles.Length == mergeDegree.Length == chokepoint.Length == propLayerId.Length == width * height
            //    틀리면 Debug.LogError 로 표시 (자동 보정 X — 데이터 무결성 우선)
            // 3. spawns.Length ∈ [1, 4] (1 은 수동 입력만 허용)
        }
#endif

        // 절차적 생성기가 채워넣는 setter (Editor + asmdef internal)
        internal void SetFrom(int w, int h, MapTileType[] t, byte[] md, bool[] cp, byte[] pl,
                              Vector2Int g, Vector2Int[] s, int seed, int version)
        { /* 필드 일괄 대입 + dirty mark */ }
    }
}
```

### 2. `GeneratedMap` 확장

```csharp
public struct GeneratedMap : IDisposable
{
    public NativeArray<MapTileType> tiles;
    public NativeArray<byte>        mergeDegree;   // NEW
    public NativeArray<byte>        chokepoint;    // NEW (0/1 byte, Burst 친화)
    public NativeArray<byte>        propLayerId;   // NEW
    public int2                     gridSize;
    public NativeArray<int2>        spawns;
    public int2                     goal;
    public int                      seed;
    public int                      generatorVersion;

    public bool IsCreated =>
        tiles.IsCreated && spawns.IsCreated
        && mergeDegree.IsCreated && chokepoint.IsCreated && propLayerId.IsCreated;

    public void Dispose()
    {
        if (tiles.IsCreated)       tiles.Dispose();
        if (spawns.IsCreated)      spawns.Dispose();
        if (mergeDegree.IsCreated) mergeDegree.Dispose();
        if (chokepoint.IsCreated)  chokepoint.Dispose();
        if (propLayerId.IsCreated) propLayerId.Dispose();
    }
}
```

### 3. `MapDocumentBuilder` (라운드트립)

```csharp
public static class MapDocumentBuilder
{
    public static GeneratedMap ToGeneratedMap(MapDocument doc, Allocator allocator)
    { /* tiles/md/cp/pl 4개 NativeArray + spawns 복사, goal/seed/version 채움 */ }

    public static void WriteToDocument(MapDocument doc, in GeneratedMap map)
    { /* runtime → authoring 역방향. 절차적 결과를 SO 로 저장할 때 사용. */ }
}
```

### 4. EditMode 테스트 (`MapDocumentRoundTripTests.cs`)

- `RoundTrip_TilesAndMeta_Identity`: `MapDocument` → `GeneratedMap` → `MapDocument'` 후 모든 필드 동일.
- `Dispose_IsIdempotent`: `Dispose` 두 번 호출해도 예외 없음.
- `IsCreated_PartialNativeArray_ReturnsFalse`: `tiles` 만 할당된 상태에서 `IsCreated == false`.

## 완료 기준

- [ ] `MapDocument.cs` 컴파일, `OnValidate` 경고만 검출.
- [ ] `GeneratedMap` Dispose 가 5 NativeArray 전부 안전 해제 (기존 사용처 회귀 없음).
- [ ] `MapDocumentRoundTripTests` 3 케이스 모두 통과.
- [ ] 기존 `Wassup.Data.MapData` 와 네임스페이스 충돌 없음 (`Wassup.Data.MapGrid`).
- [ ] `BattleBridge` 컴파일 그대로 통과 (이 unit 에선 BattleBridge 수정 없음).
- [ ] 확인 일자 + 커밋 해시 (구현 후 채움):
