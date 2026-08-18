using UnityEngine;

namespace Wassup.Core
{
    // map-diorama-stage unit 0 — 골(방어 마음) 위치 선언.
    // 셀만 준다 — 골 HP 는 AttackDeck.goalStabilityMax 단독 소유(이중 저작 금지, critic Minor 3).
    // unit 4 — 뷰 훅 추가: 앵커(튜토리얼 포커스)·균열 단계·붕괴 표시. 골 HP/판정은 심 소유,
    // 여기는 연출만. 상태는 매치 수명 — 스테이지 인스턴스가 판마다 새로 뜨므로 재빌드 = 원복.
    [DisallowMultipleComponent]
    public class GoalMarker : MonoBehaviour
    {
        [Tooltip("연출 대상 루트(앵커·틴트·붕괴 스케일). 비면 이 오브젝트 자신.")]
        public Transform visualRoot;

        private bool _collapsed;

        private Transform VisualRootOrSelf => visualRoot != null ? visualRoot : transform;

        // 튜토리얼 포커스 앵커 — 렌더러 바운즈 중심, 렌더러 없으면 위치 (구 의미 승계).
        public Vector3 VisualAnchor() => MarkerVisual.AnchorOf(VisualRootOrSelf);

        // 균열 단계 0~3. 온전(1,1,1)→3단계(0.42,0.36,0.32) — 붕괴색보다 밝게 남겨
        // «금이 갔다»와 «무너졌다»가 한눈에 갈린다 (구 TilemapMapView.SetGoalCrack 승계).
        public void SetCrackStage(int stage)
        {
            stage = Mathf.Clamp(stage, 0, 3);
            float k = stage / 3f;
            MarkerVisual.ApplyTint(VisualRootOrSelf, new Color(
                Mathf.Lerp(1f, 0.42f, k),
                Mathf.Lerp(1f, 0.36f, k),
                Mathf.Lerp(1f, 0.32f, k), 1f));
        }

        // 붕괴 — 어두운 틴트 + 60% 주저앉음(실루엣이 «무너졌다»를 말한다). 중복 호출 무해.
        public void MarkCollapsed()
        {
            if (_collapsed) return;
            _collapsed = true;
            MarkerVisual.ApplyTint(VisualRootOrSelf, new Color(0.22f, 0.19f, 0.17f, 1f));
            VisualRootOrSelf.localScale *= 0.6f;
        }

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
