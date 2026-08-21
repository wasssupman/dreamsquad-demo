using UnityEngine;

namespace Wassup.Data
{
    // camera-direction unit 10 — 카메라 상태. 페이즈 enum(7종)과 별개다: 상태는 훨씬 적고,
    // "어느 페이즈에 어떤 그림을 보여줄까"는 연출 정책이지 게임 규칙이 아니다.
    // 유닛 상세는 상태가 아니다 — 선택 줌은 2026-08-19 에 끈 기능이고(DcInspectController)
    // 되살리지 않기로 2026-08-21 재확인했다. 인스펙트 채널은 상태 포즈 위에 얹힌 채로 남는다.
    public enum CameraState
    {
        Placement,
        Battle,
    }

    // camera-direction unit 10 — 한 상태가 소유하는 완결된 프레이밍 레시피.
    //
    // 델타가 아니다. 상태끼리 공유하는 기준점이 없어서, 전투를 아무리 만져도 배치는 미동도
    // 하지 않는다. 이전 구조(홈 포즈 + 페이즈 델타)는 기준을 건드리면 전 페이즈가 딸려 왔다.
    [System.Serializable]
    public class CameraStateFraming
    {
        public CameraState state;
        [Tooltip("대상 위 몇 도에서 내려다보는가.")]
        public float pitchDeg = 47f;
        [Tooltip("켜면 판 전체가 들어오는 거리를 맵마다 계산한다. 끄면 fixedDistance 를 쓴다.")]
        public bool fitToBoard = true;
        [Tooltip("fit 여백 배율. 1 = 보드 코너가 화면 가장자리에 딱 닿음.")]
        public float fitMargin = 1f;
        [Tooltip("fit 을 안 쓸 때 대상까지의 거리.")]
        public float fixedDistance = 20f;
        [Tooltip("대상이 놓일 화면 세로 위치. 0.5 = 정중앙, 클수록 위(하단 HUD 피하기).")]
        public float screenY = 0.5f;
        [Tooltip("이 상태의 화각.")]
        public float fov = 36f;
        [Tooltip("이 상태로 들어올 때의 전환 시간(초). 0 이하 = 즉시 스냅.")]
        public float flightSec = 0.6f;
        [Tooltip("전환 이징 커브(0~1→0~1). 비어 있으면 smoothstep 폴백.")]
        public AnimationCurve ease = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

        // camera-direction unit 13 — 이 상태의 흐림. 임계값은 보드 깊이 범위로 정규화한다
        // (0 = 보드 앞단, 1 = 뒷단, 1 초과 = 보드 뒤). 월드 절대 거리로 두면 화면비마다
        // 그림이 무너진다(unit 9 의 결함).
        [Tooltip("이 상태에서 흐림을 쓰는가. 끄면 전환이 끝난 뒤 DoF 모드를 Off 로 내린다.")]
        public bool dofEnabled = true;
        [Tooltip("흐림이 시작되는 위치. 보드 깊이 기준(0 = 앞단, 1 = 뒷단, 1 초과 = 보드 뒤).")]
        public float dofStart = 0.6f;
        [Tooltip("흐림이 최대가 되는 위치(같은 보드 깊이 기준). dofStart 보다 커야 한다.")]
        public float dofEnd = 0.88f;
        [Tooltip("흐림 세기. URP 저작 범위는 0.5~1.5 이고 실제 반경은 해상도에 비례한다 — 실기 확인 필수.")]
        public float dofMaxRadius = 1.5f;
    }

    // camera-direction unit 0 — 연출 카메라 튜닝값 (하드코딩 금지 계약).
    // 채널별 섹션은 후속 유닛에서 누적된다 (unit 2 구두점, unit 3 브리딩).
    [CreateAssetMenu(menuName = "Wassup/Camera Direction Config", fileName = "CameraDirectionConfig")]
    public class CameraDirectionConfig : ScriptableObject
    {
        [Header("연출 채널 전역 제어")]
        [Tooltip("끄면 드래그 포커스를 제외한 페이즈 비행·구두점·브리딩·임팩트 킥을 모두 비활성화한다.")]
        public bool enableNonDragEffects;

