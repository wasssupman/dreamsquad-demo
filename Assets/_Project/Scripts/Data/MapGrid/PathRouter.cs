using Unity.Collections;
using Unity.Mathematics;
using Random = Unity.Mathematics.Random;

namespace Wassup.Data.MapGrid
{
    /// <summary>
    /// L / U / Z 형태의 직선 + 90° 후보 경로를 셀 단위로 생성. start → attach 양 끝점 포함.
    /// </summary>
    public static class PathRouter
    {
        // shape:
        // 0 : L via (attach.x, start.y)
        // 1 : L via (start.x, attach.y)
        // 2 : U_h via (mx, start.y) → (mx, attach.y) → attach
        // 3 : U_v via (start.x, my) → (attach.x, my) → attach
        // 4 : Z_h via (mx, start.y) → (mx, my) → (attach.x, my) → attach
        // 5 : Z_v via (start.x, my) → (mx, my) → (mx, attach.y) → attach
        public const int ShapeCount = 6;

        // shape 6 (4-turn S): start → (mx1, sy) → (mx1, my) → (mx2, my) → (mx2, ay) → attach
        public static bool TryBuild4Turn(
            int2 start, int2 attach, int2 gridSize,
            int mx1, int my, int mx2,
            NativeList<int2> waypoints)
        {
            waypoints.Clear();
            if (mx1 <= 0 || mx1 >= gridSize.x - 1) return false;
            if (my  <= 0 || my  >= gridSize.y - 1) return false;
            if (mx2 <= 0 || mx2 >= gridSize.x - 1) return false;
            if (mx1 == mx2) return false;

            waypoints.Add(start);
            waypoints.Add(new int2(mx1, start.y));
            waypoints.Add(new int2(mx1, my));
            waypoints.Add(new int2(mx2, my));
            waypoints.Add(new int2(mx2, attach.y));
            waypoints.Add(attach);
            return true;
        }

        // shape 7 (5-turn W): start → (sx, my1) → (mx1, my1) → (mx1, my2) → (mx2, my2) → (mx2, ay) → attach
        public static bool TryBuild5Turn(
            int2 start, int2 attach, int2 gridSize,
            int mx1, int my1, int mx2, int my2,
            NativeList<int2> waypoints)
        {
            waypoints.Clear();
            if (mx1 <= 0 || mx1 >= gridSize.x - 1) return false;
            if (my1 <= 0 || my1 >= gridSize.y - 1) return false;
            if (mx2 <= 0 || mx2 >= gridSize.x - 1) return false;
            if (my2 <= 0 || my2 >= gridSize.y - 1) return false;
            if (mx1 == mx2 || my1 == my2) return false;

            waypoints.Add(start);
            waypoints.Add(new int2(start.x, my1));
            waypoints.Add(new int2(mx1, my1));
            waypoints.Add(new int2(mx1, my2));
            waypoints.Add(new int2(mx2, my2));
            waypoints.Add(new int2(mx2, attach.y));
            waypoints.Add(attach);
            return true;
        }

        public static bool TryBuildShape(
            int shape, int2 start, int2 attach, int2 gridSize, int mx, int my,
            NativeList<int2> waypoints)
        {
            waypoints.Clear();
            waypoints.Add(start);

            switch (shape)
            {
                case 0:
                    waypoints.Add(new int2(attach.x, start.y));
                    break;
                case 1:
                    waypoints.Add(new int2(start.x, attach.y));
                    break;
                case 2:
                    if (mx < 0 || mx >= gridSize.x) return false;
                    waypoints.Add(new int2(mx, start.y));
                    waypoints.Add(new int2(mx, attach.y));
                    break;
                case 3:
                    if (my < 0 || my >= gridSize.y) return false;
                    waypoints.Add(new int2(start.x, my));
                    waypoints.Add(new int2(attach.x, my));
                    break;
                case 4:
                    if (mx < 0 || mx >= gridSize.x || my < 0 || my >= gridSize.y) return false;
                    waypoints.Add(new int2(mx, start.y));
                    waypoints.Add(new int2(mx, my));
                    waypoints.Add(new int2(attach.x, my));
                    break;
                case 5:
                    if (mx < 0 || mx >= gridSize.x || my < 0 || my >= gridSize.y) return false;
                    waypoints.Add(new int2(start.x, my));
                    waypoints.Add(new int2(mx, my));
                    waypoints.Add(new int2(mx, attach.y));
                    break;
                default:
                    return false;
            }

            waypoints.Add(attach);
            return true;
        }

        /// <summary>
        /// 인접 waypoint 사이를 axis-aligned line 으로 확장. 결과 셀 시퀀스 길이 ≥ 2.
        /// 인접 waypoint 가 같은 셀이거나 axis-aligned 가 아니면 false.
        /// </summary>
        public static bool TryExpandToCells(NativeList<int2> waypoints, NativeList<int2> outCells)
        {
            outCells.Clear();
            if (waypoints.Length < 2) return false;

            // 중복 인접 waypoint 제거
            var cleaned = new NativeList<int2>(waypoints.Length, Allocator.TempJob);
            try
            {
                cleaned.Add(waypoints[0]);
                for (int i = 1; i < waypoints.Length; i++)
                {
                    if (math.all(waypoints[i] == cleaned[cleaned.Length - 1])) continue;
                    cleaned.Add(waypoints[i]);
                }

                if (cleaned.Length < 2) return false;

                for (int i = 0; i < cleaned.Length - 1; i++)
                {
                    int2 a = cleaned[i];
                    int2 b = cleaned[i + 1];
                    if (a.x != b.x && a.y != b.y) return false;

                    int dx = (b.x > a.x) ? 1 : (b.x < a.x ? -1 : 0);
                    int dy = (b.y > a.y) ? 1 : (b.y < a.y ? -1 : 0);

                    int x = a.x, y = a.y;
                    if (i == 0) outCells.Add(new int2(x, y));
                    while (x != b.x || y != b.y)
                    {
                        x += dx; y += dy;
                        outCells.Add(new int2(x, y));
                    }
                }

                return outCells.Length >= 2;
            }
            finally
            {
                cleaned.Dispose();
            }
        }
    }
}
