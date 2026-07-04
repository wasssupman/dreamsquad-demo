using UnityEngine;

namespace Wassup.Data
{
    // placement-drag-preview-polish — 드래그 프리뷰 sway(매달린 키링) 튜닝값.
    // DefenderDragPlacementController 가 런타임 AddComponent 라 인스펙터 튜닝이 안 되므로
    // 수치를 SO 로 분리(프로젝트 원칙: 수치=ScriptableObject). DefenderSelector 에 할당 →
    // Configure 로 컨트롤러에 주입. 미할당이면 컨트롤러가 이 클래스 기본값으로 인스턴스를 만들어 사용.
    // 에셋을 편집하면 다음 Play(및 런타임)에서 그대로 반영된다.
    [CreateAssetMenu(menuName = "Wassup/Drag Sway Settings", fileName = "DragSwaySettings")]
    public class DragSwaySettings : ScriptableObject
    {
        [Tooltip("고리(pivot)→몸 길이. 몸이 이만큼 위 고리 아래 매달린다(스윙 반경).")]
        public float hangHeight = 1.5f;
        [Tooltip("최대 lean 각(deg). 오버슈트는 이 값의 1.4배까지 허용.")]
        public float maxAngle = 16f;
        [Tooltip("포인터 속도(px/s) → 목표 lean 각(deg). 진행 반대로 눕는 양. ↑=진폭 큼.")]
        public float leanPerVel = 0.035f;
        [Tooltip("목표각 추종 강성(↑=빠릿/짧은 주기). ω=sqrt(spring). 120≈1.74Hz.")]
        public float spring = 120f;
        [Tooltip("감쇠(↓=바운스 큼/오래 흔들림). ζ = damping/(2·sqrt(spring)). 4.5≈ζ0.21.")]
        public float damping = 4.5f;
        [Tooltip("포인터 속도 스무딩 반응(1/s). ↑=즉각(빠릿).")]
        public float pointerResponse = 26f;
        [Tooltip("입력 없을 때 포인터 속도 0 감쇠(1/s). ↑=정지 시 큰 스윙백.")]
        public float pointerDecay = 16f;
    }
}