        [Header("임팩트 킥 (구 CameraImpactKick 이식 — card-fly-to-target-absorb)")]
        [Tooltip("킥 위치 진폭(월드 유닛, 카메라 로컬 축).")]
        public float kickPosAmp = 0.08f;
        [Tooltip("킥 회전 진폭(도, pitch/roll 소량).")]
        public float kickRotAmp = 0.35f;
        [Tooltip("킥 총 시간(초). 0 = 킥 끔.")]
        public float kickDuration = 0.16f;

        [Header("카메라 상태 (unit 10~11)")]
        [Tooltip("상태별 프레이밍 레시피. 등록되지 않은 상태로는 전환하지 않는다(현재 포즈 유지).")]
        public CameraStateFraming[] stateFramings = System.Array.Empty<CameraStateFraming>();

        [Header("배틀 구두점 (unit 2) — additive 전용, 카메라 탈취 없음")]
        [Tooltip("헤비 임팩트(광역 착탄) 줌 펄스 FOV 델타(도). 음수 = 줌인. [은퇴 — 줌은 pulseDolly 가 담당] 0 권장.")]
        public float pulseFovDelta = -2.5f;
        // camera-fov-to-dolly — 줌 펄스를 FOV 가 아니라 전진(transform)으로 낸다.
        // FOV 줌은 원근이 함께 변해 기울어진 보드에서 왜곡이 도드라진다. dolly 는 원근을
        // 유지한 채 크기만 바꾼다. 부호 규약은 focusDolly/inspectDolly 와 동일 — 양수 = 전진.
        // 환산: 화면에 담기는 세계 크기가 같으려면 d' = d · tan((v+Δ)/2)/tan(v/2).
        // 현 씬 포즈(보드까지 약 15.5, 홈 수직 FOV 36)에서 FOV -2.5° ≈ 전진 1.15.
        // 홈 포즈나 FOV 를 크게 바꾸면 이 값도 다시 환산해야 한다.
        [Tooltip("헤비 임팩트 줌 펄스 dolly(월드 유닛). 양수 = 전진 = 줌인. 0 = 펄스 줌 끔.")]
        public float pulseDolly = 1.15f;
        [Tooltip("줌 펄스 시간(초). 0 = 펄스 끔.")]
        public float pulseSec = 0.22f;
        [Tooltip("킬 스트릭 셰이크 최대 위치 진폭(월드 유닛, heat=1 기준).")]
        public float shakeMaxPosAmp = 0.04f;
        [Tooltip("킬 스트릭 셰이크 최대 roll 진폭(도, heat=1 기준).")]
        public float shakeMaxRotAmp = 0.12f;
        [Tooltip("셰이크 가로 주파수(Hz).")]
        public float shakeFreqX = 11f;
        [Tooltip("셰이크 세로 주파수(Hz). X 와 비정수비로 두면 패턴 반복이 덜 보인다.")]
        public float shakeFreqY = 8.3f;
        [Tooltip("페이즈 비행 중 구두점 가중치 0 페이드 시간(초).")]
        public float punctuationFadeSec = 0.15f;

        [Header("최종 FOV 클램프 (spec README 계약 — SO 튜닝만으로 위험 FOV 차단)")]
        public float fovMin = 30f;
        public float fovMax = 60f;

