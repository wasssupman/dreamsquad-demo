using UnityEngine;

namespace Wassup.Data
{
    // enemy-walk-anim-speed unit 0 — 걷기 애니 재생속도를 이동속도에 맞추는 변조 파라미터.
    // SpineUnitView 가 프레임당 실제 view 변위로 고유 속도를 추정 → walkFactor 로 timeScale 변조.
    // 하드코딩 금지 계약상 모든 튜닝 값은 여기서 나온다. SO 미할당 시 뷰는 배율 1.0(현행 동작) 유지.
    [CreateAssetMenu(fileName = "WalkAnimSpeedStyle", menuName = "Wassup/Presentation/Walk Anim Speed Style", order = 30)]
    public class WalkAnimSpeedStyle : ScriptableObject
    {
        [Tooltip("walkFactor 1.0 이 되는 기준 이동속도(view units/sec, sim-time 기준).")]
        public float referenceSpeed = 2.5f;

        [Tooltip("애니 timeScale 배율 하한. 0=정지 시 완전 프리즈, >0=미세 idle.")]
        public float minTimeScale = 0.15f;

        [Tooltip("애니 timeScale 배율 상한(빠른 적 과속 방지).")]
        public float maxTimeScale = 2f;

        [Range(0f, 1f)]
        [Tooltip("측정 속도 지수 스무딩(0=고정, 1=즉시). 프레임 노이즈 억제.")]
        public float smoothing = 0.2f;

        [Tooltip("한 프레임 view 변위가 이 값을 넘으면 텔레포트로 보고 측정 스킵.")]
        public float teleportGuard = 1.5f;
    }
}
