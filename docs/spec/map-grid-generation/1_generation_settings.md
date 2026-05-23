# Unit 1 — GenerationSettings SO

## 목적

새 절차적 생성기의 모든 정책/하이퍼파라미터를 단일 ScriptableObject 로 외부화한다. 하드코딩 수치는 0개를 목표로 한다. `generatorVersion` 은 로직 변경마다 +1 한다.

## 변경 대상

- 신설: `Assets/_Project/Scripts/Data/MapGrid/MapGridGenerationSettings.cs`
- 신설: `Assets/_Project/Data/Maps/MapGridGenerationSettings_Default.asset` (Editor 에서 수동 생성)
- 신설: `Assets/_Project/Tests/EditMode/MapGrid/MapGridGenerationSettingsTests.cs`

## 구현

```csharp
namespace Wassup.Data.MapGrid
{
    public enum MapGridPreset : byte { Wide30x15 = 0, Square20x20 = 1, Tall10x20 = 2 }

    [CreateAssetMenu(fileName = "MapGridGenerationSettings",
                     menuName = "Wassup/Map/MapGridGenerationSettings", order = 2)]
    public class MapGridGenerationSettings : ScriptableObject
    {
        [Header("Grid Preset Pool")]
        [SerializeField] private MapGridPreset[] allowedPresets = {
            MapGridPreset.Wide30x15, MapGridPreset.Square20x20, MapGridPreset.Tall10x20
        };

        [Header("Spawn Policy")]
        [SerializeField, Range(2, 4)] private int minSpawnCount = 2;
        [SerializeField, Range(2, 4)] private int maxSpawnCount = 4;
        // spawn↔goal Manhattan 하한. 작은 그리드(10×20)도 통과하도록 default 6.
        // EffectiveSpawnToGoalMinManhattan(grid) = max(this, min(W,H) - 4) 로 그리드 비례 스케일.
        [SerializeField] private int spawnToGoalMinManhattan = 6;
        [SerializeField] private int spawnToSpawnMinManhattan = 3;
        [SerializeField, Range(1, 4)] private int cornerZoneRadius = 3; // 분면 외곽 코너 zone 반경

        [Header("Path Constraints (validator)")]
        // path 위 셀 수. Manhattan 거리와 다른 차원 (실제 경로 길이).
        [SerializeField] private int minBranchCellCount = 8;
        [SerializeField] private int minBranchTurnCount = 3;

        [Header("Generation Attempts")]
        [SerializeField] private int maxMapAttempts = 600;
        [SerializeField] private int maxRouteAttempts = 160;
        [SerializeField] private int routeCandidateMidpointSamples = 28;

        [Header("Versioning")]
        [SerializeField] private int generatorVersion = 1;

        public IReadOnlyList<MapGridPreset> AllowedPresets => allowedPresets;
        public int MinSpawnCount => minSpawnCount;
        public int MaxSpawnCount => maxSpawnCount;
        public int SpawnToGoalMinManhattan => spawnToGoalMinManhattan;
        public int SpawnToSpawnMinManhattan => spawnToSpawnMinManhattan;
        public int CornerZoneRadius => cornerZoneRadius;
        public int MinBranchCellCount => minBranchCellCount;
        public int MinBranchTurnCount => minBranchTurnCount;
        public int MaxMapAttempts => maxMapAttempts;
        public int MaxRouteAttempts => maxRouteAttempts;
        public int RouteCandidateMidpointSamples => routeCandidateMidpointSamples;
        public int GeneratorVersion => generatorVersion;

        public static int2 PresetToGridSize(MapGridPreset preset) => preset switch
        {
            MapGridPreset.Wide30x15  => new int2(30, 15),
            MapGridPreset.Square20x20 => new int2(20, 20),
            MapGridPreset.Tall10x20  => new int2(10, 20),
            _ => new int2(20, 10),
        };

        public int EffectiveMinBranchCellCount(int2 gridSize) =>
            math.max(minBranchCellCount, math.min(gridSize.x, gridSize.y) / 2);

        // 작은 그리드는 너무 빡빡해지지 않도록 min(W,H)-4 와 SO default 의 max.
        // 10×20 → max(6, 6)=6, 20×20 → max(6, 16)=16... 너무 큼.
        // 따라서 작은 축에 비례하는 더 부드러운 스케일 사용:
        public int EffectiveSpawnToGoalMinManhattan(int2 gridSize)
        {
            int scaled = math.max(4, math.min(gridSize.x, gridSize.y) - 4);
            return math.max(spawnToGoalMinManhattan, scaled);
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            minSpawnCount = math.clamp(minSpawnCount, 2, 4);
            maxSpawnCount = math.clamp(maxSpawnCount, minSpawnCount, 4);
        }
#endif
    }
}
```

### EditMode 테스트

- `PresetToGridSize_AllPresets_MatchSpec`: Wide30x15 → (30,15), Square20x20 → (20,20), Tall10x20 → (10,20).
- `EffectiveMinBranchCellCount_SmallGrid_RespectsFloor`: 10×20 grid + minBranchCellCount=8 → 8. (min(10,20)/2 = 5, max(8,5) = 8)
- `EffectiveMinBranchCellCount_LargeGrid_ScalesUp`: 30×15 grid + minBranchCellCount=4 → max(4, min(30,15)/2=7) = 7. (formula 가 floor 를 끌어올림)
- `OnValidate_MaxSpawnCountClampedToMin`: minSpawnCount=4, maxSpawnCount=2 입력 후 OnValidate → maxSpawnCount=4.

## 완료 기준

- [ ] `MapGridGenerationSettings.cs` 컴파일.
- [ ] `MapGridGenerationSettings_Default.asset` 생성 후 Inspector 에서 모든 필드 노출.
- [ ] EditMode 테스트 4 케이스 통과.
- [ ] 하드코딩 수치는 `generatorVersion = 1` 의 default 외엔 SO 의 default 만 존재. 후속 단위(2~4)는 SO 참조만 사용.
- [ ] 확인 일자 + 커밋 해시 (구현 후 채움):