        [Header("드래그 포커스 (unit 5) — 스와이프 중 유닛 줌인 + 방향 lookat 리드")]
        [Tooltip("유닛 방향 전진 거리(월드 유닛). 0 = 포커스 끔.")]
        public float focusDolly = 2f;
        [Tooltip("포커스 중 FOV 델타(도). 음수 = 줌인.")]
        public float focusFovDelta = -2f;
        [Tooltip("유닛 방향 lookat 블렌드. 포인터→카메라 피드백 루프의 수축 계수라 0.5 초과 금지 — 1.0 이면 발산(무한 회전). 정지 포인터의 최종 각 변위 = 오프셋 × w/(1-w).")]
        [Range(0f, 0.5f)] public float focusLookWeight = 0.25f;
        [Tooltip("스와이프 NDC 속도(화면폭 2 기준/s) → 시선 리드 각(도) 변환 계수.")]
        public float focusLeanPerSpeed = 1.2f;
        [Tooltip("시선 리드 최대각(도).")]
        public float focusLeanMaxDeg = 2.5f;
        [Tooltip("포인터 추종 스프링 강성(↑=빠릿). KeyringSim.SpringStep.")]
        public float focusSpring = 60f;
        [Tooltip("포인터 추종 감쇠(임계≈2√spring — 그 이상이면 출렁임 없음).")]
        public float focusDamping = 14f;
        [Tooltip("포커스 진입 페이드(초).")]
        public float focusFadeInSec = 0.25f;
        [Tooltip("포커스 해제 페이드(초).")]
        public float focusFadeOutSec = 0.35f;
        // camera-direction unit 12 — 배치 상태는 같은 채널을 «화면 밀기» 로 해석한다.
        // 스프링·감쇠·페이드는 위 값을 그대로 쓴다(새 채널을 만들지 않는 이유).
        [Tooltip("배치 상태에서 커서 쪽으로 화면을 미는 양(0~1). 1 = 커서 지점이 화면 중앙까지 온다.")]
        public float placementFocusLead = 0.35f;

        [Header("인스펙트 포커스 (unit-dreamcatcher-inspect unit 4) — 유닛 탭 시 들여다보기 줌")]
        // 드래그 포커스(dolly 1 / fov -1)보다 강하다: 그건 스와이프 중 미묘한 리드고,
        // 이건 의도적으로 멈춰서 들여다보는 연출이다. enableNonDragEffects 에 묶이지 않는다.
        // dolly 와 FOV 를 **함께** 쓴다(selection-hand-attach unit 13). dolly 만이면 "가까이
        // 갔다"이고, FOV 를 좁히면 원근이 압축돼 배경이 납작해지며 "주목했다"가 된다.
        //
        // 과거 주석은 "FOV 여유 2도뿐(fovMin 41 / 홈 43)"이라 dolly 단독을 권했는데, 그건
        // fovMin 이 41 이던 시절 기준이다. 실측 2026-07-30: **fovMin 31 / 홈 FOV 60 → 여유 ≈29도**.
        // 클램프에 조용히 깎이지 않으니 FOV 를 실제 레버로 쓸 수 있다.
        [Tooltip("선택 유닛 방향 전진 거리(월드 유닛). 0 + fovDelta 0 + lookWeight 0 = 인스펙트 줌 끔.")]
        public float inspectDolly = 3f;
        [Tooltip("인스펙트 중 FOV 델타(도). 음수 = 줌인(원근 압축). 최종 FOV 는 fovMin/fovMax 로 클램프.")]
        public float inspectFovDelta = -6f;
        [Tooltip("선택 전환 시 프레이밍이 새 유닛으로 미끄러지는 속도(1/초). 클수록 빠르다. 0 이하 = 즉시 스냅(구 동작).")]
        public float inspectFollowRate = 12f;
        // selection-hand-attach unit 13 rev2 — **기본 0(끔)**. rev1 에서 -5 로 켰다가 되돌렸다.
        //
        // 이유: 음수 pitch 는 "올려다보는" 각도인 동시에 **보드를 화면 아래로 내린다**
        // (handHeadroomPitchDeg 와 같은 부호 규약 — 1도당 약 25px). 선택 중에는 손패가 **항상**
        // 열려 있어(계약 1) 헤드룸 -2 가 이미 걸린 상태라, 여기에 -5 를 더하면 총 ~175px 하강이다.
        // 하단에 배치된 유닛이 손패 카드 밑으로 완전히 깔려 보이지 않는다(실측 2026-07-30).
        //
        // 즉 **극적인 틸트와 "선택 중 손패 상시 개방"은 구조적으로 양립하지 않는다.**
        // 유닛을 부각하되 가려지지 않게 하려면 각도가 아니라 **프레이밍**을 올려야 한다 →
        // inspectFrameBiasY 가 그 일을 한다. 이 노브는 실험용으로 남긴다.
        [Tooltip("인스펙트 중 pitch 델타(도). 음수 = 보드를 화면 아래로(하단 유닛이 손패에 가린다). 기본 0 권장.")]
        public float inspectPitchDeg = 0f;
        // 선택 유닛을 화면에서 **위로** 올리는 프레이밍 바이어스(NDC). 카메라가 유닛보다 살짝
        // 아래를 겨냥하게 만들어, 결과적으로 유닛이 프레임 위쪽에 놓인다. 손패가 덮는 하단
        // 대역에서 유닛을 꺼내는 것이 목적이라 pitch 와 달리 보드 전체를 기울이지 않는다.
        // NDC 1.0 = 프러스텀 절반 높이. lookWeight(≤0.5) 블렌드를 지나므로 실효는 그 비율만큼이다.
        [Tooltip("선택 유닛을 프레임 위쪽으로 올리는 양(NDC). 하단 배치 유닛이 손패에 가리지 않게 한다. 0 = 끔.")]
        [Range(0f, 1f)] public float inspectFrameBiasY = 0.35f;
        [Tooltip("선택 유닛 방향 lookat 블렌드. FocusDelta 가 0~0.5 로 클램프(풀 lookat 은 보드 좌표감 파괴). 인스펙트는 고정 월드 타겟이라 되먹임 없음 — 상한은 취향. 0.5 = 허용 최대(유닛이 중앙에 가장 가까움).")]
        [Range(0f, 0.5f)] public float inspectLookWeight = 0.5f;
        [Tooltip("인스펙트 진입 페이드(초).")]
        public float inspectFadeInSec = 0.22f;
        [Tooltip("인스펙트 해제 페이드(초).")]
        public float inspectFadeOutSec = 0.3f;

