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
        // action-tray unit 3 — 드림캐쳐 핸드 외곽(트레이와 같은 폭/문법, 높이는 카드 fan).
        public Vector2 handSize = new Vector2(980f, 232f);
        public float anchoredY = 32f;
        public float slotSpacing = 8f;
        public int horizontalPadding = 18;
        public int verticalPadding = 12;

        // tray-cost-well unit 0 — 트레이 폭은 상수가 아니라 슬롯 수에서 유도한 뒤
        // 하단 코너 위젯 예약폭으로 클램프한 값이다(unit 4). placementSize 는 폴백.
        [Header("Tray Sizing (cost-well unit 0)")]
        [Tooltip("목표 슬롯 폭 — 트레이 폭 산출 입력")]
        public float slotWidth = 154f;
        public float slotHeight = 134f;
        [Tooltip("하단 코너 위젯(전투시작 버튼·각성 게이지·NextWaveDock) 예약폭. 트레이 폭 상한 = SafeArea 폭 − 이 값")]
        public float cornerReservedWidth = 640f;

        // action-tray unit 1 — 슬롯 정보 계층(비용 ⚡플레이트·role badge·이름 밴드).
        // 값은 전부 여기서(컴포넌트 매직 넘버 금지 계약). 시안(battle-hud-safe-
        // action-tray-proposal.jpg): 비용 = 좌상단 다크 코너 플레이트에 볼트+숫자.
        // tray-cost-well unit 3 — slotCostBolt / roleBadgeSize / roleFontSize 제거.
        // 슬롯 ⚡볼트는 코스트 셀의 아이콘 1개로 통합됐고(CostDisplay.costEnergyIcon),
        // role 배지는 이름 밴드 틴트로 대체됐다. roles 데이터는 그 틴트 입력이라 유지.
        [Header("Slot Info (unit 1)")]
        public Vector2 costPlateSize = new Vector2(52f, 44f);
        public float costFontSize = 40f;
        public Color costPlateColor = new Color(0.03f, 0.06f, 0.12f, 0.88f);
        public float nameFontMin = 16f;
        public float nameFontMax = 26f;
        public Color nameBandColor = new Color(0.02f, 0.04f, 0.09f, 0.88f);

        // tray-cost-well unit 0 — 트레이 좌측 코스트 셀(상단 숫자 / 하단 물통).
        // 액체 색은 각성 게이지(보라→시안)와 색상 축에서 분리한다 — 두 위젯이
        // Battle 페이즈에 동시에 떠 있고 동작 규칙이 정반대라(각성=보유량,
        // 물통=진행률) 어휘까지 같으면 한 덩어리로 읽힌다(unit 2).
        [Header("Cost Cell (cost-well unit 0)")]
        [Tooltip("계약: slotWidth 이하. 자원 표시가 행동 대상보다 넓으면 위계가 뒤집힌다")]
        public float costCellWidth = 154f;
        public float cellNumberHeight = 48f;
        public float cellRowGap = 4f;
        public Vector2 wellPadding = new Vector2(8f, 6f);
        public Color wellBackColor = new Color(0.04f, 0.06f, 0.11f, 0.92f);
        [Tooltip("충전 중 액체색")]
        public Color wellLiquidColor = new Color(0.95f, 0.62f, 0.15f, 1f);
        [Tooltip("가득 찼을 때 액체색 — wellLiquidColor 와 명도차를 유지한다(색 단독 판별 금지)")]
        public Color wellLiquidFullColor = new Color(1f, 0.85f, 0.25f, 1f);
        public Color wellSurfaceColor = new Color(1f, 0.95f, 0.7f, 0.85f);
        [Tooltip("코스트 셀 숫자. 슬롯 가격(costFontSize)보다 커야 '내 잔량'과 '유닛 가격'이 구분된다")]
        public float cellNumberFontSize = 52f;

        [Header("Slot Name Band (cost-well unit 0)")]
        public float nameBandHeight = 36f;
        public float nameTextHeight = 38f;

        // tray-cost-well unit 1 — 코스트 레일(트레이 상단에 겹친 별도 캔버스 탭)은
        // 트레이 안 좌측 셀로 흡수되면서 사라졌다. railSize/railOverlap 제거.

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
