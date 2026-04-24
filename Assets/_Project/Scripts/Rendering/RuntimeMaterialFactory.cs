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

        public static Material CreateOpaqueTexture(Texture texture, Color color)
        {
            var shader = Shader.Find("Wassup/Tile_Unlit") ??
                         Shader.Find("Universal Render Pipeline/Unlit") ??
                         Shader.Find("Unlit/Texture") ??
                         Shader.Find("Wassup/Solid_Unlit") ??
                         Shader.Find("Standard");
            if (shader == null)
            {
                if (!_loggedMissingRuntimeMaterial)
                {
                    Debug.LogError("[RuntimeMaterialFactory] Textured material fallback shaders are missing.");
                    _loggedMissingRuntimeMaterial = true;
                }
                return null;
            }

            var material = new Material(shader);
            ApplyColor(material, color);
            if (texture != null)
            {
                if (material.HasProperty("_BaseMap"))
                    material.SetTexture("_BaseMap", texture);
                if (material.HasProperty("_MainTex"))
                    material.SetTexture("_MainTex", texture);
            }

            return material;
        }

        public static Material CreateTransparentTexture(Texture texture, Color color)
        {
            var shader = Shader.Find("Universal Render Pipeline/Unlit") ??
                         Shader.Find("Unlit/Transparent") ??
                         Shader.Find("Sprites/Default") ??
                         Shader.Find("Standard");
            if (shader == null)
            {
                if (!_loggedMissingRuntimeMaterial)
                {
                    Debug.LogError("[RuntimeMaterialFactory] Transparent textured material fallback shaders are missing.");
                    _loggedMissingRuntimeMaterial = true;
                }
                return null;
            }

            var material = new Material(shader);
            ApplyColor(material, color);
            if (texture != null)
            {
                if (material.HasProperty("_BaseMap"))
                    material.SetTexture("_BaseMap", texture);
                if (material.HasProperty("_MainTex"))
                    material.SetTexture("_MainTex", texture);
            }

            material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            if (material.HasProperty("_Surface"))
                material.SetFloat("_Surface", 1f);
            if (material.HasProperty("_AlphaClip"))
                material.SetFloat("_AlphaClip", 0f);
            if (material.HasProperty("_SrcBlend"))
                material.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            if (material.HasProperty("_DstBlend"))
                material.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            if (material.HasProperty("_ZWrite"))
                material.SetFloat("_ZWrite", 0f);
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");

            return material;
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
