using UnityEngine;

namespace Wassup.Data
{
    // camera-direction unit 0 — 연출 카메라 튜닝값 (하드코딩 금지 계약).
    // 채널별 섹션은 후속 유닛에서 누적된다 (unit 1 페이즈 비행, unit 2 구두점, unit 3 브리딩).
    [CreateAssetMenu(menuName = "Wassup/Camera Direction Config", fileName = "CameraDirectionConfig")]
    public class CameraDirectionConfig : ScriptableObject
    {
        [Header("임팩트 킥 (구 CameraImpactKick 이식 — card-fly-to-target-absorb)")]
        [Tooltip("킥 위치 진폭(월드 유닛, 카메라 로컬 축).")]
        public float kickPosAmp = 0.08f;
        [Tooltip("킥 회전 진폭(도, pitch/roll 소량).")]
        public float kickRotAmp = 0.35f;
        [Tooltip("킥 총 시간(초).")]
        public float kickDuration = 0.16f;
    }
}
