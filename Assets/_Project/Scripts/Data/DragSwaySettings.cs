using UnityEngine;
using UnityEngine.Serialization;

namespace Wassup.Data
{
    // keyring-cord-preview — 드래그/탭 배치 프리뷰 튜닝값 묶음(키링 스윙 = 제어형 스프링 진자).
    // 움직임은 자유 물리 시뮬이 아니라 이동범위·각도·스프링·댐핑으로 제어한다(고리 아래 실루엣이 진자처럼 스윙).
    // 컨트롤러가 런타임 AddComponent 라 인스펙터 튜닝이 안 되므로 SO 로 분리. DefenderSelector 에 할당 →
    // Configure 로 주입. 미할당이면 컨트롤러가 클래스 기본값 인스턴스로 폴백. 에셋 편집은 런타임 즉시 반영.
    //
    // 그룹 맵(인스펙터 헤더 순): ① 키링 추종  ② 줄·고리 비주얼  ③ 스타일  ④ 배치 컷신
    //                          ⑤ 컷신 틸트  ⑥ 셀 스냅  ⑦ 탭 배치 시뮬  ⑧ 방향 페이즈
    [CreateAssetMenu(menuName = "Wassup/Drag Sway Settings", fileName = "DragSwaySettings")]
    public class DragSwaySettings : ScriptableObject
    {
        [Header("① 키링 추종 — 고리 밑 유닛이 진자처럼 지연 스윙 (스프링+속도상한)")]
        [Tooltip("줄 길이 — 고리 아래 매달리는 로컬 길이(월드=×visualScale). ↑=길게 늘어짐.")]
        public float ropeLength = 2.0f;
        [Tooltip("최대 기울임각 — 유닛이 흔들리는 각도 상한(deg). ↓=덜 흔들림.")]
        public float maxAngle = 8f;
        [Tooltip("추종 강성 — 목표를 따라붙는 탄성 세기. ↑=팽팽/빠릿, ↓=늘어지고 부드럽게.")]
        public float spring = 100f;
        [Tooltip("감쇠 — 탄성 잔진동 억제. ↑=빨리 멎음, ↓=바운스·출렁 큼(floaty).")]
        public float damping = 2.5f;
        [Tooltip("추종 속도 상한(월드/s) — 빠른 스와이프 시 튀어나감만 방지(탄성은 유지). 0=무제한.")]
        public float maxSpeed = 12f;

        [Header("② 줄·고리 비주얼 — 프리뷰 줄/고리 굵기·색·크기")]
        [Tooltip("줄 폭(로컬, 월드=×visualScale) — 너무 얇으면 sub-pixel 로 렌더 컬링됨.")]
        public float cordWidth = 0.14f;
        [Tooltip("줄 색 — 절차적 폴백 줄/고리 틴트(스타일 스프라이트 있으면 무시).")]
        public Color cordColor = new Color(0.45f, 0.38f, 0.28f, 1f);
        [Tooltip("고리 반경(로컬, 월드=×visualScale).")]
        public float ringRadius = 0.18f;
        [Tooltip("실루엣 추가 드롭(로컬) — 머리는 줄 끝 자동정렬, 이 값은 미세조정. 0=머리가 줄 끝.")]
        public float charmDrop = 0.0f;

        [Header("③ 스타일 — 월드 슬롯 스프라이트(비우면 절차적 폴백)")]
        [Tooltip("키링 스타일 — 월드 슬롯(ringSprite/worldCord·RingMaterial) 사용. 비우면 절차적 폴백(원 루프 + cordColor 단색 줄).")]
        public KeyringStyle style;

        [Header("④ 배치 컷신 — 드래그 시작 시 좌상단 유닛 컷신 on/off")]
        [Tooltip("컷신 on/off — 드래그 배치 시작 시 좌상단 유닛 컷신 재생. 끄면 프레임 있어도 미재생.")]
        public bool enableDeployCutscene = true;

        [Header("⑤ 컷신 틸트 — 스와이프 속도 → 컷신 기울임 피드")]
        [Tooltip("틸트 포화 속도 — 이 화면px/s 스와이프에서 틸트 |1| 포화. ↓=작은 스와이프도 크게 기욺.")]
        public float deployCutsceneSwipeRefSpeed = 1400f;
        [Tooltip("틸트 스무딩 — 스와이프 속도 exp-lerp 계수(0..1). ↑=빠릿(즉각), ↓=부드럽게 지연.")]
        public float deployCutsceneSwipeSmoothing = 0.5f;

