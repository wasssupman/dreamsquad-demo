using UnityEngine;

namespace Wassup.Rendering
{
    public static class RuntimeMaterialFactory
    {
        private const string OpaqueMaterialPath = "RuntimeMaterials/SolidOpaque";
        private const string TransparentMaterialPath = "RuntimeMaterials/SolidTransparent";
        private static bool _loggedMissingRuntimeMaterial;

        public static Material CreateOpaque(Color color)
        {
            return Create(OpaqueMaterialPath, color);
        }

        public static Material CreateTransparent(Color color)
        {
            return Create(TransparentMaterialPath, color);
        }

        public static void ApplyColor(Material material, Color color)
        {
            if (material == null) return;

            if (material.HasProperty("_BaseColor"))
                material.SetColor("_BaseColor", color);
            if (material.HasProperty("_Color"))
                material.SetColor("_Color", color);

            material.color = color;
        }

        private static Material Create(string resourcePath, Color color)
        {
            var source = Resources.Load<Material>(resourcePath);
            if (source != null)
            {
                var material = new Material(source);
                ApplyColor(material, color);
                return material;
            }

            var shader = Shader.Find("Wassup/Solid_Unlit") ??
                         Shader.Find("Universal Render Pipeline/Unlit") ??
                         Shader.Find("Sprites/Default") ??
                         Shader.Find("Standard");
            if (shader == null)
            {
                if (!_loggedMissingRuntimeMaterial)
                {
                    Debug.LogError("[RuntimeMaterialFactory] Runtime material resources and fallback shaders are missing.");
                    _loggedMissingRuntimeMaterial = true;
                }
                return null;
            }

            var fallback = new Material(shader);
            ApplyColor(fallback, color);
            return fallback;
        }
    }
}
