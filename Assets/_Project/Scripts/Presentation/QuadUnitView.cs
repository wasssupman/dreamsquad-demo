using Unity.Entities;
using UnityEngine;

namespace Wassup.Presentation
{
    [DisallowMultipleComponent]
    public class QuadUnitView : MonoBehaviour
    {
        private MeshFilter _filter;
        private MeshRenderer _renderer;
        private Entity _entity;
        // tilemap-view-backend unit 3 — sim 좌표 보존 (sorting 셀 역산용; view 좌표는 z 소실).
        private Vector3 _simWorld;

        public Entity Entity => _entity;

        public void Configure(Entity entity, Mesh mesh, Material material, float visualScale)
        {
            _entity = entity;

            if (_filter == null) _filter = gameObject.AddComponent<MeshFilter>();
            if (_renderer == null) _renderer = gameObject.AddComponent<MeshRenderer>();

            _filter.sharedMesh = mesh != null
                ? mesh
                : Resources.GetBuiltinResource<Mesh>("Quad.fbx");
            _renderer.sharedMaterial = material;
            transform.localScale = Vector3.one * Mathf.Max(0.01f, visualScale);
        }

        public void UpdatePosition(Vector3 world)
        {
            _simWorld = world;
            Vector3 view = Wassup.Core.BoardSpace.ToView(world);
            // 이 빌보드는 Opaque(ZWrite On, Geometry queue). Tilemap 모드에선 보드/프랍 타일맵
            // (Transparent, z=0)과 동일 평면이라, 나중에 그려지는 투명 타일맵이 ZTest LEqual 로
            // 같은 깊이에서 덮어써 빌보드가 가려진다. 카메라 쪽(−Z)으로 살짝 당겨 보드 앞에 둔다.
            // Legacy3D 는 유닛이 보드 위(y=0.5)에 떠 평면 충돌이 없으므로 건드리지 않는다.
            if (Wassup.Core.BoardSpace.Mode != Wassup.Core.BoardViewMode.Legacy3D)
                view.z -= 0.1f;
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
    }
}
