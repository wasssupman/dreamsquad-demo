using UnityEngine;

namespace Wassup.Core
{
    // map-diorama-stage unit 0 — 적 스폰 지점 선언. 런타임 로직 0.
    // laneIndex 는 웨이브 결정론의 정본이다 (README 계약 5) — 씬 계층 순서에 기대지 않는다.
    // 빌더(unit 1)는 laneIndex 오름차순으로 spawns[] 를 만들고, 중복/공백 인덱스는 형식 오류다.
    [DisallowMultipleComponent]
    public class SpawnMarker : MonoBehaviour
    {
        [Tooltip("레인 번호(0부터 연속). 웨이브 생성 결정론 키 — 씬 계층 순서가 아니라 이 값이 정본.")]
        [Min(0)] public int laneIndex;

        [Tooltip("이 레인의 기본 웨이포인트 경로(RouteMarker.routeIndex). -1 = 골 직행(열린 마당 기본값). MapDocument.spawnRoutes 의 후계.")]
        [Min(-1)] public int routeIndex = -1;

#if UNITY_EDITOR
        void OnDrawGizmos()
        {
            if (!MapStageGizmoUtil.TryGetStage(this, out var stage)) return;
            Vector2Int cell = MapStageGizmoUtil.CellOf(stage, this);
            MapStageGizmoUtil.DrawCell(stage, cell, new Color(0.2f, 0.95f, 0.3f, 0.55f));
            MapStageGizmoUtil.Label(stage, cell, $"S{laneIndex}");
        }
#endif
    }
}
