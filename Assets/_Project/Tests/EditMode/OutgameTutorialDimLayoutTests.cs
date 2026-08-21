using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using Wassup.UI;

namespace Wassup.Tests.EditMode
{
    // outgame-tutorial unit 1 — coordinate space is the overlay FullBleedRoot local
    // Rect: centre origin, y-up. A stretched RectTransform with pivot 0.5 reports
    // exactly this, so 1920x1080 is (-960, -540, 1920, 1080).
    public class OutgameTutorialDimLayoutTests
    {
        private static readonly Rect Area = new Rect(-960f, -540f, 1920f, 1080f);

        private readonly List<Rect> _results = new List<Rect>();

        // Real lobby geometry: left vertical column, anchor (0,1), 180x228.
        // SquadButton anchoredPosition (48, -300), DreamcatcherButton (48, -552).
        private static Rect ColumnButton(float anchoredY) =>
            new Rect(Area.xMin + 48f, Area.yMax + anchoredY - 228f, 180f, 228f);

        private static Rect Squad => ColumnButton(-300f);
        private static Rect Dreamcatcher => ColumnButton(-552f);

        [SetUp]
        public void SetUp() => _results.Clear();

        private float TotalArea()
        {
            float sum = 0f;
            for (int i = 0; i < _results.Count; i++) sum += _results[i].width * _results[i].height;
            return sum;
        }

        private void AssertPiecesAreValid(params Rect[] holes)
        {
            for (int i = 0; i < _results.Count; i++)
            {
                Rect piece = _results[i];
                Assert.Greater(piece.width, 0f, $"piece {i} has non-positive width");
                Assert.Greater(piece.height, 0f, $"piece {i} has non-positive height");
                Assert.IsTrue(Area.xMin - 0.01f <= piece.xMin && piece.xMax <= Area.xMax + 0.01f &&
                              Area.yMin - 0.01f <= piece.yMin && piece.yMax <= Area.yMax + 0.01f,
                    $"piece {i} {piece} escapes the area");

                for (int h = 0; h < holes.Length; h++)
                    Assert.IsFalse(piece.Overlaps(holes[h]),
                        $"piece {i} {piece} overlaps hole {holes[h]}");

                for (int j = i + 1; j < _results.Count; j++)
                    Assert.IsFalse(piece.Overlaps(_results[j]),
                        $"pieces {i} and {j} overlap");
            }
        }

        // ⚠ **구멍 없음 = 전면 dim 이지 「전부 열림」이 아니다.**
        //
        // 이 테스트는 처음부터 있었는데도 first-run-tutorial 의 배치 스텝이 보드를 열려고
        // `SetHoles(null)` 을 불렀다 — 도구를 겨눈 단언은 **호출부의 오용을 막지 못한다**.
        // 보드는 UGUI 가 아니라 감쌀 RectTransform 이 없으므로, 보드를 열어야 하면 구멍이
        // 아니라 딤을 **내려야** 한다.
        [Test]
        public void NoHoles_CoversTheWholeArea()
        {
            OutgameTutorialDimLayout.Subtract(Area, null, 6f, _results);
            Assert.AreEqual(1, _results.Count);
            Assert.AreEqual(Area, _results[0]);

            OutgameTutorialDimLayout.Subtract(Area, new List<Rect>(), 6f, _results);
            Assert.AreEqual(1, _results.Count);
            Assert.AreEqual(Area, _results[0]);
        }

        [Test]
        public void SingleCentralHole_LeavesFourPiecesAndExactArea()
        {
            var hole = new Rect(-100f, -100f, 200f, 200f);
            OutgameTutorialDimLayout.Subtract(Area, new[] { hole }, 0f, _results);

            Assert.AreEqual(4, _results.Count);
            AssertPiecesAreValid(hole);
            Assert.AreEqual(Area.width * Area.height - hole.width * hole.height, TotalArea(), 0.01f);
        }

        // Regression guard for the spec's original horizontal-band assumption.
        // Squad and Dreamcatcher share the same x and sit 24px apart vertically, so
        // a band decomposition would merge them and swallow the gap.
        [Test]
        public void VerticallyStackedHoles_KeepAFullWidthStripBetweenThem()
        {
            Rect squad = Squad, dreamcatcher = Dreamcatcher;
            Assert.AreEqual(squad.xMin, dreamcatcher.xMin, 0.01f, "same column");
            Assert.AreEqual(24f, squad.yMin - dreamcatcher.yMax, 0.01f, "24px gap");

            OutgameTutorialDimLayout.Subtract(Area, new[] { squad, dreamcatcher }, 0f, _results);
            AssertPiecesAreValid(squad, dreamcatcher);
            Assert.AreEqual(Area.width * Area.height
                - squad.width * squad.height - dreamcatcher.width * dreamcatcher.height,
                TotalArea(), 0.01f);

            Rect gap = FindFullWidthPieceBetween(dreamcatcher.yMax, squad.yMin);
            Assert.AreEqual(24f, gap.height, 0.01f, "the 24px gap must stay dimmed");
        }

