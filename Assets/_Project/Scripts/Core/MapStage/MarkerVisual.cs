using UnityEngine;

namespace Wassup.Core
{
    // map-diorama-stage unit 4 — 골/스폰 마커 뷰 훅 공용 헬퍼.
    // TilemapMapView 구조물 프랍 경로(TryGet*VisualAnchor/ApplyPropTint)의 의미 승계:
    // 앵커 = 렌더러 바운즈 중심(없으면 트랜스폼 위치), 틴트 = 스프라이트 color / 메쉬 MPB.
    internal static class MarkerVisual
    {
        internal static Vector3 AnchorOf(Transform root)
        {
            var renderers = root.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0) return root.position;
            var b = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++) b.Encapsulate(renderers[i].bounds);
            return b.center;
        }

        // 스프라이트는 SpriteRenderer.color(공용 머티리얼 무오염), 메쉬는 MPB — 구 구현 그대로.
        internal static void ApplyTint(Transform root, Color tint)
        {
            foreach (var sr in root.GetComponentsInChildren<SpriteRenderer>())
                sr.color = tint;
            var mpb = new MaterialPropertyBlock();
            foreach (var r in root.GetComponentsInChildren<Renderer>())
            {
                if (r is SpriteRenderer) continue;
                r.GetPropertyBlock(mpb);
                mpb.SetColor("_BaseColor", tint);
                mpb.SetColor("_Color", tint);
                r.SetPropertyBlock(mpb);
            }
        }
    }
}
