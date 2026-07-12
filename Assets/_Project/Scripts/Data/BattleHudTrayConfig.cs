using System;
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

        // action-tray unit 1 — 슬롯 정보 계층(비용 chip·role badge·이름 밴드).
        // 값은 전부 여기서(컴포넌트 매직 넘버 금지 계약).
        [Header("Slot Info (unit 1)")]
        public Sprite costChipSprite; // null = procedural 원형 폴백
        public Vector2 costChipSize = new Vector2(34f, 34f);
        public float costFontSize = 18f;
        public Vector2 roleBadgeSize = new Vector2(26f, 26f);
        public float roleFontSize = 14f;
        public float nameFontMin = 16f;
        public float nameFontMax = 26f;
        public Color nameBandColor = new Color(0f, 0f, 0f, 0.35f);

        [Header("Affordability (unit 1)")]
        [Tooltip("구매 불가 시 포트레이트/배경 곱색 (색 단독 금지 — chip 강조·glyph 동반)")]
        public Color unaffordableDim = new Color(0.45f, 0.45f, 0.52f, 1f);
        [Tooltip("구매 불가 시 비용 숫자 강조색")]
        public Color costWarnColor = new Color(1f, 0.34f, 0.28f, 1f);

        // DefenderClass별 role 표기. 누락/중복 시 소비측이 neutral 폴백(계약).
        [Serializable]
        public struct RolePresentation
        {
            public DefenderClass role;
            [Tooltip("배지에 표시할 1글자 표기 (예: 원/수/근/술/보)")]
            public string glyph;
            public Color color;
        }

        [Header("Role Presentation (unit 1)")]
        public RolePresentation[] roles;

        public bool TryGetRole(DefenderClass role, out RolePresentation entry)
        {
            if (roles != null)
            {
                for (int i = 0; i < roles.Length; i++)
                    if (roles[i].role == role) { entry = roles[i]; return true; }
            }
            entry = default;
            return false;
        }
    }
}
