using UnityEngine;
using Wassup.Data.Season;

namespace Wassup.Presentation.Backdrop
{
    public static class BackdropAnchorTable
    {
        public static Vector3 Resolve(EdgeAnchor anchor, Vector3 boardCenter,
                                      Vector2 boardHalfWorld, float paddingTiles, float tileSize)
        {
            float pad = paddingTiles * tileSize;
            float xL = boardCenter.x - boardHalfWorld.x;
            float xC = boardCenter.x;
            float xR = boardCenter.x + boardHalfWorld.x;
            float zS = boardCenter.z - boardHalfWorld.y;  // South
            float zN = boardCenter.z + boardHalfWorld.y;  // North
            float zM = boardCenter.z;                     // Middle

            return anchor switch
            {
                EdgeAnchor.NorthLeft   => new Vector3(xL,       boardCenter.y, zN + pad),
                EdgeAnchor.NorthCenter => new Vector3(xC,       boardCenter.y, zN + pad),
                EdgeAnchor.NorthRight  => new Vector3(xR,       boardCenter.y, zN + pad),
                EdgeAnchor.EastTop     => new Vector3(xR + pad, boardCenter.y, zN),
                EdgeAnchor.EastMiddle  => new Vector3(xR + pad, boardCenter.y, zM),
                EdgeAnchor.EastBottom  => new Vector3(xR + pad, boardCenter.y, zS),
                EdgeAnchor.SouthRight  => new Vector3(xR,       boardCenter.y, zS - pad),
                EdgeAnchor.SouthCenter => new Vector3(xC,       boardCenter.y, zS - pad),
                EdgeAnchor.SouthLeft   => new Vector3(xL,       boardCenter.y, zS - pad),
                EdgeAnchor.WestBottom  => new Vector3(xL - pad, boardCenter.y, zS),
                EdgeAnchor.WestMiddle  => new Vector3(xL - pad, boardCenter.y, zM),
                EdgeAnchor.WestTop     => new Vector3(xL - pad, boardCenter.y, zN),
                _ => boardCenter,
            };
        }
    }
}
