using System.Collections.Generic;
using NUnit.Framework;
using Wassup.Data;
using Wassup.Data.MapGrid;

namespace Wassup.Tests.EditMode
{
    // map-rework unit 0 → **unit 7 에서 폭 규칙 반전**. 컨셉 가드의 순수 규칙.
    // 옛 계약: 폭1 금지. 새 계약: 직선은 폭1(근접이 완전히 막을 수 있게) · 폭2 는 제한적.
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

        // ── 국소 폭 = 가로 런과 세로 런의 min ────────────────────────────────
        //
        // 폭1 복도의 팔은 1, **교차 중심은 3** 이다 — 사방이 뚫려 있으니 좁은 목이 아니다.
        // (처음엔 「교차 칸도 폭1」로 단정했다가 이 테스트가 3 을 돌려줘 바로잡았다. 그래도
        //  차단칸 집계는 흔들리지 않는다 — 교차 중심은 직교 이웃이 전부 Walk 라 애초에
        //  근접을 세울 배치칸이 없다.)
        [Test]
        public void LocalWidth_ReadsTheNarrowAxis()
        {
            var (t, w, h) = Grid(
                "□□.□□",
                "□...□",
                "□□.□□",
                "□□□□□");
            Assert.AreEqual(3, MapConceptRules.LocalWidth(t, w, h, 2, 2), "십자 중심은 사방이 뚫려 폭3");
            Assert.AreEqual(1, MapConceptRules.LocalWidth(t, w, h, 1, 2), "가로 팔은 폭1");
            Assert.AreEqual(1, MapConceptRules.LocalWidth(t, w, h, 2, 3), "세로 팔은 폭1");
            Assert.AreEqual(0, MapConceptRules.LocalWidth(t, w, h, 0, 0), "Walk 가 아니면 0");
        }

        [Test]
        public void LocalWidth_Width2Corridor_IsTwo()
        {
            var (t, w, h) = Grid(
                "□□□□□",
                "□...□",
                "□...□",
                "□□□□□");
            Assert.AreEqual(2, MapConceptRules.LocalWidth(t, w, h, 2, 1));
        }

        // ── 근접 완전차단칸 ───────────────────────────────────────────────────
        //
        // 폭1 이고 **직교로** 배치칸에 붙어야 한다. 대각은 사거리 1이 안 닿는다(1.41 > 1) —
        // 이 구분이 없으면 「닿는 줄 알았는데 안 닿는」 맵을 가드가 통과시킨다.
        [Test]
        public void Choke_NeedsWidth1_AndOrthogonalPlacement()
        {
            var (t, w, h) = Grid(
                "□□□□□",
                "□...□",     // 폭1 가로 복도, 위아래가 Place → 직교 인접
                "□□□□□");
            MapConceptRules.MeasureMeleeLanes(t, null, w, h,
                out int walk, out int choke, out int width2);
            Assert.AreEqual(3, walk);
            Assert.AreEqual(3, choke, "폭1 + 직교 배치칸 = 완전차단칸");
            Assert.AreEqual(0, width2);
        }

        [Test]
        public void Choke_Width2Corridor_CountsNone()
        {
            var (t, w, h) = Grid(
                "□□□□□",
                "□...□",
                "□...□",
                "□□□□□");
            MapConceptRules.MeasureMeleeLanes(t, null, w, h,
                out int walk, out int choke, out int width2);
            Assert.AreEqual(6, walk);
            Assert.AreEqual(0, choke, "폭2 는 먼 차선이 자유라 완전차단이 아니다");
            Assert.AreEqual(6, width2);
        }

        // placeMask 가 있으면 그쪽이 정본이다 — 타일이 Place 여도 마스크가 지상을 안 열면
        // 근접을 못 세운다(배치 판정은 층 비트필드가 소유한다).
        [Test]
        public void Choke_AuthoredMaskWins_OverTileDerivation()
        {
            var (t, w, h) = Grid(
                "□□□□□",
                "□...□",
                "□□□□□");
            var mask = new byte[w * h];   // 전 셀 0 — 아무 층도 안 연다
            MapConceptRules.MeasureMeleeLanes(t, mask, w, h,
                out _, out int choke, out _);
            Assert.AreEqual(0, choke, "저작 마스크가 지상을 닫으면 근접이 설 자리가 없다");
        }

        [Test]
        public void ValidateMeleeLanes_WarnsOnTheReworkedShape_AndPassesOnWidth1()
        {
            var (wide, w1, h1) = Grid(
                "□□□□□",
                "□...□",
                "□...□",
                "□□□□□");
            var warnings = new List<string>();
            MapConceptRules.ValidateMeleeLanes(wide, null, w1, h1, warnings);
            Assert.AreEqual(2, warnings.Count, "차단칸 부족 + 폭2 과다 둘 다 경고한다");

            var (narrow, w2, h2) = Grid(
                "□□□□□",
                "□...□",
                "□□□□□");
            warnings.Clear();
            MapConceptRules.ValidateMeleeLanes(narrow, null, w2, h2, warnings);
            Assert.IsEmpty(warnings, "직선 폭1 은 새 계약을 만족한다");
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
