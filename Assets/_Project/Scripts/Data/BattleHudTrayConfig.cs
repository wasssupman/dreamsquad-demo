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

        // action-tray unit 1 — 슬롯 정보 계층(비용 ⚡플레이트·role badge·이름 밴드).
        // 값은 전부 여기서(컴포넌트 매직 넘버 금지 계약). 시안(battle-hud-safe-
        // action-tray-proposal.jpg): 비용 = 좌상단 다크 코너 플레이트에 볼트+숫자.
        [Header("Slot Info (unit 1)")]
        public Sprite slotCostBolt; // 좌상단 비용 ⚡ 아이콘 (null = 볼트 생략, 숫자만)
        public Vector2 costPlateSize = new Vector2(56f, 26f);
        public float costFontSize = 18f;
        public Color costPlateColor = new Color(0.03f, 0.06f, 0.12f, 0.88f);
        public Vector2 roleBadgeSize = new Vector2(26f, 26f);
        public float roleFontSize = 14f;
        public float nameFontMin = 16f;
        public float nameFontMax = 26f;
        public Color nameBandColor = new Color(0.02f, 0.04f, 0.09f, 0.72f);

        // action-tray unit 2 — 코스트 레일. 시안 정합: 별도 캡슐이 아니라 트레이와
        // 같은 fill/border 로 상단 중앙에서 이어진 탭(overlap 으로 실루엣 연결).
        // 위치 = (anchoredY + phase 트레이 높이 − railOverlap) — 두 컴포넌트가
        // 같은 geometry 를 소비해 phase 전환이 한 프레임에 정합된다.
        [Header("Energy Rail (unit 2)")]
        public Vector2 railSize = new Vector2(440f, 54f);
        public float railOverlap = 14f;

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
