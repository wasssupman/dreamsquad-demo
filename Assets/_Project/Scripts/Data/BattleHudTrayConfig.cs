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
        // tray-cost-well 5 — 액체 셰이더 머티리얼(Wassup/UI/CostWell). 표면 파형·
        // 깊이 음영·유리 반사를 프래그먼트가 만든다. 미할당이면 절차 스프라이트
        // 폴백(출렁임 없는 단색 채움).
        // 씬이 아니라 여기 두는 이유: 씬 저장은 미저장 WIP 까지 함께 베이크한다.
        // Shader.Find 를 안 쓰는 이유: 빌드 스트리핑 사고 이력(2026-07-15).
        public Material wellLiquidMaterial;
        [Tooltip("코스트 셀 숫자. 슬롯 가격(costFontSize)보다 커야 '내 잔량'과 '유닛 가격'이 구분된다")]
        public float cellNumberFontSize = 52f;
        // defender-placement-cooldown 2 (사용자 결정 2026-07-22) — 액체 juice 를 코스트 물통으로 이관:
        // 기포(수면까지 떠오름) + 테두리 림 글로우(호흡). CostDisplay 가 소비. alpha 0 이면 해당 효과 off.
        [Tooltip("코스트 물통 안 기포 색(수면까지 떠오르며 페이드). alpha 0 이면 기포 없음")]
        public Color wellBubbleColor = new Color(0.85f, 0.95f, 1f, 0.4f);
        [Tooltip("코스트 물통 테두리 림 글로우 색(느린 호흡 펄스). alpha 0 이면 림 없음")]
        public Color wellRimColor = new Color(0.6f, 0.9f, 1f, 0.5f);

        // defender-placement-cooldown 2 — 배치 쿨타임 오버레이. 코스트 물통과 같은
        // 액체 셰이더(wellLiquidMaterial)를 재사용하되 색/방향/위치/숫자로 구분한다:
        // 여기 색은 탁한 슬레이트(코스트 하늘색/골드와 대비), _Fill=남은비율이라 액체가
        // 아래로 빠지며(코스트는 차오름) 유닛이 떠오른다. 셰이더가 액체색 alpha 를
        // 안 보므로 반투명은 cooldownLiquidOpacity(Image.color.a)로 준다.
        [Header("Placement Cooldown Overlay (defender-placement-cooldown 2)")]
        public Color cooldownLiquidBottom = new Color(0.04f, 0.05f, 0.08f, 1f);
        public Color cooldownLiquidTop = new Color(0.09f, 0.11f, 0.16f, 1f);
        [Tooltip("수면 하이라이트 — 어두운 body 위에서 파형이 읽히도록 약간 밝게")]
        public Color cooldownLiquidSurface = new Color(0.42f, 0.50f, 0.66f, 0.6f);
        [Range(0f, 1f)]
        [Tooltip("오버레이 불투명도(Image.color.a). 높을수록 더 어둡게 덮는다(포트레이트 덜 비침)")]
        public float cooldownLiquidOpacity = 0.92f;
        public Color cooldownTextColor = Color.white;
        public float cooldownFontSize = 38f;
        [Tooltip("쿨타임 종료 시 슬롯 스케일 펀치 배율")]
        public float cooldownReadyPopScale = 1.16f;
        public float cooldownReadyPopDuration = 0.22f;
        [Tooltip("쿨타임 중 셀 전체 딤 스크림(이름밴드/칩 포함). 빠지는 액체와 별개로 셀 전체를 상시 어둡게 = '이 셀 비활성'")]
        public Color cooldownCellDim = new Color(0.02f, 0.03f, 0.06f, 0.55f);

        // defender-placement-cooldown 2 juice — 말랑말랑 연출. (3) 숫자 틱 바운스 + (4) 종료 플러리시.
        [Tooltip("카운트다운 정수 변화(3→2→1) 시 숫자 스쿼시&스트레치 팝 배율")]
        public float cooldownTickPopScale = 1.35f;
        [Tooltip("종료 잔물결 링 색")]
        public Color cooldownReadyRippleColor = new Color(0.60f, 0.90f, 1f, 0.9f);
        [Tooltip("종료 잔물결 링 최대 확장 배율(슬롯 크기 대비)")]
        public float cooldownReadyRippleScale = 1.7f;
        [Tooltip("종료 섬광 색(빠르게 페이드)")]
        public Color cooldownReadyFlashColor = new Color(1f, 1f, 1f, 0.5f);
        [Tooltip("쿨타임 오버레이 테두리 림 글로우 색(느린 호흡 펄스). alpha 0 이면 림 없음. 기포는 코스트 물통으로 이관")]
        public Color cooldownRimColor = new Color(0.55f, 0.85f, 1f, 0.5f);

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