        [Test]
        public void VerticallyStackedHoles_GapSurvivesDefaultPadding()
        {
            // holePadding defaults to 6, so the 24px gap shrinks to 12 but must live.
            OutgameTutorialDimLayout.Subtract(Area, new[] { Squad, Dreamcatcher }, 6f, _results);
            Rect gap = FindFullWidthPieceBetween(Dreamcatcher.yMax + 6f, Squad.yMin - 6f);
            Assert.AreEqual(12f, gap.height, 0.01f);

            // ...and 12 is the ceiling: padding 12 makes the padded holes touch.
            OutgameTutorialDimLayout.Subtract(Area, new[] { Squad, Dreamcatcher }, 12f, _results);
            for (int i = 0; i < _results.Count; i++)
                Assert.IsFalse(
                    Mathf.Approximately(_results[i].width, Area.width) &&
                    _results[i].yMin > Dreamcatcher.yMax - 12f &&
                    _results[i].yMax < Squad.yMin + 12f,
                    "padding 12 is expected to merge the two holes");
        }

        private Rect FindFullWidthPieceBetween(float yMin, float yMax)
        {
            for (int i = 0; i < _results.Count; i++)
            {
                Rect piece = _results[i];
                if (!Mathf.Approximately(piece.width, Area.width)) continue;
                if (piece.yMin >= yMin - 0.01f && piece.yMax <= yMax + 0.01f) return piece;
            }
            Assert.Fail($"no full-width dim strip found between y {yMin} and {yMax}");
            return default;
        }

        [Test]
        public void HorizontallyAdjacentHoles_KeepAStripBetweenThem()
        {
            var left = new Rect(-400f, -100f, 200f, 200f);
            var right = new Rect(-100f, -100f, 200f, 200f);
            OutgameTutorialDimLayout.Subtract(Area, new[] { left, right }, 0f, _results);

            AssertPiecesAreValid(left, right);
            bool foundGap = false;
            for (int i = 0; i < _results.Count; i++)
            {
                Rect piece = _results[i];
                if (Mathf.Abs(piece.xMin - left.xMax) < 0.01f &&
                    Mathf.Abs(piece.xMax - right.xMin) < 0.01f) foundGap = true;
            }
            Assert.IsTrue(foundGap, "the 100px gap between the two holes must stay dimmed");
        }

        [Test]
        public void HolePartlyOutside_IsClampedWithoutNegativePieces()
        {
            var hole = new Rect(Area.xMin - 300f, Area.yMin - 300f, 500f, 500f);
            OutgameTutorialDimLayout.Subtract(Area, new[] { hole }, 0f, _results);

            AssertPiecesAreValid();
            float visible = 200f * 200f; // only the in-area corner is punched
            Assert.AreEqual(Area.width * Area.height - visible, TotalArea(), 0.01f);
        }

        [Test]
        public void HoleCoveringEverything_LeavesNothing()
        {
            OutgameTutorialDimLayout.Subtract(Area, new[] { Area }, 0f, _results);
            Assert.AreEqual(0, _results.Count);
        }

        [Test]
        public void HoleFlushToEdge_SkipsZeroWidthPieces()
        {
            var hole = new Rect(Area.xMin, -100f, 200f, 200f);
            OutgameTutorialDimLayout.Subtract(Area, new[] { hole }, 0f, _results);

            AssertPiecesAreValid(hole);
            Assert.AreEqual(3, _results.Count, "no zero-width left piece");
            Assert.AreEqual(Area.width * Area.height - hole.width * hole.height, TotalArea(), 0.01f);
        }

        [Test]
        public void DegenerateHoles_AreIgnored()
        {
            var holes = new[]
            {
                new Rect(0f, 0f, 0f, 200f),
                new Rect(0f, 0f, 200f, 0f),
                new Rect(0f, 0f, -50f, -50f),
            };
            OutgameTutorialDimLayout.Subtract(Area, holes, 6f, _results);

            Assert.AreEqual(1, _results.Count);
            Assert.AreEqual(Area, _results[0]);
        }

        [Test]
        public void OverlappingHoles_MergeWithoutDuplicatePieces()
        {
            var a = new Rect(-200f, -100f, 200f, 200f);
            var b = new Rect(-100f, -100f, 200f, 200f);
            OutgameTutorialDimLayout.Subtract(Area, new[] { a, b }, 0f, _results);

            AssertPiecesAreValid(a, b);
            float union = 300f * 200f;
            Assert.AreEqual(Area.width * Area.height - union, TotalArea(), 0.01f);
        }

        [Test]
        public void ThreeHoles_InTheLobbyColumn()
        {
            Rect preset = ColumnButton(-48f), squad = Squad, dreamcatcher = Dreamcatcher;
            OutgameTutorialDimLayout.Subtract(Area, new[] { preset, squad, dreamcatcher }, 0f, _results);

            AssertPiecesAreValid(preset, squad, dreamcatcher);
            Assert.AreEqual(Area.width * Area.height - 3f * 180f * 228f, TotalArea(), 0.01f);
        }

        [Test]
        public void Results_AreClearedBeforeBeingFilled()
        {
            _results.Add(new Rect(1f, 2f, 3f, 4f));
            _results.Add(new Rect(5f, 6f, 7f, 8f));

            OutgameTutorialDimLayout.Subtract(Area, null, 0f, _results);

            Assert.AreEqual(1, _results.Count);
            Assert.AreEqual(Area, _results[0]);
        }

        [Test]
        public void DegenerateArea_ProducesNothing()
        {
            OutgameTutorialDimLayout.Subtract(new Rect(0f, 0f, 0f, 0f), new[] { Squad }, 0f, _results);
            Assert.AreEqual(0, _results.Count);
        }
    }
}
