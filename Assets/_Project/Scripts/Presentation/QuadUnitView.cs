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
            transform.position = world;
        }

        public void SetSortingOrder(int order)
        {
            var renderers = GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < renderers.Length; i++)
                renderers[i].sortingOrder = order;
        }

    }
}