        [Header("손패 헤드룸 (hand-drag-tooltip unit 6) — 손패 열림 중 상단 UI 여백 확보")]
        // 손패가 열리면 상단 중앙에 카드 성능 툴팁이 떠서 보드 상단을 가린다. pitch 를
        // 낮추면 보드가 화면 아래로 통째로 내려가(크기는 거의 불변) 상단이 비는데,
        // 그 대가로 근경이 내려간다 — 그런데 손패가 열린 동안은 화면 하단을 카드가
        // 이미 덮고 있어 실질 손해가 없다. 그래서 **상시가 아니라 손패 연동**이다.
        // enableNonDragEffects 에 묶지 않는다(인스펙트와 같은 이유 — 명시적 제품 기능).
        [Tooltip("손패 열림 중 pitch 델타(도). 음수 = 카메라를 눕혀 보드를 화면 아래로. 현 씬 포즈에서 1도당 약 25px.")]
        // 홈 60° 기준 −2 = 손패 열림 58°(사용자 확정 2026-07-21). 카메라 이동이
        // 눈에 띄는 것보다 약간의 보드 가림을 감수하는 쪽을 택했다.
        public float handHeadroomPitchDeg = -2f;
        // pitch 는 보드를 아래로 **옮기고**(크기 유지), dolly 는 보드를 **줄인다**.
        // 둘을 같이 쓰면 상단 여백이 합산되고, 줄어든 만큼 하단 손실도 완화된다.
        // 부호 규약은 focusDolly/inspectDolly 와 동일 — 양수 = 전진. 후퇴는 음수.
        [Tooltip("손패 열림 중 dolly(월드 유닛). 음수 = 후퇴 = 줌아웃. 현 씬 포즈에서 -1.5 면 보드가 약 8.5% 축소된다. pitch 와 함께 0 이면 헤드룸 끔.")]
        public float handHeadroomDolly = -1.5f;
        // 진입/복귀 모두 스프링(사용자 결정 2026-07-21 — "스무스한 스프링 연출").
        // MoveTowards/ease 는 기계적이라, 손패가 튀어오르는 카드 문법과 안 붙는다.
        // damping 을 spring 의 2√k 아래로 두면 살짝 오버슈트 후 안착(under-damped).
        [Tooltip("헤드룸 가중치 스프링 계수. 클수록 빠르게 도달.")]
        public float handHeadroomSpring = 90f;
        [Tooltip("헤드룸 가중치 감쇠. 2*sqrt(spring) 이 임계감쇠(오버슈트 없음) — 그보다 낮으면 오버슈트.")]
        public float handHeadroomDamping = 14f;

