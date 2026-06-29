using Unity.Entities;
using UnityEngine;
using Wassup.Bridge;

namespace Wassup.Presentation
{
    [DisallowMultipleComponent]
    public class QuadUnitView : MonoBehaviour
    {
        private MeshFilter _filter;
        private MeshRenderer _renderer;
        private Transform _visual;       // tilted-billboard unit 4a: 발 피벗 자식(Tilemap). Legacy3D 는 root.
        private Material _ownedMaterial; // Tilemap 전용 unlit 복사본 (transform 이 방향을 제어). 직접 소유/파괴.
        private Entity _entity;
        // tilemap-view-backend unit 3 — sim 좌표 보존 (sorting 셀 역산용; view 좌표는 z 소실).
        private Vector3 _simWorld;

        public Entity Entity => _entity;

        public void Configure(Entity entity, Mesh mesh, Material material, float visualScale)
        {
            _entity = entity;
            Mesh quad = mesh != null ? mesh : Resources.GetBuiltinResource<Mesh>("Quad.fbx");
            bool tilemap = Wassup.Core.BoardSpace.Mode != Wassup.Core.BoardViewMode.Legacy3D;
            transform.localScale = Vector3.one * Mathf.Max(0.01f, visualScale);

            if (tilemap)
            {
                // tilted-billboard unit 4a — Spine 과 동일 메커니즘: object-space unlit + Billboard transform 틸트.
                // 발 피벗: Quad(센터, Y −0.5..0.5)를 +0.5 올려 밑동을 root 원점(셀)에 둔다 → 틸트가 발 기준.
                EnsureVisualChild(quad);
                _ownedMaterial = BuildUnlitMaterial(material, _ownedMaterial);
                _renderer.sharedMaterial = _ownedMaterial;

                var billboard = GetComponent<Billboard>();
                if (billboard == null) billboard = gameObject.AddComponent<Billboard>();
                billboard.Setup(BillboardMode.Tilted, BattleBridge.CharacterBillboardTilt);

                // tilemap-real-shadows — 진짜 그림자(실루엣 cast) vs 블롭(상호배타).
                if (BattleBridge.UseRealShadows)
                {
                    // URP/Unlit + _ALPHATEST_ON → ShadowCaster 가 실루엣 cast. 평면이라 TwoSided.
                    _renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.TwoSided;
                }
                else
                {
                    _renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    if (BattleBridge.BlobShadowSprite != null)
                        BlobShadow.Attach(transform, BattleBridge.BlobShadowSprite, BattleBridge.BlobShadowSize,
                            BattleBridge.BlobShadowColor,
                            BattleBridge.BlobShadowGroundY, BoardSortOrder.ShadowOrder, live: true); // 유닛은 이동 — 매 프레임 따라감
                }
            }
            else
            {
                // Legacy3D — 기존 셰이더 빌보드 경로 그대로(메시/머티리얼 root, Billboard 없음).
                if (_filter == null) _filter = gameObject.AddComponent<MeshFilter>();
                if (_renderer == null) _renderer = gameObject.AddComponent<MeshRenderer>();
                _filter.sharedMesh = quad;
                _renderer.sharedMaterial = material;
            }
        }

        private void EnsureVisualChild(Mesh quad)
        {
            if (_visual == null)
            {
                var go = new GameObject("Visual");
                _visual = go.transform;
                _visual.SetParent(transform, false);
                _visual.localPosition = new Vector3(0f, 0.5f, 0f); // 발 피벗
                _filter = go.AddComponent<MeshFilter>();
                _renderer = go.AddComponent<MeshRenderer>();
            }
            _filter.sharedMesh = quad;
        }

        // 전달받은(공유일 수 있는) 머티리얼을 변조하지 않고, object-space URP/Unlit 복사본을 소유한다.
        private static Material BuildUnlitMaterial(Material source, Material previousOwned)
        {
            if (previousOwned != null) Destroy(previousOwned);
            var m = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            Texture tex = source != null
                ? (source.HasProperty("_BaseMap") ? source.GetTexture("_BaseMap") : source.mainTexture)
                : null;
            Color col = source != null && source.HasProperty("_BaseColor") ? source.GetColor("_BaseColor") : Color.white;
            if (m.HasProperty("_BaseMap")) m.SetTexture("_BaseMap", tex);
            m.mainTexture = tex;
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", col);
            // 알파 컷아웃 + 양면(빌보드 메시 뒷면도 보이게)
            if (m.HasProperty("_AlphaClip")) m.SetFloat("_AlphaClip", 1f);
            if (m.HasProperty("_Cutoff")) m.SetFloat("_Cutoff", 0.5f);
            m.EnableKeyword("_ALPHATEST_ON");
            if (m.HasProperty("_Cull")) m.SetFloat("_Cull", 0f); // Cull Off
            m.renderQueue = (int)UnityEngine.Rendering.RenderQueue.AlphaTest;
            return m;
        }

        public void UpdatePosition(Vector3 world)
        {
            _simWorld = world;
            Vector3 view = Wassup.Core.BoardSpace.ToView(world);
            // Tilemap 모드는 타일맵이 XZ 바닥. 빌보드 밑동이 바닥 Y 와 같으면 z-fighting → 살짝 띄운다.
            // Legacy3D 는 유닛이 보드 위(y=0.5)에 떠 충돌이 없으므로 건드리지 않는다.
            if (Wassup.Core.BoardSpace.Mode != Wassup.Core.BoardViewMode.Legacy3D)
                view.y += 0.01f;
            transform.position = view;
        }

        public void SetSortingOrder(int order)
        {
            var renderers = GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < renderers.Length; i++)
                renderers[i].sortingOrder = order;
        }

        public void UpdateSortingOrder(Unity.Mathematics.int2 gridSize, float tileSize)
        {
            // sim 좌표로 셀 역산 — view 좌표(transform.position)는 z 소실로 행 정렬 붕괴.
            SetSortingOrder(BoardSortOrder.ComputeFromWorld(
                gridSize,
                _simWorld,
                tileSize,
                BoardSortOrder.CharacterOffset));
        }

        private void OnDestroy()
        {
            if (_ownedMaterial != null) Destroy(_ownedMaterial);
        }
    }
}
