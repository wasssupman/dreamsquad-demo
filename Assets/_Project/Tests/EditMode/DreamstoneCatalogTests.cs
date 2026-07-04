using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using Wassup.Data;

namespace Wassup.Tests.EditMode
{
    public class DreamstoneCatalogTests
    {
        private const string CatalogPath = "Assets/_Project/Data/Dreamstones/DreamstoneCatalog.asset";

        private static readonly Dictionary<DreamstoneGrade, float> GradeCaps = new()
        {
            { DreamstoneGrade.Common, 8f },
            { DreamstoneGrade.Rare, 12f },
            { DreamstoneGrade.Epic, 20f },
            { DreamstoneGrade.Unique, 30f },
        };

        [Test]
        public void CatalogAssets_AreValid()
        {
            var catalog = UnityEditor.AssetDatabase.LoadAssetAtPath<DreamstoneCatalog>(CatalogPath);
            Assert.IsNotNull(catalog, "DreamstoneCatalog asset exists");
            Assert.IsNotNull(catalog.stones, "catalog.stones assigned");
            Assert.AreEqual(16, catalog.stones.Length, "4 grades x 4 stat stones");

            var seen = new HashSet<string>();
            for (int i = 0; i < catalog.stones.Length; i++)
            {
                var stone = catalog.stones[i];
                Assert.IsNotNull(stone, $"stone[{i}] is assigned");
                Assert.IsFalse(string.IsNullOrWhiteSpace(stone.id), $"{stone.name} id");
                Assert.IsTrue(seen.Add(stone.id), $"duplicate id: {stone.id}");
                Assert.Greater(stone.effect.percent, 0f, $"{stone.id} percent");

                Assert.IsTrue(GradeCaps.TryGetValue(stone.grade, out var cap), $"{stone.id} grade cap");
                Assert.LessOrEqual(
                    stone.effect.percent,
                    cap / 4f + 0.0001f,
                    $"{stone.id} percent must not exceed grade cap / 4");
            }
        }

        [Test]
        public void ById_ReturnsCatalogStone()
        {
            var catalog = UnityEditor.AssetDatabase.LoadAssetAtPath<DreamstoneCatalog>(CatalogPath);
            Assert.IsNotNull(catalog, "DreamstoneCatalog asset exists");

            var uniqueAttack = catalog.ById("stone_atk_unique");
            Assert.IsNotNull(uniqueAttack);
            Assert.AreEqual(CardBuffKind.AttackDamage, uniqueAttack.effect.kind);
            Assert.AreEqual(DreamstoneGrade.Unique, uniqueAttack.grade);
            Assert.AreEqual(7.5f, uniqueAttack.effect.percent, 0.0001f);

            Assert.IsNull(catalog.ById(""));
            Assert.IsNull(catalog.ById("missing"));
        }
    }
}
