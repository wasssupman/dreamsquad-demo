using System.Collections.Generic;
using UnityEngine;

namespace Wassup.Data
{
    public enum TileType : byte
    {
        Buildable = 0,
        Path = 1,
        Obstacle = 2,
    }

    [CreateAssetMenu(fileName = "MapData", menuName = "Wassup/MapData", order = 0)]
    public class MapData : ScriptableObject
    {
        public const int Width = 20;
        public const int Height = 10;

        [SerializeField] private TileType[] tiles = new TileType[Width * Height];

        [SerializeField] private Vector2Int goalCell = new Vector2Int(19, 5);
        [SerializeField] private Vector2Int[] spawnCells = { new Vector2Int(0, 5) };

        public TileType GetTile(int x, int y) => tiles[y * Width + x];
        public TileType[] RawTiles => tiles;

        public Vector2Int GoalCell => goalCell;
        public IReadOnlyList<Vector2Int> SpawnCells => spawnCells;

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (tiles != null && tiles.Length != Width * Height)
                UnityEngine.Debug.LogWarning($"[MapData] tiles length must be {Width * Height}, got {tiles.Length}");
            if (spawnCells == null || spawnCells.Length == 0)
                UnityEngine.Debug.LogWarning("[MapData] spawnCells must contain at least 1 cell");
        }
#endif
    }
}