        [Header("⑥ 셀 스냅 — 포커스 칸 히스테리시스·throttle·액체 하이라이트")]
        [Tooltip("자석 세기 — 타일 경계 sticky 여유(타일 분수). 떨림 면역=2×margin, 대가로 전환이 margin 만큼 늦음.\n" +
                 "0=순수 반올림(면역 0). 0.2~0.3 권장. 0.5↑=이웃 칸에 깊이 들어가도 안 넘어감(끈적).\n" +
                 "상한 0.95 — 1.0 이상이면 이웃 셀을 건너뛴다(코드에서 clamp).")]
        [Range(0f, 0.95f)]
        public float placementStickMargin = 0.3f;
        [Tooltip("격자 밖 관용(셀) — 손가락이 보드를 벗어나 이 칸 수까지는 테두리 칸으로 붙여준다.\n" +
                 "그보다 더 나가면 **아무 칸도 잡지 않는다** → 놓으면 취소(drag-cancel-affordance unit 3).\n" +
                 "이 값이 없던 시절엔 격자로 무조건 clamp 해서 화면 어디서 놓아도 셀이 나왔고, 그래서\n" +
                 "'보드 밖 = 취소' 가 성립하지 않았다. ↑=가장자리 칸이 관대하지만 취소가 멀어짐. 0=격자 밖은 즉시 취소.")]
        [Range(0, 4)]
        public int placementOutsideToleranceCells = 1;
        [Tooltip("판정 갱신 주기(초) — 이동 중 이 간격마다 현재 칸으로 스텝 갱신. ↓=더 자주(실시간에 가까움). 0=매 프레임.")]
        [Range(0f, 1f)]
        public float placementCommitInterval = 0.5f;
        [Tooltip("끈적 액체 하이라이트 — 포커스 셀 하이라이트를 '고정 테두리 + 손가락 쪽으로 번지는 내부 액체'로 대체.\n" +
                 "히스테리시스(margin)의 시각화: 번짐이 테두리를 넘으면 곧 옆 칸으로 전환된다는 예고.\n" +
                 "끄면 기존 타일 하이라이트. 모양 튜닝 = PlacementLiquidTile.mat, 색 = TilemapMapView(liquid*).")]
        [FormerlySerializedAs("stickyBlobEnabled")]
        public bool stickyLiquidEnabled = true;

        // ⑦ 은 원래 "탭 배치 시뮬 — 트레이에서 타일까지 고스트를 던지는 비행" 이었다.
        // **그 비행은 defender-drop-dismount unit 7 (2026-08-19) 에서 폐기됐다** — 탭 배치도 이제
        // D&D 와 같은 구조로 커밋이 먼저고 실유닛이 하마 궤적(⑩ 그룹)으로 난다. 그래서 시간·정착
        // 노브(tapTravelDuration / tapTravelScale* / tapSettle* / tapFollowSmoothTime)는 소비처가
        // 사라져 삭제했다. 복원이 필요하면 그 커밋의 RunSimulatedDrag 와 함께 되살릴 것.
        //
        // 남은 둘의 **현재 소유자**:
        //  · armHighlightColor — 트레이 슬롯 탭 arm 하이라이트(그대로).
        //  · tapArc* / tapThrow* — 이제 **재배치 비행**(DefenderDragPlacementController.ComputeThrowArc
        //    → DefenderRelocationController)이 유일 소비자다. 이름의 "tap" 은 유래일 뿐이라 사실과
        //    어긋나지만, 개명하면 저작값이 든 .asset 키가 갈리므로 유래를 여기 적어 두고 이름은 둔다.
        [Header("⑦ 던지기 곡선(재배치 비행) + 트레이 arm 하이라이트")]
        [Tooltip("arm 하이라이트 색 — 탭 선택된 트레이 슬롯. 확정 팝 valid 색과 톤 맞춤.")]
        public Color armHighlightColor = new Color(0.35f, 1f, 0.9f, 0.28f);
        [Tooltip("곡선 아치 높이(직선거리 배수) — 카메라-up 으로 제어점 띄움, 유닛이 솟았다 내려옴. 0=직선.")]
        [Range(0f, 1f)]
        public float tapArcHeightFactor = 0.32f;
        [Tooltip("곡선 좌우 폭(직선거리 배수) — 매 비행 부호·크기가 달라 경로가 매번 다름. 0=좌우 없음.")]
        [Range(0f, 1f)]
        public float tapArcLateralFactor = 0.22f;
        [Tooltip("던지기 시작 제어점 — x=목표까지 전진 비율, y=아치 높이 배수. 앞·위로 튀어나가는 출발 접선.")]
        public Vector2 tapThrowLaunchControl = new Vector2(0.18f, 1f);
        [Tooltip("던지기 도착 제어점 — x=목표까지 전진 비율, y=아치 높이 배수. 낮게 내려오는 착지 접선.")]
        public Vector2 tapThrowLandingControl = new Vector2(0.72f, 0.22f);

