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

        // heart-stress-axis unit 1 rev 2 승계 — 마음이 스트레스만큼 붉어지고 심박(밝기 배율)에 맞춰 뛴다.
        // 틴트 writer 는 이것 하나다 — SetCrackStage 는 브리지가 push 를 끊어 휴면(같은 렌더러 색을
        // 두 곳이 쓰면 마지막이 이겨 심박이 그을림으로 덮인다). 붕괴 뒤에는 쓰지 않는다.
        // 매 프레임 불리므로 렌더러 배열·MPB 를 캐시한다(구 뷰의 _propRenderers 캐시 승계 —
        // 안 그러면 프레임당 힙 할당 3건, 안드로이드 주 타겟에서 버리는 GC).
        [Tooltip("스트레스 100%일 때 마음의 색. 흰색(온전)에서 여기로 보간된다.")]
        public Color stressTint = new Color(0.95f, 0.13f, 0.11f, 1f);

        private SpriteRenderer[] _stressSprites;
        private Renderer[] _stressMeshes;
        private Color[] _stressMeshBase;   // unit 6 — 메쉬/파티클 머티리얼의 저작 _Color(HDR 밝기 포함). 틴트는 이것에 곱한다.
        private Transform _stressCacheRoot;   // 캐시를 지은 루트 — visualRoot 가 뒤늦게 채워지면(공용 프랍 설치자) 캐시를 다시 짓는다.
        private static MaterialPropertyBlock _stressMpb;

        public void SetStressTint(float stress01, float beatScale)
        {
            if (_collapsed) return;
            float k = Mathf.Clamp01(stress01);
            var tint = Color.Lerp(Color.white, stressTint, k);
            float b = Mathf.Lerp(1f, beatScale, k);   // 스트레스가 낮으면 거의 안 뛴다
            tint.r *= b; tint.g *= b; tint.b *= b;
            tint.a = 1f;

            var root = VisualRootOrSelf;
            if (_stressSprites == null || _stressCacheRoot != root)
            {
                _stressCacheRoot = root;
                _stressSprites = root.GetComponentsInChildren<SpriteRenderer>();
                var meshes = new System.Collections.Generic.List<Renderer>();
                foreach (var r in root.GetComponentsInChildren<Renderer>())
                    if (r is not SpriteRenderer) meshes.Add(r);
                _stressMeshes = meshes.ToArray();
                // unit 6 — 포탈 프랍(파티클)의 머티리얼은 _Color 가 HDR 밝기 부스터(Portal_Circle 2.37)다. 절대값으로
                // 덮으면 스트레스 0 에서도 포탈이 어두워진다 → 저작 색을 한 번 읽어 두고 틴트를 **곱**한다.
                _stressMeshBase = new Color[_stressMeshes.Length];
                for (int i = 0; i < _stressMeshes.Length; i++)
                {
                    var m = _stressMeshes[i].sharedMaterial;
                    _stressMeshBase[i] = m == null ? Color.white
                        : m.HasProperty("_BaseColor") ? m.GetColor("_BaseColor")
                        : m.HasProperty("_Color") ? m.GetColor("_Color") : Color.white;
                }
            }
            for (int i = 0; i < _stressSprites.Length; i++) _stressSprites[i].color = tint;
            if (_stressMeshes.Length == 0) return;
            _stressMpb ??= new MaterialPropertyBlock();
            for (int i = 0; i < _stressMeshes.Length; i++)
            {
                var r = _stressMeshes[i];
                var c = tint * _stressMeshBase[i];
                c.a = _stressMeshBase[i].a;
                r.GetPropertyBlock(_stressMpb);
                _stressMpb.SetColor("_BaseColor", c);
                _stressMpb.SetColor("_Color", c);
                r.SetPropertyBlock(_stressMpb);
            }
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
