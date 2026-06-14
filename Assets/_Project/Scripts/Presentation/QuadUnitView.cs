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
            transform.position = Wassup.Core.BoardSpace.ToView(world);
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
