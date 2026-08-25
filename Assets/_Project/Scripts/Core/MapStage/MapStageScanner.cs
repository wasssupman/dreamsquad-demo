using UnityEngine;
using Wassup.Data;

namespace Wassup.Core
{
    // map-diorama-stage unit 1 — 스테이지 컴포넌트 → StageScan(plain) 얇은 변환.
    // 양자화는 MapStageMath 단일 산식(기즈모와 동일), 좌표는 스테이지 로컬 기준 —
    // 스테이지 프리팹이 씬 어디에 있든 같은 맵이 나온다. 계산/검증은 전부 DioramaMapBuilder(Data) 몫.
    public static class MapStageScanner
    {
        public static StageScan Scan(MapStage stage, float runtimeTileSize)
        {
            var scan = new StageScan
            {
                playAreaCells = stage.playAreaCells,
                previewTileSize = stage.previewTileSize,
                runtimeTileSize = runtimeTileSize,
            };

            // 비활성 오브젝트는 제외 — 프랍을 끄는 것이 «이 맵에서 뺀다»는 저작 제스처다.
            foreach (var fp in stage.GetComponentsInChildren<PropFootprint>(false))
                scan.blockedRects.Add(MapStageMath.FootprintCells(
                    CellOf(stage, fp.transform, runtimeTileSize), fp.anchorOffset, fp.size));

            foreach (var zone in stage.GetComponentsInChildren<PlacementBlockZone>(false))
                scan.placementBlockRects.Add(MapStageMath.FootprintCells(
                    CellOf(stage, zone.transform, runtimeTileSize), Vector2Int.zero, zone.size));

            foreach (var s in stage.GetComponentsInChildren<SpawnMarker>(false))
                scan.spawns.Add(new StageSpawnPoint
                {
                    cell = CellOf(stage, s.transform, runtimeTileSize),
                    laneIndex = s.laneIndex,
                    routeIndex = s.routeIndex,
                });

            foreach (var g in stage.GetComponentsInChildren<GoalMarker>(false))
                scan.goals.Add(CellOf(stage, g.transform, runtimeTileSize));

            foreach (var r in stage.GetComponentsInChildren<RouteMarker>(false))
                scan.routePoints.Add(new StageRoutePoint
                {
                    routeIndex = r.routeIndex,
                    order = r.order,
                    cell = CellOf(stage, r.transform, runtimeTileSize),
                });

            foreach (var b in stage.GetComponentsInChildren<BonusSpawnMarker>(false))
                scan.bonusSpawns.Add(CellOf(stage, b.transform, runtimeTileSize));

            return scan;
        }

        static Vector2Int CellOf(MapStage stage, Transform t, float tileSize)
        {
            Vector3 local = stage.transform.InverseTransformPoint(t.position);
            return MapStageMath.LocalToCell(local, stage.gridOriginLocal, tileSize);
        }
    }
}
