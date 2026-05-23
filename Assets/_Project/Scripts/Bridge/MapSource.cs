namespace Wassup.Bridge
{
    // map-grid-generation spec — BattleBridge 의 GeneratedMap 소스 선택.
    public enum MapSource : byte
    {
        Legacy = 0,             // 기존 if/else 행동 (useProcedural 따라 분기) — 마이그레이션 호환 기본값.
        Manual = 1,             // ManualMapInput.Value 경로.
        Fixture = 2,            // BattleMapBuilder.BuildFromFixture(map, ...).
        Procedural_Legacy = 3,  // ProceduralMapGenerator.Generate — deprecated. cleanup spec 에서 제거.
        MapGrid = 4,            // 신: MapGridGenerator.Generate + MapDocument.
    }
}
