using Unity.Collections;

namespace Wassup.Data.MapGrid
{
    // map-pipeline-cleanup unit 4 — 절차 생성 폴백 체인 제거. authored MapDocument 가
    // 유일한 입력이며, unusable 문서는 조용한 폴백 대신 명확히 실패한다
    // (painter 가 bake 시 usable 을 강제하므로 unusable = authoring 버그 → 표면화).
    public static class MapGridBattleAdapter
    {
        public static GeneratedMap Build(MapDocument doc)
        {
            if (!IsUsableDocument(doc))
                throw new MapGenerationFailedException(
                    "[MapGridBattleAdapter] usable MapDocument 가 없다 — MapDocumentPool 배선/문서 bake 를 확인하라.");
            return MapDocumentBuilder.ToGeneratedMap(doc, Allocator.Persistent);
        }

        // Build 의 문서 소비 조건과 BattleBridge 의 connectivity-guard 판단을 한 곳으로 묶는다.
        public static bool IsUsableDocument(MapDocument doc) =>
            doc != null && doc.Width > 0 && doc.Tiles != null && doc.Tiles.Count > 0;
    }
}