        [Header("⑧ 방향 페이즈 — 방향 지정 중 전투 슬로우모")]
        // 방향 지정 컨트롤러는 런타임 AddComponent 라 자체 인스펙터가 없다. 튜닝값이
        // 이 하나뿐이라 전용 SO(구 DirectionAimSettings) 대신 이미 씬에 배선된 여기로 합쳤다.
        [Tooltip("전투 시간 배율 — 방향 지정 중. 드래그 슬로우모를 이어받음. 0 금지(전투가 멈추면 안 된다).")]
        [Range(0.01f, 1f)]
        public float directionAimSlowmoScale = 0.2f;

        [Header("⑨ 보드 제스처 — armed 유닛 보드 프레스-드래그-릴리즈 배치")]
        // placement-armed-board-drag — arm 후 보드 press 를 tap/drag 로 가르는 이동 임계.
        // 시간이 아니라 이동량으로 판정(사용자 결정 2026-07-20). Unity EventSystem.pixelDragThreshold(기본 10)
        // 와 동류의 화면 px 값이나, 터치에서 조금 여유를 줘 의도치 않은 드래그 승격을 줄인다.
        [Tooltip("보드 탭/드래그 임계(스크린 px) — press 후 이 거리 이상 움직이면 드래그(범위 스카우트→릴리즈 배치), " +
                 "미만이면 탭(범위 피크). ↑=드래그로 잘 안 넘어감(탭이 관대), ↓=조금만 움직여도 드래그.")]
        [Range(1f, 64f)]
        public float boardDragThreshold = 16f;

        [Header("⑩ 드롭 하마 — 릴리스 반동→솟음→착지 (defender-drop-dismount)")]
        [Tooltip("총 시간(초, unscaled) — 반동+솟음+착지 전체. 런타임에 deploymentDuration 으로 클램프(공중=pending 보장). 느리게 관찰하려면 그 값도 같이 올려야 한다.")]
        [Range(0.1f, 1f)]
        public float dropTotalSeconds = 0.45f;
        [Tooltip("반동 구간(초) — 줄이 벙은 채 -camUp 으로 dip(힘 모으기). 총 시간 내 비율로 환산.")]
        [Range(0.02f, 0.3f)]
        public float dropRecoilSeconds = 0.12f;
        [Tooltip("반동 깊이(world) — dip = 시작점 − camUp×이 값.")]
        [Range(0f, 1f)]
        public float dropRecoilDip = 0.35f;
        [Tooltip("아치 제어점 높이 = max(거리×이 값, 하한). 실제 apex ≈ 0.4×제어점 높이.")]
        [Range(0f, 2f)]
        public float dropArcHeightFactor = 0.5f;
        [Tooltip("제어점 높이 절대 하한(world) — 짧은 드롭도 '솟음' 보장. 기본 3.5 → apex ≈ 1.4 world(유닛 키 ~1.2배).")]
        [Range(0f, 8f)]
        public float dropArcMinHeight = 3.5f;
        [Tooltip("솟음 시작 제어점 — x=목표까지 전진 비율, y=제어점 높이 배수.")]
        public Vector2 dropLaunchControl = new Vector2(0.25f, 1f);
        [Tooltip("착지 제어점 높이 배수 — end 직상방(수직 스틱 착지). 0 금지(접선 퇴화).")]
        [Range(0.05f, 1f)]
        public float dropLandingHeight = 0.25f;
        [Tooltip("분리 후 줄 페이드(초).")]
        [Range(0.05f, 1f)]
        public float dropCordSnapFade = 0.15f;
        [Tooltip("고리 페이드(초) — 놓은 자리에 잠시 남았다 사라짐.")]
        [Range(0.05f, 1f)]
        public float dropRingFade = 0.3f;
        // flight-lift-feel unit 3 — 리듬·착지 반응은 **연출별 취향**이라 궤적 기하와 같은 자리에 둔다
        // (보스 도약 값과 1:1 대응 유지). 뜬 높이의 시각 반응(확대·그림자)은 성격이 달라 화면 전역
        // 단일 소유다 — BattleBridge 의 lift* 노브. 이 구분을 뒤집지 말 것.
        [Tooltip("비행 구간 시간 리듬. 1=현행 등속. 낮출수록 초반 급상승→정점 체공→후반 급하강.")]
        [Range(0.3f, 1f)]
        public float dropHangPower = 0.7f;
        [Tooltip("착지 눌림 세기(0=없음). 가로 확장·세로 압축.")]
        [Range(0f, 0.4f)]
        public float dropLandingSquash = 0.10f;
        [Tooltip("착지 눌림 복귀 시간(초).")]
        [Range(0.02f, 0.3f)]
        public float dropLandingSquashSeconds = 0.05f;

