using UnityEngine;

namespace Wassup.Core
{
    // map-diorama-stage unit 0 — 골(방어 마음) 위치 선언. 런타임 로직 0.
    // 셀만 준다 — 골 HP 는 AttackDeck.goalStabilityMax 단독 소유(이중 저작 금지, critic Minor 3).
    // 뷰 앵커·균열·붕괴 연출 훅은 unit 4 에서 이 컴포넌트에 추가된다.
    [DisallowMultipleComponent]
    public class GoalMarker : MonoBehaviour
    {
#if UNITY_EDITOR
        void OnDrawGizmos()
        {
            if (!MapStageGizmoUtil.TryGetStage(this, out var stage)) return;
            Vector2Int cell = MapStageGizmoUtil.CellOf(stage, this);
            MapStageGizmoUtil.DrawCell(stage, cell, new Color(1f, 0.85f, 0.1f, 0.55f));
            MapStageGizmoUtil.Label(stage, cell, "G");
        }
#endif
    }
}
