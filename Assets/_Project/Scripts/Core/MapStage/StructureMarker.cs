using UnityEngine;
using Wassup.Data;

namespace Wassup.Core
{
    // map-diorama-stage unit 10 — 거점(본능) 셀 선언. 런타임 로직 0.
    // MapDocument.structures(StructureEntry) 후계 — 스캐너가 StructureEntry 로 옮기고 빌더가
    // GeneratedMap.structures(cell + faction) 로 투영한다. 체력·프랍·공격은 StructureData SO 가 소유하고
    // 브리지가 맵 빌드(뷰)/전투 시작(엔티티)에 세운다 — 여기에 비주얼 자식을 두지 않는다.
    // footprint(본능 3×3)는 **점유**(배치 배제 + OccupiedCells)이고 통행 차단이 아니다(instinct-content unit 1)
    // — PropFootprint 를 겹치지 말 것. 마음(Core)은 계약 11 로 비가용 — 빌더가 거부한다.
    [DisallowMultipleComponent]
    public class StructureMarker : MonoBehaviour
    {
        [Tooltip("편. 진영 비트는 side × data.kind 로 파생된다(StructurePlacements.DeriveFaction).")]
        public StructureSide side = StructureSide.Enemy;

        [Tooltip("거점 SO(체력·프랍·공격). kind=Instinct 만 허용 — 마음은 계약 11 비가용.")]
        public StructureData data;

#if UNITY_EDITOR
        void OnDrawGizmos()
        {
            if (!MapStageGizmoUtil.TryGetStage(this, out var stage)) return;
            Vector2Int center = MapStageGizmoUtil.CellOf(stage, this);
            bool instinct = data != null && data.kind == StructureKind.Instinct;
            int half = (instinct ? StructurePlacements.InstinctFootprint : StructurePlacements.CoreFootprint) / 2;
            Color fill = side == StructureSide.Defender ? new Color(0.3f, 0.55f, 1f, 0.45f)
                       : side == StructureSide.Enemy    ? new Color(1f, 0.35f, 0.3f, 0.45f)
                                                        : new Color(0.7f, 0.7f, 0.7f, 0.45f);
            for (int dy = -half; dy <= half; dy++)
                for (int dx = -half; dx <= half; dx++)
                    MapStageGizmoUtil.DrawCell(stage, center + new Vector2Int(dx, dy), fill);
            MapStageGizmoUtil.Label(stage, center, data == null ? "?" : instinct ? "I" : "C");
        }
#endif
    }
}
