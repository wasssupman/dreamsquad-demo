using Unity.Collections;
using Unity.Mathematics;
using Random = Unity.Mathematics.Random;

namespace Wassup.Data
{
    public static class PathCarver
    {
        public const float DetourProbability = 0.15f;
        public const int MaxStepsMultiplier = 4;

        public static bool CarveAllSpawnsToGoal(
            ref Random rng,
            NativeArray<MapTileType> tiles,
            int2 gridSize,
            NativeArray<int2> spawns,
            int2 goal,
            MapPathShape shape = MapPathShape.Free)
        {
            if (!MapConnectivity.InBounds(goal, gridSize)) return false;
            if (!BranchNodesHaveGap(spawns)) return false;

            SetTile(tiles, gridSize, goal, MapTileType.Walk);
            int trunkX = BranchTrunkX(gridSize);
            int2 mergeRoot = new int2(trunkX, goal.y);
            SetTile(tiles, gridSize, mergeRoot, MapTileType.Walk);
            CarveStraightSegment(tiles, gridSize, mergeRoot, goal);

            for (int i = 0; i < spawns.Length; i++)
            {
                if (!MapConnectivity.InBounds(spawns[i], gridSize)) return false;
                var branchNode = new int2(trunkX, spawns[i].y);
                if (!MapConnectivity.InBounds(branchNode, gridSize)) return false;

                if (shape == MapPathShape.Straight)
                {
                    CarveStraightSegment(tiles, gridSize, spawns[i], branchNode);
                }
                else if (!CarveFreePath(ref rng, tiles, gridSize, spawns[i], branchNode))
                {
                    return false;
                }

                CarveStraightSegment(tiles, gridSize, branchNode, mergeRoot);
            }

            return true;
        }

        private static bool BranchNodesHaveGap(NativeArray<int2> spawns)
        {
            for (int i = 0; i < spawns.Length; i++)
            for (int j = i + 1; j < spawns.Length; j++)
            {
                if (math.abs(spawns[i].y - spawns[j].y) < 2) return false;
            }
            return true;
        }

        private static int BranchTrunkX(int2 gridSize)
        {
            return math.clamp((gridSize.x * 2) / 3, 1, math.max(1, gridSize.x - 2));
        }

        private static bool CarveFreePath(
            ref Random rng,
            NativeArray<MapTileType> tiles,
            int2 gridSize,
            int2 spawn,
            int2 goal)
        {
            int2 current = spawn;
            SetTile(tiles, gridSize, current, MapTileType.Walk);

            int manhattan = math.abs(goal.x - spawn.x) + math.abs(goal.y - spawn.y);
            int maxSteps = math.max(1, manhattan * MaxStepsMultiplier);

            for (int step = 0; step < maxSteps; step++)
            {
                if (current.x == goal.x && current.y == goal.y) return true;

                int2 next = current + DecideStepDir(ref rng, current, goal);
                if (!MapConnectivity.InBounds(next, gridSize)) continue;

                SetTile(tiles, gridSize, next, MapTileType.Walk);
                current = next;
            }

            return current.x == goal.x && current.y == goal.y;
        }

        private static void CarveStraightSegment(
            NativeArray<MapTileType> tiles,
            int2 gridSize,
            int2 spawn,
            int2 goal)
        {
            int2 current = spawn;
            SetTile(tiles, gridSize, current, MapTileType.Walk);
            CarveAxis(tiles, gridSize, ref current, new int2(goal.x, current.y));
            CarveAxis(tiles, gridSize, ref current, goal);
        }

        private static void CarveAxis(NativeArray<MapTileType> tiles, int2 gridSize, ref int2 current, int2 target)
        {
            while (current.x != target.x)
            {
                current.x += math.sign(target.x - current.x);
                if (MapConnectivity.InBounds(current, gridSize))
                    SetTile(tiles, gridSize, current, MapTileType.Walk);
            }

            while (current.y != target.y)
            {
                current.y += math.sign(target.y - current.y);
                if (MapConnectivity.InBounds(current, gridSize))
                    SetTile(tiles, gridSize, current, MapTileType.Walk);
            }
        }

        private static int2 DecideStepDir(ref Random rng, int2 current, int2 goal)
        {
            int dx = goal.x - current.x;
            int dy = goal.y - current.y;

            if (rng.NextFloat() < DetourProbability)
            {
                bool verticalDetour = math.abs(dx) >= math.abs(dy);
                int sign = rng.NextBool() ? 1 : -1;
                return verticalDetour ? new int2(0, sign) : new int2(sign, 0);
            }

            if (math.abs(dx) > math.abs(dy))
                return new int2(math.sign(dx), 0);
            if (math.abs(dy) > math.abs(dx))
                return new int2(0, math.sign(dy));

            return rng.NextBool()
                ? new int2(math.sign(dx), 0)
                : new int2(0, math.sign(dy));
        }

        private static void SetTile(NativeArray<MapTileType> tiles, int2 gridSize, int2 cell, MapTileType type)
        {
            tiles[cell.y * gridSize.x + cell.x] = type;
        }
    }
}
