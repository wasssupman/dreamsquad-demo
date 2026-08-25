using UnityEngine;

namespace Wassup.Core
{
    // map-diorama-stage unit 9 — 보너스 당기기 포탈 칸 선언(선택 저작). 런타임 로직 0.
    // bonus-wave-pull 의 MapDocument.bonusSpawns 후계 — 빌더가 GeneratedMap.bonusSpawns 로 투영하고
    // 규칙(0개 또는 정확히 2개 · 서로 다른 칸 · 통행 가능 · 골 도달 가능)은 BonusSpawnAuthoringRules
    // 단일 소유자가 검사한다. 마커가 없으면 «보너스 당기기 없는 맵»(bonus-wave-pull 계약 8) — 버튼이 뜨지 않는다.
    // 포탈 비주얼은 저작하지 않는다 — 웨이브 수명으로 BattleBridge.BonusWave 가 띄우고 지운다.
    [DisallowMultipleComponent]
    public class BonusSpawnMarker : MonoBehaviour
    {
#if UNITY_EDITOR
        void OnDrawGizmos()
        {
            if (!MapStageGizmoUtil.TryGetStage(this, out var stage)) return;
            Vector2Int cell = MapStageGizmoUtil.CellOf(stage, this);
            MapStageGizmoUtil.DrawCell(stage, cell, new Color(1f, 0.3f, 0.75f, 0.55f));
            MapStageGizmoUtil.Label(stage, cell, "B");
        }
#endif
    }
}
