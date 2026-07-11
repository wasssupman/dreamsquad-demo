using UnityEngine;

namespace Wassup.Data
{
    /// <summary>
    /// Shared presentation contract for the defender action tray and its later
    /// hand/energy-rail variants. Safe-area ownership stays with UiCanvasSetup.
    /// </summary>
    [CreateAssetMenu(menuName = "Wassup/UI/Battle HUD Tray Config", fileName = "BattleHudTrayConfig")]
    public sealed class BattleHudTrayConfig : ScriptableObject
    {
        [Header("Frame")]
        public Sprite trayFrame;
        public Color fallbackFill = new Color(0.05f, 0.11f, 0.20f, 0.96f);
        public Color fallbackBorder = new Color(0.94f, 0.72f, 0.24f, 1f);

        [Header("Layout")]
        public Vector2 placementSize = new Vector2(980f, 136f);
        public Vector2 battleSize = new Vector2(980f, 104f);
        public float anchoredY = 32f;
        public float slotSpacing = 8f;
        public int horizontalPadding = 18;
        public int verticalPadding = 12;
    }
}