        [Header("이동모드 줌아웃 (defender-relocation) — 목적지 선택 중 고정 오버뷰")]
        [Tooltip("이동모드 중 dolly(월드 유닛). 음수 = 후퇴 = 줌아웃. 목적지 선택 위해 보드를 넓게 보이게. 0 = 끔.")]
        public float moveOverviewDolly = -4.5f;
        [Tooltip("이동모드 중 pitch 델타(도). 음수 = 카메라를 눕힘. 0 = 유지.")]
        public float moveOverviewPitchDeg = 0f;
        [Tooltip("이동모드 줌아웃 가중치 스프링 계수. 클수록 빠르게 도달.")]
        public float moveOverviewSpring = 90f;
        [Tooltip("이동모드 줌아웃 가중치 감쇠.")]
        public float moveOverviewDamping = 16f;

        [Header("앰비언트 브리딩 (unit 3) — 인지 임계 이하 상시 생명감")]
        [Tooltip("브리딩 위치 진폭(월드 유닛). 0 = 끔. 호버 셀 플립이 상한(spec).")]
        public float breathPosAmp = 0.03f;
        [Tooltip("브리딩 pitch 진폭(도).")]
        public float breathRotAmp = 0.06f;
        [Tooltip("합성 파동(주기+시작위상+축 가중). 비우면 브리딩 끔. 주기를 비정수비로 두면 반복이 덜 보인다.")]
        public CameraBreathWave[] breathWaves = System.Array.Empty<CameraBreathWave>();
        // gift-phase-removal unit 1 — ⚠ 이 배열은 **enum 값이 직렬화**된다(에셋에 정수로 박힌다).
        // GamePhase 에서 값을 빼거나 순서를 바꾸면 저장된 정수의 의미가 밀리므로,
        // 반드시 CameraDirectionConfig.asset 의 breathPhases 도 같은 커밋에서 마이그레이션한다.
        [Tooltip("브리딩이 켜지는 페이즈 (기본 Draft/Placement/Battle — Result 는 자체 연출과 간섭 방지).")]
        public Wassup.Core.GamePhase[] breathPhases =
        {
            Wassup.Core.GamePhase.Draft,
            Wassup.Core.GamePhase.Placement,
            Wassup.Core.GamePhase.Battle,
        };
        [Tooltip("브리딩 가중치 크로스페이드 시간(초) — 비행 중 0, 종료 후 서서히 복귀. 급격한 on/off 금지(spec).")]
        public float breathFadeSec = 1.5f;
    }

    // camera-direction unit 3 — 브리딩 파동 1개. 위상은 SO 소유("모든 수치는 SO" 계약).
    [System.Serializable]
    public class CameraBreathWave
    {
        [Tooltip("주기(초).")]
        public float periodSec = 8f;
        [Tooltip("시작 위상(0~1).")]
        public float phase01;
        [Tooltip("카메라 로컬 X/Y 위치 축 가중치(-1~1 권장).")]
        public Vector2 posWeight = new Vector2(1f, 1f);
        [Tooltip("pitch 축 가중치(-1~1 권장).")]
        public float pitchWeight;
    }
}
