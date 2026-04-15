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
        [SerializeField] private List<PathDefinition> paths = new List<PathDefinition>();

        public TileType GetTile(int x, int y) => tiles[y * Width + x];
        public IReadOnlyList<PathDefinition> Paths => paths;
        public TileType[] RawTiles => tiles;

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (tiles != null && tiles.Length != Width * Height)
                UnityEngine.Debug.LogWarning($"[MapData] tiles length must be {Width * Height}, got {tiles.Length}");
        }
#endif
    }

    [System.Serializable]
    public class PathDefinition
    {
        public string id;
        public List<Vector2Int> waypoints = new List<Vector2Int>();
    }
}