        [Header("⑪ 배치 포인터 오프셋 — 손가락 가림 회피 (placement-thumb-occlusion)")]
        // 배치 판정 포인터를 실제 포인터보다 화면상 위로 파생시켜 손가락이 포커스 칸 하이라이트를
        // 덮지 않게 한다. 원 포인터를 덮어쓰지 않는다(UI 레이캐스트·탭/드래그 임계는 계속 raw).
        // 화면 높이 비율인 이유: Screen.dpi 가 Android 에서 신뢰 불가라 물리 단위(dp)를 쓸 수 없다.
        [Tooltip("배치 판정 포인터를 화면상 위로 올리는 양(화면 높이 비율). 0=현행(손가락 위치 그대로).\n" +
                 "↑=칸이 손가락에서 멀어져 잘 보이나, 최하단 행을 노리는 손가락이 화면 하단(트레이 영역)으로 밀린다.\n" +
                 "상한은 DragPlacementReachTest 의 하단 계측이 잰다 — 그 단언이 깨지는 값은 채택 금지.")]
        [Range(0f, 0.2f)]
        public float placementPointerOffsetHeightRatio = 0.06f;

        [Tooltip("오프셋 램프 거리(스크린 px) — 보드 제스처가 드래그로 승격된 뒤, 임계보다 이만큼 더 끌면 " +
                 "풀 오프셋에 도달한다. 승격 순간 하이라이트가 한 칸 순간이동하는 걸 막는다.\n" +
                 "시간이 아니라 이동량 기준 — 시간 램프는 손가락이 멈춰도 하이라이트가 계속 올라가고 " +
                 "16px 승격이 65px 점프를 만드는 증폭이 남는다. 0=램프 없음(승격 즉시 풀 오프셋).\n" +
                 "트레이 D&D 는 직전 하이라이트가 없어 램프 없이 첫 프레임부터 풀 오프셋이다.")]
        [Range(0f, 200f)]
        public float placementPointerOffsetRampDistance = 60f;

        // px 환산은 값과 같은 곳에 둔다(소비처: 드래그 컨트롤러 · 재배치 seam · PlayMode 테스트).
        public float PlacementPointerOffsetPx => placementPointerOffsetHeightRatio * Screen.height;

