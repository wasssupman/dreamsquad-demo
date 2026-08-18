using UnityEngine;

namespace Wassup.Core
{
    // map-diorama-stage unit 0 — 웨이포인트 루트 선언(선택 저작). 런타임 로직 0.
    // 같은 routeIndex 의 마커를 order 오름차순으로 이으면 경로 하나가 된다.
    // 루트 마커가 하나도 없으면 전 lane 골 직행(-1)이 열린 마당의 기본값이다 (unit 1).
    // 지형 메쉬에 그려 넣은 «시각적 길»은 논리가 모른다 — 적이 그 길을 따르게 하는 수단이 이것이다.
    [DisallowMultipleComponent]
    public class RouteMarker : MonoBehaviour
    {
        [Tooltip("경로 번호. MapDocument.waypointPaths 인덱스의 후계 — AttackUnitData.waypointPathIndex/spawnRoutes 가 이 번호를 가리킨다.")]
        [Min(0)] public int routeIndex;

        [Tooltip("경로 내 순번(0부터). 같은 routeIndex 안에서 오름차순으로 이어진다.")]
        [Min(0)] public int order;

#if UNITY_EDITOR
        void OnDrawGizmos()
        {
            if (!MapStageGizmoUtil.TryGetStage(this, out var stage)) return;
            Vector2Int cell = MapStageGizmoUtil.CellOf(stage, this);
            MapStageGizmoUtil.DrawCell(stage, cell, new Color(0.6f, 0.4f, 1f, 0.5f));
            MapStageGizmoUtil.Label(stage, cell, $"R{routeIndex}.{order}");

            // 다음 순번으로 선 하나 — 각 마커가 자기 다음 구간만 그리면 체인 전체가 이어진다.
            RouteMarker next = null;
            foreach (var m in stage.GetComponentsInChildren<RouteMarker>(true))
            {
                if (m == this || m.routeIndex != routeIndex || m.order <= order) continue;
                if (next == null || m.order < next.order) next = m;
            }
            if (next != null)
            {
                Gizmos.matrix = Matrix4x4.identity;
                Gizmos.color = new Color(0.6f, 0.4f, 1f, 0.9f);
                Gizmos.DrawLine(transform.position + Vector3.up * 0.05f, next.transform.position + Vector3.up * 0.05f);
            }
        }
#endif
    }
}
