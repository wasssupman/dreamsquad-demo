using UnityEngine;

namespace Wassup.Data
{
    public readonly struct TerrainTileRenderInfo
    {
        public readonly bool drawBase;
        public readonly Texture2D baseTexture;
        public readonly float baseYaw;
        public readonly float baseHeightOffset;
        public readonly float baseScale;
        public readonly bool drawEdgeOverlay;
        public readonly Texture2D edgeOverlayTexture;
        public readonly int edgeOverlayMask;

        public TerrainTileRenderInfo(
            bool drawBase,
            Texture2D baseTexture,
            float baseYaw,
            float baseHeightOffset,
            float baseScale,
            bool drawEdgeOverlay = false,
            Texture2D edgeOverlayTexture = null,
            int edgeOverlayMask = 0)
        {
            this.drawBase = drawBase;
            this.baseTexture = baseTexture;
            this.baseYaw = baseYaw;
            this.baseHeightOffset = baseHeightOffset;
            this.baseScale = baseScale;
            this.drawEdgeOverlay = drawEdgeOverlay;
            this.edgeOverlayTexture = edgeOverlayTexture;
            this.edgeOverlayMask = edgeOverlayMask;
        }

        public static TerrainTileRenderInfo None => new TerrainTileRenderInfo(false, null, 0f, 0f, 1f);
    }
}
