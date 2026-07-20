using System.Collections.Generic;
using UnityEngine;

namespace Wassup.UI
{
    // outgame-tutorial unit 1 — pure rectangle subtraction: what is left of `area`
    // once the hole rects are punched out. General y-scanline, no axis assumption:
    // the lobby menu buttons are a left vertical column (SquadButton y -300,
    // DreamcatcherButton y -552, both x 48), so a horizontal-band decomposition
    // would merge them into one tall hole and swallow the 24px gap between them.
    //
    // Coordinate space is the overlay's FullBleedRoot local Rect — centre origin,
    // y-up — but the maths is space-agnostic as long as callers stay consistent.
    // The scratch lists make this allocation-free; main thread only.
    public static class OutgameTutorialDimLayout
    {
        private const float Epsilon = 0.01f;

        private static readonly List<Rect> s_holes = new List<Rect>();
        private static readonly List<float> s_bounds = new List<float>();
        private static readonly List<Vector2> s_spans = new List<Vector2>();

        public static void Subtract(Rect area, IReadOnlyList<Rect> holes, float padding,
            List<Rect> results)
        {
            if (results == null) return;
            results.Clear();
            if (area.width <= Epsilon || area.height <= Epsilon) return;

            CollectHoles(area, holes, padding);
            if (s_holes.Count == 0)
            {
                results.Add(area);
                return;
            }

            CollectYBounds(area);

            for (int b = 0; b + 1 < s_bounds.Count; b++)
            {
                float y0 = s_bounds[b];
                float y1 = s_bounds[b + 1];
                float height = y1 - y0;
                if (height <= Epsilon) continue;

                // Only holes that cross the whole band matter — inside a band every
                // hole edge is either at y0/y1 or outside, so this is exact.
                s_spans.Clear();
                for (int i = 0; i < s_holes.Count; i++)
                {
                    Rect hole = s_holes[i];
                    if (hole.yMin <= y0 + Epsilon && hole.yMax >= y1 - Epsilon)
                        s_spans.Add(new Vector2(hole.xMin, hole.xMax));
                }

                if (s_spans.Count == 0)
                {
                    results.Add(new Rect(area.xMin, y0, area.width, height));
                    continue;
                }

                s_spans.Sort(CompareSpanStart);

                float cursor = area.xMin;
                for (int i = 0; i < s_spans.Count; i++)
                {
                    Vector2 span = s_spans[i];
                    if (span.x - cursor > Epsilon)
                        results.Add(new Rect(cursor, y0, span.x - cursor, height));
                    // Overlapping spans merge naturally: the cursor never moves back.
                    if (span.y > cursor) cursor = span.y;
                }
                if (area.xMax - cursor > Epsilon)
                    results.Add(new Rect(cursor, y0, area.xMax - cursor, height));
            }
        }

        private static void CollectHoles(Rect area, IReadOnlyList<Rect> holes, float padding)
        {
            s_holes.Clear();
            if (holes == null) return;

            for (int i = 0; i < holes.Count; i++)
            {
                Rect hole = holes[i];
                if (hole.width <= 0f || hole.height <= 0f) continue;

                var padded = new Rect(hole.xMin - padding, hole.yMin - padding,
                    hole.width + padding * 2f, hole.height + padding * 2f);

                float xMin = Mathf.Max(padded.xMin, area.xMin);
                float yMin = Mathf.Max(padded.yMin, area.yMin);
                float xMax = Mathf.Min(padded.xMax, area.xMax);
                float yMax = Mathf.Min(padded.yMax, area.yMax);
                if (xMax - xMin <= Epsilon || yMax - yMin <= Epsilon) continue;

                s_holes.Add(new Rect(xMin, yMin, xMax - xMin, yMax - yMin));
            }
        }

        private static void CollectYBounds(Rect area)
        {
            s_bounds.Clear();
            AddBound(area.yMin);
            AddBound(area.yMax);
            for (int i = 0; i < s_holes.Count; i++)
            {
                AddBound(s_holes[i].yMin);
                AddBound(s_holes[i].yMax);
            }
            s_bounds.Sort();
        }

        private static void AddBound(float y)
        {
            for (int i = 0; i < s_bounds.Count; i++)
                if (Mathf.Abs(s_bounds[i] - y) <= Epsilon) return;
            s_bounds.Add(y);
        }

        private static int CompareSpanStart(Vector2 a, Vector2 b) => a.x.CompareTo(b.x);
    }
}
