using UnityEngine;

namespace Wassup.Data
{
    // camera-direction unit 1 — 페이즈별 카메라 포즈 델타 (홈 포즈 기준).
    // 등록된 페이즈로의 전환만 카메라를 움직인다. 미등록 페이즈 = hold (spec README 계약).
    [System.Serializable]
    public class CameraPhasePose
    {
        public Wassup.Core.GamePhase phase;
        [Tooltip("홈 회전 기준 카메라 로컬 위치 오프셋(월드 유닛). -z = 보드에서 멀어짐.")]
        public Vector3 localPosOffset;
        [Tooltip("pitch 오프셋(도). + = 더 내려다봄.")]
        public float pitchOffset;
        [Tooltip("FOV 오프셋(도). - = 줌인.")]
        public float fovOffset;
        [Tooltip("이 포즈로의 비행 시간(초). 0 이하 = 즉시 스냅.")]
        public float flightSec = 0.6f;
        [Tooltip("비행 이징 커브(0~1→0~1). 비어 있으면 smoothstep 폴백.")]
        public AnimationCurve ease = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
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

        [Header("페이즈 전환 비행 (unit 1)")]
        [Tooltip("페이즈별 포즈 델타. 미등록 페이즈 진입은 현재 델타 유지(hold).")]
        public CameraPhasePose[] phasePoses = System.Array.Empty<CameraPhasePose>();

        [Header("배틀 구두점 (unit 2) — additive 전용, 카메라 탈취 없음")]
        [Tooltip("헤비 임팩트(광역 착탄) 줌 펄스 FOV 델타(도). 음수 = 줌인.")]
        public float pulseFovDelta = -2.5f;
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

        [Header("인스펙트 포커스 (unit-dreamcatcher-inspect unit 4) — 유닛 탭 시 들여다보기 줌")]
        // 드래그 포커스(dolly 1 / fov -1)보다 강하다: 그건 스와이프 중 미묘한 리드고,
        // 이건 의도적으로 멈춰서 들여다보는 연출이다. enableNonDragEffects 에 묶이지 않는다.
        // 줌의 실질은 dolly 가 담당한다. FOV 여유가 거의 없기 때문 — 현 에셋은 fovMin 41,
        // 홈 FOV ≈43 이라 Compose 의 클램프가 남기는 여유가 **2도뿐**이다(실측 2026-07-15).
        // fovDelta 에 -6 같은 큰 값을 적어도 조용히 -2 로 깎이므로, 실제 효과와 같은 수를 적는다.
        [Tooltip("선택 유닛 방향 전진 거리(월드 유닛). 줌의 주 레버. 0 + fovDelta 0 + lookWeight 0 = 인스펙트 줌 끔.")]
        public float inspectDolly = 4f;
        [Tooltip("인스펙트 중 FOV 델타(도). 음수 = 줌인. 최종 FOV 는 fovMin/fovMax 로 클램프 — 현 에셋(fovMin 41, 홈 43)에선 -2 가 실효 상한이다.")]
        public float inspectFovDelta = -2f;
        [Tooltip("선택 유닛 방향 lookat 블렌드. FocusDelta 가 0~0.5 로 클램프(풀 lookat 은 보드 좌표감 파괴). 인스펙트는 고정 월드 타겟이라 되먹임 없음 — 상한은 취향. 0.5 = 허용 최대(유닛이 중앙에 가장 가까움).")]
        [Range(0f, 0.5f)] public float inspectLookWeight = 0.5f;
        [Tooltip("인스펙트 진입 페이드(초).")]
        public float inspectFadeInSec = 0.22f;
        [Tooltip("인스펙트 해제 페이드(초).")]
        public float inspectFadeOutSec = 0.3f;

        [Header("앰비언트 브리딩 (unit 3) — 인지 임계 이하 상시 생명감")]
        [Tooltip("브리딩 위치 진폭(월드 유닛). 0 = 끔. 호버 셀 플립이 상한(spec).")]
        public float breathPosAmp = 0.03f;
        [Tooltip("브리딩 pitch 진폭(도).")]
        public float breathRotAmp = 0.06f;
        [Tooltip("합성 파동(주기+시작위상+축 가중). 비우면 브리딩 끔. 주기를 비정수비로 두면 반복이 덜 보인다.")]
        public CameraBreathWave[] breathWaves = System.Array.Empty<CameraBreathWave>();
        [Tooltip("브리딩이 켜지는 페이즈 (기본 Draft/Placement/Battle — Gift/Result 는 자체 연출과 간섭 방지).")]
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
