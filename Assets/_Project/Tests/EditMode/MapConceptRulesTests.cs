using System.Collections.Generic;
using NUnit.Framework;
using Wassup.Data;
using Wassup.Data.MapGrid;

namespace Wassup.Tests.EditMode
{
    // map-rework unit 0 — 컨셉 가드(폭1 경고 · 광장 판정)의 순수 규칙.
    public class MapConceptRulesTests
    {
        // 문자열 → 타일. '.'=Walk 나머지 Place. 첫 행 = y(높은쪽) — 가독용이므로 뒤집어 굽는다.
        private static (List<MapTileType> tiles, int w, int h) Grid(params string[] rowsTopDown)
        {
            int h = rowsTopDown.Length, w = rowsTopDown[0].Length;
            var tiles = new List<MapTileType>(new MapTileType[w * h]);
            for (int i = 0; i < h; i++)
            {
                int y = h - 1 - i;
                for (int x = 0; x < w; x++)
                    tiles[y * w + x] = rowsTopDown[i][x] == '.' ? MapTileType.Walk : MapTileType.Place;
            }
            return (tiles, w, h);
        }

        [Test]
        public void Width1_HorizontalCorridor_Warns()
        {
            var (t, w, h) = Grid(
                "□□□□□",
                "□...□",
                "□□□□□");
            var warnings = new List<string>();
            MapConceptRules.ValidateCorridorWidth(t, w, h, warnings);
            Assert.AreEqual(1, warnings.Count);
            StringAssert.Contains("폭1 Walk 3칸", warnings[0]);
        }

        [Test]
        public void Width2_Corridor_DoesNotWarn()
        {
            var (t, w, h) = Grid(
                "□□□□□",
                "□...□",
                "□...□",
                "□□□□□");
            var warnings = new List<string>();
            MapConceptRules.ValidateCorridorWidth(t, w, h, warnings);
            Assert.AreEqual(0, warnings.Count);
        }

        [Test]
        public void Width2_LCorner_DoesNotWarn()
        {
            var (t, w, h) = Grid(
                "□□..□",
                "□□..□",
                "□...□",
                "□...□",
                "□□□□□");
            var warnings = new List<string>();
            MapConceptRules.ValidateCorridorWidth(t, w, h, warnings);
            Assert.AreEqual(0, warnings.Count);
        }

        [Test]
        public void Width1_VerticalStub_Warns_WithCoordinates()
        {
            var (t, w, h) = Grid(
                "□□.□□",
                "□□.□□",
                "□□□□□");
            var warnings = new List<string>();
            MapConceptRules.ValidateCorridorWidth(t, w, h, warnings);
            Assert.AreEqual(1, warnings.Count);
            StringAssert.Contains("(2,", warnings[0]);
        }

        [Test]
        public void Plaza_4x4_Detected()
        {
            var (t, w, h) = Grid(
                "□□□□□□",
                "□....□",
                "□....□",
                "□....□",
                "□....□",
                "□□□□□□");
            Assert.IsTrue(MapConceptRules.HasPlaza(t, w, h));
            var warnings = new List<string>();
            MapConceptRules.ValidatePlaza(t, w, h, warnings);
            Assert.AreEqual(0, warnings.Count);
        }

        [Test]
        public void Plaza_Missing_Warns()
        {
            var (t, w, h) = Grid(
                "□□□□□□",
                "□...□□",
                "□...□□",
                "□...□□",
                "□□□□□□");   // 3×3 최대 — 광장 아님
            Assert.IsFalse(MapConceptRules.HasPlaza(t, w, h));
            var warnings = new List<string>();
            MapConceptRules.ValidatePlaza(t, w, h, warnings);
            Assert.AreEqual(1, warnings.Count);
        }
    }
}
