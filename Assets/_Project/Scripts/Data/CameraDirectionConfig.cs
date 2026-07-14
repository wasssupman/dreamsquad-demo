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
    }
}
