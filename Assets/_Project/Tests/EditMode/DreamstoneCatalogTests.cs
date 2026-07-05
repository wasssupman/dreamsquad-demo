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

        // dreamstone-loadout Unit 5 (rev 2026-07-06b) — flat item-instance model: 64
        // individually-owned stones, no grouping/duplicate concept. Ids are sequential
        // stone_001..stone_064, catalog order == id order. Tiers are 0.1-precision
        // decimals (cap, 0.8*cap, 0.8*cap, 0.6*cap) per kind, cap = grade cap / 4.
        [Test]
        public void CatalogAssets_AreValid()
        {
            var catalog = UnityEditor.AssetDatabase.LoadAssetAtPath<DreamstoneCatalog>(CatalogPath);
            Assert.IsNotNull(catalog, "DreamstoneCatalog asset exists");
            Assert.IsNotNull(catalog.stones, "catalog.stones assigned");
            Assert.AreEqual(64, catalog.stones.Length, "64 individually-owned stone items");

            var seen = new HashSet<string>();
            for (int i = 0; i < catalog.stones.Length; i++)
            {
                var stone = catalog.stones[i];
                Assert.IsNotNull(stone, $"stone[{i}] is assigned");
                Assert.AreEqual($"stone_{i + 1:D3}", stone.id, $"catalog order == sequential id order at index {i}");
                Assert.IsNotNull(stone.icon, $"{stone.id} icon assigned");
                Assert.IsTrue(seen.Add(stone.id), $"duplicate id: {stone.id}");

                Assert.IsTrue(GradeCaps.TryGetValue(stone.grade, out var cap), $"{stone.id} grade cap");
                float tierCap = cap / 4f;
                float tenths = stone.effect.percent * 10f;
                Assert.AreEqual(Mathf.Round(tenths), tenths, 0.001f, $"{stone.id} percent must be 0.1-precision");
                Assert.LessOrEqual(stone.effect.percent, tierCap + 0.0001f, $"{stone.id} percent must not exceed grade cap / 4");
            }
        }

        // dreamstone-loadout Unit 5 (rev 2026-07-06b) — every consecutive 4-id block
        // is one "kind" (same grade + same effect.kind), tiered exactly
        // [cap, 0.8*cap, 0.8*cap, 0.6*cap] where cap = grade cap / 4.
        [Test]
        public void Catalog_TierBlocks_AreConsistent()
        {
            var catalog = UnityEditor.AssetDatabase.LoadAssetAtPath<DreamstoneCatalog>(CatalogPath);
            Assert.IsNotNull(catalog, "DreamstoneCatalog asset exists");
            Assert.AreEqual(0, catalog.stones.Length % 4, "stones divide evenly into 4-id blocks");

            for (int block = 0; block < catalog.stones.Length; block += 4)
            {
                var top = catalog.stones[block];
                var midA = catalog.stones[block + 1];
                var midB = catalog.stones[block + 2];
                var bottom = catalog.stones[block + 3];
                string label = $"block[{block / 4}] ({top.id}..{bottom.id})";

                Assert.AreEqual(top.grade, midA.grade, $"{label} grade consistent");
                Assert.AreEqual(top.grade, midB.grade, $"{label} grade consistent");
                Assert.AreEqual(top.grade, bottom.grade, $"{label} grade consistent");
                Assert.AreEqual(top.effect.kind, midA.effect.kind, $"{label} effect.kind consistent");
                Assert.AreEqual(top.effect.kind, midB.effect.kind, $"{label} effect.kind consistent");
                Assert.AreEqual(top.effect.kind, bottom.effect.kind, $"{label} effect.kind consistent");
                Assert.AreEqual(top.displayName, midA.displayName, $"{label} displayName consistent (tier differs only by %)");

                Assert.IsTrue(GradeCaps.TryGetValue(top.grade, out var cap), $"{label} grade cap");
                float tierCap = cap / 4f;

                Assert.AreEqual(tierCap, top.effect.percent, 0.0001f, $"{label} top tier == grade cap / 4");
                Assert.AreEqual(tierCap * 0.8f, midA.effect.percent, 0.0001f, $"{label} mid tier == 0.8 * (grade cap / 4)");
                Assert.AreEqual(tierCap * 0.8f, midB.effect.percent, 0.0001f, $"{label} mid tier == 0.8 * (grade cap / 4)");
                Assert.AreEqual(tierCap * 0.6f, bottom.effect.percent, 0.0001f, $"{label} bottom tier == 0.6 * (grade cap / 4)");
            }
        }

        // dreamstone-loadout Unit 6 — MoveSpeed stones retired entirely (replaced by
        // CostRate stones, useless on placement-only defenders); the enum value
        // itself survives for serialization safety, but no catalog asset may use it
        // anymore. stone_049..stone_064 (indices 48..63, the last stat-major block)
        // are the CostRate block.
        [Test]
        public void Catalog_NoMoveSpeedStones_CostRateBlockExists()
        {
            var catalog = UnityEditor.AssetDatabase.LoadAssetAtPath<DreamstoneCatalog>(CatalogPath);
            Assert.IsNotNull(catalog, "DreamstoneCatalog asset exists");

            foreach (var stone in catalog.stones)
                Assert.AreNotEqual(CardBuffKind.MoveSpeed, stone.effect.kind, $"{stone.id} must not be MoveSpeed (retired)");

            for (int i = 48; i < 64; i++)
            {
                var stone = catalog.stones[i];
                Assert.AreEqual(CardBuffKind.CostRate, stone.effect.kind, $"{stone.id} must be CostRate");
                Assert.IsTrue(stone.displayName.EndsWith("Cost Stone"), $"{stone.id} displayName ends with 'Cost Stone'");
            }
        }

        [Test]
        public void ById_ReturnsCatalogStone()
        {
            var catalog = UnityEditor.AssetDatabase.LoadAssetAtPath<DreamstoneCatalog>(CatalogPath);
            Assert.IsNotNull(catalog, "DreamstoneCatalog asset exists");

            // stone_001 == top-tier Unique Attack Stone (block 0 of the ATK/Unique
            // stat-major, grade-descending catalog order).
            var topUniqueAttack = catalog.ById("stone_001");
            Assert.IsNotNull(topUniqueAttack);
            Assert.AreEqual(CardBuffKind.AttackDamage, topUniqueAttack.effect.kind);
            Assert.AreEqual(DreamstoneGrade.Unique, topUniqueAttack.grade);
            Assert.AreEqual(7.5f, topUniqueAttack.effect.percent, 0.0001f);

            Assert.IsNull(catalog.ById(""));
            Assert.IsNull(catalog.ById("missing"));
            // dreamstone-loadout Unit 5 — pre-rev ids are gone; a legacy save
            // referencing them must resolve to null (skip -> empty slot on load,
            // no migration), not throw.
            Assert.IsNull(catalog.ById("stone_atk_unique"));
        }
    }
}
