using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Wassup.Data;

namespace Wassup.Tests.EditMode
{
    // dreamcatcher-card-art unit 7 — 실제 노출되는 카드는 작은 카드 슬롯의 얼굴이라
    // 폴백이나 공유 이미지를 허용하지 않는다. 밸런스/콘텐츠 개수는 pin 하지 않고
    // visible 축으로 전수 검사해 카드 추가에도 같은 저작 계약을 적용한다.
    public class DreamcatcherCardArtTests
    {
        private const string CardsRoot = "Assets/_Project/Data/Dreamcatcher";

        [Test]
        public void VisibleCards_HaveUniquePortraitSpriteArtwork()
        {
            string[] guids = AssetDatabase.FindAssets(
                "t:DreamcatcherCard", new[] { CardsRoot });
            Assert.IsNotEmpty(guids, "no DreamcatcherCard assets found");

            int visibleCount = 0;
            var seenPaths = new HashSet<string>();
            foreach (string guid in guids)
            {
                string cardPath = AssetDatabase.GUIDToAssetPath(guid);
                var card = AssetDatabase.LoadAssetAtPath<DreamcatcherCard>(cardPath);
                if (card == null || card.visible == 0) continue;

                visibleCount++;
                Assert.IsNotNull(card.art, $"{card.id}: visible card art missing");

                string artPath = AssetDatabase.GetAssetPath(card.art);
                Assert.IsFalse(string.IsNullOrEmpty(artPath),
                    $"{card.id}: art has no asset path");
                Assert.IsTrue(seenPaths.Add(artPath),
                    $"{card.id}: visible cards must not share art ({artPath})");

                Assert.AreEqual(1024, card.art.texture.width, $"{card.id}: art width");
                Assert.AreEqual(1536, card.art.texture.height, $"{card.id}: art height");

                var importer = AssetImporter.GetAtPath(artPath) as TextureImporter;
                Assert.IsNotNull(importer, $"{card.id}: TextureImporter missing");
                Assert.AreEqual(TextureImporterType.Sprite, importer.textureType,
                    $"{card.id}: art must import as Sprite");
                Assert.AreEqual(SpriteImportMode.Single, importer.spriteImportMode,
                    $"{card.id}: art must be Single sprite");
                Assert.IsFalse(importer.mipmapEnabled, $"{card.id}: UI art mipmap must be off");
                Assert.IsTrue(importer.sRGBTexture, $"{card.id}: UI art must use sRGB");
                Assert.IsTrue(importer.alphaIsTransparency,
                    $"{card.id}: UI art alpha transparency setting");
            }

            Assert.Greater(visibleCount, 0, "no visible DreamcatcherCard assets found");
        }
    }
}
