# Unit 8 — EditMode 테스트 통합

## 목적

단위 0~5 의 EditMode 테스트가 모두 모인 상태에서, 양극단 그리드 케이스와 시드 전수 회귀를 추가해 결정성·제약·실패 모드를 lock-in 한다. 본 unit 의 테스트는 단위 0~5 에서 이미 작성한 테스트의 **상위 통합/회귀** 만 다룬다. 중복 작성 금지.

## 변경 대상

- 신설: `Assets/_Project/Tests/EditMode/MapGrid/MapGridIntegrationTests.cs`
- 신설: `Assets/_Project/Tests/EditMode/MapGrid/MapGridSeedSweepTests.cs`

## 구현

### `MapGridIntegrationTests`

엔드투엔드 (Settings → Pick → Build → Validate → Bake → GeneratedMap) 시나리오:

```csharp
[Test]
public void Integration_DefaultSettings_Wide30x15_Seed42_ProducesValidGeneratedMap()
{
    var settings = MapGridGenerationSettingsFactory.Default();  // 테스트 헬퍼
    using var map = MapGridGenerator.Generate(42, new int2(30, 15), settings, Allocator.TempJob);

    Assert.IsTrue(map.IsCreated);
    Assert.AreEqual(30 * 15, map.tiles.Length);
    Assert.AreEqual(30 * 15, map.mergeDegree.Length);
    Assert.AreEqual(30 * 15, map.chokepoint.Length);
    Assert.AreEqual(30 * 15, map.propLayerId.Length);

    int goalIdx = map.goal.y * 30 + map.goal.x;
    Assert.AreEqual(MapTileType.Walk, map.tiles[goalIdx]);
    Assert.AreEqual((byte)1, map.mergeDegree[goalIdx]);

    foreach (var s in map.spawns)
    {
        int sIdx = s.y * 30 + s.x;
        Assert.AreEqual(MapTileType.Walk, map.tiles[sIdx]);
        Assert.AreEqual((byte)1, map.mergeDegree[sIdx]);
    }
}
```

추가 케이스:
- `Integration_Tall10x20_Seed0_Succeeds`.
- `Integration_Square20x20_Seed0_Succeeds`.
- `Integration_RoundTrip_GeneratedMap_To_MapDocument_To_GeneratedMap`: `MapDocumentBuilder.WriteToDocument` → `MapDocumentBuilder.ToGeneratedMap` 결과가 원본과 모든 필드 동일.

### `MapGridSeedSweepTests`

```csharp
[TestCase(MapGridPreset.Wide30x15)]
[TestCase(MapGridPreset.Square20x20)]
[TestCase(MapGridPreset.Tall10x20)]
public void Sweep_100Seeds_Preset_PassesQualityBar(MapGridPreset preset)
{
    var settings = MapGridGenerationSettingsFactory.Default();
    var size = MapGridGenerationSettings.PresetToGridSize(preset);

    int success = 0, totalAttempts = 0, withChokepoint = 0;

    for (int seed = 0; seed < 100; seed++)
    {
        GeneratedMap map = default;
        int attempts = 0;
        try { map = MapGridGenerator.Generate(seed, size, settings, Allocator.TempJob, out attempts); }
        catch (MapGenerationFailedException) { continue; }
        success++;
        totalAttempts += attempts;

        int chokepoints = 0;
        for (int i = 0; i < map.chokepoint.Length; i++) if (map.chokepoint[i] != 0) chokepoints++;
        if (chokepoints > 0) withChokepoint++;

        map.Dispose();
    }

    Assert.GreaterOrEqual(success, 95, $"{preset}: 성공률 ≥ 95 % 필요");
    Assert.LessOrEqual(totalAttempts / (float)success, 50f, $"{preset}: 평균 attempt ≤ 50 필요");
    Assert.GreaterOrEqual(withChokepoint, success * 0.7f, $"{preset}: 합류 셀이 있는 맵 비율 ≥ 70 % (단조로움 방지)");
}
```

### 헬퍼: `MapGridGenerationSettingsFactory`

테스트 전용 정적 헬퍼 — Inspector 가 아닌 코드에서 SO 인스턴스 생성:

```csharp
internal static class MapGridGenerationSettingsFactory
{
    public static MapGridGenerationSettings Default()
    {
        var s = ScriptableObject.CreateInstance<MapGridGenerationSettings>();
        // reflection 또는 internal setter 로 default 값 주입
        return s;
    }
}
```

이 헬퍼는 `Wassup.Tests.EditMode` asmdef 의 `InternalsVisibleTo` 또는 `MapGridGenerationSettings` 의 internal API 를 통해 동작.

### 테스트 전용 attempt 카운터

`MapGridGenerator.Generate(... , out int attemptCount)` 오버로드만 사용 (`LastAttemptCount` 정적 필드 노출 금지 — 동시성/리엔트런시 위험). sweep 테스트는 이 오버로드로 평균 attempt 측정.

### 추가 회귀 케이스

- `HashSeed_NoCollisionsAcrossAttempts`: `seed ∈ {0..99} × attempt ∈ {0..599} × generatorVersion=1` 의 60,000 조합에서 unique uint 비율 ≥ 99.9 %.
- `Integration_AuthoringSeedNegative_MeansManual`: `MapDocument.authoringSeed = -1` 인 문서가 `MapDocumentBuilder.ToGeneratedMap` 후에도 `seed = -1` 로 전달 (BattleBridge 가 manual 임을 인지할 수 있도록).

## 완료 기준

- [ ] `MapGridIntegrationTests` 4 케이스 통과.
- [ ] `MapGridSeedSweepTests` 3 프리셋 × 100 seed = 300 케이스에서 성공률 ≥ 95 %, 평균 attempt ≤ 50, chokepoint 보유율 ≥ 70 %.
- [ ] EditMode 전체 (unit 0~5 + 본 unit) 실행 시간 ≤ 60 초.
- [ ] CI 또는 로컬 EditMode runner 에서 0 실패.
- [ ] 확인 일자 + 커밋 해시 (구현 후 채움):
