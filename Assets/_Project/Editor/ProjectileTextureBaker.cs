using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Wassup.Editor
{
    public static class ProjectileTextureBaker
    {
        private struct BakeJob
        {
            public string sourcePath;
            public string outputPrefix;
            public int variantCount;
        }

        private static readonly BakeJob[] Jobs =
        {
            new BakeJob { sourcePath = "Assets/PixPlays/Components/Textures/Noise51_rounded.png",          outputPrefix = "wind",  variantCount = 3 },
            new BakeJob { sourcePath = "Assets/PixPlays/Components/Textures/RockTextures/Rock_Diffuse.png", outputPrefix = "stone", variantCount = 3 },
            new BakeJob { sourcePath = "Assets/PixPlays/Components/Textures/T_Lu_Noise_09.png",             outputPrefix = "fire",  variantCount = 3 },
            new BakeJob { sourcePath = "Assets/PixPlays/Components/Textures/noise_2.png",                   outputPrefix = "water", variantCount = 3 },
        };

        private const string OutputDir = "Assets/_Project/Generated/Projectiles/Textures";
        private static readonly float[] HueShifts   = { 0f, 1f / 3f, 2f / 3f };
        private static readonly float[] BrightMuls  = { 0.85f, 1.0f, 1.2f };

        [MenuItem("Wassup/Tools/Generate Projectile Texture Variants")]
        public static void GenerateAll()
        {
            string absOutputDir = Application.dataPath + "/_Project/Generated/Projectiles/Textures";
            if (!Directory.Exists(absOutputDir))
                Directory.CreateDirectory(absOutputDir);

            var written = new List<string>();
            foreach (var job in Jobs)
                BakeVariants(job, written);

            AssetDatabase.Refresh();

            foreach (var path in written)
                ConfigureImportedTexture(path);

            Debug.Log($"[ProjectileTextureBaker] Done — {written.Count} textures written to {OutputDir}");
        }

        private static void BakeVariants(BakeJob job, List<string> written)
        {
            var importer = AssetImporter.GetAtPath(job.sourcePath) as TextureImporter;
            if (importer == null)
            {
                Debug.LogError($"[Baker] TextureImporter not found: {job.sourcePath}");
                return;
            }

            bool wasReadable = importer.isReadable;
            if (!wasReadable)
            {
                importer.isReadable = true;
                AssetDatabase.ImportAsset(job.sourcePath, ImportAssetOptions.ForceUpdate);
            }

            var src = AssetDatabase.LoadAssetAtPath<Texture2D>(job.sourcePath);
            if (src == null)
            {
                Debug.LogError($"[Baker] Texture not loaded: {job.sourcePath}");
                return;
            }

            for (int i = 0; i < job.variantCount; i++)
            {
                var pixels = src.GetPixels();
                for (int p = 0; p < pixels.Length; p++)
                {
                    Color.RGBToHSV(pixels[p], out float h, out float s, out float v);
                    h = Mathf.Repeat(h + HueShifts[i], 1f);
                    v = Mathf.Clamp01(v * BrightMuls[i]);
                    var rgb = Color.HSVToRGB(h, s, v);
                    rgb.a = pixels[p].a;
                    pixels[p] = rgb;
                }

                var output = new Texture2D(src.width, src.height, TextureFormat.RGBA32, false);
                output.SetPixels(pixels);
                output.Apply();

                string assetPath = $"{OutputDir}/{job.outputPrefix}_var{i}.png";
                string absPath = Application.dataPath + assetPath.Substring("Assets".Length);
                File.WriteAllBytes(absPath, output.EncodeToPNG());
                Object.DestroyImmediate(output);
                written.Add(assetPath);
            }

            if (!wasReadable)
            {
                importer.isReadable = false;
                AssetDatabase.ImportAsset(job.sourcePath, ImportAssetOptions.ForceUpdate);
            }
        }

        private static void ConfigureImportedTexture(string path)
        {
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null) return;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.sRGBTexture = true;
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
        }
    }
}
