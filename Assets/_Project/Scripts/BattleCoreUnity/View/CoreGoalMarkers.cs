using System.Collections.Generic;
using Unity.Mathematics;
using Wassup.Core;

namespace Wassup.BattleCoreUnity.View
{
    // battle-core-rebuild unit 8a2 — **스테이지 골 마커 ↔ 정의표 골 칸** 사상 하나.
    //
    // 옛 `BattleBridge.cs:1161-1169`(`_goalMarkersByCell` — 스테이지 로컬 → `MapStageMath.LocalToCell`)의 후계.
    // 마커는 연출의 host 다(스트레스 틴트·심박 · 붕괴 주저앉음 — `GoalMarker` 가 소유). 쓰는 곳이 둘(`CoreScoreHud` 심박 ·
    // `CoreVfxSpawner` 붕괴)이라 사상을 한 곳에 둔다 — 두 벌이면 한쪽만 칸 계산이 어긋나는 날 틴트와 붕괴가 다른 마커에 걸린다.
    public static class CoreGoalMarkers
    {
        /// <summary>그 판 스테이지에서 골 칸 위에 선 마커를 모은다(순서 = 하이어라키). 스테이지가 없으면 빈다.</summary>
        public static void Collect(BattleDriver driver, List<GoalMarker> into)
        {
            into.Clear();
            var stage = driver != null ? driver.StageRoot : null;
            var def = driver != null ? driver.Definition : null;
            if (stage == null || def == null) return;
            int2[] goals = def.Map.Goals;
            foreach (var marker in stage.GetComponentsInChildren<GoalMarker>(false))
            {
                var local = stage.transform.InverseTransformPoint(marker.transform.position);
                var cell = Wassup.Data.MapStageMath.LocalToCell(local, stage.gridOriginLocal, driver.TileSize);
                for (int i = 0; i < goals.Length; i++)
                {
                    if (goals[i].x != cell.x || goals[i].y != cell.y) continue;
                    into.Add(marker);
                    break;
                }
            }
        }
    }
}