        [Header("⑫ 드래그 취소 — 트레이로 되돌리면 취소 (drag-cancel-affordance)")]
        // 취소 판정 자체는 트레이 패널 rect 가 소유한다(별도 좌표 노브를 두지 않는다 — 계약 2).
        // 여기 있는 건 "취소될 것"을 놓기 전에 알리는 룩뿐이다.
        [Tooltip("취소 존 안에서 드래그 프리뷰 실루엣 알파. ↓=더 유령처럼(배치 안 된다는 신호가 강함). 1=변화 없음.")]
        [Range(0.1f, 1f)]
        public float cancelPreviewAlpha = 0.4f;
        [Tooltip("취소 예고 라벨 색(포인터 추종). 거부 라벨의 코랄과 같은 계열로 두면 '이대로는 안 꽂힌다' 신호가 통일된다.")]
        public Color cancelTint = new Color(1f, 0.42f, 0.36f, 1f);
        [Tooltip("취소 예고 dwell(초) — 취소 상태(트레이 존 안 / 격자 밖)에 이 시간 이상 머물러야 예고가 켜진다.\n" +
                 "두 깜빡임을 막는다: 트레이 드래그는 취소 존 안에서 시작하고, 가장자리 열을 노리는 손가락은\n" +
                 "관용 링을 순간 넘었다 돌아온다. 존을 한 번 벗어난 뒤의 **재진입**은 의도적 복귀라 dwell 면제.\n" +
                 "0=즉시(오버슛마다 고스트 알파와 라벨이 껌뻑인다). ↑=늦게 뜸(가장 빠른 취소가 안 보인다).")]
        [Range(0f, 1f)]
        public float cancelHintDwellSeconds = 0.18f;

        [Header("⑬ Footprint 고스트·자석 (defender-footprint)")]
        [Tooltip("정상 Ghost — footprint 전 칸이 배치 가능할 때(하늘 계열).")]
        public Color ghostValidColor = new Color(0.32f, 0.78f, 1f, 0.55f);
        [Tooltip("Ghost 충돌 — footprint 안에서 문제가 된 칸(빨간 계열). 비공간 사유(코스트 등)면 전 칸이 이 색.")]
        public Color ghostInvalidColor = new Color(1f, 0.25f, 0.2f, 0.6f);
        [Tooltip("기배치 유닛 점유 — Ghost 주변 컨텍스트(노란 계열).")]
        public Color ghostOccupiedColor = new Color(1f, 0.85f, 0.25f, 0.5f);
        [Tooltip("지형 배치 불가 — 보드 전역 표시(빨간 계열, 사용자 결정 2026-08-28). 유닛을 들면 켜진다.")]
        public Color ghostTerrainColor = new Color(1f, 0.3f, 0.24f, 0.3f);
        [Tooltip("후보 앵커가 공간 사유로 무효일 때 최근접 유효 앵커로 흡착하는 자석 반경(셀).\n" +
                 "**기본 0 = 비활성**(사용자 결정 2026-08-28 — 위치 보정 없이 배치 불가 유지·빨간 고스트만).\n" +
                 "구현은 남아 있어 값을 올리면 다시 켜진다. 반경 밖은 배치 불가 유지(원거리 강제 보정 금지).")]
        [Range(0f, 4f)]
        public float placementMagnetRadiusCells = 0f;

        [Header("⑭ 배치 되돌리기 — 활성화 전 취소 유예 (defender-footprint unit 5)")]
        [Tooltip("false = 유예 버튼 자체를 끈다(브리지 되감기 API 는 남는다).")]
        public bool deployUndoEnabled = true;
        [Tooltip("버튼이 유닛 머리 위로 뜨는 화면 오프셋(px).")]
        public float deployUndoOffsetPx = 96f;
        [Tooltip("버튼 크기(px).")]
        public Vector2 deployUndoSize = new Vector2(168f, 56f);
        [Tooltip("버튼 배경색.")]
        public Color deployUndoBgColor = new Color(0.12f, 0.12f, 0.16f, 0.85f);
        [Tooltip("버튼 글자색.")]
        public Color deployUndoTextColor = new Color(1f, 0.86f, 0.5f, 1f);

        [Header("⑮ armed 보드 드래그 실루엣 (defender-footprint unit 7)")]
        [Tooltip("false = 실루엣 자체를 끈다(드래그 스카우트는 range+고스트만 — unit 7 이전 동작).")]
        public bool armedSilhouetteEnabled = true;
        [Tooltip("실루엣 알파. 유효성 전달은 Ghost 4색 전담이라 실루엣은 이 값 고정.")]
        [Range(0f, 1f)]
        public float armedSilhouetteAlpha = 0.55f;
        [Tooltip("footprint 뷰 중심 추종 속도(지수 lerp 계수). 0 = 셀 스냅 즉시 점프.")]
        [Range(0f, 40f)]
        public float armedSilhouetteFollowSpeed = 14f;
    }
}
